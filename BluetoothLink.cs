using System.Runtime.InteropServices;
using System.Globalization;
namespace BatteryDisplay;

enum LinkState { Unknown, Connected, Disconnected }
static class BluetoothLink
{
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    struct DeviceInfo
    {
        public uint Size; public ulong Address; public uint Class;
        public int Connected, Remembered, Authenticated;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 16)] public ushort[] Times;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 248)] public string Name;
    }
    [DllImport("bthprops.cpl")] static extern uint BluetoothGetDeviceInfo(IntPtr radio, ref DeviceInfo info);
    public static LinkState Get(string id)
    {
        if (!id.StartsWith("BTHENUM\\DEV_")) return LinkState.Unknown;
        var text = id.Split("DEV_")[1].Split('\\')[0];
        if (!ulong.TryParse(text, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var address)) return LinkState.Unknown;
        var info = new DeviceInfo { Size = (uint)Marshal.SizeOf<DeviceInfo>(), Address = address, Times = new ushort[16], Name = "" };
        return BluetoothGetDeviceInfo(IntPtr.Zero, ref info) == 0 ? info.Connected != 0 ? LinkState.Connected : LinkState.Disconnected : LinkState.Unknown;
    }
}

