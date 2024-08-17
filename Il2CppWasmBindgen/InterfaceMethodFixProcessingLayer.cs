using Cpp2IL.Core.Api;
using Cpp2IL.Core.Model.Contexts;

namespace Il2CppWasmBindgen;

// TODO: determine root cause
public class InterfaceMethodFixProcessingLayer : Cpp2IlProcessingLayer
{
    public override void Process(ApplicationAnalysisContext appContext, Action<int, int>? progressCallback = null)
    {
        foreach (var type in appContext.AllTypes)
        {
            foreach (var property in type.Properties.Where(method => method.Name.Contains('.') && !method.Name.StartsWith('.') && !method.Name.StartsWith('<')))
            {
                property.OverrideName = property.Name.Split('.').Last();
                // Console.WriteLine($"Changing {type.Name}'s {property.DefaultName} to {property.OverrideName}");
            }
            foreach (var method in type.Methods.Where(method => method.Name.Contains('.') && !method.Name.StartsWith('.') && !method.Name.StartsWith('<')))
            {
                method.OverrideName = method.Name.Split('.').Last();
                // Console.WriteLine($"Changing {type.Name}'s {method.DefaultName} to {method.OverrideName}");
            }
        }
    }

    public override string Name => "InterfaceMethodFixProcessingLayer";
    public override string Id => "InterfaceMethodFixProcessingLayer";
}