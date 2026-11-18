namespace eQuantic.UI.Primitives;

/// <summary>
/// Where a <see cref="Listener"/> hears the pointer (#1562): Flutter's <c>HitTestBehavior</c>, by its names and
/// by its rule. A point is "on the child" where the child draws something (a text, an icon, an image, a vector,
/// a canvas, a box with a fill, a border or a shadow) or where something in it answers the pointer (a control,
/// a field, a scroll view, an <see cref="Opaque"/> listener). A layout's empty space, a column's gap or a
/// padding, is not.
/// <para>
/// It decides which LISTENERS hear a pointer, and never takes a press, a drag or a scroll from anything: a
/// listener only listens.
/// </para>
/// </summary>
public enum HitTestBehavior : byte
{
    /// <summary>Hears only where its child is hit, and is passed over elsewhere, so the listeners behind it
    /// hear what lands on its empty space. The default, as in Flutter.</summary>
    DeferToChild = 0,

    /// <summary>Hears anywhere in its box, its empty space included, and hides the listeners behind it.</summary>
    Opaque = 1,

    /// <summary>Hears anywhere in its box, and lets the listeners behind it hear what lands where its child is
    /// not hit.</summary>
    Translucent = 2,
}
