Imports System.Runtime.InteropServices
Imports System.Diagnostics
Imports SwConst

Public Class Form1


    Private Sub Button1_Click(sender As Object, e As EventArgs) Handles Button1.Click

        Dim swApp As Object = GetSelectedSwApp()
        If swApp Is Nothing Then
            MsgBox("请先从下拉列表选择一个 SolidWorks 实例并确保它处于活动状态。")
            Exit Sub
        End If

        Dim part As Object
        Dim fileName2 As String

        Part = swApp.ActiveDoc
        fileName2 = Strings.Left(Part.GetPathName, Len(Part.GetPathName) - 7) & ".dwg"
        Part.SaveAs3(fileName2, 0, 2)
    End Sub

    Private Sub Form1_Load(sender As Object, e As EventArgs) Handles MyBase.Load
        PopulateSolidWorksProcesses()
        ConnectToSw()
        AttachDocEvents()
        UpdateStatusBar()
        Dim timer As New Timer() With {.Interval = 3000}
        AddHandler timer.Tick, AddressOf ProcessTimer_Tick
        timer.Start()
    End Sub
    
    

    Private Sub ProcessTimer_Tick(sender As Object, e As EventArgs)
        Try
            If _swApp IsNot Nothing Then
                Dim test As Object = _swApp.ActiveDoc
            End If
        Catch
            ConnectToSw()
            Dim prevIdx As Integer = swProcessCombo.SelectedIndex
            PopulateSolidWorksProcesses()
            If prevIdx >= 0 AndAlso prevIdx < swProcessCombo.Items.Count Then
                swProcessCombo.SelectedIndex = prevIdx
            End If
            AttachDocEvents()
            UpdateStatusBar()
        End Try
    End Sub

    Private Sub UpdateStatusBar()
        Try
            If _swApp Is Nothing Then
                fileNameLabel.Text = "未连接"
                dirPathLabel.Text = ""
                Return
            End If
            Dim modelDoc As SldWorks.ModelDoc2 = TryCast(_swApp.ActiveDoc, SldWorks.ModelDoc2)
            If modelDoc Is Nothing Then
                fileNameLabel.Text = "无文档"
                dirPathLabel.Text = ""
                Return
            End If

            ' 尝试获取选中对象的文件名
            Dim selMgr As SldWorks.SelectionMgr = modelDoc.SelectionManager
            Dim selCount As Integer = 0
            Try
                selCount = selMgr.GetSelectedObjectCount2(-1)
            Catch
            End Try

            If selCount >= 1 Then
                Dim selObj As Object = selMgr.GetSelectedObject6(1, -1)
                If TypeOf selObj Is SldWorks.Component2 Then
                    Dim comp As SldWorks.Component2 = CType(selObj, SldWorks.Component2)
                    Dim refModel As SldWorks.ModelDoc2 = comp.GetModelDoc2()
                    If refModel IsNot Nothing Then
                        Dim fp As String = refModel.GetPathName()
                        If Not String.IsNullOrEmpty(fp) Then
                            fileNameLabel.Text = System.IO.Path.GetFileNameWithoutExtension(fp)
                            dirPathLabel.Text = System.IO.Path.GetDirectoryName(fp)
                            Return
                        End If
                    End If
                    Dim cp As String = comp.GetPathName()
                    If Not String.IsNullOrEmpty(cp) Then
                        fileNameLabel.Text = System.IO.Path.GetFileNameWithoutExtension(cp)
                        dirPathLabel.Text = System.IO.Path.GetDirectoryName(cp)
                        Return
                    End If
                ElseIf TypeOf selObj Is SldWorks.ModelDoc2 Then
                    Dim selModel As SldWorks.ModelDoc2 = CType(selObj, SldWorks.ModelDoc2)
                    Dim fp As String = selModel.GetPathName()
                    If Not String.IsNullOrEmpty(fp) Then
                        fileNameLabel.Text = System.IO.Path.GetFileNameWithoutExtension(fp)
                        dirPathLabel.Text = System.IO.Path.GetDirectoryName(fp)
                        Return
                    End If
                End If
            End If

            ' 无选择时显示文档标题
            Dim docPath As String = modelDoc.GetPathName()
            If Not String.IsNullOrEmpty(docPath) Then
                fileNameLabel.Text = System.IO.Path.GetFileNameWithoutExtension(docPath)
                dirPathLabel.Text = System.IO.Path.GetDirectoryName(docPath)
            Else
                fileNameLabel.Text = modelDoc.GetTitle()
                dirPathLabel.Text = ""
            End If
        Catch
            fileNameLabel.Text = "错误"
            dirPathLabel.Text = ""
        End Try
    End Sub

    Private Sub Button2_Click(sender As Object, e As EventArgs) Handles Button2.Click
        Dim swApp As Object = GetSelectedSwApp()
        If swApp Is Nothing Then
            MsgBox("请先从下拉列表选择一个 SolidWorks 实例并确保它处于活动状态。")
            Exit Sub
        End If

        Dim part As Object
        Dim fileName2 As String

        part = swApp.ActiveDoc
        fileName2 = Strings.Left(part.GetPathName, Len(part.GetPathName) - 7) & ".pdf"
        part.SaveAs3(fileName2, 0, 2)
    End Sub

    Private Sub Button3_Click(sender As Object, e As EventArgs) Handles Button3.Click
        Dim swApp As Object = GetSelectedSwApp()
        If swApp Is Nothing Then
            MsgBox("请先从下拉列表选择一个 SolidWorks 实例并确保它处于活动状态。")
            Exit Sub
        End If
        Dim part As SldWorks.ModelDoc2
        part = swApp.ActiveDoc

        'Mod = swApp.ModelDoc

        If part Is Nothing Then Exit Sub
        If part.GetType <> 2 Then '零件模型或工程图
            Shell("explorer.exe /select, " & part.GetPathName, vbNormalFocus)
        Else
            Dim swComp As Object   'Component2
            swComp = part.SelectionManager.GetSelectedObjectsComponent(1)
            If swComp IsNot Nothing Then '选中了子件
                Shell("explorer.exe /select, " & swComp.GetPathName, vbNormalFocus)
            Else
                Shell("explorer.exe /select, " & part.GetPathName, vbNormalFocus)
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

        Dim part As SldWorks.ModelDoc2
        part = swApp.ActiveDoc
        If part Is Nothing Then Exit Sub

        Dim x As Double
        Dim y As Double
        Dim z As Double

        Dim c As String
        Dim corners As Object
        Dim values(2) As Double
        If part.GetType = swDocumentTypes_e.swDocPART Then
            corners = CType(part, SldWorks.PartDoc).GetPartBox(True)
        Else
            corners = CType(part, SldWorks.AssemblyDoc).GetBox(swBoundingBoxOptions_e.swBoundingBoxIncludeRefPlanes)
        End If

        y = corners(4) * 1000 - corners(1) * 1000

        z = corners(5) * 1000 - corners(2) * 1000

        x = corners(3) * 1000 - corners(0) * 1000



        values(0) = Math.Round(x, 1)
        values(1) = Math.Round(y, 1)
        values(2) = Math.Round(z, 1)

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

        Dim config As Object
        Dim cusPropMgr As Object
        config = part.GetActiveConfiguration
        cusPropMgr = config.CustomPropertyManager
        cusPropMgr.Add3("下料尺寸", swCustomInfoType_e.swCustomInfoText, c, swCustomPropertyAddOption_e.swCustomPropertyDeleteAndAdd)

        part.SketchManager.Insert3DSketch(True)
        part.SketchManager.Insert3DSketch(True)

    End Sub

    Private Sub Button5_Click(sender As Object, e As EventArgs) Handles Button5.Click
        Dim swApp As Object = GetSelectedSwApp()
        If swApp Is Nothing Then
            MsgBox("请先从下拉列表选择一个 SolidWorks 实例并确保它处于活动状态。")
            Exit Sub
        End If
        Dim part As SldWorks.ModelDoc2
        part = swApp.ActiveDoc
        part.Extension.SetUserPreferenceInteger(swUserPreferenceIntegerValue_e.swDetailingDimensionStandard, 0, swDetailingStandard_e.swDetailingStandardISO)
        part.SketchManager.Insert3DSketch(True)
        part.SketchManager.Insert3DSketch(True)
    End Sub

    Private Sub Button7_Click(sender As Object, e As EventArgs) Handles Button7.Click
        Dim swApp As Object = GetSelectedSwApp()
        If swApp Is Nothing Then
            MsgBox("请先从下拉列表选择一个 SolidWorks 实例并确保它处于活动状态。")
            Exit Sub
        End If
        Dim part As SldWorks.ModelDoc2
        part = swApp.ActiveDoc

        If part.GetType = swDocumentTypes_e.swDocDRAWING Then

            Dim swDraw As SldWorks.DrawingDoc = CType(part, SldWorks.DrawingDoc)
            Dim vSheetNames As Object
            Dim swAnn As SldWorks.Annotation
            Dim i As Double

            part.ClearSelection2(True)
            vSheetNames = swDraw.GetSheetNames
            For i = 0 To UBound(vSheetNames)
                swDraw.ActivateSheet(vSheetNames(i))
                Dim swview As Object
                swview = swDraw.GetFirstView()

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
        Dim swApp As Object = GetSelectedSwApp()
        If swApp Is Nothing Then
            MsgBox("请先从下拉列表选择一个 SolidWorks 实例并确保它处于活动状态。")
            Exit Sub
        End If
        Dim part As SldWorks.ModelDoc2
        part = swApp.ActiveDoc

        Dim test As Integer
        Dim pi As Double

        test = part.GetType
        pi = 3.14159265358979
        If test <> swDocumentTypes_e.swDocDRAWING Then
            swApp.SendMsgToUser2("请在工程图环境下使用", swMessageBoxIcon_e.swMbInformation, swMessageBoxBtn_e.swMbOk)
            Exit Sub
        End If
        Dim swSelMgr As Object
        Dim swView As Object
        swSelMgr = part.SelectionManager
        'On Error GoTo error
        swView = swSelMgr.GetSelectedObject6(1, -1)
        If swView Is Nothing Then
            swApp.SendMsgToUser2("选择一个视图", swMessageBoxIcon_e.swMbInformation, swMessageBoxBtn_e.swMbOk)
            Exit Sub
        End If
        If swView.Angle > 4.5 Then
            swView.Angle = 0
        Else
            swView.Angle += pi / 2
        End If

        part.Extension.SelectByID2(swView.Name, "DRAWINGVIEW", 0, 0, 0, False, 0, Nothing, 0)
    End Sub

    Private Sub Button9_Click(sender As Object, e As EventArgs) Handles Button9.Click
        Dim swApp As Object = GetSelectedSwApp()
        If swApp Is Nothing Then
            MsgBox("请先从下拉列表选择一个 SolidWorks 实例并确保它处于活动状态。")
            Exit Sub
        End If
        Dim part As SldWorks.ModelDoc2
        part = swApp.ActiveDoc

        Dim swConfigurationManager As Object
        Dim swConfiguration As Object
        Dim activeCName As Object
        swConfigurationManager = part.ConfigurationManager
        swConfiguration = swConfigurationManager.ActiveConfiguration
        activeCName = swConfiguration.Name

        Dim c As String
        Dim d As String
        Dim f As String
        c = part.GetTitle()
        If InStr(c, ".") > 0 Then
            c = Strings.Left(c, Len(c) - 7)
        End If
        d = part.GetCustomInfoValue(activeCName, "物料编码")
        f = part.GetCustomInfoValue(activeCName, "零件图号")

        If c <> d Or c <> f Then
            Dim config As Object
            Dim cusPropMgr As Object
            config = part.GetActiveConfiguration
            cusPropMgr = config.CustomPropertyManager
            cusPropMgr.Add3("物料编码", swCustomInfoType_e.swCustomInfoText, c, swCustomPropertyAddOption_e.swCustomPropertyDeleteAndAdd)
            cusPropMgr.Add3("零件图号", swCustomInfoType_e.swCustomInfoText, c, swCustomPropertyAddOption_e.swCustomPropertyDeleteAndAdd)
            cusPropMgr.Add3("文件名称", swCustomInfoType_e.swCustomInfoText, c, swCustomPropertyAddOption_e.swCustomPropertyDeleteAndAdd)
            part.SketchManager.Insert3DSketch(True)
            part.SketchManager.Insert3DSketch(True)
        End If


    End Sub



    Private Sub Button11_Click(sender As Object, e As EventArgs) Handles Button11.Click
        Dim swApp As SldWorks.SldWorks = TryCast(GetSelectedSwApp(), SldWorks.SldWorks)
        If swApp Is Nothing Then
            MsgBox("请先从下拉列表选择一个 SolidWorks 实例并确保它处于活动状态。")
            Exit Sub
        End If
        Dim part As SldWorks.ModelDoc2 = TryCast(swApp.ActiveDoc, SldWorks.ModelDoc2)
        If part Is Nothing Then
            MsgBox("请先在 SolidWorks 中打开一个文档。")
            Exit Sub
        End If
        Dim swFeatMgr As SldWorks.FeatureManager
        swFeatMgr = part.FeatureManager

        If part.GetType = swDocumentTypes_e.swDocASSEMBLY Then
            swFeatMgr.HideComponentSingleConfigurationOrDisplayStateNames = False
            swFeatMgr.SetComponentIdentifiers(4, 0, 0)
            swFeatMgr.SetComponentIdentifiers(2, 0, 0)

            swFeatMgr.ShowComponentConfigurationNames = False
            swFeatMgr.ShowComponentConfigurationDescriptions = False
            swFeatMgr.ShowDisplayStateNames = False
            SubAsmsjs(swApp, part)
            MessageBox.Show("完成", "", MessageBoxButtons.OK, MessageBoxIcon.Information, MessageBoxDefaultButton.Button1, MessageBoxOptions.DefaultDesktopOnly)
        End If

    End Sub
    Function SubAsmsjs(swApp As SldWorks.SldWorks, asmDoc As SldWorks.ModelDoc2) As Object
        Dim configuration As SldWorks.Configuration
        Dim rootComponent As SldWorks.Component2
        Dim comps As Object
        Dim child As Object
        Dim childModel As SldWorks.ModelDoc2
        Dim fopen As SldWorks.ModelDoc2
        Dim childType As Integer
        Dim longstatus As Integer, longWarnings As Integer

        configuration = asmDoc.GetActiveConfiguration
        rootComponent = configuration.GetRootComponent
        Components = rootComponent.GetChildren  ‘’获取目录树

        For Each child In comps
            childModel = child.GetModelDoc
            If Not (childModel Is Nothing) Then
                childType = childModel.GetType

                If childType = swDocumentTypes_e.swDocPART Then  ‘处理零件

                End If
                If childType = swDocumentTypes_e.swDocASSEMBLY Then ‘ 装配体遍历
                    fopen = swApp.OpenDoc6(child.GetPathName, swDocumentTypes_e.swDocASSEMBLY, swOpenDocOptions_e.swOpenDocOptions_Silent, "", longstatus, longWarnings)

                    If longstatus = 0 Then
                        Dim swFeatMgr As SldWorks.FeatureManager
                        swFeatMgr = fopen.FeatureManager
                        swFeatMgr.HideComponentSingleConfigurationOrDisplayStateNames = False
                        swFeatMgr.SetComponentIdentifiers(4, 0, 0)
                        swFeatMgr.SetComponentIdentifiers(2, 0, 0)

                        swFeatMgr.ShowComponentConfigurationNames = False
                        swFeatMgr.ShowComponentConfigurationDescriptions = False
                        swFeatMgr.ShowDisplayStateNames = False
                    End If
                    SubAsmsjs(swApp, childModel)
                End If
            End If
        Next
        Return True
    End Function

    Private Sub Button12_Click(sender As Object, e As EventArgs) Handles Button12.Click
        TopMost = Not TopMost

        ' 根据状态更新按钮文本
        If TopMost Then
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
        CType(Part, SldWorks.AssemblyDoc).ResolveAllLightWeightComponents(True) '把所有的轻化零件还原

        Dim c As String
        c = Part.GetTitle()
        If InStr(c, ".") > 0 Then
            c = Strings.Left(c, Len(c) - 7)
        End If

        ' === 单次遍历，按文件夹开始/结束标记分组 ===
        Dim vFeats As Object = Part.FeatureManager.GetFeatures(True)
        Debug.Print("=== GetFeatures(True) 原始数据，共 " & (UBound(vFeats) + 1) & " 条 ===")
        For i = 0 To UBound(vFeats)
            Debug.Print("[" & i & "] Name=" & vFeats(i).Name & ", Type=" & vFeats(i).GetTypeName2)
        Next
        Debug.Print("=== 原始数据结束 ===")

        Dim modelDoc2 As SldWorks.ModelDoc2 = swApp.ActiveDoc
        Dim assemblyDoc As SldWorks.AssemblyDoc = CType(modelDoc2, SldWorks.AssemblyDoc)
        Dim modelDocExt As SldWorks.ModelDocExtension = modelDoc2.Extension
        Dim selectionMgr As SldWorks.SelectionMgr = modelDoc2.SelectionManager

        Dim folders As New List(Of SldWorks.Feature)()
        Dim folderComponents As New List(Of List(Of SldWorks.Feature))()
        Dim topLevelFeats As New List(Of SldWorks.Feature)()
        Dim suppressedEnvFeats As New List(Of SldWorks.Feature)()
        Dim currentFolderComps As List(Of SldWorks.Feature) = Nothing

        ' 遍历范围：原点之后、MateGroup 之前
        Dim startIdx As Integer = -1
        Dim endIdx As Integer = UBound(vFeats)
        For i = 0 To UBound(vFeats)
            Dim t As String = vFeats(i).GetTypeName2
            If startIdx < 0 AndAlso t = "OriginProfileFeature" Then startIdx = i + 1
            If t = "MateGroup" Then endIdx = i - 1 : Exit For
        Next
        If startIdx < 0 Then startIdx = 0

        Debug.Print("=== 遍历特征 [" & startIdx & ".." & endIdx & "] ===")
        For i = startIdx To endIdx
            Dim featType As String = vFeats(i).GetTypeName2
            Debug.Print("Feature[" & i & "]: Name=" & vFeats(i).Name & ", Type=" & featType)
            If featType = "FtrFolder" Then
                If Not vFeats(i).Name.Contains("___EndTag___") Then
                    folders.Add(vFeats(i))
                    currentFolderComps = New List(Of SldWorks.Feature)()
                    folderComponents.Add(currentFolderComps)
                    Debug.Print("  -> 文件夹开始: " & vFeats(i).Name)
                Else
                    Debug.Print("  -> 文件夹结束: " & vFeats(i).Name)
                    currentFolderComps = Nothing
                End If
            ElseIf featType = "Reference" Then
                Dim isSupOrEnv As Boolean = False
                Try
                    Dim comp As SldWorks.Component2 = TryCast(vFeats(i).GetSpecificFeature2, SldWorks.Component2)
                    If comp IsNot Nothing Then
                        isSupOrEnv = comp.IsSuppressed() OrElse comp.IsEnvelope()
                    End If
                Catch
                End Try
                If isSupOrEnv Then
                    If currentFolderComps IsNot Nothing Then
                        currentFolderComps.Add(vFeats(i))
                        Debug.Print("    文件夹内封套/压缩: " & vFeats(i).Name)
                    Else
                        suppressedEnvFeats.Add(vFeats(i))
                        Debug.Print("  顶层封套/压缩: " & vFeats(i).Name)
                    End If
                ElseIf currentFolderComps IsNot Nothing Then
                    currentFolderComps.Add(vFeats(i))
                    Debug.Print("    文件夹内组件: " & vFeats(i).Name)
                Else
                    topLevelFeats.Add(vFeats(i))
                    Debug.Print("  顶层组件: " & vFeats(i).Name)
                End If
            End If
        Next i

        Debug.Print("=== 分组结果 ===")
        For fi = 0 To folders.Count - 1
            Debug.Print("  " & folders(fi).Name & " 内含 " & folderComponents(fi).Count & " 个组件")
        Next
        Debug.Print("  顶层组件: " & topLevelFeats.Count & " 个")

        ' 收集顶层组件名称
        Dim b As Long = 0
        Dim d As Long = 0
        Dim compNames() As String = Nothing
        Dim assNames() As String = Nothing
        For Each feat In topLevelFeats
            CollectComponent(feat, compNames, b, assNames, d)
        Next

        ' 收集封套/压缩组件名称
        Dim sb As Long = 0
        Dim sd As Long = 0
        Dim supCompNames() As String = Nothing
        Dim supAssNames() As String = Nothing
        If suppressedEnvFeats.Count > 0 Then
            Debug.Print("=== 封套/压缩组件 (" & suppressedEnvFeats.Count & " 个) ===")
            For Each feat In suppressedEnvFeats
                Debug.Print("  " & feat.Name)
                CollectComponent(feat, supCompNames, sb, supAssNames, sd)
            Next
        End If

        Debug.Print("=== compNames (零件) ===")
        If compNames IsNot Nothing Then
            For i = 0 To UBound(compNames)
                Debug.Print("  " & compNames(i))
            Next
        End If

        Debug.Print("=== assNames (装配体) ===")
        If assNames IsNot Nothing Then
            For i = 0 To UBound(assNames)
                Debug.Print("  " & assNames(i))
            Next
        End If

        ' === 排序顶层组件，放到最后一个文件夹后面 ===
        If compNames IsNot Nothing Then Array.Sort(compNames)
        If assNames IsNot Nothing Then Array.Sort(assNames)
        If supCompNames IsNot Nothing Then Array.Sort(supCompNames)
        If supAssNames IsNot Nothing Then Array.Sort(supAssNames)

        Dim combinedArray As List(Of String) = New List(Of String)()
        If assNames IsNot Nothing Then combinedArray.AddRange(assNames)
        If compNames IsNot Nothing Then combinedArray.AddRange(compNames)
        If supAssNames IsNot Nothing Then combinedArray.AddRange(supAssNames)
        If supCompNames IsNot Nothing Then combinedArray.AddRange(supCompNames)

        Dim combinedArr() As String = If(combinedArray.Count > 0, combinedArray.ToArray(), Nothing)

        Debug.Print("=== combinedArray (最终排序结果) ===")
        If combinedArr IsNot Nothing Then
            For i = 0 To UBound(combinedArr)
                Debug.Print("  " & combinedArr(i))
            Next
        End If

    If combinedArr IsNot Nothing AndAlso combinedArr.Length > 0 Then
            Dim componentsToMove() As Object
            ReDim componentsToMove(UBound(combinedArr))

            For i = 0 To UBound(combinedArr)
                modelDocExt.SelectByID2(combinedArr(i) & "@" & c, "COMPONENT", 0, 0, 0, False, 0, Nothing, 0)
                componentsToMove(i) = selectionMgr.GetSelectedObjectsComponent4(1, 0)
            Next i

            Dim lastFolder As SldWorks.Feature = If(folders.Count > 0, folders(folders.Count - 1), Nothing)
            If lastFolder IsNot Nothing Then
                assemblyDoc.ReorderComponents(componentsToMove(0), lastFolder, SwConst.swReorderComponentsWhere_e.swReorderComponents_after)
            End If
            For i = 1 To UBound(combinedArr)
                assemblyDoc.ReorderComponents(componentsToMove(i), componentsToMove(i - 1), 1) ' Below
            Next i
        End If

        ' === 排序每个文件夹内的组件 ===
        For Each f As SldWorks.Feature In folders
            SortComponentsInFolder(f, c, assemblyDoc)
        Next

        ' === 递归处理子装配体 ===
        Dim processedPaths As New HashSet(Of String)(StringComparer.OrdinalIgnoreCase)
        RecursiveSortSubAssemblies(topLevelFeats, processedPaths)
        For Each fc In folderComponents
            RecursiveSortSubAssemblies(fc, processedPaths)
        Next

        Part.EditRebuild3()
        Part.ClearSelection2(True)
    End Sub

    ''' <summary>
    ''' 对文件夹内的组件排序 (装配体在前、零件在后，各自按名称排序)
    ''' </summary>
    Private Sub SortComponentsInFolder(folder As SldWorks.Feature, topAsmName As String, assemblyDoc As SldWorks.AssemblyDoc)
        Dim partComps As New List(Of SldWorks.Component2)()
        Dim asmComps As New List(Of SldWorks.Component2)()
        Dim supPartComps As New List(Of SldWorks.Component2)()
        Dim supAsmComps As New List(Of SldWorks.Component2)()

        Dim subFeat As SldWorks.Feature = folder.GetFirstSubFeature()
        While subFeat IsNot Nothing
            If subFeat.GetTypeName2 = "Reference" Then
                Dim swty As SldWorks.Component2 = TryCast(subFeat.GetSpecificFeature2, SldWorks.Component2)
                If swty IsNot Nothing Then
                    Dim compModel As SldWorks.ModelDoc2 = swty.GetModelDoc2
                    If compModel IsNot Nothing Then
                        Dim isSupEnv As Boolean = swty.IsSuppressed() OrElse swty.IsEnvelope()
                        Select Case compModel.GetType()
                            Case SwConst.swDocumentTypes_e.swDocPART
                                If isSupEnv Then supPartComps.Add(swty) Else partComps.Add(swty)
                            Case SwConst.swDocumentTypes_e.swDocASSEMBLY
                                If isSupEnv Then supAsmComps.Add(swty) Else asmComps.Add(swty)
                        End Select
                    End If
                End If
            End If
            subFeat = subFeat.GetNextSubFeature()
        End While

        If asmComps.Count = 0 AndAlso partComps.Count = 0 AndAlso supAsmComps.Count = 0 AndAlso supPartComps.Count = 0 Then Return

        asmComps.Sort(Function(a, b) String.Compare(a.Name2, b.Name2, StringComparison.OrdinalIgnoreCase))
        partComps.Sort(Function(a, b) String.Compare(a.Name2, b.Name2, StringComparison.OrdinalIgnoreCase))
        supAsmComps.Sort(Function(a, b) String.Compare(a.Name2, b.Name2, StringComparison.OrdinalIgnoreCase))
        supPartComps.Sort(Function(a, b) String.Compare(a.Name2, b.Name2, StringComparison.OrdinalIgnoreCase))

        Dim allComps As New List(Of SldWorks.Component2)(asmComps)
        allComps.AddRange(partComps)
        allComps.AddRange(supAsmComps)
        allComps.AddRange(supPartComps)

        Dim firstRef As SldWorks.Feature = folder.GetFirstSubFeature()
        If firstRef IsNot Nothing Then
            Dim anchorComp As SldWorks.Component2 = TryCast(firstRef.GetSpecificFeature2, SldWorks.Component2)
            If anchorComp IsNot Nothing Then
                assemblyDoc.ReorderComponents(allComps(0), anchorComp, 0) ' Above
                For i = 1 To allComps.Count - 1
                    assemblyDoc.ReorderComponents(allComps(i), allComps(i - 1), 1) ' Below
                Next
            End If
        End If
    End Sub

    ''' <summary>
    ''' 收集 Reference 特征中的零件/装配体名称
    ''' </summary>
    Private Sub CollectComponent(swFeat As Object, ByRef compNames() As String, ByRef b As Long, ByRef assNames() As String, ByRef d As Long)
        Dim swty As SldWorks.Component2
        swty = swFeat.GetSpecificFeature2
        If swty IsNot Nothing Then
            Dim compModel As SldWorks.ModelDoc2
            compModel = swty.GetModelDoc2
            If compModel IsNot Nothing Then
                Select Case compModel.GetType()
                    Case SwConst.swDocumentTypes_e.swDocPART
                        ReDim Preserve compNames(b)
                        compNames(b) = swFeat.Name
                        b += 1
                    Case SwConst.swDocumentTypes_e.swDocASSEMBLY
                        ReDim Preserve assNames(d)
                        assNames(d) = swFeat.Name
                        d += 1
                End Select
            End If
        End If
    End Sub

    Private Sub RecursiveSortSubAssemblies(feats As List(Of SldWorks.Feature), processed As HashSet(Of String))
        For Each feat In feats
            Try
                Dim comp As SldWorks.Component2 = TryCast(feat.GetSpecificFeature2, SldWorks.Component2)
                If comp Is Nothing Then Continue For
                Dim childModel As SldWorks.ModelDoc2 = comp.GetModelDoc2()
                If childModel Is Nothing Then Continue For
                If childModel.GetType() <> swDocumentTypes_e.swDocASSEMBLY Then Continue For
                SortSubAsmByFeatures(childModel, processed)
            Catch ex As Exception
                Debug.Print("[递归] " & feat.Name & ": " & ex.Message)
            End Try
        Next
    End Sub

    Private Sub SortSubAsmByFeatures(asmModel As SldWorks.ModelDoc2, processed As HashSet(Of String))
        If asmModel Is Nothing Then Return
        If asmModel.GetType() <> swDocumentTypes_e.swDocASSEMBLY Then Return
        Dim path As String = asmModel.GetPathName()
        If Not String.IsNullOrEmpty(path) Then
            If processed.Contains(path) Then Return
            processed.Add(path)
        End If

        Dim asmName As String = IO.Path.GetFileNameWithoutExtension(path)
        If String.IsNullOrEmpty(asmName) Then asmName = asmModel.GetTitle()
        Debug.Print("[递归] 处理: " & asmName)

        Try
            Dim vFeats As Object = asmModel.FeatureManager.GetFeatures(True)
            Dim asmDoc As SldWorks.AssemblyDoc = CType(asmModel, SldWorks.AssemblyDoc)
            Dim modelDocExt As SldWorks.ModelDocExtension = asmModel.Extension
            Dim selMgr As SldWorks.SelectionMgr = asmModel.SelectionManager

            Dim startIdx As Integer = -1
            Dim endIdx As Integer = UBound(vFeats)
            For i = 0 To UBound(vFeats)
                Dim t As String = vFeats(i).GetTypeName2
                If startIdx < 0 AndAlso t = "OriginProfileFeature" Then startIdx = i + 1
                If t = "MateGroup" Then endIdx = i - 1 : Exit For
            Next
            If startIdx < 0 Then startIdx = 0

            Dim folders As New List(Of SldWorks.Feature)()
            Dim folderComponents As New List(Of List(Of SldWorks.Feature))()
            Dim topLevelFeats As New List(Of SldWorks.Feature)()
            Dim suppressedEnvFeats As New List(Of SldWorks.Feature)()
            Dim currentFolderComps As List(Of SldWorks.Feature) = Nothing

            For i = startIdx To endIdx
                Dim featType As String = vFeats(i).GetTypeName2
                If featType = "FtrFolder" Then
                    If Not vFeats(i).Name.Contains("___EndTag___") Then
                        folders.Add(vFeats(i))
                        currentFolderComps = New List(Of SldWorks.Feature)()
                        folderComponents.Add(currentFolderComps)
                    Else
                        currentFolderComps = Nothing
                    End If
                ElseIf featType = "Reference" Then
                    Dim isSupOrEnv As Boolean = False
                    Try
                        Dim comp As SldWorks.Component2 = TryCast(vFeats(i).GetSpecificFeature2, SldWorks.Component2)
                        If comp IsNot Nothing Then
                            isSupOrEnv = comp.IsSuppressed() OrElse comp.IsEnvelope()
                        End If
                    Catch
                    End Try
                    If isSupOrEnv Then
                        If currentFolderComps IsNot Nothing Then
                            currentFolderComps.Add(vFeats(i))
                        Else
                            suppressedEnvFeats.Add(vFeats(i))
                        End If
                    ElseIf currentFolderComps IsNot Nothing Then
                        currentFolderComps.Add(vFeats(i))
                    Else
                        topLevelFeats.Add(vFeats(i))
                    End If
                End If
            Next

            Dim b As Long = 0, d As Long = 0
            Dim compNames() As String = Nothing, assNames() As String = Nothing
            For Each feat In topLevelFeats
                CollectComponent(feat, compNames, b, assNames, d)
            Next
            Dim sb As Long = 0, sd As Long = 0
            Dim supCompNames() As String = Nothing, supAssNames() As String = Nothing
            For Each feat In suppressedEnvFeats
                CollectComponent(feat, supCompNames, sb, supAssNames, sd)
            Next

            If compNames IsNot Nothing Then Array.Sort(compNames)
            If assNames IsNot Nothing Then Array.Sort(assNames)
            If supCompNames IsNot Nothing Then Array.Sort(supCompNames)
            If supAssNames IsNot Nothing Then Array.Sort(supAssNames)

            Dim combinedList As New List(Of String)()
            If assNames IsNot Nothing Then combinedList.AddRange(assNames)
            If compNames IsNot Nothing Then combinedList.AddRange(compNames)
            If supAssNames IsNot Nothing Then combinedList.AddRange(supAssNames)
            If supCompNames IsNot Nothing Then combinedList.AddRange(supCompNames)

            If combinedList.Count > 0 Then
                Dim componentsToMove(combinedList.Count - 1) As Object
                For i = 0 To combinedList.Count - 1
                    modelDocExt.SelectByID2(combinedList(i) & "@" & asmName, "COMPONENT", 0, 0, 0, False, 0, Nothing, 0)
                    componentsToMove(i) = selMgr.GetSelectedObjectsComponent4(1, 0)
                Next
                If folders.Count > 0 Then
                    asmDoc.ReorderComponents(componentsToMove(0), folders(folders.Count - 1), SwConst.swReorderComponentsWhere_e.swReorderComponents_after)
                End If
                For i = 1 To combinedList.Count - 1
                    asmDoc.ReorderComponents(componentsToMove(i), componentsToMove(i - 1), 1)
                Next
            End If

            For Each f As SldWorks.Feature In folders
                SortComponentsInFolder(f, asmName, asmDoc)
            Next

            RecursiveSortSubAssemblies(topLevelFeats, processed)
            For Each fc In folderComponents
                RecursiveSortSubAssemblies(fc, processed)
            Next

            asmModel.EditRebuild3()
            asmModel.ClearSelection2(True)
            Debug.Print("[递归] " & asmName & " 完成")
        Catch ex As Exception
            Debug.Print("[递归] " & asmName & " 出错: " & ex.Message)
        End Try
    End Sub

    Private Sub GroupBox2_Enter(sender As Object, e As EventArgs) Handles GroupBox2.Enter

    End Sub

    Private Sub Button10_Click(sender As Object, e As EventArgs) Handles Button10.Click
        Shell("cmd.exe /c taskkill /F /IM sldworks.exe ", AppWinStyle.Hide)
    End Sub

    Private Sub Button6_Click_1(sender As Object, e As EventArgs) Handles Button6.Click
        Dim swApp As SldWorks.SldWorks = TryCast(GetSelectedSwApp(), SldWorks.SldWorks)
        If swApp Is Nothing Then
            MsgBox("请先从下拉列表选择一个 SolidWorks 实例并确保它处于活动状态。")
            Exit Sub
        End If
        Dim part As SldWorks.ModelDoc2 = TryCast(swApp.ActiveDoc, SldWorks.ModelDoc2)
        If part Is Nothing Then
            MsgBox("请先在 SolidWorks 中打开一个文档。")
            Exit Sub
        End If
        If part.GetType <> swDocumentTypes_e.swDocASSEMBLY Then
            MsgBox("请在装配体环境下使用此功能。")
            Exit Sub
        End If

        Dim docName As String = IO.Path.GetFileNameWithoutExtension(part.GetPathName())
        If String.IsNullOrEmpty(docName) Then docName = part.GetTitle()
        Dim wasTopMost As Boolean = TopMost
        TopMost = True
        Dim confirmResult As DialogResult = MessageBox.Show(
            "将删除【" & docName & "】及其所有子件的自定义属性，确定继续？",
            "确认删除自定义属性",
            MessageBoxButtons.OKCancel,
            MessageBoxIcon.Warning)
        TopMost = wasTopMost
        If confirmResult <> DialogResult.OK Then Exit Sub

        Dim topConfString As String = part.GetActiveConfiguration.Name
        SubAsm(swApp, part, topConfString)
        MessageBox.Show("完成", "", MessageBoxButtons.OK, MessageBoxIcon.Information, MessageBoxDefaultButton.Button1, MessageBoxOptions.DefaultDesktopOnly)
    End Sub

    Function SubAsm(swApp As SldWorks.SldWorks, asmDoc As SldWorks.ModelDoc2, confString As String) As Object
        Dim configuration As SldWorks.Configuration
        Dim rootComponent As SldWorks.Component2
        Dim comps As Object
        Dim child As Object
        Dim childModel As SldWorks.ModelDoc2
        Dim fopen As SldWorks.ModelDoc2
        Dim childConfString As String
        Dim childType As Integer
        Dim namearr As Object
        Dim longstatus As Integer, longWarnings As Integer
        Dim vCustInfoNameArr As Object
        Dim vCustInfoName As Object

        ' 处理主装配体自身
        vCustInfoNameArr = asmDoc.GetConfigurationNames
        namearr = asmDoc.GetCustomInfoNames2(confString)
        If namearr IsNot Nothing Then
            For Each vCustInfoName In namearr
                asmDoc.DeleteCustomInfo(vCustInfoName)
            Next
        End If

        configuration = asmDoc.GetConfigurationByName(confString)
        rootComponent = configuration.GetRootComponent
        Components = rootComponent.GetChildren  ''获取目录树

        For Each child In comps
            childModel = child.GetModelDoc
            If Not (childModel Is Nothing) Then
                childConfString = child.ReferencedConfiguration
                childType = childModel.GetType

                If childType = swDocumentTypes_e.swDocPART Then  '处理零件
                    fopen = swApp.OpenDoc6(child.GetPathName, swDocumentTypes_e.swDocPART, swOpenDocOptions_e.swOpenDocOptions_Silent, "", longstatus, longWarnings)

                    If longstatus = 0 Then
                        vCustInfoNameArr = fopen.GetConfigurationNames
                        namearr = fopen.GetCustomInfoNames2(“”)
                        For Each vCustInfoName In namearr
                            fopen.DeleteCustomInfo(vCustInfoName)
                        Next

                  
                        fopen.Save3(0, 0, 0)
                    End If

                End If
                If childType = swDocumentTypes_e.swDocASSEMBLY Then ' 装配体遍历
                    fopen = swApp.OpenDoc6(child.GetPathName, swDocumentTypes_e.swDocASSEMBLY, swOpenDocOptions_e.swOpenDocOptions_Silent, "", longstatus, longWarnings)

                    If longstatus = 0 Then
                        vCustInfoNameArr = fopen.GetConfigurationNames
                        namearr = fopen.GetCustomInfoNames2(“”)
                      

                        For Each vCustInfoName In namearr
                            fopen.DeleteCustomInfo(vCustInfoName)
                        Next
                        
                        fopen.Save3(0, 0, 0)
                    End If

                    SubAsm(swApp, childModel, childConfString)
                End If
            End If
        Next
        Return True
    End Function

    Private Sub Button14_Click_1(sender As Object, e As EventArgs) Handles Button14.Click
        ' 仅打开 Form2（刷新由单独刷新按钮处理）
        Form2.Show()

    End Sub

    Private Sub swProcessCombo_SelectedIndexChanged(sender As Object, e As EventArgs) Handles swProcessCombo.SelectedIndexChanged
        Dim info = TryCast(swProcessCombo.SelectedItem, SwProcessInfo)
        If info Is Nothing Then
            Return
        End If

        ' 保存或使用所选 SolidWorks 进程
        Try
            _selectedSwProcess = Process.GetProcessById(info.ProcessId)
        Catch ex As Exception
            _selectedSwProcess = Nothing
        End Try
        ConnectToSw()
        AttachDocEvents()
        UpdateStatusBar()
    End Sub

    Private _selectedSwProcess As Process = Nothing
    Private WithEvents _swApp As SldWorks.SldWorks
    Private _attachedPartDoc As SldWorks.PartDoc
    Private _attachedAsmDoc As SldWorks.AssemblyDoc
    Private _attachedDrawDoc As SldWorks.DrawingDoc
    Private _attachedDocPath As String

    <DllImport("user32.dll")>
    Private Shared Function SetForegroundWindow(hWnd As IntPtr) As Boolean
    End Function

    <DllImport("user32.dll")>
    Private Shared Function ShowWindow(hWnd As IntPtr, nCmdShow As Integer) As Boolean
    End Function

    <DllImport("user32.dll", CharSet:=CharSet.Auto, SetLastError:=True)>
    Private Shared Function GetWindowText(hWnd As IntPtr, lpString As Text.StringBuilder, nMaxCount As Integer) As Integer
    End Function

    <DllImport("user32.dll", SetLastError:=True)>
    Private Shared Function GetWindowTextLength(hWnd As IntPtr) As Integer
    End Function

    Private Const SwRestore As Integer = 9

    Private Sub ConnectToSw()
        Try
            _swApp = CType(Marshal.GetActiveObject("SldWorks.Application"), SldWorks.SldWorks)
        Catch
            _swApp = Nothing
        End Try
    End Sub

    Private Function _swApp_ActiveDocChangeNotify() As Integer Handles _swApp.ActiveDocChangeNotify
        Dim prevIndex As Integer = swProcessCombo.SelectedIndex
        PopulateSolidWorksProcesses()
        If prevIndex >= 0 AndAlso prevIndex < swProcessCombo.Items.Count Then
            swProcessCombo.SelectedIndex = prevIndex
        End If
        UpdateStatusBar()
        AttachDocEvents()
        Return 0
    End Function

    Private Function _swApp_ActiveModelDocChangeNotify() As Integer Handles _swApp.ActiveModelDocChangeNotify
        Dim prevIndex As Integer = swProcessCombo.SelectedIndex
        PopulateSolidWorksProcesses()
        If prevIndex >= 0 AndAlso prevIndex < swProcessCombo.Items.Count Then
            swProcessCombo.SelectedIndex = prevIndex
        End If
        UpdateStatusBar()
        AttachDocEvents()
        Return 0
    End Function

    Private Function _swApp_FileCloseNotify(fileName As String, reason As Integer) As Integer Handles _swApp.FileCloseNotify
        Try
            If _swApp IsNot Nothing Then
                Dim doc As SldWorks.ModelDoc2 = TryCast(_swApp.ActiveDoc, SldWorks.ModelDoc2)
                If doc Is Nothing Then
                    fileNameLabel.Text = "无文档"
                    dirPathLabel.Text = ""
                ElseIf String.Equals(doc.GetPathName(), fileName, StringComparison.OrdinalIgnoreCase) Then
                    fileNameLabel.Text = "无文档"
                    dirPathLabel.Text = ""
                Else
                    UpdateStatusBar()
                End If
            Else
                fileNameLabel.Text = "未连接"
                dirPathLabel.Text = ""
            End If
        Catch
            UpdateStatusBar()
        End Try
        Return 0
    End Function

    Private Sub DetachDocEvents()
        If _attachedPartDoc IsNot Nothing Then
            RemoveHandler _attachedPartDoc.NewSelectionNotify, AddressOf Doc_SelectionChange
            _attachedPartDoc = Nothing
        End If
        If _attachedAsmDoc IsNot Nothing Then
            RemoveHandler _attachedAsmDoc.NewSelectionNotify, AddressOf Doc_SelectionChange
            _attachedAsmDoc = Nothing
        End If
        If _attachedDrawDoc IsNot Nothing Then
            RemoveHandler _attachedDrawDoc.NewSelectionNotify, AddressOf Doc_SelectionChange
            _attachedDrawDoc = Nothing
        End If
        _attachedDocPath = Nothing
    End Sub

    Private Sub AttachDocEvents()
        Try
            If _swApp Is Nothing Then Return
            Dim modelDoc As SldWorks.ModelDoc2 = TryCast(_swApp.ActiveDoc, SldWorks.ModelDoc2)
            If modelDoc Is Nothing Then
                DetachDocEvents()
                Return
            End If
            Dim docPath As String = modelDoc.GetPathName()
            If String.Equals(docPath, _attachedDocPath, StringComparison.OrdinalIgnoreCase) Then Return
            DetachDocEvents()
            _attachedDocPath = docPath
            Dim docType As Integer = modelDoc.GetType()
            If docType = CInt(swDocumentTypes_e.swDocPART) Then
                _attachedPartDoc = CType(modelDoc, SldWorks.PartDoc)
                AddHandler _attachedPartDoc.NewSelectionNotify, AddressOf Doc_SelectionChange
            ElseIf docType = CInt(swDocumentTypes_e.swDocASSEMBLY) Then
                _attachedAsmDoc = CType(modelDoc, SldWorks.AssemblyDoc)
                AddHandler _attachedAsmDoc.NewSelectionNotify, AddressOf Doc_SelectionChange
            ElseIf docType = CInt(swDocumentTypes_e.swDocDRAWING) Then
                _attachedDrawDoc = CType(modelDoc, SldWorks.DrawingDoc)
                AddHandler _attachedDrawDoc.NewSelectionNotify, AddressOf Doc_SelectionChange
            End If
        Catch
        End Try
    End Sub

    Private Function Doc_SelectionChange() As Integer
        UpdateStatusBar()
        Return 0
    End Function

    ' 尝试根据选定的进程返回对应的 SolidWorks COM 对象
    Public Function GetSelectedSwApp() As Object
        Try
            If _selectedSwProcess IsNot Nothing Then
                Dim h = _selectedSwProcess.MainWindowHandle
                If h <> IntPtr.Zero Then
                    ' 恢复并置前窗口，随后从 ROT 获取当前活动 SolidWorks 对象
                    ShowWindow(h, SwRestore)
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
        swProcessCombo.Items.Clear()

        Try
            Dim procs = Process.GetProcessesByName("sldworks")
            For Each p In procs
                Dim h As IntPtr = p.MainWindowHandle

                ' 首先尝试常规属性
                Dim title As String = p.MainWindowTitle

                ' 如果为空，尝试使用 Win32 API 直接读取窗口文本
                If String.IsNullOrWhiteSpace(title) AndAlso h <> IntPtr.Zero Then
                    Try
                        Dim len As Integer = GetWindowTextLength(h)
                        If len > 0 Then
                            Dim sb As New Text.StringBuilder(len + 1)
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
                        ShowWindow(h, SwRestore)
                        SetForegroundWindow(h)
                        Dim swApp = Marshal.GetActiveObject("SldWorks.Application")
                        If swApp IsNot Nothing Then
                            Dim doc As SldWorks.ModelDoc2 = CType(swApp.ActiveDoc, SldWorks.ModelDoc2)
                            If doc IsNot Nothing Then
                                Dim path As String = doc.GetPathName()
                                If Not String.IsNullOrWhiteSpace(path) Then
                                    title = IO.Path.GetFileNameWithoutExtension(path)
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
                    Dim idx As Integer = title.IndexOf(sep, StringComparison.Ordinal)
                    If idx >= 0 Then
                        title = title.Substring(idx + sep.Length).Trim()
                    End If

                    ' 去除可能的方括号包围（例如："[name.SLDASM]")
                    If title.StartsWith("[") AndAlso title.EndsWith("]") AndAlso title.Length > 2 Then
                        title = title.Substring(1, title.Length - 2).Trim()
                    End If
                End If

                swProcessCombo.Items.Add(New SwProcessInfo With {.Title = title, .ProcessId = p.Id})
            Next

            If swProcessCombo.Items.Count > 0 Then
                swProcessCombo.SelectedIndex = 0
            End If
        Catch ex As Exception
            ' 忽略异常或根据需要记录
        End Try
    End Sub

    Private Class SwProcessInfo
        Public Property Title As String
        Public Property ProcessId As Integer
        Public Overrides Function ToString() As String
            Return ProcessId.ToString() & ": " & Title
        End Function
    End Class

    Private Sub refreshBtn_Click(sender As Object, e As EventArgs) Handles refreshBtn.Click
        ' 刷新 SolidWorks 进程列表
        PopulateSolidWorksProcesses()
        UpdateStatusBar()
    End Sub

    Private Sub Button16_Click(sender As Object, e As EventArgs) Handles Button16.Click
        Form3.Show()
    End Sub

    Private Sub Button17_Click(sender As Object, e As EventArgs) Handles Button17.Click
        Dim swApp As SldWorks.SldWorks = TryCast(GetSelectedSwApp(), SldWorks.SldWorks)
        If swApp Is Nothing Then
            MsgBox("请先从下拉列表选择一个 SolidWorks 实例并确保它处于活动状态。")
            Exit Sub
        End If
        Dim part As SldWorks.ModelDoc2 = TryCast(swApp.ActiveDoc, SldWorks.ModelDoc2)
        If part Is Nothing Then
            MsgBox("请先在 SolidWorks 中打开一个文档。")
            Exit Sub
        End If
        If part.GetType <> swDocumentTypes_e.swDocASSEMBLY Then
            MsgBox("请在装配体环境下使用此功能。")
            Exit Sub
        End If

        Dim docName As String = IO.Path.GetFileNameWithoutExtension(part.GetPathName())
        If String.IsNullOrEmpty(docName) Then docName = part.GetTitle()
        Dim wasTopMost As Boolean = Me.TopMost
        Me.TopMost = True
        Dim confirmResult As DialogResult = MessageBox.Show(
            "将删除【" & docName & "】及其所有子件的配置属性，确定继续？",
            "确认删除配置属性",
            MessageBoxButtons.OKCancel,
            MessageBoxIcon.Warning)
        Me.TopMost = wasTopMost
        If confirmResult <> DialogResult.OK Then Exit Sub

        Dim topConfString As String = part.GetActiveConfiguration.Name
        DelConfProps(swApp, part, topConfString)
        MessageBox.Show("完成", "", MessageBoxButtons.OK, MessageBoxIcon.Information, MessageBoxDefaultButton.Button1, MessageBoxOptions.DefaultDesktopOnly)
    End Sub

    Function DelConfProps(swApp As SldWorks.SldWorks, asmDoc As SldWorks.ModelDoc2, confString As String) As Object
        ' 先处理当前装配体自身
        Dim namearr As Object = asmDoc.GetCustomInfoNames2(confString)
        If namearr IsNot Nothing Then
            For Each n In namearr
                asmDoc.DeleteCustomInfo2(confString, n)
            Next
        End If

        Dim configuration As SldWorks.Configuration = asmDoc.GetConfigurationByName(confString)
        Dim rootComponent As SldWorks.Component2 = configuration.GetRootComponent
        Dim comps As Object = rootComponent.GetChildren

        Dim child As Object, childModel As SldWorks.ModelDoc2, fopen As SldWorks.ModelDoc2
        Dim childConfString As String, childType As Integer
        Dim longstatus As Integer, longWarnings As Integer

        For Each child In comps
            childModel = child.GetModelDoc
            If Not (childModel Is Nothing) Then
                childConfString = child.ReferencedConfiguration
                childType = childModel.GetType

                If childType = swDocumentTypes_e.swDocPART Then
                    fopen = swApp.OpenDoc6(child.GetPathName, swDocumentTypes_e.swDocPART, swOpenDocOptions_e.swOpenDocOptions_Silent, "", longstatus, longWarnings)
                    If longstatus = 0 Then
                        namearr = fopen.GetCustomInfoNames2(childConfString)
                        If namearr IsNot Nothing Then
                            For Each n In namearr
                                fopen.DeleteCustomInfo2(childConfString, n)
                            Next
                        End If
                        fopen.Save3(0, 0, 0)
                    End If
                End If
                If childType = swDocumentTypes_e.swDocASSEMBLY Then
                    fopen = swApp.OpenDoc6(child.GetPathName, swDocumentTypes_e.swDocASSEMBLY, swOpenDocOptions_e.swOpenDocOptions_Silent, "", longstatus, longWarnings)
                    If longstatus = 0 Then
                        namearr = fopen.GetCustomInfoNames2(childConfString)
                        If namearr IsNot Nothing Then
                            For Each n In namearr
                                fopen.DeleteCustomInfo2(childConfString, n)
                            Next
                        End If
                        fopen.Save3(0, 0, 0)
                    End If
                    DelConfProps(swApp, childModel, childConfString)
                End If
            End If
        Next
        Return True
    End Function

    Private Sub Button18_Click(sender As Object, e As EventArgs) Handles Button18.Click

    End Sub
End Class
