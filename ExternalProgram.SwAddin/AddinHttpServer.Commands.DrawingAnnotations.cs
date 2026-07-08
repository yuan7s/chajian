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
    private static IEnumerable<string> GetDrawingViewModelNameCandidates(string sourcePath)
    {
        var fileName = Safe(() => Path.GetFileName(sourcePath)) ?? "";
        if (!string.IsNullOrWhiteSpace(fileName))
            yield return fileName;
        if (!string.IsNullOrWhiteSpace(sourcePath) &&
            !string.Equals(sourcePath, fileName, StringComparison.OrdinalIgnoreCase))
        {
            yield return sourcePath;
        }
    }

    private static int ImportDrawingModelItems(
        ModelDoc2 drawingModel,
        DrawingDoc drawing,
        Dictionary<string, object> args,
        List<DrawingAutomationIssue> issues)
    {
        var before = CountDrawingDisplayDimensions(drawing);
        var duplicateDimensions = true; // 强制消除重复
        var hiddenFeatureDimensions = GetArgBool(args, "hiddenFeatureDimensions");
        var usePlacementInSketch = GetArgBool(args, "usePlacementInSketch");

        var annotations = ToObjectArray(drawing.InsertModelAnnotations3(
            0,
            DrawingModelAnnotationOptions,
            true,
            duplicateDimensions,
            hiddenFeatureDimensions,
            usePlacementInSketch));

        if (annotations.Length == 0)
            AddDrawingIssue(issues, "warning", "模型项目", "没有导入新的模型项目", "");

        AssignDrawingAnnotationsToLayers(drawingModel, drawing);

        var after = CountDrawingDisplayDimensions(drawing);
        return Math.Max(annotations.Length, Math.Max(0, after - before));
    }

    private static int ImportDrawingHoleCallouts(
        ModelDoc2 drawingModel,
        DrawingDoc drawing,
        Dictionary<string, object> args,
        List<DrawingAutomationIssue> issues)
    {
        var before = CountDrawingHoleCallouts(drawing);
        var duplicateDimensions = true; // 强制消除重复
        var hiddenFeatureDimensions = GetArgBool(args, "hiddenFeatureDimensions");
        var usePlacementInSketch = GetArgBool(args, "usePlacementInSketch");

        var annotations = ToObjectArray(drawing.InsertModelAnnotations3(
            0,
            DrawingHoleCalloutAnnotationOptions,
            true,
            duplicateDimensions,
            hiddenFeatureDimensions,
            usePlacementInSketch));

        AssignDrawingAnnotationsToLayers(drawingModel, drawing);

        var after = CountDrawingHoleCallouts(drawing);
        var imported = Math.Max(annotations.Length, Math.Max(0, after - before));
        if (imported == 0 && after == 0)
        {
            AddDrawingIssue(
                issues,
                "warning",
                "孔标注",
                "未导入新的孔标注，如模型含孔请复核",
                "通过 SolidWorks 孔标注模型项目导入，未使用圆边扫描");
        }
        else if (imported == 0)
        {
            AddDrawingIssue(
                issues,
                "info",
                "孔标注",
                "未导入新的孔标注，当前工程图已存在孔标注",
                "通过 SolidWorks 孔标注模型项目导入，未使用圆边扫描");
        }

        return imported;
    }

    private static int CountDrawingHoleCallouts(DrawingDoc drawing)
    {
        var count = 0;
        foreach (var view in GetModelDrawingViews(drawing))
        {
            var dimension = Safe(view.GetFirstDisplayDimension5);
            var guard = 0;
            while (dimension != null && guard++ < 10000)
            {
                if (Safe(() => dimension.IsHoleCallout()))
                    count++;

                var current = dimension;
                dimension = Safe(current.GetNext5);
            }
        }

        return count;
    }

    private static void AppendDrawingHoleCalloutCheck(
        DrawingDoc drawing,
        int dimensionCount,
        List<DrawingAutomationIssue> issues,
        DrawingHoleFeatureSummary sourceHoleFeatures = null)
    {
        var holeCalloutCount = CountDrawingHoleCallouts(drawing);
        if (sourceHoleFeatures != null && sourceHoleFeatures.FeatureGroups > 0)
        {
            if (holeCalloutCount == 0)
            {
                AddDrawingIssue(
                    issues,
                    "warning",
                    "孔标注",
                    $"检测到{sourceHoleFeatures.FeatureGroups}个孔特征组/{sourceHoleFeatures.HoleInstances}个孔点，但未检测到孔标注",
                    sourceHoleFeatures.BuildIssueDetail());
                return;
            }

            if (holeCalloutCount < sourceHoleFeatures.FeatureGroups)
            {
                AddDrawingIssue(
                    issues,
                    "warning",
                    "孔标注",
                    $"检测到{sourceHoleFeatures.FeatureGroups}个孔特征组，当前孔标注{holeCalloutCount}个，请复核是否漏标",
                    sourceHoleFeatures.BuildIssueDetail());
            }

            return;
        }

        if (dimensionCount <= 0 || holeCalloutCount > 0)
            return;

        AddDrawingIssue(
            issues,
            "info",
            "孔标注",
            "未检测到孔标注，如模型含孔请复核",
            "未使用圆边扫描");
    }

    private static DrawingHoleFeatureSummary GetModelHoleFeatureSummary(ModelDoc2 model)
    {
        var summary = new DrawingHoleFeatureSummary();
        var visitedModels = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var count = CountModelHoleFeatures(model, visitedModels, 0);
        summary.ScannedModels = visitedModels.Count > 0 ? visitedModels.Count : 1;
        summary.FeatureGroups = count.FeatureGroups;
        summary.HoleInstances = count.HoleInstances;
        foreach (var detail in count.Details.Take(6))
            summary.AddDetail(detail);

        return summary;
    }

    private static DrawingHoleFeatureCount CountModelHoleFeatures(
        ModelDoc2 model,
        HashSet<string> visitedModels,
        int depth)
    {
        var result = new DrawingHoleFeatureCount();
        if (model == null || depth > 4) return result;

        var identity = GetModelIdentity(model, "");
        if (!string.IsNullOrWhiteSpace(identity) && !visitedModels.Add(identity))
            return result;

        foreach (var feature in GetFeatureArray(model).OfType<Feature>())
        {
            if (Safe(feature.IsSuppressed)) continue;

            if (!TryGetHoleFeatureInfo(feature, out var displayName, out var holeInstances))
                continue;

            result.FeatureGroups++;
            result.HoleInstances += Math.Max(1, holeInstances);
            result.AddDetail(displayName + " x" + Math.Max(1, holeInstances));
        }

        if (Safe(model.GetType) == (int)swDocumentTypes_e.swDocASSEMBLY)
        {
            var configuration = Safe(() => model.GetActiveConfiguration() as Configuration);
            var rootComponent = Safe(() => configuration?.GetRootComponent() as Component2);
            foreach (var component in EnumerateComponents(GetComponentChildren(rootComponent)))
            {
                if (Safe(component.IsSuppressed)) continue;

                var childModel = Safe(() => component.GetModelDoc() as ModelDoc2);
                if (childModel == null) continue;

                var componentPath = Safe(childModel.GetPathName) ?? Safe(component.GetPathName) ?? "";
                var key = GetModelIdentity(childModel, componentPath);
                if (!string.IsNullOrWhiteSpace(key) && visitedModels.Contains(key))
                    continue;

                var childCount = CountModelHoleFeatures(childModel, visitedModels, depth + 1);
                result.FeatureGroups += childCount.FeatureGroups;
                result.HoleInstances += childCount.HoleInstances;
                foreach (var detail in childCount.Details)
                    result.AddDetail(detail);
            }
        }

        return result;
    }

    private static bool TryGetHoleFeatureInfo(Feature feature, out string displayName, out int holeInstances)
    {
        displayName = "";
        holeInstances = 0;
        if (feature == null) return false;

        var typeName = Safe(feature.GetTypeName2) ?? Safe(feature.GetTypeName) ?? "";
        if (!IsSolidWorksHoleFeatureType(typeName))
            return false;

        var featureName = Safe(() => feature.Name) ?? typeName;
        var definition = Safe(feature.GetSpecificFeature2) ?? Safe(feature.GetDefinition);
        var kind = GetHoleFeatureKind(typeName, definition);
        holeInstances = GetHoleFeatureInstanceCount(typeName, definition);
        displayName = string.IsNullOrWhiteSpace(featureName) ? kind : featureName + "/" + kind;
        return true;
    }

    private static bool IsSolidWorksHoleFeatureType(string typeName)
    {
        if (string.IsNullOrWhiteSpace(typeName)) return false;

        var value = typeName.Trim();
        return value.Equals("HoleWzd", StringComparison.OrdinalIgnoreCase) ||
               value.Equals("HoleWzdLegacy", StringComparison.OrdinalIgnoreCase) ||
               value.Equals("HoleSeries", StringComparison.OrdinalIgnoreCase) ||
               value.Equals("SimpleHole", StringComparison.OrdinalIgnoreCase) ||
               value.Equals("AdvHole", StringComparison.OrdinalIgnoreCase) ||
               value.StartsWith("HoleWzd", StringComparison.OrdinalIgnoreCase);
    }

    private static string GetHoleFeatureKind(string typeName, object definition)
    {
        if (definition is WizardHoleFeatureData || definition is WizardHoleFeatureData2)
            return "孔向导";
        if (definition is SimpleHoleFeatureData || definition is SimpleHoleFeatureData2)
            return "简单孔";
        if (definition is AdvancedHoleFeatureData)
            return "异形孔";
        if (definition is HoleSeriesFeatureData || definition is HoleSeriesFeatureData2)
            return "孔系列";

        if (typeName.IndexOf("HoleWzd", StringComparison.OrdinalIgnoreCase) >= 0)
            return "孔向导";
        if (typeName.Equals("SimpleHole", StringComparison.OrdinalIgnoreCase))
            return "简单孔";
        if (typeName.Equals("AdvHole", StringComparison.OrdinalIgnoreCase))
            return "异形孔";
        if (typeName.Equals("HoleSeries", StringComparison.OrdinalIgnoreCase))
            return "孔系列";

        return typeName;
    }

    private static int GetHoleFeatureInstanceCount(string typeName, object definition)
    {
        if (definition is WizardHoleFeatureData2 wizardHole)
        {
            var count = Safe(wizardHole.GetSketchPointCount);
            if (count > 0) return count;
            count = ToObjectArray(Safe(wizardHole.GetSketchPoints)).Length;
            return count > 0 ? count : 1;
        }

        if (definition is HoleSeriesFeatureData holeSeries)
        {
            var count = Safe(holeSeries.GetSketchPointsCount);
            if (count > 0) return count;
            count = Safe(() => holeSeries.FastenerHoleCount);
            return count > 0 ? count : 1;
        }

        if (definition is HoleSeriesFeatureData2 holeSeries2)
        {
            var count = Safe(holeSeries2.GetSketchPointsCount);
            if (count > 0) return count;
            count = Safe(() => holeSeries2.FastenerHoleCount);
            return count > 0 ? count : 1;
        }

        return 1;
    }

    private static string GetModelIdentity(ModelDoc2 model, string fallbackPath)
    {
        var path = Safe(model.GetPathName) ?? fallbackPath ?? "";
        if (!string.IsNullOrWhiteSpace(path))
            return path;

        return Safe(model.GetTitle) ?? "";
    }

    private static string SaveDrawingAutomationDocument(
        ModelDoc2 drawingModel,
        string sourcePath,
        Dictionary<string, object> args)
    {
        if (!GetArgBool(args, "saveDrawing")) return "";

        var targetPath = Path.ChangeExtension(sourcePath, ".SLDDRW");
        if (File.Exists(targetPath) && !GetArgBool(args, "overwriteDrawing"))
            throw CommandFailure("target_file_exists", "path", targetPath);

        var extension = drawingModel.Extension;
        if (extension == null)
            throw CommandFailure("document_extension_unavailable");

        var errors = 0;
        var warnings = 0;
        var ok = extension.SaveAs3(
            targetPath,
            0,
            (int)swSaveAsOptions_e.swSaveAsOptions_Silent,
            null,
            null,
            ref errors,
            ref warnings);

        if (!ok || errors != 0)
            throw CommandFailure("save_new_file_failed", "errors", errors, "warnings", warnings, "path", targetPath);

        return targetPath;
    }

    private void AppendDrawingAutomationCheckIssues(
        ModelDoc2 drawingModel,
        DrawingDoc drawing,
        Dictionary<string, object> args,
        List<DrawingAutomationIssue> issues,
        DrawingHoleFeatureSummary sourceHoleFeatures = null)
    {
        if (drawing == null)
        {
            AddDrawingIssue(issues, "error", "工程图", "无法获取工程图对象", "");
            return;
        }

        var sheetNames = ToStringArray(drawing.GetSheetNames());
        if (sheetNames.Length == 0)
            AddDrawingIssue(issues, "error", "图纸", "未发现图纸页", "");

        var modelViews = EnumerateDrawingViews(drawing)
            .Where(view => !string.IsNullOrWhiteSpace(GetViewReferencedModelPath(view)))
            .ToArray();
        if (modelViews.Length == 0)
            AddDrawingIssue(issues, "warning", "模型视图", "未发现关联模型视图", "");

        var dimensionCount = CountDrawingDisplayDimensions(drawing);
        if (dimensionCount == 0)
            AddDrawingIssue(issues, "warning", "模型项目", "未发现尺寸标注", "");

        AppendDrawingHoleCalloutCheck(drawing, dimensionCount, issues, sourceHoleFeatures);

        var danglingAnnotations = CountDanglingAnnotations(drawing);
        if (danglingAnnotations > 0)
            AddDrawingIssue(issues, "warning", "悬空标注", "存在悬空标注", danglingAnnotations.ToString());

        var sheetFormatPath = GetArgString(args, "sheetFormatPath");
        if (GetArgBool(args, "applyDrawingSettings", true) &&
            !string.IsNullOrWhiteSpace(sheetFormatPath) &&
            File.Exists(sheetFormatPath) &&
            !IsSheetFormatApplied(drawing, sheetFormatPath))
        {
            AddDrawingIssue(issues, "warning", "图纸格式", "当前图纸格式与独立设置不一致", sheetFormatPath);
        }

        var drawingPath = Safe(drawingModel.GetPathName) ?? "";
        if (string.IsNullOrWhiteSpace(drawingPath))
            AddDrawingIssue(issues, "info", "保存", "当前工程图尚未保存", "");
    }

    private static IEnumerable<View> EnumerateDrawingViews(DrawingDoc drawing)
    {
        var view = Safe(() => drawing.GetFirstView() as View);
        var guard = 0;
        while (view != null && guard++ < 1000)
        {
            yield return view;
            var current = view;
            view = Safe(() => current.GetNextView() as View);
        }
    }

    private static bool IsCurrentSheetFirstAngle(DrawingDoc drawing)
    {
        var sheet = drawing.GetCurrentSheet() as Sheet;
        var properties = ToObjectArray(Safe(sheet.GetProperties));
        return GetSheetPropertyBool(properties, 4, false);
    }

    private static int CountDrawingModelViews(DrawingDoc drawing)
    {
        return EnumerateDrawingViews(drawing)
            .Count(view => !string.IsNullOrWhiteSpace(GetViewReferencedModelPath(view)));
    }

    private static string GetViewReferencedModelPath(View view)
    {
        var path = Safe(view.GetReferencedModelName) ?? "";
        if (!string.IsNullOrWhiteSpace(path)) return path;

        var referencedModel = Safe(() => view.ReferencedDocument as ModelDoc2);
        return Safe(() => referencedModel?.GetPathName()) ?? "";
    }

    private static string GetDrawingViewName(View view)
    {
        return Safe(view.GetName2) ?? Safe(() => view.Name) ?? "";
    }

    private static int CountDrawingDisplayDimensions(DrawingDoc drawing)
    {
        var count = 0;
        foreach (var view in EnumerateDrawingViews(drawing))
        {
            var dimension = Safe(view.GetFirstDisplayDimension5);
            var guard = 0;
            while (dimension != null && guard++ < 10000)
            {
                count++;
                var current = dimension;
                dimension = Safe(current.GetNext5);
            }
        }

        return count;
    }

    private static int CountDanglingAnnotations(DrawingDoc drawing)
    {
        var count = 0;
        foreach (var view in EnumerateDrawingViews(drawing))
        {
            var annotation = Safe(view.GetFirstAnnotation3);
            var guard = 0;
            while (annotation != null && guard++ < 10000)
            {
                if (Safe(annotation.IsDangling))
                    count++;

                var current = annotation;
                annotation = Safe(current.GetNext3);
            }
        }

        return count;
    }

    private static bool IsSheetFormatApplied(DrawingDoc drawing, string sheetFormatPath)
    {
        var sheetNames = ToStringArray(drawing.GetSheetNames());
        var originalSheetName = Safe(() => ((Sheet)drawing.GetCurrentSheet())?.GetName()) ?? "";
        var applied = false;

        foreach (var sheetName in sheetNames)
        {
            if (!drawing.ActivateSheet(sheetName)) continue;
            var sheet = drawing.GetCurrentSheet() as Sheet;
            var templateName = Safe(() => sheet?.GetTemplateName()) ?? "";
            if (IsSameReferencePath(templateName, sheetFormatPath))
            {
                applied = true;
                break;
            }
        }

        if (!string.IsNullOrWhiteSpace(originalSheetName))
            drawing.ActivateSheet(originalSheetName);

        return applied;
    }

    private static string BuildDrawingAutomationSummary(
        string prefix,
        int viewsInserted,
        int annotationsImported,
        List<DrawingAutomationIssue> issues,
        string scaleText = "",
        int arrangedAnnotations = 0)
    {
        var warnings = issues.Count(item => string.Equals(item.severity, "warning", StringComparison.OrdinalIgnoreCase));
        var errors = issues.Count(item => string.Equals(item.severity, "error", StringComparison.OrdinalIgnoreCase));
        var parts = new List<string> { prefix };
        if (viewsInserted > 0) parts.Add("视图 " + viewsInserted);
        if (!string.IsNullOrWhiteSpace(scaleText)) parts.Add("比例 " + scaleText);
        if (annotationsImported > 0) parts.Add("模型项目 " + annotationsImported);
        if (arrangedAnnotations > 0) parts.Add("重排 " + arrangedAnnotations);
        if (warnings > 0) parts.Add("警告 " + warnings);
        if (errors > 0) parts.Add("错误 " + errors);
        return string.Join("，", parts);
    }

    private static string BuildHoleCalloutImportSummary(
        int imported,
        int total,
        List<DrawingAutomationIssue> issues)
    {
        var warnings = issues.Count(item => string.Equals(item.severity, "warning", StringComparison.OrdinalIgnoreCase));
        var errors = issues.Count(item => string.Equals(item.severity, "error", StringComparison.OrdinalIgnoreCase));
        var parts = new List<string> { "孔标注完成" };
        parts.Add("当前 " + total);
        if (imported > 0) parts.Add("新增 " + imported);
        if (warnings > 0) parts.Add("警告 " + warnings);
        if (errors > 0) parts.Add("错误 " + errors);
        return string.Join("，", parts);
    }

    private static void AddDrawingIssue(
        List<DrawingAutomationIssue> issues,
        string severity,
        string item,
        string message,
        string detail)
    {
        issues.Add(new DrawingAutomationIssue
        {
            severity = severity,
            item = item,
            message = message,
            detail = detail
        });
    }

}
