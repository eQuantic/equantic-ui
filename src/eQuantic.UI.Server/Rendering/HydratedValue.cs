namespace eQuantic.UI.Server.Rendering;

/// <summary>
/// One value of a <see cref="HydrationContract"/>: the name the twin reads it by, how to read it off the
/// live component, what of it crosses (<c>null</c> for all of it), and the type the member is declared
/// as, which a projection's reads are bound against as C# binds them.
/// </summary>
internal sealed record HydratedValue(string Name, Func<object, object?> Read, string? Projection, Type Declared);
