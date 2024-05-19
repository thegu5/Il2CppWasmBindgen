using System.Data.Common;
using System.Diagnostics;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using AssetRipper.Primitives;
using Cpp2IL.Core;
using Cpp2IL.Core.Api;
using Cpp2IL.Core.InstructionSets;
using Cpp2IL.Core.Model.Contexts;
using Cpp2IL.Core.Utils;
using static Extensions.Extensions;
using LibCpp2IL;
using LibCpp2IL.BinaryStructures;
using LibCpp2IL.Metadata;
using LibCpp2IL.Reflection;
using LibCpp2IL.Wasm;
using StableNameDotNet.Providers;

#region proc arg handling

if (args.Length != 3)
{
    Console.OpenStandardError().Write("Make sure to provide three arguments:\n"u8);
    Console.OpenStandardError().Write("1. WASM file\n"u8);
    Console.OpenStandardError().Write("2. global-metadata.dat file\n"u8);
    Console.OpenStandardError().Write("3. Output file (probably ending in .json)\n"u8);
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

Console.WriteLine("Parsing all classes...");
Dictionary<string, Il2CppClass> classDict = [];

foreach (var t in Cpp2IlApi.CurrentAppContext.AllTypes)
{
    if (t.DeclaringType is not null) continue;
    classDict.TryAdd(t.FullName, t);
}

#region Serialize (no more tree, sadge)

Console.WriteLine("Serializing to " + args[2] + "...");
var sorted = from entry in classDict orderby entry.Value.InheritanceDepth ascending select entry;

var opts = new JsonSerializerOptions
{
    ReferenceHandler = ReferenceHandler.IgnoreCycles,
    Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    TypeInfoResolver = SourceGenerationContext.Default
};
File.WriteAllText(args[2],
    JsonSerializer.Serialize<IOrderedEnumerable<KeyValuePair<string, Il2CppClass>>>(sorted, opts));

#endregion

Console.WriteLine("Done!");

namespace Extensions
{
    static class Extensions
    {

        public static int GetInheritanceDepth(this TypeAnalysisContext? type)
        {
            var counter = 0;
            while (type.BaseType is not null)
            {
                type = type.BaseType;
                counter++;
            }

            return counter;
        }

        public static int? GetWasmIndex(this Il2CppMethodDefinition method)
        {
            var wasmdef = WasmUtils.TryGetWasmDefinition(method);
            if (wasmdef is null) return null;
            return wasmdef.IsImport
                ? ((WasmFile)LibCpp2IlMain.Binary!).FunctionTable.IndexOf(wasmdef)
                : wasmdef.FunctionTableIndex;
        }

        public static string? FixedRetString(this ITypeInfoProvider t)
        {
            if (t.RewrittenTypeName == "Void") return null;
            return t.TypeNamespace == "" ? t.RewrittenTypeName : t.TypeNamespace + "." + t.RewrittenTypeName;
        }
    }
}

#region Serializable data types

// var treeRoot = TypeTreeBuilder.BuildTree(classDict);
[JsonSourceGenerationOptions(WriteIndented = true)]
[JsonSerializable(typeof(IOrderedEnumerable<KeyValuePair<string, Il2CppClass>>))]
internal partial class SourceGenerationContext : JsonSerializerContext;

public record Il2CppField(
    string Name,
    int Offset,
    string Type // maybe do better abstraction idk
);

public record Il2CppMethod(
    string Name,
    Il2CppParameter[] Parameters,
    string? ReturnType,
    bool IsStatic,
    int? Index,
    ulong MethodInfoPtr
);

public record Il2CppParameter(
    string Name,
    string Type
);

public record Il2CppClass(
    string Name,
    string[] GenericParams,
    string? BaseType,
    string Namespace,
    bool IsStruct,
    int InheritanceDepth,
    Il2CppField[] Fields,
    Il2CppMethod[] Methods,
    Il2CppClass[] NestedClasses
)
{
    public static implicit operator Il2CppClass(TypeAnalysisContext t)
    {

        return new Il2CppClass(
            t.Name,
            t.Definition.GenericContainer is not null ? t.Definition.GenericContainer.GenericParameters.Select(gp => gp.Name).ToArray() : [],
            t.BaseType?.Definition?.FullName.Replace("/", "."),
            t.Namespace,
            t.IsValueType || t.IsEnumType,
            t.GetInheritanceDepth(),
            t.Fields.Select(f => new Il2CppField(
                f.Name,
                f.Offset,
                f.FieldTypeContext.FullName
            )).ToArray(),
            t.Methods.Select(m => new Il2CppMethod(
                m.Definition.Name,
                m.Parameters.Select(p => new Il2CppParameter(p.Name, p.ReadableTypeName.Replace("/", "."))).ToArray(),
                m.ReturnType.FixedRetString(),
                m.IsStatic,
                m.Definition.GetWasmIndex(),
                m.UnderlyingPointer
            )).ToArray(),
            t.NestedTypes.Select(n => (Il2CppClass) n).ToArray()
        );
    }
}

#endregion