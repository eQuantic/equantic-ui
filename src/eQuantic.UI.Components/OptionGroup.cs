namespace eQuantic.UI.Components;

/// <summary>One group of a <see cref="Select"/>'s options: the heading drawn over them, which every
/// reader announces as the group's name, and the options it holds. The Select numbers them with the
/// rest, in reading order, so an index counts across the groups.</summary>
public sealed record OptionGroup(string Label, IReadOnlyList<string> Options);
