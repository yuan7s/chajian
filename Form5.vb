Imports System.Runtime.InteropServices
Imports SwConst

Public Class Form5

    Private Const WmNclbuttondown As Integer = &HA1
    Private Const HtCaption As Integer = &H2

    <DllImport("user32.dll")>
    Private Shared Function ReleaseCapture() As Boolean
    End Function

    <DllImport("user32.dll")>
    Private Shared Function SendMessage(hWnd As IntPtr, msg As Integer, wParam As IntPtr, lParam As IntPtr) As IntPtr
    End Function

    Private WithEvents _swAppField As SldWorks.SldWorks

    Public Property SwApp As SldWorks.SldWorks
        Get
            Return _swAppField
        End Get
        Set(value As SldWorks.SldWorks)
            _swAppField = value
        End Set
    End Property

    Private _showKeyOnly As Boolean
    Private _showCustomProps As Boolean
    Private _attachedPartDoc As SldWorks.PartDoc
    Private _attachedAsmDoc As SldWorks.AssemblyDoc
    Private _attachedDrawDoc As SldWorks.DrawingDoc
    Private _attachedDocPath As String

    Private Sub Form5_Load(sender As Object, e As EventArgs) Handles MyBase.Load
        ApplyTransparency()
        ApplyColorScheme(My.Settings.Form5_ColorScheme)
        TopMost = My.Settings.Form5_TopMost

        ' 窗口定位在屏幕右侧
        Dim workArea As Drawing.Rectangle = Screen.PrimaryScreen.WorkingArea
        Me.Location = New Drawing.Point(workArea.Right - Me.Width - 20, workArea.Top + 60)

        EnableDrag()

        If _swAppField IsNot Nothing Then
            AttachDocEvents()
            RefreshProperties()
        End If
    End Sub

    Private Sub Form5_FormClosing(sender As Object, e As FormClosingEventArgs) Handles Me.FormClosing
        DetachDocEvents()
        _swAppField = Nothing
    End Sub

    Private Sub LblClose_Click(sender As Object, e As EventArgs) Handles LblClose.Click
        Me.Close()
    End Sub

    ' 切换配置属性 / 自定义属性
    Private Sub BtnPropType_Click(sender As Object, e As EventArgs) Handles BtnPropType.Click
        _showCustomProps = Not _showCustomProps
        BtnPropType.Text = If(_showCustomProps, "自定义属性", "配置属性")
        RefreshProperties()
    End Sub

    Private Sub RefreshProperties()
        DataGridView1.Rows.Clear()
        Try
            Dim targetDoc As SldWorks.ModelDoc2 = GetTargetDoc()
            If targetDoc Is Nothing Then
                Label1.Text = "无文档"
                Return
            End If

            Dim docPath As String = targetDoc.GetPathName()
            If Not String.IsNullOrEmpty(docPath) Then
                Label1.Text = IO.Path.GetFileNameWithoutExtension(docPath)
            Else
                Label1.Text = targetDoc.GetTitle()
            End If

            Dim confString As String = ""
            Dim nameArr As Object = Nothing

            If _showCustomProps Then
                nameArr = targetDoc.GetCustomInfoNames()
            Else
                confString = targetDoc.GetActiveConfiguration().Name
                nameArr = targetDoc.GetCustomInfoNames2(confString)
            End If
            If nameArr Is Nothing Then Return

            Dim keySet As HashSet(Of String) = Nothing
            If _showKeyOnly Then
                keySet = New HashSet(Of String)(StringComparer.OrdinalIgnoreCase)
                For Each k In GetKeyProperties()
                    keySet.Add(k)
                Next
            End If

            For i As Integer = 0 To UBound(nameArr)
                Dim propName As String = nameArr(i).ToString()
                If _showKeyOnly AndAlso Not keySet.Contains(propName) Then Continue For
                Dim propVal As String = GetPropValue(targetDoc, confString, propName)
                DataGridView1.Rows.Add(propName, If(propVal, ""))
            Next
        Catch
        End Try
    End Sub

    Private Function GetTargetDoc() As SldWorks.ModelDoc2
        If _swAppField Is Nothing Then Return Nothing
        Dim modelDoc As SldWorks.ModelDoc2 = TryCast(_swAppField.ActiveDoc, SldWorks.ModelDoc2)
        If modelDoc Is Nothing Then Return Nothing

        Dim selMgr As SldWorks.SelectionMgr = modelDoc.SelectionManager
        Dim selCount As Integer = 0
        Try : selCount = selMgr.GetSelectedObjectCount2(-1) : Catch : End Try

        If selCount >= 1 Then
            Dim selObj As Object = selMgr.GetSelectedObject6(1, -1)
            If TypeOf selObj Is SldWorks.Component2 Then
                Dim comp As SldWorks.Component2 = CType(selObj, SldWorks.Component2)
                Dim refModel As SldWorks.ModelDoc2 = comp.GetModelDoc2()
                If refModel IsNot Nothing Then Return refModel
            End If
        End If

        Return modelDoc
    End Function

    Private Function GetPropValue(doc As SldWorks.ModelDoc2, conf As String, propName As String) As String
        Try
            Dim v As String = doc.GetCustomInfoValue(conf, propName)
            Return If(v, "")
        Catch
            Return ""
        End Try
    End Function

    ' 切换全部属性 / 关键属性
    Private Sub BtnToggle_Click(sender As Object, e As EventArgs) Handles BtnToggle.Click
        _showKeyOnly = Not _showKeyOnly
        BtnToggle.Text = If(_showKeyOnly, "关键属性", "全部属性")
        RefreshProperties()
    End Sub

    ' 打开设置
    Private Sub BtnSettings_Click(sender As Object, e As EventArgs) Handles BtnSettings.Click
        Dim wasTopMost As Boolean = Me.TopMost
        Me.TopMost = False
        Dim f6 As New Form6()
        f6.Owner = Me
        f6.ShowDialog()
        Me.TopMost = wasTopMost
        ApplyTransparency()
        ApplyColorScheme(My.Settings.Form5_ColorScheme)
        RefreshProperties()
    End Sub

    Private ReadOnly KEY_COLOR As Drawing.Color = Drawing.Color.Fuchsia

    Private Sub ApplyTransparency()
        Me.Opacity = 1.0
        Me.BackColor = KEY_COLOR
        Me.TransparencyKey = KEY_COLOR
    End Sub

    Private Function TextAlpha() As Integer
        Return CInt(Math.Max(10, Math.Min(255, My.Settings.Form5_TextOpacity * 255)))
    End Function

    Protected Overrides Sub OnPaintBackground(e As System.Windows.Forms.PaintEventArgs)
        MyBase.OnPaintBackground(e)
        Dim bgAlpha As Integer = CInt(Math.Max(5, Math.Min(255, My.Settings.Form5_Opacity * 255)))
        Using brush As New Drawing.SolidBrush(Drawing.Color.FromArgb(bgAlpha, 0, 0, 0))
            e.Graphics.FillRectangle(brush, Me.ClientRectangle)
        End Using
    End Sub

    Private Sub ApplyColorScheme(scheme As String)
        Dim alpha As Integer = TextAlpha()
        Dim foreColor As Drawing.Color
        Dim gridBackColor As Drawing.Color
        Dim gridForeColor As Drawing.Color
        Dim headerBackColor As Drawing.Color
        Dim headerForeColor As Drawing.Color

        Select Case scheme
            Case "终端绿"
                foreColor = Drawing.Color.FromArgb(alpha, Drawing.Color.Lime)
                gridBackColor = KEY_COLOR
                gridForeColor = Drawing.Color.FromArgb(alpha, Drawing.Color.Lime)
                headerBackColor = KEY_COLOR
                headerForeColor = Drawing.Color.FromArgb(alpha, Drawing.Color.Lime)
            Case "白字"
                foreColor = Drawing.Color.FromArgb(alpha, Drawing.Color.White)
                gridBackColor = KEY_COLOR
                gridForeColor = Drawing.Color.FromArgb(alpha, Drawing.Color.White)
                headerBackColor = KEY_COLOR
                headerForeColor = Drawing.Color.FromArgb(alpha, Drawing.Color.White)
            Case Else
                foreColor = Drawing.Color.FromArgb(alpha, System.Drawing.SystemColors.ControlText)
                gridBackColor = KEY_COLOR
                gridForeColor = Drawing.Color.FromArgb(alpha, System.Drawing.SystemColors.WindowText)
                headerBackColor = KEY_COLOR
                headerForeColor = Drawing.Color.FromArgb(alpha, System.Drawing.SystemColors.ControlText)
        End Select

        Me.ForeColor = foreColor
        DataGridView1.BackgroundColor = KEY_COLOR
        DataGridView1.DefaultCellStyle.BackColor = KEY_COLOR
        DataGridView1.DefaultCellStyle.ForeColor = gridForeColor
        DataGridView1.ColumnHeadersDefaultCellStyle.BackColor = headerBackColor
        DataGridView1.ColumnHeadersDefaultCellStyle.ForeColor = headerForeColor
        DataGridView1.EnableHeadersVisualStyles = False
    End Sub

    Private Function GetKeyProperties() As String()
        Dim raw As String = My.Settings.Form5_KeyProperties
        If String.IsNullOrWhiteSpace(raw) Then
            Return {"物料编码", "零件图号", "文件名称", "零件类型", "下料尺寸", "版本", "设计", "出图"}
        End If
        Return raw.Split({","c}, StringSplitOptions.RemoveEmptyEntries)
    End Function

    ' SW 事件
    Private Function _swAppField_ActiveDocChangeNotify() As Integer Handles _swAppField.ActiveDocChangeNotify
        AttachDocEvents()
        RefreshProperties()
        Return 0
    End Function

    Private Function _swAppField_ActiveModelDocChangeNotify() As Integer Handles _swAppField.ActiveModelDocChangeNotify
        AttachDocEvents()
        RefreshProperties()
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
            If _swAppField Is Nothing Then Return
            Dim modelDoc As SldWorks.ModelDoc2 = TryCast(_swAppField.ActiveDoc, SldWorks.ModelDoc2)
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
        RefreshProperties()
        Return 0
    End Function

    ' 拖拽
    Private Sub EnableDrag()
        AddHandler MouseDown, AddressOf DragForm_MouseDown
        AddHandler Label1.MouseDown, AddressOf DragForm_MouseDown
        AddHandler DataGridView1.MouseDown, AddressOf DragForm_MouseDown
    End Sub

    Private Sub DragForm_MouseDown(sender As Object, e As MouseEventArgs)
        If e.Button = MouseButtons.Left Then
            ReleaseCapture()
            SendMessage(Handle, WmNclbuttondown, New IntPtr(HtCaption), IntPtr.Zero)
        End If
    End Sub
End Class
