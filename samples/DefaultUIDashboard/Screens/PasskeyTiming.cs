namespace eQuantic.Console;

/// <summary>
/// How long the passkey page's options stay good (#1017), shared by the relying party that issues each
/// challenge and the page that keeps the options it asked for, so the two agree without asking each
/// other. A page that used options older than their challenge would run a ceremony the server then
/// refuses, and a passkey it created would stay on the device with nothing recording it.
/// </summary>
public static class PasskeyTiming
{
    /// <summary>How long the person has to answer the prompt: the options' own <c>timeout</c>, after
    /// which the browser ends the ceremony as cancelled.</summary>
    public static readonly TimeSpan Prompt = TimeSpan.FromMinutes(1);

    /// <summary>How long a challenge stays good after the relying party issues it.</summary>
    public static readonly TimeSpan Challenge = TimeSpan.FromMinutes(5);

    /// <summary>The oldest options the page starts a ceremony with: one that starts with them answers
    /// before their challenge lapses, with a minute to spare for the round trip.</summary>
    public static readonly TimeSpan Usable = Challenge - Prompt - TimeSpan.FromMinutes(1);

    /// <summary>How often the page asks for new options while it sits idle, well inside
    /// <see cref="Usable"/>, so a press finds options it can use even when a renewal comes late.</summary>
    public static readonly TimeSpan Renewal = TimeSpan.FromMinutes(1);
}
