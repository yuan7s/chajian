using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Windows.Data;
using WpfControls = System.Windows.Controls;
using WpfNs = System.Windows;

namespace ExternalProgram;

public partial class DrawingBatchFileFilterWindow : WpfNs.Window
{
    private readonly List<BatchFileItem> _allItems;
    private readonly ObservableCollection<BatchFileItem> _displayItems = new ObservableCollection<BatchFileItem>();
    private bool _isLoading = true;

    public List<string> SelectedPaths { get; private set; } = new List<string>();

    public DrawingBatchFileFilterWindow(List<string> scannedPaths)
    {
        InitializeComponent();

        _allItems = scannedPaths
            .Select(BatchFileItem.FromPath)
            .OrderBy(i => i.FullPath, StringComparer.OrdinalIgnoreCase)
            .ToList();

        FileList.ItemsSource = _displayItems;

        _isLoading = false;
        ApplyFilters();
    }

    private void ApplyFilters()
    {
        var includePart = FilterPartCheck.IsChecked.GetValueOrDefault();
        var includeAsm = FilterAssemblyCheck.IsChecked.GetValueOrDefault();
        var nameFilter = (NameFilterBox.Text ?? "").Trim();
        var propFilter = (PropertyFilterBox.Text ?? "").Trim();

        var filtered = _allItems.AsEnumerable();

        // Type filter
        if (!includePart || !includeAsm)
        {
            filtered = filtered.Where(i =>
                (includePart && i.DocType == "零件") ||
                (includeAsm && i.DocType == "装配体"));
        }

        // Filename filter
        if (!string.IsNullOrWhiteSpace(nameFilter))
        {
            filtered = filtered.Where(i =>
                i.FileName.Contains(nameFilter, StringComparison.OrdinalIgnoreCase));
        }

        // Path/property keyword filter
        if (!string.IsNullOrWhiteSpace(propFilter))
        {
            // Split by spaces for multi-keyword search
            var keywords = propFilter.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
            filtered = filtered.Where(i =>
                keywords.All(k => i.FullPath.Contains(k, StringComparison.OrdinalIgnoreCase)));
        }

        var filteredList = filtered.ToList();

        _displayItems.Clear();
        foreach (var item in filteredList)
            _displayItems.Add(item);

        UpdateCounts();
    }

    private void UpdateCounts()
    {
        var selected = _displayItems.Count(i => i.IsSelected);
        SelectedCountText.Text = selected.ToString();
        TotalCountText.Text = _displayItems.Count.ToString();
    }

    private void FilterPartCheck_Changed() => ApplyFilters();
    private void FilterAssemblyCheck_Changed() => ApplyFilters();

    private void Filter_Changed(object sender, WpfNs.RoutedEventArgs e)
    {
        if (_isLoading) return;
        ApplyFilters();
    }

    private void NameFilter_TextChanged(object sender, WpfControls.TextChangedEventArgs e)
    {
        if (_isLoading) return;
        ApplyFilters();
    }

    private void PropertyFilter_TextChanged(object sender, WpfControls.TextChangedEventArgs e)
    {
        if (_isLoading) return;
        ApplyFilters();
    }

    private void SelectAll_Click(object sender, WpfNs.RoutedEventArgs e)
    {
        foreach (var item in _displayItems)
            item.IsSelected = true;
        UpdateCounts();
    }

    private void InvertSelection_Click(object sender, WpfNs.RoutedEventArgs e)
    {
        foreach (var item in _displayItems)
            item.IsSelected = !item.IsSelected;
        UpdateCounts();
    }

    private void Ok_Click(object sender, WpfNs.RoutedEventArgs e)
    {
        // 仅返回当前筛选可见且已勾选的项（所见即所得）
        SelectedPaths = _displayItems
            .Where(i => i.IsSelected)
            .Select(i => i.FullPath)
            .ToList();

        DialogResult = true;
        Close();
    }

    private void Cancel_Click(object sender, WpfNs.RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
