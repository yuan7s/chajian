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
    private DrawingAutomationRunResult RunDrawingAutomation(Dictionary<string, object> args)
    {
        var activeModel = GetActiveModel();
        var activeType = Safe(activeModel.GetType);
        var issues = new List<DrawingAutomationIssue>();
        var created = false;
        var viewsInserted = 0;
        var annotationsImported = 0;
        var scaleAdjusted = false;
        var arrangedAnnotations = 0;
        var scaleText = "";
        var savedPath = "";
        var sourceHoleFeatures = activeType == (int)swDocumentTypes_e.swDocPART ||
                                 activeType == (int)swDocumentTypes_e.swDocASSEMBLY
            ? GetModelHoleFeatureSummary(activeModel)
            : null;

        ModelDoc2 drawingModel;
        DrawingDoc drawing;
        AddinLog.Write($"Drawing automation start: activeType={activeType}, title={Safe(activeModel.GetTitle)}, path={Safe(activeModel.GetPathName)}");

        if (activeType == (int)swDocumentTypes_e.swDocDRAWING)
        {
            drawingModel = activeModel;
            drawing = GetDrawingDoc(drawingModel);
            if (drawing == null)
                throw CommandFailure("drawing_doc_unavailable");
        }
        else if (activeType == (int)swDocumentTypes_e.swDocPART ||
                 activeType == (int)swDocumentTypes_e.swDocASSEMBLY)
        {
            var sourcePath = Safe(activeModel.GetPathName) ?? "";
            if (string.IsNullOrWhiteSpace(sourcePath))
                throw CommandFailure("drawing_source_must_be_saved");

            var baseDrawingPath = ResolveDrawingAutomationBaseDrawingPath(args);
            if (!string.IsNullOrWhiteSpace(baseDrawingPath))
            {
                AddinLog.Write("Drawing automation creating drawing by base copy: " + baseDrawingPath);
                savedPath = CreateDrawingAutomationDocumentFromBaseDrawing(
                    sourcePath,
                    baseDrawingPath,
                    args,
                    out drawingModel,
                    out drawing);
                if (drawingModel == null || drawing == null)
                {
                    created = true;
                    AddDrawingIssue(issues, "info", "工程图", "已复制并交给 SolidWorks 打开", savedPath);
                    return new DrawingAutomationRunResult
                    {
                        summary = BuildDrawingAutomationSummary(
                            "工程图已创建",
                            0,
                            0,
                            issues),
                        created = created,
                        viewsInserted = viewsInserted,
                        annotationsImported = annotationsImported,
                        scaleAdjusted = scaleAdjusted,
                        scaleText = scaleText,
                        arrangedAnnotations = arrangedAnnotations,
                        savedPath = savedPath,
                        issues = issues
                    };
                }

                AddinLog.Write($"Drawing automation base drawing opened: title={Safe(drawingModel.GetTitle)}, path={Safe(drawingModel.GetPathName)}");
            }
            else
            {
                AddinLog.Write("Drawing automation creating drawing from: " + sourcePath);
                drawing = CreateDrawingAutomationDocument(args, out drawingModel);
            }

            created = true;
            AddinLog.Write($"Drawing automation drawing created: title={Safe(drawingModel.GetTitle)}, path={Safe(drawingModel.GetPathName)}");

            AddinLog.Write("Drawing automation applying settings.");
            ApplyDrawingAutomationSettings(drawingModel, drawing, args, issues);
            AddinLog.Write("Drawing automation settings applied.");

            if (GetArgBool(args, "insertStandardViews", true))
            {
                if (string.IsNullOrWhiteSpace(baseDrawingPath))
                {
                    AddinLog.Write("Drawing automation inserting standard views.");
                    viewsInserted = InsertStandardDrawingViews(drawingModel, drawing, sourcePath, args, issues);
                    AddinLog.Write("Drawing automation standard views inserted: " + viewsInserted);
                }
                else
                {
                    viewsInserted = GetModelDrawingViews(drawing).Length;
                    AddinLog.Write("Drawing automation using base drawing views: " + viewsInserted);
                    if (GetArgBool(args, "insertIsoView", true))
                    {
                        viewsInserted += InsertMissingIsoDrawingView(drawingModel, drawing, issues);
                        AddinLog.Write("Drawing automation base drawing views after iso check: " + viewsInserted);
                    }
                }

                AddinLog.Write("Drawing automation scaling and laying out views.");
                var scaleResult = AutoScaleAndLayoutDrawingViews(drawingModel, drawing, issues);
                scaleAdjusted = scaleResult.adjusted;
                scaleText = scaleResult.scaleText;
                AddinLog.Write($"Drawing automation scale/layout done: adjusted={scaleAdjusted}, scale={scaleText}, moved={scaleResult.viewsMoved}");
            }

            if (GetArgBool(args, "importModelItems", true))
            {
                AddinLog.Write("Drawing automation importing model items.");
                annotationsImported = ImportDrawingModelItems(drawingModel, drawing, args, issues);
                AddinLog.Write("Drawing automation model items imported: " + annotationsImported);
                AddinLog.Write("Drawing automation importing hole callouts.");
                var holeCallouts = ImportDrawingHoleCallouts(drawingModel, drawing, args, issues);
                AddinLog.Write("Drawing automation hole callouts imported: " + holeCallouts);
                if (GetArgBool(args, "autoArrangeDimensions", true))
                {
                    AddinLog.Write("Drawing automation rearranging annotations.");
                    arrangedAnnotations = RearrangeDrawingAnnotations(drawingModel, drawing, issues);
                    AddinLog.Write("Drawing automation annotations rearranged: " + arrangedAnnotations);
                }
            }

            if (string.IsNullOrWhiteSpace(baseDrawingPath))
            {
                AddinLog.Write("Drawing automation saving drawing.");
                savedPath = SaveDrawingAutomationDocument(drawingModel, sourcePath, args);
                AddinLog.Write("Drawing automation saved drawing: " + savedPath);
            }
            else
            {
                AddinLog.Write("Drawing automation saving copied drawing.");
                SaveCopiedDrawingAutomationDocument(drawingModel, args);
                AddinLog.Write("Drawing automation copied drawing saved: " + savedPath);
            }
        }
        else
        {
            throw CommandFailure("unsupported_document_type");
        }

        if (!created)
        {
            ApplyDrawingAutomationSettings(drawingModel, drawing, args, issues);

            if (GetArgBool(args, "insertStandardViews", true))
            {
                viewsInserted = GetModelDrawingViews(drawing).Length;
                AddinLog.Write("Drawing automation existing drawing views: " + viewsInserted);
                if (GetArgBool(args, "insertIsoView", true))
                {
                    viewsInserted += InsertMissingIsoDrawingView(drawingModel, drawing, issues);
                    AddinLog.Write("Drawing automation existing drawing views after iso check: " + viewsInserted);
                }
                var scaleResult = AutoScaleAndLayoutDrawingViews(drawingModel, drawing, issues);
                scaleAdjusted = scaleResult.adjusted;
                scaleText = scaleResult.scaleText;
                AddinLog.Write($"Drawing automation existing drawing scale/layout done: adjusted={scaleAdjusted}, scale={scaleText}, moved={scaleResult.viewsMoved}");
            }

            if (GetArgBool(args, "importModelItems", true))
            {
                annotationsImported = ImportDrawingModelItems(drawingModel, drawing, args, issues);
                AddinLog.Write("Drawing automation importing hole callouts (existing).");
                ImportDrawingHoleCallouts(drawingModel, drawing, args, issues);
                if (GetArgBool(args, "autoArrangeDimensions", true))
                    arrangedAnnotations = RearrangeDrawingAnnotations(drawingModel, drawing, issues);
            }

            if (GetArgBool(args, "saveDrawing"))
            {
                AddinLog.Write("Drawing automation saving existing drawing.");
                SaveCopiedDrawingAutomationDocument(drawingModel, args);
                savedPath = Safe(drawingModel.GetPathName) ?? "";
                AddinLog.Write("Drawing automation existing drawing saved: " + savedPath);
            }
        }

        drawingModel.ForceRebuild3(true);
        MarkDocDirty(drawingModel);
        AppendDrawingAutomationCheckIssues(drawingModel, drawing, args, issues, sourceHoleFeatures);
        if (GetArgBool(args, "saveDrawing"))
        {
            var finalPath = Safe(drawingModel.GetPathName) ?? "";
            if (!string.IsNullOrWhiteSpace(finalPath))
            {
                SaveCopiedDrawingAutomationDocument(drawingModel, args);
                savedPath = finalPath;
                AddinLog.Write("Drawing automation final save: " + finalPath);
            }
        }

        return new DrawingAutomationRunResult
        {
            summary = BuildDrawingAutomationSummary(
                "工程图处理完成",
                viewsInserted,
                annotationsImported,
                issues,
                scaleText,
                arrangedAnnotations),
            created = created,
            viewsInserted = viewsInserted,
            annotationsImported = annotationsImported,
            scaleAdjusted = scaleAdjusted,
            scaleText = scaleText,
            arrangedAnnotations = arrangedAnnotations,
            savedPath = savedPath,
            issues = issues
        };
    }

    private object ImportDrawingModelItemsCommand(Dictionary<string, object> args)
    {
        var drawingModel = GetActiveDrawingModel();
        var drawing = GetDrawingDoc(drawingModel);
        if (drawing == null)
            throw CommandFailure("drawing_doc_unavailable");

        var issues = new List<DrawingAutomationIssue>();
        var annotationsImported = ImportDrawingModelItems(drawingModel, drawing, args, issues);
        var arrangedAnnotations = GetArgBool(args, "autoArrangeDimensions", true)
            ? RearrangeDrawingAnnotations(drawingModel, drawing, issues)
            : 0;

        drawingModel.ForceRebuild3(true);
        MarkDocDirty(drawingModel);
        AppendDrawingAutomationCheckIssues(drawingModel, drawing, args, issues);

        return new
        {
            summary = BuildDrawingAutomationSummary(
                "模型项目导入完成",
                0,
                annotationsImported,
                issues,
                "",
                arrangedAnnotations),
            annotationsImported,
            arrangedAnnotations,
            issues
        };
    }

    private object ImportDrawingHoleCalloutsCommand(Dictionary<string, object> args)
    {
        var drawingModel = GetActiveDrawingModel();
        var drawing = GetDrawingDoc(drawingModel);
        if (drawing == null)
            throw CommandFailure("drawing_doc_unavailable");

        var issues = new List<DrawingAutomationIssue>();
        var before = CountDrawingHoleCallouts(drawing);
        var annotationsImported = ImportDrawingHoleCallouts(drawingModel, drawing, args, issues);
        var after = CountDrawingHoleCallouts(drawing);

        drawingModel.ForceRebuild3(true);
        MarkDocDirty(drawingModel);

        if (after > 0)
        {
            AddDrawingIssue(
                issues,
                "pass",
                "孔标注",
                $"当前孔标注 {after} 个，新增 {Math.Max(0, after - before)} 个",
                "通过 SolidWorks 孔标注模型项目导入，未使用圆边扫描");
        }

        return new
        {
            summary = BuildHoleCalloutImportSummary(annotationsImported, after, issues),
            holeCalloutsImported = annotationsImported,
            holeCallouts = after,
            issues
        };
    }

    private object CheckDrawingAutomation(Dictionary<string, object> args)
    {
        var drawingModel = GetActiveDrawingModel();
        var drawing = GetDrawingDoc(drawingModel);
        if (drawing == null)
            throw CommandFailure("drawing_doc_unavailable");

        var issues = new List<DrawingAutomationIssue>();
        var sourceHoleFeatures = GetDrawingSourceHoleFeaturesViaBridge(drawing);
        AppendDrawingAutomationCheckIssues(drawingModel, drawing, args, issues, sourceHoleFeatures);

        return new
        {
            summary = BuildDrawingAutomationSummary("工程图检查完成", 0, 0, issues),
            issues
        };
    }

    private DrawingDoc CreateDrawingAutomationDocument(
        Dictionary<string, object> args,
        out ModelDoc2 drawingModel,
        string templatePath = null)
    {
        templatePath = templatePath ?? ResolveDrawingAutomationTemplatePath(args);

        // Use the cross-AppDomain COM bridge. INewDrawing2 is called in the
        // MAIN AppDomain where _swApp is a direct RCW — no COM proxy deadlock.
        var bridge = GetComBridge();
        if (bridge != null)
        {
            AddinLog.Write("Drawing automation INewDrawing2 via bridge: " + templatePath);
            using var fontDialogHandler = StartSolidWorksFontDialogWatcher(TimeSpan.FromSeconds(60));
            var bridgeType = bridge.GetType();
            drawingModel = bridgeType.InvokeMember(
                "NewDrawing",
                System.Reflection.BindingFlags.InvokeMethod |
                System.Reflection.BindingFlags.Instance |
                System.Reflection.BindingFlags.Public,
                null,
                bridge,
                new object[] { templatePath }) as ModelDoc2;
            if (fontDialogHandler.HandledCount > 0)
                AddinLog.Write("Drawing automation font dialogs handled: " +
                               fontDialogHandler.HandledCount);
        }
        else
        {
            // Fallback: ShellExecute (no bridge available — old shell version)
            AddinLog.Write("Drawing automation ShellExecute (no bridge): " + templatePath);
            var originalTitle = Safe(() => (_swApp.ActiveDoc as ModelDoc2)?.GetTitle()) ?? "";
            var originalPath = Safe(() => (_swApp.ActiveDoc as ModelDoc2)?.GetPathName()) ?? "";
            Process.Start(new ProcessStartInfo(templatePath) { UseShellExecute = true });
            drawingModel = PollForActiveDrawing(originalTitle, originalPath, TimeSpan.FromSeconds(30));
        }

        if (drawingModel == null || Safe(drawingModel.GetType) != (int)swDocumentTypes_e.swDocDRAWING)
            throw CommandFailure("drawing_create_failed", "path", templatePath);

        AddinLog.Write("Drawing automation drawing created: " + (Safe(drawingModel.GetTitle) ?? "?"));

        var drawing = GetDrawingDoc(drawingModel);
        if (drawing == null)
            throw CommandFailure("drawing_doc_unavailable");

        ConfigureDrawingAutomationSheet(drawing, args);
        return drawing;
    }

    private static object GetComBridge()
    {
        try
        {
            return AppDomain.CurrentDomain.GetData("ExternalProgram.SwAddin.ComBridge");
        }
        catch (Exception ex)
        {
            AddinLog.Write("GetComBridge ignored: " + ex.Message);
            return null;
        }
    }

    private ModelDoc2 PollForActiveDrawing(
        string originalTitle,
        string originalPath,
        TimeSpan timeout)
    {
        var sw = Stopwatch.StartNew();
        while (sw.Elapsed < timeout)
        {
            var activeDoc = _swApp.ActiveDoc as ModelDoc2;
            if (activeDoc != null)
            {
                var activeTitle = Safe(activeDoc.GetTitle) ?? "";
                var activePath = Safe(activeDoc.GetPathName) ?? "";
                var activeType = Safe(activeDoc.GetType);

                var isNewDrawing = activeType == (int)swDocumentTypes_e.swDocDRAWING &&
                                   (string.IsNullOrWhiteSpace(activePath) ||
                                    (!string.Equals(activeTitle, originalTitle, StringComparison.OrdinalIgnoreCase) &&
                                     !string.Equals(activePath, originalPath, StringComparison.OrdinalIgnoreCase)));
                if (isNewDrawing)
                    return activeDoc;
            }

            Thread.Sleep(300);
        }

        return null;
    }

    private string CreateDrawingAutomationDocumentFromBaseDrawing(
        string sourcePath,
        string baseDrawingPath,
        Dictionary<string, object> args,
        out ModelDoc2 drawingModel,
        out DrawingDoc drawing)
    {
        if (string.IsNullOrWhiteSpace(sourcePath) || !File.Exists(sourcePath))
            throw CommandFailure("drawing_source_not_found", "path", sourcePath);
        if (string.IsNullOrWhiteSpace(baseDrawingPath) || !File.Exists(baseDrawingPath))
            throw CommandFailure("file_not_found", "path", baseDrawingPath);

        var targetPath = Path.ChangeExtension(sourcePath, ".SLDDRW");
        if (IsSameFileIfExists(baseDrawingPath, targetPath))
            throw CommandFailure("drawing_base_same_as_target", "path", baseDrawingPath);

        if (File.Exists(targetPath))
        {
            if (!GetArgBool(args, "overwriteDrawing"))
                throw CommandFailure("target_file_exists", "path", targetPath);

            File.Delete(targetPath);
        }

        var references = GetDrawingModelDependencies(baseDrawingPath)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var replacementCandidates = references
            .Where(item => string.Equals(Path.GetExtension(item), Path.GetExtension(sourcePath), StringComparison.OrdinalIgnoreCase))
            .Concat(references)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (replacementCandidates.Length == 0)
            throw CommandFailure("drawing_base_reference_required", "path", baseDrawingPath);

        File.Copy(baseDrawingPath, targetPath, false);
        ClearReadOnlyAttribute(targetPath);
        AddinLog.Write("Drawing automation base drawing copied: " + baseDrawingPath + " -> " + targetPath);

        var replacements = ReplaceDrawingReferencesOffline(
            targetPath,
            replacementCandidates,
            sourcePath,
            "Drawing automation");

        if (replacements == 0)
        {
            var keepOpen = GetArgBool(args, "openCopiedDrawingInProcess");
            if (!TryReplaceDrawingReferencesByOpening(
                    targetPath,
                    replacementCandidates,
                    sourcePath,
                    keepOpen,
                    "Drawing automation",
                    out drawingModel,
                    out drawing))
            {
                TryDeleteDrawingAutomationCopy(targetPath);
                throw CommandFailure("drawing_reference_replace_failed", "path", targetPath);
            }

            if (keepOpen)
            {
                ConfigureDrawingAutomationSheet(drawing, args);
                return targetPath;
            }
        }

        if (!GetArgBool(args, "openCopiedDrawingInProcess"))
        {
            OpenDrawingWithShell(targetPath);
            drawingModel = null;
            drawing = null;
            return targetPath;
        }

        var errors = 0;
        var warnings = 0;
        drawingModel = OpenDoc6WithDialogHandling(
            targetPath,
            (int)swDocumentTypes_e.swDocDRAWING,
            (int)swOpenDocOptions_e.swOpenDocOptions_Silent,
            "",
            ref errors,
            ref warnings);
        if (drawingModel == null || errors != 0)
            throw CommandFailure("open_document_failed", "errors", errors, "warnings", warnings, "path", targetPath);

        drawing = GetDrawingDoc(drawingModel);
        if (drawing == null)
            throw CommandFailure("drawing_doc_unavailable");

        ConfigureDrawingAutomationSheet(drawing, args);
        return targetPath;
    }

    private static void OpenDrawingWithShell(string targetPath)
    {
        try
        {
            _ = StartSolidWorksFontDialogWatcher(TimeSpan.FromSeconds(60));
            Process.Start(new ProcessStartInfo(targetPath) { UseShellExecute = true });
            AddinLog.Write("Drawing automation shell open requested: " + targetPath);
        }
        catch (Exception ex)
        {
            LogIgnoredException("OpenDrawingWithShell", ex);
        }
    }

    private static void TryDeleteDrawingAutomationCopy(string targetPath)
    {
        try
        {
            if (File.Exists(targetPath))
                File.Delete(targetPath);
        }
        catch (Exception ex)
        {
            LogIgnoredException("TryDeleteDrawingAutomationCopy", ex);
        }
    }

    private static void ClearReadOnlyAttribute(string path)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) return;

            var attributes = File.GetAttributes(path);
            if ((attributes & FileAttributes.ReadOnly) == 0) return;

            File.SetAttributes(path, attributes & ~FileAttributes.ReadOnly);
            AddinLog.Write("Cleared read-only attribute: " + path);
        }
        catch (Exception ex)
        {
            LogIgnoredException("ClearReadOnlyAttribute", ex);
        }
    }

    private static void SaveCopiedDrawingAutomationDocument(ModelDoc2 drawingModel, Dictionary<string, object> args)
    {
        if (!GetArgBool(args, "saveDrawing")) return;

        var errors = 0;
        var warnings = 0;
        var ok = drawingModel.Save3(
            (int)swSaveAsOptions_e.swSaveAsOptions_Silent,
            ref errors,
            ref warnings);
        if (!ok || errors != 0)
            throw CommandFailure("save_new_file_failed", "errors", errors, "warnings", warnings,
                "path", Safe(drawingModel.GetPathName) ?? "");
    }

    // 把图幅字符串映射成 SolidWorks 纸张枚举与尺寸(米，横向)。
    // 未知值回退 A3，与历史行为一致。
    private static (int paperSize, double widthMeters, double heightMeters) MapPaperSize(string size)
    {
        switch ((size ?? "").Trim().ToUpperInvariant())
        {
            case "A4": return ((int)swDwgPaperSizes_e.swDwgPaperA4size, 0.297, 0.210);
            case "A2": return ((int)swDwgPaperSizes_e.swDwgPaperA2size, 0.594, 0.420);
            case "A1": return ((int)swDwgPaperSizes_e.swDwgPaperA1size, 0.841, 0.594);
            case "A0": return ((int)swDwgPaperSizes_e.swDwgPaperA0size, 1.189, 0.841);
            case "A3":
            default:   return ((int)swDwgPaperSizes_e.swDwgPaperA3size, 0.420, 0.297);
        }
    }

    private static void ConfigureDrawingAutomationSheet(DrawingDoc drawing, Dictionary<string, object> args)
    {
        var sheet = drawing.GetCurrentSheet() as Sheet;
        if (sheet == null) return;

        // When using the SW default template, the sheet properties and format
        // are already set correctly by the template — don't override them.
        if (GetArgBool(args, "useDefaultTemplate", true))
        {
            AddinLog.Write("ConfigureDrawingAutomationSheet skipped (default template)");
            return;
        }

        try
        {
            var (paperSize, paperWidth, paperHeight) = MapPaperSize(GetArgString(args, "paperSize", "A3"));
            sheet.SetProperties2(
                paperSize,
                (int)swDwgTemplates_e.swDwgTemplateCustom,
                1.0,
                1.0,
                false,
                paperWidth,
                paperHeight,
                true);
        }
        catch (Exception ex)
        {
            LogIgnoredException("ConfigureDrawingAutomationSheet.SetProperties2", ex);
        }

        var sheetFormatPath = GetArgString(args, "sheetFormatPath");
        if (string.IsNullOrWhiteSpace(sheetFormatPath))
            return;

        if (!File.Exists(sheetFormatPath)) return;

        try
        {
            sheet.SetTemplateName(sheetFormatPath);
            sheet.ReloadTemplate(true);
        }
        catch (Exception ex)
        {
            LogIgnoredException("ConfigureDrawingAutomationSheet.ReloadTemplate", ex);
        }
    }

    private string ResolveDrawingAutomationTemplatePath(Dictionary<string, object> args)
    {
        if (!GetArgBool(args, "useDefaultTemplate", true))
        {
            var templatePath = GetArgString(args, "templatePath");
            if (string.IsNullOrWhiteSpace(templatePath))
                throw CommandFailure("drawing_template_required");
            if (!File.Exists(templatePath))
                throw CommandFailure("file_not_found", "path", templatePath);
            if (!string.Equals(Path.GetExtension(templatePath), ".drwdot", StringComparison.OrdinalIgnoreCase))
                throw CommandFailure("unsupported_file_type", "path", templatePath);

            return templatePath;
        }

        var defaultTemplate = ResolveDefaultDrawingTemplatePath();
        if (string.IsNullOrWhiteSpace(defaultTemplate))
            throw CommandFailure("drawing_template_unavailable");

        return defaultTemplate;
    }

    private string ResolveDefaultDrawingTemplatePath()
    {
        var candidates = new List<string>();

        // Primary: read user's configured default template from SW preferences.
        // GetUserPreferenceStringValue is a registry read — no dialogs, no COM hang.
        AddTemplateCandidate(candidates, Safe(() =>
            _swApp.GetUserPreferenceStringValue((int)swUserPreferenceStringValue_e.swDefaultTemplateDrawing)));

        // Fallback: search known template directories
        foreach (var candidate in GetKnownDrawingTemplateCandidates())
            AddTemplateCandidate(candidates, candidate);

        foreach (var candidate in candidates.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (!string.Equals(Path.GetExtension(candidate), ".drwdot", StringComparison.OrdinalIgnoreCase))
                continue;

            if (!File.Exists(candidate))
                continue;

            AddinLog.Write("Drawing automation default template resolved: " + candidate);
            return candidate;
        }

        AddinLog.Write("Drawing automation default template unavailable. Candidates: " + string.Join(";", candidates));
        return "";
    }

    private DrawingHoleFeatureSummary GetDrawingSourceHoleFeaturesViaBridge(DrawingDoc drawing)
    {
        try
        {
            if (drawing == null) return null;
            var view = drawing.GetFirstView() as View;
            if (view == null) return null;
            var modelPath = GetViewReferencedModelPath(view);
            if (string.IsNullOrWhiteSpace(modelPath) || !File.Exists(modelPath)) return null;

            // Check if model is already open
            var existing = _swApp.GetOpenDocumentByName(modelPath) as ModelDoc2;
            if (existing != null)
                return GetModelHoleFeatureSummary(existing);

            // Open via bridge to avoid cross-AppDomain COM hang
            var bridge = GetComBridge();
            if (bridge == null) return null;

            var bridgeType = bridge.GetType();
            var docType = string.Equals(Path.GetExtension(modelPath), ".SLDASM", StringComparison.OrdinalIgnoreCase)
                ? (int)swDocumentTypes_e.swDocASSEMBLY
                : (int)swDocumentTypes_e.swDocPART;

            var model = bridgeType.InvokeMember(
                "OpenDocReadOnly",
                System.Reflection.BindingFlags.InvokeMethod |
                System.Reflection.BindingFlags.Instance |
                System.Reflection.BindingFlags.Public,
                null,
                bridge,
                new object[] { modelPath, docType }) as ModelDoc2;

            if (model == null) return null;
            var summary = GetModelHoleFeatureSummary(model);
            bridgeType.InvokeMember("CloseDoc", System.Reflection.BindingFlags.InvokeMethod |
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public,
                null, bridge, new object[] { Safe(model.GetTitle) ?? "" });
            return summary;
        }
        catch (Exception ex)
        {
            AddinLog.Write("GetDrawingSourceHoleFeaturesViaBridge ignored: " + ex.Message);
            return null;
        }
    }

    private static void AddTemplateCandidate(List<string> candidates, string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return;
        candidates.Add(path.Trim());
    }

    private static IEnumerable<string> GetKnownDrawingTemplateCandidates()
    {
        var programData = System.Environment.GetFolderPath(System.Environment.SpecialFolder.CommonApplicationData);
        var solidWorksRoot = Path.Combine(programData, "SolidWorks");

        foreach (var version in GetKnownSolidWorksTemplateVersions(solidWorksRoot))
        {
            foreach (var fileName in DrawingDefaultTemplateFileNames)
                yield return Path.Combine(version, "templates", fileName);
        }
    }

    private static IEnumerable<string> GetKnownSolidWorksTemplateVersions(string solidWorksRoot)
    {
        yield return Path.Combine(solidWorksRoot, "SOLIDWORKS 2022");

        if (!Directory.Exists(solidWorksRoot))
            yield break;

        foreach (var directory in Directory.EnumerateDirectories(solidWorksRoot, "SOLIDWORKS *"))
            yield return directory;
    }

    private static string ResolveDrawingAutomationBaseDrawingPath(Dictionary<string, object> args)
    {
        var path = GetArgString(args, "baseDrawingPath");
        if (string.IsNullOrWhiteSpace(path))
            path = GetArgString(args, "drawingTemplatePath");
        if (string.IsNullOrWhiteSpace(path))
            path = GetArgString(args, "referenceDrawingPath");
        if (string.IsNullOrWhiteSpace(path))
            return "";

        if (!File.Exists(path))
            throw CommandFailure("file_not_found", "path", path);
        if (!string.Equals(Path.GetExtension(path), ".SLDDRW", StringComparison.OrdinalIgnoreCase))
            throw CommandFailure("unsupported_file_type", "path", path);

        return Path.GetFullPath(path);
    }

    private void ApplyDrawingAutomationSettings(
        ModelDoc2 drawingModel,
        DrawingDoc drawing,
        Dictionary<string, object> args,
        List<DrawingAutomationIssue> issues)
    {
        if (!GetArgBool(args, "applyDrawingSettings", true)) return;

        if (TryGetOptionalDrawingResourcePath(args, "standardPath", ".sldstd", "绘图标准文件", issues,
                out var standardPath))
        {
            if (!LoadDrawingStandard(drawingModel, standardPath))
                AddDrawingIssue(issues, "warning", "绘图标准", "绘图标准加载结果未确认", standardPath);
        }

        if (TryGetOptionalDrawingResourcePath(args, "sheetFormatPath", ".slddrt", "图纸格式文件", issues,
                out var sheetFormatPath))
        {
            var changed = ApplySheetFormatToAllSheets(drawing, sheetFormatPath);
            if (changed == 0)
                AddDrawingIssue(issues, "warning", "图纸格式", "未确认图纸格式已替换", sheetFormatPath);
        }
    }

    // ReSharper disable InconsistentNaming
    private sealed class DrawingAutomationRunResult
    {
        public string summary { get; set; }
        public bool created { get; set; }
        public int viewsInserted { get; set; }
        public int annotationsImported { get; set; }
        public bool scaleAdjusted { get; set; }
        public string scaleText { get; set; }
        public int arrangedAnnotations { get; set; }
        public string savedPath { get; set; }
        public List<DrawingAutomationIssue> issues { get; set; }
    }
    // ReSharper restore InconsistentNaming

}
