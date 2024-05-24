using System.Diagnostics;
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
using LibCpp2IL;
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
List<Il2CppClass> classDict = [];
var alltypes = Cpp2IlApi.CurrentAppContext.AllTypes.ToList();

foreach (var t in alltypes.Where(t => t.DeclaringType is null))
{
    classDict.Add(t);
}

#region Serialize

Console.WriteLine("Serializing to " + args[2] + "...");
var sorted = from entry in classDict orderby entry.InheritanceDepth ascending select entry;

var opts = new JsonSerializerOptions
{
    ReferenceHandler = ReferenceHandler.IgnoreCycles,
    Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    TypeInfoResolver = SourceGenerationContext.Default
};
File.WriteAllText(args[2],
    JsonSerializer.Serialize<IOrderedEnumerable<Il2CppClass>>(sorted, opts));

#endregion

Console.WriteLine("Done!");

internal static class Extensions
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

    /*public static Il2CppType ToIl2CppType(this TypeAnalysisContext t)
    {
        if (t.GenericParameterCount > 0) Debugger.Break();
        if (t is GenericInstanceTypeAnalysisContext gent) return gent.GenericType.ToIl2CppType();
        var thing = new Il2CppType(
            t.OriginalTypeName,
            t is TypeAnalysisContext ctx
                ? LibCpp2ILUtils.GetTypeReflectionData(LibCpp2IlMain.Binary.GetType(ctx.Definition.TypeIndex))
                    .ToString()
                : t.RewrittenTypeName, // get il2cpptypereflectiondata
            t.DeclaringTypeInfoProvider is null ? t.TypeNamespace
            : t.DeclaringTypeInfoProvider.TypeNamespace == "" ? t.DeclaringTypeInfoProvider.OriginalTypeName
            : t.DeclaringTypeInfoProvider.TypeNamespace + "." + t.DeclaringTypeInfoProvider.OriginalTypeName,
            t.DeclaringTypeInfoProvider is not null,
            t.IsGenericInstance // ??
        );
        return thing;
    }*/

    public static string FullNameFromRef(this Il2CppTypeReflectionData t, bool ns = true)
    {
        var toret = "";
        if (t.isPointer) toret += "Pointer<";
        if (t.isArray)
            return toret + t.arrayType.FullNameFromRef() + "[]".Repeat(t.arrayRank) + (t.isPointer ? ">" : "");

        if (!t.isType)
            return toret + t.variableGenericParamName.Clean() + (t.isPointer ? ":" : "");

        if (!t.isGenericType)
            return toret + t.baseType!.FullName!.Clean() + (t.isPointer ? ">" : ""); // property accessors

        var builder = new StringBuilder(toret + (ns ? t.baseType!.FullName.Clean() : t.baseType!.Name.Clean()) + "<");
        foreach (var genericParam in t.genericParams)
        {
            builder.Append(genericParam.FullNameFromRef()).Append(", ");
        }

        builder.Remove(builder.Length - 2, 2);
        builder.Append(">");
        return builder + (t.isPointer ? ">" : "");
    }

    public static string Clean(this string str)
    {
        if (str.Length > 0 && char.IsDigit(str[0]))
        {
            str = "_" + str;
        }

        return Regex.Replace(Regex.Replace(str, @"[^$_0-9a-zA-Z\.\/]", "_"), @"\/([^\/<>\s]+)", "['$1']");
    }
}


#region Serializable data types

// var treeRoot = TypeTreeBuilder.BuildTree(classDict);
[JsonSourceGenerationOptions(WriteIndented = true)]
[JsonSerializable(typeof(IOrderedEnumerable<Il2CppClass>))]
internal partial class SourceGenerationContext : JsonSerializerContext;

public record Il2CppType(
    string Name,
    string FullName,
    string Namespace
    // bool IsNested,
    // bool IsGeneric
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
            new Il2CppType(
                t.Name.Clean(),
                LibCpp2ILUtils.GetTypeReflectionData(LibCpp2IlMain.Binary!.GetType(t.Definition.TypeIndex)).FullNameFromRef(),
                t.Namespace
                ),
            //null
            t.BaseType is not null ? new Il2CppType(
                t.BaseType.Name.Clean(),
                LibCpp2ILUtils.GetTypeReflectionData(LibCpp2IlMain.Binary!.GetType(t.Definition.TypeIndex)).FullNameFromRef(),
                t.BaseType.Namespace.Clean()
                ) : null,
            t.IsValueType || t.IsEnumType,
            t.GetInheritanceDepth(),
            t.Fields.Select(f => new Il2CppField(
                f.Name.Clean(),
                f.Offset,
                new Il2CppType(
                    LibCpp2ILUtils.GetTypeReflectionData(f.FieldType).FullNameFromRef(false),
                    LibCpp2ILUtils.GetTypeReflectionData(f.FieldType).FullNameFromRef(),
                    f.FieldTypeInfoProvider.TypeNamespace
                    // f.FieldTypeInfoProvider.IsGenericInstance
                    // f.FieldTypeInfoProvider.DeclaringTypeInfoProvider == null,
                    // f.FieldTypeInfoProvider.gen
                )
            )).ToArray(),
            t.Methods.Select(m => new Il2CppMethod(
                m.MethodName.Clean(),
                m.Parameters.Select(p => new Il2CppParameter(p.Name.Clean(), new Il2CppType(
                    LibCpp2ILUtils.GetTypeReflectionData(p.ParameterType).FullNameFromRef(false),
                    LibCpp2ILUtils.GetTypeReflectionData(p.ParameterType).FullNameFromRef(true),
                    p.ParameterTypeInfoProvider.TypeNamespace
                ))).ToArray(),
                m.ReturnType.OriginalTypeName == "Void" ? null : /*m.ReturnType.ToIl2CppType()*/null,
                m.IsStatic,
                m.Definition.GetWasmIndex(),
                m.UnderlyingPointer
            )).ToArray(),
            t.NestedTypes.Select(n => (Il2CppClass)n).ToArray()
        );
    }
}

#endregion