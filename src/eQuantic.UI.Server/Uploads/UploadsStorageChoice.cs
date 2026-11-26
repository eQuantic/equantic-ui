namespace eQuantic.UI.Server;

/// <summary>Whether the app keeps its uploads in a storage of its own, so the folder's settings are not read.</summary>
internal sealed record UploadsStorageChoice(bool OwnStorage);
