using GBX.NET.Generators.Extensions;
using System.Text;

namespace GBX.NET.Generators.ChunkL;

class ChunkLChunkBuilder
{
    private readonly ChunkLClassBuilder classBuilder;
    private readonly ChunkLChunk chunk;
    private readonly List<ChunkLMember> usedChunkMembers = new();

    private readonly StringBuilder sbClass;
    private readonly StringBuilder sbChunks;
    private readonly StringBuilder sbReadWrite = new();

    private int unknownMemberCounter = 0;

    public ChunkLChunkBuilder(ChunkLClassBuilder classBuilder, ChunkLChunk chunk)
    {
        this.classBuilder = classBuilder;
        this.chunk = chunk;

        sbClass = classBuilder.SbClass;
        sbChunks = classBuilder.SbChunks;
    }

    public void Build()
    {
        var chunkIdHexStr = (chunk.ChunkId & 0xFFF).ToString("X3");
        var skippableStr = chunk.Skippable ? " skippable" : "";

        var hasVersion = chunk.Members.Any(x => x.Type == "version"); // it does not count nesting

        sbChunks.AppendLine();
        sbChunks.AppendLine(1, $"#region 0x{chunkIdHexStr}{skippableStr} chunk");
        sbChunks.AppendLine();
        sbChunks.AppendLine(1, "/// <summary>");
        sbChunks.AppendLine(1, $"/// {classBuilder.ChunkL.ClassName} 0x{chunkIdHexStr}{skippableStr} chunk");
        sbChunks.AppendLine(1, "/// </summary>");
        sbChunks.AppendLine(1, $"[Chunk(0x{chunk.ChunkId:X8})]");

        if (chunk.Ignored)
        {
            sbChunks.AppendLine(1, "[IgnoreChunk]");
        }

        sbChunks.Append(1, $"public class Chunk{chunk.ChunkId:X8} : {(chunk.Skippable ? "Skippable" : "")}Chunk<{classBuilder.ChunkL.ClassName}>");

        if (hasVersion)
        {
            sbChunks.Append(", IVersionable");
        }

        sbChunks.AppendLine();
        sbChunks.AppendLine(1, "{");

        if (!chunk.Ignored)
        {
            AppendChunkMembers(sbRWIndent: 3, chunk);

            sbChunks.AppendLine();
            sbChunks.AppendLine(2, $"public override void ReadWrite({classBuilder.ChunkL.ClassName} n, GameBoxReaderWriter rw)");
            sbChunks.AppendLine(2, "{");
            sbChunks.Append(sbReadWrite.ToString());
            sbChunks.AppendLine(2, "}");
        }

        sbChunks.AppendLine(1, "}");

        sbChunks.AppendLine();
        sbChunks.AppendLine(1, "#endregion");
    }

    private void AppendChunkMembers(int sbRWIndent, IChunkLMemberList memberList)
    {
        foreach (var m in memberList.Members)
        {
            switch (m)
            {
                case ChunkLMember member:
                    switch (member.Type.ToLowerInvariant())
                    {
                        case "version":
                            if (usedChunkMembers.Contains(member))
                            {
                                throw new Exception("Double version not supported");
                            }

                            if (member.Nullable)
                            {
                                throw new Exception("Nullable version not supported");
                            }

                            if (!string.IsNullOrWhiteSpace(member.Comment))
                            {
                                sbChunks.AppendLine(2, "/// <summary>");
                                sbChunks.Append(2, "/// ");
                                sbChunks.AppendLine(member.Comment);
                                sbChunks.AppendLine(2, "/// </summary>");
                            }

                            sbChunks.Append(2, "public int Version { get; set; }");

                            if (!string.IsNullOrWhiteSpace(member.DefaultValue))
                            {
                                sbChunks.Append($" = {member.DefaultValue};");
                            }

                            sbChunks.AppendLine();
                            sbReadWrite.AppendLine(sbRWIndent, "rw.VersionInt32(this);");
                            break;
                        case "ident" or "meta": AppendMember(sbRWIndent, member, "Ident"); break;
                        case "vec3": AppendMember(sbRWIndent, member, "Vec3"); break;
                        case "byte3": AppendMember(sbRWIndent, member, "Byte3"); break;
                        case "id" or "lookbackstring": AppendMember(sbRWIndent, member, "string", "Id"); break;
                        case "int16": AppendMember(sbRWIndent, member, "short", "Int16"); break;
                        case "int" or "int32": AppendMember(sbRWIndent, member, "int", "Int32"); break;
                        case "float" or "single": AppendMember(sbRWIndent, member, "float", "Single"); break;
                        case "fileref": AppendMember(sbRWIndent, member, "FileRef"); break;
                        case "string" or "str": AppendMember(sbRWIndent, member, "String"); break;
                        case "throw":
                            if (member.Name is "v" or "version" or "Version")
                            {
                                sbReadWrite.AppendLine(sbRWIndent, $"throw new VersionNotSupportedException(Version);");
                            }
                            else
                            {
                                sbReadWrite.AppendLine(sbRWIndent, "throw new Exception();");
                            }
                            break;
                        default:
                            if (member.Type[0] == 'C')
                            {
                                AppendMember(sbRWIndent, member, member.Type, $"NodeRef<{member.Type}>");
                            }
                            else
                            {
                                sbReadWrite.AppendLine(sbRWIndent, $"throw new Exception(\"{member.Type} {member.Name}\");");
                            }
                            break;
                    }
                    usedChunkMembers.Add(member);
                    break;
                case ChunkLIfStatement ifStatement:
                    var sign = ifStatement.Sign == "=" ? "==" : ifStatement.Sign;
                    var left = ifStatement.Left == "version" ? "Version" : ifStatement.Left;

                    sbReadWrite.AppendLine();
                    sbReadWrite.AppendLine(sbRWIndent, $"if ({left} {sign} {ifStatement.Right})");
                    sbReadWrite.AppendLine(sbRWIndent, "{");
                    AppendChunkMembers(sbRWIndent + 1, ifStatement);
                    sbReadWrite.AppendLine(sbRWIndent, "}");

                    if (ifStatement.Else is not null)
                    {
                        sbReadWrite.AppendLine(sbRWIndent, "else");
                        sbReadWrite.AppendLine(sbRWIndent, "{");
                        AppendChunkMembers(sbRWIndent + 1, ifStatement.Else);
                        sbReadWrite.AppendLine(sbRWIndent, "}");
                    }

                    break;
            }
        }
    }

