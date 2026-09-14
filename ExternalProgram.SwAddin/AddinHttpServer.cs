using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.WebSockets;
using System.Text;
using System.Runtime.InteropServices;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using SldWorks;

namespace ExternalProgram.SwAddin;

/// <summary>
/// HTTP+WebSocket 服务端，运行在 SW 进程中。
/// 监听 127.0.0.1 回环地址，接收主程序（ExternalProgram.exe）的 JSON 命令，
/// 通过 WinForms Control.BeginInvoke 将所有 COM 调用封送到 SW 主线程（STA）执行。
/// </summary>
internal sealed partial class AddinHttpServer : IDisposable
{
    // 命令执行超时（SW 主线程忙时自动失败，避免永久挂起）
    private const int DefaultCommandTimeoutSeconds = 30;
    // 健康检查超时（需要更短，因为客户端频繁扫描端口）
    private const int HealthCheckTimeoutSeconds = 5;

    // 高频轮询命令：主程序状态栏定时拉取，日志静默以免刷屏。
    private static readonly HashSet<string> QuietCommands = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "active-document"
    };

    private readonly SldWorks.SldWorks _swApp;
    // 用于将线程池回调封送到 SW 主线程的 WinForms 控件
    private readonly System.Windows.Forms.Control _mainThreadControl;
    private readonly string _prefix;
    private readonly JavaScriptSerializer _json = new JavaScriptSerializer();
    private HttpListener _listener;
    private CancellationTokenSource _cts;
    // 命令互斥锁：同一时间只允许一个命令在 SW 主线程执行
    private int _commandInProgress;
    // 命令代际计数器：超时后新命令递增，旧回调检测到代际不匹配则丢弃
    private long _commandGeneration;
    // 所有已连接的 WebSocket 客户端，用于广播事件
    private readonly ConcurrentBag<WebSocket> _webSockets = new ConcurrentBag<WebSocket>();

    // 静态网页资源映射：请求路径 → (嵌入资源名, Content-Type)
    private static readonly Dictionary<string, (string Resource, string ContentType)> StaticFiles =
        new Dictionary<string, (string, string)>(StringComparer.OrdinalIgnoreCase)
        {
            { "/", ("ExternalProgram.SwAddin.wwwroot.index.html", "text/html; charset=utf-8") },
            { "/index.html", ("ExternalProgram.SwAddin.wwwroot.index.html", "text/html; charset=utf-8") },
            { "/app.js", ("ExternalProgram.SwAddin.wwwroot.app.js", "application/javascript; charset=utf-8") },
            { "/style.css", ("ExternalProgram.SwAddin.wwwroot.style.css", "text/css; charset=utf-8") },
        };

    public AddinHttpServer(SldWorks.SldWorks swApp, System.Windows.Forms.Control mainThreadControl, string prefix)
    {
        _swApp = swApp;
        _mainThreadControl = mainThreadControl;
        _prefix = prefix.EndsWith("/") ? prefix : prefix + "/";
        _json.MaxJsonLength = int.MaxValue;
    }

    public void Start()
    {
        if (_listener != null) return;
        _cts = new CancellationTokenSource();
        _listener = new HttpListener();
        _listener.Prefixes.Add(_prefix);
        AddinLog.Write("Starting HTTP listener: " + _prefix);
        _listener.Start();
        _ = Task.Run(() => ListenLoop(_cts.Token));
    }

    public void Dispose()
    {
        // Close all WebSocket connections
        foreach (var ws in _webSockets)
        {
            try { ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "Shutdown", CancellationToken.None).Wait(1000); }
            catch (Exception ex) { AddinLog.Write("WebSocket close during dispose ignored: " + ex.Message); }
            try { ws.Dispose(); }
            catch (Exception ex) { AddinLog.Write("WebSocket dispose ignored: " + ex.Message); }
        }

        try { _cts?.Cancel(); }
        catch (Exception ex) { AddinLog.Write("Cancellation during dispose ignored: " + ex.Message); }
        try { _listener?.Stop(); }
        catch (Exception ex) { AddinLog.Write("HTTP listener stop during dispose ignored: " + ex.Message); }
        try { _listener?.Close(); }
        catch (Exception ex) { AddinLog.Write("HTTP listener close during dispose ignored: " + ex.Message); }
        _listener = null;
        _cts?.Dispose();
        _cts = null;
    }

    private async Task ListenLoop(CancellationToken token)
    {
        while (!token.IsCancellationRequested && _listener != null)
        {
            try
            {
                var context = await _listener.GetContextAsync().ConfigureAwait(false);
                _ = HandleRequest(context);
            }
            catch
            {
                if (token.IsCancellationRequested) return;
            }
        }
    }

    /// <summary>
    /// 将工作封送到 SW 主线程执行。SW COM API 要求 STA 单线程访问。
    /// 通过 WinForms Control.BeginInvoke 将回调投递到 SW 主线程消息队列。
    /// 超时后抛出 CommandFailure，并递增代际计数器淘汰过期回调。
    /// </summary>
    private async Task<object> RunOnMainThread(Func<object> work, int timeoutSeconds = DefaultCommandTimeoutSeconds)
    {
        if (_mainThreadControl is null || !_mainThreadControl.InvokeRequired)
            return work();

        var gen = Interlocked.Read(ref _commandGeneration);
        var tcs = new TaskCompletionSource<object>();
        _mainThreadControl.BeginInvoke((Action)(() =>
        {
            // Discard stale callbacks from timed-out commands
            if (Interlocked.Read(ref _commandGeneration) != gen)
            {
                AddinLog.Write("RunOnMainThread discarding stale callback (generation mismatch)");
                return;
            }
            try { tcs.TrySetResult(work()); }
            catch (Exception ex) { tcs.TrySetException(ex); }
        }));

        using var cts = new CancellationTokenSource();
        var delayTask = Task.Delay(TimeSpan.FromSeconds(timeoutSeconds), cts.Token);
        var completed = await Task.WhenAny(tcs.Task, delayTask).ConfigureAwait(false);

        if (completed == tcs.Task)
        {
            cts.Cancel(); // Cancel the delay
            return await tcs.Task.ConfigureAwait(false);
        }

        // Timeout — command generation already advanced by HandleRequest's finally,
        // so the stale BeginInvoke callback will be discarded when it eventually fires.
        AddinLog.Write($"RunOnMainThread timed out after {timeoutSeconds}s");
        throw CommandFailure("main_thread_timeout");
    }

    /// <summary>
    /// 检测 COM RCW 代理是否仍然有效（底层 COM 对象未被释放）。
    /// 用于防止对已关闭文档的代理调用导致 RPC_E_DISCONNECTED 崩溃。
    /// </summary>
    internal static bool IsComAlive(object comObject)
    {
        if (comObject == null) return false;
        try
        {
            var unk = Marshal.GetIUnknownForObject(comObject);
            Marshal.AddRef(unk);
            Marshal.Release(unk);
            Marshal.Release(unk);
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// HTTP 请求分发入口。路由：
    ///   GET  /health          → 健康检查（客户端端口扫描用）
    ///   GET  /events (WS)     → WebSocket 事件推送（doc-changed / selection-changed）
    ///   POST /cancel          → 取消当前批量操作
    ///   POST /command         → 命令执行（JSON body: {Command, Args}）
    /// </summary>
    private async Task HandleRequest(HttpListenerContext context)
    {
        var watch = Stopwatch.StartNew();
        var requestPath = context.Request.Url?.AbsolutePath ?? string.Empty;
        try
        {
            var isHealthCheck = context.Request.HttpMethod == "GET" && requestPath == "/health";
            var isWebSocket = context.Request.IsWebSocketRequest && requestPath == "/events";

            // 只记录非健康检查、非 WebSocket、非命令的请求，减少日志噪音。
            // 命令请求在读到命令名后再按需记录（高频轮询命令静默）。
            if (!isHealthCheck && !isWebSocket && requestPath != "/command")
                AddinLog.Write($"HTTP {context.Request.HttpMethod} {requestPath} from {context.Request.RemoteEndPoint}");

            // WebSocket upgrade for /events
            if (isWebSocket)
            {
                await HandleWebSocket(context);
                return;
            }

            // Health check
            if (context.Request.HttpMethod == "GET" && requestPath == "/health")
            {
                HealthDocumentInfo document;
                try
                {
                    document = (HealthDocumentInfo)await RunOnMainThread(GetHealthDocumentInfo, HealthCheckTimeoutSeconds);
                }
                catch (CommandFailureException ex) when (ex.Code == "main_thread_timeout")
                {
                    // SW main thread is busy (e.g. processing a long command).
                    // The HTTP server is alive — report ok with empty document info
                    // so the client does not mistake this for a disconnection.
                    AddinLog.Write("HTTP health: SW main thread busy, reporting ok with empty doc");
                    document = new HealthDocumentInfo(string.Empty, string.Empty, string.Empty);
                }
                WriteJson(context, new
                {
                    ok = true,
                    addin = "ExternalProgram.SwAddin",
                    prefix = _prefix,
                    processId = Process.GetCurrentProcess().Id,
                    title = document.Title,
                    path = document.Path,
                    shortTitle = document.ShortTitle,
                    wsClients = _webSockets.Count
                });
                return;
            }

            // Static web UI
            if (context.Request.HttpMethod == "GET" && StaticFiles.TryGetValue(requestPath, out var file))
            {
                ServeStaticFile(context, file.Resource, file.ContentType);
                return;
            }

            // 非 GET 请求校验 Origin，拦截跨站 CSRF
            if (context.Request.HttpMethod != "GET" && !IsSameOrigin(context.Request))
            {
                AddinLog.Write($"HTTP forbidden origin: {context.Request.Headers["Origin"]}");
                context.Response.StatusCode = 403;
                WriteJson(context, new { ok = false, errorCode = "forbidden" });
                return;
            }

            // Command endpoint
            if (context.Request.HttpMethod != "POST" || requestPath != "/command")
            {
                context.Response.StatusCode = 404;
                WriteJson(context, new { ok = false, error = "not_found" });
                AddinLog.Write($"HTTP not_found {requestPath} in {watch.ElapsedMilliseconds}ms");
                return;
            }

            var request = ReadCommand(context.Request);
            var commandName = (request.Command ?? string.Empty).Trim();
            // 高频轮询命令（状态栏每 5 秒拉取活动文档等）静默，避免刷屏淹没有用日志。
            var quiet = QuietCommands.Contains(commandName);
            if (!quiet)
                AddinLog.Write($"HTTP POST /command from {context.Request.RemoteEndPoint}");

            if (Interlocked.CompareExchange(ref _commandInProgress, 1, 0) != 0)
            {
                AddinLog.Write($"Command rejected while busy: {commandName}");
                context.Response.StatusCode = 409;
                WriteJson(context, new { ok = false, errorCode = "command_busy" });
                return;
            }

            if (!quiet) AddinLog.Write($"Command start: {commandName}");
            try
            {
                Interlocked.Increment(ref _commandGeneration);
                var executeWatch = Stopwatch.StartNew();
                var result = await RunOnMainThread(() => ExecuteCommand(request));
                if (!quiet)
                {
                    AddinLog.Write($"Command execute done: {commandName} in {executeWatch.ElapsedMilliseconds}ms");
                    WriteJson(context, new { ok = true, data = result });
                    AddinLog.Write($"Command ok: {commandName} in {watch.ElapsedMilliseconds}ms");
                }
                else
                {
                    WriteJson(context, new { ok = true, data = result });
                }
            }
            finally
            {
                Interlocked.Exchange(ref _commandInProgress, 0);
            }
        }
        catch (CommandFailureException ex)
        {
            AddinLog.Write($"Command failed {context.Request.HttpMethod} {requestPath} in {watch.ElapsedMilliseconds}ms: {ex.Code}");
            WriteJson(context, new { ok = false, errorCode = ex.Code, errorArgs = ex.Details });
        }
        catch (Exception ex)
        {
            AddinLog.Write($"HTTP failed {context.Request.HttpMethod} {requestPath} in {watch.ElapsedMilliseconds}ms: {ex}");
            context.Response.StatusCode = 500;
            WriteJson(context, new { ok = false, errorCode = "internal_error" });
        }
    }

    private CommandRequest ReadCommand(HttpListenerRequest request)
    {
        using var reader = new StreamReader(request.InputStream, request.ContentEncoding ?? Encoding.UTF8);
        var body = reader.ReadToEnd();
        if (string.IsNullOrWhiteSpace(body)) return new CommandRequest();

        var raw = _json.DeserializeObject(body) as Dictionary<string, object>;
        if (raw == null) return new CommandRequest();

        return new CommandRequest
        {
            Command = GetCommandRequestString(raw, "Command"),
            Args = raw.TryGetValue("Args", out var args) && args is Dictionary<string, object> argsDict
                ? argsDict
                : new Dictionary<string, object>()
        };
    }

    private static string GetCommandRequestString(Dictionary<string, object> raw, string key)
    {
        return raw.TryGetValue(key, out var value) && value != null ? value.ToString() : "";
    }

    private HealthDocumentInfo GetHealthDocumentInfo()
    {
        var model = Safe(() => _swApp.ActiveDoc as ModelDoc2);
        if (model == null)
        {
            return new HealthDocumentInfo(string.Empty, string.Empty, "\u65e0\u6587\u6863");
        }

        var title = Safe(() => model.GetTitle()) ?? string.Empty;
        var path = Safe(() => model.GetPathName()) ?? string.Empty;
        return new HealthDocumentInfo(title, path, BuildShortDocumentTitle(title, path));
    }

    private static string BuildShortDocumentTitle(string title, string path)
    {
        var candidate = GetFileTitle(path);
        if (string.IsNullOrWhiteSpace(candidate))
        {
            candidate = GetFileTitle(title);
        }

        return string.IsNullOrWhiteSpace(candidate) ? "\u65e0\u6587\u6863" : candidate.Trim();
    }

    private static string GetFileTitle(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        try
        {
            var fileName = Path.GetFileNameWithoutExtension(value.Trim());
            return string.IsNullOrWhiteSpace(fileName) ? value.Trim() : fileName;
        }
        catch
        {
            return value.Trim();
        }
    }

    private sealed class HealthDocumentInfo
    {
        public HealthDocumentInfo(string title, string path, string shortTitle)
        {
            Title = title ?? string.Empty;
            Path = path ?? string.Empty;
            ShortTitle = string.IsNullOrWhiteSpace(shortTitle) ? "\u65e0\u6587\u6863" : shortTitle.Trim();
        }

        public string Title { get; }
        public string Path { get; }
        public string ShortTitle { get; }
    }

    // --- WebSocket ---

    private async Task HandleWebSocket(HttpListenerContext context)
    {
        try
        {
            var wsCtx = await context.AcceptWebSocketAsync(null);
            var ws = wsCtx.WebSocket;
            _webSockets.Add(ws);
            AddinLog.Write($"WebSocket client connected, total={_webSockets.Count}");

            // Start heartbeat
            var hbCts = new CancellationTokenSource();
            var heartbeatTask = RunHeartbeat(ws, hbCts.Token);

            // Read loop (just to detect disconnect)
            var buffer = new byte[1024];
            while (ws.State == WebSocketState.Open)
            {
                try
                {
                    var result = await ws.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "", CancellationToken.None);
                        break;
                    }
                }
                catch (WebSocketException)
                {
                    break;
                }
            }

            hbCts.Cancel();
            try { await heartbeatTask; }
            catch (Exception ex) { AddinLog.Write("WebSocket heartbeat shutdown ignored: " + ex.Message); }
        }
        catch (Exception ex)
        {
            AddinLog.Write("WebSocket upgrade failed: " + ex.Message);
        }
        finally
        {
            // Rebuild the bag with only open sockets (ConcurrentBag has no Clear in net48)
            var openSockets = new List<WebSocket>();
            while (_webSockets.TryTake(out var sock))
            {
                if (sock.State == WebSocketState.Open)
                    openSockets.Add(sock);
                else
                    try { sock.Dispose(); }
                    catch (Exception ex) { AddinLog.Write("WebSocket dispose during cleanup ignored: " + ex.Message); }
            }
            foreach (var sock in openSockets)
                _webSockets.Add(sock);
        }
    }

    private async Task RunHeartbeat(WebSocket ws, CancellationToken token)
    {
        while (!token.IsCancellationRequested && ws.State == WebSocketState.Open)
        {
            try { await Task.Delay(30000, token); }
            catch (Exception)
            {
                return;
            }

            try
            {
                var ping = Encoding.UTF8.GetBytes("{\"type\":\"ping\"}");
                await ws.SendAsync(new ArraySegment<byte>(ping), WebSocketMessageType.Text, true, token);
            }
            catch (Exception)
            {
                return;
            }
        }
    }

    public void BroadcastEvent(string type, object data)
    {
        var payload = _json.Serialize(new { type, timestamp = DateTime.Now.ToString("o"), data });
        var bytes = Encoding.UTF8.GetBytes(payload);
        var segment = new ArraySegment<byte>(bytes);

        foreach (var ws in _webSockets)
        {
            if (ws.State == WebSocketState.Open)
            {
                try { ws.SendAsync(segment, WebSocketMessageType.Text, true, CancellationToken.None); }
                catch (Exception ex) { AddinLog.Write("WebSocket broadcast ignored: " + ex.Message); }
            }
        }
    }

    private void ServeStaticFile(HttpListenerContext context, string resourceName, string contentType)
    {
        try
        {
            var assembly = Assembly.GetExecutingAssembly();
            using var stream = assembly.GetManifestResourceStream(resourceName);
            if (stream == null)
            {
                context.Response.StatusCode = 404;
                WriteJson(context, new { ok = false, error = "not_found" });
                return;
            }

            using var ms = new MemoryStream();
            stream.CopyTo(ms);
            var bytes = ms.ToArray();
            context.Response.ContentType = contentType;
            context.Response.ContentLength64 = bytes.Length;
            context.Response.OutputStream.Write(bytes, 0, bytes.Length);
            context.Response.OutputStream.Close();
        }
        catch (Exception ex)
        {
            AddinLog.Write("ServeStaticFile failed: " + ex.Message);
            try { WriteJson(context, new { ok = false, error = "internal_error" }); }
            catch { }
        }
    }

    /// <summary>
    /// CSRF 防护：浏览器跨站请求会携带 Origin 头，与自身 origin 不符时拒绝。
    /// 旧 WPF 客户端（HttpClient）不带 Origin 头，放行。
    /// </summary>
    private bool IsSameOrigin(HttpListenerRequest request)
    {
        var origin = request.Headers["Origin"];
        if (string.IsNullOrWhiteSpace(origin)) return true;

        var expected = _prefix.TrimEnd('/');
        return string.Equals(origin.TrimEnd('/'), expected, StringComparison.OrdinalIgnoreCase);
    }

    private void WriteJson(HttpListenerContext context, object obj)
    {
        var json = _json.Serialize(obj);
        var bytes = Encoding.UTF8.GetBytes(json);
        context.Response.ContentType = "application/json; charset=utf-8";
        context.Response.ContentLength64 = bytes.Length;
        context.Response.OutputStream.Write(bytes, 0, bytes.Length);
        context.Response.OutputStream.Close();
    }

    // ExecuteCommand is defined in the partial class AddinHttpServer.Commands.cs
    // (Task 5-7)
}
