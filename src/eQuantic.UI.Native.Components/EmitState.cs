using eQuantic.UI.Native.Engine;
using eQuantic.UI.Native.Framework;
using eQuantic.UI.Primitives;

namespace eQuantic.UI.Native.Components;

/// <summary>
/// Everything the paint walk carries. ALL of it, which is the point: <see cref="EmitVisitor"/> holds
/// no state at all, so one instance serves every frame and every layer and the walk allocates
/// nothing to dispatch.
///
/// <para>
/// The first arrangement gave the visitor six fields and this struct three. It cost ONE object per
/// frame — 64 bytes — and <c>PerfHarnessTests</c> refused it at 75,778 against a 75,776 ceiling, by
/// two bytes. The budget was right to refuse: a visitor with fields is a visitor with identity, and
/// this one has none. Moving the six here made it a singleton and the cost zero rather than small.
/// </para>
///
/// <para>
/// A struct, so a node's visit costs a copy on the stack and nothing on the heap. It is passed by
/// value because <c>IVisualNodeVisitor</c> takes its state that way; a door that changes something
/// for its children spells the change (<c>s with { Node = child, Input = scrolled }</c>) instead of
/// mutating what its siblings will read.
/// </para>
///
/// <para>
/// <see cref="Press"/> is the exception that proves it: a mutable scope, ONE PER LAYER, pushed and
/// restored down a subtree. <see cref="PhotonRealizer"/> builds a fresh one for the page and for
/// each overlay, so a leftover cannot cross between them.
/// </para>
/// </summary>
internal readonly record struct EmitState(
    LayoutNode Node,
    InputSink Input,
    PressScope Press,
    IAppTheme Theme,
    ThemeMode Mode,
    DisplayListBuilder Builder,
    Dictionary<ScrollView, (string Path, float MaxOffset)> ScrollMeta,
    MotionScope Motion,
    List<Overlay> Overlays);
