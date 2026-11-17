using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32.SafeHandles;
namespace BatteryDisplay;

static class Native
{
    [StructLayout(LayoutKind.Sequential)] public struct Info { public uint Size; public Guid Class; public uint Dev; public IntPtr Reserved; }
    [StructLayout(LayoutKind.Sequential)] public struct Key { public Guid Guid; public uint Id; public Key(string g, uint id) { Guid = new(g); Id = id; } }
    [DllImport("setupapi.dll", CharSet = CharSet.Unicode)] public static extern IntPtr SetupDiGetClassDevsW(IntPtr guid, string? enumerator, IntPtr hwnd, uint flags);
    [DllImport("setupapi.dll")] public static extern bool SetupDiEnumDeviceInfo(IntPtr set, uint index, ref Info info);
    [DllImport("setupapi.dll", CharSet = CharSet.Unicode)] public static extern bool SetupDiGetDeviceInstanceIdW(IntPtr set, ref Info info, StringBuilder id, int size, out int required);
    [DllImport("setupapi.dll", CharSet = CharSet.Unicode)] public static extern bool SetupDiGetDevicePropertyW(IntPtr set, ref Info info, ref Key key, out uint type, byte[] data, uint size, out uint required, uint flags);
    [DllImport("setupapi.dll")] public static extern bool SetupDiDestroyDeviceInfoList(IntPtr set);
    [DllImport("setupapi.dll", CharSet = CharSet.Unicode)] public static extern IntPtr SetupDiGetClassDevsW(ref Guid guid, string? enumerator, IntPtr hwnd, uint flags);
    [DllImport("setupapi.dll")] public static extern bool SetupDiEnumDeviceInterfaces(IntPtr set, IntPtr dev, ref Guid guid, uint index, ref Info info);
    [DllImport("setupapi.dll", CharSet = CharSet.Unicode)] public static extern bool SetupDiGetDeviceInterfaceDetailW(IntPtr set, ref Info info, IntPtr detail, uint size, out uint needed, IntPtr dev);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)] public static extern SafeFileHandle CreateFileW(string path, uint access, uint share, IntPtr security, uint creation, uint flags, IntPtr template);
    [DllImport("hid.dll")] public static extern bool HidD_GetFeature(SafeFileHandle h, byte[] data, int len);
    [DllImport("hid.dll")] public static extern bool HidD_SetFeature(SafeFileHandle h, byte[] data, int len);
    [DllImport("hid.dll")] public static extern bool HidD_GetPreparsedData(SafeFileHandle h, out IntPtr data);
    [DllImport("hid.dll")] public static extern bool HidD_FreePreparsedData(IntPtr data);
    [DllImport("hid.dll")] public static extern int HidP_GetCaps(IntPtr data, byte[] caps);
    public static List<string> HidPaths() { var paths = new List<string>(); Guid g = new("4d1e55b2-f16f-11cf-88cb-001111000030"); var s = SetupDiGetClassDevsW(ref g, null, IntPtr.Zero, 18); try { for (uint n = 0; ; n++) { var i = new Info { Size = (uint)Marshal.SizeOf<Info>() }; if (!SetupDiEnumDeviceInterfaces(s, IntPtr.Zero, ref g, n, ref i)) break; SetupDiGetDeviceInterfaceDetailW(s, ref i, IntPtr.Zero, 0, out var size, IntPtr.Zero); var p = Marshal.AllocHGlobal((int)size); try { Marshal.WriteInt32(p, IntPtr.Size == 8 ? 8 : 6); if (SetupDiGetDeviceInterfaceDetailW(s, ref i, p, size, out _, IntPtr.Zero)) paths.Add(Marshal.PtrToStringUni(p + 4)!); } finally { Marshal.FreeHGlobal(p); } } } finally { SetupDiDestroyDeviceInfoList(s); } return paths; }
    public static List<Reading> Scan()
    {
        var list = new List<Reading>(); var s = SetupDiGetClassDevsW(IntPtr.Zero, null, IntPtr.Zero, 6); try { for (uint n = 0; ; n++) { var i = new Info { Size = (uint)Marshal.SizeOf<Info>() }; if (!SetupDiEnumDeviceInfo(s, n, ref i)) break; var b = new StringBuilder(1024); SetupDiGetDeviceInstanceIdW(s, ref i, b, b.Capacity, out _); string id = b.ToString(); if (!id.StartsWith("BTHENUM\\") && !id.StartsWith("BTHLE\\")) continue; string name = ReadString(s, ref i, 14); if (name.Length == 0) name = ReadString(s, ref i, 2); var k = new Key("104ea319-6ee2-4701-bd47-8ddbf425bbe5", 2); byte[] data = new byte[512]; int? value = null; if (SetupDiGetDevicePropertyW(s, ref i, ref k, out var t, data, 512, out _, 0)) { int v = t == 3 ? data[0] : BitConverter.ToInt32(data); if (v >= 0 && v <= 100) value = v; } if (!value.HasValue && !id.Contains("\\DEV_")) continue; list.Add(new(id, name, value, "Наушники", value.HasValue ? "Заряд из кэша Windows" : "Windows не сообщает заряд")); } } finally { SetupDiDestroyDeviceInfoList(s); }
        var paths = HidPaths(); if (paths.Any(p => p.Contains("vid_3151&pid_5007"))) { var v = Dongles.Ajazz(paths); list.Add(new("ajazz", "AJAZZ AJ159 Apex", v, "Мышь", v.HasValue ? "Донгл • HID" : "Донгл найден • нет телеметрии")); }
        if (paths.Any(p => p.Contains("vid_05ac&pid_024f"))) { var v = Dongles.Aula(paths); list.Add(new("aula", "AULA F108 Pro", v, "Клавиатура", v.HasValue ? "Донгл • HID" : "Донгл найден • нет телеметрии")); }
        if (paths.Any(p => p.Contains("vid_2dc8&pid_301c"))) list.Add(new("8bitdo", "8BitDo U2C", null, "Геймпад", "Донгл найден • нет данных о заряде"));
        foreach (var parent in list.Where(r => r.Id.Contains("\\DEV_")).ToList()) { string address = parent.Id.Split("DEV_")[1].Split('\\')[0]; var service = list.FirstOrDefault(r => !r.Id.Contains("\\DEV_") && r.Id.Contains(address) && r.Percent.HasValue); if (service != null) { list[list.IndexOf(parent)] = parent with { Percent = service.Percent, Status = service.Status }; list.Remove(service); } }
        list.AddRange(Controllers.Scan());
        for (int n = 0; n < list.Count; n++)
        {
            var r = list[n];
            var link = r.Id.StartsWith("BTHENUM\\DEV_") ? BluetoothLink.Get(r.Id) : r.Connection;
            if (link == LinkState.Unknown && r.Percent.HasValue && !r.Id.StartsWith("BTH")) link = LinkState.Connected;
            string kind = r.Name.Contains("AJ159", StringComparison.OrdinalIgnoreCase) ? "Мышь" : r.Kind;
            list[n] = r with
            {
                Connection = link,
                Kind = kind,
                Name = r.Id.StartsWith("BTH") ? r.Name + " (Bluetooth)" : r.Id == "ajazz" ? r.Name + " (2.4G)" : r.Name,
                Percent = link == LinkState.Disconnected || (r.Id.StartsWith("BTHLE") && link == LinkState.Unknown) ? null : r.Percent,
                Status = r.Status + " • " + (link switch { LinkState.Connected => "Подключено", LinkState.Disconnected => "Не подключено", _ => "Подключение неизвестно" })
            };
        }
        return list;
    }
    static string ReadString(IntPtr s, ref Info i, uint id) { var k = new Key("a45c254e-df1c-4efd-8020-67d146a850e0", id); byte[] d = new byte[1024]; return SetupDiGetDevicePropertyW(s, ref i, ref k, out _, d, 1024, out _, 0) ? Encoding.Unicode.GetString(d).TrimEnd('\0') : ""; }
}
record Reading(string Id, string Name, int? Percent, string Kind, string Status, LinkState Connection = LinkState.Unknown);





