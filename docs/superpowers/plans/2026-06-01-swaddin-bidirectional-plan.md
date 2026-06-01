# 外部程序.SwAddin 双向通信插件 实现计划

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 在外部程序.sln 中新建 C# SolidWorks 插件，提供 HTTP + WebSocket 双向通信桥接，将外部程序所有 SW 操作迁移到插件内部。

**Architecture:** 新建 `外部程序.SwAddin` C# 项目 (.NET Framework 4.8, x64)，参照 ReadBom.SwAddin 的 COM 注册/HTTP 模式，增加 WebSocket 事件推送。外部程序移除 Interop.SldWorks 直接引用，改为通过 `SwAddinClient` (HTTP+WS) 调用。

**Tech Stack:** C# 7.3+, VB.NET, .NET Framework 4.8, HttpListener, System.Net.WebSockets, SolidWorks Interop

---

### Task 1: 创建插件项目文件

**Files:**
- Create: `外部程序.SwAddin/外部程序.SwAddin.csproj`

- [ ] **Step 1: 创建项目文件**

```xml
<Project Sdk="Microsoft.NET.Sdk">

    <PropertyGroup>
        <TargetFramework>net48</TargetFramework>
        <LangVersion>latest</LangVersion>
        <Nullable>disable</Nullable>
        <PlatformTarget>x64</PlatformTarget>
        <Prefer32Bit>false</Prefer32Bit>
        <RegisterForComInterop>false</RegisterForComInterop>
        <AssemblyName>外部程序.SwAddin</AssemblyName>
        <RootNamespace>外部程序.SwAddin</RootNamespace>
        <OutputPath>bin\Debug\</OutputPath>
    </PropertyGroup>

    <ItemGroup>
        <Reference Include="Interop.SldWorks">
            <HintPath>..\lib\Interop.SldWorks.dll</HintPath>
            <EmbedInteropTypes>False</EmbedInteropTypes>
        </Reference>
        <Reference Include="Interop.SwConst">
            <HintPath>..\lib\Interop.SwConst.dll</HintPath>
            <EmbedInteropTypes>False</EmbedInteropTypes>
        </Reference>
        <Reference Include="SolidWorks.Interop.swpublished">
            <HintPath>C:\Program Files\SOLIDWORKS\SolidWorks.Interop.swpublished.dll</HintPath>
            <EmbedInteropTypes>False</EmbedInteropTypes>
        </Reference>
        <Reference Include="SolidWorksTools">
            <HintPath>C:\Program Files\SOLIDWORKS\SolidWorksTools.dll</HintPath>
        </Reference>
        <Reference Include="System.Net.Http" />
        <Reference Include="System.Web.Extensions" />
        <Reference Include="System.Windows.Forms" />
    </ItemGroup>

    <Target Name="CloseSolidWorksWhenAddinDllLocked" BeforeTargets="BeforeBuild" Condition="'$(DesignTimeBuild)' != 'true'">
        <Exec Command="powershell -NoProfile -ExecutionPolicy Bypass -Command &quot;$path = '$(TargetPath)'; if (Test-Path -LiteralPath $path) { try { $stream = [System.IO.File]::Open($path, 'Open', 'ReadWrite', 'None'); $stream.Dispose() } catch { Write-Host '外部程序.SwAddin.dll is locked; closing SLDWORKS.exe'; Get-Process -Name SLDWORKS -ErrorAction SilentlyContinue | Stop-Process -Force } }&quot;" />
    </Target>

</Project>
```

- [ ] **Step 2: 将新项目添加到解决方案**

Run: `dotnet sln 外部程序.sln add 外部程序.SwAddin/外部程序.SwAddin.csproj`

- [ ] **Step 3: 验证项目能编译**

Run: `dotnet build 外部程序.SwAddin/外部程序.SwAddin.csproj`
Expected: Build succeeded (0 errors, may have warnings about unused references)

- [ ] **Step 4: Commit**

```bash
git add 外部程序.SwAddin/外部程序.SwAddin.csproj 外部程序.sln
git commit -m "feat: add SwAddin C# project scaffold"
```

---

### Task 2: SwAddin 入口和 COM 注册

**Files:**
- Create: `外部程序.SwAddin/SwAddin.cs`

- [ ] **Step 1: 编写 SwAddin.cs**

```csharp
using System;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Microsoft.Win32;
using SolidWorksTools;

namespace 外部程序.SwAddin;

[ComVisible(true)]
[Guid("C8F7A3D2-6B51-4E92-A814-7F2D3C1E9A56")]
[ProgId("外部程序.SwAddin")]
[SwAddin(Description = AddinDescription, Title = AddinTitle, LoadAtStartup = true)]
public sealed class SwAddin : SolidWorks.Interop.swpublished.SwAddin
{
    private const string AddinTitle = "外部程序 HTTP Addin";
    private const string AddinDescription = "外部程序 local HTTP/WS bridge for SolidWorks commands.";
    private SldWorks.SldWorks _swApp;
    private int _cookie;
    private AddinHttpServer _server;
    private Control _mainThreadControl;

    public bool ConnectToSW(object thisSw, int cookie)
    {
        try
        {
            AddinLog.Write("ConnectToSW called");
            var assemblyPath = Assembly.GetExecutingAssembly().Location;
            AddinLog.Write($"Addin assembly: {assemblyPath}, lastWrite={File.GetLastWriteTime(assemblyPath):yyyy-MM-dd HH:mm:ss}");
            _swApp = (SldWorks.SldWorks)thisSw;
            _cookie = cookie;

            _mainThreadControl = new Control();
            var _ = _mainThreadControl.Handle;

            try
            {
                _swApp.SetAddinCallbackInfo2(0, this, _cookie);
                AddinLog.Write("SetAddinCallbackInfo2 ok");
            }
            catch (Exception ex)
            {
                AddinLog.Write("SetAddinCallbackInfo2 ignored: " + ex.Message);
            }

            _server = new AddinHttpServer(_swApp, _mainThreadControl, "http://127.0.0.1:32128/");
            _server.Start();
            AddinLog.Write("HTTP+WS server started");
            return true;
        }
        catch (Exception ex)
        {
            AddinLog.Write("ConnectToSW failed: " + ex);
            return false;
        }
    }

    public bool DisconnectFromSW()
    {
        AddinLog.Write("DisconnectFromSW called");
        _server?.Dispose();
        _server = null;
        _swApp = null;
        _cookie = 0;
        return true;
    }

    [ComRegisterFunction]
    public static void Register(Type type)
    {
        var attribute = (SwAddinAttribute)System.Attribute.GetCustomAttribute(type, typeof(SwAddinAttribute));
        var title = attribute?.Title ?? AddinTitle;
        var description = attribute?.Description ?? AddinDescription;
        var loadAtStartup = attribute?.LoadAtStartup == true ? 1 : 0;

        using (var key = Registry.LocalMachine.CreateSubKey($@"SOFTWARE\SolidWorks\AddIns\{{{type.GUID}}}"))
        {
            key.SetValue(null, 0, RegistryValueKind.DWord);
            key.SetValue("Title", title, RegistryValueKind.String);
            key.SetValue("Description", description, RegistryValueKind.String);
        }

        using (var key = Registry.CurrentUser.CreateSubKey($@"SOFTWARE\SolidWorks\AddIns\{{{type.GUID}}}"))
        {
            key.SetValue(null, 0, RegistryValueKind.DWord);
            key.SetValue("Title", title, RegistryValueKind.String);
            key.SetValue("Description", description, RegistryValueKind.String);
        }

        using (var key = Registry.CurrentUser.CreateSubKey($@"SOFTWARE\SolidWorks\AddInsStartup\{{{type.GUID}}}"))
        {
            key.SetValue(null, loadAtStartup, RegistryValueKind.DWord);
        }
    }

    [ComUnregisterFunction]
    public static void Unregister(Type type)
    {
        Registry.LocalMachine.DeleteSubKey($@"SOFTWARE\SolidWorks\AddIns\{{{type.GUID}}}", false);
        Registry.CurrentUser.DeleteSubKey($@"SOFTWARE\SolidWorks\AddIns\{{{type.GUID}}}", false);
        Registry.CurrentUser.DeleteSubKey($@"SOFTWARE\SolidWorks\AddInsStartup\{{{type.GUID}}}", false);
    }
}

internal static class AddinLog
{
    public static readonly string DirectoryPath = GetAddinDirectory();
    private static readonly string LogPath = Path.Combine(DirectoryPath, "SwAddin.log");

    public static void Write(string message)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(LogPath));
            File.AppendAllText(LogPath, $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} {message}{Environment.NewLine}");
        }
        catch { }
    }

    private static string GetAddinDirectory()
    {
        try
        {
            var location = Assembly.GetExecutingAssembly().Location;
            var directory = Path.GetDirectoryName(location);
            if (!string.IsNullOrWhiteSpace(directory)) return directory;
        }
        catch { }
        return AppDomain.CurrentDomain.BaseDirectory;
    }
}
```

