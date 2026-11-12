namespace eQuantic.UI.Primitives;

/// <summary>What the SDK offers on any <see cref="IAppTheme"/>, the two it ships and an app's own alike.</summary>
public static class AppThemeExtensions
{
    /// <summary>
    /// The theme with a slot laid over its own, member by member, and every other member forwarded, the
    /// defaults included. A member the given slot names wins, one it leaves null is the theme's, and a
    /// nested slot (a button's own ladder, a ladder's rungs) merges the same way, so overriding one value
    /// keeps every other the design system set. An argument left null keeps the slot the theme answers.
    /// A decorator written by hand forwards the members it declares and leaves the defaults to the
    /// interface, so one wrapped around a theme that fills its slots loses them; this one is the SDK's,
    /// and forwards them all (<c>docs/ELEMENT-TREE-PLAN.md</c> §3.10).
    /// <code>ui.UseTheme(MaterialTheme.Instance.With(button: new ButtonTheme { MinWidth = 88 }));</code>
    /// </summary>
    public static IAppTheme With(this IAppTheme theme, ControlsTheme? controls = null, ButtonTheme? button = null,
        NavigationTheme? navigation = null, ListTheme? list = null, TableTheme? table = null, BadgeTheme? badge = null,
        CalendarTheme? calendar = null, MarkdownTheme? markdown = null) =>
        new OverriddenTheme(theme, controls, button, navigation, list, table, badge, calendar, markdown);
}
