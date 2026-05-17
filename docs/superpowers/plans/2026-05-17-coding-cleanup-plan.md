# 编码整理 (Form4 Coding Cleanup) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build a floating Form4 config window that batch-processes assembly components — filtering by name, type, virtual/standard/purchased status — then syncs document title to custom properties.

**Architecture:** New Form4 floating window (like Form3), opened from Form1.Button18. Settings persisted to My.Settings. Executes recursive assembly traversal with configurable filter conditions.

**Tech Stack:** VB.NET, .NET Framework 4.8, WinForms, SolidWorks COM API (SldWorks, SwConst)

---

## File Structure

| File | Responsibility |
|------|---------------|
| `Form4.vb` (new) | Window logic: load/save settings, drag, filter execution, recursive traversal, property sync |
| `Form4.Designer.vb` (new) | Control layout and initialization |
| `Form4.resx` (new) | WinForms resources |
| `Form1.vb` (modify) | Add Button18_Click to open Form4 |
| `Form1.Designer.vb` (modify) | Add Button18 to GroupBox2 |
| `外部程序.vbproj` (modify) | Register Form4 compilation items |
| `My Project/Settings.settings` (modify) | Add 6 user-scoped settings |
| `My Project/Settings.Designer.vb` (modify) | Add 6 settings properties |

---

### Task 1: Add My.Settings entries

**Files:**
- Modify: `My Project/Settings.settings`
- Modify: `My Project/Settings.Designer.vb`

- [ ] **Step 1: Add settings to Settings.settings**

Open `My Project/Settings.settings`. After the existing `DefaultSwProcessId` setting block, add:

```xml
<Setting Name="CodingCleanup_NameFilter" Type="System.String" Scope="User">
  <Value Profile="(Default)"></Value>
</Setting>
<Setting Name="CodingCleanup_ProcessAsm" Type="System.Boolean" Scope="User">
  <Value Profile="(Default)">True</Value>
</Setting>
<Setting Name="CodingCleanup_ProcessPart" Type="System.Boolean" Scope="User">
  <Value Profile="(Default)">True</Value>
</Setting>
<Setting Name="CodingCleanup_ExcludeVirtual" Type="System.Boolean" Scope="User">
  <Value Profile="(Default)">False</Value>
</Setting>
<Setting Name="CodingCleanup_ExcludeStandard" Type="System.Boolean" Scope="User">
  <Value Profile="(Default)">False</Value>
</Setting>
<Setting Name="CodingCleanup_ExcludePurchased" Type="System.Boolean" Scope="User">
  <Value Profile="(Default)">False</Value>
</Setting>
```

The final file should look like:

```xml
<?xml version='1.0' encoding='utf-8'?>
<SettingsFile xmlns="http://schemas.microsoft.com/VisualStudio/2004/01/settings" CurrentProfile="(Default)" UseMySettingsClassName="true">
  <Profiles>
    <Profile Name="(Default)" />
  </Profiles>
  <Settings>
    <Setting Name="DefaultSwProcessId" Type="System.Int32" Scope="User">
      <Value Profile="(Default)">0</Value>
    </Setting>
    <Setting Name="CodingCleanup_NameFilter" Type="System.String" Scope="User">
      <Value Profile="(Default)"></Value>
    </Setting>
    <Setting Name="CodingCleanup_ProcessAsm" Type="System.Boolean" Scope="User">
      <Value Profile="(Default)">True</Value>
    </Setting>
    <Setting Name="CodingCleanup_ProcessPart" Type="System.Boolean" Scope="User">
      <Value Profile="(Default)">True</Value>
    </Setting>
    <Setting Name="CodingCleanup_ExcludeVirtual" Type="System.Boolean" Scope="User">
      <Value Profile="(Default)">False</Value>
    </Setting>
    <Setting Name="CodingCleanup_ExcludeStandard" Type="System.Boolean" Scope="User">
      <Value Profile="(Default)">False</Value>
    </Setting>
    <Setting Name="CodingCleanup_ExcludePurchased" Type="System.Boolean" Scope="User">
      <Value Profile="(Default)">False</Value>
    </Setting>
  </Settings>
</SettingsFile>
```

- [ ] **Step 2: Add settings properties to Settings.Designer.vb**