- [ ] **Step 2: 验证编译**

Run: `dotnet build 外部程序.SwAddin/外部程序.SwAddin.csproj`
Expected: Build succeeded

- [ ] **Step 3: Commit**

```bash
git add 外部程序.SwAddin/SwAddin.cs
git commit -m "feat: add SwAddin COM entry point with registration"
```

---

### Task 3: CommandRequest DTO

**Files:**
- Create: `外部程序.SwAddin/CommandRequest.cs`

- [ ] **Step 1: 编写 DTO**

```csharp
using System.Collections.Generic;

namespace 外部程序.SwAddin;

public sealed class CommandRequest
{
    public string Command { get; set; }
    public Dictionary<string, object> Args { get; set; }
}
```

- [ ] **Step 2: 验证编译**

Run: `dotnet build 外部程序.SwAddin/外部程序.SwAddin.csproj`

- [ ] **Step 3: Commit**

```bash
git add 外部程序.SwAddin/CommandRequest.cs
git commit -m "feat: add CommandRequest DTO"
```

---

### Task 4: HTTP Server 基础设施 + WebSocket

**Files:**
- Create: `外部程序.SwAddin/AddinHttpServer.cs`

- [ ] **Step 1: 编写 AddinHttpServer.cs（HTTP 监听 + WebSocket 升级）**

```csharp
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
            // Remove closed socket
            var toRemove = new List<WebSocket>();
            foreach (var ws in _webSockets)
            {
                if (ws.State != WebSocketState.Open) toRemove.Add(ws);
            }
            foreach (var ws in toRemove)
            {
                try { ws.Dispose(); } catch { }
                _webSockets.TryTake(out _);
            }
            AddinLog.Write($"WebSocket client disconnected, total={_webSockets.Count}");
        }
    }

    private async Task RunHeartbeat(WebSocket ws, CancellationToken token)
    {
        while (!token.IsCancellationRequested && ws.State == WebSocketState.Open)
        {
            try
            {
                await Task.Delay(30000, token);
            }
            catch { return; }

            try
            {
                // Send ping via text message with ping type
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
                try
                {
                    ws.SendAsync(segment, WebSocketMessageType.Text, true, CancellationToken.None);
                }
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
}
```

- [ ] **Step 2: 验证编译**

Run: `dotnet build 外部程序.SwAddin/外部程序.SwAddin.csproj`

- [ ] **Step 3: Commit**

```bash
git add 外部程序.SwAddin/AddinHttpServer.cs
git commit -m "feat: add HTTP server with WebSocket support"
```

---

### Task 5: SW 命令实现（基础命令）

**Files:**
- Create: `外部程序.SwAddin/AddinHttpServer.Commands.cs`

- [ ] **Step 1: 编写基础命令（ExecuteCommand + ping + active-document + rebuild + save + open-document）**

```csharp
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using SldWorks;
using SwConst;

namespace 外部程序.SwAddin;

internal sealed partial class AddinHttpServer
{
    private object ExecuteCommand(CommandRequest request)
    {
        var args = request.Args ?? new Dictionary<string, object>();
        switch ((request.Command ?? string.Empty).Trim().ToLowerInvariant())
        {
            case "ping":
                return new { message = "pong", time = DateTime.Now };

            case "active-document":
                return GetActiveDocumentInfo();

            case "save-dwg":
                return SaveActiveDrawingAs(".dwg", "DWG");

            case "save-pdf":
                return SaveActiveDrawingAs(".pdf", "PDF");

            case "open-file-location":
                return GetActiveOrSelectedModelPath();

            case "rotate-drawing-view":
                return RotateSelectedDrawingView();

            case "get-component-tree":
                return GetComponentTree();

            case "sort-components":
                return SortComponents();

            case "hide-config-names":
                return HideConfigNames();

            case "sync-coding-props":
                return SyncCodingProps();

            case "read-properties":
                return ReadProperties(args);

            case "write-properties":
                return WriteProperties(args);

            case "get-bounding-box":
                return GetBoundingBox(args);

            case "rename-component":
                return RenameComponent(args);

            case "coding-cleanup":
                return CodingCleanup(args);

            case "rebuild":
                return Rebuild();

            case "save":
                return Save();

            case "open-document":
                return OpenDocument(args);

            default:
                throw new InvalidOperationException($"未知命令: {request.Command}");
        }
    }

    private ModelDoc2 GetActiveModel()
    {
        var model = _swApp.ActiveDoc as ModelDoc2;
        if (model == null) throw new InvalidOperationException("没有活动文档");
        return model;
    }

    private static T Safe<T>(Func<T> work)
    {
        try { return work(); }
        catch { return default; }
    }

    private string GetArgString(Dictionary<string, object> args, string key, string fallback = null)
    {
        if (args.TryGetValue(key, out var val) && val != null) return val.ToString();
        return fallback;
    }

    private object GetActiveDocumentInfo()
    {
        var model = GetActiveModel();
        return new
        {
            title = Safe(() => model.GetTitle()),
            path = Safe(() => model.GetPathName()),
            configuration = Safe(() => model.ConfigurationManager.ActiveConfiguration.Name),
            type = Safe(() => (int)model.GetType())
        };
    }

    private object Rebuild()
    {
        var model = GetActiveModel();
        model.EditRebuild3();
        return new { rebuilt = true };
    }

    private object Save()
    {
        var model = GetActiveModel();
        model.Save3((int)swSaveAsOptions_e.swSaveAsOptions_Silent, 0, 0);
        return new { saved = true };
    }

    private object OpenDocument(Dictionary<string, object> args)
    {
        var path = GetArgString(args, "path");
        if (string.IsNullOrWhiteSpace(path)) throw new InvalidOperationException("path 不能为空");
        if (!File.Exists(path)) throw new InvalidOperationException("文件不存在");

        var docType = GetDocumentTypeFromPath(path);
        if (docType == 0) throw new InvalidOperationException("不支持的文件类型");

        // Check if already open
        var existing = TryGetOpenModelByPath(path);
        if (existing != null)
        {
            return new { path, title = Safe(() => existing.GetTitle()), alreadyOpen = true };
        }

        var errors = 0;
        var warnings = 0;
        var model = _swApp.OpenDoc6(path, docType, (int)swOpenDocOptions_e.swOpenDocOptions_Silent, "", ref errors, ref warnings);
        return new { path, title = Safe(() => model?.GetTitle()), errors, warnings };
    }

    private ModelDoc2 TryGetOpenModelByPath(string path)
    {
        try
        {
            var model = _swApp.GetOpenDocumentByName(path) as ModelDoc2;
            return model;
        }
        catch { return null; }
    }

    private int GetDocumentTypeFromPath(string path)
    {
        var ext = Path.GetExtension(path)?.ToLowerInvariant();
        switch (ext)
        {
            case ".sldprt": return (int)swDocumentTypes_e.swDocPART;
            case ".sldasm": return (int)swDocumentTypes_e.swDocASSEMBLY;
            case ".slddrw": return (int)swDocumentTypes_e.swDocDRAWING;
            default: return 0;
        }
    }

    // --- Drawing Save ---

    private object SaveActiveDrawingAs(string extension, string formatName)
    {
        var model = GetActiveModel();
        if (model.GetType() != (int)swDocumentTypes_e.swDocDRAWING)
            throw new InvalidOperationException("请在工程图环境下使用");

        var docPath = model.GetPathName();
        if (string.IsNullOrWhiteSpace(docPath))
            throw new InvalidOperationException($"当前工程图还没有保存，无法另存 {formatName}。");

        var outputPath = Path.ChangeExtension(docPath, extension);
        model.SaveAs3(outputPath, 0, 2);
        return new { outputPath, format = formatName };
    }

    // --- Open File Location ---

    private object GetActiveOrSelectedModelPath()
    {
        var model = GetActiveModel();
        var path = model.GetPathName();
        if (!string.IsNullOrWhiteSpace(path)) return new { path };

        // Try getting selected component path
        if (model.GetType() == (int)swDocumentTypes_e.swDocASSEMBLY)
        {
            try
            {
                var selMgr = model.SelectionManager;
                var comp = selMgr.GetSelectedObjectsComponent(1) as Component2;
                if (comp != null)
                {
                    path = comp.GetPathName();
                    if (!string.IsNullOrWhiteSpace(path)) return new { path };
                }
            }
            catch { }
        }

        return new { path = model.GetTitle() };
    }

    // --- Rotate Drawing View ---

    private object RotateSelectedDrawingView()
    {
        var model = GetActiveModel();
        if (model.GetType() != (int)swDocumentTypes_e.swDocDRAWING)
            throw new InvalidOperationException("请在工程图环境下使用");

        var selMgr = model.SelectionManager;
        var swView = selMgr.GetSelectedObject6(1, -1) as View;
        if (swView == null)
            throw new InvalidOperationException("请选择一个视图");

        if (swView.Angle > 4.5)
            swView.Angle = 0;
        else
            swView.Angle += Math.PI / 2;

        model.Extension.SelectByID2(swView.Name, "DRAWINGVIEW", 0, 0, 0, false, 0, null, 0);
        MarkDocDirty(model);

        return new { viewName = swView.Name, angle = swView.Angle };
    }

    private void MarkDocDirty(ModelDoc2 model)
    {
        try { model.SetSaveFlag(); } catch { }
    }
}
```

