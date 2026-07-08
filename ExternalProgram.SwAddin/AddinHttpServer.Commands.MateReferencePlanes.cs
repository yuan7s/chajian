using System;
using System.Collections.Generic;
using System.Linq;
using SldWorks;
using SwConst;

namespace ExternalProgram.SwAddin;

// ReSharper disable CatchAllClause
#pragma warning disable CA1031 // SolidWorks COM APIs throw broad COM/runtime exceptions; command handlers isolate and log recoverable failures.

internal sealed partial class AddinHttpServer
{
    /// <summary>
    /// 基准面配合：为选中的组件添加与装配体默认基准面的重合配合。
    /// 自动枚举装配体和组件特征树中前 3 个 RefPlane（前视/上视/右视），按索引一一对应。
    /// 中英文名称自动适配——装配体可能用"前视基准面"，组件可能用"Front Plane"。
    /// </summary>
    private object MateReferencePlanes()
    {
        var model = GetActiveModel();
        if (model.GetType() != (int)swDocumentTypes_e.swDocASSEMBLY)
            throw CommandFailure("assembly_required");

        var assemblyDoc = GetAssemblyDoc(model);
        if (assemblyDoc == null) throw CommandFailure("assembly_doc_unavailable");

        var selectedComponents = GetSelectedComponentsForReferencePlaneMate(model);
        if (selectedComponents.Count == 0)
            throw CommandFailure("component_selection_required");

        var asmPlaneNames = GetFirstNFeatureNamesByType(model, "RefPlane", 3);
        AddinLog.Write($"MateReferencePlanes: assembly first-3 planes={string.Join(",", asmPlaneNames)}");

        var totalMates = 0;
        var failed = 0;
        var details = new List<object>();
        var failureMessages = new List<string>();

        foreach (var component in selectedComponents)
        {
            var componentName = Safe(() => component.Name2) ?? "";
            var componentMates = 0;
            var failures = new List<string>();

            var compModel = Safe(() => component.GetModelDoc() as ModelDoc2);
            var compPlaneNames = compModel != null
                ? GetFirstNFeatureNamesByType(compModel, "RefPlane", 3)
                : new List<string>();
            AddinLog.Write($"MateReferencePlanes: component {componentName} first-3 planes={string.Join(",", compPlaneNames)}");

            // Mate by index: asm[0]×comp[0] (前视), asm[1]×comp[1] (上视), asm[2]×comp[2] (右视)
            var pairCount = Math.Min(asmPlaneNames.Count, compPlaneNames.Count);
            for (var i = 0; i < pairCount; i++)
            {
                if (TryMatePlanePair(model, assemblyDoc, component,
                        asmPlaneNames[i], compPlaneNames[i], out var errorMessage))
                {
                    totalMates++;
                    componentMates++;
                }
                else
                {
                    failed++;
                    failures.Add(errorMessage);
                    failureMessages.Add(componentName + ": " + errorMessage);
                }
            }

            details.Add(new { component = componentName, mates = componentMates, failures });
        }

        if (totalMates == 0)
        {
            var failureSummary = string.Join("; ",
                failureMessages.Take(5).Select(f => f.Length > 120 ? f.Substring(0, 117) + "..." : f));
            if (failureMessages.Count > 5)
                failureSummary += "; ... (+" + (failureMessages.Count - 5) + " more)";
            AddinLog.Write($"ReferencePlaneMate all failed: {failureSummary}");
            throw CommandFailure("reference_plane_mate_failed", "error", failureSummary);
        }

        Safe(() => model.EditRebuild3());
        try { MarkDocDirty(model); } catch (Exception ex) { LogIgnoredException("MateReferencePlanes.MarkDocDirty", ex); }
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

    /// <summary>
    /// Return the first N feature names of the given type (e.g. "RefPlane") in tree order.
    /// Default planes always appear first, so N=3 gives 前视/上视/右视 (or their localized names).
    /// </summary>
    private static List<string> GetFirstNFeatureNamesByType(ModelDoc2 model, string typeName, int n)
    {
        var names = new List<string>();
        var feat = Safe(() => model.FirstFeature() as Feature);
        while (feat != null && names.Count < n)
        {
            var current = feat;
            if (string.Equals(Safe(current.GetTypeName2), typeName, StringComparison.OrdinalIgnoreCase))
            {
                var name = Safe(() => current.Name);
                if (!string.IsNullOrWhiteSpace(name))
                    names.Add(name);
            }
            feat = Safe(() => current.GetNextFeature() as Feature);
        }
        return names;
    }

