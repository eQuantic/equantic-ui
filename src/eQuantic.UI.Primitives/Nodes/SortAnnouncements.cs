namespace eQuantic.UI.Primitives;

/// <summary>
/// What a reader hears while an item of a <see cref="Sortable"/> is sorted by the keyboard (#1564): how to do it,
/// and each step, as data on the node, so every realizer says the same words and an app can give its own.
/// <c>UI.Sortable</c> fills it with the SDK's words in the reader's culture when the app gives none.
/// <para>
/// Each step's text has numbered holes, as .NET's composite format numbers them, which
/// <see cref="SortWords.Fill"/> fills on every target: <c>{0}</c> is the item's name; for a place, <c>{1}</c> is
/// its position from one, <c>{2}</c> the count and <c>{3}</c> the list's name; for a place inside another item,
/// <c>{1}</c> is that item's name. A text may leave any hole out.
/// </para>
/// </summary>
/// <param name="Instructions">How to sort the focused item, read after its name: it has no holes.</param>
/// <param name="PickedUp">The item was picked up: name, position, count, list.</param>
/// <param name="Moved">The item stands at a new place: name, position, count, list.</param>
/// <param name="MovedInto">The item stands inside another, in a list that nests: name, the other's name.</param>
/// <param name="Refused">The list refused the place the item was carried to: name.</param>
/// <param name="Dropped">The item was dropped at a place: name, position, count, list.</param>
/// <param name="DroppedInto">The item was dropped inside another: name, the other's name.</param>
/// <param name="PutBack">The carry was given up and the item is back where it was: name.</param>
public sealed record SortAnnouncements(
    string Instructions,
    string PickedUp,
    string Moved,
    string MovedInto,
    string Refused,
    string Dropped,
    string DroppedInto,
    string PutBack);
