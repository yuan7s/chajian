using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SldWorks;
using SwConst;

namespace ExternalProgram.SwAddin;

// ReSharper disable CatchAllClause
#pragma warning disable CA1031 // SolidWorks COM APIs throw broad COM/runtime exceptions; command handlers isolate and log recoverable failures.

internal sealed partial class AddinHttpServer
{
    /// <summary>
    /// 装配体排序：按用户配置的排序规则（文件名/组件名，升序/降序）重排顶层组件。
    /// 支持文件夹内排序和递归子装配体排序。
    /// 失败时返回具体错误（如轻化组件、只读文档），不再笼统报 internal_error。
    /// </summary>
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
            try
            {
                var lastFolder = folders.Count > 0 ? folders[folders.Count - 1] : null;
                if (lastFolder != null)
                    assemblyDoc.ReorderComponents(allSorted[0], lastFolder,
                        (int)swReorderComponentsWhere_e.swReorderComponents_After);
                for (var i = 1; i < allSorted.Count; i++)
                    assemblyDoc.ReorderComponents(allSorted[i], allSorted[i - 1], 1);
            }
            catch (Exception ex)
            {
                LogIgnoredException("SortComponents.Reorder", ex);
                throw CommandFailure("sort_reorder_failed", ex.Message);
            }
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

        Safe<bool>(() => model.EditRebuild3());
        try { model.ClearSelection2(true); } catch (Exception ex) { LogIgnoredException("SortComponents.ClearSelection", ex); }

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
            try
            {
                for (var i = 1; i < all.Count; i++)
                    assemblyDoc.ReorderComponents(all[i], all[i - 1], 1);
                return true;
            }
            catch (Exception ex)
            {
                LogIgnoredException("SortComponentsInFolder.Reorder", ex);
                return false;
            }
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

    private sealed class SortOptions
    {
        public bool AssemblyFirst { get; private set; } = true;
        public bool SuppressedLast { get; private set; } = true;
        public bool SortFolders { get; private set; } = true;
        public bool RecursiveSubAssemblies { get; private set; } = true;
        public bool Descending { get; private set; }
        public string NameSource { get; private set; } = "ComponentName";

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
}
