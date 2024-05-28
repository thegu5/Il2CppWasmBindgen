using System.Text.RegularExpressions;
using Cpp2IL.Core.Api;
using Cpp2IL.Core.Model.Contexts;

namespace Il2CppTsBindgen;

// ReSharper disable once InconsistentNaming
public partial class JSNamingProcessingLayer : Cpp2IlProcessingLayer
{
    public override string Name => "Javascript Naming";
    public override string Id => "jsnaming";

    public override void Process(ApplicationAnalysisContext appContext, Action<int, int>? progressCallback = null)
    {
        var typesToProcess = appContext.AllTypes.Where(t => t is not InjectedTypeAnalysisContext).ToArray();


        foreach (var typeAnalysisContext in typesToProcess)
        {
            typeAnalysisContext.OverrideName = Clean(typeAnalysisContext.OriginalTypeName);
            
            foreach (var methodAnalysisContext in typeAnalysisContext.Methods)
            {
                if (methodAnalysisContext is InjectedMethodAnalysisContext)
                    continue;
                
                foreach (var param in methodAnalysisContext.Parameters)
                {
                    param.OverrideName = Clean(param.Name);
                }

                methodAnalysisContext.OverrideName = Clean(methodAnalysisContext.MethodName);
            }
            foreach (var fieldAnalysisContext in typeAnalysisContext.Fields)
            {
                if(fieldAnalysisContext is InjectedFieldAnalysisContext)
                    continue;
                fieldAnalysisContext.OverrideName = Clean(fieldAnalysisContext.FieldName);
            }
            foreach (var propAnalysisContext in typeAnalysisContext.Properties)
            {
                propAnalysisContext.OverrideName = Clean(propAnalysisContext.PropertyName);
            }
            foreach (var eventAnalysisContext in typeAnalysisContext.Events)
            {
                eventAnalysisContext.OverrideName = Clean(eventAnalysisContext.EventName);
            }
        }

    }

    private static string Clean(string name)
    {
        if (name is ".ctor" or ".cctor") return name;
        if (name.Length > 0 && char.IsDigit(name[0]))
        {
            name = "_" + name;
        }
        return CleanRegex().Replace(name, "_");
    }
    // allowing backticks so ilspy avalonia doesn't throw a fit
    [GeneratedRegex(@"[^$_0-9a-zA-Z`]")]
    private static partial Regex CleanRegex();
}