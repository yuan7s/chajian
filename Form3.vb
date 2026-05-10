Imports System.Runtime.InteropServices
Imports SldWorks
Imports SwConst

Public Class Form3
    Private Sub CheckBox3_CheckedChanged(sender As Object, e As EventArgs) Handles CheckBox3.CheckedChanged

    End Sub

    Private Sub Form3_Load(sender As Object, e As EventArgs) Handles MyBase.Load
        Try
            ' 尝试通过 Form1 中选择的进程获取 SolidWorks 实例
            Dim swApp As Object = Form1.GetSelectedSwApp()
            If swApp Is Nothing Then
                TextBox1.Text = "无法连接到所选 SolidWorks 实例"
                Return
            End If
            Dim modelDoc As SldWorks.ModelDoc2 = CType(swApp.ActiveDoc, SldWorks.ModelDoc2)
            If modelDoc Is Nothing Then
                TextBox1.Text = ""
                Return
            End If

            Dim selMgr As SldWorks.SelectionMgr = modelDoc.SelectionManager
            Dim selCount As Integer = selMgr.GetSelectedObjectCount2(-1)
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
                TextBox1.Text = If(String.IsNullOrEmpty(filename), "", System.IO.Path.GetFileNameWithoutExtension(filename))
            Else
                Dim fullName As String = modelDoc.GetPathName()
                TextBox1.Text = If(String.IsNullOrEmpty(fullName), "", System.IO.Path.GetFileNameWithoutExtension(fullName))
            End If
        Catch ex As Exception
            TextBox1.Text = ex.Message
        End Try
    End Sub

    Private Sub TextBox1_TextChanged(sender As Object, e As EventArgs) Handles TextBox1.TextChanged

    End Sub
End Class
