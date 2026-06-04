using System;
using System.Collections.Generic;
using System.Linq;
using WpfNs = System.Windows;
using WpfInput = System.Windows.Input;
using WpfUiControls = Wpf.Ui.Controls;

namespace 外部程序;
using 外部程序.Properties;

partial class CodingCleanupWindow : WpfUiControls.FluentWindow
{
    public CodingCleanupWindow()
    {
        InitializeComponent();
    }

    private bool _isLoading;

    public SwAddinClient Client { get; set; }

    private void Window_Loaded(object sender, WpfNs.RoutedEventArgs e)
    {
        Topmost = true;
        LoadSettings();
    }

    private void LoadSettings()
    {
        _isLoading = true;
        NameFilterBox.Text = Properties.Settings.Default.CodingCleanup_NameFilter;
        ProcessAsmCheck.IsChecked = true;
        ProcessPartCheck.IsChecked = Properties.Settings.Default.CodingCleanup_ProcessPart;
        ExcludeVirtualCheck.IsChecked = Properties.Settings.Default.CodingCleanup_ExcludeVirtual;
        ExcludeStandardCheck.IsChecked = Properties.Settings.Default.CodingCleanup_ExcludeStandard;
        ExcludePurchasedCheck.IsChecked = Properties.Settings.Default.CodingCleanup_ExcludePurchased;
        _isLoading = false;
    }

    private void NameFilterBox_LostFocus(object sender, WpfNs.RoutedEventArgs e)
    {
        SaveNameFilter();
    }

    private void NameFilterBox_KeyDown(object sender, WpfInput.KeyEventArgs e)
    {
        if (e.Key == WpfInput.Key.Enter)
        {
            SaveNameFilter();
            ExecuteButton.Focus();
        }
    }

    private void SaveNameFilter()
    {
        if (_isLoading) return;
        Properties.Settings.Default.CodingCleanup_NameFilter = NameFilterBox.Text;
        Properties.Settings.Default.Save();
    }

    private void SettingCheck_Changed(object sender, WpfNs.RoutedEventArgs e)
    {
        if (_isLoading) return;
        Properties.Settings.Default.CodingCleanup_ProcessAsm = ProcessAsmCheck.IsChecked.GetValueOrDefault();
        Properties.Settings.Default.CodingCleanup_ProcessPart = ProcessPartCheck.IsChecked.GetValueOrDefault();
        Properties.Settings.Default.CodingCleanup_ExcludeVirtual = ExcludeVirtualCheck.IsChecked.GetValueOrDefault();
        Properties.Settings.Default.CodingCleanup_ExcludeStandard = ExcludeStandardCheck.IsChecked.GetValueOrDefault();
        Properties.Settings.Default.CodingCleanup_ExcludePurchased = ExcludePurchasedCheck.IsChecked.GetValueOrDefault();
        Properties.Settings.Default.Save();
    }

    private async void ExecuteButton_Click(object sender, WpfNs.RoutedEventArgs e)
    {
        if (Client == null)
        {
            WpfNs.MessageBox.Show(this, "未连接到 SolidWorks，请从主界面重新打开。", "提示", WpfNs.MessageBoxButton.OK, WpfNs.MessageBoxImage.Information);
            return;
        }

        WpfInput.Mouse.OverrideCursor = WpfInput.Cursors.Wait;
        ExecuteButton.IsEnabled = false;
        try
        {
            var args = new Dictionary<string, object>
            {
                { "nameFilter", NameFilterBox.Text },
                { "processAsm", ProcessAsmCheck.IsChecked.GetValueOrDefault() ? "true" : "false" },
                { "processPart", ProcessPartCheck.IsChecked.GetValueOrDefault() ? "true" : "false" },
                { "excludeVirtual", ExcludeVirtualCheck.IsChecked.GetValueOrDefault() ? "true" : "false" },
                { "excludeStandard", ExcludeStandardCheck.IsChecked.GetValueOrDefault() ? "true" : "false" },
                { "excludePurchased", ExcludePurchasedCheck.IsChecked.GetValueOrDefault() ? "true" : "false" }
            };
            await Client.SendCommandAsync("coding-cleanup", args);
            WpfNs.MessageBox.Show(this, "编码清理完成。", "提示", WpfNs.MessageBoxButton.OK, WpfNs.MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            WpfNs.MessageBox.Show(this, "编码清理失败: " + ex.Message, "错误", WpfNs.MessageBoxButton.OK, WpfNs.MessageBoxImage.Error);
        }
        finally
        {
            ExecuteButton.IsEnabled = true;
            WpfInput.Mouse.OverrideCursor = null;
        }
    }
}
