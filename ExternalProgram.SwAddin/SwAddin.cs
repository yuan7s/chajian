using System;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Win32;
using SolidWorksTools;
using SwConst;

namespace ExternalProgram.SwAddin;

[ComVisible(true)]
[Guid("C8F7A3D2-6B51-4E92-A814-7F2D3C1E9A56")]
[ProgId("ExternalProgram.SwAddin")]
[SwAddin(Description = AddinDescription, Title = AddinTitle, LoadAtStartup = true)]
public sealed class SwAddin : SolidWorks.Interop.swpublished.SwAddin
{
    private const string AddinTitle = "External Program Add-in";
    private const string AddinDescription = "External Program local command bridge for SolidWorks.";
    private const int ExternalBridgeBasePort = 32128;
    private const int ReadBomBridgeBasePort = 32127;
    private const int PortScanCount = 60;
    private const string RuntimeAssemblyFileName = "ExternalProgram.SwAddin.Runtime.dll";
    private const string RuntimeTypeName = "ExternalProgram.SwAddin.SwAddinRuntime";
    private const string RuntimeAssemblyPathDataName = "ExternalProgram.SwAddin.RuntimeAssemblyPath";
    private const string RuntimeInstanceDataName = "ExternalProgram.SwAddin.RuntimeInstance";
    private const string SwAppDataName = "ExternalProgram.SwAddin.SwApp";
    private const string SwAppDirectDataName = "ExternalProgram.SwAddin.SwAppDirect";
    private const string SwAppUnkDataName = "ExternalProgram.SwAddin.SwAppUnk";
    private const string ShellInstanceDataName = "ExternalProgram.SwAddin.ShellInstance";
    private const string ComBridgeDataName = "ExternalProgram.SwAddin.ComBridge";
    private const string ExternalBridgeBasePortDataName = "ExternalProgram.SwAddin.ExternalBridgeBasePort";
    private const string ReadBomBridgeBasePortDataName = "ExternalProgram.SwAddin.ReadBomBridgeBasePort";
    private const string PortScanCountDataName = "ExternalProgram.SwAddin.PortScanCount";
    private const string AddinDirectoryDataName = "ExternalProgram.SwAddin.AddinDirectory";
    private const string ReadBomAddinDirectoryDataName = "ReadBom.SwAddin.AddinDirectory";

    private readonly object _runtimeLock = new object();
    private SldWorks.SldWorks _swApp;
    private int _cookie;
    private Control _mainThreadControl;
    private AppDomain _runtimeDomain;
    private string _runtimeShadowDirectory;
    private FileSystemWatcher _runtimeWatcher;
    private System.Threading.Timer _reloadTimer;
    private bool _disconnecting;

    public bool ConnectToSW(object thisSw, int cookie)
    {
        try
        {
            _disconnecting = false;
            AddinLog.Write("ConnectToSW called");
            var assemblyPath = Assembly.GetExecutingAssembly().Location;
            AddinLog.Write($"Addin shell assembly: {assemblyPath}, lastWrite={File.GetLastWriteTime(assemblyPath):yyyy-MM-dd HH:mm:ss}");
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

            StartRuntimeWatcher();
            RestartRuntime("connect");
            return true;
        }
        catch (Exception ex)
        {
            AddinLog.Write("ConnectToSW failed: " + ex);
            StopRuntime();
            StopRuntimeWatcher();
            DisposeMainThreadControl();
            return false;
        }
    }

    public bool DisconnectFromSW()
    {
        AddinLog.Write("DisconnectFromSW called");
        _disconnecting = true;
        StopRuntimeWatcher();
        StopRuntime();
        DisposeMainThreadControl();
        _swApp = null;
        _cookie = 0;
        return true;
    }

    private void RestartRuntime(string reason)
    {
        if (_disconnecting || _swApp == null)
        {
            AddinLog.Write("Runtime restart skipped while disconnected: " + reason);
            return;
        }

        lock (_runtimeLock)
        {
            StopRuntimeCore();
            StartRuntimeCore(reason);
        }
    }

    private void StopRuntime()
    {
        lock (_runtimeLock)
        {
            StopRuntimeCore();
        }
    }

