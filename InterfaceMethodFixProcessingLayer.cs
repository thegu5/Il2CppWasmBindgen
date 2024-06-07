using System.Diagnostics;
using Cpp2IL.Core.Api;
using Cpp2IL.Core.Model.Contexts;

namespace Il2CppTsBindgen;

// TODO: determine root cause (after fixing assembly resolve events)
public class InterfaceMethodFixProcessingLayer : Cpp2IlProcessingLayer
{
    public override void Process(ApplicationAnalysisContext appContext, Action<int, int>? progressCallback = null)
    {
        foreach (var type in appContext.AllTypes)
        {
            foreach (var method in type.Methods.Where(method => method.Name.Contains('.') && !method.Name.StartsWith('.') && !method.Name.StartsWith('<')))
            {
                Console.WriteLine("Found violating name: " + method.Name);
                // Debugger.Launch();
                Debugger.Break();
                method.OverrideName = method.Name.Split('.').Last();
            }
        }
    }

    public override string Name => "InterfaceMethodFixProcessingLayer";
    public override string Id => "InterfaceMethodFixProcessingLayer";
}