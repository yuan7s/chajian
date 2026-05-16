# Form1 状态栏重构 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 将 Form1 底部的 ComboBox1、Label1、Label2、Label3、Button15 重构为 StatusStrip 状态栏。

**Architecture:** 用一个 StatusStrip（Dock=Bottom）替代 5 个独立控件，内含 ToolStripButton、ToolStripComboBox 和 4 个 ToolStripStatusLabel。StatusStrip 自动停靠底部，所有子项从左到右排列。

**Tech Stack:** VB.NET, .NET Framework 4.8, WinForms, SolidWorks COM API

---

### Task 1: 更新 SwProcessInfo.ToString() 为 `PID: 文档名` 格式

**Files:**
- Modify: `Form1.vb:1187-1193`

- [ ] **Step 1: 修改 ToString 方法**

将 `SwProcessInfo.ToString()` 返回值从 `Title` 改为 `ProcessId & ": " & Title`：

```vb
Private Class SwProcessInfo
    Public Property Title As String
    Public Property ProcessId As Integer
    Public Overrides Function ToString() As String
        Return ProcessId.ToString() & ": " & Title
    End Function
End Class
```

- [ ] **Step 2: 编译验证**

```bash
"C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe" "C:\Users\yuan7\RiderProjects\chajian2\外部程序.vbproj" /t:Build /p:Configuration=Debug
```

Expected: Build succeeded.

- [ ] **Step 3: 提交**

```bash
git add "C:\Users\yuan7\RiderProjects\chajian2\Form1.vb"
git commit -m "feat: update SwProcessInfo.ToString to show PID: Title

Co-Authored-By: Claude Opus 4.7 <noreply@anthropic.com>"
```

---

### Task 2: 更新 Form1.Designer.vb — 添加 StatusStrip，移除旧控件

**Files:**
- Modify: `Form1.Designer.vb`

- [ ] **Step 1: 移除旧控件声明**

删除 `Form1.Designer.vb` 末尾 Friend WithEvents 声明中的以下行：

```
Friend WithEvents ComboBox1 As ComboBox
Friend WithEvents Button15 As Button
Friend WithEvents Label1 As Label
Friend WithEvents Label2 As Label
Friend WithEvents Label3 As Label
```

替换为：

```vb
Friend WithEvents StatusStrip1 As StatusStrip
Friend WithEvents refreshBtn As ToolStripButton
Friend WithEvents swProcessCombo As ToolStripComboBox
Friend WithEvents separator1 As ToolStripSeparator
Friend WithEvents filePrefixLabel As ToolStripStatusLabel
Friend WithEvents fileNameLabel As ToolStripStatusLabel
Friend WithEvents separator2 As ToolStripSeparator
Friend WithEvents dirPrefixLabel As ToolStripStatusLabel
Friend WithEvents dirPathLabel As ToolStripStatusLabel
```

- [ ] **Step 2: 在 InitializeComponent 开头添加 StatusStrip 子项创建**

在 `InitializeComponent` 中，紧跟 `Me.Button9 = New ...` 等按钮创建行之后，在 GroupBox 创建之前，添加所有 StatusStrip 子项的实例化：

```vb
Me.StatusStrip1 = New System.Windows.Forms.StatusStrip()
Me.refreshBtn = New System.Windows.Forms.ToolStripButton()
Me.swProcessCombo = New System.Windows.Forms.ToolStripComboBox()
Me.separator1 = New System.Windows.Forms.ToolStripSeparator()
Me.filePrefixLabel = New System.Windows.Forms.ToolStripStatusLabel()
Me.fileNameLabel = New System.Windows.Forms.ToolStripStatusLabel()
Me.separator2 = New System.Windows.Forms.ToolStripSeparator()
Me.dirPrefixLabel = New System.Windows.Forms.ToolStripStatusLabel()
Me.dirPathLabel = New System.Windows.Forms.ToolStripStatusLabel()
Me.StatusStrip1.SuspendLayout()
```

- [ ] **Step 3: 在 InitializeComponent 末尾添加 StatusStrip 配置**

在所有控件属性设置之后，`Me.ResumeLayout(False)` 之前，添加：

