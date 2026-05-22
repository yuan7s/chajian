<Global.Microsoft.VisualBasic.CompilerServices.DesignerGenerated()> _
Partial Class Form5
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
        Me.LblClose = New System.Windows.Forms.Label()
        Me.DataGridView1 = New System.Windows.Forms.DataGridView()
        Me.ColName = New System.Windows.Forms.DataGridViewTextBoxColumn()
        Me.ColValue = New System.Windows.Forms.DataGridViewTextBoxColumn()
        Me.BtnToggle = New System.Windows.Forms.Button()
        Me.BtnSettings = New System.Windows.Forms.Button()
        Me.PanelSettings = New System.Windows.Forms.Panel()
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
        CType(Me.DataGridView1, System.ComponentModel.ISupportInitialize).BeginInit()
        Me.PanelSettings.SuspendLayout()
        CType(Me.TrackBarOpacity, System.ComponentModel.ISupportInitialize).BeginInit()
        Me.SuspendLayout()
        '
        'Label1
        '
        Me.Label1.AutoSize = True
        Me.Label1.Font = New System.Drawing.Font("Microsoft YaHei UI", 10.0!, System.Drawing.FontStyle.Bold)
        Me.Label1.Location = New System.Drawing.Point(12, 9)
        Me.Label1.Name = "Label1"
        Me.Label1.Size = New System.Drawing.Size(80, 20)
        Me.Label1.TabIndex = 0
        Me.Label1.Text = "-"
        '
        'LblClose
        '
        Me.LblClose.AutoSize = True
        Me.LblClose.Cursor = System.Windows.Forms.Cursors.Hand
        Me.LblClose.Font = New System.Drawing.Font("Microsoft YaHei UI", 10.0!, System.Drawing.FontStyle.Bold)
        Me.LblClose.Location = New System.Drawing.Point(362, 7)
        Me.LblClose.Name = "LblClose"
        Me.LblClose.Size = New System.Drawing.Size(18, 20)
        Me.LblClose.TabIndex = 0
        Me.LblClose.Text = "X"
        '
        'DataGridView1
        '
        Me.DataGridView1.AllowUserToAddRows = False
        Me.DataGridView1.AllowUserToDeleteRows = False
        Me.DataGridView1.AllowUserToResizeRows = False
        Me.DataGridView1.Anchor = CType((((System.Windows.Forms.AnchorStyles.Top Or System.Windows.Forms.AnchorStyles.Bottom) _
            Or System.Windows.Forms.AnchorStyles.Left) Or System.Windows.Forms.AnchorStyles.Right), System.Windows.Forms.AnchorStyles)
        Me.DataGridView1.BackgroundColor = System.Drawing.SystemColors.Control
        Me.DataGridView1.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize
        Me.DataGridView1.Columns.AddRange(New System.Windows.Forms.DataGridViewColumn() {Me.ColName, Me.ColValue})
        Me.DataGridView1.Location = New System.Drawing.Point(12, 35)
        Me.DataGridView1.Name = "DataGridView1"
        Me.DataGridView1.ReadOnly = True
        Me.DataGridView1.RowHeadersVisible = False
        Me.DataGridView1.SelectionMode = System.Windows.Forms.DataGridViewSelectionMode.FullRowSelect
        Me.DataGridView1.Size = New System.Drawing.Size(360, 200)
        Me.DataGridView1.TabIndex = 1
        '
        'ColName
        '
        Me.ColName.HeaderText = "属性名"
        Me.ColName.Name = "ColName"
        Me.ColName.ReadOnly = True
        Me.ColName.Width = 140
        '
        'ColValue
        '
        Me.ColValue.HeaderText = "值"
        Me.ColValue.Name = "ColValue"
        Me.ColValue.ReadOnly = True
        Me.ColValue.Width = 200
        '
        'BtnToggle
        '
        Me.BtnToggle.Anchor = CType((System.Windows.Forms.AnchorStyles.Bottom Or System.Windows.Forms.AnchorStyles.Left), System.Windows.Forms.AnchorStyles)
        Me.BtnToggle.FlatStyle = System.Windows.Forms.FlatStyle.Flat
        Me.BtnToggle.Location = New System.Drawing.Point(12, 241)
        Me.BtnToggle.Name = "BtnToggle"
        Me.BtnToggle.Size = New System.Drawing.Size(90, 26)
        Me.BtnToggle.TabIndex = 2
        Me.BtnToggle.Text = "关键属性"
        '
        'BtnSettings
        '
        Me.BtnSettings.Anchor = CType((System.Windows.Forms.AnchorStyles.Bottom Or System.Windows.Forms.AnchorStyles.Left), System.Windows.Forms.AnchorStyles)
        Me.BtnSettings.FlatStyle = System.Windows.Forms.FlatStyle.Flat
        Me.BtnSettings.Location = New System.Drawing.Point(108, 241)
        Me.BtnSettings.Name = "BtnSettings"
        Me.BtnSettings.Size = New System.Drawing.Size(50, 26)
        Me.BtnSettings.TabIndex = 3
        Me.BtnSettings.Text = "设置"
        '
        'PanelSettings
        '
        Me.PanelSettings.Anchor = CType(((System.Windows.Forms.AnchorStyles.Bottom Or System.Windows.Forms.AnchorStyles.Left) _
            Or System.Windows.Forms.AnchorStyles.Right), System.Windows.Forms.AnchorStyles)
        Me.PanelSettings.Controls.Add(Me.LabelOpacity)
        Me.PanelSettings.Controls.Add(Me.TrackBarOpacity)
        Me.PanelSettings.Controls.Add(Me.CboColorScheme)
        Me.PanelSettings.Controls.Add(Me.LabelColor)
        Me.PanelSettings.Controls.Add(Me.LabelKeyProps)
        Me.PanelSettings.Controls.Add(Me.TxtNewKey)
        Me.PanelSettings.Controls.Add(Me.BtnAddKey)
        Me.PanelSettings.Controls.Add(Me.LstKeyProps)
        Me.PanelSettings.Controls.Add(Me.BtnDelKey)
        Me.PanelSettings.Controls.Add(Me.ChkTopMost)
        Me.PanelSettings.Location = New System.Drawing.Point(12, 273)
        Me.PanelSettings.Name = "PanelSettings"
        Me.PanelSettings.Size = New System.Drawing.Size(360, 200)
        Me.PanelSettings.TabIndex = 4
        Me.PanelSettings.Visible = False
        '
        'LabelOpacity
        '
        Me.LabelOpacity.AutoSize = True
        Me.LabelOpacity.Location = New System.Drawing.Point(3, 5)
        Me.LabelOpacity.Name = "LabelOpacity"
        Me.LabelOpacity.Size = New System.Drawing.Size(56, 13)
        Me.LabelOpacity.TabIndex = 0
        Me.LabelOpacity.Text = "透明度: 25%"
        '
        'TrackBarOpacity
        '
        Me.TrackBarOpacity.Location = New System.Drawing.Point(3, 21)
        Me.TrackBarOpacity.Maximum = 100
        Me.TrackBarOpacity.Minimum = 15
        Me.TrackBarOpacity.Name = "TrackBarOpacity"
        Me.TrackBarOpacity.Size = New System.Drawing.Size(200, 45)
        Me.TrackBarOpacity.TabIndex = 1
        Me.TrackBarOpacity.TickFrequency = 10
        Me.TrackBarOpacity.Value = 25
        '
        'CboColorScheme
        '
        Me.CboColorScheme.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList
        Me.CboColorScheme.FormattingEnabled = True
        Me.CboColorScheme.Items.AddRange(New Object() {"自适应", "终端绿", "白字"})
        Me.CboColorScheme.Location = New System.Drawing.Point(210, 21)
        Me.CboColorScheme.Name = "CboColorScheme"
        Me.CboColorScheme.Size = New System.Drawing.Size(80, 21)
        Me.CboColorScheme.TabIndex = 2
        '
        'LabelColor
        '
        Me.LabelColor.AutoSize = True
        Me.LabelColor.Location = New System.Drawing.Point(210, 5)
        Me.LabelColor.Name = "LabelColor"
        Me.LabelColor.Size = New System.Drawing.Size(44, 13)
        Me.LabelColor.TabIndex = 3
        Me.LabelColor.Text = "颜色方案"
        '
        'LabelKeyProps
        '
        Me.LabelKeyProps.AutoSize = True
        Me.LabelKeyProps.Location = New System.Drawing.Point(3, 65)
        Me.LabelKeyProps.Name = "LabelKeyProps"
        Me.LabelKeyProps.Size = New System.Drawing.Size(80, 13)
        Me.LabelKeyProps.TabIndex = 4
        Me.LabelKeyProps.Text = "关键属性列表"
        '
        'TxtNewKey
        '
        Me.TxtNewKey.Location = New System.Drawing.Point(3, 81)
        Me.TxtNewKey.Name = "TxtNewKey"
        Me.TxtNewKey.Size = New System.Drawing.Size(130, 21)
        Me.TxtNewKey.TabIndex = 5
        '
        'BtnAddKey
        '
        Me.BtnAddKey.FlatStyle = System.Windows.Forms.FlatStyle.Flat
        Me.BtnAddKey.Location = New System.Drawing.Point(139, 80)
        Me.BtnAddKey.Name = "BtnAddKey"
        Me.BtnAddKey.Size = New System.Drawing.Size(50, 23)
        Me.BtnAddKey.TabIndex = 6
        Me.BtnAddKey.Text = "添加"
        '
        'LstKeyProps
        '
        Me.LstKeyProps.FormattingEnabled = True
        Me.LstKeyProps.Location = New System.Drawing.Point(3, 108)
        Me.LstKeyProps.Name = "LstKeyProps"
        Me.LstKeyProps.Size = New System.Drawing.Size(186, 82)
        Me.LstKeyProps.TabIndex = 7
        '
        'BtnDelKey
        '
        Me.BtnDelKey.FlatStyle = System.Windows.Forms.FlatStyle.Flat
        Me.BtnDelKey.Location = New System.Drawing.Point(195, 108)
        Me.BtnDelKey.Name = "BtnDelKey"
        Me.BtnDelKey.Size = New System.Drawing.Size(50, 23)
        Me.BtnDelKey.TabIndex = 8
        Me.BtnDelKey.Text = "删除"
        '
        'ChkTopMost
        '
        Me.ChkTopMost.AutoSize = True
        Me.ChkTopMost.Checked = True
        Me.ChkTopMost.CheckState = System.Windows.Forms.CheckState.Checked
        Me.ChkTopMost.Location = New System.Drawing.Point(210, 135)
        Me.ChkTopMost.Name = "ChkTopMost"
        Me.ChkTopMost.Size = New System.Drawing.Size(72, 17)
        Me.ChkTopMost.TabIndex = 9
        Me.ChkTopMost.Text = "始终置顶"
        '
        'Form5
        '
        Me.AutoScaleDimensions = New System.Drawing.SizeF(6.0!, 12.0!)
        Me.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font
        Me.ClientSize = New System.Drawing.Size(384, 311)
        Me.Controls.Add(Me.PanelSettings)
        Me.Controls.Add(Me.BtnSettings)
        Me.Controls.Add(Me.BtnToggle)
        Me.Controls.Add(Me.DataGridView1)
        Me.Controls.Add(Me.LblClose)
        Me.Controls.Add(Me.Label1)
        Me.FormBorderStyle = System.Windows.Forms.FormBorderStyle.None
        Me.Name = "Form5"
        Me.ShowInTaskbar = False
        Me.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen
        Me.Text = "配置属性"
        CType(Me.DataGridView1, System.ComponentModel.ISupportInitialize).EndInit()
        Me.PanelSettings.ResumeLayout(False)
        Me.PanelSettings.PerformLayout()
        CType(Me.TrackBarOpacity, System.ComponentModel.ISupportInitialize).EndInit()
        Me.ResumeLayout(False)
        Me.PerformLayout()

    End Sub

    Friend WithEvents Label1 As System.Windows.Forms.Label
    Friend WithEvents LblClose As System.Windows.Forms.Label
    Friend WithEvents DataGridView1 As System.Windows.Forms.DataGridView
    Friend WithEvents ColName As System.Windows.Forms.DataGridViewTextBoxColumn
    Friend WithEvents ColValue As System.Windows.Forms.DataGridViewTextBoxColumn
    Friend WithEvents BtnToggle As System.Windows.Forms.Button
    Friend WithEvents BtnSettings As System.Windows.Forms.Button
    Friend WithEvents PanelSettings As System.Windows.Forms.Panel
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
End Class
