using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace GBX.NET.Generators.ChunkL.FromCSharp.StatementMemberMakers;

class ThrowStatementMemberMaker : StatementMemberMaker<ThrowStatementSyntax>
{
    public ThrowStatementMemberMaker(CSharpToChunkLProperties properties) : base(properties)
    {
    }

    public override IChunkLMember Make(ThrowStatementSyntax syntax)
    {
        return new ChunkLMember { Type = "throw", Name = "" };
    }
}
