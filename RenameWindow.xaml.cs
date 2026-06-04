using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Windows.Forms;
using System.Windows.Threading;
using Drawing = System.Drawing;
using WpfNs = System.Windows;
using WpfControls = System.Windows.Controls;
using WpfMedia = System.Windows.Media;
using WpfUiControls = Wpf.Ui.Controls;

namespace 外部程序;

partial class RenameWindow : WpfUiControls.FluentWindow
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

        _nameCheckTimer = new DispatcherTimer() { Interval = TimeSpan.FromMilliseconds(500) };
        _nameCheckTimer.Tick += NameCheckTimer_Tick;

        _docWatchTimer = new DispatcherTimer() { Interval = TimeSpan.FromMilliseconds(400) };
        _docWatchTimer.Tick += DocWatchTimer_Tick;

        Closed += RenameWindow_Closed;
        OpenWindows.Add(this);
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
            var result = await _client.SendCommandAsync("active-document");
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

            var result = await _client.SendCommandAsync("active-document");
            var dict = result as Dictionary<string, object>;
            if (dict == null)
            {
                ClearFileInfo();
                return;
            }

            var filePath = "";
            if (dict.ContainsKey("path") && dict["path"] != null)
                filePath = dict["path"].ToString();
            var title = "";
            if (dict.ContainsKey("title") && dict["title"] != null)
                title = dict["title"].ToString();
            var docType = "";
            if (dict.ContainsKey("type") && dict["type"] != null)
                docType = dict["type"].ToString();

            var baseName = string.IsNullOrEmpty(filePath) ? title : Path.GetFileNameWithoutExtension(filePath);
            OldNameBox.Text = baseName;
            NewNameBox.Text = baseName;

            var ext = string.IsNullOrEmpty(filePath) ? DocTypeToExtensionString(docType) : Path.GetExtension(filePath).ToLowerInvariant();
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
        SaveAsAndReplaceSelectedComponent();
    }

    private void SaveAsButton_Click(object sender, WpfNs.RoutedEventArgs e)
    {
        SaveAsNewDocumentAndOpen();
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

            var args = new Dictionary<string, object>
            {
                { "newName", newName },
                { "fileName", FileNameCheck.IsChecked.GetValueOrDefault() ? "true" : "false" },
                { "materialCode", MaterialCodeCheck.IsChecked.GetValueOrDefault() ? "true" : "false" },
                { "partNumber", PartNumberCheck.IsChecked.GetValueOrDefault() ? "true" : "false" },
                { "version", VersionCheck.IsChecked.GetValueOrDefault() ? "true" : "false" },
                { "design", DesignCheck.IsChecked.GetValueOrDefault() ? "true" : "false" },
                { "blankSize", BlankSizeCheck.IsChecked.GetValueOrDefault() ? "true" : "false" },
                { "versionText", VersionBox.Text.Trim() },
                { "designText", DesignBox.Text.Trim() },
                { "copyDrawing", "true" }
            };
            var result = await _client.SendCommandAsync("save-as-new", args);
            var dict = result as Dictionary<string, object>;
            if (dict != null && dict.ContainsKey("success") && Convert.ToBoolean(dict["success"]))
            {
                UpdateSelectionInfo();
                ShowAutoCloseNotice("另存完成");
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

            var args = new Dictionary<string, object>
            {
                { "newName", newName },
                { "fileName", FileNameCheck.IsChecked.GetValueOrDefault() ? "true" : "false" },
                { "materialCode", MaterialCodeCheck.IsChecked.GetValueOrDefault() ? "true" : "false" },
                { "partNumber", PartNumberCheck.IsChecked.GetValueOrDefault() ? "true" : "false" },
                { "version", VersionCheck.IsChecked.GetValueOrDefault() ? "true" : "false" },
                { "design", DesignCheck.IsChecked.GetValueOrDefault() ? "true" : "false" },
                { "blankSize", BlankSizeCheck.IsChecked.GetValueOrDefault() ? "true" : "false" },
                { "versionText", VersionBox.Text.Trim() },
                { "designText", DesignBox.Text.Trim() },
                { "copyDrawing", "true" }
            };
            var result = await _client.SendCommandAsync("rename-component", args);
            var dict = result as Dictionary<string, object>;
            if (dict != null && dict.ContainsKey("success") && Convert.ToBoolean(dict["success"]))
            {
                UpdateSelectionInfo();
                ShowAutoCloseNotice("重命名完成");
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

    private void ShowAutoCloseNotice(string message, string title = "提示")
    {
        try
        {
            var ni = new NotifyIcon();
            ni.Icon = Drawing.SystemIcons.Information;
            ni.Visible = true;
            ni.BalloonTipTitle = title;
            ni.BalloonTipText = message;
            ni.BalloonTipIcon = ToolTipIcon.Info;
            ni.ShowBalloonTip(1800);

            var t = new Timer() { Interval = 2200 };
            t.Tick += (s, ev) =>
            {
                t.Stop();
                t.Dispose();
                ni.Visible = false;
                ni.Dispose();
            };
            t.Start();
        }
        catch (Exception ex)
        {
            Debug.WriteLine("RenameWindow.ShowAutoCloseNotice failed: " + ex.Message);
        }
    }
}