    private void StartRuntimeCore(string reason)
    {
        var runtimePath = GetRuntimeAssemblyPath();
        if (!File.Exists(runtimePath))
        {
            AddinLog.Write("Runtime assembly not found: " + runtimePath);
            return;
        }

        AppDomain runtimeDomain = null;
        string shadowDirectory = null;
        try
        {
            var runtimeDirectory = Path.GetDirectoryName(runtimePath);
            if (string.IsNullOrWhiteSpace(runtimeDirectory))
                throw new InvalidOperationException("Runtime directory is empty.");

            shadowDirectory = CreateRuntimeShadowCopy(runtimeDirectory);
            var shadowRuntimePath = Path.Combine(shadowDirectory, RuntimeAssemblyFileName);
            var setup = new AppDomainSetup
            {
                ApplicationBase = AddinLog.DirectoryPath,
                PrivateBinPath = shadowDirectory,
                ConfigurationFile = AppDomain.CurrentDomain.SetupInformation.ConfigurationFile
            };

            runtimeDomain = AppDomain.CreateDomain(
                "ExternalProgram.SwAddin.Runtime." + DateTime.Now.ToString("yyyyMMddHHmmssfff"),
                null,
                setup);

            runtimeDomain.SetData(RuntimeAssemblyPathDataName, shadowRuntimePath);
            runtimeDomain.SetData(SwAppDataName, _swApp);
            var swAppUnk = Marshal.GetIUnknownForObject(_swApp);
            runtimeDomain.SetData(SwAppUnkDataName, swAppUnk);
            runtimeDomain.SetData(ComBridgeDataName, new SwComBridge(_swApp));
            runtimeDomain.SetData(ExternalBridgeBasePortDataName, ExternalBridgeBasePort);
            runtimeDomain.SetData(ReadBomBridgeBasePortDataName, ReadBomBridgeBasePort);
            runtimeDomain.SetData(PortScanCountDataName, PortScanCount);
            runtimeDomain.SetData(AddinDirectoryDataName, AddinLog.DirectoryPath);
            runtimeDomain.SetData(ReadBomAddinDirectoryDataName, AddinLog.DirectoryPath);
            runtimeDomain.DoCallBack(StartRuntimeInCurrentDomain);

            _runtimeDomain = runtimeDomain;
            _runtimeShadowDirectory = shadowDirectory;
            AddinLog.Write("Runtime started: reason=" + reason + ", source=" + runtimePath + ", shadow=" + shadowRuntimePath);
            CleanupOldRuntimeShadows(shadowDirectory);
        }
        catch (Exception ex)
        {
            AddinLog.Write("Runtime start failed: " + ex);
            TryUnloadRuntimeDomain(runtimeDomain);
            TryDeleteDirectory(shadowDirectory);
        }
    }

    private void StopRuntimeCore()
    {
        var runtimeDomain = _runtimeDomain;
        var shadowDirectory = _runtimeShadowDirectory;
        _runtimeDomain = null;
        _runtimeShadowDirectory = null;

        if (runtimeDomain != null)
        {
            try
            {
                runtimeDomain.DoCallBack(StopRuntimeInCurrentDomain);
            }
            catch (Exception ex)
            {
                AddinLog.Write("Runtime stop callback ignored: " + ex.Message);
            }

            TryUnloadRuntimeDomain(runtimeDomain);
        }

        TryDeleteDirectory(shadowDirectory);
    }

    private static void StartRuntimeInCurrentDomain()
    {
        var domain = AppDomain.CurrentDomain;
        var runtimePath = (string)domain.GetData(RuntimeAssemblyPathDataName);
        var runtimeDirectory = Path.GetDirectoryName(runtimePath);
        RegisterRuntimeAssemblyResolver(runtimeDirectory);
        LoadRuntimeDependency(runtimeDirectory, "Interop.SldWorks.dll");
        LoadRuntimeDependency(runtimeDirectory, "Interop.SwConst.dll");
        LoadRuntimeDependency(runtimeDirectory, "SolidWorks.Interop.swdocumentmgr.dll");
        LoadRuntimeDependency(runtimeDirectory, "SolidWorks.Interop.swpublished.dll");
        LoadRuntimeDependency(runtimeDirectory, "SolidWorksTools.dll");

        var assembly = Assembly.LoadFrom(runtimePath);
        var runtimeType = assembly.GetType(RuntimeTypeName, true);
        var runtime = Activator.CreateInstance(runtimeType);

        // Create a direct RCW from the IUnknown pointer to bypass .NET remoting
        // serialization. This gives the runtime AppDomain a native COM proxy that
        // won't deadlock on calls like INewDrawing2.
        var swAppUnk = (IntPtr)domain.GetData(SwAppUnkDataName);
        if (swAppUnk != IntPtr.Zero)
        {
            try
            {
                var directSwApp = Marshal.GetObjectForIUnknown(swAppUnk) as SldWorks.SldWorks;
                domain.SetData(SwAppDirectDataName, directSwApp);
                AddinLog.Write("Runtime direct COM proxy created from IUnknown.");
            }
            catch (Exception ex)
            {
                AddinLog.Write("Runtime direct COM proxy creation failed: " + ex.Message);
            }
            finally
            {
                Marshal.Release(swAppUnk);
            }
        }

        runtimeType.InvokeMember(
            "Start",
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.InvokeMethod,
            null,
            runtime,
            new[]
            {
                domain.GetData(SwAppDataName),
                domain.GetData(ExternalBridgeBasePortDataName),
                domain.GetData(ReadBomBridgeBasePortDataName),
                domain.GetData(PortScanCountDataName)
            });

        domain.SetData(RuntimeInstanceDataName, runtime);
    }

