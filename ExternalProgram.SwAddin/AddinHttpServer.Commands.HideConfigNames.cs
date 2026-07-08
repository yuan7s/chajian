using System;
using System.Collections.Generic;
using SldWorks;
using SwConst;

namespace ExternalProgram.SwAddin;

// ReSharper disable CatchAllClause
#pragma warning disable CA1031 // SolidWorks COM APIs throw broad COM/runtime exceptions; command handlers isolate and log recoverable failures.

internal sealed partial class AddinHttpServer
{
    /// <summary>
    /// 树设置（隐藏配置名）：隐藏特征树中组件的配置名称和显示状态名称，
    /// 递归应用到所有子装配体。使特征树更简洁易读。
    /// </summary>
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
        var configuration = Safe(() => asmDoc?.GetActiveConfiguration()) as Configuration;
        var rootComponent = Safe(() => configuration?.GetRootComponent()) as Component2;

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
        Safe(() => featureManager.HideComponentSingleConfigurationOrDisplayStateNames = false);
        Safe(() => featureManager.SetComponentIdentifiers(4, 0, 0));
        Safe(() => featureManager.SetComponentIdentifiers(2, 0, 0));
        Safe(() => featureManager.ShowComponentConfigurationNames = false);
        Safe(() => featureManager.ShowComponentConfigurationDescriptions = false);
        Safe(() => featureManager.ShowDisplayStateNames = false);
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
}
