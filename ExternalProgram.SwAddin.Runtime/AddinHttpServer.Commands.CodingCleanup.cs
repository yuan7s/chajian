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
            throw CommandFailure("assembly_required");

        var configuration = model.GetActiveConfiguration() as Configuration;
        if (configuration == null)
            throw CommandFailure("assembly_configuration_unavailable");

        var rootComponent = configuration.GetRootComponent() as Component2;
        if (rootComponent == null)
            throw CommandFailure("assembly_root_component_unavailable");

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
                var excludedByFlag =
                    IsExcludedCodingCleanupComponent(childModel, configName, excludeStandard, excludePurchased);
                var matchesNameFilter = string.IsNullOrWhiteSpace(nameFilter) ||
                                        string.IsNullOrWhiteSpace(childName) ||
                                        childName.IndexOf(nameFilter, StringComparison.OrdinalIgnoreCase) >= 0;
                var shouldProcessCurrent = matchesNameFilter &&
                                           !excludedByVirtual &&
                                           !excludedByFlag &&
                                           ((isPart && processPart) || (isAssembly && processAsm));

                if (shouldProcessCurrent)
                {
                    var title = Safe(childModel.GetTitle) ?? "";
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
                                results.Add(
                                    new { name = childName, title, materialCode, partNumber, action = "synced" });
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
                    Safe(() => model.ShowConfiguration2(activeConfigName));
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

    private static bool IsExcludedCodingCleanupComponent(ModelDoc2 model, string configName, bool excludeStandard,
        bool excludePurchased)
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

    private static bool HasPositiveCodingCleanupFlag(ModelDoc2 model, string configName,
        IEnumerable<string> propertyNames)
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

    private sealed class DrawingViewSpec
    {
        public DrawingViewSpec(string displayName, double xRatio, double yRatio, params string[] orientationNames)
        {
            DisplayName = displayName;
            XRatio = xRatio;
            YRatio = yRatio;
            OrientationNames = orientationNames ?? Array.Empty<string>();
        }

        public string DisplayName { get; }
        public double XRatio { get; }
        public double YRatio { get; }
        public string[] OrientationNames { get; }
    }

    private sealed class DrawingViewSlotSpec
    {
        public DrawingViewSlotSpec(string displayName, double centerXRatio, double centerYRatio)
        {
            DisplayName = displayName;
            CenterXRatio = centerXRatio;
            CenterYRatio = centerYRatio;
        }

        public string DisplayName { get; }
        public double CenterXRatio { get; }
        public double CenterYRatio { get; }
    }

    private sealed class DrawingViewSlot
    {
        public DrawingViewSlot(
            DrawingViewSlotSpec spec,
            double centerX,
            double centerY,
            double minX,
            double maxX,
            double minY,
            double maxY)
        {
            Spec = spec;
            CenterX = centerX;
            CenterY = centerY;
            MinX = minX;
            MaxX = maxX;
            MinY = minY;
            MaxY = maxY;
        }

        public DrawingViewSlotSpec Spec { get; }
        public double CenterX { get; }
        public double CenterY { get; }
        public double MinX { get; }
        public double MaxX { get; }
        public double MinY { get; }
        public double MaxY { get; }
        public double Width => Math.Max(0.0, MaxX - MinX);
        public double Height => Math.Max(0.0, MaxY - MinY);
    }

    private sealed class DrawingScaleRatio
    {
        public DrawingScaleRatio(int numerator, int denominator)
        {
            Numerator = numerator;
            Denominator = denominator <= 0 ? 1 : denominator;
        }

        public int Numerator { get; }
        public int Denominator { get; }
        public double DecimalValue => (double)Numerator / Denominator;
        public string DisplayText => Numerator + ":" + Denominator;
    }

    private sealed class DrawingScaleLayoutResult
    {
        public bool adjusted { get; set; }
        public string scaleText { get; set; } = "";
        public int viewsMoved { get; set; }
    }

    private sealed class DrawingViewLayoutPlan
    {
        public DrawingViewLayoutPlan(DrawingScaleRatio scale, DrawingViewSlot[] slots, double fillRatio)
        {
            Scale = scale;
            Slots = slots ?? Array.Empty<DrawingViewSlot>();
            FillRatio = fillRatio;
        }

        public DrawingScaleRatio Scale { get; }
        public DrawingViewSlot[] Slots { get; }
        public double FillRatio { get; }
    }

    private readonly struct DrawingViewScaledSize
    {
        public static readonly DrawingViewScaledSize Invalid = new DrawingViewScaledSize(0.0, 0.0, 0.0, 0.0, false);

        public DrawingViewScaledSize(double width, double height)
            : this(width, height, 0.0, 0.0, true)
        {
        }

        public DrawingViewScaledSize(double width, double height, double centerOffsetX, double centerOffsetY)
            : this(width, height, centerOffsetX, centerOffsetY, true)
        {
        }

        private DrawingViewScaledSize(
            double width,
            double height,
            double centerOffsetX,
            double centerOffsetY,
            bool isValid)
        {
            Width = width;
            Height = height;
            CenterOffsetX = centerOffsetX;
            CenterOffsetY = centerOffsetY;
            IsValid = isValid && width > 0 && height > 0;
        }

        public double Width { get; }
        public double Height { get; }
        public double CenterOffsetX { get; }
        public double CenterOffsetY { get; }
        public bool IsValid { get; }

        public DrawingViewScaledSize Scale(double scale)
        {
            return IsValid
                ? new DrawingViewScaledSize(
                    Width * scale,
                    Height * scale,
                    CenterOffsetX * scale,
                    CenterOffsetY * scale)
                : Invalid;
        }
    }

    private readonly struct DrawingSheetSize
    {
        public DrawingSheetSize(double width, double height)
        {
            Width = width;
            Height = height;
        }

        public double Width { get; }
        public double Height { get; }
    }

    private readonly struct DrawingRect
    {
        public static readonly DrawingRect Invalid = new DrawingRect(0.0, 0.0, 0.0, 0.0, false);

        public DrawingRect(double minX, double minY, double maxX, double maxY)
            : this(minX, minY, maxX, maxY, true)
        {
        }

        private DrawingRect(double minX, double minY, double maxX, double maxY, bool isValid)
        {
            MinX = minX;
            MinY = minY;
            MaxX = maxX;
            MaxY = maxY;
            IsValid = isValid && maxX > minX && maxY > minY;
        }

        public double MinX { get; }
        public double MinY { get; }
        public double MaxX { get; }
        public double MaxY { get; }
        public bool IsValid { get; }
        public double Width => Math.Max(0.0, MaxX - MinX);
        public double Height => Math.Max(0.0, MaxY - MinY);
    }

    private readonly struct DrawingPoint
    {
        public static readonly DrawingPoint Invalid = new DrawingPoint(0.0, 0.0, 0.0, false);

        public DrawingPoint(double x, double y, double z, bool isValid)
        {
            X = x;
            Y = y;
            Z = z;
            IsValid = isValid;
        }

        public double X { get; }
        public double Y { get; }
        public double Z { get; }
        public bool IsValid { get; }
    }

    private enum DrawingAnnotationSide
    {
        Top,
        Bottom,
        Left,
        Right
    }

    private sealed class DrawingDimensionPlacement
    {
        public DrawingDimensionPlacement(
            Annotation annotation,
            DrawingPoint position,
            DrawingAnnotationSide side)
        {
            Annotation = annotation;
            Position = position;
            Side = side;
        }

        public Annotation Annotation { get; }
        public DrawingPoint Position { get; }
        public DrawingAnnotationSide Side { get; }
    }

    private sealed class DrawingHoleFeatureSummary
    {
        private readonly List<string> _details = new List<string>();

        public int ScannedModels { get; set; }
        public int SkippedModels { get; set; }
        public int FeatureGroups { get; set; }
        public int HoleInstances { get; set; }

        public void AddDetail(string detail)
        {
            if (!string.IsNullOrWhiteSpace(detail) && _details.Count < 8)
                _details.Add(detail);
        }

        public string BuildIssueDetail()
        {
            var parts = new List<string>
            {
                "按孔特征检查，未使用圆边扫描",
                "模型 " + ScannedModels,
                "跳过 " + SkippedModels
            };
            if (_details.Count > 0)
                parts.Add(string.Join("; ", _details));

            return string.Join("；", parts);
        }
    }

    private sealed class DrawingHoleFeatureCount
    {
        private readonly List<string> _details = new List<string>();

        public int FeatureGroups { get; set; }
        public int HoleInstances { get; set; }
        public IReadOnlyList<string> Details => _details;

        public void AddDetail(string detail)
        {
            if (!string.IsNullOrWhiteSpace(detail) && _details.Count < 12)
                _details.Add(detail);
        }
    }

    // ReSharper disable InconsistentNaming
    private sealed class DrawingAutomationIssue
    {
        public string severity { get; set; }
        public string item { get; set; }
        public string message { get; set; }
        public string detail { get; set; }
    }
    // ReSharper restore InconsistentNaming

    // ReSharper disable InconsistentNaming
    // ReSharper disable UnusedAutoPropertyAccessor.Local
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

        public static DrawingCopyResult Copied(string sourcePath, string path, string replacedReference = null,
            string sourceReference = null)
        {
            return new DrawingCopyResult
            {
                attempted = true, success = true, skipped = false, sourcePath = sourcePath, path = path,
                replacedReference = replacedReference, sourceReference = sourceReference
            };
        }

        public static DrawingCopyResult Failed(string sourcePath, string path, string error)
        {
            return new DrawingCopyResult
            {
                attempted = true, success = false, skipped = false, sourcePath = sourcePath, path = path, error = error
            };
        }
    }
    // ReSharper restore UnusedAutoPropertyAccessor.Local
    // ReSharper restore InconsistentNaming

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
}
