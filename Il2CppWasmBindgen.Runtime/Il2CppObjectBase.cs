using System.Diagnostics.CodeAnalysis;

namespace Il2CppWasmBindgen.Runtime;

// TODO: solution for creating GCHandle (no circular dependency)
[method: DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(Il2CppObjectBase))]
public class Il2CppObjectBase(IntPtr pointer)
{
    public IntPtr Pointer = pointer;
}