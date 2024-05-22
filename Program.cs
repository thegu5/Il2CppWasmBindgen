using System.Diagnostics;
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
using LibCpp2IL.Metadata;
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
var alltypes = Cpp2IlApi.CurrentAppContext.AllTypes.ToList();

foreach (var t in alltypes.Where(t => t.DeclaringType is null))
{
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
        public static int GetInheritanceDepth(this TypeAnalysisContext type)
        {
            var counter = 0;
            while (type.BaseType is not null)
            {
                type = type.BaseType;
                counter++;
            }

            return counter;
        }

        public static List<string> GetAllDeps(this TypeAnalysisContext type)
        {
            var toret = new List<string>();
            if (type.BaseType is not null)
            {
                var rep = Regex.Replace(type.BaseType.UltimateDeclaringType.Name, @"(`\d)(<.+>$)", "$1")
                    .Replace("[]", "");
                toret.Add(rep);
                /*if (type.BaseType.Name.Contains('['))
                {
                    Console.WriteLine("From " + type.BaseType.Name + " to " + rep);
                }*/
            }

            var counter = 0;
            foreach (var nested in type.NestedTypes)
            {
                counter++;
                toret = toret.Concat(nested.GetAllDeps()).ToList();
            }

            // if (counter > 1) Debugger.Break();
            return toret;
        }

        public static int? GetWasmIndex(this Il2CppMethodDefinition method)
        {
            var wasmdef = WasmUtils.TryGetWasmDefinition(method);
            if (wasmdef is null) return null;
            return wasmdef.IsImport
                ? ((WasmFile)LibCpp2IlMain.Binary!).FunctionTable.IndexOf(wasmdef)
                : wasmdef.FunctionTableIndex;
        }

        public static Il2CppType ToIl2CppType(this ITypeInfoProvider t)
        {
            if (t.OriginalTypeName == "Error" && t.TypeNamespace == "") Debugger.Break();
            if (t is GenericInstanceTypeAnalysisContext gent) return gent.GenericType.ToIl2CppType();
            if (t.OriginalTypeName == "Property`2<Angle, AngleUnit>")
            {
                Debugger.Break();
            }
            var thing = new Il2CppType(
                t.OriginalTypeName,
                t.GenericArgumentInfoProviders.Select(prov => prov.ToIl2CppType()).ToArray(),
                t is TypeAnalysisContext { Definition: not null, GenericParameterCount: > 0 } context ? context.Definition.GenericContainer.GenericParameters.Select<Il2CppGenericParameter, string>(gp => gp.Name!).ToArray() : [],
                t.DeclaringTypeInfoProvider is null ?
                    t.TypeNamespace
                    :
                    t.DeclaringTypeInfoProvider.TypeNamespace == "" ?
                        t.DeclaringTypeInfoProvider.OriginalTypeName
                        :
                        t.DeclaringTypeInfoProvider.TypeNamespace + "." + t.DeclaringTypeInfoProvider.OriginalTypeName,
                t.DeclaringTypeInfoProvider is not null
            );
            return thing;
        }
    }
}

#region Serializable data types

// var treeRoot = TypeTreeBuilder.BuildTree(classDict);
[JsonSourceGenerationOptions(WriteIndented = true)]
[JsonSerializable(typeof(IOrderedEnumerable<KeyValuePair<string, Il2CppClass>>))]
internal partial class SourceGenerationContext : JsonSerializerContext;

public record Il2CppType(
    string Name,
    Il2CppType[] GenericArgs,
    string[] GenericParams,
    string Namespace,
    bool IsNested
);

public record Il2CppField(
    string Name,
    int Offset,
    Il2CppType Type
);

public record Il2CppMethod(
    string Name,
    Il2CppParameter[] Parameters,
    Il2CppType? ReturnType,
    bool IsStatic,
    int? Index,
    ulong MethodInfoPtr
);

public record Il2CppParameter(
    string Name,
    Il2CppType Type
);

public record Il2CppClass(
    Il2CppType Type,
    Il2CppType? BaseType,
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
            t.ToIl2CppType(),
            t.BaseType?.ToIl2CppType(),
            t.IsValueType || t.IsEnumType,
            t.GetInheritanceDepth(),
            t.Fields.Select(f => new Il2CppField(
                f.Name,
                f.Offset,
                f.FieldTypeInfoProvider.ToIl2CppType()
            )).ToArray(),
            t.Methods.Select(m => new Il2CppMethod(
                m.Definition.Name,
                m.Parameters.Select(p => new Il2CppParameter(p.Name, p.ParameterTypeInfoProvider.ToIl2CppType()))
                    .ToArray(),
                m.ReturnType.OriginalTypeName == "Void" ? null : m.ReturnType.ToIl2CppType(),
                m.IsStatic,
                m.Definition.GetWasmIndex(),
                m.UnderlyingPointer
            )).ToArray(),
            t.NestedTypes.Select(n => (Il2CppClass)n).ToArray()
        );
    }
}

#endregion