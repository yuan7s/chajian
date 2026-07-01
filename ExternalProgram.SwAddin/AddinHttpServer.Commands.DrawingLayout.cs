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
    private static int RearrangeDrawingAnnotations(
        ModelDoc2 drawingModel,
        DrawingDoc drawing,
        List<DrawingAutomationIssue> issues)
    {
        var sheetSize = GetCurrentDrawingSheetSize(drawing);
        var moved = 0;
        var dimensionCount = 0;

        foreach (var view in GetModelDrawingViews(drawing))
        {
            var outline = GetDrawingViewOutline(view);
            if (!outline.IsValid) continue;

            var placements = EnumerateDrawingDimensionPlacements(view, outline).ToArray();
            dimensionCount += placements.Length;
            moved += ArrangeDrawingDimensionPlacements(placements, outline, sheetSize);
        }

        var aligned = Safe(() => drawingModel.Extension.AlignDimensions(
            (int)swAlignDimensionType_e.swAlignDimensionType_AutoArrange,
            DrawingAnnotationSpacing));
        if (moved == 0 && dimensionCount > 0 && !aligned)
            AddDrawingIssue(issues, "warning", "尺寸排列", "自动排列尺寸未确认完成", "");
        else if (moved > 0)
            AddDrawingIssue(issues, "info", "尺寸排列", "已重新排列尺寸标注", moved.ToString());

        return moved;
    }

    private static IEnumerable<DrawingDimensionPlacement> EnumerateDrawingDimensionPlacements(
        View view,
        DrawingRect outline)
    {
        var dimension = Safe(view.GetFirstDisplayDimension5);
        var guard = 0;
        while (dimension != null && guard++ < 10000)
        {
            var annotation = Safe(() => dimension.GetAnnotation() as Annotation);
            if (annotation != null && !Safe(annotation.IsDangling))
            {
                var position = GetAnnotationPosition(annotation);
                if (position.IsValid)
                {
                    yield return new DrawingDimensionPlacement(
                        annotation,
                        position,
                        ClassifyDrawingAnnotationSide(outline, position));
                }
            }

            var current = dimension;
            dimension = Safe(current.GetNext5);
        }
    }

    private static int ArrangeDrawingDimensionPlacements(
        DrawingDimensionPlacement[] placements,
        DrawingRect outline,
        DrawingSheetSize sheetSize)
    {
        var moved = 0;
        moved += ArrangeDrawingDimensionSide(placements, DrawingAnnotationSide.Top, outline, sheetSize);
        moved += ArrangeDrawingDimensionSide(placements, DrawingAnnotationSide.Bottom, outline, sheetSize);
        moved += ArrangeDrawingDimensionSide(placements, DrawingAnnotationSide.Left, outline, sheetSize);
        moved += ArrangeDrawingDimensionSide(placements, DrawingAnnotationSide.Right, outline, sheetSize);
        return moved;
    }

    private static int ArrangeDrawingDimensionSide(
        DrawingDimensionPlacement[] placements,
        DrawingAnnotationSide side,
        DrawingRect outline,
        DrawingSheetSize sheetSize)
    {
        var items = placements
            .Where(item => item.Side == side)
            .OrderBy(item => side == DrawingAnnotationSide.Left || side == DrawingAnnotationSide.Right
                ? item.Position.Y
                : item.Position.X)
            .ToArray();
        if (items.Length == 0) return 0;

        var rows = Math.Min(4, Math.Max(1, (items.Length + 5) / 6));
        var moved = 0;
        var minX = Math.Max(outline.MinX + DrawingAnnotationMargin, sheetSize.Width * 0.035);
        var maxX = Math.Min(outline.MaxX - DrawingAnnotationMargin, sheetSize.Width * 0.965);
        var minY = Math.Max(outline.MinY + DrawingAnnotationMargin, sheetSize.Height * 0.04);
        var maxY = Math.Min(outline.MaxY - DrawingAnnotationMargin, sheetSize.Height * 0.96);

        for (var i = 0; i < items.Length; i++)
        {
            var item = items[i];
            var row = i % rows;
            var order = rows == 1 ? i : i / rows;
            var orderCount = rows == 1
                ? items.Length
                : (items.Length + rows - 1) / rows;
            var along = GetEvenDrawingPosition(order, orderCount);
            var x = item.Position.X;
            var y = item.Position.Y;

            switch (side)
            {
                case DrawingAnnotationSide.Top:
                    x = Lerp(minX, maxX, along);
                    y = outline.MaxY + DrawingAnnotationMargin + row * DrawingAnnotationSpacing;
                    break;
                case DrawingAnnotationSide.Bottom:
                    x = Lerp(minX, maxX, along);
                    y = outline.MinY - DrawingAnnotationMargin - row * DrawingAnnotationSpacing;
                    break;
                case DrawingAnnotationSide.Left:
                    x = outline.MinX - DrawingAnnotationMargin - row * DrawingAnnotationSpacing;
                    y = Lerp(minY, maxY, along);
                    break;
                case DrawingAnnotationSide.Right:
                    x = outline.MaxX + DrawingAnnotationMargin + row * DrawingAnnotationSpacing;
                    y = Lerp(minY, maxY, along);
                    break;
            }

            x = Clamp(x, sheetSize.Width * 0.02, sheetSize.Width * 0.98);
            y = Clamp(y, sheetSize.Height * 0.03, sheetSize.Height * 0.97);
            if (SetAnnotationPosition(item.Annotation, x, y) &&
                (Math.Abs(item.Position.X - x) > 0.0005 || Math.Abs(item.Position.Y - y) > 0.0005))
                moved++;
        }

        return moved;
    }

    private static DrawingAnnotationSide ClassifyDrawingAnnotationSide(DrawingRect outline, DrawingPoint position)
    {
        var centerX = (outline.MinX + outline.MaxX) / 2.0;
        var centerY = (outline.MinY + outline.MaxY) / 2.0;
        var normalizedX = (position.X - centerX) / Math.Max(outline.Width, 0.001);
        var normalizedY = (position.Y - centerY) / Math.Max(outline.Height, 0.001);

        if (Math.Abs(normalizedX) > Math.Abs(normalizedY))
            return normalizedX < 0 ? DrawingAnnotationSide.Left : DrawingAnnotationSide.Right;

        return normalizedY < 0 ? DrawingAnnotationSide.Bottom : DrawingAnnotationSide.Top;
    }

    private static DrawingViewLayoutPlan ChooseMaxFittingDrawingScaleLayout(
        View[] views,
        DrawingSheetSize sheetSize,
        double currentScale)
    {
        if (views == null || views.Length == 0) return null;
        if (currentScale <= 0 || double.IsNaN(currentScale) || double.IsInfinity(currentScale))
            currentScale = 1.0;

        var unitSizes = views
            .Select(view => GetDrawingViewUnitSize(view, currentScale))
            .ToArray();
        if (unitSizes.Length == 0 || unitSizes.Any(size => !size.IsValid))
            return null;

        foreach (var scale in DrawingStandardScales.OrderByDescending(item => item.DecimalValue))
        {
            if (TryBuildMaxFittingDrawingViewSlots(
                    unitSizes,
                    sheetSize,
                    scale.DecimalValue,
                    out var slots,
                    out var fillRatio))
            {
                return new DrawingViewLayoutPlan(scale, slots, fillRatio);
            }
        }

        return null;
    }

    private static DrawingViewScaledSize GetDrawingViewUnitSize(View view, double currentScale)
    {
        var outline = GetDrawingViewOutline(view);
        if (!outline.IsValid || outline.Width <= 0 || outline.Height <= 0)
            return DrawingViewScaledSize.Invalid;

        var position = GetDrawingViewPosition(view);
        if (!position.IsValid)
            position = new DrawingPoint(
                (outline.MinX + outline.MaxX) / 2.0,
                (outline.MinY + outline.MaxY) / 2.0,
                0.0,
                true);

        var outlineCenterX = (outline.MinX + outline.MaxX) / 2.0;
        var outlineCenterY = (outline.MinY + outline.MaxY) / 2.0;
        return new DrawingViewScaledSize(
            outline.Width / currentScale,
            outline.Height / currentScale,
            (outlineCenterX - position.X) / currentScale,
            (outlineCenterY - position.Y) / currentScale);
    }

    private static bool TryBuildMaxFittingDrawingViewSlots(
        DrawingViewScaledSize[] unitSizes,
        DrawingSheetSize sheetSize,
        double scale,
        out DrawingViewSlot[] slots,
        out double fillRatio)
    {
        slots = Array.Empty<DrawingViewSlot>();
        fillRatio = 0.0;
        if (unitSizes == null || unitSizes.Length == 0 || scale <= 0) return false;

        var scaledSizes = unitSizes
            .Select(size => size.Scale(scale))
            .ToArray();
        var baseSlots = BuildStandardDrawingViewSlots(sheetSize.Width, sheetSize.Height, scaledSizes.Length);
        if (baseSlots.Length < scaledSizes.Length) return false;

        var plannedSlots = new DrawingViewSlot[scaledSizes.Length];
        for (var i = 0; i < scaledSizes.Length; i++)
        {
            var size = scaledSizes[i];
            var baseSlot = baseSlots[i];
            if (!CanDrawingViewFitSlot(size, baseSlot)) return false;

            plannedSlots[i] = new DrawingViewSlot(
                baseSlot.Spec,
                baseSlot.CenterX - size.CenterOffsetX,
                baseSlot.CenterY - size.CenterOffsetY,
                baseSlot.MinX,
                baseSlot.MaxX,
                baseSlot.MinY,
                baseSlot.MaxY);
            fillRatio = Math.Max(fillRatio, GetDrawingViewSlotFillRatio(size, baseSlot));
        }

        slots = plannedSlots;
        return true;
    }

    private static bool CanDrawingViewFitSlot(DrawingViewScaledSize size, DrawingViewSlot slot)
    {
        return CanDrawingViewFitSlot(size, slot, DrawingViewLayoutClearance);
    }

    private static bool CanDrawingViewFitSlot(DrawingViewScaledSize size, DrawingViewSlot slot, double clearance)
    {
        if (!size.IsValid || slot.Width <= 0 || slot.Height <= 0) return false;

        var safeClearance = Math.Max(0.0, clearance);
        return size.Width + safeClearance * 2.0 <= slot.Width &&
               size.Height + safeClearance * 2.0 <= slot.Height;
    }

    private static double GetDrawingViewSlotFillRatio(DrawingViewScaledSize size, DrawingViewSlot slot)
    {
        if (!size.IsValid || slot.Width <= 0 || slot.Height <= 0) return 0.0;

        return Math.Max(
            (size.Width + DrawingViewLayoutClearance * 2.0) / slot.Width,
            (size.Height + DrawingViewLayoutClearance * 2.0) / slot.Height);
    }

    private static DrawingViewSlot[] BuildStandardDrawingViewSlots(double sheetWidth, double sheetHeight, int viewCount)
    {
        var specs = GetDrawingViewSlotSpecs(viewCount);
        if (specs.Length == 0) return Array.Empty<DrawingViewSlot>();

        var leftBound = sheetWidth * DrawingViewLayoutMarginX;
        var rightBound = sheetWidth * (1.0 - DrawingViewLayoutMarginX);
        var topBound = sheetHeight * (1.0 - DrawingViewLayoutTopMargin);
        var bottomBound = sheetHeight * DrawingViewLayoutBottomMargin;
        if (rightBound <= leftBound || topBound <= bottomBound) return Array.Empty<DrawingViewSlot>();

        if (specs.Length == 1)
        {
            return
            [
                CreateCenteredDrawingViewSlot(specs[0], leftBound, rightBound, bottomBound, topBound)
            ];
        }

        var firstCenterX = Clamp(sheetWidth * specs[0].CenterXRatio, leftBound, rightBound);
        var secondCenterX = Clamp(sheetWidth * specs[1].CenterXRatio, leftBound, rightBound);
        var splitX = (firstCenterX + secondCenterX) / 2.0;
        var leftMaxX = splitX - DrawingViewLayoutGap / 2.0;
        var rightMinX = splitX + DrawingViewLayoutGap / 2.0;
        if (leftMaxX <= leftBound || rightBound <= rightMinX) return Array.Empty<DrawingViewSlot>();

        if (specs.Length == 2)
        {
            return
            [
                CreateRatioDrawingViewSlot(specs[0], sheetWidth, sheetHeight, leftBound, leftMaxX, bottomBound, topBound),
                CreateRatioDrawingViewSlot(specs[1], sheetWidth, sheetHeight, rightMinX, rightBound, bottomBound, topBound)
            ];
        }

        var firstCenterY = Clamp(sheetHeight * specs[0].CenterYRatio, bottomBound, topBound);
        var thirdCenterY = Clamp(sheetHeight * specs[2].CenterYRatio, bottomBound, topBound);
        var splitY = (firstCenterY + thirdCenterY) / 2.0;
        var lowerMaxY = splitY - DrawingViewLayoutGap / 2.0;
        var upperMinY = splitY + DrawingViewLayoutGap / 2.0;
        if (lowerMaxY <= bottomBound || topBound <= upperMinY) return Array.Empty<DrawingViewSlot>();

        var slots = new List<DrawingViewSlot>
        {
            CreateRatioDrawingViewSlot(specs[0], sheetWidth, sheetHeight, leftBound, leftMaxX, upperMinY, topBound),
            CreateRatioDrawingViewSlot(specs[1], sheetWidth, sheetHeight, rightMinX, rightBound, upperMinY, topBound),
            CreateRatioDrawingViewSlot(specs[2], sheetWidth, sheetHeight, leftBound, leftMaxX, bottomBound, lowerMaxY)
        };

        if (specs.Length > 3)
        {
            var isoBottomBound = Math.Max(bottomBound, sheetHeight * DrawingIsoViewLayoutBottomMargin);
            if (isoBottomBound >= lowerMaxY)
                isoBottomBound = bottomBound;

            slots.Add(CreateRatioDrawingViewSlot(
                specs[3],
                sheetWidth,
                sheetHeight,
                rightMinX,
                rightBound,
                isoBottomBound,
                lowerMaxY));
        }

        return slots.ToArray();
    }

    private static DrawingViewSlotSpec[] GetDrawingViewSlotSpecs(int viewCount)
    {
        if (viewCount <= 0) return Array.Empty<DrawingViewSlotSpec>();

        var specs = new List<DrawingViewSlotSpec>();
        var standardCount = Math.Min(viewCount, StandardDrawingViewSlots.Length);
        for (var i = 0; i < standardCount; i++)
            specs.Add(StandardDrawingViewSlots[i]);

        if (viewCount > StandardDrawingViewSlots.Length)
            specs.Add(IsoDrawingViewSlot);

        return specs.ToArray();
    }

    private static DrawingViewSlot CreateCenteredDrawingViewSlot(
        DrawingViewSlotSpec spec,
        double minX,
        double maxX,
        double minY,
        double maxY)
    {
        return new DrawingViewSlot(
            spec,
            (minX + maxX) / 2.0,
            (minY + maxY) / 2.0,
            minX,
            maxX,
            minY,
            maxY);
    }

    private static DrawingViewSlot CreateRatioDrawingViewSlot(
        DrawingViewSlotSpec spec,
        double sheetWidth,
        double sheetHeight,
        double minX,
        double maxX,
        double minY,
        double maxY)
    {
        var centerX = Clamp(sheetWidth * spec.CenterXRatio, minX, maxX);
        var centerY = Clamp(sheetHeight * spec.CenterYRatio, minY, maxY);
        return new DrawingViewSlot(spec, centerX, centerY, minX, maxX, minY, maxY);
    }

    private static View[] GetModelDrawingViews(DrawingDoc drawing)
    {
        return EnumerateDrawingViews(drawing)
            .Where(view => !string.IsNullOrWhiteSpace(GetViewReferencedModelPath(view)))
            .ToArray();
    }

    private static string GetPrimaryDrawingModelPath(DrawingDoc drawing)
    {
        return GetModelDrawingViews(drawing)
            .Select(GetViewReferencedModelPath)
            .FirstOrDefault(path => !string.IsNullOrWhiteSpace(path) && File.Exists(path)) ?? "";
    }

    private static DrawingSheetSize GetCurrentDrawingSheetSize(DrawingDoc drawing)
    {
        var width = DrawingSheetWidth;
        var height = DrawingSheetHeight;
        var sheet = drawing.GetCurrentSheet() as Sheet;
        if (sheet != null)
            Safe(() => sheet.GetSize(ref width, ref height));

        if (width <= 0 || height <= 0)
            return new DrawingSheetSize(DrawingSheetWidth, DrawingSheetHeight);

        return new DrawingSheetSize(width, height);
    }

    private static double GetCurrentDrawingScaleDecimal(DrawingDoc drawing, View[] views)
    {
        var sheet = drawing.GetCurrentSheet() as Sheet;
        var properties = ToObjectArray(Safe(sheet.GetProperties));
        var numerator = GetSheetPropertyDouble(properties, 2, 1.0);
        var denominator = GetSheetPropertyDouble(properties, 3, 1.0);
        if (numerator > 0 && denominator > 0)
            return numerator / denominator;

        foreach (var view in views ?? Array.Empty<View>())
        {
            var scale = Safe(() => view.ScaleDecimal);
            if (scale > 0) return scale;
        }

        return 1.0;
    }

    private static DrawingScaleRatio ChooseDrawingScaleRatio(double desiredScale)
    {
        if (desiredScale <= 0 || double.IsNaN(desiredScale) || double.IsInfinity(desiredScale))
            return DrawingStandardScales.First(item => item.Numerator == 1 && item.Denominator == 1);

        foreach (var scale in DrawingStandardScales.OrderByDescending(item => item.DecimalValue))
            if (scale.DecimalValue <= desiredScale + 0.0000001)
                return scale;

        return DrawingStandardScales.OrderBy(item => item.DecimalValue).First();
    }

    private static void TryUseSheetScale(View view)
    {
        try
        {
            view.UseParentScale = false;
        }
        catch (Exception ex)
        {
            LogIgnoredException("TryUseSheetScale.UseParentScale", ex);
        }

        try
        {
            view.UseSheetScale = 1;
        }
        catch (Exception ex)
        {
            LogIgnoredException("TryUseSheetScale.UseSheetScale", ex);
        }
    }

    private static DrawingRect GetDrawingViewOutline(View view)
    {
        var values = ToDoubleArray(Safe(view.GetOutline));
        if (values.Length < 4) return DrawingRect.Invalid;

        return new DrawingRect(
            Math.Min(values[0], values[2]),
            Math.Min(values[1], values[3]),
            Math.Max(values[0], values[2]),
            Math.Max(values[1], values[3]));
    }

    private static DrawingPoint GetDrawingViewPosition(View view)
    {
        var values = ToDoubleArray(Safe(() => view.Position));
        if (values.Length >= 2) return new DrawingPoint(values[0], values[1], 0.0, true);

        var outline = GetDrawingViewOutline(view);
        if (!outline.IsValid) return DrawingPoint.Invalid;

        return new DrawingPoint(
            (outline.MinX + outline.MaxX) / 2.0,
            (outline.MinY + outline.MaxY) / 2.0,
            0.0,
            true);
    }

    private static bool SetDrawingViewPosition(View view, double x, double y)
    {
        try
        {
            view.PositionLocked = false;
        }
        catch (Exception ex)
        {
            LogIgnoredException("SetDrawingViewPosition.PositionLocked", ex);
        }

        try
        {
            view.Position = new[] { x, y, 0.0 };
            return true;
        }
        catch (Exception ex)
        {
            LogIgnoredException("SetDrawingViewPosition", ex);
            return false;
        }
    }

    private static DrawingPoint GetAnnotationPosition(Annotation annotation)
    {
        var values = ToDoubleArray(Safe(annotation.GetPosition));
        return values.Length >= 2
            ? new DrawingPoint(values[0], values[1], values.Length > 2 ? values[2] : 0.0, true)
            : DrawingPoint.Invalid;
    }

    private static bool SetAnnotationPosition(Annotation annotation, double x, double y)
    {
        if (annotation == null) return false;

        try
        {
            if (annotation.SetPosition2(x, y, 0.0)) return true;
        }
        catch (Exception ex)
        {
            LogIgnoredException("SetAnnotationPosition.SetPosition2", ex);
        }

        try
        {
            return annotation.SetPosition(x, y, 0.0);
        }
        catch (Exception ex)
        {
            LogIgnoredException("SetAnnotationPosition.SetPosition", ex);
            return false;
        }
    }

    private static double[] ToDoubleArray(object value)
    {
        if (value == null) return Array.Empty<double>();
        if (value is double[] doubleArray) return doubleArray;
        if (value is object[] objectArray)
            return objectArray
                .Select(item => item == null ? double.NaN : Convert.ToDouble(item))
                .Where(item => !double.IsNaN(item))
                .ToArray();
        if (value is Array array)
            return array
                .Cast<object>()
                .Select(item => item == null ? double.NaN : Convert.ToDouble(item))
                .Where(item => !double.IsNaN(item))
                .ToArray();
        return double.TryParse(value.ToString(), out var parsed) ? [parsed] : Array.Empty<double>();
    }

    private static double GetEvenDrawingPosition(int index, int count)
    {
        if (count <= 1) return 0.5;
        return (index + 0.5) / count;
    }

    private static double Lerp(double min, double max, double amount)
    {
        if (max <= min) return (min + max) / 2.0;
        return min + (max - min) * Clamp(amount, 0.0, 1.0);
    }

    private static double Clamp(double value, double min, double max)
    {
        if (max < min) return value;
        if (value < min) return min;
        return value > max ? max : value;
    }

    private static string[] GenerateDrawingPaletteViewNames(
        DrawingDoc drawing,
        string sourcePath,
        List<DrawingAutomationIssue> issues)
    {
        if (string.IsNullOrWhiteSpace(sourcePath) || !File.Exists(sourcePath))
        {
            AddDrawingIssue(issues, "warning", "视图调色板", "模型文件不存在，无法生成视图", sourcePath);
            return Array.Empty<string>();
        }

        var generated = Safe(() => drawing.GenerateViewPaletteViews(sourcePath));
        if (!generated)
        {
            AddDrawingIssue(issues, "warning", "视图调色板", "视图调色板生成失败", sourcePath);
            return Array.Empty<string>();
        }

        var names = ToStringArray(Safe(drawing.GetDrawingPaletteViewNames))
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (names.Length == 0)
            AddDrawingIssue(issues, "warning", "视图调色板", "未发现可用模型视图", sourcePath);

        return names;
    }

    private static View DropDrawingPaletteView(
        DrawingDoc drawing,
        string[] paletteViewNames,
        DrawingViewSpec spec,
        double sheetWidth,
        double sheetHeight,
        List<DrawingAutomationIssue> issues)
    {
        var paletteViewName = FindPaletteViewName(paletteViewNames, spec);
        if (string.IsNullOrWhiteSpace(paletteViewName))
        {
            AddDrawingIssue(issues, "warning", spec.DisplayName, "视图调色板中未找到匹配视图",
                paletteViewNames.Length == 0 ? "" : string.Join(", ", paletteViewNames));
            return null;
        }

        var view = Safe(() => drawing.DropDrawingViewFromPalette2(
            paletteViewName,
            sheetWidth * spec.XRatio,
            sheetHeight * spec.YRatio,
            0.0));

        if (view == null)
        {
            AddDrawingIssue(issues, "warning", spec.DisplayName, "视图放置失败", paletteViewName);
            return null;
        }

        try
        {
            view.UseSheetScale = 1;
        }
        catch (Exception ex)
        {
            LogIgnoredException("DropDrawingPaletteView.UseSheetScale", ex);
        }

        return view;
    }

    private static string FindPaletteViewName(string[] paletteViewNames, DrawingViewSpec spec)
    {
        if (paletteViewNames == null || paletteViewNames.Length == 0) return "";

        foreach (var orientationName in spec.OrientationNames)
        {
            var exact = paletteViewNames.FirstOrDefault(name =>
                string.Equals(name, orientationName, StringComparison.OrdinalIgnoreCase));
            if (!string.IsNullOrWhiteSpace(exact)) return exact;
        }

        foreach (var orientationName in spec.OrientationNames)
        {
            var normalizedOrientation = NormalizeDrawingViewName(orientationName);
            var match = paletteViewNames.FirstOrDefault(name =>
                NormalizeDrawingViewName(name).Contains(normalizedOrientation));
            if (!string.IsNullOrWhiteSpace(match)) return match;
        }

        return "";
    }

    private static string NormalizeDrawingViewName(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return "";
        return value
            .Replace("*", "")
            .Replace(" ", "")
            .Replace("_", "")
            .Replace("-", "")
            .Trim()
            .ToUpperInvariant();
    }

    private static View CreateDrawingView(
        DrawingDoc drawing,
        string sourcePath,
        DrawingViewSpec spec,
        double sheetWidth,
        double sheetHeight,
        List<DrawingAutomationIssue> issues)
    {
        foreach (var modelName in GetDrawingViewModelNameCandidates(sourcePath))
        {
            foreach (var orientationName in spec.OrientationNames)
            {
                var view = Safe(() => drawing.CreateDrawViewFromModelView3(
                    modelName,
                    orientationName,
                    sheetWidth * spec.XRatio,
                    sheetHeight * spec.YRatio,
                    0.0));

                if (view == null) continue;

                try
                {
                    view.UseSheetScale = 1;
                }
                catch (Exception ex)
                {
                    LogIgnoredException("CreateDrawingView.UseSheetScale", ex);
                }

                return view;
            }
        }

        AddDrawingIssue(issues, "warning", spec.DisplayName, "视图生成失败", string.Join("/", spec.OrientationNames));
        return null;
    }

}
