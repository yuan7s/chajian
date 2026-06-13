using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;

namespace readbom;

public partial class SettingsWindow : Window
{
    private static readonly string[] BuiltInPropertyNames =
    [
        PropertyMappingConfig.FolderNamePropertyName,
        PropertyMappingConfig.FileNamePropertyName,
        PropertyMappingConfig.QuantityPropertyName
    ];

    private static readonly IReadOnlyList<PropertyMappingItem> DefaultProperties = PropertyMappingConfig.DefaultUserProperties;
    private static readonly IReadOnlyList<PropertySourceOption> SourceOptions =
    [
        new(PropertySourceMode.CurrentConfiguration, "配置属性"),
        new(PropertySourceMode.Custom, "自定义属性")
    ];

    private readonly ObservableCollection<PropertySettingItem> _items = [];

    public List<PropertyMappingItem> Properties { get; private set; } = [];
    public AppSettingsConfig Settings { get; private set; }

    public SettingsWindow(IReadOnlyList<PropertyMappingItem> properties, AppSettingsConfig settings)
    {
        InitializeComponent();
        Settings = settings;
        SourceModeColumn.ItemsSource = SourceOptions;
        PropertyGrid.ItemsSource = _items;
        LoadItems(properties);
        LoadSettings(settings);
    }

    private void SaveButton_OnClick(object sender, RoutedEventArgs e)
    {
        PropertyGrid.CommitEdit(DataGridEditingUnit.Cell, true);
        PropertyGrid.CommitEdit(DataGridEditingUnit.Row, true);

        var properties = ReadUserProperties();
        var errors = ValidateProperties(properties);
        errors.AddRange(ValidateFilterSettings());
        if (errors.Count > 0)
        {
            ValidationText.Text = string.Join(Environment.NewLine, errors);
            return;
        }

        Properties = properties;
        Settings = new AppSettingsConfig
        {
            PropertySourceMode = CurrentConfigurationSourceRadioButton.IsChecked == true
                ? PropertySourceMode.CurrentConfiguration
                : PropertySourceMode.Custom,
            SkipVirtual = SkipVirtualCheckBox.IsChecked == true,
            GroupByConfig = GroupByConfigCheckBox.IsChecked == true,
            ExcludeAssemblyIncludesMainAssembly = ExcludeMainAssemblyCheckBox.IsChecked == true,
            ExcludeAssemblyDocumentType = AssemblyDocumentTypeTextBox.Text.Trim(),
            ExcludeAssemblyPartType = AssemblyPartTypeTextBox.Text.Trim()
        };
        DialogResult = true;
    }

    private void AddButton_OnClick(object sender, RoutedEventArgs e)
    {
        var item = new PropertySettingItem
        {
            Name = string.Empty,
            IsBuiltIn = false,
            SourceMode = GetSelectedDefaultSourceMode()
        };
        _items.Add(item);
        PropertyGrid.SelectedItem = item;
        PropertyGrid.ScrollIntoView(item);
        if (PropertyGrid.Columns.Count > 0)
        {
            PropertyGrid.CurrentCell = new DataGridCellInfo(item, PropertyGrid.Columns[0]);
            PropertyGrid.BeginEdit();
        }
    }

    private void DeleteButton_OnClick(object sender, RoutedEventArgs e)
    {
        var selected = PropertyGrid.SelectedItems
            .OfType<PropertySettingItem>()
            .Where(item => !item.IsBuiltIn)
            .ToList();
        foreach (var item in selected)
        {
            _items.Remove(item);
        }
    }

    private void MoveUpButton_OnClick(object sender, RoutedEventArgs e)
    {
        MoveSelectedUserItem(-1);
    }

    private void MoveDownButton_OnClick(object sender, RoutedEventArgs e)
    {
        MoveSelectedUserItem(1);
    }

    private void ResetPropertiesButton_OnClick(object sender, RoutedEventArgs e)
    {
        LoadItems(DefaultProperties);
        ValidationText.Text = string.Empty;
    }

    private void ResetFilterButton_OnClick(object sender, RoutedEventArgs e)
    {
        var defaults = AppSettingsConfig.CreateDefault();
        ExcludeMainAssemblyCheckBox.IsChecked = defaults.ExcludeAssemblyIncludesMainAssembly;
        AssemblyDocumentTypeTextBox.Text = defaults.ExcludeAssemblyDocumentType;
        AssemblyPartTypeTextBox.Text = defaults.ExcludeAssemblyPartType;
        ValidationText.Text = string.Empty;
    }

    private void PropertyGrid_OnCellEditEnding(object? sender, DataGridCellEditEndingEventArgs e)
    {
        if (e.Row.Item is not PropertySettingItem { IsBuiltIn: true })
        {
            return;
        }

        e.Cancel = true;
    }