Open `My Project/Settings.Designer.vb`. After the existing `DefaultSwProcessId` property (before `End Class`), add:

```vb
<Global.System.Configuration.UserScopedSettingAttribute(),  _
 Global.System.Diagnostics.DebuggerNonUserCodeAttribute(),  _
 Global.System.Configuration.DefaultSettingValueAttribute("")>  _
Public Property CodingCleanup_NameFilter() As String
    Get
        Return CType(Me("CodingCleanup_NameFilter"), String)
    End Get
    Set
        Me("CodingCleanup_NameFilter") = value
    End Set
End Property

<Global.System.Configuration.UserScopedSettingAttribute(),  _
 Global.System.Diagnostics.DebuggerNonUserCodeAttribute(),  _
 Global.System.Configuration.DefaultSettingValueAttribute("True")>  _
Public Property CodingCleanup_ProcessAsm() As Boolean
    Get
        Return CType(Me("CodingCleanup_ProcessAsm"), Boolean)
    End Get
    Set
        Me("CodingCleanup_ProcessAsm") = value
    End Set
End Property

<Global.System.Configuration.UserScopedSettingAttribute(),  _
 Global.System.Diagnostics.DebuggerNonUserCodeAttribute(),  _
 Global.System.Configuration.DefaultSettingValueAttribute("True")>  _
Public Property CodingCleanup_ProcessPart() As Boolean
    Get
        Return CType(Me("CodingCleanup_ProcessPart"), Boolean)
    End Get
    Set
        Me("CodingCleanup_ProcessPart") = value
    End Set
End Property

<Global.System.Configuration.UserScopedSettingAttribute(),  _
 Global.System.Diagnostics.DebuggerNonUserCodeAttribute(),  _
 Global.System.Configuration.DefaultSettingValueAttribute("False")>  _
Public Property CodingCleanup_ExcludeVirtual() As Boolean
    Get
        Return CType(Me("CodingCleanup_ExcludeVirtual"), Boolean)
    End Get
    Set
        Me("CodingCleanup_ExcludeVirtual") = value
    End Set
End Property

<Global.System.Configuration.UserScopedSettingAttribute(),  _
 Global.System.Diagnostics.DebuggerNonUserCodeAttribute(),  _
 Global.System.Configuration.DefaultSettingValueAttribute("False")>  _
Public Property CodingCleanup_ExcludeStandard() As Boolean
    Get
        Return CType(Me("CodingCleanup_ExcludeStandard"), Boolean)
    End Get
    Set
        Me("CodingCleanup_ExcludeStandard") = value
    End Set
End Property

<Global.System.Configuration.UserScopedSettingAttribute(),  _
 Global.System.Diagnostics.DebuggerNonUserCodeAttribute(),  _
 Global.System.Configuration.DefaultSettingValueAttribute("False")>  _
Public Property CodingCleanup_ExcludePurchased() As Boolean
    Get
        Return CType(Me("CodingCleanup_ExcludePurchased"), Boolean)
    End Get
    Set
        Me("CodingCleanup_ExcludePurchased") = value
    End Set
End Property
```

- [ ] **Step 3: Commit**

```bash
git add "My Project/Settings.settings" "My Project/Settings.Designer.vb"
git commit -m "feat: add CodingCleanup settings (6 user-scoped properties)"
```

---

### Task 2: Create Form4 Designer (layout)

**Files:**
- Create: `Form4.Designer.vb`
- Create: `Form4.resx`

- [ ] **Step 1: Write Form4.Designer.vb**

