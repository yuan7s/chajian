using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows.Threading;
using WpfNs = System.Windows;
using WpfControls = System.Windows.Controls;
using WpfInput = System.Windows.Input;
using WpfInterop = System.Windows.Interop;
using WpfMedia = System.Windows.Media;

namespace 外部程序;
using 外部程序.Properties;

public class PropertyOverlayWindow : WpfNs.Window
{
    private const int GwlExstyle = -20;
    private const int WsExTransparent = 0x20;
    private const int WmHotkey = 0x312;
    private const int HotkeyIdClickThrough = 2;
    private const int ModControl = 0x2;
    private const int VkF2 = 0x71;
    private static readonly IntPtr HwndTopmost = new IntPtr(-1);
    private static readonly IntPtr HwndNoTopmost = new IntPtr(-2);
    private const uint SwpNoSize = 0x1;
    private const uint SwpNoMove = 0x2;
    private const uint SwpNoActivate = 0x10;

    [DllImport("user32.dll", EntryPoint = "GetWindowLong")]
    private static extern int GetWindowLong32(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll", EntryPoint = "SetWindowLong")]
    private static extern int SetWindowLong32(IntPtr hWnd, int nIndex, int dwNewLong);

    [DllImport("user32.dll")]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, int fsModifiers, int vk);

