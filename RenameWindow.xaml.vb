Imports System.IO
Imports System.Diagnostics
Imports System.Windows.Forms
Imports System.Windows.Threading
Imports SwConst
Imports Wpf = System.Windows
Imports WpfControls = System.Windows.Controls
Imports WpfMedia = System.Windows.Media

Partial Public Class RenameWindow
    Inherits Wpf.Window

    Private Shared ReadOnly OpenWindows As New List(Of RenameWindow)()
    Private WithEvents SwAppField As SldWorks.SldWorks
    Private AttachedDocPath As String
    Private AttachedPartDoc As SldWorks.PartDoc
    Private AttachedAsmDoc As SldWorks.AssemblyDoc
    Private AttachedDrawDoc As SldWorks.DrawingDoc
    Private SuppressNameCheck As Boolean
    Private ReadOnly NameCheckTimer As DispatcherTimer
    Private ReadOnly DocWatchTimer As DispatcherTimer
    Private LastActiveDocKey As String
    Private LastTargetDocKey As String

    Public Sub New()
        InitializeComponent()

        NameCheckTimer = New DispatcherTimer() With {.Interval = TimeSpan.FromMilliseconds(500)}
        AddHandler NameCheckTimer.Tick, AddressOf NameCheckTimer_Tick

        DocWatchTimer = New DispatcherTimer() With {.Interval = TimeSpan.FromMilliseconds(400)}
        AddHandler DocWatchTimer.Tick, AddressOf DocWatchTimer_Tick
        DocWatchTimer.Start()

        AddHandler Closed, AddressOf RenameWindow_Closed
        OpenWindows.Add(Me)
    End Sub

    Public Property SwApp As SldWorks.SldWorks
        Get
            Return SwAppField
        End Get
        Set(value As SldWorks.SldWorks)
            If Not Object.ReferenceEquals(SwAppField, value) Then
                DetachDocEvents()
                LastActiveDocKey = Nothing
                LastTargetDocKey = Nothing
            End If
            SwAppField = value
            SuppressNameCheck = True
            UpdateSelectionInfo()
            SuppressNameCheck = False
            AttachDocEvents()
        End Set
    End Property

    Public Shared Sub UpdateOpenWindowsSwApp(swApp As SldWorks.SldWorks)
        For Each window In OpenWindows.ToArray()
            If window IsNot Nothing Then window.SwApp = swApp
        Next
    End Sub

    Private Sub RenameWindow_Closed(sender As Object, e As EventArgs)
        NameCheckTimer.Stop()
        DocWatchTimer.Stop()
        DetachDocEvents()
        SwAppField = Nothing
        OpenWindows.Remove(Me)
    End Sub

    Private Function SwAppField_ActiveDocChangeNotify() As Integer Handles SwAppField.ActiveDocChangeNotify
        Dispatcher.BeginInvoke(New Action(AddressOf UpdateSelectionInfo))
        Dispatcher.BeginInvoke(New Action(AddressOf AttachDocEvents))
        Return 0
    End Function

    Private Function SwAppField_ActiveModelDocChangeNotify() As Integer Handles SwAppField.ActiveModelDocChangeNotify
        Dispatcher.BeginInvoke(New Action(AddressOf UpdateSelectionInfo))
        Dispatcher.BeginInvoke(New Action(AddressOf AttachDocEvents))
        Return 0
    End Function

    Private Sub DetachDocEvents()
        If AttachedPartDoc IsNot Nothing Then
            RemoveHandler AttachedPartDoc.NewSelectionNotify, AddressOf Doc_SelectionChange
            AttachedPartDoc = Nothing
        End If
        If AttachedAsmDoc IsNot Nothing Then
            RemoveHandler AttachedAsmDoc.NewSelectionNotify, AddressOf Doc_SelectionChange
            AttachedAsmDoc = Nothing
        End If
        If AttachedDrawDoc IsNot Nothing Then
            RemoveHandler AttachedDrawDoc.NewSelectionNotify, AddressOf Doc_SelectionChange
            AttachedDrawDoc = Nothing
        End If
        AttachedDocPath = Nothing
    End Sub

    Private Sub AttachDocEvents()
        Try
            If SwAppField Is Nothing Then Return
            Dim modelDoc As SldWorks.ModelDoc2 = TryCast(SwAppField.ActiveDoc, SldWorks.ModelDoc2)
            If modelDoc Is Nothing Then
                DetachDocEvents()
                Return
            End If

            Dim docPath As String = modelDoc.GetPathName()
            Dim docKey As String = If(String.IsNullOrEmpty(docPath), modelDoc.GetTitle(), docPath)
            If String.Equals(docKey, AttachedDocPath, StringComparison.OrdinalIgnoreCase) Then Return

            DetachDocEvents()
            AttachedDocPath = docKey

            Dim docType As Integer = modelDoc.GetType()
            If docType = CInt(swDocumentTypes_e.swDocPART) Then
                AttachedPartDoc = CType(modelDoc, SldWorks.PartDoc)
                AddHandler AttachedPartDoc.NewSelectionNotify, AddressOf Doc_SelectionChange
            ElseIf docType = CInt(swDocumentTypes_e.swDocASSEMBLY) Then
                AttachedAsmDoc = CType(modelDoc, SldWorks.AssemblyDoc)
                AddHandler AttachedAsmDoc.NewSelectionNotify, AddressOf Doc_SelectionChange
            ElseIf docType = CInt(swDocumentTypes_e.swDocDRAWING) Then
                AttachedDrawDoc = CType(modelDoc, SldWorks.DrawingDoc)
                AddHandler AttachedDrawDoc.NewSelectionNotify, AddressOf Doc_SelectionChange
            End If
        Catch ex As Exception
            Debug.WriteLine("RenameWindow.AttachDocEvents failed: " & ex.Message)
        End Try
    End Sub

    Private Function Doc_SelectionChange() As Integer
        Dispatcher.BeginInvoke(New Action(
            Sub()
                LastTargetDocKey = Nothing
                UpdateSelectionInfo()
            End Sub))
        Return 0
    End Function

    Private Sub DocWatchTimer_Tick(sender As Object, e As EventArgs)
        Try
            If SwAppField Is Nothing Then Return
            Dim modelDoc As SldWorks.ModelDoc2 = TryCast(SwAppField.ActiveDoc, SldWorks.ModelDoc2)
            Dim key As String = ""
            If modelDoc IsNot Nothing Then
                Dim p As String = modelDoc.GetPathName()
                key = If(String.IsNullOrEmpty(p), modelDoc.GetTitle(), p)
            End If
            Dim targetKey As String = GetCurrentTargetDocumentKey(modelDoc)
            If Not String.Equals(LastActiveDocKey, key, StringComparison.OrdinalIgnoreCase) OrElse
                Not String.Equals(LastTargetDocKey, targetKey, StringComparison.OrdinalIgnoreCase) Then
                LastActiveDocKey = key
                LastTargetDocKey = targetKey
                UpdateSelectionInfo()
                AttachDocEvents()
            End If
        Catch ex As Exception
            Debug.WriteLine("RenameWindow.DocWatchTimer_Tick failed: " & ex.Message)
        End Try
    End Sub

    Private Sub UpdateSelectionInfo()
        SuppressNameCheck = True
        Try
            If SwAppField Is Nothing Then
                ClearFileInfo()
                Return
            End If

            Dim modelDoc As SldWorks.ModelDoc2 = TryCast(SwAppField.ActiveDoc, SldWorks.ModelDoc2)
            If modelDoc Is Nothing Then
                ClearFileInfo()
                Return
            End If

            Dim targetInfo As SelectedDocumentInfo = GetSelectedDocumentInfo(modelDoc)
            If targetInfo Is Nothing OrElse targetInfo.Document Is Nothing Then
                ClearFileInfo()
                Return
            End If

            Dim targetDoc As SldWorks.ModelDoc2 = targetInfo.Document
            Dim filePath As String = targetInfo.Path
            Dim baseName As String = If(String.IsNullOrEmpty(filePath), targetDoc.GetTitle(), Path.GetFileNameWithoutExtension(filePath))
            OldNameBox.Text = baseName
            NewNameBox.Text = baseName

            Dim ext As String = If(String.IsNullOrEmpty(filePath), DocTypeToExtension(targetDoc.GetType()), Path.GetExtension(filePath).ToLowerInvariant())
            OldExtText.Text = ext
            NewExtText.Text = ext
            UpdateDrawingExistsIndicator(filePath)

            Dim activePath As String = modelDoc.GetPathName()
            LastActiveDocKey = If(String.IsNullOrEmpty(activePath), modelDoc.GetTitle(), activePath)
            LastTargetDocKey = GetDocumentInfoKey(targetInfo)
        Catch ex As Exception
            Debug.WriteLine("RenameWindow.UpdateSelectionInfo failed: " & ex.Message)
        Finally
            SuppressNameCheck = False
            NameStatusBorder.Visibility = Wpf.Visibility.Collapsed
        End Try
    End Sub

    Private Sub ClearFileInfo()
        OldNameBox.Text = ""
        NewNameBox.Text = ""
        OldExtText.Text = ""
        NewExtText.Text = ""
        DrawingStatusBorder.Visibility = Wpf.Visibility.Collapsed
    End Sub

    Private Function DocTypeToExtension(docType As Integer) As String
        Select Case docType
            Case CInt(swDocumentTypes_e.swDocPART) : Return ".sldprt"
            Case CInt(swDocumentTypes_e.swDocASSEMBLY) : Return ".sldasm"
            Case CInt(swDocumentTypes_e.swDocDRAWING) : Return ".slddrw"
            Case Else : Return ""
        End Select
    End Function

    Private Sub UpdateDrawingExistsIndicator(modelPath As String)
        If String.IsNullOrEmpty(modelPath) Then
            DrawingStatusBorder.Visibility = Wpf.Visibility.Collapsed
            Return
        End If

        Try
            Dim dir As String = Path.GetDirectoryName(modelPath)
            Dim baseName As String = Path.GetFileNameWithoutExtension(modelPath)
            If String.IsNullOrEmpty(dir) OrElse String.IsNullOrEmpty(baseName) Then
                DrawingStatusBorder.Visibility = Wpf.Visibility.Collapsed
                Return
            End If

            Dim drawingPath As String = Path.Combine(dir, baseName & ".SLDDRW")
            DrawingStatusText.Text = If(File.Exists(drawingPath), "存在工程图", "无工程图")
            DrawingStatusBorder.Background = If(File.Exists(drawingPath),
                New WpfMedia.SolidColorBrush(WpfMedia.Color.FromRgb(50, 205, 50)),
                New WpfMedia.SolidColorBrush(WpfMedia.Color.FromRgb(220, 38, 38)))
            DrawingStatusBorder.Visibility = Wpf.Visibility.Visible
        Catch ex As Exception
            Debug.WriteLine("RenameWindow.UpdateDrawingExistsIndicator failed: " & ex.Message)
            DrawingStatusBorder.Visibility = Wpf.Visibility.Collapsed
        End Try
    End Sub

    Private Sub NewNameBox_TextChanged(sender As Object, e As WpfControls.TextChangedEventArgs)
        If SuppressNameCheck Then Return
        NameCheckTimer.Stop()
        NameCheckTimer.Start()
    End Sub

    Private Sub NameCheckTimer_Tick(sender As Object, e As EventArgs)
        NameCheckTimer.Stop()
        CheckNewNameConflict()
    End Sub

    Private Sub CheckNewNameConflict()
        Dim newName As String = NewNameBox.Text.Trim()
        If String.IsNullOrEmpty(newName) Then
            ResetNewNameIndicators()
            NameStatusBorder.Visibility = Wpf.Visibility.Collapsed
            Return
        End If

        Try
            If SwAppField Is Nothing Then Return
            Dim modelDoc As SldWorks.ModelDoc2 = TryCast(SwAppField.ActiveDoc, SldWorks.ModelDoc2)
            If modelDoc Is Nothing Then Return
            Dim docToCheck As SldWorks.ModelDoc2 = GetDocumentToSave(modelDoc, False)
            If docToCheck Is Nothing Then Return

            Dim docPath As String = docToCheck.GetPathName()
            If String.IsNullOrEmpty(docPath) Then Return

            Dim dir As String = Path.GetDirectoryName(docPath)
            Dim ext As String = Path.GetExtension(docPath)
            Dim newFilePath As String = Path.Combine(dir, newName & ext)
            Dim fileExists As Boolean = File.Exists(newFilePath)
            Dim openedDoc As SldWorks.ModelDoc2 = TryCast(SwAppField.GetOpenDocumentByName(newFilePath), SldWorks.ModelDoc2)
            Dim existsInOpenDocs As Boolean = openedDoc IsNot Nothing

            If fileExists OrElse existsInOpenDocs Then
                NewNameBox.Background = New WpfMedia.SolidColorBrush(WpfMedia.Color.FromRgb(255, 228, 225))
                NameStatusText.Foreground = WpfMedia.Brushes.Red
                NameStatusText.Text = If(existsInOpenDocs AndAlso fileExists, "重名(打开+本地)", If(existsInOpenDocs, "重名(已打开)", "重名(本地)"))
                NameStatusBorder.Visibility = Wpf.Visibility.Visible
            Else
                NewNameBox.Background = New WpfMedia.SolidColorBrush(WpfMedia.Color.FromRgb(144, 238, 144))
                NameStatusText.Text = "可保存"
                NameStatusText.Foreground = WpfMedia.Brushes.Green
                NameStatusBorder.Visibility = Wpf.Visibility.Visible
                UpdateNewNameDrawingIndicator(dir, newName)
            End If
        Catch ex As Exception
            Debug.WriteLine("RenameWindow.CheckNewNameConflict failed: " & ex.Message)
        End Try
    End Sub

    Private Sub ResetNewNameIndicators()
        NewNameBox.ClearValue(WpfControls.Control.BackgroundProperty)
        NameStatusText.Foreground = WpfMedia.Brushes.Green
    End Sub

    Private Sub UpdateNewNameDrawingIndicator(dir As String, newName As String)
        If String.IsNullOrEmpty(dir) OrElse String.IsNullOrEmpty(newName) Then Return
        Try
            Dim drawingPath As String = Path.Combine(dir, newName & ".SLDDRW")
            If File.Exists(drawingPath) Then NameStatusText.Text &= "，有工程图"
        Catch ex As Exception
            Debug.WriteLine("RenameWindow.UpdateNewNameDrawingIndicator failed: " & ex.Message)
        End Try
    End Sub

    Private Function GetDocumentToSave(modelDoc As SldWorks.ModelDoc2, Optional allowSelectedComponent As Boolean = True) As SldWorks.ModelDoc2
        If modelDoc Is Nothing Then Return Nothing

        Dim docType As Integer = modelDoc.GetType()
        If docType = CInt(swDocumentTypes_e.swDocPART) OrElse docType = CInt(swDocumentTypes_e.swDocDRAWING) Then Return modelDoc
        If Not allowSelectedComponent Then Return modelDoc

        Dim selMgr As SldWorks.SelectionMgr = modelDoc.SelectionManager
        Dim selCount As Integer = 0
        Try
            selCount = selMgr.GetSelectedObjectCount2(-1)
        Catch ex As Exception
            Debug.WriteLine("RenameWindow.GetDocumentToSave failed: " & ex.Message)
            Return modelDoc
        End Try

        If selCount < 1 Then Return modelDoc
        Dim selObj As Object = selMgr.GetSelectedObject6(1, -1)
        If TypeOf selObj Is SldWorks.Component2 Then
            Dim comp As SldWorks.Component2 = CType(selObj, SldWorks.Component2)
            Dim refModel As SldWorks.ModelDoc2 = comp.GetModelDoc2()
            If refModel IsNot Nothing Then Return refModel
        End If
        Return modelDoc
    End Function

    Private Function GetSelectedDocumentInfo(modelDoc As SldWorks.ModelDoc2) As SelectedDocumentInfo
        If modelDoc Is Nothing Then Return Nothing

        Dim info As New SelectedDocumentInfo With {
            .Document = modelDoc,
            .Path = modelDoc.GetPathName()
        }

        Dim docType As Integer = modelDoc.GetType()
        If docType = CInt(swDocumentTypes_e.swDocPART) OrElse docType = CInt(swDocumentTypes_e.swDocDRAWING) Then Return info

        Try
            Dim selMgr As SldWorks.SelectionMgr = modelDoc.SelectionManager
            If selMgr Is Nothing OrElse selMgr.GetSelectedObjectCount2(-1) < 1 Then Return info

            Dim selObj As Object = selMgr.GetSelectedObject6(1, -1)
            If TypeOf selObj Is SldWorks.Component2 Then
                Dim comp As SldWorks.Component2 = CType(selObj, SldWorks.Component2)
                Dim compModel As SldWorks.ModelDoc2 = comp.GetModelDoc2()
                If compModel IsNot Nothing Then
                    info.Document = compModel
                    info.Path = compModel.GetPathName()
                Else
                    Dim compPath As String = comp.GetPathName()
                    If Not String.IsNullOrEmpty(compPath) Then info.Path = compPath
                End If
            End If
        Catch ex As Exception
            Debug.WriteLine("RenameWindow.GetSelectedDocumentInfo failed: " & ex.Message)
        End Try

        Return info
    End Function

    Private Function GetCurrentTargetDocumentKey(modelDoc As SldWorks.ModelDoc2) As String
        Try
            Dim info As SelectedDocumentInfo = GetSelectedDocumentInfo(modelDoc)
            Return GetDocumentInfoKey(info)
        Catch ex As Exception
            Debug.WriteLine("RenameWindow.GetCurrentTargetDocumentKey failed: " & ex.Message)
            Return ""
        End Try
    End Function

    Private Function GetDocumentInfoKey(info As SelectedDocumentInfo) As String
        If info Is Nothing Then Return ""
        If Not String.IsNullOrEmpty(info.Path) Then Return info.Path
        If info.Document Is Nothing Then Return ""
        Try
            Dim docPath As String = info.Document.GetPathName()
            If Not String.IsNullOrEmpty(docPath) Then Return docPath
            Return info.Document.GetTitle()
        Catch ex As Exception
            Debug.WriteLine("RenameWindow.GetDocumentInfoKey failed: " & ex.Message)
            Return ""
        End Try
    End Function

    Private Sub RenameButton_Click(sender As Object, e As Wpf.RoutedEventArgs)
        SaveAsAndReplaceSelectedComponent()
    End Sub

    Private Sub SaveAsButton_Click(sender As Object, e As Wpf.RoutedEventArgs)
        SaveAsNewDocumentAndOpen()
    End Sub

    Private Sub SaveAsNewDocumentAndOpen()
        Dim newName As String = NewNameBox.Text.Trim()
        If String.IsNullOrEmpty(newName) Then
            MessageBox.Show("请输入新文件名", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning)
            Return
        End If

        Try
            If SwAppField Is Nothing Then
                MessageBox.Show("未连接到 SolidWorks", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error)
                Return
            End If

            Dim modelDoc As SldWorks.ModelDoc2 = TryCast(SwAppField.ActiveDoc, SldWorks.ModelDoc2)
            If modelDoc Is Nothing Then
                MessageBox.Show("没有打开的文档", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error)
                Return
            End If

            Dim docToSave As SldWorks.ModelDoc2 = GetDocumentToSave(modelDoc)
            If docToSave Is Nothing Then
                MessageBox.Show("无法确定要另存的文档", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error)
                Return
            End If

            Dim oldPath As String = docToSave.GetPathName()
            If String.IsNullOrEmpty(oldPath) Then
                MessageBox.Show("请先保存文档后再另存", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning)
                Return
            End If

            Dim dir As String = Path.GetDirectoryName(oldPath)
            Dim ext As String = Path.GetExtension(oldPath)
            Dim newPath As String = Path.Combine(dir, newName & ext)
            If String.Equals(oldPath, newPath, StringComparison.OrdinalIgnoreCase) Then
                MessageBox.Show("新文件名与当前文件名相同", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning)
                Return
            End If

            If File.Exists(newPath) Then
                Dim overwriteResult As System.Windows.Forms.DialogResult = MessageBox.Show(newPath & " 已存在，是否覆盖？", "询问", MessageBoxButtons.YesNo, MessageBoxIcon.Question)
                If overwriteResult <> System.Windows.Forms.DialogResult.Yes Then Return
            End If

            Dim openedDocFast As SldWorks.ModelDoc2 = TryCast(SwAppField.GetOpenDocumentByName(newPath), SldWorks.ModelDoc2)
            If openedDocFast IsNot Nothing Then
                Dim openedPathFast As String = openedDocFast.GetPathName()
                If Not String.Equals(openedPathFast, oldPath, StringComparison.OrdinalIgnoreCase) Then
                    MessageBox.Show("同名文件已在 SolidWorks 中打开，请修改名称", "信息", MessageBoxButtons.OK, MessageBoxIcon.Information)
                    Return
                End If
            End If

            Dim copyErrors As Integer = 0
            Dim copyWarnings As Integer = 0
            Dim saveCopyResult As Boolean = docToSave.Extension.SaveAs(
                newPath,
                swSaveAsVersion_e.swSaveAsCurrentVersion,
                swSaveAsOptions_e.swSaveAsOptions_Silent + swSaveAsOptions_e.swSaveAsOptions_Copy,
                Nothing,
                copyErrors,
                copyWarnings)
            If Not saveCopyResult Then
                MessageBox.Show("另存副本失败，错误码: " & copyErrors, "错误", MessageBoxButtons.OK, MessageBoxIcon.Error)
                Return
            End If

            Dim openErrors As Integer = 0
            Dim openWarnings As Integer = 0
            Dim docType As Integer = docToSave.GetType()
            Dim savedDoc As SldWorks.ModelDoc2 = TryCast(SwAppField.OpenDoc6(newPath, docType, swOpenDocOptions_e.swOpenDocOptions_Silent, "", openErrors, openWarnings), SldWorks.ModelDoc2)
            If savedDoc Is Nothing Then
                MessageBox.Show("副本已生成，但打开失败，错误码: " & openErrors, "错误", MessageBoxButtons.OK, MessageBoxIcon.Error)
                Return
            End If

            WriteRenameProperties(savedDoc, newName)
            If BlankSizeCheck.IsChecked.GetValueOrDefault() Then SetBlankSize(savedDoc)
            CopyDrawingForSavedDocument(SwAppField, oldPath, newPath)

            savedDoc.Save3(0, 0, 0)
            SwAppField.ActivateDoc3(Path.GetFileName(newPath), False, swRebuildOnActivation_e.swUserDecision, 0)

            UpdateSelectionInfo()
            ShowAutoCloseNotice("另存完成")
        Catch ex As Exception
            MessageBox.Show("另存失败: " & ex.Message, "错误", MessageBoxButtons.OK, MessageBoxIcon.Error)
        End Try
    End Sub

    Private Sub SaveAsAndReplaceSelectedComponent()
        Dim newName As String = NewNameBox.Text.Trim()
        If String.IsNullOrEmpty(newName) Then
            MessageBox.Show("请输入新文件名", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning)
            Return
        End If

        Try
            If SwAppField Is Nothing Then
                MessageBox.Show("未连接到 SolidWorks", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error)
                Return
            End If

            Dim asmModel As SldWorks.ModelDoc2 = TryCast(SwAppField.ActiveDoc, SldWorks.ModelDoc2)
            If asmModel Is Nothing OrElse asmModel.GetType() <> CInt(swDocumentTypes_e.swDocASSEMBLY) Then
                MessageBox.Show("请在装配体中选择要替换的零件或子装配体。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning)
                Return
            End If

            Dim selectedComp As SldWorks.Component2 = GetSelectedComponent(asmModel)
            If selectedComp Is Nothing Then
                MessageBox.Show("请先在装配体中选择一个组件。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning)
                Return
            End If

            Dim sourceDoc As SldWorks.ModelDoc2 = TryCast(selectedComp.GetModelDoc2(), SldWorks.ModelDoc2)
            If sourceDoc Is Nothing Then
                MessageBox.Show("无法读取所选组件的模型文档，请先解析/打开该组件。", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error)
                Return
            End If

            Dim oldPath As String = sourceDoc.GetPathName()
            If String.IsNullOrEmpty(oldPath) Then
                MessageBox.Show("请先保存所选组件后再重命名。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning)
                Return
            End If

            Dim dir As String = Path.GetDirectoryName(oldPath)
            Dim ext As String = Path.GetExtension(oldPath)
            Dim newPath As String = Path.Combine(dir, newName & ext)
            If String.Equals(oldPath, newPath, StringComparison.OrdinalIgnoreCase) Then
                MessageBox.Show("新文件名与当前文件名相同", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning)
                Return
            End If

            If File.Exists(newPath) Then
                Dim overwriteResult As System.Windows.Forms.DialogResult = MessageBox.Show(newPath & " 已存在，是否覆盖？", "询问", MessageBoxButtons.YesNo, MessageBoxIcon.Question)
                If overwriteResult <> System.Windows.Forms.DialogResult.Yes Then Return
            End If

            Dim copyErrors As Integer = 0
            Dim copyWarnings As Integer = 0
            Dim saveCopyResult As Boolean = sourceDoc.Extension.SaveAs(
                newPath,
                swSaveAsVersion_e.swSaveAsCurrentVersion,
                swSaveAsOptions_e.swSaveAsOptions_Silent + swSaveAsOptions_e.swSaveAsOptions_Copy,
                Nothing,
                copyErrors,
                copyWarnings)
            If Not saveCopyResult Then
                MessageBox.Show("另存副本失败，错误码: " & copyErrors, "错误", MessageBoxButtons.OK, MessageBoxIcon.Error)
                Return
            End If

            CopyDrawingForSavedDocument(SwAppField, oldPath, newPath)

            Dim replaceResult As Integer = selectedComp.ReplaceReference(newPath)
            If replaceResult <> 0 Then
                MessageBox.Show("装配体组件替换失败，错误码: " & replaceResult, "错误", MessageBoxButtons.OK, MessageBoxIcon.Error)
                Return
            End If

            asmModel.EditRebuild3()
            MarkDocDirty(asmModel)
            Dim errors As Integer = 0
            Dim warnings As Integer = 0
            asmModel.Save3(swSaveAsOptions_e.swSaveAsOptions_Silent, errors, warnings)

            UpdateSelectionInfo()
            ShowAutoCloseNotice("重命名完成")
        Catch ex As Exception
            MessageBox.Show("重命名失败: " & ex.Message, "错误", MessageBoxButtons.OK, MessageBoxIcon.Error)
        End Try
    End Sub

    Private Function GetSelectedComponent(modelDoc As SldWorks.ModelDoc2) As SldWorks.Component2
        If modelDoc Is Nothing Then Return Nothing
        Try
            Dim selMgr As SldWorks.SelectionMgr = modelDoc.SelectionManager
            If selMgr Is Nothing OrElse selMgr.GetSelectedObjectCount2(-1) < 1 Then Return Nothing

            Dim selObj As Object = selMgr.GetSelectedObject6(1, -1)
            If TypeOf selObj Is SldWorks.Component2 Then Return CType(selObj, SldWorks.Component2)
            Return TryCast(selMgr.GetSelectedObjectsComponent(1), SldWorks.Component2)
        Catch ex As Exception
            Debug.WriteLine("RenameWindow.GetSelectedComponent failed: " & ex.Message)
            Return Nothing
        End Try
    End Function

    Private Sub WriteRenameProperties(doc As SldWorks.ModelDoc2, newName As String)
        If doc Is Nothing Then Return

        Dim config As Object = doc.GetActiveConfiguration()
        If config Is Nothing Then
            Debug.WriteLine("RenameWindow.WriteRenameProperties skipped: active configuration is Nothing.")
            Return
        End If
        Dim cusPropMgr As Object = config.CustomPropertyManager
        If cusPropMgr Is Nothing Then
            Debug.WriteLine("RenameWindow.WriteRenameProperties skipped: CustomPropertyManager is Nothing.")
            Return
        End If

        If FileNameCheck.IsChecked.GetValueOrDefault() Then cusPropMgr.Add3("文件名称", swCustomInfoType_e.swCustomInfoText, newName, swCustomPropertyAddOption_e.swCustomPropertyDeleteAndAdd)
        If MaterialCodeCheck.IsChecked.GetValueOrDefault() Then cusPropMgr.Add3("物料编码", swCustomInfoType_e.swCustomInfoText, newName, swCustomPropertyAddOption_e.swCustomPropertyDeleteAndAdd)
        If PartNumberCheck.IsChecked.GetValueOrDefault() Then cusPropMgr.Add3("零件图号", swCustomInfoType_e.swCustomInfoText, newName, swCustomPropertyAddOption_e.swCustomPropertyDeleteAndAdd)
        If VersionCheck.IsChecked.GetValueOrDefault() AndAlso Not String.IsNullOrWhiteSpace(VersionBox.Text) Then cusPropMgr.Add3("版本", swCustomInfoType_e.swCustomInfoText, VersionBox.Text.Trim(), swCustomPropertyAddOption_e.swCustomPropertyDeleteAndAdd)
        If DesignCheck.IsChecked.GetValueOrDefault() AndAlso Not String.IsNullOrWhiteSpace(DesignBox.Text) Then
            Dim designInfo As String = DesignBox.Text.Trim()
            cusPropMgr.Add3("设计", swCustomInfoType_e.swCustomInfoText, designInfo, swCustomPropertyAddOption_e.swCustomPropertyDeleteAndAdd)
            cusPropMgr.Add3("出图", swCustomInfoType_e.swCustomInfoText, designInfo, swCustomPropertyAddOption_e.swCustomPropertyDeleteAndAdd)
        End If

        MarkDocDirty(doc)
    End Sub

    Private Sub SetBlankSize(doc As SldWorks.ModelDoc2)
        If doc Is Nothing Then Return
        Dim docType As Integer = doc.GetType()
        If docType <> CInt(swDocumentTypes_e.swDocPART) AndAlso docType <> CInt(swDocumentTypes_e.swDocASSEMBLY) Then Return

        Dim corners As Object
        If docType = CInt(swDocumentTypes_e.swDocPART) Then
            corners = CType(doc, SldWorks.PartDoc).GetPartBox(True)
        Else
            corners = CType(doc, SldWorks.AssemblyDoc).GetBox(swBoundingBoxOptions_e.swBoundingBoxIncludeRefPlanes)
        End If
        If corners Is Nothing Then
            Debug.WriteLine("RenameWindow.SetBlankSize skipped: bounding box is Nothing.")
            Return
        End If

        Dim values = {
            Math.Round((corners(3) - corners(0)) * 1000, 1),
            Math.Round((corners(4) - corners(1)) * 1000, 1),
            Math.Round((corners(5) - corners(2)) * 1000, 1)}
        Array.Sort(values)

        Dim blankSize As String = values(2) & "x" & values(1) & "x" & values(0)
        Dim config As Object = doc.GetActiveConfiguration()
        If config Is Nothing Then
            Debug.WriteLine("RenameWindow.SetBlankSize skipped: active configuration is Nothing.")
            Return
        End If
        Dim cusPropMgr As Object = config.CustomPropertyManager
        If cusPropMgr Is Nothing Then
            Debug.WriteLine("RenameWindow.SetBlankSize skipped: CustomPropertyManager is Nothing.")
            Return
        End If
        cusPropMgr.Add3("下料尺寸", swCustomInfoType_e.swCustomInfoText, blankSize, swCustomPropertyAddOption_e.swCustomPropertyDeleteAndAdd)
        MarkDocDirty(doc)
    End Sub

    Private Sub CopyDrawingForSavedDocument(swApp As SldWorks.SldWorks, oldModelPath As String, newModelPath As String)
        If swApp Is Nothing OrElse String.IsNullOrEmpty(oldModelPath) OrElse String.IsNullOrEmpty(newModelPath) Then Return

        Try
            Dim dir As String = Path.GetDirectoryName(oldModelPath)
            Dim oldBaseName As String = Path.GetFileNameWithoutExtension(oldModelPath)
            Dim newBaseName As String = Path.GetFileNameWithoutExtension(newModelPath)
            Dim oldDrawingPath As String = Path.Combine(dir, oldBaseName & ".SLDDRW")
            If Not File.Exists(oldDrawingPath) Then Return

            Dim newDrawingPath As String = Path.Combine(dir, newBaseName & ".SLDDRW")
            If File.Exists(newDrawingPath) Then
                Dim overwriteResult As System.Windows.Forms.DialogResult = MessageBox.Show(newDrawingPath & " 已存在，是否覆盖？", "询问", MessageBoxButtons.YesNo, MessageBoxIcon.Question)
                If overwriteResult <> System.Windows.Forms.DialogResult.Yes Then Return
            End If

            File.Copy(oldDrawingPath, newDrawingPath, True)
            swApp.ReplaceReferencedDocument(newDrawingPath, oldModelPath, newModelPath)
        Catch ex As Exception
            MessageBox.Show("工程图复制或关联更新失败: " & ex.Message, "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning)
        End Try
    End Sub

    Private Sub MarkDocDirty(doc As SldWorks.ModelDoc2)
        If doc Is Nothing Then Return
        Try
            doc.SetSaveFlag()
        Catch ex As Exception
            Debug.WriteLine("RenameWindow.MarkDocDirty failed: " & ex.Message)
        End Try
    End Sub

    Private Sub ShowAutoCloseNotice(message As String, Optional title As String = "提示")
        Try
            Dim ni As New NotifyIcon()
            ni.Icon = Drawing.SystemIcons.Information
            ni.Visible = True
            ni.BalloonTipTitle = title
            ni.BalloonTipText = message
            ni.BalloonTipIcon = ToolTipIcon.Info
            ni.ShowBalloonTip(1800)

            Dim t As New Timer() With {.Interval = 2200}
            AddHandler t.Tick,
                Sub()
                    t.Stop()
                    t.Dispose()
                    ni.Visible = False
                    ni.Dispose()
                End Sub
            t.Start()
        Catch ex As Exception
            Debug.WriteLine("RenameWindow.ShowAutoCloseNotice failed: " & ex.Message)
        End Try
    End Sub

    Private Class SelectedDocumentInfo
        Public Property Document As SldWorks.ModelDoc2
        Public Property Path As String
    End Class
End Class
