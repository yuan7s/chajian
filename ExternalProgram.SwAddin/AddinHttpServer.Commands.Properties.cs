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
    private object ReadProperties(Dictionary<string, object> args)
    {
        var target = ResolvePropertyTarget(args);
        if (target.FileProperties != null)
            return new
            {
                title = target.Title,
                path = target.Path,
                configuration = string.IsNullOrWhiteSpace(target.ConfigurationName)
                    ? "custom"
                    : target.ConfigurationName,
                source = target.Source,
                selectedComponent = target.SelectedComponent,
                properties = target.FileProperties
            };

        var manager = GetCustomPropertyManager(target.Model, target.ConfigurationName);
        if (manager == null) throw CommandFailure("property_manager_unavailable");

        var values = new Dictionary<string, string>();
        var rawValues = new Dictionary<string, string>();

        try
        {
            foreach (var name in GetPropertyNames(manager))
                try
                {
                    manager.Get5(name, false, out var rawValue, out var resolvedValue, out _);
                    rawValues[name] = rawValue ?? "";
                    values[name] = !string.IsNullOrWhiteSpace(resolvedValue) ? resolvedValue : rawValue ?? "";
                }
                catch (Exception ex)
                {
                    LogIgnoredException("ReadProperties.GetValue", ex);
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
            rawProperties = rawValues,
            properties = values
        };
    }

    // --- Write Properties ---

    private object WriteProperties(Dictionary<string, object> args)
    {
        var target = ResolvePropertyTarget(args);
        if (target.FileProperties != null) throw CommandFailure("lightweight_component_write_unsupported");

        // 在同一次主线程命令内校验目标，避免编辑期间切换零件导致误写。
        if (args.ContainsKey("expectedPath") &&
            (!string.Equals(GetArgString(args, "expectedPath"), target.Path ?? "", StringComparison.OrdinalIgnoreCase) ||
             !string.Equals(GetArgString(args, "expectedTitle"), target.Title ?? "", StringComparison.Ordinal) ||
             !string.Equals(GetArgString(args, "expectedConfiguration"), string.IsNullOrWhiteSpace(target.ConfigurationName) ? "custom" : target.ConfigurationName, StringComparison.Ordinal)))
            throw CommandFailure("property_target_changed");


        var propsArg = args.TryGetValue("properties", out var p) ? p : null;
        var properties = propsArg as Dictionary<string, object>;
        if (properties == null) throw CommandFailure("argument_required", "name", "properties");

        var manager = GetCustomPropertyManager(target.Model, target.ConfigurationName);
        if (manager == null) throw CommandFailure("property_manager_unavailable");

        var written = new List<string>();
        foreach (var kvp in properties)
        {
            var result = manager.Add3(kvp.Key, 30, kvp.Value?.ToString() ?? "", 2);
            if (result != 0)
                throw CommandFailure("property_write_failed", "name", kvp.Key, "result", result);
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
        var title = Safe(activeModel.GetTitle) ?? "";
        var path = Safe(activeModel.GetPathName) ?? "";

        var comp = GetSelectedComponent(activeModel);
        if (comp != null)
        {
            var componentModel = Safe(() => comp.GetModelDoc() as ModelDoc2) ??
                                 Safe(() => comp.GetModelDoc2() as ModelDoc2);
            if (componentModel != null)
            {
                targetModel = componentModel;
                selectedComponent = true;
                title = Safe(() => comp.Name2) ?? Safe(componentModel.GetTitle) ?? title;
                path = Safe(componentModel.GetPathName) ?? path;
            }
            else
            {
                selectedComponent = true;
                title = Safe(() => comp.Name2) ?? title;
                path = Safe(comp.GetPathName) ?? "";
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
            configName = selectedComponent
                ? Safe(() => comp.ReferencedConfiguration) ?? ""
                : GetActiveConfigurationName(targetModel);

        Dictionary<string, string> fileProperties = null;
        if (selectedComponent && targetModel == activeModel && !string.IsNullOrWhiteSpace(path))
            fileProperties = ReadFileProperties(path, useCustomProperties ? "" : configName);

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
            throw CommandFailure("component_file_property_path_invalid", "path", path);

        ISwDMDocument document = null;

        try
        {
            var application = CreateDocumentManagerApplication();
            var documentType = GetDocumentManagerType(path);
            if (documentType == SwDmDocumentType.swDmDocumentUnknown)
                throw CommandFailure("unsupported_solidworks_file_type", "path", path);

            document = application.GetDocument(path, documentType, true, out var error);
            if (document == null || error != SwDmDocumentOpenError.swDmDocumentOpenErrorNone)
                throw CommandFailure("document_manager_open_failed", "error", error.ToString(), "path", path);

            return string.IsNullOrWhiteSpace(configurationName)
                ? ReadDocumentManagerCustomProperties(document)
                : ReadDocumentManagerConfigurationProperties(document, configurationName);
        }
        finally
        {
            try
            {
                document?.CloseDoc();
            }
            catch (Exception ex)
            {
                LogIgnoredException("ReadFileProperties.CloseDoc", ex);
            }
        }
    }

    private static SwDMApplication CreateDocumentManagerApplication()
    {
        var factory = new SwDMClassFactoryClass();
        foreach (var license in LoadDocumentManagerLicenseCandidates())
            try
            {
                var application = factory.GetApplication(license);
                if (application != null) return application;
            }
            catch
            {
                // Try the next normalized license token.
            }

        throw CommandFailure("document_manager_unavailable");
    }

    private static IEnumerable<string> LoadDocumentManagerLicenseCandidates()
    {
        const string embeddedLicense =
            "SOLIDWORKS_2022:swdocmgr_general-11785-02051-00064-50177-06612-36864-49921-00003-21424-27712-02333-41243-17858-56070-14468-62467-35817-38164-41304-28396-16118-35795-11666-40614-37528-44680-42142-42646-25790-25696-00100-12572-11577-30049-11623-12338-12338-4,swdocmgr_previews-11785-02051-00064-50177-06612-36864-49921-00003-55360-53060-25343-42769-59069-52473-22473-52225-12074-04081-32909-30728-28589-33225-11507-40614-37528-44680-42142-42646-25790-25696-00100-12572-11577-30049-11623-12338-12338-3,swdocmgr_geometry-11785-02051-00064-50177-06612-36864-49921-00003-47348-23774-43673-03705-12160-02780-32459-34818-63205-17331-46285-63135-49863-07899-11787-40614-37528-44680-42142-42646-25790-25696-00100-12572-11577-30049-11623-12338-12338-2,swdocmgr_dimxpert-11785-02051-00064-50177-06612-36864-49921-00003-29996-08472-47529-56591-62456-46237-10843-31747-32567-27053-59927-17093-07167-05996-11461-40614-37528-44680-42142-42646-25790-25696-00100-12572-11577-30049-11623-12338-12338-7,swdocmgr_tessellation-11785-02051-00064-50177-06612-36864-49921-00003-10436-56910-32567-34976-20928-11850-62944-24577-40211-48738-24555-37037-32570-31276-11315-40614-37528-44680-42142-42646-25790-25696-00100-12572-11577-30049-11623-12338-12338-9,swdocmgr_xml-11785-02051-00064-50177-06612-36864-49921-00003-42016-05489-35279-24819-52264-14236-16400-48131-43581-35953-11290-28017-22653-22728-11882-40614-37528-44680-42142-42646-25790-25696-00100-12572-11577-30049-11623-12338-12338-8";

        if (string.IsNullOrWhiteSpace(embeddedLicense)) yield break;

        yield return embeddedLicense;
        foreach (var entry in embeddedLicense.Split([','], StringSplitOptions.RemoveEmptyEntries))
        {
            var trimmed = entry.Trim();
            if (trimmed.Length == 0) continue;

            var parts = trimmed.Split(['-'], 2, StringSplitOptions.None);
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
            try
            {
                var value = document is ISwDMDocument5 document5
                    ? document5.GetCustomPropertyValues(name, out _, out _)
                    : document.GetCustomProperty(name, out _);
                values[name] = value ?? string.Empty;
            }
            catch (Exception ex)
            {
                LogIgnoredException("ReadDocumentManagerCustomProperties", ex);
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
            try
            {
                var value = configuration is ISwDMConfiguration5 configuration5
                    ? configuration5.GetCustomPropertyValues(name, out _, out _)
                    : configuration.GetCustomProperty(name, out _);
                values[name] = value ?? string.Empty;
            }
            catch (Exception ex)
            {
                LogIgnoredException("ReadDocumentManagerConfigurationProperties", ex);
            }

        return values;
    }

    private static ISwDMConfiguration ResolveDocumentManagerConfiguration(ISwDMDocument document,
        string configurationName)
    {
        if (document?.ConfigurationManager == null) return null;

        foreach (var candidate in GetDocumentManagerConfigurationCandidates(document, configurationName))
        {
            if (document.ConfigurationManager.GetConfigurationByName(candidate) is ISwDMConfiguration configuration)
                return configuration;
        }

        return null;
    }

    private static IEnumerable<string> GetDocumentManagerConfigurationCandidates(ISwDMDocument document,
        string configurationName)
    {
        if (!string.IsNullOrWhiteSpace(configurationName)) yield return configurationName;

        foreach (var name in GetDocumentManagerPropertyNames(document.ConfigurationManager.GetConfigurationNames))
            if (!string.IsNullOrWhiteSpace(name) &&
                !string.Equals(name, configurationName, StringComparison.OrdinalIgnoreCase))
                yield return name;
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
                if (!string.IsNullOrWhiteSpace(name))
                    yield return name;
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

    // --- Get Bounding Box ---

    private object GetBoundingBox(Dictionary<string, object> args)
    {
        var model = GetActiveModel();
        var configName = GetArgString(args, "configuration");

        if (!string.IsNullOrWhiteSpace(configName))
            try
            {
                model.ShowConfiguration2(configName);
            }
            catch (Exception ex)
            {
                LogIgnoredException("GetBoundingBox.ShowConfiguration", ex);
            }

        var part = GetPartDoc(model);
        if (part == null) throw CommandFailure("part_required_for_bounding_box");

        var box = part.GetPartBox(false) as double[];
        if (box == null || box.Length < 6) throw CommandFailure("bounding_box_unavailable");

        return new
        {
            x1 = box[0], y1 = box[1], z1 = box[2],
            x2 = box[3], y2 = box[4], z2 = box[5],
            dx = box[3] - box[0],
            dy = box[4] - box[1],
            dz = box[5] - box[2]
        };
    }

    // ──────────── 共享删除属性方法 ────────────

    private static DeletePropertyStats DeletePropertiesRecursive(ModelDoc2 model, bool deleteConfigurationProperties,
        HashSet<string> processed)
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
                configurationNames = [activeConfiguration];
        }

        foreach (var configurationName in configurationNames)
            deleted += DeleteManagerProperties(GetCustomPropertyManager(model, configurationName));

        return deleted;
    }

    private static int DeleteManagerProperties(CustomPropertyManager manager)
    {
        if (manager == null) return 0;

        var deleted = 0;
        foreach (var name in GetPropertyNames(manager).ToArray())
            try
            {
                manager.Delete2(name);
                deleted++;
            }
            catch (Exception ex)
            {
                LogIgnoredException("DeleteManagerProperties", ex);
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
        var path = Safe(model.GetPathName) ?? "";
        if (!string.IsNullOrWhiteSpace(path)) return path;

        var title = Safe(model.GetTitle) ?? "";
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

    // --- Sync Coding Props ---
}