    [DllImport("user32.dll")]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int x, int y, int cx, int cy, uint flags);

    private static readonly List<PropertyOverlayWindow> OpenWindows = new List<PropertyOverlayWindow>();

    private SwAddinClient _client;
    private readonly WpfControls.Border _root;
    private readonly WpfControls.TextBlock _titleText;
    private readonly WpfControls.StackPanel _propertyPanel;
    private readonly DispatcherTimer _docWatchTimer;

    public PropertyOverlayWindow()
    {
        Width = 420;
        SizeToContent = WpfNs.SizeToContent.Height;
        MinHeight = 120;
        MaxHeight = 900;
        WindowStyle = WpfNs.WindowStyle.None;
        WindowStartupLocation = WpfNs.WindowStartupLocation.Manual;
        AllowsTransparency = true;
        ResizeMode = WpfNs.ResizeMode.NoResize;
        ShowInTaskbar = false;
        Background = WpfMedia.Brushes.Transparent;

        _titleText = new WpfControls.TextBlock()
        {
            FontSize = 17,
            FontWeight = WpfNs.FontWeights.SemiBold,
            Foreground = WpfMedia.Brushes.Black,
            Text = "-",
            TextTrimming = WpfNs.TextTrimming.CharacterEllipsis,
            VerticalAlignment = WpfNs.VerticalAlignment.Center
        };

        var closeButton = new WpfControls.Button()
        {
            Content = "X",
            Width = 28,
            Height = 28,
            FontWeight = WpfNs.FontWeights.Bold,
            Foreground = WpfMedia.Brushes.Black,
            Background = WpfMedia.Brushes.Transparent,
            BorderThickness = new WpfNs.Thickness(0),
            Cursor = WpfInput.Cursors.Hand
        };
        closeButton.Click += (s, e) => Close();

        var header = new WpfControls.Grid() { Margin = new WpfNs.Thickness(18, 14, 14, 6) };
        header.ColumnDefinitions.Add(new WpfControls.ColumnDefinition() { Width = new WpfNs.GridLength(1, WpfNs.GridUnitType.Star) });
        header.ColumnDefinitions.Add(new WpfControls.ColumnDefinition() { Width = WpfNs.GridLength.Auto });
        WpfControls.Grid.SetColumn(_titleText, 0);
        WpfControls.Grid.SetColumn(closeButton, 1);
        header.Children.Add(_titleText);
        header.Children.Add(closeButton);

        _propertyPanel = new WpfControls.StackPanel() { Margin = new WpfNs.Thickness(18, 0, 18, 14) };

        var layout = new WpfControls.StackPanel();
        layout.Children.Add(header);
        layout.Children.Add(new WpfControls.Border() { Height = 2 });
        layout.Children.Add(_propertyPanel);

        _root = new WpfControls.Border()
        {
            CornerRadius = new WpfNs.CornerRadius(10),
            Padding = new WpfNs.Thickness(0),
            Child = layout
        };
        Content = _root;

        _docWatchTimer = new DispatcherTimer() { Interval = TimeSpan.FromMilliseconds(800) };
        _docWatchTimer.Tick += DocWatchTimer_Tick;

        MouseLeftButtonDown += Window_MouseLeftButtonDown;
        SourceInitialized += Window_SourceInitialized;
        Closed += (s, e) =>
        {
            _docWatchTimer.Stop();
            UnregisterOverlayHotKey();
            if (_client != null)
            {
                _client.DocChanged -= Client_DocChanged;
                _client.SelectionChanged -= Client_SelectionChanged;
            }
            OpenWindows.Remove(this);
        };
        OpenWindows.Add(this);

        ApplyDisplaySettings();
        SetInitialLocation();
    }

    public SwAddinClient Client
    {
        get => _client;
        set
        {
            if (!object.ReferenceEquals(_client, value))
            {
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
            var _ = RefreshPropertiesAsync();
        }
    }

    public static void ShowOrActivate(SwAddinClient client)
    {
        var window = OpenWindows.FirstOrDefault();
        if (window == null)
        {
            window = new PropertyOverlayWindow();
        }

        window.Client = client;
        if (!window.IsVisible)
        {
            window.Show();
        }

        window.BringToFront();
    }

    public static void ApplySettingsToOpenWindows()
    {
        foreach (var window in OpenWindows.ToArray())
        {
            if (window != null) window.ApplyDisplaySettings();
        }
    }

    public static void UpdateOpenWindowsClient(SwAddinClient client)
    {
        foreach (var window in OpenWindows.ToArray())
        {
            if (window != null) window.Client = client;
        }
    }

    private void BringToFront()
    {
        var source = WpfNs.PresentationSource.FromVisual(this) as WpfInterop.HwndSource;
        if (source == null) return;

        var flags = SwpNoMove | SwpNoSize | SwpNoActivate;
        SetWindowPos(source.Handle, HwndTopmost, 0, 0, 0, 0, flags);
        if (!Topmost)
        {
            SetWindowPos(source.Handle, HwndNoTopmost, 0, 0, 0, 0, flags);
        }
    }

    public void ApplyDisplaySettings()
    {
        Topmost = Properties.Settings.Default.Form5_TopMost;

        var alpha = (byte)Math.Max(25, Math.Min(230, Properties.Settings.Default.Form5_Opacity * 255));
        _root.Background = new WpfMedia.SolidColorBrush(WpfMedia.Color.FromArgb(alpha, 255, 255, 255));
        _titleText.Foreground = WpfMedia.Brushes.Black;

        var propertyTextBrush = GetPropertyTextBrush();
        foreach (var row in _propertyPanel.Children.OfType<WpfControls.Grid>())
        {
            foreach (var text in row.Children.OfType<WpfControls.TextBlock>())
            {
                text.Foreground = propertyTextBrush;
            }
        }

        ApplyMouseThrough();
        var _ = RefreshPropertiesAsync();
    }

    private async Task RefreshPropertiesAsync()
    {
        if (_client == null)
        {
            Dispatcher.Invoke(() =>
            {
                _propertyPanel.Children.Clear();
                _titleText.Text = TextByCodes(0x65E0, 0x6587, 0x6863);
            });
            return;
        }

        try
        {
            var result = await _client.SendCommandAsync("read-properties", BuildPropertyArgs());
            var dict = result as Dictionary<string, object>;
            if (dict != null)
            {
                Dispatcher.Invoke(() => UpdatePropertyDisplay(dict));
            }
            else
            {
                Dispatcher.Invoke(() =>
                {
                    _propertyPanel.Children.Clear();
                    _titleText.Text = TextByCodes(0x65E0, 0x6587, 0x6863);
                });
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine("PropertyOverlayWindow.RefreshPropertiesAsync error: " + ex.Message);
            Dispatcher.Invoke(() =>
            {
                _propertyPanel.Children.Clear();
                _titleText.Text = TextByCodes(0x65E0, 0x6587, 0x6863);
            });
        }
    }

    private void UpdatePropertyDisplay(Dictionary<string, object> dict)
    {
        _propertyPanel.Children.Clear();

        var title = "-";
        if (dict.ContainsKey("title") && dict["title"] != null)
            title = dict["title"].ToString();
        if (string.IsNullOrEmpty(title)) title = "-";
        _titleText.Text = title;

        Dictionary<string, object> props = null;
        if (dict.ContainsKey("properties"))
        {
            props = dict["properties"] as Dictionary<string, object>;
        }
        if (props == null) return;

        if (Properties.Settings.Default.Form5_ShowKeyOnly)
        {
            foreach (var propName in GetKeyProperties())
            {
                var val = "";
                if (props.ContainsKey(propName) && props[propName] != null)
                    val = props[propName].ToString();
                AddPropertyRow(propName, val);
            }
            return;
        }

        foreach (var kvp in props)
        {
            AddPropertyRow(kvp.Key, kvp.Value != null ? kvp.Value.ToString() : "");
        }
    }

    public async Task SavePropertyAsync(string name, string value)
    {
        if (_client == null) return;
        var args = new Dictionary<string, object>
        {
            { "source", GetPropertySource() },
            { "properties", new Dictionary<string, object> { { name, value } } }
        };
        await _client.SendCommandAsync("write-properties", args);
    }

    private Dictionary<string, object> BuildPropertyArgs()
    {
        return new Dictionary<string, object>
        {
            { "source", GetPropertySource() }
        };
    }

    private string GetPropertySource()
    {
        if (Properties.Settings.Default.Form5_ShowCustomProps) return "custom";
        return "configuration";
    }

    private string[] GetKeyProperties()
    {
        var raw = Properties.Settings.Default.Form5_KeyProperties;
        if (string.IsNullOrWhiteSpace(raw))
        {
            return new[] {
                TextByCodes(0x7269, 0x6599, 0x7F16, 0x7801),
                TextByCodes(0x96F6, 0x4EF6, 0x56FE, 0x53F7),
                TextByCodes(0x6587, 0x4EF6, 0x540D, 0x79F0),
                TextByCodes(0x96F6, 0x4EF6, 0x7C7B, 0x578B),
                TextByCodes(0x4E0B, 0x6599, 0x5C3A, 0x5BF8),
                TextByCodes(0x7248, 0x672C),
                TextByCodes(0x8BBE, 0x8BA1),
                TextByCodes(0x51FA, 0x56FE) };
        }

        return raw.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(item => item.Trim())
            .Where(item => item.Length > 0)
            .ToArray();
    }

    private void AddPropertyRow(string propName, string propValue)
    {
        var row = new WpfControls.Grid() { Margin = new WpfNs.Thickness(0, 1, 0, 1) };
        row.ColumnDefinitions.Add(new WpfControls.ColumnDefinition() { Width = new WpfNs.GridLength(132) });
        row.ColumnDefinitions.Add(new WpfControls.ColumnDefinition() { Width = new WpfNs.GridLength(1, WpfNs.GridUnitType.Star) });
        AddCellText(row, propName, 0, true);
        AddCellText(row, propValue ?? "", 1, false);
        _propertyPanel.Children.Add(row);
    }

    private void AddCellText(WpfControls.Grid grid, string text, int column, bool isName)
    {
        var tb = new WpfControls.TextBlock()
        {
            Text = text,
            ToolTip = text,
            FontFamily = new WpfMedia.FontFamily("Microsoft YaHei"),
            FontSize = 15,
            FontWeight = WpfNs.FontWeights.Normal,
            Foreground = GetPropertyTextBrush(),
            TextTrimming = WpfNs.TextTrimming.CharacterEllipsis,
            VerticalAlignment = WpfNs.VerticalAlignment.Center,
            Margin = isName ? new WpfNs.Thickness(0, 1, 8, 1) : new WpfNs.Thickness(0, 1, 0, 1)
        };
        WpfControls.Grid.SetColumn(tb, column);
        grid.Children.Add(tb);
    }

    private WpfMedia.Brush GetPropertyTextBrush()
    {
        var scheme = Properties.Settings.Default.Form5_ColorScheme ?? "";
        if (scheme.Contains(TextByCodes(0x7EC8, 0x7AEF, 0x7EFF)) || scheme.Contains("缁堢"))
            return new WpfMedia.SolidColorBrush(WpfMedia.Color.FromRgb(16, 185, 129));
        if (scheme.Contains(TextByCodes(0x767D, 0x5B57)) || scheme.Contains("鐧"))
            return WpfMedia.Brushes.White;
        return WpfMedia.Brushes.Black;
    }

    private void Window_MouseLeftButtonDown(object sender, WpfInput.MouseButtonEventArgs e)
    {
        if (!Properties.Settings.Default.Form5_MouseThrough) DragMove();
    }

    private void Window_SourceInitialized(object sender, EventArgs e)
    {
        RegisterOverlayHotKey();
        ApplyMouseThrough();
    }

    private void ApplyMouseThrough()
    {
        var source = WpfNs.PresentationSource.FromVisual(this) as WpfInterop.HwndSource;
        if (source == null) return;

        var exStyle = GetWindowLong32(source.Handle, GwlExstyle);
        if (Properties.Settings.Default.Form5_MouseThrough)
            exStyle |= WsExTransparent;
        else
            exStyle &= ~WsExTransparent;
        SetWindowLong32(source.Handle, GwlExstyle, exStyle);
    }

    private void SetInitialLocation()
    {
        var workArea = WpfNs.SystemParameters.WorkArea;
        Left = workArea.Right - Width - 20;
        Top = workArea.Top + 60;
    }

    private void RegisterOverlayHotKey()
    {
        var source = WpfNs.PresentationSource.FromVisual(this) as WpfInterop.HwndSource;
        if (source == null) return;

        source.AddHook(WndProc);
        RegisterHotKey(source.Handle, HotkeyIdClickThrough, ModControl, VkF2);
    }

    private void UnregisterOverlayHotKey()
    {
        var source = WpfNs.PresentationSource.FromVisual(this) as WpfInterop.HwndSource;
        if (source == null) return;

        UnregisterHotKey(source.Handle, HotkeyIdClickThrough);
        source.RemoveHook(WndProc);
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WmHotkey && wParam.ToInt32() == HotkeyIdClickThrough)
        {
            ToggleMouseThrough();
            handled = true;
        }
        return IntPtr.Zero;
    }

    private void ToggleMouseThrough()
    {
        Properties.Settings.Default.Form5_MouseThrough = !Properties.Settings.Default.Form5_MouseThrough;
        Properties.Settings.Default.Save();
        ApplyMouseThrough();
    }

    private void Client_DocChanged(string title, string path)
    {
        Dispatcher.BeginInvoke(new Action(async () => await RefreshPropertiesAsync()));
    }

    private void Client_SelectionChanged(string name, string type)
    {
        Dispatcher.BeginInvoke(new Action(async () => await RefreshPropertiesAsync()));
    }

    private async void DocWatchTimer_Tick(object sender, EventArgs e)
    {
        await RefreshPropertiesAsync();
    }

    private static string TextByCodes(params int[] codes)
    {
        return new string(codes.Select(code => (char)code).ToArray());
    }
}