- [ ] **Step 2: 验证编译**

Run: `dotnet build 外部程序.SwAddin/外部程序.SwAddin.csproj`

- [ ] **Step 3: Commit**

```bash
git add 外部程序.SwAddin/AddinHttpServer.Commands.cs
git commit -m "feat: add basic SW commands (ping, active-doc, save, rebuild, open, drawing ops)"
```

---

### Task 6: SW 命令实现（装配体操作）

**Files:**
- Modify: `外部程序.SwAddin/AddinHttpServer.Commands.cs`

在文件末尾（最后一个 `}` 之前）添加以下方法：

- [ ] **Step 1: 添加 HideConfigNames 和 GetComponentTree**

```csharp
    // --- Hide Config Names (FeatureManager) ---

    private object HideConfigNames()
    {
        var model = GetActiveModel();
        if (model.GetType() != (int)swDocumentTypes_e.swDocASSEMBLY)
            throw new InvalidOperationException("请在装配体环境下使用");

        var featMgr = model.FeatureManager;
        featMgr.HideComponentSingleConfigurationOrDisplayStateNames = false;
        featMgr.SetComponentIdentifiers(4, 0, 0);
        featMgr.SetComponentIdentifiers(2, 0, 0);
        featMgr.ShowComponentConfigurationNames = false;
        featMgr.ShowComponentConfigurationDescriptions = false;
        featMgr.ShowDisplayStateNames = false;

        RecursiveHideConfigNames(_swApp, model);

        return new { done = true };
    }

    private void RecursiveHideConfigNames(SldWorks.SldWorks swApp, ModelDoc2 asmDoc)
    {
        var configuration = asmDoc.GetActiveConfiguration();
        var rootComponent = configuration.GetRootComponent();
        var comps = (object[])rootComponent.GetChildren();

        foreach (Component2 child in comps)
        {
            var childModel = child.GetModelDoc() as ModelDoc2;
            if (childModel == null) continue;

            var childType = childModel.GetType();
            if (childType == (int)swDocumentTypes_e.swDocASSEMBLY)
            {
                var longstatus = 0;
                var longWarnings = 0;
                var fopen = swApp.OpenDoc6(child.GetPathName(), (int)swDocumentTypes_e.swDocASSEMBLY,
                    (int)swOpenDocOptions_e.swOpenDocOptions_Silent, "", ref longstatus, ref longWarnings);
                if (longstatus == 0 && fopen != null)
                {
                    var swFeatMgr = fopen.FeatureManager;
                    swFeatMgr.HideComponentSingleConfigurationOrDisplayStateNames = false;
                    swFeatMgr.SetComponentIdentifiers(4, 0, 0);
                    swFeatMgr.SetComponentIdentifiers(2, 0, 0);
                    swFeatMgr.ShowComponentConfigurationNames = false;
                    swFeatMgr.ShowComponentConfigurationDescriptions = false;
                    swFeatMgr.ShowDisplayStateNames = false;
                }
                RecursiveHideConfigNames(swApp, childModel);
            }
        }
    }

    // --- Get Component Tree ---

    private object GetComponentTree()
    {
        var model = GetActiveModel();
        if (model.GetType() != (int)swDocumentTypes_e.swDocASSEMBLY)
            throw new InvalidOperationException("请在装配体环境下使用");

        var components = new List<object>();
        BuildComponentTree(model, components);
        return new { components };
    }

    private void BuildComponentTree(ModelDoc2 asmDoc, List<object> result)
    {
        var configuration = asmDoc.GetActiveConfiguration();
        var rootComponent = configuration.GetRootComponent();
        var comps = (object[])rootComponent.GetChildren();

        foreach (Component2 child in comps)
        {
            var childModel = child.GetModelDoc() as ModelDoc2;
            var info = new
            {
                name = Safe(() => child.Name2),
                path = Safe(() => child.GetPathName()),
                referencedPath = childModel != null ? Safe(() => childModel.GetPathName()) : null,
                type = childModel != null ? Safe(() => (int)childModel.GetType()) : -1,
                isSuppressed = Safe(() => child.IsSuppressed()),
                isEnvelope = Safe(() => child.IsEnvelope())
            };
            result.Add(info);
        }
    }
```

- [ ] **Step 2: 添加 SortComponents（设计树排序）**

