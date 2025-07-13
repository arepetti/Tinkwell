using System.Text.RegularExpressions;
using System.Text;

namespace Tinkwell.Bootstrapper.Expressions;

/// <summary>
/// Helper class for converting wildcard patterns to regular expressions.
/// </summary>
public static class TextHelpers
{
    /// <summary>
    /// Converts a simple wildcard pattern to an equivalent regular expression string.
    /// </summary>
    /// <param name="pattern">
    /// The wildcard pattern to convert. Supports the following syntax:
    /// <list type="bullet">
    ///   <item><description><c>*</c>: matches any sequence of characters (including none).</description></item>
    ///   <item><description><c>?</c>: matches any single character.</description></item>
    ///   <item><description><c>[abc]</c>: matches any one character listed (e.g., 'a', 'b', or 'c').</description></item>
    ///   <item><description><c>[^abc]</c>: matches any one character not listed.</description></item>
    ///   <item><description><c>[a-z]</c>: matches a single character in the specified range.</description></item>
    /// </list>
    /// Special characters are escaped automatically unless part of a character class.
    /// </param>
    /// <returns>
    /// A regular expression string; it's anchored with <c>^</c> and <c>$</c> for full-string matching
    /// if <paramref name="fullString"/> is <c>true</c>.
    /// </returns>
    /// <remarks>
    /// Note that this is "git-like" and we do not support anchoring (it's fixed
    /// if <paramref name="fullString"/> is <c>true</c>) and recursive matching.
    /// </remarks>
    /// <exception cref="ArgumentException">
    /// Thrown if the pattern contains malformed or unsupported bracket expressions,
    /// such as unmatched <c>[</c> or non-alphanumeric characters (except <c>^</c> and <c>-</c>) inside brackets.
    /// </exception>
    public static string GitLikeWildcardToRegex(string pattern, bool fullString = true)
    {
        var regex = new StringBuilder(fullString ? "^" : "");
        bool inBracket = false;
        bool firstInBracket = false;

        foreach (char c in pattern)
        {
            if (c == '[')
            {
                if (inBracket)
                    throw new ArgumentException("Nested '[' not allowed.");

                inBracket = true;
                firstInBracket = true;
                regex.Append(c);
            }
            else if (c == ']')
            {
                if (!inBracket)
                    throw new ArgumentException("Unmatched ']'.");

                inBracket = false;
                regex.Append(c);
            }
            else if (inBracket)
            {
                if (firstInBracket)
                {
                    if (c != '^' && !char.IsLetterOrDigit(c) && c != '-')
                        throw new ArgumentException($"Invalid character '{c}' inside character class.");

                    regex.Append(c);
                    firstInBracket = false;
                }
                else
                {
                    if (!char.IsLetterOrDigit(c) && c != '-')
                        throw new ArgumentException($"Invalid character '{c}' inside character class.");

                    regex.Append(c);
                }
            }
            else
            {
                switch (c)
                {
                    case '*':
                        regex.Append(".*");
                        break;
                    case '?':
                        regex.Append('.');
                        break;
                    default:
                        regex.Append(Regex.Escape(c.ToString()));
                        break;
                }
            }
        }

        if (inBracket)
            throw new ArgumentException("Unclosed '[' in pattern.");

        if (fullString)
            regex.Append('$');

        return regex.ToString();
    }

    /// <summary>
    /// Converts a DOS-style wildcard pattern into a regular expression string.
    /// </summary>
    /// <param name="pattern">
    /// A pattern string using DOS-style wildcards:
    /// <list type="bullet">
    ///   <item><description><c>*</c>: matches any sequence of characters (including none).</description></item>
    ///   <item><description><c>?</c>: matches any single character.</description></item>
    /// </list>
    /// All other characters are treated literally; regular expression metacharacters are escaped automatically.
    /// </param>
    /// <returns>
    /// A regular expression string that matches the input pattern exactly, anchored with <c>^</c> and <c>$</c>.
    /// </returns>
    public static string DosWildcardToRegex(string pattern)
        => "^" + Regex.Escape(pattern).Replace("\\*", ".*").Replace("\\?", ".") + "$";

    /// <summary>
    /// Helper function to convert git-like wildcard patterns to regular expressions.
    /// </summary>
    /// <param name="pattern">
    /// Pattern to convert to a regular expression.
    /// See <see cref="GitLikeWildcardToRegex(string, bool)"/> for all the details.
    /// </param>
    /// <returns>
    /// A regular expression that matches the specified pattern. It's compiled,
    /// <strong>case-insensitive</strong> and performs <strong>invariant culture matching</strong>.
    /// Use <c>GitLikeWilcardToRegex()</c> if you need control over the regex options.
    /// </returns>
    public static Regex PatternToRegex(string pattern)
        => new Regex(GitLikeWildcardToRegex(pattern), RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
}