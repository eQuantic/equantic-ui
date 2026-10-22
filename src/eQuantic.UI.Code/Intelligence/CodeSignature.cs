namespace eQuantic.UI.Code;

/// <summary>
/// One signature of a call: its label as the panel shows it, its documentation, and its parameters, each by
/// its own label as it stands in this one. LSP's <c>SignatureInformation</c>.
/// </summary>
public sealed record CodeSignature(string Label)
{
    /// <summary>What the signature does, shown under its parameters.</summary>
    public string? Documentation { get; init; }

    /// <summary>The parameters, in the order they stand in <see cref="Label"/>.</summary>
    public IReadOnlyList<CodeSignatureParameter> Parameters { get; init; } = [];

    /// <summary>The argument the caret is on for this signature, over the answer's own
    /// (<see cref="CodeSignatureHelp.ActiveParameter"/>).</summary>
    public int? ActiveParameter { get; init; }

    /// <summary>
    /// Where the parameter at <paramref name="index"/> starts in <see cref="Label"/>, or -1 when its label
    /// is not there. Each is found in order, after the one before it and after the label's first opening
    /// parenthesis, and only as a whole word, so <c>int x</c> and <c>int xy</c> in <c>f(int x, int xy)</c>
    /// are each their own, and a parameter named like the function is not found in its name.
    /// </summary>
    public int ParameterStart(int index)
    {
        if (index < 0 || index >= Parameters.Count) return -1;
        var from = Label.IndexOf('(') + 1;
        for (var i = 0; i <= index; i++)
        {
            var label = Parameters[i].Label;
            var at = Find(label, from);
            if (i == index) return at;
            if (at >= 0) from = at + label.Length;
        }
        return -1;
    }

    /// <summary>The first place at or after <paramref name="from"/> where <paramref name="text"/> stands as
    /// a whole word, or -1.</summary>
    private int Find(string text, int from)
    {
        if (text.Length == 0) return -1;
        for (var at = Label.IndexOf(text, from, StringComparison.Ordinal); at >= 0;
             at = Label.IndexOf(text, at + 1, StringComparison.Ordinal))
        {
            var before = at == 0 || !CodeDocument.IsWordChar(Label[at - 1]);
            var end = at + text.Length;
            var after = end == Label.Length || !CodeDocument.IsWordChar(Label[end]);
            if (before && after) return at;
        }
        return -1;
    }
}