```vb
<Global.Microsoft.VisualBasic.CompilerServices.DesignerGenerated()> _
Partial Class Form4
    Inherits System.Windows.Forms.Form

    <System.Diagnostics.DebuggerNonUserCode()> _
    Protected Overrides Sub Dispose(ByVal disposing As Boolean)
        Try
            If disposing AndAlso components IsNot Nothing Then
                components.Dispose()
            End If
        Finally
            MyBase.Dispose(disposing)
        End Try
    End Sub

    Private components As System.ComponentModel.IContainer

    <System.Diagnostics.DebuggerStepThrough()> _
    Private Sub InitializeComponent()
        Me.Label1 = New System.Windows.Forms.Label()
        Me.TextBox1 = New System.Windows.Forms.TextBox()
        Me.Label2 = New System.Windows.Forms.Label()
        Me.CheckBox1 = New System.Windows.Forms.CheckBox()
        Me.CheckBox2 = New System.Windows.Forms.CheckBox()
        Me.Label3 = New System.Windows.Forms.Label()
        Me.CheckBox3 = New System.Windows.Forms.CheckBox()
        Me.CheckBox4 = New System.Windows.Forms.CheckBox()
        Me.CheckBox5 = New System.Windows.Forms.CheckBox()
        Me.Button1 = New System.Windows.Forms.Button()
        Me.SuspendLayout()
        '
        'Label1
        '
        Me.Label1.AutoSize = True
        Me.Label1.Font = New System.Drawing.Font("Microsoft YaHei UI", 9.0!, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, CType(134, Byte))
        Me.Label1.Location = New System.Drawing.Point(12, 15)
        Me.Label1.Name = "Label1"
        Me.Label1.Size = New System.Drawing.Size(120, 17)
        Me.Label1.TabIndex = 0
        Me.Label1.Text = "名称筛选（可选）"
        '
        'TextBox1
        '
        Me.TextBox1.Font = New System.Drawing.Font("Microsoft YaHei UI", 9.0!, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, CType(134, Byte))
        Me.TextBox1.Location = New System.Drawing.Point(12, 35)
        Me.TextBox1.Name = "TextBox1"
        Me.TextBox1.Size = New System.Drawing.Size(320, 23)
        Me.TextBox1.TabIndex = 1
        '
        'Label2
        '
        Me.Label2.AutoSize = True
        Me.Label2.Font = New System.Drawing.Font("Microsoft YaHei UI", 9.0!, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, CType(134, Byte))
        Me.Label2.Location = New System.Drawing.Point(12, 71)
        Me.Label2.Name = "Label2"
        Me.Label2.Size = New System.Drawing.Size(56, 17)
        Me.Label2.TabIndex = 2
        Me.Label2.Text = "处理范围"
        '
        'CheckBox1
        '
        Me.CheckBox1.AutoSize = True
        Me.CheckBox1.Font = New System.Drawing.Font("Microsoft YaHei UI", 9.0!, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, CType(134, Byte))
        Me.CheckBox1.Location = New System.Drawing.Point(15, 95)
        Me.CheckBox1.Name = "CheckBox1"
        Me.CheckBox1.Size = New System.Drawing.Size(87, 21)
        Me.CheckBox1.TabIndex = 3
        Me.CheckBox1.Text = "处理装配体"
        Me.CheckBox1.UseVisualStyleBackColor = True
        '
        'CheckBox2
        '
        Me.CheckBox2.AutoSize = True
        Me.CheckBox2.Font = New System.Drawing.Font("Microsoft YaHei UI", 9.0!, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, CType(134, Byte))
        Me.CheckBox2.Location = New System.Drawing.Point(130, 95)
        Me.CheckBox2.Name = "CheckBox2"
        Me.CheckBox2.Size = New System.Drawing.Size(75, 21)
        Me.CheckBox2.TabIndex = 4
        Me.CheckBox2.Text = "处理零件"
        Me.CheckBox2.UseVisualStyleBackColor = True
        '
        'Label3
        '
        Me.Label3.AutoSize = True
        Me.Label3.Font = New System.Drawing.Font("Microsoft YaHei UI", 9.0!, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, CType(134, Byte))
        Me.Label3.Location = New System.Drawing.Point(12, 130)
        Me.Label3.Name = "Label3"
        Me.Label3.Size = New System.Drawing.Size(56, 17)
        Me.Label3.TabIndex = 5
        Me.Label3.Text = "排除条件"
        '
        'CheckBox3
        '
        Me.CheckBox3.AutoSize = True
        Me.CheckBox3.Font = New System.Drawing.Font("Microsoft YaHei UI", 9.0!, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, CType(134, Byte))
        Me.CheckBox3.Location = New System.Drawing.Point(15, 154)
        Me.CheckBox3.Name = "CheckBox3"
        Me.CheckBox3.Size = New System.Drawing.Size(126, 21)
        Me.CheckBox3.TabIndex = 6
        Me.CheckBox3.Text = "排除虚拟装配体"
        Me.CheckBox3.UseVisualStyleBackColor = True
        '
        'CheckBox4
        '
        Me.CheckBox4.AutoSize = True
        Me.CheckBox4.Font = New System.Drawing.Font("Microsoft YaHei UI", 9.0!, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, CType(134, Byte))
        Me.CheckBox4.Location = New System.Drawing.Point(15, 181)
        Me.CheckBox4.Name = "CheckBox4"
        Me.CheckBox4.Size = New System.Drawing.Size(219, 21)
        Me.CheckBox4.TabIndex = 7
        Me.CheckBox4.Text = "排除标准件（零件类型=标准件）"
        Me.CheckBox4.UseVisualStyleBackColor = True
        '
        'CheckBox5
        '
        Me.CheckBox5.AutoSize = True
        Me.CheckBox5.Font = New System.Drawing.Font("Microsoft YaHei UI", 9.0!, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, CType(134, Byte))
        Me.CheckBox5.Location = New System.Drawing.Point(15, 208)
        Me.CheckBox5.Name = "CheckBox5"
        Me.CheckBox5.Size = New System.Drawing.Size(219, 21)
        Me.CheckBox5.TabIndex = 8
        Me.CheckBox5.Text = "排除外购件（零件类型=外购件）"
        Me.CheckBox5.UseVisualStyleBackColor = True
        '
        'Button1
        '
        Me.Button1.Font = New System.Drawing.Font("Microsoft YaHei UI", 9.0!, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, CType(134, Byte))
        Me.Button1.Location = New System.Drawing.Point(100, 246)
        Me.Button1.Name = "Button1"
        Me.Button1.Size = New System.Drawing.Size(146, 32)
        Me.Button1.TabIndex = 9
        Me.Button1.Text = "执行编码整理"
        Me.Button1.UseVisualStyleBackColor = True
        '
        'Form4
        '
        Me.AutoScaleDimensions = New System.Drawing.SizeF(6.0!, 12.0!)
        Me.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font
        Me.ClientSize = New System.Drawing.Size(347, 293)
        Me.Controls.Add(Me.Button1)
        Me.Controls.Add(Me.CheckBox5)
        Me.Controls.Add(Me.CheckBox4)
        Me.Controls.Add(Me.CheckBox3)
        Me.Controls.Add(Me.Label3)
        Me.Controls.Add(Me.CheckBox2)
        Me.Controls.Add(Me.CheckBox1)
        Me.Controls.Add(Me.Label2)
        Me.Controls.Add(Me.TextBox1)
        Me.Controls.Add(Me.Label1)
        Me.FormBorderStyle = System.Windows.Forms.FormBorderStyle.None
        Me.MaximizeBox = False
        Me.MinimizeBox = False
        Me.Name = "Form4"
        Me.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen
        Me.Text = "编码整理"
        Me.ResumeLayout(False)
        Me.PerformLayout()

    End Sub

    Friend WithEvents Label1 As System.Windows.Forms.Label
    Friend WithEvents TextBox1 As System.Windows.Forms.TextBox
    Friend WithEvents Label2 As System.Windows.Forms.Label
    Friend WithEvents CheckBox1 As System.Windows.Forms.CheckBox
    Friend WithEvents CheckBox2 As System.Windows.Forms.CheckBox
    Friend WithEvents Label3 As System.Windows.Forms.Label
    Friend WithEvents CheckBox3 As System.Windows.Forms.CheckBox
    Friend WithEvents CheckBox4 As System.Windows.Forms.CheckBox
    Friend WithEvents CheckBox5 As System.Windows.Forms.CheckBox
    Friend WithEvents Button1 As System.Windows.Forms.Button
End Class
```