```vb
'
'StatusStrip1
'
Me.StatusStrip1.Items.AddRange(New System.Windows.Forms.ToolStripItem() {Me.refreshBtn, Me.swProcessCombo, Me.separator1, Me.filePrefixLabel, Me.fileNameLabel, Me.separator2, Me.dirPrefixLabel, Me.dirPathLabel})
Me.StatusStrip1.Location = New System.Drawing.Point(0, 454)
Me.StatusStrip1.Name = "StatusStrip1"
Me.StatusStrip1.Size = New System.Drawing.Size(1264, 26)
Me.StatusStrip1.TabIndex = 16
Me.StatusStrip1.Text = "StatusStrip1"
'
'refreshBtn
'
Me.refreshBtn.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Text
Me.refreshBtn.Name = "refreshBtn"
Me.refreshBtn.Size = New System.Drawing.Size(36, 23)
Me.refreshBtn.Text = "刷新"
'
'swProcessCombo
'
Me.swProcessCombo.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList
Me.swProcessCombo.Name = "swProcessCombo"
Me.swProcessCombo.Size = New System.Drawing.Size(260, 25)
'
'separator1
'
Me.separator1.Name = "separator1"
Me.separator1.Size = New System.Drawing.Size(6, 26)
'
'filePrefixLabel
'
Me.filePrefixLabel.Name = "filePrefixLabel"
Me.filePrefixLabel.Size = New System.Drawing.Size(72, 21)
Me.filePrefixLabel.Text = "选中文件:"
'
'fileNameLabel
'
Me.fileNameLabel.Name = "fileNameLabel"
Me.fileNameLabel.Size = New System.Drawing.Size(100, 21)
Me.fileNameLabel.Text = ""
'
'separator2
'
Me.separator2.Name = "separator2"
Me.separator2.Size = New System.Drawing.Size(6, 26)
'
'dirPrefixLabel
'
Me.dirPrefixLabel.Name = "dirPrefixLabel"
Me.dirPrefixLabel.Size = New System.Drawing.Size(72, 21)
Me.dirPrefixLabel.Text = "工作目录:"
'
'dirPathLabel
'
Me.dirPathLabel.Name = "dirPathLabel"
Me.dirPathLabel.Size = New System.Drawing.Size(300, 21)
Me.dirPathLabel.Text = ""
Me.StatusStrip1.ResumeLayout(False)
Me.StatusStrip1.PerformLayout()
```

- [ ] **Step 4: 移除旧控件的属性设置代码**

从 `InitializeComponent` 中删除以下区块：
- ComboBox1 的属性设置（约4行：ItemHeight, Location, Size, TabIndex + Name/Type/Parent/ZOrder）
- Button15 的属性设置（约3行：Location, Size, Text + TabIndex/Name/Type/Parent/ZOrder）
- Label1 的属性设置（约4行：AutoSize, Location, Size, Text + TabIndex/Name/Type/Parent/ZOrder）
- Label2 的属性设置（约4行：AutoSize, Location, Size, Text + TabIndex/Name/Type/Parent/ZOrder）
- Label3 的属性设置（约4行：AutoSize, Location, Size, Text + TabIndex/Name/Type/Parent/ZOrder）

- [ ] **Step 5: 更新 Controls 集合和 ZOrder**

在 `InitializeComponent` 中 `Me.Controls.Add(Me.Label3)` 等添加行：
- 删除 `Me.Controls.Add(Me.Label3)`、`Me.Controls.Add(Me.Label2)`、`Me.Controls.Add(Me.Label1)`、`Me.Controls.Add(Me.Button15)`、`Me.Controls.Add(Me.ComboBox1)`
- 添加 `Me.Controls.Add(Me.StatusStrip1)`

- [ ] **Step 6: 更新 Dispose 方法**

在 `Dispose` 方法中，`components.Dispose()` 前添加：

```vb
If disposing AndAlso StatusStrip1 IsNot Nothing Then
    StatusStrip1.Dispose()
End If
```

按需清理 StatusStrip 资源。

---

### Task 3: 更新 Form1.resx — 移除旧控件条目，添加 StatusStrip 条目

**Files:**
- Modify: `Form1.resx`

- [ ] **Step 1: 移除旧控件的 resx 条目**

删除以下 `data` 条目（每个约3行：key, value, comment（如有））：

从 resx 文件中删除包含以下 Name 的所有 `<data>` 块：
- `ComboBox1.*`（ItemHeight, Location, Size, TabIndex, Name, Type, Parent, ZOrder）
- `Button15.*`（Location, Size, TabIndex, Text, Name, Type, Parent, ZOrder）
- `Label1.*`（AutoSize, Location, Size, TabIndex, Text, Name, Type, Parent, ZOrder）
- `Label2.*`（AutoSize, Location, Size, TabIndex, Text, Name, Type, Parent, ZOrder）
- `Label3.*`（AutoSize, Location, Size, TabIndex, Text, Name, Type, Parent, ZOrder）

- [ ] **Step 2: 添加 StatusStrip 的 resx 条目**

