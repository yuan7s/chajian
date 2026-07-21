using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Text.Json;
using Microsoft.Win32;
using WpfNs = System.Windows;
using WpfControls = System.Windows.Controls;
using WpfInput = System.Windows.Input;
using WpfMedia = System.Windows.Media;
using WpfPrimitives = System.Windows.Controls.Primitives;
using WpfUiControls = Wpf.Ui.Controls;

namespace ExternalProgram;
using ExternalProgram.Properties;

partial class SettingsWindow : WpfUiControls.FluentWindow
{
    public event EventHandler SettingsApplied;
    private readonly ObservableCollection<ToolbarButtonLayoutItem> _toolbarPoolButtons = new ObservableCollection<ToolbarButtonLayoutItem>();
    private readonly ObservableCollection<ToolbarButtonLayoutItem> _toolbarPartButtons = new ObservableCollection<ToolbarButtonLayoutItem>();
    private readonly ObservableCollection<ToolbarButtonLayoutItem> _toolbarDrawingButtons = new ObservableCollection<ToolbarButtonLayoutItem>();
    private readonly ObservableCollection<ToolbarButtonLayoutItem> _toolbarAssemblyButtons = new ObservableCollection<ToolbarButtonLayoutItem>();
    private readonly ObservableCollection<RenamePropertySetting> _renameProperties = new ObservableCollection<RenamePropertySetting>();
    private WpfControls.ListBox _toolbarDragSourceList;
    private ToolbarButtonLayoutItem _toolbarDragItem;
    private WpfNs.Point _toolbarDragStartPoint;

    public SettingsWindow(int selectedTabIndex = 0)
    {
        InitializeComponent();
        ToolbarPoolList.ItemsSource = _toolbarPoolButtons;
        ToolbarPartList.ItemsSource = _toolbarPartButtons;
        ToolbarDrawingList.ItemsSource = _toolbarDrawingButtons;
        ToolbarAssemblyList.ItemsSource = _toolbarAssemblyButtons;
        RenamePropertyList.ItemsSource = _renameProperties;
        LoadSettings();

        SelectTab(selectedTabIndex);

        OverlayOpacitySlider.ValueChanged += OverlayOpacitySlider_ValueChanged;
    }

    public void SelectTab(int selectedTabIndex)
    {
        if (selectedTabIndex >= 0 && selectedTabIndex < SettingsTabs.Items.Count)
            SettingsTabs.SelectedIndex = selectedTabIndex;
    }

    private void LoadSettings()
    {
        var settings = Properties.Settings.Default;

        LoadToolbarButtonLayout(settings);

        DrawingStandardBox.Text = settings.Drawing_StandardPath ?? "";
        SheetFormatBox.Text = settings.Drawing_SheetFormatPath ?? "";

        OverlayOpacitySlider.Value = Math.Max(15, Math.Min(100, settings.Form5_Opacity * 100));
        UpdateOverlayOpacityLabel();
        SetComboText(OverlayColorCombo, string.IsNullOrWhiteSpace(settings.Form5_ColorScheme) ? "自适应" : settings.Form5_ColorScheme);
        OverlayDisplayModeCombo.SelectedIndex = settings.Form5_ShowKeyOnly ? 0 : 1;
        OverlayPropSourceCombo.SelectedIndex = settings.Form5_ShowCustomProps ? 1 : 0;
        OverlayTopMostCheck.IsChecked = settings.Form5_TopMost;
        OverlayMouseThroughCheck.IsChecked = settings.Form5_MouseThrough;
        LoadOverlayKeyProperties();

        SetRenameNamePropertySelection(settings);
        RenameBlankSizeCheck.IsChecked = settings.Rename_WriteBlankSize;
        SetComboTag(RenamePropertyTargetCombo, string.IsNullOrWhiteSpace(settings.Rename_PropertyTarget) ? "configuration" : settings.Rename_PropertyTarget);
        LoadRenameProperties();

        SortAssemblyFirstCheck.IsChecked = settings.Sort_AssemblyFirst;
        SortSuppressedLastCheck.IsChecked = settings.Sort_SuppressedLast;
        SortFoldersCheck.IsChecked = settings.Sort_SortFolders;
        SortRecursiveCheck.IsChecked = settings.Sort_RecursiveSubAssemblies;
        SetComboTag(SortNameSourceCombo, string.IsNullOrWhiteSpace(settings.Sort_NameSource) ? "ComponentName" : settings.Sort_NameSource);
        SetComboTag(SortDirectionCombo, string.IsNullOrWhiteSpace(settings.Sort_Direction) ? "Ascending" : settings.Sort_Direction);

        // 检查更新设置
        CurrentVersionText.Text = "v" + GetCurrentVersion();
        UpdateCheckUrlBox.Text = settings.Update_CheckUrl ?? "";
        AutoUpdateCheckBox.IsChecked = settings.Update_AutoCheck;
        var lastCheck = settings.Update_LastCheck;
        if (lastCheck > new DateTime(2001, 1, 1))
            UpdateStatusText.Text = "上次检查: " + lastCheck.ToString("yyyy-MM-dd HH:mm");

        // 插件注册设置
        PluginAutoRegisterCheck.IsChecked = settings.Plugin_AutoRegisterOnStart;
        PluginDllPathBox.Text = GetSwAddinDllPath();
        RefreshPluginRegistrationStatus();
    }

