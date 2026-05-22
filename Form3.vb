Imports System.IO
Imports System.Runtime.InteropServices
Imports SwConst

Public Class Form3
    Private WithEvents _swAppField As SldWorks.SldWorks
    Private _attachedDocPath As String
    Private _attachedPartDoc As SldWorks.PartDoc
    Private _attachedAsmDoc As SldWorks.AssemblyDoc
    Private _attachedDrawDoc As SldWorks.DrawingDoc
    Private _suppressNameCheck As Boolean
    Private _nameCheckTimer As Timer
    Private _assemblyNameCache As HashSet(Of String)
    Private _cachedAssemblyPath As String

    Private Const WmNclbuttondown As Integer = &HA1
    Private Const HtCaption As Integer = &H2

    <DllImport("user32.dll")>
    Private Shared Function ReleaseCapture() As Boolean
    End Function

    <DllImport("user32.dll")>
    Private Shared Function SendMessage(hWnd As IntPtr, msg As Integer, wParam As IntPtr, lParam As IntPtr) As IntPtr
    End Function

    Private Sub Form3_Load(sender As Object, e As EventArgs) Handles MyBase.Load
        TopMost = True
        TextBox1.ReadOnly = True
        EnableDrag()
        _nameCheckTimer = New Timer() With {.Interval = 500}
        AddHandler _nameCheckTimer.Tick, AddressOf NameCheckTimer_Tick
        _suppressNameCheck = True
        UpdateSelectionInfo()
        _suppressNameCheck = False
        AttachDocEvents()
    End Sub

    Private Sub Form3_FormClosing(sender As Object, e As FormClosingEventArgs) Handles MyBase.FormClosing
        If _nameCheckTimer IsNot Nothing Then
            _nameCheckTimer.Stop()
            _nameCheckTimer.Dispose()
            _nameCheckTimer = Nothing
        End If
        _swAppField = Nothing
    End Sub

    Public Property SwApp As SldWorks.SldWorks
        Get
            Return _swAppField
        End Get
        Set(value As SldWorks.SldWorks)
            _swAppField = value
        End Set
    End Property

    Private Function _swAppField_ActiveDocChangeNotify() As Integer Handles _swAppField.ActiveDocChangeNotify
        InvalidateNameCache()
        UpdateSelectionInfo()
        AttachDocEvents()
        Return 0
    End Function

    Private Function _swAppField_ActiveModelDocChangeNotify() As Integer Handles _swAppField.ActiveModelDocChangeNotify
        InvalidateNameCache()
        UpdateSelectionInfo()
        AttachDocEvents()
        Return 0
    End Function

    Private Sub InvalidateNameCache()
        _assemblyNameCache = Nothing
        _cachedAssemblyPath = Nothing
    End Sub

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
            If _swAppField Is Nothing Then Return
            Dim modelDoc As SldWorks.ModelDoc2 = CType(_swAppField.ActiveDoc, SldWorks.ModelDoc2)
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
        UpdateSelectionInfo()
        Return 0
    End Function

    Private Sub UpdateSelectionInfo()
        _suppressNameCheck = True
        Try
            Dim swApp As SldWorks.SldWorks = _swAppField
            If swApp Is Nothing Then
                TextBox1.Text = ""
                Label3.Text = ""
                Label4.Text = ""
                Return
            End If

            Dim modelDoc As SldWorks.ModelDoc2 = CType(swApp.ActiveDoc, SldWorks.ModelDoc2)
            If modelDoc Is Nothing Then
                TextBox1.Text = ""
                Label3.Text = ""
                Label4.Text = ""
                Return
            End If

            Dim activePath As String = modelDoc.GetPathName()
            Dim activeExt As String = ""
            If Not String.IsNullOrEmpty(activePath) Then
                activeExt = Path.GetExtension(activePath)
                If Not String.IsNullOrEmpty(activeExt) Then activeExt = activeExt.TrimStart("."c).ToLowerInvariant()
            End If

            Dim selMgr As SldWorks.SelectionMgr = modelDoc.SelectionManager
            Dim selCount As Integer = 0
            Try
                selCount = selMgr.GetSelectedObjectCount2(-1)
            Catch
                selCount = 0
            End Try

            If selCount >= 1 Then
                Dim selObj As Object = selMgr.GetSelectedObject6(1, -1)
                Dim filename As String = ""
                If TypeOf selObj Is SldWorks.Component2 Then
                    Dim comp As SldWorks.Component2 = CType(selObj, SldWorks.Component2)
                    Dim refModel As SldWorks.ModelDoc2 = comp.GetModelDoc2()
                    If refModel IsNot Nothing Then
                        filename = refModel.GetPathName()
                    Else
                        filename = comp.GetPathName()
                    End If
                ElseIf TypeOf selObj Is SldWorks.ModelDoc2 Then
                    Dim selModel As SldWorks.ModelDoc2 = CType(selObj, SldWorks.ModelDoc2)
                    filename = selModel.GetPathName()
                Else
                    filename = modelDoc.GetPathName()
                End If

                TextBox1.Text = If(String.IsNullOrEmpty(filename), "", Path.GetFileNameWithoutExtension(filename))
                TextBox2.Text = If(String.IsNullOrEmpty(filename), "", Path.GetFileNameWithoutExtension(filename))

                Dim selExt As String = ""
                If Not String.IsNullOrEmpty(filename) Then
                    selExt = Path.GetExtension(filename)
                    If Not String.IsNullOrEmpty(selExt) Then selExt = selExt.TrimStart("."c).ToLowerInvariant()
                End If
                Label3.Text = If(String.IsNullOrEmpty(selExt), "", "." & selExt)
                Label4.Text = If(String.IsNullOrEmpty(selExt), "", "." & selExt)

                UpdateDrawingExistsIndicator(filename)
            Else
                Dim docName As String = modelDoc.GetPathName()
                Dim defaultName As String = If(String.IsNullOrEmpty(docName), "", Path.GetFileNameWithoutExtension(docName))
                TextBox1.Text = defaultName
                TextBox2.Text = defaultName
                Dim docExt As String = If(String.IsNullOrEmpty(docName), "", Path.GetExtension(docName).TrimStart("."c).ToLowerInvariant())
                Label3.Text = If(String.IsNullOrEmpty(docExt), "", "." & docExt)
                Label4.Text = If(String.IsNullOrEmpty(docExt), "", "." & docExt)
                UpdateDrawingExistsIndicator(docName)
            End If
        Catch
        Finally
            _suppressNameCheck = False
            Label6.Visible = False
        End Try
    End Sub

    Private Sub UpdateDrawingExistsIndicator(modelPath As String)
        If String.IsNullOrEmpty(modelPath) Then
            Label5.Visible = False
            Return
        End If

        Try
            Dim dir As String = Path.GetDirectoryName(modelPath)
            Dim baseName As String = Path.GetFileNameWithoutExtension(modelPath)
            If String.IsNullOrEmpty(dir) OrElse String.IsNullOrEmpty(baseName) Then
                Label5.Visible = False
                Return
            End If

            Dim drawingPath As String = Path.Combine(dir, baseName & ".SLDDRW")
            If System.IO.File.Exists(drawingPath) Then
                Label5.Text = "存在工程图"
                Label5.BackColor = System.Drawing.Color.LimeGreen
                Label5.Visible = True
            Else
                Label5.Text = "无工程图"
                Label5.BackColor = System.Drawing.Color.Red
                Label5.Visible = True
            End If
        Catch
            Label5.Visible = False
        End Try
    End Sub

    Private Sub Button2_Click(sender As Object, e As EventArgs) Handles Button2.Click
        Dim newName As String = TextBox2.Text.Trim()
        If String.IsNullOrEmpty(newName) Then
            MessageBox.Show("请输入新文件名", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning)
            Return
        End If

        Try
            Dim swApp As SldWorks.SldWorks = _swAppField
            If swApp Is Nothing Then
                MessageBox.Show("未连接到SolidWorks", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error)
                Return
            End If

            Dim modelDoc As SldWorks.ModelDoc2 = CType(swApp.ActiveDoc, SldWorks.ModelDoc2)
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

            If System.IO.File.Exists(newPath) Then
                Dim overwriteResult As DialogResult = MessageBox.Show(newPath & " 已存在，是否覆盖？", "询问", MessageBoxButtons.YesNo, MessageBoxIcon.Question)
                If overwriteResult <> DialogResult.Yes Then Return
            End If

            Dim docs As Object = swApp.GetDocuments()
            If docs IsNot Nothing Then
                For Each doc As SldWorks.ModelDoc2 In docs
                    Dim docPath As String = doc.GetPathName()
                    If Not String.IsNullOrEmpty(docPath) AndAlso
                       Not String.Equals(oldPath, docPath, StringComparison.OrdinalIgnoreCase) Then
                        If String.Equals(Path.GetFileName(docPath), newName & ext, StringComparison.OrdinalIgnoreCase) Then
                            MessageBox.Show("名称为 " & newName & ext & " 的文件已经打开，请修改为不同的名称", "信息", MessageBoxButtons.OK, MessageBoxIcon.Information)
                            Return
                        End If
                    End If
                Next
            End If

            Dim saveResult As Integer = docToSave.SaveAs3(newPath, 0, 0)
            If saveResult <> 0 Then
                MessageBox.Show("另存失败", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error)
                Return
            End If

            Dim config As Object = docToSave.GetActiveConfiguration
            Dim cusPropMgr As Object = config.CustomPropertyManager
            cusPropMgr.Add3("文件名称", swCustomInfoType_e.swCustomInfoText, newName, swCustomPropertyAddOption_e.swCustomPropertyDeleteAndAdd)
            cusPropMgr.Add3("物料编码", swCustomInfoType_e.swCustomInfoText, newName, swCustomPropertyAddOption_e.swCustomPropertyDeleteAndAdd)
            cusPropMgr.Add3("零件图号", swCustomInfoType_e.swCustomInfoText, newName, swCustomPropertyAddOption_e.swCustomPropertyDeleteAndAdd)

            If CheckBox3.Checked AndAlso Not String.IsNullOrWhiteSpace(TextBox3.Text) Then
                cusPropMgr.Add3("版本", swCustomInfoType_e.swCustomInfoText, TextBox3.Text.Trim(), swCustomPropertyAddOption_e.swCustomPropertyDeleteAndAdd)
            End If

            If CheckBox4.Checked AndAlso Not String.IsNullOrWhiteSpace(TextBox4.Text) Then
                Dim designInfo As String = TextBox4.Text.Trim()
                cusPropMgr.Add3("设计", swCustomInfoType_e.swCustomInfoText, designInfo, swCustomPropertyAddOption_e.swCustomPropertyDeleteAndAdd)
                cusPropMgr.Add3("出图", swCustomInfoType_e.swCustomInfoText, designInfo, swCustomPropertyAddOption_e.swCustomPropertyDeleteAndAdd)
            End If

            If CheckBox2.Checked Then
                SetBlankSize(docToSave)
            End If

            If CheckBox1.Checked Then
                Dim oldBaseName As String = Path.GetFileNameWithoutExtension(oldPath)
                Dim oldDrawingPath As String = Path.Combine(dir, oldBaseName & ".SLDDRW")
                If System.IO.File.Exists(oldDrawingPath) Then
                    Dim newDrawingPath As String = Path.Combine(dir, newName & ".SLDDRW")
                    System.IO.File.Copy(oldDrawingPath, newDrawingPath)
                    swApp.ReplaceReferencedDocument(newDrawingPath, oldPath, newPath)
                    System.IO.File.Delete(oldDrawingPath)
                End If
            End If

            System.IO.File.Delete(oldPath)

            UpdateSelectionInfo()
        Catch ex As Exception
            MessageBox.Show("另存失败: " & ex.Message, "错误", MessageBoxButtons.OK, MessageBoxIcon.Error)
        End Try
    End Sub

    Private Function GetDocumentToSave(modelDoc As SldWorks.ModelDoc2) As SldWorks.ModelDoc2
        Dim selMgr As SldWorks.SelectionMgr = modelDoc.SelectionManager
        Dim selCount As Integer = 0
        Try
            selCount = selMgr.GetSelectedObjectCount2(-1)
        Catch
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

    Private Sub TextBox2_TextChanged(sender As Object, e As EventArgs) Handles TextBox2.TextChanged
        ' 防抖：重置定时器，用户停顿时才检查
        If _suppressNameCheck Then Return
        _nameCheckTimer.Stop()
        _nameCheckTimer.Start()
    End Sub

    Private Sub NameCheckTimer_Tick(sender As Object, e As EventArgs)
        _nameCheckTimer.Stop()
        CheckNewNameConflict()
    End Sub

    Private Sub CheckNewNameConflict()
        Dim newName As String = TextBox2.Text.Trim()
        If String.IsNullOrEmpty(newName) Then
            ResetNewNameIndicators()
            Label6.Visible = False
            Return
        End If

        Try
            Dim swApp As SldWorks.SldWorks = _swAppField
            If swApp Is Nothing Then Return

            Dim modelDoc As SldWorks.ModelDoc2 = CType(swApp.ActiveDoc, SldWorks.ModelDoc2)
            If modelDoc Is Nothing Then Return

            Dim docPath As String = modelDoc.GetPathName()
            If String.IsNullOrEmpty(docPath) Then Return

            Dim dir As String = Path.GetDirectoryName(docPath)
            Dim ext As String = Label4.Text.Trim()

            Dim fileExists As Boolean = False
            If Not String.IsNullOrEmpty(ext) Then
                Dim newFilePath As String = Path.Combine(dir, newName & ext)
                fileExists = System.IO.File.Exists(newFilePath)
            End If

            Dim existsInAssembly As Boolean = CheckNameExistsInAssembly(modelDoc, newName)

            If fileExists OrElse existsInAssembly Then
                TextBox2.BackColor = System.Drawing.Color.MistyRose
                Label6.ForeColor = System.Drawing.Color.Red
                If existsInAssembly AndAlso fileExists Then
                    Label6.Text = "重名(装配体+本地)"
                ElseIf existsInAssembly Then
                    Label6.Text = "重名(装配体)"
                Else
                    Label6.Text = "重名(本地)"
                End If
                Label6.Visible = True
            Else
                TextBox2.BackColor = System.Drawing.Color.LightGreen
                Label6.Text = "可保存"
                Label6.ForeColor = System.Drawing.Color.Green
                Label6.BackColor = System.Drawing.SystemColors.Control
                Label6.Visible = True
                UpdateNewNameDrawingIndicator(dir, newName)
            End If
        Catch
        End Try
    End Sub

    Private Function CheckNameExistsInAssembly(modelDoc As SldWorks.ModelDoc2, newName As String) As Boolean
        Try
            Dim docType As Integer = modelDoc.GetType()
            If docType <> CInt(swDocumentTypes_e.swDocASSEMBLY) Then
                Return False
            End If

            ' 缓存失效时重建组件名 HashSet
            Dim docPath As String = modelDoc.GetPathName()
            If _assemblyNameCache Is Nothing OrElse
               Not String.Equals(docPath, _cachedAssemblyPath, StringComparison.OrdinalIgnoreCase) Then
                BuildAssemblyNameCache(modelDoc)
                _cachedAssemblyPath = docPath
            End If

            Return _assemblyNameCache IsNot Nothing AndAlso
                   _assemblyNameCache.Contains(If(newName, ""))
        Catch
            Return False
        End Try
    End Function

    Private Sub BuildAssemblyNameCache(modelDoc As SldWorks.ModelDoc2)
        _assemblyNameCache = New HashSet(Of String)(StringComparer.OrdinalIgnoreCase)
        Try
            Dim assemblyDoc As SldWorks.AssemblyDoc = CType(modelDoc, SldWorks.AssemblyDoc)
            Dim comps As Object = assemblyDoc.GetComponents(False)
            If comps Is Nothing Then Return

            For Each comp As SldWorks.Component2 In comps
                Dim refName As String = comp.Name2
                If Not String.IsNullOrEmpty(refName) Then
                    _assemblyNameCache.Add(refName)
                End If
            Next
        Catch
        End Try
    End Sub

    Private Sub ResetNewNameIndicators()
        TextBox2.BackColor = System.Drawing.SystemColors.Window
        TextBox2.ForeColor = System.Drawing.SystemColors.WindowText
        Label6.ForeColor = System.Drawing.SystemColors.ControlText
    End Sub

    Private Sub UpdateNewNameDrawingIndicator(dir As String, newName As String)
        If String.IsNullOrEmpty(dir) OrElse String.IsNullOrEmpty(newName) Then
            Return
        End If

        Try
            Dim drawingPath As String = Path.Combine(dir, newName & ".SLDDRW")
            If System.IO.File.Exists(drawingPath) Then
                Label6.Text &= "，有工程图"
            End If
        Catch
        End Try
    End Sub

    Private Sub EnableDrag()
        AddHandler MouseDown, AddressOf DragForm_MouseDown
        AttachDragHandlers(Me)
    End Sub

    Private Sub AttachDragHandlers(ctrl As Control)
        For Each child As Control In ctrl.Controls
            If TypeOf child Is TextBox OrElse
               TypeOf child Is Button OrElse
               TypeOf child Is CheckBox Then Continue For
            AddHandler child.MouseDown, AddressOf DragForm_MouseDown
            If child.HasChildren Then
                AttachDragHandlers(child)
            End If
        Next
    End Sub

    Private Sub DragForm_MouseDown(sender As Object, e As MouseEventArgs)
        If e.Button = MouseButtons.Left Then
            ReleaseCapture()
            SendMessage(Handle, WmNclbuttondown, New IntPtr(HtCaption), IntPtr.Zero)
        End If
    End Sub

    Private Sub SetBlankSize(doc As SldWorks.ModelDoc2)
        If doc Is Nothing Then Return
        Dim docType As Integer = doc.GetType()
        If docType <> SwConst.swDocumentTypes_e.swDocPART AndAlso docType <> SwConst.swDocumentTypes_e.swDocASSEMBLY Then Return

        Dim corners As Object
        If docType = SwConst.swDocumentTypes_e.swDocPART Then
            corners = CType(doc, SldWorks.PartDoc).GetPartBox(True)
        Else
            corners = CType(doc, SldWorks.AssemblyDoc).GetBox(SwConst.swBoundingBoxOptions_e.swBoundingBoxIncludeRefPlanes)
        End If

        Dim x As Double = (corners(3) - corners(0)) * 1000
        Dim y As Double = (corners(4) - corners(1)) * 1000
        Dim z As Double = (corners(5) - corners(2)) * 1000

        Dim values(2) As Double
        values(0) = Math.Round(x, 1)
        values(1) = Math.Round(y, 1)
        values(2) = Math.Round(z, 1)

        For i As Integer = 0 To 2
            For j As Integer = i + 1 To 2
                If values(i) > values(j) Then
                    Dim temp As Double = values(i)
                    values(i) = values(j)
                    values(j) = temp
                End If
            Next
        Next

        Dim c As String = values(2) & "x" & values(1) & "x" & values(0)
        Dim config As Object = doc.GetActiveConfiguration
        Dim cusPropMgr As Object = config.CustomPropertyManager
        cusPropMgr.Add3("下料尺寸", SwConst.swCustomInfoType_e.swCustomInfoText, c, SwConst.swCustomPropertyAddOption_e.swCustomPropertyDeleteAndAdd)

        doc.SketchManager.Insert3DSketch(True)
        doc.SketchManager.Insert3DSketch(True)
    End Sub

    Private Sub TextBox4_TextChanged(sender As Object, e As EventArgs) Handles TextBox4.TextChanged

    End Sub
End Class