using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using SolidWorks.Interop.swdocumentmgr;
using SldWorks;
using SwConst;

namespace ExternalProgram.SwAddin;

// ReSharper disable CatchAllClause
#pragma warning disable CA1031 // SolidWorks COM APIs throw broad COM/runtime exceptions; command handlers isolate and log recoverable failures.

internal sealed partial class AddinHttpServer
{
    private static readonly string[] StandardComponentFlagProperties = { "标准件"};
    private static readonly string[] PurchasedComponentFlagProperties = { "外购件" };
    private static readonly string[][] ReferencePlaneMateNameGroups =
    {
        new[] { "前视基准面", "Front Plane" },
        new[] { "上视基准面", "Top Plane" },
        new[] { "右视基准面", "Right Plane" }
    };

    private string _lastDocPath;

    private void CheckAndBroadcastDocChange()
    {
        try
        {
            var model = _swApp.ActiveDoc as ModelDoc2;
            var currentPath = model != null ? Safe(() => model.GetPathName()) ?? "" : "";
            if (currentPath != _lastDocPath)
            {
                _lastDocPath = currentPath;
                var title = model != null ? Safe(() => model.GetTitle()) ?? "" : "";
                BroadcastEvent("doc-changed", new { title, path = currentPath });
            }
        }
        catch (Exception ex)
        {
            LogIgnoredException("CheckAndBroadcastDocChange", ex);
        }
    }

    private object ExecuteCommand(CommandRequest request)
    {
        if (request == null) throw new ArgumentNullException(nameof(request));

        var args = request.Args ?? new Dictionary<string, object>();
        object result;
        switch ((request.Command ?? string.Empty).Trim().ToUpperInvariant())
        {
            case "PING":
                result = new { message = "pong", time = DateTime.Now };
                break;

            case "ACTIVE-DOCUMENT":
                result = GetActiveDocumentInfo();
                break;

            case "SAVE-DWG":
                result = SaveActiveDrawingAs(".dwg", "DWG");
                break;

            case "SAVE-PDF":
                result = SaveActiveDrawingAs(".pdf", "PDF");
                break;

            case "OPEN-FILE-LOCATION":
                result = GetActiveOrSelectedModelPath();
                break;

            case "ROTATE-DRAWING-VIEW":
                result = RotateSelectedDrawingView();
                break;

            case "SET-ISO-STANDARD":
                result = SetIsoStandard();
                break;

            case "MATE-REFERENCE-PLANES":
                result = MateReferencePlanes();
                break;

            case "DELETE-ERROR-MATES":
                result = DeleteErrorMates();
                break;

            case "RUN-SWP-MACRO":
                result = RunSwpMacro(args);
                break;

            case "GET-COMPONENT-TREE":
                result = GetComponentTree();
                break;

            case "SELECT-COMPONENT":
                result = SelectComponent(args);
                break;

            case "SORT-COMPONENTS":
                result = SortComponents(args);
                break;

            case "HIDE-CONFIG-NAMES":
                result = HideConfigNames();
                break;

            case "SYNC-CODING-PROPS":
                result = SyncCodingProps();
                break;

            case "DELETE-CUSTOM-PROPS":
                result = DeleteCustomProperties();
                break;

            case "DELETE-CONFIG-PROPS":
                result = DeleteConfigurationProperties();
                break;

            case "READ-PROPERTIES":
                result = ReadProperties(args);
                break;

            case "WRITE-PROPERTIES":
                result = WriteProperties(args);
                break;

            case "GET-BOUNDING-BOX":
                result = GetBoundingBox(args);
                break;

            case "RENAME-TARGET":
                result = GetRenameTargetInfo();
                break;

            case "CHECK-NAME-CONFLICT":
                result = CheckNameConflict(args);
                break;

            case "SAVE-AS-NEW":
                result = SaveAsNew(args);
                break;

            case "SAVE-AS-REPLACE":
                result = SaveAsReplace(args);
                break;

            case "RENAME-COMPONENT":
                result = RenameComponent(args);
                break;

            case "CODING-CLEANUP":
                result = CodingCleanup(args);
                break;

            case "REBUILD":
                result = Rebuild();
                break;

            case "SAVE":
                result = Save();
                break;

            case "OPEN-DOCUMENT":
                result = OpenDocument(args);
                break;

            case "LIST-EXTERNAL-REFERENCES":
                result = ListExternalReferences(args);
                break;

            default:
                throw new InvalidOperationException($"未知命令: {request.Command}");
        }

        CheckAndBroadcastDocChange();
        return result;
    }

    private ModelDoc2 GetActiveModel()
    {
        var model = _swApp.ActiveDoc as ModelDoc2;
        if (model == null) throw new InvalidOperationException("没有活动文档");
        return model;
    }

    private static T Safe<T>(Func<T> work)
    {
        try { return work(); }
        catch (Exception ex)
        {
            LogIgnoredException("Safe", ex);
            return default;
        }
    }

    private static void LogIgnoredException(string context, Exception ex)
    {
        AddinLog.Write(context + " ignored: " + ex.Message);
    }

    private static object[] GetComponentChildren(Component2 component)
    {
        if (component == null) return Array.Empty<object>();

        try
        {
            return ToObjectArray(component.GetChildren());
        }
        catch (Exception ex)
        {
            LogIgnoredException("GetComponentChildren", ex);
            return Array.Empty<object>();
        }
    }

    private static object[] GetFeatureArray(ModelDoc2 model)
    {
        var featureManager = model?.FeatureManager;
        if (featureManager == null) return Array.Empty<object>();

        try
        {
            return ToObjectArray(featureManager.GetFeatures(true));
        }
        catch (Exception ex)
        {
            LogIgnoredException("GetFeatureArray", ex);
            return Array.Empty<object>();
        }
    }

    private static object[] ToObjectArray(object value)
    {
        if (value == null) return Array.Empty<object>();
        if (value is object[] objectArray) return objectArray;
        if (value is Array array) return array.Cast<object>().ToArray();
        return Array.Empty<object>();
    }

    private static string[] ToStringArray(object value)
    {
        return ToObjectArray(value)
            .Select(item => item?.ToString())
            .Where(text => !string.IsNullOrWhiteSpace(text))
            .ToArray();
    }

    private static IEnumerable<Component2> EnumerateComponents(object[] items)
    {
        foreach (var item in items)
        {
            if (item is Component2 component)
                yield return component;
        }
    }

    private static CustomPropertyManager GetCustomPropertyManager(ModelDoc2 model, string configurationName)
    {
        return model?.Extension?.get_CustomPropertyManager(configurationName ?? "");
    }

    private static string GetActiveConfigurationName(ModelDoc2 model)
    {
        return Safe(() => model?.ConfigurationManager?.ActiveConfiguration?.Name) ?? "";
    }

    private static AssemblyDoc GetAssemblyDoc(ModelDoc2 model)
    {
        return GetComInterface<AssemblyDoc>(model);
    }

    private static PartDoc GetPartDoc(ModelDoc2 model)
    {
        return GetComInterface<PartDoc>(model);
    }

    private static T GetComInterface<T>(object comObject) where T : class
    {
        if (comObject == null) return null;

        var unknown = IntPtr.Zero;
        try
        {
            unknown = Marshal.GetIUnknownForObject(comObject);
            return (T)Marshal.GetTypedObjectForIUnknown(unknown, typeof(T));
        }
        catch (InvalidCastException)
        {
            return null;
        }
        catch (COMException)
        {
            return null;
        }
        finally
        {
            if (unknown != IntPtr.Zero)
                Marshal.Release(unknown);
        }
    }

    private static string GetArgString(Dictionary<string, object> args, string key, string fallback = "")
    {
        if (args != null && args.TryGetValue(key, out var val) && val != null) return val.ToString();
        return fallback;
    }

    private static bool GetArgBool(Dictionary<string, object> args, string key, bool fallback = false)
    {
        if (args == null || !args.TryGetValue(key, out var val) || val == null) return fallback;
        if (val is bool boolValue) return boolValue;
        return bool.TryParse(val.ToString(), out var parsed) ? parsed : fallback;
    }

    private object GetActiveDocumentInfo()
    {
        var model = GetActiveModel();
        return new
        {
            title = Safe(() => model.GetTitle()) ?? "",
            path = Safe(() => model.GetPathName()) ?? "",
            configuration = GetActiveConfigurationName(model),
            type = Safe(() => model.GetType())
        };
    }

    private object Rebuild()
    {
        var model = GetActiveModel();
        model.EditRebuild3();
        return new { rebuilt = true };
    }

    private object Save()
    {
        var model = GetActiveModel();
        model.Save3((int)swSaveAsOptions_e.swSaveAsOptions_Silent, 0, 0);
        return new { saved = true };
    }

    private object OpenDocument(Dictionary<string, object> args)
    {
        var path = GetArgString(args, "path");
        if (string.IsNullOrWhiteSpace(path)) throw new InvalidOperationException("path 不能为空");
        if (!File.Exists(path)) throw new InvalidOperationException("文件不存在");

        var docType = GetDocumentTypeFromPath(path);
        if (docType == 0) throw new InvalidOperationException("不支持的文件类型");

        var existing = TryGetOpenModelByPath(path);
        if (existing != null)
        {
            return new { path, title = Safe(() => existing.GetTitle()), alreadyOpen = true };
        }

        var errors = 0;
        var warnings = 0;
        var model = _swApp.OpenDoc6(path, docType, (int)swOpenDocOptions_e.swOpenDocOptions_Silent, "", ref errors, ref warnings);
        return new { path, title = Safe(() => model?.GetTitle()), errors, warnings };
    }

    private object ListExternalReferences(Dictionary<string, object> args)
    {
        var path = GetArgString(args, "path");
        if (string.IsNullOrWhiteSpace(path)) throw new InvalidOperationException("path is required");
        if (!File.Exists(path)) throw new InvalidOperationException("file does not exist: " + path);

        var dependencies = ToStringArray(_swApp.GetDocumentDependencies2(path, false, true, false));
        return new { path, dependencies };
    }

    private ModelDoc2 TryGetOpenModelByPath(string path)
    {
        try
        {
            var model = _swApp.GetOpenDocumentByName(path) as ModelDoc2;
            return model;
        }
        catch (Exception ex)
        {
            LogIgnoredException("TryGetOpenModelByPath", ex);
            return null;
        }
    }

    private static int GetDocumentTypeFromPath(string path)
    {
        var ext = Path.GetExtension(path)?.ToUpperInvariant();
        switch (ext)
        {
            case ".SLDPRT": return (int)swDocumentTypes_e.swDocPART;
            case ".SLDASM": return (int)swDocumentTypes_e.swDocASSEMBLY;
            case ".SLDDRW": return (int)swDocumentTypes_e.swDocDRAWING;
            default: return 0;
        }
    }

    // --- Drawing Save ---

    private object SaveActiveDrawingAs(string extension, string formatName)
    {
        var model = GetActiveModel();
        if (model.GetType() != (int)swDocumentTypes_e.swDocDRAWING)
            throw new InvalidOperationException("请在工程图环境下使用");

