namespace GBX.NET.Generators;

public interface IChunkLMember
{
    string Type { get; init; }
}

class ChunkLMember : IChunkLMember
{
    public required string Type { get; init; }
    public required string Name { get; init; }
    public bool ExactlyNamed { get; init; }
    public string? ExactName { get; init; }
    public string Comment { get; init; } = "";
    public string? DefaultValue { get; init; }
    public bool Nullable { get; init; }
    public int MinVersion { get; init; }

    public override string ToString()
    {
        return $"{Type} {Name}";
    }
}