Imports Wpf = System.Windows
Imports WpfInput = System.Windows.Input

Partial Public Class CodingCleanupWindow
    Inherits Wpf.Window

    Private IsLoading As Boolean

    Public Property Client As SwAddinClient

    Private Sub Window_Loaded(sender As Object, e As Wpf.RoutedEventArgs)
        Topmost = True
        LoadSettings()
    End Sub

    Private Sub LoadSettings()
        IsLoading = True
        NameFilterBox.Text = My.Settings.CodingCleanup_NameFilter
        ProcessAsmCheck.IsChecked = My.Settings.CodingCleanup_ProcessAsm
        ProcessPartCheck.IsChecked = My.Settings.CodingCleanup_ProcessPart
        ExcludeVirtualCheck.IsChecked = My.Settings.CodingCleanup_ExcludeVirtual
        ExcludeStandardCheck.IsChecked = My.Settings.CodingCleanup_ExcludeStandard
        ExcludePurchasedCheck.IsChecked = My.Settings.CodingCleanup_ExcludePurchased
        IsLoading = False
    End Sub

    Private Sub NameFilterBox_LostFocus(sender As Object, e As Wpf.RoutedEventArgs)
        SaveNameFilter()
    End Sub

    Private Sub NameFilterBox_KeyDown(sender As Object, e As WpfInput.KeyEventArgs)
        If e.Key = WpfInput.Key.Enter Then
            SaveNameFilter()
            ExecuteButton.Focus()
        End If
    End Sub

    Private Sub SaveNameFilter()
        If IsLoading Then Return
        My.Settings.CodingCleanup_NameFilter = NameFilterBox.Text
        My.Settings.Save()
    End Sub

    Private Sub SettingCheck_Changed(sender As Object, e As Wpf.RoutedEventArgs)
        If IsLoading Then Return
        My.Settings.CodingCleanup_ProcessAsm = ProcessAsmCheck.IsChecked.GetValueOrDefault()
        My.Settings.CodingCleanup_ProcessPart = ProcessPartCheck.IsChecked.GetValueOrDefault()
        My.Settings.CodingCleanup_ExcludeVirtual = ExcludeVirtualCheck.IsChecked.GetValueOrDefault()
        My.Settings.CodingCleanup_ExcludeStandard = ExcludeStandardCheck.IsChecked.GetValueOrDefault()
        My.Settings.CodingCleanup_ExcludePurchased = ExcludePurchasedCheck.IsChecked.GetValueOrDefault()
        My.Settings.Save()
    End Sub

    Private Async Sub ExecuteButton_Click(sender As Object, e As Wpf.RoutedEventArgs)
        If Client Is Nothing Then
            Wpf.MessageBox.Show(Me, "未连接到 SolidWorks，请从主界面重新打开。", "提示", Wpf.MessageBoxButton.OK, Wpf.MessageBoxImage.Information)
            Return
        End If

        WpfInput.Mouse.OverrideCursor = WpfInput.Cursors.Wait
        ExecuteButton.IsEnabled = False
        Try
            Dim args = New Dictionary(Of String, Object) From {
                {"nameFilter", NameFilterBox.Text},
                {"processAsm", If(ProcessAsmCheck.IsChecked.GetValueOrDefault(), "true", "false")},
                {"processPart", If(ProcessPartCheck.IsChecked.GetValueOrDefault(), "true", "false")},
                {"excludeVirtual", If(ExcludeVirtualCheck.IsChecked.GetValueOrDefault(), "true", "false")},
                {"excludeStandard", If(ExcludeStandardCheck.IsChecked.GetValueOrDefault(), "true", "false")},
                {"excludePurchased", If(ExcludePurchasedCheck.IsChecked.GetValueOrDefault(), "true", "false")}
            }
            Await Client.SendCommandAsync("coding-cleanup", args)
            Wpf.MessageBox.Show(Me, "编码清理完成。", "提示", Wpf.MessageBoxButton.OK, Wpf.MessageBoxImage.Information)
        Catch ex As Exception
            Wpf.MessageBox.Show(Me, "编码清理失败: " & ex.Message, "错误", Wpf.MessageBoxButton.OK, Wpf.MessageBoxImage.Error)
        Finally
            ExecuteButton.IsEnabled = True
            WpfInput.Mouse.OverrideCursor = Nothing
        End Try
    End Sub
End Class
