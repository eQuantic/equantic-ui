namespace eQuantic.Console;

/// <summary>One entry of a folder of the sample repository, as the server lists it.</summary>
public sealed record RepositoryEntry(string Path, string Name, bool IsDirectory);
