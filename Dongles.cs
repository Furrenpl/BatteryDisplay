using Microsoft.Win32.SafeHandles;
using System.Runtime.InteropServices;
namespace BatteryDisplay;

static class Dongles
{
    public static byte[]? Caps(SafeFileHandle h) { if (!Native.HidD_GetPreparsedData(h, out var p)) return null; try { var c = new byte[64]; return Native.HidP_GetCaps(p, c) >= 0 ? c : null; } finally { Native.HidD_FreePreparsedData(p); } }
    public static int? Ajazz(List<string> paths) { var endpoints = paths.Where(p => p.Contains("vid_3151&pid_5007")).ToList(); foreach (var p in endpoints) { using var h = Native.CreateFileW(p, 0, 3, IntPtr.Zero, 3, 0, IntPtr.Zero); if (h.IsInvalid) continue; var c = Caps(h); if (c == null || BitConverter.ToUInt16(c, 2) != 0xffff || BitConverter.ToUInt16(c, 0) != 2) continue; int len = BitConverter.ToUInt16(c, 8); if (len < 8) continue; var b = new byte[len]; b[1] = 0xf7; Native.HidD_SetFeature(h, b, len); } Thread.Sleep(50); foreach (var p in endpoints) { using var h = Native.CreateFileW(p, 0, 3, IntPtr.Zero, 3, 0, IntPtr.Zero); if (h.IsInvalid) continue; var c = Caps(h); if (c == null) continue; int len = BitConverter.ToUInt16(c, 8); if (len < 8) continue; var b = new byte[len]; b[0] = 5; bool ok = Native.HidD_GetFeature(h, b, len);  if (ok && b[0] == 5 && b[1] == 0 && b[2] == 0 && b[3] > 0 && b[3] <= 100 && b[4] == 1) return b[3]; } return null; }
    public static int? Aula(List<string> paths) { foreach (var p in paths.Where(p => p.Contains("vid_05ac&pid_024f"))) { using var inspect = Native.CreateFileW(p, 0, 3, IntPtr.Zero, 3, 0, IntPtr.Zero); if (inspect.IsInvalid) continue; var c = Caps(inspect); if (c == null || BitConverter.ToUInt16(c, 2) != 0xff60) continue; int input = BitConverter.ToUInt16(c, 4), output = BitConverter.ToUInt16(c, 6); if (input < 4 || output < 33) continue; try { return Query(p, input, output).GetAwaiter().GetResult(); } catch (IOException) { } catch (OperationCanceledException) { } } return null; }
    static async Task<int?> Query(string path, int input, int output) { using var h = Native.CreateFileW(path, 0xc0000000, 3, IntPtr.Zero, 3, 0x40000000, IntPtr.Zero); if (h.IsInvalid) return null; using var stream = new FileStream(h, FileAccess.ReadWrite, 4096, true); using var timeout = new CancellationTokenSource(1000); var request = new byte[output]; request[1] = 0x20; request[2] = 1; request[32] = 0x21; await stream.WriteAsync(request, timeout.Token); var response = new byte[input]; while (!timeout.IsCancellationRequested) { int n = await stream.ReadAsync(response, timeout.Token);  if (n == 0) return null; if (n >= 33 && response[0] == 0 && response[1] == 0x20 && response[2] == 1 && response[4] <= 100 && (response.Skip(1).Take(31).Sum(x => (int)x) & 255) == response[32]) return response[4]; } return null; }
}







