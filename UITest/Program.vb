Imports FlaUI.Core
Imports FlaUI.UIA3
Imports FlaUI.Core.AutomationElements
Imports FlaUI.Core.Conditions
Imports FlaUI.Core.Definitions

Module Program

    Sub Main()
        Dim exePath As String = "..\bin\Release\外部程序.exe"
        exePath = IO.Path.GetFullPath(exePath)

        If Not IO.File.Exists(exePath) Then
            Console.WriteLine("ERROR: 未找到 " & exePath)
            Console.WriteLine("请先生成 Release: msbuild 外部程序.vbproj /t:Build /p:Configuration=Release")
            Return
        End If

        Console.WriteLine("启动: " & exePath)
        Dim app = FlaUI.Core.Application.Launch(exePath)
        System.Threading.Thread.Sleep(3000)

        Using automation = New UIA3Automation()
            Dim mainWindow = app.GetMainWindow(automation)
            If mainWindow Is Nothing Then
                Console.WriteLine("ERROR: 未找到主窗口")
                app.Kill()
                Return
            End If
            Console.WriteLine("主窗口: " & mainWindow.Title)

            ' 测试1: 查找并点击 Button19
            Console.WriteLine(vbCrLf & "=== 测试1: Button19 ===")
            Dim btn19 = FindByText(mainWindow, ControlType.Button, "配置属性")
            If btn19 IsNot Nothing Then
                Console.WriteLine("  OK 找到 Button19")
                btn19.Click()
                System.Threading.Thread.Sleep(500)

                ' 测试2: Form5
                Console.WriteLine(vbCrLf & "=== 测试2: Form5 ===")
                Dim form5 = FindWindowByTitle(automation, "配置属性")
                If form5 IsNot Nothing Then
                    Console.WriteLine("  OK Form5 已打开")

                    ' 测试3: 验证控件
                    Console.WriteLine(vbCrLf & "=== 测试3: 控件检查 ===")

                    If FindByText(form5, ControlType.Button, "关键属性") IsNot Nothing Then
                        Console.WriteLine("  OK 切换按钮存在")
                    End If
                    If FindByText(form5, ControlType.Button, "设置") IsNot Nothing Then
                        Console.WriteLine("  OK 设置按钮存在")
                    End If
                    If FindByText(form5, ControlType.Text, "X") IsNot Nothing Then
                        Console.WriteLine("  OK 关闭按钮存在")
                    End If

                    ' 测试4: 打开设置
                    Console.WriteLine(vbCrLf & "=== 测试4: 设置窗口 ===")
                    Dim btnSet = FindByText(form5, ControlType.Button, "设置")
                    If btnSet IsNot Nothing Then
                        btnSet.Click()
                        System.Threading.Thread.Sleep(500)
                        Dim form6 = FindWindowByTitle(automation, "配置属性设置")
                        If form6 IsNot Nothing Then
                            Console.WriteLine("  OK Form6 已打开")
                            FindByText(form6, ControlType.Button, "关闭")?.Click()
                            System.Threading.Thread.Sleep(200)
                            Console.WriteLine("  OK Form6 已关闭")
                        Else
                            Console.WriteLine("  WARN Form6 未找到")
                        End If
                    End If

                    ' 测试5: 关闭 Form5
                    Console.WriteLine(vbCrLf & "=== 测试5: 关闭 Form5 ===")
                    FindByText(form5, ControlType.Text, "X")?.Click()
                    System.Threading.Thread.Sleep(300)
                    Console.WriteLine("  OK Form5 已关闭")
                Else
                    Console.WriteLine("  FAIL Form5 未打开")
                End If
            Else
                Console.WriteLine("  FAIL Button19 未找到 (可用按钮列表见下)")
                Dim allBtns = mainWindow.FindAllDescendants(Function(cf) cf.ByControlType(ControlType.Button))
                For Each b In allBtns
                    Console.WriteLine("    按钮: " & b.Name)
                Next
            End If
        End Using

        Console.WriteLine(vbCrLf & "=== 测试完成 ===")
        Console.WriteLine("按任意键退出...")
        Console.ReadKey()
        app.Kill()
    End Sub

    Private Function FindByText(parent As AutomationElement, ctrlType As ControlType, text As String) As AutomationElement
        Return parent.FindFirstDescendant(
            Function(cf) cf.ByControlType(ctrlType).And(cf.ByName(text)))
    End Function

    Private Function FindWindowByTitle(automation As UIA3Automation, title As String) As AutomationElement
        Return automation.GetDesktop().FindFirstChild(
            Function(cf) cf.ByControlType(ControlType.Window).And(cf.ByName(title)))
    End Function
End Module