        var docPath = model.GetPathName();
        if (string.IsNullOrWhiteSpace(docPath))
            throw new InvalidOperationException($"当前工程图还没有保存，无法另存 {formatName}。");

        var outputPath = Path.ChangeExtension(docPath, extension);
        model.SaveAs3(outputPath, 0, 2);
        return new { outputPath, format = formatName };
    }

    // --- Open File Location ---

    private object GetActiveOrSelectedModelPath()
    {
        var model = GetActiveModel();
        var selectedComponent = GetSelectedComponent(model);
        if (selectedComponent != null)
        {
            var selectedPath = Safe(() => selectedComponent.GetPathName()) ?? "";
            if (string.IsNullOrWhiteSpace(selectedPath))
                selectedPath = Safe(() => (selectedComponent.GetModelDoc() as ModelDoc2)?.GetPathName()) ?? "";
            if (!string.IsNullOrWhiteSpace(selectedPath)) return new { path = selectedPath, selected = true };
        }

        var path = Safe(() => model.GetPathName()) ?? "";
        if (!string.IsNullOrWhiteSpace(path)) return new { path, selected = false };

        return new { path = Safe(() => model.GetTitle()) ?? "", selected = false };
    }

    // --- Rotate Drawing View ---

    private object RotateSelectedDrawingView()
    {
        var model = GetActiveModel();
        if (model.GetType() != (int)swDocumentTypes_e.swDocDRAWING)
            throw new InvalidOperationException("请在工程图环境下使用");

        var selMgr = model.SelectionManager as SelectionMgr;
        var swView = selMgr?.GetSelectedObject6(1, -1) as View;
        if (swView == null)
            throw new InvalidOperationException("请选择一个视图");

        if (swView.Angle > 4.5)
            swView.Angle = 0;
        else
            swView.Angle += Math.PI / 2;

        model.Extension.SelectByID2(swView.Name, "DRAWINGVIEW", 0, 0, 0, false, 0, null, 0);
        MarkDocDirty(model);

        return new { viewName = swView.Name, angle = swView.Angle };
    }

    private object SetIsoStandard()
    {
        var model = GetActiveModel();
        if (model.GetType() != (int)swDocumentTypes_e.swDocDRAWING)
            throw new InvalidOperationException("请在工程图环境下使用");

        var extension = model.Extension;
        if (extension == null)
            throw new InvalidOperationException("无法获取当前文档扩展对象");

        var ok = extension.SetUserPreferenceInteger(
            (int)swUserPreferenceIntegerValue_e.swDetailingDimensionStandard,
            0,
            (int)swDetailingStandard_e.swDetailingStandardISO);

        if (!ok)
            throw new InvalidOperationException("SolidWorks 未接受 ISO 标准设置");

        MarkDocDirty(model);
        return new { standard = "ISO", success = true };
    }

    private object MateReferencePlanes()
    {
        var model = GetActiveModel();
        if (model.GetType() != (int)swDocumentTypes_e.swDocASSEMBLY)
            throw new InvalidOperationException("请在装配体环境下使用");

        var assemblyDoc = GetAssemblyDoc(model);
        if (assemblyDoc == null) throw new InvalidOperationException("无法获取 AssemblyDoc");

        var selectedComponents = GetSelectedComponentsForReferencePlaneMate(model);
        if (selectedComponents.Count == 0)
            throw new InvalidOperationException("请先选中一个或多个组件");

        var totalMates = 0;
        var failed = 0;
        var details = new List<object>();

        foreach (var component in selectedComponents)
        {
            var componentName = Safe(() => component.Name2) ?? "";
            var componentMates = 0;
            var failures = new List<string>();

            foreach (var planeNames in ReferencePlaneMateNameGroups)
            {
                if (TryAddReferencePlaneMate(model, assemblyDoc, component, planeNames, out _, out var errorMessage))
                {
                    totalMates++;
                    componentMates++;
                }
                else
                {
                    failed++;
                    failures.Add(errorMessage);
                }
            }

            details.Add(new { component = componentName, mates = componentMates, failures });
        }

        if (totalMates == 0)
            throw new InvalidOperationException("未能添加基准面配合，请确认组件和装配体基准面名称匹配");

        model.EditRebuild3();
        MarkDocDirty(model);
        return new { components = selectedComponents.Count, mates = totalMates, failed, details };
    }

    private static List<Component2> GetSelectedComponentsForReferencePlaneMate(ModelDoc2 model)
    {
        var selectionMgr = model.SelectionManager as SelectionMgr;
        if (selectionMgr == null) return new List<Component2>();

        var components = new List<Component2>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var count = Safe(() => selectionMgr.GetSelectedObjectCount2(-1));
        for (var i = 1; i <= count; i++)
        {
            var selectedIndex = i;
            var component = Safe(() => selectionMgr.GetSelectedObjectsComponent3(selectedIndex, -1)) ??
                            Safe(() => selectionMgr.GetSelectedObjectsComponent(selectedIndex) as Component2);
            if (component == null) continue;

            var key = GetComponentSelectionName(component);
            if (string.IsNullOrWhiteSpace(key)) key = Safe(() => component.Name2) ?? selectedIndex.ToString();
            if (seen.Add(key)) components.Add(component);
        }

        return components;
    }

    private static bool TryAddReferencePlaneMate(
        ModelDoc2 model,
        AssemblyDoc assemblyDoc,
        Component2 component,
        string[] planeNames,
        out string usedPlaneName,
        out string errorMessage)
    {
        usedPlaneName = "";
        errorMessage = "";

        var componentSelectionName = GetComponentSelectionName(component);
        if (string.IsNullOrWhiteSpace(componentSelectionName))
        {
            errorMessage = "无法获取组件选择路径";
            return false;
        }

        foreach (var planeName in planeNames)
        {
            var componentPlaneName = planeName + "@" + componentSelectionName;
            try
            {
                model.ClearSelection2(true);
                var assemblyPlaneSelected = model.Extension.SelectByID2(planeName, "PLANE", 0, 0, 0, false, 1, null, 0);
                var componentPlaneSelected = model.Extension.SelectByID2(componentPlaneName, "PLANE", 0, 0, 0, true, 1, null, 0);
                if (!assemblyPlaneSelected || !componentPlaneSelected)
                {
                    errorMessage = planeName + ": 基准面选择失败";
                    continue;
                }

                var errorStatus = 0;
                var mate = assemblyDoc.AddMate4(
                    (int)swMateType_e.swMateCOINCIDENT,
                    (int)swMateAlign_e.swMateAlignALIGNED,
                    false,
                    0,
                    0,
                    0,
                    0,
                    0,
                    0,
                    0,
                    0,
                    false,
                    false,
                    out errorStatus);

                if (mate != null && errorStatus == 0)
                {
                    usedPlaneName = planeName;
                    return true;
                }

                errorMessage = planeName + ": AddMate4 错误 " + errorStatus;
            }
            catch (Exception ex)
            {
                errorMessage = planeName + ": " + ex.Message;
                LogIgnoredException("TryAddReferencePlaneMate", ex);
            }
            finally
            {
                try { model.ClearSelection2(true); }
                catch (Exception ex) { LogIgnoredException("TryAddReferencePlaneMate.ClearSelection", ex); }
            }
        }

        return false;
    }

    private static string GetComponentSelectionName(Component2 component)
    {
        var selectionName = Safe(() => component.GetSelectByIDString());
        if (!string.IsNullOrWhiteSpace(selectionName)) return selectionName;

        return Safe(() => component.Name2);
    }

    private object SelectComponent(Dictionary<string, object> args)
    {
        var model = GetActiveModel();
        if (model.GetType() != (int)swDocumentTypes_e.swDocASSEMBLY)
            throw new InvalidOperationException("请在装配体环境下使用");

        var name = GetArgString(args, "name");
        if (string.IsNullOrWhiteSpace(name))
            throw new InvalidOperationException("name 不能为空");

        var component = FindComponentByName(model, name);
        if (component == null)
            throw new InvalidOperationException("未找到组件: " + name);

        model.ClearSelection2(true);
        var ok = Safe(() => component.Select4(false, null, false));
        if (!ok)
            ok = Safe(() => component.Select2(false, 0));
        if (!ok)
            throw new InvalidOperationException("组件选择失败: " + name);

        return new
        {
            selected = true,
            name = Safe(() => component.Name2) ?? "",
            path = Safe(() => component.GetPathName()) ?? ""
        };
    }

    private static Component2 FindComponentByName(ModelDoc2 model, string name)
    {
        var configuration = model?.GetActiveConfiguration() as Configuration;
        var rootComponent = configuration?.GetRootComponent() as Component2;
        return FindComponentByName(GetComponentChildren(rootComponent), name);
    }

    private static Component2 FindComponentByName(object[] components, string name)
    {
        foreach (var component in EnumerateComponents(components))
        {
            var componentName = Safe(() => component.Name2) ?? "";
            if (string.Equals(componentName, name, StringComparison.OrdinalIgnoreCase))
                return component;

            var child = FindComponentByName(GetComponentChildren(component), name);
            if (child != null) return child;
        }

        return null;
    }

    private static void MarkDocDirty(ModelDoc2 model)
    {
        try { model?.SetSaveFlag(); }
        catch (Exception ex) { LogIgnoredException("MarkDocDirty", ex); }
    }

    private object DeleteErrorMates()
    {
        var model = GetActiveModel();
        if (model.GetType() != (int)swDocumentTypes_e.swDocASSEMBLY)
            throw new InvalidOperationException("请在装配体环境下使用");

        var mateGroups = 0;
        var selected = 0;
        var feature = Safe(() => model.FirstFeature() as Feature);
        while (feature != null)
        {
            var typeName = Safe(() => feature.GetTypeName2()) ?? "";
            if (string.Equals(typeName, "MateGroup", StringComparison.OrdinalIgnoreCase))
            {
                mateGroups++;
                model.ClearSelection2(true);

                var groupSelected = 0;
                var subFeature = Safe(() => feature.GetFirstSubFeature() as Feature);
                while (subFeature != null)
                {
                    var errorCode = GetFeatureErrorCode(subFeature, out var isWarning);
                    if (errorCode != 0 && !isWarning)
                    {
                        if (Safe(() => subFeature.Select2(true, 0)))
                        {
                            groupSelected++;
                            selected++;
                        }
                    }

                    subFeature = Safe(() => subFeature.GetNextSubFeature() as Feature);
                }

                if (groupSelected > 0)
                    model.EditDelete();
            }

            feature = Safe(() => feature.GetNextFeature() as Feature);
        }

        if (selected > 0)
        {
            model.EditRebuild3();
            MarkDocDirty(model);
        }

        return new { done = true, mateGroups, deleted = selected };
    }

    private static int GetFeatureErrorCode(Feature feature, out bool isWarning)
    {
        isWarning = false;
        try
        {
            return feature?.GetErrorCode2(out isWarning) ?? 0;
        }
        catch (Exception ex)
        {
            LogIgnoredException("GetFeatureErrorCode", ex);
            return 0;
        }
    }

