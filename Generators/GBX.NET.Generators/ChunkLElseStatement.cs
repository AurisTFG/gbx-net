namespace GBX.NET.Generators;

class ChunkLElseStatement : IChunkLMember, IChunkLMemberList
{
    public string Type { get; init; } = "else";
    public List<IChunkLMember> Members { get; init; } = new();

    public override string ToString()
    {
        return "else";
    }
}
