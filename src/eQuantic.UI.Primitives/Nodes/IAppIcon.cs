namespace eQuantic.UI.Primitives;

/// <summary>
/// The app's icon, written the way everything else is: a tree, in C#. The framework rasterizes it
/// at build time into whatever each platform's installer wants, so publishing an app never sends
/// its author to an image editor.
/// <para>
/// The tree is rendered into a SQUARE canvas that it should fill edge to edge — every platform
/// applies its own mask (iOS a superellipse, Android an adaptive shape), so rounding the corners
/// here only rounds them twice. Leave the background opaque for the same reason: a transparent icon
/// is composited against whatever the launcher happens to be showing.
/// </para>
/// <para>
/// It is built ONCE, with no state and no interaction — a press handler on an icon is a handler
/// nobody can reach. Keep it to shapes, gradients and at most a letter or two: at the size a home
/// screen actually shows, anything finer is a smudge.
/// </para>
/// </summary>
public interface IAppIcon
{
    VisualNode Build(ComponentContext context);
}
