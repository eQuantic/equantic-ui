namespace eQuantic.UI.Server.Client;

/// <summary>The language-prefix policy: the default culture, which has no prefix, and the prefixed ones.</summary>
internal sealed record ClientCultureRoutes(string Default, IReadOnlyList<string> Prefixed);
