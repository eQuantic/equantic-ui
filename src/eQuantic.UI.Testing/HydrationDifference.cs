namespace eQuantic.UI.Testing;

/// <summary>
/// One place where the browser's tree said something other than the server's markup, which hydration
/// wrote over: a text, an attribute, or an element only the server wrote.
/// </summary>
/// <param name="Kind"><c>text</c>, <c>attribute</c> or <c>element</c>.</param>
/// <param name="Where">The element, as a path from the page's root (<c>div#app &gt; section &gt; p</c>).</param>
/// <param name="Name">The attribute's name; null for a text or an element.</param>
/// <param name="Server">What the server wrote (an element's tag), or null where it wrote nothing.</param>
/// <param name="Client">What the browser's tree says, or null where it says nothing (an empty text is "").</param>
public sealed record HydrationDifference(string Kind, string Where, string? Name, string? Server, string? Client)
{
    /// <inheritdoc />
    public override string ToString() => Kind switch
    {
        "text" => $"the text of {Where} was '{Server}' on the server and '{Client}' in the browser",
        "element" => $"{Where} holds a <{Server}> the server wrote and the browser does not",
        _ => $"the attribute {Name} of {Where} was {Quoted(Server)} on the server and {Quoted(Client)} in the browser",
    };

    private static string Quoted(string? value) => value is null ? "absent" : $"'{value}'";
}
