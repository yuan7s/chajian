using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Net.WebSockets;
using System.Runtime.InteropServices;
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
    private readonly int _basePort;
    private readonly SemaphoreSlim _endpointDiscoveryLock = new SemaphoreSlim(1, 1);
    private string _baseUrl;
    private string _wsUrl;
    private int _port;
    private int _processId;
    private int? _manualPort;
    private DateTime _lastEndpointDiscoveryUtc = DateTime.MinValue;
    private ClientWebSocket _ws;
    private CancellationTokenSource _wsCts;
    private Timer _pingTimer;
    private const int PortScanCount = 60;
    private const int EndpointDiscoveryThrottleMilliseconds = 750;
    private const int GwHwndNext = 2;
    private const string NoDocumentTitle = "\u65e0\u6587\u6863";

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern IntPtr GetTopWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern IntPtr GetWindow(IntPtr hWnd, int uCmd);

    [DllImport("user32.dll")]
    private static extern bool IsWindowVisible(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out int processId);

    public SwAddinClient(int port = 32128)
    {
        _basePort = port;
        SetEndpoint(port, 0);
    }

    public int CurrentPort => _port;

    public int CurrentProcessId => _processId;

    public bool IsManualEndpointSelection => _manualPort.HasValue;

    public async Task<bool> ConnectAsync()
    {
        try
        {
            await RefreshEndpointAsync(true);
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
        await RefreshEndpointAsync(false);
        try
        {
            return await SendCommandCoreAsync(command, args);
        }
        catch (InvalidOperationException ex) when (IsTransportFailure(ex.InnerException))
        {
            await RefreshEndpointAsync(true);
            return await SendCommandCoreAsync(command, args);
        }
    }

    public async Task<IReadOnlyList<SwAddinEndpoint>> GetAvailableEndpointsAsync()
    {
        return await DiscoverEndpointsAsync();
    }

    public async Task SelectEndpointAsync(int port)
    {
        var endpoints = await DiscoverEndpointsAsync();
        var endpoint = endpoints.Find(item => item.Port == port);
        if (endpoint == null)
            throw new InvalidOperationException("未找到指定的 SolidWorks 连接: " + port);

        _manualPort = port;
        _lastEndpointDiscoveryUtc = DateTime.MinValue;
        if (endpoint.Port != _port || endpoint.ProcessId != _processId)
        {
            Debug.WriteLine("SwAddinClient: manually selected endpoint port " + endpoint.Port + ", pid=" + endpoint.ProcessId);
            SetEndpoint(endpoint.Port, endpoint.ProcessId);
            ResetWebSocket();
        }
    }

    public async Task UseAutomaticEndpointSelectionAsync()
    {
        _manualPort = null;
        _lastEndpointDiscoveryUtc = DateTime.MinValue;
        await RefreshEndpointAsync(true);
    }

    private async Task<object> SendCommandCoreAsync(string command, Dictionary<string, object> args = null)
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

    private async Task RefreshEndpointAsync(bool force)
    {
        if (!force &&
            (DateTime.UtcNow - _lastEndpointDiscoveryUtc).TotalMilliseconds < EndpointDiscoveryThrottleMilliseconds)
        {
            return;
        }

        await _endpointDiscoveryLock.WaitAsync();
        try
        {
            if (!force &&
                (DateTime.UtcNow - _lastEndpointDiscoveryUtc).TotalMilliseconds < EndpointDiscoveryThrottleMilliseconds)
            {
                return;
            }

            var endpoint = await DiscoverEndpointAsync();
            _lastEndpointDiscoveryUtc = DateTime.UtcNow;
            if (endpoint == null) return;
            if (endpoint.Port == _port && endpoint.ProcessId == _processId) return;

            Debug.WriteLine("SwAddinClient: switching endpoint to port " + endpoint.Port + ", pid=" + endpoint.ProcessId);
            SetEndpoint(endpoint.Port, endpoint.ProcessId);
            ResetWebSocket();
        }
        finally
        {
            _endpointDiscoveryLock.Release();
        }
    }

    private async Task<SwAddinEndpoint> DiscoverEndpointAsync()
    {
        var endpoints = await DiscoverEndpointsAsync();
        if (_manualPort.HasValue)
        {
            var manualEndpoint = endpoints.Find(item => item.Port == _manualPort.Value);
            if (manualEndpoint != null) return manualEndpoint;

            _manualPort = null;
        }

        var preferredProcessId = GetPreferredSolidWorksProcessId();
        SwAddinEndpoint currentEndpoint = null;
        SwAddinEndpoint firstEndpoint = null;

        foreach (var endpoint in endpoints)
        {
            if (firstEndpoint == null) firstEndpoint = endpoint;
            if (endpoint.Port == _port) currentEndpoint = endpoint;
            if (preferredProcessId != 0 && endpoint.ProcessId == preferredProcessId)
                return endpoint;
        }

        return currentEndpoint ?? firstEndpoint;
    }

    private async Task<List<SwAddinEndpoint>> DiscoverEndpointsAsync()
    {
        var tasks = new List<Task<SwAddinEndpoint>>();
        for (var port = _basePort; port < _basePort + PortScanCount; port++)
        {
            tasks.Add(TryGetEndpointAsync(port));
        }

        var results = await Task.WhenAll(tasks);
        var endpoints = new List<SwAddinEndpoint>();

        foreach (var endpoint in results)
        {
            if (endpoint == null) continue;
            endpoint.IsSelected = endpoint.Port == _port;
            endpoint.IsManualSelection = _manualPort.HasValue && endpoint.Port == _manualPort.Value;
            endpoints.Add(endpoint);
        }

        endpoints.Sort((left, right) => left.Port.CompareTo(right.Port));
        return endpoints;
    }

    private async Task<SwAddinEndpoint> TryGetEndpointAsync(int port)
    {
        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(250));
            using var response = await _http.GetAsync("http://127.0.0.1:" + port + "/health", cts.Token);
            if (!response.IsSuccessStatusCode) return null;

            var body = await response.Content.ReadAsStringAsync();
            var result = DeserializeJson(body) as Dictionary<string, object>;
            if (result == null) return null;
            if (!result.TryGetValue("ok", out var okValue) || !Convert.ToBoolean(okValue)) return null;
            if (!result.TryGetValue("addin", out var addinValue) || addinValue == null) return null;
            if (!string.Equals(addinValue.ToString(), "ExternalProgram.SwAddin", StringComparison.OrdinalIgnoreCase))
                return null;

            var processId = GetResultInt(result, "processId");
            var title = GetResultString(result, "title");
            var path = GetResultString(result, "path");
            var shortTitle = GetResultString(result, "shortTitle");
            return new SwAddinEndpoint(port, processId, shortTitle, title, path);
        }
        catch
        {
            return null;
        }
    }

    private void SetEndpoint(int port, int processId)
    {
        _port = port;
        _processId = processId;
        _baseUrl = string.Format("http://127.0.0.1:{0}/", port);
        _wsUrl = string.Format("ws://127.0.0.1:{0}/events", port);
    }

    private void ResetWebSocket()
    {
        try { _wsCts?.Cancel(); } catch { }
        try { _ws?.Dispose(); } catch { }
        _ws = null;
    }

    private static int GetPreferredSolidWorksProcessId()
    {
        var foregroundPid = GetWindowProcessId(GetForegroundWindow());
        if (IsSolidWorksProcess(foregroundPid)) return foregroundPid;

        var currentProcessId = Process.GetCurrentProcess().Id;
        var hwnd = GetTopWindow(IntPtr.Zero);
        while (hwnd != IntPtr.Zero)
        {
            if (IsWindowVisible(hwnd))
            {
                var processId = GetWindowProcessId(hwnd);
                if (processId != currentProcessId && IsSolidWorksProcess(processId))
                    return processId;
            }

            hwnd = GetWindow(hwnd, GwHwndNext);
        }

        return 0;
    }

    private static int GetWindowProcessId(IntPtr hwnd)
    {
        if (hwnd == IntPtr.Zero) return 0;
        GetWindowThreadProcessId(hwnd, out var processId);
        return processId;
    }

    private static bool IsSolidWorksProcess(int processId)
    {
        if (processId == 0) return false;
        try
        {
            using var process = Process.GetProcessById(processId);
            return string.Equals(process.ProcessName, "SLDWORKS", StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    private static bool IsTransportFailure(Exception ex)
    {
        return ex is HttpRequestException;
    }

    private static int GetResultInt(Dictionary<string, object> dict, string key)
    {
        if (!dict.TryGetValue(key, out var value) || value == null) return 0;
        if (value is int intValue) return intValue;
        if (value is long longValue) return longValue > int.MaxValue ? 0 : (int)longValue;
        return int.TryParse(value.ToString(), out var parsed) ? parsed : 0;
    }

    private static string GetResultString(Dictionary<string, object> dict, string key)
    {
        if (!dict.TryGetValue(key, out var value) || value == null) return string.Empty;
        return value.ToString() ?? string.Empty;
    }

    private static string BuildEndpointShortTitle(string shortTitle, string title, string path, int processId)
    {
        var candidate = NormalizeEndpointTitle(shortTitle);
        if (string.IsNullOrWhiteSpace(candidate))
        {
            candidate = BuildShortDocumentTitle(title, path);
        }

        if (string.IsNullOrWhiteSpace(candidate))
        {
            candidate = GetProcessWindowShortTitle(processId);
        }

        return string.IsNullOrWhiteSpace(candidate) ? NoDocumentTitle : candidate.Trim();
    }

    private static string BuildShortDocumentTitle(string title, string path)
    {
        var candidate = GetFileTitle(path);
        if (string.IsNullOrWhiteSpace(candidate))
        {
            candidate = GetFileTitle(title);
        }

        return NormalizeEndpointTitle(candidate);
    }

    private static string GetProcessWindowShortTitle(int processId)
    {
        if (processId <= 0) return string.Empty;
        try
        {
            using var process = Process.GetProcessById(processId);
            return BuildShortWindowTitle(process.MainWindowTitle);
        }
        catch
        {
            return string.Empty;
        }
    }

    private static string BuildShortWindowTitle(string windowTitle)
    {
        var title = NormalizeEndpointTitle(windowTitle);
        if (string.IsNullOrWhiteSpace(title)) return string.Empty;

        var bracketStart = title.LastIndexOf('[');
        var bracketEnd = title.LastIndexOf(']');
        if (bracketStart >= 0 && bracketEnd > bracketStart)
        {
            title = title.Substring(bracketStart + 1, bracketEnd - bracketStart - 1);
        }
        else
        {
            var separatorIndex = title.LastIndexOf(" - ", StringComparison.Ordinal);
            if (separatorIndex >= 0 && separatorIndex + 3 < title.Length)
            {
                title = title.Substring(separatorIndex + 3);
            }
        }

        return BuildShortDocumentTitle(title, string.Empty);
    }

    private static string GetFileTitle(string value)
    {
        var title = NormalizeEndpointTitle(value);
        if (string.IsNullOrWhiteSpace(title)) return string.Empty;

        try
        {
            var fileName = Path.GetFileNameWithoutExtension(title);
            return NormalizeEndpointTitle(fileName);
        }
        catch
        {
            return title;
        }
    }

    private static string NormalizeEndpointTitle(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
    }

    public sealed class SwAddinEndpoint
    {
        public SwAddinEndpoint(int port, int processId, string shortTitle = "", string title = "", string path = "")
        {
            Port = port;
            ProcessId = processId;
            Title = title ?? string.Empty;
            Path = path ?? string.Empty;
            ShortTitle = SwAddinClient.BuildEndpointShortTitle(shortTitle, Title, Path, processId);
        }

        public int Port { get; }
        public int ProcessId { get; }
        public string ShortTitle { get; }
        public string Title { get; }
        public string Path { get; }
        public bool IsSelected { get; set; }
        public bool IsManualSelection { get; set; }

        public string DisplayName
        {
            get
            {
                var title = string.IsNullOrWhiteSpace(ShortTitle) ? SwAddinClient.NoDocumentTitle : ShortTitle;
                return "SW: " + title + "  (" + Port + ")";
            }
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
