using eQuantic.UI.Primitives;

namespace eQuantic.UI.Web;

/// <summary>
/// WHAT ONE COMPONENT'S PREFETCH LEFT BEHIND, and what is needed to tell that component apart from
/// a different one that lands on its key in a later round.
///
/// <para>
/// A discovery round constructs fresh components, so the values a prefetch loaded live on instances
/// the next round throws away — they have to be carried across. Carrying the WHOLE component was
/// the first shape of this and it was wrong in a way only a data-driven page shows: a page whose own
/// prefetch decides WHICH row it composes builds a different row next round, and writing every
/// captured field onto it handed the new row the old row's constructor argument. The page then drew
/// row <c>a</c> on a request that had already chosen row <c>b</c>.
/// </para>
///
/// <para>
/// So two things travel instead of one. <see cref="Fields"/> is what the component held after its
/// prefetch ran. <see cref="AsBuilt"/> is how it looked BEFORE, in the fields that can be compared
/// at all — if the next round's component does not match it, the key names a different component
/// and what loaded under it belongs to nobody: nothing is restored and the new one is asked for
/// its own data.
/// </para>
/// </summary>
/// <param name="Loaded">
/// The very instance that loaded. A page root is the SAME object every round, and it already holds
/// what it loaded — recognising it by reference is what stops it being mistaken for a replacement.
/// </param>
/// <param name="AsBuilt">
/// The comparable fields as the constructor left them, which is what a later round's component is
/// measured against. Only value types and strings are in it: anything else may have been filled IN
/// PLACE, and no comparison here tells that from a field nothing touched.
/// </param>
/// <param name="Fields">Everything the component holds — raw CLR values under raw field names.</param>
public sealed record LoadedComponentState(
    UiComponent Loaded,
    IReadOnlyDictionary<string, object?> AsBuilt,
    IReadOnlyDictionary<string, object?> Fields);
