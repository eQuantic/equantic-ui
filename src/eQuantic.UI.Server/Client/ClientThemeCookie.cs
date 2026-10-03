namespace eQuantic.UI.Server.Client;

/// <summary>The cookie the browser writes the reader's theme mode to, and how long it keeps it.</summary>
internal sealed record ClientThemeCookie(string Name, int Days);
