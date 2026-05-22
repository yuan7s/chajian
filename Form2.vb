Public Class Form2
    'Private Sub ButtonBack_Click(sender As Object, e As EventArgs) Handles ButtonBack.Click
    '    ' 关闭当前窗体，显示之前的窗体
    '    Me.Close()
    'End Sub

    Private Sub Form2_FormClosed(sender As Object, e As FormClosedEventArgs) Handles MyBase.FormClosed
        ' 当 Form2 关闭时，重新显示 Form1
        Dim f1 As Form = Application.OpenForms("Form1")
        If f1 IsNot Nothing Then
            f1.Show()
        End If
    End Sub

    Private Sub Form2_Load(sender As Object, e As EventArgs) Handles MyBase.Load

    End Sub

    Private Sub Label1_Click(sender As Object, e As EventArgs) Handles Label1.Click

    End Sub
End Class