    private object RunSwpMacro(Dictionary<string, object> args)
    {
        var path = GetArgString(args, "path");
        if (string.IsNullOrWhiteSpace(path))
            throw new InvalidOperationException("请选择 .swp 宏文件");
        if (!File.Exists(path))
            throw new InvalidOperationException("宏文件不存在: " + path);
        if (!string.Equals(Path.GetExtension(path), ".swp", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("只支持 .swp 宏文件");

        var error = 0;
        var ok = _swApp.RunMacro2(path, "", "main", 0, out error);
        if (!ok || error != 0)
            throw new InvalidOperationException("宏执行失败，错误码: " + error);

        CheckAndBroadcastDocChange();
        return new { done = true, path };
    }

    // --- Hide Config Names (FeatureManager) ---

    private object HideConfigNames()
    {
        var model = GetActiveModel();
        if (model.GetType() != (int)swDocumentTypes_e.swDocASSEMBLY)
            throw new InvalidOperationException("请在装配体环境下使用");

        var featMgr = model.FeatureManager;
        if (featMgr == null) throw new InvalidOperationException("无法获取 FeatureManager");

        ConfigureFeatureManagerDisplay(featMgr);

        RecursiveHideConfigNames(_swApp, model);

        return new { done = true };
    }

    private static void RecursiveHideConfigNames(SldWorks.SldWorks swApp, ModelDoc2 asmDoc)
    {
        var configuration = asmDoc?.GetActiveConfiguration() as Configuration;
        var rootComponent = configuration?.GetRootComponent() as Component2;

        foreach (var child in EnumerateComponents(GetComponentChildren(rootComponent)))
        {
            try
            {
                var childModel = child.GetModelDoc() as ModelDoc2;
                if (childModel == null) continue;

                var childType = childModel.GetType();
                if (childType == (int)swDocumentTypes_e.swDocASSEMBLY)
                {
                    var longstatus = 0;
                    var longWarnings = 0;
                    var childPath = Safe(() => child.GetPathName()) ?? "";
                    if (!string.IsNullOrWhiteSpace(childPath))
                    {
                        var fopen = swApp.OpenDoc6(childPath, (int)swDocumentTypes_e.swDocASSEMBLY,
                            (int)swOpenDocOptions_e.swOpenDocOptions_Silent, "", ref longstatus, ref longWarnings);
                        if (longstatus == 0 && fopen != null)
                        {
                            var swFeatMgr = fopen.FeatureManager;
                            if (swFeatMgr != null)
                                ConfigureFeatureManagerDisplay(swFeatMgr);
                        }
                    }

                    RecursiveHideConfigNames(swApp, childModel);
                }
            }
            catch (Exception ex)
            {
                LogIgnoredException("RecursiveHideConfigNames", ex);
            }
        }
    }

    private static void ConfigureFeatureManagerDisplay(FeatureManager featureManager)
    {
        featureManager.HideComponentSingleConfigurationOrDisplayStateNames = false;
        featureManager.SetComponentIdentifiers(4, 0, 0);
        featureManager.SetComponentIdentifiers(2, 0, 0);
        featureManager.ShowComponentConfigurationNames = false;
        featureManager.ShowComponentConfigurationDescriptions = false;
        featureManager.ShowDisplayStateNames = false;
    }

    // --- Get Component Tree ---

    private object GetComponentTree()
    {
        var model = GetActiveModel();
        if (model.GetType() != (int)swDocumentTypes_e.swDocASSEMBLY)
            throw new InvalidOperationException("请在装配体环境下使用");

        var components = new List<object>();
        var configuration = model.GetActiveConfiguration() as Configuration;
        var rootComponent = configuration?.GetRootComponent() as Component2;

        foreach (var child in EnumerateComponents(GetComponentChildren(rootComponent)))
        {
            var childModel = child.GetModelDoc() as ModelDoc2;
            var info = new
            {
                name = Safe(() => child.Name2),
                path = Safe(() => child.GetPathName()),
                referencedPath = childModel != null ? Safe(() => childModel.GetPathName()) : null,
                type = childModel != null ? Safe(() => childModel.GetType()) : -1,
                isSuppressed = Safe(() => child.IsSuppressed()),
                isEnvelope = Safe(() => child.IsEnvelope())
            };
            components.Add(info);
        }

        return new { components };
    }

    // --- Sort Components ---

    private object SortComponents(Dictionary<string, object> args)
    {
        var model = GetActiveModel();
        if (model.GetType() != (int)swDocumentTypes_e.swDocASSEMBLY)
            throw new InvalidOperationException("请在装配体环境下使用");

        var assemblyDoc = GetAssemblyDoc(model);
        if (assemblyDoc == null) throw new InvalidOperationException("无法获取 AssemblyDoc");
        var options = SortOptions.FromArgs(args);

        var vFeats = GetFeatureArray(model);

        // Find start/end indices
        var startIdx = 0;
        var endIdx = vFeats.Length - 1;
        for (var i = 0; i < vFeats.Length; i++)
        {
            var feature = vFeats[i] as Feature;
            if (feature == null) continue;

            var t = Safe(() => feature.GetTypeName2()) ?? "";
            if (t == "OriginProfileFeature") startIdx = i + 1;
            if (t == "MateGroup") { endIdx = i - 1; break; }
        }

        // Group features
        var folders = new List<Feature>();
        var folderComponents = new List<List<Feature>>();
        var topLevelFeats = new List<Feature>();
        var suppressedEnvFeats = new List<Feature>();
        List<Feature> currentFolderComps = null;

        for (var i = startIdx; i <= endIdx; i++)
        {
            var feat = vFeats[i] as Feature;
            if (feat == null) continue;

            var featType = Safe(() => feat.GetTypeName2()) ?? "";
            if (featType == "FtrFolder")
            {
                var featureName = Safe(() => feat.Name) ?? "";
                if (featureName.IndexOf("___EndTag___", StringComparison.Ordinal) < 0)
                {
                    folders.Add(feat);
                    currentFolderComps = new List<Feature>();
                    folderComponents.Add(currentFolderComps);
                }
                else
                {
                    currentFolderComps = null;
                }
            }
            else if (featType == "Reference")
            {
                var isSupOrEnv = false;
                try
                {
                    var comp = feat.GetSpecificFeature2() as Component2;
                    if (comp != null) isSupOrEnv = comp.IsSuppressed() || comp.IsEnvelope();
                }
                catch (Exception ex)
                {
                    LogIgnoredException("SortComponents.ReferenceState", ex);
                }
                if (isSupOrEnv)
                {
                    if (currentFolderComps != null) currentFolderComps.Add(feat);
                    else suppressedEnvFeats.Add(feat);
                }
                else if (currentFolderComps != null)
                {
                    currentFolderComps.Add(feat);
                }
                else
                {
                    topLevelFeats.Add(feat);
                }
            }
        }

        // Collect and sort
        var sortedPartComps = CollectAndSort(topLevelFeats, false, options);
        var sortedAsmComps = CollectAndSort(topLevelFeats, true, options);
        var sortedSupPart = CollectAndSort(suppressedEnvFeats, false, options);
        var sortedSupAsm = CollectAndSort(suppressedEnvFeats, true, options);

        var allSorted = BuildSortedComponentOrder(sortedAsmComps, sortedPartComps, sortedSupAsm, sortedSupPart, options);

        if (allSorted.Count > 0)
        {
            var lastFolder = folders.Count > 0 ? folders[folders.Count - 1] : null;
            if (lastFolder != null)
                assemblyDoc.ReorderComponents(allSorted[0], lastFolder, (int)swReorderComponentsWhere_e.swReorderComponents_After);
            for (var i = 1; i < allSorted.Count; i++)
                assemblyDoc.ReorderComponents(allSorted[i], allSorted[i - 1], 1);
        }

        var foldersSorted = 0;
        if (options.SortFolders)
        {
            for (var i = 0; i < folders.Count; i++)
            {
                var folderFeats = i < folderComponents.Count ? folderComponents[i] : null;
                if (SortComponentsInFolder(folders[i], folderFeats, assemblyDoc, options))
                    foldersSorted++;
            }
        }

        if (options.RecursiveSubAssemblies)
        {
            var processed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            RecursiveSortSubAssemblies(topLevelFeats, processed, options);
            foreach (var fc in folderComponents)
                RecursiveSortSubAssemblies(fc, processed, options);
        }

        model.EditRebuild3();
        model.ClearSelection2(true);

        return new { done = true, topLevelSorted = allSorted.Count, foldersSorted };
    }

    private static List<Component2> CollectAndSort(List<Feature> feats, bool assemblies, SortOptions options)
    {
        var result = new List<Component2>();
        var targetType = assemblies ? (int)swDocumentTypes_e.swDocASSEMBLY : (int)swDocumentTypes_e.swDocPART;
        foreach (var feat in feats)
        {
            var comp = Safe(() => feat.GetSpecificFeature2() as Component2);
            if (comp == null) continue;
            try
            {
                var childModel = comp.GetModelDoc() as ModelDoc2;
                var childType = childModel != null ? childModel.GetType() : -1;
                if (childType == targetType) result.Add(comp);
            }
            catch (Exception ex)
            {
                LogIgnoredException("CollectAndSort", ex);
            }
        }
        result.Sort((a, b) => CompareComponents(a, b, options));
        return result;
    }

    private static List<Component2> BuildSortedComponentOrder(
        List<Component2> asmComps,
        List<Component2> partComps,
        List<Component2> suppressedAsmComps,
        List<Component2> suppressedPartComps,
        SortOptions options)
    {
        var all = new List<Component2>();
        if (!options.SuppressedLast)
        {
            var mergedAsmComps = new List<Component2>();
            mergedAsmComps.AddRange(asmComps);
            mergedAsmComps.AddRange(suppressedAsmComps);
            mergedAsmComps.Sort((a, b) => CompareComponents(a, b, options));

            var mergedPartComps = new List<Component2>();
            mergedPartComps.AddRange(partComps);
            mergedPartComps.AddRange(suppressedPartComps);
            mergedPartComps.Sort((a, b) => CompareComponents(a, b, options));

            AppendTypeGroups(all, mergedAsmComps, mergedPartComps, options);
            return all;
        }

        AppendTypeGroups(all, asmComps, partComps, options);
        AppendTypeGroups(all, suppressedAsmComps, suppressedPartComps, options);

        return all;
    }

    private static void AppendTypeGroups(List<Component2> target, List<Component2> asmComps, List<Component2> partComps, SortOptions options)
    {
        if (options.AssemblyFirst)
        {
            target.AddRange(asmComps);
            target.AddRange(partComps);
        }
        else
        {
            target.AddRange(partComps);
            target.AddRange(asmComps);
        }
    }

    private static int CompareComponents(Component2 a, Component2 b, SortOptions options)
    {
        var left = GetComponentSortKey(a, options);
        var right = GetComponentSortKey(b, options);
        var result = string.Compare(left, right, StringComparison.OrdinalIgnoreCase);
        return options.Descending ? -result : result;
    }

    private static string GetComponentSortKey(Component2 component, SortOptions options)
    {
        if (component == null) return "";

        if (string.Equals(options.NameSource, "FileName", StringComparison.OrdinalIgnoreCase))
        {
            var path = Safe(() => component.GetPathName()) ?? "";
            var fileName = Path.GetFileNameWithoutExtension(path);
            if (!string.IsNullOrWhiteSpace(fileName)) return fileName;
        }

        return Safe(() => component.Name2) ?? "";
    }

    private static bool SortComponentsInFolder(Feature folder, List<Feature> folderFeats, AssemblyDoc assemblyDoc, SortOptions options)
    {
        var partComps = new List<Component2>();
        var asmComps = new List<Component2>();
        var supPartComps = new List<Component2>();
        var supAsmComps = new List<Component2>();

        var feats = folderFeats != null && folderFeats.Count > 0
            ? folderFeats
            : GetFolderReferenceFeaturesFromSubFeatures(folder);

        foreach (var currentSubFeat in feats)
        {
            if ((Safe(() => currentSubFeat.GetTypeName2()) ?? "") == "Reference")
            {
                var comp = Safe(() => currentSubFeat.GetSpecificFeature2() as Component2);
                if (comp != null)
                {
                    var isSupEnv = Safe(() => comp.IsSuppressed()) || Safe(() => comp.IsEnvelope());
                    var childModel = Safe(() => comp.GetModelDoc() as ModelDoc2);
                    var childType = childModel != null ? childModel.GetType() : -1;
                    if (childType == (int)swDocumentTypes_e.swDocASSEMBLY)
                    {
                        if (isSupEnv) supAsmComps.Add(comp); else asmComps.Add(comp);
                    }
                    else if (childType == (int)swDocumentTypes_e.swDocPART)
                    {
                        if (isSupEnv) supPartComps.Add(comp); else partComps.Add(comp);
                    }
                }
            }
        }

        asmComps.Sort((a, b) => CompareComponents(a, b, options));
        partComps.Sort((a, b) => CompareComponents(a, b, options));
        supAsmComps.Sort((a, b) => CompareComponents(a, b, options));
        supPartComps.Sort((a, b) => CompareComponents(a, b, options));

        var all = BuildSortedComponentOrder(asmComps, partComps, supAsmComps, supPartComps, options);

        if (all.Count > 1)
        {
            for (var i = 1; i < all.Count; i++)
                assemblyDoc.ReorderComponents(all[i], all[i - 1], 1);
            return true;
        }

        return false;
    }

    private static List<Feature> GetFolderReferenceFeaturesFromSubFeatures(Feature folder)
    {
        var result = new List<Feature>();
        var subFeat = Safe(() => folder.GetFirstSubFeature() as Feature);
        while (subFeat != null)
        {
            var currentSubFeat = subFeat;
            if ((Safe(() => currentSubFeat.GetTypeName2()) ?? "") == "Reference")
                result.Add(currentSubFeat);

            subFeat = Safe(() => currentSubFeat.GetNextSubFeature() as Feature);
        }

        return result;
    }

    private static void RecursiveSortSubAssemblies(List<Feature> feats, HashSet<string> processed, SortOptions options)
    {
        foreach (var feat in feats)
        {
            if ((Safe(() => feat.GetTypeName2()) ?? "") != "Reference") continue;
            var comp = Safe(() => feat.GetSpecificFeature2() as Component2);
            if (comp == null) continue;
            try
            {
                var model = comp.GetModelDoc() as ModelDoc2;
                if (model == null || model.GetType() != (int)swDocumentTypes_e.swDocASSEMBLY) continue;
                var path = model.GetPathName();
                if (string.IsNullOrWhiteSpace(path) || !processed.Add(path)) continue;
                SortSubAsmByFeatures(model, processed, options);
            }
            catch (Exception ex)
            {
                LogIgnoredException("RecursiveSortSubAssemblies", ex);
            }
        }
    }

    private static void SortSubAsmByFeatures(ModelDoc2 asmModel, HashSet<string> processed, SortOptions options)
    {
        try
        {
            var vFeats = GetFeatureArray(asmModel);
            var refFeats = new List<Feature>();
            for (var i = 0; i < vFeats.Length; i++)
            {
                var feature = vFeats[i] as Feature;
                if (feature != null && (Safe(() => feature.GetTypeName2()) ?? "") == "Reference")
                    refFeats.Add(feature);
            }

            var asmComps = CollectAndSort(refFeats, true, options);
            var partComps = CollectAndSort(refFeats, false, options);
            var all = BuildSortedComponentOrder(asmComps, partComps, new List<Component2>(), new List<Component2>(), options);

            if (all.Count > 1)
            {
                var asmDoc = GetAssemblyDoc(asmModel);
                if (asmDoc != null)
                {
                    for (var i = 1; i < all.Count; i++)
                        asmDoc.ReorderComponents(all[i], all[i - 1], 1);
                }
            }

            RecursiveSortSubAssemblies(refFeats, processed, options);
        }
        catch (Exception ex)
        {
            LogIgnoredException("SortSubAsmByFeatures", ex);
        }
    }

    private sealed class SortOptions
    {
        public bool AssemblyFirst { get; set; } = true;
        public bool SuppressedLast { get; set; } = true;
        public bool SortFolders { get; set; } = true;
        public bool RecursiveSubAssemblies { get; set; } = true;
        public bool Descending { get; set; }
        public string NameSource { get; set; } = "ComponentName";

        public static SortOptions FromArgs(Dictionary<string, object> args)
        {
            var options = new SortOptions();
            if (args == null) return options;

            options.AssemblyFirst = GetBool(args, "assemblyFirst", true);
            options.SuppressedLast = GetBool(args, "suppressedLast", true);
            options.SortFolders = GetBool(args, "sortFolders", true);
            options.RecursiveSubAssemblies = GetBool(args, "recursiveSubAssemblies", true);
            options.Descending = GetBool(args, "descending", false);
            options.NameSource = GetString(args, "nameSource", "ComponentName");
            return options;
        }

        private static bool GetBool(Dictionary<string, object> args, string key, bool fallback)
        {
            if (!args.TryGetValue(key, out var value) || value == null) return fallback;
            if (value is bool boolValue) return boolValue;
            return bool.TryParse(value.ToString(), out var parsed) ? parsed : fallback;
        }

        private static string GetString(Dictionary<string, object> args, string key, string fallback)
        {
            if (!args.TryGetValue(key, out var value) || value == null) return fallback;
            var text = value.ToString();
            return string.IsNullOrWhiteSpace(text) ? fallback : text;
        }
    }

    // --- Read Properties ---

    private object ReadProperties(Dictionary<string, object> args)
    {
        var target = ResolvePropertyTarget(args);
        if (target.FileProperties != null)
        {
            return new
            {
                title = target.Title,
                path = target.Path,
                configuration = string.IsNullOrWhiteSpace(target.ConfigurationName) ? "custom" : target.ConfigurationName,
                source = target.Source,
                selectedComponent = target.SelectedComponent,
                properties = target.FileProperties
            };
        }

        var manager = GetCustomPropertyManager(target.Model, target.ConfigurationName);
        if (manager == null) throw new InvalidOperationException("无法获取属性管理器");

        var values = new Dictionary<string, string>();

        try
        {
            foreach (var name in GetPropertyNames(manager))
            {
                try
                {
                    manager.Get5(name, false, out var rawValue, out var resolvedValue, out var _);
                    values[name] = !string.IsNullOrWhiteSpace(resolvedValue) ? resolvedValue : rawValue ?? "";
                }
                catch (Exception ex)
                {
                    LogIgnoredException("ReadProperties.GetValue", ex);
                }
            }
        }
        catch (Exception ex)
        {
            LogIgnoredException("ReadProperties", ex);
        }

        return new
        {
            title = target.Title,
            path = target.Path,
            configuration = string.IsNullOrWhiteSpace(target.ConfigurationName) ? "custom" : target.ConfigurationName,
            source = target.Source,
            selectedComponent = target.SelectedComponent,
            properties = values
        };
    }

    // --- Write Properties ---

    private object WriteProperties(Dictionary<string, object> args)
    {
        var target = ResolvePropertyTarget(args);
        if (target.FileProperties != null)
        {
            throw new InvalidOperationException("选中子件为轻化状态时仅支持读取文件属性，写入前请在 SolidWorks 中还原该子件。");
        }

        var propsArg = args.TryGetValue("properties", out var p) ? p : null;
        var properties = propsArg as Dictionary<string, object>;
        if (properties == null) throw new InvalidOperationException("properties 不能为空");

        var manager = GetCustomPropertyManager(target.Model, target.ConfigurationName);
        if (manager == null) throw new InvalidOperationException("无法获取属性管理器");

        var written = new List<string>();
        foreach (var kvp in properties)
        {
            manager.Add3(kvp.Key, 30, kvp.Value?.ToString() ?? "", 2);
            written.Add(kvp.Key);
        }

        MarkDocDirty(target.Model);

        return new
        {
            title = target.Title,
            configuration = target.ConfigurationName,
            source = target.Source,
            selectedComponent = target.SelectedComponent,
            written = written.ToArray()
        };
    }

    private PropertyTarget ResolvePropertyTarget(Dictionary<string, object> args)
    {
        var activeModel = GetActiveModel();
        var targetModel = activeModel;
        var selectedComponent = false;
        var title = Safe(() => activeModel.GetTitle()) ?? "";
        var path = Safe(() => activeModel.GetPathName()) ?? "";

        var comp = GetSelectedComponent(activeModel);
        if (comp != null)
        {
            var componentModel = Safe(() => comp.GetModelDoc() as ModelDoc2) ??
                                 Safe(() => comp.GetModelDoc2() as ModelDoc2);
            if (componentModel != null)
            {
                targetModel = componentModel;
                selectedComponent = true;
                title = Safe(() => comp.Name2) ?? Safe(() => componentModel.GetTitle()) ?? title;
                path = Safe(() => componentModel.GetPathName()) ?? path;
            }
            else
            {
                selectedComponent = true;
                title = Safe(() => comp.Name2) ?? title;
                path = Safe(() => comp.GetPathName()) ?? "";
            }
        }

        var hasExplicitConfiguration = args.ContainsKey("configuration");
        var explicitSource = GetArgString(args, "source");
        var useCustomProperties =
            string.Equals(explicitSource, "custom", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(explicitSource, "file", StringComparison.OrdinalIgnoreCase) ||
            (!hasExplicitConfiguration && GetArgBool(args, "customProperties"));

        var configName = hasExplicitConfiguration ? GetArgString(args, "configuration") : "";
        if (!useCustomProperties && !hasExplicitConfiguration)
        {
            configName = selectedComponent
                ? Safe(() => comp.ReferencedConfiguration) ?? ""
                : GetActiveConfigurationName(targetModel);
        }

        Dictionary<string, string> fileProperties = null;
        if (selectedComponent && targetModel == activeModel && !string.IsNullOrWhiteSpace(path))
        {
            fileProperties = ReadFileProperties(path, useCustomProperties ? "" : configName);
        }

        return new PropertyTarget
        {
            Model = targetModel,
            Title = title,
            Path = path,
            ConfigurationName = configName ?? "",
            Source = useCustomProperties ? "custom" : "configuration",
            SelectedComponent = selectedComponent,
            FileProperties = fileProperties
        };
    }

    private static Component2 GetSelectedComponent(ModelDoc2 activeModel)
    {
        if (activeModel.GetType() != (int)swDocumentTypes_e.swDocASSEMBLY) return null;

        try
        {
            var selMgr = activeModel.SelectionManager as SelectionMgr;
            if (selMgr == null) return null;

            var count = Safe(() => selMgr.GetSelectedObjectCount2(-1));
            for (var i = 1; i <= count; i++)
            {
                var selectedIndex = i;
                var comp = Safe(() => selMgr.GetSelectedObjectsComponent(selectedIndex) as Component2);
                if (comp != null) return comp;
            }
        }
        catch (Exception ex)
        {
            LogIgnoredException("GetSelectedComponent", ex);
        }

        return null;
    }

    private Dictionary<string, string> ReadFileProperties(string path, string configurationName)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            throw new InvalidOperationException("无法读取选中子件文件属性，文件路径无效: " + path);
        }

        SwDMApplication application = null;
        ISwDMDocument document = null;

        try
        {
            application = CreateDocumentManagerApplication();
            var documentType = GetDocumentManagerType(path);
            if (documentType == SwDmDocumentType.swDmDocumentUnknown)
            {
                throw new InvalidOperationException("不支持的 SolidWorks 文件类型: " + path);
            }

            document = application.GetDocument(path, documentType, true, out var error) as ISwDMDocument;
            if (document == null || error != SwDmDocumentOpenError.swDmDocumentOpenErrorNone)
            {
                throw new InvalidOperationException("Document Manager 打开文件失败: " + error);
            }

            return string.IsNullOrWhiteSpace(configurationName)
                ? ReadDocumentManagerCustomProperties(document)
                : ReadDocumentManagerConfigurationProperties(document, configurationName);
        }
        finally
        {
            try { document?.CloseDoc(); } catch { }
        }
    }

