<Global.Microsoft.VisualBasic.CompilerServices.DesignerGenerated()> _
Partial Class Form6
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
        Me.LabelBgOpacity = New System.Windows.Forms.Label()
        Me.TrackBarBgOpacity = New System.Windows.Forms.TrackBar()
        Me.LabelTextOpacity = New System.Windows.Forms.Label()
        Me.TrackBarTextOpacity = New System.Windows.Forms.TrackBar()
        Me.CboColorScheme = New System.Windows.Forms.ComboBox()
        Me.LabelColor = New System.Windows.Forms.Label()
        Me.LabelKeyProps = New System.Windows.Forms.Label()
        Me.TxtNewKey = New System.Windows.Forms.TextBox()
        Me.BtnAddKey = New System.Windows.Forms.Button()
        Me.LstKeyProps = New System.Windows.Forms.ListBox()
        Me.BtnDelKey = New System.Windows.Forms.Button()
        Me.ChkTopMost = New System.Windows.Forms.CheckBox()
        Me.BtnClose = New System.Windows.Forms.Button()
        CType(Me.TrackBarBgOpacity, System.ComponentModel.ISupportInitialize).BeginInit()
        CType(Me.TrackBarTextOpacity, System.ComponentModel.ISupportInitialize).BeginInit()
        Me.SuspendLayout()
        '
        'LabelBgOpacity
        '
        Me.LabelBgOpacity.AutoSize = True
        Me.LabelBgOpacity.Location = New System.Drawing.Point(12, 12)
        Me.LabelBgOpacity.Name = "LabelBgOpacity"
        Me.LabelBgOpacity.Size = New System.Drawing.Size(80, 17)
        Me.LabelBgOpacity.TabIndex = 0
        Me.LabelBgOpacity.Text = "背景透明度: 25%"
        '
        'TrackBarBgOpacity
        '
        Me.TrackBarBgOpacity.Location = New System.Drawing.Point(12, 32)
        Me.TrackBarBgOpacity.Maximum = 100
        Me.TrackBarBgOpacity.Minimum = 5
        Me.TrackBarBgOpacity.Name = "TrackBarBgOpacity"
        Me.TrackBarBgOpacity.Size = New System.Drawing.Size(250, 45)
        Me.TrackBarBgOpacity.TabIndex = 1
        Me.TrackBarBgOpacity.TickFrequency = 10
        Me.TrackBarBgOpacity.Value = 25
        '
        'LabelTextOpacity
        '
        Me.LabelTextOpacity.AutoSize = True
        Me.LabelTextOpacity.Location = New System.Drawing.Point(12, 75)
        Me.LabelTextOpacity.Name = "LabelTextOpacity"
        Me.LabelTextOpacity.Size = New System.Drawing.Size(80, 17)
        Me.LabelTextOpacity.TabIndex = 0
        Me.LabelTextOpacity.Text = "文字透明度: 90%"
        '
        'TrackBarTextOpacity
        '
        Me.TrackBarTextOpacity.Location = New System.Drawing.Point(12, 95)
        Me.TrackBarTextOpacity.Maximum = 100
        Me.TrackBarTextOpacity.Minimum = 10
        Me.TrackBarTextOpacity.Name = "TrackBarTextOpacity"
        Me.TrackBarTextOpacity.Size = New System.Drawing.Size(250, 45)
        Me.TrackBarTextOpacity.TabIndex = 1
        Me.TrackBarTextOpacity.TickFrequency = 10
        Me.TrackBarTextOpacity.Value = 90
        '
        'CboColorScheme
        '
        Me.CboColorScheme.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList
        Me.CboColorScheme.FormattingEnabled = True
        Me.CboColorScheme.Items.AddRange(New Object() {"自适应", "终端绿", "白字"})
        Me.CboColorScheme.Location = New System.Drawing.Point(12, 160)
        Me.CboColorScheme.Name = "CboColorScheme"
        Me.CboColorScheme.Size = New System.Drawing.Size(100, 25)
        Me.CboColorScheme.TabIndex = 2
        '
        'LabelColor
        '
        Me.LabelColor.AutoSize = True
        Me.LabelColor.Location = New System.Drawing.Point(12, 140)
        Me.LabelColor.Name = "LabelColor"
        Me.LabelColor.Size = New System.Drawing.Size(56, 17)
        Me.LabelColor.TabIndex = 3
        Me.LabelColor.Text = "颜色方案"
        '
        'LabelKeyProps
        '
        Me.LabelKeyProps.AutoSize = True
        Me.LabelKeyProps.Location = New System.Drawing.Point(12, 198)
        Me.LabelKeyProps.Name = "LabelKeyProps"
        Me.LabelKeyProps.Size = New System.Drawing.Size(80, 17)
        Me.LabelKeyProps.TabIndex = 4
        Me.LabelKeyProps.Text = "关键属性列表"
        '
        'TxtNewKey
        '
        Me.TxtNewKey.Location = New System.Drawing.Point(12, 218)
        Me.TxtNewKey.Name = "TxtNewKey"
        Me.TxtNewKey.Size = New System.Drawing.Size(180, 23)
        Me.TxtNewKey.TabIndex = 5
        '
        'BtnAddKey
        '
        Me.BtnAddKey.Location = New System.Drawing.Point(198, 217)
        Me.BtnAddKey.Name = "BtnAddKey"
        Me.BtnAddKey.Size = New System.Drawing.Size(65, 25)
        Me.BtnAddKey.TabIndex = 6
        Me.BtnAddKey.Text = "添加"
        Me.BtnAddKey.UseVisualStyleBackColor = True
        '
        'LstKeyProps
        '
        Me.LstKeyProps.FormattingEnabled = True
        Me.LstKeyProps.ItemHeight = 17
        Me.LstKeyProps.Location = New System.Drawing.Point(12, 248)
        Me.LstKeyProps.Name = "LstKeyProps"
        Me.LstKeyProps.Size = New System.Drawing.Size(185, 89)
        Me.LstKeyProps.TabIndex = 7
        '
        'BtnDelKey
        '
        Me.BtnDelKey.Location = New System.Drawing.Point(203, 248)
        Me.BtnDelKey.Name = "BtnDelKey"
        Me.BtnDelKey.Size = New System.Drawing.Size(60, 25)
        Me.BtnDelKey.TabIndex = 8
        Me.BtnDelKey.Text = "删除"
        Me.BtnDelKey.UseVisualStyleBackColor = True
        '
        'ChkTopMost
        '
        Me.ChkTopMost.AutoSize = True
        Me.ChkTopMost.Location = New System.Drawing.Point(12, 348)
        Me.ChkTopMost.Name = "ChkTopMost"
        Me.ChkTopMost.Size = New System.Drawing.Size(75, 21)
        Me.ChkTopMost.TabIndex = 9
        Me.ChkTopMost.Text = "始终置顶"
        Me.ChkTopMost.UseVisualStyleBackColor = True
        '
        'BtnClose
        '
        Me.BtnClose.Location = New System.Drawing.Point(188, 344)
        Me.BtnClose.Name = "BtnClose"
        Me.BtnClose.Size = New System.Drawing.Size(75, 30)
        Me.BtnClose.TabIndex = 10
        Me.BtnClose.Text = "关闭"
        Me.BtnClose.UseVisualStyleBackColor = True
        '
        'Form6
        '
        Me.AutoScaleDimensions = New System.Drawing.SizeF(7.0!, 17.0!)
        Me.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font
        Me.ClientSize = New System.Drawing.Size(278, 386)
        Me.Controls.Add(Me.BtnClose)
        Me.Controls.Add(Me.ChkTopMost)
        Me.Controls.Add(Me.BtnDelKey)
        Me.Controls.Add(Me.LstKeyProps)
        Me.Controls.Add(Me.BtnAddKey)
        Me.Controls.Add(Me.TxtNewKey)
        Me.Controls.Add(Me.LabelKeyProps)
        Me.Controls.Add(Me.LabelColor)
        Me.Controls.Add(Me.CboColorScheme)
        Me.Controls.Add(Me.TrackBarTextOpacity)
        Me.Controls.Add(Me.LabelTextOpacity)
        Me.Controls.Add(Me.TrackBarBgOpacity)
        Me.Controls.Add(Me.LabelBgOpacity)
        Me.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog
        Me.MaximizeBox = False
        Me.MinimizeBox = False
        Me.Name = "Form6"
        Me.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent
        Me.Text = "配置属性设置"
        CType(Me.TrackBarBgOpacity, System.ComponentModel.ISupportInitialize).EndInit()
        CType(Me.TrackBarTextOpacity, System.ComponentModel.ISupportInitialize).EndInit()
        Me.ResumeLayout(False)
        Me.PerformLayout()

    End Sub

    Friend WithEvents LabelBgOpacity As System.Windows.Forms.Label
    Friend WithEvents TrackBarBgOpacity As System.Windows.Forms.TrackBar
    Friend WithEvents LabelTextOpacity As System.Windows.Forms.Label
    Friend WithEvents TrackBarTextOpacity As System.Windows.Forms.TrackBar
    Friend WithEvents CboColorScheme As System.Windows.Forms.ComboBox
    Friend WithEvents LabelColor As System.Windows.Forms.Label
    Friend WithEvents LabelKeyProps As System.Windows.Forms.Label
    Friend WithEvents TxtNewKey As System.Windows.Forms.TextBox
    Friend WithEvents BtnAddKey As System.Windows.Forms.Button
    Friend WithEvents LstKeyProps As System.Windows.Forms.ListBox
    Friend WithEvents BtnDelKey As System.Windows.Forms.Button
    Friend WithEvents ChkTopMost As System.Windows.Forms.CheckBox
    Friend WithEvents BtnClose As System.Windows.Forms.Button
End Class
