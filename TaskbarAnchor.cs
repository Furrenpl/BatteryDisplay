using System.Runtime.InteropServices;
namespace BatteryDisplay;

// Explorer owns the taskbar. Read its notification-area rectangle without
// injecting into Explorer or changing the user's taskbar configuration.
static class TaskbarAnchor
{
    [StructLayout(LayoutKind.Sequential)] struct Rect { public int Left, Top, Right, Bottom; public Rectangle Bounds => Rectangle.FromLTRB(Left, Top, Right, Bottom); }
    delegate bool EnumProc(IntPtr window, IntPtr parameter);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern IntPtr FindWindow(string name, string? title);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern IntPtr FindWindowEx(IntPtr parent, IntPtr after, string name, string? title);
    [DllImport("user32.dll")] static extern bool GetWindowRect(IntPtr window, out Rect rectangle);
    [DllImport("user32.dll")] static extern bool IsWindowVisible(IntPtr window);
    [DllImport("user32.dll")] internal static extern bool SetWindowPos(IntPtr window, IntPtr after, int x, int y, int width, int height, uint flags);
    [DllImport("user32.dll")] internal static extern bool ShowWindow(IntPtr window, int command);
    [DllImport("user32.dll")] internal static extern bool SetForegroundWindow(IntPtr window);
    public static Rectangle? TrayBounds(Screen screen)
    {
        var bar = FindWindow("Shell_TrayWnd", null);
        if (bar != IntPtr.Zero && GetWindowRect(bar, out var primary) && primary.Bounds.IntersectsWith(screen.Bounds))
        {
            var tray = FindWindowEx(bar, IntPtr.Zero, "TrayNotifyWnd", null);
            if (tray != IntPtr.Zero && IsWindowVisible(tray) && GetWindowRect(tray, out var r) && r.Right > r.Left) return r.Bounds;
        }
        return null;
    }
    public static void RaiseWithoutFocus(Form form) => SetWindowPos(form.Handle, new IntPtr(-1), 0, 0, 0, 0, 0x0013);
}