    private void SaveSettings()
    {
        var settings = Properties.Settings.Default;

        SaveToolbarButtonLayout(settings);

        settings.Drawing_StandardPath = DrawingStandardBox.Text.Trim();
        settings.Drawing_SheetFormatPath = SheetFormatBox.Text.Trim();

        settings.Form5_Opacity = OverlayOpacitySlider.Value / 100.0;
        settings.Form5_ColorScheme = GetComboText(OverlayColorCombo, "自适应");
        settings.Form5_ShowKeyOnly = OverlayDisplayModeCombo.SelectedIndex == 0;
        settings.Form5_ShowCustomProps = OverlayPropSourceCombo.SelectedIndex == 1;
        settings.Form5_TopMost = OverlayTopMostCheck.IsChecked.GetValueOrDefault();
        settings.Form5_MouseThrough = OverlayMouseThroughCheck.IsChecked.GetValueOrDefault();
        settings.Form5_KeyProperties = string.Join(",", OverlayKeyList.Items.Cast<object>()
            .Select(item => item.ToString())
            .Where(item => !string.IsNullOrWhiteSpace(item)));

        SaveRenameNamePropertySelection(settings);
        settings.Rename_CopyDrawing = true;
        settings.Rename_WriteBlankSize = RenameBlankSizeCheck.IsChecked.GetValueOrDefault();
        settings.Rename_PropertyTarget = GetComboTag(RenamePropertyTargetCombo, "configuration");
        SaveRenameProperties(settings);

        settings.Sort_AssemblyFirst = SortAssemblyFirstCheck.IsChecked.GetValueOrDefault();
        settings.Sort_SuppressedLast = SortSuppressedLastCheck.IsChecked.GetValueOrDefault();
        settings.Sort_SortFolders = SortFoldersCheck.IsChecked.GetValueOrDefault();
        settings.Sort_RecursiveSubAssemblies = SortRecursiveCheck.IsChecked.GetValueOrDefault();
        settings.Sort_NameSource = GetComboTag(SortNameSourceCombo, "ComponentName");
        settings.Sort_Direction = GetComboTag(SortDirectionCombo, "Ascending");

        settings.Update_CheckUrl = UpdateCheckUrlBox.Text.Trim();
        settings.Update_AutoCheck = AutoUpdateCheckBox.IsChecked.GetValueOrDefault();

        settings.Plugin_AutoRegisterOnStart = PluginAutoRegisterCheck.IsChecked.GetValueOrDefault();

        settings.Save();
        PropertyOverlayWindow.ApplySettingsToOpenWindows();
        SettingsApplied?.Invoke(this, EventArgs.Empty);
        SaveStatusText.Text = "设置已保存";
    }

    private void LoadToolbarButtonLayout(Properties.Settings settings)
    {
        _toolbarPoolButtons.Clear();
        for (var i = 0; i < ToolbarButtonLayoutStore.Definitions.Count; i++)
        {
            var definition = ToolbarButtonLayoutStore.Definitions[i];
            _toolbarPoolButtons.Add(new ToolbarButtonLayoutItem
            {
                Id = definition.Id,
                Text = definition.Text,
                Group = ToolbarButtonGroups.Pool,
                Order = i
            });
        }

        _toolbarPartButtons.Clear();
        _toolbarDrawingButtons.Clear();
        _toolbarAssemblyButtons.Clear();

        foreach (var item in ToolbarButtonLayoutStore.Load(settings))
        {
            GetToolbarCollection(item.Group)?.Add(item.CloneForGroup(item.Group, item.Order));
        }
    }

