namespace GBX.NET.Generators;

class ChunkLChunk : IChunkLMemberList
{
    public required int ChunkId { get; init; }
    public List<IChunkLMember> Members { get; init; } = new();
}
