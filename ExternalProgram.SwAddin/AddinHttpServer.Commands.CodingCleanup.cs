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
