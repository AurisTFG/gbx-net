using GBX.NET.Generators.ChunkL.FromCSharp.StatementMemberMakers;
using GBX.NET.Generators.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace GBX.NET.Generators.ChunkL.FromCSharp;

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
                var members = GetFieldPropertyDictionary(engineSymbol);

                var chunkList = new List<ChunkLChunk>();

                foreach (var c in chunks)
                {
                    try
                    {
                        chunkList.Add(ChunkSymbolToChunkLChunk(c, members));
                    }
                    catch (Exception ex)
                    {
                        metadata.Add($"chunk_{c.Name}", ex.Message);
                        context.ReportDiagnostic(Diagnostic.Create(new DiagnosticDescriptor("GBX.NET.Generators", "ChunkL (specific chunk) generation failed", ex.ToString(), "GBX.NET.Generators", DiagnosticSeverity.Error, true), Location.None));
                    }
                }

                if (chunkList.Count == 0)
                {
                    continue;
                }

                var chunkL = new ChunkL
                {
                    ClassId = classId,
                    ClassName = engineSymbol.Name,
                    Metadata = metadata,
                    Chunks = chunkList
                };

                using var sw = new StringWriter();

                chunkL.Save(sw);
                File.WriteAllText(chunkLPath, sw.ToString());
            }
            catch (Exception ex)
            {
                context.ReportDiagnostic(Diagnostic.Create(new DiagnosticDescriptor("GBX.NET.Generators", "ChunkL generation failed", ex.ToString(), "GBX.NET.Generators", DiagnosticSeverity.Error, true), Location.None));
            }
        }
    }

    private Dictionary<string, IPropertySymbol> GetFieldPropertyDictionary(INamedTypeSymbol engineSymbol)
    {
        var dict = new Dictionary<string, IPropertySymbol>();

        foreach (var propertySymbol in engineSymbol.GetMembers().OfType<IPropertySymbol>())
        {
            var syntax = propertySymbol.GetMethod?.DeclaringSyntaxReferences[0].GetSyntax();

            if (syntax is not AccessorDeclarationSyntax accessorSyntax)
            {
                continue;
            }

            if (accessorSyntax.ExpressionBody is ArrowExpressionClauseSyntax arrowSyntax)
            {
                if (arrowSyntax.Expression is IdentifierNameSyntax nameSyntax)
                {
                    dict.Add(nameSyntax.Identifier.Text, propertySymbol);
                }
                else if (arrowSyntax.Expression is AssignmentExpressionSyntax assignmentExpressionSyntax && assignmentExpressionSyntax.Left is IdentifierNameSyntax identifierName)
                {
                    dict.Add(identifierName.Identifier.Text, propertySymbol);
                }
            }
            else if (accessorSyntax.Body is BlockSyntax blockSyntax)
            {
                if (blockSyntax.Statements.Last() is ReturnStatementSyntax returnStatement && returnStatement.Expression is IdentifierNameSyntax identSyntax)
                {
                    dict.Add(identSyntax.Identifier.Text, propertySymbol);
                }
            }
        }

        return dict;
    }

    private ChunkLChunk ChunkSymbolToChunkLChunk(INamedTypeSymbol chunkSymbol, Dictionary<string, IPropertySymbol> classMembers)
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

        var readWriteMethodSyntax = default(MethodDeclarationSyntax);
        var chunkFieldMembers = new Dictionary<string, IFieldSymbol>();

        foreach (var chunkMember in chunkSymbol.GetMembers())
        {
            switch (chunkMember)
            {
                case IMethodSymbol { Name: "ReadWrite" } readWriteMethod:
                    readWriteMethodSyntax = readWriteMethod.DeclaringSyntaxReferences[0].GetSyntax() as MethodDeclarationSyntax;
                    break;
                case IFieldSymbol field:
                    chunkFieldMembers.Add(field.Name, field);
                    break;
            }
        }

        var properties = new CSharpToChunkLProperties { ClassMembers = classMembers, ChunkFieldMembers = chunkFieldMembers };

        var members = new List<IChunkLMember>();

        foreach (var statement in readWriteMethodSyntax?.Body?.Statements ?? Enumerable.Empty<StatementSyntax>())
        {
            members.Add(StatementMemberMaker<StatementSyntax>.Make(statement, properties));
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
