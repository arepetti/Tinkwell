namespace Tinkwell.Package;

/// <summary>
/// Minimal tw format parser for package files. Handles only the subset
/// needed by <c>package.tw</c> and <c>signatures.tw</c>: blocks with
/// type + name, key=value properties, nested blocks, and comments.
/// </summary>
internal sealed class TwReader
{
    internal const int MaxIdentifierLength = 512;
    internal const int MaxValueLength = 512 * 1024;
    internal const int MaxPropertiesPerBlock = 10_000;
    internal const int MaxNestingDepth = 16;

    /// <summary>Parses <paramref name="text"/> as a single tw block. Throws if the input is empty or has trailing content.</summary>
    /// <param name="text">Full tw-format text containing exactly one top-level block.</param>
    public static TwBlock ReadSingleBlock(string text)
    {
        var reader = new TwReader(text);
        reader.SkipWhitespaceAndComments();

        if (reader.IsAtEnd)
            throw new PackageException("Empty tw file");

        var block = reader.ReadBlock(0);
        reader.SkipWhitespaceAndComments();

        if (!reader.IsAtEnd)
            throw new PackageException("Unexpected content after block");

        return block;
    }

    private readonly string _text;
    private int _pos;

    private TwReader(string text)
    {
        _text = text;
        _pos = 0;
    }

    private bool IsAtEnd => _pos >= _text.Length;
    private char Current => _text[_pos];

    private TwBlock ReadBlock(int depth)
    {
        if (depth > MaxNestingDepth)
            throw new PackageException(
                $"Maximum nesting depth ({MaxNestingDepth}) exceeded at position {_pos}");

        var type = ReadIdentifier();
        SkipWhitespaceAndComments();
        var name = ReadValue();
        SkipWhitespaceAndComments();
        Expect('{');
        SkipWhitespaceAndComments();

        var properties = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var children = new List<TwBlock>();

        while (!IsAtEnd && Current != '}')
        {
            if (properties.Count + children.Count >= MaxPropertiesPerBlock)
                throw new PackageException(
                    $"Block '{type}' exceeds maximum number of entries ({MaxPropertiesPerBlock})");

            var ident = ReadIdentifier();
            SkipWhitespaceAndComments();
            int posAfterKey = _pos;

            if (!IsAtEnd && Current == '=')
            {
                _pos++; // skip =
                SkipWhitespaceAndComments();
                var value = ReadValue();
                properties[ident] = value;
            }
            else if (!IsAtEnd && (Current == '"' || Current == '{' || char.IsLetterOrDigit(Current)))
            {
                var childName = (Current == '{') ? "" : ReadValue();
                SkipWhitespaceAndComments();
                Expect('{');
                SkipWhitespaceAndComments();

                var childProps = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                while (!IsAtEnd && Current != '}')
                {
                    if (childProps.Count >= MaxPropertiesPerBlock)
                        throw new PackageException(
                            $"Child block '{ident}' exceeds maximum number of entries ({MaxPropertiesPerBlock})");

                    var key = ReadIdentifier();
                    SkipWhitespaceAndComments();
                    Expect('=');
                    SkipWhitespaceAndComments();
                    var val = ReadValue();
                    childProps[key] = val;
                    SkipWhitespaceAndComments();
                }
                Expect('}');

                children.Add(new TwBlock(ident, childName, childProps, []));
            }

            SkipWhitespaceAndComments();
            if (_pos == posAfterKey)
                throw new PackageException(
                    $"Unexpected content after identifier '{ident}' at position {posAfterKey}");
        }

        Expect('}');
        return new TwBlock(type, name, properties, children);
    }

    private string ReadIdentifier()
    {
        int start = _pos;
        while (!IsAtEnd && (char.IsLetterOrDigit(Current) || Current == '-' || Current == '_'))
            _pos++;

        if (_pos == start)
            throw new PackageException($"Expected identifier at position {_pos}");

        int length = _pos - start;
        if (length > MaxIdentifierLength)
            throw new PackageException(
                $"Identifier at position {start} exceeds maximum length ({MaxIdentifierLength})");

        return _text[start.._pos];
    }

    private string ReadValue()
    {
        if (!IsAtEnd && Current == '"')
            return ReadQuotedString();

        return ReadBareValue();
    }

    private string ReadQuotedString()
    {
        int startPos = _pos;
        Expect('"');
        var sb = new System.Text.StringBuilder();
        while (!IsAtEnd && Current != '"')
        {
            if (sb.Length >= MaxValueLength)
                throw new PackageException(
                    $"Quoted string at position {startPos} exceeds maximum length ({MaxValueLength})");

            if (Current == '\\')
            {
                _pos++;
                if (IsAtEnd)
                    throw new PackageException("Unterminated escape sequence");
                sb.Append(Current switch
                {
                    'n' => '\n',
                    't' => '\t',
                    'r' => '\r',
                    '\\' => '\\',
                    '"' => '"',
                    _ => Current,
                });
                _pos++;
            }
            else
            {
                sb.Append(Current);
                _pos++;
            }
        }
        Expect('"');
        return sb.ToString();
    }

    private string ReadBareValue()
    {
        int start = _pos;
        while (!IsAtEnd && !char.IsWhiteSpace(Current)
            && Current != '}' && Current != '{' && Current != '=' && Current != '#')
            _pos++;

        if (_pos == start)
            throw new PackageException($"Expected value at position {_pos}");

        int length = _pos - start;
        if (length > MaxValueLength)
            throw new PackageException(
                $"Value at position {start} exceeds maximum length ({MaxValueLength})");

        return _text[start.._pos];
    }

    private void SkipWhitespaceAndComments()
    {
        while (!IsAtEnd)
        {
            if (char.IsWhiteSpace(Current))
            {
                _pos++;
                continue;
            }

            if (Current == '#')
            {
                SkipToEndOfLine();
                continue;
            }

            if (_pos + 1 < _text.Length && Current == '/' && _text[_pos + 1] == '/')
            {
                SkipToEndOfLine();
                continue;
            }

            break;
        }
    }

    private void SkipToEndOfLine()
    {
        while (!IsAtEnd && Current != '\n')
            _pos++;
    }

    private void Expect(char ch)
    {
        if (IsAtEnd || Current != ch)
            throw new PackageException($"Expected '{ch}' at position {_pos}");
        _pos++;
    }
}

/// <summary>
/// A parsed tw block with type, name, string properties, and child blocks.
/// </summary>
internal sealed record TwBlock(
    string Type,
    string Name,
    IReadOnlyDictionary<string, string> Properties,
    IReadOnlyList<TwBlock> Children);
