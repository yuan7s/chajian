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

    Private Overloads Sub ExecuteCodingCleanup(swApp As SldWorks.SldWorks, comp As SldWorks.Component2)
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