using System.Text;

namespace GBX.NET.Generators.Extensions;

internal static class StringBuilderExtensions
{
    public static void AppendIndent(this StringBuilder builder, int indent = 1, char indentChar = '\t')
    {
        for (var i = 0; i < indent; i++)
        {
            builder.Append(indentChar);
        }
    }

    public static void AppendLine(this StringBuilder builder, int indent, string value, char indentChar = '\t')
    {
        builder.AppendIndent(indent, indentChar);
        builder.AppendLine(value);
    }

    public static void Append(this StringBuilder builder, int indent, char value, char indentChar = '\t')
    {
        builder.AppendIndent(indent, indentChar);
        builder.Append(value);
    }

    public static void Append(this StringBuilder builder, int indent, string value, char indentChar = '\t')
    {
        builder.AppendIndent(indent, indentChar);
        builder.Append(value);
    }
}
