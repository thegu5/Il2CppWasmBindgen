using System.Diagnostics;
using System.Text;
using AsmResolver.DotNet;
using AsmResolver.DotNet.Signatures.Types;
using AssetRipper.Primitives;
using Cpp2IL.Core;
using Cpp2IL.Core.Api;
using Cpp2IL.Core.InstructionSets;
using Cpp2IL.Core.OutputFormats;
using LibCpp2IL;
using Microsoft.ClearScript;
using Microsoft.ClearScript.V8;
using React;
using Serenity.TypeScript;

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

/*
#region Cpp2IL Setup

Console.WriteLine("Setting up Cpp2IL...");
InstructionSetRegistry.RegisterInstructionSet<WasmInstructionSet>(DefaultInstructionSets.WASM);
Console.SetIn(new StringReader("4fb240"));
Cpp2IlApi.InitializeLibCpp2Il(args[0],
    args[1],
    new UnityVersion(2023, 2, 5), true);

#endregion


Console.WriteLine("Building assemblies...");
Directory.CreateDirectory(Path.Combine(Directory.GetCurrentDirectory(), "cpp2il_out"));
new AsmResolverDllOutputFormatDefault().BuildAssemblies(Cpp2IlApi.CurrentAppContext).ForEach(asm =>
    asm.Write(Path.Combine(Directory.GetCurrentDirectory(), "cpp2il_out", asm.Name)));
    */
Console.WriteLine("Reading assemblies back from disc...");
var types = Directory.GetFiles(Path.Combine(Directory.GetCurrentDirectory(), "cpp2il_out"))
        .Select(ModuleDefinition.FromFile).SelectMany(m => m.TopLevelTypes)
    ;

var babel = ReactEnvironment.Current.Babel;


Helpers.Engine.AddHostObject("types", types);
var ts = Helpers.ts;
var console = Helpers.console;
// Helpers.Engine.Execute("let statements = [];");
var resultFile = ts.createSourceFile("testing.ts", "", ts.ScriptTarget.ESNext, false);
var printer = ts.createPrinter();
var result = new StringBuilder();
foreach (var type in types)
{
    Debugger.Break();
    var module = ts.factory.createModuleDeclaration(
        null,
        ts.factory.createIdentifier(type.Namespace is not null ? $"Il2Cpp.{type.Namespace}" : "Il2Cpp"),
        ts.factory.createModuleBlock(new[]{type.AsTsClass()}),
        ts.NodeFlags.Namespace
    );
    result.AppendLine(printer.printNode(ts.EmitHint.Unspecified, module, resultFile));
}

File.WriteAllText("bindings.ts", result.ToString());


Debugger.Break();
/*
Console.WriteLine("Parsing all classes...");
var file = new SourceFile
{
    FileName = args[2],
    Statements = new NodeArray<IStatement>(types.Select(t =>
    {
        var node = t.AsTsClass();
        return new ModuleDeclaration(null, new Identifier(t.Namespace), new ModuleBlock([node]));
    })),
};
*/


internal static class Helpers
{
    public static V8ScriptEngine Engine = new V8ScriptEngine();

    public static dynamic ts => Engine.Script.ts;
    public static dynamic console => Engine.Script.console;

    static Helpers()
    {
        Engine.DocumentSettings.AccessFlags = DocumentAccessFlags.EnableAllLoading;
        Engine.AddHostType("Console", typeof(Console));
        Engine.Evaluate(
            File.ReadAllText("/home/gu5/RiderProjects/Il2CppTsBindgen/node_modules/typescript/lib/typescript.js"));
        Engine.Execute("var console; console.log = Console.WriteLine;");
    }

    public static object AsTsClass(this TypeDefinition type)
    {
        var heritage = new NodeArray<HeritageClause>();
        if (type.BaseType is not null)
            heritage.Add(new HeritageClause(SyntaxKind.ExtendsKeyword,
            [
                ((TypeReferenceNode)type.BaseType.ToTypeSignature().ToAstNode()).ToExpression()
            ]));
        return ts.factory.createClassDeclaration(
            null,
            ts.factory.createIdentifier(type.Name),
            type.GenericParameters.Count > 0
                ? type.GenericParameters.Select(p =>
                    ts.factory.createTypeParameterDeclaration(null, new Identifier(p.Name), null, null))
                : Engine.Evaluate("[]"),
            Engine.Evaluate("[]"), // heritage
            Engine.Evaluate("undefined") // ?
        );
    }

    public static ExpressionWithTypeArguments ToExpression(this TypeReferenceNode type)
    {
        return new ExpressionWithTypeArguments(
            (Identifier)type.TypeName,
            type.TypeArguments
        );
    }

    public static TypeNodeBase ToAstNode(this TypeSignature type)
    {
        return type switch
        {
            GenericInstanceTypeSignature gentype => new TypeReferenceNode(new Identifier(gentype.GenericType.Name),
                new NodeArray<ITypeNode>(gentype.TypeArguments.Select(ToAstNode))),
            PointerTypeSignature ptype => new TypeReferenceNode(new Identifier("Pointer"), [ToAstNode(ptype.BaseType)]),
            ArrayTypeSignature arrtype => new ArrayTypeNode(ToAstNode(arrtype.BaseType)),
            _ => new TypeReferenceNode(new Identifier(type.Name), [])
        };
    }
}