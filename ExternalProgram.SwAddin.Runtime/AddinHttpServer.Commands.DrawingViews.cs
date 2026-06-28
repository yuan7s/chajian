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
    private static bool TryGetOptionalDrawingResourcePath(
        Dictionary<string, object> args,
        string key,
        string expectedExtension,
        string name,
        List<DrawingAutomationIssue> issues,
        out string path)
    {
        path = GetArgString(args, key);
        if (string.IsNullOrWhiteSpace(path))
        {
            AddDrawingIssue(issues, "warning", name, "未设置，已跳过", "");
            return false;
        }

        if (!File.Exists(path))
        {
            AddDrawingIssue(issues, "warning", name, "文件不存在，已跳过", path);
            return false;
        }

        if (!string.Equals(Path.GetExtension(path), expectedExtension, StringComparison.OrdinalIgnoreCase))
        {
            AddDrawingIssue(issues, "warning", name, "文件类型不匹配，已跳过", path);
            return false;
        }

        return true;
    }

    private int InsertStandardDrawingViews(
        ModelDoc2 drawingModel,
        DrawingDoc drawing,
        string sourcePath,
        Dictionary<string, object> args,
        List<DrawingAutomationIssue> issues)
    {
        var sheet = drawing.GetCurrentSheet() as Sheet;
        const double defaultSheetWidth = 0.42;
        const double defaultSheetHeight = 0.297;
        var width = defaultSheetWidth;
        var height = defaultSheetHeight;
        if (sheet != null)
            sheet.GetSize(ref width, ref height);

        var wasCommandInProgress = Safe(() => _swApp.CommandInProgress);
        _swApp.CommandInProgress = true;
        try
        {
            try
            {
                drawing.EditSheet();
            }
            catch (Exception ex)
            {
                LogIgnoredException("InsertStandardDrawingViews.EditSheet", ex);
            }

            drawingModel.ClearSelection2(true);

            if (string.IsNullOrWhiteSpace(sourcePath) || !File.Exists(sourcePath))
            {
                AddDrawingIssue(issues, "error", "视图", "模型文件不存在，无法生成视图", sourcePath);
                return 0;
            }

            if (!Safe(() => drawing.GenerateViewPaletteViews(sourcePath)))
            {
                AddDrawingIssue(issues, "error", "视图调色板", "视图调色板生成失败", sourcePath);
                return 0;
            }

            var baseView = DropBaseDrawingPaletteView(drawing, width, height, issues);
            if (baseView == null)
            {
                AddDrawingIssue(issues, "error", "主视图", "未能放置工程图视图1", sourcePath);
                return 0;
            }

            var inserted = 1;
            var baseViewName = Safe(baseView.GetName2) ?? Safe(() => baseView.Name) ?? "工程图视图1";
            inserted += CreateProjectedDrawingView(drawingModel, drawing, baseViewName, width * 0.6015328888757833,
                height * 0.7117505740745859, "右视图", issues);
            inserted += CreateProjectedDrawingView(drawingModel, drawing, baseViewName, width * 0.3324447719576762,
                height * 0.4073276539248047, "俯视图", issues);

            if (GetArgBool(args, "insertIsoView", true))
                inserted += InsertIsoDrawingView(drawingModel, drawing, sourcePath, width, height, issues);

            if (inserted == 0)
                AddDrawingIssue(issues, "error", "视图", "未能生成任何标准视图", sourcePath);

            drawingModel.ClearSelection2(true);
            return inserted;
        }
        finally
        {
            _swApp.CommandInProgress = wasCommandInProgress;
        }
    }

    private static int InsertMissingIsoDrawingView(
        ModelDoc2 drawingModel,
        DrawingDoc drawing,
        List<DrawingAutomationIssue> issues)
    {
        if (CountDrawingModelViews(drawing) >= 4)
            return 0;

        var sourcePath = GetPrimaryDrawingModelPath(drawing);
        var sheetSize = GetCurrentDrawingSheetSize(drawing);
        return InsertIsoDrawingView(drawingModel, drawing, sourcePath, sheetSize.Width, sheetSize.Height, issues);
    }

    private static int InsertIsoDrawingView(
        ModelDoc2 drawingModel,
        DrawingDoc drawing,
        string sourcePath,
        double sheetWidth,
        double sheetHeight,
        List<DrawingAutomationIssue> issues)
    {
        if (CountDrawingModelViews(drawing) >= 4)
            return 0;
        if (string.IsNullOrWhiteSpace(sourcePath) || !File.Exists(sourcePath))
        {
            AddDrawingIssue(issues, "warning", "等轴测", "模型文件不存在，无法生成等轴测视图", sourcePath);
            return 0;
        }

        var paletteViewNames = ToStringArray(Safe(drawing.GetDrawingPaletteViewNames));
        if (paletteViewNames.Length == 0 && Safe(() => drawing.GenerateViewPaletteViews(sourcePath)))
            paletteViewNames = ToStringArray(Safe(drawing.GetDrawingPaletteViewNames));

        var view = paletteViewNames.Length > 0
            ? DropDrawingPaletteView(drawing, paletteViewNames, IsoDrawingViewSpec, sheetWidth, sheetHeight, issues)
            : null;
        view ??= CreateDrawingView(drawing, sourcePath, IsoDrawingViewSpec, sheetWidth, sheetHeight, issues);

        drawingModel.ClearSelection2(true);
        if (view == null)
        {
            AddDrawingIssue(issues, "warning", "等轴测", "等轴测视图生成失败", sourcePath);
            return 0;
        }

        AddDrawingIssue(issues, "info", "等轴测", "已插入等轴测视图", Safe(view.GetName2) ?? Safe(() => view.Name) ?? "");
        return 1;
    }

    private static View DropBaseDrawingPaletteView(
        DrawingDoc drawing,
        double sheetWidth,
        double sheetHeight,
        List<DrawingAutomationIssue> issues)
    {
        var x = sheetWidth * 0.3324447719576762;
        var y = sheetHeight * 0.7117505740745859;
        var candidates = new[] { "工程图视图1", "Drawing View1", "Drawing View 1", "*Front", "*前视", "*前视图" };

        foreach (var candidate in candidates)
        {
            var view = Safe(() => drawing.DropDrawingViewFromPalette2(candidate, x, y, 0.0));
            if (view == null) continue;

            try
            {
                view.UseSheetScale = 1;
            }
            catch (Exception ex)
            {
                LogIgnoredException("DropBaseDrawingPaletteView.UseSheetScale", ex);
            }

            return view;
        }

        AddDrawingIssue(issues, "warning", "主视图", "视图调色板未找到工程图视图1", "");
        return null;
    }

    private static int CreateProjectedDrawingView(
        ModelDoc2 drawingModel,
        DrawingDoc drawing,
        string baseViewName,
        double x,
        double y,
        string displayName,
        List<DrawingAutomationIssue> issues)
    {
        if (string.IsNullOrWhiteSpace(baseViewName))
        {
            AddDrawingIssue(issues, "warning", displayName, "主视图名称为空，无法投影视图", "");
            return 0;
        }

        drawingModel.ClearSelection2(true);
        var selected = Safe(() => drawingModel.Extension.SelectByID2(
            baseViewName,
            "DRAWINGVIEW",
            0.0,
            0.0,
            0.0,
            false,
            0,
            null,
            0));

        if (!selected)
        {
            AddDrawingIssue(issues, "warning", displayName, "无法选中主视图", baseViewName);
            return 0;
        }

        Safe(() => drawing.ActivateView(baseViewName));
        var view = Safe(() => drawing.CreateUnfoldedViewAt3(x, y, 0.0, false));
        drawingModel.ClearSelection2(true);
        if (view != null) return 1;

        AddDrawingIssue(issues, "warning", displayName, "投影视图生成失败", baseViewName);
        return 0;
    }

    private static DrawingScaleLayoutResult AutoScaleAndLayoutDrawingViews(
        ModelDoc2 drawingModel,
        DrawingDoc drawing,
        List<DrawingAutomationIssue> issues)
    {
        var result = new DrawingScaleLayoutResult();
        var sheetSize = GetCurrentDrawingSheetSize(drawing);
        var views = GetModelDrawingViews(drawing)
            .Take(StandardDrawingViewSlots.Length + 1)
            .ToArray();
        if (views.Length == 0) return result;

        foreach (var view in views)
            TryUseSheetScale(view);

        drawingModel.ForceRebuild3(true);

        var scaleViews = views
            .Take(Math.Min(StandardDrawingViewSlots.Length, views.Length))
            .ToArray();
        var currentScale = GetCurrentDrawingScaleDecimal(drawing, scaleViews);
        var plan = ChooseMaxFittingDrawingScaleLayout(scaleViews, sheetSize, currentScale);
        var ratio = plan?.Scale ?? ChooseDrawingScaleRatio(currentScale);
        var slots = BuildStandardDrawingViewSlots(sheetSize.Width, sheetSize.Height, views.Length);
        if (plan?.Slots != null)
        {
            for (var i = 0; i < plan.Slots.Length && i < slots.Length; i++)
                slots[i] = plan.Slots[i];
        }

        result.scaleText = ratio.DisplayText;
        var scaleChanged = Math.Abs(currentScale - ratio.DecimalValue) > Math.Max(0.0001, currentScale * 0.02);
        if (scaleChanged)
        {
            var sheet = drawing.GetCurrentSheet() as Sheet;
            var scaleSet = sheet != null &&
                           Safe(() => sheet.SetScale(ratio.Numerator, ratio.Denominator, true, true));
            if (scaleSet)
            {
                result.adjusted = true;
                AddDrawingIssue(issues, "info", "图纸比例", "已自动调整图纸比例", ratio.DisplayText);
                drawingModel.ForceRebuild3(true);
            }
            else
            {
                AddDrawingIssue(issues, "warning", "图纸比例", "自动调整图纸比例未确认完成", ratio.DisplayText);
            }
        }

        if (views.Length > StandardDrawingViewSlots.Length && slots.Length > StandardDrawingViewSlots.Length)
        {
            drawingModel.ForceRebuild3(true);
            if (TryFitIndependentDrawingViewScale(
                    views[StandardDrawingViewSlots.Length],
                    slots[StandardDrawingViewSlots.Length],
                    ratio,
                    out var isoSlot,
                    out var isoScale))
            {
                slots[StandardDrawingViewSlots.Length] = isoSlot;
                if (Math.Abs(isoScale.DecimalValue - ratio.DecimalValue) > 0.0001)
                {
                    result.adjusted = true;
                    AddDrawingIssue(issues, "info", "等轴测比例", "已单独调整等轴测比例", isoScale.DisplayText);
                    drawingModel.ForceRebuild3(true);
                }
            }
        }

        result.viewsMoved = LayoutDrawingViewsInSlots(views, slots);
        if (result.viewsMoved > 0)
        {
            result.adjusted = true;
            drawingModel.ForceRebuild3(true);
            AddDrawingIssue(issues, "info", "视图布局", "已按标准三视图位置重新布局", result.viewsMoved.ToString());
        }

        return result;
    }

    private static int LayoutDrawingViewsInSlots(View[] views, DrawingViewSlot[] slots)
    {
        var moved = 0;
        for (var i = 0; i < views.Length && i < slots.Length; i++)
        {
            var view = views[i];
            var slot = slots[i];
            var currentPosition = GetDrawingViewPosition(view);
            if (Math.Abs(currentPosition.X - slot.CenterX) < 0.0005 &&
                Math.Abs(currentPosition.Y - slot.CenterY) < 0.0005)
                continue;

            if (SetDrawingViewPosition(view, slot.CenterX, slot.CenterY))
                moved++;
        }

        return moved;
    }

    private static bool TryFitIndependentDrawingViewScale(
        View view,
        DrawingViewSlot slot,
        DrawingScaleRatio maxScale,
        out DrawingViewSlot adjustedSlot,
        out DrawingScaleRatio selectedScale)
    {
        adjustedSlot = slot;
        selectedScale = maxScale;
        if (view == null || maxScale == null || maxScale.DecimalValue <= 0) return false;

        var currentScale = Safe(() => view.ScaleDecimal);
        if (currentScale <= 0 || double.IsNaN(currentScale) || double.IsInfinity(currentScale))
            currentScale = maxScale.DecimalValue;

        var unitSize = GetDrawingViewUnitSize(view, currentScale);
        if (!unitSize.IsValid) return false;

        foreach (var scale in DrawingStandardScales
                     .Where(item => item.DecimalValue <= maxScale.DecimalValue + 0.0000001)
                     .OrderByDescending(item => item.DecimalValue))
        {
            var scaledSize = unitSize.Scale(scale.DecimalValue);
            if (!CanDrawingViewFitSlot(scaledSize, slot, DrawingIsoViewLayoutClearance)) continue;

            if (Math.Abs(scale.DecimalValue - maxScale.DecimalValue) <= 0.0001)
            {
                TryUseSheetScale(view);
            }
            else if (!TrySetIndependentDrawingViewScale(view, scale))
            {
                continue;
            }

            adjustedSlot = new DrawingViewSlot(
                slot.Spec,
                slot.CenterX - scaledSize.CenterOffsetX,
                slot.CenterY - scaledSize.CenterOffsetY,
                slot.MinX,
                slot.MaxX,
                slot.MinY,
                slot.MaxY);
            selectedScale = scale;
            return true;
        }

        return false;
    }

    private static bool TrySetIndependentDrawingViewScale(View view, DrawingScaleRatio scale)
    {
        try
        {
            view.UseParentScale = false;
        }
        catch (Exception ex)
        {
            LogIgnoredException("TrySetIndependentDrawingViewScale.UseParentScale", ex);
        }

        try
        {
            view.UseSheetScale = 0;
        }
        catch (Exception ex)
        {
            LogIgnoredException("TrySetIndependentDrawingViewScale.UseSheetScale", ex);
        }

        try
        {
            view.ScaleDecimal = scale.DecimalValue;
            return true;
        }
        catch (Exception ex)
        {
            LogIgnoredException("TrySetIndependentDrawingViewScale.ScaleDecimal", ex);
            return false;
        }
    }

}
