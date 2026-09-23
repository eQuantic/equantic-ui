namespace eQuantic.UI.Code;

/// <summary>How far one caret movement goes.</summary>
public enum CodeMotion : byte
{
    Character = 0, Word = 1, Line = 2, LineBoundary = 3, DocumentBoundary = 4, Page = 5,
}
