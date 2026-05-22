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
        Me.BtnPropType = New System.Windows.Forms.Button()
        Me.DataGridView1 = New System.Windows.Forms.DataGridView()
        Me.ColName = New System.Windows.Forms.DataGridViewTextBoxColumn()
        Me.ColValue = New System.Windows.Forms.DataGridViewTextBoxColumn()
        Me.BtnToggle = New System.Windows.Forms.Button()
        CType(Me.DataGridView1, System.ComponentModel.ISupportInitialize).BeginInit()
        Me.SuspendLayout()
        '
        'Label1
        '
        Me.Label1.AutoSize = True
        Me.Label1.Font = New System.Drawing.Font("Microsoft YaHei UI", 10.0!, System.Drawing.FontStyle.Bold)
        Me.Label1.Location = New System.Drawing.Point(18, 14)
        Me.Label1.Margin = New System.Windows.Forms.Padding(4, 0, 4, 0)
        Me.Label1.Name = "Label1"
        Me.Label1.Size = New System.Drawing.Size(17, 24)
        Me.Label1.TabIndex = 0
        Me.Label1.Text = "-"
        '
        'LblClose
        '
        Me.LblClose.AutoSize = True
        Me.LblClose.Cursor = System.Windows.Forms.Cursors.Hand
        Me.LblClose.Font = New System.Drawing.Font("Microsoft YaHei UI", 10.0!, System.Drawing.FontStyle.Bold)
        Me.LblClose.Location = New System.Drawing.Point(541, 9)
        Me.LblClose.Margin = New System.Windows.Forms.Padding(4, 0, 4, 0)
        Me.LblClose.Name = "LblClose"
        Me.LblClose.Size = New System.Drawing.Size(22, 24)
        Me.LblClose.TabIndex = 3
        Me.LblClose.Text = "X"
        '
        'BtnPropType
        '
        Me.BtnPropType.Anchor = CType((System.Windows.Forms.AnchorStyles.Bottom Or System.Windows.Forms.AnchorStyles.Right), System.Windows.Forms.AnchorStyles)
        Me.BtnPropType.FlatStyle = System.Windows.Forms.FlatStyle.Flat
        Me.BtnPropType.Font = New System.Drawing.Font("Microsoft YaHei UI", 9.0!)
        Me.BtnPropType.Location = New System.Drawing.Point(423, 363)
        Me.BtnPropType.Margin = New System.Windows.Forms.Padding(4, 4, 4, 4)
        Me.BtnPropType.Name = "BtnPropType"
        Me.BtnPropType.Size = New System.Drawing.Size(135, 36)
        Me.BtnPropType.TabIndex = 0
        Me.BtnPropType.Text = "配置属性"
        '
        'DataGridView1
        '
        Me.DataGridView1.AllowUserToAddRows = False
        Me.DataGridView1.AllowUserToDeleteRows = False
        Me.DataGridView1.AllowUserToResizeRows = False
        Me.DataGridView1.Anchor = CType((((System.Windows.Forms.AnchorStyles.Top Or System.Windows.Forms.AnchorStyles.Bottom) _
            Or System.Windows.Forms.AnchorStyles.Left) _
            Or System.Windows.Forms.AnchorStyles.Right), System.Windows.Forms.AnchorStyles)
        Me.DataGridView1.BackgroundColor = System.Drawing.SystemColors.Control
        Me.DataGridView1.BorderStyle = System.Windows.Forms.BorderStyle.None
        Me.DataGridView1.ColumnHeadersBorderStyle = System.Windows.Forms.DataGridViewHeaderBorderStyle.None
        Me.DataGridView1.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize
        Me.DataGridView1.Columns.AddRange(New System.Windows.Forms.DataGridViewColumn() {Me.ColName, Me.ColValue})
        Me.DataGridView1.GridColor = System.Drawing.SystemColors.Control
        Me.DataGridView1.Location = New System.Drawing.Point(18, 52)
        Me.DataGridView1.Margin = New System.Windows.Forms.Padding(4, 4, 4, 4)
        Me.DataGridView1.Name = "DataGridView1"
        Me.DataGridView1.ReadOnly = True
        Me.DataGridView1.RowHeadersVisible = False
        Me.DataGridView1.RowHeadersWidth = 51
        Me.DataGridView1.RowTemplate.Height = 23
        Me.DataGridView1.SelectionMode = System.Windows.Forms.DataGridViewSelectionMode.FullRowSelect
        Me.DataGridView1.Size = New System.Drawing.Size(540, 300)
        Me.DataGridView1.TabIndex = 1
        '
        'ColName
        '
        Me.ColName.HeaderText = "属性名"
        Me.ColName.MinimumWidth = 6
        Me.ColName.Name = "ColName"
        Me.ColName.ReadOnly = True
        Me.ColName.Width = 140
        '
        'ColValue
        '
        Me.ColValue.HeaderText = "值"
        Me.ColValue.MinimumWidth = 6
        Me.ColValue.Name = "ColValue"
        Me.ColValue.ReadOnly = True
        Me.ColValue.Width = 200
        '
        'BtnToggle
        '
        Me.BtnToggle.Anchor = CType((System.Windows.Forms.AnchorStyles.Bottom Or System.Windows.Forms.AnchorStyles.Left), System.Windows.Forms.AnchorStyles)
        Me.BtnToggle.FlatStyle = System.Windows.Forms.FlatStyle.Flat
        Me.BtnToggle.Location = New System.Drawing.Point(18, 362)
        Me.BtnToggle.Margin = New System.Windows.Forms.Padding(4, 4, 4, 4)
        Me.BtnToggle.Name = "BtnToggle"
        Me.BtnToggle.Size = New System.Drawing.Size(135, 39)
        Me.BtnToggle.TabIndex = 2
        Me.BtnToggle.Text = "关键属性"
        '
        'Form5
        '
        Me.AutoScaleDimensions = New System.Drawing.SizeF(9.0!, 18.0!)
        Me.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font
        Me.ClientSize = New System.Drawing.Size(576, 414)
        Me.Controls.Add(Me.BtnToggle)
        Me.Controls.Add(Me.DataGridView1)
        Me.Controls.Add(Me.BtnPropType)
        Me.Controls.Add(Me.LblClose)
        Me.Controls.Add(Me.Label1)
        Me.FormBorderStyle = System.Windows.Forms.FormBorderStyle.None
        Me.Margin = New System.Windows.Forms.Padding(4, 4, 4, 4)
        Me.Name = "Form5"
        Me.ShowInTaskbar = False
        Me.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen
        Me.Text = "配置属性"
        CType(Me.DataGridView1, System.ComponentModel.ISupportInitialize).EndInit()
        Me.ResumeLayout(False)
        Me.PerformLayout()

    End Sub

    Friend WithEvents Label1 As System.Windows.Forms.Label
    Friend WithEvents LblClose As System.Windows.Forms.Label
    Friend WithEvents BtnPropType As System.Windows.Forms.Button
    Friend WithEvents DataGridView1 As System.Windows.Forms.DataGridView
    Friend WithEvents ColName As System.Windows.Forms.DataGridViewTextBoxColumn
    Friend WithEvents ColValue As System.Windows.Forms.DataGridViewTextBoxColumn
    Friend WithEvents BtnToggle As System.Windows.Forms.Button
End Class
