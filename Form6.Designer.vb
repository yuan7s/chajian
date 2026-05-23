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
        Me.LabelOpacity = New System.Windows.Forms.Label()
        Me.TrackBarOpacity = New System.Windows.Forms.TrackBar()
        Me.CboColorScheme = New System.Windows.Forms.ComboBox()
        Me.LabelColor = New System.Windows.Forms.Label()
        Me.LabelKeyProps = New System.Windows.Forms.Label()
        Me.TxtNewKey = New System.Windows.Forms.TextBox()
        Me.BtnAddKey = New System.Windows.Forms.Button()
        Me.LstKeyProps = New System.Windows.Forms.ListBox()
        Me.BtnDelKey = New System.Windows.Forms.Button()
        Me.ChkTopMost = New System.Windows.Forms.CheckBox()
        Me.ChkMouseThrough = New System.Windows.Forms.CheckBox()
        Me.BtnClose = New System.Windows.Forms.Button()
        Me.LabelDisplayMode = New System.Windows.Forms.Label()
        Me.CboDisplayMode = New System.Windows.Forms.ComboBox()
        Me.LabelPropSource = New System.Windows.Forms.Label()
        Me.CboPropSource = New System.Windows.Forms.ComboBox()
        CType(Me.TrackBarOpacity, System.ComponentModel.ISupportInitialize).BeginInit()
        Me.SuspendLayout()
        '
        'LabelOpacity
        '
        Me.LabelOpacity.AutoSize = True
        Me.LabelOpacity.Location = New System.Drawing.Point(12, 15)
        Me.LabelOpacity.Name = "LabelOpacity"
        Me.LabelOpacity.Size = New System.Drawing.Size(68, 17)
        Me.LabelOpacity.TabIndex = 0
        Me.LabelOpacity.Text = "透明度: 25%"
        '
        'TrackBarOpacity
        '
        Me.TrackBarOpacity.Location = New System.Drawing.Point(12, 35)
        Me.TrackBarOpacity.Maximum = 100
        Me.TrackBarOpacity.Minimum = 15
        Me.TrackBarOpacity.Name = "TrackBarOpacity"
        Me.TrackBarOpacity.Size = New System.Drawing.Size(250, 45)
        Me.TrackBarOpacity.TabIndex = 1
        Me.TrackBarOpacity.TickFrequency = 10
        Me.TrackBarOpacity.Value = 25
        '
        'CboColorScheme
        '
        Me.CboColorScheme.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList
        Me.CboColorScheme.FormattingEnabled = True
        Me.CboColorScheme.Items.AddRange(New Object() {"自适应", "终端绿", "白字"})
        Me.CboColorScheme.Location = New System.Drawing.Point(12, 105)
        Me.CboColorScheme.Name = "CboColorScheme"
        Me.CboColorScheme.Size = New System.Drawing.Size(100, 25)
        Me.CboColorScheme.TabIndex = 2
        '
        'LabelColor
        '
        Me.LabelColor.AutoSize = True
        Me.LabelColor.Location = New System.Drawing.Point(12, 85)
        Me.LabelColor.Name = "LabelColor"
        Me.LabelColor.Size = New System.Drawing.Size(56, 17)
        Me.LabelColor.TabIndex = 3
        Me.LabelColor.Text = "颜色方案"
        '
        'LabelKeyProps
        '
        Me.LabelKeyProps.AutoSize = True
        Me.LabelKeyProps.Location = New System.Drawing.Point(12, 205)
        Me.LabelKeyProps.Name = "LabelKeyProps"
        Me.LabelKeyProps.Size = New System.Drawing.Size(80, 17)
        Me.LabelKeyProps.TabIndex = 4
        Me.LabelKeyProps.Text = "关键属性列表"
        '
        'TxtNewKey
        '
        Me.TxtNewKey.Location = New System.Drawing.Point(12, 225)
        Me.TxtNewKey.Name = "TxtNewKey"
        Me.TxtNewKey.Size = New System.Drawing.Size(180, 23)
        Me.TxtNewKey.TabIndex = 5
        '
        'BtnAddKey
        '
        Me.BtnAddKey.Location = New System.Drawing.Point(198, 224)
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
        Me.LstKeyProps.Location = New System.Drawing.Point(12, 255)
        Me.LstKeyProps.Name = "LstKeyProps"
        Me.LstKeyProps.Size = New System.Drawing.Size(185, 106)
        Me.LstKeyProps.TabIndex = 7
        '
        'BtnDelKey
        '
        Me.BtnDelKey.Location = New System.Drawing.Point(203, 255)
        Me.BtnDelKey.Name = "BtnDelKey"
        Me.BtnDelKey.Size = New System.Drawing.Size(60, 25)
        Me.BtnDelKey.TabIndex = 8
        Me.BtnDelKey.Text = "删除"
        Me.BtnDelKey.UseVisualStyleBackColor = True
        '
        'ChkTopMost
        '
        Me.ChkTopMost.AutoSize = True
        Me.ChkTopMost.Location = New System.Drawing.Point(12, 375)
        Me.ChkTopMost.Name = "ChkTopMost"
        Me.ChkTopMost.Size = New System.Drawing.Size(75, 21)
        Me.ChkTopMost.TabIndex = 9
        Me.ChkTopMost.Text = "始终置顶"
        Me.ChkTopMost.UseVisualStyleBackColor = True
        '
        'ChkMouseThrough
        '
        Me.ChkMouseThrough.AutoSize = True
        Me.ChkMouseThrough.Location = New System.Drawing.Point(103, 375)
        Me.ChkMouseThrough.Name = "ChkMouseThrough"
        Me.ChkMouseThrough.Size = New System.Drawing.Size(75, 21)
        Me.ChkMouseThrough.TabIndex = 10
        Me.ChkMouseThrough.Text = "鼠标穿透"
        Me.ChkMouseThrough.UseVisualStyleBackColor = True
        '
        'BtnClose
        '
        Me.BtnClose.Location = New System.Drawing.Point(188, 371)
        Me.BtnClose.Name = "BtnClose"
        Me.BtnClose.Size = New System.Drawing.Size(75, 30)
        Me.BtnClose.TabIndex = 11
        Me.BtnClose.Text = "关闭"
        Me.BtnClose.UseVisualStyleBackColor = True
        '
        'LabelDisplayMode
        '
        Me.LabelDisplayMode.AutoSize = True
        Me.LabelDisplayMode.Location = New System.Drawing.Point(12, 145)
        Me.LabelDisplayMode.Name = "LabelDisplayMode"
        Me.LabelDisplayMode.Size = New System.Drawing.Size(56, 17)
        Me.LabelDisplayMode.TabIndex = 12
        Me.LabelDisplayMode.Text = "显示范围"
        '
        'CboDisplayMode
        '
        Me.CboDisplayMode.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList
        Me.CboDisplayMode.FormattingEnabled = True
        Me.CboDisplayMode.Items.AddRange(New Object() {"关键属性", "全部属性"})
        Me.CboDisplayMode.Location = New System.Drawing.Point(12, 165)
        Me.CboDisplayMode.Name = "CboDisplayMode"
        Me.CboDisplayMode.Size = New System.Drawing.Size(100, 25)
        Me.CboDisplayMode.TabIndex = 13
        '
        'LabelPropSource
        '
        Me.LabelPropSource.AutoSize = True
        Me.LabelPropSource.Location = New System.Drawing.Point(132, 145)
        Me.LabelPropSource.Name = "LabelPropSource"
        Me.LabelPropSource.Size = New System.Drawing.Size(56, 17)
        Me.LabelPropSource.TabIndex = 14
        Me.LabelPropSource.Text = "属性来源"
        '
        'CboPropSource
        '
        Me.CboPropSource.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList
        Me.CboPropSource.FormattingEnabled = True
        Me.CboPropSource.Items.AddRange(New Object() {"配置属性", "自定义属性"})
        Me.CboPropSource.Location = New System.Drawing.Point(132, 165)
        Me.CboPropSource.Name = "CboPropSource"
        Me.CboPropSource.Size = New System.Drawing.Size(100, 25)
        Me.CboPropSource.TabIndex = 15
        '
        'Form6
        '
        Me.AutoScaleDimensions = New System.Drawing.SizeF(7.0!, 17.0!)
        Me.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font
        Me.ClientSize = New System.Drawing.Size(278, 413)
        Me.Controls.Add(Me.CboPropSource)
        Me.Controls.Add(Me.LabelPropSource)
        Me.Controls.Add(Me.CboDisplayMode)
        Me.Controls.Add(Me.LabelDisplayMode)
        Me.Controls.Add(Me.BtnClose)
        Me.Controls.Add(Me.ChkMouseThrough)
        Me.Controls.Add(Me.ChkTopMost)
        Me.Controls.Add(Me.BtnDelKey)
        Me.Controls.Add(Me.LstKeyProps)
        Me.Controls.Add(Me.BtnAddKey)
        Me.Controls.Add(Me.TxtNewKey)
        Me.Controls.Add(Me.LabelKeyProps)
        Me.Controls.Add(Me.LabelColor)
        Me.Controls.Add(Me.CboColorScheme)
        Me.Controls.Add(Me.TrackBarOpacity)
        Me.Controls.Add(Me.LabelOpacity)
        Me.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog
        Me.MaximizeBox = False
        Me.MinimizeBox = False
        Me.Name = "Form6"
        Me.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent
        Me.Text = "配置属性设置"
        CType(Me.TrackBarOpacity, System.ComponentModel.ISupportInitialize).EndInit()
        Me.ResumeLayout(False)
        Me.PerformLayout()

    End Sub

    Friend WithEvents LabelOpacity As System.Windows.Forms.Label
    Friend WithEvents TrackBarOpacity As System.Windows.Forms.TrackBar
    Friend WithEvents CboColorScheme As System.Windows.Forms.ComboBox
    Friend WithEvents LabelColor As System.Windows.Forms.Label
    Friend WithEvents LabelKeyProps As System.Windows.Forms.Label
    Friend WithEvents TxtNewKey As System.Windows.Forms.TextBox
    Friend WithEvents BtnAddKey As System.Windows.Forms.Button
    Friend WithEvents LstKeyProps As System.Windows.Forms.ListBox
    Friend WithEvents BtnDelKey As System.Windows.Forms.Button
    Friend WithEvents ChkTopMost As System.Windows.Forms.CheckBox
    Friend WithEvents ChkMouseThrough As System.Windows.Forms.CheckBox
    Friend WithEvents BtnClose As System.Windows.Forms.Button
    Friend WithEvents LabelDisplayMode As System.Windows.Forms.Label
    Friend WithEvents CboDisplayMode As System.Windows.Forms.ComboBox
    Friend WithEvents LabelPropSource As System.Windows.Forms.Label
    Friend WithEvents CboPropSource As System.Windows.Forms.ComboBox
End Class
