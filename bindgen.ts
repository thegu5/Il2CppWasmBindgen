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
    TypeGenericParams: string[]
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

const ast = recast.parse(`
export class Il2CppObject {
  ptr: number;

  constructor(ptr: number) {
    this.ptr = ptr;
  }
}

export class Pointer<T extends Il2CppObject> {

  ptr: number;

  get value(): T | null {
    // would deref by reading memory at ptr, putting that into a il2cppobject and casting to T
    return null;
  }

  set value(value: T) {
    this.ptr = value.ptr;
  }
}
`, {
    parser: require("recast/parsers/typescript")
});

function normalize(name: string, prepend = false) {
    name = name.replace(/[=/`<>|-]/g, "_");
    if (name.charAt(0) >= '0' && name.charAt(0) <= '9') {
        name = "_" + name;
    }
    // bad idea o_o
    let ptrcount = name.replace(/[^*]/g, "").length;
    name = name.replaceAll("*", "");
    for (let i = 0; i < ptrcount; i++) {
        name = "Pointer<" + name + ">";
    }
    return name;
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
    let superClass = data.BaseType ? b.identifier(normalize(data.BaseType, true)) : null;
    if (data.Name == "Object" && data.Namespace == "System") superClass = b.identifier("Il2CppObject");
    let dec = b.classDeclaration(
        b.identifier(normalize(data.Name)),
        b.classBody([]),
        superClass
    );
    if (data.GenericParams.length > 0) {
        dec.typeParameters = b.tsTypeParameterDeclaration(
            data.GenericParams.map((p) => b.tsTypeParameter(normalize(p)))
        );
    }
    data.NestedClasses.forEach((nc) => {
        let prop = b.classProperty(
            b.identifier(normalize(nc.Name)),
            classDeclarationToExpression(classToSyntax(nc))
        );
        (prop.value as recast.types.namedTypes.ClassExpression).superClass = b.identifier("any");
        /*let m = b.classMethod("get", b.identifier(normalize(nc.Name)), [], b.blockStatement(
            [b.returnStatement(classDeclarationToExpression(classToSyntax(nc)))]
        ))*/
        dec.body.body.push(prop);

    });
    data.Fields.forEach((f) => {
        let typeannotation = b.tsTypeAnnotation(b.tsUnionType([
            b.tsTypeReference(
                b.identifier(normalize(f.Type, true)),
                f.TypeGenericParams.length > 0 ? b.tsTypeParameterInstantiation(f.TypeGenericParams.map(genparam => {
                    return b.tsTypeReference(b.identifier(normalize(genparam)))
                })) : null
            ),
            b.tsNullKeyword()
        ]));
        /* if (f.TypeGenericParams.length > 0) {
             typeannotation.
         }*/
        let getter = b.classMethod("get", b.identifier(normalize(f.Name)), [], b.blockStatement(
            [
                b.returnStatement(b.nullLiteral())
            ]
        ));
        getter.returnType = typeannotation;
        let sparamid = b.identifier("value");
        sparamid.typeAnnotation = typeannotation
        let setter = b.classMethod("set", b.identifier(normalize(f.Name)), [sparamid], b.blockStatement([]))
        dec.body.body.push(getter);
        dec.body.body.push(setter);
    })
    return dec;
}

typedata.forEach((arrelem) => {
    let t = arrelem.Value;
    // if (t.Name[0] == '<' || t.Name[0] == '>') return; // weird anonymous type, nested class stuff (TODO!)
    ast.program.body.push(
        b.exportNamedDeclaration(
            b.tsModuleDeclaration(
                b.identifier(t.Namespace == "" ? "Il2Cpp" : normalize(t.Namespace, true)),
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