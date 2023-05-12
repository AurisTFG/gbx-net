namespace GBX.NET.Generators;

class ChunkLChunk : IChunkLMemberList
{
    public required uint ChunkId { get; init; }
    public List<IChunkLMember> Members { get; init; } = new();
    public bool Skippable { get; init; }
    public bool Ignored { get; init; }
    public string Comment { get; init; } = "";

    public override string ToString()
    {
        return $"0x{ChunkId:X8}{(Skippable ? " skippable" : "")}{(Ignored ? " ignored" : "")} ({Members.Count} members)";
    }

    public void Save(TextWriter writer, uint? classId = null)
    {
        writer.Write("0x");

        if (classId.HasValue && classId.Value == (ChunkId & 0xFFFFF000))
        {
            writer.Write((ChunkId & 0xFFF).ToString("X3"));
        }
        else
        {
            writer.Write(ChunkId.ToString("X8"));
        }

        if (Skippable || Ignored)
        {
            writer.Write(" (");

            var pairs = new (string, bool)[]
            {
                ("skippable", Skippable),
                ("ignore", Ignored)
            };

            writer.Write(string.Join(", ", pairs.Where(x => x.Item2).Select(x => x.Item1)));

            writer.Write(")");
        }

        if (!string.IsNullOrWhiteSpace(Comment))
        {
            writer.Write(" // ");
            writer.Write(Comment);
        }

        writer.WriteLine();

        foreach (var member in Members)
        {
            WriteMember(member, writer, indent: 1);
        }
    }

    private void WriteMember(IChunkLMember generalMember, TextWriter writer, int indent)
    {
        for (var i = 0; i < indent; i++)
        {
            writer.Write(' ');
        }

        switch (generalMember)
        {
            case ChunkLMember member:
                writer.WriteLine(member.ToString());
                break;
            case ChunkLIfStatement ifStatement:
                writer.WriteLine(ifStatement.ToString());

                foreach(var m in ifStatement.Members)
                {
                    WriteMember(m, writer, indent + 1);
                }

                break;
            default:
                throw new NotImplementedException();
        }
    }
}
