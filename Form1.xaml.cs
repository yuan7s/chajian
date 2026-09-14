using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
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
using WpfDialogs = Microsoft.Win32;
using ExternalProgram.Properties;

namespace ExternalProgram;

partial class Form1
{
    public Form1()
    {
        InitializeComponent();
    }

    private WinForms.Timer _statusTimer;
    private WinForms.NotifyIcon _trayIcon;
    private System.Windows.Threading.DispatcherTimer _noticeTimer;
    private WinForms.ContextMenuStrip _trayMenu;
    private WinForms.ToolStripMenuItem _trayToggleWindowItem;
    private WinForms.ToolStripMenuItem _trayTogglePropertyOverlayItem;
    private WinForms.Form _sortProgressForm;
    private WinForms.Label _sortProgressLabel;
    private SettingsWindow _settingsWindow;
    private CodingCleanupWindow _codingCleanupWindow;
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
        _trayToggleWindowItem = new WinForms.ToolStripMenuItem("隐藏主窗口", null, TrayToggleWindow_Click);
        _trayMenu.Items.Add(_trayToggleWindowItem);
        _trayTogglePropertyOverlayItem = new WinForms.ToolStripMenuItem("显示属性悬浮框", null, TrayPropertyOverlay_Click);
        _trayMenu.Items.Add(_trayTogglePropertyOverlayItem);
        _trayMenu.Items.Add("设置...", null, TraySettings_Click);
        _trayMenu.Items.Add("-");
        _trayMenu.Items.Add("退出", null, TrayExit_Click);
        _trayMenu.Opening += TrayMenu_Opening;

        // 启动时定位到桌面右下角
        var workArea = WpfNs.SystemParameters.WorkArea;
        Left = workArea.Right - Width - 10;
        Top = workArea.Bottom - Height - 10;

        _trayIcon = new WinForms.NotifyIcon()
        {
            Icon = LoadTrayIcon(),
            Text = "External Program",
            Visible = true,
            ContextMenuStrip = _trayMenu
        };
        _trayIcon.DoubleClick += TrayToggleWindow_Click;

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

