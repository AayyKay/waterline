using Waterline.Core;

namespace Waterline.Infrastructure;

public readonly record struct WidgetWorkArea(string MonitorId, double Left, double Top, double Width, double Height);
public readonly record struct WidgetBounds(double Left, double Top, double Width, double Height);

public static class WidgetPlacementPolicy
{
    public static WidgetBounds Restore(
        WidgetPlacement? placement,
        IReadOnlyList<WidgetWorkArea> workAreas,
        string fallbackMonitorId,
        double desiredWidth,
        double desiredHeight)
    {
        if (workAreas.Count == 0) throw new ArgumentException("At least one work area is required.", nameof(workAreas));

        var area = workAreas.FirstOrDefault(candidate =>
            string.Equals(candidate.MonitorId, placement?.MonitorId, StringComparison.OrdinalIgnoreCase));
        if (area.Width <= 0 || area.Height <= 0)
            area = workAreas.FirstOrDefault(candidate =>
                string.Equals(candidate.MonitorId, fallbackMonitorId, StringComparison.OrdinalIgnoreCase));
        if (area.Width <= 0 || area.Height <= 0) area = workAreas[0];

        var width = Math.Min(Math.Max(1, desiredWidth), area.Width);
        var height = Math.Min(Math.Max(1, desiredHeight), area.Height);
        var anchorX = Math.Clamp(placement?.AnchorX ?? 1, 0, 1);
        var anchorY = Math.Clamp(placement?.AnchorY ?? 1, 0, 1);
        var left = area.Left + Math.Max(0, area.Width - width) * anchorX;
        var top = area.Top + Math.Max(0, area.Height - height) * anchorY;
        return new WidgetBounds(left, top, width, height);
    }

    public static WidgetPlacement Capture(WidgetBounds bounds, WidgetWorkArea area, double dpiScale)
    {
        var horizontalRange = Math.Max(0, area.Width - bounds.Width);
        var verticalRange = Math.Max(0, area.Height - bounds.Height);
        return new WidgetPlacement
        {
            MonitorId = area.MonitorId,
            AnchorX = horizontalRange == 0 ? 0 : Math.Clamp((bounds.Left - area.Left) / horizontalRange, 0, 1),
            AnchorY = verticalRange == 0 ? 0 : Math.Clamp((bounds.Top - area.Top) / verticalRange, 0, 1),
            Width = bounds.Width,
            Height = bounds.Height,
            DpiScale = double.IsFinite(dpiScale) && dpiScale > 0 ? dpiScale : 1
        };
    }
}
