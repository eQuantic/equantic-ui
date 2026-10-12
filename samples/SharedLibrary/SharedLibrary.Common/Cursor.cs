namespace SharedLibrary.Common;

/// <summary>Where a reader is in a list: a record a page BUILDS in the browser, so two cursors at
/// one position are equal there as they are on the server.</summary>
public sealed record Cursor(int Position)
{
    public Cursor Next() => new(Position + 1);
}