    private void AppendMember(int sbRWIndent, ChunkLMember member, string type, string? rwName = null)
    {
        if (string.IsNullOrWhiteSpace(member.Name))
        {
            unknownMemberCounter++;
            sbChunks.AppendLine(2, $"public {type} U{unknownMemberCounter:00};");
            sbReadWrite.AppendLine(sbRWIndent, $"rw.{rwName ?? type}(ref U{unknownMemberCounter:00});");
            return;
        }

        var lowerFirstCaseName = char.ToLowerInvariant(member.Name[0]) + member.Name.Substring(1);

        if (!classBuilder.ExistingPropertySymbols.ContainsKey(member.Name))
        {
            sbClass.Append(1, "private ");
            sbClass.Append(type);

            if (member.Nullable)
            {
                sbClass.Append('?');
            }

            sbClass.Append(' ');
            sbClass.Append(lowerFirstCaseName);
            sbClass.AppendLine(";");

            if (!string.IsNullOrWhiteSpace(member.Comment))
            {
                sbClass.AppendLine(1, "/// <summary>");
                sbClass.Append(1, "/// ");
                sbClass.AppendLine(member.Comment);
                sbClass.AppendLine(1, "/// </summary>");
            }

            sbClass.Append(1, $"[NodeMember(");

            if (member.ExactlyNamed)
            {
                sbClass.Append("ExactlyNamed = true");
            }
            else if (member.ExactName is not null)
            {
                sbClass.Append($"ExactName = \"{member.ExactName}\"");
            }

            sbClass.AppendLine(")]");

            sbClass.Append(1, "[AppliedWithChunk<Chunk");
            sbClass.Append(chunk.ChunkId.ToString("X8"));
            sbClass.Append(">(");

            if (member.MinVersion > 0)
            {
                sbClass.Append("sinceVersion: ");
                sbClass.Append(member.MinVersion);
            }

            sbClass.AppendLine(")]");

            sbClass.Append(1, "public ");
            sbClass.Append(type);

            if (member.Nullable)
            {
                sbClass.Append('?');
            }

            sbClass.Append(" ");
            sbClass.Append(member.Name);
            sbClass.Append(" { get => ");
            sbClass.Append(lowerFirstCaseName);
            sbClass.Append("; set => ");
            sbClass.Append(lowerFirstCaseName);
            sbClass.AppendLine(" = value; }");
            sbClass.AppendLine();

            classBuilder.ExistingPropertySymbols.Add(member.Name, null); // null means it exists but no symbol is tracked
        }

        sbReadWrite.Append(sbRWIndent, $"rw.{rwName ?? type}(ref n.{lowerFirstCaseName}");

        if (!member.Nullable)
        {
            sbReadWrite.Append('!');
        }

        sbReadWrite.AppendLine(");");
    }
}