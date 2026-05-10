Imports System.Runtime.InteropServices
Imports SldWorks
Imports System.Diagnostics
Imports SwConst

Public Class Form1


    Private Sub Button1_Click(sender As Object, e As EventArgs) Handles Button1.Click

        Const PROG_ID As String = "SldWorks.Application"
        Dim swApp As Object

        'Using Interaction.CreateObject function
        swApp = CreateObject(PROG_ID)
        'app1.Visible = True

        Dim Part As Object
        Dim FileName2 As String
        Dim longstatus As Long

        Part = swApp.ActiveDoc
        FileName2 = Strings.Left(Part.GetPathName, Len(Part.GetPathName) - 7) & ".dwg"
        longstatus = Part.SaveAs3(FileName2, 0, 2)
    End Sub

    Private Sub TabPage1_Click(sender As Object, e As EventArgs)

    End Sub

    Private Sub Form1_Load(sender As Object, e As EventArgs) Handles MyBase.Load
        PopulateSolidWorksProcesses()
    End Sub

    Private Sub Button2_Click(sender As Object, e As EventArgs) Handles Button2.Click
        Const PROG_ID As String = "SldWorks.Application"
        Dim swApp = CreateObject(PROG_ID)

        Dim Part As Object
        Dim FileName2 As String
        Dim longstatus As Long

        Part = swApp.ActiveDoc
        FileName2 = Strings.Left(Part.GetPathName, Len(Part.GetPathName) - 7) & ".pdf"
        longstatus = Part.SaveAs3(FileName2, 0, 2)
    End Sub

    Private Sub Button3_Click(sender As Object, e As EventArgs) Handles Button3.Click
        Const PROG_ID As String = "SldWorks.Application"
        Dim swApp As Object
        Dim Part As SldWorks.ModelDoc2
        swApp = CreateObject(PROG_ID)
        Part = swApp.ActiveDoc

        'Mod = swApp.ModelDoc

        If Part Is Nothing Then Exit Sub
        If Part.GetType <> 2 Then '零件模型或工程图
            Shell("explorer.exe /select, " & Part.GetPathName, vbNormalFocus)
        Else
            Dim SwComp As Object   'Component2
            SwComp = Part.SelectionManager.GetSelectedObjectsComponent(1)
            If SwComp IsNot Nothing Then '选中了子件
                Shell("explorer.exe /select, " & SwComp.GetPathName, vbNormalFocus)
            Else
                Shell("explorer.exe /select, " & Part.GetPathName, vbNormalFocus)
            End If


        End If
    End Sub

    Private Sub Button4_Click(sender As Object, e As EventArgs) Handles Button4.Click
        ' 使用下拉选择的 SolidWorks 实例
        Dim swApp As Object
        swApp = GetSelectedSwApp()
        If swApp Is Nothing Then
            MsgBox("请先从下拉列表选择一个 SolidWorks 实例并确保它处于活动状态。")
            Exit Sub
        End If

        Dim Part As SldWorks.ModelDoc2
        Part = swApp.ActiveDoc
        If Part Is Nothing Then Exit Sub

        Dim X As Double
        Dim Y As Double
        Dim Z As Double

        Dim c As String
        Dim Corners As Object
        Dim values(2) As Double
        If Part.GetType = SwConst.swDocumentTypes_e.swDocPART Then
            Corners = Part.GetPartBox(True)
        Else
            Corners = Part.GetBox(SwConst.swBoundingBoxOptions_e.swBoundingBoxIncludeRefPlanes)
        End If

        Y = Corners(4) * 1000 - Corners(1) * 1000

        Z = Corners(5) * 1000 - Corners(2) * 1000

        X = Corners(3) * 1000 - Corners(0) * 1000



        values(0) = Math.Round(X, 1)
        values(1) = Math.Round(Y, 1)
        values(2) = Math.Round(Z, 1)

        ' 冒泡排序（从小到大）
        Dim i As Integer, j As Integer
        For i = 0 To 2
            For j = i + 1 To 2
                If values(i) > values(j) Then
                    Dim temp As Double
                    temp = values(i)
                    values(i) = values(j)
                    values(j) = temp
                End If
            Next j
        Next i
        'MsgBox "排序结果：" & values(2) & ", " & values(1) & ", " & values(0)
        c = values(2) & "x" & values(1) & "x" & values(0)

        Dim blnretval As Object
        Dim config As Object
        Dim cusPropMgr As Object
        config = Part.GetActiveConfiguration
        cusPropMgr = config.CustomPropertyManager
        blnretval = cusPropMgr.Add3("下料尺寸", SwConst.swCustomInfoType_e.swCustomInfoText, c， SwConst.swCustomPropertyAddOption_e.swCustomPropertyDeleteAndAdd)

        Part.SketchManager.Insert3DSketch(True)
        Part.SketchManager.Insert3DSketch(True)

    End Sub

    Private Sub Button5_Click(sender As Object, e As EventArgs) Handles Button5.Click
        Const PROG_ID As String = "SldWorks.Application"
        Dim Part As SldWorks.ModelDoc2
        Dim swApp = CreateObject(PROG_ID)
        Part = swApp.ActiveDoc
        Part.Extension.SetUserPreferenceInteger(SwConst.swUserPreferenceIntegerValue_e.swDetailingDimensionStandard, 0, SwConst.swDetailingStandard_e.swDetailingStandardISO)
        Part.SketchManager.Insert3DSketch(True)
        Part.SketchManager.Insert3DSketch(True)
    End Sub

    Private Sub Button6_Click(sender As Object, e As EventArgs)
        Const PROG_ID As String = "SldWorks.Application"
        Dim swApp = CreateObject(PROG_ID)
        Dim part As Object

        part = swApp.ActiveDoc
        part.Extension.SetUserPreferenceInteger(SwConst.swUserPreferenceIntegerValue_e.swDetailingDimensionStandard, 0, SwConst.swDetailingStandard_e.swDetailingStandardISO)
        part.SketchManager.Insert3DSketch(True)
        part.SketchManager.Insert3DSketch(True)
    End Sub

    Private Sub Button7_Click(sender As Object, e As EventArgs) Handles Button7.Click
        Const PROG_ID As String = "SldWorks.Application"
        Dim Part As SldWorks.ModelDoc2
        Dim swApp = CreateObject(PROG_ID)
        Part = swApp.ActiveDoc

        If Part.GetType = SwConst.swDocumentTypes_e.swDocDRAWING Then

            Dim vSheetNames As Object
            Dim swSheet As SldWorks.Sheet
            Dim swAnn As SldWorks.Annotation
            Dim i As Double

            Part.ClearSelection2(True)
            vSheetNames = Part.GetSheetNames
            For i = 0 To UBound(vSheetNames)
                Part.ActivateSheet(vSheetNames(i))
                swSheet = Part.Sheet(vSheetNames(i))
                Dim swview As Object
                swview = Part.GetFirstView()

                Do While swview IsNot Nothing
                    swAnn = swview.GetFirstAnnotation3
                    Do While swAnn IsNot Nothing
                        If swAnn.IsDangling Then
                            swAnn.Select(True)
                        End If
                        swAnn = swAnn.GetNext3
                    Loop
                    swview = swview.GetNextView
                Loop
            Next i
        End If
    End Sub

    Private Sub Button8_Click(sender As Object, e As EventArgs) Handles Button8.Click
        Const PROG_ID As String = "SldWorks.Application"
        Dim swApp As Object
        Dim Part As SldWorks.ModelDoc2
        swApp = CreateObject(PROG_ID)
        Part = swApp.ActiveDoc

        Dim test As Integer
        Dim pi As Double

        test = Part.GetType
        pi = 3.14159265358979
        If test <> SwConst.swDocumentTypes_e.swDocDRAWING Then
            swApp.SendMsgToUser2("请在工程图环境下使用", SwConst.swMessageBoxIcon_e.swMbInformation, SwConst.swMessageBoxBtn_e.swMbOk)
            Exit Sub
        End If
        Dim swSelMgr As Object
        Dim swView As Object
        swSelMgr = Part.SelectionManager
        'On Error GoTo error
        swView = swSelMgr.GetSelectedObject6(1, -1)
        If swView Is Nothing Then
            swApp.SendMsgToUser2("选择一个视图", SwConst.swMessageBoxIcon_e.swMbInformation, SwConst.swMessageBoxBtn_e.swMbOk)
            Exit Sub
        End If
        If swView.Angle > 4.5 Then
            swView.Angle = 0
        Else
            swView.Angle += pi / 2
        End If

        Part.Extension.SelectByID2(swView.Name, "DRAWINGVIEW", 0, 0, 0, False, 0, Nothing, 0)
    End Sub

    Private Sub Button9_Click(sender As Object, e As EventArgs) Handles Button9.Click
        Const PROG_ID As String = "SldWorks.Application"
        Dim swApp As Object
        Dim Part As SldWorks.ModelDoc2
        swApp = CreateObject(PROG_ID)
        Part = swApp.ActiveDoc

        Dim swConfigurationManager As Object
        Dim swConfiguration As Object
        Dim ActiveCName As Object
        swConfigurationManager = Part.ConfigurationManager
        swConfiguration = swConfigurationManager.ActiveConfiguration
        ActiveCName = swConfiguration.Name

        Dim c As String
        Dim d As String
        Dim f As String
        Dim blnretval As String

        c = Part.GetTitle()
        If InStr(c, ".") > 0 Then
            c = Strings.Left(c, Len(c) - 7)
        End If
        d = Part.GetCustomInfoValue(ActiveCName, "物料编码")
        f = Part.GetCustomInfoValue(ActiveCName, "零件图号")

        If c <> d Or c <> f Then
            Dim config As Object
            Dim cusPropMgr As Object
            config = Part.GetActiveConfiguration
            cusPropMgr = config.CustomPropertyManager
            blnretval = cusPropMgr.Add3("物料编码", SwConst.swCustomInfoType_e.swCustomInfoText, c， SwConst.swCustomPropertyAddOption_e.swCustomPropertyDeleteAndAdd)
            blnretval = cusPropMgr.Add3("零件图号", SwConst.swCustomInfoType_e.swCustomInfoText, c， SwConst.swCustomPropertyAddOption_e.swCustomPropertyDeleteAndAdd)
            blnretval = cusPropMgr.Add3("文件名称", SwConst.swCustomInfoType_e.swCustomInfoText, c， SwConst.swCustomPropertyAddOption_e.swCustomPropertyDeleteAndAdd)
            Part.SketchManager.Insert3DSketch（True）
            Part.SketchManager.Insert3DSketch（True）
        End If


    End Sub



    Private Sub Button11_Click(sender As Object, e As EventArgs) Handles Button11.Click
        Dim swApp As Object

        swApp = Marshal.GetActiveObject("SldWorks.Application")
        Dim Part As SldWorks.ModelDoc2
        Dim swFeatMgr As SldWorks.FeatureManager
        Part = swApp.ActiveDoc
        'swFeatMgr = Part.FeatureManager

        Dim compIdentifierRet As Long
        swFeatMgr = Part.FeatureManager


        Select Case Part.GetType
            Case swDocumentTypes_e.swDocASSEMBLY
                swFeatMgr.HideComponentSingleConfigurationOrDisplayStateNames = False
                compIdentifierRet = swFeatMgr.SetComponentIdentifiers(4, 0, 0)
                compIdentifierRet = swFeatMgr.SetComponentIdentifiers(2, 0, 0)

                swFeatMgr.ShowComponentConfigurationNames = False
                swFeatMgr.ShowComponentConfigurationDescriptions = False
                swFeatMgr.ShowDisplayStateNames = False
                SubAsmsjs(Part)
            Case Else
                MsgBox("错误的文档")
        End Select

    End Sub
    Function SubAsmsjs(AsmDoc As Object) As Object
        Dim Configuration As SldWorks.Configuration
        Dim RootComponent As SldWorks.Component2
        Dim Components As Object
        Dim Child As Object
        Dim ChildModel As SldWorks.ModelDoc2
        Dim fopen As Object
        Dim ChildConfString As String
        Dim ChildType As Integer
        Dim longstatus As Long, LongWarnings As Long



        Configuration = AsmDoc.GetActiveConfiguration
        RootComponent = Configuration.GetRootComponent
        Components = RootComponent.GetChildren  ''获取目录树


        For Each Child In Components
            ChildModel = Child.GetModelDoc
            If Not (ChildModel Is Nothing) Then
                ChildConfString = Child.ReferencedConfiguration
                ChildType = ChildModel.GetType
                Dim swapp As SldWorks.SldWorks
                swapp = Marshal.GetActiveObject("SldWorks.Application")

                If ChildType = 1 Then  '处理零件

                End If
                If ChildType = 2 Then ' 装配体遍历
                    fopen = swapp.OpenDoc6(Child.GetPathName, swDocumentTypes_e.swDocASSEMBLY, swOpenDocOptions_e.swOpenDocOptions_Silent, "", longstatus, LongWarnings)


                    If longstatus = 0 Then
                        Dim compIdentifierRet As Long
                        Dim swFeatMgr As SldWorks.FeatureManager
                        swFeatMgr = fopen.FeatureManager
                        swFeatMgr.HideComponentSingleConfigurationOrDisplayStateNames = False
                        compIdentifierRet = swFeatMgr.SetComponentIdentifiers(4, 0, 0)
                        compIdentifierRet = swFeatMgr.SetComponentIdentifiers(2, 0, 0)

                        swFeatMgr.ShowComponentConfigurationNames = False
                        swFeatMgr.ShowComponentConfigurationDescriptions = False
                        swFeatMgr.ShowDisplayStateNames = False
                        fopen.SketchManager.Insert3DSketch(True)
                        fopen.SketchManager.Insert3DSketch(True)
                    End If

                    SubAsmsjs(ChildModel)
                End If
            End If
        Next
        Return True
    End Function

    Private Sub Button12_Click(sender As Object, e As EventArgs) Handles Button12.Click
        Me.TopMost = Not Me.TopMost

        ' 根据状态更新按钮文本
        If Me.TopMost Then
            Button12.Text = "取消置顶"
        Else
            Button12.Text = "置顶"
        End If
    End Sub

    Private Sub Button13_Click(sender As Object, e As EventArgs) Handles Button13.Click
        Dim swApp As Object

        swApp = Marshal.GetActiveObject("SldWorks.Application")
        Dim Part As SldWorks.ModelDoc2
        Part = swApp.ActiveDoc

        If Part Is Nothing Then
            MsgBox("当前没有任何文档打开， 该程序必须在装配体中运行！")
            Exit Sub
        ElseIf Part.GetType <> SwConst.swDocumentTypes_e.swDocASSEMBLY Then
            MsgBox("当前打开的文档不是一个装配体，请打开装配体后再试！")
            Exit Sub
        End If

        Dim Configuration As SldWorks.Configuration
        Configuration = Part.GetConfigurationByName(Part.GetActiveConfiguration.Name)
        Part.ResolveAllLightWeightComponents(True) '把所有的轻化零件还原

        Dim c As String
        c = Part.GetTitle()
        If InStr(c, ".") > 0 Then
            c = Strings.Left(c, Len(c) - 7)
        End If


        ' 获取所有特征数组
        Dim vFeats As Object
        vFeats = Part.FeatureManager.GetFeatures(True)  ' True = 返回特征对象

        Debug.Print（"共找到 " & (UBound(vFeats) + 1) & " 个顶层特征"）


        Dim b As Long
        Dim d As Long
        Dim compNames() As String = Nothing
        Dim assNames() As String = Nothing

        b = 0
        d = 0
        For i = 0 To UBound(vFeats)
            Dim swFeat As Object
            swFeat = vFeats(i)

            Dim featType As String
            featType = vFeats(i).GetTypeName2


            'Debug.Print(swFeat.Name & featType)
            If featType = "Reference" Then
                Dim swty As SldWorks.Component2
                swty = vFeats(i).GetSpecificFeature2
                If swty IsNot Nothing Then
                    Dim compModel As SldWorks.ModelDoc2
                    compModel = swty.GetModelDoc2
                    Select Case compModel.GetType()
                        Case SwConst.swDocumentTypes_e.swDocPART
                            ReDim Preserve compNames(b)
                            compNames(b) = swFeat.Name
                            b += 1
                            'Debug.Print(swFeat.Name)
                            'Debug.Print（"特征 " & i & ": " & swFeat.Name & swFeat.GetTypeName2）

                            'partFeatures.Add swFeat
                            'Debug.Print("零件特征: " & " -> " & swFeat.Name)

                        Case SwConst.swDocumentTypes_e.swDocASSEMBLY
                            ReDim Preserve assNames(d)
                            assNames(d) = swFeat.Name
                            d += 1
                            'assemblyFeatures.Add swFeat
                            'Debug.Print("装配体特征: " & " -> " & swFeat.Name)
                    End Select


                End If

            End If

        Next i


        Dim combinedArray() As String
        Array.Sort(compNames)
        If assNames IsNot Nothing Then
            Array.Sort(assNames)
            'Dim f = UBound(assNames) + UBound(compNames)
            combinedArray = assNames.Concat(compNames).ToArray()
        Else
            combinedArray = compNames
        End If

        Dim modelDoc2 As SldWorks.ModelDoc2
        Dim assemblyDoc As SldWorks.AssemblyDoc
        Dim modelDocExt As SldWorks.ModelDocExtension
        Dim selectionMgr As SldWorks.SelectionMgr

        Dim selObj As Object
        Dim retVal As Boolean

        modelDoc2 = swApp.ActiveDoc
        assemblyDoc = modelDoc2
        modelDocExt = modelDoc2.Extension
        selectionMgr = modelDoc2.SelectionManager

        Dim boolstatus As Boolean
        Dim componentToMove As SldWorks.Component2
        Dim componentsToMove() As Object
        ReDim componentsToMove(UBound(combinedArray))



        For i = 0 To UBound(combinedArray)

            boolstatus = modelDocExt.SelectByID2(combinedArray(i) & "@" & c, "COMPONENT", 0, 0, 0, False, 0, Nothing, 0)
            selObj = selectionMgr.GetSelectedObject6(i + 1, -1)
            componentToMove = selectionMgr.GetSelectedObjectsComponent4(1, 0)
            componentsToMove(i) = componentToMove

        Next i

        Dim featureMgr As SldWorks.FeatureManager
        Dim feature As SldWorks.Feature
        featureMgr = modelDoc2.FeatureManager
        feature = featureMgr.InsertFeatureTreeFolder2(1)
        feature = assemblyDoc.FeatureByName(feature.Name)



        For i = 0 To UBound(combinedArray)
            retVal = assemblyDoc.ReorderComponents(componentsToMove(i), feature, SwConst.swReorderComponentsWhere_e.swReorderComponents_LastInFolder)
        Next i
        modelDocExt.SelectByID2(feature.Name, "FTRFOLDER", 0, 0, 0, False, 0, Nothing, 0)
        modelDoc2.EditDelete()
        Part.EditRebuild3()
        Part.ClearSelection2(True)
    End Sub

    Private Sub GroupBox2_Enter(sender As Object, e As EventArgs) Handles GroupBox2.Enter

    End Sub

    Private Sub Button10_Click(sender As Object, e As EventArgs) Handles Button10.Click
        Shell("cmd.exe /c taskkill /F /IM sldworks.exe ", AppWinStyle.Hide)
    End Sub

    Private Sub Button6_Click_1(sender As Object, e As EventArgs) Handles Button6.Click
        Dim swApp As Object
        swApp = Marshal.GetActiveObject("SldWorks.Application")
        Dim Part As SldWorks.ModelDoc2
        Part = swApp.ActiveDoc
        Dim TopConfString As String

        If Part.GetType <> 2 Then Exit Sub
        TopConfString = Part.GetActiveConfiguration.Name

        SubAsm(Part, TopConfString)


        MsgBox("完成")
    End Sub

    Function SubAsm(AsmDoc As Object, ConfString As String) As Object
        Dim Configuration As SldWorks.Configuration
        Dim RootComponent As SldWorks.Component2
        Dim Components As Object
        Dim Child As Object
        Dim ChildModel As SldWorks.ModelDoc2
        Dim fopen As Object
        Dim ChildConfString As String
        Dim ChildType As Integer
        Dim namearr As Object
        Dim longstatus As Long, LongWarnings As Long
        Dim vConfigNameArr As Object
        Dim vConfigName As Object
        Dim vCustInfoNameArr As Object
        Dim vCustInfoName As Object
        Dim retval As String


        Configuration = AsmDoc.GetConfigurationByName(ConfString)
        RootComponent = Configuration.GetRootComponent
        Components = RootComponent.GetChildren  ''获取目录树




        For Each Child In Components
            ChildModel = Child.GetModelDoc
            If Not (ChildModel Is Nothing) Then
                ChildConfString = Child.ReferencedConfiguration
                ChildType = ChildModel.GetType
                Dim swapp As SldWorks.SldWorks
                swapp = Marshal.GetActiveObject("SldWorks.Application")

                If ChildType = 1 Then  '处理零件

                    fopen = swapp.OpenDoc6(Child.GetPathName, swDocumentTypes_e.swDocPART, swOpenDocOptions_e.swOpenDocOptions_Silent, "", longstatus, LongWarnings)

                    If longstatus = 0 Then
                        vCustInfoNameArr = fopen.GetConfigurationNames
                        namearr = fopen.GetCustomInfoNames2(vCustInfoNameArr(0))   '自定义属性为空值 配置属性为默认或者default
                        vConfigNameArr = fopen.GetCustomInfoNames


                        For Each vCustInfoName In namearr
                            retval = fopen.DeleteCustomInfo2(vCustInfoNameArr(0), vCustInfoName)
                        Next
                        For Each vConfigName In vConfigNameArr

                            retval = fopen.DeleteCustomInfo(vConfigName)
                        Next

                        fopen.SketchManager.Insert3DSketch(True)
                        fopen.SketchManager.Insert3DSketch(True)
                    End If

                End If
                If ChildType = 2 Then ' 装配体遍历
                    fopen = swapp.OpenDoc6(Child.GetPathName, swDocumentTypes_e.swDocASSEMBLY, swOpenDocOptions_e.swOpenDocOptions_Silent, "", longstatus, LongWarnings)


                    If longstatus = 0 Then
                        vCustInfoNameArr = fopen.GetConfigurationNames
                        namearr = fopen.GetCustomInfoNames2(vCustInfoNameArr(0))   '自定义属性为空值 配置属性为默认或者default
                        vConfigNameArr = fopen.GetCustomInfoNames


                        For Each vCustInfoName In namearr
                            retval = fopen.DeleteCustomInfo2(vCustInfoNameArr(0), vCustInfoName)
                        Next
                        For Each vConfigName In vConfigNameArr

                            retval = fopen.DeleteCustomInfo(vConfigName)
                        Next
                        fopen.SketchManager.Insert3DSketch(True)
                        fopen.SketchManager.Insert3DSketch(True)
                    End If

                    SubAsm(ChildModel, ChildConfString)
                End If
            End If
        Next
        Return True
    End Function

    Private Sub MenuStrip1_ItemClicked(sender As Object, e As ToolStripItemClickedEventArgs)

    End Sub

    Private Sub Panel1_Paint(sender As Object, e As PaintEventArgs)

    End Sub

    Private Sub Button14_Click(sender As Object, e As EventArgs)

    End Sub

    Private Sub Button14_Click_1(sender As Object, e As EventArgs) Handles Button14.Click
        ' 仅打开 Form2（刷新由单独的刷新按钮处理）
        Form2.Show()

    End Sub

    Private Sub ListView1_SelectedIndexChanged(sender As Object, e As EventArgs)

    End Sub

    Private Sub ComboBox1_SelectedIndexChanged(sender As Object, e As EventArgs) Handles ComboBox1.SelectedIndexChanged
        Dim info = TryCast(ComboBox1.SelectedItem, SwProcessInfo)
        If info Is Nothing Then
            Return
        End If

        ' 保存或使用所选 SolidWorks 进程的 PID
        SelectedSwProcessId = info.ProcessId
        Try
            SelectedSwProcess = Process.GetProcessById(info.ProcessId)
        Catch ex As Exception
            SelectedSwProcess = Nothing
        End Try
        ' 可在此处执行进一步操作，例如显示所选窗口标题或与该进程交互
        ' MsgBox("已选择: " & info.Title & " (PID=" & info.ProcessId & ")")
    End Sub

    Private SelectedSwProcessId As Integer = -1
    Private SelectedSwProcess As Process = Nothing

    <DllImport("user32.dll")>
    Private Shared Function SetForegroundWindow(hWnd As IntPtr) As Boolean
    End Function

    <DllImport("user32.dll")>
    Private Shared Function ShowWindow(hWnd As IntPtr, nCmdShow As Integer) As Boolean
    End Function

    <DllImport("user32.dll", CharSet:=CharSet.Auto, SetLastError:=True)>
    Private Shared Function GetWindowText(hWnd As IntPtr, lpString As System.Text.StringBuilder, nMaxCount As Integer) As Integer
    End Function

    <DllImport("user32.dll", SetLastError:=True)>
    Private Shared Function GetWindowTextLength(hWnd As IntPtr) As Integer
    End Function

    Private Const SW_RESTORE As Integer = 9

    ' 尝试根据选定的进程返回对应的 SolidWorks COM 对象
    Public Function GetSelectedSwApp() As Object
        Try
            If SelectedSwProcess IsNot Nothing Then
                Dim h = SelectedSwProcess.MainWindowHandle
                If h <> IntPtr.Zero Then
                    ' 恢复并置前窗口，随后从 ROT 获取当前活动 SolidWorks 对象
                    ShowWindow(h, SW_RESTORE)
                    SetForegroundWindow(h)
                End If
            End If

            ' 这是常用的获取 SolidWorks COM 对象方法（如果有多个实例，通常返回当前活动项）
            Dim swApp = Marshal.GetActiveObject("SldWorks.Application")
            Return swApp
        Catch ex As Exception
            Return Nothing
        End Try
    End Function

    Private Sub PopulateSolidWorksProcesses()
        ComboBox1.Items.Clear()

        Try
            Dim procs = Process.GetProcessesByName("sldworks")
            For Each p In procs
                Dim title As String = String.Empty
                Dim h As IntPtr = p.MainWindowHandle

                ' 首先尝试常规属性
                title = p.MainWindowTitle

                ' 如果为空，尝试使用 Win32 API 直接读取窗口文本
                If String.IsNullOrWhiteSpace(title) AndAlso h <> IntPtr.Zero Then
                    Try
                        Dim len As Integer = GetWindowTextLength(h)
                        If len > 0 Then
                            Dim sb As New System.Text.StringBuilder(len + 1)
                            GetWindowText(h, sb, sb.Capacity)
                            title = sb.ToString()
                        End If
                    Catch
                        ' 忽略
                    End Try
                End If

                ' 如果仍然为空，尝试将该实例置前并通过 COM 获取活动文档名作为标题
                If String.IsNullOrWhiteSpace(title) AndAlso h <> IntPtr.Zero Then
                    Try
                        ShowWindow(h, SW_RESTORE)
                        SetForegroundWindow(h)
                        Dim swApp = Marshal.GetActiveObject("SldWorks.Application")
                        If swApp IsNot Nothing Then
                            Dim doc As SldWorks.ModelDoc2 = CType(swApp.ActiveDoc, SldWorks.ModelDoc2)
                            If doc IsNot Nothing Then
                                Dim path As String = doc.GetPathName()
                                If Not String.IsNullOrWhiteSpace(path) Then
                                    title = System.IO.Path.GetFileNameWithoutExtension(path)
                                End If
                            End If
                        End If
                    Catch
                        ' 忽略
                    End Try
                End If

                If String.IsNullOrWhiteSpace(title) Then
                    title = "SolidWorks (PID " & p.Id & ")"
                Else
                    ' 如果标题包含 " - "，取其后部分（通常为文档名或文档信息）
                    Dim sep As String = " - "
                    Dim idx As Integer = title.IndexOf(sep)
                    If idx >= 0 Then
                        title = title.Substring(idx + sep.Length).Trim()
                    End If

                    ' 去除可能的方括号包围（例如："[name.SLDASM]")
                    If title.StartsWith("[") AndAlso title.EndsWith("]") AndAlso title.Length > 2 Then
                        title = title.Substring(1, title.Length - 2).Trim()
                    End If
                End If

                ComboBox1.Items.Add(New SwProcessInfo With {.Title = title, .ProcessId = p.Id})
            Next

            If ComboBox1.Items.Count > 0 Then
                ComboBox1.SelectedIndex = 0
            End If
        Catch ex As Exception
            ' 忽略异常或根据需要记录
        End Try
    End Sub

    Private Class SwProcessInfo
        Public Property Title As String
        Public Property ProcessId As Integer
        Public Overrides Function ToString() As String
            Return Title
        End Function
    End Class

    Private Sub Button15_Click(sender As Object, e As EventArgs) Handles Button15.Click
        ' 刷新 SolidWorks 进程列表
        PopulateSolidWorksProcesses()
    End Sub

    Private Sub Button16_Click(sender As Object, e As EventArgs) Handles Button16.Click
        Form3.Show()
    End Sub
End Class
