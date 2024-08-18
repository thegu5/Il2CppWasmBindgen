using System.Diagnostics;
using System.Reflection;
using AssetRipper.Primitives;
using BepInEx.AssemblyPublicizer;
using Cpp2IL.Core;
using Cpp2IL.Core.Api;
using Cpp2IL.Core.InstructionSets;
using Cpp2IL.Core.OutputFormats;
using Cpp2IL.Core.ProcessingLayers;
using Il2CppWasmBindgen;
using LibCpp2IL;

#region proc arg handling

if (args.Length != 2)
{
    Console.OpenStandardError().Write("Make sure to provide two arguments:\n"u8);
    Console.OpenStandardError().Write("1. WASM file\n"u8);
    Console.OpenStandardError().Write("2. global-metadata.dat file\n"u8);
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
Cpp2IlApi.InitializeLibCpp2Il(args[0],
    args[1],
    new UnityVersion(/*2023*/2022, 2, 5));

#endregion

Console.WriteLine("Running processing layers...");
new InterfaceMethodFixProcessingLayer().Process(Cpp2IlApi.CurrentAppContext); // <- for debugging
new AttributeInjectorProcessingLayer().Process(Cpp2IlApi.CurrentAppContext);
new WasmMethodAttributeProcessingLayer().Process(Cpp2IlApi.CurrentAppContext);


Console.WriteLine("Building assemblies...");
Directory.CreateDirectory(Path.Combine(Directory.GetCurrentDirectory(), "cpp2il_out"));
new AsmResolverDllOutputFormatThrowNull().BuildAssemblies(Cpp2IlApi.CurrentAppContext).ForEach(asm =>
    asm.Write(Path.Combine(Directory.GetCurrentDirectory(), "cpp2il_out", asm.Name + ".dll")));

// This is both needed to access certain types and to (try to) fix a cpp2il bug
// https://github.com/SamboyCoding/Cpp2IL/issues/310
Console.WriteLine("Publicizing...");
var assemblypaths = Directory.GetFiles(Path.Combine(Directory.GetCurrentDirectory(), "cpp2il_out"));
assemblypaths.ToList().ForEach(p => AssemblyPublicizer.Publicize(p, p, new AssemblyPublicizerOptions
{
    IncludeOriginalAttributesAttribute = false,
    PublicizeCompilerGenerated = true,
    Strip = false
}));

Console.WriteLine("Reading assemblies back from disk...");
assemblypaths = assemblypaths.Where(path => path.Contains("Assembly-CSharp") || path.Contains("mscorlib")).ToArray();
Console.WriteLine($"Assemblies: {assemblypaths.Count()}");
var modules = assemblypaths.Select(Assembly.LoadFrom);

foreach (var module in modules)
{
    Console.WriteLine($"Module: {module.FullName}"); // todo: fix mscorlib not being processed here idk
    foreach (var type in module.DefinedTypes)
    {
        Console.WriteLine($"Type: {type.FullName}");
        foreach (var method in type.DeclaredMethods)
        {
            var wasmattrs = method.CustomAttributes.Where(a => a.AttributeType.Name == "WasmMethod");
            if (wasmattrs.Count() != 0 && method.IsStatic)
            {
                var idx = (int)wasmattrs.First().NamedArguments.First().TypedValue.Value;
                Console.WriteLine("Method " + method.Name + " has idx " + idx);
            }
    
        }
    }
}