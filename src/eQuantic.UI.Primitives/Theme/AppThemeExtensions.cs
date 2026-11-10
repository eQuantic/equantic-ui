namespace eQuantic.UI.Primitives;

/// <summary>What the SDK offers on any <see cref="IAppTheme"/>, the two it ships and an app's own alike.</summary>
public static class AppThemeExtensions
{
    /// <summary>
    /// The theme with a slot replaced, every other member forwarded, the defaults included: an argument
    /// left null keeps the slot the theme answers. A decorator written by hand forwards the members it
    /// declares and leaves the defaults to the interface, so one wrapped around a theme that fills its
    /// slots loses them; this one is the SDK's, and forwards them all (<c>docs/ELEMENT-TREE-PLAN.md</c>
    /// §3.10).
    /// <code>ui.UseTheme(PhotonTheme.Instance.With(button: new ButtonTheme { MinWidth = 88 }));</code>
    /// </summary>
    public static IAppTheme With(this IAppTheme theme, ControlsTheme? controls = null, ButtonTheme? button = null, SurfaceTheme? surfaces = null, MenuTheme? menu = null, NoticeTheme? notice = null, ProgressTheme? progress = null, CardTheme? card = null) =>
        new OverriddenTheme(theme, controls, button, surfaces, menu, notice, progress, card);
}
