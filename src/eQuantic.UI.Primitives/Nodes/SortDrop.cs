namespace eQuantic.UI.Primitives;

/// <summary>
/// Where a carried <see cref="SortableItem"/> lands (#1535): the item's id (<see cref="Item"/>), the
/// <see cref="Sortable"/> it left (<see cref="From"/>) and the one it lands in (<see cref="To"/>), by their
/// ids, and the position it takes there, counted in that list WITHOUT the item, so moving it is
/// "take it out, insert it at <see cref="Index"/>" in either list.
/// <para>
/// In a list that nests, <see cref="Into"/> names the item it lands INSIDE, and <see cref="Index"/> is that
/// item's position.
/// </para>
/// </summary>
public sealed record SortDrop(string Item, string From, string To, int Index)
{
    /// <summary>The id of the item the carried one lands inside, in a list that nests; null otherwise.</summary>
    public string? Into { get; init; }
}