    private void SaveToolbarButtonLayout(Properties.Settings settings)
    {
        var items = GetToolbarLayoutItems();
        settings.Toolbar_ButtonLayout = ToolbarButtonLayoutStore.Serialize(items);
        settings.Toolbar_ButtonLayoutVersion = ToolbarButtonLayoutStore.CurrentLayoutVersion;
        ToolbarButtonLayoutStore.ApplyLegacyVisibility(settings, items);
    }

    private List<ToolbarButtonLayoutItem> GetToolbarLayoutItems()
    {
        var items = new List<ToolbarButtonLayoutItem>();
        AddToolbarLayoutItems(items, _toolbarPartButtons, ToolbarButtonGroups.Part);
        AddToolbarLayoutItems(items, _toolbarDrawingButtons, ToolbarButtonGroups.Drawing);
        AddToolbarLayoutItems(items, _toolbarAssemblyButtons, ToolbarButtonGroups.Assembly);
        return items;
    }

    private static void AddToolbarLayoutItems(
        List<ToolbarButtonLayoutItem> target,
        ObservableCollection<ToolbarButtonLayoutItem> source,
        string group)
    {
        for (var i = 0; i < source.Count; i++)
            target.Add(source[i].CloneForGroup(group, i));
    }

    private ObservableCollection<ToolbarButtonLayoutItem> GetToolbarCollection(string group)
    {
        if (string.Equals(group, ToolbarButtonGroups.Part, StringComparison.OrdinalIgnoreCase)) return _toolbarPartButtons;
        if (string.Equals(group, ToolbarButtonGroups.Drawing, StringComparison.OrdinalIgnoreCase)) return _toolbarDrawingButtons;
        if (string.Equals(group, ToolbarButtonGroups.Assembly, StringComparison.OrdinalIgnoreCase)) return _toolbarAssemblyButtons;
        if (string.Equals(group, ToolbarButtonGroups.Pool, StringComparison.OrdinalIgnoreCase)) return _toolbarPoolButtons;
        return null;
    }

    private void ToolbarList_PreviewMouseLeftButtonDown(object sender, WpfInput.MouseButtonEventArgs e)
    {
        _toolbarDragSourceList = sender as WpfControls.ListBox;
        _toolbarDragItem = GetToolbarItemFromSource(e.OriginalSource as WpfNs.DependencyObject);
        _toolbarDragStartPoint = e.GetPosition(null);
    }

    private void ToolbarList_MouseMove(object sender, WpfInput.MouseEventArgs e)
    {
        if (e.LeftButton != WpfInput.MouseButtonState.Pressed ||
            _toolbarDragSourceList == null ||
            _toolbarDragItem == null)
        {
            return;
        }

        var position = e.GetPosition(null);
        if (Math.Abs(position.X - _toolbarDragStartPoint.X) < WpfNs.SystemParameters.MinimumHorizontalDragDistance &&
            Math.Abs(position.Y - _toolbarDragStartPoint.Y) < WpfNs.SystemParameters.MinimumVerticalDragDistance)
        {
            return;
        }

        WpfNs.DragDrop.DoDragDrop(_toolbarDragSourceList, _toolbarDragItem, WpfNs.DragDropEffects.Move);
    }

    private void ToolbarList_DragOver(object sender, WpfNs.DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(typeof(ToolbarButtonLayoutItem))
            ? WpfNs.DragDropEffects.Move
            : WpfNs.DragDropEffects.None;
        e.Handled = true;
    }

    private void ToolbarList_Drop(object sender, WpfNs.DragEventArgs e)
    {
        var targetList = sender as WpfControls.ListBox;
        var draggedItem = e.Data.GetData(typeof(ToolbarButtonLayoutItem)) as ToolbarButtonLayoutItem;
        if (targetList == null || draggedItem == null) return;

        var sourceGroup = _toolbarDragSourceList?.Tag?.ToString() ?? ToolbarButtonGroups.Pool;
        var targetGroup = targetList.Tag?.ToString() ?? ToolbarButtonGroups.Pool;
        var sourceCollection = GetToolbarCollection(sourceGroup);
        var targetCollection = GetToolbarCollection(targetGroup);
        if (sourceCollection == null || targetCollection == null) return;

        if (string.Equals(targetGroup, ToolbarButtonGroups.Pool, StringComparison.OrdinalIgnoreCase))
        {
            if (!string.Equals(sourceGroup, ToolbarButtonGroups.Pool, StringComparison.OrdinalIgnoreCase))
                sourceCollection.Remove(draggedItem);
            MarkToolbarLayoutChanged();
            return;
        }

        var insertIndex = GetToolbarDropIndex(targetList, e);
        var existingTargetItem = targetCollection.FirstOrDefault(item =>
            string.Equals(item.Id, draggedItem.Id, StringComparison.OrdinalIgnoreCase));

        if (string.Equals(sourceGroup, ToolbarButtonGroups.Pool, StringComparison.OrdinalIgnoreCase))
        {
            if (existingTargetItem == null)
                InsertToolbarItem(targetCollection, draggedItem.CloneForGroup(targetGroup, insertIndex), insertIndex);
        }
        else if (ReferenceEquals(sourceCollection, targetCollection))
        {
            MoveToolbarItem(targetCollection, draggedItem, insertIndex);
        }
        else
        {
            sourceCollection.Remove(draggedItem);
            if (existingTargetItem == null)
                InsertToolbarItem(targetCollection, draggedItem.CloneForGroup(targetGroup, insertIndex), insertIndex);
        }

        MarkToolbarLayoutChanged();
    }

