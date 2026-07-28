using System.Runtime.InteropServices;

namespace Onyx.Core.Detection;

/// <summary>
/// Picks the DirectML adapter to run inference on. On a laptop, adapter 0 is
/// usually the integrated Intel GPU; the discrete GPU (e.g. RTX 4050) is a
/// later index. We enumerate DXGI adapters and choose the one with the most
/// dedicated video memory — the discrete card.
/// </summary>
internal static class GpuSelector
{
    [DllImport("dxgi.dll")]
    private static extern int CreateDXGIFactory1(ref Guid riid, out IntPtr ppFactory);

    private static readonly Guid IID_IDXGIFactory1 = new("770aae78-f26f-4dba-a829-253c83d1b387");

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct DxgiAdapterDesc1
    {
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string Description;
        public uint VendorId, DeviceId, SubSysId, Revision;
        public UIntPtr DedicatedVideoMemory;
        public UIntPtr DedicatedSystemMemory;
        public UIntPtr SharedSystemMemory;
        public long AdapterLuid;
        public uint Flags;
    }

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate int EnumAdapters1(IntPtr self, uint index, out IntPtr adapter);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate int GetDesc1(IntPtr self, out DxgiAdapterDesc1 desc);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate uint Release(IntPtr self);

    private static T Vtbl<T>(IntPtr obj, int slot) where T : Delegate
    {
        IntPtr vtbl = Marshal.ReadIntPtr(obj);
        IntPtr fn = Marshal.ReadIntPtr(vtbl, slot * IntPtr.Size);
        return Marshal.GetDelegateForFunctionPointer<T>(fn);
    }

    /// <summary>Returns (adapterIndex, name) of the discrete GPU, or (0, "") on failure.</summary>
    public static (int index, string name) SelectDiscrete()
    {
        try
        {
            var iid = IID_IDXGIFactory1;
            if (CreateDXGIFactory1(ref iid, out IntPtr factory) != 0 || factory == IntPtr.Zero)
            {
                return (0, string.Empty);
            }

            var enumAdapters = Vtbl<EnumAdapters1>(factory, 12);
            int best = 0;
            string name = string.Empty;
            ulong bestMem = 0;

            uint i = 0;
            while (enumAdapters(factory, i, out IntPtr adapter) == 0 && adapter != IntPtr.Zero)
            {
                var getDesc = Vtbl<GetDesc1>(adapter, 10);
                if (getDesc(adapter, out DxgiAdapterDesc1 desc) == 0)
                {
                    bool isSoftware = (desc.Flags & 2u) != 0; // DXGI_ADAPTER_FLAG_SOFTWARE
                    ulong mem = desc.DedicatedVideoMemory.ToUInt64();
                    if (!isSoftware && mem > bestMem)
                    {
                        bestMem = mem;
                        best = (int)i;
                        name = desc.Description;
                    }
                }
                Vtbl<Release>(adapter, 2)(adapter);
                i++;
            }

            Vtbl<Release>(factory, 2)(factory);
            return (best, name);
        }
        catch
        {
            return (0, string.Empty);
        }
    }
}