```csharp
    // --- Sort Components ---

    private object SortComponents()
    {
        var model = GetActiveModel();
        if (model.GetType() != (int)swDocumentTypes_e.swDocASSEMBLY)
            throw new InvalidOperationException("请在装配体环境下使用");

        var assemblyDoc = model as AssemblyDoc;
        if (assemblyDoc == null) throw new InvalidOperationException("无法获取 AssemblyDoc");

        var topAsmName = model.GetTitle();
        var dotIdx = topAsmName.IndexOf(".");
        if (dotIdx > 0) topAsmName = topAsmName.Substring(0, dotIdx - 7 < 0 ? dotIdx : dotIdx - 7);

        var vFeats = (object[])model.FeatureManager.GetFeatures(true);

        // Find start/end indices
        var startIdx = 0;
        var endIdx = vFeats.Length - 1;
        for (var i = 0; i < vFeats.Length; i++)
        {
            var t = ((Feature)vFeats[i]).GetTypeName2();
            if (t == "OriginProfileFeature") startIdx = i + 1;
            if (t == "MateGroup") { endIdx = i - 1; break; }
        }

        // Group features
        var folders = new List<Feature>();
        var folderComponents = new List<List<Feature>>();
        var topLevelFeats = new List<Feature>();
        var suppressedEnvFeats = new List<Feature>();
        List<Feature> currentFolderComps = null;

        for (var i = startIdx; i <= endIdx; i++)
        {
            var feat = (Feature)vFeats[i];
            var featType = feat.GetTypeName2();
            if (featType == "FtrFolder")
            {
                if (!feat.Name.Contains("___EndTag___"))
                {
                    folders.Add(feat);
                    currentFolderComps = new List<Feature>();
                    folderComponents.Add(currentFolderComps);
                }
                else
                {
                    currentFolderComps = null;
                }
            }
            else if (featType == "Reference")
            {
                var isSupOrEnv = false;
                try
                {
                    var comp = feat.GetSpecificFeature2() as Component2;
                    if (comp != null) isSupOrEnv = comp.IsSuppressed() || comp.IsEnvelope();
                }
                catch { }
                if (isSupOrEnv)
                {
                    if (currentFolderComps != null) currentFolderComps.Add(feat);
                    else suppressedEnvFeats.Add(feat);
                }
                else if (currentFolderComps != null)
                {
                    currentFolderComps.Add(feat);
                }
                else
                {
                    topLevelFeats.Add(feat);
                }
            }
        }

        // Collect component names and sort
        var sortedPartComps = CollectAndSort(topLevelFeats, false);
        var sortedAsmComps = CollectAndSort(topLevelFeats, true);
        var sortedSupPart = CollectAndSort(suppressedEnvFeats, false);
        var sortedSupAsm = CollectAndSort(suppressedEnvFeats, true);

        // Merge: asm first, then part, then suppressed
        var allSorted = new List<Component2>();
        allSorted.AddRange(sortedAsmComps);
        allSorted.AddRange(sortedPartComps);
        allSorted.AddRange(sortedSupAsm);
        allSorted.AddRange(sortedSupPart);

        // Move after last folder
        if (allSorted.Count > 0)
        {
            var lastFolder = folders.Count > 0 ? folders[folders.Count - 1] : null;
            if (lastFolder != null)
                assemblyDoc.ReorderComponents(allSorted[0], lastFolder, (int)swReorderComponentsWhere_e.swReorderComponents_After);
            for (var i = 1; i < allSorted.Count; i++)
                assemblyDoc.ReorderComponents(allSorted[i], allSorted[i - 1], 1);
        }

        // Sort within folders
        foreach (var f in folders)
            SortComponentsInFolder(f, assemblyDoc);

        // Recursive sub-assembly sort
        var processed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        RecursiveSortSubAssemblies(topLevelFeats, processed);
        foreach (var fc in folderComponents)
            RecursiveSortSubAssemblies(fc, processed);

        model.EditRebuild3();
        model.ClearSelection2(true);

        return new { done = true, topLevelSorted = allSorted.Count, foldersSorted = folders.Count };
    }

    private List<Component2> CollectAndSort(List<Feature> feats, bool assemblies)
    {
        var result = new List<Component2>();
        var targetType = assemblies ? (int)swDocumentTypes_e.swDocASSEMBLY : (int)swDocumentTypes_e.swDocPART;
        foreach (var feat in feats)
        {
            var comp = feat.GetSpecificFeature2() as Component2;
            if (comp == null) continue;
            try
            {
                var childModel = comp.GetModelDoc() as ModelDoc2;
                var childType = childModel != null ? childModel.GetType() : -1;
                if (childType == targetType) result.Add(comp);
            }
            catch { }
        }
        result.Sort((a, b) => string.Compare(Safe(() => a.Name2), Safe(() => b.Name2), StringComparison.OrdinalIgnoreCase));
        return result;
    }

    private void SortComponentsInFolder(Feature folder, AssemblyDoc assemblyDoc)
    {
        var partComps = new List<Component2>();
        var asmComps = new List<Component2>();
        var supPartComps = new List<Component2>();
        var supAsmComps = new List<Component2>();

        var subFeat = folder.GetFirstSubFeature();
        while (subFeat != null)
        {
            if (subFeat.GetTypeName2() == "Reference")
            {
                var comp = subFeat.GetSpecificFeature2() as Component2;
                if (comp != null)
                {
                    var isSupEnv = Safe(() => comp.IsSuppressed()) || Safe(() => comp.IsEnvelope());
                    var childModel = comp.GetModelDoc() as ModelDoc2;
                    var childType = childModel != null ? childModel.GetType() : -1;
                    if (childType == (int)swDocumentTypes_e.swDocASSEMBLY)
                    {
                        if (isSupEnv) supAsmComps.Add(comp); else asmComps.Add(comp);
                    }
                    else if (childType == (int)swDocumentTypes_e.swDocPART)
                    {
                        if (isSupEnv) supPartComps.Add(comp); else partComps.Add(comp);
                    }
                }
            }
            subFeat = subFeat.GetNextSubFeature();
        }

        asmComps.Sort((a, b) => string.Compare(Safe(() => a.Name2), Safe(() => b.Name2), StringComparison.OrdinalIgnoreCase));
        partComps.Sort((a, b) => string.Compare(Safe(() => a.Name2), Safe(() => b.Name2), StringComparison.OrdinalIgnoreCase));
        supAsmComps.Sort((a, b) => string.Compare(Safe(() => a.Name2), Safe(() => b.Name2), StringComparison.OrdinalIgnoreCase));
        supPartComps.Sort((a, b) => string.Compare(Safe(() => a.Name2), Safe(() => b.Name2), StringComparison.OrdinalIgnoreCase));

        var all = new List<Component2>();
        all.AddRange(asmComps); all.AddRange(partComps);
        all.AddRange(supAsmComps); all.AddRange(supPartComps);

        if (all.Count > 1)
        {
            for (var i = 1; i < all.Count; i++)
                assemblyDoc.ReorderComponents(all[i], all[i - 1], 1);
        }
    }

    private void RecursiveSortSubAssemblies(List<Feature> feats, HashSet<string> processed)
    {
        foreach (var feat in feats)
        {
            if (feat.GetTypeName2() != "Reference") continue;
            var comp = feat.GetSpecificFeature2() as Component2;
            if (comp == null) continue;
            try
            {
                var model = comp.GetModelDoc() as ModelDoc2;
                if (model == null || model.GetType() != (int)swDocumentTypes_e.swDocASSEMBLY) continue;
                var path = model.GetPathName();
                if (string.IsNullOrWhiteSpace(path) || !processed.Add(path)) continue;
                SortSubAsmByFeatures(model, processed);
            }
            catch { }
        }
    }

    private void SortSubAsmByFeatures(ModelDoc2 asmModel, HashSet<string> processed)
    {
        try
        {
            var vFeats = (object[])asmModel.FeatureManager.GetFeatures(true);
            var refFeats = new List<Feature>();
            for (var i = 0; i < vFeats.Length; i++)
            {
                if (((Feature)vFeats[i]).GetTypeName2() == "Reference")
                    refFeats.Add((Feature)vFeats[i]);
            }

            var asmComps = CollectAndSort(refFeats, true);
            var partComps = CollectAndSort(refFeats, false);

            var all = new List<Component2>();
            all.AddRange(asmComps); all.AddRange(partComps);

            if (all.Count > 1)
            {
                var asmDoc = asmModel as AssemblyDoc;
                if (asmDoc != null)
                {
                    for (var i = 1; i < all.Count; i++)
                        asmDoc.ReorderComponents(all[i], all[i - 1], 1);
                }
            }

            RecursiveSortSubAssemblies(refFeats, processed);
        }
        catch { }
    }
```

- [ ] **Step 3: 验证编译**

Run: `dotnet build 外部程序.SwAddin/外部程序.SwAddin.csproj`

- [ ] **Step 4: Commit**

```bash
git add 外部程序.SwAddin/AddinHttpServer.Commands.cs
git commit -m "feat: add assembly commands (config-names, component-tree, sort)"
```

---

### Task 7: SW 命令实现（属性读写 + 包围盒 + 同步编码属性）

**Files:**
- Modify: `外部程序.SwAddin/AddinHttpServer.Commands.cs`

在文件末尾添加以下方法：

- [ ] **Step 1: 添加属性读写和同步编码属性**

