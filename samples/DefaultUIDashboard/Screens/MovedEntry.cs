namespace eQuantic.Console;

/// <summary>An entry this page moved (#1533): the folder it went to ("" for the top) and the sibling it lands after,
/// null when it lands first.</summary>
public sealed record MovedEntry(RepositoryEntry Entry, string Parent, string? After);
