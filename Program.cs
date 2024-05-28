using System.Collections;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.Loader;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using AssetRipper.Primitives;
using Cpp2IL.Core;
using Cpp2IL.Core.Api;
using Cpp2IL.Core.InstructionSets;
using Cpp2IL.Core.Model.Contexts;
using Cpp2IL.Core.OutputFormats;
using Cpp2IL.Core.Utils;
using HarmonyLib;
using Il2CppTsBindgen;
using LibCpp2IL;
using LibCpp2IL.Metadata;
using LibCpp2IL.Reflection;
using LibCpp2IL.Wasm;
using Reinforced.Typings;
using Reinforced.Typings.Attributes;
using TypeExtensions = Reinforced.Typings.TypeExtensions;

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

/*
#region Cpp2IL Assembly Generation

Console.WriteLine("Setting up Cpp2IL...");
InstructionSetRegistry.RegisterInstructionSet<WasmInstructionSet>(DefaultInstructionSets.WASM);
Console.SetIn(new StringReader("4fb240"));
Cpp2IlApi.InitializeLibCpp2Il(args[0],
    args[1],
    new UnityVersion(2023, 2, 5), true);

// ProcessingLayerRegistry.Register<JSNamingProcessingLayer>();

// new JSNamingProcessingLayer().Process(Cpp2IlApi.CurrentAppContext);
// new StableRenamingProcessingLayer().Process(Cpp2IlApi.CurrentAppContext);

new AsmResolverDllOutputFormatDefault().DoOutput(Cpp2IlApi.CurrentAppContext,
    Path.Combine(Directory.GetCurrentDirectory(), "cpp2il_out"));

#endregion
*/

// new Harmony("Il2CppTsBindgen").PatchAll();

var mlc = new MetadataLoadContext(new PathAssemblyResolver(Directory.GetFiles(Path.Combine(Directory.GetCurrentDirectory(), "cpp2il_out"), "*.dll")));

var mainAssembly = mlc.LoadFromAssemblyName("Assembly-CSharp");

// ExportContext ctx =new ExportContext(Directory.EnumerateFiles(Path.Combine(Directory.GetCurrentDirectory(), "cpp2il_out"), "*.dll").Select(Assembly.LoadFrom).ToArray());

var ctx = new ExportContext([]);
Console.WriteLine(ctx.SourceAssemblies.Length);
ctx.Hierarchical = true;
ctx.TargetDirectory = Path.Combine(Directory.GetCurrentDirectory(), "bindings");
ctx.Global.UseModules = true;

Directory.CreateDirectory(Path.Combine(Directory.GetCurrentDirectory(), "bindings"));
var exporter = new TsExporter(ctx);
exporter.Initialize();
// why
Debugger.Break();
exporter.Context.GetType().GetField("TypesToFilesMap").SetValue(exporter.Context,
        mlc.GetAssemblies().SelectMany<Assembly, Type>(c =>
                c.GetTypes())
            .Distinct().ToList()
    // .Where(d => exporter.Context.Project.Blueprint(d).ThirdParty == null)
    .GroupBy<Type, string>(c => (string)exporter.Context.GetType().GetRuntimeMethods().ElementAt(23).Invoke(exporter.Context, [c, false]))
    .ToDictionary<IGrouping<string, Type>, string, IEnumerable<Type>>(
        c => c.Key,
        c => c.AsEnumerable()));

exporter.Export();
Process.GetCurrentProcess().Kill();
// end

Console.WriteLine("Parsing all classes...");        
List<Il2CppClass> classDict = [];

classDict.AddRange(Cpp2IlApi.CurrentAppContext.AllTypes
    .Where(t => t.DeclaringType is null && t.GenericParameterCount == 0).Select(t => (Il2CppClass)t));

classDict.AddRange(Cpp2IlApi.CurrentAppContext.ConcreteGenericMethodsByRef.Values.Select(t => t.DeclaringType)
    .Where(t => t.DeclaringType is null).DistinctBy(ctx => ctx.FullName)
    .Select(t => (Il2CppClass)t));

#region Serialize

Console.WriteLine("Serializing to " + args[2] + "...");
var sorted = from entry in classDict orderby entry.InheritanceDepth select entry;

