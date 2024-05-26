import * as process from "node:process";
import * as fs from "node:fs";
import * as recast from "recast";

interface Il2CppType {
    Name: string,
    FullName: string,
    Namespace: string,
    IsNested: boolean
}

interface Il2CppClass {
    Type: Il2CppType,
    BaseType: Il2CppType | null,
    IsStruct: boolean,
    InheritanceDepth: number,
    Fields: Il2CppField[],
    Methods: Il2CppMethod[],
    NestedClasses: Il2CppClass[]
}

interface Il2CppField {
    Name: string,
    Offset: number,
    Type: Il2CppType
}

interface Il2CppMethod {
    Name: string,
    Parameters: { Name: string, Type: Il2CppType }[],
    ReturnType: Il2CppType | null,
    IsStatic: boolean,
    Index: number | null,
    MethodInfoPtr: number
}


const typedata = JSON.parse(fs.readFileSync(process.argv[process.argv.length - 1]).toString()) as Il2CppClass[];

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

const b = recast.types.builders;

function getModifiedNamespace(t: Il2CppType): string {
    return t.Namespace.length > 0 ? "Il2Cpp." + t.Namespace : "Il2Cpp";
}

/*function toTypeRef(t: Il2CppType): recast.types.namedTypes.TSTypeReference {
    let toret = b.tsTypeReference(
            b.identifier(!t.IsNested ? getFullName(t) : getFullName(t).replace(/\.([^.]+$)/, '["$1"]')),
        b.tsTypeParameterInstantiation(
            t.GenericArgs.map(genarg => !genarg.IsNested ? toTypeRef(genarg) : b.tsAnyKeyword()) // stub nested classes
        )
    );
    if (t.GenericArgs.length == 0) toret.typeParameters = undefined; // remove empty <>
    return toret;
}*/



function classDeclarationToExpression(dec: recast.types.namedTypes.ClassDeclaration): recast.types.namedTypes.ClassExpression {
    return b.classExpression(
        dec.id,
        dec.body,
        dec.superClass
    );
}


function classToSyntax(data: Il2CppClass): recast.types.namedTypes.ClassDeclaration {
    let superClass = data.BaseType ? b.identifier(data.BaseType.FullName) : null;
    if (data.Type.Name == "Object" && data.Type.Namespace == "System") superClass = b.identifier("Il2CppObject");
    let dec = b.classDeclaration(
        b.identifier(data.Type.Name),
        b.classBody([]),
        superClass,
    );
    data.NestedClasses.forEach((nc) => {
        // typing this as any since i cba to fix this ! !
        /*let prop = b.classProperty(
            b.identifier(nc.Type.Name),
            null
        );
        prop.typeAnnotation = b.tsTypeAnnotation(b.tsAnyKeyword());*/
        let prop = b.classProperty(
            b.identifier(nc.Type.Name.split("<")[0]),
            classDeclarationToExpression(classToSyntax(nc))
        );
        // TODO: do something sane like split up each class into a es module instead of this annoying hack
        (prop.value as recast.types.namedTypes.ClassExpression).superClass = b.identifier("any");
        dec.body.body.push(prop);

    });
    data.Fields.forEach((f) => {
        let typeannotation = b.tsTypeAnnotation(b.tsUnionType([
            b.tsTypeReference(b.identifier(f.Type.FullName)),
            b.tsNullKeyword()
        ]));
        // if (f.Type.IsNested) typeannotation = b.tsTypeAnnotation(b.tsAnyKeyword()); // hack!!!
        
        let getter = b.classMethod("get", b.identifier(f.Name), [], b.blockStatement(
            [
                b.returnStatement(b.nullLiteral())
            ]
        ));
        getter.returnType = typeannotation;
        
        let sparamid = b.identifier("value");
        sparamid.typeAnnotation = typeannotation;
        let setter = b.classMethod("set", b.identifier(f.Name), [sparamid], b.blockStatement([]));
        
        dec.body.body.push(getter);
        dec.body.body.push(setter);
    })
    
    return dec;
}

typedata.forEach((t) => {
    // if (t.Name[0] == '<' || t.Name[0] == '>') return; // weird anonymous type, nested class stuff (TODO!)
    ast.program.body.push(
        b.exportNamedDeclaration(
            b.tsModuleDeclaration(
                b.identifier(getModifiedNamespace(t.Type)),
                b.tsModuleBlock([
                    b.exportNamedDeclaration(
                        classToSyntax(t)
                    )
                ])
            )
        )
    );
});
fs.writeFileSync("testing.ts", recast.print(ast).code);