在当前最后一个 `</data>` 标签后、`<metadata>` 标签前，添加：

```xml
  <data name="StatusStrip1.Location" type="System.Drawing.Point, System.Drawing">
    <value>0, 454</value>
  </data>
  <data name="StatusStrip1.Size" type="System.Drawing.Size, System.Drawing">
    <value>1264, 26</value>
  </data>
  <data name="StatusStrip1.TabIndex" type="System.Int32, mscorlib">
    <value>16</value>
  </data>
  <data name="&gt;&gt;StatusStrip1.Name" xml:space="preserve">
    <value>StatusStrip1</value>
  </data>
  <data name="&gt;&gt;StatusStrip1.Type" xml:space="preserve">
    <value>System.Windows.Forms.StatusStrip, System.Windows.Forms, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089</value>
  </data>
  <data name="&gt;&gt;StatusStrip1.Parent" xml:space="preserve">
    <value>$this</value>
  </data>
  <data name="&gt;&gt;StatusStrip1.ZOrder" xml:space="preserve">
    <value>0</value>
  </data>
```

ZOrder 设为 0 保证 StatusStrip 在最底层（但实际上 Dock=Bottom 不受 ZOrder 影响，这里只是为了完整性）。

---

### Task 4: 更新 Form1.vb — 替换所有旧控件引用

**Files:**
- Modify: `Form1.vb`

需要修改的方法列表：
- `Form1_Load` (line 24-32)
- `ProcessTimer_Tick` (line 34-48)
- `UpdateLabel1` → 重命名为 `UpdateStatusBar` (line 51-113)
- `ComboBox1_SelectedIndexChanged` → `swProcessCombo_SelectedIndexChanged` (line 952-967)
- `_swApp_ActiveDocChangeNotify` (line 1002-1011)
- `_swApp_ActiveModelDocChangeNotify` (line 1013-1022)
- `_swApp_FileCloseNotify` (line 1024-1045)
- `PopulateSolidWorksProcesses` (line 1115-1185)
- `Button15_Click` → `refreshBtn_Click` (line 1195-1198)

- [ ] **Step 1: 重命名并更新 `UpdateLabel1` → `UpdateStatusBar`**

将方法声明从：

```vb
Private Sub UpdateLabel1()
```

改为：

```vb
Private Sub UpdateStatusBar()
```

方法体内所有 `Label1.Text` → `fileNameLabel.Text`，所有 `Label3.Text` → `dirPathLabel.Text`：

```vb
Private Sub UpdateStatusBar()
    Try
        If _swApp Is Nothing Then
            fileNameLabel.Text = "未连接"
            dirPathLabel.Text = ""
            Return
        End If
        Dim modelDoc As SldWorks.ModelDoc2 = TryCast(_swApp.ActiveDoc, SldWorks.ModelDoc2)
        If modelDoc Is Nothing Then
            fileNameLabel.Text = "无文档"
            dirPathLabel.Text = ""
            Return
        End If

        ' 尝试获取选中对象的文件名
        Dim selMgr As SldWorks.SelectionMgr = modelDoc.SelectionManager
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
                If refModel IsNot Nothing Then
                    Dim fp As String = refModel.GetPathName()
                    If Not String.IsNullOrEmpty(fp) Then
                        fileNameLabel.Text = System.IO.Path.GetFileNameWithoutExtension(fp)
                        Return
                    End If
                End If
                Dim cp As String = comp.GetPathName()
                If Not String.IsNullOrEmpty(cp) Then
                    fileNameLabel.Text = System.IO.Path.GetFileNameWithoutExtension(cp)
                    Return
                End If
            ElseIf TypeOf selObj Is SldWorks.ModelDoc2 Then
                Dim selModel As SldWorks.ModelDoc2 = CType(selObj, SldWorks.ModelDoc2)
                Dim fp As String = selModel.GetPathName()
                If Not String.IsNullOrEmpty(fp) Then
                    fileNameLabel.Text = System.IO.Path.GetFileNameWithoutExtension(fp)
                    Return
                End If
            End If
        End If

        ' 无选择时显示文档标题
        Dim docPath As String = modelDoc.GetPathName()
        If Not String.IsNullOrEmpty(docPath) Then
            fileNameLabel.Text = System.IO.Path.GetFileNameWithoutExtension(docPath)
            dirPathLabel.Text = System.IO.Path.GetDirectoryName(docPath)
        Else
            fileNameLabel.Text = modelDoc.GetTitle()
            dirPathLabel.Text = ""
        End If
    Catch
        fileNameLabel.Text = "错误"
        dirPathLabel.Text = ""
    End Try
End Sub
```