    private static SwDMApplication CreateDocumentManagerApplication()
    {
        var factory = new SwDMClassFactoryClass();
        foreach (var license in LoadDocumentManagerLicenseCandidates())
        {
            try
            {
                var application = factory.GetApplication(license) as SwDMApplication;
                if (application != null) return application;
            }
            catch
            {
                // Try the next normalized license token.
            }
        }

        throw new InvalidOperationException("无法初始化 SolidWorks Document Manager，请检查内置许可证。");
    }

    private static IEnumerable<string> LoadDocumentManagerLicenseCandidates()
    {
        const string embeddedLicense = "SOLIDWORKS_2022:swdocmgr_general-11785-02051-00064-50177-06612-36864-49921-00003-21424-27712-02333-41243-17858-56070-14468-62467-35817-38164-41304-28396-16118-35795-11666-40614-37528-44680-42142-42646-25790-25696-00100-12572-11577-30049-11623-12338-12338-4,swdocmgr_previews-11785-02051-00064-50177-06612-36864-49921-00003-55360-53060-25343-42769-59069-52473-22473-52225-12074-04081-32909-30728-28589-33225-11507-40614-37528-44680-42142-42646-25790-25696-00100-12572-11577-30049-11623-12338-12338-3,swdocmgr_geometry-11785-02051-00064-50177-06612-36864-49921-00003-47348-23774-43673-03705-12160-02780-32459-34818-63205-17331-46285-63135-49863-07899-11787-40614-37528-44680-42142-42646-25790-25696-00100-12572-11577-30049-11623-12338-12338-2,swdocmgr_dimxpert-11785-02051-00064-50177-06612-36864-49921-00003-29996-08472-47529-56591-62456-46237-10843-31747-32567-27053-59927-17093-07167-05996-11461-40614-37528-44680-42142-42646-25790-25696-00100-12572-11577-30049-11623-12338-12338-7,swdocmgr_tessellation-11785-02051-00064-50177-06612-36864-49921-00003-10436-56910-32567-34976-20928-11850-62944-24577-40211-48738-24555-37037-32570-31276-11315-40614-37528-44680-42142-42646-25790-25696-00100-12572-11577-30049-11623-12338-12338-9,swdocmgr_xml-11785-02051-00064-50177-06612-36864-49921-00003-42016-05489-35279-24819-52264-14236-16400-48131-43581-35953-11290-28017-22653-22728-11882-40614-37528-44680-42142-42646-25790-25696-00100-12572-11577-30049-11623-12338-12338-8";

        if (string.IsNullOrWhiteSpace(embeddedLicense)) yield break;

        yield return embeddedLicense;
        foreach (var entry in embeddedLicense.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries))
        {
            var trimmed = entry.Trim();
            if (trimmed.Length == 0) continue;

            var parts = trimmed.Split(new[] { '-' }, 2, StringSplitOptions.None);
            yield return parts.Length == 2 ? parts[1] : parts[0];
        }
    }

