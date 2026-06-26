using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Text.Json;

namespace ReadBom;

internal static partial class SolidWorksAddinClient
{
    private static readonly HttpClient Client = new() { Timeout = Timeout.InfiniteTimeSpan };
    private static readonly SemaphoreSlim EndpointDiscoveryLock = new(1, 1);
    private static Uri BaseUri = new("http://127.0.0.1:32127/");
    private static int CurrentPort = 32127;
    private static int CurrentProcessId;
    private static string CurrentShortTitle = string.Empty;
    private static string CurrentTitle = string.Empty;
    private static string CurrentPath = string.Empty;
    private static int? ManualPort;
    private static DateTime LastEndpointDiscoveryUtc = DateTime.MinValue;
    private const int BasePort = 32127;
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

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    public static int SelectedPort => CurrentPort;

    public static int SelectedProcessId => CurrentProcessId;

    public static string SelectedShortTitle => CurrentShortTitle;

    public static string SelectedTitle => CurrentTitle;

    public static string SelectedPath => CurrentPath;

    public static bool IsManualEndpointSelection => ManualPort.HasValue;

    public static async Task<bool> IsAvailableAsync()
    {
        try
        {
            await RefreshEndpointAsync(true);
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
            using var response = await Client.GetAsync(new Uri(BaseUri, "health"), cts.Token);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    public static async Task<IReadOnlyList<AddinEndpoint>> GetAvailableEndpointsAsync()
    {
        return await DiscoverEndpointsAsync();
    }

    public static async Task SelectEndpointAsync(int port)
    {
        var endpoints = await DiscoverEndpointsAsync();
        var endpoint = endpoints.Find(item => item.Port == port);
        if (endpoint is null)
            throw new InvalidOperationException("未找到指定的 SolidWorks 连接: " + port);

        ManualPort = port;
        LastEndpointDiscoveryUtc = DateTime.MinValue;
        SetEndpoint(endpoint);
    }

    public static async Task UseAutomaticEndpointSelectionAsync()
    {
        ManualPort = null;
        LastEndpointDiscoveryUtc = DateTime.MinValue;
        await RefreshEndpointAsync(true);
    }

    private static async Task RefreshEndpointAsync(bool force)
    {
        if (!force &&
            (DateTime.UtcNow - LastEndpointDiscoveryUtc).TotalMilliseconds < EndpointDiscoveryThrottleMilliseconds)
        {
            return;
        }

        await EndpointDiscoveryLock.WaitAsync();
        try
        {
            if (!force &&
                (DateTime.UtcNow - LastEndpointDiscoveryUtc).TotalMilliseconds < EndpointDiscoveryThrottleMilliseconds)
            {
                return;
            }

            var endpoint = await DiscoverEndpointAsync();
            LastEndpointDiscoveryUtc = DateTime.UtcNow;
            if (endpoint == null) return;
            if (endpoint.Port == CurrentPort && endpoint.ProcessId == CurrentProcessId) return;

            SetEndpoint(endpoint);
        }
        finally
        {
            EndpointDiscoveryLock.Release();
        }
    }

    private static async Task<AddinEndpoint?> DiscoverEndpointAsync()
    {
        var endpoints = await DiscoverEndpointsAsync();
        if (ManualPort.HasValue)
        {
            var manualEndpoint = endpoints.Find(item => item.Port == ManualPort.Value);
            if (manualEndpoint is not null) return manualEndpoint;

            ManualPort = null;
        }

        var preferredProcessId = GetPreferredSolidWorksProcessId();
        AddinEndpoint? currentEndpoint = null;
        AddinEndpoint? firstEndpoint = null;

        foreach (var endpoint in endpoints)
        {
            firstEndpoint ??= endpoint;
            if (endpoint.Port == CurrentPort) currentEndpoint = endpoint;
            if (preferredProcessId != 0 && endpoint.ProcessId == preferredProcessId)
                return endpoint;
        }

        return currentEndpoint ?? firstEndpoint;
    }

    private static async Task<List<AddinEndpoint>> DiscoverEndpointsAsync()
    {
        var tasks = new List<Task<AddinEndpoint?>>();
        for (var port = BasePort; port < BasePort + PortScanCount; port++)
        {
            tasks.Add(TryGetEndpointAsync(port));
        }

        var results = await Task.WhenAll(tasks);
        var endpoints = new List<AddinEndpoint>();

        foreach (var endpoint in results)
        {
            if (endpoint == null) continue;
            endpoint.IsSelected = endpoint.Port == CurrentPort;
            endpoint.IsManualSelection = ManualPort.HasValue && endpoint.Port == ManualPort.Value;
            endpoints.Add(endpoint);
        }

        endpoints.Sort((left, right) => left.Port.CompareTo(right.Port));
        return endpoints;
    }

    private static async Task<AddinEndpoint?> TryGetEndpointAsync(int port)
    {
        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(250));
            using var response = await Client.GetAsync("http://127.0.0.1:" + port + "/health", cts.Token);
            if (!response.IsSuccessStatusCode) return null;

            var body = await response.Content.ReadAsStringAsync();
            using var document = JsonDocument.Parse(body);
            var root = document.RootElement;
            if (!root.TryGetProperty("ok", out var ok) || ok.ValueKind != JsonValueKind.True) return null;
            if (!root.TryGetProperty("addin", out var addin) ||
                !string.Equals(addin.GetString(), "ReadBom.SwAddin", StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            var processId = root.TryGetProperty("processId", out var pid) && pid.TryGetInt32(out var value)
                ? value
                : 0;
            var title = root.TryGetProperty("title", out var titleValue)
                ? titleValue.GetString() ?? string.Empty
                : string.Empty;
            var path = root.TryGetProperty("path", out var pathValue)
                ? pathValue.GetString() ?? string.Empty
                : string.Empty;
            var shortTitle = root.TryGetProperty("shortTitle", out var shortTitleValue)
                ? shortTitleValue.GetString() ?? string.Empty
                : string.Empty;
            return new AddinEndpoint(port, processId, shortTitle, title, path);
        }
        catch
        {
            return null;
        }
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

    private static void SetEndpoint(AddinEndpoint endpoint)
    {
        CurrentPort = endpoint.Port;
        CurrentProcessId = endpoint.ProcessId;
        CurrentShortTitle = endpoint.ShortTitle;
        CurrentTitle = endpoint.Title;
        CurrentPath = endpoint.Path;
        BaseUri = new Uri("http://127.0.0.1:" + endpoint.Port + "/");
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

    internal sealed class AddinEndpoint
    {
        public AddinEndpoint(int port, int processId, string shortTitle = "", string title = "", string path = "")
        {
            Port = port;
            ProcessId = processId;
            Title = title ?? string.Empty;
            Path = path ?? string.Empty;
            ShortTitle = BuildEndpointShortTitle(shortTitle, Title, Path, processId);
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
                var title = string.IsNullOrWhiteSpace(ShortTitle) ? NoDocumentTitle : ShortTitle;
                return "SW: " + title + "  (" + Port + ")";
            }
        }
    }
    public static async Task<AddinActiveDocumentInfo> GetActiveDocumentInfoAsync(Action<string>? log = null)
    {
        return await PostCommandAsync<AddinActiveDocumentInfo>(
            new { Command = "active-document" },
            TimeSpan.FromSeconds(10),
            log);
    }

    public static async Task<AddinOpenDocumentResult> OpenDocumentAsync(string path, Action<string>? log = null)
    {
        return await PostCommandAsync<AddinOpenDocumentResult>(
            new { Command = "open-document", Path = path },
            TimeSpan.FromMinutes(2),
            log);
    }

    public static async Task<List<string>> GetRelatedFilesAsync(string mainPath, Action<string>? log = null)
    {
        var response = await PostCommandAsync<AddinRelatedFilesResult>(
            new { Command = "related-files", Path = mainPath },
            TimeSpan.FromMinutes(2),
            log);
        return response.Files ?? [];
    }

    public static async Task<SaveResult> SavePropertiesBatchAsync(
        IReadOnlyList<AddinSavePropertyRow> rows,
        Action<ReadProgress>? progress = null,
        Action<string>? log = null)
    {
        var total = Math.Max(rows.Count, 1);
        progress?.Invoke(new ReadProgress("保存 TXT 属性到 SW (Add-in)", 0, total));
        var response = await PostCommandAsync<AddinSavePropertiesResult>(
            new
            {
                Command = "save-properties-batch",
                SaveRows = rows
            },
            TimeSpan.FromMinutes(10),
            log);
        progress?.Invoke(new ReadProgress("保存 TXT 属性到 SW (Add-in)", rows.Count, total));
        if (response.Failures is { Count: > 0 })
        {
            foreach (var failure in response.Failures.Take(5))
            {
                log?.Invoke($"Add-in 保存失败: {failure.DisplayName ?? failure.Path} - {failure.Error}");
            }
        }

        return new SaveResult(response.TotalRows, response.SavedRows, response.FailedRows, response.SavedProperties);
    }

    public static async Task<AddinBoxBatchResult> GetBoxBatchAsync(
        IReadOnlyList<AddinBlankSizeRow> rows,
        Action<ReadProgress>? progress = null,
        Action<string>? log = null)
    {
        var total = Math.Max(rows.Count, 1);
        progress?.Invoke(new ReadProgress("获取包围盒 (Add-in)", 0, total));
        var response = await PostCommandAsync<AddinBoxBatchResult>(
            new
            {
                Command = "calculate-blank-size",
                BlankRows = rows
            },
            TimeSpan.FromMinutes(10),
            log);
        progress?.Invoke(new ReadProgress("获取包围盒 (Add-in)", rows.Count, total));
        response.Results ??= [];
        return response;
    }

    public static async Task<List<BomRow>> ReadBomAsync(
        IReadOnlyList<PropertyMappingItem> propertiesToRead,
        ReadOptions options,
        Action<ReadProgress>? progress = null,
        Action<string>? log = null)
    {
        progress?.Invoke(new ReadProgress("Add-in 读取 BOM", 0, 1));
        var requestPropertyMappings = BuildAddinReadPropertyMappings(propertiesToRead);
        var requestPropertyNames = requestPropertyMappings.Select(item => item.Name).ToList();
        var request = new
        {
            Command = "read-bom",
            PropertyNames = requestPropertyNames.ToArray(),
            PropertyMappings = requestPropertyMappings
                .Select(item => new { item.Name, SourceMode = item.SourceMode.ToString() })
                .ToArray(),
            PropertySourceMode = options.PropertySourceMode.ToString(),
            GroupByConfig = options.GroupByConfig,
            SkipVirtual = options.SkipVirtual
        };
        var requestWatch = Stopwatch.StartNew();
        var response = await PostCommandAsync<ReadBomResponse>(request, TimeSpan.FromMinutes(10), log);
        log?.Invoke($"Add-in 计时: HTTP 请求到响应对象 {requestWatch.ElapsedMilliseconds}ms");

        var convertWatch = Stopwatch.StartNew();
        var rows = new List<BomRow>();
        var needsDeduplicate = true;
        if (!string.IsNullOrWhiteSpace(response.TableCsv?.CsvBase64))
        {
            rows.AddRange(CreateBomRowsFromCsv(response.TableCsv, requestPropertyNames, response.MainRow,
                options.GroupByConfig, log));
            needsDeduplicate = false;
        }
        else if (response.Table?.Rows is { Count: > 0 } tableRows)
        {
            var tablePropertyNames = response.Table.PropertyNames ?? requestPropertyNames;
            foreach (var row in tableRows)
            {
                rows.Add(CreateBomRowFromAddin(row, tablePropertyNames));
            }
        }
        else
        {
            foreach (var row in response.Rows ?? [])
            {
                rows.Add(CreateBomRowFromAddin(row, requestPropertyNames));
            }
        }

        if (needsDeduplicate)
        {
            rows = DeduplicateBomRows(rows, options.GroupByConfig, log);
        }

        log?.Invoke($"Add-in 计时: 主程序转换 BomRow {rows.Count} 行 {convertWatch.ElapsedMilliseconds}ms");
        progress?.Invoke(new ReadProgress("Add-in 读取 BOM", 1, 1));
        return rows;
    }

    private static List<PropertyMappingItem> BuildAddinReadPropertyMappings(IReadOnlyList<PropertyMappingItem> properties)
    {
        var result = properties
            .Where(item => !string.IsNullOrWhiteSpace(item.Name))
            .Select(item => item with { Name = item.Name.Trim() })
            .GroupBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .ToList();
        AddPropertyName(result, PropertyMappingConfig.FolderNamePropertyName, PropertySourceMode.CurrentConfiguration);
        AddPropertyName(result, PropertyMappingConfig.FileNamePropertyName, PropertySourceMode.CurrentConfiguration);
        return result;
    }

    private static void AddPropertyName(List<PropertyMappingItem> properties, string propertyName,
        PropertySourceMode sourceMode)
    {
        if (properties.All(item => !string.Equals(item.Name, propertyName, StringComparison.OrdinalIgnoreCase)))
        {
            properties.Add(new PropertyMappingItem(propertyName, sourceMode));
        }
    }

    private static BomRow CreateBomRowFromAddin(AddinBomRow row, IReadOnlyList<string> fallbackPropertyNames)
    {
        var fullPath = row.FullPath ?? string.Empty;
        var fileName = string.IsNullOrWhiteSpace(fullPath)
            ? row.FileName ?? string.Empty
            : Path.GetFileNameWithoutExtension(fullPath);
        var quantity = row.Quantity;
        var properties = new Dictionary<string, string>(
            row.Properties ?? new Dictionary<string, string>(),
            StringComparer.OrdinalIgnoreCase);
        SetBuiltInProperties(properties, fullPath, fileName, quantity);
        var available = row.AvailablePropertyNames is { Count: > 0 }
            ? row.AvailablePropertyNames
            : fallbackPropertyNames
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        EnsureBuiltInPropertyNames(available);
        foreach (var name in available)
        {
            if (!properties.ContainsKey(name))
            {
                properties[name] = string.Empty;
            }
        }

        return new BomRow
        {
            DocumentType = row.DocumentType ?? string.Empty,
            DocumentIconPath = GetDocumentIconPathFromLabel(row.DocumentType),
            DrawingStatus = row.DrawingStatus ?? string.Empty,
            DrawingIconPath = string.Empty,
            FileName = fileName,
            Configuration = string.IsNullOrWhiteSpace(row.Configuration) ? "Default" : row.Configuration,
            Quantity = quantity,
            Material = GetMaterialDisplay(row.DocumentType, row.Material),
            Properties = new Dictionary<string, string>(properties, StringComparer.OrdinalIgnoreCase),
            OriginalProperties = new Dictionary<string, string>(properties, StringComparer.OrdinalIgnoreCase),
            AvailablePropertyNames = available,
            FullPath = fullPath
        };
    }

    private static List<BomRow> CreateBomRowsFromCsv(
        AddinBomCsvTable table,
        IReadOnlyList<string> fallbackPropertyNames,
        AddinBomRow? mainRowFromAddin,
        bool groupByConfig,
        Action<string>? log)
    {
        var decodeWatch = Stopwatch.StartNew();
        var csvBytes = Convert.FromBase64String(table.CsvBase64 ?? string.Empty);
        var csvText = DecodeCsvBytes(csvBytes);
        log?.Invoke(
            $"Add-in timing: CSV decode {csvBytes.Length} bytes -> {csvText.Length} chars {decodeWatch.ElapsedMilliseconds}ms");

        var parseWatch = Stopwatch.StartNew();
        var delimiter = DetectDelimitedTextSeparator(csvText, table.Separator);
        var records = ParseDelimitedText(csvText, delimiter);
        log?.Invoke(
            $"Add-in timing: CSV parse {records.Count} rows, delimiter=[{DescribeDelimiter(delimiter)}], {parseWatch.ElapsedMilliseconds}ms");
        if (records.Count == 0)
        {
            return [];
        }

        var propertyNames = table.PropertyNames is { Count: > 0 }
            ? table.PropertyNames
            : fallbackPropertyNames
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        EnsureBuiltInPropertyNames(propertyNames);
        var headerIndex = FindCsvHeaderRow(records, propertyNames);
        var headers = records[headerIndex].Select(NormalizeCsvHeader).ToList();
        var quantityIndex = FindCsvColumn(headers, "\u6570\u91cf", "QTY", "QTY.", "Quantity");
        var fileNameIndex = FindCsvColumn(headers, "\u96f6\u4ef6\u53f7", "\u96f6\u4ef6\u7f16\u53f7", "Part Number",
            "PART NUMBER", "\u6587\u4ef6\u540d", "File Name", "\u540d\u79f0", "\u4ee3\u53f7", "PART NO.");
        var configurationIndex = FindCsvColumn(headers, "\u914d\u7f6e", "Configuration", "Config");
        var fullPathIndex = FindCsvColumn(headers, "\u5b8c\u6574\u8def\u5f84", "FullPath", "Path");
        var materialIndex = FindCsvColumn(headers, "SW\u6750\u6599", "SW-Material");
        var propertyIndexes = propertyNames.ToDictionary(name => name, name => FindCsvColumn(headers, name),
            StringComparer.OrdinalIgnoreCase);
        log?.Invoke($"CSV header: row={headerIndex + 1}, columns={headers.Count}, {string.Join(" | ", headers)}");
        log?.Invoke($"CSV column indexes: fileName={fileNameIndex}, quantity={quantityIndex}, configuration={configurationIndex}, material={materialIndex}");
        log?.Invoke(
            $"CSV row samples: {string.Join("; ", records.Skip(headerIndex + 1).Take(3).Select(row => row.Count + " columns"))}");

        var rows = new List<BomRow>();
        BomRow? mainRow = null;
        if (!string.IsNullOrWhiteSpace(table.MainPath))
        {
            if (mainRowFromAddin is not null)
            {
                mainRow = CreateBomRowFromAddin(mainRowFromAddin, propertyNames);
            }
            else
            {
                var mainProperties = CreateEmptyProperties(propertyNames);
                var mainFileName = Path.GetFileNameWithoutExtension(table.MainPath);
                SetBuiltInProperties(mainProperties, table.MainPath, mainFileName, 1);
                mainRow = new BomRow
                {
                    DocumentType = "\u88c5\u914d\u4f53",
                    DocumentIconPath = GetDocumentIconPathFromLabel("\u88c5\u914d\u4f53"),
                    DrawingStatus = string.Empty,
                    DrawingIconPath = string.Empty,
                    FileName = mainFileName,
                    Configuration =
                        string.IsNullOrWhiteSpace(table.MainConfiguration) ? "Default" : table.MainConfiguration,
                    Quantity = 1,
                    Material = "无须设置",
                    Properties = mainProperties,
                    OriginalProperties =
                        new Dictionary<string, string>(mainProperties, StringComparer.OrdinalIgnoreCase),
                    AvailablePropertyNames = propertyNames.ToList(),
                    FullPath = table.MainPath
                };
            }
        }

        for (var rowIndex = headerIndex + 1; rowIndex < records.Count; rowIndex++)
        {
            var record = records[rowIndex];
            if (record.All(string.IsNullOrWhiteSpace))
            {
                continue;
            }

            var fullPath = GetCsvCell(record, fullPathIndex);
            var rawFileName = GetCsvCell(record, fileNameIndex);
            if (string.IsNullOrWhiteSpace(rawFileName))
            {
                rawFileName = PickCsvDisplayName(record, headers, quantityIndex, materialIndex, propertyIndexes.Values);
            }

            if (ShouldSkipCsvBomRow(rawFileName, record, headers))
            {
                continue;
            }

            var quantity = ParseCsvQuantity(GetCsvCell(record, quantityIndex));
            var properties = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var propertyName in propertyNames)
            {
                properties[propertyName] = propertyIndexes.TryGetValue(propertyName, out var index)
                    ? GetCsvCell(record, index)
                    : string.Empty;
            }

            var csvFolderPath =
                properties.TryGetValue(PropertyMappingConfig.FolderNamePropertyName, out var folderValue)
                    ? folderValue
                    : string.Empty;
            var csvFileName = properties.TryGetValue(PropertyMappingConfig.FileNamePropertyName, out var fileValue)
                ? fileValue
                : string.Empty;
            fullPath = ResolveCsvFullPath(fullPath, csvFolderPath, csvFileName, rawFileName);
            var fileName = NormalizeCsvFileName(
                string.IsNullOrWhiteSpace(csvFileName) ? rawFileName : csvFileName,
                fullPath);
            var documentType = InferDocumentType(fullPath, fileName);
            SetBuiltInProperties(properties, fullPath, fileName, quantity);

            rows.Add(new BomRow
            {
                DocumentType = documentType,
                DocumentIconPath = GetDocumentIconPathFromLabel(documentType),
                DrawingStatus = string.Empty,
                DrawingIconPath = string.Empty,
                FileName = fileName,
                Configuration = string.IsNullOrWhiteSpace(GetCsvCell(record, configurationIndex))
                    ? "Default"
                    : GetCsvCell(record, configurationIndex),
                Quantity = quantity,
                Material = GetMaterialDisplay(documentType, GetCsvCell(record, materialIndex)),
                Properties = properties,
                OriginalProperties = new Dictionary<string, string>(properties, StringComparer.OrdinalIgnoreCase),
                AvailablePropertyNames = propertyNames.ToList(),
                FullPath = fullPath
            });
        }

        log?.Invoke($"Add-in 计时: CSV 转 BomRow {rows.Count} 行，headerRow={headerIndex + 1}");
        var deduplicatedRows = DeduplicateBomRows(rows, groupByConfig, log);
        SaveLastBomCsv(BuildLastBomCsv(deduplicatedRows, propertyNames), log);
        if (mainRow is not null)
        {
            deduplicatedRows.Insert(0, mainRow);
        }

        return deduplicatedRows;
    }

    private static List<BomRow> DeduplicateBomRows(IReadOnlyList<BomRow> rows, bool groupByConfig,
        Action<string>? log)
    {
        var result = new List<BomRow>();
        var index = new Dictionary<string, BomRow>(StringComparer.OrdinalIgnoreCase);
        var removed = 0;

        foreach (var row in rows)
        {
            var key = BuildDuplicateKey(row, groupByConfig);
            if (string.IsNullOrWhiteSpace(key) || !index.TryGetValue(key, out var existing))
            {
                result.Add(row);
                if (!string.IsNullOrWhiteSpace(key))
                {
                    index[key] = row;
                }

                continue;
            }

            MergeDuplicateRow(existing, row);
            removed++;
        }

        if (removed > 0)
        {
            log?.Invoke($"Add-in deduplicate: merged {removed} duplicate rows, remaining {result.Count} rows");
        }

        return result;
    }

    private static string BuildDuplicateKey(BomRow row, bool groupByConfig)
    {
        var identity = NormalizeDuplicateFileName(row.FullPath);
        if (string.IsNullOrWhiteSpace(identity))
        {
            identity = NormalizeDuplicateText(row.FileName);
        }

        if (string.IsNullOrWhiteSpace(identity))
        {
            return string.Empty;
        }

        return groupByConfig
            ? $"{identity}|{NormalizeDuplicateText(row.Configuration)}"
            : identity;
    }

    private static string NormalizeDuplicateFileName(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return string.Empty;
        }

        try
        {
            var fileName = Path.GetFileName(path.Trim());
            return string.IsNullOrWhiteSpace(fileName) ? string.Empty : fileName;
        }
        catch
        {
            return string.Empty;
        }
    }

    private static string NormalizeDuplicateText(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? "Default" : value.Trim();
    }

    private static void MergeDuplicateRow(BomRow target, BomRow duplicate)
    {
        target.Quantity += duplicate.Quantity;
        target.Properties[PropertyMappingConfig.QuantityPropertyName] =
            target.Quantity.ToString(CultureInfo.InvariantCulture);
        target.OriginalProperties[PropertyMappingConfig.QuantityPropertyName] =
            target.Quantity.ToString(CultureInfo.InvariantCulture);

        MergeMissingProperties(target.Properties, duplicate.Properties);
        MergeMissingProperties(target.OriginalProperties, duplicate.OriginalProperties);
        foreach (var propertyName in duplicate.AvailablePropertyNames)
        {
            if (!target.AvailablePropertyNames.Contains(propertyName, StringComparer.OrdinalIgnoreCase))
            {
                target.AvailablePropertyNames.Add(propertyName);
            }
        }

        if (string.IsNullOrWhiteSpace(target.FullPath) && !string.IsNullOrWhiteSpace(duplicate.FullPath))
        {
            target.FullPath = duplicate.FullPath;
        }

        if (string.IsNullOrWhiteSpace(target.DocumentType) && !string.IsNullOrWhiteSpace(duplicate.DocumentType))
        {
            target.DocumentType = duplicate.DocumentType;
            target.DocumentIconPath = duplicate.DocumentIconPath;
        }

        if (string.IsNullOrWhiteSpace(target.DrawingStatus) && !string.IsNullOrWhiteSpace(duplicate.DrawingStatus))
        {
            target.DrawingStatus = duplicate.DrawingStatus;
            target.DrawingIconPath = duplicate.DrawingIconPath;
        }

        if (IsUnsetMaterial(target.Material) && !IsUnsetMaterial(duplicate.Material))
        {
            target.Material = duplicate.Material;
        }
    }

    private static void MergeMissingProperties(Dictionary<string, string> target, IReadOnlyDictionary<string, string> source)
    {
        foreach (var (name, value) in source)
        {
            if (!target.TryGetValue(name, out var current) || string.IsNullOrWhiteSpace(current))
            {
                target[name] = value;
            }
        }
    }

    private static bool IsUnsetMaterial(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
               || value.Equals("\u672a\u8bbe\u7f6e", StringComparison.OrdinalIgnoreCase)
               || value.Equals("无须设置", StringComparison.OrdinalIgnoreCase);
    }

    private static Dictionary<string, string> CreateEmptyProperties(IReadOnlyList<string> propertyNames)
    {
        var properties = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var propertyName in propertyNames)
        {
            properties[propertyName] = string.Empty;
        }

        return properties;
    }

