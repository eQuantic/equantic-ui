namespace eQuantic.UI.Code;

/// <summary>A mark in the gutter — a breakpoint, a git change, the statement a debugger stopped
/// on. The line is where it sits; pressing it is an event the app answers.</summary>
public sealed record CodeGutterMarker(int Line, CodeGutterKind Kind)
{
    public string? Tooltip { get; init; }
}
