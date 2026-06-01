using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using SldWorks;
using SwConst;

namespace 外部程序.SwAddin;

internal sealed partial class AddinHttpServer
{
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
        catch { }
    }

    private object ExecuteCommand(CommandRequest request)
    {
        var args = request.Args ?? new Dictionary<string, object>();
        object result;
        switch ((request.Command ?? string.Empty).Trim().ToLowerInvariant())
        {
            case "ping":
                result = new { message = "pong", time = DateTime.Now };
                break;

            case "active-document":
                result = GetActiveDocumentInfo();
                break;

            case "save-dwg":
                result = SaveActiveDrawingAs(".dwg", "DWG");
                break;

            case "save-pdf":
                result = SaveActiveDrawingAs(".pdf", "PDF");
                break;

            case "open-file-location":
                result = GetActiveOrSelectedModelPath();
                break;

            case "rotate-drawing-view":
                result = RotateSelectedDrawingView();
                break;

            case "get-component-tree":
                result = GetComponentTree();
                break;

            case "sort-components":
                result = SortComponents();
                break;

            case "hide-config-names":
                result = HideConfigNames();
                break;

            case "sync-coding-props":
                result = SyncCodingProps();
                break;

            case "read-properties":
                result = ReadProperties(args);
                break;

            case "write-properties":
                result = WriteProperties(args);
                break;

            case "get-bounding-box":
                result = GetBoundingBox(args);
                break;

            case "rename-component":
                result = RenameComponent(args);
                break;

            case "coding-cleanup":
                result = CodingCleanup(args);
                break;

            case "rebuild":
                result = Rebuild();
                break;

            case "save":
                result = Save();
                break;

            case "open-document":
                result = OpenDocument(args);
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
        catch { return default; }
    }

    private string GetArgString(Dictionary<string, object> args, string key, string fallback = null)
    {
        if (args.TryGetValue(key, out var val) && val != null) return val.ToString();
        return fallback;
    }

    private object GetActiveDocumentInfo()
    {
        var model = GetActiveModel();
        return new
        {
            title = Safe(() => model.GetTitle()),
            path = Safe(() => model.GetPathName()),
            configuration = Safe(() => model.ConfigurationManager.ActiveConfiguration.Name),
            type = Safe(() => (int)model.GetType())
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

    private ModelDoc2 TryGetOpenModelByPath(string path)
    {
        try
        {
            var model = _swApp.GetOpenDocumentByName(path) as ModelDoc2;
            return model;
        }
        catch { return null; }
    }

    private int GetDocumentTypeFromPath(string path)
    {
        var ext = Path.GetExtension(path)?.ToLowerInvariant();
        switch (ext)
        {
            case ".sldprt": return (int)swDocumentTypes_e.swDocPART;
            case ".sldasm": return (int)swDocumentTypes_e.swDocASSEMBLY;
            case ".slddrw": return (int)swDocumentTypes_e.swDocDRAWING;
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
        var path = model.GetPathName();
        if (!string.IsNullOrWhiteSpace(path)) return new { path };

        if (model.GetType() == (int)swDocumentTypes_e.swDocASSEMBLY)
        {
            try
            {
                var selMgr = model.SelectionManager as SelectionMgr;
                var comp = selMgr?.GetSelectedObjectsComponent(1) as Component2;
                if (comp != null)
                {
                    path = comp.GetPathName();
                    if (!string.IsNullOrWhiteSpace(path)) return new { path };
                }
            }
            catch { }
        }

        return new { path = model.GetTitle() };
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

    private void MarkDocDirty(ModelDoc2 model)
    {
        try { model.SetSaveFlag(); } catch { }
    }

    // --- Hide Config Names (FeatureManager) ---

    private object HideConfigNames()
    {
        var model = GetActiveModel();
        if (model.GetType() != (int)swDocumentTypes_e.swDocASSEMBLY)
            throw new InvalidOperationException("请在装配体环境下使用");

        var featMgr = model.FeatureManager;
        featMgr.HideComponentSingleConfigurationOrDisplayStateNames = false;
        featMgr.SetComponentIdentifiers(4, 0, 0);
        featMgr.SetComponentIdentifiers(2, 0, 0);
        featMgr.ShowComponentConfigurationNames = false;
        featMgr.ShowComponentConfigurationDescriptions = false;
        featMgr.ShowDisplayStateNames = false;

        RecursiveHideConfigNames(_swApp, model);

        return new { done = true };
    }

    private void RecursiveHideConfigNames(SldWorks.SldWorks swApp, ModelDoc2 asmDoc)
    {
        var configuration = (Configuration)asmDoc.GetActiveConfiguration();
        var rootComponent = (Component2)configuration.GetRootComponent();
        var comps = (object[])rootComponent.GetChildren();

        foreach (Component2 child in comps)
        {
            var childModel = child.GetModelDoc() as ModelDoc2;
            if (childModel == null) continue;

            var childType = childModel.GetType();
            if (childType == (int)swDocumentTypes_e.swDocASSEMBLY)
            {
                var longstatus = 0;
                var longWarnings = 0;
                var fopen = swApp.OpenDoc6(child.GetPathName(), (int)swDocumentTypes_e.swDocASSEMBLY,
                    (int)swOpenDocOptions_e.swOpenDocOptions_Silent, "", ref longstatus, ref longWarnings);
                if (longstatus == 0 && fopen != null)
                {
                    var swFeatMgr = fopen.FeatureManager;
                    swFeatMgr.HideComponentSingleConfigurationOrDisplayStateNames = false;
                    swFeatMgr.SetComponentIdentifiers(4, 0, 0);
                    swFeatMgr.SetComponentIdentifiers(2, 0, 0);
                    swFeatMgr.ShowComponentConfigurationNames = false;
                    swFeatMgr.ShowComponentConfigurationDescriptions = false;
                    swFeatMgr.ShowDisplayStateNames = false;
                }
                RecursiveHideConfigNames(swApp, childModel);
            }
        }
    }

    // --- Get Component Tree ---

    private object GetComponentTree()
    {
        var model = GetActiveModel();
        if (model.GetType() != (int)swDocumentTypes_e.swDocASSEMBLY)
            throw new InvalidOperationException("请在装配体环境下使用");

        var components = new List<object>();
        var configuration = (Configuration)model.GetActiveConfiguration();
        var rootComponent = (Component2)configuration.GetRootComponent();
        var comps = (object[])rootComponent.GetChildren();

        foreach (Component2 child in comps)
        {
            var childModel = child.GetModelDoc() as ModelDoc2;
            var info = new
            {
                name = Safe(() => child.Name2),
                path = Safe(() => child.GetPathName()),
                referencedPath = childModel != null ? Safe(() => childModel.GetPathName()) : null,
                type = childModel != null ? Safe(() => (int)childModel.GetType()) : -1,
                isSuppressed = Safe(() => child.IsSuppressed()),
                isEnvelope = Safe(() => child.IsEnvelope())
            };
            components.Add(info);
        }

        return new { components };
    }

    // --- Sort Components ---

    private object SortComponents()
    {
        var model = GetActiveModel();
        if (model.GetType() != (int)swDocumentTypes_e.swDocASSEMBLY)
            throw new InvalidOperationException("请在装配体环境下使用");

        var assemblyDoc = model as AssemblyDoc;
        if (assemblyDoc == null) throw new InvalidOperationException("无法获取 AssemblyDoc");

        var vFeats = (object[])model.FeatureManager.GetFeatures(true);

        // Find start/end indices
        var startIdx = 0;
        var endIdx = vFeats.Length - 1;
        for (var i = 0; i < vFeats.Length; i++)
        {
            var t = ((Feature)vFeats[i]).GetTypeName2();
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
            var feat = (Feature)vFeats[i];
            var featType = feat.GetTypeName2();
            if (featType == "FtrFolder")
            {
                if (!feat.Name.Contains("___EndTag___"))
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
                catch { }
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
        var sortedPartComps = CollectAndSort(topLevelFeats, false);
        var sortedAsmComps = CollectAndSort(topLevelFeats, true);
        var sortedSupPart = CollectAndSort(suppressedEnvFeats, false);
        var sortedSupAsm = CollectAndSort(suppressedEnvFeats, true);

        var allSorted = new List<Component2>();
        allSorted.AddRange(sortedAsmComps);
        allSorted.AddRange(sortedPartComps);
        allSorted.AddRange(sortedSupAsm);
        allSorted.AddRange(sortedSupPart);

        if (allSorted.Count > 0)
        {
            var lastFolder = folders.Count > 0 ? folders[folders.Count - 1] : null;
            if (lastFolder != null)
                assemblyDoc.ReorderComponents(allSorted[0], lastFolder, (int)swReorderComponentsWhere_e.swReorderComponents_After);
            for (var i = 1; i < allSorted.Count; i++)
                assemblyDoc.ReorderComponents(allSorted[i], allSorted[i - 1], 1);
        }

        foreach (var f in folders)
            SortComponentsInFolder(f, assemblyDoc);

        var processed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        RecursiveSortSubAssemblies(topLevelFeats, processed);
        foreach (var fc in folderComponents)
            RecursiveSortSubAssemblies(fc, processed);

        model.EditRebuild3();
        model.ClearSelection2(true);

        return new { done = true, topLevelSorted = allSorted.Count, foldersSorted = folders.Count };
    }

    private List<Component2> CollectAndSort(List<Feature> feats, bool assemblies)
    {
        var result = new List<Component2>();
        var targetType = assemblies ? (int)swDocumentTypes_e.swDocASSEMBLY : (int)swDocumentTypes_e.swDocPART;
        foreach (var feat in feats)
        {
            var comp = feat.GetSpecificFeature2() as Component2;
            if (comp == null) continue;
            try
            {
                var childModel = comp.GetModelDoc() as ModelDoc2;
                var childType = childModel != null ? childModel.GetType() : -1;
                if (childType == targetType) result.Add(comp);
            }
            catch { }
        }
        result.Sort((a, b) => string.Compare(Safe(() => a.Name2), Safe(() => b.Name2), StringComparison.OrdinalIgnoreCase));
        return result;
    }

    private void SortComponentsInFolder(Feature folder, AssemblyDoc assemblyDoc)
    {
        var partComps = new List<Component2>();
        var asmComps = new List<Component2>();
        var supPartComps = new List<Component2>();
        var supAsmComps = new List<Component2>();

        var subFeat = folder.GetFirstSubFeature() as Feature;
        while (subFeat != null)
        {
            if (subFeat.GetTypeName2() == "Reference")
            {
                var comp = subFeat.GetSpecificFeature2() as Component2;
                if (comp != null)
                {
                    var isSupEnv = Safe(() => comp.IsSuppressed()) || Safe(() => comp.IsEnvelope());
                    var childModel = comp.GetModelDoc() as ModelDoc2;
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
            subFeat = subFeat.GetNextSubFeature() as Feature;
        }

        asmComps.Sort((a, b) => string.Compare(Safe(() => a.Name2), Safe(() => b.Name2), StringComparison.OrdinalIgnoreCase));
        partComps.Sort((a, b) => string.Compare(Safe(() => a.Name2), Safe(() => b.Name2), StringComparison.OrdinalIgnoreCase));
        supAsmComps.Sort((a, b) => string.Compare(Safe(() => a.Name2), Safe(() => b.Name2), StringComparison.OrdinalIgnoreCase));
        supPartComps.Sort((a, b) => string.Compare(Safe(() => a.Name2), Safe(() => b.Name2), StringComparison.OrdinalIgnoreCase));

        var all = new List<Component2>();
        all.AddRange(asmComps); all.AddRange(partComps);
        all.AddRange(supAsmComps); all.AddRange(supPartComps);

        if (all.Count > 1)
        {
            for (var i = 1; i < all.Count; i++)
                assemblyDoc.ReorderComponents(all[i], all[i - 1], 1);
        }
    }

    private void RecursiveSortSubAssemblies(List<Feature> feats, HashSet<string> processed)
    {
        foreach (var feat in feats)
        {
            if (feat.GetTypeName2() != "Reference") continue;
            var comp = feat.GetSpecificFeature2() as Component2;
            if (comp == null) continue;
            try
            {
                var model = comp.GetModelDoc() as ModelDoc2;
                if (model == null || model.GetType() != (int)swDocumentTypes_e.swDocASSEMBLY) continue;
                var path = model.GetPathName();
                if (string.IsNullOrWhiteSpace(path) || !processed.Add(path)) continue;
                SortSubAsmByFeatures(model, processed);
            }
            catch { }
        }
    }

    private void SortSubAsmByFeatures(ModelDoc2 asmModel, HashSet<string> processed)
    {
        try
        {
            var vFeats = (object[])asmModel.FeatureManager.GetFeatures(true);
            var refFeats = new List<Feature>();
            for (var i = 0; i < vFeats.Length; i++)
            {
                if (((Feature)vFeats[i]).GetTypeName2() == "Reference")
                    refFeats.Add((Feature)vFeats[i]);
            }

            var asmComps = CollectAndSort(refFeats, true);
            var partComps = CollectAndSort(refFeats, false);

            var all = new List<Component2>();
            all.AddRange(asmComps); all.AddRange(partComps);

            if (all.Count > 1)
            {
                var asmDoc = asmModel as AssemblyDoc;
                if (asmDoc != null)
                {
                    for (var i = 1; i < all.Count; i++)
                        asmDoc.ReorderComponents(all[i], all[i - 1], 1);
                }
            }

            RecursiveSortSubAssemblies(refFeats, processed);
        }
        catch { }
    }

    // --- Read Properties ---

    private object ReadProperties(Dictionary<string, object> args)
    {
        var model = GetActiveModel();
        var configName = GetArgString(args, "configuration", "");
        var manager = model.Extension.get_CustomPropertyManager(configName);
        var values = new Dictionary<string, string>();

        try
        {
            var names = (string[])manager.GetNames();
            if (names != null)
            {
                foreach (var name in names)
                {
                    try
                    {
                        manager.Get5(name, false, out var val, out var _, out var _);
                        values[name] = val ?? "";
                    }
                    catch { }
                }
            }
        }
        catch { }

        return new { configuration = string.IsNullOrWhiteSpace(configName) ? "custom" : configName, properties = values };
    }

    // --- Write Properties ---

    private object WriteProperties(Dictionary<string, object> args)
    {
        var model = GetActiveModel();
        var configName = GetArgString(args, "configuration", "");
        var propsArg = args.TryGetValue("properties", out var p) ? p : null;
        var properties = propsArg as Dictionary<string, object>;
        if (properties == null) throw new InvalidOperationException("properties 不能为空");

        var manager = model.Extension.get_CustomPropertyManager(configName);
        var written = new List<string>();
        foreach (var kvp in properties)
        {
            manager.Add3(kvp.Key, 30, kvp.Value?.ToString() ?? "", 2);
            written.Add(kvp.Key);
        }

        try { model.SetSaveFlag(); } catch { }

        return new { configuration = configName, written = written.ToArray() };
    }

    // --- Get Bounding Box ---

    private object GetBoundingBox(Dictionary<string, object> args)
    {
        var model = GetActiveModel();
        var configName = GetArgString(args, "configuration", "");

        if (!string.IsNullOrWhiteSpace(configName))
        {
            try { model.ShowConfiguration2(configName); }
            catch { }
        }

        var box = (double[])((PartDoc)model).GetPartBox(false);
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
        var configName = Safe(() => model.ConfigurationManager.ActiveConfiguration.Name);

        var title = model.GetTitle();
        var dotIdx = title.IndexOf(".");
        if (dotIdx > 0) title = title.Substring(0, dotIdx);

        var materialCode = Safe(() => model.GetCustomInfoValue(configName, "物料编码"));
        var partNumber = Safe(() => model.GetCustomInfoValue(configName, "零件图号"));

        if (title != materialCode || title != partNumber)
        {
            var config = model.GetActiveConfiguration() as Configuration;
            var cusPropMgr = config.CustomPropertyManager;
            cusPropMgr.Add3("物料编码", 30, title, 2);
            cusPropMgr.Add3("零件图号", 30, title, 2);
            cusPropMgr.Add3("文件名称", 30, title, 2);
            model.SetSaveFlag();
            return new { synced = true, title, materialCode, partNumber };
        }

        return new { synced = false, title, materialCode, partNumber, message = "已同步，无需更新" };
    }

    // --- Rename Component ---

    private object RenameComponent(Dictionary<string, object> args)
    {
        var model = GetActiveModel();
        var newName = GetArgString(args, "newName");
        var saveAs = GetArgString(args, "saveAs", "");
        var targetPath = GetArgString(args, "targetPath", "");

        if (string.IsNullOrWhiteSpace(newName)) throw new InvalidOperationException("newName 不能为空");

        Component2 comp = null;
        try
        {
            var selMgr = model.SelectionManager as SelectionMgr;
            comp = selMgr?.GetSelectedObjectsComponent(1) as Component2;
        }
        catch { }
        if (comp == null) throw new InvalidOperationException("请先选中一个组件");

        var oldName = Safe(() => comp.Name2);
        var compPath = Safe(() => comp.GetPathName());

        comp.Name2 = newName;

        model.SetSaveFlag();

        var savedAsPath = "";
        if (!string.IsNullOrWhiteSpace(saveAs))
        {
            try
            {
                var refModel = comp.GetModelDoc() as ModelDoc2;
                if (refModel != null)
                {
                    var saveDir = Path.GetDirectoryName(model.GetPathName());
                    var destPath = Path.Combine(saveDir, saveAs);
                    if (!destPath.EndsWith(".sldprt") && !destPath.EndsWith(".sldasm"))
                        destPath += Path.GetExtension(compPath);
                    refModel.SaveAs3(destPath, 0, 2);
                    savedAsPath = destPath;
                }
            }
            catch { }
        }

        return new { oldName, newName, compPath, savedAsPath };
    }

    // --- Coding Cleanup ---

    private object CodingCleanup(Dictionary<string, object> args)
    {
        var model = GetActiveModel();
        var nameFilter = GetArgString(args, "nameFilter", "");
        var processAsm = GetArgString(args, "processAsm", "true") == "true";
        var processPart = GetArgString(args, "processPart", "true") == "true";
        var excludeVirtual = GetArgString(args, "excludeVirtual", "true") == "true";
        var excludeStandard = GetArgString(args, "excludeStandard", "true") == "true";
        var excludePurchased = GetArgString(args, "excludePurchased", "true") == "true";

        if (model.GetType() != (int)swDocumentTypes_e.swDocASSEMBLY)
            throw new InvalidOperationException("请在装配体环境下使用");

        var configuration = model.GetActiveConfiguration() as Configuration;
        var rootComponent = configuration.GetRootComponent() as Component2;
        var comps = (object[])rootComponent.GetChildren();

        var results = new List<object>();
        ProcessCodingCleanup(comps, nameFilter, processAsm, processPart,
            excludeVirtual, excludeStandard, excludePurchased, results);

        model.EditRebuild3();
        return new { processed = results.Count, results };
    }

    private void ProcessCodingCleanup(object[] comps, string nameFilter, bool processAsm, bool processPart,
        bool excludeVirtual, bool excludeStandard, bool excludePurchased, List<object> results)
    {
        foreach (Component2 child in comps)
        {
            try
            {
                var childModel = child.GetModelDoc() as ModelDoc2;
                if (childModel == null) continue;

                var childType = childModel.GetType();
                var childName = Safe(() => child.Name2);

                if (!string.IsNullOrWhiteSpace(nameFilter) && childName != null && !childName.Contains(nameFilter)) continue;

                var isVirtual = Safe(() => child.IsVirtual);
                if (excludeVirtual && isVirtual) continue;

                if (childType == (int)swDocumentTypes_e.swDocPART && !processPart) continue;
                if (childType == (int)swDocumentTypes_e.swDocASSEMBLY && !processAsm) continue;

                var configName = Safe(() => child.ReferencedConfiguration);
                if (string.IsNullOrWhiteSpace(configName)) configName = "";

                var title = Safe(() => childModel.GetTitle());
                var dotIdx = title.IndexOf(".");
                if (dotIdx > 0) title = title.Substring(0, dotIdx);

                var materialCode = Safe(() => childModel.GetCustomInfoValue(configName, "物料编码"));
                var partNumber = Safe(() => childModel.GetCustomInfoValue(configName, "零件图号"));

                if (title != materialCode || title != partNumber)
                {
                    var cusPropMgr = childModel.Extension.get_CustomPropertyManager(configName);
                    cusPropMgr.Add3("物料编码", 30, title, 2);
                    cusPropMgr.Add3("零件图号", 30, title, 2);
                    cusPropMgr.Add3("文件名称", 30, title, 2);
                    childModel.SetSaveFlag();
                    results.Add(new { name = childName, title, materialCode, partNumber, action = "synced" });
                }

                if (childType == (int)swDocumentTypes_e.swDocASSEMBLY)
                {
                    var subConfig = childModel.GetActiveConfiguration() as Configuration;
                    var subRoot = subConfig.GetRootComponent() as Component2;
                    var subComps = (object[])subRoot.GetChildren();
                    ProcessCodingCleanup(subComps, nameFilter, processAsm, processPart,
                        excludeVirtual, excludeStandard, excludePurchased, results);
                }
            }
            catch { }
        }
    }
}
