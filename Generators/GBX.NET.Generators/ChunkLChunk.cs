namespace GBX.NET.Generators;

class ChunkLChunk : IChunkLMemberList
{
    public required int ChunkId { get; init; }
    public List<IChunkLMember> Members { get; init; } = new();
    public bool Skippable { get; init; }
    public bool Ignored { get; init; }
    public string Comment { get; init; } = "";

    public override string ToString()
    {
        return $"0x{ChunkId:X8}{(Skippable ? " skippable" : "")}{(Ignored ? " ignored" : "")} ({Members.Count} members)";
    }
}