- [ ] **Step 2: 更新 `Form1_Load`**

将 `UpdateLabel1()` 改为 `UpdateStatusBar()`：

```vb
Private Sub Form1_Load(sender As Object, e As EventArgs) Handles MyBase.Load
    PopulateSolidWorksProcesses()
    ConnectToSw()
    AttachDocEvents()
    UpdateStatusBar()
    Dim timer As New Timer() With {.Interval = 3000}
    AddHandler timer.Tick, AddressOf ProcessTimer_Tick
    timer.Start()
End Sub
```

- [ ] **Step 3: 更新 `ProcessTimer_Tick`**

所有 `ComboBox1` 引用改为使用 `swProcessCombo`：

```vb
Private Sub ProcessTimer_Tick(sender As Object, e As EventArgs)
    Try
        If _swApp IsNot Nothing Then
            Dim test As Object = _swApp.ActiveDoc
        End If
    Catch
        ConnectToSw()
        Dim prevIdx As Integer = swProcessCombo.SelectedIndex
        PopulateSolidWorksProcesses()
        If prevIdx >= 0 AndAlso prevIdx < swProcessCombo.Items.Count Then
            swProcessCombo.SelectedIndex = prevIdx
        End If
        AttachDocEvents()
        UpdateStatusBar()
    End Try
End Sub
```

- [ ] **Step 4: 替换 `ComboBox1_SelectedIndexChanged` 为 ToolStripComboBox 事件**

删除原有的 `ComboBox1_SelectedIndexChanged`，添加：

```vb
Private Sub swProcessCombo_SelectedIndexChanged(sender As Object, e As EventArgs) Handles swProcessCombo.SelectedIndexChanged
    Dim info = TryCast(swProcessCombo.SelectedItem, SwProcessInfo)
    If info Is Nothing Then
        Return
    End If

    Try
        _selectedSwProcess = Process.GetProcessById(info.ProcessId)
    Catch ex As Exception
        _selectedSwProcess = Nothing
    End Try
    ConnectToSw()
    AttachDocEvents()
    UpdateStatusBar()
End Sub
```

- [ ] **Step 5: 替换 `Button15_Click` 为 `refreshBtn_Click`**

删除原有的 `Button15_Click`，添加：

```vb
Private Sub refreshBtn_Click(sender As Object, e As EventArgs) Handles refreshBtn.Click
    PopulateSolidWorksProcesses()
    UpdateStatusBar()
End Sub
```

- [ ] **Step 6: 更新 `_swApp_ActiveDocChangeNotify`**

```vb
Private Function _swApp_ActiveDocChangeNotify() As Integer Handles _swApp.ActiveDocChangeNotify
    Dim prevIndex As Integer = swProcessCombo.SelectedIndex
    PopulateSolidWorksProcesses()
    If prevIndex >= 0 AndAlso prevIndex < swProcessCombo.Items.Count Then
        swProcessCombo.SelectedIndex = prevIndex
    End If
    UpdateStatusBar()
    AttachDocEvents()
    Return 0
End Function
```

- [ ] **Step 7: 更新 `_swApp_ActiveModelDocChangeNotify`**

```vb
Private Function _swApp_ActiveModelDocChangeNotify() As Integer Handles _swApp.ActiveModelDocChangeNotify
    Dim prevIndex As Integer = swProcessCombo.SelectedIndex
    PopulateSolidWorksProcesses()
    If prevIndex >= 0 AndAlso prevIndex < swProcessCombo.Items.Count Then
        swProcessCombo.SelectedIndex = prevIndex
    End If
    UpdateStatusBar()
    AttachDocEvents()
    Return 0
End Function
```

- [ ] **Step 8: 更新 `_swApp_FileCloseNotify`**

```vb
Private Function _swApp_FileCloseNotify(fileName As String, reason As Integer) As Integer Handles _swApp.FileCloseNotify
    Try
        If _swApp IsNot Nothing Then
            Dim doc As SldWorks.ModelDoc2 = TryCast(_swApp.ActiveDoc, SldWorks.ModelDoc2)
            If doc Is Nothing Then
                fileNameLabel.Text = "无文档"
                dirPathLabel.Text = ""
            ElseIf String.Equals(doc.GetPathName(), fileName, StringComparison.OrdinalIgnoreCase) Then
                fileNameLabel.Text = "无文档"
                dirPathLabel.Text = ""
            Else
                UpdateStatusBar()
            End If
        Else
            fileNameLabel.Text = "未连接"
            dirPathLabel.Text = ""
        End If
    Catch
        UpdateStatusBar()
    End Try
    Return 0
End Function
```

