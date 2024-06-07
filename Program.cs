using System.Diagnostics;
using System.Reflection;
using AssetRipper.Primitives;
using Cpp2IL.Core;
using Cpp2IL.Core.Api;
using Cpp2IL.Core.InstructionSets;
using Cpp2IL.Core.OutputFormats;
using Cpp2IL.Core.ProcessingLayers;
using Il2CppTsBindgen;
using LibCpp2IL;

#pragma warning disable IL2026

#region proc arg handling

if (args.Length != 3)
{
    Console.OpenStandardError().Write("Make sure to provide three arguments:\n"u8);
    Console.OpenStandardError().Write("1. WASM file\n"u8);
    Console.OpenStandardError().Write("2. global-metadata.dat file\n"u8);
    Console.OpenStandardError().Write("3. Output file (probably ending in .ts)\n"u8);
    Process.GetCurrentProcess().Kill();
}

if (!File.Exists(args[0]))
{
    Console.OpenStandardError().Write("First file does not exist"u8);
    Process.GetCurrentProcess().Kill();
}

if (!File.Exists(args[1]))
{
    Console.OpenStandardError().Write("Second file does not exist"u8);
    Process.GetCurrentProcess().Kill();
}

#endregion


#region Cpp2IL Setup

Console.WriteLine("Setting up Cpp2IL...");
InstructionSetRegistry.RegisterInstructionSet<WasmInstructionSet>(DefaultInstructionSets.WASM);
Console.SetIn(new StringReader("4fb240"));
Cpp2IlApi.InitializeLibCpp2Il(args[0],
    args[1],
    new UnityVersion(2023, 2, 5), true);

#endregion

// new InterfaceMethodFixProcessingLayer().Process(Cpp2IlApi.CurrentAppContext);
new AttributeInjectorProcessingLayer().Process(Cpp2IlApi.CurrentAppContext);
new WasmMethodAttributeProcessingLayer().Process(Cpp2IlApi.CurrentAppContext);

Console.WriteLine("Building assemblies...");
Directory.CreateDirectory(Path.Combine(Directory.GetCurrentDirectory(), "cpp2il_out"));
new AsmResolverDllOutputFormatDefault().BuildAssemblies(Cpp2IlApi.CurrentAppContext).ForEach(asm =>
    asm.Write(Path.Combine(Directory.GetCurrentDirectory(), "cpp2il_out", asm.Name + ".dll")));

Process.GetCurrentProcess().Kill();

// broken - resolver doesn't find files, interface implementations are borked somehow
Console.WriteLine("Reading assemblies back from disk...");
var assemblies = Directory.GetFiles(Path.Combine(Directory.GetCurrentDirectory(), "cpp2il_out"))
    .Select(Assembly.LoadFile);
/*
AppDomain.CurrentDomain.AssemblyResolve += (sender, eventArgs) =>
{
    return eventArgs.;
};*/
foreach (var a in assemblies)
{
    Console.WriteLine(a.GetName().Name);
    Console.WriteLine(a.DefinedTypes);
}
Debugger.Break();
