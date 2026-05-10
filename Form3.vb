Imports System.Runtime.InteropServices
Imports SldWorks
Imports SwConst

Public Class Form3
    Private WithEvents selectionTimer As System.Windows.Forms.Timer

    Private Sub CheckBox3_CheckedChanged(sender As Object, e As EventArgs) Handles CheckBox3.CheckedChanged

    End Sub

    Private Sub Form3_Load(sender As Object, e As EventArgs) Handles MyBase.Load
        Me.TopMost = True
        ' 使用定时器定期轮询 SolidWorks 的选择状态并更新 TextBox1
        selectionTimer = New System.Windows.Forms.Timer()
        selectionTimer.Interval = 1000 ' 1 秒
        selectionTimer.Start()
        ' 立即执行一次更新
        UpdateSelectionInfo()
    End Sub

    Private Sub Form3_FormClosing(sender As Object, e As FormClosingEventArgs) Handles MyBase.FormClosing
        If selectionTimer IsNot Nothing Then
            selectionTimer.Stop()
            selectionTimer.Dispose()
            selectionTimer = Nothing
        End If
    End Sub

    Private Sub selectionTimer_Tick(sender As Object, e As EventArgs) Handles selectionTimer.Tick
        UpdateSelectionInfo()
    End Sub

    ''' <summary>
    ''' 从选定的 SolidWorks 实例读取当前选择，并更新 TextBox1（仅当有选择时）和 Label3/Label4 扩展名。
    ''' 如果未选择任何对象，则保持 TextBox1 为空。
    ''' </summary>
    Private Sub UpdateSelectionInfo()
        Try
            Dim swApp As Object = Form1.GetSelectedSwApp()
            If swApp Is Nothing Then
                ' 无法连接到实例，保持为空
                RichTextBox1.Text = ""
                Label3.Text = ""
                Label4.Text = ""
                Return
            End If

            Dim modelDoc As SldWorks.ModelDoc2 = CType(swApp.ActiveDoc, SldWorks.ModelDoc2)
            If modelDoc Is Nothing Then
                RichTextBox1.Text = ""
                Label3.Text = ""
                Label4.Text = ""
                Return
            End If

            ' 更新活动文档扩展名到 Label4
            Dim activePath As String = modelDoc.GetPathName()
            Dim activeExt As String = ""
            If Not String.IsNullOrEmpty(activePath) Then
                activeExt = System.IO.Path.GetExtension(activePath)
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

                RichTextBox1.Text = If(String.IsNullOrEmpty(filename), "", System.IO.Path.GetFileNameWithoutExtension(filename))
                RichTextBox2.Text = If(String.IsNullOrEmpty(filename), "", System.IO.Path.GetFileNameWithoutExtension(filename))

                ' 填充扩展名到 Label3（选中项）
                Dim selExt As String = ""
                If Not String.IsNullOrEmpty(filename) Then
                    selExt = System.IO.Path.GetExtension(filename)
                    If Not String.IsNullOrEmpty(selExt) Then selExt = selExt.TrimStart("."c).ToLowerInvariant()
                End If
                Label3.Text = selExt
                Label4.Text = selExt
            Else
                ' 未选中任何对象，保持 TextBox1 为空
                RichTextBox1.Text = ""
                Label3.Text = ""
            End If
        Catch
            ' 忽略异常，保持现有显示
        End Try
    End Sub

    Private Sub TextBox1_TextChanged(sender As Object, e As EventArgs)

    End Sub

    Private Sub Label3_Click(sender As Object, e As EventArgs) Handles Label3.Click

    End Sub

    Private Sub Label4_Click(sender As Object, e As EventArgs) Handles Label4.Click

    End Sub

    Private Sub TextBox2_TextChanged(sender As Object, e As EventArgs)

    End Sub

    Private Sub RichTextBox1_TextChanged(sender As Object, e As EventArgs) Handles RichTextBox1.TextChanged

    End Sub
End Class
