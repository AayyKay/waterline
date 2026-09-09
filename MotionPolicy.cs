using System.Windows;

namespace Waterline;

internal static class MotionPolicy
{
    private static bool _forceReduced;
    public static bool IsEnabled => !_forceReduced && SystemParameters.ClientAreaAnimation && !SystemParameters.HighContrast;
    public static void ForceReducedForSnapshot() => _forceReduced = true;
}
