using GBX.NET.Generators.Extensions;
using Microsoft.CodeAnalysis;
using System.Text;

namespace GBX.NET.Generators;

[Generator]
public class ChunkLToCSharpGenerator : SourceGenerator
{
    public override bool Debug => false;

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
            var chunkL = ChunkL.Parse(reader);

            _ = engineTypes.TryGetValue($"{ns}::{chunkL.ClassName}", out var existingType);

            var sb = new StringBuilder();

            sb.AppendLine($"namespace GBX.NET.Engines.{ns};");
            sb.AppendLine();

            sb.AppendLine($"/// <remarks>ID: 0x{chunkL.ClassId:X8}</remarks>");

            if (existingType is null || !existingType.GetAttributes().Any(x => x.AttributeClass?.Name == "NodeAttribute"))
            {
                sb.AppendLine($"[Node(0x{chunkL.ClassId:X8})]");
            }

            sb.Append($"public partial class {chunkL.ClassName}");

            if (existingType is null || existingType.BaseType is null)
            {
                if (chunkL.Metadata.TryGetValue("inherits", out var inheritedClass))
                {
                    sb.Append($" : {inheritedClass}");
                }
                else
                {
                    sb.Append($" : CMwNod");
                }
            }

            sb.AppendLine();
            sb.AppendLine("{");

            if (existingType is null || !existingType.Constructors.Any(x => x.Parameters.Length == 0))
            {
                sb.AppendLine(1, $"internal {chunkL.ClassName}()");
                sb.AppendLine(1, "{");
                sb.AppendLine(1, "}");
            }

            sb.AppendLine();
            sb.AppendLine(1, "#region Chunks");

            foreach (var chunk in chunkL.Chunks)
            {
                var chunkIdHexStr = (chunk.ChunkId & 0xFFF).ToString("X3");
                var skippableStr = chunk.Skippable ? " skippable" : "";

                var hasVersion = chunk.Members.Any(x => x.Type == "version"); // it does not count nesting

                sb.AppendLine();
                sb.AppendLine(1, $"#region 0x{chunkIdHexStr}{skippableStr} chunk");
                sb.AppendLine();
                sb.AppendLine(1, "/// <summary>");
                sb.AppendLine(1, $"/// {chunkL.ClassName} 0x{chunkIdHexStr}{skippableStr} chunk");
                sb.AppendLine(1, "/// </summary>");
                sb.Append(1, $"public class Chunk{chunk.ChunkId:X8} : {(chunk.Skippable ? "Skippable" : "")}Chunk<{chunkL.ClassName}>");

                if (hasVersion)
                {
                    sb.Append(", IVersionable");
                }

                sb.AppendLine();
                sb.AppendLine(1, "{");

                var sbRW = new StringBuilder();

                AppendChunkMembers(sb, sbRW, sbRWIndent: 3, chunk);

                sb.AppendLine();
                sb.AppendLine(2, $"public override void ReadWrite({chunkL.ClassName} n, GameBoxReaderWriter rw)");
                sb.AppendLine(2, "{");
                sb.Append(sbRW.ToString());
                sb.AppendLine(2, "}");


                sb.AppendLine(1, "}");

                sb.AppendLine();
                sb.AppendLine(1, "#endregion");
            }

            sb.AppendLine();
            sb.AppendLine(1, "#endregion");


            sb.AppendLine("}");

            context.AddSource(chunkL.ClassName, sb.ToString());
        }
    }

    private static void AppendChunkMembers(StringBuilder sb, StringBuilder sbRW, int sbRWIndent, IChunkLMemberList memberList)
    {
        foreach (var m in memberList.Members)
        {
            switch (m)
            {
                case ChunkLMember member:
                    switch (member.Type)
                    {
                        case "version":
                            sb.AppendLine(2, "private int version;");
                            sb.AppendLine(2, "public int Version { get => version; set => version = value; }");
                            sbRW.AppendLine(sbRWIndent, "rw.Version(this);");
                            break;
                        default:
                            sbRW.AppendLine(sbRWIndent, $"throw new Exception(\"{member.Type} {member.Name}\");");
                            break;
                    }
                    break;
                case ChunkLIfStatement ifStatement:
                    sbRW.AppendLine(sbRWIndent, $"if ({ifStatement.Left} {ifStatement.Sign} {ifStatement.Right})");
                    sbRW.AppendLine(sbRWIndent, "{");
                    AppendChunkMembers(sb, sbRW, sbRWIndent + 1, ifStatement);
                    sbRW.AppendLine(sbRWIndent, "}");
                    break;
            }
        }
    }
}
