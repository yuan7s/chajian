Imports System.IO
Imports System.Diagnostics
Imports System.Windows.Forms
Imports System.Windows.Threading
Imports Wpf = System.Windows
Imports WpfControls = System.Windows.Controls
Imports WpfMedia = System.Windows.Media

Partial Public Class RenameWindow
    Inherits Wpf.Window

    Private Shared ReadOnly OpenWindows As New List(Of RenameWindow)()
    Private _client As SwAddinClient
    Private SuppressNameCheck As Boolean
    Private ReadOnly NameCheckTimer As DispatcherTimer
    Private ReadOnly DocWatchTimer As DispatcherTimer
    Private LastActiveDocKey As String
    Private LastTargetDocKey As String

    Public Sub New()
        InitializeComponent()

        NameCheckTimer = New DispatcherTimer() With {.Interval = TimeSpan.FromMilliseconds(500)}
        AddHandler NameCheckTimer.Tick, AddressOf NameCheckTimer_Tick

        DocWatchTimer = New DispatcherTimer() With {.Interval = TimeSpan.FromMilliseconds(400)}
        AddHandler DocWatchTimer.Tick, AddressOf DocWatchTimer_Tick

        AddHandler Closed, AddressOf RenameWindow_Closed
        OpenWindows.Add(Me)
    End Sub

    Public Property Client As SwAddinClient
        Get
            Return _client
        End Get
        Set(value As SwAddinClient)
            If Not Object.ReferenceEquals(_client, value) Then
                LastActiveDocKey = Nothing
                LastTargetDocKey = Nothing
                If _client IsNot Nothing Then
                    RemoveHandler _client.DocChanged, AddressOf Client_DocChanged
                    RemoveHandler _client.SelectionChanged, AddressOf Client_SelectionChanged
                End If
                _client = value
                If _client IsNot Nothing Then
                    AddHandler _client.DocChanged, AddressOf Client_DocChanged
                    AddHandler _client.SelectionChanged, AddressOf Client_SelectionChanged
                    DocWatchTimer.Start()
                Else
                    DocWatchTimer.Stop()
                End If
            End If
            SuppressNameCheck = True
            UpdateSelectionInfo()
            SuppressNameCheck = False
        End Set
    End Property

    Public Shared Sub UpdateOpenWindowsClient(client As SwAddinClient)
        For Each window In OpenWindows.ToArray()
            If window IsNot Nothing Then window.Client = client
        Next
    End Sub

    Private Sub RenameWindow_Closed(sender As Object, e As EventArgs)
        NameCheckTimer.Stop()
        DocWatchTimer.Stop()
        If _client IsNot Nothing Then
            RemoveHandler _client.DocChanged, AddressOf Client_DocChanged
            RemoveHandler _client.SelectionChanged, AddressOf Client_SelectionChanged
        End If
        _client = Nothing
        OpenWindows.Remove(Me)
    End Sub

    Private Sub Client_DocChanged(title As String, path As String)
        Dispatcher.BeginInvoke(New Action(AddressOf UpdateSelectionInfo))
    End Sub

    Private Sub Client_SelectionChanged(name As String, type As String)
        LastTargetDocKey = Nothing
        Dispatcher.BeginInvoke(New Action(AddressOf UpdateSelectionInfo))
    End Sub

    Private Async Sub DocWatchTimer_Tick(sender As Object, e As EventArgs)
        Try
            If _client Is Nothing Then Return
            Dim result = Await _client.SendCommandAsync("active-document")
            Dim dict = TryCast(result, Dictionary(Of String, Object))
            If dict Is Nothing Then Return

            Dim key As String = ""
            Dim path As String = ""
            If dict.ContainsKey("path") AndAlso dict("path") IsNot Nothing Then
                path = dict("path").ToString()
            End If
            Dim title As String = ""
            If dict.ContainsKey("title") AndAlso dict("title") IsNot Nothing Then
                title = dict("title").ToString()
            End If
            key = If(String.IsNullOrEmpty(path), title, path)

            Dim targetKey As String = ""
            If dict.ContainsKey("targetPath") Then
                targetKey = If(dict("targetPath") IsNot Nothing, dict("targetPath").ToString(), "")
            End If

            If Not String.Equals(LastActiveDocKey, key, StringComparison.OrdinalIgnoreCase) OrElse
                Not String.Equals(LastTargetDocKey, targetKey, StringComparison.OrdinalIgnoreCase) Then
                LastActiveDocKey = key
                LastTargetDocKey = targetKey
                UpdateSelectionInfo()
            End If
        Catch ex As Exception
            Debug.WriteLine("RenameWindow.DocWatchTimer_Tick failed: " & ex.Message)
        End Try
    End Sub

    Private Async Sub UpdateSelectionInfo()
        SuppressNameCheck = True
        Try
            If _client Is Nothing Then
                ClearFileInfo()
                Return
            End If

            Dim result = Await _client.SendCommandAsync("active-document")
            Dim dict = TryCast(result, Dictionary(Of String, Object))
            If dict Is Nothing Then
                ClearFileInfo()
                Return
            End If

            Dim filePath As String = ""
            If dict.ContainsKey("path") AndAlso dict("path") IsNot Nothing Then
                filePath = dict("path").ToString()
            End If
            Dim title As String = ""
            If dict.ContainsKey("title") AndAlso dict("title") IsNot Nothing Then
                title = dict("title").ToString()
            End If
            Dim docType As String = ""
            If dict.ContainsKey("type") AndAlso dict("type") IsNot Nothing Then
                docType = dict("type").ToString()
            End If

            Dim baseName As String = If(String.IsNullOrEmpty(filePath), title, Path.GetFileNameWithoutExtension(filePath))
            OldNameBox.Text = baseName
            NewNameBox.Text = baseName

            Dim ext As String = If(String.IsNullOrEmpty(filePath), DocTypeToExtensionString(docType), Path.GetExtension(filePath).ToLowerInvariant())
            OldExtText.Text = ext
            NewExtText.Text = ext
            UpdateDrawingExistsIndicator(filePath)

            Dim activePath As String = filePath
            LastActiveDocKey = If(String.IsNullOrEmpty(activePath), title, activePath)
            If dict.ContainsKey("targetPath") AndAlso dict("targetPath") IsNot Nothing Then
                LastTargetDocKey = dict("targetPath").ToString()
            Else
                LastTargetDocKey = LastActiveDocKey
            End If
        Catch ex As Exception
            Debug.WriteLine("RenameWindow.UpdateSelectionInfo failed: " & ex.Message)
        Finally
            SuppressNameCheck = False
            NameStatusBorder.Visibility = Wpf.Visibility.Collapsed
        End Try
    End Sub

    Private Sub ClearFileInfo()
        OldNameBox.Text = ""
        NewNameBox.Text = ""
        OldExtText.Text = ""
        NewExtText.Text = ""
        DrawingStatusBorder.Visibility = Wpf.Visibility.Collapsed
    End Sub

    Private Function DocTypeToExtensionString(docType As String) As String
        Select Case If(docType IsNot Nothing, docType.ToUpperInvariant(), Nothing)
            Case "PART" : Return ".sldprt"
            Case "ASSEMBLY" : Return ".sldasm"
            Case "DRAWING" : Return ".slddrw"
            Case Else : Return ""
        End Select
    End Function

    Private Sub UpdateDrawingExistsIndicator(modelPath As String)
        If String.IsNullOrEmpty(modelPath) Then
            DrawingStatusBorder.Visibility = Wpf.Visibility.Collapsed
            Return
        End If

        Try
            Dim dir As String = Path.GetDirectoryName(modelPath)
            Dim baseName As String = Path.GetFileNameWithoutExtension(modelPath)
            If String.IsNullOrEmpty(dir) OrElse String.IsNullOrEmpty(baseName) Then
                DrawingStatusBorder.Visibility = Wpf.Visibility.Collapsed
                Return
            End If

            Dim drawingPath As String = Path.Combine(dir, baseName & ".SLDDRW")
            DrawingStatusText.Text = If(File.Exists(drawingPath), "存在工程图", "无工程图")
            DrawingStatusBorder.Background = If(File.Exists(drawingPath),
                New WpfMedia.SolidColorBrush(WpfMedia.Color.FromRgb(50, 205, 50)),
                New WpfMedia.SolidColorBrush(WpfMedia.Color.FromRgb(220, 38, 38)))
            DrawingStatusBorder.Visibility = Wpf.Visibility.Visible
        Catch ex As Exception
            Debug.WriteLine("RenameWindow.UpdateDrawingExistsIndicator failed: " & ex.Message)
            DrawingStatusBorder.Visibility = Wpf.Visibility.Collapsed
        End Try
    End Sub

    Private Sub NewNameBox_TextChanged(sender As Object, e As WpfControls.TextChangedEventArgs)
        If SuppressNameCheck Then Return
        NameCheckTimer.Stop()
        NameCheckTimer.Start()
    End Sub

    Private Async Sub NameCheckTimer_Tick(sender As Object, e As EventArgs)
        NameCheckTimer.Stop()
        Await CheckNewNameConflictAsync()
    End Sub

    Private Async Function CheckNewNameConflictAsync() As Task
        Dim newName As String = NewNameBox.Text.Trim()
        If String.IsNullOrEmpty(newName) Then
            ResetNewNameIndicators()
            NameStatusBorder.Visibility = Wpf.Visibility.Collapsed
            Return
        End If

        Try
            If _client Is Nothing Then Return

            Dim args = New Dictionary(Of String, Object) From {
                {"newName", newName}
            }
            Dim result = Await _client.SendCommandAsync("check-name-conflict", args)
            Dim dict = TryCast(result, Dictionary(Of String, Object))
            If dict Is Nothing Then Return

            Dim conflict As Boolean = False
            Dim existsOpen As Boolean = False
            Dim existsFile As Boolean = False
            If dict.ContainsKey("conflict") Then
                Dim conflictVal As String = If(dict("conflict") IsNot Nothing, dict("conflict").ToString(), Nothing)
                Boolean.TryParse(conflictVal, conflict)
            End If
            If dict.ContainsKey("existsOpen") Then
                Dim openVal As String = If(dict("existsOpen") IsNot Nothing, dict("existsOpen").ToString(), Nothing)
                Boolean.TryParse(openVal, existsOpen)
            End If
            If dict.ContainsKey("existsFile") Then
                Dim fileVal As String = If(dict("existsFile") IsNot Nothing, dict("existsFile").ToString(), Nothing)
                Boolean.TryParse(fileVal, existsFile)
            End If

            If conflict Then
                NewNameBox.Background = New WpfMedia.SolidColorBrush(WpfMedia.Color.FromRgb(255, 228, 225))
                NameStatusText.Foreground = WpfMedia.Brushes.Red
                NameStatusText.Text = If(existsOpen AndAlso existsFile, "重名(打开+本地)", If(existsOpen, "重名(已打开)", "重名(本地)"))
                NameStatusBorder.Visibility = Wpf.Visibility.Visible
            Else
                NewNameBox.Background = New WpfMedia.SolidColorBrush(WpfMedia.Color.FromRgb(144, 238, 144))
                NameStatusText.Text = "可保存"
                NameStatusText.Foreground = WpfMedia.Brushes.Green
                NameStatusBorder.Visibility = Wpf.Visibility.Visible
            End If
        Catch ex As Exception
            Debug.WriteLine("RenameWindow.CheckNewNameConflictAsync failed: " & ex.Message)
        End Try
    End Function

    Private Sub ResetNewNameIndicators()
        NewNameBox.ClearValue(WpfControls.Control.BackgroundProperty)
        NameStatusText.Foreground = WpfMedia.Brushes.Green
    End Sub

    Private Sub RenameButton_Click(sender As Object, e As Wpf.RoutedEventArgs)
        SaveAsAndReplaceSelectedComponent()
    End Sub

    Private Sub SaveAsButton_Click(sender As Object, e As Wpf.RoutedEventArgs)
        SaveAsNewDocumentAndOpen()
    End Sub

    Private Async Sub SaveAsNewDocumentAndOpen()
        Dim newName As String = NewNameBox.Text.Trim()
        If String.IsNullOrEmpty(newName) Then
            MessageBox.Show("请输入新文件名", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning)
            Return
        End If

        Try
            If _client Is Nothing Then
                MessageBox.Show("未连接到 SolidWorks", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error)
                Return
            End If

            Dim args = New Dictionary(Of String, Object) From {
                {"newName", newName},
                {"fileName", If(FileNameCheck.IsChecked.GetValueOrDefault(), "true", "false")},
                {"materialCode", If(MaterialCodeCheck.IsChecked.GetValueOrDefault(), "true", "false")},
                {"partNumber", If(PartNumberCheck.IsChecked.GetValueOrDefault(), "true", "false")},
                {"version", If(VersionCheck.IsChecked.GetValueOrDefault(), "true", "false")},
                {"design", If(DesignCheck.IsChecked.GetValueOrDefault(), "true", "false")},
                {"blankSize", If(BlankSizeCheck.IsChecked.GetValueOrDefault(), "true", "false")},
                {"versionText", VersionBox.Text.Trim()},
                {"designText", DesignBox.Text.Trim()},
                {"copyDrawing", "true"}
            }
            Dim result = Await _client.SendCommandAsync("save-as-new", args)
            Dim dict = TryCast(result, Dictionary(Of String, Object))
            If dict IsNot Nothing AndAlso dict.ContainsKey("success") AndAlso CBool(dict("success")) Then
                UpdateSelectionInfo()
                ShowAutoCloseNotice("另存完成")
            Else
                Dim errMsg As String = "另存失败"
                If dict IsNot Nothing AndAlso dict.ContainsKey("error") Then errMsg = dict("error").ToString()
                MessageBox.Show(errMsg, "错误", MessageBoxButtons.OK, MessageBoxIcon.Error)
            End If
        Catch ex As Exception
            MessageBox.Show("另存失败: " & ex.Message, "错误", MessageBoxButtons.OK, MessageBoxIcon.Error)
        End Try
    End Sub

    Private Async Sub SaveAsAndReplaceSelectedComponent()
        Dim newName As String = NewNameBox.Text.Trim()
        If String.IsNullOrEmpty(newName) Then
            MessageBox.Show("请输入新文件名", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning)
            Return
        End If

        Try
            If _client Is Nothing Then
                MessageBox.Show("未连接到 SolidWorks", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error)
                Return
            End If

            Dim args = New Dictionary(Of String, Object) From {
                {"newName", newName},
                {"fileName", If(FileNameCheck.IsChecked.GetValueOrDefault(), "true", "false")},
                {"materialCode", If(MaterialCodeCheck.IsChecked.GetValueOrDefault(), "true", "false")},
                {"partNumber", If(PartNumberCheck.IsChecked.GetValueOrDefault(), "true", "false")},
                {"version", If(VersionCheck.IsChecked.GetValueOrDefault(), "true", "false")},
                {"design", If(DesignCheck.IsChecked.GetValueOrDefault(), "true", "false")},
                {"blankSize", If(BlankSizeCheck.IsChecked.GetValueOrDefault(), "true", "false")},
                {"versionText", VersionBox.Text.Trim()},
                {"designText", DesignBox.Text.Trim()},
                {"copyDrawing", "true"}
            }
            Dim result = Await _client.SendCommandAsync("rename-component", args)
            Dim dict = TryCast(result, Dictionary(Of String, Object))
            If dict IsNot Nothing AndAlso dict.ContainsKey("success") AndAlso CBool(dict("success")) Then
                UpdateSelectionInfo()
                ShowAutoCloseNotice("重命名完成")
            Else
                Dim errMsg As String = "重命名失败"
                If dict IsNot Nothing AndAlso dict.ContainsKey("error") Then errMsg = dict("error").ToString()
                MessageBox.Show(errMsg, "错误", MessageBoxButtons.OK, MessageBoxIcon.Error)
            End If
        Catch ex As Exception
            MessageBox.Show("重命名失败: " & ex.Message, "错误", MessageBoxButtons.OK, MessageBoxIcon.Error)
        End Try
    End Sub

    Private Sub ShowAutoCloseNotice(message As String, Optional title As String = "提示")
        Try
            Dim ni As New NotifyIcon()
            ni.Icon = Drawing.SystemIcons.Information
            ni.Visible = True
            ni.BalloonTipTitle = title
            ni.BalloonTipText = message
            ni.BalloonTipIcon = ToolTipIcon.Info
            ni.ShowBalloonTip(1800)

            Dim t As New Timer() With {.Interval = 2200}
            AddHandler t.Tick,
                Sub()
                    t.Stop()
                    t.Dispose()
                    ni.Visible = False
                    ni.Dispose()
                End Sub
            t.Start()
        Catch ex As Exception
            Debug.WriteLine("RenameWindow.ShowAutoCloseNotice failed: " & ex.Message)
        End Try
    End Sub
End Class
