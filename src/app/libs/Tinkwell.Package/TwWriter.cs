using System.Globalization;
using System.Text;

namespace Tinkwell.Package;

/// <summary>
/// Generates tw format text from blocks.
/// </summary>
internal static class TwWriter
{
    /// <summary>Serializes <paramref name="block"/> to canonical tw-format text.</summary>
    /// <param name="block">Block to serialize, including its properties and children.</param>
    public static string Write(TwBlock block)
    {
        var sb = new StringBuilder();
        WriteBlock(sb, block, 0);
        return sb.ToString();
    }

    private static void WriteBlock(StringBuilder sb, TwBlock block, int indent)
    {
        var pad = new string(' ', indent);
        sb.Append(pad);
        sb.Append(block.Type);
        sb.Append(' ');
        sb.Append(QuoteIfNeeded(block.Name));
        sb.AppendLine(" {");

        var innerPad = new string(' ', indent + 2);

        foreach (var (key, value) in block.Properties)
        {
            sb.Append(innerPad);
            sb.Append(key);
            sb.Append(" = ");
            sb.AppendLine(FormatValue(value));
        }

        foreach (var child in block.Children)
        {
            if (block.Properties.Count > 0 || block.Children[0] != child)
                sb.AppendLine();
            WriteBlock(sb, child, indent + 2);
        }

        sb.Append(pad);
        sb.AppendLine("}");
    }

    private static string FormatValue(string value)
    {
        if (long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out _))
            return value;
        if (double.TryParse(value, System.Globalization.CultureInfo.InvariantCulture, out _))
            return value;
        if (value is "true" or "false")
            return value;

        return Quote(value);
    }

    private static string QuoteIfNeeded(string value)
    {
        if (value.Length == 0)
            return "\"\"";

        foreach (var ch in value)
        {
            if (!char.IsLetterOrDigit(ch) && ch != '-' && ch != '_')
                return Quote(value);
        }
        return value;
    }

    private static string Quote(string value) =>
        $"\"{value.Replace("\\", "\\\\").Replace("\"", "\\\"")}\"";
}
