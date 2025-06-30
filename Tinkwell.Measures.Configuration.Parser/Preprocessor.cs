using System.Text;

namespace Tinkwell.Measures.Configuration.Parser;

static class Preprocessor
{
    public static string Transform(string content)
        => FlattenMultilines(content);

    private static string FlattenMultilines(string input)
    {
        var result = new List<string>();
        var buffer = new StringBuilder();

        using var reader = new StringReader(input);
        string? line;

        while ((line = reader.ReadLine()) != null)
        {
            line = line.Trim();
            if (line.EndsWith('\\') && !(buffer.Length == 0 && line.StartsWith("//")))
            {
                buffer.Append(line.TrimEnd('\\').Trim() + " ");
                continue;
            }
            else
            {
                if (buffer.Length > 0)
                {
                    buffer.Append(line);
                    result.Add(buffer.ToString());
                    buffer.Clear();
                }
                else
                {
                    result.Add(line);
                }
            }
        }

        if (buffer.Length > 0)
            result.Add(buffer.ToString());

        return string.Join("\n", result);
    }
}