namespace eQuantic.Console;

/// <summary>
/// Who is signed in, as an identity provider's client would hold it: a service the container hands a
/// page, registered as a class. Its <see cref="Authority"/> belongs to the server alone.
/// </summary>
public sealed class ConsoleIdentity
{
    /// <summary>The identity provider the server signs in against, which no page should show.</summary>
    public string Authority { get; init; } = "";

    public string DisplayName { get; init; } = "";
}
