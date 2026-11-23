namespace eQuantic.UI.Primitives;

/// <summary>
/// What a component tells the DEVELOPER while the app runs in development: a mistake that draws and
/// runs, and shows only as a page that behaves wrong. A picker handed back no value after it reported
/// one (#1780) is the first: its field emptied on every redraw while the day stayed chosen underneath,
/// which React says aloud for a component that switches from controlled to uncontrolled, and nothing here
/// did.
/// <para>
/// The browser's development runtime, the one the server serves in Development and under hot reload,
/// writes each notice to the console. A production page loads no channel, so a notice is dropped where
/// it is said and costs nothing. A host or a test that wants them sets <see cref="Sink"/>.
/// </para>
/// </summary>
public static class DevelopmentNotices
{
    /// <summary>Where a notice goes, or null, and it is dropped. Set by whoever runs the app in development.</summary>
    public static Action<string>? Sink { get; set; }

    /// <summary>Hands <paramref name="notice"/> to the developer, where a channel is armed.</summary>
    public static void Say(string notice)
    {
        if (Sink is { } sink && !string.IsNullOrEmpty(notice)) sink(notice);
    }
}
