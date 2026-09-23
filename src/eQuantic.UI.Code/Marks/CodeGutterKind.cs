namespace eQuantic.UI.Code;

/// <summary>What the gutter shows beside a line, left of its number.</summary>
public enum CodeGutterKind : byte
{
    None = 0, Breakpoint = 1, BreakpointDisabled = 2, Error = 3, Warning = 4,
    Added = 5, Modified = 6, Removed = 7, CurrentStatement = 8,
}
