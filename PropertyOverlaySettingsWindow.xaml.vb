Imports Wpf = System.Windows
Imports WpfControls = System.Windows.Controls

Partial Public Class PropertyOverlaySettingsWindow
    Inherits Wpf.Window

    Public Sub New()
        InitializeComponent()
        LoadSettings()
    End Sub

    Private Sub LoadSettings()
        OpacitySlider.Value = Math.Max(15, Math.Min(100, My.Settings.Form5_Opacity * 100))
        UpdateOpacityLabel()
        SetComboText(ColorCombo, If(String.IsNullOrWhiteSpace(My.Settings.Form5_ColorScheme), "自适应", My.Settings.Form5_ColorScheme))
        DisplayModeCombo.SelectedIndex = If(My.Settings.Form5_ShowKeyOnly, 0, 1)
        PropSourceCombo.SelectedIndex = If(My.Settings.Form5_ShowCustomProps, 1, 0)
        TopMostCheck.IsChecked = My.Settings.Form5_TopMost
        MouseThroughCheck.IsChecked = My.Settings.Form5_MouseThrough
        LoadKeyProperties()
    End Sub

    Private Sub LoadKeyProperties()
        KeyList.Items.Clear()
        For Each keyName In GetKeyProperties()
            KeyList.Items.Add(keyName)
        Next
    End Sub

    Private Function GetKeyProperties() As String()
        Dim raw As String = My.Settings.Form5_KeyProperties
        If String.IsNullOrWhiteSpace(raw) Then
            Return {"物料编码", "零件图号", "文件名称", "零件类型", "下料尺寸", "版本", "设计", "出图"}
        End If

        Return raw.Split({","c}, StringSplitOptions.RemoveEmptyEntries).
            Select(Function(item) item.Trim()).
            Where(Function(item) item.Length > 0).
            ToArray()
    End Function

    Private Sub SaveKeyProperties()
        Dim items = KeyList.Items.Cast(Of Object)().
            Select(Function(item) item.ToString()).
            Where(Function(item) Not String.IsNullOrWhiteSpace(item)).
            ToArray()
        My.Settings.Form5_KeyProperties = String.Join(",", items)
        My.Settings.Save()
        ApplyOverlaySettings()
    End Sub

    Private Sub OpacitySlider_ValueChanged(sender As Object, e As Wpf.RoutedPropertyChangedEventArgs(Of Double))
        If OpacityLabel Is Nothing Then Return
        UpdateOpacityLabel()
        My.Settings.Form5_Opacity = OpacitySlider.Value / 100.0
        My.Settings.Save()
        ApplyOverlaySettings()
    End Sub

    Private Sub Combo_SelectionChanged(sender As Object, e As WpfControls.SelectionChangedEventArgs)
        If ColorCombo Is Nothing OrElse DisplayModeCombo Is Nothing OrElse PropSourceCombo Is Nothing Then Return
        My.Settings.Form5_ColorScheme = GetComboText(ColorCombo, "自适应")
        If DisplayModeCombo.SelectedIndex >= 0 Then My.Settings.Form5_ShowKeyOnly = DisplayModeCombo.SelectedIndex = 0
        If PropSourceCombo.SelectedIndex >= 0 Then My.Settings.Form5_ShowCustomProps = PropSourceCombo.SelectedIndex = 1
        My.Settings.Save()
        ApplyOverlaySettings()
    End Sub

    Private Sub CheckBox_Changed(sender As Object, e As Wpf.RoutedEventArgs)
        If TopMostCheck Is Nothing OrElse MouseThroughCheck Is Nothing Then Return
        My.Settings.Form5_TopMost = TopMostCheck.IsChecked.GetValueOrDefault()
        My.Settings.Form5_MouseThrough = MouseThroughCheck.IsChecked.GetValueOrDefault()
        My.Settings.Save()
        ApplyOverlaySettings()
    End Sub

    Private Sub AddButton_Click(sender As Object, e As Wpf.RoutedEventArgs)
        Dim newKey = NewKeyText.Text.Trim()
        If String.IsNullOrEmpty(newKey) Then Return
        For Each item In KeyList.Items
            If String.Equals(item.ToString(), newKey, StringComparison.OrdinalIgnoreCase) Then Return
        Next

        KeyList.Items.Add(newKey)
        NewKeyText.Clear()
        SaveKeyProperties()
    End Sub

    Private Sub DeleteButton_Click(sender As Object, e As Wpf.RoutedEventArgs)
        If KeyList.SelectedIndex < 0 Then Return
        KeyList.Items.RemoveAt(KeyList.SelectedIndex)
        SaveKeyProperties()
    End Sub

    Private Sub UpButton_Click(sender As Object, e As Wpf.RoutedEventArgs)
        MoveSelectedKey(-1)
    End Sub

    Private Sub DownButton_Click(sender As Object, e As Wpf.RoutedEventArgs)
        MoveSelectedKey(1)
    End Sub

    Private Sub MoveSelectedKey(direction As Integer)
        Dim oldIndex As Integer = KeyList.SelectedIndex
        If oldIndex < 0 Then Return

        Dim newIndex As Integer = oldIndex + direction
        If newIndex < 0 OrElse newIndex >= KeyList.Items.Count Then Return

        Dim item = KeyList.Items(oldIndex)
        KeyList.Items.RemoveAt(oldIndex)
        KeyList.Items.Insert(newIndex, item)
        KeyList.SelectedIndex = newIndex
        KeyList.ScrollIntoView(item)
        SaveKeyProperties()
    End Sub

    Private Sub CloseButton_Click(sender As Object, e As Wpf.RoutedEventArgs)
        Close()
    End Sub

    Private Sub UpdateOpacityLabel()
        OpacityLabel.Text = "透明度: " & CInt(OpacitySlider.Value).ToString() & "%"
    End Sub

    Private Sub ApplyOverlaySettings()
        PropertyOverlayWindow.ApplySettingsToOpenWindows()
    End Sub

    Private Sub SetComboText(combo As WpfControls.ComboBox, text As String)
        For i As Integer = 0 To combo.Items.Count - 1
            Dim item = TryCast(combo.Items(i), WpfControls.ComboBoxItem)
            If item IsNot Nothing AndAlso String.Equals(item.Content.ToString(), text, StringComparison.OrdinalIgnoreCase) Then
                combo.SelectedIndex = i
                Return
            End If
        Next
        combo.SelectedIndex = 0
    End Sub

    Private Function GetComboText(combo As WpfControls.ComboBox, fallback As String) As String
        Dim item = TryCast(combo.SelectedItem, WpfControls.ComboBoxItem)
        If item Is Nothing OrElse item.Content Is Nothing Then Return fallback
        Return item.Content.ToString()
    End Function
End Class
