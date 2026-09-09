using System.ComponentModel;
using System.Windows;
using System.Windows.Media;
using MediaColor = System.Windows.Media.Color;
using WpfApplication = System.Windows.Application;
using WpfSystemColors = System.Windows.SystemColors;

namespace Waterline;

internal static class ThemeManager
{
    private static readonly string[] ProductBrushKeys =
    [
        "CanvasBrush", "CanvasRaisedBrush", "SurfaceBrush", "SurfaceRaisedBrush", "SurfaceHoverBrush",
        "SurfacePressedBrush", "ActivityRailBrush", "BoundaryBrush", "DividerBrush", "TextPrimaryBrush",
        "TextSecondaryBrush", "TextTertiaryBrush", "CyanBrush", "AquaBrush", "FocusBrush", "SuccessBrush",
        "WarningBrush", "ErrorBrush", "ScrimBrush", "AccentTextBrush", "AtmosphereBrush", "AtmosphereLineBrush",
        "SelectedSurfaceBrush", "SelectedTextBrush", "SelectedThumbBrush", "CloseHoverBrush", "ClosePressedBrush",
        "InkBrush", "MutedBrush", "LineBrush", "PanelBrush"
    ];

    public static void Initialize()
    {
        Apply();
        SystemParameters.StaticPropertyChanged += OnSystemParametersChanged;
    }

    public static void ApplyHighContrastForSnapshot() => Apply(true);

    private static void OnSystemParametersChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(SystemParameters.HighContrast)) Apply();
    }

    private static void Apply(bool? forceHighContrast = null)
    {
        if (WpfApplication.Current is null) return;
        if (!(forceHighContrast ?? SystemParameters.HighContrast))
        {
            WpfApplication.Current.Resources["DialogShadowOpacity"] = .55d;
            WpfApplication.Current.Resources["FloatingShadowEnabled"] = true;
            foreach (var key in ProductBrushKeys)
            {
                var colorKey = key switch
                {
                    "InkBrush" => "TextPrimaryColor",
                    "MutedBrush" => "TextSecondaryColor",
                    "LineBrush" => "DividerColor",
                    "PanelBrush" => "SurfaceColor",
                    _ => key.Replace("Brush", "Color", StringComparison.Ordinal)
                };
                if (WpfApplication.Current.TryFindResource(colorKey) is MediaColor color)
                    WpfApplication.Current.Resources[key] = new SolidColorBrush(color);
            }
            var reservoir = new LinearGradientBrush { StartPoint = new System.Windows.Point(0, 0), EndPoint = new System.Windows.Point(0, 1) };
            reservoir.GradientStops.Add(new GradientStop((MediaColor)WpfApplication.Current.FindResource("ReservoirTopColor"), 0));
            reservoir.GradientStops.Add(new GradientStop((MediaColor)WpfApplication.Current.FindResource("ReservoirMiddleColor"), .55));
            reservoir.GradientStops.Add(new GradientStop((MediaColor)WpfApplication.Current.FindResource("ReservoirBottomColor"), 1));
            WpfApplication.Current.Resources["ReservoirBrush"] = reservoir;
            return;
        }

        Set("CanvasBrush", WpfSystemColors.WindowBrush); Set("CanvasRaisedBrush", WpfSystemColors.WindowBrush);
        Set("SurfaceBrush", WpfSystemColors.WindowBrush); Set("SurfaceRaisedBrush", WpfSystemColors.ControlBrush);
        Set("SurfaceHoverBrush", WpfSystemColors.HighlightBrush); Set("SurfacePressedBrush", WpfSystemColors.HighlightBrush);
        Set("ActivityRailBrush", WpfSystemColors.WindowBrush); Set("BoundaryBrush", WpfSystemColors.WindowTextBrush);
        Set("DividerBrush", WpfSystemColors.GrayTextBrush); Set("TextPrimaryBrush", WpfSystemColors.WindowTextBrush);
        Set("TextSecondaryBrush", WpfSystemColors.WindowTextBrush); Set("TextTertiaryBrush", WpfSystemColors.GrayTextBrush);
        Set("CyanBrush", WpfSystemColors.HighlightBrush); Set("AquaBrush", WpfSystemColors.HighlightBrush);
        Set("FocusBrush", WpfSystemColors.HotTrackBrush); Set("SuccessBrush", WpfSystemColors.HighlightBrush);
        Set("WarningBrush", WpfSystemColors.WindowTextBrush); Set("ErrorBrush", WpfSystemColors.WindowTextBrush);
        Set("AccentTextBrush", WpfSystemColors.HighlightTextBrush);
        Set("AtmosphereBrush", System.Windows.Media.Brushes.Transparent);
        Set("AtmosphereLineBrush", System.Windows.Media.Brushes.Transparent);
        Set("SelectedSurfaceBrush", WpfSystemColors.HighlightBrush);
        Set("SelectedTextBrush", WpfSystemColors.HighlightTextBrush);
        Set("SelectedThumbBrush", WpfSystemColors.HighlightTextBrush);
        Set("CloseHoverBrush", WpfSystemColors.HighlightBrush);
        Set("ClosePressedBrush", WpfSystemColors.HighlightBrush);
        Set("ScrimBrush", WpfSystemColors.WindowBrush); Set("InkBrush", WpfSystemColors.WindowTextBrush);
        Set("MutedBrush", WpfSystemColors.WindowTextBrush); Set("LineBrush", WpfSystemColors.WindowTextBrush);
        Set("PanelBrush", WpfSystemColors.ControlBrush);
        Set("ReservoirBrush", WpfSystemColors.HighlightBrush);
        WpfApplication.Current.Resources["DialogShadowOpacity"] = 0d;
        WpfApplication.Current.Resources["FloatingShadowEnabled"] = false;
    }

    private static void Set(string key, System.Windows.Media.Brush brush) => WpfApplication.Current.Resources[key] = brush;
}
