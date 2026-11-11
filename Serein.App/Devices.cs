using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace Serein.App;
public record AudioDevice(string Id, string Name) { public override string ToString() => Name; }
public static class Devices
{
    public static List<AudioDevice> List()
    {
        var list = new List<AudioDevice> { new("*", T("Alle Wiedergabegeräte")) };
        using var render = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\MMDevices\Audio\Render");
        if (render == null) return list;
        foreach (var key in render.GetSubKeyNames())
        {
            using var endpoint = render.OpenSubKey(key);
            if (Convert.ToInt32(endpoint?.GetValue("DeviceState", 0)) != 1) continue;
            using var props = endpoint?.OpenSubKey("Properties");
            var name = props?.GetValue("{a45c254e-df1c-4efd-8020-67d146a850e0},14") as string;
            if (string.IsNullOrWhiteSpace(name))
            {
                var description = props?.GetValue("{a45c254e-df1c-4efd-8020-67d146a850e0},2") as string;
                var adapter = props?.GetValue("{b3f8fa53-0004-438e-9003-51a46e139bfc},6") as string;
                name = description == null ? key : description + (adapter == null ? "" : " (" + adapter + ")");
            }
            list.Add(new("{0.0.0.00000000}." + key, name));
        }
        return list;
    }
    public static string? DefaultId()
    {
        object? enumerator = null; IMMDevice? device = null; IntPtr ptr = IntPtr.Zero;
        try
        {
            enumerator = Activator.CreateInstance(Type.GetTypeFromCLSID(new("BCDE0395-E52F-467C-8E3D-C4579291692E"))!);
            Marshal.ThrowExceptionForHR(((IMMDeviceEnumerator)enumerator!).GetDefaultAudioEndpoint(0, 1, out device));
            Marshal.ThrowExceptionForHR(device.GetId(out ptr)); return Marshal.PtrToStringUni(ptr);
        }
        catch { return null; }
        finally { if (ptr != IntPtr.Zero) Marshal.FreeCoTaskMem(ptr); if (device != null) Marshal.ReleaseComObject(device); if (enumerator != null) Marshal.ReleaseComObject(enumerator); }
    }
    [ComImport, Guid("A95664D2-9614-4F35-A746-DE8DB63617E6"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IMMDeviceEnumerator
    {
        [PreserveSig] int EnumAudioEndpoints(int flow, uint mask, out IntPtr devices);
        [PreserveSig] int GetDefaultAudioEndpoint(int flow, int role, out IMMDevice endpoint);
    }
    [ComImport, Guid("D666063F-1587-4E43-81F1-B948E807363F"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IMMDevice
    {
        [PreserveSig] int Activate(ref Guid iid, uint context, IntPtr parameters, out IntPtr instance);
        [PreserveSig] int OpenPropertyStore(uint mode, out IntPtr properties);
        [PreserveSig] int GetId(out IntPtr id);
    }
}
