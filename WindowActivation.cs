using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace Waterline;

public static class WindowActivation
{
    public static void Restore(Window window)
    {
        window.Show();
        var handle = new WindowInteropHelper(window).Handle;
        if (window.WindowState == WindowState.Minimized)
        {
            window.WindowState = WindowState.Normal;
            if (handle != IntPtr.Zero) ShowWindowAsync(handle, 9);
        }
        if (handle == IntPtr.Zero || !SetForegroundWindow(handle))
        {
            window.Activate();
            if (handle != IntPtr.Zero)
            {
                var info = new FlashInfo
                {
                    Size = (uint)Marshal.SizeOf<FlashInfo>(),
                    Window = handle,
                    Flags = 3,
                    Count = 3,
                    Timeout = 0
                };
                FlashWindowEx(ref info);
            }
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct FlashInfo
    {
        public uint Size;
        public IntPtr Window;
        public uint Flags;
        public uint Count;
        public uint Timeout;
    }

    [DllImport("user32.dll")] private static extern bool SetForegroundWindow(IntPtr window);
    [DllImport("user32.dll")] private static extern bool ShowWindowAsync(IntPtr window, int command);
    [DllImport("user32.dll")] private static extern bool FlashWindowEx(ref FlashInfo info);
}
