using System;
using System.IO;
using System.Windows;
using System.Windows.Markup;
using WpfNs = System.Windows;

namespace ExternalProgram;

public class Program
{
    [STAThread]
    public static void Main()
    {
        try
        {
            var app = new WpfNs.Application();
            LoadWpfUiResources(app);
            app.ShutdownMode = WpfNs.ShutdownMode.OnExplicitShutdown;
            app.Run(new Form1());
        }
        catch (Exception ex)
        {
            var logPath = Path.Combine(Path.GetTempPath(), "ExternalProgram-startup-error.log");
            File.WriteAllText(logPath, ex.ToString());
            throw;
        }
    }

    private static void LoadWpfUiResources(WpfNs.Application app)
    {
        const string resourceXaml =
            """
            <ResourceDictionary xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
                                xmlns:ui="http://schemas.lepo.co/wpfui/2022/xaml">
                <ResourceDictionary.MergedDictionaries>
                    <ui:ThemesDictionary Theme="Light" />
                    <ui:ControlsDictionary />
                </ResourceDictionary.MergedDictionaries>
            </ResourceDictionary>
            """;

        var resources = (ResourceDictionary)XamlReader.Parse(resourceXaml);
        app.Resources.MergedDictionaries.Add(resources);
    }
}

