using System.Globalization;
using System.Text.RegularExpressions;

namespace GBX.NET.Generators;

class ChunkL
{
    public required string ClassName { get; init; }
    public required uint ClassId { get; init; }
    public required Dictionary<string, string> Metadata { get; init; } = new();
    public required List<ChunkLChunk> Chunks { get; init; } = new();

    public void Save(TextWriter writer)
    {
        writer.Write(ClassName);
        writer.Write(" 0x");
        writer.WriteLine(ClassId.ToString("X8"));

        foreach (var keyVal in Metadata)
        {
            writer.Write("- ");
            writer.Write(keyVal.Key);
            writer.Write(": ");
            writer.WriteLine(keyVal.Value);
        }
        
        foreach (var chunk in Chunks)
        {
            writer.WriteLine();
            chunk.Save(writer, ClassId);
        }
    }

    public static ChunkL Parse(StreamReader reader)
    {
        var headerLine = reader.ReadLine();

        var headerMatch = Regex.Match(headerLine.Trim(), @"^(\w+)\s0x([0-9a-fA-F]{8})(\s*\/\/(.*))?$"); // Example: CGameCtnAnchoredObject 0x03101000 // comment

        if (!headerMatch.Success)
        {
            throw new Exception("Invalid header");
        }

        var className = headerMatch.Groups[1].Value;
        var classIdStr = headerMatch.Groups[2].Value;
        var classId = uint.Parse(classIdStr, NumberStyles.HexNumber);
        var metadata = new Dictionary<string, string>();

        string lineStr;
        int indent = 0;

        while (true)
        {
            lineStr = reader.ReadLine();

            if (lineStr is null)
            {
                break;
            }

            if (string.IsNullOrWhiteSpace(lineStr))
            {
                continue;
            }

            indent = lineStr.Length - lineStr.TrimStart().Length;
            lineStr = lineStr.Trim();

            var line = lineStr.AsSpan();

            if (line[0] is '/' && line.Length >= 2 && line[1] is '/')
            {
                continue;
            }

            if (IsRecognizableHex(line))
            {
                break; // chunk time
            }

            if (line[0] != '-')
            {
                throw new Exception("Invalid syntax after header");
            }

            var metadataMatch = Regex.Match(lineStr, @"^-\s*(\w+):(.+?)(\s*\/\/(.*))?$");
            var metadataName = metadataMatch.Groups[1].Value.Trim().ToLowerInvariant();
            var metadataValue = metadataMatch.Groups[2].Value.Trim();

            metadata.Add(metadataName, metadataValue);
        }

        if (lineStr is null || reader.EndOfStream)
        {
            return new()
            {
                ClassName = className,
                ClassId = classId,
                Metadata = metadata,
                Chunks = new()
            };
        }

        if (ParseChunk(reader, lineStr, classId, indent, out var c) is not string nullableLine)
        {
            return new()
            {
                ClassName = className,
                ClassId = classId,
                Metadata = metadata,
                Chunks = c is null ? new() : new() { c }
            };
        }

        var chunks = new List<ChunkLChunk>();

        if (c is not null)
        {
            chunks.Add(c);
        }

        lineStr = nullableLine;

        while (true)
        {
            lineStr ??= reader.ReadLine();

            if (lineStr is null)
            {
                break;
            }

            if (string.IsNullOrWhiteSpace(lineStr))
            {
                lineStr = null!;
                continue;
            }

            indent = lineStr.Length - lineStr.TrimStart().Length;
            lineStr = lineStr.Trim();
            var line = lineStr.AsSpan();

            if (line[0] is '/' && line.Length >= 2 && line[1] is '/')
            {
                lineStr = null!;
                continue;
            }

            if (!IsRecognizableHex(line))
            {
                throw new Exception("Unexpected syntax");
            }

            var chunkLine = ParseChunk(reader, lineStr, classId, indent, out var chunk);

            chunks.Add(chunk);

            if (chunkLine is null)
            {
                break;
            }

            lineStr = chunkLine;
        }

        return new()
        {
            ClassName = className,
            ClassId = classId,
            Metadata = metadata,
            Chunks = chunks
        };
    }

    private static bool IsRecognizableHex(ReadOnlySpan<char> str)
    {
        return str[0] is '0' && str.Length >= 5 && str[1] is 'x' or 'X';
    }

    private static string? ParseChunk(StreamReader reader, string chunkLine, uint classId, int indent, out ChunkLChunk chunk)
    {
        var chunkMatch = Regex.Match(chunkLine, @"^0x([0-9a-fA-F]{8}|[0-9a-fA-F]{3})(?=\s|$)\s*(\((.*)\))?\s*(\/\/(.*))?$");

        if (!chunkMatch.Success)
        {
            throw new Exception("Invalid chunk definition");
        }

        var chunkIdStr = chunkMatch.Groups[1].Value;
        var chunkId = uint.Parse(chunkIdStr, NumberStyles.HexNumber);

        if (chunkIdStr.Length == 3)
        {
            chunkId += classId;
        }
        else if (chunkIdStr.Length != 8) // Already validated but whatever
        {
            throw new Exception("Invalid chunk id");
        }

        var optionsDict = ParseOptions(chunkMatch.Groups[3].Value);

        chunk = new()
        {
            ChunkId = chunkId,
            Skippable = optionsDict.ContainsKey("skippable"),
            Ignored = optionsDict.ContainsKey("ignored") || optionsDict.ContainsKey("ignore"),
            Comment = chunkMatch.Groups[chunkMatch.Groups.Count - 1].Value
        };

        return ReadWholeIndentation(reader, chunkId, indent, chunk);
    }

