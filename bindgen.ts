import * as process from "node:process";
import * as fs from "node:fs";
import * as recast from "recast";

interface Il2CppClass {
    Name: string,
    GenericParams: string[],
    BaseType: string | null,
    Namespace: string,
    IsStruct: boolean,
    InheritanceDepth: number,
    Fields: Il2CppField[],
    Methods: Il2CppMethod[],
    NestedClasses: Il2CppClass[]
}

interface Il2CppField {
    Name: string,
    Offset: number,
    Type: string
}

interface Il2CppMethod {
    Name: string,
    Parameters: { Name: string, Type: string }[],
    ReturnType: string | null,
    IsStatic: boolean,
    Index: number | null,
    MethodInfoPtr: number
}


const typedata = JSON.parse(fs.readFileSync(process.argv[process.argv.length - 1]).toString()) as {
    Key: string,
    Value: Il2CppClass
}[];

const ast = recast.parse("", {
    parser: require("recast/parsers/typescript")
});

function formatPropName(name: string) {
    return name.replace(/[=/`]/g, "_");
}

const b = recast.types.builders;

function classDeclarationToExpression(dec: recast.types.namedTypes.ClassDeclaration): recast.types.namedTypes.ClassExpression {
    return b.classExpression(
        dec.id,
        dec.body,
        dec.superClass
    );
}


function classToSyntax(data: Il2CppClass): recast.types.namedTypes.ClassDeclaration {
    let dec = b.classDeclaration(
        b.identifier(formatPropName(data.Name)),
        b.classBody([]),
        data.BaseType ? b.identifier("Il2Cpp." + formatPropName(data.BaseType)) : null
    );
    if (data.GenericParams.length > 0) {
        dec.typeParameters = b.tsTypeParameterDeclaration(
            data.GenericParams.map((p) => b.tsTypeParameter(p))
        );
    }
    data.NestedClasses.forEach((nc) => {
        /*dec.body.body.push(b.classProperty(
            b.identifier(nc.Name),
            classDeclarationToExpression(classToSyntax(nc))
        ))*/
    });
    return dec;
}

typedata.forEach((arrelem) => {
    let t = arrelem.Value;
    if (t.Name[0] == '<' || t.Name[0] == '>') return; // weird anonymous type, nested class stuff (TODO!)
    ast.program.body.push(
        b.exportNamedDeclaration(
            b.tsModuleDeclaration(
                b.identifier(t.Namespace == "" ? "Il2Cpp" : "Il2Cpp." + t.Namespace),
                b.tsModuleBlock([
                    b.exportNamedDeclaration(
                        classToSyntax(t)
                    )
                ])
            )
        )
    );
});
console.log(recast.print(ast).code);