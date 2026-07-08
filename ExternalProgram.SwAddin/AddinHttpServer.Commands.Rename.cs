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
    // --- Rename Component ---

    private object GetRenameTargetInfo()
    {
        var target = ResolveRenameTarget(true);
        return CreateRenameTargetInfo(target);
    }

    private object CheckNameConflict(Dictionary<string, object> args)
    {
        var target = ResolveRenameTarget(true);
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
        var target = ResolveRenameTarget(true);
        var newBaseName = NormalizeNewBaseName(GetArgString(args, "newName"));
        var destPath = BuildRenameDestinationPath(target, newBaseName);
        var copyDrawing = GetArgBool(args, "copyDrawing", true);
        EnsureRenameDestinationAvailable(destPath);

        SaveModelCopy(target.Model, destPath);
        var drawing = CopyRelatedDrawing(target.Path, destPath, copyDrawing);

        var newModel = OpenModelForRename(destPath, true);
        ApplyRenameProperties(newModel, target.Component, args, newBaseName);
        SaveModel(newModel);
        ActivateModel(newModel);

        return new
        {
            success = true,
            path = destPath,
            title = Safe(newModel.GetTitle) ?? Path.GetFileName(destPath),
            selectedComponent = target.SelectedComponent,
            drawing
        };
    }

    private object RenameComponent(Dictionary<string, object> args)
    {
        var target = ResolveRenameTarget(true);
        var newBaseName = NormalizeNewBaseName(GetArgString(args, "newName"));
        var destPath = BuildRenameDestinationPath(target, newBaseName);
        EnsureRenameDestinationAvailable(destPath);

        var oldPath = target.Path;
        var copyDrawing = GetArgBool(args, "copyDrawing", true);
        var renameError = RenameDocumentInSolidWorks(target, newBaseName);
        if (renameError != (int)swRenameDocumentError_e.swRenameDocumentError_None)
            throw CommandFailure("solidworks_rename_failed", "error", renameError);

        SaveRenamedDocumentBeforeProperties(target);
        ApplyRenameProperties(target.Model, target.Component, args, newBaseName);
        SaveRenamePropertyChanges(target);

        var drawing = CopyRelatedDrawing(oldPath, destPath, copyDrawing);

        return new
        {
            success = true,
            oldName = target.BaseName,
            newName = newBaseName,
            oldPath,
            path = destPath,
            directRename = true,
            replaced = false,
            drawing
        };
    }

    private object SaveAsReplace(Dictionary<string, object> args)
    {
        var target = ResolveRenameTarget(true);
        if (!target.SelectedComponent || target.Component == null)
            throw CommandFailure("assembly_component_selection_required");

        var assemblyDoc = GetAssemblyDoc(target.ActiveModel);
        if (assemblyDoc == null)
            throw CommandFailure("assembly_doc_unavailable");

        var newBaseName = NormalizeNewBaseName(GetArgString(args, "newName"));
        var destPath = BuildRenameDestinationPath(target, newBaseName);
        var copyDrawing = GetArgBool(args, "copyDrawing", true);
        EnsureRenameDestinationAvailable(destPath);

        SaveModelCopy(target.Model, destPath);
        var drawing = CopyRelatedDrawing(target.Path, destPath, copyDrawing);

        var newModel = OpenModelForRename(destPath);
        ApplyRenameProperties(newModel, target.Component, args, newBaseName);
        SaveModel(newModel);
        var activateErrors = 0;
        Safe(() => _swApp.ActivateDoc3(Safe(() => target.ActiveModel.GetTitle()) ?? "", false, 0, ref activateErrors));

        target.ActiveModel.ClearSelection2(true);
        if (!Safe(() => target.Component.Select4(false, null, false)))
            throw CommandFailure("replace_component_reselect_failed");

        var configurationName = Safe(() => target.Component.ReferencedConfiguration) ?? "";
        var replaced = assemblyDoc.ReplaceComponents2(destPath, configurationName, false, 0, true);
        if (!replaced)
            throw CommandFailure("replace_component_failed");

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
                throw CommandFailure("rename_component_select_failed");

            var assemblyExtension = target.ActiveModel.Extension;
            if (assemblyExtension == null)
                throw CommandFailure("assembly_extension_unavailable");

            return assemblyExtension.RenameDocument(newBaseName);
        }

        var extension = target.Model.Extension;
        if (extension == null)
            throw CommandFailure("document_extension_unavailable");

        return extension.RenameDocument(newBaseName);
    }

    private static void SaveRenamedDocumentBeforeProperties(RenameTarget target)
    {
        if (target.SelectedComponent)
        {
            target.ActiveModel.EditRebuild3();
            MarkDocDirty(target.Model);
            MarkDocDirty(target.ActiveModel);
            SaveModel(target.ActiveModel, "save_assembly_failed", true);
            return;
        }

        SaveModel(target.Model);
    }

    private static void SaveRenamePropertyChanges(RenameTarget target)
    {
        if (target.SelectedComponent)
        {
            target.ActiveModel.EditRebuild3();
            MarkDocDirty(target.Model);
            MarkDocDirty(target.ActiveModel);
            SaveModel(target.ActiveModel, "save_assembly_failed", true);
            return;
        }

        SaveModel(target.Model);
    }

    private static void ApplyRenameProperties(ModelDoc2 targetModel, Component2 component,
        Dictionary<string, object> args, string newName)
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
            throw CommandFailure(selectedComponent
                ? "selected_component_must_be_saved_for_rename"
                : "document_must_be_saved_for_rename");

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
            BaseName = baseName,
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
            throw CommandFailure("new_file_name_required");

        var trimmed = Path.GetFileNameWithoutExtension(newName.Trim());
        if (string.IsNullOrWhiteSpace(trimmed))
            throw CommandFailure("new_file_name_required");

        if (trimmed.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
            throw CommandFailure("invalid_file_name");

        return trimmed;
    }

    private static string BuildRenameDestinationPath(RenameTarget target, string newName)
    {
        var baseName = NormalizeNewBaseName(newName);
        var directory = Path.GetDirectoryName(target.Path);
        if (string.IsNullOrWhiteSpace(directory))
            throw CommandFailure("target_directory_unavailable");

        var extension = string.IsNullOrWhiteSpace(target.Extension)
            ? GetDocumentExtension(target.DocumentType)
            : target.Extension;
        if (string.IsNullOrWhiteSpace(extension))
            throw CommandFailure("unsupported_document_type");

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

    private static void EnsureRenameDestinationAvailable(string destPath)
    {
        if (File.Exists(destPath))
            throw CommandFailure("target_file_exists", "path", destPath);
    }

    private static void SaveModelCopy(ModelDoc2 model, string destPath)
    {
        var extension = model.Extension;
        if (extension == null)
            throw CommandFailure("document_extension_unavailable");

        var errors = 0;
        var warnings = 0;
        var options = (int)swSaveAsOptions_e.swSaveAsOptions_Silent | (int)swSaveAsOptions_e.swSaveAsOptions_Copy;
        var ok = extension.SaveAs3(destPath, 0, options, null, null, ref errors, ref warnings);
        if (!ok || errors != 0)
            throw CommandFailure("save_new_file_failed", "errors", errors, "warnings", warnings, "path", destPath);
    }

    private ModelDoc2 OpenModelForRename(string path, bool activate = false)
    {
        var existing = TryGetOpenModelByPath(path);
        if (existing != null)
        {
            if (activate) ActivateModel(existing);
            return existing;
        }

        var docType = GetDocumentTypeFromPath(path);
        var errors = 0;
        var warnings = 0;
        var wasVisible = true;
        try
        {
            wasVisible = Safe(() => _swApp.GetDocumentVisible(docType));
            _swApp.DocumentVisible(activate, docType);

            var model = OpenDoc6WithDialogHandling(
                path,
                docType,
                (int)swOpenDocOptions_e.swOpenDocOptions_Silent,
                "",
                ref errors,
                ref warnings);
            if (model == null || errors != 0)
                throw CommandFailure("open_new_file_failed", "errors", errors, "warnings", warnings, "path", path);

            if (activate) ActivateModel(model);
            return model;
        }
        finally
        {
            if (!activate)
            {
                try
                {
                    _swApp.DocumentVisible(wasVisible, docType);
                }
                catch (Exception ex)
                {
                    LogIgnoredException("OpenModelForRename.DocumentVisible", ex);
                }
            }
        }
    }

    private static string GetActivationTitle(ModelDoc2 model)
    {
        var title = Safe(model.GetTitle) ?? "";
        if (!string.IsNullOrWhiteSpace(title)) return title;

        var path = Safe(model.GetPathName) ?? "";
        return string.IsNullOrWhiteSpace(path) ? "" : Path.GetFileName(path);
    }

    private void ActivateModel(ModelDoc2 model)
    {
        var title = GetActivationTitle(model);
        if (string.IsNullOrWhiteSpace(title)) return;

        var errors = 0;
        Safe(() => _swApp.ActivateDoc3(title, false, 0, ref errors));
    }

    private static void SaveModel(
        ModelDoc2 model,
        string failureCode = "save_properties_failed",
        bool saveReferenced = false)
    {
        var errors = 0;
        var warnings = 0;
        var options = (int)swSaveAsOptions_e.swSaveAsOptions_Silent;
        if (saveReferenced)
            options |= (int)swSaveAsOptions_e.swSaveAsOptions_SaveReferenced;

        var ok = model.Save3(options, ref errors, ref warnings);
        if (!ok || errors != 0)
            throw CommandFailure(failureCode, "errors", errors, "warnings", warnings);
    }

    private int ReplaceDrawingReferencesOffline(
        string drawingPath,
        IEnumerable<string> oldReferences,
        string newReference,
        string logContext)
    {
        var replacements = 0;
        foreach (var reference in oldReferences
                     .Where(item => !string.IsNullOrWhiteSpace(item))
                     .Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (IsSameReferencePath(reference, newReference))
            {
                AddinLog.Write(logContext + " drawing reference already targets new model: drawing=" +
                               drawingPath + ", reference=" + reference);
                replacements++;
                continue;
            }

            if (TryReplaceReferencedDocument(drawingPath, reference, newReference, logContext))
                replacements++;
        }

        return replacements;
    }

    private bool TryReplaceReferencedDocument(
        string drawingPath,
        string oldReference,
        string newReference,
        string logContext)
    {
        try
        {
            var replaced = _swApp.ReplaceReferencedDocument(drawingPath, oldReference, newReference);
            AddinLog.Write(logContext + " ReplaceReferencedDocument result=" + replaced +
                           ", drawing=" + drawingPath + ", old=" + oldReference + ", new=" + newReference);
            return replaced;
        }
        catch (Exception ex)
        {
            AddinLog.Write(logContext + " ReplaceReferencedDocument failed: drawing=" + drawingPath +
                           ", old=" + oldReference + ", new=" + newReference + ", error=" + ex.Message);
            return false;
        }
    }

    private bool TryReplaceDrawingReferencesByOpening(
        string drawingPath,
        IEnumerable<string> oldReferences,
        string newReference,
        bool keepOpen,
        string logContext,
        out ModelDoc2 drawingModel,
        out DrawingDoc drawing)
    {
        drawingModel = null;
        drawing = null;

        ModelDoc2 openedModel = null;
        var openedHere = false;
        var visibilityChanged = false;
        var previousVisibility = true;
        const int docType = (int)swDocumentTypes_e.swDocDRAWING;

        try
        {
            openedModel = TryGetOpenModelByPath(drawingPath);
            if (openedModel == null)
            {
                previousVisibility = Safe(() => _swApp.GetDocumentVisible(docType));
                _swApp.DocumentVisible(keepOpen, docType);
                visibilityChanged = true;

                var errors = 0;
                var warnings = 0;
                openedModel = OpenDoc6WithDialogHandling(
                    drawingPath,
                    docType,
                    (int)swOpenDocOptions_e.swOpenDocOptions_Silent,
                    "",
                    ref errors,
                    ref warnings);
                AddinLog.Write(logContext + " ReplaceViewModel fallback open: drawing=" + drawingPath +
                               ", errors=" + errors + ", warnings=" + warnings);
                if (openedModel == null || errors != 0)
                    return false;

                openedHere = true;
            }

            var openedDrawing = GetDrawingDoc(openedModel);
            if (openedDrawing == null)
            {
                AddinLog.Write(logContext + " ReplaceViewModel fallback failed: drawing doc unavailable, drawing=" +
                               drawingPath);
                return false;
            }

            var viewNames = GetDrawingReferenceReplacementViewNames(openedDrawing, oldReferences, newReference);
            if (viewNames.Length == 0)
            {
                AddinLog.Write(logContext + " ReplaceViewModel fallback failed: no matching views, drawing=" +
                               drawingPath + ", new=" + newReference);
                return false;
            }

            var drawingDoc = openedDrawing as IDrawingDoc;
            if (drawingDoc == null)
            {
                AddinLog.Write(logContext + " ReplaceViewModel fallback failed: IDrawingDoc unavailable, drawing=" +
                               drawingPath);
                return false;
            }

            var instances = viewNames.Select(_ => "").ToArray();
            var replaced = drawingDoc.ReplaceViewModel(newReference, viewNames, instances);
            AddinLog.Write(logContext + " ReplaceViewModel result=" + replaced + ", drawing=" + drawingPath +
                           ", new=" + newReference + ", views=" + string.Join(",", viewNames));
            if (!replaced)
                return false;

            openedModel.ForceRebuild3(true);

            var saveErrors = 0;
            var saveWarnings = 0;
            var saved = openedModel.Save3(
                (int)swSaveAsOptions_e.swSaveAsOptions_Silent,
                ref saveErrors,
                ref saveWarnings);
            AddinLog.Write(logContext + " ReplaceViewModel fallback save result=" + saved +
                           ", errors=" + saveErrors + ", warnings=" + saveWarnings + ", drawing=" + drawingPath);
            if (!saved || saveErrors != 0)
                return false;

            if (keepOpen || !openedHere)
            {
                drawingModel = openedModel;
                drawing = openedDrawing;
            }

            return true;
        }
        catch (Exception ex)
        {
            AddinLog.Write(logContext + " ReplaceViewModel fallback failed: " + ex);
            return false;
        }
        finally
        {
            if (openedHere && !keepOpen)
            {
                var title = GetActivationTitle(openedModel);
                if (!string.IsNullOrWhiteSpace(title))
                    Safe(() =>
                    {
                        _swApp.CloseDoc(title);
                        return true;
                    });
            }

            if (visibilityChanged && !keepOpen)
            {
                try
                {
                    _swApp.DocumentVisible(previousVisibility, docType);
                }
                catch (Exception ex)
                {
                    LogIgnoredException(logContext + ".DocumentVisible", ex);
                }
            }
        }
    }

    private static string[] GetDrawingReferenceReplacementViewNames(
        DrawingDoc drawing,
        IEnumerable<string> oldReferences,
        string newReference)
    {
        var references = oldReferences
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var views = EnumerateDrawingViews(drawing)
            .Select(view => new
            {
                name = GetDrawingViewName(view),
                reference = GetViewReferencedModelPath(view)
            })
            .Where(item => !string.IsNullOrWhiteSpace(item.name) &&
                           !string.IsNullOrWhiteSpace(item.reference))
            .ToArray();

        var matchingViews = views
            .Where(item => references.Any(reference => IsSameDrawingReference(item.reference, reference)))
            .Select(item => item.name)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (matchingViews.Length > 0)
            return matchingViews;

        var newExtension = Path.GetExtension(newReference);
        var sameExtensionViews = views
            .Where(item => string.Equals(Path.GetExtension(item.reference), newExtension,
                StringComparison.OrdinalIgnoreCase))
            .ToArray();
        var sameExtensionReferenceCount = sameExtensionViews
            .Select(item => Path.GetFileName(item.reference))
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Count();

        return sameExtensionReferenceCount == 1
            ? sameExtensionViews
                .Select(item => item.name)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray()
            : Array.Empty<string>();
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
            return DrawingCopyResult.Failed(sourceDrawing, destDrawing, "target_drawing_exists");

        try
        {
            var sourceReference = EnsureSourceDrawingReferencesCurrentModel(sourceDrawing, sourceModelPath);
            if (sourceReference == null)
                return DrawingCopyResult.Failed(sourceDrawing, destDrawing, "source_drawing_reference_mismatch");

            File.Copy(sourceDrawing, destDrawing);
            ClearReadOnlyAttribute(destDrawing);
            AddinLog.Write("CopyRelatedDrawing copied: " + sourceDrawing + " -> " + destDrawing);

            var replaced = TryReplaceReferencedDocument(destDrawing, sourceReference, destModelPath,
                "CopyRelatedDrawing");
            if (!replaced)
            {
                replaced = TryReplaceDrawingReferencesByOpening(
                    destDrawing,
                    new[] { sourceReference },
                    destModelPath,
                    false,
                    "CopyRelatedDrawing",
                    out _,
                    out _);
            }

            if (replaced)
                return DrawingCopyResult.Copied(sourceDrawing, destDrawing, sourceReference, sourceReference);

            try
            {
                File.Delete(destDrawing);
            }
            catch (Exception ex)
            {
                LogIgnoredException("CopyRelatedDrawing.DeleteFailedDrawing", ex);
            }

            return DrawingCopyResult.Failed(sourceDrawing, destDrawing, "drawing_reference_replace_failed");
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

            return DrawingCopyResult.Failed(sourceDrawing, destDrawing, "drawing_copy_failed");
        }
    }

    private string EnsureSourceDrawingReferencesCurrentModel(string sourceDrawing, string sourceModelPath)
    {
        var sourceFullPath = Path.GetFullPath(sourceModelPath);
        var dependencies = GetDrawingModelDependencies(sourceDrawing).ToArray();
        var currentReference = dependencies.FirstOrDefault(item =>
            IsSameReferencePath(item, sourceFullPath) || IsSameFileIfExists(item, sourceFullPath));
        if (!string.IsNullOrWhiteSpace(currentReference))
        {
            AddinLog.Write("CopyRelatedDrawing source reference already ok: drawing=" + sourceDrawing + ", model=" +
                           currentReference);
            return currentReference;
        }

        foreach (var oldReference in GetSourceDrawingRepairCandidates(dependencies, sourceModelPath))
        {
            var repaired = _swApp.ReplaceReferencedDocument(sourceDrawing, oldReference, sourceModelPath);
            AddinLog.Write("CopyRelatedDrawing repair source result=" + repaired + ", drawing=" + sourceDrawing +
                           ", old=" + oldReference + ", new=" + sourceModelPath);
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

    private IEnumerable<string> GetSourceDrawingRepairCandidates(IEnumerable<string> dependencies,
        string sourceModelPath)
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
                AddReferenceCandidate(candidates, dependency);
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

    private static bool IsSameDrawingReference(string left, string right)
    {
        return IsSameReferencePath(left, right) ||
               IsSameFileIfExists(left, right) ||
               IsSameReferenceFileName(left, right);
    }

    private static bool IsSameReferenceFileName(string left, string right)
    {
        try
        {
            var leftName = Path.GetFileName(left);
            var rightName = Path.GetFileName(right);
            return !string.IsNullOrWhiteSpace(leftName) &&
                   string.Equals(leftName, rightName, StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    // --- Coding Cleanup ---

}