    private static Drawing.Icon LoadTrayIcon()
    {
        try
        {
            var resource = WpfNs.Application.GetResourceStream(new Uri("Properties/AppIcon.ico", UriKind.Relative));
            if (resource?.Stream != null)
            {
                using (resource.Stream)
                using (var icon = new Drawing.Icon(resource.Stream))
                {
                    return (Drawing.Icon)icon.Clone();
                }
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine("LoadTrayIcon ignored: " + ex.Message);
        }

        return Drawing.SystemIcons.Application;
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
            HideMainWindow();
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
            _trayMenu.Opening -= TrayMenu_Opening;
            _trayMenu.Dispose();
            _trayMenu = null;
        }
        _trayToggleWindowItem = null;
        _trayTogglePropertyOverlayItem = null;
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
                            FileNameLabel.Text = Path.GetFileNameWithoutExtension(path);
                        else if (!string.IsNullOrWhiteSpace(title))
                            FileNameLabel.Text = title;
                        else
                            FileNameLabel.Text = "无文档";
                        SetConnected(true);
                        UpdatePanelVisibility(docType, hasActiveDocument);
                    }
                }
            }
            catch (InvalidOperationException ex) when (IsNoActiveDocumentError(ex))
            {
                FileNameLabel.Text = "无文档";
                SetConnected(true);
                UpdatePanelVisibility("", false);
            }
            catch (InvalidOperationException ex) when (IsCommandBusyError(ex))
            {
                // Addin is processing another command — keep the current status,
                // don't flip to "disconnected".
            }
            catch
            {
                FileNameLabel.Text = "未连接";
                SetConnected(false);
                UpdatePanelVisibility("", false);
            }

            await RefreshConnectionSelectorStateAsync();
        }
    }

    private void SetConnected(bool connected)
    {
        ConnectionStatusIcon.ToolTip = connected ? "已连接" : "未连接";
        ConnectionSelectorButton.ToolTip = connected ? BuildConnectionToolTip() : "未连接";
    }

    private string BuildConnectionToolTip()
    {
        if (_client == null) return "未连接";

        var mode = _client.IsManualEndpointSelection ? "手动连接" : "自动选择前台 SW";
        var process = _client.CurrentProcessId > 0 ? "PID " + _client.CurrentProcessId : "未知进程";
        return mode + Environment.NewLine + process + Environment.NewLine + "端口 " + _client.CurrentPort;
    }

    private async Task RefreshConnectionSelectorStateAsync()
    {
        if (_client == null) return;

        try
        {
            var endpoints = await _client.GetAvailableEndpointsAsync();
            if (endpoints.Count == 0)
            {
                ConnectionDropGlyph.Visibility = WpfNs.Visibility.Collapsed;
                ConnectionSelectorButton.ToolTip = "未连接";
                return;
            }

            ConnectionDropGlyph.Visibility = endpoints.Count > 1
                ? WpfNs.Visibility.Visible
                : WpfNs.Visibility.Collapsed;
            ConnectionSelectorButton.ToolTip = BuildConnectionToolTip();
        }
        catch
        {
            ConnectionDropGlyph.Visibility = WpfNs.Visibility.Collapsed;
        }
    }

    private async void ConnectionSelectorButton_Click(object sender, WpfNs.RoutedEventArgs e)
    {
        if (_client == null) return;

        var menu = new WpfControls.ContextMenu
        {
            PlacementTarget = ConnectionSelectorButton
        };

        var autoItem = new WpfControls.MenuItem
        {
            Header = "自动选择前台 SW",
            IsCheckable = true,
            IsChecked = !_client.IsManualEndpointSelection
        };
        autoItem.Click += async (_, _) =>
        {
            await _client.UseAutomaticEndpointSelectionAsync();
            await UpdateStatusBar();
        };
        menu.Items.Add(autoItem);
        menu.Items.Add(new WpfControls.Separator());

        IReadOnlyList<SwAddinClient.SwAddinEndpoint> endpoints;
        try
        {
            endpoints = await _client.GetAvailableEndpointsAsync();
        }
        catch (Exception ex)
        {
            var errorItem = new WpfControls.MenuItem
            {
                Header = "刷新连接失败: " + ex.Message,
                IsEnabled = false
            };
            menu.Items.Add(errorItem);
            ConnectionSelectorButton.ContextMenu = menu;
            menu.IsOpen = true;
            return;
        }

        if (endpoints.Count == 0)
        {
            menu.Items.Add(new WpfControls.MenuItem
            {
                Header = "未检测到 SolidWorks 插件连接",
                IsEnabled = false
            });
        }
        else
        {
            foreach (var endpoint in endpoints)
            {
                var item = new WpfControls.MenuItem
                {
                    Header = endpoint.DisplayName,
                    IsCheckable = true,
                    IsChecked = endpoint.Port == _client.CurrentPort,
                    Tag = endpoint.Port
                };
                item.Click += async (_, _) =>
                {
                    await _client.SelectEndpointAsync((int)item.Tag);
                    await UpdateStatusBar();
                };
                menu.Items.Add(item);
            }
        }

        menu.Items.Add(new WpfControls.Separator());
        var refreshItem = new WpfControls.MenuItem { Header = "刷新连接列表" };
        refreshItem.Click += async (_, _) => await RefreshConnectionSelectorStateAsync();
        menu.Items.Add(refreshItem);

        ConnectionSelectorButton.ContextMenu = menu;
        menu.IsOpen = true;
    }

    private void UpdatePanelVisibility(string docType, bool hasActiveDocument = true)
    {
        DrawingActionsPanel.Visibility = WpfNs.Visibility.Collapsed;
        AssemblyActionsPanel.Visibility = WpfNs.Visibility.Collapsed;

        if (!hasActiveDocument)
        {
            HideToolbarButtons();
            QueueAdjustWindowWidthToToolbar();
            return;
        }

        // Server returns integer type: 1=PART, 2=ASSEMBLY, 3=DRAWING
        // Events may pass string type like "PART", "DRAWING", "ASSEMBLY"
        var group = GetToolbarGroupForDocType(docType);
        if (string.IsNullOrWhiteSpace(group))
            HideToolbarButtons();
        else
            ApplyToolbarGroupLayout(group);

        QueueAdjustWindowWidthToToolbar();
    }

    private static string GetToolbarGroupForDocType(string docType)
    {
        if (docType == "1" || string.Equals(docType, "PART", StringComparison.OrdinalIgnoreCase))
            return ToolbarButtonGroups.Part;
        if (docType == "3" || string.Equals(docType, "DRAWING", StringComparison.OrdinalIgnoreCase))
            return ToolbarButtonGroups.Drawing;
        if (docType == "2" || string.Equals(docType, "ASSEMBLY", StringComparison.OrdinalIgnoreCase))
            return ToolbarButtonGroups.Assembly;

        return "";
    }

    private void ApplyToolbarGroupLayout(string group)
    {
        HideToolbarButtons();
        var buttons = GetToolbarButtonMap();
        var visibleCount = 0;

        foreach (var item in ToolbarButtonLayoutStore.GetGroupItems(Settings.Default, group))
        {
            if (!buttons.TryGetValue(item.Id, out var button)) continue;

            MoveToolbarButtonToPanel(button, CommonActionsPanel);
            button.Visibility = WpfNs.Visibility.Visible;
            visibleCount++;
        }

        CommonActionsPanel.Visibility = visibleCount > 0
            ? WpfNs.Visibility.Visible
            : WpfNs.Visibility.Collapsed;
    }

    private void HideToolbarButtons()
    {
        foreach (var button in GetToolbarButtonMap().Values)
            button.Visibility = WpfNs.Visibility.Collapsed;

        CommonActionsPanel.Visibility = WpfNs.Visibility.Collapsed;
    }

    private Dictionary<string, WpfNs.UIElement> GetToolbarButtonMap()
    {
        return new Dictionary<string, WpfNs.UIElement>(StringComparer.OrdinalIgnoreCase)
        {
            { ToolbarButtonLayoutStore.OpenFolder, Button2 },
            { ToolbarButtonLayoutStore.PartCoding, Button9 },
            { ToolbarButtonLayoutStore.BlankSize, Button24 },
            { ToolbarButtonLayoutStore.SaveDwg, Button1 },
            { ToolbarButtonLayoutStore.SavePdf, Button8 },
            { ToolbarButtonLayoutStore.RotateView, Button4 },
            { ToolbarButtonLayoutStore.IsoView, Button5 },
            { ToolbarButtonLayoutStore.ReplaceDrawingSettings, Button23 },
            { ToolbarButtonLayoutStore.AssemblyCleanup, Button18 },
            { ToolbarButtonLayoutStore.ReferencePlaneMate, Button20 },
            { ToolbarButtonLayoutStore.AssemblySort, Button13 },
            { ToolbarButtonLayoutStore.TreeSettings, Button11 },
            { ToolbarButtonLayoutStore.Rename, Button16 },
            { ToolbarButtonLayoutStore.RunSwpMacro, Button22 },
            { ToolbarButtonLayoutStore.DeleteErrorMates, Button21 },
            { ToolbarButtonLayoutStore.DeleteCustomProps, Button6 },
            { ToolbarButtonLayoutStore.DeleteConfigProps, Button17 }
        };
    }

    private static void MoveToolbarButtonToPanel(WpfNs.UIElement button, WpfControls.Panel targetPanel)
    {
        if (button is WpfNs.FrameworkElement frameworkElement &&
            frameworkElement.Parent is WpfControls.Panel currentPanel &&
            !ReferenceEquals(currentPanel, targetPanel))
        {
            currentPanel.Children.Remove(button);
        }

        if (!targetPanel.Children.Contains(button))
            targetPanel.Children.Add(button);
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

    private static bool IsCommandBusyError(InvalidOperationException ex)
    {
        return ex.Message.IndexOf("command_busy", StringComparison.OrdinalIgnoreCase) >= 0;
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
            var result = await _client.SendCommandAsync("open-file-location");
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

            Process.Start(new ProcessStartInfo("explorer.exe", "/select,\"" + targetPath + "\"") { UseShellExecute = true });
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

    private async void Button23_Click(object sender, EventArgs e)
    {
        var standardPath = Settings.Default.Drawing_StandardPath;
        var sheetFormatPath = Settings.Default.Drawing_SheetFormatPath;
        if (string.IsNullOrWhiteSpace(standardPath) || string.IsNullOrWhiteSpace(sheetFormatPath))
        {
            WpfNs.MessageBox.Show(this, "请先在设置的工程图页选择绘图标准和图纸格式文件。", "提示",
                WpfNs.MessageBoxButton.OK, WpfNs.MessageBoxImage.Information);
            return;
        }

        try
        {
            await _client.SendCommandAsync("replace-drawing-settings", new Dictionary<string, object>
            {
                { "standardPath", standardPath },
                { "sheetFormatPath", sheetFormatPath }
            });
            ShowAutoCloseNotice("绘图标准和图纸格式替换完成");
        }
        catch (Exception ex)
        {
            WpfNs.MessageBox.Show(this, "替换绘图标准和图纸格式失败: " + ex.Message, "错误",
                WpfNs.MessageBoxButton.OK, WpfNs.MessageBoxImage.Error);
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

    private async void Button24_Click(object sender, EventArgs e)
    {
        try
        {
            var result = await _client.SendCommandAsync("write-blank-size");
            var dict = result as Dictionary<string, object>;
            var blankSize = dict != null && dict.ContainsKey("blankSize") ? dict["blankSize"]?.ToString() : "";
            ShowAutoCloseNotice(string.IsNullOrWhiteSpace(blankSize) ? "下料尺寸已写入" : "下料尺寸已写入: " + blankSize);
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show("写入下料尺寸失败: " + ex.Message);
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
            UpdateSortProgress("正在排序装配体组件...");
            var result = await _client.SendCommandAsync("sort-components", BuildSortCommandArgs(), TimeSpan.FromMinutes(10));
            var foldersSorted = GetResultInt(result, "foldersSorted");
            ShowAutoCloseNotice(foldersSorted > 0
                ? "装配体排序完成，已处理 " + foldersSorted + " 个文件夹"
                : "装配体排序完成");
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
        if (IsMainWindowVisible())
            HideMainWindow();
        else
        {
            ShowMainWindow();
        }
    }

    private bool IsMainWindowVisible()
    {
        return IsVisible && WindowState != WpfNs.WindowState.Minimized;
    }

    private void ShowMainWindow()
    {
        Show();
        WindowState = WpfNs.WindowState.Normal;
        Activate();
        UpdateTrayWindowMenuItem();
    }

    private void HideMainWindow()
    {
        Hide();
        UpdateTrayWindowMenuItem();
    }

    private void UpdateTrayWindowMenuItem()
    {
        if (_trayToggleWindowItem == null) return;
        _trayToggleWindowItem.Text = IsMainWindowVisible() ? "隐藏主窗口" : "显示主窗口";
    }

    private void UpdateTrayPropertyOverlayMenuItem()
    {
        if (_trayTogglePropertyOverlayItem == null) return;
        _trayTogglePropertyOverlayItem.Text = PropertyOverlayWindow.IsAnyVisible()
            ? "隐藏属性悬浮框"
            : "显示属性悬浮框";
    }

    private void TrayMenu_Opening(object sender, CancelEventArgs e)
    {
        UpdateTrayWindowMenuItem();
        UpdateTrayPropertyOverlayMenuItem();
    }

    private void TrayToggleWindow_Click(object sender, EventArgs e)
    {
        ToggleVisibility();
    }

    private void TraySettings_Click(object sender, EventArgs e)
    {
        ShowSettingsWindow();
    }

    private void TrayPropertyOverlay_Click(object sender, EventArgs e)
    {
        PropertyOverlayWindow.ToggleVisibility(_client);
        UpdateTrayPropertyOverlayMenuItem();
    }

    private void TrayExit_Click(object sender, EventArgs e)
    {
        _allowClose = true;
        WpfNs.Application.Current.Shutdown();
    }

    private void Button16_Click(object sender, EventArgs e)
    {
        if (RenameWindow.ActivateExisting(_client)) return;

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
        try
        {
            ShowCodingCleanupWindow();
        }
        catch (Exception ex)
        {
            ResetCodingCleanupWindow();
            WpfNs.MessageBox.Show(this, "打开编码整理窗口失败: " + ex.Message, "错误", WpfNs.MessageBoxButton.OK, WpfNs.MessageBoxImage.Error);
        }
    }

    private void ShowCodingCleanupWindow()
    {
        if (_codingCleanupWindow == null)
        {
            _codingCleanupWindow = new CodingCleanupWindow { Client = _client };
            _codingCleanupWindow.Closed += CodingCleanupWindow_Closed;
        }
        else
        {
            _codingCleanupWindow.Client = _client;
        }

        if (!_codingCleanupWindow.IsVisible)
            _codingCleanupWindow.Show();

        _codingCleanupWindow.WindowState = WpfNs.WindowState.Normal;
        _codingCleanupWindow.Activate();
    }

    private void CodingCleanupWindow_Closed(object sender, EventArgs e)
    {
        ResetCodingCleanupWindow();
    }

    private void ResetCodingCleanupWindow()
    {
        if (_codingCleanupWindow != null)
        {
            _codingCleanupWindow.Closed -= CodingCleanupWindow_Closed;
            _codingCleanupWindow = null;
        }
    }

    private void Button19_Click(object sender, EventArgs e)
    {
        PropertyOverlayWindow.ShowOrActivate(_client);
    }

    private async void Button20_Click(object sender, EventArgs e)
    {
        try
        {
            await _client.SendCommandAsync("mate-reference-planes");
            ShowAutoCloseNotice("基准面配合完成");
        }
        catch (Exception ex)
        {
            WpfNs.MessageBox.Show(this, "基准面配合失败: " + ex.Message, "错误", WpfNs.MessageBoxButton.OK, WpfNs.MessageBoxImage.Error);
        }
    }

    private async void Button21_Click(object sender, EventArgs e)
    {
        try
        {
            var result = await _client.SendCommandAsync("active-document");
            var dict = result as Dictionary<string, object>;
            var docName = "当前装配体";
            if (dict != null && dict.ContainsKey("title") && dict["title"] != null)
                docName = dict["title"].ToString();

            var wasTopMost = Topmost;
            Topmost = true;
            var confirmResult = WinForms.MessageBox.Show(
                "将删除【" + docName + "】中的错误配合，确定继续？",
                "确认删除错误配合",
                WinForms.MessageBoxButtons.OKCancel,
                WinForms.MessageBoxIcon.Warning);
            Topmost = wasTopMost;
            if (confirmResult != WinForms.DialogResult.OK) return;

            var deleteResult = await _client.SendCommandAsync("delete-error-mates");
            var deleted = GetResultInt(deleteResult, "deleted");
            ShowAutoCloseNotice(deleted > 0 ? "已删除错误配合：" + deleted : "没有发现错误配合");
        }
        catch (Exception ex)
        {
            WpfNs.MessageBox.Show(this, "删除错误配合失败: " + ex.Message, "错误", WpfNs.MessageBoxButton.OK, WpfNs.MessageBoxImage.Error);
        }
    }

    private async void Button22_Click(object sender, EventArgs e)
    {
        try
        {
            var dialog = new WpfDialogs.OpenFileDialog
            {
                CheckFileExists = true,
                DefaultExt = ".swp",
                Filter = "SolidWorks 宏 (*.swp)|*.swp",
                Multiselect = false,
                Title = "选择 SolidWorks 宏"
            };

            if (!dialog.ShowDialog(this).GetValueOrDefault()) return;

            await _client.SendCommandAsync("run-swp-macro", new Dictionary<string, object>
            {
                { "path", dialog.FileName }
            });
            ShowAutoCloseNotice("宏执行完成");
        }
        catch (Exception ex)
        {
            WpfNs.MessageBox.Show(this, "运行宏失败: " + ex.Message, "错误", WpfNs.MessageBoxButton.OK, WpfNs.MessageBoxImage.Error);
        }
    }

    private void ShowSettingsWindow(int selectedTabIndex = 0)
    {
        if (_settingsWindow != null)
        {
            _settingsWindow.SelectTab(selectedTabIndex);
            if (!_settingsWindow.IsVisible) _settingsWindow.Show();
            _settingsWindow.WindowState = WpfNs.WindowState.Normal;
            _settingsWindow.Activate();
            return;
        }

        _settingsWindow = new SettingsWindow(selectedTabIndex);
        _settingsWindow.SettingsApplied += SettingsWindow_SettingsApplied;
        _settingsWindow.Closed += SettingsWindow_Closed;

        var helper = new WpfInterop.WindowInteropHelper(_settingsWindow);
        helper.Owner = _mainWindowHandle;
        _settingsWindow.Show();
    }

    private async void SettingsWindow_SettingsApplied(object sender, EventArgs e)
    {
        await UpdateStatusBar();
        QueueAdjustWindowWidthToToolbar();
    }

    private void SettingsWindow_Closed(object sender, EventArgs e)
    {
        if (_settingsWindow != null)
        {
            _settingsWindow.SettingsApplied -= SettingsWindow_SettingsApplied;
            _settingsWindow.Closed -= SettingsWindow_Closed;
            _settingsWindow = null;
        }
    }

    private static Dictionary<string, object> BuildSortCommandArgs()
    {
        var settings = Settings.Default;
        return new Dictionary<string, object>
        {
            { "assemblyFirst", settings.Sort_AssemblyFirst },
            { "suppressedLast", settings.Sort_SuppressedLast },
            { "sortFolders", settings.Sort_SortFolders },
            { "recursiveSubAssemblies", settings.Sort_RecursiveSubAssemblies },
            { "nameSource", string.IsNullOrWhiteSpace(settings.Sort_NameSource) ? "ComponentName" : settings.Sort_NameSource },
            { "descending", string.Equals(settings.Sort_Direction, "Descending", StringComparison.OrdinalIgnoreCase) }
        };
    }

    private static int GetResultInt(object result, string key)
    {
        if (result is not Dictionary<string, object> dict) return 0;
        if (!dict.TryGetValue(key, out var value) || value == null) return 0;
        if (value is int intValue) return intValue;
        return int.TryParse(value.ToString(), out var parsed) ? parsed : 0;
    }

    private void OnAddinDocChanged(string title, string path)
    {
        Dispatcher.Invoke(() =>
        {
            SetConnected(true);
            var hasActiveDocument = HasActiveDocument(title, path);
            if (string.IsNullOrWhiteSpace(path))
                FileNameLabel.Text = string.IsNullOrWhiteSpace(title) ? "无文档" : title;
            else
                FileNameLabel.Text = Path.GetFileNameWithoutExtension(path);

            if (hasActiveDocument)
            {
                _ = UpdateStatusBar();
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
            FileNameLabel.Text = "插件断开";
            UpdatePanelVisibility("", false);
            QueueAdjustWindowWidthToToolbar();
        });
    }

    private void ShowAutoCloseNotice(string message, string noticeTitle = "提示")
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
            Debug.WriteLine("ShowAutoCloseNotice failed: " + ex.Message);
        }
    }

    private void StopNoticeTimer()
    {
        try { _noticeTimer?.Stop(); } catch { }
        _noticeTimer = null;
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


