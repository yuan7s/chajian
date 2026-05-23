Imports SwConst
Imports Wpf = System.Windows
Imports WpfInput = System.Windows.Input

Partial Public Class CodingCleanupWindow
    Inherits Wpf.Window

    Private IsLoading As Boolean

    Public Property SwApp As SldWorks.SldWorks

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

    Private Sub ExecuteButton_Click(sender As Object, e As Wpf.RoutedEventArgs)
        If SwApp Is Nothing Then
            Wpf.MessageBox.Show(Me, "未连接到 SolidWorks，请从主界面重新打开。", "提示", Wpf.MessageBoxButton.OK, Wpf.MessageBoxImage.Information)
            Return
        End If

        Dim asmDoc As SldWorks.ModelDoc2 = TryCast(SwApp.ActiveDoc, SldWorks.ModelDoc2)
        If asmDoc Is Nothing Then
            Wpf.MessageBox.Show(Me, "请先在 SolidWorks 中打开一个文档。", "提示", Wpf.MessageBoxButton.OK, Wpf.MessageBoxImage.Information)
            Return
        End If
        If asmDoc.GetType() <> swDocumentTypes_e.swDocASSEMBLY Then
            Wpf.MessageBox.Show(Me, "请在装配体环境下使用此功能。", "提示", Wpf.MessageBoxButton.OK, Wpf.MessageBoxImage.Information)
            Return
        End If

        WpfInput.Mouse.OverrideCursor = WpfInput.Cursors.Wait
        ExecuteButton.IsEnabled = False
        Try
            CType(asmDoc, SldWorks.AssemblyDoc).ResolveAllLightWeightComponents(True)
            Dim activeConfig As SldWorks.Configuration = asmDoc.GetActiveConfiguration()
            If activeConfig Is Nothing Then Return
            Dim topConfString As String = activeConfig.Name
            ProcessConfig(SwApp, asmDoc, topConfString)

            Dim configuration As SldWorks.Configuration = asmDoc.GetConfigurationByName(topConfString)
            If configuration Is Nothing Then Return
            Dim rootComponent As SldWorks.Component2 = configuration.GetRootComponent()
            If rootComponent Is Nothing Then Return
            Dim comps As Object = rootComponent.GetChildren()
            If comps IsNot Nothing Then
                For Each child As SldWorks.Component2 In comps
                    ExecuteCodingCleanup(SwApp, child)
                Next
            End If

            Wpf.MessageBox.Show(Me, "编码整理完成", "提示", Wpf.MessageBoxButton.OK, Wpf.MessageBoxImage.Information)
        Catch ex As Exception
            Wpf.MessageBox.Show(Me, "执行出错: " & ex.Message, "错误", Wpf.MessageBoxButton.OK, Wpf.MessageBoxImage.Warning)
        Finally
            ExecuteButton.IsEnabled = True
            WpfInput.Mouse.OverrideCursor = Nothing
        End Try
    End Sub

    Private Sub ProcessConfig(swApp As SldWorks.SldWorks, modelDoc As SldWorks.ModelDoc2, confString As String)
        SyncTitleToCustomProperties(modelDoc, confString)
        MarkDocDirty(modelDoc)
    End Sub

    Private Overloads Sub ExecuteCodingCleanup(swApp As SldWorks.SldWorks, comp As SldWorks.Component2)
        If ShouldSkip(comp) Then Return

        Dim childModel As SldWorks.ModelDoc2 = comp.GetModelDoc()
        If childModel Is Nothing Then Return

        Dim childConfString As String = comp.ReferencedConfiguration
        Dim childType As Integer = childModel.GetType()
        Dim longstatus As Integer
        Dim longWarnings As Integer
        Dim fopen As SldWorks.ModelDoc2 = Nothing

        If childType = swDocumentTypes_e.swDocPART Then
            fopen = swApp.OpenDoc6(comp.GetPathName(), swDocumentTypes_e.swDocPART,
                swOpenDocOptions_e.swOpenDocOptions_Silent, "", longstatus, longWarnings)
            If longstatus = 0 AndAlso fopen IsNot Nothing Then
                SyncTitleToCustomProperties(fopen, childConfString)
                MarkDocDirty(fopen)
            End If
        End If

        If childType = swDocumentTypes_e.swDocASSEMBLY Then
            fopen = swApp.OpenDoc6(comp.GetPathName(), swDocumentTypes_e.swDocASSEMBLY,
                swOpenDocOptions_e.swOpenDocOptions_Silent, "", longstatus, longWarnings)
            If longstatus = 0 AndAlso fopen IsNot Nothing Then
                ProcessConfig(swApp, fopen, childConfString)
            End If
            ExecuteCodingCleanup(swApp, childModel, childConfString)
        End If
    End Sub

    Private Overloads Sub ExecuteCodingCleanup(swApp As SldWorks.SldWorks, asmDoc As SldWorks.ModelDoc2, confString As String)
        Dim configuration As SldWorks.Configuration = asmDoc.GetConfigurationByName(confString)
        If configuration Is Nothing Then Return
        Dim rootComponent As SldWorks.Component2 = configuration.GetRootComponent()
        If rootComponent Is Nothing Then Return
        Dim comps As Object = rootComponent.GetChildren()
        If comps Is Nothing Then Return

        For Each child As SldWorks.Component2 In comps
            ExecuteCodingCleanup(swApp, child)
        Next
    End Sub

    Private Function ShouldSkip(comp As SldWorks.Component2) As Boolean
        Dim nameFilter As String = NameFilterBox.Text.Trim()
        If Not String.IsNullOrEmpty(nameFilter) AndAlso Not comp.Name.Contains(nameFilter) Then Return True

        Dim compModel As SldWorks.ModelDoc2 = comp.GetModelDoc()
        Dim compType As Integer = If(compModel IsNot Nothing, compModel.GetType(), -1)

        If compType = swDocumentTypes_e.swDocPART Then
            If Not ProcessPartCheck.IsChecked.GetValueOrDefault() Then Return True
        ElseIf compType = swDocumentTypes_e.swDocASSEMBLY Then
            If Not ProcessAsmCheck.IsChecked.GetValueOrDefault() Then Return True
        End If

        If ExcludeVirtualCheck.IsChecked.GetValueOrDefault() Then
            Try
                If comp.IsVirtual() Then Return True
            Catch
            End Try
        End If

        If compModel Is Nothing Then Return False
        If ExcludeStandardCheck.IsChecked.GetValueOrDefault() Then
            Try
                Dim partType As String = compModel.GetCustomInfoValue(comp.ReferencedConfiguration, "零件类型")
                If Not String.IsNullOrEmpty(partType) AndAlso partType.Contains("标准件") Then Return True
            Catch
            End Try
        End If

        If ExcludePurchasedCheck.IsChecked.GetValueOrDefault() Then
            Try
                Dim partType As String = compModel.GetCustomInfoValue(comp.ReferencedConfiguration, "零件类型")
                If Not String.IsNullOrEmpty(partType) AndAlso partType.Contains("外购") Then Return True
            Catch
            End Try
        End If

        Return False
    End Function

    Private Sub SyncTitleToCustomProperties(modelDoc As SldWorks.ModelDoc2, confString As String)
        If modelDoc Is Nothing Then Return
        Dim docTitle As String = modelDoc.GetTitle()
        If InStr(docTitle, ".") > 0 Then
            docTitle = Strings.Left(docTitle, Len(docTitle) - 7)
        End If
        If String.IsNullOrEmpty(docTitle) Then Return

        Dim config As SldWorks.Configuration = modelDoc.GetConfigurationByName(confString)
        If config Is Nothing Then Return
        Dim cusPropMgr As SldWorks.CustomPropertyManager = config.CustomPropertyManager
        If cusPropMgr Is Nothing Then Return

        cusPropMgr.Add3("物料编码", swCustomInfoType_e.swCustomInfoText, docTitle,
            swCustomPropertyAddOption_e.swCustomPropertyDeleteAndAdd)
        cusPropMgr.Add3("零件图号", swCustomInfoType_e.swCustomInfoText, docTitle,
            swCustomPropertyAddOption_e.swCustomPropertyDeleteAndAdd)
        cusPropMgr.Add3("文件名称", swCustomInfoType_e.swCustomInfoText, docTitle,
            swCustomPropertyAddOption_e.swCustomPropertyDeleteAndAdd)
    End Sub

    Private Sub MarkDocDirty(modelDoc As SldWorks.ModelDoc2)
        If modelDoc Is Nothing Then Return
        Try
            modelDoc.SetSaveFlag()
        Catch
        End Try
    End Sub
End Class
