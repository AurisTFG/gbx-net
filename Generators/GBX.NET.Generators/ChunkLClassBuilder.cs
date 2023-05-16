using GBX.NET.Generators.Extensions;
using Microsoft.CodeAnalysis;
using System.Text;

namespace GBX.NET.Generators;

class ChunkLClassBuilder
{
    private readonly string ns;
    
    public ChunkL ChunkL { get; }
    public INamedTypeSymbol? ExistingTypeSymbol { get; }
    public Dictionary<string, IPropertySymbol?> ExistingPropertySymbols { get; } = new();

    public StringBuilder SbClass { get; } = new();
    public StringBuilder SbChunks { get; } = new();

    public ChunkLClassBuilder(string ns, ChunkL chunkL, INamedTypeSymbol? existingTypeSymbol)
    {
        this.ns = ns;
        
        ChunkL = chunkL;
        ExistingTypeSymbol = existingTypeSymbol;

        if (existingTypeSymbol is not null)
        {
            foreach (var property in existingTypeSymbol.GetMembers().OfType<IPropertySymbol>())
            {
                ExistingPropertySymbols.Add(property.Name, property);
            }
        }
    }

    public string Build()
    {
        SbClass.AppendLine($"namespace GBX.NET.Engines.{ns};");
        SbClass.AppendLine();

        SbClass.AppendLine($"/// <remarks>ID: 0x{ChunkL.ClassId:X8}</remarks>");

        if (ExistingTypeSymbol is null || !ExistingTypeSymbol.GetAttributes().Any(x => x.AttributeClass?.Name == "NodeAttribute"))
        {
            SbClass.AppendLine($"[Node(0x{ChunkL.ClassId:X8})]");
        }

        SbClass.Append($"public partial class {ChunkL.ClassName}");

        if (ExistingTypeSymbol is null || ExistingTypeSymbol.BaseType is null)
        {
            if (ChunkL.Metadata.TryGetValue("inherits", out var inheritedClass))
            {
                SbClass.Append($" : {inheritedClass}");
            }
            else
            {
                SbClass.Append($" : CMwNod");
            }
        }

        SbClass.AppendLine();
        SbClass.AppendLine("{");

        if (ExistingTypeSymbol is null || !ExistingTypeSymbol.Constructors.Any(x => x.Parameters.Length == 0))
        {
            SbClass.AppendLine(1, $"internal {ChunkL.ClassName}()");
            SbClass.AppendLine(1, "{");
            SbClass.AppendLine(1, "}");
        }

        SbChunks.AppendLine();
        SbChunks.AppendLine(1, "#region Chunks");

        foreach (var chunk in ChunkL.Chunks)
        {
            var chunkBuilder = new ChunkLChunkBuilder(this, chunk);
            chunkBuilder.Build();
        }

        SbChunks.AppendLine();
        SbChunks.AppendLine(1, "#endregion");

        SbClass.AppendLine(SbChunks.ToString());

        SbClass.AppendLine("}");

        return SbClass.ToString();
    }
}
