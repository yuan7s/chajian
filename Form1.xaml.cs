using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using WinForms = System.Windows.Forms;
using Drawing = System.Drawing;
using WpfNs = System.Windows;
using WpfControls = System.Windows.Controls;
using WpfInput = System.Windows.Input;
using WpfInterop = System.Windows.Interop;
using WpfMedia = System.Windows.Media;
using WpfPrimitives = System.Windows.Controls.Primitives;
using 外部程序.Properties;

namespace 外部程序;

partial class Form1
{
    public Form1()
    {
        InitializeComponent();
    }

    private WinForms.Timer _statusTimer;
    private WinForms.NotifyIcon _trayIcon;
    private WinForms.ContextMenuStrip _trayMenu;
    private WinForms.Form _sortProgressForm;
    private WinForms.Label _sortProgressLabel;
    private bool _allowClose;
    private IntPtr _mainWindowHandle;
    private SwAddinClient _client;
    private const double ToolbarWindowWidthPadding = 40.0;

    private const int ModControl = 0x2;
    private const int VkF1 = 0x70;
    private const int WmHotkey = 0x312;
    private const int HotkeyId = 1;

    [DllImport("user32.dll")]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, int fsModifiers, int vk);

    [DllImport("user32.dll")]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    private async void Button1_Click(object sender, EventArgs e)
    {
        try
        {
            await _client.SendCommandAsync("save-dwg");
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show("保存 DWG 失败: " + ex.Message);
        }
    }

    private async void Form1_Load(object sender, WpfNs.RoutedEventArgs e)
    {
        _trayMenu = new WinForms.ContextMenuStrip();
        _trayMenu.Items.Add("显示主窗口", null, TrayShow_Click);
        _trayMenu.Items.Add("-");
        _trayMenu.Items.Add("退出", null, TrayExit_Click);

        _trayIcon = new WinForms.NotifyIcon()
        {
            Icon = Drawing.SystemIcons.Application,
            Text = "外部程序",
            Visible = true,
            ContextMenuStrip = _trayMenu
        };
        _trayIcon.DoubleClick += TrayShow_Click;

        _client = new SwAddinClient();
        _client.DocChanged += OnAddinDocChanged;
        _client.SelectionChanged += OnAddinSelectionChanged;
        _client.Disconnected += OnAddinDisconnected;
        await _client.ConnectAsync();
        await UpdateStatusBar();

        _statusTimer = new WinForms.Timer() { Interval = 5000 };
        _statusTimer.Tick += StatusTimer_Tick;
        _statusTimer.Start();

        QueueAdjustWindowWidthToToolbar();
    }

    private void Form1_SourceInitialized(object sender, EventArgs e)
    {
        _mainWindowHandle = new WpfInterop.WindowInteropHelper(this).Handle;
        var source = WpfNs.PresentationSource.FromVisual(this) as WpfInterop.HwndSource;
        if (source != null) source.AddHook(WndProc);
        if (!RegisterHotKey(_mainWindowHandle, HotkeyId, ModControl, VkF1))
        {
            System.Windows.MessageBox.Show("快捷键 Ctrl+F1 注册失败，可能已被其他程序占用。");
        }
    }

    private void WindowDragArea_MouseLeftButtonDown(object sender, WpfInput.MouseButtonEventArgs e)
    {
        if (e.ButtonState != WpfInput.MouseButtonState.Pressed) return;
        if (IsInteractiveElement(e.OriginalSource as WpfNs.DependencyObject)) return;

        try
        {
            DragMove();
        }
        catch
        {
            // DragMove can throw if the mouse state changes during the drag gesture.
        }
    }

    private static bool IsInteractiveElement(WpfNs.DependencyObject source)
    {
        while (source != null)
        {
            if (source is WpfPrimitives.ButtonBase ||
                source is WpfPrimitives.Thumb ||
                source is WpfControls.TextBox ||
                source is WpfControls.ComboBox ||
                source is WpfPrimitives.ScrollBar)
            {
                return true;
            }

            source = WpfMedia.VisualTreeHelper.GetParent(source);
        }

        return false;
    }

    private async void StatusTimer_Tick(object sender, EventArgs e)
    {
        await UpdateStatusBar();
    }

    private void Form1_Closing(object sender, CancelEventArgs e)
    {
        if (!_allowClose)
        {
            e.Cancel = true;
            Hide();
        }
    }

    private void Form1_Closed(object sender, EventArgs e)
    {
        if (_mainWindowHandle != IntPtr.Zero) UnregisterHotKey(_mainWindowHandle, HotkeyId);
        _client?.Dispose();
        _client = null;
        if (_trayIcon != null)
        {
            _trayIcon.Visible = false;
            _trayIcon.Dispose();
            _trayIcon = null;
        }
        if (_trayMenu != null)
        {
            _trayMenu.Dispose();
            _trayMenu = null;
        }
        if (_statusTimer != null)
        {
            _statusTimer.Stop();
            _statusTimer.Dispose();
            _statusTimer = null;
        }
    }

    private async Task UpdateStatusBar()
    {
        if (_client != null)
        {
            try
            {
                var info = await _client.SendCommandAsync("active-document");
                if (info != null)
                {
                    var dict = info as Dictionary<string, object>;
                    if (dict != null)
                    {
                        string path = "";
                        if (dict.ContainsKey("path") && dict["path"] != null)
                            path = dict["path"].ToString();
                        string title = "";
                        if (dict.ContainsKey("title") && dict["title"] != null)
                            title = dict["title"].ToString();
                        string docType = "";
                        if (dict.ContainsKey("type") && dict["type"] != null)
                            docType = dict["type"].ToString();
                        var hasActiveDocument = HasActiveDocument(title, path, docType);
                        if (!string.IsNullOrWhiteSpace(path))
                            fileNameLabel.Text = Path.GetFileNameWithoutExtension(path);
                        else if (!string.IsNullOrWhiteSpace(title))
                            fileNameLabel.Text = title;
                        else
                            fileNameLabel.Text = "无文档";
                        SetConnected(true);
                        UpdatePanelVisibility(docType, hasActiveDocument);
                    }
                }
            }
            catch (InvalidOperationException ex) when (IsNoActiveDocumentError(ex))
            {
                fileNameLabel.Text = "无文档";
                SetConnected(true);
                UpdatePanelVisibility("", false);
            }
            catch
            {
                fileNameLabel.Text = "未连接";
                SetConnected(false);
                UpdatePanelVisibility("", false);
            }
        }
    }

    private void SetConnected(bool connected)
    {
        var brushKey = connected ? "ConnectionOnlineBrush" : "ConnectionOfflineBrush";
        var brush = (System.Windows.Media.Brush)FindResource(brushKey);
        if (brush != null) ConnectionStatusIcon.Fill = brush;
        ConnectionStatusIcon.ToolTip = connected ? "已连接" : "未连接";
    }

    private void UpdatePanelVisibility(string docType, bool hasActiveDocument = true)
    {
        Button2.Visibility = hasActiveDocument ? WpfNs.Visibility.Visible : WpfNs.Visibility.Collapsed;
        if (!hasActiveDocument)
        {
            DrawingActionsPanel.Visibility = WpfNs.Visibility.Collapsed;
            AssemblyActionsPanel.Visibility = WpfNs.Visibility.Collapsed;
            Button9.Visibility = WpfNs.Visibility.Collapsed;
            Button16.Visibility = WpfNs.Visibility.Collapsed;
            Button19.Visibility = WpfNs.Visibility.Collapsed;
            Button20.Visibility = WpfNs.Visibility.Collapsed;
            QueueAdjustWindowWidthToToolbar();
            return;
        }

        // Server returns integer type: 1=PART, 2=ASSEMBLY, 3=DRAWING
        // Events may pass string type like "PART", "DRAWING", "ASSEMBLY"
        var isPart = docType == "1" || string.Equals(docType, "PART", StringComparison.OrdinalIgnoreCase);
        var isDrawing = docType == "3" || string.Equals(docType, "DRAWING", StringComparison.OrdinalIgnoreCase);
        var isAssembly = docType == "2" || string.Equals(docType, "ASSEMBLY", StringComparison.OrdinalIgnoreCase);

        DrawingActionsPanel.Visibility = isDrawing ? WpfNs.Visibility.Visible : WpfNs.Visibility.Collapsed;
        AssemblyActionsPanel.Visibility = isAssembly ? WpfNs.Visibility.Visible : WpfNs.Visibility.Collapsed;
        Button9.Visibility = isPart ? WpfNs.Visibility.Visible : WpfNs.Visibility.Collapsed;
        var assemblyOnlyVisibility = isAssembly ? WpfNs.Visibility.Visible : WpfNs.Visibility.Collapsed;
        Button16.Visibility = assemblyOnlyVisibility;
        Button19.Visibility = assemblyOnlyVisibility;
        Button20.Visibility = assemblyOnlyVisibility;
        QueueAdjustWindowWidthToToolbar();
    }

    private static bool HasActiveDocument(string title, string path, string docType = "")
    {
        return !string.IsNullOrWhiteSpace(path)
               || !string.IsNullOrWhiteSpace(title)
               || !string.IsNullOrWhiteSpace(docType);
    }

    private static bool IsNoActiveDocumentError(InvalidOperationException ex)
    {
        return ex.Message.IndexOf("没有活动文档", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private void QueueAdjustWindowWidthToToolbar()
    {
        Dispatcher.BeginInvoke(new Action(AdjustWindowWidthToToolbar));
    }

    private void AdjustWindowWidthToToolbar()
    {
        if (ToolbarPanel == null) return;

        ToolbarPanel.Measure(new WpfNs.Size(double.PositiveInfinity, double.PositiveInfinity));
        var desiredWidth = Math.Ceiling(ToolbarPanel.DesiredSize.Width + ToolbarWindowWidthPadding);
        var boundedWidth = Math.Max(MinWidth, Math.Min(MaxWidth, desiredWidth));

        if (Math.Abs(Width - boundedWidth) > 0.5)
        {
            Width = boundedWidth;
        }
    }

    private async void Button2_Click(object sender, EventArgs e)
    {
        try
        {
            var result = await _client.SendCommandAsync("active-document");
            var dict = result as Dictionary<string, object>;
            var path = "";
            if (dict != null && dict.ContainsKey("path") && dict["path"] != null)
            {
                path = dict["path"].ToString();
            }

            OpenPathInExplorer(path);
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show("获取路径失败: " + ex.Message);
        }
    }

    private void OpenPathInExplorer(string targetPath)
    {
        if (string.IsNullOrWhiteSpace(targetPath))
        {
            System.Windows.MessageBox.Show("当前文件还没有保存，无法打开目录。");
            return;
        }

        if (File.Exists(targetPath))
        {
            var fileDirectory = Path.GetDirectoryName(targetPath);
            if (string.IsNullOrWhiteSpace(fileDirectory) || !Directory.Exists(fileDirectory))
            {
                System.Windows.MessageBox.Show("找不到文件所在目录：" + targetPath);
                return;
            }

            Process.Start(new ProcessStartInfo("explorer.exe", "\"" + fileDirectory + "\"") { UseShellExecute = true });
            return;
        }

        var directoryPath = Directory.Exists(targetPath) ? targetPath : Path.GetDirectoryName(targetPath);
        if (string.IsNullOrWhiteSpace(directoryPath) || !Directory.Exists(directoryPath))
        {
            System.Windows.MessageBox.Show("找不到文件所在目录：" + targetPath);
            return;
        }

        Process.Start(new ProcessStartInfo("explorer.exe", "\"" + directoryPath + "\"") { UseShellExecute = true });
    }

    private async void Button4_Click(object sender, EventArgs e)
    {
        try
        {
            await _client.SendCommandAsync("rotate-drawing-view");
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show("旋转视图失败: " + ex.Message);
        }
    }

    private async void Button5_Click(object sender, EventArgs e)
    {
        try
        {
            await _client.SendCommandAsync("set-iso-standard");
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show("设置 ISO 标准失败: " + ex.Message);
        }
    }

    private async void Button8_Click(object sender, EventArgs e)
    {
        try
        {
            await _client.SendCommandAsync("save-pdf");
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show("保存 PDF 失败: " + ex.Message);
        }
    }

    private async void Button9_Click(object sender, EventArgs e)
    {
        try
        {
            await _client.SendCommandAsync("sync-coding-props");
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show("同步属性失败: " + ex.Message);
        }
    }

    private async void Button11_Click(object sender, EventArgs e)
    {
        try
        {
            await _client.SendCommandAsync("hide-config-names");
            ShowAutoCloseNotice("完成");
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show("操作失败: " + ex.Message);
        }
    }

    private async void Button13_Click(object sender, EventArgs e)
    {
        ShowSortProgress("正在准备装配体排序...");
        try
        {
            await _client.SendCommandAsync("sort-components");
            ShowAutoCloseNotice("装配体排序完成");
        }
        catch (Exception ex)
        {
            WpfNs.MessageBox.Show(this, "装配体排序失败: " + ex.Message, "错误", WpfNs.MessageBoxButton.OK, WpfNs.MessageBoxImage.Error);
        }
        finally
        {
            CloseSortProgress();
        }
    }

    private async void Button6_Click_1(object sender, EventArgs e)
    {
        try
        {
            var result = await _client.SendCommandAsync("active-document");
            var dict = result as Dictionary<string, object>;
            var docName = "文档";
            if (dict != null && dict.ContainsKey("title"))
            {
                docName = dict["title"].ToString();
            }

            var wasTopMost = Topmost;
            Topmost = true;
            var confirmResult = WinForms.MessageBox.Show(
                "将删除【" + docName + "】及其所有子件的自定义属性，确定继续？",
                "确认删除自定义属性",
                WinForms.MessageBoxButtons.OKCancel,
                WinForms.MessageBoxIcon.Warning);
            Topmost = wasTopMost;
            if (confirmResult != WinForms.DialogResult.OK) return;

            await _client.SendCommandAsync("delete-custom-props");
            ShowAutoCloseNotice("完成");
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show("删除自定义属性失败: " + ex.Message);
        }
    }

    private void Button14_Click_1(object sender, EventArgs e)
    {
        var settingsWindow = new DrawingSettingsWindow();
        var helper = new System.Windows.Interop.WindowInteropHelper(settingsWindow);
        helper.Owner = _mainWindowHandle;
        settingsWindow.Show();
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WmHotkey && wParam.ToInt32() == HotkeyId)
        {
            ToggleVisibility();
            handled = true;
        }
        return IntPtr.Zero;
    }

    private void ToggleVisibility()
    {
        if (IsVisible)
            Hide();
        else
        {
            Show();
            WindowState = WpfNs.WindowState.Normal;
            Activate();
        }
    }

    private void TrayShow_Click(object sender, EventArgs e)
    {
        Show();
        WindowState = WpfNs.WindowState.Normal;
        Activate();
    }

    private void TrayExit_Click(object sender, EventArgs e)
    {
        _allowClose = true;
        WpfNs.Application.Current.Shutdown();
    }

    private void Button16_Click(object sender, EventArgs e)
    {
        var renameWindow = new RenameWindow();
        renameWindow.Client = _client;
        renameWindow.Show();
    }

    private async void Button17_Click(object sender, EventArgs e)
    {
        try
        {
            var result = await _client.SendCommandAsync("active-document");
            var dict = result as Dictionary<string, object>;
            var docName = "文档";
            if (dict != null && dict.ContainsKey("title"))
            {
                docName = dict["title"].ToString();
            }

            var wasTopMost = Topmost;
            Topmost = true;
            var confirmResult = WinForms.MessageBox.Show(
                "将删除【" + docName + "】及其所有子件的配置属性，确定继续？",
                "确认删除配置属性",
                WinForms.MessageBoxButtons.OKCancel,
                WinForms.MessageBoxIcon.Warning);
            Topmost = wasTopMost;
            if (confirmResult != WinForms.DialogResult.OK) return;

            await _client.SendCommandAsync("delete-config-props");
            ShowAutoCloseNotice("完成");
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show("删除配置属性失败: " + ex.Message);
        }
    }

    private void Button18_Click(object sender, EventArgs e)
    {
        var cleanupWindow = new CodingCleanupWindow();
        cleanupWindow.Client = _client;
        var helper = new System.Windows.Interop.WindowInteropHelper(cleanupWindow);
        helper.Owner = _mainWindowHandle;
        cleanupWindow.Show();
    }

    private void Button19_Click(object sender, EventArgs e)
    {
        PropertyOverlayWindow.ShowOrActivate(_client);
    }

    private void Button20_Click(object sender, EventArgs e)
    {
        var settingsWindow = new PropertyOverlaySettingsWindow();
        var helper = new System.Windows.Interop.WindowInteropHelper(settingsWindow);
        helper.Owner = _mainWindowHandle;
        settingsWindow.ShowDialog();
    }

    private void OnAddinDocChanged(string title, string path)
    {
        Dispatcher.Invoke(() =>
        {
            SetConnected(true);
            var hasActiveDocument = HasActiveDocument(title, path);
            if (string.IsNullOrWhiteSpace(path))
                fileNameLabel.Text = string.IsNullOrWhiteSpace(title) ? "无文档" : title;
            else
                fileNameLabel.Text = Path.GetFileNameWithoutExtension(path);

            if (hasActiveDocument)
            {
                Button2.Visibility = WpfNs.Visibility.Visible;
                QueueAdjustWindowWidthToToolbar();
            }
            else
            {
                UpdatePanelVisibility("", false);
            }
        });
    }

    private void OnAddinSelectionChanged(string name, string type)
    {
        Dispatcher.Invoke(() =>
        {
            UpdatePanelVisibility(type);
        });
    }

    private void OnAddinDisconnected()
    {
        Dispatcher.Invoke(() =>
        {
            SetConnected(false);
            fileNameLabel.Text = "插件断开";
            Button2.Visibility = WpfNs.Visibility.Collapsed;
            DrawingActionsPanel.Visibility = WpfNs.Visibility.Collapsed;
            AssemblyActionsPanel.Visibility = WpfNs.Visibility.Collapsed;
            Button9.Visibility = WpfNs.Visibility.Collapsed;
            Button16.Visibility = WpfNs.Visibility.Collapsed;
            Button19.Visibility = WpfNs.Visibility.Collapsed;
            Button20.Visibility = WpfNs.Visibility.Collapsed;
            QueueAdjustWindowWidthToToolbar();
        });
    }

    private void ShowAutoCloseNotice(string message, string noticeTitle = "提示")
    {
        try
        {
            if (_trayIcon != null)
            {
                _trayIcon.BalloonTipTitle = noticeTitle;
                _trayIcon.BalloonTipText = message;
                _trayIcon.BalloonTipIcon = WinForms.ToolTipIcon.Info;
                _trayIcon.ShowBalloonTip(1800);
                return;
            }
        }
        catch
        {
        }

        WpfNs.MessageBox.Show(this, message, noticeTitle, WpfNs.MessageBoxButton.OK, WpfNs.MessageBoxImage.Information);
    }

    private void ShowSortProgress(string message)
    {
        CloseSortProgress();

        _sortProgressLabel = new WinForms.Label()
        {
            AutoSize = false,
            Dock = WinForms.DockStyle.Fill,
            Font = new Drawing.Font("微软雅黑", 10.0f, Drawing.FontStyle.Bold),
            ForeColor = Drawing.Color.FromArgb(45, 55, 72),
            TextAlign = Drawing.ContentAlignment.MiddleCenter,
            Text = message
        };

        _sortProgressForm = new WinForms.Form()
        {
            AutoScaleMode = WinForms.AutoScaleMode.None,
            BackColor = Drawing.Color.White,
            ClientSize = new Drawing.Size(280, 76),
            ControlBox = false,
            FormBorderStyle = WinForms.FormBorderStyle.FixedSingle,
            MaximizeBox = false,
            MinimizeBox = false,
            ShowIcon = false,
            ShowInTaskbar = false,
            StartPosition = WinForms.FormStartPosition.Manual,
            Text = "排序中",
            TopMost = true
        };

        var x = (int)(Left + Math.Max(0, (Width - _sortProgressForm.Width) / 2));
        var y = (int)(Top + Math.Max(0, (Height - _sortProgressForm.Height) / 2));
        _sortProgressForm.Location = new Drawing.Point(x, y);
        _sortProgressForm.Controls.Add(_sortProgressLabel);
        _sortProgressForm.Show();
        _sortProgressForm.Refresh();
        WinForms.Application.DoEvents();
    }

    private void UpdateSortProgress(string message)
    {
        if (_sortProgressForm == null || _sortProgressForm.IsDisposed) return;
        if (_sortProgressLabel == null || _sortProgressLabel.IsDisposed) return;

        _sortProgressLabel.Text = message;
        _sortProgressLabel.Refresh();
        _sortProgressForm.Refresh();
        WinForms.Application.DoEvents();
    }

    private void CloseSortProgress()
    {
        try
        {
            if (_sortProgressForm != null)
            {
                if (!_sortProgressForm.IsDisposed) _sortProgressForm.Close();
                _sortProgressForm.Dispose();
            }
        }
        catch
        {
        }
        finally
        {
            _sortProgressForm = null;
            _sortProgressLabel = null;
        }
    }

}

