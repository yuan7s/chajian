using System;
using System.Net;
using System.Runtime.InteropServices;
using System.Diagnostics;
using SwConst;
using System.Windows.Forms;
using Microsoft.Win32;
using SolidWorksTools;

namespace ExternalProgram.SwAddin;

/// <summary>
/// SolidWorks 插件入口。
/// SW 启动时自动加载，在 SW 主 AppDomain 中直接启动 HTTP+WebSocket 服务，
/// 供 ExternalProgram.exe（WPF 主程序）通过 127.0.0.1 回环通信。
/// </summary>
[ComVisible(true)]
[Guid("C8F7A3D2-6B51-4E92-A814-7F2D3C1E9A56")]
[ProgId("ExternalProgram.SwAddin")]
[SwAddin(Description = AddinDescription, Title = AddinTitle, LoadAtStartup = true)]
public sealed class SwAddin : SolidWorks.Interop.swpublished.SwAddin
{
    private const string AddinTitle = "External Program Add-in";
    private const string AddinDescription = "External Program local command bridge for SolidWorks.";
    private const int ExternalBridgeBasePort = 32128;
    private const int PortScanCount = 60;

    private SldWorks.SldWorks _swApp;
    private int _cookie;
    // WinForms Control 用于将线程池回调封送到 SW 主线程（COM STA 要求）
    private Control _mainThreadControl;
    // 对外 HTTP 服务（主程序命令通道）
    private AddinHttpServer _server;
    private int _serverPort;

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
            AddWebConsoleMenu();
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
    /// 启动 HTTP 服务，扫描可用端口。
    /// </summary>
    private void StartServers()
    {
        _server = StartExternalBridgeServer(ExternalBridgeBasePort, PortScanCount);
        AddinLog.Write("HTTP server started on port " + _serverPort);
    }

    /// <summary>
    /// 在 SolidWorks 菜单栏注册「External Program」菜单及「打开网页控制台」项。
    /// swDocNONE 仅对应「无文档」主框架，打开文档后菜单栏会切换为文档类型菜单，
    /// 因此同时注册到零件/装配体/工程图上下文，确保菜单始终可见。
    /// </summary>
    private void AddWebConsoleMenu()
    {
        try
        {
            const string menuName = "External Program";
            var docTypes = new[]
            {
                (int)swDocumentTypes_e.swDocNONE,
                (int)swDocumentTypes_e.swDocPART,
                (int)swDocumentTypes_e.swDocASSEMBLY,
                (int)swDocumentTypes_e.swDocDRAWING
            };

            foreach (var docType in docTypes)
            {
                var menuId = _swApp.AddMenu(docType, menuName, 5);
                var itemOk = _swApp.AddMenuItem2(
                    docType,
                    _cookie,
                    "打开网页控制台@" + menuName,
                    -1,
                    "OpenWebConsole",
                    "",
                    "在浏览器中打开网页控制台");
                AddinLog.Write($"AddWebConsoleMenu docType={docType} menuId={menuId} itemOk={itemOk}");
            }
        }
        catch (Exception ex)
        {
            AddinLog.Write("AddWebConsoleMenu ignored: " + ex.Message);
        }
    }

    /// <summary>
    /// 菜单回调：用默认浏览器打开本插件网页控制台。
    /// </summary>
    public void OpenWebConsole()
    {
        try
        {
            var url = "http://127.0.0.1:" + _serverPort + "/";
            using var p = Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            AddinLog.Write("OpenWebConsole failed: " + ex.Message);
        }
    }

    /// <summary>
    /// 安全关闭 HTTP 服务。
    /// </summary>
    private void StopServers()
    {
        try { _server?.Dispose(); }
        catch (Exception ex) { AddinLog.Write("External server dispose ignored: " + ex.Message); }

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
