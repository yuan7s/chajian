using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.Json;
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
            "SolidWorks 图纸格式 (*.slddrt)|*.slddrt|工程图模板 (*.drwdot)|*.drwdot|所有文件 (*.*)|*.*");
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

    public sealed class RenamePropertySetting
    {
        public string Name { get; set; }
        public string Value { get; set; }
    }
}
