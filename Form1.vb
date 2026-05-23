Imports System.Runtime.InteropServices
Imports System.Runtime.InteropServices.ComTypes
Imports System.Diagnostics
Imports SwConst
Imports Wpf = System.Windows
Imports WpfControls = System.Windows.Controls
Imports WpfInterop = System.Windows.Interop
Imports WinForms = System.Windows.Forms

Public Class Form1

    Private _statusTimer As WinForms.Timer
    Private _trayIcon As WinForms.NotifyIcon
    Private _trayMenu As WinForms.ContextMenuStrip
    Private _sortProgressForm As WinForms.Form
    Private _sortProgressLabel As WinForms.Label
    Private _allowClose As Boolean
    Private _mainWindowHandle As IntPtr

    ' 全局热键
    Private Const MOD_CONTROL As Integer = &H2
    Private Const VK_F1 As Integer = &H70
    Private Const WM_HOTKEY As Integer = &H312
    Private Const HOTKEY_ID As Integer = 1

    <DllImport("user32.dll")>
    Private Shared Function RegisterHotKey(hWnd As IntPtr, id As Integer, fsModifiers As Integer, vk As Integer) As Boolean
    End Function

    <DllImport("user32.dll")>
    Private Shared Function UnregisterHotKey(hWnd As IntPtr, id As Integer) As Boolean
    End Function

    ' 另存为 DWG
    Private Sub Button1_Click(sender As Object, e As EventArgs)
        SaveActiveDrawingAsDwg()
    End Sub

    Private Sub Form1_Load(sender As Object, e As Wpf.RoutedEventArgs)
        TopMost = True
        Button12.Content = "取消置顶"

        ' 初始化托盘图标
        _trayMenu = New WinForms.ContextMenuStrip()
        _trayMenu.Items.Add("显示主窗口", Nothing, AddressOf TrayShow_Click)
        _trayMenu.Items.Add("-")
        _trayMenu.Items.Add("退出", Nothing, AddressOf TrayExit_Click)

        _trayIcon = New WinForms.NotifyIcon() With {
            .Icon = Drawing.SystemIcons.Application,
            .Text = "外部程序",
            .Visible = True,
            .ContextMenuStrip = _trayMenu
        }
        AddHandler _trayIcon.DoubleClick, AddressOf TrayShow_Click

        RefreshProcessList()
        ConnectToSelectedSw()
        _statusTimer = New WinForms.Timer() With {.Interval = 1000}
        AddHandler _statusTimer.Tick, AddressOf StatusTimer_Tick
        _statusTimer.Start()
    End Sub

    Private Sub Form1_SourceInitialized(sender As Object, e As EventArgs)
        _mainWindowHandle = New WpfInterop.WindowInteropHelper(Me).Handle
        Dim source = TryCast(Wpf.PresentationSource.FromVisual(Me), WpfInterop.HwndSource)
        If source IsNot Nothing Then source.AddHook(AddressOf WndProc)
        ' 窗口句柄就绪后再注册热键
        If Not RegisterHotKey(_mainWindowHandle, HOTKEY_ID, MOD_CONTROL, VK_F1) Then
            MsgBox("快捷键 Ctrl+F1 注册失败，可能已被其他程序占用。")
        End If
    End Sub

    Private Sub StatusTimer_Tick(sender As Object, e As EventArgs)
        UpdateStatusBar()
    End Sub

    Private Sub Form1_Closing(sender As Object, e As ComponentModel.CancelEventArgs)
        ' 拦截关闭按钮，隐藏到托盘
        If Not _allowClose Then
            e.Cancel = True
            Me.Hide()
        End If
    End Sub

    Private Sub Form1_Closed(sender As Object, e As EventArgs)
        ' 清理热键
        If _mainWindowHandle <> IntPtr.Zero Then UnregisterHotKey(_mainWindowHandle, HOTKEY_ID)

        ' 清理托盘图标
        If _trayIcon IsNot Nothing Then
            _trayIcon.Visible = False
            _trayIcon.Dispose()
            _trayIcon = Nothing
        End If
        If _trayMenu IsNot Nothing Then
            _trayMenu.Dispose()
            _trayMenu = Nothing
        End If

        If _statusTimer IsNot Nothing Then
            _statusTimer.Stop()
            _statusTimer.Dispose()
            _statusTimer = Nothing
        End If
    End Sub

    Private Sub RefreshProcessList()
        Dim prevInfo = TryCast(swProcessCombo.SelectedItem, SwProcessInfo)
        Dim preferredPid As Integer = If(My.Settings.DefaultSwProcessId > 0, My.Settings.DefaultSwProcessId, -1)
        PopulateSolidWorksProcesses()
        If swProcessCombo.Items.Count = 0 Then Return
        If preferredPid > 0 Then
            For i As Integer = 0 To swProcessCombo.Items.Count - 1
                Dim preferred = TryCast(swProcessCombo.Items(i), SwProcessInfo)
                If preferred IsNot Nothing AndAlso preferred.ProcessId = preferredPid Then
                    swProcessCombo.SelectedIndex = i
                    Return
                End If
            Next
        End If
        If swProcessCombo.Items.Count = 1 Then
            swProcessCombo.SelectedIndex = 0
            Return
        End If
        If prevInfo IsNot Nothing Then
            For i As Integer = 0 To swProcessCombo.Items.Count - 1
                Dim info = TryCast(swProcessCombo.Items(i), SwProcessInfo)
                If info IsNot Nothing AndAlso info.ProcessId = prevInfo.ProcessId Then
                    swProcessCombo.SelectedIndex = i
                    Return
                End If
            Next
        End If
    End Sub

    Private Sub UpdateStatusBar()
        Try
            If _swApp Is Nothing Then
                fileNameLabel.Text = "未连接"
                Return
            End If
            Dim modelDoc As SldWorks.ModelDoc2 = TryCast(_swApp.ActiveDoc, SldWorks.ModelDoc2)
            If modelDoc Is Nothing Then
                fileNameLabel.Text = "无文档"
                Return
            End If

            ' 尝试获取选中对象的文件名
            Dim selMgr As SldWorks.SelectionMgr = modelDoc.SelectionManager
            Dim selCount As Integer = 0
            Try
                selCount = selMgr.GetSelectedObjectCount2(-1)
            Catch
            End Try

            If selCount >= 1 Then
                Dim selObj As Object = selMgr.GetSelectedObject6(1, -1)
                If TypeOf selObj Is SldWorks.Component2 Then
                    Dim comp As SldWorks.Component2 = CType(selObj, SldWorks.Component2)
                    Dim refModel As SldWorks.ModelDoc2 = comp.GetModelDoc2()
                    If refModel IsNot Nothing Then
                        Dim fp As String = refModel.GetPathName()
                        If Not String.IsNullOrEmpty(fp) Then
                            fileNameLabel.Text = System.IO.Path.GetFileNameWithoutExtension(fp)
                            Return
                        End If
                    End If
                    Dim cp As String = comp.GetPathName()
                    If Not String.IsNullOrEmpty(cp) Then
                        fileNameLabel.Text = System.IO.Path.GetFileNameWithoutExtension(cp)
                        Return
                    End If
                ElseIf TypeOf selObj Is SldWorks.ModelDoc2 Then
                    Dim selModel As SldWorks.ModelDoc2 = CType(selObj, SldWorks.ModelDoc2)
                    Dim fp As String = selModel.GetPathName()
                    If Not String.IsNullOrEmpty(fp) Then
                        fileNameLabel.Text = System.IO.Path.GetFileNameWithoutExtension(fp)
                        Return
                    End If
                End If
            End If

            ' 无选择时显示文档标题
            Dim docPath As String = modelDoc.GetPathName()
            If Not String.IsNullOrEmpty(docPath) Then
                fileNameLabel.Text = System.IO.Path.GetFileNameWithoutExtension(docPath)
            Else
                fileNameLabel.Text = modelDoc.GetTitle()
            End If
        Catch
            fileNameLabel.Text = "错误"
        End Try
    End Sub

    ' 另存为 PDF
    Private Sub Button2_Click(sender As Object, e As EventArgs)
        Dim swApp As Object = GetSelectedSwApp()
        If swApp Is Nothing Then
            MsgBox("请先从下拉列表选择一个 SolidWorks 实例并确保它处于活动状态。")
            Exit Sub
        End If

        Dim targetPath As String = GetActiveOrSelectedModelPath(swApp)
        OpenPathInExplorer(targetPath)
    End Sub

    ' 在资源管理器中打开文件位置

    Private Function GetActiveOrSelectedModelPath(swApp As Object) As String
        If swApp Is Nothing Then Return ""
        Dim part As SldWorks.ModelDoc2 = TryCast(swApp.ActiveDoc, SldWorks.ModelDoc2)
        If part Is Nothing Then Return ""

        Try
            If part.GetType() = CInt(swDocumentTypes_e.swDocASSEMBLY) AndAlso part.SelectionManager IsNot Nothing Then
                Dim swComp As SldWorks.Component2 = TryCast(part.SelectionManager.GetSelectedObjectsComponent(1), SldWorks.Component2)
                If swComp IsNot Nothing Then
                    Dim compPath As String = swComp.GetPathName()
                    If Not String.IsNullOrWhiteSpace(compPath) Then Return compPath
                End If
            End If
        Catch
        End Try

        Try
            Return If(part.GetPathName(), "")
        Catch
            Return ""
        End Try
    End Function

    Private Sub OpenPathInExplorer(targetPath As String)
        If String.IsNullOrWhiteSpace(targetPath) Then
            MsgBox("当前文件还没有保存，无法打开目录。")
            Return
        End If

        If System.IO.File.Exists(targetPath) Then
            Process.Start(New ProcessStartInfo("explorer.exe", "/select,""" & targetPath & """") With {.UseShellExecute = True})
            Return
        End If

        Dim directoryPath As String = If(System.IO.Directory.Exists(targetPath), targetPath, System.IO.Path.GetDirectoryName(targetPath))
        If String.IsNullOrWhiteSpace(directoryPath) OrElse Not System.IO.Directory.Exists(directoryPath) Then
            MsgBox("找不到文件所在目录：" & targetPath)
            Return
        End If

        Process.Start(New ProcessStartInfo("explorer.exe", """" & directoryPath & """") With {.UseShellExecute = True})
    End Sub


    ' 写入下料尺寸属性
    Private Sub Button4_Click(sender As Object, e As EventArgs)
        RotateSelectedDrawingView()
    End Sub

    ' 设置绘图标准为 ISO
    Private Sub Button5_Click(sender As Object, e As EventArgs)
        Dim swApp As Object = GetSelectedSwApp()
        If swApp Is Nothing Then
            MsgBox("请先从下拉列表选择一个 SolidWorks 实例并确保它处于活动状态。")
            Exit Sub
        End If
        Dim part As SldWorks.ModelDoc2
        part = swApp.ActiveDoc
        part.Extension.SetUserPreferenceInteger(swUserPreferenceIntegerValue_e.swDetailingDimensionStandard, 0, swDetailingStandard_e.swDetailingStandardISO)
        MarkDocDirty(part)
    End Sub

    ' 选中工程图中所有悬空标注

    ' 旋转选中工程图视图 90°
    Private Sub Button8_Click(sender As Object, e As EventArgs)
        SaveActiveDrawingAsPdf()
    End Sub

    ' 同步物料编码/零件图号/文件名称属性
    Private Sub SaveActiveDrawingAsDwg()
        SaveActiveDrawingAs(".dwg", "DWG")
    End Sub

    Private Sub SaveActiveDrawingAsPdf()
        SaveActiveDrawingAs(".pdf", "PDF")
    End Sub

    Private Sub SaveActiveDrawingAs(extension As String, formatName As String)
        Dim swApp As Object = GetSelectedSwApp()
        If swApp Is Nothing Then
            MsgBox("请先从下拉列表选择一个 SolidWorks 实例并确保它处于活动状态。")
            Exit Sub
        End If

        Dim part As SldWorks.ModelDoc2 = TryCast(swApp.ActiveDoc, SldWorks.ModelDoc2)
        If part Is Nothing Then
            MsgBox("请先在 SolidWorks 中打开一个工程图。")
            Exit Sub
        End If
        If part.GetType() <> swDocumentTypes_e.swDocDRAWING Then
            swApp.SendMsgToUser2("请在工程图环境下使用", swMessageBoxIcon_e.swMbInformation, swMessageBoxBtn_e.swMbOk)
            Exit Sub
        End If

        Dim docPath As String = part.GetPathName()
        If String.IsNullOrWhiteSpace(docPath) Then
            MsgBox("当前工程图还没有保存，无法另存 " & formatName & "。")
            Exit Sub
        End If

        Dim outputPath As String = IO.Path.ChangeExtension(docPath, extension)
        part.SaveAs3(outputPath, 0, 2)
    End Sub

    Private Sub RotateSelectedDrawingView()
        Dim swApp As Object = GetSelectedSwApp()
        If swApp Is Nothing Then
            MsgBox("请先从下拉列表选择一个 SolidWorks 实例并确保它处于活动状态。")
            Exit Sub
        End If

        Dim part As SldWorks.ModelDoc2 = TryCast(swApp.ActiveDoc, SldWorks.ModelDoc2)
        If part Is Nothing Then
            MsgBox("请先在 SolidWorks 中打开一个工程图。")
            Exit Sub
        End If
        If part.GetType() <> swDocumentTypes_e.swDocDRAWING Then
            swApp.SendMsgToUser2("请在工程图环境下使用", swMessageBoxIcon_e.swMbInformation, swMessageBoxBtn_e.swMbOk)
            Exit Sub
        End If

        Dim swSelMgr As SldWorks.SelectionMgr = part.SelectionManager
        Dim swView As SldWorks.View = TryCast(swSelMgr.GetSelectedObject6(1, -1), SldWorks.View)
        If swView Is Nothing Then
            swApp.SendMsgToUser2("请选择一个视图", swMessageBoxIcon_e.swMbInformation, swMessageBoxBtn_e.swMbOk)
            Exit Sub
        End If

        If swView.Angle > 4.5 Then
            swView.Angle = 0
        Else
            swView.Angle += Math.PI / 2
        End If
        part.Extension.SelectByID2(swView.Name, "DRAWINGVIEW", 0, 0, 0, False, 0, Nothing, 0)
        MarkDocDirty(part)
    End Sub

    Private Sub Button9_Click(sender As Object, e As EventArgs)
        Dim swApp As Object = GetSelectedSwApp()
        If swApp Is Nothing Then
            MsgBox("请先从下拉列表选择一个 SolidWorks 实例并确保它处于活动状态。")
            Exit Sub
        End If
        Dim part As SldWorks.ModelDoc2
        part = swApp.ActiveDoc

        Dim swConfigurationManager As Object
        Dim swConfiguration As Object
        Dim activeCName As Object
        swConfigurationManager = part.ConfigurationManager
        swConfiguration = swConfigurationManager.ActiveConfiguration
        activeCName = swConfiguration.Name

        Dim c As String
        Dim d As String
        Dim f As String
        c = part.GetTitle()
        If InStr(c, ".") > 0 Then
            c = Strings.Left(c, Len(c) - 7)
        End If
        d = part.GetCustomInfoValue(activeCName, "物料编码")
        f = part.GetCustomInfoValue(activeCName, "零件图号")

        If c <> d Or c <> f Then
            Dim config As Object
            Dim cusPropMgr As Object
            config = part.GetActiveConfiguration
            cusPropMgr = config.CustomPropertyManager
            cusPropMgr.Add3("物料编码", swCustomInfoType_e.swCustomInfoText, c, swCustomPropertyAddOption_e.swCustomPropertyDeleteAndAdd)
            cusPropMgr.Add3("零件图号", swCustomInfoType_e.swCustomInfoText, c, swCustomPropertyAddOption_e.swCustomPropertyDeleteAndAdd)
            cusPropMgr.Add3("文件名称", swCustomInfoType_e.swCustomInfoText, c, swCustomPropertyAddOption_e.swCustomPropertyDeleteAndAdd)
            MarkDocDirty(part)
        End If


    End Sub


    ' FeatureManager 显示设置（隐藏配置/显示状态名称，递归）
    Private Sub Button11_Click(sender As Object, e As EventArgs)
        Dim swApp As SldWorks.SldWorks = TryCast(GetSelectedSwApp(), SldWorks.SldWorks)
        If swApp Is Nothing Then
            MsgBox("请先从下拉列表选择一个 SolidWorks 实例并确保它处于活动状态。")
            Exit Sub
        End If
        Dim part As SldWorks.ModelDoc2 = TryCast(swApp.ActiveDoc, SldWorks.ModelDoc2)
        If part Is Nothing Then
            MsgBox("请先在 SolidWorks 中打开一个文档。")
            Exit Sub
        End If
        Dim swFeatMgr As SldWorks.FeatureManager
        swFeatMgr = part.FeatureManager

        If part.GetType = swDocumentTypes_e.swDocASSEMBLY Then
            swFeatMgr.HideComponentSingleConfigurationOrDisplayStateNames = False
            swFeatMgr.SetComponentIdentifiers(4, 0, 0)
            swFeatMgr.SetComponentIdentifiers(2, 0, 0)

            swFeatMgr.ShowComponentConfigurationNames = False
            swFeatMgr.ShowComponentConfigurationDescriptions = False
            swFeatMgr.ShowDisplayStateNames = False
            SubAsmsjs(swApp, part)
            ShowAutoCloseNotice("完成")
        End If

    End Sub
    Function SubAsmsjs(swApp As SldWorks.SldWorks, asmDoc As SldWorks.ModelDoc2) As Object
        Dim configuration As SldWorks.Configuration
        Dim rootComponent As SldWorks.Component2
        Dim comps As Object
        Dim child As Object
        Dim childModel As SldWorks.ModelDoc2
        Dim fopen As SldWorks.ModelDoc2
        Dim childType As Integer
        Dim longstatus As Integer, longWarnings As Integer

        configuration = asmDoc.GetActiveConfiguration
        rootComponent = configuration.GetRootComponent
        comps = rootComponent.GetChildren  '’获取目录树

        For Each child In comps
            childModel = child.GetModelDoc
            If Not (childModel Is Nothing) Then
                childType = childModel.GetType

                If childType = swDocumentTypes_e.swDocPART Then  '处理零件

                End If
                If childType = swDocumentTypes_e.swDocASSEMBLY Then ' 装配体遍历
                    fopen = swApp.OpenDoc6(child.GetPathName, swDocumentTypes_e.swDocASSEMBLY, swOpenDocOptions_e.swOpenDocOptions_Silent, "", longstatus, longWarnings)

                    If longstatus = 0 Then
                        Dim swFeatMgr As SldWorks.FeatureManager
                        swFeatMgr = fopen.FeatureManager
                        swFeatMgr.HideComponentSingleConfigurationOrDisplayStateNames = False
                        swFeatMgr.SetComponentIdentifiers(4, 0, 0)
                        swFeatMgr.SetComponentIdentifiers(2, 0, 0)

                        swFeatMgr.ShowComponentConfigurationNames = False
                        swFeatMgr.ShowComponentConfigurationDescriptions = False
                        swFeatMgr.ShowDisplayStateNames = False
                    End If
                    SubAsmsjs(swApp, childModel)
                End If
            End If
        Next
        Return True
    End Function

    ' 切换窗口置顶
    Private Sub Button12_Click(sender As Object, e As EventArgs)
        TopMost = Not TopMost

        ' 根据状态更新按钮文本
        If TopMost Then
            Button12.Content = "取消置顶"
        Else
            Button12.Content = "置顶"
        End If
    End Sub

    ' 设计树排序（文件夹分组 + 递归子装配体）
    Private Sub Button13_Click(sender As Object, e As EventArgs)
        Dim swApp As Object = GetSelectedSwApp()
        If swApp Is Nothing Then
            MsgBox("请先从下拉列表选择一个 SolidWorks 实例并确保它处于活动状态。")
            Exit Sub
        End If

        Dim Part As SldWorks.ModelDoc2
        Part = swApp.ActiveDoc

        If Part Is Nothing Then
            MsgBox("当前没有任何文档打开， 该程序必须在装配体中运行！")
            Exit Sub
        ElseIf Part.GetType <> SwConst.swDocumentTypes_e.swDocASSEMBLY Then
            MsgBox("当前打开的文档不是一个装配体，请打开装配体后再试！")
            Exit Sub
        End If

        ShowSortProgress("正在准备装配体排序...")
        Try
            Dim Configuration As SldWorks.Configuration
            Configuration = Part.GetConfigurationByName(Part.GetActiveConfiguration.Name)

            Dim c As String
            c = Part.GetTitle()
            If InStr(c, ".") > 0 Then
                c = Strings.Left(c, Len(c) - 7)
            End If

            ' === 单次遍历，按文件夹开始/结束标记分组 ===
            UpdateSortProgress("正在读取设计树...")
            Dim vFeats As Object = Part.FeatureManager.GetFeatures(True)
            Dim modelDoc2 As SldWorks.ModelDoc2 = swApp.ActiveDoc
            Dim assemblyDoc As SldWorks.AssemblyDoc = CType(modelDoc2, SldWorks.AssemblyDoc)

            Dim folders As New List(Of SldWorks.Feature)()
            Dim folderComponents As New List(Of List(Of SldWorks.Feature))()
            Dim topLevelFeats As New List(Of SldWorks.Feature)()
            Dim suppressedEnvFeats As New List(Of SldWorks.Feature)()
            Dim currentFolderComps As List(Of SldWorks.Feature) = Nothing

            ' 遍历范围：原点之后、MateGroup 之前
            Dim startIdx As Integer = -1
            Dim endIdx As Integer = UBound(vFeats)
            For i = 0 To UBound(vFeats)
                Dim t As String = vFeats(i).GetTypeName2
                If startIdx < 0 AndAlso t = "OriginProfileFeature" Then startIdx = i + 1
                If t = "MateGroup" Then endIdx = i - 1 : Exit For
            Next
            If startIdx < 0 Then startIdx = 0

            UpdateSortProgress("正在分组组件...")
            For i = startIdx To endIdx
                Dim featType As String = vFeats(i).GetTypeName2
                If featType = "FtrFolder" Then
                    If Not vFeats(i).Name.Contains("___EndTag___") Then
                        folders.Add(vFeats(i))
                        currentFolderComps = New List(Of SldWorks.Feature)()
                        folderComponents.Add(currentFolderComps)
                    Else
                        currentFolderComps = Nothing
                    End If
                ElseIf featType = "Reference" Then
                    Dim isSupOrEnv As Boolean = False
                    Try
                        Dim comp As SldWorks.Component2 = TryCast(vFeats(i).GetSpecificFeature2, SldWorks.Component2)
                        If comp IsNot Nothing Then
                            isSupOrEnv = comp.IsSuppressed() OrElse comp.IsEnvelope()
                        End If
                    Catch
                    End Try
                    If isSupOrEnv Then
                        If currentFolderComps IsNot Nothing Then
                            currentFolderComps.Add(vFeats(i))
                        Else
                            suppressedEnvFeats.Add(vFeats(i))
                        End If
                    ElseIf currentFolderComps IsNot Nothing Then
                        currentFolderComps.Add(vFeats(i))
                    Else
                        topLevelFeats.Add(vFeats(i))
                    End If
                End If
            Next i

            ' 收集顶层组件名称
            Dim partComps As New List(Of SldWorks.Component2)()
            Dim asmComps As New List(Of SldWorks.Component2)()
            For Each feat In topLevelFeats
                CollectComponent(feat, partComps, asmComps)
            Next

            ' 收集封套/压缩组件名称
            Dim supPartComps As New List(Of SldWorks.Component2)()
            Dim supAsmComps As New List(Of SldWorks.Component2)()
            For Each feat In suppressedEnvFeats
                CollectComponent(feat, supPartComps, supAsmComps)
            Next

            ' === 排序顶层组件，放到最后一个文件夹后面 ===
            UpdateSortProgress("正在排序顶层组件...")
            SortComponentsByName(asmComps)
            SortComponentsByName(partComps)
            SortComponentsByName(supAsmComps)
            SortComponentsByName(supPartComps)

            Dim componentsToMove As New List(Of SldWorks.Component2)(asmComps)
            componentsToMove.AddRange(partComps)
            componentsToMove.AddRange(supAsmComps)
            componentsToMove.AddRange(supPartComps)

            If componentsToMove.Count > 0 Then
                Dim lastFolder As SldWorks.Feature = If(folders.Count > 0, folders(folders.Count - 1), Nothing)
                If lastFolder IsNot Nothing Then
                    assemblyDoc.ReorderComponents(componentsToMove(0), lastFolder, SwConst.swReorderComponentsWhere_e.swReorderComponents_After)
                End If
                For i = 1 To componentsToMove.Count - 1
                    assemblyDoc.ReorderComponents(componentsToMove(i), componentsToMove(i - 1), 1) ' Below
                Next i
            End If

            ' === 排序每个文件夹内的组件 ===
            UpdateSortProgress("正在排序文件夹内组件...")
            For Each f As SldWorks.Feature In folders
                SortComponentsInFolder(f, c, assemblyDoc)
            Next

            ' === 递归处理子装配体 ===
            UpdateSortProgress("正在排序子装配体...")
            Dim processedPaths As New HashSet(Of String)(StringComparer.OrdinalIgnoreCase)
            RecursiveSortSubAssemblies(topLevelFeats, processedPaths)
            For Each fc In folderComponents
                RecursiveSortSubAssemblies(fc, processedPaths)
            Next

            UpdateSortProgress("正在刷新装配体...")
            Part.EditRebuild3()
            Part.ClearSelection2(True)
            ShowAutoCloseNotice("装配体排序完成")
        Catch ex As Exception
            MessageBox.Show("装配体排序失败: " & ex.Message, "错误", MessageBoxButtons.OK, MessageBoxIcon.Error)
        Finally
            CloseSortProgress()
        End Try
    End Sub

    ''' <summary>
    ''' 对文件夹内的组件排序 (装配体在前、零件在后，各自按名称排序)
    ''' </summary>
    Private Sub SortComponentsInFolder(folder As SldWorks.Feature, topAsmName As String, assemblyDoc As SldWorks.AssemblyDoc)
        Dim partComps As New List(Of SldWorks.Component2)()
        Dim asmComps As New List(Of SldWorks.Component2)()
        Dim supPartComps As New List(Of SldWorks.Component2)()
        Dim supAsmComps As New List(Of SldWorks.Component2)()

        Dim subFeat As SldWorks.Feature = folder.GetFirstSubFeature()
        While subFeat IsNot Nothing
            If subFeat.GetTypeName2 = "Reference" Then
                Dim swty As SldWorks.Component2 = TryCast(subFeat.GetSpecificFeature2, SldWorks.Component2)
                If swty IsNot Nothing Then
                    Dim isSupEnv As Boolean = swty.IsSuppressed() OrElse swty.IsEnvelope()
                    Select Case GetComponentDocType(swty)
                        Case SwConst.swDocumentTypes_e.swDocPART
                            If isSupEnv Then supPartComps.Add(swty) Else partComps.Add(swty)
                        Case SwConst.swDocumentTypes_e.swDocASSEMBLY
                            If isSupEnv Then supAsmComps.Add(swty) Else asmComps.Add(swty)
                    End Select
                End If
            End If
            subFeat = subFeat.GetNextSubFeature()
        End While

        If asmComps.Count = 0 AndAlso partComps.Count = 0 AndAlso supAsmComps.Count = 0 AndAlso supPartComps.Count = 0 Then Return

        SortComponentsByName(asmComps)
        SortComponentsByName(partComps)
        SortComponentsByName(supAsmComps)
        SortComponentsByName(supPartComps)

        Dim allComps As New List(Of SldWorks.Component2)(asmComps)
        allComps.AddRange(partComps)
        allComps.AddRange(supAsmComps)
        allComps.AddRange(supPartComps)

        Dim firstRef As SldWorks.Feature = folder.GetFirstSubFeature()
        If firstRef IsNot Nothing Then
            Dim anchorComp As SldWorks.Component2 = TryCast(firstRef.GetSpecificFeature2, SldWorks.Component2)
            If anchorComp IsNot Nothing Then
                assemblyDoc.ReorderComponents(allComps(0), anchorComp, 0) ' Above
                For i = 1 To allComps.Count - 1
                    assemblyDoc.ReorderComponents(allComps(i), allComps(i - 1), 1) ' Below
                Next
            End If
        End If
    End Sub

    ''' <summary>
    ''' 收集 Reference 特征中的零件/装配体名称
    ''' </summary>

    Private Sub CollectComponent(swFeat As Object, partComps As List(Of SldWorks.Component2), asmComps As List(Of SldWorks.Component2))
        Dim comp As SldWorks.Component2 = TryCast(swFeat.GetSpecificFeature2, SldWorks.Component2)
        If comp Is Nothing Then Return

        Select Case GetComponentDocType(comp)
            Case SwConst.swDocumentTypes_e.swDocPART
                partComps.Add(comp)
            Case SwConst.swDocumentTypes_e.swDocASSEMBLY
                asmComps.Add(comp)
        End Select
    End Sub

    Private Function GetComponentDocType(comp As SldWorks.Component2) As Integer
        If comp Is Nothing Then Return 0

        Try
            Dim compPath As String = comp.GetPathName()
            If Not String.IsNullOrEmpty(compPath) Then
                Dim ext As String = IO.Path.GetExtension(compPath)
                If String.Equals(ext, ".SLDPRT", StringComparison.OrdinalIgnoreCase) Then Return SwConst.swDocumentTypes_e.swDocPART
                If String.Equals(ext, ".SLDASM", StringComparison.OrdinalIgnoreCase) Then Return SwConst.swDocumentTypes_e.swDocASSEMBLY
            End If
        Catch
        End Try

        Try
            Dim compModel As SldWorks.ModelDoc2 = comp.GetModelDoc2()
            If compModel IsNot Nothing Then Return compModel.GetType()
        Catch
        End Try

        Return 0
    End Function

    Private Sub SortComponentsByName(comps As List(Of SldWorks.Component2))
        comps.Sort(Function(a, b) String.Compare(a.Name2, b.Name2, StringComparison.OrdinalIgnoreCase))
    End Sub

    Private Sub RecursiveSortSubAssemblies(feats As List(Of SldWorks.Feature), processed As HashSet(Of String))
        For Each feat In feats
            Try
                Dim comp As SldWorks.Component2 = TryCast(feat.GetSpecificFeature2, SldWorks.Component2)
                If comp Is Nothing Then Continue For
                Dim childModel As SldWorks.ModelDoc2 = comp.GetModelDoc2()
                If childModel Is Nothing Then Continue For
                If childModel.GetType() <> swDocumentTypes_e.swDocASSEMBLY Then Continue For
                SortSubAsmByFeatures(childModel, processed)
            Catch
            End Try
        Next
    End Sub

    Private Sub SortSubAsmByFeatures(asmModel As SldWorks.ModelDoc2, processed As HashSet(Of String))
        If asmModel Is Nothing Then Return
        If asmModel.GetType() <> swDocumentTypes_e.swDocASSEMBLY Then Return
        Dim path As String = asmModel.GetPathName()
        If Not String.IsNullOrEmpty(path) Then
            If processed.Contains(path) Then Return
            processed.Add(path)
        End If

        Dim asmName As String = IO.Path.GetFileNameWithoutExtension(path)
        If String.IsNullOrEmpty(asmName) Then asmName = asmModel.GetTitle()
        Try
            Dim vFeats As Object = asmModel.FeatureManager.GetFeatures(True)
            Dim asmDoc As SldWorks.AssemblyDoc = CType(asmModel, SldWorks.AssemblyDoc)
            Dim startIdx As Integer = -1
            Dim endIdx As Integer = UBound(vFeats)
            For i = 0 To UBound(vFeats)
                Dim t As String = vFeats(i).GetTypeName2
                If startIdx < 0 AndAlso t = "OriginProfileFeature" Then startIdx = i + 1
                If t = "MateGroup" Then endIdx = i - 1 : Exit For
            Next
            If startIdx < 0 Then startIdx = 0

            Dim folders As New List(Of SldWorks.Feature)()
            Dim folderComponents As New List(Of List(Of SldWorks.Feature))()
            Dim topLevelFeats As New List(Of SldWorks.Feature)()
            Dim suppressedEnvFeats As New List(Of SldWorks.Feature)()
            Dim currentFolderComps As List(Of SldWorks.Feature) = Nothing

            For i = startIdx To endIdx
                Dim featType As String = vFeats(i).GetTypeName2
                If featType = "FtrFolder" Then
                    If Not vFeats(i).Name.Contains("___EndTag___") Then
                        folders.Add(vFeats(i))
                        currentFolderComps = New List(Of SldWorks.Feature)()
                        folderComponents.Add(currentFolderComps)
                    Else
                        currentFolderComps = Nothing
                    End If
                ElseIf featType = "Reference" Then
                    Dim isSupOrEnv As Boolean = False
                    Try
                        Dim comp As SldWorks.Component2 = TryCast(vFeats(i).GetSpecificFeature2, SldWorks.Component2)
                        If comp IsNot Nothing Then
                            isSupOrEnv = comp.IsSuppressed() OrElse comp.IsEnvelope()
                        End If
                    Catch
                    End Try
                    If isSupOrEnv Then
                        If currentFolderComps IsNot Nothing Then
                            currentFolderComps.Add(vFeats(i))
                        Else
                            suppressedEnvFeats.Add(vFeats(i))
                        End If
                    ElseIf currentFolderComps IsNot Nothing Then
                        currentFolderComps.Add(vFeats(i))
                    Else
                        topLevelFeats.Add(vFeats(i))
                    End If
                End If
            Next

            Dim partComps As New List(Of SldWorks.Component2)()
            Dim asmComps As New List(Of SldWorks.Component2)()
            For Each feat In topLevelFeats
                CollectComponent(feat, partComps, asmComps)
            Next
            Dim supPartComps As New List(Of SldWorks.Component2)()
            Dim supAsmComps As New List(Of SldWorks.Component2)()
            For Each feat In suppressedEnvFeats
                CollectComponent(feat, supPartComps, supAsmComps)
            Next

            SortComponentsByName(asmComps)
            SortComponentsByName(partComps)
            SortComponentsByName(supAsmComps)
            SortComponentsByName(supPartComps)

            Dim componentsToMove As New List(Of SldWorks.Component2)(asmComps)
            componentsToMove.AddRange(partComps)
            componentsToMove.AddRange(supAsmComps)
            componentsToMove.AddRange(supPartComps)

            If componentsToMove.Count > 0 Then
                If folders.Count > 0 Then
                    asmDoc.ReorderComponents(componentsToMove(0), folders(folders.Count - 1), SwConst.swReorderComponentsWhere_e.swReorderComponents_After)
                End If
                For i = 1 To componentsToMove.Count - 1
                    asmDoc.ReorderComponents(componentsToMove(i), componentsToMove(i - 1), 1)
                Next
            End If

            For Each f As SldWorks.Feature In folders
                SortComponentsInFolder(f, asmName, asmDoc)
            Next

            RecursiveSortSubAssemblies(topLevelFeats, processed)
            For Each fc In folderComponents
                RecursiveSortSubAssemblies(fc, processed)
            Next

            asmModel.EditRebuild3()
            asmModel.ClearSelection2(True)
        Catch
        End Try
    End Sub


    ' 强制结束所有 SolidWorks 进程
    Private Sub Button10_Click(sender As Object, e As EventArgs)
        Dim info = TryCast(swProcessCombo.SelectedItem, SwProcessInfo)
        If info Is Nothing Then
            MsgBox("请先在下拉列表中选择 SolidWorks 实例。")
            Exit Sub
        End If

        Try
            Dim targetProcess As Process = Process.GetProcessById(info.ProcessId)
            targetProcess.Kill()
            targetProcess.WaitForExit(3000)
            _selectedSwProcess = Nothing
            _swApp = Nothing
            RefreshProcessList()
            UpdateStatusBar()
            PropertyOverlayWindow.UpdateOpenWindowsSwApp(Nothing)
            RenameWindow.UpdateOpenWindowsSwApp(Nothing)
        Catch ex As Exception
            MsgBox("关闭 SolidWorks 失败：" & ex.Message)
        End Try
    End Sub

    ' 删除自定义属性（递归子件）
    Private Sub Button6_Click_1(sender As Object, e As EventArgs)
        Dim swApp As SldWorks.SldWorks = TryCast(GetSelectedSwApp(), SldWorks.SldWorks)
        If swApp Is Nothing Then
            MsgBox("请先从下拉列表选择一个 SolidWorks 实例并确保它处于活动状态。")
            Exit Sub
        End If
        Dim part As SldWorks.ModelDoc2 = TryCast(swApp.ActiveDoc, SldWorks.ModelDoc2)
        If part Is Nothing Then
            MsgBox("请先在 SolidWorks 中打开一个文档。")
            Exit Sub
        End If
        If part.GetType <> swDocumentTypes_e.swDocASSEMBLY Then
            MsgBox("请在装配体环境下使用此功能。")
            Exit Sub
        End If

        Dim docName As String = IO.Path.GetFileNameWithoutExtension(part.GetPathName())
        If String.IsNullOrEmpty(docName) Then docName = part.GetTitle()
        Dim wasTopMost As Boolean = TopMost
        TopMost = True
        Dim confirmResult As WinForms.DialogResult = WinForms.MessageBox.Show(
            "将删除【" & docName & "】及其所有子件的自定义属性，确定继续？",
            "确认删除自定义属性",
            WinForms.MessageBoxButtons.OKCancel,
            WinForms.MessageBoxIcon.Warning)
        TopMost = wasTopMost
        If confirmResult <> WinForms.DialogResult.OK Then Exit Sub

        Dim topConfString As String = part.GetActiveConfiguration.Name
        SubAsm(swApp, part, topConfString)
        ShowAutoCloseNotice("完成")
    End Sub

    Function SubAsm(swApp As SldWorks.SldWorks, asmDoc As SldWorks.ModelDoc2, confString As String) As Object
        Dim configuration As SldWorks.Configuration
        Dim rootComponent As SldWorks.Component2
        Dim comps As Object
        Dim child As Object
        Dim childModel As SldWorks.ModelDoc2
        Dim fopen As SldWorks.ModelDoc2
        Dim childConfString As String
        Dim childType As Integer
        Dim namearr As Object
        Dim longstatus As Integer, longWarnings As Integer
        Dim vCustInfoNameArr As Object
        Dim vCustInfoName As Object

        ' 处理主装配体自身
        vCustInfoNameArr = asmDoc.GetConfigurationNames
        namearr = asmDoc.GetCustomInfoNames2(confString)
        If namearr IsNot Nothing Then
            For Each vCustInfoName In namearr
                asmDoc.DeleteCustomInfo(vCustInfoName)
            Next
        End If

        configuration = asmDoc.GetConfigurationByName(confString)
        rootComponent = configuration.GetRootComponent
        comps = rootComponent.GetChildren  ''获取目录树

        For Each child In comps
            childModel = child.GetModelDoc
            If Not (childModel Is Nothing) Then
                childConfString = child.ReferencedConfiguration
                childType = childModel.GetType

                If childType = swDocumentTypes_e.swDocPART Then  '处理零件
                    fopen = swApp.OpenDoc6(child.GetPathName, swDocumentTypes_e.swDocPART, swOpenDocOptions_e.swOpenDocOptions_Silent, "", longstatus, longWarnings)

                    If longstatus = 0 Then
                        vCustInfoNameArr = fopen.GetConfigurationNames
                        namearr = fopen.GetCustomInfoNames2(“”)
                        For Each vCustInfoName In namearr
                            fopen.DeleteCustomInfo(vCustInfoName)
                        Next


                        fopen.Save3(0, 0, 0)
                    End If

                End If
                If childType = swDocumentTypes_e.swDocASSEMBLY Then ' 装配体遍历
                    fopen = swApp.OpenDoc6(child.GetPathName, swDocumentTypes_e.swDocASSEMBLY, swOpenDocOptions_e.swOpenDocOptions_Silent, "", longstatus, longWarnings)

                    If longstatus = 0 Then
                        vCustInfoNameArr = fopen.GetConfigurationNames
                        namearr = fopen.GetCustomInfoNames2(“”)


                        For Each vCustInfoName In namearr
                            fopen.DeleteCustomInfo(vCustInfoName)
                        Next

                        fopen.Save3(0, 0, 0)
                    End If

                    SubAsm(swApp, childModel, childConfString)
                End If
            End If
        Next
        Return True
    End Function

    ' 打开绘图标准设置
    Private Sub Button14_Click_1(sender As Object, e As EventArgs)
        Dim settingsWindow As New DrawingSettingsWindow()
        Dim helper As New System.Windows.Interop.WindowInteropHelper(settingsWindow)
        helper.Owner = _mainWindowHandle
        settingsWindow.Show()
    End Sub

    Private Sub swProcessCombo_SelectedIndexChanged(sender As Object, e As WpfControls.SelectionChangedEventArgs)
        Dim info = TryCast(swProcessCombo.SelectedItem, SwProcessInfo)
        If info Is Nothing Then
            _selectedSwProcess = Nothing
            DetachDocEvents()
            _swApp = Nothing
            UpdateStatusBar()
            PropertyOverlayWindow.UpdateOpenWindowsSwApp(Nothing)
            RenameWindow.UpdateOpenWindowsSwApp(Nothing)
            Return
        End If

        Try
            _selectedSwProcess = Process.GetProcessById(info.ProcessId)
        Catch ex As Exception
            _selectedSwProcess = Nothing
        End Try
        My.Settings.DefaultSwProcessId = info.ProcessId
        My.Settings.Save()
        ConnectToSelectedSw()
        Dispatcher.BeginInvoke(New Action(AddressOf SyncOpenSwWindows))

        ' 通过 ROT 查找所选进程的 SW COM 实例
    End Sub

    Private _selectedSwProcess As Process = Nothing
    Private WithEvents _swApp As SldWorks.SldWorks
    Private _attachedPartDoc As SldWorks.PartDoc
    Private _attachedAsmDoc As SldWorks.AssemblyDoc
    Private _attachedDrawDoc As SldWorks.DrawingDoc
    Private _attachedDocPath As String

    <DllImport("user32.dll")>
    Private Shared Function SetForegroundWindow(hWnd As IntPtr) As Boolean
    End Function

    <DllImport("user32.dll")>
    Private Shared Function ShowWindow(hWnd As IntPtr, nCmdShow As Integer) As Boolean
    End Function

    <DllImport("user32.dll")>
    Private Shared Function IsIconic(hWnd As IntPtr) As Boolean
    End Function

    <DllImport("user32.dll", CharSet:=CharSet.Auto, SetLastError:=True)>
    Private Shared Function GetWindowText(hWnd As IntPtr, lpString As System.Text.StringBuilder, nMaxCount As Integer) As Integer
    End Function

    <DllImport("user32.dll", SetLastError:=True)>
    Private Shared Function GetWindowTextLength(hWnd As IntPtr) As Integer
    End Function

    <DllImport("user32.dll", SetLastError:=True)>
    Private Shared Function GetWindowThreadProcessId(hWnd As IntPtr, ByRef lpdwProcessId As Integer) As Integer
    End Function

    Private Const SwRestore As Integer = 9


    Private Sub ConnectToSelectedSw()
        Dim info = TryCast(swProcessCombo.SelectedItem, SwProcessInfo)
        If info Is Nothing Then
            DetachDocEvents()
            _swApp = Nothing
            UpdateStatusBar()
            PropertyOverlayWindow.UpdateOpenWindowsSwApp(Nothing)
            RenameWindow.UpdateOpenWindowsSwApp(Nothing)
            Return
        End If
        Try
            _selectedSwProcess = Process.GetProcessById(info.ProcessId)
        Catch
            _selectedSwProcess = Nothing
        End Try

        DetachDocEvents()
        _swApp = Nothing
        _attachedDocPath = Nothing
        _swApp = TryCast(GetSelectedSwApp(True), SldWorks.SldWorks)
        AttachDocEvents()
        UpdateStatusBar()
        PropertyOverlayWindow.UpdateOpenWindowsSwApp(_swApp)
        RenameWindow.UpdateOpenWindowsSwApp(_swApp)
    End Sub

    Private Sub SyncOpenSwWindows()
        PropertyOverlayWindow.UpdateOpenWindowsSwApp(_swApp)
        RenameWindow.UpdateOpenWindowsSwApp(_swApp)
    End Sub

    Private Function FindSwAppByPid(pid As Integer) As SldWorks.SldWorks
        Dim rot As IRunningObjectTable = Nothing
        Dim enumMoniker As IEnumMoniker = Nothing
        Dim expectedMoniker As String = "solidworks_pid_" & pid.ToString()
        Try
            GetRunningObjectTable(0, rot)
            rot.EnumRunning(enumMoniker)
            Dim monikers(0) As IMoniker
            Dim fetched As IntPtr = IntPtr.Zero

            While enumMoniker.Next(1, monikers, fetched) = 0
                Dim ctx As IBindCtx = Nothing
                CreateBindCtx(0, ctx)
                Dim displayName As String = Nothing
                monikers(0).GetDisplayName(ctx, Nothing, displayName)

                If displayName IsNot Nothing AndAlso displayName.ToLower().Contains(expectedMoniker) Then
                    Dim obj As Object = Nothing
                    Dim returning As Boolean = False
                    Try
                        rot.GetObject(monikers(0), obj)
                        If obj IsNot Nothing Then
                            Dim swApp As SldWorks.SldWorks = CType(obj, SldWorks.SldWorks)
                            Dim swPid As Integer = GetSwProcessId(swApp)

                            If swPid = pid Then
                                returning = True
                                Return swApp
                            End If

                        End If
                    Catch
                    Finally
                        If Not returning AndAlso obj IsNot Nothing AndAlso Marshal.IsComObject(obj) Then
                            Marshal.ReleaseComObject(obj)
                        End If
                    End Try
                End If
                If monikers(0) IsNot Nothing Then Marshal.ReleaseComObject(monikers(0))
            End While
        Catch
        Finally
            If enumMoniker IsNot Nothing Then Marshal.ReleaseComObject(enumMoniker)
            If rot IsNot Nothing Then Marshal.ReleaseComObject(rot)
        End Try
        Return Nothing
    End Function

    Private Function GetSwProcessId(swApp As SldWorks.SldWorks) As Integer
        If swApp Is Nothing Then Return 0

        Try
            Dim pidFromApi As Integer = swApp.GetProcessID()
            If pidFromApi > 0 Then Return pidFromApi
        Catch
        End Try

        Try
            Dim frame As SldWorks.Frame = swApp.Frame()
            If frame IsNot Nothing Then
                Dim hwnd As Integer = frame.GetHWnd()
                If hwnd <> 0 Then
                    Dim pidFromHwnd As Integer = 0
                    GetWindowThreadProcessId(New IntPtr(hwnd), pidFromHwnd)
                    If pidFromHwnd > 0 Then Return pidFromHwnd
                End If
            End If
        Catch
        End Try

        Return 0
    End Function

    Private Function GetSwMainWindowTitle(swApp As SldWorks.SldWorks) As String
        If swApp Is Nothing Then Return ""
        Try
            Dim frame As SldWorks.Frame = swApp.Frame()
            If frame Is Nothing Then Return ""
            Dim hwnd As Integer = frame.GetHWnd()
            If hwnd = 0 Then Return ""
            Dim len As Integer = GetWindowTextLength(New IntPtr(hwnd))
            If len <= 0 Then Return ""
            Dim sb As New System.Text.StringBuilder(len + 1)
            GetWindowText(New IntPtr(hwnd), sb, sb.Capacity)
            Return sb.ToString()
        Catch
            Return ""
        End Try
    End Function

    Private Function NormalizeWindowTitle(value As String) As String
        If String.IsNullOrWhiteSpace(value) Then Return ""
        Dim title As String = value.Trim()
        Dim idx As Integer = title.IndexOf(" - ", StringComparison.Ordinal)
        If idx >= 0 Then title = title.Substring(0, idx).Trim()
        If title.StartsWith("[") AndAlso title.EndsWith("]") AndAlso title.Length > 2 Then
            title = title.Substring(1, title.Length - 2).Trim()
        End If
        Return title
    End Function

    Private Function TryGetActiveSwByPid(pid As Integer) As SldWorks.SldWorks
        Try
            Dim p As Process = Process.GetProcessById(pid)
            If p IsNot Nothing Then
                Dim h As IntPtr = p.MainWindowHandle
                If h <> IntPtr.Zero Then
                    If IsIconic(h) Then ShowWindow(h, SwRestore)
                    SetForegroundWindow(h)
                End If
            End If
        Catch
        End Try

        Try
            Dim app As SldWorks.SldWorks = CType(Marshal.GetActiveObject("SldWorks.Application"), SldWorks.SldWorks)
            If app IsNot Nothing Then
                Dim resolvedPid As Integer = GetSwProcessId(app)
                If resolvedPid = pid Then
                    Return app
                End If
            End If
        Catch
        End Try

        Return Nothing
    End Function

    <DllImport("ole32.dll")>
    Private Shared Function GetRunningObjectTable(reserved As UInteger, ByRef prot As IRunningObjectTable) As Integer
    End Function

    <DllImport("ole32.dll")>
    Private Shared Function CreateBindCtx(reserved As UInteger, ByRef ppbc As IBindCtx) As Integer
    End Function

    Private Function _swApp_ActiveDocChangeNotify() As Integer Handles _swApp.ActiveDocChangeNotify
        UpdateStatusBar()
        AttachDocEvents()
        Return 0
    End Function

    Private Function _swApp_ActiveModelDocChangeNotify() As Integer Handles _swApp.ActiveModelDocChangeNotify
        UpdateStatusBar()
        AttachDocEvents()
        Return 0
    End Function

    Private Function _swApp_FileCloseNotify(fileName As String, reason As Integer) As Integer Handles _swApp.FileCloseNotify
        Try
            If _swApp IsNot Nothing Then
                Dim doc As SldWorks.ModelDoc2 = TryCast(_swApp.ActiveDoc, SldWorks.ModelDoc2)
                If doc Is Nothing Then
                    fileNameLabel.Text = "无文档"
                ElseIf String.Equals(doc.GetPathName(), fileName, StringComparison.OrdinalIgnoreCase) Then
                    fileNameLabel.Text = "无文档"
                Else
                    UpdateStatusBar()
                End If
            Else
                fileNameLabel.Text = "未连接"
            End If
        Catch
            UpdateStatusBar()
        End Try
        Return 0
    End Function

    Private Sub DetachDocEvents()
        If _attachedPartDoc IsNot Nothing Then
            RemoveHandler _attachedPartDoc.NewSelectionNotify, AddressOf Doc_SelectionChange
            _attachedPartDoc = Nothing
        End If
        If _attachedAsmDoc IsNot Nothing Then
            RemoveHandler _attachedAsmDoc.NewSelectionNotify, AddressOf Doc_SelectionChange
            _attachedAsmDoc = Nothing
        End If
        If _attachedDrawDoc IsNot Nothing Then
            RemoveHandler _attachedDrawDoc.NewSelectionNotify, AddressOf Doc_SelectionChange
            _attachedDrawDoc = Nothing
        End If
        _attachedDocPath = Nothing
    End Sub

    Private Sub AttachDocEvents()
        Try
            If _swApp Is Nothing Then Return
            Dim modelDoc As SldWorks.ModelDoc2 = TryCast(_swApp.ActiveDoc, SldWorks.ModelDoc2)
            If modelDoc Is Nothing Then
                DetachDocEvents()
                Return
            End If
            Dim docPath As String = modelDoc.GetPathName()
            If String.Equals(docPath, _attachedDocPath, StringComparison.OrdinalIgnoreCase) Then Return
            DetachDocEvents()
            _attachedDocPath = docPath
            Dim docType As Integer = modelDoc.GetType()
            If docType = CInt(swDocumentTypes_e.swDocPART) Then
                _attachedPartDoc = CType(modelDoc, SldWorks.PartDoc)
                AddHandler _attachedPartDoc.NewSelectionNotify, AddressOf Doc_SelectionChange
            ElseIf docType = CInt(swDocumentTypes_e.swDocASSEMBLY) Then
                _attachedAsmDoc = CType(modelDoc, SldWorks.AssemblyDoc)
                AddHandler _attachedAsmDoc.NewSelectionNotify, AddressOf Doc_SelectionChange
            ElseIf docType = CInt(swDocumentTypes_e.swDocDRAWING) Then
                _attachedDrawDoc = CType(modelDoc, SldWorks.DrawingDoc)
                AddHandler _attachedDrawDoc.NewSelectionNotify, AddressOf Doc_SelectionChange
            End If
        Catch
        End Try
    End Sub

    Private Function Doc_SelectionChange() As Integer
        UpdateStatusBar()
        Return 0
    End Function

    ' 尝试根据选定的进程返回对应的 SolidWorks COM 对象
    Public Function GetSelectedSwApp(Optional bringToFront As Boolean = False) As Object
        Try
            Dim targetPid As Integer = If(_selectedSwProcess IsNot Nothing, _selectedSwProcess.Id, 0)

            If bringToFront AndAlso _selectedSwProcess IsNot Nothing Then
                Dim h = _selectedSwProcess.MainWindowHandle
                If h <> IntPtr.Zero Then
                    If IsIconic(h) Then ShowWindow(h, SwRestore)
                    SetForegroundWindow(h)
                End If
            End If

            If targetPid > 0 Then
                Dim strictApp As SldWorks.SldWorks = FindSwAppByPid(targetPid)
                If strictApp IsNot Nothing Then Return strictApp

                Dim activeApp As SldWorks.SldWorks = TryGetActiveSwByPid(targetPid)
                If activeApp IsNot Nothing Then Return activeApp
            End If

            Return Nothing
        Catch ex As Exception
            Return Nothing
        End Try
    End Function

    Private Sub PopulateSolidWorksProcesses()
        swProcessCombo.Items.Clear()

        Try
            Dim procs = Process.GetProcessesByName("sldworks")
            For Each p In procs
                Dim h As IntPtr = p.MainWindowHandle

                ' 首先尝试常规属性
                Dim title As String = p.MainWindowTitle
                Dim rawWindowTitle As String = title

                ' 如果为空，尝试使用 Win32 API 直接读取窗口文本
                If String.IsNullOrWhiteSpace(title) AndAlso h <> IntPtr.Zero Then
                    Try
                        Dim len As Integer = GetWindowTextLength(h)
                        If len > 0 Then
                            Dim sb As New System.Text.StringBuilder(len + 1)
                            GetWindowText(h, sb, sb.Capacity)
                            title = sb.ToString()
                            rawWindowTitle = title
                        End If
                    Catch
                        ' 忽略
                    End Try
                End If

                ' 如果仍然为空，尝试将该实例置前并通过 COM 获取活动文档名作为标题
                If String.IsNullOrWhiteSpace(title) AndAlso h <> IntPtr.Zero Then
                    Try
                        ' no-op
                    Catch
                        ' 忽略
                    End Try
                End If

                If String.IsNullOrWhiteSpace(title) Then
                    title = "SolidWorks (PID " & p.Id & ")"
                Else
                    ' 如果标题包含 " - "，取其后部分（通常为文档名或文档信息）
                    Dim sep As String = " - "
                    Dim idx As Integer = title.IndexOf(sep, StringComparison.Ordinal)
                    If idx >= 0 Then
                        title = title.Substring(0, idx).Trim()
                    End If

                    ' 去除可能的方括号包围（例如："[name.SLDASM]")
                    If title.StartsWith("[") AndAlso title.EndsWith("]") AndAlso title.Length > 2 Then
                        title = title.Substring(1, title.Length - 2).Trim()
                    End If
                End If

                swProcessCombo.Items.Add(New SwProcessInfo With {.Title = title, .ProcessId = p.Id, .WindowTitle = rawWindowTitle})
            Next

        Catch ex As Exception
            ' 忽略异常或根据需要记录
        End Try
    End Sub

    Private Class SwProcessInfo
        Public Property Title As String
        Public Property ProcessId As Integer
        Public Property WindowTitle As String
        Public Overrides Function ToString() As String
            Dim displayTitle As String = "未打开文档"
            Dim sourceTitle As String = If(String.IsNullOrWhiteSpace(WindowTitle), "", WindowTitle)
            Dim leftBracket As Integer = sourceTitle.IndexOf("["c)
            Dim rightBracket As Integer = sourceTitle.IndexOf("]"c)
            If leftBracket >= 0 AndAlso rightBracket > leftBracket + 1 Then
                displayTitle = sourceTitle.Substring(leftBracket + 1, rightBracket - leftBracket - 1).Trim()
            End If
            Return ProcessId.ToString() & ":" & displayTitle
        End Function
    End Class

    ' 全局热键处理
    Private Function WndProc(hwnd As IntPtr, msg As Integer, wParam As IntPtr, lParam As IntPtr, ByRef handled As Boolean) As IntPtr
        If msg = WM_HOTKEY AndAlso wParam.ToInt32() = HOTKEY_ID Then
            ToggleVisibility()
            handled = True
        End If
        Return IntPtr.Zero
    End Function

    Private Sub ToggleVisibility()
        If Me.IsVisible Then
            Me.Hide()
        Else
            Me.Show()
            Me.WindowState = Wpf.WindowState.Normal
            Me.Activate()
        End If
    End Sub

    ' 托盘菜单 — 显示主窗口
    Private Sub TrayShow_Click(sender As Object, e As EventArgs)
        Me.Show()
        Me.WindowState = Wpf.WindowState.Normal
        Me.Activate()
    End Sub

    ' 托盘菜单 — 退出
    Private Sub TrayExit_Click(sender As Object, e As EventArgs)
        _allowClose = True
        Wpf.Application.Current.Shutdown()
    End Sub

    Private Sub refreshBtn_Click(sender As Object, e As EventArgs)
        RefreshProcessList()
        ConnectToSelectedSw()
    End Sub



    ' 打开重命名工具
    Private Sub Button16_Click(sender As Object, e As EventArgs)
        Dim swApp As SldWorks.SldWorks = TryCast(GetSelectedSwApp(), SldWorks.SldWorks)
        If swApp Is Nothing Then
            MsgBox("请先从下拉列表选择一个 SolidWorks 实例并确保它处于活动状态。")
            Exit Sub
        End If
        Dim renameWindow As New RenameWindow()
        renameWindow.SwApp = swApp
        renameWindow.Show()
    End Sub

    ' 删除配置属性（递归子件）
    Private Sub Button17_Click(sender As Object, e As EventArgs)
        Dim swApp As SldWorks.SldWorks = TryCast(GetSelectedSwApp(), SldWorks.SldWorks)
        If swApp Is Nothing Then
            MsgBox("请先从下拉列表选择一个 SolidWorks 实例并确保它处于活动状态。")
            Exit Sub
        End If
        Dim part As SldWorks.ModelDoc2 = TryCast(swApp.ActiveDoc, SldWorks.ModelDoc2)
        If part Is Nothing Then
            MsgBox("请先在 SolidWorks 中打开一个文档。")
            Exit Sub
        End If
        If part.GetType <> swDocumentTypes_e.swDocASSEMBLY Then
            MsgBox("请在装配体环境下使用此功能。")
            Exit Sub
        End If

        Dim docName As String = IO.Path.GetFileNameWithoutExtension(part.GetPathName())
        If String.IsNullOrEmpty(docName) Then docName = part.GetTitle()
        Dim wasTopMost As Boolean = Me.TopMost
        Me.TopMost = True
        Dim confirmResult As WinForms.DialogResult = WinForms.MessageBox.Show(
            "将删除【" & docName & "】及其所有子件的配置属性，确定继续？",
            "确认删除配置属性",
            WinForms.MessageBoxButtons.OKCancel,
            WinForms.MessageBoxIcon.Warning)
        Me.TopMost = wasTopMost
        If confirmResult <> WinForms.DialogResult.OK Then Exit Sub

        Dim topConfString As String = part.GetActiveConfiguration.Name
        DelConfProps(swApp, part, topConfString)
        ShowAutoCloseNotice("完成")
    End Sub

    Function DelConfProps(swApp As SldWorks.SldWorks, asmDoc As SldWorks.ModelDoc2, confString As String) As Object
        ' 先处理当前装配体自身
        Dim namearr As Object = asmDoc.GetCustomInfoNames2(confString)
        If namearr IsNot Nothing Then
            For Each n In namearr
                asmDoc.DeleteCustomInfo2(confString, n)
            Next
        End If

        Dim configuration As SldWorks.Configuration = asmDoc.GetConfigurationByName(confString)
        Dim rootComponent As SldWorks.Component2 = configuration.GetRootComponent
        Dim comps As Object = rootComponent.GetChildren

        Dim child As Object, childModel As SldWorks.ModelDoc2, fopen As SldWorks.ModelDoc2
        Dim childConfString As String, childType As Integer
        Dim longstatus As Integer, longWarnings As Integer

        For Each child In comps
            childModel = child.GetModelDoc
            If Not (childModel Is Nothing) Then
                childConfString = child.ReferencedConfiguration
                childType = childModel.GetType

                If childType = swDocumentTypes_e.swDocPART Then
                    fopen = swApp.OpenDoc6(child.GetPathName, swDocumentTypes_e.swDocPART, swOpenDocOptions_e.swOpenDocOptions_Silent, "", longstatus, longWarnings)
                    If longstatus = 0 Then
                        namearr = fopen.GetCustomInfoNames2(childConfString)
                        If namearr IsNot Nothing Then
                            For Each n In namearr
                                fopen.DeleteCustomInfo2(childConfString, n)
                            Next
                        End If
                        fopen.Save3(0, 0, 0)
                    End If
                End If
                If childType = swDocumentTypes_e.swDocASSEMBLY Then
                    fopen = swApp.OpenDoc6(child.GetPathName, swDocumentTypes_e.swDocASSEMBLY, swOpenDocOptions_e.swOpenDocOptions_Silent, "", longstatus, longWarnings)
                    If longstatus = 0 Then
                        namearr = fopen.GetCustomInfoNames2(childConfString)
                        If namearr IsNot Nothing Then
                            For Each n In namearr
                                fopen.DeleteCustomInfo2(childConfString, n)
                            Next
                        End If
                        fopen.Save3(0, 0, 0)
                    End If
                    DelConfProps(swApp, childModel, childConfString)
                End If
            End If
        Next
        Return True
    End Function

    ' 打开编码整理工具
    Private Sub Button18_Click(sender As Object, e As EventArgs)
        Dim swApp As SldWorks.SldWorks = TryCast(GetSelectedSwApp(), SldWorks.SldWorks)
        If swApp Is Nothing Then
            MsgBox("请先从下拉列表选择一个 SolidWorks 实例并确保它处于活动状态。")
            Exit Sub
        End If
        Dim cleanupWindow As New CodingCleanupWindow()
        cleanupWindow.SwApp = swApp
        Dim helper As New System.Windows.Interop.WindowInteropHelper(cleanupWindow)
        helper.Owner = _mainWindowHandle
        cleanupWindow.Show()
    End Sub

    ' 打开配置属性透明窗口
    Private Sub Button19_Click(sender As Object, e As EventArgs)
        Dim swApp As SldWorks.SldWorks = TryCast(GetSelectedSwApp(), SldWorks.SldWorks)
        If swApp Is Nothing Then
            MsgBox("请先从下拉列表选择一个 SolidWorks 实例并确保它处于活动状态。")
            Exit Sub
        End If
        PropertyOverlayWindow.ShowOrActivate(swApp)
    End Sub
    Private Sub Button20_Click(sender As Object, e As EventArgs)
        Dim settingsWindow As New PropertyOverlaySettingsWindow()
        Dim helper As New System.Windows.Interop.WindowInteropHelper(settingsWindow)
        helper.Owner = _mainWindowHandle
        settingsWindow.ShowDialog()
    End Sub

    Private Sub ShowAutoCloseNotice(message As String, Optional title As String = "提示")
        Try
            If _trayIcon IsNot Nothing Then
                _trayIcon.BalloonTipTitle = title
                _trayIcon.BalloonTipText = message
                _trayIcon.BalloonTipIcon = WinForms.ToolTipIcon.Info
                _trayIcon.ShowBalloonTip(1800)
                Return
            End If
        Catch
        End Try

        Wpf.MessageBox.Show(Me, message, title, Wpf.MessageBoxButton.OK, Wpf.MessageBoxImage.Information)
    End Sub

    Private Sub ShowSortProgress(message As String)
        CloseSortProgress()

        _sortProgressLabel = New WinForms.Label() With {
            .AutoSize = False,
            .Dock = WinForms.DockStyle.Fill,
            .Font = New Drawing.Font("微软雅黑", 10.0!, Drawing.FontStyle.Bold),
            .ForeColor = Drawing.Color.FromArgb(45, 55, 72),
            .TextAlign = Drawing.ContentAlignment.MiddleCenter,
            .Text = message
        }

        _sortProgressForm = New WinForms.Form() With {
            .AutoScaleMode = AutoScaleMode.None,
            .BackColor = Color.White,
            .ClientSize = New Size(280, 76),
            .ControlBox = False,
            .FormBorderStyle = FormBorderStyle.FixedSingle,
            .MaximizeBox = False,
            .MinimizeBox = False,
            .ShowIcon = False,
            .ShowInTaskbar = False,
            .StartPosition = FormStartPosition.Manual,
            .Text = "排序中",
            .TopMost = True
        }

        Dim x As Integer = Me.Left + Math.Max(0, (Me.Width - _sortProgressForm.Width) \ 2)
        Dim y As Integer = Me.Top + Math.Max(0, (Me.Height - _sortProgressForm.Height) \ 2)
        _sortProgressForm.Location = New Point(x, y)
        _sortProgressForm.Controls.Add(_sortProgressLabel)
        _sortProgressForm.Show()
        _sortProgressForm.Refresh()
        Application.DoEvents()
    End Sub

    Private Sub UpdateSortProgress(message As String)
        If _sortProgressForm Is Nothing OrElse _sortProgressForm.IsDisposed Then Return
        If _sortProgressLabel Is Nothing OrElse _sortProgressLabel.IsDisposed Then Return

        _sortProgressLabel.Text = message
        _sortProgressLabel.Refresh()
        _sortProgressForm.Refresh()
        Application.DoEvents()
    End Sub

    Private Sub CloseSortProgress()
        Try
            If _sortProgressForm IsNot Nothing Then
                If Not _sortProgressForm.IsDisposed Then _sortProgressForm.Close()
                _sortProgressForm.Dispose()
            End If
        Catch
        Finally
            _sortProgressForm = Nothing
            _sortProgressLabel = Nothing
        End Try
    End Sub

    Private Sub MarkDocDirty(doc As SldWorks.ModelDoc2)
        If doc Is Nothing Then Return
        Try
            doc.SetSaveFlag()
        Catch
        End Try
    End Sub
End Class
