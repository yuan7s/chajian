using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows.Forms;
using System.Windows.Threading;
using Drawing = System.Drawing;
using WpfNs = System.Windows;
using WpfControls = System.Windows.Controls;
using WpfMedia = System.Windows.Media;

namespace ExternalProgram;
using ExternalProgram.Properties;

partial class RenameWindow : WpfNs.Window
{
    private static readonly List<RenameWindow> OpenWindows = new List<RenameWindow>();
    private SwAddinClient _client;
    private bool _suppressNameCheck;
    private readonly DispatcherTimer _nameCheckTimer;
    private readonly DispatcherTimer _docWatchTimer;
    private string _lastActiveDocKey;
    private string _lastTargetDocKey;

    public RenameWindow()
    {
        InitializeComponent();
        Topmost = true;
        LoadDefaultParameters();

        _nameCheckTimer = new DispatcherTimer() { Interval = TimeSpan.FromMilliseconds(500) };
        _nameCheckTimer.Tick += NameCheckTimer_Tick;

        _docWatchTimer = new DispatcherTimer() { Interval = TimeSpan.FromMilliseconds(400) };
        _docWatchTimer.Tick += DocWatchTimer_Tick;

        Closed += RenameWindow_Closed;
        OpenWindows.Add(this);
    }

    private void LoadDefaultParameters()
    {
        // Rename parameter controls were moved to SettingsWindow.
    }

    private static string GetRenamePropertyTarget()
    {
        var target = Properties.Settings.Default.Rename_PropertyTarget;
        return string.Equals(target, "custom", StringComparison.OrdinalIgnoreCase) ? "custom" : "configuration";
    }

    private static RenameParameterDefaults GetRenameParameterDefaults()
    {
        var settings = Properties.Settings.Default;
        var renameProperties = GetRenamePropertyDefaults();
        var designProperty = renameProperties.FirstOrDefault(item => IsDesignPropertyName(GetPropertyName(item)));
        var versionProperty = renameProperties.FirstOrDefault(item => IsVersionPropertyName(GetPropertyName(item)));

        return new RenameParameterDefaults
        {
            WriteFileName = settings.Rename_WriteFileName,
            WriteMaterialCode = settings.Rename_WriteMaterialCode,
            WritePartNumber = settings.Rename_WritePartNumber,
            WriteBlankSize = settings.Rename_WriteBlankSize,
            WriteDesign = designProperty != null || settings.Rename_WriteDesign,
            DesignText = designProperty != null ? GetPropertyValue(designProperty) : settings.Rename_DesignText ?? "",
            WriteVersion = versionProperty != null || settings.Rename_WriteVersion,
            VersionText = versionProperty != null
                ? GetPropertyValue(versionProperty)
                : string.IsNullOrWhiteSpace(settings.Rename_VersionText) ? "A" : settings.Rename_VersionText,
            RenameProperties = renameProperties
        };
    }

    private static string ToCommandBool(bool value)
    {
        return value ? "true" : "false";
    }

    private static List<Dictionary<string, object>> GetRenamePropertyDefaults()
    {
        var raw = Properties.Settings.Default.Rename_CustomProperties;
        if (string.IsNullOrWhiteSpace(raw)) return new List<Dictionary<string, object>>();

        try
        {
            var items = JsonSerializer.Deserialize<List<Dictionary<string, object>>>(raw);
            if (items == null) return new List<Dictionary<string, object>>();

            return items
                .Where(item => !string.IsNullOrWhiteSpace(GetPropertyName(item)))
                .Select(item => new Dictionary<string, object>
                {
                    { "name", GetPropertyName(item).Trim() },
                    { "value", GetPropertyValue(item) }
                })
                .ToList();
        }
        catch
        {
            return new List<Dictionary<string, object>>();
        }
    }

    private static string GetPropertyName(Dictionary<string, object> item)
    {
        if (item == null) return "";
        if (item.TryGetValue("Name", out var name) && name != null) return name.ToString();
        if (item.TryGetValue("name", out name) && name != null) return name.ToString();
        return "";
    }

