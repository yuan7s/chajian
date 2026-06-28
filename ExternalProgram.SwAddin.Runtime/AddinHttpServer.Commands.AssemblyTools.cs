using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using SldWorks;
using SolidWorks.Interop.swdocumentmgr;
using SwConst;

namespace ExternalProgram.SwAddin;

// ReSharper disable CatchAllClause
#pragma warning disable CA1031 // SolidWorks COM APIs throw broad COM/runtime exceptions; command handlers isolate and log recoverable failures.

internal sealed partial class AddinHttpServer
{
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

        var totalMates = 0;
        var failed = 0;
        var details = new List<object>();

        foreach (var component in selectedComponents)
        {
            var componentName = Safe(() => component.Name2) ?? "";
            var componentMates = 0;
            var failures = new List<string>();

            foreach (var planeNames in ReferencePlaneMateNameGroups)
                if (TryAddReferencePlaneMate(model, assemblyDoc, component, planeNames, out var errorMessage))
                {
                    totalMates++;
                    componentMates++;
                }
                else
                {
                    failed++;
                    failures.Add(errorMessage);
                }

            details.Add(new { component = componentName, mates = componentMates, failures });
        }

        if (totalMates == 0)
            throw CommandFailure("reference_plane_mate_failed");

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
        out string errorMessage)
    {
        errorMessage = "";

        var componentSelectionName = GetComponentSelectionName(component);
        if (string.IsNullOrWhiteSpace(componentSelectionName))
        {
            errorMessage = "component_selection_path_unavailable";
            return false;
        }

        foreach (var planeName in planeNames)
        {
            var componentPlaneName = planeName + "@" + componentSelectionName;
            try
            {
                model.ClearSelection2(true);
                var assemblyPlaneSelected = model.Extension.SelectByID2(planeName, "PLANE", 0, 0, 0, false, 1, null, 0);
                var componentPlaneSelected =
                    model.Extension.SelectByID2(componentPlaneName, "PLANE", 0, 0, 0, true, 1, null, 0);
                if (!assemblyPlaneSelected || !componentPlaneSelected)
                {
                    errorMessage = "reference_plane_select_failed:" + planeName;
                    continue;
                }

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
                    out var errorStatus);

                if (mate != null && errorStatus == 0) return true;

                errorMessage = "add_mate_failed:" + planeName + ":" + errorStatus;
            }
            catch (Exception ex)
            {
                errorMessage = "add_mate_exception:" + planeName;
                LogIgnoredException("TryAddReferencePlaneMate", ex);
            }
            finally
            {
                try
                {
                    model.ClearSelection2(true);
                }
                catch (Exception ex)
                {
                    LogIgnoredException("TryAddReferencePlaneMate.ClearSelection", ex);
                }
            }
        }

        return false;
    }

    private static string GetComponentSelectionName(Component2 component)
    {
        var selectionName = Safe(component.GetSelectByIDString);
        if (!string.IsNullOrWhiteSpace(selectionName)) return selectionName;

        return Safe(() => component.Name2);
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
        try
        {
            model?.SetSaveFlag();
        }
        catch (Exception ex)
        {
            LogIgnoredException("MarkDocDirty", ex);
        }
    }

    private object DeleteErrorMates()
    {
        var model = GetActiveModel();
        if (model.GetType() != (int)swDocumentTypes_e.swDocASSEMBLY)
            throw CommandFailure("assembly_required");

        var mateGroups = 0;
        var selected = 0;
        var feature = Safe(() => model.FirstFeature() as Feature);
        while (feature != null)
        {
            var currentFeature = feature;
            var typeName = Safe(currentFeature.GetTypeName2) ?? "";
            if (string.Equals(typeName, "MateGroup", StringComparison.OrdinalIgnoreCase))
            {
                mateGroups++;
                model.ClearSelection2(true);

                var groupSelected = 0;
                var subFeature = Safe(() => currentFeature.GetFirstSubFeature() as Feature);
                while (subFeature != null)
                {
                    var currentSubFeature = subFeature;
                    var errorCode = GetFeatureErrorCode(currentSubFeature, out var isWarning);
                    if (errorCode != 0 && !isWarning)
                        if (Safe(() => currentSubFeature.Select2(true, 0)))
                        {
                            groupSelected++;
                            selected++;
                        }

                    subFeature = Safe(() => currentSubFeature.GetNextSubFeature() as Feature);
                }

                if (groupSelected > 0)
                    model.EditDelete();
            }

            feature = Safe(() => currentFeature.GetNextFeature() as Feature);
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
            throw CommandFailure("macro_file_required");
        if (!File.Exists(path))
            throw CommandFailure("file_not_found", "path", path);
        if (!string.Equals(Path.GetExtension(path), ".swp", StringComparison.OrdinalIgnoreCase))
            throw CommandFailure("unsupported_macro_file", "path", path);

        var ok = _swApp.RunMacro2(path, "", "main", 0, out var error);
        if (!ok || error != 0)
            throw CommandFailure("macro_run_failed", "error", error);

        CheckAndBroadcastDocChange();
        return new { done = true, path };
    }

    // --- Hide Config Names (FeatureManager) ---

    private object HideConfigNames()
    {
        var model = GetActiveModel();
        if (model.GetType() != (int)swDocumentTypes_e.swDocASSEMBLY)
            throw CommandFailure("assembly_required");

        var featMgr = model.FeatureManager;
        if (featMgr == null) throw CommandFailure("feature_manager_unavailable");

        ConfigureFeatureManagerDisplay(featMgr);

        RecursiveHideConfigNames(_swApp, model);

        return new { done = true };
    }

    private static void RecursiveHideConfigNames(SldWorks.SldWorks swApp, ModelDoc2 asmDoc)
    {
        var configuration = asmDoc?.GetActiveConfiguration() as Configuration;
        var rootComponent = configuration?.GetRootComponent() as Component2;

        foreach (var child in EnumerateComponents(GetComponentChildren(rootComponent)))
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
                        var fopen = OpenDoc6WithDialogHandling(
                            swApp,
                            childPath,
                            (int)swDocumentTypes_e.swDocASSEMBLY,
                            (int)swOpenDocOptions_e.swOpenDocOptions_Silent,
                            "",
                            ref longstatus,
                            ref longWarnings);
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
            throw CommandFailure("assembly_required");

        var components = new List<object>();
        var configuration = model.GetActiveConfiguration() as Configuration;
        var rootComponent = configuration?.GetRootComponent() as Component2;

        foreach (var child in EnumerateComponents(GetComponentChildren(rootComponent)))
        {
            var childModel = child.GetModelDoc() as ModelDoc2;
            var info = new
            {
                name = Safe(() => child.Name2),
                path = Safe(child.GetPathName),
                referencedPath = childModel != null ? Safe(childModel.GetPathName) : null,
                type = childModel != null ? Safe(childModel.GetType) : -1,
                isSuppressed = Safe(child.IsSuppressed),
                isEnvelope = Safe(child.IsEnvelope)
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
            throw CommandFailure("assembly_required");

        var assemblyDoc = GetAssemblyDoc(model);
        if (assemblyDoc == null) throw CommandFailure("assembly_doc_unavailable");
        var options = SortOptions.FromArgs(args);

        var vFeats = GetFeatureArray(model);

        // Find start/end indices
        var startIdx = 0;
        var endIdx = vFeats.Length - 1;
        for (var i = 0; i < vFeats.Length; i++)
        {
            var feature = vFeats[i] as Feature;
            if (feature == null) continue;

            var t = Safe(feature.GetTypeName2) ?? "";
            if (t == "OriginProfileFeature") startIdx = i + 1;
            if (t == "MateGroup")
            {
                endIdx = i - 1;
                break;
            }
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

            var featType = Safe(feat.GetTypeName2) ?? "";
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
                    if (feat.GetSpecificFeature2() is Component2 comp)
                        isSupOrEnv = comp.IsSuppressed() || comp.IsEnvelope();
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

        var allSorted =
            BuildSortedComponentOrder(sortedAsmComps, sortedPartComps, sortedSupAsm, sortedSupPart, options);

        if (allSorted.Count > 0)
        {
            var lastFolder = folders.Count > 0 ? folders[folders.Count - 1] : null;
            if (lastFolder != null)
                assemblyDoc.ReorderComponents(allSorted[0], lastFolder,
                    (int)swReorderComponentsWhere_e.swReorderComponents_After);
            for (var i = 1; i < allSorted.Count; i++)
                assemblyDoc.ReorderComponents(allSorted[i], allSorted[i - 1], 1);
        }

        var foldersSorted = 0;
        if (options.SortFolders)
            for (var i = 0; i < folders.Count; i++)
            {
                var folderFeats = i < folderComponents.Count ? folderComponents[i] : null;
                if (SortComponentsInFolder(folders[i], folderFeats, assemblyDoc, options))
                    foldersSorted++;
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
                if (comp.GetModelDoc() is ModelDoc2 childModel && childModel.GetType() == targetType)
                    result.Add(comp);
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

    private static void AppendTypeGroups(List<Component2> target, List<Component2> asmComps, List<Component2> partComps,
        SortOptions options)
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
            var path = Safe(component.GetPathName) ?? "";
            var fileName = Path.GetFileNameWithoutExtension(path);
            if (!string.IsNullOrWhiteSpace(fileName)) return fileName;
        }

        return Safe(() => component.Name2) ?? "";
    }

    private static bool SortComponentsInFolder(Feature folder, List<Feature> folderFeats, AssemblyDoc assemblyDoc,
        SortOptions options)
    {
        var partComps = new List<Component2>();
        var asmComps = new List<Component2>();
        var supPartComps = new List<Component2>();
        var supAsmComps = new List<Component2>();

        var feats = folderFeats is { Count: > 0 }
            ? folderFeats
            : GetFolderReferenceFeaturesFromSubFeatures(folder);

        foreach (var currentSubFeat in feats)
            if ((Safe(currentSubFeat.GetTypeName2) ?? "") == "Reference")
            {
                var comp = Safe(() => currentSubFeat.GetSpecificFeature2() as Component2);
                if (comp != null)
                {
                    var isSupEnv = Safe(comp.IsSuppressed) || Safe(comp.IsEnvelope);
                    var childModel = Safe(() => comp.GetModelDoc() as ModelDoc2);
                    if (childModel is { } && childModel.GetType() == (int)swDocumentTypes_e.swDocASSEMBLY)
                    {
                        if (isSupEnv) supAsmComps.Add(comp);
                        else asmComps.Add(comp);
                    }
                    else if (childModel is { } && childModel.GetType() == (int)swDocumentTypes_e.swDocPART)
                    {
                        if (isSupEnv) supPartComps.Add(comp);
                        else partComps.Add(comp);
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
            if ((Safe(currentSubFeat.GetTypeName2) ?? "") == "Reference")
                result.Add(currentSubFeat);

            subFeat = Safe(() => currentSubFeat.GetNextSubFeature() as Feature);
        }

        return result;
    }

    private static void RecursiveSortSubAssemblies(List<Feature> feats, HashSet<string> processed, SortOptions options)
    {
        foreach (var feat in feats)
        {
            if ((Safe(feat.GetTypeName2) ?? "") != "Reference") continue;
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
            foreach (var item in vFeats)
            {
                if (item is Feature feature && (Safe(feature.GetTypeName2) ?? "") == "Reference")
                    refFeats.Add(feature);
            }

            var asmComps = CollectAndSort(refFeats, true, options);
            var partComps = CollectAndSort(refFeats, false, options);
            var all = BuildSortedComponentOrder(asmComps, partComps, new List<Component2>(), new List<Component2>(),
                options);

            if (all.Count > 1)
            {
                var asmDoc = GetAssemblyDoc(asmModel);
                if (asmDoc != null)
                    for (var i = 1; i < all.Count; i++)
                        asmDoc.ReorderComponents(all[i], all[i - 1], 1);
            }

            RecursiveSortSubAssemblies(refFeats, processed, options);
        }
        catch (Exception ex)
        {
            LogIgnoredException("SortSubAsmByFeatures", ex);
        }
    }

    // --- Read Properties ---

}
