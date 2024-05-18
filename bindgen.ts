import * as process from "node:process";
import * as fs from "node:fs";
import * as recast from "recast";

const typedata = JSON.parse(fs.readFileSync(process.argv[process.argv.length - 1]).toString()) as {
    Key: string,
    Value: { Name: string, BaseType: string | null, Namespace: string, IsStruct: boolean, InheritanceDepth: number, Fields: any[], Methods: any[] }
}[];

const ast = recast.parse("", {
    parser: require("recast/parsers/typescript")
});
const b = recast.types.builders;
typedata.forEach((t) => {
    ast.program.body.push(b.tsModuleDeclaration(b.identifier(t.Value.Namespace == "" ? "Il2Cpp" : "Il2Cpp." + t.Value.Namespace), b.tsModuleBlock([])));
})
console.log(recast.print(ast).code);