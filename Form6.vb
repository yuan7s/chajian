Public Class Form6

    Private Sub Form6_Load(sender As Object, e As EventArgs) Handles MyBase.Load
        TrackBarOpacity.Value = CInt(My.Settings.Form5_Opacity * 100)
        LabelOpacity.Text = "透明度: " & TrackBarOpacity.Value & "%"
        CboColorScheme.SelectedItem = My.Settings.Form5_ColorScheme
        ChkTopMost.Checked = My.Settings.Form5_TopMost
        ChkMouseThrough.Checked = My.Settings.Form5_MouseThrough
        CboDisplayMode.SelectedItem = If(My.Settings.Form5_ShowKeyOnly, "关键属性", "全部属性")
        CboPropSource.SelectedItem = If(My.Settings.Form5_ShowCustomProps, "自定义属性", "配置属性")
        LoadKeyProperties()
    End Sub

    Private Sub TrackBarOpacity_Scroll(sender As Object, e As EventArgs) Handles TrackBarOpacity.Scroll
        SaveOpacitySetting()
    End Sub

    Private Sub CboColorScheme_SelectedIndexChanged(sender As Object, e As EventArgs) Handles CboColorScheme.SelectedIndexChanged
        My.Settings.Form5_ColorScheme = CboColorScheme.SelectedItem.ToString()
        My.Settings.Save()
        ApplyForm5SettingsToOpenWindows()
    End Sub

    Private Sub ChkTopMost_CheckedChanged(sender As Object, e As EventArgs) Handles ChkTopMost.CheckedChanged
        My.Settings.Form5_TopMost = ChkTopMost.Checked
        My.Settings.Save()
        ApplyForm5SettingsToOpenWindows()
    End Sub

    Private Sub ChkMouseThrough_CheckedChanged(sender As Object, e As EventArgs) Handles ChkMouseThrough.CheckedChanged
        My.Settings.Form5_MouseThrough = ChkMouseThrough.Checked
        My.Settings.Save()
        ApplyForm5SettingsToOpenWindows()
    End Sub

    Private Sub CboDisplayMode_SelectedIndexChanged(sender As Object, e As EventArgs) Handles CboDisplayMode.SelectedIndexChanged
        If CboDisplayMode.SelectedItem Is Nothing Then Return
        My.Settings.Form5_ShowKeyOnly = CboDisplayMode.SelectedItem.ToString() = "关键属性"
        My.Settings.Save()
        ApplyForm5SettingsToOpenWindows()
    End Sub

    Private Sub CboPropSource_SelectedIndexChanged(sender As Object, e As EventArgs) Handles CboPropSource.SelectedIndexChanged
        If CboPropSource.SelectedItem Is Nothing Then Return
        My.Settings.Form5_ShowCustomProps = CboPropSource.SelectedItem.ToString() = "自定义属性"
        My.Settings.Save()
        ApplyForm5SettingsToOpenWindows()
    End Sub

    Private Sub LoadKeyProperties()
        LstKeyProps.Items.Clear()
        For Each k In GetKeyProperties()
            LstKeyProps.Items.Add(k)
        Next
    End Sub

    Private Function GetKeyProperties() As String()
        Dim raw As String = My.Settings.Form5_KeyProperties
        If String.IsNullOrWhiteSpace(raw) Then
            Return {"物料编码", "零件图号", "文件名称", "零件类型", "下料尺寸", "版本", "设计", "出图"}
        End If
        Return raw.Split({","c}, StringSplitOptions.RemoveEmptyEntries)
    End Function

    Private Sub SaveKeyProperties()
        Dim items = LstKeyProps.Items.Cast(Of String)().ToArray()
        My.Settings.Form5_KeyProperties = String.Join(",", items)
        My.Settings.Save()
        ApplyForm5SettingsToOpenWindows()
    End Sub

    Private Sub BtnAddKey_Click(sender As Object, e As EventArgs) Handles BtnAddKey.Click
        Dim newKey As String = TxtNewKey.Text.Trim()
        If String.IsNullOrEmpty(newKey) Then Return
        If LstKeyProps.Items.Contains(newKey) Then Return
        LstKeyProps.Items.Add(newKey)
        TxtNewKey.Clear()
        SaveKeyProperties()
    End Sub

    Private Sub BtnDelKey_Click(sender As Object, e As EventArgs) Handles BtnDelKey.Click
        If LstKeyProps.SelectedIndex < 0 Then Return
        LstKeyProps.Items.RemoveAt(LstKeyProps.SelectedIndex)
        SaveKeyProperties()
    End Sub

    Private Sub BtnClose_Click(sender As Object, e As EventArgs) Handles BtnClose.Click
        Me.Close()
    End Sub

    Private Sub SaveOpacitySetting()
        LabelOpacity.Text = "透明度: " & TrackBarOpacity.Value & "%"
        My.Settings.Form5_Opacity = TrackBarOpacity.Value / 100.0
        My.Settings.Save()
        ApplyForm5SettingsToOpenWindows()
    End Sub

    Private Sub ApplyForm5SettingsToOpenWindows()
        For Each f As Form In Application.OpenForms
            Dim propForm As Form5 = TryCast(f, Form5)
            If propForm IsNot Nothing AndAlso Not propForm.IsDisposed Then
                propForm.ApplyDisplaySettings()
            End If
        Next
        PropertyOverlayWindow.ApplySettingsToOpenWindows()
    End Sub
End Class
