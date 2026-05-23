Imports Wpf = System.Windows
Imports Microsoft.Win32

Partial Public Class DrawingSettingsWindow
    Inherits Wpf.Window

    Private Sub SelectDrawingStandard_Click(sender As Object, e As Wpf.RoutedEventArgs)
        SelectFileInto(DrawingStandardBox)
    End Sub

    Private Sub SelectSheetFormat_Click(sender As Object, e As Wpf.RoutedEventArgs)
        SelectFileInto(SheetFormatBox)
    End Sub

    Private Sub SelectFileInto(target As Wpf.Controls.TextBox)
        Dim dialog As New OpenFileDialog() With {
            .CheckFileExists = True,
            .Filter = "所有文件 (*.*)|*.*"
        }
        If dialog.ShowDialog(Me).GetValueOrDefault() Then
            target.Text = dialog.FileName
        End If
    End Sub
End Class
