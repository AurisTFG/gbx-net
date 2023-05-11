using GBX.NET.Generators.Extensions;
using Microsoft.CodeAnalysis;

namespace GBX.NET.Generators;

[Generator]
public class CSharpToChunkLGenerator : SourceGenerator
{
    public override bool Debug => false;

    public override void Execute(GeneratorExecutionContext context)
    {
        if (!context.AnalyzerConfigOptions.GlobalOptions.TryGetValue("build_property.projectdir", out var projDir))
        {
            throw new Exception("Project dir not found");
        }

        var enginesNamespace = context.Compilation
            .GlobalNamespace
            .NavigateToNamespace("GBX.NET.Engines") ?? throw new Exception("GBX.NET.Engines namespace not found.");

        var engineTypes = enginesNamespace.GetNamespaceMembers()
            .SelectMany(x => x.GetTypeMembers())
            .Where(x =>
            {
                var baseType = x.BaseType;

                while (baseType is not null && baseType.Name != "Node")
                {
                    baseType = baseType.BaseType;
                }

                return baseType is not null && baseType.Name == "Node";
            })
            .ToDictionary(x => $"{x.ContainingNamespace.Name}::{x.Name}");

        var enginesDir = Path.Combine(projDir, "ChunkL");

        foreach (var engineType in engineTypes)
        {
            var relatedDir = Path.Combine(enginesDir, engineType.Value.ContainingNamespace.Name);
            
            Directory.CreateDirectory(relatedDir);
        }
    }
}
