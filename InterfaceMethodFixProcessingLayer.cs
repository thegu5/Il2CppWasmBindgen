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
            if (type.Name.Contains("FarWaypointDelay")) Debugger.Break();
            foreach (var prop in type.Properties.Where(method => method.Name.Contains('.') && !method.Name.StartsWith('.') && !method.Name.StartsWith('<')))
            {
                Console.WriteLine("Found violating property: " + prop.Name);
                prop.OverrideName = prop.Name.Split('.').Last();
            }
        }
    }

    public override string Name => "InterfaceMethodFixProcessingLayer";
    public override string Id => "InterfaceMethodFixProcessingLayer";
}