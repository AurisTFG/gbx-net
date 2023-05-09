namespace GBX.NET.Generators;

class ChunkLIfStatement : IChunkLMember, IChunkLMemberList
{
    public string Type { get; init; } = "if";
    public required string Left { get; init; }
    public required string Sign { get; init; }
    public required string Right { get; init; }
    public List<IChunkLMember> Members { get; init; } = new();

    public override string ToString()
    {
        return $"{Type} {Left} {Sign} {Right}";
    }
}
