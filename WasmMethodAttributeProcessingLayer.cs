using Cpp2IL.Core.Api;
using Cpp2IL.Core.Model.Contexts;
using Cpp2IL.Core.Utils;
using LibCpp2IL;
using LibCpp2IL.Wasm;

namespace Il2CppTsBindgen;

public class WasmMethodAttributeProcessingLayer : Cpp2IlProcessingLayer
{
    public override void Process(ApplicationAnalysisContext appContext, Action<int, int>? progressCallback = null)
    {
        var methodIndexAttributes = AttributeInjectionUtils.InjectOneParameterAttribute(appContext, "Cpp2IlInjected.WasmMethod",
            "WasmMethod", AttributeTargets.Method, false, appContext.SystemTypes.SystemInt32Type, "Index");
        
        foreach (var assembly in appContext.Assemblies)
        {
            var methodIndexAttributeInfo = methodIndexAttributes[assembly];
            
            foreach (var method in assembly.Types.SelectMany(t => t.Methods))
            {
                method.AnalyzeCustomAttributeData();
                if (method.Definition is null || method.CustomAttributes == null || method.UnderlyingPointer == 0) continue;
                
                WasmFunctionDefinition wasmdef;
                try
                {
                    wasmdef = WasmUtils.GetWasmDefinition(method.Definition);
                }
                catch
                {
                    continue;
                }
                
                AttributeInjectionUtils.AddOneParameterAttribute(method, methodIndexAttributeInfo, wasmdef.IsImport
                    ? ((WasmFile)LibCpp2IlMain.Binary!).FunctionTable.IndexOf(wasmdef)
                    : wasmdef.FunctionTableIndex);
            }
        }
    }

    public override string Name => "Wasm Method Attribute Injector";
    public override string Id => "WasmMethodAttributeProcessingLayer";
}