using System;
using System.Net;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Microsoft.Win32;
using SolidWorksTools;

namespace ExternalProgram.SwAddin;

/// <summary>
/// SolidWorks 插件入口。
/// SW 启动时自动加载，在 SW 主 AppDomain 中直接启动 HTTP+WebSocket 服务，
/// 供 ExternalProgram.exe（WPF 主程序）和 ReadBom.exe 通过 127.0.0.1 回环通信。
/// </summary>
[ComVisible(true)]
[Guid("C8F7A3D2-6B51-4E92-A814-7F2D3C1E9A56")]
[ProgId("ExternalProgram.SwAddin")]
[SwAddin(Description = AddinDescription, Title = AddinTitle, LoadAtStartup = true)]
public sealed class SwAddin : SolidWorks.Interop.swpublished.SwAddin
{
    private const string AddinTitle = "External Program Add-in";
    private const string AddinDescription = "External Program local command bridge for SolidWorks.";
    // 两个 HTTP 服务各自扫描一段端口，避免冲突
    private const int ExternalBridgeBasePort = 32128;
    private const int ReadBomBridgeBasePort = 32127;
    private const int PortScanCount = 60;

    private SldWorks.SldWorks _swApp;
    private int _cookie;
    // WinForms Control 用于将线程池回调封送到 SW 主线程（COM STA 要求）
    private Control _mainThreadControl;
    // 对外 HTTP 服务（主程序命令通道）
    private AddinHttpServer _server;
    // ReadBom 专用 HTTP 服务
    private ReadBom.SwAddin.AddinHttpServer _readBomServer;
    private int _serverPort;
    private int _readBomServerPort;

    /// <summary>
    /// SW 加载插件时回调。运行在 SW 主线程（STA）。
    /// </summary>
    public bool ConnectToSW(object thisSw, int cookie)
    {
        try
        {
            AddinLog.Write("ConnectToSW called");
            _swApp = (SldWorks.SldWorks)thisSw;
            _cookie = cookie;

            // 创建 WinForms Control，借助其消息泵将线程池回调封送到 SW 主线程
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

            StartServers();
            return true;
        }
        catch (Exception ex)
        {
            AddinLog.Write("ConnectToSW failed: " + ex);
            StopServers();
            DisposeMainThreadControl();
            return false;
        }
    }

    /// <summary>
    /// SW 卸载插件时回调。关闭 HTTP 服务，释放资源。
    /// </summary>
    public bool DisconnectFromSW()
    {
        AddinLog.Write("DisconnectFromSW called");
        StopServers();
        DisposeMainThreadControl();
        _swApp = null;
        _cookie = 0;
        return true;
    }

    /// <summary>
    /// 启动两个 HTTP 服务，各自扫描可用端口。
    /// </summary>
    private void StartServers()
    {
        _server = StartExternalBridgeServer(ExternalBridgeBasePort, PortScanCount);
        _readBomServer = StartReadBomBridgeServer(ReadBomBridgeBasePort, PortScanCount);
        AddinLog.Write("HTTP server started on port " + _serverPort);
        AddinLog.Write("ReadBom HTTP server started on port " + _readBomServerPort);
    }

    /// <summary>
    /// 安全关闭所有 HTTP 服务。
    /// </summary>
    private void StopServers()
    {
        try { _readBomServer?.Dispose(); }
        catch (Exception ex) { AddinLog.Write("ReadBom server dispose ignored: " + ex.Message); }

        try { _server?.Dispose(); }
        catch (Exception ex) { AddinLog.Write("External server dispose ignored: " + ex.Message); }

        _readBomServer = null;
        _server = null;
    }

    /// <summary>
    /// 在主程序命令通道的端口范围内扫描，启动 HTTP 服务。
    /// </summary>
    private AddinHttpServer StartExternalBridgeServer(int basePort, int portScanCount)
    {
        Exception lastError = null;
        for (var port = basePort; port < basePort + portScanCount; port++)
        {
            var server = new AddinHttpServer(_swApp, _mainThreadControl, BuildLoopbackPrefix(port));
            try
            {
                server.Start();
                _serverPort = port;
                return server;
            }
            catch (Exception ex) when (IsPortStartFailure(ex))
            {
                lastError = ex;
                AddinLog.Write("External bridge port unavailable: " + port + ", " + ex.Message);
                try { server.Dispose(); }
                catch (Exception disposeEx) { AddinLog.Write("External bridge server dispose after port failure ignored: " + disposeEx.Message); }
            }
        }

        throw new InvalidOperationException("No available External bridge port.", lastError);
    }

    /// <summary>
    /// 在 ReadBom 命令通道的端口范围内扫描，启动 HTTP 服务。
    /// </summary>
    private ReadBom.SwAddin.AddinHttpServer StartReadBomBridgeServer(int basePort, int portScanCount)
    {
        Exception lastError = null;
        for (var port = basePort; port < basePort + portScanCount; port++)
        {
            var server = new ReadBom.SwAddin.AddinHttpServer(_swApp, _mainThreadControl, BuildLoopbackPrefix(port));
            try
            {
                server.Start();
                _readBomServerPort = port;
                return server;
            }
            catch (Exception ex) when (IsPortStartFailure(ex))
            {
                lastError = ex;
                AddinLog.Write("ReadBom bridge port unavailable: " + port + ", " + ex.Message);
                try { server.Dispose(); }
                catch (Exception disposeEx) { AddinLog.Write("ReadBom bridge server dispose after port failure ignored: " + disposeEx.Message); }
            }
        }

        throw new InvalidOperationException("No available ReadBom bridge port.", lastError);
    }

    /// <summary>
    /// 安全释放主线程 Control。如果当前不在主线程，通过 BeginInvoke 封送回去销毁。
    /// </summary>
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

    private static string BuildLoopbackPrefix(int port)
    {
        return "http://127.0.0.1:" + port + "/";
    }

    private static bool IsPortStartFailure(Exception ex)
    {
        return ex is HttpListenerException || ex is InvalidOperationException;
    }

    /// <summary>
    /// COM 注册时写入 SolidWorks AddIn 注册表项。
    /// </summary>
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

    /// <summary>
    /// COM 卸载时清理 SolidWorks AddIn 注册表项。
    /// </summary>
    [ComUnregisterFunction]
    public static void Unregister(Type type)
    {
        Registry.LocalMachine.DeleteSubKey($@"SOFTWARE\SolidWorks\AddIns\{{{type.GUID}}}", false);
        Registry.CurrentUser.DeleteSubKey($@"SOFTWARE\SolidWorks\AddIns\{{{type.GUID}}}", false);
        Registry.CurrentUser.DeleteSubKey($@"SOFTWARE\SolidWorks\AddInsStartup\{{{type.GUID}}}", false);
    }
}