- [ ] **Step 2: Write Form4.resx**

Create `Form4.resx` with standard WinForms resource XML. Copy from `Form3.resx` and change the resource names:

```xml
<?xml version="1.0" encoding="utf-8"?>
<root>
  <xsd:schema id="root" xmlns="" xmlns:xsd="http://www.w3.org/2001/XMLSchema" xmlns:msdata="urn:schemas-microsoft-com:xml-msdata">
    <xsd:import namespace="http://www.w3.org/XML/1998/namespace" />
    <xsd:element name="root" msdata:IsDataSet="true">
      <xsd:complexType>
        <xsd:choice maxOccurs="unbounded">
          <xsd:element name="metadata">
            <xsd:complexType>
              <xsd:sequence>
                <xsd:element name="value" type="xsd:string" minOccurs="0" />
              </xsd:sequence>
              <xsd:attribute name="name" use="required" type="xsd:string" />
              <xsd:attribute name="type" type="xsd:string" />
              <xsd:attribute name="mimetype" type="xsd:string" />
              <xsd:attribute ref="xml:space" />
            </xsd:complexType>
          </xsd:element>
          <xsd:element name="assembly">
            <xsd:complexType>
              <xsd:attribute name="alias" type="xsd:string" />
              <xsd:attribute name="name" type="xsd:string" />
            </xsd:complexType>
          </xsd:element>
          <xsd:element name="data">
            <xsd:complexType>
              <xsd:sequence>
                <xsd:element name="value" type="xsd:string" minOccurs="0" msdata:Ordinal="1" />
                <xsd:element name="comment" type="xsd:string" minOccurs="0" msdata:Ordinal="2" />
              </xsd:sequence>
              <xsd:attribute name="name" type="xsd:string" use="required" msdata:Ordinal="1" />
              <xsd:attribute name="type" type="xsd:string" msdata:Ordinal="3" />
              <xsd:attribute name="mimetype" type="xsd:string" msdata:Ordinal="4" />
              <xsd:attribute ref="xml:space" />
            </xsd:complexType>
          </xsd:element>
          <xsd:element name="resheader">
            <xsd:complexType>
              <xsd:sequence>
                <xsd:element name="value" type="xsd:string" minOccurs="0" msdata:Ordinal="1" />
              </xsd:sequence>
              <xsd:attribute name="name" type="xsd:string" use="required" />
            </xsd:complexType>
          </xsd:element>
        </xsd:choice>
      </xsd:complexType>
    </xsd:element>
  </xsd:schema>
  <resheader name="resmimetype">
    <value>text/microsoft-resx</value>
  </resheader>
  <resheader name="version">
    <value>2.0</value>
  </resheader>
  <resheader name="reader">
    <value>System.Resources.ResXResourceReader, System.Windows.Forms, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089</value>
  </resheader>
  <resheader name="writer">
    <value>System.Resources.ResXResourceWriter, System.Windows.Forms, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089</value>
  </resheader>
</root>
```

