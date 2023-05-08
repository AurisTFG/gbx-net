using GBX.NET.Generators.Extensions;
using Microsoft.CodeAnalysis;
using System.Globalization;
using System.Text.RegularExpressions;

namespace GBX.NET.Generators;

[Generator]
public class ChunkLGenerator : SourceGenerator
{
    public override bool Debug => true;

    public override void Execute(GeneratorExecutionContext context)
    {
        if (!context.AnalyzerConfigOptions.GlobalOptions.TryGetValue("build_property.projectdir", out var projDir))
        {
            throw new Exception("Project dir not found");
        }

        var enginesNamespace = context.Compilation
            .GlobalNamespace
            .NavigateToNamespace("GBX.NET.Engines") ?? throw new Exception("GBX.NET.Engines namespace not found.");

        var engineTypes = enginesNamespace.GetNamespaceMembers()
            .SelectMany(x => x.GetTypeMembers())
            .Where(x =>
            {
                var baseType = x.BaseType;

                while (baseType is not null && baseType.Name != "Node")
                {
                    baseType = baseType.BaseType;
                }

                return baseType is not null && baseType.Name == "Node";
            })
            .ToDictionary(x => $"{x.ContainingNamespace.Name}::{x.Name}");

        var enginesDir = Path.Combine(projDir, "Engines");

        foreach (var chunkLFile in Directory.GetFiles(enginesDir, "*.chunkl", SearchOption.AllDirectories))
        {
            var ns = Path.GetFileName(Path.GetDirectoryName(chunkLFile));

            using var reader = new StreamReader(chunkLFile);
            var headerLine = reader.ReadLine();

            var headerMatch = Regex.Match(headerLine.Trim(), @"(\w+)\s0x([0-9a-fA-F]{8})(\s*\/\/(.*))?$"); // Example: CGameCtnAnchoredObject 0x03101000 // comment

            if (!headerMatch.Success)
            {
                throw new Exception($"Invalid header in {chunkLFile}");
            }

            var className = headerMatch.Groups[1].Value;
            var classIdStr = headerMatch.Groups[2].Value;
            var classId = int.Parse(classIdStr, NumberStyles.HexNumber);

            engineTypes.TryGetValue($"{ns}::{className}", out var existingType);

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

                var metadataMatch = Regex.Match(lineStr, @"-\s*(\w+):(.+?)(\s*\/\/(.*))?$");
                var metadataName = metadataMatch.Groups[1].Value.Trim();
                var metadataValue = metadataMatch.Groups[2].Value.Trim();
            }

            if (lineStr is null || reader.EndOfStream)
            {
                continue;
            }
            
            var nullableLine = ParseChunk(reader, lineStr, classId, indent);

            if (nullableLine is null)
            {
                continue;
            }

            lineStr = nullableLine;

            while (true)
            {
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

                if (!IsRecognizableHex(line))
                {
                    throw new Exception("Unexpected syntax");
                }

                var chunkLine = ParseChunk(reader, lineStr, classId, indent);

                if (chunkLine is null)
                {
                    break;
                }

                lineStr = reader.ReadLine();
            }
        }
    }

    private static bool IsRecognizableHex(ReadOnlySpan<char> str)
    {
        return str[0] is '0' && str.Length >= 5 && str[1] is 'x' or 'X';
    }

    private static string? ParseChunk(StreamReader reader, string chunkLine, int classId, int indent)
    {
        var chunkMatch = Regex.Match(chunkLine, @"0x([0-9a-fA-F]{8}|[0-9a-fA-F]{3})(?=\s|$)\s*(skippable)?");

        if (!chunkMatch.Success)
        {
            throw new Exception("Invalid chunk definition");
        }

        var chunkIdStr = chunkMatch.Groups[1].Value;
        var chunkId = int.Parse(chunkIdStr, NumberStyles.HexNumber);

        if (chunkIdStr.Length == 3)
        {
            chunkId += classId;
        }
        else if (chunkIdStr.Length != 8) // Already validated but whatever
        {
            throw new Exception("Invalid chunk id");
        }

        while (true)
        {
            var lineStr = reader.ReadLine();

            if (lineStr is null)
            {
                break;
            }

            if (string.IsNullOrWhiteSpace(lineStr))
            {
                continue;
            }

            var lineIndent = lineStr.Length - lineStr.TrimStart().Length;

            lineStr = lineStr.Trim();
            var line = lineStr.AsSpan();

            if (line[0] is '/' && line.Length >= 2 && line[1] is '/')
            {
                continue;
            }

            if (lineIndent <= indent)
            {
                return lineStr;
            }

            ParseChunkMember(line, chunkId, lineIndent);
        }

        return null;
    }

    private static void ParseChunkMember(ReadOnlySpan<char> line, int chunkId, int lineIndent)
    {
        
    }
}
