namespace eQuantic.UI.Testing;

/// <summary>How <see cref="HydrationAssert"/> draws a page: how long it waits, and how wide the window is.</summary>
public sealed class HydrationOptions
{
    /// <summary>How long the page has to load its scripts and hydrate. Thirty seconds unless said.</summary>
    public TimeSpan Timeout { get; init; } = TimeSpan.FromSeconds(30);

    /// <summary>The window's width in CSS pixels, which decides the arm of every adaptive node. 1280 unless said.</summary>
    public int Width { get; init; } = 1280;
}