- [ ] **Step 3: Commit**

```bash
git add Form4.Designer.vb Form4.resx
git commit -m "feat: add Form4 designer layout for coding cleanup"
```

---

### Task 3: Implement Form4 logic

**Files:**
- Create: `Form4.vb`

- [ ] **Step 1: Write Form4.vb**

```vb
Imports System.Runtime.InteropServices
Imports SwConst

Public Class Form4

    Private Const WmNclbuttondown As Integer = &HA1
    Private Const HtCaption As Integer = &H2

    <DllImport("user32.dll")>
    Private Shared Function ReleaseCapture() As Boolean
    End Function

    <DllImport("user32.dll")>
    Private Shared Function SendMessage(hWnd As IntPtr, msg As Integer, wParam As IntPtr, lParam As IntPtr) As IntPtr
    End Function

    Public Property SwApp As SldWorks.SldWorks

    Private Sub Form4_Load(sender As Object, e As EventArgs) Handles MyBase.Load
        TopMost = True
        EnableDrag()
        LoadSettings()
    End Sub

    Private Sub LoadSettings()
        TextBox1.Text = My.Settings.CodingCleanup_NameFilter
        CheckBox1.Checked = My.Settings.CodingCleanup_ProcessAsm
        CheckBox2.Checked = My.Settings.CodingCleanup_ProcessPart
        CheckBox3.Checked = My.Settings.CodingCleanup_ExcludeVirtual
        CheckBox4.Checked = My.Settings.CodingCleanup_ExcludeStandard
        CheckBox5.Checked = My.Settings.CodingCleanup_ExcludePurchased
    End Sub

    Private Sub TextBox1_TextChanged(sender As Object, e As EventArgs) Handles TextBox1.TextChanged
        My.Settings.CodingCleanup_NameFilter = TextBox1.Text
        My.Settings.Save()
    End Sub

    Private Sub CheckBox1_CheckedChanged(sender As Object, e As EventArgs) Handles CheckBox1.CheckedChanged
        My.Settings.CodingCleanup_ProcessAsm = CheckBox1.Checked
        My.Settings.Save()
    End Sub

    Private Sub CheckBox2_CheckedChanged(sender As Object, e As EventArgs) Handles CheckBox2.CheckedChanged
        My.Settings.CodingCleanup_ProcessPart = CheckBox2.Checked
        My.Settings.Save()
    End Sub

    Private Sub CheckBox3_CheckedChanged(sender As Object, e As EventArgs) Handles CheckBox3.CheckedChanged
        My.Settings.CodingCleanup_ExcludeVirtual = CheckBox3.Checked
        My.Settings.Save()
    End Sub

    Private Sub CheckBox4_CheckedChanged(sender As Object, e As EventArgs) Handles CheckBox4.CheckedChanged
        My.Settings.CodingCleanup_ExcludeStandard = CheckBox4.Checked
        My.Settings.Save()
    End Sub

    Private Sub CheckBox5_CheckedChanged(sender As Object, e As EventArgs) Handles CheckBox5.CheckedChanged
        My.Settings.CodingCleanup_ExcludePurchased = CheckBox5.Checked
        My.Settings.Save()
    End Sub

    Private Sub Button1_Click(sender As Object, e As EventArgs) Handles Button1.Click
        If SwApp Is Nothing Then
            MsgBox("未连接到 SolidWorks，请从主界面重新打开。")
            Exit Sub
        End If

        Dim asmDoc As SldWorks.ModelDoc2 = TryCast(SwApp.ActiveDoc, SldWorks.ModelDoc2)
        If asmDoc Is Nothing Then
            MsgBox("请先在 SolidWorks 中打开一个文档。")
            Exit Sub
        End If
        If asmDoc.GetType <> swDocumentTypes_e.swDocASSEMBLY Then
            MsgBox("请在装配体环境下使用此功能。")
            Exit Sub
        End If

        Me.Cursor = Cursors.WaitCursor
        Try
            CType(asmDoc, SldWorks.AssemblyDoc).ResolveAllLightWeightComponents(True)
            Dim topConfString As String = asmDoc.GetActiveConfiguration.Name
            ProcessConfig(SwApp, asmDoc, topConfString)

            Dim configuration As SldWorks.Configuration = asmDoc.GetConfigurationByName(topConfString)
            Dim rootComponent As SldWorks.Component2 = configuration.GetRootComponent
            Dim comps As Object = rootComponent.GetChildren

            For Each child As SldWorks.Component2 In comps
                ExecuteCodingCleanup(SwApp, child)
            Next

            MsgBox("编码整理完成。", vbInformation, "")
        Catch ex As Exception
            MsgBox("执行出错: " & ex.Message, vbExclamation, "")
        Finally
            Me.Cursor = Cursors.Default
        End Try
    End Sub

    Private Sub ProcessConfig(swApp As SldWorks.SldWorks, modelDoc As SldWorks.ModelDoc2, confString As String)
        SyncTitleToCustomProperties(modelDoc, confString)
    End Sub

    Private Sub ExecuteCodingCleanup(swApp As SldWorks.SldWorks, comp As SldWorks.Component2)
        If ShouldSkip(comp) Then Return

        Dim childModel As SldWorks.ModelDoc2 = comp.GetModelDoc
        If childModel Is Nothing Then Return

        Dim childConfString As String = comp.ReferencedConfiguration
        Dim childType As Integer = childModel.GetType

        Dim longstatus As Integer, longWarnings As Integer
        Dim fopen As SldWorks.ModelDoc2 = Nothing

        If childType = swDocumentTypes_e.swDocPART Then
            fopen = swApp.OpenDoc6(comp.GetPathName, swDocumentTypes_e.swDocPART,
                swOpenDocOptions_e.swOpenDocOptions_Silent, "", longstatus, longWarnings)
            If longstatus = 0 AndAlso fopen IsNot Nothing Then
                SyncTitleToCustomProperties(fopen, childConfString)
                fopen.Save3(0, 0, 0)
            End If
        End If

        If childType = swDocumentTypes_e.swDocASSEMBLY Then
            fopen = swApp.OpenDoc6(comp.GetPathName, swDocumentTypes_e.swDocASSEMBLY,
                swOpenDocOptions_e.swOpenDocOptions_Silent, "", longstatus, longWarnings)
            If longstatus = 0 AndAlso fopen IsNot Nothing Then
                ProcessConfig(swApp, fopen, childConfString)
                fopen.Save3(0, 0, 0)
            End If
            ' 递归处理子装配体的子组件
            ExecuteCodingCleanup(swApp, childModel, childConfString)
        End If
    End Sub

    Private Overloads Sub ExecuteCodingCleanup(swApp As SldWorks.SldWorks, asmDoc As SldWorks.ModelDoc2, confString As String)
        Dim configuration As SldWorks.Configuration = asmDoc.GetConfigurationByName(confString)
        If configuration Is Nothing Then Return
        Dim rootComponent As SldWorks.Component2 = configuration.GetRootComponent
        If rootComponent Is Nothing Then Return
        Dim comps As Object = rootComponent.GetChildren
        If comps Is Nothing Then Return

        For Each child As SldWorks.Component2 In comps
            ExecuteCodingCleanup(swApp, child)
        Next
    End Sub

    Private Function ShouldSkip(comp As SldWorks.Component2) As Boolean
        ' 1. 名称筛选
        Dim nameFilter As String = TextBox1.Text.Trim()
        If Not String.IsNullOrEmpty(nameFilter) Then
            If Not comp.Name.Contains(nameFilter) Then Return True
        End If

        Dim compModel As SldWorks.ModelDoc2 = comp.GetModelDoc
        Dim compType As Integer = If(compModel IsNot Nothing, compModel.GetType, -1)

        ' 2. 类型筛选
        If compType = swDocumentTypes_e.swDocPART Then
            If Not CheckBox2.Checked Then Return True
        ElseIf compType = swDocumentTypes_e.swDocASSEMBLY Then
            If Not CheckBox1.Checked Then Return True
        End If

        ' 3. 虚拟装配体
        If CheckBox3.Checked Then
            Try
                If comp.IsVirtual() Then Return True
            Catch
            End Try
        End If

        ' 4. 标准件
        If CheckBox4.Checked Then
            Try
                Dim partType As String = compModel.GetCustomInfoValue(comp.ReferencedConfiguration, "零件类型")
                If String.Equals(partType, "标准件", StringComparison.OrdinalIgnoreCase) Then Return True
            Catch
            End Try
        End If

        ' 5. 外购件
        If CheckBox5.Checked Then
            Try
                Dim partType As String = compModel.GetCustomInfoValue(comp.ReferencedConfiguration, "零件类型")
                If String.Equals(partType, "外购件", StringComparison.OrdinalIgnoreCase) Then Return True
            Catch
            End Try
        End If

        Return False
    End Function

    Private Sub SyncTitleToCustomProperties(modelDoc As SldWorks.ModelDoc2, confString As String)
        If modelDoc Is Nothing Then Return
        Dim c As String = modelDoc.GetTitle()
        If InStr(c, ".") > 0 Then
            c = Strings.Left(c, Len(c) - 7)
        End If
        If String.IsNullOrEmpty(c) Then Return

        Dim config As SldWorks.Configuration = modelDoc.GetConfigurationByName(confString)
        If config Is Nothing Then Return
        Dim cusPropMgr As SldWorks.CustomPropertyManager = config.CustomPropertyManager
        If cusPropMgr Is Nothing Then Return

        cusPropMgr.Add3("物料编码", swCustomInfoType_e.swCustomInfoText, c,
            swCustomPropertyAddOption_e.swCustomPropertyDeleteAndAdd)
        cusPropMgr.Add3("零件图号", swCustomInfoType_e.swCustomInfoText, c,
            swCustomPropertyAddOption_e.swCustomPropertyDeleteAndAdd)
        cusPropMgr.Add3("文件名称", swCustomInfoType_e.swCustomInfoText, c,
            swCustomPropertyAddOption_e.swCustomPropertyDeleteAndAdd)
    End Sub

    Private Sub EnableDrag()
        AddHandler MouseDown, AddressOf DragForm_MouseDown
        For Each child As Control In Controls
            AddHandler child.MouseDown, AddressOf DragForm_MouseDown
        Next
    End Sub

    Private Sub DragForm_MouseDown(sender As Object, e As MouseEventArgs)
        If e.Button = MouseButtons.Left Then
            ReleaseCapture()
            SendMessage(Handle, WmNclbuttondown, New IntPtr(HtCaption), IntPtr.Zero)
        End If
    End Sub
End Class
```

