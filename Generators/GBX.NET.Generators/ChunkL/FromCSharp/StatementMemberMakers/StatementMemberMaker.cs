using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis;

namespace GBX.NET.Generators.ChunkL.FromCSharp.StatementMemberMakers;

abstract class StatementMemberMaker
{
    public static IChunkLMember Make(StatementSyntax syntax, CSharpToChunkLProperties properties)
    {
        return syntax switch
        {
            ExpressionStatementSyntax expressionStatementSyntax => new ExpressionStatementMemberMaker(properties).Make(expressionStatementSyntax),
            IfStatementSyntax ifStatementSyntax => new IfStatementMemberMaker(properties).Make(ifStatementSyntax),
            ReturnStatementSyntax => new ChunkLMember { Type = "return", Name = "" },
            ThrowStatementSyntax throwStatementSyntax => new ThrowStatementMemberMaker(properties).Make(throwStatementSyntax),

            _ => ChunkLMember.OnlyComment(syntax.ToString())
        };
    }
}

abstract class StatementMemberMaker<T> : StatementMemberMaker where T : StatementSyntax
{
    public CSharpToChunkLProperties Properties { get; }

    public StatementMemberMaker(CSharpToChunkLProperties properties)
    {
        Properties = properties;
    }

    public abstract IChunkLMember Make(T syntax);
}