    private static bool TryMatePlanePair(
        ModelDoc2 model,
        AssemblyDoc assemblyDoc,
        Component2 component,
        string asmPlaneName,
        string compPlaneName,
        out string errorMessage)
    {
        errorMessage = "";

        var componentSelectionName = GetComponentSelectionName(component);
        if (string.IsNullOrWhiteSpace(componentSelectionName))
        {
            errorMessage = "component_path_unavailable";
            return false;
        }

        var componentPlanePath = compPlaneName + "@" + componentSelectionName;
        try
        {
            model.ClearSelection2(true);
            var asmSelected = model.Extension.SelectByID2(asmPlaneName, "PLANE", 0, 0, 0, false, 1, null, 0);
            var compSelected = model.Extension.SelectByID2(componentPlanePath, "PLANE", 0, 0, 0, true, 1, null, 0);
            if (!asmSelected || !compSelected)
            {
                errorMessage = "select:" + asmPlaneName + "/" + compPlaneName;
                return false;
            }

            var mate = assemblyDoc.AddMate4(
                (int)swMateType_e.swMateCOINCIDENT,
                (int)swMateAlign_e.swMateAlignALIGNED,
                false, 0, 0, 0, 0, 0, 0, 0, 0,
                false, false,
                out var errorStatus);

            if (mate != null && errorStatus == 0) return true;

            // errorStatus=1 typically means the component is already constrained in this direction.
            // If a mate object was still returned, treat as non-critical success.
            if (mate != null && errorStatus != 0)
            {
                AddinLog.Write($"TryMatePlanePair: mate created with non-zero status={errorStatus} for {asmPlaneName}/{compPlaneName}");
                return true;
            }

            errorMessage = "mate:" + asmPlaneName + "/" + compPlaneName + ":" + errorStatus;
            return false;
        }
        catch (Exception ex)
        {
            errorMessage = "exception:" + asmPlaneName + "/" + compPlaneName;
            LogIgnoredException("TryMatePlanePair", ex);
            return false;
        }
        finally
        {
            try { model.ClearSelection2(true); }
            catch (Exception ex) { LogIgnoredException("TryMatePlanePair.ClearSelection", ex); }
        }
    }

    /// <summary>
    /// Translate SolidWorks AddMate4 errorStatus to a human-readable Chinese message.
    /// Based on swMateError_e: 0=ok, 1=冲突/不可解, 2=冗余, 3=过定义, 4=实体缺失, 5=实体无效.
    /// </summary>
    private static string FormatMateError(int errorStatus)
    {
        return errorStatus switch
        {
            0 => "ok",
            1 => "配合冲突或无法求解",
            2 => "冗余配合",
            3 => "过定义",
            4 => "配合实体不存在",
            5 => "配合实体无效",
            _ => "错误码" + errorStatus
        };
    }

    private object SelectComponent(Dictionary<string, object> args)
    {
        var model = GetActiveModel();
        if (model.GetType() != (int)swDocumentTypes_e.swDocASSEMBLY)
            throw CommandFailure("assembly_required");

        var name = GetArgString(args, "name");
        if (string.IsNullOrWhiteSpace(name))
            throw CommandFailure("argument_required", "name", "name");

        var component = FindComponentByName(model, name);
        if (component == null)
            throw CommandFailure("component_not_found", "name", name);

        model.ClearSelection2(true);
        var ok = Safe(() => component.Select4(false, null, false));
        if (!ok)
            ok = Safe(() => component.Select2(false, 0));
        if (!ok)
            throw CommandFailure("component_select_failed", "name", name);

        return new
        {
            selected = true,
            name = Safe(() => component.Name2) ?? "",
            path = Safe(component.GetPathName) ?? ""
        };
    }
}
