import * as process from "node:process";
import * as fs from "node:fs";
import * as recast from "recast";

const typedata = JSON.parse(fs.readFileSync(process.argv[process.argv.length - 1]).toString()) as {
    Key: string,
    Value: {
        Name: string,
        BaseType: string | null,
        Namespace: string,
        IsStruct: boolean,
        InheritanceDepth: number,
        Fields: any[],
        Methods: any[]
    }
}[];

const ast = recast.parse("", {
    parser: require("recast/parsers/typescript")
});
function formatPropName(name: string) {
    return name.replace(/[=/`]/g, "_");
}

const b = recast.types.builders;
typedata.forEach((t) => {
    if (t.Value.Name[0] == '<' || t.Value.Name[0] == '>') return; // weird anonymous type, nested class stuff (TODO!)
    ast.program.body.push(
        b.exportNamedDeclaration(
            b.tsModuleDeclaration(
                b.identifier(t.Value.Namespace == "" ? "Il2Cpp" : "Il2Cpp." + t.Value.Namespace), 
                b.tsModuleBlock([
                    b.exportNamedDeclaration(
                        b.classDeclaration(
                            b.identifier(formatPropName(t.Value.Name)),
                            b.classBody(
                                []
                            ),
                            t.Value.BaseType ? b.identifier("Il2Cpp." + formatPropName(t.Value.BaseType)) : null
                        )
                    )
                ]))));
})
console.log(recast.print(ast).code);