using System.Text;

namespace GBX.NET.Generators.Extensions;

internal static class StringBuilderExtensions
{
    public static void AppendIndent(this StringBuilder builder, int indent = 1)
    {
        for (var i = 0; i < indent; i++)
        {
            builder.Append('\t');
        }
    }

    public static void AppendLine(this StringBuilder builder, int indent, string value)
    {
        builder.AppendIndent(indent);
        builder.AppendLine(value);
    }

    public static void Append(this StringBuilder builder, int indent, char value)
    {
        builder.AppendIndent(indent);
        builder.Append(value);
    }

    public static void Append(this StringBuilder builder, int indent, string value)
    {
        builder.AppendIndent(indent);
        builder.Append(value);
    }
}
