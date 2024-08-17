using System.Runtime.InteropServices;

namespace LibIl2CppWasmBindgen;

public static class LibIl2CppWasmBindgen
{
    [WasmImportLinkage]
    [DllImport("env", EntryPoint = "iwbcall")]
    public static extern void Call(int idx);
}

public static class Plugin
{
    [UnmanagedCallersOnly(EntryPoint = "Start")]
    public static int Start()
    {
        LibIl2CppWasmBindgen.Call(0);
        return 42;
    }
}