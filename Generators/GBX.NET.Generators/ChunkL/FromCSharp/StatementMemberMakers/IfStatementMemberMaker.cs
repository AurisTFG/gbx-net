using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace GBX.NET.Generators.ChunkL.FromCSharp.StatementMemberMakers;

class IfStatementMemberMaker : StatementMemberMaker<IfStatementSyntax>
{
    public IfStatementMemberMaker(CSharpToChunkLProperties properties) : base(properties)
    {
    }

    public override IChunkLMember Make(IfStatementSyntax syntax)
    {
        string left;
        string sign;
        string right;

        switch (syntax.Condition)
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
                else if (binarySyntax.Left is MemberAccessExpressionSyntax leftMemberSyntax && leftMemberSyntax.Expression is IdentifierNameSyntax leftMemberIdentSyntax && leftMemberIdentSyntax.Identifier.Text == "n" && Properties.ClassMembers.TryGetValue(leftMemberSyntax.Name.Identifier.Text, out var propertySymbolLeft))
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
                if (memberSyntax.Expression is IdentifierNameSyntax memberIdentSyntax && memberIdentSyntax.Identifier.Text == "n" && Properties.ClassMembers.TryGetValue(memberSyntax.Name.Identifier.Text, out var propertySymbol))
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
                    if (prefixSyntax.Operand is IdentifierNameSyntax identSyntax)
                    {
                        left = identSyntax.Identifier.Text;
                        sign = "=";
                        right = "false";

                    }
                    else if (prefixSyntax.Operand is MemberAccessExpressionSyntax prefixMemberSyntax && prefixMemberSyntax.Expression is IdentifierNameSyntax prefixMemberIdentSyntax && prefixMemberIdentSyntax.Identifier.Text == "n" && Properties.ClassMembers.TryGetValue(prefixMemberSyntax.Name.Identifier.Text, out var propertySymbolPrefix))
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
        
        AddMembersFromBlock(member, syntax.Statement);

        if (syntax.Else is not null)
        {
            member.Else = new();

            AddMembersFromBlock(member.Else, syntax.Else.Statement);
        }

        return member;
    }

    private void AddMembersFromBlock(IChunkLMemberList member, StatementSyntax syntax)
    {
        if (syntax is BlockSyntax blockSyntax)
        {
            foreach (var blockStatement in blockSyntax.Statements)
            {
                member.Members.Add(Make(blockStatement, Properties));
            }
        }
        else
        {
            member.Members.Add(Make(syntax, Properties));
        }
    }
}