    private static string GetPropertyValue(Dictionary<string, object> item)
    {
        if (item == null) return "";
        if (item.TryGetValue("Value", out var value) && value != null) return value.ToString();
        if (item.TryGetValue("value", out value) && value != null) return value.ToString();
        return "";
    }

    private static bool IsDesignPropertyName(string name)
    {
        return string.Equals(name, "设计出图", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(name, "设计", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsVersionPropertyName(string name)
    {
        return string.Equals(name, "版本", StringComparison.OrdinalIgnoreCase);
    }

    private sealed class RenameParameterDefaults
    {
        public bool WriteFileName { get; init; }
        public bool WriteMaterialCode { get; init; }
        public bool WritePartNumber { get; init; }
        public bool WriteBlankSize { get; init; }
        public bool WriteDesign { get; init; }
        public string DesignText { get; init; } = "";
        public bool WriteVersion { get; init; }
        public string VersionText { get; init; } = "A";
        public List<Dictionary<string, object>> RenameProperties { get; init; } = new List<Dictionary<string, object>>();
    }

    public SwAddinClient Client
    {
        get => _client;
        set
        {
            if (!object.ReferenceEquals(_client, value))
            {
                _lastActiveDocKey = null;
                _lastTargetDocKey = null;
                if (_client != null)
                {
                    _client.DocChanged -= Client_DocChanged;
                    _client.SelectionChanged -= Client_SelectionChanged;
                }
                _client = value;
                if (_client != null)
                {
                    _client.DocChanged += Client_DocChanged;
                    _client.SelectionChanged += Client_SelectionChanged;
                    _docWatchTimer.Start();
                }
                else
                {
                    _docWatchTimer.Stop();
                }
            }
            _suppressNameCheck = true;
            UpdateSelectionInfo();
            _suppressNameCheck = false;
        }
    }

    public static void UpdateOpenWindowsClient(SwAddinClient client)
    {
        foreach (var window in OpenWindows.ToArray())
        {
            if (window != null) window.Client = client;
        }
    }

    public static bool ActivateExisting(SwAddinClient client)
    {
        var window = OpenWindows.FirstOrDefault(item => item != null && item.IsVisible);
        if (window == null) return false;

        window.Client = client;
        if (window.WindowState == WpfNs.WindowState.Minimized)
            window.WindowState = WpfNs.WindowState.Normal;

        window.Activate();
        window.Topmost = true;
        window.Focus();
        return true;
    }

    private void RenameWindow_Closed(object sender, EventArgs e)
    {
        _nameCheckTimer.Stop();
        _docWatchTimer.Stop();
        if (_client != null)
        {
            _client.DocChanged -= Client_DocChanged;
            _client.SelectionChanged -= Client_SelectionChanged;
        }
        _client = null;
        OpenWindows.Remove(this);
    }

    private void Client_DocChanged(string title, string path)
    {
        Dispatcher.BeginInvoke(new Action(UpdateSelectionInfo));
    }

    private void Client_SelectionChanged(string name, string type)
    {
        _lastTargetDocKey = null;
        Dispatcher.BeginInvoke(new Action(UpdateSelectionInfo));
    }

    private async void DocWatchTimer_Tick(object sender, EventArgs e)
    {
        try
        {
            if (_client == null) return;
            var result = await _client.SendCommandAsync("rename-target");
            var dict = result as Dictionary<string, object>;
            if (dict == null) return;

            var key = "";
            var path = "";
            if (dict.ContainsKey("path") && dict["path"] != null)
                path = dict["path"].ToString();
            var title = "";
            if (dict.ContainsKey("title") && dict["title"] != null)
                title = dict["title"].ToString();
            key = string.IsNullOrEmpty(path) ? title : path;

            var targetKey = "";
            if (dict.ContainsKey("targetPath"))
            {
                targetKey = dict["targetPath"] != null ? dict["targetPath"].ToString() : "";
            }

            if (!string.Equals(_lastActiveDocKey, key, StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(_lastTargetDocKey, targetKey, StringComparison.OrdinalIgnoreCase))
            {
                _lastActiveDocKey = key;
                _lastTargetDocKey = targetKey;
                UpdateSelectionInfo();
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine("RenameWindow.DocWatchTimer_Tick failed: " + ex.Message);
        }
    }

    private async void UpdateSelectionInfo()
    {
        _suppressNameCheck = true;
        try
        {
            if (_client == null)
            {
                ClearFileInfo();
                return;
            }

            var result = await _client.SendCommandAsync("rename-target");
            var dict = result as Dictionary<string, object>;
            if (dict == null)
            {
                ClearFileInfo();
                return;
            }

            var filePath = "";
            if (dict.ContainsKey("targetPath") && dict["targetPath"] != null)
                filePath = dict["targetPath"].ToString();
            if (string.IsNullOrWhiteSpace(filePath) && dict.ContainsKey("path") && dict["path"] != null)
                filePath = dict["path"].ToString();
            var title = "";
            if (dict.ContainsKey("baseName") && dict["baseName"] != null)
                title = dict["baseName"].ToString();
            if (string.IsNullOrWhiteSpace(title) && dict.ContainsKey("targetTitle") && dict["targetTitle"] != null)
                title = dict["targetTitle"].ToString();
            if (string.IsNullOrWhiteSpace(title) && dict.ContainsKey("title") && dict["title"] != null)
                title = dict["title"].ToString();
            var docType = "";
            if (dict.ContainsKey("type") && dict["type"] != null)
                docType = dict["type"].ToString();

            var baseName = string.IsNullOrEmpty(filePath) ? title : Path.GetFileNameWithoutExtension(filePath);
            OldNameBox.Text = baseName;
            NewNameBox.Text = baseName;

            var ext = "";
            if (dict.ContainsKey("extension") && dict["extension"] != null)
                ext = dict["extension"].ToString();
            if (string.IsNullOrWhiteSpace(ext))
                ext = string.IsNullOrEmpty(filePath) ? DocTypeToExtensionString(docType) : Path.GetExtension(filePath).ToLowerInvariant();
            OldExtText.Text = ext;
            NewExtText.Text = ext;
            UpdateDrawingExistsIndicator(filePath);

            var activePath = filePath;
            _lastActiveDocKey = string.IsNullOrEmpty(activePath) ? title : activePath;
            if (dict.ContainsKey("targetPath") && dict["targetPath"] != null)
                _lastTargetDocKey = dict["targetPath"].ToString();
            else
                _lastTargetDocKey = _lastActiveDocKey;
        }
        catch (Exception ex)
        {
            Debug.WriteLine("RenameWindow.UpdateSelectionInfo failed: " + ex.Message);
        }
        finally
        {
            _suppressNameCheck = false;
            NameStatusBorder.Visibility = WpfNs.Visibility.Collapsed;
        }
    }

    private void ClearFileInfo()
    {
        OldNameBox.Text = "";
        NewNameBox.Text = "";
        OldExtText.Text = "";
        NewExtText.Text = "";
        DrawingStatusBorder.Visibility = WpfNs.Visibility.Collapsed;
    }

    private string DocTypeToExtensionString(string docType)
    {
        switch (docType?.ToUpperInvariant())
        {
            case "PART": return ".sldprt";
            case "ASSEMBLY": return ".sldasm";
            case "DRAWING": return ".slddrw";
            default: return "";
        }
    }

    private void UpdateDrawingExistsIndicator(string modelPath)
    {
        if (string.IsNullOrEmpty(modelPath))
        {
            DrawingStatusBorder.Visibility = WpfNs.Visibility.Collapsed;
            return;
        }

        try
        {
            var dir = Path.GetDirectoryName(modelPath);
            var baseName = Path.GetFileNameWithoutExtension(modelPath);
            if (string.IsNullOrEmpty(dir) || string.IsNullOrEmpty(baseName))
            {
                DrawingStatusBorder.Visibility = WpfNs.Visibility.Collapsed;
                return;
            }

            var drawingPath = Path.Combine(dir, baseName + ".SLDDRW");
            DrawingStatusText.Text = File.Exists(drawingPath) ? "存在工程图" : "无工程图";
            DrawingStatusBorder.Background = File.Exists(drawingPath)
                ? new WpfMedia.SolidColorBrush(WpfMedia.Color.FromRgb(50, 205, 50))
                : new WpfMedia.SolidColorBrush(WpfMedia.Color.FromRgb(220, 38, 38));
            DrawingStatusBorder.Visibility = WpfNs.Visibility.Visible;
        }
        catch (Exception ex)
        {
            Debug.WriteLine("RenameWindow.UpdateDrawingExistsIndicator failed: " + ex.Message);
            DrawingStatusBorder.Visibility = WpfNs.Visibility.Collapsed;
        }
    }

    private void NewNameBox_TextChanged(object sender, WpfControls.TextChangedEventArgs e)
    {
        if (_suppressNameCheck) return;
        _nameCheckTimer.Stop();
        _nameCheckTimer.Start();
    }

    private async void NameCheckTimer_Tick(object sender, EventArgs e)
    {
        _nameCheckTimer.Stop();
        await CheckNewNameConflictAsync();
    }

    private async System.Threading.Tasks.Task CheckNewNameConflictAsync()
    {
        var newName = NewNameBox.Text.Trim();
        if (string.IsNullOrEmpty(newName))
        {
            ResetNewNameIndicators();
            NameStatusBorder.Visibility = WpfNs.Visibility.Collapsed;
            return;
        }

        try
        {
            if (_client == null) return;

            var args = new Dictionary<string, object>
            {
                { "newName", newName }
            };
            var result = await _client.SendCommandAsync("check-name-conflict", args);
            var dict = result as Dictionary<string, object>;
            if (dict == null) return;

            var conflict = false;
            var existsOpen = false;
            var existsFile = false;
            if (dict.ContainsKey("conflict"))
            {
                var conflictVal = dict["conflict"] != null ? dict["conflict"].ToString() : null;
                bool.TryParse(conflictVal, out conflict);
            }
            if (dict.ContainsKey("existsOpen"))
            {
                var openVal = dict["existsOpen"] != null ? dict["existsOpen"].ToString() : null;
                bool.TryParse(openVal, out existsOpen);
            }
            if (dict.ContainsKey("existsFile"))
            {
                var fileVal = dict["existsFile"] != null ? dict["existsFile"].ToString() : null;
                bool.TryParse(fileVal, out existsFile);
            }

            if (conflict)
            {
                NewNameBox.Background = new WpfMedia.SolidColorBrush(WpfMedia.Color.FromRgb(255, 228, 225));
                NameStatusText.Foreground = WpfMedia.Brushes.Red;
                NameStatusText.Text = existsOpen && existsFile ? "重名(打开+本地)" : existsOpen ? "重名(已打开)" : "重名(本地)";
                NameStatusBorder.Visibility = WpfNs.Visibility.Visible;
            }
            else
            {
                NewNameBox.Background = new WpfMedia.SolidColorBrush(WpfMedia.Color.FromRgb(144, 238, 144));
                NameStatusText.Text = "可保存";
                NameStatusText.Foreground = WpfMedia.Brushes.Green;
                NameStatusBorder.Visibility = WpfNs.Visibility.Visible;
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine("RenameWindow.CheckNewNameConflictAsync failed: " + ex.Message);
        }
    }

    private void ResetNewNameIndicators()
    {
        NewNameBox.ClearValue(WpfControls.Control.BackgroundProperty);
        NameStatusText.Foreground = WpfMedia.Brushes.Green;
    }

    private void RenameButton_Click(object sender, WpfNs.RoutedEventArgs e)
    {
        RenameSelectedOrActiveDocument();
    }

    private void SaveAsButton_Click(object sender, WpfNs.RoutedEventArgs e)
    {
        SaveAsNewDocumentAndOpen();
    }

    private void SaveAsReplaceButton_Click(object sender, WpfNs.RoutedEventArgs e)
    {
        SaveAsAndReplaceSelectedComponent();
    }

    private async void SaveAsNewDocumentAndOpen()
    {
        var newName = NewNameBox.Text.Trim();
        if (string.IsNullOrEmpty(newName))
        {
            MessageBox.Show("请输入新文件名", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        try
        {
            if (_client == null)
            {
                MessageBox.Show("未连接到 SolidWorks", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            var propertyTarget = GetRenamePropertyTarget();
            var defaults = GetRenameParameterDefaults();
            var args = new Dictionary<string, object>
            {
                { "newName", newName },
                { "propertyTarget", propertyTarget },
                { "source", propertyTarget },
                { "customProperties", propertyTarget == "custom" ? "true" : "false" },
                { "renameProperties", defaults.RenameProperties },
                { "fileName", ToCommandBool(defaults.WriteFileName) },
                { "materialCode", ToCommandBool(defaults.WriteMaterialCode) },
                { "partNumber", ToCommandBool(defaults.WritePartNumber) },
                { "version", ToCommandBool(defaults.WriteVersion) },
                { "design", ToCommandBool(defaults.WriteDesign) },
                { "blankSize", ToCommandBool(defaults.WriteBlankSize) },
                { "versionText", defaults.VersionText.Trim() },
                { "designText", defaults.DesignText.Trim() },
                { "copyDrawing", "true" }
            };
            var result = await _client.SendCommandAsync("save-as-new", args);
            var dict = result as Dictionary<string, object>;
            if (dict != null && dict.ContainsKey("success") && Convert.ToBoolean(dict["success"]))
            {
                UpdateSelectionInfo();
                ShowAutoCloseNotice("另存完成");
                ShowDrawingWarningIfNeeded(dict);
            }
            else
            {
                var errMsg = "另存失败";
                if (dict != null && dict.ContainsKey("error")) errMsg = dict["error"].ToString();
                MessageBox.Show(errMsg, "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show("另存失败: " + ex.Message, "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private async void RenameSelectedOrActiveDocument()
    {
        var newName = NewNameBox.Text.Trim();
        if (string.IsNullOrEmpty(newName))
        {
            MessageBox.Show("请输入新文件名", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        try
        {
            if (_client == null)
            {
                MessageBox.Show("未连接到 SolidWorks", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            var propertyTarget = GetRenamePropertyTarget();
            var defaults = GetRenameParameterDefaults();
            var args = new Dictionary<string, object>
            {
                { "newName", newName },
                { "propertyTarget", propertyTarget },
                { "source", propertyTarget },
                { "customProperties", propertyTarget == "custom" ? "true" : "false" },
                { "renameProperties", defaults.RenameProperties },
                { "fileName", ToCommandBool(defaults.WriteFileName) },
                { "materialCode", ToCommandBool(defaults.WriteMaterialCode) },
                { "partNumber", ToCommandBool(defaults.WritePartNumber) },
                { "version", ToCommandBool(defaults.WriteVersion) },
                { "design", ToCommandBool(defaults.WriteDesign) },
                { "blankSize", ToCommandBool(defaults.WriteBlankSize) },
                { "versionText", defaults.VersionText.Trim() },
                { "designText", defaults.DesignText.Trim() },
                { "copyDrawing", "true" }
            };
            var result = await _client.SendCommandAsync("rename-component", args);
            var dict = result as Dictionary<string, object>;
            if (dict != null && dict.ContainsKey("success") && Convert.ToBoolean(dict["success"]))
            {
                UpdateSelectionInfo();
                ShowAutoCloseNotice("重命名完成");
                ShowDrawingWarningIfNeeded(dict);
            }
            else
            {
                var errMsg = "重命名失败";
                if (dict != null && dict.ContainsKey("error")) errMsg = dict["error"].ToString();
                MessageBox.Show(errMsg, "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show("重命名失败: " + ex.Message, "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private async void SaveAsAndReplaceSelectedComponent()
    {
        var newName = NewNameBox.Text.Trim();
        if (string.IsNullOrEmpty(newName))
        {
            MessageBox.Show("请输入新文件名", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        try
        {
            if (_client == null)
            {
                MessageBox.Show("未连接到 SolidWorks", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            var propertyTarget = GetRenamePropertyTarget();
            var defaults = GetRenameParameterDefaults();
            var args = new Dictionary<string, object>
            {
                { "newName", newName },
                { "propertyTarget", propertyTarget },
                { "source", propertyTarget },
                { "customProperties", propertyTarget == "custom" ? "true" : "false" },
                { "renameProperties", defaults.RenameProperties },
                { "fileName", ToCommandBool(defaults.WriteFileName) },
                { "materialCode", ToCommandBool(defaults.WriteMaterialCode) },
                { "partNumber", ToCommandBool(defaults.WritePartNumber) },
                { "version", ToCommandBool(defaults.WriteVersion) },
                { "design", ToCommandBool(defaults.WriteDesign) },
                { "blankSize", ToCommandBool(defaults.WriteBlankSize) },
                { "versionText", defaults.VersionText.Trim() },
                { "designText", defaults.DesignText.Trim() },
                { "copyDrawing", "true" }
            };
            var result = await _client.SendCommandAsync("save-as-replace", args);
            var dict = result as Dictionary<string, object>;
            if (dict != null && dict.ContainsKey("success") && Convert.ToBoolean(dict["success"]))
            {
                UpdateSelectionInfo();
                ShowAutoCloseNotice("另存替换完成");
                ShowDrawingWarningIfNeeded(dict);
            }
            else
            {
                var errMsg = "另存替换失败";
                if (dict != null && dict.ContainsKey("error")) errMsg = dict["error"].ToString();
                MessageBox.Show(errMsg, "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show("另存替换失败: " + ex.Message, "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void ShowDrawingWarningIfNeeded(Dictionary<string, object> result)
    {
        if (result == null || !result.TryGetValue("drawing", out var rawDrawing)) return;

        var drawing = rawDrawing as Dictionary<string, object>;
        if (drawing == null) return;

        var attempted = GetDictionaryBool(drawing, "attempted");
        var success = GetDictionaryBool(drawing, "success");
        if (!attempted || success) return;

        var message = "工程图关联失败，文件改名已继续完成。";
        if (drawing.TryGetValue("error", out var error) && error != null && !string.IsNullOrWhiteSpace(error.ToString()))
            message += Environment.NewLine + FormatDrawingCopyError(error.ToString());
        if (drawing.TryGetValue("path", out var path) && path != null && !string.IsNullOrWhiteSpace(path.ToString()))
            message += Environment.NewLine + path;

        MessageBox.Show(message, "工程图关联错误", MessageBoxButtons.OK, MessageBoxIcon.Warning);
    }

    private static string FormatDrawingCopyError(string code)
    {
        switch (code)
        {
            case "target_drawing_exists":
                return "目标工程图已存在，已继续改名但未复制工程图。";
            case "source_drawing_reference_mismatch":
                return "原工程图未关联当前模型，已继续改名但未复制工程图。";
            case "drawing_reference_replace_failed":
                return "工程图引用替换失败，已继续改名但未保留新工程图。";
            case "drawing_copy_failed":
                return "工程图处理失败，已继续改名。";
            default:
                return "工程图处理失败，已继续改名。";
        }
    }

    private static bool GetDictionaryBool(Dictionary<string, object> dict, string key)
    {
        if (dict == null || !dict.TryGetValue(key, out var value) || value == null) return false;
        if (value is bool boolValue) return boolValue;
        return bool.TryParse(value.ToString(), out var parsed) && parsed;
    }
    private void ShowAutoCloseNotice(string message, string title = "提示")
    {
        try
        {
            StopNoticeTimer();

            AutoCloseNoticeText.Text = message;
            AutoCloseNoticePopup.IsOpen = true;

            _noticeTimer = new System.Windows.Threading.DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(2)
            };
            _noticeTimer.Tick += (_, _) =>
            {
                _noticeTimer?.Stop();
                try { AutoCloseNoticePopup.IsOpen = false; }
                catch { }
            };
            _noticeTimer.Start();
        }
        catch (Exception ex)
        {
            Debug.WriteLine("RenameWindow.ShowAutoCloseNotice failed: " + ex.Message);
        }
    }

    private void StopNoticeTimer()
    {
        try { _noticeTimer?.Stop(); } catch { }
        _noticeTimer = null;
    }

    private System.Windows.Threading.DispatcherTimer _noticeTimer;
}
