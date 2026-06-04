using System;
using System.Linq;
using WpfNs = System.Windows;
using WpfControls = System.Windows.Controls;
using WpfUiControls = Wpf.Ui.Controls;

namespace 外部程序;
using 外部程序.Properties;

partial class PropertyOverlaySettingsWindow : WpfUiControls.FluentWindow
{
    public PropertyOverlaySettingsWindow()
    {
        InitializeComponent();
        LoadSettings();
    }

    private void LoadSettings()
    {
        OpacitySlider.Value = Math.Max(15, Math.Min(100, Properties.Settings.Default.Form5_Opacity * 100));
        UpdateOpacityLabel();
        SetComboText(ColorCombo, string.IsNullOrWhiteSpace(Properties.Settings.Default.Form5_ColorScheme) ? "自适应" : Properties.Settings.Default.Form5_ColorScheme);
        DisplayModeCombo.SelectedIndex = Properties.Settings.Default.Form5_ShowKeyOnly ? 0 : 1;
        PropSourceCombo.SelectedIndex = Properties.Settings.Default.Form5_ShowCustomProps ? 1 : 0;
        TopMostCheck.IsChecked = Properties.Settings.Default.Form5_TopMost;
        MouseThroughCheck.IsChecked = Properties.Settings.Default.Form5_MouseThrough;
        LoadKeyProperties();
    }

    private void LoadKeyProperties()
    {
        KeyList.Items.Clear();
        foreach (var keyName in GetKeyProperties())
            KeyList.Items.Add(keyName);
    }

    private string[] GetKeyProperties()
    {
        var raw = Properties.Settings.Default.Form5_KeyProperties;
        if (string.IsNullOrWhiteSpace(raw))
        {
            return new[] { "物料编码", "零件图号", "文件名称", "零件类型","零件材质","表面处理/热处理" ,"下料尺寸", "版本", "设计者", "出图者" };
        }

        return raw.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(item => item.Trim())
            .Where(item => item.Length > 0)
            .ToArray();
    }

    private void SaveKeyProperties()
    {
        var items = KeyList.Items.Cast<object>()
            .Select(item => item.ToString())
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .ToArray();
        Properties.Settings.Default.Form5_KeyProperties = string.Join(",", items);
        Properties.Settings.Default.Save();
        ApplyOverlaySettings();
    }

    private void OpacitySlider_ValueChanged(object sender, WpfNs.RoutedPropertyChangedEventArgs<double> e)
    {
        if (OpacityLabel == null) return;
        UpdateOpacityLabel();
        Properties.Settings.Default.Form5_Opacity = OpacitySlider.Value / 100.0;
        Properties.Settings.Default.Save();
        ApplyOverlaySettings();
    }

    private void Combo_SelectionChanged(object sender, WpfControls.SelectionChangedEventArgs e)
    {
        if (ColorCombo == null || DisplayModeCombo == null || PropSourceCombo == null) return;
        Properties.Settings.Default.Form5_ColorScheme = GetComboText(ColorCombo, "自适应");
        if (DisplayModeCombo.SelectedIndex >= 0) Properties.Settings.Default.Form5_ShowKeyOnly = DisplayModeCombo.SelectedIndex == 0;
        if (PropSourceCombo.SelectedIndex >= 0) Properties.Settings.Default.Form5_ShowCustomProps = PropSourceCombo.SelectedIndex == 1;
        Properties.Settings.Default.Save();
        ApplyOverlaySettings();
    }

    private void CheckBox_Changed(object sender, WpfNs.RoutedEventArgs e)
    {
        if (TopMostCheck == null || MouseThroughCheck == null) return;
        Properties.Settings.Default.Form5_TopMost = TopMostCheck.IsChecked.GetValueOrDefault();
        Properties.Settings.Default.Form5_MouseThrough = MouseThroughCheck.IsChecked.GetValueOrDefault();
        Properties.Settings.Default.Save();
        ApplyOverlaySettings();
    }

    private void AddButton_Click(object sender, WpfNs.RoutedEventArgs e)
    {
        var newKey = NewKeyText.Text.Trim();
        if (string.IsNullOrEmpty(newKey)) return;
        foreach (var item in KeyList.Items)
        {
            if (string.Equals(item.ToString(), newKey, StringComparison.OrdinalIgnoreCase)) return;
        }

        KeyList.Items.Add(newKey);
        NewKeyText.Clear();
        SaveKeyProperties();
    }

    private void DeleteButton_Click(object sender, WpfNs.RoutedEventArgs e)
    {
        if (KeyList.SelectedIndex < 0) return;
        KeyList.Items.RemoveAt(KeyList.SelectedIndex);
        SaveKeyProperties();
    }

    private void UpButton_Click(object sender, WpfNs.RoutedEventArgs e)
    {
        MoveSelectedKey(-1);
    }

    private void DownButton_Click(object sender, WpfNs.RoutedEventArgs e)
    {
        MoveSelectedKey(1);
    }

    private void MoveSelectedKey(int direction)
    {
        var oldIndex = KeyList.SelectedIndex;
        if (oldIndex < 0) return;

        var newIndex = oldIndex + direction;
        if (newIndex < 0 || newIndex >= KeyList.Items.Count) return;

        var item = KeyList.Items[oldIndex];
        KeyList.Items.RemoveAt(oldIndex);
        KeyList.Items.Insert(newIndex, item);
        KeyList.SelectedIndex = newIndex;
        KeyList.ScrollIntoView(item);
        SaveKeyProperties();
    }

    private void CloseButton_Click(object sender, WpfNs.RoutedEventArgs e)
    {
        Close();
    }

    private void UpdateOpacityLabel()
    {
        OpacityLabel.Text = "透明度: " + ((int)OpacitySlider.Value).ToString() + "%";
    }

    private void ApplyOverlaySettings()
    {
        PropertyOverlayWindow.ApplySettingsToOpenWindows();
    }

    private void SetComboText(WpfControls.ComboBox combo, string text)
    {
        for (var i = 0; i < combo.Items.Count; i++)
        {
            var item = combo.Items[i] as WpfControls.ComboBoxItem;
            if (item != null && string.Equals(item.Content.ToString(), text, StringComparison.OrdinalIgnoreCase))
            {
                combo.SelectedIndex = i;
                return;
            }
        }
        combo.SelectedIndex = 0;
    }

    private string GetComboText(WpfControls.ComboBox combo, string fallback)
    {
        var item = combo.SelectedItem as WpfControls.ComboBoxItem;
        if (item == null || item.Content == null) return fallback;
        return item.Content.ToString();
    }
}
