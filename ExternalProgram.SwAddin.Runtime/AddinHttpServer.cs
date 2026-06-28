using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using SldWorks;

namespace ExternalProgram.SwAddin;

internal sealed partial class AddinHttpServer : IDisposable
{
    private readonly SldWorks.SldWorks _swApp;
    private readonly System.Windows.Forms.Control _mainThreadControl;
    private readonly string _prefix;
    private readonly JavaScriptSerializer _json = new JavaScriptSerializer();
    private HttpListener _listener;
    private CancellationTokenSource _cts;
    private int _commandInProgress;
    private volatile bool _batchCancelled;
    private readonly ConcurrentBag<WebSocket> _webSockets = new ConcurrentBag<WebSocket>();

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

    private Task<object> RunOnMainThread(Func<object> work)
    {
        if (_mainThreadControl is null || !_mainThreadControl.InvokeRequired)
            return Task.FromResult(work());

        var tcs = new TaskCompletionSource<object>();
        _mainThreadControl.BeginInvoke((Action)(() =>
        {
            try { tcs.TrySetResult(work()); }
            catch (Exception ex) { tcs.TrySetException(ex); }
        }));
        return tcs.Task;
    }

    private async Task HandleRequest(HttpListenerContext context)
    {
        var watch = Stopwatch.StartNew();
        var requestPath = context.Request.Url?.AbsolutePath ?? string.Empty;
        try
        {
            AddinLog.Write($"HTTP {context.Request.HttpMethod} {requestPath} from {context.Request.RemoteEndPoint}");

            // WebSocket upgrade for /events
            if (context.Request.IsWebSocketRequest && requestPath == "/events")
            {
                await HandleWebSocket(context);
                return;
            }

            // Health check
            if (context.Request.HttpMethod == "GET" && requestPath == "/health")
            {
                var document = (HealthDocumentInfo)await RunOnMainThread(GetHealthDocumentInfo);
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
                AddinLog.Write($"HTTP health ok in {watch.ElapsedMilliseconds}ms");
                return;
            }

            // Cancel endpoint — bypasses command lock so the client can interrupt
            // a long-running batch without getting 409.
            if (context.Request.HttpMethod == "POST" && requestPath == "/cancel")
            {
                _batchCancelled = true;
                AddinLog.Write("HTTP cancel requested");
                WriteJson(context, new { ok = true, data = new { cancelled = true } });
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
            if (Interlocked.CompareExchange(ref _commandInProgress, 1, 0) != 0)
            {
                AddinLog.Write($"Command rejected while busy: {commandName}");
                context.Response.StatusCode = 409;
                WriteJson(context, new { ok = false, errorCode = "command_busy" });
                return;
            }

            AddinLog.Write($"Command start: {commandName}");
            try
            {
                var executeWatch = Stopwatch.StartNew();
                var result = await RunOnMainThread(() => ExecuteCommand(request));
                AddinLog.Write($"Command execute done: {commandName} in {executeWatch.ElapsedMilliseconds}ms");
                WriteJson(context, new { ok = true, data = result });
                AddinLog.Write($"Command ok: {commandName} in {watch.ElapsedMilliseconds}ms");
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
            catch (Exception ex)
            {
                AddinLog.Write("WebSocket heartbeat delay stopped: " + ex.Message);
                return;
            }

            try
            {
                var ping = Encoding.UTF8.GetBytes("{\"type\":\"ping\"}");
                await ws.SendAsync(new ArraySegment<byte>(ping), WebSocketMessageType.Text, true, token);
            }
            catch (Exception ex)
            {
                AddinLog.Write("WebSocket heartbeat send stopped: " + ex.Message);
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
