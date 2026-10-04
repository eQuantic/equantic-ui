using System.Text.Encodings.Web;
using System.Text.Json;

namespace eQuantic.UI.Codegen;

/// <summary>
/// JSON, written to be READ: the generated file sits in a repository next to files people wrote,
/// and a single-line blob is a file nobody reviews. Serializers exist; none of them indent the way a
/// hand-written neighbour does, and none let a caller lay a document out in the order that makes
/// sense to a reader.
/// <para>
/// The comma is the one thing a JSON writer must not get wrong, so the caller never writes one. A
/// member's line is left OPEN until we know whether another follows it — which is the only way the
/// last member is reliably the one without a comma.
/// </para>
/// <para>
/// The escape is the other, and it is System.Text.Json's, not a list of replacements: every JSON
/// file the build lays out goes through this writer, a source map, a web manifest and an asset
/// catalog among them. Two hand-kept lists escaped the backslash, the quote and a line break or
/// three, and wrote every other control raw, which JSON refuses (#525): a form feed in a C# comment
/// made its source map a file the bundler and the browser dropped in silence.
/// </para>
/// </summary>
public sealed class JsonWriter : CodeWriter
{
    /// <summary>
    /// What a string is escaped with. Relaxed, because nothing this writes is placed inside markup:
    /// the default encoder would spell every <c>&lt;</c> of a generic argument and every accented
    /// letter of an app's name as a six-character escape. Relaxed still escapes what JSON refuses,
    /// every control among them, and writes an unpaired surrogate as U+FFFD.
    /// </summary>
    private static readonly JavaScriptEncoder Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping;

    private readonly Stack<bool> _hasMembers = new();
    private bool _lineOpen;

    /// <summary>A document whose root is an object — every catalog and manifest we generate.</summary>
    public static string Document(Action<JsonWriter> body)
    {
        var writer = new JsonWriter();
        writer.Open("{");
        writer.Members(body);
        writer.Close("}");
        return writer.ToString();
    }

    public JsonWriter String(string name, string value) => Member(name, Quote(value));

    public JsonWriter Number(string name, long value) => Member(name, value.ToString(System.Globalization.CultureInfo.InvariantCulture));

    /// <summary>An array of strings, one to a line, a null among them written as <c>null</c>: a source
    /// map's sources, their contents and its names. An empty one is <c>[]</c>.</summary>
    public JsonWriter Strings(string name, IEnumerable<string?> values)
    {
        var items = values.ToList();
        StartMember(name);
        if (items.Count == 0)
        {
            Append("[]");
            return this;
        }
        Append("[");
        Members(array =>
        {
            foreach (var item in items) array.Value(item is null ? "null" : Quote(item));
        });
        Close("]");
        return this;
    }

    /// <summary>A named object member.</summary>
    public JsonWriter Object(string name, Action<JsonWriter> body)
    {
        StartMember(name);
        Append("{");
        Members(body);
        Close("}");
        return this;
    }

    /// <summary>An array of objects — the shape of every asset catalog's <c>images</c>.</summary>
    public JsonWriter Array(string name, Action<JsonWriter> body)
    {
        StartMember(name);
        Append("[");
        Members(body);
        Close("]");
        return this;
    }

    /// <summary>One object inside the array we are in.</summary>
    public JsonWriter Element(Action<JsonWriter> body)
    {
        StartMember(null);
        Append("{");
        Members(body);
        Close("}");
        return this;
    }

    private JsonWriter Member(string name, string literal)
    {
        StartMember(name);
        Append(literal);
        return this;
    }

    /// <summary>One value inside the array we are in, already written as JSON.</summary>
    private void Value(string literal)
    {
        StartMember(null);
        Append(literal);
    }

    /// <summary>Closes the previous member's line — with a comma, because another one follows.</summary>
    private void StartMember(string? name)
    {
        if (_lineOpen) Append(",").EndLine();
        _lineOpen = false;
        if (_hasMembers.Count > 0)
        {
            _hasMembers.Pop();
            _hasMembers.Push(true);
        }
        StartLine();
        if (name is not null) Append($"{Quote(name)} : ");
        _lineOpen = true;
    }

    private void Open(string opener)
    {
        StartLine();
        Append(opener);
        _lineOpen = true;
    }

    private void Members(Action<JsonWriter> body)
    {
        EndLine();
        _lineOpen = false;
        _hasMembers.Push(false);
        IndentLevel++;
        body(this);
        IndentLevel--;
    }

    /// <summary>Closes the LAST member's line without a comma, then the bracket.</summary>
    private void Close(string closer)
    {
        if (_lineOpen) EndLine();
        _lineOpen = false;
        _hasMembers.Pop();
        StartLine();
        Append(closer);
        _lineOpen = true;
        if (IndentLevel == 0) { EndLine(); _lineOpen = false; }
    }

    /// <summary>A string as a JSON string literal, its quotes included, escaped by System.Text.Json.</summary>
    private static string Quote(string value) => $"\"{JsonEncodedText.Encode(value, Encoder).Value}\"";
}
