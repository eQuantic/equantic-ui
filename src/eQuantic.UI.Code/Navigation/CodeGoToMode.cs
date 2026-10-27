namespace eQuantic.UI.Code;

/// <summary>What the go-to field goes to. A line and a symbol are chosen by the text's first character, as VS
/// Code's quick access reads it: <c>:</c> a line, <c>@</c> a symbol, anything else nothing yet. A list of places
/// is opened by asking for it (<see cref="CodeGoTo.Definition"/>, <see cref="CodeGoTo.References"/>), and its
/// text only filters it.</summary>
public enum CodeGoToMode : byte
{
    None = 0,
    Line = 1,
    Symbol = 2,
    /// <summary>The places a name is defined, declared, typed or implemented, as <see cref="CodeGoTo.DefinitionKind"/>
    /// says (#1084).</summary>
    Definitions = 3,
    /// <summary>The places a name is used (#1084).</summary>
    References = 4,
}
