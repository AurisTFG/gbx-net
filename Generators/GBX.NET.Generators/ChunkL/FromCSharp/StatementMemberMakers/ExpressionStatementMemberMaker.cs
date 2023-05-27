using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace GBX.NET.Generators.ChunkL.FromCSharp.StatementMemberMakers;

class ExpressionStatementMemberMaker : StatementMemberMaker<ExpressionStatementSyntax>
{
    public ExpressionStatementMemberMaker(CSharpToChunkLProperties properties) : base(properties)
    {
    }

    public override IChunkLMember Make(ExpressionStatementSyntax syntax)
    {
        if (syntax.Expression is not InvocationExpressionSyntax invocationExpressionSyntax)
        {
            return ChunkLMember.OnlyComment(syntax.ToString());
        }

        if (invocationExpressionSyntax.Expression is not MemberAccessExpressionSyntax memberAccessExpressionSyntax)
        {
            return ChunkLMember.OnlyComment(syntax.ToString());
        }

        if (memberAccessExpressionSyntax.Expression is not IdentifierNameSyntax identifierNameSyntax)
        {
            return ChunkLMember.OnlyComment(syntax.ToString());
        }

        if (identifierNameSyntax.Identifier.Text != "rw")
        {
            return ChunkLMember.OnlyComment(syntax.ToString());
        }

        var methodName = memberAccessExpressionSyntax.Name.Identifier.Text;

        if (methodName == "VersionInt32")
        {
            return GetChunkLMemberFromMethodAndMemberName(methodName, nullable: false);
        }

        var args = invocationExpressionSyntax.ArgumentList.Arguments;

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

                    if (Properties.ChunkFieldMembers.TryGetValue(chunkMemberName, out var field))
                    {
                        if (methodName == "NodeRef")
                        {
                            nodeType = field.Type.Name is "Node" or "CMwNod" ? "node" : field.Type.Name;
                        }

                        nullable = field.NullableAnnotation == NullableAnnotation.Annotated;
                    }


                    return GetChunkLMemberFromMethodAndMemberName(methodName, nullable, nodeType: nodeType);
                }

                if (chunkMemberName == "version")
                {
                    return GetChunkLMemberFromMethodAndMemberName(methodName, nullable: false, chunkMemberName);
                }

                throw new Exception($"Unexpected syntax ({chunkMemberName})");
            }
            else if (expression is MemberAccessExpressionSyntax expectedNodeMemberSyntax && expectedNodeMemberSyntax.Expression is IdentifierNameSyntax expectedNodeSyntax && expectedNodeSyntax.Identifier.Text == "n")
            {
                if (Properties.ClassMembers.TryGetValue(expectedNodeMemberSyntax.Name.Identifier.Text, out var propertySymbol))
                {
                    var nodeType = "";

                    if (methodName == "NodeRef")
                    {
                        nodeType = propertySymbol.Type.Name is "Node" or "CMwNod" ? "node" : propertySymbol.Type.Name;
                    }

                    return GetChunkLMemberFromMethodAndMemberName(methodName, propertySymbol.NullableAnnotation == NullableAnnotation.Annotated, propertySymbol.Name, nodeType);
                }

                throw new Exception($"Unexpected syntax ({expectedNodeMemberSyntax.Name.Identifier.Text} is not [valid] field)");
            }
            else
            {
                throw new Exception($"Unexpected syntax (args[0] not 'UXX' or 'n')");
            }
        }
        else if (args.Count == 2)
        {
            var expression = args[0].Expression;

            if (methodName == "NodeRef" && expression is MemberAccessExpressionSyntax expectedNodeMemberSyntax && expectedNodeMemberSyntax.Expression is IdentifierNameSyntax expectedNodeSyntax && expectedNodeSyntax.Identifier.Text == "n")
            {
                if (Properties.ClassMembers.TryGetValue(expectedNodeMemberSyntax.Name.Identifier.Text, out var propertySymbol))
                {
                    var nodeType = propertySymbol.Type.Name is "Node" or "CMwNod" ? "node" : propertySymbol.Type.Name;

                    return new ChunkLMember { Type = nodeType + (propertySymbol.NullableAnnotation == NullableAnnotation.Annotated ? "?" : ""), Name = propertySymbol.Name, External = true };
                }

                throw new Exception($"Unexpected syntax ({expectedNodeMemberSyntax.Name.Identifier.Text} is not [valid] field)");
            }

            throw new Exception("Unexpected syntax");
        }
        else
        {
            throw new Exception("Unexpected syntax (args.Count != 1 or 2)");
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
}
