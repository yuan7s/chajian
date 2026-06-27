using System;
using System.IO;
using System.Net;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Microsoft.Win32;
using SolidWorksTools;

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
    private SldWorks.SldWorks _swApp;
    private int _cookie;
    private AddinHttpServer _server;
    private ReadBom.SwAddin.AddinHttpServer _readBomServer;
    private Control _mainThreadControl;
    private int _serverPort;
    private int _readBomServerPort;

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

            _server = StartExternalBridgeServer();
            _readBomServer = StartReadBomBridgeServer();
            AddinLog.Write("HTTP+WS server started on port " + _serverPort);
            AddinLog.Write("ReadBom HTTP server started on port " + _readBomServerPort);
            return true;
        }
        catch (Exception ex)
        {
            AddinLog.Write("ConnectToSW failed: " + ex);
            try { _readBomServer?.Dispose(); }
            catch (Exception disposeEx) { AddinLog.Write("ReadBom server dispose after ConnectToSW failure ignored: " + disposeEx.Message); }
            try { _server?.Dispose(); }
            catch (Exception disposeEx) { AddinLog.Write("External server dispose after ConnectToSW failure ignored: " + disposeEx.Message); }
            _readBomServer = null;
            _server = null;
            return false;
        }
    }

    public bool DisconnectFromSW()
    {
        AddinLog.Write("DisconnectFromSW called");
        _readBomServer?.Dispose();
        _readBomServer = null;
        _server?.Dispose();
        _server = null;
        _swApp = null;
        _cookie = 0;
        return true;
    }

    private AddinHttpServer StartExternalBridgeServer()
    {
        Exception lastError = null;
        for (var port = ExternalBridgeBasePort; port < ExternalBridgeBasePort + PortScanCount; port++)
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

    private ReadBom.SwAddin.AddinHttpServer StartReadBomBridgeServer()
    {
        Exception lastError = null;
        for (var port = ReadBomBridgeBasePort; port < ReadBomBridgeBasePort + PortScanCount; port++)
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

    private static string BuildLoopbackPrefix(int port)
    {
        return "http://127.0.0.1:" + port + "/";
    }

    private static bool IsPortStartFailure(Exception ex)
    {
        return ex is HttpListenerException || ex is InvalidOperationException;
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
}

internal static class AddinLog
{
    public static readonly string DirectoryPath = GetAddinDirectory();
    private static readonly string LogPath = Path.Combine(DirectoryPath, "SwAddin.log");

    public static void Write(string message)
    {
        try
        {
            var directory = Path.GetDirectoryName(LogPath);
            if (!string.IsNullOrWhiteSpace(directory))
                Directory.CreateDirectory(directory);
            File.AppendAllText(LogPath, $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} {message}{Environment.NewLine}");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine("SwAddin log write failed: " + ex.Message);
        }
    }

    private static string GetAddinDirectory()
    {
        try
        {
            var location = Assembly.GetExecutingAssembly().Location;
            var directory = Path.GetDirectoryName(location);
            if (!string.IsNullOrWhiteSpace(directory)) return directory;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine("SwAddin directory lookup failed: " + ex.Message);
        }
        return AppDomain.CurrentDomain.BaseDirectory;
    }
}
