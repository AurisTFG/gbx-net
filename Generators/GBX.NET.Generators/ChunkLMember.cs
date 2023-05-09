namespace GBX.NET.Generators;

public interface IChunkLMember
{
    string Type { get; init; }
}

class ChunkLMember : IChunkLMember
{
    public required string Type { get; init; }
    public required string Name { get; init; }

    public override string ToString()
    {
        return $"{Type} {Name}";
    }
}