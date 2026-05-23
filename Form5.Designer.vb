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
        Me.BtnToggle = New System.Windows.Forms.Button()
        Me.PropPanel = New System.Windows.Forms.Panel()
        Me.PropHeaderName = New System.Windows.Forms.Label()
        Me.PropHeaderValue = New System.Windows.Forms.Label()
        Me.SuspendLayout()
        '
        'Label1
        '
        Me.Label1.AutoEllipsis = True
        Me.Label1.Font = New System.Drawing.Font("Microsoft YaHei UI", 10.0!, System.Drawing.FontStyle.Bold)
        Me.Label1.Location = New System.Drawing.Point(18, 14)
        Me.Label1.Margin = New System.Windows.Forms.Padding(4, 0, 4, 0)
        Me.Label1.Name = "Label1"
        Me.Label1.Size = New System.Drawing.Size(470, 28)
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
        Me.BtnPropType.Font = New System.Drawing.Font("Microsoft YaHei UI", 9.0!, System.Drawing.FontStyle.Bold)
        Me.BtnPropType.Location = New System.Drawing.Point(423, 363)
        Me.BtnPropType.Margin = New System.Windows.Forms.Padding(4)
        Me.BtnPropType.Name = "BtnPropType"
        Me.BtnPropType.Size = New System.Drawing.Size(135, 36)
        Me.BtnPropType.TabIndex = 0
        Me.BtnPropType.Text = "配置属性"
        '
        'BtnToggle
        '
        Me.BtnToggle.Anchor = CType((System.Windows.Forms.AnchorStyles.Bottom Or System.Windows.Forms.AnchorStyles.Left), System.Windows.Forms.AnchorStyles)
        Me.BtnToggle.FlatStyle = System.Windows.Forms.FlatStyle.Flat
        Me.BtnToggle.Font = New System.Drawing.Font("Microsoft YaHei UI", 9.0!, System.Drawing.FontStyle.Bold)
        Me.BtnToggle.Location = New System.Drawing.Point(18, 362)
        Me.BtnToggle.Margin = New System.Windows.Forms.Padding(4)
        Me.BtnToggle.Name = "BtnToggle"
        Me.BtnToggle.Size = New System.Drawing.Size(135, 39)
        Me.BtnToggle.TabIndex = 2
        Me.BtnToggle.Text = "全部属性"
        '
        'PropPanel
        '
        Me.PropPanel.Anchor = CType((((System.Windows.Forms.AnchorStyles.Top Or System.Windows.Forms.AnchorStyles.Bottom) _
            Or System.Windows.Forms.AnchorStyles.Left) _
            Or System.Windows.Forms.AnchorStyles.Right), System.Windows.Forms.AnchorStyles)
        Me.PropPanel.AutoScroll = False
        Me.PropPanel.Location = New System.Drawing.Point(18, 82)
        Me.PropPanel.Margin = New System.Windows.Forms.Padding(4)
        Me.PropPanel.Name = "PropPanel"
        Me.PropPanel.Size = New System.Drawing.Size(540, 270)
        Me.PropPanel.TabIndex = 4
        '
        'PropHeaderName
        '
        Me.PropHeaderName.AutoSize = True
        Me.PropHeaderName.Font = New System.Drawing.Font("Microsoft YaHei UI", 9.0!, System.Drawing.FontStyle.Bold)
        Me.PropHeaderName.Location = New System.Drawing.Point(18, 52)
        Me.PropHeaderName.Name = "PropHeaderName"
        Me.PropHeaderName.Size = New System.Drawing.Size(63, 20)
        Me.PropHeaderName.TabIndex = 5
        Me.PropHeaderName.Text = "属性名"
        '
        'PropHeaderValue
        '
        Me.PropHeaderValue.AutoSize = True
        Me.PropHeaderValue.Font = New System.Drawing.Font("Microsoft YaHei UI", 9.0!, System.Drawing.FontStyle.Bold)
        Me.PropHeaderValue.Location = New System.Drawing.Point(205, 52)
        Me.PropHeaderValue.Name = "PropHeaderValue"
        Me.PropHeaderValue.Size = New System.Drawing.Size(39, 20)
        Me.PropHeaderValue.TabIndex = 6
        Me.PropHeaderValue.Text = "值"
        '
        'Form5
        '
        Me.AutoScaleDimensions = New System.Drawing.SizeF(9.0!, 18.0!)
        Me.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font
        Me.ClientSize = New System.Drawing.Size(576, 414)
        Me.Controls.Add(Me.PropHeaderValue)
        Me.Controls.Add(Me.PropHeaderName)
        Me.Controls.Add(Me.PropPanel)
        Me.Controls.Add(Me.BtnToggle)
        Me.Controls.Add(Me.BtnPropType)
        Me.Controls.Add(Me.LblClose)
        Me.Controls.Add(Me.Label1)
        Me.FormBorderStyle = System.Windows.Forms.FormBorderStyle.None
        Me.Margin = New System.Windows.Forms.Padding(4)
        Me.Name = "Form5"
        Me.ShowInTaskbar = False
        Me.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen
        Me.Text = "配置属性"
        Me.ResumeLayout(False)
        Me.PerformLayout()

    End Sub

    Friend WithEvents Label1 As System.Windows.Forms.Label
    Friend WithEvents LblClose As System.Windows.Forms.Label
    Friend WithEvents BtnPropType As System.Windows.Forms.Button
    Friend WithEvents BtnToggle As System.Windows.Forms.Button
    Friend WithEvents PropPanel As System.Windows.Forms.Panel
    Friend WithEvents PropHeaderName As System.Windows.Forms.Label
    Friend WithEvents PropHeaderValue As System.Windows.Forms.Label
End Class