```csharp
    // --- Read Properties ---

    private object ReadProperties(Dictionary<string, object> args)
    {
        var model = GetActiveModel();
        var configName = GetArgString(args, "configuration", "");
        var manager = model.Extension.get_CustomPropertyManager(configName);
        var values = new Dictionary<string, string>();

        try
        {
            var names = (string[])manager.GetNames();
            if (names != null)
            {
                foreach (var name in names)
                {
                    try
                    {
                        manager.Get5(name, false, false, out var val, out var _, false);
                        values[name] = val ?? "";
                    }
                    catch { }
                }
            }
        }
        catch { }

        return new { configuration = string.IsNullOrWhiteSpace(configName) ? "custom" : configName, properties = values };
    }

    // --- Write Properties ---

    private object WriteProperties(Dictionary<string, object> args)
    {
        var model = GetActiveModel();
        var configName = GetArgString(args, "configuration", "");
        var propsArg = args.TryGetValue("properties", out var p) ? p : null;
        var properties = propsArg as Dictionary<string, object>;
        if (properties == null) throw new InvalidOperationException("properties 不能为空");

        var manager = model.Extension.get_CustomPropertyManager(configName);
        var written = new List<string>();
        foreach (var kvp in properties)
        {
            manager.Add3(kvp.Key, 30, kvp.Value?.ToString() ?? "", 2);
            written.Add(kvp.Key);
        }

        try { model.SetSaveFlag(); } catch { }

        return new { configuration = configName, written = written.ToArray() };
    }

    // --- Get Bounding Box ---

    private object GetBoundingBox(Dictionary<string, object> args)
    {
        var model = GetActiveModel();
        var configName = GetArgString(args, "configuration", "");

        if (!string.IsNullOrWhiteSpace(configName))
        {
            try
            {
                model.ShowConfiguration2(configName);
            }
            catch { }
    }

        var box = (double[])model.GetPartBox(false);
        if (box == null || box.Length < 6) throw new InvalidOperationException("无法获取包围盒");

        return new
        {
            x1 = box[0], y1 = box[1], z1 = box[2],
            x2 = box[3], y2 = box[4], z2 = box[5],
            dx = box[3] - box[0],
            dy = box[4] - box[1],
            dz = box[5] - box[2]
        };
    }

    // --- Sync Coding Props ---

    private object SyncCodingProps()
    {
        var model = GetActiveModel();
        var configName = Safe(() => model.ConfigurationManager.ActiveConfiguration.Name);

        var title = model.GetTitle();
        var dotIdx = title.IndexOf(".");
        if (dotIdx > 0) title = title.Substring(0, dotIdx);

        var materialCode = Safe(() => model.GetCustomInfoValue(configName, "物料编码"));
        var partNumber = Safe(() => model.GetCustomInfoValue(configName, "零件图号"));

        if (title != materialCode || title != partNumber)
        {
            var config = model.GetActiveConfiguration();
            var cusPropMgr = config.CustomPropertyManager;
            cusPropMgr.Add3("物料编码", 30, title, 2);
            cusPropMgr.Add3("零件图号", 30, title, 2);
            cusPropMgr.Add3("文件名称", 30, title, 2);
            model.SetSaveFlag();
            return new { synced = true, title, materialCode, partNumber };
        }

        return new { synced = false, title, materialCode, partNumber, message = "已同步，无需更新" };
    }
```

- [ ] **Step 2: 添加重命名和编码清理命令**

```csharp
    // --- Rename Component ---

    private object RenameComponent(Dictionary<string, object> args)
    {
        var model = GetActiveModel();
        var newName = GetArgString(args, "newName");
        var saveAs = GetArgString(args, "saveAs", "");
        var targetPath = GetArgString(args, "targetPath", "");

        if (string.IsNullOrWhiteSpace(newName)) throw new InvalidOperationException("newName 不能为空");

        // Get selected component
        Component2 comp = null;
        try
        {
            comp = model.SelectionManager.GetSelectedObjectsComponent(1) as Component2;
        }
        catch { }
        if (comp == null) throw new InvalidOperationException("请先选中一个组件");

        var oldName = Safe(() => comp.Name2);
        var compPath = Safe(() => comp.GetPathName());

        // Rename
        if (!string.IsNullOrWhiteSpace(targetPath))
        {
            comp.SetName2(newName, 1);
        }
        else
        {
            comp.SetName2(newName, 0);
        }

        model.SetSaveFlag();

        // SaveAs if requested
        var savedAsPath = "";
        if (!string.IsNullOrWhiteSpace(saveAs))
        {
            try
            {
                var refModel = comp.GetModelDoc() as ModelDoc2;
                if (refModel != null)
                {
                    var saveDir = Path.GetDirectoryName(model.GetPathName());
                    var destPath = Path.Combine(saveDir, saveAs);
                    if (!destPath.EndsWith(".sldprt") && !destPath.EndsWith(".sldasm"))
                        destPath += Path.GetExtension(compPath);
                    refModel.SaveAs3(destPath, 0, 2);
                    savedAsPath = destPath;
                }
            }
            catch { }
        }

        return new { oldName, newName, compPath, savedAsPath };
    }

    // --- Coding Cleanup ---

    private object CodingCleanup(Dictionary<string, object> args)
    {
        var model = GetActiveModel();
        var nameFilter = GetArgString(args, "nameFilter", "");
        var processAsm = GetArgString(args, "processAsm", "true") == "true";
        var processPart = GetArgString(args, "processPart", "true") == "true";
        var excludeVirtual = GetArgString(args, "excludeVirtual", "true") == "true";
        var excludeStandard = GetArgString(args, "excludeStandard", "true") == "true";
        var excludePurchased = GetArgString(args, "excludePurchased", "true") == "true";

        if (model.GetType() != (int)swDocumentTypes_e.swDocASSEMBLY)
            throw new InvalidOperationException("请在装配体环境下使用");

        var configuration = model.GetActiveConfiguration();
        var rootComponent = configuration.GetRootComponent();
        var comps = (object[])rootComponent.GetChildren();

        var results = new List<object>();
        ProcessCodingCleanup(comps, nameFilter, processAsm, processPart, excludeVirtual, excludeStandard, excludePurchased, results);

        model.EditRebuild3();
        return new { processed = results.Count, results };
    }

    private void ProcessCodingCleanup(object[] comps, string nameFilter, bool processAsm, bool processPart,
        bool excludeVirtual, bool excludeStandard, bool excludePurchased, List<object> results)
    {
        foreach (Component2 child in comps)
        {
            try
            {
                var childModel = child.GetModelDoc() as ModelDoc2;
                if (childModel == null) continue;

                var childType = childModel.GetType();
                var childName = Safe(() => child.Name2);
                var childPath = Safe(() => child.GetPathName());

                if (!string.IsNullOrWhiteSpace(nameFilter) && childName != null && !childName.Contains(nameFilter)) continue;

                var isVirtual = Safe(() => child.IsVirtual);
                if (excludeVirtual && isVirtual) continue;

                if (childType == (int)swDocumentTypes_e.swDocPART && !processPart) continue;
                if (childType == (int)swDocumentTypes_e.swDocASSEMBLY && !processAsm) continue;

                var configName = Safe(() => child.ReferencedConfiguration);
                if (string.IsNullOrWhiteSpace(configName)) configName = "";

                var title = Safe(() => childModel.GetTitle());
                var dotIdx = title.IndexOf(".");
                if (dotIdx > 0) title = title.Substring(0, dotIdx);

                var materialCode = Safe(() => childModel.GetCustomInfoValue(configName, "物料编码"));
                var partNumber = Safe(() => childModel.GetCustomInfoValue(configName, "零件图号"));

                if (title != materialCode || title != partNumber)
                {
                    var cusPropMgr = childModel.Extension.get_CustomPropertyManager(configName);
                    cusPropMgr.Add3("物料编码", 30, title, 2);
                    cusPropMgr.Add3("零件图号", 30, title, 2);
                    cusPropMgr.Add3("文件名称", 30, title, 2);
                    childModel.SetSaveFlag();
                    results.Add(new { name = childName, title, materialCode, partNumber, action = "synced" });
                }

                // Recurse into subassemblies
                if (childType == (int)swDocumentTypes_e.swDocASSEMBLY)
                {
                    var subConfig = childModel.GetActiveConfiguration();
                    var subRoot = subConfig.GetRootComponent();
                    var subComps = (object[])subRoot.GetChildren();
                    ProcessCodingCleanup(subComps, nameFilter, processAsm, processPart, excludeVirtual, excludeStandard, excludePurchased, results);
            }
            }
            catch { }
        }
    }
```

- [ ] **Step 3: 验证编译**

Run: `dotnet build 外部程序.SwAddin/外部程序.SwAddin.csproj`

- [ ] **Step 4: Commit**

```bash
git add 外部程序.SwAddin/AddinHttpServer.Commands.cs
git commit -m "feat: add property, bounding-box, rename, coding-cleanup commands"
```

---

### Task 8: 在外部程序中创建 SwAddinClient（VB.NET HTTP + WebSocket 客户端）

**Files:**
- Create: `SwAddinClient.vb`

- [ ] **Step 1: 编写 SwAddinClient.vb**

