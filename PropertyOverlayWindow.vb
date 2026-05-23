Imports System.Runtime.InteropServices
Imports SwConst
Imports Wpf = System.Windows
Imports WpfControls = System.Windows.Controls
Imports WpfInput = System.Windows.Input
Imports WpfInterop = System.Windows.Interop
Imports WpfMedia = System.Windows.Media

Public Class PropertyOverlayWindow
    Inherits Wpf.Window

    Private Const GwlExstyle As Integer = -20
    Private Const WsExTransparent As Integer = &H20
    Private Const WmHotkey As Integer = &H312
    Private Const HotkeyIdClickThrough As Integer = 2
    Private Const ModControl As Integer = &H2
    Private Const VkF2 As Integer = &H71
    Private Shared ReadOnly HwndTopmost As New IntPtr(-1)
    Private Shared ReadOnly HwndNoTopmost As New IntPtr(-2)
    Private Const SwpNoSize As UInteger = &H1UI
    Private Const SwpNoMove As UInteger = &H2UI
    Private Const SwpNoActivate As UInteger = &H10UI

    <DllImport("user32.dll", EntryPoint:="GetWindowLong")>
    Private Shared Function GetWindowLong32(hWnd As IntPtr, nIndex As Integer) As Integer
    End Function

    <DllImport("user32.dll", EntryPoint:="SetWindowLong")>
    Private Shared Function SetWindowLong32(hWnd As IntPtr, nIndex As Integer, dwNewLong As Integer) As Integer
    End Function

    <DllImport("user32.dll")>
    Private Shared Function RegisterHotKey(hWnd As IntPtr, id As Integer, fsModifiers As Integer, vk As Integer) As Boolean
    End Function

    <DllImport("user32.dll")>
    Private Shared Function UnregisterHotKey(hWnd As IntPtr, id As Integer) As Boolean
    End Function

    <DllImport("user32.dll", SetLastError:=True)>
    Private Shared Function SetWindowPos(hWnd As IntPtr, hWndInsertAfter As IntPtr, x As Integer, y As Integer, cx As Integer, cy As Integer, flags As UInteger) As Boolean
    End Function

    Private Shared ReadOnly OpenWindows As New List(Of PropertyOverlayWindow)()

    Private WithEvents SwAppField As SldWorks.SldWorks
    Private ReadOnly Root As WpfControls.Border
    Private ReadOnly TitleText As WpfControls.TextBlock
    Private ReadOnly PropertyPanel As WpfControls.StackPanel
    Private AttachedPartDoc As SldWorks.PartDoc
    Private AttachedAsmDoc As SldWorks.AssemblyDoc
    Private AttachedDrawDoc As SldWorks.DrawingDoc
    Private AttachedDocPath As String

    Public Sub New()
        Width = 340
        SizeToContent = Wpf.SizeToContent.Height
        MinHeight = 120
        MaxHeight = 900
        WindowStyle = Wpf.WindowStyle.None
        WindowStartupLocation = Wpf.WindowStartupLocation.Manual
        AllowsTransparency = True
        ResizeMode = Wpf.ResizeMode.NoResize
        ShowInTaskbar = False
        Background = WpfMedia.Brushes.Transparent

        TitleText = New WpfControls.TextBlock() With {
            .FontSize = 17,
            .FontWeight = Wpf.FontWeights.SemiBold,
            .Foreground = WpfMedia.Brushes.Black,
            .Text = "-",
            .TextTrimming = Wpf.TextTrimming.CharacterEllipsis,
            .VerticalAlignment = Wpf.VerticalAlignment.Center
        }

        Dim closeButton As New WpfControls.Button() With {
            .Content = "X",
            .Width = 28,
            .Height = 28,
            .FontWeight = Wpf.FontWeights.Bold,
            .Foreground = WpfMedia.Brushes.Black,
            .Background = WpfMedia.Brushes.Transparent,
            .BorderThickness = New Wpf.Thickness(0),
            .Cursor = WpfInput.Cursors.Hand
        }
        AddHandler closeButton.Click, Sub() Close()

        Dim header As New WpfControls.Grid() With {.Margin = New Wpf.Thickness(18, 14, 14, 6)}
        header.ColumnDefinitions.Add(New WpfControls.ColumnDefinition() With {.Width = New Wpf.GridLength(1, Wpf.GridUnitType.Star)})
        header.ColumnDefinitions.Add(New WpfControls.ColumnDefinition() With {.Width = Wpf.GridLength.Auto})
        WpfControls.Grid.SetColumn(TitleText, 0)
        WpfControls.Grid.SetColumn(closeButton, 1)
        header.Children.Add(TitleText)
        header.Children.Add(closeButton)

        PropertyPanel = New WpfControls.StackPanel() With {.Margin = New Wpf.Thickness(18, 0, 18, 14)}

        Dim layout As New WpfControls.StackPanel()
        layout.Children.Add(header)
        layout.Children.Add(New WpfControls.Border() With {.Height = 2})
        layout.Children.Add(PropertyPanel)

        Root = New WpfControls.Border() With {
            .CornerRadius = New Wpf.CornerRadius(10),
            .Padding = New Wpf.Thickness(0),
            .Child = layout
        }
        Content = Root

        AddHandler MouseLeftButtonDown, AddressOf Window_MouseLeftButtonDown
        AddHandler SourceInitialized, AddressOf Window_SourceInitialized
        AddHandler Closed,
            Sub()
                UnregisterOverlayHotKey()
                DetachDocEvents()
                SwAppField = Nothing
                OpenWindows.Remove(Me)
            End Sub
        OpenWindows.Add(Me)

        ApplyDisplaySettings()
        SetInitialLocation()
    End Sub

    Public Property SwApp As SldWorks.SldWorks
        Get
            Return SwAppField
        End Get
        Set(value As SldWorks.SldWorks)
            If Not Object.ReferenceEquals(SwAppField, value) Then
                DetachDocEvents()
            End If
            SwAppField = value
            AttachDocEvents()
            RefreshProperties()
        End Set
    End Property

    Public Shared Sub ShowOrActivate(swApp As SldWorks.SldWorks)
        Dim window As PropertyOverlayWindow = OpenWindows.FirstOrDefault()
        If window Is Nothing Then
            window = New PropertyOverlayWindow()
        End If

        window.SwApp = swApp
        If Not window.IsVisible Then
            window.Show()
        End If

        window.BringToFront()
    End Sub

    Public Shared Sub ApplySettingsToOpenWindows()
        For Each window In OpenWindows.ToArray()
            If window IsNot Nothing Then window.ApplyDisplaySettings()
        Next
    End Sub

    Public Shared Sub UpdateOpenWindowsSwApp(swApp As SldWorks.SldWorks)
        For Each window In OpenWindows.ToArray()
            If window IsNot Nothing Then window.SwApp = swApp
        Next
    End Sub

    Private Sub BringToFront()
        Dim source = TryCast(Wpf.PresentationSource.FromVisual(Me), WpfInterop.HwndSource)
        If source Is Nothing Then Return

        Dim flags As UInteger = SwpNoMove Or SwpNoSize Or SwpNoActivate
        SetWindowPos(source.Handle, HwndTopmost, 0, 0, 0, 0, flags)
        If Not Topmost Then
            SetWindowPos(source.Handle, HwndNoTopmost, 0, 0, 0, 0, flags)
        End If
    End Sub

    Public Sub ApplyDisplaySettings()
        Topmost = My.Settings.Form5_TopMost

        Dim alpha As Byte = CByte(Math.Max(25, Math.Min(230, My.Settings.Form5_Opacity * 255)))
        Root.Background = New WpfMedia.SolidColorBrush(WpfMedia.Color.FromArgb(alpha, 255, 255, 255))
        TitleText.Foreground = WpfMedia.Brushes.Black

        Dim propertyTextBrush As WpfMedia.Brush = GetPropertyTextBrush()
        For Each row As WpfControls.Grid In PropertyPanel.Children.OfType(Of WpfControls.Grid)()
            For Each text As WpfControls.TextBlock In row.Children.OfType(Of WpfControls.TextBlock)()
                text.Foreground = propertyTextBrush
            Next
        Next

        ApplyMouseThrough()
        RefreshProperties()
    End Sub

    Private Sub RefreshProperties()
        PropertyPanel.Children.Clear()

        Dim targetDoc As SldWorks.ModelDoc2 = GetTargetDoc()
        If targetDoc Is Nothing Then
            TitleText.Text = TextByCodes(&H65E0, &H6587, &H6863)
            Return
        End If

        Dim docPath As String = targetDoc.GetPathName()
        TitleText.Text = If(String.IsNullOrEmpty(docPath), targetDoc.GetTitle(), IO.Path.GetFileNameWithoutExtension(docPath))

        Dim confString As String = ""
        Dim nameArr As Object = Nothing
        If My.Settings.Form5_ShowCustomProps Then
            nameArr = targetDoc.GetCustomInfoNames()
        Else
            Dim activeConfig = targetDoc.GetActiveConfiguration()
            If activeConfig Is Nothing Then Return
            confString = activeConfig.Name
            nameArr = targetDoc.GetCustomInfoNames2(confString)
        End If

        If My.Settings.Form5_ShowKeyOnly Then
            For Each propName In GetKeyProperties()
                AddPropertyRow(propName, GetPropValue(targetDoc, confString, propName))
            Next
            Return
        End If

        If nameArr Is Nothing Then Return
        For i As Integer = 0 To UBound(nameArr)
            Dim propName As String = nameArr(i).ToString()
            AddPropertyRow(propName, GetPropValue(targetDoc, confString, propName))
        Next
    End Sub

    Private Function GetTargetDoc() As SldWorks.ModelDoc2
        If SwAppField Is Nothing Then Return Nothing
        Dim modelDoc As SldWorks.ModelDoc2 = TryCast(SwAppField.ActiveDoc, SldWorks.ModelDoc2)
        If modelDoc Is Nothing Then Return Nothing

        Dim selMgr As SldWorks.SelectionMgr = modelDoc.SelectionManager
        If selMgr IsNot Nothing Then
            Dim selCount As Integer = 0
            Try
                selCount = selMgr.GetSelectedObjectCount2(-1)
            Catch
            End Try

            If selCount >= 1 Then
                Dim selObj As Object = selMgr.GetSelectedObject6(1, -1)
                If TypeOf selObj Is SldWorks.Component2 Then
                    Dim comp As SldWorks.Component2 = CType(selObj, SldWorks.Component2)
                    Dim refModel As SldWorks.ModelDoc2 = comp.GetModelDoc2()
                    If refModel IsNot Nothing Then Return refModel
                End If
            End If
        End If

        Return modelDoc
    End Function

    Private Function GetPropValue(doc As SldWorks.ModelDoc2, conf As String, propName As String) As String
        Try
            Return If(doc.GetCustomInfoValue(conf, propName), "")
        Catch
            Return ""
        End Try
    End Function

    Private Function GetKeyProperties() As String()
        Dim raw As String = My.Settings.Form5_KeyProperties
        If String.IsNullOrWhiteSpace(raw) Then
            Return {
                TextByCodes(&H7269, &H6599, &H7F16, &H7801),
                TextByCodes(&H96F6, &H4EF6, &H56FE, &H53F7),
                TextByCodes(&H6587, &H4EF6, &H540D, &H79F0),
                TextByCodes(&H96F6, &H4EF6, &H7C7B, &H578B),
                TextByCodes(&H4E0B, &H6599, &H5C3A, &H5BF8),
                TextByCodes(&H7248, &H672C),
                TextByCodes(&H8BBE, &H8BA1),
                TextByCodes(&H51FA, &H56FE)}
        End If

        Return raw.Split({","c}, StringSplitOptions.RemoveEmptyEntries).
            Select(Function(item) item.Trim()).
            Where(Function(item) item.Length > 0).
            ToArray()
    End Function

    Private Sub AddPropertyRow(propName As String, propValue As String)
        Dim row As New WpfControls.Grid() With {.Margin = New Wpf.Thickness(0, 1, 0, 1)}
        row.ColumnDefinitions.Add(New WpfControls.ColumnDefinition() With {.Width = New Wpf.GridLength(92)})
        row.ColumnDefinitions.Add(New WpfControls.ColumnDefinition() With {.Width = New Wpf.GridLength(1, Wpf.GridUnitType.Star)})
        AddCellText(row, propName, 0, True)
        AddCellText(row, If(propValue, ""), 1, False)
        PropertyPanel.Children.Add(row)
    End Sub

    Private Sub AddCellText(grid As WpfControls.Grid, text As String, column As Integer, isName As Boolean)
        Dim tb As New WpfControls.TextBlock() With {
            .Text = text,
            .FontFamily = New WpfMedia.FontFamily("Microsoft YaHei"),
            .FontSize = 15,
            .FontWeight = Wpf.FontWeights.Normal,
            .Foreground = GetPropertyTextBrush(),
            .TextTrimming = Wpf.TextTrimming.CharacterEllipsis,
            .VerticalAlignment = Wpf.VerticalAlignment.Center,
            .Margin = If(isName, New Wpf.Thickness(0, 1, 8, 1), New Wpf.Thickness(0, 1, 0, 1))
        }
        WpfControls.Grid.SetColumn(tb, column)
        grid.Children.Add(tb)
    End Sub

    Private Function GetPropertyTextBrush() As WpfMedia.Brush
        Dim scheme As String = If(My.Settings.Form5_ColorScheme, "")
        If scheme.Contains(TextByCodes(&H7EC8, &H7AEF, &H7EFF)) OrElse scheme.Contains("缁堢") Then
            Return New WpfMedia.SolidColorBrush(WpfMedia.Color.FromRgb(16, 185, 129))
        End If
        If scheme.Contains(TextByCodes(&H767D, &H5B57)) OrElse scheme.Contains("鐧") Then
            Return WpfMedia.Brushes.White
        End If
        Return WpfMedia.Brushes.Black
    End Function

    Private Sub Window_MouseLeftButtonDown(sender As Object, e As WpfInput.MouseButtonEventArgs)
        If Not My.Settings.Form5_MouseThrough Then DragMove()
    End Sub

    Private Sub Window_SourceInitialized(sender As Object, e As EventArgs)
        RegisterOverlayHotKey()
        ApplyMouseThrough()
    End Sub

    Private Sub ApplyMouseThrough()
        Dim source = TryCast(Wpf.PresentationSource.FromVisual(Me), WpfInterop.HwndSource)
        If source Is Nothing Then Return

        Dim exStyle As Integer = GetWindowLong32(source.Handle, GwlExstyle)
        If My.Settings.Form5_MouseThrough Then
            exStyle = exStyle Or WsExTransparent
        Else
            exStyle = exStyle And Not WsExTransparent
        End If
        SetWindowLong32(source.Handle, GwlExstyle, exStyle)
    End Sub

    Private Sub SetInitialLocation()
        Dim workArea As Wpf.Rect = Wpf.SystemParameters.WorkArea
        Left = workArea.Right - Width - 20
        Top = workArea.Top + 60
    End Sub

    Private Sub RegisterOverlayHotKey()
        Dim source = TryCast(Wpf.PresentationSource.FromVisual(Me), WpfInterop.HwndSource)
        If source Is Nothing Then Return

        source.AddHook(AddressOf WndProc)
        RegisterHotKey(source.Handle, HotkeyIdClickThrough, ModControl, VkF2)
    End Sub

    Private Sub UnregisterOverlayHotKey()
        Dim source = TryCast(Wpf.PresentationSource.FromVisual(Me), WpfInterop.HwndSource)
        If source Is Nothing Then Return

        UnregisterHotKey(source.Handle, HotkeyIdClickThrough)
        source.RemoveHook(AddressOf WndProc)
    End Sub

    Private Function WndProc(hwnd As IntPtr, msg As Integer, wParam As IntPtr, lParam As IntPtr, ByRef handled As Boolean) As IntPtr
        If msg = WmHotkey AndAlso wParam.ToInt32() = HotkeyIdClickThrough Then
            ToggleMouseThrough()
            handled = True
        End If
        Return IntPtr.Zero
    End Function

    Private Sub ToggleMouseThrough()
        My.Settings.Form5_MouseThrough = Not My.Settings.Form5_MouseThrough
        My.Settings.Save()
        ApplyMouseThrough()
    End Sub

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
        Catch
        End Try
    End Sub

    Private Function Doc_SelectionChange() As Integer
        Dispatcher.BeginInvoke(New Action(AddressOf RefreshProperties))
        Return 0
    End Function

    Private Function SwAppField_ActiveDocChangeNotify() As Integer Handles SwAppField.ActiveDocChangeNotify
        Dispatcher.BeginInvoke(New Action(AddressOf AttachDocEvents))
        Dispatcher.BeginInvoke(New Action(AddressOf RefreshProperties))
        Return 0
    End Function

    Private Function SwAppField_ActiveModelDocChangeNotify() As Integer Handles SwAppField.ActiveModelDocChangeNotify
        Dispatcher.BeginInvoke(New Action(AddressOf AttachDocEvents))
        Dispatcher.BeginInvoke(New Action(AddressOf RefreshProperties))
        Return 0
    End Function

    Private Shared Function TextByCodes(ParamArray codes As Integer()) As String
        Return New String(codes.Select(Function(code) ChrW(code)).ToArray())
    End Function
End Class
