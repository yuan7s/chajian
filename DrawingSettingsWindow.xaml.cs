using WpfNs = System.Windows;
using WpfUiControls = Wpf.Ui.Controls;

namespace ExternalProgram;

partial class DrawingSettingsWindow : WpfUiControls.FluentWindow
{
    public DrawingSettingsWindow()
    {
        InitializeComponent();
    }

    private void SelectDrawingStandard_Click(object sender, WpfNs.RoutedEventArgs e)
    {
        SelectFileInto(DrawingStandardBox);
    }

    private void SelectSheetFormat_Click(object sender, WpfNs.RoutedEventArgs e)
    {
        SelectFileInto(SheetFormatBox);
    }

    private void SelectFileInto(WpfNs.Controls.TextBox target)
    {
        var dialog = new Microsoft.Win32.OpenFileDialog()
        {
            CheckFileExists = true,
            Filter = "所有文件 (*.*)|*.*"
        };
        if (dialog.ShowDialog(this).GetValueOrDefault())
        {
            target.Text = dialog.FileName;
        }
    }
}
