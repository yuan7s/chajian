using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net.Http;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace 外部程序;

public class SwAddinClient : IDisposable
{
    public event Action<string, string> DocChanged;
    public event Action<string, string> SelectionChanged;
    public event Action Disconnected;

    private readonly HttpClient _http = new HttpClient() { Timeout = TimeSpan.FromSeconds(30) };
    private readonly string _baseUrl;
    private readonly string _wsUrl;
    private ClientWebSocket _ws;
    private CancellationTokenSource _wsCts;
    private Timer _pingTimer;

    public SwAddinClient(int port = 32128)
    {
        _baseUrl = string.Format("http://127.0.0.1:{0}/", port);
        _wsUrl = string.Format("ws://127.0.0.1:{0}/events", port);
    }

    public async Task<bool> ConnectAsync()
    {
        try
        {
            var pingResult = await SendCommandAsync("ping");
            if (pingResult == null) return false;

            _wsCts = new CancellationTokenSource();
            _ws = new ClientWebSocket();
            await _ws.ConnectAsync(new Uri(_wsUrl), _wsCts.Token);
            StartWsReadLoop();
            return true;
        }
        catch (Exception ex)
        {
            Debug.WriteLine("SwAddinClient: WebSocket failed, falling back to HTTP ping: " + ex.Message);
            StartHttpPolling();
            return true;
        }
    }

    private void StartWsReadLoop()
    {
        Task.Run(async () =>
        {
            var buffer = new byte[4096];
            while (_ws != null && _ws.State == WebSocketState.Open)
            {
                try
                {
                    var result = await _ws.ReceiveAsync(new ArraySegment<byte>(buffer), _wsCts.Token);
                    if (result.MessageType == WebSocketMessageType.Close) break;
                    if (result.MessageType == WebSocketMessageType.Text)
                    {
                        var json = Encoding.UTF8.GetString(buffer, 0, result.Count);
                        ProcessWsMessage(json);
                    }
                }
                catch (Exception)
                {
                    break;
                }
            }
            Debug.WriteLine("SwAddinClient: WebSocket disconnected, attempting reconnect...");
            await ReconnectWsAsync();
        });
    }

    private async Task ReconnectWsAsync()
    {
        for (var attempt = 1; attempt <= 3; attempt++)
        {
            try
            {
                await Task.Delay(1000 * (int)Math.Pow(2, attempt - 1));
                _ws?.Dispose();
                _ws = new ClientWebSocket();
                _wsCts?.Cancel();
                _wsCts = new CancellationTokenSource();
                await _ws.ConnectAsync(new Uri(_wsUrl), _wsCts.Token);
                StartWsReadLoop();
                Debug.WriteLine("SwAddinClient: WebSocket reconnected on attempt " + attempt);
                return;
            }
            catch (Exception ex)
            {
                Debug.WriteLine("SwAddinClient: WS reconnect attempt " + attempt + " failed: " + ex.Message);
            }
        }
        Debug.WriteLine("SwAddinClient: WS reconnect exhausted, falling back to HTTP polling");
        Disconnected?.Invoke();
        StartHttpPolling();
    }

    private void StartHttpPolling()
    {
        _pingTimer = new Timer(
            async state =>
            {
                try
                {
                    var info = await SendCommandAsync("active-document");
                    if (info != null)
                    {
                        ProcessDocInfo(info);
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine("SwAddinClient: HTTP poll failed: " + ex.Message);
                }
            }, null, 5000, 5000);
    }

    private void ProcessWsMessage(string json)
    {
        try
        {
            var msg = DeserializeJson(json) as Dictionary<string, object>;
            if (msg == null) return;

            string msgType = null;
            if (msg.ContainsKey("type") && msg["type"] != null)
            {
                msgType = msg["type"].ToString();
            }

            switch (msgType)
            {
                case "ping":
                    break;
                case "doc-changed":
                    var docData = msg["data"] as Dictionary<string, object>;
                    if (docData != null)
                    {
                        var title = "";
                        var path = "";
                        if (docData.ContainsKey("title") && docData["title"] != null)
                            title = docData["title"].ToString();
                        if (docData.ContainsKey("path") && docData["path"] != null)
                            path = docData["path"].ToString();
                        DocChanged?.Invoke(title, path);
                    }
                    break;
                case "selection-changed":
                    var selData = msg["data"] as Dictionary<string, object>;
                    if (selData != null)
                    {
                        var name = "";
                        var sType = "";
                        if (selData.ContainsKey("name") && selData["name"] != null)
                            name = selData["name"].ToString();
                        if (selData.ContainsKey("type") && selData["type"] != null)
                            sType = selData["type"].ToString();
                        SelectionChanged?.Invoke(name, sType);
                    }
                    break;
                case "sw-shutdown":
                    Disconnected?.Invoke();
                    break;
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine("SwAddinClient: WS message parse error: " + ex.Message);
        }
    }

    private void ProcessDocInfo(object info)
    {
        try
        {
            var dict = info as Dictionary<string, object>;
            if (dict != null)
            {
                var title = "";
                var path = "";
                if (dict.ContainsKey("title") && dict["title"] != null)
                    title = dict["title"].ToString();
                if (dict.ContainsKey("path") && dict["path"] != null)
                    path = dict["path"].ToString();
                DocChanged?.Invoke(title, path);
            }
        }
        catch
        {
        }
    }

    public async Task<object> SendCommandAsync(string command, Dictionary<string, object> args = null)
    {
        try
        {
            var req = new Dictionary<string, object>
            {
                { "Command", command },
                { "Args", args ?? new Dictionary<string, object>() }
            };
            var json = JsonSerializer.Serialize(req);
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            var response = await _http.PostAsync(_baseUrl + "command", content);
            var body = await response.Content.ReadAsStringAsync();
            var result = DeserializeJson(body) as Dictionary<string, object>;

            if (result != null && result.ContainsKey("ok") && Convert.ToBoolean(result["ok"]))
            {
                return result.ContainsKey("data") ? result["data"] : null;
            }
            else
            {
                var errMsg = "未知错误";
                if (result != null && result.ContainsKey("error") && result["error"] != null)
                    errMsg = result["error"].ToString();
                throw new InvalidOperationException(errMsg);
            }
        }
        catch (InvalidOperationException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("通信失败: " + ex.Message, ex);
        }
    }

    private static object DeserializeJson(string json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        using var doc = JsonDocument.Parse(json);
        return ConvertJsonElement(doc.RootElement);
    }

    private static object ConvertJsonElement(JsonElement element)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                var dict = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
                foreach (var prop in element.EnumerateObject())
                    dict[prop.Name] = ConvertJsonElement(prop.Value);
                return dict;

            case JsonValueKind.Array:
                var items = new List<object>();
                foreach (var item in element.EnumerateArray())
                    items.Add(ConvertJsonElement(item));
                return items;

            case JsonValueKind.String:
                return element.GetString();

            case JsonValueKind.Number:
                if (element.TryGetInt64(out var longValue)) return longValue;
                return element.GetDouble();

            case JsonValueKind.True:
                return true;

            case JsonValueKind.False:
                return false;

            default:
                return null;
        }
    }

    public void Dispose()
    {
        _pingTimer?.Dispose();
        _pingTimer = null;
        _wsCts?.Cancel();
        if (_ws != null)
        {
            try
            {
                _ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "", CancellationToken.None).Wait(1000);
            }
            catch
            {
            }
            _ws.Dispose();
            _ws = null;
        }
        _http?.Dispose();
    }
}
