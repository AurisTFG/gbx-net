using Microsoft.CodeAnalysis;

namespace GBX.NET.Generators.ChunkL.FromCSharp;

class CSharpToChunkLProperties
{
    public required Dictionary<string, IPropertySymbol> ClassMembers { get; init; }
    public required Dictionary<string, IFieldSymbol> ChunkFieldMembers { get; init; }
}
