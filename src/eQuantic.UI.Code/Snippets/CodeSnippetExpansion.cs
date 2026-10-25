using System.Text;

namespace eQuantic.UI.Code;

/// <summary>
/// One copy of a snippet as it is inserted at a caret (#1013): its text, and every place each of its stops is
/// written, relative to the copy's start, with where the copy ends.
/// <para>
/// Each stop's text is its first placeholder's or choice's, and every other place it is written holds that
/// placeholder, its nested stops included, as VS Code fills a snippet in. A variable is what <c>variable</c> answers for it, its default when
/// that is empty, and an unknown one (<c>variable</c> answers null) is a placeholder holding its default or
/// its name, at a stop after the snippet's own. Every line after the first starts with
/// <c>indent</c>, the indentation of the line the copy is inserted on, and a tab at the start of one of those
/// lines is <c>step</c>, the language's indent step.
/// </para>
/// </summary>
internal sealed class CodeSnippetExpansion
{
    private readonly Func<string, string?> _variable;
    private readonly string _indent;
    private readonly string _step;
    private readonly StringBuilder _text = new();
    private readonly List<CodeSnippetPlace> _places = [];
    private readonly Dictionary<int, string> _defaults = new();
    private readonly Dictionary<int, CodeSnippetPart> _defining = new();
    private readonly Dictionary<string, int> _unknown = new();
    // The stops whose text is being read, and whose placeholder is being written: a stop that reaches itself
    // stops there.
    private readonly List<int> _resolving = [];
    private readonly List<int> _writing = [];
    private int _line;
    private int _column;
    private bool _lineStart;

    private CodeSnippetExpansion(Func<string, string?> variable, string indent, string step)
    {
        _variable = variable;
        _indent = indent;
        _step = step;
    }

    /// <summary>The copy's text.</summary>
    public string Text => _text.ToString();

    /// <summary>Every place a stop is written, in the order written, the final stop's among them.</summary>
    public IReadOnlyList<CodeSnippetPlace> Places => _places;

    /// <summary>Where the copy ends, relative to its start.</summary>
    public CodePosition End => new(_line, _column);

    public static CodeSnippetExpansion Of(CodeSnippet snippet, Func<string, string?> variable, string indent, string step)
    {
        var expansion = new CodeSnippetExpansion(variable, indent, step);
        expansion.Number(snippet.Parts, 1 + MaxStop(snippet.Parts));
        expansion.Define(snippet.Parts);
        expansion.Write(snippet.Parts, -1);
        return expansion;
    }

    private static int MaxStop(IReadOnlyList<CodeSnippetPart> parts)
    {
        var max = 0;
        foreach (var part in parts)
        {
            if (part.IsStop && part.Index > max) max = part.Index;
            if (part.Children is { } children)
            {
                var inner = MaxStop(children);
                if (inner > max) max = inner;
            }
        }
        return max;
    }

    /// <summary>Gives each unknown variable a stop after the snippet's own, one a name, in the order first
    /// written.</summary>
    private int Number(IReadOnlyList<CodeSnippetPart> parts, int next)
    {
        foreach (var part in parts)
        {
            if (part.Variable && _variable(part.Text) is null && !_unknown.ContainsKey(part.Text))
                _unknown[part.Text] = next++;
            if (part.Children is { } children) next = Number(children, next);
        }
        return next;
    }

    /// <summary>The part that gives each stop its text: the first placeholder, choice or unknown variable written
    /// for it, in the order written. Its text is read later, once every stop's part is known
    /// (<see cref="DefaultOf"/>).</summary>
    private void Define(IReadOnlyList<CodeSnippetPart> parts)
    {
        foreach (var part in parts)
        {
            var index = IndexOf(part);
            if (index >= 0 && !_defining.ContainsKey(index)
                && (part.Children is not null || part.Choices is not null || part.Variable))
                _defining[index] = part;
            if (part.Children is { } children) Define(children);
        }
    }

    /// <summary>The stop a part is, a variable nobody knows included, or -1.</summary>
    private int IndexOf(CodeSnippetPart part) =>
        part.IsStop ? part.Index
        : part.Variable && _unknown.TryGetValue(part.Text, out var index) ? index
        : -1;