    private static ToolbarButtonLayoutItem GetToolbarItemFromSource(WpfNs.DependencyObject source)
    {
        while (source != null)
        {
            if (source is WpfControls.ListBoxItem item)
                return item.DataContext as ToolbarButtonLayoutItem;

            source = WpfMedia.VisualTreeHelper.GetParent(source);
        }

        return null;
    }

    private static int GetToolbarDropIndex(WpfControls.ListBox list, WpfNs.DragEventArgs e)
    {
        var position = e.GetPosition(list);
        for (var i = 0; i < list.Items.Count; i++)
        {
            var item = list.ItemContainerGenerator.ContainerFromIndex(i) as WpfControls.ListBoxItem;
            if (item == null) continue;

            var topLeft = item.TransformToAncestor(list).Transform(new WpfNs.Point(0, 0));
            if (position.Y < topLeft.Y + item.ActualHeight / 2.0)
                return i;
        }

        return list.Items.Count;
    }

    private static void InsertToolbarItem(
        ObservableCollection<ToolbarButtonLayoutItem> collection,
        ToolbarButtonLayoutItem item,
        int index)
    {
        item.Group = item.Group ?? "";
        index = Math.Max(0, Math.Min(index, collection.Count));
        collection.Insert(index, item);
    }

    private static void MoveToolbarItem(
        ObservableCollection<ToolbarButtonLayoutItem> collection,
        ToolbarButtonLayoutItem item,
        int targetIndex)
    {
        var oldIndex = collection.IndexOf(item);
        if (oldIndex < 0) return;

        targetIndex = Math.Max(0, Math.Min(targetIndex, collection.Count));
        if (oldIndex < targetIndex) targetIndex--;
        if (oldIndex == targetIndex) return;

        collection.Move(oldIndex, targetIndex);
    }

    private void MarkToolbarLayoutChanged()
    {
        SaveStatusText.Text = "按钮布局已修改，点击应用保存";
    }

    private void LoadRenameProperties()
    {
        _renameProperties.Clear();
        foreach (var item in GetRenameProperties())
            _renameProperties.Add(item);
    }

    private RenamePropertySetting[] GetRenameProperties()
    {
        var settings = Properties.Settings.Default;
        var raw = settings.Rename_CustomProperties;
        if (!string.IsNullOrWhiteSpace(raw))
        {
            try
            {
                var items = JsonSerializer.Deserialize<List<RenamePropertySetting>>(raw);
                if (items != null)
                {
                    return items
                        .Where(item => item != null && !string.IsNullOrWhiteSpace(item.Name))
                        .Select(item => new RenamePropertySetting
                        {
                            Name = item.Name.Trim(),
                            Value = item.Value ?? ""
                        })
                        .ToArray();
                }
            }
            catch
            {
            }
        }

        return new[]
        {
            new RenamePropertySetting { Name = "设计出图", Value = settings.Rename_DesignText ?? "" },
            new RenamePropertySetting { Name = "版本", Value = string.IsNullOrWhiteSpace(settings.Rename_VersionText) ? "A" : settings.Rename_VersionText }
        };
    }

    private void SaveRenameProperties(Properties.Settings settings)
    {
        var items = _renameProperties
            .Where(item => item != null && !string.IsNullOrWhiteSpace(item.Name))
            .Select(item => new RenamePropertySetting
            {
                Name = item.Name.Trim(),
                Value = item.Value ?? ""
            })
            .ToList();

        settings.Rename_CustomProperties = JsonSerializer.Serialize(items);

        var designItem = items.FirstOrDefault(item => IsDesignPropertyName(item.Name));
        settings.Rename_WriteDesign = designItem != null;
        settings.Rename_DesignText = designItem?.Value ?? "";

        var versionItem = items.FirstOrDefault(item => IsVersionPropertyName(item.Name));
        settings.Rename_WriteVersion = versionItem != null;
        settings.Rename_VersionText = versionItem?.Value ?? "";
    }

