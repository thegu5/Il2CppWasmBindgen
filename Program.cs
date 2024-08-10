using System.Diagnostics;
using System.Runtime.InteropServices.JavaScript;
using AsmResolver.DotNet;
using AsmResolver.DotNet.Code.Cil;
using AsmResolver.DotNet.Signatures;
using AsmResolver.PE.DotNet.Cil;
using AssetRipper.Primitives;
using BepInEx.AssemblyPublicizer;
using Cpp2IL.Core;
using Cpp2IL.Core.Api;
using Cpp2IL.Core.InstructionSets;
using Cpp2IL.Core.OutputFormats;
using Cpp2IL.Core.ProcessingLayers;
using Il2CppWasmBindgen;
using LibCpp2IL;
using AsmResolver.PE.DotNet.Metadata.Tables;

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
new WasmDirectILOutputFormat().BuildAssemblies(Cpp2IlApi.CurrentAppContext).ForEach(asm =>
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

/*Console.WriteLine("Generating Javascript interop assembly...");
var interopModule = new ModuleDefinition("Il2CppWasmBindgen.dll");
var interopType = new TypeDefinition("", "Il2CppWasmBindgen", TypeAttributes.Public | TypeAttributes.Class);
var callMethod = new MethodDefinition(
    "Call",
    MethodAttributes.Public | MethodAttributes.Static,
    MethodSignature.CreateStatic(
        interopModule.CorLibTypeFactory.Void,
        new ArrayTypeSignature(interopModule.CorLibTypeFactory.Object)
    )
);
// callMethod.CustomAttributes.Add(new CustomAttribute(typeof(JSImportAttribute).GetConstructor([]))
interopModule.TopLevelTypes.Add(interopType);

Console.WriteLine("Reading assemblies back from disk...");
assemblypaths = assemblypaths.ToList().Where(path => path.Contains("Assembly-CSharp")).ToArray();
var modules = assemblypaths.Select(ModuleDefinition.FromFile);

foreach (var module in modules)
{
    foreach (var type in module.TopLevelTypes)
    {
        Console.WriteLine(type.FullName);
        foreach (var method in type.Methods)
        {
            var wasmattrs = method.FindCustomAttributes("Cpp2ILInjected", "WasmMethod").ToList();
            if (wasmattrs.Count != 0 && method.IsStatic)
            {
                var idx = (int)wasmattrs.First().Signature.NamedArguments.First().Argument.Element;
                // Console.WriteLine("Method " + method.FullName + " has idx " + idx);
                var body = new CilMethodBody(method);
                method.CilMethodBody = body;
                var in
                body.Instructions.Add(new CilInstruction(CilOpCodes.Call, ))
            }
    
        }
    }
}*/