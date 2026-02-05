<Global.Microsoft.VisualBasic.CompilerServices.DesignerGenerated()> _
Partial Class Form1
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
        Me.Button1 = New System.Windows.Forms.Button()
        Me.tabpage3 = New System.Windows.Forms.TabControl()
        Me.TabPage1 = New System.Windows.Forms.TabPage()
        Me.Button8 = New System.Windows.Forms.Button()
        Me.Button7 = New System.Windows.Forms.Button()
        Me.Button2 = New System.Windows.Forms.Button()
        Me.TabPage2 = New System.Windows.Forms.TabPage()
        Me.Button9 = New System.Windows.Forms.Button()
        Me.Button5 = New System.Windows.Forms.Button()
        Me.Button4 = New System.Windows.Forms.Button()
        Me.TabPage4 = New System.Windows.Forms.TabPage()
        Me.Button13 = New System.Windows.Forms.Button()
        Me.Button6 = New System.Windows.Forms.Button()
        Me.Button3 = New System.Windows.Forms.Button()
        Me.Button11 = New System.Windows.Forms.Button()
        Me.Button12 = New System.Windows.Forms.Button()
        Me.tabpage3.SuspendLayout()
        Me.TabPage1.SuspendLayout()
        Me.TabPage2.SuspendLayout()
        Me.TabPage4.SuspendLayout()
        Me.SuspendLayout()
        '
        'Button1
        '
        Me.Button1.Location = New System.Drawing.Point(6, 24)
        Me.Button1.Name = "Button1"
        Me.Button1.Size = New System.Drawing.Size(122, 50)
        Me.Button1.TabIndex = 0
        Me.Button1.Text = "另存DWG"
        Me.Button1.UseVisualStyleBackColor = True
        '
        'tabpage3
        '
        Me.tabpage3.Controls.Add(Me.TabPage1)
        Me.tabpage3.Controls.Add(Me.TabPage2)
        Me.tabpage3.Controls.Add(Me.TabPage4)
        Me.tabpage3.Location = New System.Drawing.Point(12, 176)
        Me.tabpage3.Name = "tabpage3"
        Me.tabpage3.Padding = New System.Drawing.Point(12, 8)
        Me.tabpage3.SelectedIndex = 0
        Me.tabpage3.Size = New System.Drawing.Size(776, 262)
        Me.tabpage3.TabIndex = 1
        '
        'TabPage1
        '
        Me.TabPage1.Controls.Add(Me.Button8)
        Me.TabPage1.Controls.Add(Me.Button7)
        Me.TabPage1.Controls.Add(Me.Button2)
        Me.TabPage1.Controls.Add(Me.Button1)
        Me.TabPage1.Location = New System.Drawing.Point(4, 38)
        Me.TabPage1.Name = "TabPage1"
        Me.TabPage1.Padding = New System.Windows.Forms.Padding(3)
        Me.TabPage1.Size = New System.Drawing.Size(768, 220)
        Me.TabPage1.TabIndex = 0
        Me.TabPage1.Text = "工程图"
        Me.TabPage1.UseVisualStyleBackColor = True
        '
        'Button8
        '
        Me.Button8.Location = New System.Drawing.Point(445, 24)
        Me.Button8.Name = "Button8"
        Me.Button8.Size = New System.Drawing.Size(122, 50)
        Me.Button8.TabIndex = 3
        Me.Button8.Text = "旋转视图"
        Me.Button8.UseVisualStyleBackColor = True
        '
        'Button7
        '
        Me.Button7.Location = New System.Drawing.Point(295, 24)
        Me.Button7.Name = "Button7"
        Me.Button7.Size = New System.Drawing.Size(122, 50)
        Me.Button7.TabIndex = 2
        Me.Button7.Text = "悬空尺寸"
        Me.Button7.UseVisualStyleBackColor = True
        '
        'Button2
        '
        Me.Button2.Location = New System.Drawing.Point(147, 24)
        Me.Button2.Name = "Button2"
        Me.Button2.Size = New System.Drawing.Size(122, 50)
        Me.Button2.TabIndex = 1
        Me.Button2.Text = "另存pdf"
        Me.Button2.UseVisualStyleBackColor = True
        '
        'TabPage2
        '
        Me.TabPage2.Controls.Add(Me.Button9)
        Me.TabPage2.Controls.Add(Me.Button5)
        Me.TabPage2.Location = New System.Drawing.Point(4, 38)
        Me.TabPage2.Name = "TabPage2"
        Me.TabPage2.Padding = New System.Windows.Forms.Padding(3)
        Me.TabPage2.Size = New System.Drawing.Size(768, 220)
        Me.TabPage2.TabIndex = 1
        Me.TabPage2.Text = "零件"
        Me.TabPage2.UseVisualStyleBackColor = True
        '
        'Button9
        '
        Me.Button9.Location = New System.Drawing.Point(333, 27)
        Me.Button9.Name = "Button9"
        Me.Button9.Size = New System.Drawing.Size(122, 50)
        Me.Button9.TabIndex = 4
        Me.Button9.Text = "图号编码"
        Me.Button9.UseVisualStyleBackColor = True
        '
        'Button5
        '
        Me.Button5.Location = New System.Drawing.Point(178, 27)
        Me.Button5.Name = "Button5"
        Me.Button5.Size = New System.Drawing.Size(122, 50)
        Me.Button5.TabIndex = 3
        Me.Button5.Text = "绘图标准ISO"
        Me.Button5.UseVisualStyleBackColor = True
        '
        'Button4
        '
        Me.Button4.Location = New System.Drawing.Point(383, 14)
        Me.Button4.Name = "Button4"
        Me.Button4.Size = New System.Drawing.Size(122, 50)
        Me.Button4.TabIndex = 2
        Me.Button4.Text = "下料尺寸"
        Me.Button4.UseVisualStyleBackColor = True
        '
        'TabPage4
        '
        Me.TabPage4.Controls.Add(Me.Button13)
        Me.TabPage4.Controls.Add(Me.Button6)
        Me.TabPage4.Location = New System.Drawing.Point(4, 38)
        Me.TabPage4.Name = "TabPage4"
        Me.TabPage4.Padding = New System.Windows.Forms.Padding(3)
        Me.TabPage4.Size = New System.Drawing.Size(768, 220)
        Me.TabPage4.TabIndex = 2
        Me.TabPage4.Text = "装配体"
        Me.TabPage4.UseVisualStyleBackColor = True
        '
        'Button13
        '
        Me.Button13.Location = New System.Drawing.Point(230, 34)
        Me.Button13.Name = "Button13"
        Me.Button13.Size = New System.Drawing.Size(122, 50)
        Me.Button13.TabIndex = 6
        Me.Button13.Text = "装配体排序"
        Me.Button13.UseVisualStyleBackColor = True
        '
        'Button6
        '
        Me.Button6.Location = New System.Drawing.Point(45, 34)
        Me.Button6.Name = "Button6"
        Me.Button6.Size = New System.Drawing.Size(122, 50)
        Me.Button6.TabIndex = 4
        Me.Button6.Text = "绘图标准ISO"
        Me.Button6.UseVisualStyleBackColor = True
        '
        'Button3
        '
        Me.Button3.Location = New System.Drawing.Point(12, 12)
        Me.Button3.Name = "Button3"
        Me.Button3.Size = New System.Drawing.Size(143, 50)
        Me.Button3.TabIndex = 2
        Me.Button3.Text = "打开文件目录"
        Me.Button3.UseVisualStyleBackColor = True
        '
        'Button11
        '
        Me.Button11.Location = New System.Drawing.Point(191, 14)
        Me.Button11.Name = "Button11"
        Me.Button11.Size = New System.Drawing.Size(143, 50)
        Me.Button11.TabIndex = 3
        Me.Button11.Text = "设计树"
        Me.Button11.UseVisualStyleBackColor = True
        '
        'Button12
        '
        Me.Button12.Location = New System.Drawing.Point(667, 14)
        Me.Button12.Name = "Button12"
        Me.Button12.Size = New System.Drawing.Size(103, 38)
        Me.Button12.TabIndex = 4
        Me.Button12.Text = "置顶"
        Me.Button12.UseVisualStyleBackColor = True
        '
        'Form1
        '
        Me.AutoScaleDimensions = New System.Drawing.SizeF(9.0!, 18.0!)
        Me.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font
        Me.ClientSize = New System.Drawing.Size(800, 450)
        Me.Controls.Add(Me.Button12)
        Me.Controls.Add(Me.Button11)
        Me.Controls.Add(Me.Button4)
        Me.Controls.Add(Me.Button3)
        Me.Controls.Add(Me.tabpage3)
        Me.Name = "Form1"
        Me.Text = "插件"
        Me.tabpage3.ResumeLayout(False)
        Me.TabPage1.ResumeLayout(False)
        Me.TabPage2.ResumeLayout(False)
        Me.TabPage4.ResumeLayout(False)
        Me.ResumeLayout(False)

    End Sub

    Friend WithEvents Button1 As Button
    Friend WithEvents tabpage3 As TabControl
    Friend WithEvents TabPage1 As TabPage
    Friend WithEvents TabPage2 As TabPage
    Friend WithEvents Button2 As Button
    Friend WithEvents Button3 As Button
    Friend WithEvents Button4 As Button
    Friend WithEvents Button5 As Button
    Friend WithEvents TabPage4 As TabPage
    Friend WithEvents Button6 As Button
    Friend WithEvents Button7 As Button
    Friend WithEvents Button8 As Button
    Friend WithEvents Button9 As Button
    Friend WithEvents Button11 As Button
    Friend WithEvents Button12 As Button
    Friend WithEvents Button13 As Button
End Class