    private static void SetBuiltInProperties(Dictionary<string, string> properties, string fullPath, string fileName,
        int quantity)
    {
        var folderPath = GetDirectoryPath(fullPath);
        if (!string.IsNullOrWhiteSpace(folderPath)
            || !properties.TryGetValue(PropertyMappingConfig.FolderNamePropertyName, out var currentFolderPath)
            || string.IsNullOrWhiteSpace(currentFolderPath))
        {
            properties[PropertyMappingConfig.FolderNamePropertyName] = folderPath;
        }

        var resolvedFileName = ResolveDisplayFileName(fileName, fullPath);
        if (!string.IsNullOrWhiteSpace(resolvedFileName)
            || !properties.TryGetValue(PropertyMappingConfig.FileNamePropertyName, out var currentFileName)
            || string.IsNullOrWhiteSpace(currentFileName))
        {
            properties[PropertyMappingConfig.FileNamePropertyName] = resolvedFileName;
        }

        properties[PropertyMappingConfig.QuantityPropertyName] = quantity.ToString(CultureInfo.InvariantCulture);
    }

    private static string ResolveCsvFullPath(string fullPath, string folderPath, string csvFileName, string rawFileName)
    {
        if (!string.IsNullOrWhiteSpace(fullPath))
        {
            return fullPath;
        }

        if (string.IsNullOrWhiteSpace(folderPath))
        {
            return string.Empty;
        }

        var fileName = !string.IsNullOrWhiteSpace(csvFileName) ? csvFileName : rawFileName;
        if (string.IsNullOrWhiteSpace(fileName))
        {
            return string.Empty;
        }

        try
        {
            var candidate = Path.Combine(folderPath.Trim(), fileName.Trim());
            if (HasSolidWorksExtension(candidate))
            {
                return candidate;
            }

            foreach (var extension in new[] { ".sldprt", ".sldasm" })
            {
                var pathWithExtension = candidate + extension;
                if (File.Exists(pathWithExtension))
                {
                    return pathWithExtension;
                }
            }

            return candidate;
        }
        catch
        {
            return string.Empty;
        }
    }

    private static string GetDirectoryPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return string.Empty;
        }

        try
        {
            return Path.GetDirectoryName(path) ?? string.Empty;
        }
        catch
        {
            return string.Empty;
        }
    }

    private static void EnsureBuiltInPropertyNames(List<string> propertyNames)
    {
        foreach (var propertyName in new[]
                 {
                     PropertyMappingConfig.FolderNamePropertyName,
                     PropertyMappingConfig.FileNamePropertyName,
                     PropertyMappingConfig.QuantityPropertyName
                 })
        {
            if (!propertyNames.Contains(propertyName, StringComparer.OrdinalIgnoreCase))
            {
                propertyNames.Add(propertyName);
            }
        }
    }
}
