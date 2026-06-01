Imports System.Runtime.InteropServices
Imports System.Diagnostics
Imports Wpf = System.Windows
Imports WpfControls = System.Windows.Controls
Imports WpfInterop = System.Windows.Interop
Imports WinForms = System.Windows.Forms

Public Class Form1

    Private _statusTimer As WinForms.Timer
    Private _trayIcon As WinForms.NotifyIcon
    Private _trayMenu As WinForms.ContextMenuStrip
    Private _sortProgressForm As WinForms.Form
    Private _sortProgressLabel As WinForms.Label
    Private _allowClose As Boolean
    Private _mainWindowHandle As IntPtr
    Private _client As SwAddinClient

    ' 全局热键
    Private Const ModControl As Integer = &H2
    Private Const VkF1 As Integer = &H70
    Private Const WmHotkey As Integer = &H312
    Private Const HotkeyId As Integer = 1

    <DllImport("user32.dll")>
    Private Shared Function RegisterHotKey(hWnd As IntPtr, id As Integer, fsModifiers As Integer, vk As Integer) As Boolean
    End Function

    <DllImport("user32.dll")>
    Private Shared Function UnregisterHotKey(hWnd As IntPtr, id As Integer) As Boolean
    End Function

    ' 另存为 DWG
    Private Async Sub Button1_Click(sender As Object, e As EventArgs)
        Try
            Await _client.SendCommandAsync("save-dwg")
        Catch ex As Exception
            MsgBox("保存 DWG 失败: " & ex.Message)
        End Try
    End Sub

    Private Async Sub Form1_Load(sender As Object, e As Wpf.RoutedEventArgs)
        TopMost = True
        Button12.Content = "取消置顶"

        ' 初始化托盘图标
        _trayMenu = New WinForms.ContextMenuStrip()
        _trayMenu.Items.Add("显示主窗口", Nothing, AddressOf TrayShow_Click)
        _trayMenu.Items.Add("-")
        _trayMenu.Items.Add("退出", Nothing, AddressOf TrayExit_Click)

        _trayIcon = New WinForms.NotifyIcon() With {
            .Icon = Drawing.SystemIcons.Application,
            .Text = "外部程序",
            .Visible = True,
            .ContextMenuStrip = _trayMenu
        }
        AddHandler _trayIcon.DoubleClick, AddressOf TrayShow_Click

        ' Connect to addin via HTTP/WS
        _client = New SwAddinClient()
        AddHandler _client.DocChanged, AddressOf OnAddinDocChanged
        AddHandler _client.SelectionChanged, AddressOf OnAddinSelectionChanged
        AddHandler _client.Disconnected, AddressOf OnAddinDisconnected
        Await _client.ConnectAsync()

        _statusTimer = New WinForms.Timer() With {.Interval = 5000}
        AddHandler _statusTimer.Tick, AddressOf StatusTimer_Tick
        _statusTimer.Start()
    End Sub

    Private Sub Form1_SourceInitialized(sender As Object, e As EventArgs)
        _mainWindowHandle = New WpfInterop.WindowInteropHelper(Me).Handle
        Dim source = TryCast(Wpf.PresentationSource.FromVisual(Me), WpfInterop.HwndSource)
        If source IsNot Nothing Then source.AddHook(AddressOf WndProc)
        ' 窗口句柄就绪后再注册热键
        If Not RegisterHotKey(_mainWindowHandle, HotkeyId, ModControl, VkF1) Then
            MsgBox("快捷键 Ctrl+F1 注册失败，可能已被其他程序占用。")
        End If
    End Sub

    Private Async Sub StatusTimer_Tick(sender As Object, e As EventArgs)
        Await UpdateStatusBar()
    End Sub

    Private Sub Form1_Closing(sender As Object, e As System.ComponentModel.CancelEventArgs)
        ' 拦截关闭按钮，隐藏到托盘
        If Not _allowClose Then
            e.Cancel = True
            Me.Hide()
        End If
    End Sub

    Private Sub Form1_Closed(sender As Object, e As EventArgs)
        ' 清理热键
        If _mainWindowHandle <> IntPtr.Zero Then UnregisterHotKey(_mainWindowHandle, HotkeyId)
        If _client IsNot Nothing Then _client.Dispose()
        _client = Nothing
        If _trayIcon IsNot Nothing Then
            _trayIcon.Visible = False
            _trayIcon.Dispose()
            _trayIcon = Nothing
        End If
        If _trayMenu IsNot Nothing Then
            _trayMenu.Dispose()
            _trayMenu = Nothing
        End If
        If _statusTimer IsNot Nothing Then
            _statusTimer.Stop()
            _statusTimer.Dispose()
            _statusTimer = Nothing
        End If
    End Sub

    Private Async Function UpdateStatusBar() As Task
        If _client IsNot Nothing Then
            Try
                Dim info = Await _client.SendCommandAsync("active-document")
                If info IsNot Nothing Then
                    Dim dict = TryCast(info, Dictionary(Of String, Object))
                    If dict IsNot Nothing Then
                        Dim path As String = ""
                        If dict.ContainsKey("path") AndAlso dict("path") IsNot Nothing Then
                            path = dict("path").ToString()
                        End If
                        Dim title As String = ""
                        If dict.ContainsKey("title") AndAlso dict("title") IsNot Nothing Then
                            title = dict("title").ToString()
                        End If
                        If Not String.IsNullOrWhiteSpace(path) Then
                            fileNameLabel.Text = IO.Path.GetFileNameWithoutExtension(path)
                        ElseIf Not String.IsNullOrWhiteSpace(title) Then
                            fileNameLabel.Text = title
                        Else
                            fileNameLabel.Text = "无文档"
                        End If
                    End If
                End If
            Catch
                fileNameLabel.Text = "未连接"
            End Try
        End If
    End Function

    ' 打开文件位置
    Private Async Sub Button2_Click(sender As Object, e As EventArgs)
        Try
            Dim result = Await _client.SendCommandAsync("open-file-location")
            Dim dict = TryCast(result, Dictionary(Of String, Object))
            If dict IsNot Nothing AndAlso dict.ContainsKey("path") Then
                OpenPathInExplorer(dict("path").ToString())
            End If
        Catch ex As Exception
            MsgBox("获取路径失败: " & ex.Message)
        End Try
    End Sub

    Private Sub OpenPathInExplorer(targetPath As String)
        If String.IsNullOrWhiteSpace(targetPath) Then
            MsgBox("当前文件还没有保存，无法打开目录。")
            Return
        End If

        If System.IO.File.Exists(targetPath) Then
            Process.Start(New ProcessStartInfo("explorer.exe", "/select,""" & targetPath & """") With {.UseShellExecute = True})
            Return
        End If

        Dim directoryPath As String = If(System.IO.Directory.Exists(targetPath), targetPath, System.IO.Path.GetDirectoryName(targetPath))
        If String.IsNullOrWhiteSpace(directoryPath) OrElse Not System.IO.Directory.Exists(directoryPath) Then
            MsgBox("找不到文件所在目录：" & targetPath)
            Return
        End If

        Process.Start(New ProcessStartInfo("explorer.exe", """" & directoryPath & """") With {.UseShellExecute = True})
    End Sub

    ' 旋转视图
    Private Async Sub Button4_Click(sender As Object, e As EventArgs)
        Try
            Await _client.SendCommandAsync("rotate-drawing-view")
        Catch ex As Exception
            MsgBox("旋转视图失败: " & ex.Message)
        End Try
    End Sub

    ' 设置绘图标准为 ISO
    Private Async Sub Button5_Click(sender As Object, e As EventArgs)
        Try
            Await _client.SendCommandAsync("set-iso-standard")
        Catch ex As Exception
            MsgBox("设置 ISO 标准失败: " & ex.Message)
        End Try
    End Sub

    ' 另存为 PDF
    Private Async Sub Button8_Click(sender As Object, e As EventArgs)
        Try
            Await _client.SendCommandAsync("save-pdf")
        Catch ex As Exception
            MsgBox("保存 PDF 失败: " & ex.Message)
        End Try
    End Sub

    ' 同步物料编码/零件图号/文件名称属性
    Private Async Sub Button9_Click(sender As Object, e As EventArgs)
        Try
            Await _client.SendCommandAsync("sync-coding-props")
        Catch ex As Exception
            MsgBox("同步属性失败: " & ex.Message)
        End Try
    End Sub

    ' FeatureManager 显示设置（隐藏配置/显示状态名称，递归）
    Private Async Sub Button11_Click(sender As Object, e As EventArgs)
        Try
            Await _client.SendCommandAsync("hide-config-names")
            ShowAutoCloseNotice("完成")
        Catch ex As Exception
            MsgBox("操作失败: " & ex.Message)
        End Try
    End Sub

    ' 切换窗口置顶
    Private Sub Button12_Click(sender As Object, e As EventArgs)
        TopMost = Not TopMost

        ' 根据状态更新按钮文本
        If TopMost Then
            Button12.Content = "取消置顶"
        Else
            Button12.Content = "置顶"
        End If
    End Sub

    ' 设计树排序（文件夹分组 + 递归子装配体）
    Private Async Sub Button13_Click(sender As Object, e As EventArgs)
        ShowSortProgress("正在准备装配体排序...")
        Try
            Await _client.SendCommandAsync("sort-components")
            ShowAutoCloseNotice("装配体排序完成")
        Catch ex As Exception
            MessageBox.Show("装配体排序失败: " & ex.Message, "错误", MessageBoxButtons.OK, MessageBoxIcon.Error)
        Finally
            CloseSortProgress()
        End Try
    End Sub

    ' 强制结束所有 SolidWorks 进程
    Private Async Sub Button10_Click(sender As Object, e As EventArgs)
        Try
            Await _client.SendCommandAsync("close-sw")
        Catch ex As Exception
            MsgBox("关闭 SolidWorks 失败：" & ex.Message)
        End Try
    End Sub

    ' 删除自定义属性（递归子件）
    Private Async Sub Button6_Click_1(sender As Object, e As EventArgs)
        Try
            Dim result = Await _client.SendCommandAsync("active-document")
            Dim dict = TryCast(result, Dictionary(Of String, Object))
            Dim docName As String = "文档"
            If dict IsNot Nothing AndAlso dict.ContainsKey("title") Then
                docName = dict("title").ToString()
            End If

            Dim wasTopMost As Boolean = TopMost
            TopMost = True
            Dim confirmResult As WinForms.DialogResult = WinForms.MessageBox.Show(
                "将删除【" & docName & "】及其所有子件的自定义属性，确定继续？",
                "确认删除自定义属性",
                WinForms.MessageBoxButtons.OKCancel,
                WinForms.MessageBoxIcon.Warning)
            TopMost = wasTopMost
            If confirmResult <> WinForms.DialogResult.OK Then Exit Sub

            Await _client.SendCommandAsync("delete-custom-props")
            ShowAutoCloseNotice("完成")
        Catch ex As Exception
            MsgBox("删除自定义属性失败: " & ex.Message)
        End Try
    End Sub

    ' 打开绘图标准设置
    Private Sub Button14_Click_1(sender As Object, e As EventArgs)
        Dim settingsWindow As New DrawingSettingsWindow()
        Dim helper As New System.Windows.Interop.WindowInteropHelper(settingsWindow)
        helper.Owner = _mainWindowHandle
        settingsWindow.Show()
    End Sub

    ' 全局热键处理
    Private Function WndProc(hwnd As IntPtr, msg As Integer, wParam As IntPtr, lParam As IntPtr, ByRef handled As Boolean) As IntPtr
        If msg = WmHotkey AndAlso wParam.ToInt32() = HotkeyId Then
            ToggleVisibility()
            handled = True
        End If
        Return IntPtr.Zero
    End Function

    Private Sub ToggleVisibility()
        If Me.IsVisible Then
            Me.Hide()
        Else
            Me.Show()
            Me.WindowState = Wpf.WindowState.Normal
            Me.Activate()
        End If
    End Sub

    ' 托盘菜单 — 显示主窗口
    Private Sub TrayShow_Click(sender As Object, e As EventArgs)
        Me.Show()
        Me.WindowState = Wpf.WindowState.Normal
        Me.Activate()
    End Sub

    ' 托盘菜单 — 退出
    Private Sub TrayExit_Click(sender As Object, e As EventArgs)
        _allowClose = True
        Wpf.Application.Current.Shutdown()
    End Sub

    ' 打开重命名工具
    Private Sub Button16_Click(sender As Object, e As EventArgs)
        Dim renameWindow As New RenameWindow()
        renameWindow.Client = _client
        renameWindow.Show()
    End Sub

    ' 删除配置属性（递归子件）
    Private Async Sub Button17_Click(sender As Object, e As EventArgs)
        Try
            Dim result = Await _client.SendCommandAsync("active-document")
            Dim dict = TryCast(result, Dictionary(Of String, Object))
            Dim docName As String = "文档"
            If dict IsNot Nothing AndAlso dict.ContainsKey("title") Then
                docName = dict("title").ToString()
            End If

            Dim wasTopMost As Boolean = Me.TopMost
            Me.TopMost = True
            Dim confirmResult As WinForms.DialogResult = WinForms.MessageBox.Show(
                "将删除【" & docName & "】及其所有子件的配置属性，确定继续？",
                "确认删除配置属性",
                WinForms.MessageBoxButtons.OKCancel,
                WinForms.MessageBoxIcon.Warning)
            Me.TopMost = wasTopMost
            If confirmResult <> WinForms.DialogResult.OK Then Exit Sub

            Await _client.SendCommandAsync("delete-config-props")
            ShowAutoCloseNotice("完成")
        Catch ex As Exception
            MsgBox("删除配置属性失败: " & ex.Message)
        End Try
    End Sub

    ' 打开编码整理工具
    Private Sub Button18_Click(sender As Object, e As EventArgs)
        Dim cleanupWindow As New CodingCleanupWindow()
        cleanupWindow.Client = _client
        Dim helper As New System.Windows.Interop.WindowInteropHelper(cleanupWindow)
        helper.Owner = _mainWindowHandle
        cleanupWindow.Show()
    End Sub

    ' 打开配置属性透明窗口
    Private Sub Button19_Click(sender As Object, e As EventArgs)
        PropertyOverlayWindow.ShowOrActivate(_client)
    End Sub

    Private Sub Button20_Click(sender As Object, e As EventArgs)
        Dim settingsWindow As New PropertyOverlaySettingsWindow()
        Dim helper As New System.Windows.Interop.WindowInteropHelper(settingsWindow)
        helper.Owner = _mainWindowHandle
        settingsWindow.ShowDialog()
    End Sub

    ' Push event handlers
    Private Sub OnAddinDocChanged(title As String, path As String)
        Dispatcher.Invoke(Sub()
            If String.IsNullOrWhiteSpace(path) Then
                fileNameLabel.Text = If(String.IsNullOrWhiteSpace(title), "无文档", title)
            Else
                fileNameLabel.Text = IO.Path.GetFileNameWithoutExtension(path)
            End If
        End Sub)
    End Sub

    Private Sub OnAddinSelectionChanged(name As String, type As String)
        ' Optional: display selection info
    End Sub

    Private Sub OnAddinDisconnected()
        Dispatcher.Invoke(Sub()
            fileNameLabel.Text = "插件断开"
        End Sub)
    End Sub

    Private Sub ShowAutoCloseNotice(message As String, Optional noticeTitle As String = "提示")
        Try
            If _trayIcon IsNot Nothing Then
                _trayIcon.BalloonTipTitle = noticeTitle
                _trayIcon.BalloonTipText = message
                _trayIcon.BalloonTipIcon = WinForms.ToolTipIcon.Info
                _trayIcon.ShowBalloonTip(1800)
                Return
            End If
        Catch
        End Try

        Wpf.MessageBox.Show(Me, message, noticeTitle, Wpf.MessageBoxButton.OK, Wpf.MessageBoxImage.Information)
    End Sub

    Private Sub ShowSortProgress(message As String)
        CloseSortProgress()

        _sortProgressLabel = New WinForms.Label() With {
            .AutoSize = False,
            .Dock = WinForms.DockStyle.Fill,
            .Font = New Drawing.Font("微软雅黑", 10.0!, Drawing.FontStyle.Bold),
            .ForeColor = Drawing.Color.FromArgb(45, 55, 72),
            .TextAlign = Drawing.ContentAlignment.MiddleCenter,
            .Text = message
        }

        _sortProgressForm = New WinForms.Form() With {
            .AutoScaleMode = AutoScaleMode.None,
            .BackColor = Color.White,
            .ClientSize = New Size(280, 76),
            .ControlBox = False,
            .FormBorderStyle = FormBorderStyle.FixedSingle,
            .MaximizeBox = False,
            .MinimizeBox = False,
            .ShowIcon = False,
            .ShowInTaskbar = False,
            .StartPosition = FormStartPosition.Manual,
            .Text = "排序中",
            .TopMost = True
        }

        Dim x As Integer = Me.Left + Math.Max(0, (Me.Width - _sortProgressForm.Width) \ 2)
        Dim y As Integer = Me.Top + Math.Max(0, (Me.Height - _sortProgressForm.Height) \ 2)
        _sortProgressForm.Location = New Point(x, y)
        _sortProgressForm.Controls.Add(_sortProgressLabel)
        _sortProgressForm.Show()
        _sortProgressForm.Refresh()
        Application.DoEvents()
    End Sub

    Private Sub UpdateSortProgress(message As String)
        If _sortProgressForm Is Nothing OrElse _sortProgressForm.IsDisposed Then Return
        If _sortProgressLabel Is Nothing OrElse _sortProgressLabel.IsDisposed Then Return

        _sortProgressLabel.Text = message
        _sortProgressLabel.Refresh()
        _sortProgressForm.Refresh()
        Application.DoEvents()
    End Sub

    Private Sub CloseSortProgress()
        Try
            If _sortProgressForm IsNot Nothing Then
                If Not _sortProgressForm.IsDisposed Then _sortProgressForm.Close()
                _sortProgressForm.Dispose()
            End If
        Catch
        Finally
            _sortProgressForm = Nothing
            _sortProgressLabel = Nothing
        End Try
    End Sub
End Class