```vb
Imports System.Net.Http
Imports System.Net.WebSockets
Imports System.Text
Imports System.Threading
Imports System.Web.Script.Serialization

Public Class SwAddinClient
    Implements IDisposable

    Public Event DocChanged As Action(Of String, String) ' (title, path)
    Public Event SelectionChanged As Action(Of String, String) ' (name, type)
    Public Event Disconnected As Action()

    Private ReadOnly _http As New HttpClient() With {.Timeout = TimeSpan.FromSeconds(30)}
    Private ReadOnly _json As New JavaScriptSerializer()
    Private ReadOnly _baseUrl As String
    Private ReadOnly _wsUrl As String
    Private _ws As ClientWebSocket
    Private _wsCts As CancellationTokenSource
    Private _pingInterval As Integer = 5 ' seconds fallback
    Private _pingTimer As Threading.Timer

    Public Sub New(Optional port As Integer = 32128)
        _baseUrl = $"http://127.0.0.1:{port}/"
        _wsUrl = $"ws://127.0.0.1:{port}/events"
    End Sub

    Public Async Function ConnectAsync() As Task(Of Boolean)
        Try
            ' Verify HTTP connectivity first
            Dim pingResult = Await SendCommandAsync("ping")
            If pingResult Is Nothing Then Return False

            ' Connect WebSocket
            _wsCts = New CancellationTokenSource()
            _ws = New ClientWebSocket()
            Await _ws.ConnectAsync(New Uri(_wsUrl), _wsCts.Token)
            StartWsReadLoop()
            Return True
        Catch ex As Exception
            Debug.WriteLine($"SwAddinClient: WebSocket failed, falling back to HTTP ping: {ex.Message}")
            ' Fallback to HTTP polling
            StartHttpPolling()
            Return True ' HTTP ping succeeded, consider it connected
        End Try
    End Function

    Private Sub StartWsReadLoop()
        Task.Run(Async Function()
            Dim buffer As Byte() = New Byte(4095) {}
            While _ws IsNot Nothing AndAlso _ws.State = WebSocketState.Open
                Try
                    Dim result = Await _ws.ReceiveAsync(New ArraySegment(Of Byte)(buffer), _wsCts.Token)
                    If result.MessageType = WebSocketMessageType.Close Then Exit While
                    If result.MessageType = WebSocketMessageType.Text Then
                        Dim json = Encoding.UTF8.GetString(buffer, 0, result.Count)
                        ProcessWsMessage(json)
                    End If
                Catch ex As Exception
                    Exit While
                End Try
            End While
            ' Reconnect
            Debug.WriteLine("SwAddinClient: WebSocket disconnected, attempting reconnect...")
            Await ReconnectWsAsync()
        End Function)
    End Sub

    Private Async Function ReconnectWsAsync() As Task
        For attempt As Integer = 1 To 3
            Try
                Await Task.Delay(1000 * Math.Pow(2, attempt - 1))
                _ws?.Dispose()
                _ws = New ClientWebSocket()
                _wsCts?.Cancel()
                _wsCts = New CancellationTokenSource()
                Await _ws.ConnectAsync(New Uri(_wsUrl), _wsCts.Token)
                StartWsReadLoop()
                Debug.WriteLine($"SwAddinClient: WebSocket reconnected on attempt {attempt}")
                Return
            Catch ex As Exception
                Debug.WriteLine($"SwAddinClient: WS reconnect attempt {attempt} failed: {ex.Message}")
            End Try
        Next
        Debug.WriteLine("SwAddinClient: WS reconnect exhausted, falling back to HTTP polling")
        RaiseEvent Disconnected()
        StartHttpPolling()
    End Function

    Private Sub StartHttpPolling()
        _pingTimer = New Threading.Timer(
            Async Sub(state)
                Try
                    Dim info = Await SendCommandAsync("active-document")
                    If info IsNot Nothing Then
                        ProcessDocInfo(info)
                    End If
                Catch ex As Exception
                    Debug.WriteLine($"SwAddinClient: HTTP poll failed: {ex.Message}")
                End Try
            End Sub, Nothing, 5000, 5000)
    End Sub

    Private Sub ProcessWsMessage(json As String)
        Try
            Dim msg = _json.Deserialize(Of Dictionary(Of String, Object))(json)
            If msg Is Nothing Then Return
            Dim msgType As String = If(msg.ContainsKey("type"), msg("type")?.ToString(), "")
            Select Case msgType
                Case "ping"
                    ' Heartbeat, ignore
                Case "doc-changed"
                    Dim data = TryCast(msg("data"), Dictionary(Of String, Object))
                    If data IsNot Nothing Then
                        Dim title = If(data.ContainsKey("title"), data("title")?.ToString(), "")
                        Dim path = If(data.ContainsKey("path"), data("path")?.ToString(), "")
                        RaiseEvent DocChanged(title, path)
                    End If
                Case "selection-changed"
                    Dim data = TryCast(msg("data"), Dictionary(Of String, Object))
                    If data IsNot Nothing Then
                        Dim name = If(data.ContainsKey("name"), data("name")?.ToString(), "")
                        Dim sType = If(data.ContainsKey("type"), data("type")?.ToString(), "")
                        RaiseEvent SelectionChanged(name, sType)
                    End If
                Case "sw-shutdown"
                    RaiseEvent Disconnected()
            End Select
        Catch ex As Exception
            Debug.WriteLine($"SwAddinClient: WS message parse error: {ex.Message}")
        End Try
    End Sub

    Private Sub ProcessDocInfo(info As Object)
        Try
            Dim dict = TryCast(info, Dictionary(Of String, Object))
            If dict IsNot Nothing Then
                Dim title = If(dict.ContainsKey("title"), dict("title")?.ToString(), "")
                Dim path = If(dict.ContainsKey("path"), dict("path")?.ToString(), "")
                RaiseEvent DocChanged(title, path)
            End If
        Catch
        End Try
    End Sub

    Public Async Function SendCommandAsync(command As String, Optional args As Dictionary(Of String, Object) = Nothing) As Task(Of Object)
        Try
            Dim req = New Dictionary(Of String, Object) From {
                {"Command", command},
                {"Args", If(args, New Dictionary(Of String, Object)())}
            }
            Dim json = _json.Serialize(req)
            Dim content = New StringContent(json, Encoding.UTF8, "application/json")
            Dim response = Await _http.PostAsync(_baseUrl & "command", content)
            Dim body = Await response.Content.ReadAsStringAsync()
            Dim result = _json.Deserialize(Of Dictionary(Of String, Object))(body)

            If result IsNot Nothing AndAlso result.ContainsKey("ok") AndAlso CBool(result("ok")) Then
                Return If(result.ContainsKey("data"), result("data"), Nothing)
            Else
                Dim errMsg = If(result IsNot Nothing AndAlso result.ContainsKey("error"), result("error")?.ToString(), "未知错误")
                Throw New InvalidOperationException(errMsg)
            End If
        Catch ex As Exception
            Throw
        End Try
    End Function

    Public Sub Dispose() Implements IDisposable.Dispose
        _pingTimer?.Dispose()
        _pingTimer = Nothing
        _wsCts?.Cancel()
        Try
            _ws?.CloseAsync(WebSocketCloseStatus.NormalClosure, "", CancellationToken.None).Wait(1000)
        Catch
        End Try
        _ws?.Dispose()
        _ws = Nothing
        _http?.Dispose()
    End Sub
End Class
```

- [ ] **Step 2: 验证编译**

Run: `dotnet build 外部程序.vbproj`

- [ ] **Step 3: Commit**

```bash
git add SwAddinClient.vb
git commit -m "feat: add SwAddinClient with HTTP + WebSocket support"
```

---

### Task 9: 修改外部程序.vbproj——移除 COM 依赖，添加构建集成

**Files:**
- Modify: `外部程序.vbproj`

- [ ] **Step 1: 移除 Interop 引用，添加 SwAddinClient.vb 编译项**

在 `外部程序.vbproj` 中：
- 删除 `Interop.SldWorks` 和 `Interop.SwConst` 两个 Reference
- 添加 `SwAddinClient.vb` 到 Compile 项
- 在 PostBuild 中添加复制插件输出的逻辑

Run the Edit tool to remove these two lines:
```xml
    <Reference Include="Interop.SldWorks">
      <HintPath>lib\Interop.SldWorks.dll</HintPath>
      <EmbedInteropTypes>True</EmbedInteropTypes>
    </Reference>
    <Reference Include="Interop.SwConst">
      <HintPath>lib\Interop.SwConst.dll</HintPath>
      <EmbedInteropTypes>True</EmbedInteropTypes>
    </Reference>
```

