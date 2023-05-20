using System.Text;

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
    public bool External { get; init; }

    public override string ToString()
    {
        var sb = new StringBuilder();
        sb.Append(Type);

        if (Nullable)
        {
            sb.Append('?');
        }

        if (!string.IsNullOrEmpty(Name))
        {
            sb.Append(' ');
            sb.Append(Name);
        }

        if (ExactlyNamed || ExactName is not null || DefaultValue is not null || External)
        {
            sb.Append(" (");

            var pairs = new (string, string?)[]
            {
                ("exact", ExactlyNamed ? "" : ExactName),
                ("default", DefaultValue),
                ("ext", External ? "" : null)
            };

            sb.Append(string.Join(", ", pairs.Where(x => x.Item2 is not null).Select(x =>
            {
                if (x.Item1 is "exact" or "ext" && x.Item2 == "")
                {
                    return x.Item1;
                }
                
                return $"{x.Item1}={x.Item2}";
            })));

            sb.Append(")");
        }

        if (!string.IsNullOrWhiteSpace(Comment))
        {
            sb.Append(" // ");
            sb.Append(Comment);
        }

        return sb.ToString();
    }
}