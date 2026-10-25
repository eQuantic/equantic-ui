namespace eQuantic.UI.Code;

/// <summary>What the go-to field's text goes to, by its first character, as VS Code's quick access reads
/// it: <c>:</c> a line, <c>@</c> a symbol, anything else nothing yet.</summary>
public enum CodeGoToMode : byte
{
    None = 0,
    Line = 1,
    Symbol = 2,
}