Replace PostBuildEvent with:
```xml
    <PostBuildEvent>
      xcopy /Y /D "$(SolutionDir)外部程序.SwAddin\bin\Debug\net48\*.dll" "$(TargetDir)"
      xcopy /Y /D "$(SolutionDir)外部程序.SwAddin\bin\Debug\net48\*.pdb" "$(TargetDir)" 2>nul
      if not exist "C:\Users\yuan7\OneDrive\软件包" mkdir "C:\Users\yuan7\OneDrive\软件包"
      xcopy /Y /D "$(TargetDir)*" "C:\Users\yuan7\OneDrive\软件包\"
      exit 0
    </PostBuildEvent>
```

Add `SwAddinClient.vb` to Compile items.

- [ ] **Step 2: 验证编译**

Run: `dotnet build 外部程序.vbproj`
Expected: Compilation errors about missing SldWorks types (to be fixed in subsequent tasks)

- [ ] **Step 3: Commit**

```bash
git add 外部程序.vbproj
git commit -m "refactor: remove COM interop refs, add SwAddinClient compile item, update post-build"
```

---

### Task 10: 重构 Form1.vb —— 移除 COM 依赖，使用 SwAddinClient

**Files:**
- Modify: `Form1.vb`

This is the largest task. We'll remove all `Imports SwConst`, `SldWorks.*` types, COM connection logic, process selection combo, and replace with `SwAddinClient`.

- [ ] **Step 1: 移除 Imports 和 COM 相关字段**

移除以下 Imports:
```vb
Imports SwConst
```

移除 COM 相关字段和 Win32 P/Invoke（已不需要）：
```vb
' Remove: _swApp, GetSelectedSwApp, PopulateSolidWorksProcesses, swProcessCombo, 
' ConnectToSelectedSw, RegisterHotKey/UnregisterHotKey usage for SW connection,
' SetForegroundWindow, ShowWindow, IsIconic, GetWindowText
```

添加：
```vb
Private _client As SwAddinClient
```

- [ ] **Step 2: 修改 Form1_Load 和初始化逻辑**

```vb
Private Async Sub Form1_Load(sender As Object, e As Wpf.RoutedEventArgs)
    TopMost = True
    Button12.Content = "取消置顶"

    ' Initialize tray icon (unchanged)
    _trayMenu = New WinForms.ContextMenuStrip()
    _trayMenu.Items.Add("显示主窗口", Nothing, AddressOf TrayShow_Click)
    _trayMenu.Items.Add("-")
    _trayMenu.Items.Add("退出", Nothing, AddressOf TrayExit_Click)

    _trayIcon = New WinForms.NotifyIcon() With {
        .Icon = Drawing.SystemIcons.Application,
        .Text = "外部程序",
        .Visible = True,
        .ContextMenuStrip = _trayMenu
    }
    AddHandler _trayIcon.DoubleClick, AddressOf TrayShow_Click

    ' Connect to addin
    _client = New SwAddinClient()
    AddHandler _client.DocChanged, AddressOf OnAddinDocChanged
    AddHandler _client.SelectionChanged, AddressOf OnAddinSelectionChanged
    AddHandler _client.Disconnected, AddressOf OnAddinDisconnected
    Await _client.ConnectAsync()
End Sub

Private Sub OnAddinDocChanged(title As String, path As String)
    Dispatcher.Invoke(Sub()
        If String.IsNullOrWhiteSpace(path) Then
            fileNameLabel.Text = If(String.IsNullOrWhiteSpace(title), "无文档", title)
        Else
            fileNameLabel.Text = IO.Path.GetFileNameWithoutExtension(path)
        End If
    End Sub)
End Sub

Private Sub OnAddinSelectionChanged(name As String, type As String)
    Dispatcher.Invoke(Sub()
        If Not String.IsNullOrWhiteSpace(name) Then
            fileNameLabel.Text = IO.Path.GetFileNameWithoutExtension(name)
        End If
    End Sub)
End Sub

Private Sub OnAddinDisconnected()
    Dispatcher.Invoke(Sub()
        fileNameLabel.Text = "插件断开"
    End Sub)
End Sub
```

- [ ] **Step 3: 修改按钮事件处理器——使用 _client.SendCommandAsync**

将所有 SW 操作替换为 HTTP 调用。关键改动：

```vb
' Button1: Save DWG
Private Async Sub Button1_Click(sender As Object, e As EventArgs)
    Try
        Await _client.SendCommandAsync("save-dwg")
        fileNameLabel.Text = "已另存 DWG"
    Catch ex As Exception
        MsgBox("保存 DWG 失败: " & ex.Message)
    End Try
End Sub

' Button2: Save PDF
Private Async Sub Button2_Click(sender As Object, e As EventArgs)
    Try
        Await _client.SendCommandAsync("save-pdf")
        fileNameLabel.Text = "已另存 PDF"
    Catch ex As Exception
        MsgBox("保存 PDF 失败: " & ex.Message)
    End Try
End Sub

' Button4: Open file location
Private Async Sub Button4_Click(sender As Object, e As EventArgs)
    Try
        Dim result = Await _client.SendCommandAsync("open-file-location")
        Dim dict = TryCast(result, Dictionary(Of String, Object))
        If dict IsNot Nothing AndAlso dict.ContainsKey("path") Then
            OpenPathInExplorer(dict("path").ToString())
        End If
    Catch ex As Exception
        MsgBox("获取路径失败: " & ex.Message)
    End Try
End Sub

' Button5: Rotate view
Private Async Sub Button5_Click(sender As Object, e As EventArgs)
    Try
        Await _client.SendCommandAsync("rotate-drawing-view")
    Catch ex As Exception
        MsgBox("旋转视图失败: " & ex.Message)
    End Try
End Sub

' Button8: Sync coding props
Private Async Sub Button8_Click(sender As Object, e As EventArgs)
    Try
        Await _client.SendCommandAsync("sync-coding-props")
    Catch ex As Exception
        MsgBox("同步属性失败: " & ex.Message)
    End Try
End Sub

' Button11: Hide config names
Private Async Sub Button11_Click(sender As Object, e As EventArgs)
    Try
        Await _client.SendCommandAsync("hide-config-names")
        ShowAutoCloseNotice("完成")
    Catch ex As Exception
        MsgBox("操作失败: " & ex.Message)
    End Try
End Sub

' Button13: Sort components
Private Async Sub Button13_Click(sender As Object, e As EventArgs)
    ShowSortProgress("正在准备装配体排序...")
    Try
        Await _client.SendCommandAsync("sort-components")
        ShowAutoCloseNotice("装配体排序完成")
    Catch ex As Exception
        MessageBox.Show("装配体排序失败: " & ex.Message, "错误", MessageBoxButtons.OK, MessageBoxIcon.Error)
    Finally
        CloseSortProgress()
    End Try
End Sub
```

- [ ] **Step 4: 移除不再需要的方法**

删除以下方法（其逻辑已迁移到插件）：
- `PopulateSolidWorksProcesses`
- `GetSelectedSwApp`
- `ConnectToSelectedSw`
- `SaveActiveDrawingAsDwg`
- `SaveActiveDrawingAsPdf`
- `SaveActiveDrawingAs`
- `RotateSelectedDrawingView`
- `GetActiveOrSelectedModelPath`
- `SubAsmsjs`
- `SortComponentsInFolder`
- `CollectComponent`
- `GetComponentDocType`
- `SortComponentsByName`
- `RecursiveSortSubAssemblies`
- `SortSubAsmByFeatures`
- `MarkDocDirty`
- `SetForegroundWindow`/`ShowWindow`/`IsIconic`/`GetWindowText` P/Invoke
- 所有 `SldWorks.*` 类型声明

保留：
- `Form1` 类自身（Window management）
- 托盘图标逻辑
- 置顶切换
- `StatusTimer` → 改为依赖 WebSocket 推送，保留 timer 仅用于兜底
- `OpenPathInExplorer`
- `ShowSortProgress`/`UpdateSortProgress`/`CloseSortProgress`/`ShowAutoCloseNotice`
- 进度条逻辑
- `RefreshProcessList` → 改为刷新插件连接状态
- `UpdateStatusBar` → 改为检查 SwAddinClient 状态

- [ ] **Step 5: 移除 swProcessCombo 和相关 UI**

删除 XAML 中的进程选择 ComboBox 及 Form1.vb 中的所有相关事件处理器。

