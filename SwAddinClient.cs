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

namespace ExternalProgram;

public class SwAddinClient : IDisposable
{
    public event Action<string, string> DocChanged;
    public event Action<string, string> SelectionChanged;
    public event Action Disconnected;

    private static readonly TimeSpan DefaultCommandTimeout = TimeSpan.FromSeconds(30);
    private readonly HttpClient _http = new HttpClient() { Timeout = Timeout.InfiniteTimeSpan };
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

    public async Task<object> SendCommandAsync(string command, Dictionary<string, object> args = null, TimeSpan? timeout = null)
    {
        await RefreshEndpointAsync(false);
        try
        {
            return await SendCommandCoreAsync(command, args, timeout);
        }
        catch (InvalidOperationException ex) when (IsTransportFailure(ex.InnerException))
        {
            await RefreshEndpointAsync(true);
            return await SendCommandCoreAsync(command, args, timeout);
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

    private async Task<object> SendCommandCoreAsync(string command, Dictionary<string, object> args = null, TimeSpan? timeout = null)
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
            using var timeoutCts = new CancellationTokenSource(timeout ?? DefaultCommandTimeout);
            var response = await _http.PostAsync(_baseUrl + "command", content, timeoutCts.Token);
            var body = await response.Content.ReadAsStringAsync();
            var result = DeserializeJson(body) as Dictionary<string, object>;

            if (result != null && result.ContainsKey("ok") && Convert.ToBoolean(result["ok"]))
            {
                return result.ContainsKey("data") ? result["data"] : null;
            }
            else
            {
                var errMsg = FormatCommandError(result);
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

    private static string FormatCommandError(Dictionary<string, object> result)
    {
        if (result == null) return "未知错误";

        var code = GetDictionaryString(result, "errorCode");
        var args = GetDictionary(result, "errorArgs");
        if (string.IsNullOrWhiteSpace(code))
        {
            var legacyError = GetDictionaryString(result, "error");
            return string.IsNullOrWhiteSpace(legacyError) ? "未知错误" : legacyError;
        }

        var path = GetDictionaryString(args, "path");
        var name = GetDictionaryString(args, "name");
        var format = GetDictionaryString(args, "format");
        var operation = GetDictionaryString(args, "operation");
        var errors = GetDictionaryString(args, "errors");
        var warnings = GetDictionaryString(args, "warnings");
        var error = GetDictionaryString(args, "error");
        var command = GetDictionaryString(args, "command");

        switch (code)
        {
            case "active_document_required": return "没有活动文档";
            case "unknown_command": return "未知命令: " + command;
            case "argument_required": return string.IsNullOrWhiteSpace(name) ? "缺少必要参数" : name + " 不能为空";
            case "file_not_found": return string.IsNullOrWhiteSpace(path) ? "文件不存在" : "文件不存在: " + path;
            case "unsupported_file_type": return "不支持的文件类型";
            case "unsupported_solidworks_file_type": return "不支持的 SolidWorks 文件类型: " + path;
            case "drawing_required": return "请在工程图环境下使用";
            case "drawing_must_be_saved": return "当前工程图还没有保存，无法另存 " + format + "。";
            case "drawing_view_required": return "请选择一个视图";
            case "drawing_doc_unavailable": return "无法获取当前工程图对象";
            case "drawing_template_required": return "请选择工程图模板，或勾选使用 SW 默认模板。";
            case "drawing_template_unavailable": return "无法获取 SolidWorks 默认工程图模板，请在工程图窗口中选择 .drwdot 模板。";
            case "drawing_source_must_be_saved": return "当前模型尚未保存，无法生成关联工程图。";
            case "drawing_create_failed": return string.IsNullOrWhiteSpace(path) ? "创建工程图失败" : "创建工程图失败: " + path;
            case "assembly_required": return "请在装配体环境下使用";
            case "assembly_doc_unavailable": return "无法获取装配体对象";
            case "assembly_component_selection_required": return "请在装配体中选中一个组件";
            case "component_selection_required": return "请先选中一个或多个组件";
            case "component_not_found": return "未找到组件: " + name;
            case "component_select_failed": return "组件选择失败: " + name;
            case "document_extension_unavailable": return "无法获取当前文档扩展对象";
            case "assembly_extension_unavailable": return "无法获取装配体扩展对象";
            case "solidworks_operation_failed": return string.IsNullOrWhiteSpace(operation) ? "SolidWorks 操作失败" : "SolidWorks 操作失败: " + operation;
            case "reference_plane_mate_failed": return "未能添加基准面配合，请确认组件和装配体基准面名称匹配";
            case "macro_file_required": return "请选择 .swp 宏文件";
            case "unsupported_macro_file": return "只支持 .swp 宏文件";
            case "macro_run_failed": return "宏执行失败，错误码: " + error;
            case "feature_manager_unavailable": return "无法获取 FeatureManager";
            case "property_manager_unavailable": return "无法获取属性管理器";
            case "configuration_property_manager_unavailable": return "无法获取当前配置属性管理器";
            case "lightweight_component_write_unsupported": return "选中子件为轻化状态时仅支持读取文件属性，写入前请在 SolidWorks 中还原该子件。";
            case "component_file_property_path_invalid": return "无法读取选中子件文件属性，文件路径无效: " + path;
            case "document_manager_open_failed": return "Document Manager 打开文件失败: " + error;
            case "document_manager_unavailable": return "无法初始化 SolidWorks Document Manager，请检查内置许可证。";
            case "part_required_for_bounding_box": return "请在零件环境下获取包围盒";
            case "bounding_box_unavailable": return "无法获取包围盒";
            case "solidworks_rename_failed": return "SolidWorks 重命名失败，错误码: " + error;
            case "replace_component_reselect_failed": return "无法重新选中待替换组件";
            case "replace_component_failed": return "SolidWorks 未能替换组件引用";
            case "rename_component_select_failed": return "无法选中待重命名组件";
            case "selected_component_must_be_saved_for_rename": return "选中组件尚未保存，无法重命名";
            case "document_must_be_saved_for_rename": return "当前文档尚未保存，无法重命名";
            case "new_file_name_required": return "请输入新文件名";
            case "invalid_file_name": return "文件名包含非法字符";
            case "target_directory_unavailable": return "无法确定目标文件夹";
            case "unsupported_document_type": return "不支持的文档类型";
            case "target_file_exists": return "目标文件已存在: " + path;
            case "save_new_file_failed": return FormatSolidWorksCodeMessage("保存新文件失败", errors, warnings);
            case "open_new_file_failed": return FormatSolidWorksCodeMessage("打开新文件失败", errors, warnings);
            case "save_renamed_component_failed": return FormatSolidWorksCodeMessage("保存重命名后的组件失败", errors, warnings);
            case "save_assembly_failed": return FormatSolidWorksCodeMessage("保存当前装配体失败", errors, warnings);
            case "save_properties_failed": return FormatSolidWorksCodeMessage("保存属性失败", errors, warnings);
            case "assembly_configuration_unavailable": return "无法获取当前装配体配置";
            case "assembly_root_component_unavailable": return "无法获取装配体根组件";
            case "internal_error": return "插件内部错误，请查看插件日志。";
            default: return "插件返回失败: " + code;
        }
    }

    private static string FormatSolidWorksCodeMessage(string prefix, string errors, string warnings)
    {
        var message = string.IsNullOrWhiteSpace(warnings)
            ? prefix + "，错误码: " + errors
            : prefix + "，错误码: " + errors + "，警告码: " + warnings;
        var hint = GetSolidWorksSaveErrorHint(errors);
        return string.IsNullOrWhiteSpace(hint) ? message : message + "（" + hint + "）";
    }

    private static string GetSolidWorksSaveErrorHint(string errors)
    {
        if (!int.TryParse(errors, out var errorCode)) return "";
        return (errorCode & 8192) != 0 ? "需要先保存引用文档" : "";
    }

    private static Dictionary<string, object> GetDictionary(Dictionary<string, object> dict, string key)
    {
        if (dict == null || !dict.TryGetValue(key, out var value)) return null;
        return value as Dictionary<string, object>;
    }

    private static string GetDictionaryString(Dictionary<string, object> dict, string key)
    {
        if (dict == null || !dict.TryGetValue(key, out var value) || value == null) return "";
        return value.ToString();
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
