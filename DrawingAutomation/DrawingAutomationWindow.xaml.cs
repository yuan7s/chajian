using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Win32;
using WpfControls = System.Windows.Controls;
using WpfInput = System.Windows.Input;
using WpfNs = System.Windows;

namespace ExternalProgram;
using ExternalProgram.Properties;

partial class DrawingAutomationWindow : WpfNs.Window
{
    private readonly ObservableCollection<DrawingAutomationResultRow> _results =
        new ObservableCollection<DrawingAutomationResultRow>();
    private bool _isLoading = true;

    public DrawingAutomationWindow()
    {
        InitializeComponent();
        ResultList.ItemsSource = _results;
    }

    public SwAddinClient Client { get; set; }

    private async void Window_Loaded(object sender, WpfNs.RoutedEventArgs e)
    {
        LoadSettings();
        UpdateTemplatePathState();
        _isLoading = false;
        await RefreshActiveDocumentAsync();
        Activate();
    }

    private void LoadSettings()
    {
        var settings = Settings.Default;
        TemplatePathBox.Text = settings.DrawingAutomation_TemplatePath ?? "";
        StandardPathBox.Text = settings.DrawingAutomation_StandardPath ?? "";
        SheetFormatPathBox.Text = settings.DrawingAutomation_SheetFormatPath ?? "";
        UseDefaultTemplateCheck.IsChecked = settings.DrawingAutomation_UseDefaultTemplate;
        SaveDrawingCheck.IsChecked = settings.DrawingAutomation_SaveDrawing;
        OverwriteDrawingCheck.IsChecked = settings.DrawingAutomation_OverwriteDrawing;
        ApplySettingsCheck.IsChecked = settings.DrawingAutomation_ApplySettings;
        InsertViewsCheck.IsChecked = settings.DrawingAutomation_InsertViews;
        InsertIsoViewCheck.IsChecked = settings.DrawingAutomation_InsertIsoView;
        ImportModelItemsCheck.IsChecked = settings.DrawingAutomation_ImportModelItems;
        DuplicateDimsCheck.IsChecked = settings.DrawingAutomation_DuplicateDimensions;
        HiddenFeatureDimsCheck.IsChecked = settings.DrawingAutomation_HiddenFeatureDimensions;
        UseSketchPlacementCheck.IsChecked = settings.DrawingAutomation_UseSketchPlacement;
        AutoArrangeCheck.IsChecked = settings.DrawingAutomation_AutoArrangeDimensions;
        SelectPaperSize(settings.DrawingAutomation_PaperSize);
    }

    private void SaveSettings()
    {
        var settings = Settings.Default;
        settings.DrawingAutomation_TemplatePath = TemplatePathBox.Text.Trim();
        settings.DrawingAutomation_StandardPath = StandardPathBox.Text.Trim();
        settings.DrawingAutomation_SheetFormatPath = SheetFormatPathBox.Text.Trim();
        settings.DrawingAutomation_UseDefaultTemplate = UseDefaultTemplateCheck.IsChecked.GetValueOrDefault();
        settings.DrawingAutomation_SaveDrawing = SaveDrawingCheck.IsChecked.GetValueOrDefault();
        settings.DrawingAutomation_OverwriteDrawing = OverwriteDrawingCheck.IsChecked.GetValueOrDefault();
        settings.DrawingAutomation_ApplySettings = ApplySettingsCheck.IsChecked.GetValueOrDefault();
        settings.DrawingAutomation_InsertViews = InsertViewsCheck.IsChecked.GetValueOrDefault();
        settings.DrawingAutomation_InsertIsoView = InsertIsoViewCheck.IsChecked.GetValueOrDefault();
        settings.DrawingAutomation_ImportModelItems = ImportModelItemsCheck.IsChecked.GetValueOrDefault();
        settings.DrawingAutomation_DuplicateDimensions = DuplicateDimsCheck.IsChecked.GetValueOrDefault();
        settings.DrawingAutomation_HiddenFeatureDimensions = HiddenFeatureDimsCheck.IsChecked.GetValueOrDefault();
        settings.DrawingAutomation_UseSketchPlacement = UseSketchPlacementCheck.IsChecked.GetValueOrDefault();
        settings.DrawingAutomation_AutoArrangeDimensions = AutoArrangeCheck.IsChecked.GetValueOrDefault();
        settings.DrawingAutomation_PaperSize = GetSelectedPaperSize();
        settings.Save();
    }

    private void TemplateMode_Changed(object sender, WpfNs.RoutedEventArgs e)
    {
        if (_isLoading) return;
        UpdateTemplatePathState();
    }

