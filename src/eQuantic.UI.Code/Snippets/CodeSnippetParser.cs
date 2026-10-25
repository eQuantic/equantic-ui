using System.Text;

namespace eQuantic.UI.Code;

/// <summary>
/// Reads the Language Server Protocol's snippet syntax into parts, as VS Code's <c>SnippetParser</c> reads it
/// (#1013). It never fails: a construct the grammar does not make is text from its <c>$</c> on, so
/// <c>${1</c> and a <c>$</c> alone are inserted as typed. A transformation (<c>${1/a/b/g}</c>) is read so the
/// text after it is right, and dropped: its stop or variable is inserted as it would be without one.
/// </summary>
internal sealed class CodeSnippetParser
{
    private readonly string _source;
    private int _at;

    public CodeSnippetParser(string source) => _source = source;

    /// <summary>The parts of the whole source.</summary>
    public List<CodeSnippetPart> Parse()
    {
        _at = 0;
        return Parts(inPlaceholder: false);
    }

    /// <summary>
    /// The parts from here to the end of the source, or, inside a placeholder or a variable's default, to the
    /// <c>}</c> that closes it, which is left for the caller. A backslash escapes <c>$</c>, <c>}</c> and
    /// <c>\</c>; before anything else it is a backslash.
    /// </summary>
    private List<CodeSnippetPart> Parts(bool inPlaceholder)
    {
        var parts = new List<CodeSnippetPart>();
        var text = new StringBuilder();
        while (_at < _source.Length)
        {
            var c = _source[_at];
            if (c == '\\' && _at + 1 < _source.Length && IsEscaped(_source[_at + 1]))
            {
                text.Append(_source[_at + 1]);
                _at += 2;
                continue;
            }
            if (inPlaceholder && c == '}') break;
            if (c == '$' && Construct() is { } part)
            {
                Flush(text, parts);
                parts.Add(part);
                continue;
            }
            text.Append(c);
            _at++;
        }
        Flush(text, parts);
        return parts;
    }

    private static bool IsEscaped(char c) => c == '$' || c == '}' || c == '\\';

    private static void Flush(StringBuilder text, List<CodeSnippetPart> parts)
    {
        if (text.Length == 0) return;
        parts.Add(new CodeSnippetPart(text.ToString(), -1, false, null, null));
        text.Clear();
    }

    /// <summary>The construct the <c>$</c> here starts, consumed, or null with nothing consumed.</summary>
    private CodeSnippetPart? Construct()
    {
        var start = _at;
        _at++;
        if (_at < _source.Length && IsDigit(_source[_at]))
            return Stop(Number(), null, null);
        if (_at < _source.Length && IsNameStart(_source[_at]))
            return Variable(Name(), null);
        if (Eat('{'))
        {
            if (_at < _source.Length && IsDigit(_source[_at]))
            {
                var index = Number();
                if (Eat('}')) return Stop(index, null, null);
                if (Eat(':'))
                {
                    var children = Parts(inPlaceholder: true);
                    if (Eat('}')) return Stop(index, children, null);
                }
                else if (Eat('|'))
                {
                    if (Choices() is { } options && Eat('}')) return Stop(index, null, options);
                }
                else if (Transform() && Eat('}')) return Stop(index, null, null);
            }
            else if (_at < _source.Length && IsNameStart(_source[_at]))
            {
                var name = Name();
                if (Eat('}')) return Variable(name, null);
                if (Eat(':'))
                {
                    var children = Parts(inPlaceholder: true);
                    if (Eat('}')) return Variable(name, children);
                }
                else if (Transform() && Eat('}')) return Variable(name, null);
            }
        }
        _at = start;
        return null;
    }

    private static CodeSnippetPart Stop(int index, IReadOnlyList<CodeSnippetPart>? children, IReadOnlyList<string>? choices) =>
        new("", index, false, children, choices);

    private static CodeSnippetPart Variable(string name, IReadOnlyList<CodeSnippetPart>? children) =>
        new(name, -1, true, children, null);

    /// <summary>
    /// A choice's options, after its <c>|</c> and through the <c>|</c> that ends them, or null when they do not
    /// end. A backslash escapes <c>,</c>, <c>|</c> and <c>\</c> there, as VS Code reads an option.
    /// </summary>
    private List<string>? Choices()
    {
        var options = new List<string>();
        var option = new StringBuilder();
        while (_at < _source.Length)
        {
            var c = _source[_at];
            if (c == '\\' && _at + 1 < _source.Length && (_source[_at + 1] == ',' || _source[_at + 1] == '|' || _source[_at + 1] == '\\'))
            {
                option.Append(_source[_at + 1]);
                _at += 2;
                continue;
            }
            _at++;
            if (c == ',' || c == '|')
            {
                options.Add(option.ToString());
                option.Clear();
                if (c == '|') return options;
                continue;
            }
            option.Append(c);
        }
        return null;
    }

    /// <summary>
    /// A transformation, from its first <c>/</c> through its options: the regular expression, the format, whose
    /// <c>${1:/upcase}</c> are read whole, and the flags. Whether one was read; it is dropped either way.
    /// </summary>
    private bool Transform()
    {
        if (!Eat('/')) return false;
        if (!Until('/', inFormat: false) || !Until('/', inFormat: true)) return false;
        while (_at < _source.Length && IsNameStart(_source[_at]) && _source[_at] != '_') _at++;
        return true;
    }

    /// <summary>Reads through the next unescaped <paramref name="end"/>, a format's <c>${…}</c> whole; whether
    /// there was one.</summary>
    private bool Until(char end, bool inFormat)
    {
        while (_at < _source.Length)
        {
            var c = _source[_at];
            if (c == '\\' && _at + 1 < _source.Length)
            {
                _at += 2;
                continue;
            }
            if (inFormat && c == '$' && _at + 1 < _source.Length && _source[_at + 1] == '{')
            {
                var close = _source.IndexOf('}', _at);
                if (close < 0) return false;
                _at = close + 1;
                continue;
            }
            _at++;
            if (c == end) return true;
        }
        return false;
    }

    private bool Eat(char c)
    {
        if (_at >= _source.Length || _source[_at] != c) return false;
        _at++;
        return true;
    }

    /// <summary>A stop's number. Its digits are ASCII's, as the protocol's grammar has them, and it stops growing
    /// long before an int would wrap, so a runaway number is one number on both targets.</summary>
    private int Number()
    {
        var value = 0;
        while (_at < _source.Length && IsDigit(_source[_at]))
        {
            if (value < 100_000_000) value = value * 10 + (_source[_at] - '0');
            _at++;
        }
        return value;
    }

    private static bool IsDigit(char c) => c >= '0' && c <= '9';

    private static bool IsNameStart(char c) => c == '_' || (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z');

    private string Name()
    {
        var start = _at;
        while (_at < _source.Length && (IsNameStart(_source[_at]) || IsDigit(_source[_at]))) _at++;
        return _source.Substring(start, _at - start);
    }
}
