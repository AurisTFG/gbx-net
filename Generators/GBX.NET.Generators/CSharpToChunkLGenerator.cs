using GBX.NET.Generators.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

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
        var chunkLDir = Path.Combine(projDir, "ChunkL");

        foreach (var engineTypeSymbolPair in engineTypes)
        {
            var engineSymbol = engineTypeSymbolPair.Value;

            var relatedDir = Path.Combine(chunkLDir, engineSymbol.ContainingNamespace.Name);
            
            Directory.CreateDirectory(relatedDir);

            var chunkLPath = Path.Combine(relatedDir, $"{engineSymbol.Name}.chunkl");

            var chunks = new List<INamedTypeSymbol>();

            foreach (var typeMember in engineSymbol.GetTypeMembers().Where(IsAnyChunk))
            {
                chunks.Add(typeMember);
            }

            if (chunks.Count == 0)
            {
                continue;
            }

            if (engineSymbol.GetAttributes().FirstOrDefault(x => x.AttributeClass?.Name == "NodeAttribute")?.ConstructorArguments[0].Value is not uint classId)
            {
                throw new Exception("Invalid class ID");
            }

            var metadata = new Dictionary<string, string>();

            if (engineSymbol.BaseType is not null && engineSymbol.BaseType.Name != "CMwNod")
            {
                metadata.Add("inherits", engineSymbol.BaseType.Name);
            }

            try
            {
                var chunkL = new ChunkL
                {
                    ClassId = classId,
                    ClassName = engineSymbol.Name,
                    Metadata = metadata,
                    Chunks = chunks.Select(ChunkSymbolToChunkLChunk).ToList()
                };

                using var sw = new StringWriter();

                chunkL.Save(sw);
                File.WriteAllText(chunkLPath, sw.ToString());
            }
            catch (Exception ex)
            {

            }
        }
    }

    private ChunkLChunk ChunkSymbolToChunkLChunk(INamedTypeSymbol chunkSymbol)
    {
        var chunkId = default(uint?);
        var comment = "";
        var ignored = false;

        foreach (var att in chunkSymbol.GetAttributes())
        {
            switch (att.AttributeClass?.Name)
            {
                case "ChunkAttribute":
                    chunkId = (uint)att.ConstructorArguments[0].Value!;
                    comment = att.ConstructorArguments.ElementAtOrDefault(1).Value?.ToString() ?? "";
                    break;
                case "IgnoreChunkAttribute":
                    ignored = true;
                    break;
            }
        }

        if (chunkId is null)
        {
            throw new Exception("Invalid chunk ID");
        }

        var readWriteMethod = chunkSymbol.GetMembers().OfType<IMethodSymbol>().FirstOrDefault(x => x.Name == "ReadWrite");

        var readWriteMethodSyntax = readWriteMethod?.DeclaringSyntaxReferences[0].GetSyntax() as MethodDeclarationSyntax;

        if (readWriteMethodSyntax?.Body is null)
        {
            throw new Exception("ReadWrite method not found");
        }

        var members = new List<IChunkLMember>();

        foreach (var statement in readWriteMethodSyntax.Body.Statements)
        {
            if (statement is ExpressionStatementSyntax expressionStatement && expressionStatement.Expression is InvocationExpressionSyntax invocation)
            {
                if (invocation.Expression is MemberAccessExpressionSyntax memberAccess)
                {
                    var name = ((invocation.ArgumentList.Arguments.FirstOrDefault()?.Expression as MemberAccessExpressionSyntax)?.Name as IdentifierNameSyntax)?.Identifier.Text;

                    // this also doesnt work with ! expression

                    /*if (name is not null && char.ToLowerInvariant(name[0]) == 'u' && int.TryParse(name.Substring(1), out _))
                    {
                        name = "";
                    }*/

                    switch ((memberAccess.Name as IdentifierNameSyntax)?.Identifier.Text)
                    {
                        case "Int32":
                            if (name == "version")
                            {
                                members.Add(new ChunkLMember { Type = "version", Name = "" });
                            }
                            else
                            {
                                members.Add(new ChunkLMember { Type = "int", Name = name ?? "" });
                            }
                            break;
                        case "Single":
                            members.Add(new ChunkLMember { Type = "float", Name = name ?? "" });
                            break;
                        case "String":
                            members.Add(new ChunkLMember { Type = "string", Name = name ?? "" });
                            break;
                        case "Id":
                            members.Add(new ChunkLMember { Type = "id", Name = name ?? "" });
                            break;
                        case "Ident":
                            members.Add(new ChunkLMember { Type = "ident", Name = name ?? "" });
                            break;
                        case "FileRef":
                            members.Add(new ChunkLMember { Type = "fileref", Name = name ?? "" });
                            break;
                        case "Boolean":
                            members.Add(new ChunkLMember { Type = "bool", Name = name ?? "" });
                            break;
                        default:
                            members.Add(new ChunkLMember { Type = "X", Name = name ?? "" });
                            break;
                    }
                }
            }
        }

        return new ChunkLChunk
        {
            ChunkId = chunkId.Value,
            Ignored = ignored,
            Skippable = IsSkippableChunk(chunkSymbol),
            Comment = comment,
            Members = members
        };
    }

    private bool IsAnyChunk(INamedTypeSymbol chunkSymbol)
    {
        while (chunkSymbol.BaseType is not null)
        {
            if (chunkSymbol.OriginalDefinition.Name is "Chunk" or "SkippableChunk" or "HeaderChunk")
            {
                return true;
            }

            chunkSymbol = chunkSymbol.BaseType;
        }

        return false;
    }

    private bool IsSkippableChunk(INamedTypeSymbol chunkSymbol)
    {
        while (chunkSymbol.BaseType is not null)
        {
            if (chunkSymbol.OriginalDefinition.Name == "SkippableChunk")
            {
                return true;
            }

            chunkSymbol = chunkSymbol.BaseType;
        }

        return false;
    }
}