- [ ] **Step 6: UpdateStatusBar 改为依赖推送**

```vb
Private Sub UpdateStatusBar()
    ' Status is now updated via WebSocket events (OnAddinDocChanged, etc.)
    ' This timer is kept as a low-frequency fallback only
    If _client IsNot Nothing Then
        Try
            _client.SendCommandAsync("active-document").ContinueWith(Sub(t)
                If Not t.IsFaulted AndAlso t.Result IsNot Nothing Then
                    Dim dict = TryCast(t.Result, Dictionary(Of String, Object))
                    If dict IsNot Nothing Then
                        Dispatcher.Invoke(Sub()
                            Dim path = If(dict.ContainsKey("path"), dict("path")?.ToString(), "")
                            Dim title = If(dict.ContainsKey("title"), dict("title")?.ToString(), "")
                            If Not String.IsNullOrWhiteSpace(path) Then
                                fileNameLabel.Text = IO.Path.GetFileNameWithoutExtension(path)
                            ElseIf Not String.IsNullOrWhiteSpace(title) Then
                                fileNameLabel.Text = title
                            End If
                        End Sub)
                    End If
                End If
            End Sub)
        Catch
        End Try
    End If
End Sub
```

- [ ] **Step 7: 添加 Form1_Closed 清理**

```vb
Private Sub Form1_Closed(sender As Object, e As EventArgs)
    ' Cleanup hotkey
    If _mainWindowHandle <> IntPtr.Zero Then UnregisterHotKey(_mainWindowHandle, HotkeyId)

    ' Cleanup client
    _client?.Dispose()
    _client = Nothing

    ' Cleanup tray icon (unchanged)
    If _trayIcon IsNot Nothing Then
        _trayIcon.Visible = False
        _trayIcon.Dispose()
        _trayIcon = Nothing
    End If
    If _trayMenu IsNot Nothing Then
        _trayMenu.Dispose()
        _trayMenu = Nothing
    End If
    If _statusTimer IsNot Nothing Then
        _statusTimer.Stop()
        _statusTimer.Dispose()
        _statusTimer = Nothing
    End If
End Sub
```

- [ ] **Step 8: 验证编译**

Run: `dotnet build 外部程序.vbproj`
Expected: Build succeeded, no SldWorks types referenced

- [ ] **Step 9: Commit**

```bash
git add Form1.vb Form1.xaml
git commit -m "refactor: migrate Form1 from direct COM to SwAddinClient HTTP/WS"
```

---

### Task 11: 重构 PropertyOverlayWindow.vb 和 RenameWindow

**Files:**
- Modify: `PropertyOverlayWindow.vb`
- Modify: `RenameWindow.xaml.vb`

- [ ] **Step 1: 将 PropertyOverlayWindow 改为接收 ISwAddinClient 或静态客户端**

PropertyOverlayWindow 当前持有 `SwApp As SldWorks.SldWorks` 并直接调用 COM。改为：
- 添加 `Public Property Client As SwAddinClient`
- 所有 `SwApp` 调用替换为 `_client.SendCommandAsync("read-properties", ...)` 和 `_client.SendCommandAsync("write-properties", ...)`
- 移除 `Imports SwConst` 和 `Imports System.Runtime.InteropServices`
- 移除所有 SldWorks 类型字段

- [ ] **Step 2: 同样更新 RenameWindow**

RenameWindow 也有 `SwApp As SldWorks.SldWorks` 属性和直接 COM 事件订阅。改为：
- 添加 `Public Property Client As SwAddinClient`
- 重命名操作改为 `_client.SendCommandAsync("rename-component", ...)`
- 移除 COM 事件订阅 (`SwAppField_ActiveDocChangeNotify`, `SwAppField_ActiveModelDocChangeNotify`)
- 改为通过 WebSocket 的 `selection-changed` 事件更新选中状态
- 移除 `Imports SwConst` 和 SldWorks 类型

- [ ] **Step 3: 更新 CodingCleanupWindow**

- 添加 `Public Property Client As SwAddinClient`
- 执行按钮改为 `_client.SendCommandAsync("coding-cleanup", ...)`
- 移除 `Imports SwConst`
- 移除 `SwApp As SldWorks.SldWorks` 属性

- [ ] **Step 4: 更新 Form1 中创建这些窗口的代码**

Form1 中打开这些窗口的按钮事件需要传递 `_client` 引用：
```vb
' Example for PropertyOverlayWindow
Dim window = New PropertyOverlayWindow()
window.Client = _client
window.Show()
```

- [ ] **Step 5: 验证编译**

Run: `dotnet build 外部程序.vbproj`

- [ ] **Step 6: Commit**

```bash
git add PropertyOverlayWindow.vb RenameWindow.xaml.vb CodingCleanupWindow.xaml.vb Form1.vb
git commit -m "refactor: migrate PropertyOverlay, Rename, CodingCleanup windows to SwAddinClient"
```

---

### Task 12: 构建验证 + 集成测试

**Files:**
- 无新文件

- [ ] **Step 1: 完整编译解决方案**

Run: `dotnet build 外部程序.sln`
Expected: 两个项目都编译成功，0 errors

- [ ] **Step 2: 验证插件 DLL 输出**

Run: `ls 外部程序.SwAddin/bin/Debug/net48/`
Expected: 包含 `外部程序.SwAddin.dll`, `Interop.SldWorks.dll`, `Interop.SwConst.dll`, `SolidWorks.Interop.swpublished.dll`, `SolidWorksTools.dll`

- [ ] **Step 3: 验证外部程序构建输出包含插件**

Run: `dotnet build 外部程序.vbproj` → check `bin/Debug/` contains `外部程序.SwAddin.dll`

- [ ] **Step 4: 代码审查检查项**

- [ ] Form1.vb 无残留 `SldWorks.` 类型引用
- [ ] 无 `Imports SwConst`
- [ ] 无 `Interop.SldWorks` / `Interop.SwConst` 引用在 vbproj 中
- [ ] 所有 SW 操作通过 `_client.SendCommandAsync` 调用
- [ ] WebSocket 推送事件处理正确
- [ ] 断线重连逻辑完整

- [ ] **Step 5: Commit（如有小修改）**

```bash
git add -A
git diff --cached --stat
# Commit if any fixes
```

---

### Task 13: 插件的 WebSocket 事件推送集成

**Files:**
- Modify: `外部程序.SwAddin/AddinHttpServer.Commands.cs`
- Modify: `外部程序.SwAddin/AddinHttpServer.cs`

在插件中添加文档切换和选择变化的检测与推送。

- [ ] **Step 1: 在 AddinHttpServer 中添加 SW 事件订阅**

在 `AddinHttpServer.cs` 的 `Start()` 方法后添加事件订阅，在 `Dispose()` 中取消订阅。

我们需要订阅 SolidWorks 的 `ActiveDocChangeNotify` 和选择变化。由于插件本身不直接暴露这些事件，改为在每个命令执行后检查文档/选择状态变化，并在 `GetActiveDocumentInfo`、`Save`、`OpenDocument` 等操作后广播事件。

在 `AddinHttpServer.cs` 类中添加：

```csharp
private string _lastDocPath;
private string _lastSelectionName;

private void CheckAndBroadcastDocChange()
{
    try
    {
        var model = _swApp.ActiveDoc as ModelDoc2;
        var currentPath = model != null ? Safe(() => model.GetPathName()) ?? "" : "";
        if (currentPath != _lastDocPath)
        {
            _lastDocPath = currentPath;
            var title = model != null ? Safe(() => model.GetTitle()) ?? "" : "";
            BroadcastEvent("doc-changed", new { title, path = currentPath });
        }
    }
    catch { }
}
```

- [ ] **Step 2: 在关键命令执行后调用 CheckAndBroadcastDocChange**

在 `ExecuteCommand` 中，`open-document`、`save` 命令执行后调用 `CheckAndBroadcastDocChange()`。
在 `GetActiveDocumentInfo` 方法末尾添加选择检查。

- [ ] **Step 3: 验证编译**

Run: `dotnet build 外部程序.SwAddin/外部程序.SwAddin.csproj`

- [ ] **Step 4: Commit**

```bash
git add 外部程序.SwAddin/AddinHttpServer.cs 外部程序.SwAddin/AddinHttpServer.Commands.cs
git commit -m "feat: add doc-change event broadcasting to WebSocket"
```
