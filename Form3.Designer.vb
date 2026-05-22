<Global.Microsoft.VisualBasic.CompilerServices.DesignerGenerated()> _
Partial Class Form3
    Inherits System.Windows.Forms.Form

    'Form 重写 Dispose，以清理组件列表。
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

    'Windows 窗体设计器所必需的
    Private components As System.ComponentModel.IContainer

    '注意: 以下过程是 Windows 窗体设计器所必需的
    '可以使用 Windows 窗体设计器修改它。
    '不要使用代码编辑器修改它。
    <System.Diagnostics.DebuggerStepThrough()> _
    Private Sub InitializeComponent()
        Me.LabelTitle = New System.Windows.Forms.Label()
        Me.PanelHeader = New System.Windows.Forms.Panel()
        Me.LabelSubtitle = New System.Windows.Forms.Label()
        Me.GroupBoxName = New System.Windows.Forms.GroupBox()
        Me.Label1 = New System.Windows.Forms.Label()
        Me.Label2 = New System.Windows.Forms.Label()
        Me.Label3 = New System.Windows.Forms.Label()
        Me.Label4 = New System.Windows.Forms.Label()
        Me.Label5 = New System.Windows.Forms.Label()
        Me.Label6 = New System.Windows.Forms.Label()
        Me.TextBox1 = New System.Windows.Forms.TextBox()
        Me.TextBox2 = New System.Windows.Forms.TextBox()
        Me.GroupBoxProps = New System.Windows.Forms.GroupBox()
        Me.CheckBox5 = New System.Windows.Forms.CheckBox()
        Me.CheckBox6 = New System.Windows.Forms.CheckBox()
        Me.CheckBox7 = New System.Windows.Forms.CheckBox()
        Me.CheckBox3 = New System.Windows.Forms.CheckBox()
        Me.TextBox3 = New System.Windows.Forms.TextBox()
        Me.CheckBox4 = New System.Windows.Forms.CheckBox()
        Me.TextBox4 = New System.Windows.Forms.TextBox()
        Me.GroupBoxOptions = New System.Windows.Forms.GroupBox()
        Me.CheckBox1 = New System.Windows.Forms.CheckBox()
        Me.CheckBox2 = New System.Windows.Forms.CheckBox()
        Me.Button1 = New System.Windows.Forms.Button()
        Me.Button2 = New System.Windows.Forms.Button()
        Me.PanelHeader.SuspendLayout()
        Me.GroupBoxName.SuspendLayout()
        Me.GroupBoxProps.SuspendLayout()
        Me.GroupBoxOptions.SuspendLayout()
        Me.SuspendLayout()
        '
        'LabelTitle
        '
        Me.LabelTitle.AutoSize = True
        Me.LabelTitle.Font = New System.Drawing.Font("微软雅黑", 15.0!, System.Drawing.FontStyle.Bold)
        Me.LabelTitle.ForeColor = System.Drawing.Color.FromArgb(CType(CType(32, Byte), Integer), CType(CType(45, Byte), Integer), CType(CType(63, Byte), Integer))
        Me.LabelTitle.Location = New System.Drawing.Point(22, 14)
        Me.LabelTitle.Name = "LabelTitle"
        Me.LabelTitle.Size = New System.Drawing.Size(137, 40)
        Me.LabelTitle.TabIndex = 0
        Me.LabelTitle.Text = "重命名"
        '
        'PanelHeader
        '
        Me.PanelHeader.BackColor = System.Drawing.Color.FromArgb(CType(CType(244, Byte), Integer), CType(CType(247, Byte), Integer), CType(CType(251, Byte), Integer))
        Me.PanelHeader.Controls.Add(Me.LabelSubtitle)
        Me.PanelHeader.Controls.Add(Me.LabelTitle)
        Me.PanelHeader.Dock = System.Windows.Forms.DockStyle.Top
        Me.PanelHeader.Location = New System.Drawing.Point(0, 0)
        Me.PanelHeader.Name = "PanelHeader"
        Me.PanelHeader.Size = New System.Drawing.Size(820, 78)
        Me.PanelHeader.TabIndex = 0
        '
        'LabelSubtitle
        '
        Me.LabelSubtitle.AutoSize = True
        Me.LabelSubtitle.Font = New System.Drawing.Font("微软雅黑", 9.0!)
        Me.LabelSubtitle.ForeColor = System.Drawing.Color.FromArgb(CType(CType(96, Byte), Integer), CType(CType(108, Byte), Integer), CType(CType(125, Byte), Integer))
        Me.LabelSubtitle.Location = New System.Drawing.Point(174, 27)
        Me.LabelSubtitle.Name = "LabelSubtitle"
        Me.LabelSubtitle.Size = New System.Drawing.Size(334, 24)
        Me.LabelSubtitle.TabIndex = 1
        Me.LabelSubtitle.Text = "保存为新名称，并按勾选项写入配置属性"
        '
        'GroupBoxName
        '
        Me.GroupBoxName.Controls.Add(Me.Label1)
        Me.GroupBoxName.Controls.Add(Me.Label2)
        Me.GroupBoxName.Controls.Add(Me.Label3)
        Me.GroupBoxName.Controls.Add(Me.Label4)
        Me.GroupBoxName.Controls.Add(Me.Label5)
        Me.GroupBoxName.Controls.Add(Me.Label6)
        Me.GroupBoxName.Controls.Add(Me.TextBox1)
        Me.GroupBoxName.Controls.Add(Me.TextBox2)
        Me.GroupBoxName.Font = New System.Drawing.Font("微软雅黑", 10.0!, System.Drawing.FontStyle.Bold)
        Me.GroupBoxName.ForeColor = System.Drawing.Color.FromArgb(CType(CType(45, Byte), Integer), CType(CType(55, Byte), Integer), CType(CType(72, Byte), Integer))
        Me.GroupBoxName.Location = New System.Drawing.Point(22, 92)
        Me.GroupBoxName.Name = "GroupBoxName"
        Me.GroupBoxName.Size = New System.Drawing.Size(776, 154)
        Me.GroupBoxName.TabIndex = 1
        Me.GroupBoxName.TabStop = False
        Me.GroupBoxName.Text = "文件命名"
        '
        'Label1
        '
        Me.Label1.AutoSize = True
        Me.Label1.Font = New System.Drawing.Font("微软雅黑", 10.0!)
        Me.Label1.Location = New System.Drawing.Point(18, 42)
        Me.Label1.Name = "Label1"
        Me.Label1.Size = New System.Drawing.Size(72, 27)
        Me.Label1.TabIndex = 0
        Me.Label1.Text = "旧名称"
        '
        'Label2
        '
        Me.Label2.AutoSize = True
        Me.Label2.Font = New System.Drawing.Font("微软雅黑", 10.0!)
        Me.Label2.Location = New System.Drawing.Point(18, 96)
        Me.Label2.Name = "Label2"
        Me.Label2.Size = New System.Drawing.Size(72, 27)
        Me.Label2.TabIndex = 1
        Me.Label2.Text = "新名称"
        '
        'Label3
        '
        Me.Label3.AutoSize = True
        Me.Label3.Font = New System.Drawing.Font("微软雅黑", 9.0!)
        Me.Label3.ForeColor = System.Drawing.Color.FromArgb(CType(CType(96, Byte), Integer), CType(CType(108, Byte), Integer), CType(CType(125, Byte), Integer))
        Me.Label3.Location = New System.Drawing.Point(538, 45)
        Me.Label3.Name = "Label3"
        Me.Label3.Size = New System.Drawing.Size(65, 24)
        Me.Label3.TabIndex = 4
        Me.Label3.Text = ".sldprt"
        '
        'Label4
        '
        Me.Label4.AutoSize = True
        Me.Label4.Font = New System.Drawing.Font("微软雅黑", 9.0!)
        Me.Label4.ForeColor = System.Drawing.Color.FromArgb(CType(CType(96, Byte), Integer), CType(CType(108, Byte), Integer), CType(CType(125, Byte), Integer))
        Me.Label4.Location = New System.Drawing.Point(538, 99)
        Me.Label4.Name = "Label4"
        Me.Label4.Size = New System.Drawing.Size(65, 24)
        Me.Label4.TabIndex = 5
        Me.Label4.Text = ".sldprt"
        '
        'Label5
        '
        Me.Label5.AutoSize = True
        Me.Label5.BackColor = System.Drawing.Color.LimeGreen
        Me.Label5.Font = New System.Drawing.Font("微软雅黑", 9.0!, System.Drawing.FontStyle.Bold)
        Me.Label5.ForeColor = System.Drawing.Color.White
        Me.Label5.Location = New System.Drawing.Point(627, 42)
        Me.Label5.MinimumSize = New System.Drawing.Size(104, 30)
        Me.Label5.Name = "Label5"
        Me.Label5.Padding = New System.Windows.Forms.Padding(8, 2, 8, 2)
        Me.Label5.Size = New System.Drawing.Size(104, 30)
        Me.Label5.TabIndex = 6
        Me.Label5.Text = "存在工程图"
        Me.Label5.TextAlign = System.Drawing.ContentAlignment.MiddleCenter
        '
        'Label6
        '
        Me.Label6.AutoSize = True
        Me.Label6.BackColor = System.Drawing.SystemColors.Control
        Me.Label6.Font = New System.Drawing.Font("微软雅黑", 9.0!, System.Drawing.FontStyle.Bold)
        Me.Label6.ForeColor = System.Drawing.Color.Green
        Me.Label6.Location = New System.Drawing.Point(627, 96)
        Me.Label6.MinimumSize = New System.Drawing.Size(104, 30)
        Me.Label6.Name = "Label6"
        Me.Label6.Padding = New System.Windows.Forms.Padding(8, 2, 8, 2)
        Me.Label6.Size = New System.Drawing.Size(104, 30)
        Me.Label6.TabIndex = 7
        Me.Label6.Text = "可保存"
        Me.Label6.TextAlign = System.Drawing.ContentAlignment.MiddleCenter
        '
        'TextBox1
        '
        Me.TextBox1.BackColor = System.Drawing.Color.FromArgb(CType(CType(248, Byte), Integer), CType(CType(250, Byte), Integer), CType(CType(252, Byte), Integer))
        Me.TextBox1.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle
        Me.TextBox1.Font = New System.Drawing.Font("微软雅黑", 10.5!)
        Me.TextBox1.Location = New System.Drawing.Point(108, 39)
        Me.TextBox1.Name = "TextBox1"
        Me.TextBox1.Size = New System.Drawing.Size(416, 35)
        Me.TextBox1.TabIndex = 2
        '
        'TextBox2
        '
        Me.TextBox2.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle
        Me.TextBox2.Font = New System.Drawing.Font("微软雅黑", 10.5!)
        Me.TextBox2.Location = New System.Drawing.Point(108, 93)
        Me.TextBox2.Name = "TextBox2"
        Me.TextBox2.Size = New System.Drawing.Size(416, 35)
        Me.TextBox2.TabIndex = 3
        '
        'GroupBoxProps
        '
        Me.GroupBoxProps.Controls.Add(Me.CheckBox5)
        Me.GroupBoxProps.Controls.Add(Me.CheckBox6)
        Me.GroupBoxProps.Controls.Add(Me.CheckBox7)
        Me.GroupBoxProps.Controls.Add(Me.CheckBox3)
        Me.GroupBoxProps.Controls.Add(Me.TextBox3)
        Me.GroupBoxProps.Controls.Add(Me.CheckBox4)
        Me.GroupBoxProps.Controls.Add(Me.TextBox4)
        Me.GroupBoxProps.Font = New System.Drawing.Font("微软雅黑", 10.0!, System.Drawing.FontStyle.Bold)
        Me.GroupBoxProps.ForeColor = System.Drawing.Color.FromArgb(CType(CType(45, Byte), Integer), CType(CType(55, Byte), Integer), CType(CType(72, Byte), Integer))
        Me.GroupBoxProps.Location = New System.Drawing.Point(22, 262)
        Me.GroupBoxProps.Name = "GroupBoxProps"
        Me.GroupBoxProps.Size = New System.Drawing.Size(500, 178)
        Me.GroupBoxProps.TabIndex = 2
        Me.GroupBoxProps.TabStop = False
        Me.GroupBoxProps.Text = "写入属性"
        '
        'CheckBox5
        '
        Me.CheckBox5.AutoSize = True
        Me.CheckBox5.Checked = True
        Me.CheckBox5.CheckState = System.Windows.Forms.CheckState.Checked
        Me.CheckBox5.Font = New System.Drawing.Font("微软雅黑", 9.0!)
        Me.CheckBox5.Location = New System.Drawing.Point(24, 39)
        Me.CheckBox5.Name = "CheckBox5"
        Me.CheckBox5.Size = New System.Drawing.Size(108, 28)
        Me.CheckBox5.TabIndex = 0
        Me.CheckBox5.Text = "文件名称"
        Me.CheckBox5.UseVisualStyleBackColor = True
        '
        'CheckBox6
        '
        Me.CheckBox6.AutoSize = True
        Me.CheckBox6.Checked = True
        Me.CheckBox6.CheckState = System.Windows.Forms.CheckState.Checked
        Me.CheckBox6.Font = New System.Drawing.Font("微软雅黑", 9.0!)
        Me.CheckBox6.Location = New System.Drawing.Point(170, 39)
        Me.CheckBox6.Name = "CheckBox6"
        Me.CheckBox6.Size = New System.Drawing.Size(108, 28)
        Me.CheckBox6.TabIndex = 1
        Me.CheckBox6.Text = "物料编码"
        Me.CheckBox6.UseVisualStyleBackColor = True
        '
        'CheckBox7
        '
        Me.CheckBox7.AutoSize = True
        Me.CheckBox7.Checked = True
        Me.CheckBox7.CheckState = System.Windows.Forms.CheckState.Checked
        Me.CheckBox7.Font = New System.Drawing.Font("微软雅黑", 9.0!)
        Me.CheckBox7.Location = New System.Drawing.Point(316, 39)
        Me.CheckBox7.Name = "CheckBox7"
        Me.CheckBox7.Size = New System.Drawing.Size(108, 28)
        Me.CheckBox7.TabIndex = 2
        Me.CheckBox7.Text = "零件图号"
        Me.CheckBox7.UseVisualStyleBackColor = True
        '
        'CheckBox3
        '
        Me.CheckBox3.AutoSize = True
        Me.CheckBox3.Font = New System.Drawing.Font("微软雅黑", 9.0!)
        Me.CheckBox3.Location = New System.Drawing.Point(24, 92)
        Me.CheckBox3.Name = "CheckBox3"
        Me.CheckBox3.Size = New System.Drawing.Size(72, 28)
        Me.CheckBox3.TabIndex = 3
        Me.CheckBox3.Text = "版本"
        Me.CheckBox3.UseVisualStyleBackColor = True
        '
        'TextBox3
        '
        Me.TextBox3.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle
        Me.TextBox3.Font = New System.Drawing.Font("微软雅黑", 9.0!)
        Me.TextBox3.Location = New System.Drawing.Point(105, 91)
        Me.TextBox3.Name = "TextBox3"
        Me.TextBox3.Size = New System.Drawing.Size(70, 31)
        Me.TextBox3.TabIndex = 4
        Me.TextBox3.Text = "A"
        '
        'CheckBox4
        '
        Me.CheckBox4.AutoSize = True
        Me.CheckBox4.Font = New System.Drawing.Font("微软雅黑", 9.0!)
        Me.CheckBox4.Location = New System.Drawing.Point(214, 92)
        Me.CheckBox4.Name = "CheckBox4"
        Me.CheckBox4.Size = New System.Drawing.Size(108, 28)
        Me.CheckBox4.TabIndex = 5
        Me.CheckBox4.Text = "设计/出图"
        Me.CheckBox4.UseVisualStyleBackColor = True
        '
        'TextBox4
        '
        Me.TextBox4.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle
        Me.TextBox4.Font = New System.Drawing.Font("微软雅黑", 9.0!)
        Me.TextBox4.Location = New System.Drawing.Point(350, 91)
        Me.TextBox4.Name = "TextBox4"
        Me.TextBox4.Size = New System.Drawing.Size(112, 31)
        Me.TextBox4.TabIndex = 6
        '
        'GroupBoxOptions
        '
        Me.GroupBoxOptions.Controls.Add(Me.CheckBox1)
        Me.GroupBoxOptions.Controls.Add(Me.CheckBox2)
        Me.GroupBoxOptions.Font = New System.Drawing.Font("微软雅黑", 10.0!, System.Drawing.FontStyle.Bold)
        Me.GroupBoxOptions.ForeColor = System.Drawing.Color.FromArgb(CType(CType(45, Byte), Integer), CType(CType(55, Byte), Integer), CType(CType(72, Byte), Integer))
        Me.GroupBoxOptions.Location = New System.Drawing.Point(544, 262)
        Me.GroupBoxOptions.Name = "GroupBoxOptions"
        Me.GroupBoxOptions.Size = New System.Drawing.Size(254, 110)
        Me.GroupBoxOptions.TabIndex = 3
        Me.GroupBoxOptions.TabStop = False
        Me.GroupBoxOptions.Text = "附加选项"
        '
        'CheckBox1
        '
        Me.CheckBox1.AutoSize = True
        Me.CheckBox1.Checked = True
        Me.CheckBox1.CheckState = System.Windows.Forms.CheckState.Checked
        Me.CheckBox1.Font = New System.Drawing.Font("微软雅黑", 9.0!)
        Me.CheckBox1.Location = New System.Drawing.Point(24, 38)
        Me.CheckBox1.Name = "CheckBox1"
        Me.CheckBox1.Size = New System.Drawing.Size(108, 28)
        Me.CheckBox1.TabIndex = 0
        Me.CheckBox1.Text = "带图改名"
        Me.CheckBox1.UseVisualStyleBackColor = True
        '
        'CheckBox2
        '
        Me.CheckBox2.AutoSize = True
        Me.CheckBox2.Font = New System.Drawing.Font("微软雅黑", 9.0!)
        Me.CheckBox2.Location = New System.Drawing.Point(24, 72)
        Me.CheckBox2.Name = "CheckBox2"
        Me.CheckBox2.Size = New System.Drawing.Size(108, 28)
        Me.CheckBox2.TabIndex = 1
        Me.CheckBox2.Text = "下料尺寸"
        Me.CheckBox2.UseVisualStyleBackColor = True
        '
        'Button1
        '
        Me.Button1.BackColor = System.Drawing.Color.White
        Me.Button1.FlatStyle = System.Windows.Forms.FlatStyle.Flat
        Me.Button1.Font = New System.Drawing.Font("微软雅黑", 10.0!)
        Me.Button1.ForeColor = System.Drawing.Color.FromArgb(CType(CType(45, Byte), Integer), CType(CType(55, Byte), Integer), CType(CType(72, Byte), Integer))
        Me.Button1.Location = New System.Drawing.Point(544, 392)
        Me.Button1.Name = "Button1"
        Me.Button1.Size = New System.Drawing.Size(118, 48)
        Me.Button1.TabIndex = 4
        Me.Button1.Text = "另存"
        Me.Button1.UseVisualStyleBackColor = False
        '
        'Button2
        '
        Me.Button2.BackColor = System.Drawing.Color.FromArgb(CType(CType(37, Byte), Integer), CType(CType(99, Byte), Integer), CType(CType(235, Byte), Integer))
        Me.Button2.FlatStyle = System.Windows.Forms.FlatStyle.Flat
        Me.Button2.Font = New System.Drawing.Font("微软雅黑", 10.0!, System.Drawing.FontStyle.Bold)
        Me.Button2.ForeColor = System.Drawing.Color.White
        Me.Button2.Location = New System.Drawing.Point(680, 392)
        Me.Button2.Name = "Button2"
        Me.Button2.Size = New System.Drawing.Size(118, 48)
        Me.Button2.TabIndex = 5
        Me.Button2.Text = "重命名"
        Me.Button2.UseVisualStyleBackColor = False
        '
        'Form3
        '
        Me.AutoScaleDimensions = New System.Drawing.SizeF(9.0!, 18.0!)
        Me.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font
        Me.BackColor = System.Drawing.Color.White
        Me.ClientSize = New System.Drawing.Size(820, 462)
        Me.Controls.Add(Me.Button2)
        Me.Controls.Add(Me.Button1)
        Me.Controls.Add(Me.GroupBoxOptions)
        Me.Controls.Add(Me.GroupBoxProps)
        Me.Controls.Add(Me.GroupBoxName)
        Me.Controls.Add(Me.PanelHeader)
        Me.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog
        Me.MaximizeBox = False
        Me.MinimizeBox = False
        Me.Name = "Form3"
        Me.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen
        Me.Text = "重命名"
        Me.PanelHeader.ResumeLayout(False)
        Me.PanelHeader.PerformLayout()
        Me.GroupBoxName.ResumeLayout(False)
        Me.GroupBoxName.PerformLayout()
        Me.GroupBoxProps.ResumeLayout(False)
        Me.GroupBoxProps.PerformLayout()
        Me.GroupBoxOptions.ResumeLayout(False)
        Me.GroupBoxOptions.PerformLayout()
        Me.ResumeLayout(False)

    End Sub

    Friend WithEvents LabelTitle As Label
    Friend WithEvents LabelSubtitle As Label
    Friend WithEvents PanelHeader As Panel
    Friend WithEvents GroupBoxName As GroupBox
    Friend WithEvents GroupBoxProps As GroupBox
    Friend WithEvents GroupBoxOptions As GroupBox
    Friend WithEvents Label1 As Label
    Friend WithEvents Label2 As Label
    Friend WithEvents Label3 As Label
    Friend WithEvents Label4 As Label
    Friend WithEvents Button1 As Button
    Friend WithEvents Button2 As Button
    Friend WithEvents CheckBox1 As CheckBox
    Friend WithEvents CheckBox2 As CheckBox
    Friend WithEvents CheckBox3 As CheckBox
    Friend WithEvents CheckBox4 As CheckBox
    Friend WithEvents CheckBox5 As CheckBox
    Friend WithEvents CheckBox6 As CheckBox
    Friend WithEvents CheckBox7 As CheckBox
    Friend WithEvents Label5 As Label
    Friend WithEvents Label6 As Label
    Friend WithEvents TextBox1 As TextBox
    Friend WithEvents TextBox2 As TextBox
    Friend WithEvents TextBox3 As TextBox
    Friend WithEvents TextBox4 As TextBox
End Class