    private static SwDmDocumentType GetDocumentManagerType(string path)
    {
        switch ((Path.GetExtension(path) ?? "").ToUpperInvariant())
        {
            case ".SLDPRT": return SwDmDocumentType.swDmDocumentPart;
            case ".SLDASM": return SwDmDocumentType.swDmDocumentAssembly;
            case ".SLDDRW": return SwDmDocumentType.swDmDocumentDrawing;
            default: return SwDmDocumentType.swDmDocumentUnknown;
        }
    }

    private static Dictionary<string, string> ReadDocumentManagerCustomProperties(ISwDMDocument document)
    {
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var name in GetDocumentManagerPropertyNames(document.GetCustomPropertyNames))
        {
            try
            {
                SwDmCustomInfoType type;
                string linkedTo;
                var value = document is ISwDMDocument5 document5
                    ? document5.GetCustomPropertyValues(name, out type, out linkedTo)
                    : document.GetCustomProperty(name, out type);
                values[name] = value ?? string.Empty;
            }
            catch (Exception ex)
            {
                LogIgnoredException("ReadDocumentManagerCustomProperties", ex);
            }
        }

        return values;
    }

    private static Dictionary<string, string> ReadDocumentManagerConfigurationProperties(
        ISwDMDocument document,
        string configurationName)
    {
        var configuration = ResolveDocumentManagerConfiguration(document, configurationName);
        if (configuration == null) return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var name in GetDocumentManagerPropertyNames(configuration.GetCustomPropertyNames))
        {
            try
            {
                SwDmCustomInfoType type;
                string linkedTo;
                var value = configuration is ISwDMConfiguration5 configuration5
                    ? configuration5.GetCustomPropertyValues(name, out type, out linkedTo)
                    : configuration.GetCustomProperty(name, out type);
                values[name] = value ?? string.Empty;
            }
            catch (Exception ex)
            {
                LogIgnoredException("ReadDocumentManagerConfigurationProperties", ex);
            }
        }

        return values;
    }

    private static ISwDMConfiguration ResolveDocumentManagerConfiguration(ISwDMDocument document, string configurationName)
    {
        if (document?.ConfigurationManager == null) return null;

        foreach (var candidate in GetDocumentManagerConfigurationCandidates(document, configurationName))
        {
            var configuration = document.ConfigurationManager.GetConfigurationByName(candidate) as ISwDMConfiguration;
            if (configuration != null) return configuration;
        }

        return null;
    }

    private static IEnumerable<string> GetDocumentManagerConfigurationCandidates(ISwDMDocument document, string configurationName)
    {
        if (!string.IsNullOrWhiteSpace(configurationName)) yield return configurationName;

        foreach (var name in GetDocumentManagerPropertyNames(document.ConfigurationManager.GetConfigurationNames))
        {
            if (!string.IsNullOrWhiteSpace(name) && !string.Equals(name, configurationName, StringComparison.OrdinalIgnoreCase))
            {
                yield return name;
            }
        }
    }

    private static IEnumerable<string> GetDocumentManagerPropertyNames(Func<object> getNames)
    {
        object names;
        try
        {
            names = getNames();
        }
        catch
        {
            yield break;
        }

        if (names is string[] stringNames)
        {
            foreach (var name in stringNames)
            {
                if (!string.IsNullOrWhiteSpace(name)) yield return name;
            }
        }
        else if (names is object[] objectNames)
        {
            foreach (var item in objectNames)
            {
                var name = item?.ToString();
                if (!string.IsNullOrWhiteSpace(name)) yield return name;
            }
        }
    }

    private static IEnumerable<string> GetPropertyNames(CustomPropertyManager manager)
    {
        if (manager == null) return Enumerable.Empty<string>();

        var names = manager.GetNames();
        if (names is string[] stringNames)
            return stringNames.Where(name => !string.IsNullOrWhiteSpace(name));

        if (names is object[] objectNames)
            return objectNames.Select(item => item?.ToString()).Where(name => !string.IsNullOrWhiteSpace(name));

        return Enumerable.Empty<string>();
    }

    private object DeleteCustomProperties()
    {
        var model = GetActiveModel();
        var stats = DeletePropertiesRecursive(model, deleteConfigurationProperties: false, new HashSet<string>(StringComparer.OrdinalIgnoreCase));
        return new { done = true, documents = stats.Documents, deleted = stats.Deleted };
    }

    private object DeleteConfigurationProperties()
    {
        var model = GetActiveModel();
        var stats = DeletePropertiesRecursive(model, deleteConfigurationProperties: true, new HashSet<string>(StringComparer.OrdinalIgnoreCase));
        return new { done = true, documents = stats.Documents, deleted = stats.Deleted };
    }

    private static DeletePropertyStats DeletePropertiesRecursive(ModelDoc2 model, bool deleteConfigurationProperties, HashSet<string> processed)
    {
        var stats = new DeletePropertyStats();
        if (model == null) return stats;

        var key = GetModelProcessKey(model);
        if (!processed.Add(key)) return stats;

        stats.Documents++;
        stats.Deleted += deleteConfigurationProperties
            ? DeleteAllConfigurationProperties(model)
            : DeleteManagerProperties(GetCustomPropertyManager(model, ""));

        if (model.GetType() != (int)swDocumentTypes_e.swDocASSEMBLY) return stats;

        try
        {
            var configuration = model.GetActiveConfiguration() as Configuration;
            var rootComponent = configuration?.GetRootComponent() as Component2;
            var children = GetComponentChildren(rootComponent);

            foreach (var child in EnumerateComponents(children))
            {
                var childModel = Safe(() => child.GetModelDoc() as ModelDoc2);
                if (childModel == null) continue;

                var childStats = DeletePropertiesRecursive(childModel, deleteConfigurationProperties, processed);
                stats.Documents += childStats.Documents;
                stats.Deleted += childStats.Deleted;
            }
        }
        catch (Exception ex)
        {
            LogIgnoredException("DeletePropertiesRecursive", ex);
        }

        return stats;
    }

    private static int DeleteAllConfigurationProperties(ModelDoc2 model)
    {
        var deleted = 0;
        var configurationNames = GetConfigurationNames(model).ToArray();
        if (configurationNames.Length == 0)
        {
            var activeConfiguration = GetActiveConfigurationName(model);
            if (!string.IsNullOrWhiteSpace(activeConfiguration))
                configurationNames = new[] { activeConfiguration };
        }

        foreach (var configurationName in configurationNames)
        {
            deleted += DeleteManagerProperties(GetCustomPropertyManager(model, configurationName));
        }

        return deleted;
    }

    private static int DeleteManagerProperties(CustomPropertyManager manager)
    {
        if (manager == null) return 0;

        var deleted = 0;
        foreach (var name in GetPropertyNames(manager).ToArray())
        {
            try
            {
                manager.Delete2(name);
                deleted++;
            }
            catch (Exception ex)
            {
                LogIgnoredException("DeleteManagerProperties", ex);
            }
        }

        return deleted;
    }

    private static IEnumerable<string> GetConfigurationNames(ModelDoc2 model)
    {
        var names = model.GetConfigurationNames();
        if (names is string[] stringNames)
            return stringNames.Where(name => !string.IsNullOrWhiteSpace(name));

        if (names is object[] objectNames)
            return objectNames.Select(item => item?.ToString()).Where(name => !string.IsNullOrWhiteSpace(name));

        return Enumerable.Empty<string>();
    }

    private static string GetModelProcessKey(ModelDoc2 model)
    {
        var path = Safe(() => model.GetPathName()) ?? "";
        if (!string.IsNullOrWhiteSpace(path)) return path;

        var title = Safe(() => model.GetTitle()) ?? "";
        return "unsaved:" + title + ":" + model.GetHashCode();
    }

    private sealed class PropertyTarget
    {
        public ModelDoc2 Model { get; set; }
        public string Title { get; set; }
        public string Path { get; set; }
        public string ConfigurationName { get; set; }
        public string Source { get; set; }
        public bool SelectedComponent { get; set; }
        public Dictionary<string, string> FileProperties { get; set; }
    }

    private sealed class DeletePropertyStats
    {
        public int Documents { get; set; }
        public int Deleted { get; set; }
    }

    // --- Get Bounding Box ---

    private object GetBoundingBox(Dictionary<string, object> args)
    {
        var model = GetActiveModel();
        var configName = GetArgString(args, "configuration");

        if (!string.IsNullOrWhiteSpace(configName))
        {
            try { model.ShowConfiguration2(configName); }
            catch (Exception ex) { LogIgnoredException("GetBoundingBox.ShowConfiguration", ex); }
        }

        var part = GetPartDoc(model);
        if (part == null) throw new InvalidOperationException("请在零件环境下获取包围盒");

        var box = part.GetPartBox(false) as double[];
        if (box == null || box.Length < 6) throw new InvalidOperationException("无法获取包围盒");

        return new
        {
            x1 = box[0], y1 = box[1], z1 = box[2],
            x2 = box[3], y2 = box[4], z2 = box[5],
            dx = box[3] - box[0],
            dy = box[4] - box[1],
            dz = box[5] - box[2]
        };
    }

    // --- Sync Coding Props ---

    private object SyncCodingProps()
    {
        var model = GetActiveModel();
        var configName = GetActiveConfigurationName(model);

        var path = Safe(() => model.GetPathName()) ?? "";
        var rawTitle = Safe(() => model.GetTitle()) ?? "";
        var title = !string.IsNullOrWhiteSpace(path)
            ? Path.GetFileNameWithoutExtension(path)
            : Path.GetFileNameWithoutExtension(rawTitle);
        if (string.IsNullOrWhiteSpace(title)) title = rawTitle;

        var materialCode = Safe(() => model.GetCustomInfoValue(configName, "物料编码"));
        var partNumber = Safe(() => model.GetCustomInfoValue(configName, "零件图号"));

        if (title != materialCode || title != partNumber)
        {
            var config = model.GetActiveConfiguration() as Configuration;
            var cusPropMgr = config?.CustomPropertyManager;
            if (cusPropMgr == null) throw new InvalidOperationException("无法获取当前配置属性管理器");

            cusPropMgr.Add3("物料编码", 30, title, 2);
            cusPropMgr.Add3("零件图号", 30, title, 2);
            cusPropMgr.Add3("文件名称", 30, title, 2);
            MarkDocDirty(model);
            return new { synced = true, title, materialCode, partNumber };
        }

        return new { synced = false, title, materialCode, partNumber, message = "已同步，无需更新" };
    }

    // --- Rename Component ---

    private object GetRenameTargetInfo()
    {
        var target = ResolveRenameTarget(preferSelectedComponent: true);
        return CreateRenameTargetInfo(target);
    }

    private object CheckNameConflict(Dictionary<string, object> args)
    {
        var target = ResolveRenameTarget(preferSelectedComponent: true);
        var destPath = BuildRenameDestinationPath(target, GetArgString(args, "newName"));
        var drawingPath = Path.ChangeExtension(destPath, ".SLDDRW");
        var existsFile = File.Exists(destPath);
        var existsDrawing = File.Exists(drawingPath);
        var existsOpen = TryGetOpenModelByPath(destPath) != null;

        return new
        {
            conflict = existsFile || existsOpen,
            existsFile,
            existsOpen,
            existsDrawing,
            path = destPath,
            drawingPath
        };
    }

    private object SaveAsNew(Dictionary<string, object> args)
    {
        var target = ResolveRenameTarget(preferSelectedComponent: true);
        var newBaseName = NormalizeNewBaseName(GetArgString(args, "newName"));
        var destPath = BuildRenameDestinationPath(target, newBaseName);
        var copyDrawing = GetArgBool(args, "copyDrawing", true);
        EnsureRenameDestinationAvailable(destPath, target.Path, copyDrawing);

        SaveModelCopy(target.Model, destPath);
        var drawing = CopyRelatedDrawing(target.Path, destPath, copyDrawing);

        var newModel = OpenModelForRename(destPath);
        ApplyRenameProperties(newModel, target.Component, args, newBaseName);
        SaveModel(newModel);

        return new
        {
            success = true,
            path = destPath,
            title = Safe(() => newModel.GetTitle()) ?? Path.GetFileName(destPath),
            selectedComponent = target.SelectedComponent,
            drawing
        };
    }

    private object RenameComponent(Dictionary<string, object> args)
    {
        var target = ResolveRenameTarget(preferSelectedComponent: true);
        var newBaseName = NormalizeNewBaseName(GetArgString(args, "newName"));
        var destPath = BuildRenameDestinationPath(target, newBaseName);
        EnsureRenameDestinationAvailable(destPath, target.Path, copyDrawing: false);

        var oldPath = target.Path;
        var renameError = RenameDocumentInSolidWorks(target, newBaseName);
        if (renameError != (int)swRenameDocumentError_e.swRenameDocumentError_None)
            throw new InvalidOperationException("SolidWorks 重命名失败，错误码: " + renameError);

        ApplyRenameProperties(target.Model, target.Component, args, newBaseName);
        SaveModel(target.Model);

        if (target.SelectedComponent)
        {
            target.ActiveModel.EditRebuild3();
            MarkDocDirty(target.ActiveModel);
            SaveModel(target.ActiveModel);
        }

        return new
        {
            success = true,
            oldName = target.BaseName,
            newName = newBaseName,
            oldPath,
            path = destPath,
            directRename = true,
            replaced = false
        };
    }

    private object SaveAsReplace(Dictionary<string, object> args)
    {
        var target = ResolveRenameTarget(preferSelectedComponent: true);
        if (!target.SelectedComponent || target.Component == null)
            throw new InvalidOperationException("请在装配体中选中一个组件");

        var assemblyDoc = GetAssemblyDoc(target.ActiveModel);
        if (assemblyDoc == null)
            throw new InvalidOperationException("无法获取装配体对象");

        var newBaseName = NormalizeNewBaseName(GetArgString(args, "newName"));
        var destPath = BuildRenameDestinationPath(target, newBaseName);
        var copyDrawing = GetArgBool(args, "copyDrawing", true);
        EnsureRenameDestinationAvailable(destPath, target.Path, copyDrawing);

        SaveModelCopy(target.Model, destPath);
        var drawing = CopyRelatedDrawing(target.Path, destPath, copyDrawing);

        var newModel = OpenModelForRename(destPath);
        ApplyRenameProperties(newModel, target.Component, args, newBaseName);
        SaveModel(newModel);
        var activateErrors = 0;
        Safe(() => _swApp.ActivateDoc3(Safe(() => target.ActiveModel.GetTitle()) ?? "", false, 0, ref activateErrors));

        target.ActiveModel.ClearSelection2(true);
        if (!Safe(() => target.Component.Select4(false, null, false)))
            throw new InvalidOperationException("无法重新选中待替换组件");

        var configurationName = Safe(() => target.Component.ReferencedConfiguration) ?? "";
        var replaced = assemblyDoc.ReplaceComponents2(destPath, configurationName, false, 0, true);
        if (!replaced)
            throw new InvalidOperationException("SolidWorks 未能替换组件引用");

        target.ActiveModel.EditRebuild3();
        MarkDocDirty(target.ActiveModel);

        return new
        {
            success = true,
            oldName = target.BaseName,
            newName = newBaseName,
            oldPath = target.Path,
            savedAsPath = destPath,
            replaced = true,
            drawing
        };
    }

    private int RenameDocumentInSolidWorks(RenameTarget target, string newBaseName)
    {
        if (target.SelectedComponent && target.Component != null)
        {
            target.ActiveModel.ClearSelection2(true);
            if (!Safe(() => target.Component.Select4(false, null, false)))
                throw new InvalidOperationException("无法选中待重命名组件");

            var assemblyExtension = target.ActiveModel.Extension;
            if (assemblyExtension == null)
                throw new InvalidOperationException("无法获取装配体扩展对象");

            return assemblyExtension.RenameDocument(newBaseName);
        }

        var extension = target.Model.Extension;
        if (extension == null)
            throw new InvalidOperationException("无法获取文档扩展对象");

        return extension.RenameDocument(newBaseName);
    }

    private static void ApplyRenameProperties(ModelDoc2 targetModel, Component2 component, Dictionary<string, object> args, string newName)
    {
        var propertyTarget = GetArgString(args, "propertyTarget", GetArgString(args, "source", "configuration"));
        if (GetArgBool(args, "customProperties")) propertyTarget = "custom";

        var configurationName = "";
        if (!string.Equals(propertyTarget, "custom", StringComparison.OrdinalIgnoreCase))
        {
            configurationName = component != null ? Safe(() => component.ReferencedConfiguration) ?? "" : "";
            if (string.IsNullOrWhiteSpace(configurationName))
                configurationName = GetActiveConfigurationName(targetModel);
        }

        var manager = GetCustomPropertyManager(targetModel, configurationName);
        if (manager == null) return;

        if (GetArgBool(args, "fileName"))
            manager.Add3("文件名称", 30, newName, 2);
        if (GetArgBool(args, "materialCode"))
            manager.Add3("物料编码", 30, newName, 2);
        if (GetArgBool(args, "partNumber"))
            manager.Add3("零件图号", 30, newName, 2);
        if (GetArgBool(args, "design"))
            manager.Add3("设计出图", 30, GetArgString(args, "designText"), 2);
        if (GetArgBool(args, "version"))
            manager.Add3("版本", 30, GetArgString(args, "versionText"), 2);

        if (args.TryGetValue("renameProperties", out var rawProperties))
            ApplyRenamePropertyList(manager, rawProperties);

        MarkDocDirty(targetModel);
    }

    private static void ApplyRenamePropertyList(CustomPropertyManager manager, object rawProperties)
    {
        var items = rawProperties as IEnumerable<object>;
        if (items == null) return;

        foreach (var item in items)
        {
            if (item is not Dictionary<string, object> dict) continue;
            var name = GetDictionaryString(dict, "name");
            if (string.IsNullOrWhiteSpace(name)) name = GetDictionaryString(dict, "Name");
            if (string.IsNullOrWhiteSpace(name)) continue;

            var value = GetDictionaryString(dict, "value");
            if (string.IsNullOrWhiteSpace(value)) value = GetDictionaryString(dict, "Value");
            try
            {
                manager.Add3(name.Trim(), 30, value ?? "", 2);
            }
            catch (Exception ex)
            {
                LogIgnoredException("ApplyRenamePropertyList", ex);
            }
        }
    }

    private static string GetDictionaryString(Dictionary<string, object> dict, string key)
    {
        return dict.TryGetValue(key, out var value) && value != null ? value.ToString() : "";
    }

    private RenameTarget ResolveRenameTarget(bool preferSelectedComponent)
    {
        var activeModel = GetActiveModel();
        var targetModel = activeModel;
        Component2 component = null;
        var selectedComponent = false;
        var componentName = "";

        if (preferSelectedComponent)
        {
            component = GetSelectedComponent(activeModel);
            var componentModel = component != null ? Safe(() => component.GetModelDoc() as ModelDoc2) : null;
            if (componentModel != null)
            {
                targetModel = componentModel;
                selectedComponent = true;
                componentName = Safe(() => component.Name2) ?? "";
            }
        }

        var path = Safe(() => targetModel.GetPathName()) ?? "";
        if (string.IsNullOrWhiteSpace(path))
            throw new InvalidOperationException(selectedComponent ? "选中组件尚未保存，无法重命名" : "当前文档尚未保存，无法重命名");

        var title = Safe(() => targetModel.GetTitle()) ?? "";
        var baseName = Path.GetFileNameWithoutExtension(path);
        if (string.IsNullOrWhiteSpace(baseName)) baseName = Path.GetFileNameWithoutExtension(title);

        return new RenameTarget
        {
            ActiveModel = activeModel,
            Model = targetModel,
            Component = component,
            SelectedComponent = selectedComponent,
            ComponentName = componentName,
            Title = title,
            Path = path,
            BaseName = baseName ?? "",
            Extension = Path.GetExtension(path).ToLowerInvariant(),
            DocumentType = Safe(() => targetModel.GetType())
        };
    }

    private static object CreateRenameTargetInfo(RenameTarget target)
    {
        return new
        {
            title = target.Title,
            path = target.Path,
            targetTitle = target.Title,
            targetPath = target.Path,
            baseName = target.BaseName,
            extension = target.Extension,
            type = target.DocumentType,
            selectedComponent = target.SelectedComponent,
            componentName = target.ComponentName
        };
    }

    private static string NormalizeNewBaseName(string newName)
    {
        if (string.IsNullOrWhiteSpace(newName))
            throw new InvalidOperationException("请输入新文件名");

        var trimmed = Path.GetFileNameWithoutExtension(newName.Trim());
        if (string.IsNullOrWhiteSpace(trimmed))
            throw new InvalidOperationException("请输入新文件名");

        if (trimmed.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
            throw new InvalidOperationException("文件名包含非法字符");

        return trimmed;
    }

    private static string BuildRenameDestinationPath(RenameTarget target, string newName)
    {
        var baseName = NormalizeNewBaseName(newName);
        var directory = Path.GetDirectoryName(target.Path);
        if (string.IsNullOrWhiteSpace(directory))
            throw new InvalidOperationException("无法确定目标文件夹");

        var extension = string.IsNullOrWhiteSpace(target.Extension)
            ? GetDocumentExtension(target.DocumentType)
            : target.Extension;
        if (string.IsNullOrWhiteSpace(extension))
            throw new InvalidOperationException("不支持的文档类型");

        return Path.Combine(directory, baseName + extension);
    }

    private static string GetDocumentExtension(int docType)
    {
        switch (docType)
        {
            case (int)swDocumentTypes_e.swDocPART: return ".sldprt";
            case (int)swDocumentTypes_e.swDocASSEMBLY: return ".sldasm";
            case (int)swDocumentTypes_e.swDocDRAWING: return ".slddrw";
            default: return "";
        }
    }

    private static void EnsureRenameDestinationAvailable(string destPath, string sourcePath, bool copyDrawing)
    {
        if (File.Exists(destPath))
            throw new InvalidOperationException("目标文件已存在: " + destPath);
    }
    private static void SaveModelCopy(ModelDoc2 model, string destPath)
    {
        var extension = model.Extension;
        if (extension == null)
            throw new InvalidOperationException("无法获取文档扩展对象");

        var errors = 0;
        var warnings = 0;
        var options = (int)swSaveAsOptions_e.swSaveAsOptions_Silent | (int)swSaveAsOptions_e.swSaveAsOptions_Copy;
        var ok = extension.SaveAs3(destPath, 0, options, null, null, ref errors, ref warnings);
        if (!ok || errors != 0)
            throw new InvalidOperationException("保存新文件失败，错误码: " + errors);
    }

    private ModelDoc2 OpenModelForRename(string path)
    {
        var existing = TryGetOpenModelByPath(path);
        if (existing != null) return existing;

        var docType = GetDocumentTypeFromPath(path);
        var errors = 0;
        var warnings = 0;
        var wasVisible = true;
        try
        {
            wasVisible = Safe(() => _swApp.GetDocumentVisible(docType));
            _swApp.DocumentVisible(false, docType);

            var model = _swApp.OpenDoc6(path, docType, (int)swOpenDocOptions_e.swOpenDocOptions_Silent, "", ref errors, ref warnings) as ModelDoc2;
            if (model == null || errors != 0)
                throw new InvalidOperationException("打开新文件失败，错误码: " + errors);

            return model;
        }
        finally
        {
            try { _swApp.DocumentVisible(wasVisible, docType); }
            catch (Exception ex) { LogIgnoredException("OpenModelForRename.DocumentVisible", ex); }
        }
    }

    private static void SaveModel(ModelDoc2 model)
    {
        var errors = 0;
        var warnings = 0;
        var ok = model.Save3((int)swSaveAsOptions_e.swSaveAsOptions_Silent, ref errors, ref warnings);
        if (!ok || errors != 0)
            throw new InvalidOperationException("保存属性失败，错误码: " + errors);
    }

    private DrawingCopyResult CopyRelatedDrawing(string sourceModelPath, string destModelPath, bool enabled)
    {
        if (!enabled)
            return DrawingCopyResult.Skipped("copy drawing disabled");
        if (string.IsNullOrWhiteSpace(sourceModelPath) || string.IsNullOrWhiteSpace(destModelPath))
            return DrawingCopyResult.Skipped("model path is empty");

        var sourceDrawing = Path.ChangeExtension(sourceModelPath, ".SLDDRW");
        if (!File.Exists(sourceDrawing))
            return DrawingCopyResult.Skipped("source drawing does not exist");

        var destDrawing = Path.ChangeExtension(destModelPath, ".SLDDRW");
        if (File.Exists(destDrawing))
            return DrawingCopyResult.Failed(sourceDrawing, destDrawing, "目标工程图已存在，已继续改名但未复制工程图: " + destDrawing);

        try
        {
            var sourceReference = EnsureSourceDrawingReferencesCurrentModel(sourceDrawing, sourceModelPath);
            if (sourceReference == null)
                return DrawingCopyResult.Failed(sourceDrawing, destDrawing, "原工程图未关联当前模型，已继续改名但未复制工程图: " + sourceDrawing);

            File.Copy(sourceDrawing, destDrawing);
            AddinLog.Write("CopyRelatedDrawing copied: " + sourceDrawing + " -> " + destDrawing);

            var replaced = _swApp.ReplaceReferencedDocument(destDrawing, sourceReference, destModelPath);
            AddinLog.Write("CopyRelatedDrawing ReplaceReferencedDocument result=" + replaced + ", drawing=" + destDrawing + ", old=" + sourceReference + ", new=" + destModelPath);
            if (replaced)
                return DrawingCopyResult.Copied(sourceDrawing, destDrawing, sourceReference, sourceReference);

            try { File.Delete(destDrawing); }
            catch (Exception ex) { LogIgnoredException("CopyRelatedDrawing.DeleteFailedDrawing", ex); }
            return DrawingCopyResult.Failed(sourceDrawing, destDrawing, "工程图引用替换失败，已继续改名但未保留新工程图: " + destDrawing);
        }
        catch (Exception ex)
        {
            AddinLog.Write("CopyRelatedDrawing failed: " + ex);
            try
            {
                if (File.Exists(destDrawing)) File.Delete(destDrawing);
            }
            catch (Exception deleteEx)
            {
                LogIgnoredException("CopyRelatedDrawing.DeleteFailedDrawing", deleteEx);
            }
            return DrawingCopyResult.Failed(sourceDrawing, destDrawing, "工程图处理失败，已继续改名: " + ex.Message);
        }
    }

    private string EnsureSourceDrawingReferencesCurrentModel(string sourceDrawing, string sourceModelPath)
    {
        var sourceFullPath = Path.GetFullPath(sourceModelPath);
        var dependencies = GetDrawingModelDependencies(sourceDrawing).ToArray();
        var currentReference = dependencies.FirstOrDefault(item => IsSameReferencePath(item, sourceFullPath) || IsSameFileIfExists(item, sourceFullPath));
        if (!string.IsNullOrWhiteSpace(currentReference))
        {
            AddinLog.Write("CopyRelatedDrawing source reference already ok: drawing=" + sourceDrawing + ", model=" + currentReference);
            return currentReference;
        }

        foreach (var oldReference in GetSourceDrawingRepairCandidates(dependencies, sourceModelPath))
        {
            var repaired = _swApp.ReplaceReferencedDocument(sourceDrawing, oldReference, sourceModelPath);
            AddinLog.Write("CopyRelatedDrawing repair source result=" + repaired + ", drawing=" + sourceDrawing + ", old=" + oldReference + ", new=" + sourceModelPath);
            if (repaired) return sourceModelPath;
        }

        return null;
    }

    private IEnumerable<string> GetDrawingModelDependencies(string drawingPath)
    {
        foreach (var dependency in ToStringArray(_swApp.GetDocumentDependencies2(drawingPath, false, true, false)))
        {
            if (string.IsNullOrWhiteSpace(dependency)) continue;
            if (!Path.IsPathRooted(dependency)) continue;
            if (!IsSolidWorksModelPath(dependency)) continue;
            yield return dependency;
        }
    }

    private IEnumerable<string> GetSourceDrawingRepairCandidates(IEnumerable<string> dependencies, string sourceModelPath)
    {
        var candidates = new List<string>();
        var dependencyList = dependencies.ToArray();
        var sourceExt = Path.GetExtension(sourceModelPath);
        var sourceName = Path.GetFileName(sourceModelPath);
        var sourceBaseName = Path.GetFileNameWithoutExtension(sourceModelPath);
        var sameExtensionDependencies = dependencyList
            .Where(item => string.Equals(Path.GetExtension(item), sourceExt, StringComparison.OrdinalIgnoreCase))
            .ToArray();

        foreach (var dependency in sameExtensionDependencies)
        {
            var depName = Path.GetFileName(dependency);
            var depBaseName = Path.GetFileNameWithoutExtension(dependency);
            if (string.Equals(depName, sourceName, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(depBaseName, sourceBaseName, StringComparison.OrdinalIgnoreCase) ||
                IsSameFileIfExists(dependency, sourceModelPath) ||
                sameExtensionDependencies.Length == 1)
            {
                AddReferenceCandidate(candidates, dependency);
            }
        }

        foreach (var candidate in candidates)
            yield return candidate;
    }

    private static void AddReferenceCandidate(List<string> candidates, string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return;
        if (candidates.Any(item => string.Equals(item, path, StringComparison.OrdinalIgnoreCase))) return;
        candidates.Add(path);
    }

    private static bool IsSolidWorksModelPath(string path)
    {
        var ext = Path.GetExtension(path);
        return string.Equals(ext, ".SLDPRT", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(ext, ".SLDASM", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsSameFileIfExists(string left, string right)
    {
        try
        {
            if (!File.Exists(left) || !File.Exists(right)) return false;
            return string.Equals(Path.GetFullPath(left), Path.GetFullPath(right), StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    private static bool IsSameReferencePath(string left, string right)
    {
        try
        {
            return string.Equals(Path.GetFullPath(left), Path.GetFullPath(right), StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return string.Equals(left, right, StringComparison.OrdinalIgnoreCase);
        }
    }

    private sealed class DrawingCopyResult
    {
        public bool attempted { get; set; }
        public bool success { get; set; }
        public bool skipped { get; set; }
        public string sourcePath { get; set; }
        public string path { get; set; }
        public string replacedReference { get; set; }
        public string sourceReference { get; set; }
        public string error { get; set; }

        public static DrawingCopyResult Skipped(string reason)
        {
            return new DrawingCopyResult { attempted = false, success = false, skipped = true, error = reason };
        }

        public static DrawingCopyResult Copied(string sourcePath, string path, string replacedReference = null, string sourceReference = null)
        {
            return new DrawingCopyResult { attempted = true, success = true, skipped = false, sourcePath = sourcePath, path = path, replacedReference = replacedReference, sourceReference = sourceReference };
        }

        public static DrawingCopyResult Failed(string sourcePath, string path, string error)
        {
            return new DrawingCopyResult { attempted = true, success = false, skipped = false, sourcePath = sourcePath, path = path, error = error };
        }
    }
    private sealed class RenameTarget
    {
        public ModelDoc2 ActiveModel { get; set; }
        public ModelDoc2 Model { get; set; }
        public Component2 Component { get; set; }
        public bool SelectedComponent { get; set; }
        public string ComponentName { get; set; }
        public string Title { get; set; }
        public string Path { get; set; }
        public string BaseName { get; set; }
        public string Extension { get; set; }
        public int DocumentType { get; set; }
    }

    // --- Coding Cleanup ---

    private object CodingCleanup(Dictionary<string, object> args)
    {
        var model = GetActiveModel();
        var nameFilter = GetArgString(args, "nameFilter");
        var processAsm = GetArgBool(args, "processAsm", true);
        var processPart = GetArgBool(args, "processPart", true);
        var excludeVirtual = GetArgBool(args, "excludeVirtual", true);
        var excludeStandard = GetArgBool(args, "excludeStandard", true);
        var excludePurchased = GetArgBool(args, "excludePurchased", true);

        if (model.GetType() != (int)swDocumentTypes_e.swDocASSEMBLY)
            throw new InvalidOperationException("请在装配体环境下使用");

        var configuration = model.GetActiveConfiguration() as Configuration;
        if (configuration == null)
            throw new InvalidOperationException("无法获取当前装配体配置");

        var rootComponent = configuration.GetRootComponent() as Component2;
        if (rootComponent == null)
            throw new InvalidOperationException("无法获取装配体根组件");

        var comps = GetComponentChildren(rootComponent);

        var results = new List<object>();
        ProcessCodingCleanup(comps, nameFilter, processAsm, processPart,
            excludeVirtual, excludeStandard, excludePurchased, results);

        model.EditRebuild3();
        return new { processed = results.Count, results };
    }

    private static void ProcessCodingCleanup(object[] comps, string nameFilter, bool processAsm, bool processPart,
        bool excludeVirtual, bool excludeStandard, bool excludePurchased, List<object> results)
    {
        foreach (var child in EnumerateComponents(comps))
        {
            try
            {
                var childModel = child.GetModelDoc() as ModelDoc2;
                if (childModel == null) continue;

                var childType = childModel.GetType();
                var childName = Safe(() => child.Name2);
                var isAssembly = childType == (int)swDocumentTypes_e.swDocASSEMBLY;
                var isPart = childType == (int)swDocumentTypes_e.swDocPART;

                var configName = Safe(() => child.ReferencedConfiguration);
                if (string.IsNullOrWhiteSpace(configName)) configName = "";

                var isVirtual = Safe(() => child.IsVirtual);
                var excludedByVirtual = excludeVirtual && isVirtual;
                var excludedByFlag = IsExcludedCodingCleanupComponent(childModel, configName, excludeStandard, excludePurchased);
                var matchesNameFilter = string.IsNullOrWhiteSpace(nameFilter) ||
                                        string.IsNullOrWhiteSpace(childName) ||
                                        childName.IndexOf(nameFilter, StringComparison.OrdinalIgnoreCase) >= 0;
                var shouldProcessCurrent = matchesNameFilter &&
                                           !excludedByVirtual &&
                                           !excludedByFlag &&
                                           ((isPart && processPart) || (isAssembly && processAsm));

                if (shouldProcessCurrent)
                {
                    var title = Safe(() => childModel.GetTitle()) ?? "";
                    var dotIdx = title.IndexOf(".", StringComparison.Ordinal);
                    if (dotIdx > 0) title = title.Substring(0, dotIdx);

                    if (!string.IsNullOrWhiteSpace(title))
                    {
                        var materialCode = Safe(() => childModel.GetCustomInfoValue(configName, "物料编码"));
                        var partNumber = Safe(() => childModel.GetCustomInfoValue(configName, "零件图号"));

                        if (title != materialCode || title != partNumber)
                        {
                            var cusPropMgr = GetCustomPropertyManager(childModel, configName);
                            if (cusPropMgr != null)
                            {
                                cusPropMgr.Add3("物料编码", 30, title, 2);
                                cusPropMgr.Add3("零件图号", 30, title, 2);
                                cusPropMgr.Add3("文件名称", 30, title, 2);
                                MarkDocDirty(childModel);
                                results.Add(new { name = childName, title, materialCode, partNumber, action = "synced" });
                            }
                        }
                    }
                }

                if (isAssembly && !excludedByVirtual && !excludedByFlag)
                {
                    var subComps = GetComponentChildrenForConfiguration(childModel, configName);
                    ProcessCodingCleanup(subComps, nameFilter, processAsm, processPart,
                        excludeVirtual, excludeStandard, excludePurchased, results);
                }
            }
            catch (Exception ex)
            {
                LogIgnoredException("ProcessCodingCleanup", ex);
            }
        }
    }

    private static object[] GetComponentChildrenForConfiguration(ModelDoc2 model, string configName)
    {
        if (model == null) return Array.Empty<object>();

        if (!string.IsNullOrWhiteSpace(configName))
        {
            var activeConfigName = GetActiveConfigurationName(model);
            try
            {
                if (!string.Equals(activeConfigName, configName, StringComparison.OrdinalIgnoreCase))
                    Safe(() => model.ShowConfiguration2(configName));

                return GetActiveConfigurationChildren(model);
            }
            finally
            {
                if (!string.IsNullOrWhiteSpace(activeConfigName) &&
                    !string.Equals(activeConfigName, configName, StringComparison.OrdinalIgnoreCase))
                {
                    Safe(() => model.ShowConfiguration2(activeConfigName));
                }
            }
        }

        return GetActiveConfigurationChildren(model);
    }

    private static object[] GetActiveConfigurationChildren(ModelDoc2 model)
    {
        var configuration = Safe(() => model.GetActiveConfiguration() as Configuration);
        var rootComponent = Safe(() => configuration?.GetRootComponent() as Component2);
        return GetComponentChildren(rootComponent);
    }

    private static bool IsExcludedCodingCleanupComponent(ModelDoc2 model, string configName, bool excludeStandard, bool excludePurchased)
    {
        if (!excludeStandard && !excludePurchased) return false;

        return (excludeStandard && HasPositiveCodingCleanupFlag(model, configName, StandardComponentFlagProperties)) ||
               (excludePurchased && HasPositiveCodingCleanupFlag(model, configName, PurchasedComponentFlagProperties));
    }

    private static string GetCodingCleanupPropertyValue(ModelDoc2 model, string configName, string propertyName)
    {
        var value = Safe(() => model.GetCustomInfoValue(configName, propertyName));
        if (!string.IsNullOrWhiteSpace(value) || string.IsNullOrEmpty(configName)) return value;

        return Safe(() => model.GetCustomInfoValue("", propertyName));
    }

    private static bool HasPositiveCodingCleanupFlag(ModelDoc2 model, string configName, IEnumerable<string> propertyNames)
    {
        foreach (var propertyName in propertyNames)
        {
            var value = GetCodingCleanupPropertyValue(model, configName, propertyName);
            if (TryConvertCodingCleanupFlag(value, out var enabled) && enabled) return true;
        }

        return false;
    }

    private static bool TryConvertCodingCleanupFlag(string value, out bool enabled)
    {
        enabled = false;
        if (string.IsNullOrWhiteSpace(value)) return false;

        var text = value.Trim();
        if (bool.TryParse(text, out enabled)) return true;

        switch (text.ToUpperInvariant())
        {
            case "1":
            case "是":
            case "真":
            case "YES":
            case "Y":
                enabled = true;
                return true;
            case "0":
            case "否":
            case "假":
            case "NO":
            case "N":
                enabled = false;
                return true;
            default:
                return false;
        }
    }
}