- [ ] **Step 2: Commit**

```bash
git add Form4.vb
git commit -m "feat: implement Form4 coding cleanup logic"
```

---

### Task 4: Wire existing Button18 to open Form4

**Files:**
- Modify: `Form1.vb:1549-1551`
- Modify: `Form1.Designer.vb:127,144`

Button18 already exists (declared at line 41, empty handler at line 1549) but is in GroupBox1. Move it to GroupBox2 and fill the handler.

- [ ] **Step 1: Move Button18 from GroupBox1 to GroupBox2 in Form1.Designer.vb**

In `InitializeComponent()`, change line 127 from:
```vb
Me.GroupBox1.Controls.Add(Me.Button18)
```
to remove it. Then add it to GroupBox2 (after Button17 line 144):

```vb
Me.GroupBox2.Controls.Add(Me.Button18)
Me.GroupBox2.Controls.Add(Me.Button17)
```

Button18 uses `resources.ApplyResources` for positioning so its exact Location comes from Form1.resx. The group membership change is the only designer code change needed.

- [ ] **Step 2: Fill Button18_Click in Form1.vb**

Replace the empty handler at line 1549-1551:
```vb
Private Sub Button18_Click(sender As Object, e As EventArgs) Handles Button18.Click

End Sub
```

With:
```vb
Private Sub Button18_Click(sender As Object, e As EventArgs) Handles Button18.Click
    Dim swApp As SldWorks.SldWorks = TryCast(GetSelectedSwApp(), SldWorks.SldWorks)
    If swApp Is Nothing Then
        MsgBox("请先从下拉列表选择一个 SolidWorks 实例并确保它处于活动状态。")
        Exit Sub
    End If
    Dim f4 As New Form4()
    f4.SwApp = swApp
    f4.Show()
End Sub
```