- [ ] **Step 9: 更新 `PopulateSolidWorksProcesses`**

所有 `ComboBox1` 引用改为 `swProcessCombo`：

```vb
Private Sub PopulateSolidWorksProcesses()
    swProcessCombo.Items.Clear()

    Try
        Dim procs = Process.GetProcessesByName("sldworks")
        For Each p In procs
            Dim h As IntPtr = p.MainWindowHandle

            ' 首先尝试常规属性
            Dim title As String = p.MainWindowTitle

            ' 如果为空，尝试使用 Win32 API 直接读取窗口文本
            If String.IsNullOrWhiteSpace(title) AndAlso h <> IntPtr.Zero Then
                Try
                    Dim len As Integer = GetWindowTextLength(h)
                    If len > 0 Then
                        Dim sb As New Text.StringBuilder(len + 1)
                        GetWindowText(h, sb, sb.Capacity)
                        title = sb.ToString()
                    End If
                Catch
                End Try
            End If

            ' 如果仍然为空，尝试将该实例置前并通过 COM 获取活动文档名
            If String.IsNullOrWhiteSpace(title) AndAlso h <> IntPtr.Zero Then
                Try
                    ShowWindow(h, SwRestore)
                    SetForegroundWindow(h)
                    Dim swApp = Marshal.GetActiveObject("SldWorks.Application")
                    If swApp IsNot Nothing Then
                        Dim doc As SldWorks.ModelDoc2 = CType(swApp.ActiveDoc, SldWorks.ModelDoc2)
                        If doc IsNot Nothing Then
                            Dim path As String = doc.GetPathName()
                            If Not String.IsNullOrWhiteSpace(path) Then
                                title = IO.Path.GetFileNameWithoutExtension(path)
                            End If
                        End If
                    End If
                Catch
                End Try
            End If

            If String.IsNullOrWhiteSpace(title) Then
                title = "SolidWorks (PID " & p.Id & ")"
            Else
                Dim sep As String = " - "
                Dim idx As Integer = title.IndexOf(sep, StringComparison.Ordinal)
                If idx >= 0 Then
                    title = title.Substring(idx + sep.Length).Trim()
                End If

                If title.StartsWith("[") AndAlso title.EndsWith("]") AndAlso title.Length > 2 Then
                    title = title.Substring(1, title.Length - 2).Trim()
                End If
            End If

            swProcessCombo.Items.Add(New SwProcessInfo With {.Title = title, .ProcessId = p.Id})
        Next

        If swProcessCombo.Items.Count > 0 Then
            swProcessCombo.SelectedIndex = 0
        End If
    Catch ex As Exception
    End Try
End Sub
```

- [ ] **Step 10: 更新 `Doc_SelectionChange` 委托回调**

```vb
Private Function Doc_SelectionChange() As Integer
    UpdateStatusBar()
    Return 0
End Function
```

---

### Task 5: 清理 Form2.vb 中可能的旧引用引用

**Files:**
- 检查: `Form2.vb`

- [ ] **Step 1: 确认 Form2 不引用被移除的控件**

Form2 当前只处理自身 FormClosed 事件，不引用 Form1 的控件。确认无需更改。

```bash
grep -n "ComboBox1\|Label1\|Label2\|Label3\|Button15" "C:\Users\yuan7\RiderProjects\chajian2\Form2.vb"
```

Expected: No matches.

---

### Task 6: 编译验证

**Files:**
- 所有修改过的文件

- [ ] **Step 1: 编译项目**

```bash
"C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe" "C:\Users\yuan7\RiderProjects\chajian2\外部程序.vbproj" /t:Build /p:Configuration=Debug 2>&1
```

Expected: Build succeeded, 0 errors, 0 warnings.

- [ ] **Step 2: 提交所有更改**

```bash
git add "C:\Users\yuan7\RiderProjects\chajian2\Form1.vb" "C:\Users\yuan7\RiderProjects\chajian2\Form1.Designer.vb" "C:\Users\yuan7\RiderProjects\chajian2\Form1.resx"
git commit -m "feat: replace bottom controls with StatusStrip status bar

- Add StatusStrip with ToolStripButton (refresh), ToolStripComboBox (SW process list showing PID:Title), and status labels for selected file and working directory
- Remove ComboBox1, Label1, Label2, Label3, Button15
- Rename UpdateLabel1 to UpdateStatusBar

Co-Authored-By: Claude Opus 4.7 <noreply@anthropic.com>"
```