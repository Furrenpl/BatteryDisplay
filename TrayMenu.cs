using System.Runtime.InteropServices;
namespace BatteryDisplay;

// A native popup uses the same physical coordinate space as the cursor.
// Avoid NotifyIcon's managed popup coordinate conversion across mixed-DPI screens.
sealed class TrayMenu : Form
{
    [DllImport("user32.dll")] static extern IntPtr CreatePopupMenu();
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern bool AppendMenu(IntPtr menu, uint flags, nuint id, string text);
    [DllImport("user32.dll")] static extern bool DestroyMenu(IntPtr menu);
    [DllImport("user32.dll")] static extern bool GetCursorPos(out Point point);
    [DllImport("user32.dll")] static extern uint TrackPopupMenuEx(IntPtr menu, uint flags, int x, int y, IntPtr owner, IntPtr parameters);
    [DllImport("user32.dll")] static extern bool PostMessage(IntPtr hwnd, uint message, IntPtr w, IntPtr l);
    public TrayMenu() { ShowInTaskbar = false; FormBorderStyle = FormBorderStyle.None; _ = Handle; }
    public uint ShowAtCursor()
    {
        if (!GetCursorPos(out var point)) return 0;
        var menu = CreatePopupMenu();
        try
        {
            AppendMenu(menu, 0, 1, "Настройки"); AppendMenu(menu, 0, 2, "Обновить заряд"); AppendMenu(menu, 0, 3, "Выход");
            TaskbarAnchor.SetForegroundWindow(Handle);
            var command = TrackPopupMenuEx(menu, 0x0100 | 0x0002, point.X, point.Y, Handle, IntPtr.Zero);
            PostMessage(Handle, 0, IntPtr.Zero, IntPtr.Zero); return command;
        }
        finally { DestroyMenu(menu); }
    }
}

