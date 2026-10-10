namespace eQuantic.UI.Server.Rendering;

/// <summary>
/// A server value as the browser reads it (<see cref="HydrationProjection"/>): a plain object that holds,
/// by name, each read its projection lists. A type of its own, so the wire writes it as the object it
/// is, while a dictionary value crosses as its pairs (#437).
/// </summary>
internal sealed class ProjectedMembers() : Dictionary<string, object?>(StringComparer.Ordinal);
