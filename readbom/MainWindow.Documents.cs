using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Media;

namespace ReadBom;

public partial class MainWindow
{
    private async Task OpenSolidWorksDocumentAsync(string path, string displayType)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            AppendLog($"打开{displayType}失败: 缺少完整路径", Brushes.Red);
            return;
        }

        try
        {
            var fullPath = Path.GetFullPath(path);
            if (!File.Exists(fullPath))
            {
                AppendLog($"打开{displayType}失败: 文件不存在 [{fullPath}]", Brushes.Red);
                return;
            }

            AppendLog($"打开{displayType}: {fullPath}");
            if (IsOfflineFileMode())
            {
                OpenLocalDocument(fullPath, displayType);
                return;
            }

            var result =
                await SolidWorksAddinClient.OpenDocumentAsync(fullPath, message => AppendBackgroundLog(message));
            AppendLog(result.Accepted
                ? $"已发送打开{displayType}命令: {result.Title}"
                : $"已打开{displayType}: {result.Title}");
        }
        catch (Exception ex)
        {
            AppendLog($"打开{displayType}失败: {ex.Message}", Brushes.Red);
        }
    }

    private static string ResolveDrawingPath(BomRow row)
    {
        if (string.IsNullOrWhiteSpace(row.FullPath))
        {
            return string.Empty;
        }

        if (Path.GetExtension(row.FullPath).Equals(".slddrw", StringComparison.OrdinalIgnoreCase))
        {
            return File.Exists(row.FullPath) ? row.FullPath : string.Empty;
        }

        try
        {
            var directory = Path.GetDirectoryName(row.FullPath);
            var fileName = Path.GetFileNameWithoutExtension(row.FullPath);
            if (string.IsNullOrWhiteSpace(directory) || string.IsNullOrWhiteSpace(fileName) ||
                !Directory.Exists(directory))
            {
                return string.Empty;
            }

            var directPath = Path.Combine(directory, fileName + ".slddrw");
            if (File.Exists(directPath))
            {
                return directPath;
            }

            return Directory.EnumerateFiles(directory, "*.slddrw")
                       .FirstOrDefault(path => string.Equals(Path.GetFileNameWithoutExtension(path), fileName,
                           StringComparison.OrdinalIgnoreCase))
                   ?? string.Empty;
        }
        catch
        {
            return string.Empty;
        }
    }

    private void BomGrid_OnBeginningEdit(object? sender, DataGridBeginningEditEventArgs e)
    {
        _editingRow = null;
        _editingPropertyName = string.Empty;
        _editingOriginalValue = string.Empty;

        var propertyName = GetDynamicColumnPropertyName(e.Column);
        if (string.IsNullOrWhiteSpace(propertyName) || e.Row.Item is not BomRow row)
        {
            return;
        }

        _editingRow = row;
        _editingPropertyName = propertyName;
        _editingOriginalValue = row.GetProperty(propertyName);
    }

    private void BomGrid_OnCellEditEnding(object? sender, DataGridCellEditEndingEventArgs e)
    {
        var propertyName = GetDynamicColumnPropertyName(e.Column);
        if (e.EditAction != DataGridEditAction.Commit || string.IsNullOrWhiteSpace(propertyName))
        {
            ClearEditingCellSnapshot();
            return;
        }

        if (e.Row.Item is BomRow row
            && ReferenceEquals(row, _editingRow)
            && string.Equals(propertyName, _editingPropertyName, StringComparison.OrdinalIgnoreCase)
            && e.EditingElement is TextBox textBox
            && string.Equals(_editingOriginalValue, textBox.Text, StringComparison.Ordinal))
        {
            ClearEditingCellSnapshot();
            return;
        }

        ClearEditingCellSnapshot();
        Dispatcher.BeginInvoke(new Action(() =>
        {
            try
            {
                BomGrid.Items.Refresh();
            }
            catch (InvalidOperationException)
            {
                Dispatcher.BeginInvoke(new Action(RefreshBomGridSafely),
                    System.Windows.Threading.DispatcherPriority.Background);
            }
        }), System.Windows.Threading.DispatcherPriority.Background);
    }

    private void ClearEditingCellSnapshot()
    {
        _editingRow = null;
        _editingPropertyName = string.Empty;
        _editingOriginalValue = string.Empty;
    }

    private void BomGrid_OnSelectedCellsChanged(object sender, SelectedCellsChangedEventArgs e)
    {
        UpdateSelectionRows();

        var row = e.AddedCells
            .Select(x => x.Item)
            .OfType<BomRow>()
            .FirstOrDefault();
        if (row is not null)
        {
            _contextMenuRow = row;
            _thumbnailRow = row;
            BomGrid.CurrentCell = e.AddedCells.First(x => x.Item == row);
        }

        UpdateThumbnailWindow();
    }

    private void BomGrid_OnCurrentCellChanged(object? sender, EventArgs e)
    {
        UpdateSelectionRows();

        if (BomGrid.CurrentCell.Item is BomRow row)
        {
            _contextMenuRow = row;
            _thumbnailRow = row;
        }

        UpdateThumbnailWindow();
    }

    private void UpdateSelectionRows()
    {
        var selectedRows = BomGrid.SelectedCells
            .Select(cell => cell.Item)
            .OfType<BomRow>()
            .ToHashSet();

        if (selectedRows.Count == 0 && BomGrid.CurrentCell.Item is BomRow currentRow)
        {
            selectedRows.Add(currentRow);
        }

        foreach (var row in _rows)
        {
            row.IsSelectionRow = selectedRows.Contains(row);
        }
    }

    private void UpdateThumbnailWindow()
    {
        if (!_thumbnailEnabled || _thumbnailWindow is null)
        {
            return;
        }

        var row = BomGrid.CurrentCell.Item as BomRow
                  ?? _thumbnailRow
                  ?? BomGrid.SelectedCells.Select(x => x.Item).OfType<BomRow>().FirstOrDefault();
        _thumbnailRow = row;
        _thumbnailWindow.UpdateRow(row);
    }

    private void CopyCellsMenuItem_OnClick(object sender, RoutedEventArgs e)
    {
        CopySelectedCellsToClipboard();
    }

    private void PasteCellsMenuItem_OnClick(object sender, RoutedEventArgs e)
    {
        PasteClipboardToDynamicCells();
    }

    private CopyColumnResult? ShowCopyColumnDialog()
    {
        var options = GetDynamicColumnOptions(includeAllOption: false);
        if (options.Count < 2)
        {
            MessageBox.Show(this, "至少需要 2 个 TXT 属性列才能复制列。", "提示", MessageBoxButton.OK,
                MessageBoxImage.Information);
            return null;
        }

        var sourceComboBox = CreateColumnComboBox(options);
        SelectDefaultColumn(sourceComboBox);
        var targetComboBox = CreateColumnComboBox(options);
        SelectDifferentColumn(targetComboBox, sourceComboBox.SelectedItem as ColumnOption);

        sourceComboBox.SelectionChanged += (_, _) =>
        {
            if (targetComboBox.SelectedItem is not ColumnOption target
                || sourceComboBox.SelectedItem is not ColumnOption source
                || !string.Equals(target.PropertyName, source.PropertyName, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            SelectDifferentColumn(targetComboBox, source);
        };

        var window = CreateToolDialog("复制列", 420, 250);
        var panel = CreateDialogPanel();
        panel.Children.Add(new TextBlock { Text = "源列", Margin = new Thickness(0, 0, 0, 6) });
        panel.Children.Add(sourceComboBox);
        panel.Children.Add(new TextBlock { Text = "目标列", Margin = new Thickness(0, 8, 0, 6) });
        panel.Children.Add(targetComboBox);
        AddDialogButtons(panel, window);
        window.Content = panel;

        if (window.ShowDialog() != true
            || sourceComboBox.SelectedItem is not ColumnOption sourceColumn
            || targetComboBox.SelectedItem is not ColumnOption targetColumn)
        {
            return null;
        }

        if (string.Equals(sourceColumn.PropertyName, targetColumn.PropertyName, StringComparison.OrdinalIgnoreCase))
        {
            MessageBox.Show(this, "源列和目标列不能相同。", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            return null;
        }

        return new CopyColumnResult(sourceColumn, targetColumn);
    }

    private FillColumnResult? ShowFillColumnDialog()
    {
        var options = GetDynamicColumnOptions(includeAllOption: false);
        if (options.Count == 0)
        {
            MessageBox.Show(this, "没有可填充的 TXT 属性列，请先读取 BOM。", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            return null;
        }

        var comboBox = CreateColumnComboBox(options);
        SelectDefaultColumn(comboBox);
        var valueBox = CreateToolTextBox();
        if (BomGrid.CurrentCell.Item is BomRow row && comboBox.SelectedItem is ColumnOption option)
        {
            valueBox.Text = row.GetProperty(option.PropertyName);
        }

        comboBox.SelectionChanged += (_, _) =>
        {
            if (BomGrid.CurrentCell.Item is BomRow currentRow && comboBox.SelectedItem is ColumnOption currentOption)
            {
                valueBox.Text = currentRow.GetProperty(currentOption.PropertyName);
            }
        };

        var window = CreateToolDialog("填充列", 420, 230);
        var panel = CreateDialogPanel();
        panel.Children.Add(new TextBlock { Text = "目标列", Margin = new Thickness(0, 0, 0, 6) });
        panel.Children.Add(comboBox);
        panel.Children.Add(new TextBlock { Text = "填充内容", Margin = new Thickness(0, 10, 0, 6) });
        panel.Children.Add(valueBox);
        AddDialogButtons(panel, window);
        window.Content = panel;

        return window.ShowDialog() == true && comboBox.SelectedItem is ColumnOption target
            ? new FillColumnResult(target, valueBox.Text)
            : null;
    }

    private FindReplaceResult? ShowFindReplaceDialog()
    {
        var options = GetDynamicColumnOptions(includeAllOption: true);
        if (options.Count == 0)
        {
            MessageBox.Show(this, "没有可查找替换的 TXT 属性列，请先读取 BOM。", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            return null;
        }

        var comboBox = CreateColumnComboBox(options);
        SelectDefaultColumn(comboBox);
        var findBox = CreateToolTextBox();
        var replaceBox = CreateToolTextBox();

        var window = CreateToolDialog("查找替换", 420, 300);
        var panel = CreateDialogPanel();
        panel.Children.Add(new TextBlock { Text = "目标列", Margin = new Thickness(0, 0, 0, 6) });
        panel.Children.Add(comboBox);
        panel.Children.Add(new TextBlock { Text = "查找内容", Margin = new Thickness(0, 10, 0, 6) });
        panel.Children.Add(findBox);
        panel.Children.Add(new TextBlock { Text = "替换为", Margin = new Thickness(0, 0, 0, 6) });
        panel.Children.Add(replaceBox);
        AddDialogButtons(panel, window);
        window.Content = panel;

        return window.ShowDialog() == true && comboBox.SelectedItem is ColumnOption target
            ? new FindReplaceResult(target, findBox.Text, replaceBox.Text)
            : null;
    }

    private ColumnOption? ShowBlankSizeColumnDialog()
    {
        var options = GetDynamicColumnOptions(includeAllOption: false);
        if (options.Count == 0)
        {
            MessageBox.Show(this, "没有可关联的 TXT 属性列，请先在设置 > 属性列中配置属性并读取 BOM。", "提示", MessageBoxButton.OK,
                MessageBoxImage.Information);
            return null;
        }

        var comboBox = CreateColumnComboBox(options);
        SelectColumnByPropertyName(comboBox, "下料尺寸");

        var window = CreateToolDialog("计算下料尺寸", 420, 210);
        var panel = CreateDialogPanel();
        panel.Children.Add(new TextBlock { Text = "目标属性列", Margin = new Thickness(0, 0, 0, 6) });
        panel.Children.Add(comboBox);
        panel.Children.Add(new TextBlock
        {
            Text = "仅计算并填入当前表格中目标列为空的项目。",
            Foreground = Brushes.DimGray,
            TextWrapping = TextWrapping.Wrap
        });
        AddDialogButtons(panel, window);
        window.Content = panel;

        return window.ShowDialog() == true && comboBox.SelectedItem is ColumnOption target
            ? target
            : null;
    }

    private List<ColumnOption> GetDynamicColumnOptions(bool includeAllOption)
    {
        var options = _dynamicPropertyColumns
            .OrderBy(x => x.Key.DisplayIndex)
            .Select(x => new ColumnOption(x.Value, GetPropertyDisplayName(x.Value)))
            .ToList();

        if (includeAllOption && options.Count > 0)
        {
            options.Insert(0, new ColumnOption(string.Empty, "全部TXT属性列"));
        }

        return options;
    }

    private ComboBox CreateColumnComboBox(IReadOnlyList<ColumnOption> options)
    {
        return new ComboBox
        {
            ItemsSource = options,
            DisplayMemberPath = nameof(ColumnOption.DisplayName),
            SelectedIndex = 0,
            MinWidth = 340,
            MinHeight = 36,
            FontSize = 13,
            Padding = new Thickness(8, 0, 8, 0),
            VerticalContentAlignment = VerticalAlignment.Center,
            MaxDropDownHeight = 320,
            Margin = new Thickness(0, 0, 0, 10)
        };
    }

    private static TextBox CreateToolTextBox()
    {
        return new TextBox
        {
            MinWidth = 340,
            MinHeight = 34,
            FontSize = 13,
            Padding = new Thickness(8, 3, 8, 3),
            VerticalContentAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 0, 10)
        };
    }

    private void SelectDefaultColumn(ComboBox comboBox)
    {
        if (BomGrid.CurrentCell.Column is null)
        {
            return;
        }

        var propertyName = GetDynamicColumnPropertyName(BomGrid.CurrentCell.Column);
        if (string.IsNullOrWhiteSpace(propertyName))
        {
            return;
        }

        foreach (var item in comboBox.Items)
        {
            if (item is ColumnOption option &&
                string.Equals(option.PropertyName, propertyName, StringComparison.OrdinalIgnoreCase))
            {
                comboBox.SelectedItem = option;
                return;
            }
        }
    }

    private static void SelectColumnByPropertyName(ComboBox comboBox, string propertyName)
    {
        foreach (var item in comboBox.Items)
        {
            if (item is ColumnOption option &&
                string.Equals(option.PropertyName, propertyName, StringComparison.OrdinalIgnoreCase))
            {
                comboBox.SelectedItem = option;
                return;
            }
        }
    }

    private static void SelectDifferentColumn(ComboBox comboBox, ColumnOption? source)
    {
        foreach (var item in comboBox.Items)
        {
            if (item is ColumnOption option
                && !string.Equals(option.PropertyName, source?.PropertyName, StringComparison.OrdinalIgnoreCase))
            {
                comboBox.SelectedItem = option;
                return;
            }
        }
    }

    private Window CreateToolDialog(string title, double width, double height)
    {
        return new Window
        {
            Title = title,
            Owner = this,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            SizeToContent = SizeToContent.Height,
            ResizeMode = ResizeMode.NoResize,
            Width = width,
            MinHeight = height
        };
    }

    private static StackPanel CreateDialogPanel()
    {
        return new StackPanel { Margin = new Thickness(14) };
    }

    private static void AddDialogButtons(Panel panel, Window window)
    {
        var buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(0, 8, 0, 0)
        };
        var okButton = new Button
            { Content = "确定", Width = 72, Height = 28, Margin = new Thickness(0, 0, 8, 0), IsDefault = true };
        var cancelButton = new Button { Content = "取消", Width = 72, Height = 28, IsCancel = true };
        okButton.Click += (_, _) => window.DialogResult = true;
        buttons.Children.Add(okButton);
        buttons.Children.Add(cancelButton);
        panel.Children.Add(buttons);
    }

    private void OpenHeaderFilterMenu(DataGridColumnHeader header, string propertyName)
    {
        var current = _headerFilters.TryGetValue(propertyName, out var value) ? value : string.Empty;
        var values = GetColumnDistinctValues(propertyName);
        var selectedValues = _headerValueFilters.TryGetValue(propertyName, out var storedValues)
            ? new HashSet<string>(storedValues, StringComparer.Ordinal)
            : new HashSet<string>(values, StringComparer.Ordinal);

        var textBox = new TextBox
        {
            Width = 240,
            Height = 28,
            FontSize = 12,
            Text = current,
            Margin = new Thickness(0, 4, 0, 6)
        };

        var menu = new ContextMenu
        {
            PlacementTarget = header,
            Placement = PlacementMode.Bottom,
            StaysOpen = true
        };

        var panel = new StackPanel { Width = 270, Margin = new Thickness(8) };
        panel.Children.Add(new TextBlock
            { Text = $"筛选: {GetColumnHeaderText(header.Column)}", FontSize = 12, FontWeight = FontWeights.SemiBold });
        panel.Children.Add(new TextBlock
            { Text = "文本包含", FontSize = 12, Margin = new Thickness(0, 6, 0, 0), Foreground = Brushes.DimGray });
        panel.Children.Add(textBox);

        var selectAll = new CheckBox
        {
            Content = "全选",
            IsChecked = values.Count == 0 || selectedValues.Count == values.Count,
            FontSize = 12,
            Margin = new Thickness(0, 0, 0, 4)
        };
        panel.Children.Add(selectAll);

        var valuePanel = new StackPanel();
        var checkBoxes = new List<CheckBox>();
        foreach (var columnValue in values)
        {
            var checkBox = new CheckBox
            {
                Content = string.IsNullOrEmpty(columnValue) ? "(空白)" : columnValue,
                Tag = columnValue,
                IsChecked = selectedValues.Contains(columnValue),
                FontSize = 12,
                Margin = new Thickness(0, 0, 0, 3)
            };
            checkBox.Checked += (_, _) => UpdateSelectAllCheckBox(selectAll, checkBoxes);
            checkBox.Unchecked += (_, _) => UpdateSelectAllCheckBox(selectAll, checkBoxes);
            checkBoxes.Add(checkBox);
            valuePanel.Children.Add(checkBox);
        }

        selectAll.Checked += (_, _) =>
        {
            foreach (var checkBox in checkBoxes)
            {
                checkBox.IsChecked = true;
            }
        };
        selectAll.Unchecked += (_, _) =>
        {
            if (!selectAll.IsKeyboardFocusWithin && !selectAll.IsMouseOver)
            {
                return;
            }

            foreach (var checkBox in checkBoxes)
            {
                checkBox.IsChecked = false;
            }
        };

        panel.Children.Add(new TextBlock
            { Text = "值筛选", FontSize = 12, Margin = new Thickness(0, 4, 0, 4), Foreground = Brushes.DimGray });
        panel.Children.Add(new ScrollViewer
        {
            Content = valuePanel,
            Height = 160,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            BorderBrush = Brushes.LightGray,
            BorderThickness = new Thickness(1),
            Padding = new Thickness(4)
        });

        var buttons = new StackPanel
            { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        var applyButton = new Button { Content = "应用", Width = 58, Height = 26, FontSize = 12, Margin = new Thickness(0, 0, 6, 0) };
        var clearButton = new Button { Content = "清除", Width = 58, Height = 26, FontSize = 12 };
        buttons.Children.Add(applyButton);
        buttons.Children.Add(clearButton);
        panel.Children.Add(buttons);

        applyButton.Click += (_, _) =>
        {
            var filter = textBox.Text.Trim();
            if (string.IsNullOrWhiteSpace(filter))
            {
                _headerFilters.Remove(propertyName);
            }
            else
            {
                _headerFilters[propertyName] = filter;
            }

            var checkedValues = checkBoxes
                .Where(x => x.IsChecked == true)
                .Select(x => (string)x.Tag)
                .ToHashSet(StringComparer.Ordinal);
            if (checkedValues.Count == values.Count)
            {
                _headerValueFilters.Remove(propertyName);
            }
            else
            {
                _headerValueFilters[propertyName] = checkedValues;
            }

            ApplyHeaderFilters();
            UpdateHeaderFilterButtons();
            menu.IsOpen = false;
        };

        clearButton.Click += (_, _) =>
        {
            _headerFilters.Remove(propertyName);
            _headerValueFilters.Remove(propertyName);
            ApplyHeaderFilters();
            UpdateHeaderFilterButtons();
            menu.IsOpen = false;
        };

        menu.Items.Add(new MenuItem { Header = panel, StaysOpenOnClick = true });
        menu.IsOpen = true;
        textBox.Focus();
        textBox.SelectAll();
    }

    private void ApplyHeaderFilters()
    {
        if (_view is null)
        {
            return;
        }

        if (_headerFilters.Count == 0 && _headerValueFilters.Count == 0 && !_excludeAssemblyComponentFilterEnabled)
        {
            _view.Filter = null;
        }
        else
        {
            _view.Filter = item =>
            {
                if (item is not BomRow row)
                {
                    return false;
                }

                foreach (var filter in _headerFilters)
                {
                    if (!ContainsIgnoreCase(GetCellValue(row, filter.Key), filter.Value))
                    {
                        return false;
                    }
                }

                foreach (var filter in _headerValueFilters)
                {
                    if (!filter.Value.Contains(GetCellValue(row, filter.Key)))
                    {
                        return false;
                    }
                }

                if (_excludeAssemblyComponentFilterEnabled && IsExcludedAssemblyComponentFilterRow(row))
                {
                    return false;
                }

                return true;
            };
        }

        _view.Refresh();
        ReindexVisibleRows();
        SetStatus(_headerFilters.Count == 0 && _headerValueFilters.Count == 0 && !_excludeAssemblyComponentFilterEnabled
            ? $"读取完成: {_rows.Count} 项"
            : $"过滤后: {GetVisibleRows().Count} 项");
        UpdateQuickFilterButtons();
    }

    private void ReindexVisibleRows()
    {
        var index = 1;
        foreach (var row in GetVisibleRows())
        {
            row.Index = index++;
        }

        BomGrid.Items.Refresh();
    }

    private void ExcludeStandardPartsButton_OnClick(object sender, RoutedEventArgs e)
    {
        if (_rows.Count == 0)
        {
            MessageBox.Show(this, "没有可筛选的数据，请先读取 BOM。", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        if (!_propertyMapping.PropertyNames.Contains(PropertyMappingConfig.PartTypePropertyName,
                StringComparer.OrdinalIgnoreCase))
        {
            MessageBox.Show(this, "当前属性配置中没有 [零件类型]，请先在设置 > 属性列中加入该属性。", "提示", MessageBoxButton.OK,
                MessageBoxImage.Information);
            return;
        }

        var propertyKey = GetPartTypeFilterKey();
        if (IsExcludeStandardPartsFilterActive())
        {
            _headerValueFilters.Remove(propertyKey);
            ApplyHeaderFilters();
            UpdateHeaderFilterButtons();
            AppendLog("快速筛选: 已取消去除标准件");
            return;
        }

        var values = GetColumnDistinctValues(propertyKey);
        var excludedValues = values.Where(IsStandardPartTypeValue).ToList();
        if (excludedValues.Count == 0)
        {
            MessageBox.Show(this, "零件类型中没有找到 [标准件]。", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var allowedValues = values
            .Where(value => !IsStandardPartTypeValue(value))
            .ToHashSet(StringComparer.Ordinal);
        _headerValueFilters[propertyKey] = allowedValues;
        ApplyHeaderFilters();
        UpdateHeaderFilterButtons();

        var hiddenRows = _rows.Count(row =>
            IsStandardPartTypeValue(row.GetProperty(PropertyMappingConfig.PartTypePropertyName)));
        AppendLog($"快速筛选: 已从零件类型中去除标准件，隐藏 {hiddenRows} 行");
    }

    private void ExcludeAssemblyComponentsButton_OnClick(object sender, RoutedEventArgs e)
    {
        if (_rows.Count == 0)
        {
            MessageBox.Show(this, "没有可筛选的数据，请先读取 BOM。", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        if (!string.IsNullOrWhiteSpace(_settings.ExcludeAssemblyPartType)
            && !_propertyMapping.PropertyNames.Contains(PropertyMappingConfig.PartTypePropertyName,
                StringComparer.OrdinalIgnoreCase))
        {
            MessageBox.Show(this, "当前筛选条件使用了 [零件类型]，请先在设置的属性列中加入该属性。", "提示", MessageBoxButton.OK,
                MessageBoxImage.Information);
            return;
        }

        _excludeAssemblyComponentFilterEnabled = !_excludeAssemblyComponentFilterEnabled;
        ApplyHeaderFilters();
        UpdateHeaderFilterButtons();

        if (_excludeAssemblyComponentFilterEnabled)
        {
            var hiddenRows = _rows.Count(IsExcludedAssemblyComponentFilterRow);
            AppendLog($"快速筛选: 已去除{GetExcludeAssemblyComponentFilterDescription()}，隐藏 {hiddenRows} 行");
        }
        else
        {
            AppendLog("快速筛选: 已取消去除组件装配");
        }
    }

    private void UpdateQuickFilterButtons()
    {
        ExcludeStandardPartsButton.Content = IsExcludeStandardPartsFilterActive() ? "取消去除标准件" : "去除标准件";
        ExcludeAssemblyComponentsButton.Content = _excludeAssemblyComponentFilterEnabled ? "取消组件装配" : "去除组件装配";
    }

    private bool IsExcludeStandardPartsFilterActive()
    {
        var propertyKey = GetPartTypeFilterKey();
        if (!_headerValueFilters.TryGetValue(propertyKey, out var allowedValues))
        {
            return false;
        }

        var standardValues = GetColumnDistinctValues(propertyKey)
            .Where(IsStandardPartTypeValue)
            .ToList();
        return standardValues.Count > 0 && standardValues.All(value => !allowedValues.Contains(value));
    }

    private static string GetPartTypeFilterKey()
    {
        return $"Properties[{PropertyMappingConfig.PartTypePropertyName}]";
    }

    private static bool IsStandardPartTypeValue(string value)
    {
        return !string.IsNullOrWhiteSpace(value)
               && value.Contains("标准件", StringComparison.OrdinalIgnoreCase);
    }

    private bool IsAssemblyComponentPartTypeRow(BomRow row)
    {
        return string.Equals(row.DocumentType?.Trim(), _settings.ExcludeAssemblyDocumentType,
                   StringComparison.OrdinalIgnoreCase)
               && string.Equals(row.GetProperty(PropertyMappingConfig.PartTypePropertyName).Trim(),
                   _settings.ExcludeAssemblyPartType, StringComparison.OrdinalIgnoreCase);
    }

    private bool IsExcludedAssemblyComponentFilterRow(BomRow row)
    {
        return (_settings.ExcludeAssemblyIncludesMainAssembly && IsMainAssemblyRow(row))
               || IsAssemblyComponentPartTypeRow(row);
    }

    private bool IsMainAssemblyRow(BomRow row)
    {
        return _rows.Count > 0 && ReferenceEquals(row, _rows[0]);
    }

    private string GetExcludeAssemblyComponentFilterDescription()
    {
        var target = $"文档类型为[{_settings.ExcludeAssemblyDocumentType}]且零件类型为[{_settings.ExcludeAssemblyPartType}]的行";
        return _settings.ExcludeAssemblyIncludesMainAssembly
            ? $"主装配体和{target}"
            : target;
    }

    private string GetColumnPropertyName(DataGridColumn column)
    {
        var dynamicName = GetDynamicColumnPropertyName(column);
        if (!string.IsNullOrWhiteSpace(dynamicName))
        {
            return $"Properties[{dynamicName}]";
        }

        if (column is DataGridBoundColumn boundColumn && boundColumn.Binding is Binding binding)
        {
            return binding.Path.Path;
        }

        return GetColumnHeaderText(column) switch
        {
            "类型" => nameof(BomRow.DocumentType),
            "工程图" => nameof(BomRow.DrawingStatus),
            _ => string.Empty
        };
    }

    private string GetColumnHeaderText(DataGridColumn column)
    {
        var header = _columnOriginalHeaders.TryGetValue(column, out var originalHeader)
            ? originalHeader
            : column.Header;

        return header?.ToString() ?? string.Empty;
    }

    private List<string> GetColumnDistinctValues(string propertyName)
    {
        return _rows
            .Select(row => GetCellValue(row, propertyName))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(x => string.IsNullOrEmpty(x) ? 0 : 1)
            .ThenBy(x => x, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }

    private static void UpdateSelectAllCheckBox(CheckBox selectAll, IReadOnlyCollection<CheckBox> checkBoxes)
    {
        if (checkBoxes.Count == 0)
        {
            selectAll.IsChecked = true;
            return;
        }

        var checkedCount = checkBoxes.Count(x => x.IsChecked == true);
        selectAll.IsChecked = checkedCount == checkBoxes.Count;
    }

    private DataGridColumnHeader? FindColumnHeader(DataGridColumn column)
    {
        BomGrid.UpdateLayout();
        return FindVisualChildren<DataGridColumnHeader>(BomGrid)
            .FirstOrDefault(header => header.Column == column);
    }

    private static IEnumerable<T> FindVisualChildren<T>(DependencyObject current) where T : DependencyObject
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(current); i++)
        {
            var child = VisualTreeHelper.GetChild(current, i);
            if (child is T typed)
            {
                yield return typed;
            }

            foreach (var descendant in FindVisualChildren<T>(child))
            {
                yield return descendant;
            }
        }
    }

    private void UpdateHeaderFilterButtons()
    {
        foreach (var column in BomGrid.Columns)
        {
            if (!_columnOriginalHeaders.ContainsKey(column))
            {
                _columnOriginalHeaders[column] = column.Header ?? string.Empty;
            }

            if (!_headerFilterEnabled)
            {
                column.Header = _columnOriginalHeaders[column];
                continue;
            }

            var propertyName = GetColumnPropertyName(column);
            if (string.IsNullOrWhiteSpace(propertyName))
            {
                column.Header = _columnOriginalHeaders[column];
                continue;
            }

            var isActive = _headerFilters.ContainsKey(propertyName) || _headerValueFilters.ContainsKey(propertyName);
            var button = new Button
            {
                Content = "▼",
                Width = 22,
                Height = 22,
                Padding = new Thickness(0),
                Margin = new Thickness(6, 0, 0, 0),
                Tag = column,
                ToolTip = "筛选",
                Foreground = isActive ? Brushes.White : Brushes.DimGray,
                Background = isActive ? Brushes.SteelBlue : Brushes.Transparent,
                BorderBrush = isActive ? Brushes.SteelBlue : Brushes.LightGray
            };
            button.Click += HeaderFilterGlyph_OnClick;

            column.Header = new DockPanel
            {
                LastChildFill = true,
                Children =
                {
                    button,
                    new TextBlock
                    {
                        Text = GetColumnHeaderText(column),
                        VerticalAlignment = VerticalAlignment.Center,
                        TextTrimming = TextTrimming.CharacterEllipsis
                    }
                }
            };
            DockPanel.SetDock(button, Dock.Right);
        }
    }

    private string GetDynamicColumnPropertyName(DataGridColumn column)
    {
        return _dynamicPropertyColumns.TryGetValue(column, out var propertyName) ? propertyName : string.Empty;
    }

    private static string GetCellValue(BomRow row, string propertyName)
    {
        return propertyName switch
        {
            nameof(BomRow.Index) => row.Index.ToString(CultureInfo.InvariantCulture),
            nameof(BomRow.DocumentType) => row.DocumentType,
            nameof(BomRow.DrawingStatus) => row.DrawingStatus,
            nameof(BomRow.FileName) => row.FileName,
            nameof(BomRow.Configuration) => row.Configuration,
            nameof(BomRow.Quantity) => row.Quantity.ToString(CultureInfo.InvariantCulture),
            nameof(BomRow.Material) => row.Material,
            nameof(BomRow.FullPath) => row.FullPath,
            _ when propertyName.StartsWith("Properties[", StringComparison.Ordinal) &&
                   propertyName.EndsWith("]", StringComparison.Ordinal)
                => row.GetProperty(propertyName["Properties[".Length..^1]),
            _ => row.GetProperty(propertyName)
        };
    }

    private static T? FindParent<T>(DependencyObject? current) where T : DependencyObject
    {
        while (current is not null)
        {
            if (current is T typed)
            {
                return typed;
            }

            current = VisualTreeHelper.GetParent(current);
        }

        return null;
    }

    private bool CopySelectedCellsToClipboard()
    {
        var cells = GetOrderedSelectedCells();
        if (cells.Count == 0 && BomGrid.CurrentCell.Item is BomRow currentRow && BomGrid.CurrentCell.Column is not null)
        {
            cells.Add(new DataGridCellInfo(currentRow, BomGrid.CurrentCell.Column));
        }

        if (cells.Count == 0)
        {
            return false;
        }

        var visibleRows = GetVisibleRows();
        var rowIndex = visibleRows
            .Select((row, index) => new { row, index })
            .ToDictionary(x => x.row, x => x.index);

        var lines = cells
            .Where(x => x.Item is BomRow && x.Column is not null)
            .GroupBy(x => rowIndex.TryGetValue((BomRow)x.Item, out var index) ? index : int.MaxValue)
            .OrderBy(x => x.Key)
            .Select(group => string.Join("\t", group
                .OrderBy(x => x.Column.DisplayIndex)
                .Select(x => GetCellValue((BomRow)x.Item, GetColumnPropertyName(x.Column)))))
            .ToList();

        if (lines.Count == 0)
        {
            return false;
        }

        Clipboard.SetText(string.Join(Environment.NewLine, lines));
        return true;
    }

    private bool PasteClipboardToDynamicCells()
    {
        if (!Clipboard.ContainsText())
        {
            return false;
        }

        var text = Clipboard.GetText();
        if (string.IsNullOrEmpty(text))
        {
            return false;
        }

        var cells = GetOrderedSelectedCells();
        var startCell = cells.FirstOrDefault();
        if (!startCell.IsValid && BomGrid.CurrentCell.Item is BomRow)
        {
            startCell = BomGrid.CurrentCell;
        }

        if (!startCell.IsValid || startCell.Item is not BomRow startRow || startCell.Column is null)
        {
            return false;
        }

        var startProperty = GetDynamicColumnPropertyName(startCell.Column);
        if (string.IsNullOrWhiteSpace(startProperty))
        {
            return false;
        }

        var visibleRows = GetVisibleRows();
        var startRowIndex = visibleRows.IndexOf(startRow);
        if (startRowIndex < 0)
        {
            return false;
        }

        var dynamicColumns = BomGrid.Columns
            .Select(column => new { column, propertyName = GetDynamicColumnPropertyName(column) })
            .Where(x => !string.IsNullOrWhiteSpace(x.propertyName))
            .OrderBy(x => x.column.DisplayIndex)
            .ToList();
        var startColumnIndex = dynamicColumns.FindIndex(x => x.column == startCell.Column);
        if (startColumnIndex < 0)
        {
            return false;
        }

        var matrix = ParseClipboardMatrix(text);
        if (matrix.Count == 0)
        {
            return false;
        }

        if (IsSingleClipboardValue(matrix))
        {
            var fillChanged = FillSelectedDynamicCells(cells, matrix[0][0]);
            if (fillChanged > 0)
            {
                RefreshBomGridSafely();
                AppendLog($"粘贴TXT属性: {fillChanged} 个单元格");
                return true;
            }
        }

        var changed = 0;
        for (var r = 0; r < matrix.Count; r++)
        {
            var rowIndex = startRowIndex + r;
            if (rowIndex >= visibleRows.Count)
            {
                break;
            }

            var row = visibleRows[rowIndex];
            for (var c = 0; c < matrix[r].Count; c++)
            {
                var columnIndex = startColumnIndex + c;
                if (columnIndex >= dynamicColumns.Count)
                {
                    break;
                }

                row.SetProperty(dynamicColumns[columnIndex].propertyName, matrix[r][c]);
                changed++;
            }
        }

        if (changed == 0)
        {
            return false;
        }

        RefreshBomGridSafely();
        AppendLog($"粘贴TXT属性: {changed} 个单元格");
        return true;
    }

    private bool BeginCurrentDynamicCellEdit(RoutedEventArgs editingEventArgs)
    {
        if (BomGrid.CurrentCell.Item is not BomRow || BomGrid.CurrentCell.Column is null)
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(GetDynamicColumnPropertyName(BomGrid.CurrentCell.Column)))
        {
            return false;
        }

        BomGrid.Focus();
        return BomGrid.BeginEdit(editingEventArgs);
    }

    private bool ClearSelectedDynamicCells()
    {
        var cells = GetOrderedSelectedCells();
        if (cells.Count == 0 && BomGrid.CurrentCell.Item is BomRow currentRow && BomGrid.CurrentCell.Column is not null)
        {
            cells.Add(new DataGridCellInfo(currentRow, BomGrid.CurrentCell.Column));
        }

        var changed = FillSelectedDynamicCells(cells, string.Empty);
        if (changed == 0)
        {
            return false;
        }

        RefreshBomGridSafely();
        AppendLog($"清空TXT属性: {changed} 个单元格");
        return true;
    }

    private int FillSelectedDynamicCells(IReadOnlyList<DataGridCellInfo> cells, string value)
    {
        var changed = 0;
        foreach (var cell in cells)
        {
            if (cell.Item is not BomRow row || cell.Column is null)
            {
                continue;
            }

            var propertyName = GetDynamicColumnPropertyName(cell.Column);
            if (string.IsNullOrWhiteSpace(propertyName))
            {
                continue;
            }

            row.SetProperty(propertyName, value);
            changed++;
        }

        return changed;
    }

    private List<DataGridCellInfo> GetOrderedSelectedCells()
    {
        var visibleRows = GetVisibleRows();
        var rowIndex = visibleRows
            .Select((row, index) => new { row, index })
            .ToDictionary(x => x.row, x => x.index);

        return BomGrid.SelectedCells
            .Where(x => x.Item is BomRow && x.Column is not null)
            .OrderBy(x => rowIndex.TryGetValue((BomRow)x.Item, out var index) ? index : int.MaxValue)
            .ThenBy(x => x.Column.DisplayIndex)
            .ToList();
    }

    private static List<List<string>> ParseClipboardMatrix(string text)
    {
        return text
            .Replace("\r\n", "\n")
            .Replace('\r', '\n')
            .TrimEnd('\n')
            .Split('\n')
            .Where(line => line.Length > 0)
            .Select(line => line.Split('\t').ToList())
            .ToList();
    }

    private static bool IsSingleClipboardValue(IReadOnlyList<List<string>> matrix)
    {
        return matrix.Count == 1 && matrix[0].Count == 1;
    }

    private static string Escape(string? value)
    {
        var v = value ?? string.Empty;
        if (v.Contains('"') || v.Contains(',') || v.Contains('\n') || v.Contains('\r'))
        {
            return "\"" + v.Replace("\"", "\"\"") + "\"";
        }

        return v;
    }

    private string BuildExportFileName(IReadOnlyList<BomRow> rows, string extension)
    {
        var prefix = GetExportFileNamePrefix(rows);
        var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture);
        var name = string.IsNullOrWhiteSpace(prefix)
            ? $"bom_{timestamp}"
            : $"{prefix}_bom_{timestamp}";
        return $"{name}.{extension.TrimStart('.')}";
    }

    private string GetExportFileNamePrefix(IReadOnlyList<BomRow> visibleRows)
    {
        var connectedTitle = NormalizeExportTitle(_connectedDocumentTitle);
        if (!string.IsNullOrWhiteSpace(connectedTitle))
        {
            return SanitizeFileName(connectedTitle);
        }

        if (_rows.Count > 0)
        {
            return SanitizeFileName(GetRowExportName(_rows[0]));
        }

        return visibleRows.Count > 0 ? SanitizeFileName(GetRowExportName(visibleRows[0])) : string.Empty;
    }

    private string GetMainAssemblyDirectory()
    {
        if (_rows.Count == 0 || string.IsNullOrWhiteSpace(_rows[0].FullPath))
        {
            return string.Empty;
        }

        try
        {
            var directory = Path.GetDirectoryName(Path.GetFullPath(_rows[0].FullPath));
            return !string.IsNullOrWhiteSpace(directory) && Directory.Exists(directory) ? directory : string.Empty;
        }
        catch
        {
            return string.Empty;
        }
    }

    private static string GetRowExportName(BomRow row)
    {
        if (!string.IsNullOrWhiteSpace(row.FileName))
        {
            return row.FileName;
        }

        return string.IsNullOrWhiteSpace(row.FullPath)
            ? string.Empty
            : Path.GetFileNameWithoutExtension(row.FullPath);
    }

    private static string NormalizeExportTitle(string title)
    {
        var normalized = title.Trim().TrimEnd('*').Trim();
        return Path.GetFileNameWithoutExtension(normalized);
    }

    private static string SanitizeFileName(string value)
    {
        var invalidChars = Path.GetInvalidFileNameChars();
        var cleaned = new string(value
            .Select(ch => invalidChars.Contains(ch) ? '_' : ch)
            .ToArray()).Trim();
        return string.IsNullOrWhiteSpace(cleaned) ? "bom" : cleaned;
    }
}