    private void SetRenameNamePropertySelection(Properties.Settings settings)
    {
        RenameNamePropertiesCheck.IsChecked =
            settings.Rename_WriteFileName ||
            settings.Rename_WriteMaterialCode ||
            settings.Rename_WritePartNumber;
    }

    private void SaveRenameNamePropertySelection(Properties.Settings settings)
    {
        var writeNamingProperties = RenameNamePropertiesCheck.IsChecked.GetValueOrDefault();
        settings.Rename_WriteFileName = writeNamingProperties;
        settings.Rename_WriteMaterialCode = writeNamingProperties;
        settings.Rename_WritePartNumber = writeNamingProperties;
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

    private void LoadOverlayKeyProperties()
    {
        OverlayKeyList.Items.Clear();
        foreach (var keyName in GetOverlayKeyProperties())
            OverlayKeyList.Items.Add(keyName);
    }

    private string[] GetOverlayKeyProperties()
    {
        var raw = Properties.Settings.Default.Form5_KeyProperties;
        if (string.IsNullOrWhiteSpace(raw))
        {
            return PropertyOverlayDefaults.KeyProperties;
        }

        return raw.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(item => item.Trim())
            .Where(item => item.Length > 0)
            .ToArray();
    }

    private void OverlayKeyAddButton_Click(object sender, WpfNs.RoutedEventArgs e)
    {
        var newKey = OverlayNewKeyText.Text.Trim();
        if (string.IsNullOrEmpty(newKey)) return;

        foreach (var item in OverlayKeyList.Items)
        {
            if (string.Equals(item.ToString(), newKey, StringComparison.OrdinalIgnoreCase)) return;
        }

        OverlayKeyList.Items.Add(newKey);
        OverlayNewKeyText.Clear();
        SaveStatusText.Text = "关键属性已修改，点击应用保存";
    }

    private void OverlayKeyDeleteButton_Click(object sender, WpfNs.RoutedEventArgs e)
    {
        if (OverlayKeyList.SelectedIndex < 0) return;
        OverlayKeyList.Items.RemoveAt(OverlayKeyList.SelectedIndex);
        SaveStatusText.Text = "关键属性已修改，点击应用保存";
    }

    private void OverlayKeyUpButton_Click(object sender, WpfNs.RoutedEventArgs e)
    {
        MoveOverlayKey(-1);
    }

    private void OverlayKeyDownButton_Click(object sender, WpfNs.RoutedEventArgs e)
    {
        MoveOverlayKey(1);
    }

    private void MoveOverlayKey(int direction)
    {
        var oldIndex = OverlayKeyList.SelectedIndex;
        if (oldIndex < 0) return;

        var newIndex = oldIndex + direction;
        if (newIndex < 0 || newIndex >= OverlayKeyList.Items.Count) return;

        var item = OverlayKeyList.Items[oldIndex];
        OverlayKeyList.Items.RemoveAt(oldIndex);
        OverlayKeyList.Items.Insert(newIndex, item);
        OverlayKeyList.SelectedIndex = newIndex;
        OverlayKeyList.ScrollIntoView(item);
        SaveStatusText.Text = "关键属性已修改，点击应用保存";
    }

    private void RenamePropertyAddButton_Click(object sender, WpfNs.RoutedEventArgs e)
    {
        var propertyName = RenameNewPropertyNameText.Text.Trim();
        if (string.IsNullOrEmpty(propertyName)) return;

        foreach (var item in _renameProperties)
        {
            if (string.Equals(item.Name, propertyName, StringComparison.OrdinalIgnoreCase)) return;
        }

        var itemToAdd = new RenamePropertySetting
        {
            Name = propertyName,
            Value = RenameNewPropertyValueText.Text.Trim()
        };
        _renameProperties.Add(itemToAdd);
        RenamePropertyList.SelectedItem = itemToAdd;
        RenamePropertyList.ScrollIntoView(itemToAdd);
        RenameNewPropertyNameText.Clear();
        RenameNewPropertyValueText.Clear();
        SaveStatusText.Text = "重命名写入属性已修改，点击应用保存";
    }

    private void RenamePropertyDeleteButton_Click(object sender, WpfNs.RoutedEventArgs e)
    {
        if (RenamePropertyList.SelectedIndex < 0) return;
        _renameProperties.RemoveAt(RenamePropertyList.SelectedIndex);
        SaveStatusText.Text = "重命名写入属性已修改，点击应用保存";
    }

    private void RenamePropertyUpButton_Click(object sender, WpfNs.RoutedEventArgs e)
    {
        MoveSelectedRenameProperty(-1);
    }

    private void RenamePropertyDownButton_Click(object sender, WpfNs.RoutedEventArgs e)
    {
        MoveSelectedRenameProperty(1);
    }

    private void MoveSelectedRenameProperty(int direction)
    {
        var oldIndex = RenamePropertyList.SelectedIndex;
        if (oldIndex < 0) return;

        var newIndex = oldIndex + direction;
        if (newIndex < 0 || newIndex >= _renameProperties.Count) return;

        _renameProperties.Move(oldIndex, newIndex);
        RenamePropertyList.SelectedIndex = newIndex;
        RenamePropertyList.ScrollIntoView(_renameProperties[newIndex]);
        SaveStatusText.Text = "重命名写入属性已修改，点击应用保存";
    }

    private void SelectDrawingStandard_Click(object sender, WpfNs.RoutedEventArgs e)
    {
        SelectFileInto(
            DrawingStandardBox,
            "SolidWorks 绘图标准 (*.sldstd)|*.sldstd|所有文件 (*.*)|*.*");
    }

    private void SelectSheetFormat_Click(object sender, WpfNs.RoutedEventArgs e)
    {
        SelectFileInto(
            SheetFormatBox,
            "SolidWorks 图纸格式 (*.slddrt)|*.slddrt|所有文件 (*.*)|*.*");
    }

    private void SelectFileInto(WpfControls.TextBox target, string filter)
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            CheckFileExists = true,
            Filter = filter
        };

