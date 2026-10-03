namespace eQuantic.UI.Server.Rendering;

/// <summary>
/// One value of a <see cref="HydrationContract"/>: the name the twin reads it by, how to read it off the
/// live component, and what of it crosses (<c>null</c> for all of it).
/// </summary>
internal sealed record HydratedValue(string Name, Func<object, object?> Read, string? Projection);