    private void UpdateTemplatePathState()
    {
        var useDefault = UseDefaultTemplateCheck.IsChecked.GetValueOrDefault();
        TemplatePathBox.IsEnabled = !useDefault;
    }

    private void SelectPaperSize(string size)
    {
        var target = string.IsNullOrWhiteSpace(size) ? "A4" : size.Trim();
        foreach (var obj in PaperSizeBox.Items)
        {
            if (obj is WpfControls.ComboBoxItem item &&
                string.Equals(item.Content?.ToString(), target, StringComparison.OrdinalIgnoreCase))
            {
                PaperSizeBox.SelectedItem = item;
                return;
            }
        }
        PaperSizeBox.SelectedIndex = 0; // 回退 A4
    }

    private string GetSelectedPaperSize()
    {
        return (PaperSizeBox.SelectedItem as WpfControls.ComboBoxItem)?.Content?.ToString() ?? "A4";
    }

    private void BrowseTemplate_Click(object sender, WpfNs.RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            CheckFileExists = true,
            Filter = "SolidWorks 工程图模板或基准图 (*.drwdot;*.slddrw)|*.drwdot;*.slddrw|所有文件 (*.*)|*.*"
        };

        if (dialog.ShowDialog(this).GetValueOrDefault())
        {
            TemplatePathBox.Text = dialog.FileName;
            UseDefaultTemplateCheck.IsChecked = false;
            UpdateTemplatePathState();
        }
    }

    private void BrowseStandard_Click(object sender, WpfNs.RoutedEventArgs e)
    {
        SelectFileInto(StandardPathBox, "SolidWorks 绘图标准 (*.sldstd)|*.sldstd|所有文件 (*.*)|*.*");
    }

    private void BrowseSheetFormat_Click(object sender, WpfNs.RoutedEventArgs e)
    {
        SelectFileInto(SheetFormatPathBox, "SolidWorks 图纸格式 (*.slddrt)|*.slddrt|所有文件 (*.*)|*.*");
    }

    private void SelectFileInto(WpfControls.TextBox target, string filter)
    {
        var dialog = new OpenFileDialog
        {
            CheckFileExists = true,
            Filter = filter
        };

        if (dialog.ShowDialog(this).GetValueOrDefault())
        {
            target.Text = dialog.FileName;
        }
    }

    private async void RefreshButton_Click(object sender, WpfNs.RoutedEventArgs e)
    {
        await RefreshActiveDocumentAsync();
    }

    private async Task RefreshActiveDocumentAsync()
    {
        if (Client == null)
        {
            ActiveDocumentText.Text = "未连接到 SolidWorks";
            return;
        }

        try
        {
            var result = await Client.SendCommandAsync(
                "active-document",
                null,
                TimeSpan.FromSeconds(3)) as Dictionary<string, object>;
            var title = GetString(result, "title");
            var path = GetString(result, "path");
            var type = FormatDocType(GetString(result, "type"));
            var name = !string.IsNullOrWhiteSpace(path) ? Path.GetFileName(path) : title;
            ActiveDocumentText.Text = string.IsNullOrWhiteSpace(name)
                ? "当前: 无文档"
                : "当前: " + name + (string.IsNullOrWhiteSpace(type) ? "" : " / " + type);
        }
        catch (Exception ex)
        {
            ActiveDocumentText.Text = "当前: " + ex.Message;
        }
    }

    private async void GenerateButton_Click(object sender, WpfNs.RoutedEventArgs e)
    {
        await RunCommandAsync("drawing-automation-run", BuildCommandArgs(), "工程图处理完成");
    }

    private async void ImportButton_Click(object sender, WpfNs.RoutedEventArgs e)
    {
        await RunCommandAsync("drawing-import-model-items", BuildCommandArgs(), "模型项目导入完成");
    }

    private async void HoleCalloutButton_Click(object sender, WpfNs.RoutedEventArgs e)
    {
        await RunCommandAsync("drawing-import-hole-callouts", BuildCommandArgs(), "孔标注完成");
    }

    private async void CheckButton_Click(object sender, WpfNs.RoutedEventArgs e)
    {
        await RunCommandAsync("drawing-automation-check", BuildCommandArgs(), "检查完成");
    }

    private void TemplateSettingsButton_Click(object sender, WpfNs.RoutedEventArgs e)
    {
        var window = new DrawingTemplateSettingsWindow { Owner = this };
        window.ShowDialog();
    }

    private async void CancelBatchButton_Click(object sender, WpfNs.RoutedEventArgs e)
    {
        CancelBatchButton.IsEnabled = false;
        try
        {
            if (Client != null)
                await Client.CancelBatchAsync();
        }
        catch
        {
            // Best-effort
        }
    }

    private async void BatchButton_Click(object sender, WpfNs.RoutedEventArgs e)
    {
        if (Client == null)
        {
            WpfNs.MessageBox.Show(this, "未连接到 SolidWorks，请从主界面重新打开。", "提示",
                WpfNs.MessageBoxButton.OK, WpfNs.MessageBoxImage.Information);
            return;
        }

        var folderDialog = new OpenFolderDialog { Title = "选择包含零件/装配体的文件夹" };
        if (!folderDialog.ShowDialog(this).GetValueOrDefault()) return;

        var models = DrawingBatchScanner.Scan(folderDialog.FolderName, recursive: true);
        if (models.Count == 0)
        {
            WpfNs.MessageBox.Show(this, "该文件夹下未找到 .sldprt / .sldasm 文件。", "提示",
                WpfNs.MessageBoxButton.OK, WpfNs.MessageBoxImage.Information);
            return;
        }

        // Show filter dialog for user to narrow down the file list
        var filterWindow = new DrawingBatchFileFilterWindow(models) { Owner = this };
        if (filterWindow.ShowDialog() != true || filterWindow.SelectedPaths.Count == 0)
            return;

        var selected = filterWindow.SelectedPaths;
        var confirm = WpfNs.MessageBox.Show(
            this,
            $"将对 {selected.Count} 个模型按图幅 {GetSelectedPaperSize()} 批量生成工程图，并保存后关闭。是否继续？",
            "批量生成",
            WpfNs.MessageBoxButton.OKCancel,
            WpfNs.MessageBoxImage.Question);
        if (confirm != WpfNs.MessageBoxResult.OK) return;

        var args = BuildBatchCommandArgs(selected);
        // 每个文件给足时间：基础 2 分钟 + 每文件 3 分钟，封顶 2 小时
        var timeout = TimeSpan.FromMinutes(Math.Min(120, 2 + selected.Count * 3));

        SaveSettings();
        SetBusy(true);
        try
        {
            var result = await Client.SendCommandAsync("drawing-batch-run", args, timeout)
                as Dictionary<string, object>;
            ShowResult(result, "批量生成完成");
            await RefreshActiveDocumentAsync();
        }
        catch (Exception ex)
        {
            SummaryText.Text = "批量生成失败";
            _results.Clear();
            _results.Add(new DrawingAutomationResultRow
            {
                Severity = "错误",
                Item = "批量",
                Message = ex.Message,
                Detail = "drawing-batch-run"
            });
        }
        finally
        {
            SetBusy(false);
        }
    }

    private Dictionary<string, object> BuildBatchCommandArgs(List<string> modelPaths)
    {
        var args = BuildCommandArgs();
        var size = GetSelectedPaperSize();
        var map = PaperFormatMap.FromJson(Settings.Default.DrawingAutomation_PaperFormatMap);
        var entry = map.Get(size);

        if (!string.IsNullOrWhiteSpace(entry.TemplatePath))
        {
            args["useDefaultTemplate"] = false;
            args["templatePath"] = entry.TemplatePath;
            args["baseDrawingPath"] = "";
        }
        if (!string.IsNullOrWhiteSpace(entry.SheetFormatPath))
            args["sheetFormatPath"] = entry.SheetFormatPath;

        args["paperSize"] = size;
        args["saveDrawing"] = true;          // 批量必须保存
        args["modelPaths"] = modelPaths.ToArray();
        return args;
    }

    private Dictionary<string, object> BuildCommandArgs()
    {
        var templatePath = TemplatePathBox.Text.Trim();
        var useDefaultTemplate = UseDefaultTemplateCheck.IsChecked.GetValueOrDefault();
        var useBaseDrawing = !useDefaultTemplate &&
                             string.Equals(Path.GetExtension(templatePath), ".slddrw", StringComparison.OrdinalIgnoreCase);

        return new Dictionary<string, object>
        {
            { "templatePath", useDefaultTemplate || useBaseDrawing ? "" : templatePath },
            { "baseDrawingPath", useBaseDrawing ? templatePath : "" },
            { "openCopiedDrawingInProcess", false },
            { "useDefaultTemplate", useDefaultTemplate },
            { "saveDrawing", SaveDrawingCheck.IsChecked.GetValueOrDefault() },
            { "overwriteDrawing", OverwriteDrawingCheck.IsChecked.GetValueOrDefault() },
            { "applyDrawingSettings", ApplySettingsCheck.IsChecked.GetValueOrDefault() },
            { "insertStandardViews", InsertViewsCheck.IsChecked.GetValueOrDefault() },
            { "insertIsoView", InsertIsoViewCheck.IsChecked.GetValueOrDefault() },
            { "importModelItems", ImportModelItemsCheck.IsChecked.GetValueOrDefault() },
            { "duplicateDimensions", DuplicateDimsCheck.IsChecked.GetValueOrDefault() },
            { "hiddenFeatureDimensions", HiddenFeatureDimsCheck.IsChecked.GetValueOrDefault() },
            { "usePlacementInSketch", UseSketchPlacementCheck.IsChecked.GetValueOrDefault() },
            { "autoArrangeDimensions", AutoArrangeCheck.IsChecked.GetValueOrDefault() },
            { "standardPath", StandardPathBox.Text.Trim() },
            { "sheetFormatPath", SheetFormatPathBox.Text.Trim() },
            { "paperSize", GetSelectedPaperSize() }
        };
    }

    private async System.Threading.Tasks.Task RunCommandAsync(
        string command,
        Dictionary<string, object> args,
        string fallbackSummary)
    {
        if (Client == null)
        {
            WpfNs.MessageBox.Show(this, "未连接到 SolidWorks，请从主界面重新打开。", "提示",
                WpfNs.MessageBoxButton.OK, WpfNs.MessageBoxImage.Information);
            return;
        }

        SaveSettings();
        SetBusy(true);
        try
        {
            var result = await Client.SendCommandAsync(command, args, TimeSpan.FromMinutes(10)) as Dictionary<string, object>;
            ShowResult(result, fallbackSummary);
            await RefreshActiveDocumentAsync();
        }
        catch (Exception ex)
        {
            SummaryText.Text = "执行失败";
            _results.Clear();
            _results.Add(new DrawingAutomationResultRow
            {
                Severity = "错误",
                Item = "命令",
                Message = ex.Message,
                Detail = command
            });
        }
        finally
        {
            SetBusy(false);
        }
    }

    private void SetBusy(bool busy)
    {
        GenerateButton.IsEnabled = !busy;
        ImportButton.IsEnabled = !busy;
        HoleCalloutButton.IsEnabled = !busy;
        CheckButton.IsEnabled = !busy;
        BatchButton.IsEnabled = !busy;
        TemplateSettingsButton.IsEnabled = !busy;
        CancelBatchButton.Visibility = busy ? WpfNs.Visibility.Visible : WpfNs.Visibility.Collapsed;
        WpfInput.Mouse.OverrideCursor = busy ? WpfInput.Cursors.Wait : null;
    }

    private void ShowResult(Dictionary<string, object> result, string fallbackSummary)
    {
        _results.Clear();
        SummaryText.Text = GetString(result, "summary", fallbackSummary);

        var issues = GetDictionaryList(result, "issues").ToArray();
        foreach (var issue in issues)
        {
            _results.Add(new DrawingAutomationResultRow
            {
                Severity = FormatSeverity(GetString(issue, "severity")),
                Item = GetString(issue, "item"),
                Message = GetString(issue, "message"),
                Detail = GetString(issue, "detail")
            });
        }

        if (_results.Count == 0)
        {
            _results.Add(new DrawingAutomationResultRow
            {
                Severity = "通过",
                Item = "检查",
                Message = "未发现遗漏项",
                Detail = ""
            });
        }
    }

    private static string GetString(Dictionary<string, object> dict, string key, string fallback = "")
    {
        if (dict == null || !dict.TryGetValue(key, out var value) || value == null) return fallback;
        var text = value.ToString();
        return string.IsNullOrWhiteSpace(text) ? fallback : text;
    }

    private static IEnumerable<Dictionary<string, object>> GetDictionaryList(Dictionary<string, object> dict, string key)
    {
        if (dict == null || !dict.TryGetValue(key, out var raw) || raw == null)
            yield break;

        if (raw is IEnumerable<object> list)
        {
            foreach (var item in list)
                if (item is Dictionary<string, object> issue)
                    yield return issue;
        }
    }

    private static string FormatDocType(string value)
    {
        switch (value)
        {
            case "1": return "零件";
            case "2": return "装配体";
            case "3": return "工程图";
            default: return value;
        }
    }

    private static string FormatSeverity(string value)
    {
        if (string.Equals(value, "error", StringComparison.OrdinalIgnoreCase)) return "错误";
        if (string.Equals(value, "warning", StringComparison.OrdinalIgnoreCase)) return "警告";
        if (string.Equals(value, "pass", StringComparison.OrdinalIgnoreCase)) return "通过";
        if (string.Equals(value, "info", StringComparison.OrdinalIgnoreCase)) return "信息";
        return string.IsNullOrWhiteSpace(value) ? "信息" : value;
    }

    public sealed class DrawingAutomationResultRow
    {
        public string Severity { get; set; }
        public string Item { get; set; }
        public string Message { get; set; }
        public string Detail { get; set; }
    }
}
