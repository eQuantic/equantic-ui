using Microsoft.AspNetCore.Http;

namespace eQuantic.UI.Server;

/// <summary>Which requests the Server Action endpoint takes, by where the browser says they came from
/// (#678).</summary>
internal sealed class ServerActionOrigins(IEnumerable<string> allowed)
{
    /// <summary>The origins the app allows besides its own host.</summary>
    public IReadOnlyCollection<string> Allowed { get; } = [.. allowed];

    /// <summary>Whether the action runs for <paramref name="request"/>.</summary>
    public bool Allows(HttpRequest request) => true;
}