        if (dialog.ShowDialog(this).GetValueOrDefault())
        {
            target.Text = dialog.FileName;
            SaveStatusText.Text = "工程图设置已修改，点击应用保存";
        }
    }

    private void OverlayOpacitySlider_ValueChanged(object sender, WpfNs.RoutedPropertyChangedEventArgs<double> e)
    {
        if (OverlayOpacityLabel == null) return;
        UpdateOverlayOpacityLabel();
    }

    private void UpdateOverlayOpacityLabel()
    {
        OverlayOpacityLabel.Text = "透明度: " + ((int)OverlayOpacitySlider.Value).ToString() + "%";
    }

    private void ApplyButton_Click(object sender, WpfNs.RoutedEventArgs e)
    {
        SaveSettings();
    }

    private void SaveAndCloseButton_Click(object sender, WpfNs.RoutedEventArgs e)
    {
        SaveSettings();
        Close();
    }

    private void CloseButton_Click(object sender, WpfNs.RoutedEventArgs e)
    {
        Close();
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
                source is WpfPrimitives.ScrollBar ||
                source is WpfPrimitives.Selector ||
                source is WpfControls.TextBox ||
                source is WpfControls.PasswordBox ||
                source is WpfControls.TabItem)
            {
                return true;
            }

            source = WpfMedia.VisualTreeHelper.GetParent(source);
        }

        return false;
    }

    private static void SetComboText(WpfControls.ComboBox combo, string text)
    {
        for (var i = 0; i < combo.Items.Count; i++)
        {
            var item = combo.Items[i] as WpfControls.ComboBoxItem;
            if (item != null && string.Equals(item.Content?.ToString(), text, StringComparison.OrdinalIgnoreCase))
            {
                combo.SelectedIndex = i;
                return;
            }
        }
        combo.SelectedIndex = 0;
    }

    private static string GetComboText(WpfControls.ComboBox combo, string fallback)
    {
        var item = combo.SelectedItem as WpfControls.ComboBoxItem;
        return item?.Content?.ToString() ?? fallback;
    }

    private static void SetComboTag(WpfControls.ComboBox combo, string tag)
    {
        for (var i = 0; i < combo.Items.Count; i++)
        {
            var item = combo.Items[i] as WpfControls.ComboBoxItem;
            if (item != null && string.Equals(item.Tag?.ToString(), tag, StringComparison.OrdinalIgnoreCase))
            {
                combo.SelectedIndex = i;
                return;
            }
        }
        combo.SelectedIndex = 0;
    }

    private static string GetComboTag(WpfControls.ComboBox combo, string fallback)
    {
        var item = combo.SelectedItem as WpfControls.ComboBoxItem;
        return item?.Tag?.ToString() ?? fallback;
    }

    // ==================== 检查更新 ====================

    private static string GetCurrentVersion()
    {
        var version = Assembly.GetExecutingAssembly().GetName().Version;
        return version != null ? version.ToString() : "1.0.0.0";
    }

    private async void CheckUpdateButton_Click(object sender, WpfNs.RoutedEventArgs e)
    {
        var checkUrl = UpdateCheckUrlBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(checkUrl))
        {
            UpdateStatusText.Text = "请先填写检查更新地址。";
            return;
        }

        UpdateStatusText.Text = "正在检查...";
        CheckUpdateButton.IsEnabled = false;

        try
        {
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
            var response = await http.GetStringAsync(checkUrl);
            var latestVersion = ParseVersionFromResponse(response);

            if (string.IsNullOrWhiteSpace(latestVersion))
            {
                UpdateStatusText.Text = "无法解析远程版本信息，请确认地址返回了版本号。";
                return;
            }

            var currentVersion = Assembly.GetExecutingAssembly().GetName().Version;
            if (Version.TryParse(latestVersion, out var remoteVersion))
            {
                UpdateStatusText.Text = remoteVersion > currentVersion
                    ? string.Format("发现新版本 v{0}（当前 v{1}），请下载更新。", remoteVersion, currentVersion)
                    : string.Format("已是最新版本 v{0}。", currentVersion);
            }
            else
            {
                UpdateStatusText.Text = "远程版本格式异常: " + latestVersion;
            }

            var settings = Properties.Settings.Default;
            settings.Update_LastCheck = DateTime.Now;
            settings.Save();
        }
        catch (Exception ex)
        {
            UpdateStatusText.Text = "检查失败: " + ex.Message;
        }
        finally
        {
            CheckUpdateButton.IsEnabled = true;
        }
    }

    private static string ParseVersionFromResponse(string response)
    {
        if (string.IsNullOrWhiteSpace(response)) return null;

        try
        {
            using var doc = JsonDocument.Parse(response);
            if (doc.RootElement.TryGetProperty("version", out var versionElement))
                return versionElement.GetString();
            if (doc.RootElement.TryGetProperty("tag_name", out var tagElement))
                return tagElement.GetString()?.TrimStart('v', 'V');
        }
        catch
        {
            // 非 JSON 响应，回退到纯文本解析
        }

        var trimmed = response.Trim().TrimStart('v', 'V');
        return Version.TryParse(trimmed, out _) ? trimmed : null;
    }

    // ==================== 插件注册 ====================

    private const string SwAddinRegistryPath = @"SOFTWARE\SolidWorks\AddIns\{C8F7A3D2-6B51-4E92-A814-7F2D3C1E9A56}";

    private void SelectPluginDll_Click(object sender, WpfNs.RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            CheckFileExists = true,
            Filter = "插件程序集 (ExternalProgram.SwAddin.dll)|ExternalProgram.SwAddin.dll|程序集 (*.dll)|*.dll|所有文件 (*.*)|*.*"
        };

        if (dialog.ShowDialog(this).GetValueOrDefault())
        {
            PluginDllPathBox.Text = dialog.FileName;
        }
    }

    private void RefreshPluginStatusButton_Click(object sender, WpfNs.RoutedEventArgs e)
    {
        RefreshPluginRegistrationStatus();
    }

    private void RefreshPluginRegistrationStatus()
    {
        try
        {
            // COM 注册表项写在 LocalMachine 与 CurrentUser 两处，任一存在即视为已注册
            var registered = SubKeyExists(Registry.LocalMachine, SwAddinRegistryPath)
                             || SubKeyExists(Registry.CurrentUser, SwAddinRegistryPath);

            if (registered)
            {
                PluginStatusLabel.Text = "已注册";
                PluginStatusDot.Background = new WpfMedia.SolidColorBrush(WpfMedia.Color.FromRgb(0x16, 0xA3, 0x4A));
                PluginOpStatusText.Text = "插件已在 SolidWorks 中注册。";
            }
            else
            {
                PluginStatusLabel.Text = "未注册";
                PluginStatusDot.Background = new WpfMedia.SolidColorBrush(WpfMedia.Color.FromRgb(0xDC, 0x26, 0x26));
                PluginOpStatusText.Text = "插件未注册，SolidWorks 不会加载该插件。";
            }
        }
        catch (Exception ex)
        {
            PluginStatusLabel.Text = "检测失败";
            PluginStatusDot.Background = new WpfMedia.SolidColorBrush(WpfMedia.Color.FromRgb(0x9A, 0xA5, 0xB1));
            PluginOpStatusText.Text = "无法读取注册表: " + ex.Message;
        }
    }

    private static bool SubKeyExists(RegistryKey root, string subkey)
    {
        using var key = root.OpenSubKey(subkey);
        return key != null;
    }

    private void RegisterPluginButton_Click(object sender, WpfNs.RoutedEventArgs e)
    {
        RunRegAsm(register: true);
    }

    private void UnregisterPluginButton_Click(object sender, WpfNs.RoutedEventArgs e)
    {
        RunRegAsm(register: false);
    }

    private void RunRegAsm(bool register)
    {
        var dllPath = PluginDllPathBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(dllPath) || !File.Exists(dllPath))
        {
            PluginOpStatusText.Text = "插件 DLL 文件不存在: " + dllPath;
            return;
        }

        var regAsmPath = FindRegAsm();
        if (string.IsNullOrWhiteSpace(regAsmPath))
        {
            PluginOpStatusText.Text = "未找到 RegAsm.exe，请确认已安装 .NET Framework 4.x。";
            return;
        }

        var actionName = register ? "注册" : "卸载";
        PluginOpStatusText.Text = "正在" + actionName + "插件（需要管理员授权）...";
        RegisterPluginButton.IsEnabled = false;
        UnregisterPluginButton.IsEnabled = false;

        try
        {
            // COM 注册写 HKLM，需管理员权限。runas 提权要求 UseShellExecute=true，
            // 此时无法重定向输出，因此改用退出码判断结果。
            var args = register
                ? string.Format("\"{0}\" /codebase", dllPath)
                : string.Format("\"{0}\" /unregister", dllPath);

            var psi = new ProcessStartInfo(regAsmPath, args)
            {
                UseShellExecute = true,
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden,
                Verb = "runas"
            };

            using var process = Process.Start(psi);
            if (process == null)
            {
                PluginOpStatusText.Text = actionName + "失败：无法启动 RegAsm.exe。";
                return;
            }

            process.WaitForExit(30000);
            if (!process.HasExited)
            {
                PluginOpStatusText.Text = actionName + "超时，请重试。";
                return;
            }

            if (process.ExitCode == 0)
            {
                PluginOpStatusText.Text = actionName + "成功。";
            }
            else
            {
                PluginOpStatusText.Text = string.Format(
                    "{0}失败（RegAsm 退出码 {1}）。请确认已用管理员权限运行。",
                    actionName, process.ExitCode);
            }

            RefreshPluginRegistrationStatus();
        }
        catch (System.ComponentModel.Win32Exception)
        {
            // 用户在 UAC 提示中点了"否"
            PluginOpStatusText.Text = actionName + "已取消：需要管理员权限。";
        }
        catch (Exception ex)
        {
            PluginOpStatusText.Text = actionName + "异常: " + ex.Message;
        }
        finally
        {
            RegisterPluginButton.IsEnabled = true;
            UnregisterPluginButton.IsEnabled = true;
        }
    }

    private static string FindRegAsm()
    {
        var frameworkRoot = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.Windows),
            @"Microsoft.NET\Framework64");

        if (Directory.Exists(frameworkRoot))
        {
            // 优先匹配 v4.x（SwAddin 目标框架为 net48）
            var v4 = Path.Combine(frameworkRoot, @"v4.0.30319\RegAsm.exe");
            if (File.Exists(v4)) return v4;

            // 回退：扫描其它版本目录里的 RegAsm.exe
            foreach (var dir in Directory.GetDirectories(frameworkRoot, "v*"))
            {
                var candidate = Path.Combine(dir, "RegAsm.exe");
                if (File.Exists(candidate)) return candidate;
            }
        }

        return null;
    }

    private static string GetSwAddinDllPath()
    {
        const string dllName = "ExternalProgram.SwAddin.dll";
        var exeDir = AppDomain.CurrentDomain.BaseDirectory;

        // 发布目录结构: <exe>\SwAddin\ExternalProgram.SwAddin.dll
        var publishPath = Path.Combine(exeDir, "SwAddin", dllName);
        if (File.Exists(publishPath)) return publishPath;

        // 开发构建结构: 与主程序同目录（CopySwAddinToOutput 拷贝而来）
        var devPath = Path.Combine(exeDir, dllName);
        if (File.Exists(devPath)) return devPath;

        // 都不存在时返回发布路径，供用户手动指定
        return publishPath;
    }

    public sealed class RenamePropertySetting
    {
        public string Name { get; set; }
        public string Value { get; set; }
    }
}