    private void MoveSelectedUserItem(int offset)
    {
        if (PropertyGrid.SelectedItem is not PropertySettingItem item || item.IsBuiltIn)
        {
            return;
        }

        var currentIndex = _items.IndexOf(item);
        var targetIndex = currentIndex + offset;
        var firstUserIndex = BuiltInPropertyNames.Length;
        if (targetIndex < firstUserIndex || targetIndex >= _items.Count)
        {
            return;
        }

        if (_items[targetIndex].IsBuiltIn)
        {
            return;
        }

        _items.Move(currentIndex, targetIndex);
        PropertyGrid.SelectedItem = item;
        PropertyGrid.ScrollIntoView(item);
    }

    private void LoadItems(IReadOnlyList<PropertyMappingItem> properties)
    {
        _items.Clear();
        foreach (var name in BuiltInPropertyNames)
        {
            _items.Add(new PropertySettingItem
            {
                Name = name,
                IsBuiltIn = true,
                SourceMode = PropertySourceMode.CurrentConfiguration
            });
        }

        foreach (var item in properties
                     .Where(item => !string.IsNullOrWhiteSpace(item.Name))
                     .Select(item => item with { Name = item.Name.Trim() })
                     .Where(item => !IsBuiltInPropertyName(item.Name)))
        {
            _items.Add(new PropertySettingItem
            {
                Name = item.Name,
                IsBuiltIn = false,
                SourceMode = item.SourceMode
            });
        }
    }

    private void LoadSettings(AppSettingsConfig settings)
    {
        CustomSourceRadioButton.IsChecked = settings.PropertySourceMode == PropertySourceMode.Custom;
        CurrentConfigurationSourceRadioButton.IsChecked =
            settings.PropertySourceMode == PropertySourceMode.CurrentConfiguration;
        SkipVirtualCheckBox.IsChecked = settings.SkipVirtual;
        GroupByConfigCheckBox.IsChecked = settings.GroupByConfig;
        ExcludeMainAssemblyCheckBox.IsChecked = settings.ExcludeAssemblyIncludesMainAssembly;
        AssemblyDocumentTypeTextBox.Text = settings.ExcludeAssemblyDocumentType;
        AssemblyPartTypeTextBox.Text = settings.ExcludeAssemblyPartType;
    }

    private List<PropertyMappingItem> ReadUserProperties()
    {
        return _items
            .Where(item => !item.IsBuiltIn)
            .Select(item => new PropertyMappingItem((item.Name ?? string.Empty).Trim(), item.SourceMode))
            .Where(item => !string.IsNullOrWhiteSpace(item.Name)
                           && !item.Name.StartsWith("#", StringComparison.Ordinal))
            .ToList();
    }

    private static List<string> ValidateProperties(IReadOnlyList<PropertyMappingItem> properties)
    {
        var errors = new List<string>();
        if (properties.Count == 0)
        {
            errors.Add("至少需要保留 1 个用户属性名。");
            return errors;
        }

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < properties.Count; i++)
        {
            var item = properties[i];
            var name = item.Name;
            if (name.Contains('|'))
            {
                errors.Add($"第 {i + 1} 个用户属性不支持 '|'，请只保留属性名: {name}");
            }

            if (IsBuiltInPropertyName(name))
            {
                errors.Add($"第 {i + 1} 个用户属性是内置属性，不能重复添加: {name}");
            }

            if (!seen.Add(name))
            {
                errors.Add($"第 {i + 1} 个用户属性重复: {name}");
            }

            if (!Enum.IsDefined(item.SourceMode))
            {
                errors.Add($"第 {i + 1} 个用户属性获取位置无效: {name}");
            }
        }

        return errors;
    }

    private PropertySourceMode GetSelectedDefaultSourceMode()
    {
        return CurrentConfigurationSourceRadioButton.IsChecked == true
            ? PropertySourceMode.CurrentConfiguration
            : PropertySourceMode.Custom;
    }

    private List<string> ValidateFilterSettings()
    {
        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(AssemblyDocumentTypeTextBox.Text))
        {
            errors.Add("去除组件装配的文档类型条件不能为空。");
        }

        if (string.IsNullOrWhiteSpace(AssemblyPartTypeTextBox.Text))
        {
            errors.Add("去除组件装配的零件类型条件不能为空。");
        }

        return errors;
    }

    private static bool IsBuiltInPropertyName(string name)
    {
        return BuiltInPropertyNames.Contains(name, StringComparer.OrdinalIgnoreCase);
    }
}

public sealed record PropertySourceOption(PropertySourceMode Mode, string DisplayName);

public sealed class PropertySettingItem : INotifyPropertyChanged
{
    private string _name = string.Empty;
    private PropertySourceMode _sourceMode = PropertySourceMode.CurrentConfiguration;

    public string Name
    {
        get => _name;
        set
        {
            if (string.Equals(_name, value, StringComparison.Ordinal))
            {
                return;
            }

            _name = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Name)));
        }
    }

    public bool IsBuiltIn { get; init; }

    public string Kind => IsBuiltIn ? "内置" : "用户";

    public PropertySourceMode SourceMode
    {
        get => _sourceMode;
        set
        {
            if (_sourceMode == value)
            {
                return;
            }

            _sourceMode = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SourceMode)));
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
}