    private static void StopRuntimeInCurrentDomain()
    {
        var domain = AppDomain.CurrentDomain;
        var runtime = domain.GetData(RuntimeInstanceDataName);
        if (runtime == null) return;

        runtime.GetType().InvokeMember(
            "Stop",
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.InvokeMethod,
            null,
            runtime,
            Array.Empty<object>());
        domain.SetData(RuntimeInstanceDataName, null);
    }

    private static void RegisterRuntimeAssemblyResolver(string runtimeDirectory)
    {
        if (string.IsNullOrWhiteSpace(runtimeDirectory)) return;

        AppDomain.CurrentDomain.AssemblyResolve += (_, args) =>
        {
            try
            {
                var assemblyName = new AssemblyName(args.Name).Name;
                if (string.IsNullOrWhiteSpace(assemblyName)) return null;

                var runtimePath = Path.Combine(runtimeDirectory, assemblyName + ".dll");
                if (File.Exists(runtimePath)) return Assembly.LoadFrom(runtimePath);

                var addinPath = Path.Combine(AddinLog.DirectoryPath, assemblyName + ".dll");
                return File.Exists(addinPath) ? Assembly.LoadFrom(addinPath) : null;
            }
            catch (Exception ex)
            {
                AddinLog.Write("Runtime assembly resolve ignored: " + args.Name + ", " + ex.Message);
                return null;
            }
        };
    }