    /// <summary>
    /// The text stop <paramref name="index"/> holds wherever it is written: its defining part's, read once every
    /// stop's part is known, so <c>${1:$2} ${2:x} $1</c> gives the mirror <c>x</c>, where read in the order
    /// written it gave it nothing (Copilot on #1079). A stop whose text reaches itself holds what it holds
    /// before it, and one with no defining part nothing.
    /// </summary>
    private string DefaultOf(int index)
    {
        if (_defaults.TryGetValue(index, out var known)) return known;
        if (!_defining.TryGetValue(index, out var defining) || _resolving.Contains(index)) return "";
        _resolving.Add(index);
        var text = defining.Choices is { Count: > 0 } choices ? choices[0]
            : defining.Children is { } children ? Plain(children)
            : defining.Text;
        _resolving.Remove(index);
        _defaults[index] = text;
        return text;
    }

    /// <summary>What <paramref name="parts"/> insert, before any indentation: what <see cref="Write"/> writes.</summary>
    private string Plain(IReadOnlyList<CodeSnippetPart> parts)
    {
        var text = new StringBuilder();
        foreach (var part in parts)
        {
            if (!part.IsStop && !part.Variable)
            {
                text.Append(part.Text);
                continue;
            }
            var index = IndexOf(part);
            if (index >= 0) text.Append(DefaultOf(index));
            else if (VariableText(part, out var nested) is { } value) text.Append(value);
            else if (nested is not null) text.Append(Plain(nested));
        }
        return text.ToString();
    }

    /// <summary>A known variable's value, or null with <paramref name="nested"/> its default, written as parts
    /// when the value is empty.</summary>
    private string? VariableText(CodeSnippetPart part, out IReadOnlyList<CodeSnippetPart>? nested)
    {
        nested = null;
        var value = _variable(part.Text) ?? "";
        if (value.Length > 0) return value;
        nested = part.Children;
        return null;
    }

    /// <summary>
    /// Writes <paramref name="parts"/>, every stop's place recorded under the placeholder it is nested in. Every
    /// place a stop is written holds its defining placeholder's parts, its nested stops among them, as VS Code
    /// clones a placeholder into its mirrors: a mirror of <c>${1:new ${2:Thing}()}</c> holds stop 2 too, and
    /// typing at stop 2 types in both (Copilot on #1079). A placeholder already being written inside itself is
    /// written as its text.
    /// </summary>
    private void Write(IReadOnlyList<CodeSnippetPart> parts, int parent)
    {
        foreach (var part in parts)
        {
            if (!part.IsStop && !part.Variable)
            {
                Append(part.Text);
                continue;
            }
            var index = IndexOf(part);
            if (index < 0)
            {
                if (VariableText(part, out var nested) is { } value) Append(value);
                else if (nested is not null) Write(nested, parent);
                continue;
            }
            var start = End;
            if (_defining.TryGetValue(index, out var defining) && defining.Choices is null
                && defining.Children is { } children && !_writing.Contains(index))
            {
                _writing.Add(index);
                Write(children, index);
                _writing.Remove(index);
            }
            else Append(DefaultOf(index));
            _places.Add(new CodeSnippetPlace(index, start, End, parent, part.Choices));
        }
    }

    /// <summary>Writes <paramref name="text"/>: a line break, of any kind, then the line's indentation, and a
    /// tab at the start of a line after the first the indent step.</summary>
    private void Append(string text)
    {
        for (var i = 0; i < text.Length; i++)
        {
            var c = text[i];
            if (c == '\r' || c == '\n')
            {
                if (c == '\r' && i + 1 < text.Length && text[i + 1] == '\n') i++;
                _text.Append('\n');
                _text.Append(_indent);
                _line++;
                _column = _indent.Length;
                _lineStart = true;
                continue;
            }
            if (_lineStart && c == '\t')
            {
                _text.Append(_step);
                _column += _step.Length;
                continue;
            }
            _lineStart = false;
            _text.Append(c);
            _column++;
        }
    }
}
