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

                var chunkList = new List<ChunkLChunk>();

                foreach(var c in chunks)
                {
                    try
                    {
                        chunkList.Add(ChunkSymbolToChunkLChunk(c, members));
                    }
                    catch (Exception ex)
                    {
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

            if (accessorSyntax.ExpressionBody is ArrowExpressionClauseSyntax arrowSyntax && arrowSyntax.Expression is IdentifierNameSyntax nameSyntax)
            {
                dict.Add(nameSyntax.Identifier.Text, propertySymbol);
            }

            // full body cases are missing
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

        var chunkFieldMembers = chunkSymbol.GetMembers().OfType<IFieldSymbol>().ToDictionary(x => x.Name);

        var members = new List<IChunkLMember>();

        foreach (var statement in readWriteMethodSyntax.Body.Statements)
        {
            members.Add(CreateMemberFromStatement(statement, classMembers, chunkFieldMembers));
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

    private IChunkLMember CreateMemberFromStatement(StatementSyntax statement, Dictionary<string, IPropertySymbol> classMembers, Dictionary<string, IFieldSymbol> chunkFieldMembers)
    {
        if (statement is ExpressionStatementSyntax expressionStatement && expressionStatement.Expression is InvocationExpressionSyntax invocationSyntax)
        {
            if (invocationSyntax.Expression is MemberAccessExpressionSyntax memberSyntax && memberSyntax.Expression is IdentifierNameSyntax expectedRwNameSyntax && expectedRwNameSyntax.Identifier.Text == "rw")
            {
                var methodName = memberSyntax.Name.Identifier.Text;

                if (methodName == "VersionInt32")
                {
                    return GetChunkLMemberFromMethodAndMemberName(methodName, nullable: false);
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
                        return GetChunkLMemberFromMethodAndMemberName(methodName, nullable: false);
                    }

                    if (expression is IdentifierNameSyntax nameSyntax) // if it is member inside chunk (not working with this.)
                    {
                        var chunkMemberName = nameSyntax.Identifier.Text;

                        if (chunkMemberName[0] == 'U' && int.TryParse(chunkMemberName.Substring(1), out _))
                        {
                            var nodeType = "";
                            var nullable = false;

                            if (chunkFieldMembers.TryGetValue(chunkMemberName, out var field))
                            {
                                if (methodName == "NodeRef")
                                {
                                    nodeType = (field.Type.Name is "Node" or "CMwNod" ? "node" : field.Type.Name);
                                }

                                nullable = field.NullableAnnotation == NullableAnnotation.Annotated;
                            }


                            return GetChunkLMemberFromMethodAndMemberName(methodName, nullable, nodeType: nodeType);
                        }

                        if (chunkMemberName == "version")
                        {
                            return GetChunkLMemberFromMethodAndMemberName(methodName, nullable: false, chunkMemberName);
                        }
                        
                        throw new Exception("Unexpected syntax");
                    }
                    else if (expression is MemberAccessExpressionSyntax expectedNodeMemberSyntax && expectedNodeMemberSyntax.Expression is IdentifierNameSyntax expectedNodeSyntax && expectedNodeSyntax.Identifier.Text == "n")
                    {
                        if (classMembers.TryGetValue(expectedNodeMemberSyntax.Name.Identifier.Text, out var propertySymbol))
                        {
                            var nodeType = "";

                            if (methodName == "NodeRef")
                            {
                                nodeType = (propertySymbol.Type.Name is "Node" or "CMwNod" ? "node" : propertySymbol.Type.Name);
                            }

                            return GetChunkLMemberFromMethodAndMemberName(methodName, propertySymbol.NullableAnnotation == NullableAnnotation.Annotated, propertySymbol.Name, nodeType);
                        }

                        throw new Exception($"Unexpected syntax ({expectedNodeMemberSyntax.Name.Identifier.Text} is not [valid] field)");
                    }
                    else
                    {
                        throw new Exception("Unexpected syntax (args[0] not 'UXX' or 'n')");
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
            string left;
            string sign;
            string right;

            switch (ifStatement.Condition)
            {
                case IsPatternExpressionSyntax isSyntax:
                    if (isSyntax.Expression is IdentifierNameSyntax nameSyntax && isSyntax.Pattern is ConstantPatternSyntax constantSyntax && constantSyntax.Expression is LiteralExpressionSyntax literalSyntax)
                    {
                        left = nameSyntax.Identifier.Text == "Version" ? "version" : nameSyntax.Identifier.Text;
                        sign = "is";
                        right = literalSyntax.Token.Text;
                    }
                    else
                    {
                        throw new Exception("Unexpected if statement syntax");
                    }
                    break;
                case BinaryExpressionSyntax binarySyntax:
                    if (binarySyntax.Left is IdentifierNameSyntax leftIdentSyntax)
                    {
                        left = leftIdentSyntax.Identifier.Text == "Version" ? "version" : leftIdentSyntax.Identifier.Text;
                    }
                    else if (binarySyntax.Left is MemberAccessExpressionSyntax leftMemberSyntax && leftMemberSyntax.Expression is IdentifierNameSyntax leftMemberIdentSyntax && leftMemberIdentSyntax.Identifier.Text == "n" && classMembers.TryGetValue(leftMemberSyntax.Name.Identifier.Text, out var propertySymbolLeft))
                    {
                        left = propertySymbolLeft.Name;
                    }
                    else
                    {
                        throw new Exception("Unexpected syntax");
                    }
                        
                    sign = binarySyntax.OperatorToken.Text == "==" ? "=" : binarySyntax.OperatorToken.Text;

                    if (binarySyntax.Right is IdentifierNameSyntax rightIdentSyntax)
                    {
                        right = rightIdentSyntax.Identifier.Text;
                    }
                    else if (binarySyntax.Right is LiteralExpressionSyntax rightLiteralSyntax)
                    {
                        right = rightLiteralSyntax.Token.Text;
                    }
                    else
                    {
                        throw new Exception("Unexpected syntax");
                    }
                    break;
                case IdentifierNameSyntax identifierSyntax:
                    left = identifierSyntax.Identifier.Text;
                    sign = "=";
                    right = "true";
                    break;
                case MemberAccessExpressionSyntax memberSyntax:
                    if (memberSyntax.Expression is IdentifierNameSyntax memberIdentSyntax && memberIdentSyntax.Identifier.Text == "n" && classMembers.TryGetValue(memberSyntax.Name.Identifier.Text, out var propertySymbol))
                    {
                        left = propertySymbol.Name;
                        sign = "=";
                        right = "true";
                    }
                    else
                    {
                        throw new Exception("Unexpected if statement syntax");
                    }
                    break;
                case PrefixUnaryExpressionSyntax prefixSyntax:
                    if (prefixSyntax.OperatorToken.Text == "!")
                    {
                        if(prefixSyntax.Operand is IdentifierNameSyntax identSyntax)
                        {
                            left = identSyntax.Identifier.Text;
                            sign = "=";
                            right = "false";
                                
                        }
                        else if (prefixSyntax.Operand is MemberAccessExpressionSyntax prefixMemberSyntax && prefixMemberSyntax.Expression is IdentifierNameSyntax prefixMemberIdentSyntax && prefixMemberIdentSyntax.Identifier.Text == "n" && classMembers.TryGetValue(prefixMemberSyntax.Name.Identifier.Text, out var propertySymbolPrefix))
                        {
                            left = propertySymbolPrefix.Name;
                            sign = "=";
                            right = "false";
                        }
                        else
                        {
                            throw new Exception("Unexpected if statement syntax");
                        }
                    }
                    else
                    {
                        throw new Exception("Unexpected if statement syntax");
                    }
                    break;
                default:
                    throw new Exception("Unexpected if statement syntax");
            }

            var member = new ChunkLIfStatement { Left = left, Sign = sign, Right = right };

            if (ifStatement.Statement is BlockSyntax blockSyntax)
            {
                foreach (var blockStatement in blockSyntax.Statements)
                {
                    member.Members.Add(CreateMemberFromStatement(blockStatement, classMembers, chunkFieldMembers));
                }
            }
            else
            {
                member.Members.Add(CreateMemberFromStatement(ifStatement.Statement, classMembers, chunkFieldMembers));
            }

            if (ifStatement.Else is not null)
            {
                member.Else = new();

                if (ifStatement.Else.Statement is BlockSyntax b)
                {
                    foreach (var blockStatement in b.Statements)
                    {
                        member.Else.Members.Add(CreateMemberFromStatement(blockStatement, classMembers, chunkFieldMembers));
                    }
                }
                else
                {
                    member.Else.Members.Add(CreateMemberFromStatement(ifStatement.Else.Statement, classMembers, chunkFieldMembers));
                }
            }

            return member;
        }
        else if (statement is ReturnStatementSyntax)
        {
            return new ChunkLMember { Type = "return", Name = "" };
        }
        else if (statement is ThrowStatementSyntax)
        {
            return new ChunkLMember { Type = "throw", Name = "" };
        }
        else
        {
            return new ChunkLMember { Type = "", Name = "", Comment = statement.ToString() };
        }
    }

    private (string type, string name) GetTupleFromMethodAndMemberName(string methodName, string memberName, string nodeType)
    {
        return methodName switch
        {
            "Int32" => memberName == "version" ? ("version", "") : ("int", memberName),
            "Single" => ("float", memberName),
            "Boolean" => ("bool", memberName),
            "VersionInt32" => ("version", ""),
            "VersionByte" => ("versionbyte", ""),
            "TimeInt32" => ("timeint", memberName),
            "ArrayId" => ("id[]", memberName),
            "NodeRef" => (nodeType, memberName),
            _ => (methodName.ToLower(), memberName),
        };
    }

    private ChunkLMember GetChunkLMemberFromMethodAndMemberName(string methodName, bool nullable, string memberName = "", string nodeType = "")
    {
        var (type, name) = GetTupleFromMethodAndMemberName(methodName, memberName, nodeType);
        return new ChunkLMember { Type = type + (nullable ? "?" : ""), Name = name };
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