    private static void LoadRuntimeDependency(string directory, string fileName)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(directory)) return;

            var path = Path.Combine(directory, fileName);
            if (File.Exists(path))
                Assembly.LoadFrom(path);
        }
        catch (Exception ex)
        {
            AddinLog.Write("Runtime dependency load ignored: " + fileName + ", " + ex.Message);
        }
    }

    private void StartRuntimeWatcher()
    {
        StopRuntimeWatcher();
        try
        {
            var runtimeDirectory = GetRuntimeDirectory();
            Directory.CreateDirectory(runtimeDirectory);
            _runtimeWatcher = new FileSystemWatcher(runtimeDirectory)
            {
                Filter = "*.dll",
                IncludeSubdirectories = false,
                NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.Size
            };
            _runtimeWatcher.Changed += OnRuntimeFileChanged;
            _runtimeWatcher.Created += OnRuntimeFileChanged;
            _runtimeWatcher.Renamed += OnRuntimeFileChanged;
            _runtimeWatcher.Deleted += OnRuntimeFileChanged;
            _runtimeWatcher.EnableRaisingEvents = true;
            AddinLog.Write("Runtime watcher started: " + runtimeDirectory);
        }
        catch (Exception ex)
        {
            AddinLog.Write("Runtime watcher start failed: " + ex);
        }
    }

    private void StopRuntimeWatcher()
    {
        try
        {
            if (_runtimeWatcher != null)
            {
                _runtimeWatcher.EnableRaisingEvents = false;
                _runtimeWatcher.Changed -= OnRuntimeFileChanged;
                _runtimeWatcher.Created -= OnRuntimeFileChanged;
                _runtimeWatcher.Renamed -= OnRuntimeFileChanged;
                _runtimeWatcher.Deleted -= OnRuntimeFileChanged;
                _runtimeWatcher.Dispose();
                _runtimeWatcher = null;
            }
        }
        catch (Exception ex)
        {
            AddinLog.Write("Runtime watcher stop ignored: " + ex.Message);
        }

        try
        {
            _reloadTimer?.Dispose();
            _reloadTimer = null;
        }
        catch (Exception ex)
        {
            AddinLog.Write("Runtime reload timer dispose ignored: " + ex.Message);
        }
    }

    private void OnRuntimeFileChanged(object sender, FileSystemEventArgs e)
    {
        if (!string.Equals(Path.GetExtension(e.FullPath), ".dll", StringComparison.OrdinalIgnoreCase)) return;
        ScheduleRuntimeReload(e.ChangeType + ": " + e.Name);
    }

    private void ScheduleRuntimeReload(string reason)
    {
        if (_disconnecting) return;
        AddinLog.Write("Runtime reload scheduled: " + reason);
        lock (_runtimeLock)
        {
            _reloadTimer ??= new System.Threading.Timer(_ => ReloadRuntimeFromTimer(reason), null, Timeout.Infinite, Timeout.Infinite);
            _reloadTimer.Change(1500, Timeout.Infinite);
        }
    }

    private void ReloadRuntimeFromTimer(string reason)
    {
        try
        {
            if (_disconnecting || _swApp == null) return;
            if (_mainThreadControl == null || _mainThreadControl.IsDisposed || !_mainThreadControl.IsHandleCreated)
            {
                AddinLog.Write("Runtime reload skipped: main thread control unavailable");
                return;
            }

            if (_mainThreadControl.InvokeRequired)
            {
                _mainThreadControl.BeginInvoke((Action)(() => RestartRuntime(reason)));
            }
            else
            {
                RestartRuntime(reason);
            }
        }
        catch (Exception ex)
        {
            AddinLog.Write("Runtime reload failed: " + ex);
        }
    }

    private void DisposeMainThreadControl()
    {
        try
        {
            var control = _mainThreadControl;
            _mainThreadControl = null;
            if (control == null || control.IsDisposed) return;

            if (control.IsHandleCreated && control.InvokeRequired)
                control.BeginInvoke((Action)(() => control.Dispose()));
            else
                control.Dispose();
        }
        catch (Exception ex)
        {
            AddinLog.Write("Main thread control dispose ignored: " + ex.Message);
        }
    }

    private static string GetRuntimeDirectory()
    {
        return Path.Combine(AddinLog.DirectoryPath, "Runtime", "current");
    }

    private static string GetRuntimeAssemblyPath()
    {
        return Path.Combine(GetRuntimeDirectory(), RuntimeAssemblyFileName);
    }

    private static string CreateRuntimeShadowCopy(string runtimeDirectory)
    {
        var shadowRoot = Path.Combine(AddinLog.DirectoryPath, "Runtime", "shadow");
        var shadowDirectory = Path.Combine(shadowRoot, DateTime.Now.ToString("yyyyMMddHHmmssfff"));
        Directory.CreateDirectory(shadowDirectory);

        foreach (var sourcePath in Directory.GetFiles(runtimeDirectory, "*", SearchOption.TopDirectoryOnly))
        {
            var destinationPath = Path.Combine(shadowDirectory, Path.GetFileName(sourcePath));
            CopyFileWithRetry(sourcePath, destinationPath);
        }

        return shadowDirectory;
    }

    private static void CopyFileWithRetry(string sourcePath, string destinationPath)
    {
        for (var attempt = 0; attempt < 5; attempt++)
        {
            try
            {
                File.Copy(sourcePath, destinationPath, true);
                return;
            }
            catch (Exception ex) when (attempt < 4 && (ex is IOException || ex is UnauthorizedAccessException))
            {
                Thread.Sleep(200);
            }
        }

        File.Copy(sourcePath, destinationPath, true);
    }

    private static void CleanupOldRuntimeShadows(string activeShadowDirectory)
    {
        var shadowRoot = Path.Combine(AddinLog.DirectoryPath, "Runtime", "shadow");
        if (!Directory.Exists(shadowRoot)) return;

        foreach (var directory in Directory.GetDirectories(shadowRoot))
        {
            if (string.Equals(directory, activeShadowDirectory, StringComparison.OrdinalIgnoreCase)) continue;
            TryDeleteDirectory(directory);
        }
    }

    private static void TryDeleteDirectory(string directory)
    {
        if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory)) return;

        try
        {
            Directory.Delete(directory, true);
        }
        catch (Exception ex)
        {
            AddinLog.Write("Runtime shadow cleanup ignored: " + directory + ", " + ex.Message);
        }
    }

    private static void TryUnloadRuntimeDomain(AppDomain runtimeDomain)
    {
        if (runtimeDomain == null) return;

        try
        {
            AppDomain.Unload(runtimeDomain);
        }
        catch (Exception ex)
        {
            AddinLog.Write("Runtime AppDomain unload ignored: " + ex.Message);
        }
    }

    [ComRegisterFunction]
    public static void Register(Type type)
    {
        var attribute = (SwAddinAttribute)Attribute.GetCustomAttribute(type, typeof(SwAddinAttribute));
        var title = attribute?.Title ?? AddinTitle;
        var description = attribute?.Description ?? AddinDescription;
        var loadAtStartup = attribute?.LoadAtStartup == true ? 1 : 0;

        using (var key = CreateRequiredSubKey(Registry.LocalMachine, $@"SOFTWARE\SolidWorks\AddIns\{{{type.GUID}}}"))
        {
            key.SetValue(null, 0, RegistryValueKind.DWord);
            key.SetValue("Title", title, RegistryValueKind.String);
            key.SetValue("Description", description, RegistryValueKind.String);
        }

        using (var key = CreateRequiredSubKey(Registry.CurrentUser, $@"SOFTWARE\SolidWorks\AddIns\{{{type.GUID}}}"))
        {
            key.SetValue(null, 0, RegistryValueKind.DWord);
            key.SetValue("Title", title, RegistryValueKind.String);
            key.SetValue("Description", description, RegistryValueKind.String);
        }

        using (var key = CreateRequiredSubKey(Registry.CurrentUser, $@"SOFTWARE\SolidWorks\AddInsStartup\{{{type.GUID}}}"))
        {
            key.SetValue(null, loadAtStartup, RegistryValueKind.DWord);
        }
    }

    private static RegistryKey CreateRequiredSubKey(RegistryKey root, string subkey)
    {
        return root.CreateSubKey(subkey)
               ?? throw new InvalidOperationException("Failed to create registry key: " + root.Name + "\\" + subkey);
    }

    [ComUnregisterFunction]
    public static void Unregister(Type type)
    {
        Registry.LocalMachine.DeleteSubKey($@"SOFTWARE\SolidWorks\AddIns\{{{type.GUID}}}", false);
        Registry.CurrentUser.DeleteSubKey($@"SOFTWARE\SolidWorks\AddIns\{{{type.GUID}}}", false);
        Registry.CurrentUser.DeleteSubKey($@"SOFTWARE\SolidWorks\AddInsStartup\{{{type.GUID}}}", false);
    }

    /// <summary>
    /// Cross-AppDomain bridge for SolidWorks COM calls.
    /// Lives in the main AppDomain where _swApp is a direct RCW, avoiding
    /// the COM proxy deadlock that occurs in the runtime AppDomain.
    /// </summary>
    internal sealed class SwComBridge : MarshalByRefObject
    {
        private readonly SldWorks.SldWorks _swApp;

        public SwComBridge(SldWorks.SldWorks swApp)
        {
            _swApp = swApp;
        }

        public override object InitializeLifetimeService()
        {
            return null; // infinite lifetime
        }

        /// <summary>
        /// Opens a document read-only. Safe to call from runtime AppDomain.
        /// </summary>
        public SldWorks.ModelDoc2 OpenDocReadOnly(string path, int docType)
        {
            AddinLog.Write("SwComBridge.OpenDocReadOnly: " + path);
            var errors = 0;
            var warnings = 0;
            var model = _swApp.OpenDoc6(
                path,
                docType,
                (int)swOpenDocOptions_e.swOpenDocOptions_ReadOnly,
                "",
                ref errors,
                ref warnings) as SldWorks.ModelDoc2;
            AddinLog.Write($"SwComBridge.OpenDocReadOnly returned: errors={errors}, warnings={warnings}");
            return model;
        }

        /// <summary>
        /// Closes a document by title.
        /// </summary>
        public void CloseDoc(string title)
        {
            AddinLog.Write("SwComBridge.CloseDoc: " + title);
            _swApp.CloseDoc(title);
        }

        /// <summary>
        /// Creates a new drawing from the specified template using INewDocument2.
        /// INewDocument2 is the recommended API (SW 2006+) — it determines the
        /// document type from the template extension, avoiding the templateType
        /// parameter confusion of INewDrawing2.
        /// </summary>
        public SldWorks.ModelDoc2 NewDrawing(string templatePath)
        {
            var paperSize = (int)swDwgPaperSizes_e.swDwgPapersUserDefined;
            AddinLog.Write($"SwComBridge.NewDrawing: path={templatePath}, paperSize={paperSize}");
            var doc = _swApp.INewDocument2(
                templatePath,
                paperSize,
                0.42,
                0.297);
            var model = (doc as SldWorks.ModelDoc2) ?? (_swApp.ActiveDoc as SldWorks.ModelDoc2);
            AddinLog.Write("SwComBridge.NewDrawing returned: " +
                           (model != null ? SafeTitle(model) : "null"));
            return model;
        }

        private static string SafeTitle(SldWorks.ModelDoc2 model)
        {
            try { return model.GetTitle() ?? "?"; }
            catch { return "?"; }
        }
    }
}
