using eQuantic.UI.Primitives;

namespace eQuantic.Console;

/// <summary>One entry in the console's sidebar (<see cref="ConsoleLayout"/>).</summary>
public sealed record NavEntry(Icons Icon, string Label, string Href, int Badge = 0);
