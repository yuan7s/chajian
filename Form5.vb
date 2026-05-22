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
    Private _refreshTimer As Timer

    Private Sub Form5_Load(sender As Object, e As EventArgs) Handles MyBase.Load
        ApplyOpacity()
        ApplyColorScheme(My.Settings.Form5_ColorScheme)
        TopMost = My.Settings.Form5_TopMost

        ' 窗口定位在屏幕右侧
        Dim workArea As Drawing.Rectangle = Screen.PrimaryScreen.WorkingArea
        Me.Location = New Drawing.Point(workArea.Right - Me.Width - 20, workArea.Top + 60)

        EnableDrag()

        _refreshTimer = New Timer() With {.Interval = 500}
        AddHandler _refreshTimer.Tick, AddressOf RefreshTimer_Tick
        _refreshTimer.Start()

        If _swAppField IsNot Nothing Then
            AttachDocEvents()
            RefreshProperties()
        End If
    End Sub

    Private Sub Form5_FormClosing(sender As Object, e As FormClosingEventArgs) Handles Me.FormClosing
        If _refreshTimer IsNot Nothing Then
            _refreshTimer.Stop()
            _refreshTimer.Dispose()
            _refreshTimer = Nothing
        End If
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

    Private Sub RefreshTimer_Tick(sender As Object, e As EventArgs)
        Try
            If _swAppField IsNot Nothing AndAlso _swAppField.ActiveDoc IsNot Nothing Then
                RefreshProperties()
            End If
        Catch
        End Try
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
                ' 文档级自定义属性
                nameArr = targetDoc.GetCustomInfoNames()
            Else
                ' 配置特定属性
                confString = GetTargetConf(targetDoc)
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

            For Each propName As String In nameArr
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

    Private Function GetTargetConf(targetDoc As SldWorks.ModelDoc2) As String
        Try
            If _swAppField IsNot Nothing Then
                Dim modelDoc As SldWorks.ModelDoc2 = TryCast(_swAppField.ActiveDoc, SldWorks.ModelDoc2)
                If modelDoc IsNot Nothing AndAlso modelDoc IsNot targetDoc Then
                    Dim selMgr As SldWorks.SelectionMgr = modelDoc.SelectionManager
                    Dim selCount As Integer = 0
                    Try : selCount = selMgr.GetSelectedObjectCount2(-1) : Catch : End Try
                    If selCount >= 1 Then
                        Dim selObj As Object = selMgr.GetSelectedObject6(1, -1)
                        If TypeOf selObj Is SldWorks.Component2 Then
                            Dim comp As SldWorks.Component2 = CType(selObj, SldWorks.Component2)
                            Return comp.ReferencedConfiguration
                        End If
                    End If
                End If
            End If
        Catch
        End Try
        Return targetDoc.GetActiveConfiguration().Name
    End Function

    Private Function GetPropValue(doc As SldWorks.ModelDoc2, conf As String, propName As String) As String
        Try
            ' 优先用 CustomPropertyManager.Get5
            Dim cusPropMgr As Object = doc.Extension.CustomPropertyManager(conf)
            If cusPropMgr IsNot Nothing Then
                Dim val As String = ""
                Dim wasResolved As Boolean = False
                cusPropMgr.Get5(propName, False, val, wasResolved)
                If wasResolved AndAlso Not String.IsNullOrEmpty(val) Then Return val
                ' Get5 可能失败回退到 Get4
                cusPropMgr.Get4(propName, False, val, wasResolved)
                If wasResolved AndAlso Not String.IsNullOrEmpty(val) Then Return val
            End If
            ' GetCustomInfoValue 兜底
            Dim v As Object = doc.GetCustomInfoValue(conf, propName)
            If v IsNot Nothing AndAlso Not String.IsNullOrEmpty(v.ToString()) Then Return v.ToString()
            Return ""
        Catch
            Return ""
        End Try
    End Function

    ' 切换全部属性 / 关键属性
    Private Sub BtnToggle_Click(sender As Object, e As EventArgs) Handles BtnToggle.Click
        _showKeyOnly = Not _showKeyOnly
        BtnToggle.Text = If(_showKeyOnly, "全部属性", "关键属性")
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
        ApplyOpacity()
        ApplyColorScheme(My.Settings.Form5_ColorScheme)
        RefreshProperties()
    End Sub

    Private Sub ApplyOpacity()
        Me.Opacity = My.Settings.Form5_Opacity
    End Sub

    Private Sub ApplyColorScheme(scheme As String)
        Select Case scheme
            Case "终端绿"
                Me.BackColor = Drawing.Color.Black
                Me.ForeColor = Drawing.Color.Lime
                DataGridView1.BackgroundColor = Drawing.Color.Black
                DataGridView1.DefaultCellStyle.BackColor = Drawing.Color.Black
                DataGridView1.DefaultCellStyle.ForeColor = Drawing.Color.Lime
                DataGridView1.ColumnHeadersDefaultCellStyle.BackColor = Drawing.Color.Black
                DataGridView1.ColumnHeadersDefaultCellStyle.ForeColor = Drawing.Color.Lime
            Case "白字"
                Me.BackColor = Drawing.Color.FromArgb(30, 30, 30)
                Me.ForeColor = Drawing.Color.White
                DataGridView1.BackgroundColor = Drawing.Color.FromArgb(30, 30, 30)
                DataGridView1.DefaultCellStyle.BackColor = Drawing.Color.FromArgb(30, 30, 30)
                DataGridView1.DefaultCellStyle.ForeColor = Drawing.Color.White
                DataGridView1.ColumnHeadersDefaultCellStyle.BackColor = Drawing.Color.FromArgb(30, 30, 30)
                DataGridView1.ColumnHeadersDefaultCellStyle.ForeColor = Drawing.Color.White
            Case Else
                Me.BackColor = System.Drawing.SystemColors.Control
                Me.ForeColor = System.Drawing.SystemColors.ControlText
                DataGridView1.BackgroundColor = System.Drawing.SystemColors.Control
                DataGridView1.DefaultCellStyle.BackColor = System.Drawing.SystemColors.Window
                DataGridView1.DefaultCellStyle.ForeColor = System.Drawing.SystemColors.WindowText
                DataGridView1.ColumnHeadersDefaultCellStyle.BackColor = System.Drawing.SystemColors.Control
                DataGridView1.ColumnHeadersDefaultCellStyle.ForeColor = System.Drawing.SystemColors.ControlText
        End Select
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
