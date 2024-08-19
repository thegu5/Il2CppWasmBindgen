using System.Runtime.InteropServices;

namespace Il2CppWasmBindgen.Runtime;

public static class Runtime
{
    [WasmImportLinkage]
    [DllImport("env", EntryPoint = "iwbcall")]
    public static extern int? Call(int idx);
}

public static class Plugin
{
    [UnmanagedCallersOnly(EntryPoint = "Start")]
    public static int Start()
    {
        Runtime.Call(0);
        return 42;
    }
}