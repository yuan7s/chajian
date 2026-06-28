using System;
using System.IO;
using System.Net;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace ExternalProgram.SwAddin;

public sealed class SwAddinRuntime : MarshalByRefObject
{
    private static bool _assemblyResolverRegistered;
    private object _swApp;
    private Control _mainThreadControl;
    private AddinHttpServer _server;
    private ReadBom.SwAddin.AddinHttpServer _readBomServer;
    private int _serverPort;
    private int _readBomServerPort;

    public void Start(object swApp, int externalBasePort, int readBomBasePort, int portScanCount)
    {
        RegisterRuntimeAssemblyResolver();
        LoadRuntimeDependency("Interop.SldWorks.dll");
        LoadRuntimeDependency("Interop.SwConst.dll");
        LoadRuntimeDependency("SolidWorks.Interop.swdocumentmgr.dll");
        LoadRuntimeDependency("SolidWorks.Interop.swpublished.dll");
        LoadRuntimeDependency("SolidWorksTools.dll");

        Stop();
        _swApp = GetDirectSolidWorksApplication() ?? GetSolidWorksApplication(swApp);

        _mainThreadControl = new Control();
        var _ = _mainThreadControl.Handle;

        _server = StartExternalBridgeServer(externalBasePort, portScanCount);
        _readBomServer = StartReadBomBridgeServer(readBomBasePort, portScanCount);
        AddinLog.Write("Runtime HTTP+WS server started on port " + _serverPort);
        AddinLog.Write("Runtime ReadBom HTTP server started on port " + _readBomServerPort);
    }

    public void Stop()
    {
        try
        {
            _readBomServer?.Dispose();
        }
        catch (Exception ex)
        {
            AddinLog.Write("ReadBom server dispose during runtime stop ignored: " + ex.Message);
        }

        try
        {
            _server?.Dispose();
        }
        catch (Exception ex)
        {
            AddinLog.Write("External server dispose during runtime stop ignored: " + ex.Message);
        }

        _readBomServer = null;
        _server = null;
        _swApp = null;
        DisposeMainThreadControl();
    }

    public override object InitializeLifetimeService()
    {
        return null;
    }

    private AddinHttpServer StartExternalBridgeServer(int basePort, int portScanCount)
    {
        Exception lastError = null;
        var swApp = CastSolidWorksApplication(_swApp);
        for (var port = basePort; port < basePort + portScanCount; port++)
        {
            var server = new AddinHttpServer(swApp, _mainThreadControl, BuildLoopbackPrefix(port));
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

    private ReadBom.SwAddin.AddinHttpServer StartReadBomBridgeServer(int basePort, int portScanCount)
    {
        Exception lastError = null;
        var swApp = CastSolidWorksApplication(_swApp);
        for (var port = basePort; port < basePort + portScanCount; port++)
        {
            var server = new ReadBom.SwAddin.AddinHttpServer(swApp, _mainThreadControl, BuildLoopbackPrefix(port));
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
            AddinLog.Write("Runtime main thread control dispose ignored: " + ex.Message);
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

    private static SldWorks.SldWorks GetDirectSolidWorksApplication()
    {
        try
        {
            var domain = AppDomain.CurrentDomain;
            var directSwApp = domain.GetData("ExternalProgram.SwAddin.SwAppDirect") as SldWorks.SldWorks;
            if (directSwApp != null)
            {
                AddinLog.Write("Runtime SolidWorks application resolved from direct IUnknown proxy.");
                return directSwApp;
            }
        }
        catch (Exception ex)
        {
            AddinLog.Write("Runtime direct COM proxy lookup ignored: " + ex.Message);
        }

        return null;
    }

    private static SldWorks.SldWorks GetSolidWorksApplication(object fallbackSwApp)
    {
        try
        {
            var activeSw = Marshal.GetActiveObject("SldWorks.Application");
            var swApp = activeSw as SldWorks.SldWorks;
            if (swApp != null)
            {
                AddinLog.Write("Runtime SolidWorks application resolved from ROT.");
                return swApp;
            }

            AddinLog.Write("Runtime ROT object is not SldWorks.SldWorks.");
        }
        catch (Exception ex)
        {
            AddinLog.Write("Runtime ROT SolidWorks lookup ignored: " + ex.Message);
        }

        return CastSolidWorksApplication(fallbackSwApp);
    }

    private static SldWorks.SldWorks CastSolidWorksApplication(object swApp)
    {
        return swApp as SldWorks.SldWorks
               ?? throw new InvalidOperationException("SolidWorks application is unavailable.");
    }

    private static void LoadRuntimeDependency(string fileName)
    {
        try
        {
            var path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, fileName);
            if (!File.Exists(path)) return;

            var assemblyName = AssemblyName.GetAssemblyName(path);
            try
            {
                Assembly.Load(assemblyName);
                AddinLog.Write("Runtime dependency loaded by name: " + assemblyName.FullName);
                return;
            }
            catch (Exception ex)
            {
                AddinLog.Write("Runtime dependency load by name ignored: " + fileName + ", " + ex.Message);
            }

            Assembly.LoadFrom(path);
            AddinLog.Write("Runtime dependency loaded from path: " + path);
        }
        catch (Exception ex)
        {
            AddinLog.Write("Runtime dependency load ignored: " + fileName + ", " + ex.Message);
        }
    }

    private static void RegisterRuntimeAssemblyResolver()
    {
        if (_assemblyResolverRegistered) return;

        AppDomain.CurrentDomain.AssemblyResolve += ResolveRuntimeAssembly;
        _assemblyResolverRegistered = true;
    }

    private static Assembly ResolveRuntimeAssembly(object sender, ResolveEventArgs args)
    {
        try
        {
            var assemblyName = new AssemblyName(args.Name).Name;
            if (string.IsNullOrWhiteSpace(assemblyName)) return null;

            var path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, assemblyName + ".dll");
            if (!File.Exists(path)) return null;

            try
            {
                return Assembly.Load(AssemblyName.GetAssemblyName(path));
            }
            catch
            {
                return Assembly.LoadFrom(path);
            }
        }
        catch (Exception ex)
        {
            AddinLog.Write("Runtime assembly resolve ignored: " + args.Name + ", " + ex.Message);
            return null;
        }
    }
}
