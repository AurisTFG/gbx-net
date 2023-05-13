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
                var members = GetFieldPropertyDictionary(engineSymbol);

                var chunkL = new ChunkL
                {
                    ClassId = classId,
                    ClassName = engineSymbol.Name,
                    Metadata = metadata,
                    Chunks = chunks.Select(x => ChunkSymbolToChunkLChunk(x, members)).ToList()
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
            if (propertySymbol.GetMethod?.DeclaringSyntaxReferences[0].GetSyntax() is not AccessorDeclarationSyntax accessorSyntax)
            {
                throw new Exception("Not AccessorDeclarationSyntax");
            }

            if (accessorSyntax.ExpressionBody is ArrowExpressionClauseSyntax arrowSyntax && arrowSyntax.Expression is IdentifierNameSyntax nameSyntax)
            {
                dict.Add(nameSyntax.Identifier.Text, propertySymbol);
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

        var readWriteMethod = chunkSymbol.GetMembers().OfType<IMethodSymbol>().FirstOrDefault(x => x.Name == "ReadWrite");

        var readWriteMethodSyntax = readWriteMethod?.DeclaringSyntaxReferences[0].GetSyntax() as MethodDeclarationSyntax;

        if (readWriteMethodSyntax?.Body is null)
        {
            throw new Exception("ReadWrite method not found");
        }

        var members = new List<IChunkLMember>();

        foreach (var statement in readWriteMethodSyntax.Body.Statements)
        {
            members.Add(CreateMemberFromStatement(statement, classMembers));
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

    private IChunkLMember CreateMemberFromStatement(StatementSyntax statement, Dictionary<string, IPropertySymbol> classMembers)
    {
        if (statement is ExpressionStatementSyntax expressionStatement && expressionStatement.Expression is InvocationExpressionSyntax invocationSyntax)
        {
            if (invocationSyntax.Expression is MemberAccessExpressionSyntax memberSyntax && memberSyntax.Expression is IdentifierNameSyntax expectedRwNameSyntax && expectedRwNameSyntax.Identifier.Text == "rw")
            {
                var methodName = memberSyntax.Name.Identifier.Text;

                if (methodName == "VersionInt32")
                {
                    return GetChunkLMemberFromMethodAndMemberName(methodName);
                }

                var args = invocationSyntax.ArgumentList.Arguments;

                if (args.Count == 1)
                {
                    // issues with ! expression?

                    var expression = args[0].Expression;

                    if (expression is PostfixUnaryExpressionSyntax postfixSyntax)
                    {
                        expression = postfixSyntax.Operand;
                    }

                    if (expression is LiteralExpressionSyntax)
                    {
                        return GetChunkLMemberFromMethodAndMemberName(methodName);
                    }

                    if (expression is IdentifierNameSyntax nameSyntax) // if it is member inside chunk (not working with this.)
                    {
                        var chunkMemberName = nameSyntax.Identifier.Text;

                        if (chunkMemberName[0] == 'U' && int.TryParse(chunkMemberName.Substring(1), out _))
                        {
                            return GetChunkLMemberFromMethodAndMemberName(methodName);
                        }

                        if (chunkMemberName == "version")
                        {
                            return GetChunkLMemberFromMethodAndMemberName(methodName, chunkMemberName);
                        }
                        
                        throw new Exception("Unexpected syntax");
                    }
                    else if (expression is MemberAccessExpressionSyntax expectedNodeMemberSyntax && expectedNodeMemberSyntax.Expression is IdentifierNameSyntax expectedNodeSyntax && expectedNodeSyntax.Identifier.Text == "n")
                    {
                        if (classMembers.TryGetValue(expectedNodeMemberSyntax.Name.Identifier.Text, out var propertySymbol))
                        {
                            return GetChunkLMemberFromMethodAndMemberName(methodName, propertySymbol.Name);
                        }

                        throw new Exception($"Unexpected syntax ({expectedNodeMemberSyntax.Name.Identifier.Text} is not [valid] field)");
                    }
                    else
                    {
                        throw new Exception("Unexpected syntax (args[0] not 'UXX' or 'n')"); // no longer happening yay
                    }
                }
                else
                {
                    throw new Exception("Unexpected syntax (args.Count != 1)");
                }
            }
            else
            {
                throw new Exception("Unexpected syntax");
            }
        }
        else if (statement is IfStatementSyntax ifStatement)
        {
            if (ifStatement.Statement is BlockSyntax blockSyntax)
            {
                var member = new ChunkLIfStatement { Left = "version", Sign = ">=", Right = "0" };

                foreach (var blockStatement in blockSyntax.Statements)
                {
                    member.Members.Add(CreateMemberFromStatement(blockStatement, classMembers));
                }
                
                return member;
            }
            else
            {
                throw new Exception("Unexpected syntax");
            }
        }
        else
        {
            throw new Exception("Unexpected syntax");
        }
    }

    private (string type, string name) GetTupleFromMethodAndMemberName(string methodName, string memberName = "")
    {
        return methodName switch
        {
            "Int32" => memberName == "version" ? ("version", "") : ("int", memberName),
            "Single" => ("float", memberName),
            "Boolean" => ("bool", memberName),
            "VersionInt32" => ("version", ""),
            "VersionByte" => ("versionbyte", ""),
            "TimeInt32" => ("timeint", memberName),
            _ => (methodName.ToLower(), memberName),
        };
    }

    private ChunkLMember GetChunkLMemberFromMethodAndMemberName(string methodName, string memberName = "")
    {
        var (type, name) = GetTupleFromMethodAndMemberName(methodName, memberName);
        return new ChunkLMember { Type = type, Name = name };
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