var opts = new JsonSerializerOptions
{
    ReferenceHandler = ReferenceHandler.IgnoreCycles,
    Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    TypeInfoResolver = SourceGenerationContext.Default
};
File.WriteAllText(args[2],
    JsonSerializer.Serialize<IOrderedEnumerable<Il2CppClass>>(sorted, opts));

#endregion

Console.WriteLine("Done!");
// tomfoolery

/*[HarmonyPatch(typeof(Attribute))]
[HarmonyPatch(nameof(Attribute.GetCustomAttribute))]
[HarmonyPatch([typeof(MemberInfo), typeof(Type), typeof(bool)])]
class Patch
{
    static bool Prefix(ref Attribute? __result)
    {
        Console.WriteLine("PATCH CALLED");
        __result = null;
        return false;
    }
}

[HarmonyPatch(typeof(Attribute))]
[HarmonyPatch(nameof(Attribute.GetCustomAttributes))]
[HarmonyPatch([typeof(MemberInfo), typeof(Type), typeof(bool)])]
class Patch2
{
    static bool Prefix(ref Attribute[] __result)
    {
        Console.WriteLine("PATCH2 CALLED");
        __result = [];
        return true;
    }
}*/

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

    public static int? GetWasmIndex(this Il2CppMethodDefinition method)
    {
        var wasmdef = WasmUtils.TryGetWasmDefinition(method);
        if (wasmdef is null) return null;
        return wasmdef.IsImport
            ? ((WasmFile)LibCpp2IlMain.Binary!).FunctionTable.IndexOf(wasmdef)
            : wasmdef.FunctionTableIndex;
    }

    public static string FullNameFromRef(this Il2CppTypeReflectionData t, bool ns = true)
    {
        var toret = "";
        if (t.isPointer) toret += "Pointer<";
        if (t.isArray)
            return toret + t.arrayType.FullNameFromRef() + "[]".Repeat(t.arrayRank) + (t.isPointer ? ">" : "");

        if (!t.isType)
            return toret + t.variableGenericParamName.Clean() + (t.isPointer ? ":" : "");

        if (!t.isGenericType || t.genericParams.Length == 0)
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

    public static string ModifiedSourceString(this TypeAnalysisContext t, bool withNamespace = true)
    {
        switch (t)
        {
            case GenericInstanceTypeAnalysisContext gent:
            {
                if (gent.GenericArguments.Count == 0) break;
                var sb = new StringBuilder();
                sb.Append(gent.GenericType.ModifiedSourceString());
                sb.Append('<');
                var first = true;
                foreach (var genericArgument in gent.GenericArguments)
                {
                    if (!first)
                        sb.Append(", ");
                    else
                        first = false;

                    sb.Append(genericArgument.ModifiedSourceString());
                }

                sb.Append('>');
                return sb.ToString();
            }
            case PointerTypeAnalysisContext ptrt:
                return "Pointer<" + ptrt.ElementType.ModifiedSourceString() + ">";
            case SzArrayTypeAnalysisContext szat:
                return szat.ElementType.ModifiedSourceString() + "[]";
            case ArrayTypeAnalysisContext at:
                return at.ElementType.ModifiedSourceString() + "[]".Repeat(at.Rank);
            case GenericParameterTypeAnalysisContext genp:
                return genp.DefaultName;
        }

        if (t.Definition != null)
            return withNamespace ? t.Definition.FullName!.Clean() : t.Definition.Name!.Clean();

        var ret = new StringBuilder();
        if (t.OverrideNs != null && withNamespace)
            ret.Append(t.OverrideNs).Append('.');

        ret.Append(t.Name);

        return ret.ToString().Clean();
    }
}

#region Serializable data types

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
                t.ModifiedSourceString(false),
                t.ModifiedSourceString(),
                t.Namespace
            ),
            //null
            t.BaseType is not null
                ? new Il2CppType(
                    t.BaseType.ModifiedSourceString(false),
                    t.BaseType.ModifiedSourceString(),
                    t.BaseType.Namespace.Clean()
                )
                : null,
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
                    LibCpp2ILUtils.GetTypeReflectionData(p.ParameterType).FullNameFromRef(),
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

