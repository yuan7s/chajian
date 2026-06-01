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
using SwConst;

namespace 外部程序.SwAddin;

internal sealed partial class AddinHttpServer : IDisposable
{
    private readonly SldWorks.SldWorks _swApp;
    private readonly System.Windows.Forms.Control _mainThreadControl;
    private readonly string _prefix;
    private readonly JavaScriptSerializer _json = new JavaScriptSerializer();
    private HttpListener _listener;
    private CancellationTokenSource _cts;
    private Task _listenTask;
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
        _listenTask = Task.Run(() => ListenLoop(_cts.Token));
    }

    public void Dispose()
    {
        // Close all WebSocket connections
        foreach (var ws in _webSockets)
        {
            try { ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "Shutdown", CancellationToken.None).Wait(1000); }
            catch { }
            try { ws.Dispose(); } catch { }
        }

        try { _cts?.Cancel(); } catch { }
        try { _listener?.Stop(); } catch { }
        try { _listener?.Close(); } catch { }
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
                WriteJson(context, new { ok = true, addin = "外部程序.SwAddin", prefix = _prefix, wsClients = _webSockets.Count });
                AddinLog.Write($"HTTP health ok in {watch.ElapsedMilliseconds}ms");
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
            AddinLog.Write($"Command start: {commandName}");
            var executeWatch = Stopwatch.StartNew();
            var result = await RunOnMainThread(() => ExecuteCommand(request));
            AddinLog.Write($"Command execute done: {commandName} in {executeWatch.ElapsedMilliseconds}ms");
            WriteJson(context, new { ok = true, data = result });
            AddinLog.Write($"Command ok: {commandName} in {watch.ElapsedMilliseconds}ms");
        }
        catch (Exception ex)
        {
            context.Response.StatusCode = 500;
            WriteJson(context, new { ok = false, error = ex.Message });
            AddinLog.Write($"HTTP failed {context.Request.HttpMethod} {requestPath} in {watch.ElapsedMilliseconds}ms: {ex}");
        }
    }

    private CommandRequest ReadCommand(HttpListenerRequest request)
    {
        using var reader = new StreamReader(request.InputStream, request.ContentEncoding ?? Encoding.UTF8);
        var body = reader.ReadToEnd();
        if (string.IsNullOrWhiteSpace(body)) return new CommandRequest();
        return _json.Deserialize<CommandRequest>(body) ?? new CommandRequest();
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
            try { await heartbeatTask; } catch { }
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
                    try { sock.Dispose(); } catch { }
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
            catch { return; }

            try
            {
                var ping = Encoding.UTF8.GetBytes("{\"type\":\"ping\"}");
                await ws.SendAsync(new ArraySegment<byte>(ping), WebSocketMessageType.Text, true, token);
            }
            catch { return; }
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
                catch { }
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
