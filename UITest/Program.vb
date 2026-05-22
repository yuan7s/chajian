Imports FlaUI.Core
Imports FlaUI.UIA3
Imports FlaUI.Core.AutomationElements
Imports FlaUI.Core.Conditions
Imports FlaUI.Core.Definitions

Module Program

    Sub Main()
        Dim exePath As String = IO.Path.GetFullPath("..\bin\Release\外部程序.exe")
        If Not IO.File.Exists(exePath) Then
            Console.WriteLine("ERROR: 未找到 " & exePath)
            Return
        End If

        Console.WriteLine("启动: " & exePath)
        Dim app = FlaUI.Core.Application.Launch(exePath)
        System.Threading.Thread.Sleep(4000)

        Using automation = New UIA3Automation()
            Dim mainWin = app.GetMainWindow(automation)
            If mainWin Is Nothing Then
                Console.WriteLine("ERROR: 未找到主窗口")
                app.Kill()
                Return
            End If
            Console.WriteLine("主窗口: " & mainWin.Title)

            ' 列出 Form1 上所有按钮
            Console.WriteLine(vbCrLf & "--- Form1 按钮 ---")
            Dim allBtns = mainWin.FindAllDescendants(Function(cf) cf.ByControlType(ControlType.Button))
            For Each b In allBtns
                Console.WriteLine("  " & b.Name)
            Next

            ' 点击 Button19
            Dim btn19 = FindBtn(mainWin, "配置属性")
            If btn19 Is Nothing Then
                Console.WriteLine("FAIL: Button19 未找到")
                app.Kill()
                Return
            End If
            Console.WriteLine(vbCrLf & "点击 Button19...")
            btn19.Click()
            System.Threading.Thread.Sleep(800)

            ' 处理可能弹出的 SW 连接提示
            DismissMessageBox(automation)

            ' 查找 Form5
            Dim form5 = FindWin(automation, "配置属性")
            If form5 Is Nothing Then
                ' 重试一次
                DismissMessageBox(automation)
                btn19.Click()
                System.Threading.Thread.Sleep(1000)
                DismissMessageBox(automation)
                form5 = FindWin(automation, "配置属性")
            End If

            If form5 Is Nothing Then
                Console.WriteLine("FAIL: Form5 仍然未找到")
                ' 列出所有窗口
                DumpAllWindows(automation)
                app.Kill()
                Return
            End If

            Console.WriteLine("OK Form5: " & form5.Name)

            ' 检查 Form5 控件
            Console.WriteLine(vbCrLf & "--- Form5 控件 ---")
            Dim f5Btns = form5.FindAllDescendants(Function(cf) cf.ByControlType(ControlType.Button))
            For Each b In f5Btns
                Console.WriteLine("  [按钮] " & b.Name)
            Next
            Dim f5Texts = form5.FindAllDescendants(Function(cf) cf.ByControlType(ControlType.Text))
            For Each t In f5Texts
                Console.WriteLine("  [文本] " & t.Name)
            Next

            ' 点击设置
            Dim btnSet = FindBtn(form5, "设置")
            If btnSet IsNot Nothing Then
                Console.WriteLine(vbCrLf & "点击设置...")
                btnSet.Click()
                System.Threading.Thread.Sleep(800)

                ' 再次列出所有窗口
                Console.WriteLine("--- 所有窗口 ---")
                DumpAllWindows(automation)

                Dim form6 = FindWin(automation, "设置")
                If form6 IsNot Nothing Then
                    Console.WriteLine("OK Form6: " & form6.Name)
                    FindBtn(form6, "关闭")?.Click()
                    System.Threading.Thread.Sleep(300)
                    Console.WriteLine("OK Form6 已关闭")
                Else
                    Console.WriteLine("WARN Form6 未出现")
                End If
            End If

            ' 关闭 Form5
            Console.WriteLine(vbCrLf & "关闭 Form5...")
            Dim lblX = form5.FindFirstDescendant(Function(cf) cf.ByControlType(ControlType.Text).And(cf.ByName("X")))
            lblX?.Click()
            System.Threading.Thread.Sleep(300)
            Console.WriteLine("OK")
        End Using

        Console.WriteLine(vbCrLf & "=== 测试通过 ===")
        Console.ReadKey()
        app.Kill()
    End Sub

    Private Function FindBtn(parent As AutomationElement, text As String) As AutomationElement
        Return parent.FindFirstDescendant(
            Function(cf) cf.ByControlType(ControlType.Button).And(cf.ByName(text)))
    End Function

    Private Function FindWin(automation As UIA3Automation, titlePart As String) As AutomationElement
        ' 搜索所有后代窗口（非直接子窗口），处理无边框/不在任务栏的窗口
        Dim root = automation.GetDesktop()
        Dim all = root.FindAllDescendants(Function(cf) cf.ByControlType(ControlType.Window))
        For Each w In all
            If Not String.IsNullOrEmpty(w.Name) AndAlso w.Name.Contains(titlePart) Then Return w
        Next
        ' 也查 Pane 类型控件（某些无边框窗口可能被识别为 Pane）
        all = root.FindAllDescendants(Function(cf) cf.ByControlType(ControlType.Pane))
        For Each w In all
            If Not String.IsNullOrEmpty(w.Name) AndAlso w.Name.Contains(titlePart) Then Return w
        Next
        Return Nothing
    End Function

    Private Sub DismissMessageBox(automation As UIA3Automation)
        ' 关闭弹出的消息框（如 SW 未连接提示）
        Dim mb = automation.GetDesktop().FindFirstChild(
            Function(cf) cf.ByControlType(ControlType.Window).And(cf.ByName("插件")))
        If mb Is Nothing Then
            mb = automation.GetDesktop().FindFirstChild(
                Function(cf) cf.ByControlType(ControlType.Window).And(cf.ByName("错误")))
        End If
        If mb Is Nothing Then Return
        Dim ok = mb.FindFirstDescendant(Function(cf) cf.ByControlType(ControlType.Button).And(cf.ByName("确定")))
        ok?.Click()
        System.Threading.Thread.Sleep(300)
    End Sub

    Private Sub DumpAllWindows(automation As UIA3Automation)
        Dim all = automation.GetDesktop().FindAllChildren(Function(cf) cf.ByControlType(ControlType.Window))
        For Each w In all
            Console.WriteLine("  窗口: " & w.Name)
        Next
    End Sub
End Module
