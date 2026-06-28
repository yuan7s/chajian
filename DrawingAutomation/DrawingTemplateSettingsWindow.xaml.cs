using System.Collections.Generic;
using Microsoft.Win32;
using WpfControls = System.Windows.Controls;
using WpfNs = System.Windows;
using ExternalProgram.Properties;

namespace ExternalProgram;

public partial class DrawingTemplateSettingsWindow : WpfNs.Window
{
    private readonly Dictionary<string, WpfControls.TextBox> _templateBoxes =
        new Dictionary<string, WpfControls.TextBox>();
    private readonly Dictionary<string, WpfControls.TextBox> _formatBoxes =
        new Dictionary<string, WpfControls.TextBox>();

    public DrawingTemplateSettingsWindow()
    {
        InitializeComponent();
        BuildRows();
        LoadFromSettings();
    }

    private void BuildRows()
    {
        var grid = new WpfControls.Grid();
        grid.ColumnDefinitions.Add(new WpfControls.ColumnDefinition { Width = new WpfNs.GridLength(56) });
        grid.ColumnDefinitions.Add(new WpfControls.ColumnDefinition { Width = new WpfNs.GridLength(1, WpfNs.GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new WpfControls.ColumnDefinition { Width = WpfNs.GridLength.Auto });
        grid.ColumnDefinitions.Add(new WpfControls.ColumnDefinition { Width = new WpfNs.GridLength(1, WpfNs.GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new WpfControls.ColumnDefinition { Width = WpfNs.GridLength.Auto });

        var rowIndex = 0;
        foreach (var size in PaperSizeCatalog.Sizes)
        {
            grid.RowDefinitions.Add(new WpfControls.RowDefinition { Height = WpfNs.GridLength.Auto });

            var label = new WpfControls.TextBlock
            {
                Text = size,
                VerticalAlignment = WpfNs.VerticalAlignment.Center,
                Margin = new WpfNs.Thickness(0, 6, 0, 6)
            };
            WpfControls.Grid.SetRow(label, rowIndex);
            WpfControls.Grid.SetColumn(label, 0);
            grid.Children.Add(label);

            var templateBox = new WpfControls.TextBox
            {
                MinHeight = 32,
                Margin = new WpfNs.Thickness(8, 4, 4, 4),
                VerticalContentAlignment = WpfNs.VerticalAlignment.Center
            };
            WpfControls.Grid.SetRow(templateBox, rowIndex);
            WpfControls.Grid.SetColumn(templateBox, 1);
            grid.Children.Add(templateBox);
            _templateBoxes[size] = templateBox;

            var templateBtn = new Wpf.Ui.Controls.Button
            {
                Content = "选择",
                Width = 56,
                Height = 32,
                Margin = new WpfNs.Thickness(0, 4, 0, 4)
            };
            templateBtn.Click += (s, e) => PickInto(templateBox,
                "SolidWorks 工程图模板 (*.drwdot)|*.drwdot|所有文件 (*.*)|*.*");
            WpfControls.Grid.SetRow(templateBtn, rowIndex);
            WpfControls.Grid.SetColumn(templateBtn, 2);
            grid.Children.Add(templateBtn);

            var formatBox = new WpfControls.TextBox
            {
                MinHeight = 32,
                Margin = new WpfNs.Thickness(16, 4, 4, 4),
                VerticalContentAlignment = WpfNs.VerticalAlignment.Center
            };
            WpfControls.Grid.SetRow(formatBox, rowIndex);
            WpfControls.Grid.SetColumn(formatBox, 3);
            grid.Children.Add(formatBox);
            _formatBoxes[size] = formatBox;

            var formatBtn = new Wpf.Ui.Controls.Button
            {
                Content = "选择",
                Width = 56,
                Height = 32,
                Margin = new WpfNs.Thickness(0, 4, 0, 4)
            };
            formatBtn.Click += (s, e) => PickInto(formatBox,
                "SolidWorks 图纸格式 (*.slddrt)|*.slddrt|所有文件 (*.*)|*.*");
            WpfControls.Grid.SetRow(formatBtn, rowIndex);
            WpfControls.Grid.SetColumn(formatBtn, 4);
            grid.Children.Add(formatBtn);

            rowIndex++;
        }

        RowsHost.Items.Add(grid);
    }

    private void PickInto(WpfControls.TextBox target, string filter)
    {
        var dialog = new OpenFileDialog { CheckFileExists = true, Filter = filter };
        if (dialog.ShowDialog(this).GetValueOrDefault())
            target.Text = dialog.FileName;
    }

    private void LoadFromSettings()
    {
        var map = PaperFormatMap.FromJson(Settings.Default.DrawingAutomation_PaperFormatMap);
        foreach (var size in PaperSizeCatalog.Sizes)
        {
            var entry = map.Get(size);
            _templateBoxes[size].Text = entry.TemplatePath;
            _formatBoxes[size].Text = entry.SheetFormatPath;
        }
    }

    private void Ok_Click(object sender, WpfNs.RoutedEventArgs e)
    {
        var map = new PaperFormatMap();
        foreach (var size in PaperSizeCatalog.Sizes)
            map.Set(size, _templateBoxes[size].Text, _formatBoxes[size].Text);

        Settings.Default.DrawingAutomation_PaperFormatMap = map.ToJson();
        Settings.Default.Save();
        DialogResult = true;
        Close();
    }

    private void Cancel_Click(object sender, WpfNs.RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