    private static string? ReadWholeIndentation(StreamReader reader, uint chunkId, int indent, IChunkLMemberList memberList, int minVersion = 0)
    {
        var lineStr = default(string);

        while (true)
        {
            lineStr ??= reader.ReadLine();

            if (lineStr is null)
            {
                break;
            }

            if (string.IsNullOrWhiteSpace(lineStr))
            {
                lineStr = null;
                continue;
            }

            var lineIndent = lineStr.Length - lineStr.TrimStart().Length;

            var trimmedLineStr = lineStr.Trim();
            var line = trimmedLineStr.AsSpan();

            if (line[0] is '/' && line.Length >= 2 && line[1] is '/')
            {
                lineStr = null;
                continue;
            }

            if (lineIndent <= indent)
            {
                return lineStr;
            }

            lineStr = ParseChunkMember(reader, trimmedLineStr, chunkId, lineIndent, minVersion, out IChunkLMember member);

            memberList.Members.Add(member);
        }

        return null;
    }

    private static string? ParseChunkMember(StreamReader reader, string line, uint chunkId, int lineIndent, int minVersion, out IChunkLMember member)
    {
        var ifMatch = Regex.Match(line, @"^if\s(.+?)?\s*(\((.*)\))?\s*(\/\/(.*))?$");

        if (ifMatch.Success)
        {
            var l = ParseIfStatement(reader, ifMatch.Groups[1].Value, chunkId, lineIndent, minVersion, out var ifStatement);
            member = ifStatement;
            return l;
        }

        var memberMatch = Regex.Match(line, @"^(\w+)(\?)?\s?(\w+?)?\s*(\((.*)\))?\s*(\/\/(.*))?$");

        if (!memberMatch.Success)
        {
            throw new Exception("Invalid chunk member syntax");
        }
        
        var optionsDict = ParseOptions(memberMatch.Groups[5].Value);

        member = new ChunkLMember()
        {
            Type = memberMatch.Groups[1].Value,
            Nullable = memberMatch.Groups[2].Value == "?",
            Name = memberMatch.Groups[3].Value,
            ExactlyNamed = optionsDict.TryGetValue("exact", out var exact) && exact == "",
            ExactName = optionsDict.TryGetValue("exact", out var exactName) ? exactName : null,
            DefaultValue = optionsDict.TryGetValue("default", out var defaultVal) ? defaultVal : null,
            Comment = memberMatch.Groups[memberMatch.Groups.Count - 1].Value.Trim(),
            MinVersion = minVersion
        };

        return null;
    }

    private static string? ParseIfStatement(StreamReader reader, string statement, uint chunkId, int lineIndent, int minVersion, out ChunkLIfStatement member)
    {
        var versionComparison = MatchVersion(statement);

        if (versionComparison.HasValue)
        {
            member = new ChunkLIfStatement()
            {
                Left = "version",
                Sign = versionComparison.Value.sign,
                Right = versionComparison.Value.ver.ToString(),
            };

            minVersion = versionComparison.Value.sign is "=" or "==" or ">=" ? versionComparison.Value.ver : 0;

            return ReadWholeIndentation(reader, chunkId, lineIndent, member, minVersion);
        }

        // (Flags & 4) = 4
        var averageStatementMatch = Regex.Match(statement, @"^(.+?)\s*(>=|<=|==|>|<|=)\s*(.+?)(\s*\/\/(.*))?$");

        if (averageStatementMatch.Success)
        {
            member = new ChunkLIfStatement()
            {
                Left = averageStatementMatch.Groups[1].Value,
                Sign = averageStatementMatch.Groups[2].Value,
                Right = averageStatementMatch.Groups[3].Value, // improve?
            };

            return ReadWholeIndentation(reader, chunkId, lineIndent, member, minVersion);
        }

        throw new Exception("Invalid if statement");
    }

    private static (string sign, int ver)? MatchVersion(string statement)
    {
        var shortVersionMatch = Regex.Match(statement, @"^(>=|<=|==|>|<|=)\s*v([0-9]+)(\s*\/\/(.*))?$");

        if (shortVersionMatch.Success)
        {
            return (shortVersionMatch.Groups[1].Value, int.Parse(shortVersionMatch.Groups[2].Value));
        }

        return null;
    }

    private static Dictionary<string, string> ParseOptions(string optionsString)
    {
        var options = optionsString.Trim().Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
        var optionsDict = new Dictionary<string, string>();

        foreach (var option in options)
        {
            var optionMatch = Regex.Match(option.Trim(), @"^(\w+)(?:\s*=\s*(.+))?$");

            if (!optionMatch.Success)
            {
                throw new Exception($"Invalid chunk option ({option})");
            }

            var optionName = optionMatch.Groups[1].Value.Trim().ToLowerInvariant();
            var optionValue = optionMatch.Groups[2].Value.Trim();

            optionsDict.Add(optionName, optionValue);
        }

        return optionsDict;
    }
}