- [ ] **Step 3: Commit**

```bash
git add Form1.vb Form1.Designer.vb
git commit -m "feat: wire Button18 to open Form4 coding cleanup"
```

---

### Task 5: Register Form4 in project file

**Files:**
- Modify: `外部程序.vbproj`

- [ ] **Step 1: Add Form4 to project file**

Add to the `<ItemGroup>` that contains other Compile items:

```xml
<Compile Include="Form4.vb">
  <SubType>Form</SubType>
</Compile>
<Compile Include="Form4.Designer.vb">
  <DependentUpon>Form4.vb</DependentUpon>
  <SubType>Form</SubType>
</Compile>
```

Add to the `<ItemGroup>` that contains other EmbeddedResource items:

```xml
<EmbeddedResource Include="Form4.resx">
  <DependentUpon>Form4.vb</DependentUpon>
</EmbeddedResource>
```

- [ ] **Step 2: Commit**

```bash
git add 外部程序.vbproj
git commit -m "build: register Form4 in project file"
```

---

### Task 6: Build and verify

**Files:** None (verification only)

- [ ] **Step 1: Build Release**

```bash
msbuild 外部程序.vbproj -p:Configuration=Release -p:Platform="AnyCPU" -v:minimal -t:Build
```

Expected: Build succeeds with 0 errors.

- [ ] **Step 2: Verify build output**

```bash
ls bin/Release/外部程序.exe
ls bin/Release/Form4*.dll 2>/dev/null  # Form4 may or may not produce a separate DLL
```

- [ ] **Step 3: Push to GitHub (triggers auto-build)**

```bash
git push origin master
```

---

## Verification Checklist

Manual verification in SolidWorks:
1. Open an assembly with sub-assemblies and parts
2. Open Form1, select the SW instance, click "编码整理" button
3. Verify Form4 opens with correct defaults (both type checkboxes checked, exclusions unchecked)
4. Change settings, close Form4, reopen — verify settings persisted
5. Enter a name filter, verify only matching components are processed
6. Uncheck "处理零件", verify only assemblies are processed
7. Set "零件类型" = "标准件" on a part, check "排除标准件", verify it's skipped
8. Create a virtual component in assembly, check "排除虚拟装配体", verify it's skipped
9. After execution, verify "物料编码", "零件图号", "文件名称" properties are written