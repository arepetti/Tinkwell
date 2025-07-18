# Wildcard Patterns

This document explains the basic syntax for wildcard patterns used in Tinkwell.

These patterns are "git-like" and are converted into regular expressions for matching. Special characters within the pattern are automatically escaped unless they are part of a character class (`[]`).

## Supported Syntax

| Pattern       | Meaning                                                   |
|---------------|-----------------------------------------------------------|
| `*`           | Matches any sequence of characters (including none)       |
| `?`           | Matches exactly one character                             |
| `[abc]`       | Matches a single character: `a`, `b`, or `c`              |
| `[^abc]`      | Matches any character **except** `a`, `b`, or `c`         |
| `[a-z]`       | Matches a single character in the given range             |


Unless otherwise noted pattern matching is always **case-insensitive** and matchin is always performed using a neutral culture (`en-US`):

*   The pattern `foo` will match `foo`, `FOO`, `Foo`.
*   The pattern `idea` will NOT match `İdea` (even on a Turkish locale).

Here's a breakdown of the supported wildcard characters:

*   `*`: Matches any sequence of characters, including an empty sequence.
    *   Example: `foo*bar` matches `foobar`, `foo_bar`, `foo123bar`.

*   `?`: Matches any single character.
    *   Example: `foo?bar` matches `fooabar`, `fooxbar`, but not `foobar` or `fooaabar`.

*   `[abc]`: Matches any one character listed within the brackets.
    *   Example: `foo[abc]bar` matches `fooabar`, `foobbar`, `foocbar`.

*   `[^abc]`: Matches any one character *not* listed within the brackets.
    *   Example: `foo[^abc]bar` matches `foodbar`, `fooxbar`, but not `fooabar`.

*   `[a-z]`: Matches a single character within the specified range.
    *   Example: `foo[0-9]bar` matches `foo1bar`, `foo5bar`, but not `fooabar`.

### Important Notes on Character Classes (`[]`):

*   Nested character classes (e.g., `[[abc]]`) are not allowed.
*   Only alphanumeric characters, `^` (at the beginning for negation), and `-` (for ranges) are allowed inside character classes. Other special characters within `[]` will result in an error.

## Anchoring Behavior

By default, when converting a wildcard pattern to a regular expression, the resulting regular expression is anchored to match the entire string. This means:

*   The pattern `foo*` will match `foo`, `foobar`, `foobaz`, but not `afoobar`.
*   The pattern `*bar` will match `bar`, `foobar`, `bazbar`, but not `foobazbarb`.

