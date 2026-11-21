using eQuantic.UI.Components;
using eQuantic.UI.Primitives;
using EQuanticNativeApp.Screens;

namespace EQuanticNativeApp;

/// <summary>
/// The destinations shell — and the framework's answer to "do I need a separate tablet build?",
/// which is no.
/// <para>
/// One list of destinations, one selected index, one handler. Under 600dp they sit in a bar across
/// the bottom; past it they stand up as a rail on the leading edge. Nothing here listens for a
/// resize and nothing keeps a second state: <c>AdaptiveNode</c> carries both trees and the target
/// picks one (the server writes both behind media queries, then the browser keeps the one that fits,
/// as Photon lays out only that one).
/// </para>
/// </summary>
public sealed class AppShell : StatefulComponent
{
    // The curated Icons enum is small on purpose — add an icon package (Lucide, Heroicons, Tabler)
    // and reach the rest with Icon(...) too — a pack glyph converts like a curated one.
    private static readonly NavItem[] Destinations =
    [
        new(Icons.Notifications, "Activity"),
        new(Icons.Search, "Search"),
        new(Icons.Person, "Profile"),
    ];

    private int _destination;

    // The screen is built in both variants, and the key is what makes it ONE screen: when the window
    // crosses 600dp (a phone turning to landscape, a window resized) its position moves into the other
    // tree, and what it holds comes with it.
    private readonly GlobalKey _screen = new("screen");

    public override VisualNode Build(ComponentContext context)
    {
        var theme = context.Theme;

        // A node belongs to ONE tree, so both variants below build their own — the same body twice,
        // never the same instance hung in two places.
        VisualNode Screen() => new Column(gap: 0) { Width = SizeValue.Fill, Height = SizeValue.Fill }
            .With(AppBar(Destinations[_destination].Label))
            .With(Flexible(_destination switch
            {
                1 => new SearchScreen { GlobalKey = _screen },
                2 => new ProfileScreen { GlobalKey = _screen },
                _ => new ActivityScreen { GlobalKey = _screen },
            }));

        var phone = new Column(gap: 0) { Width = SizeValue.Fill, Height = SizeValue.Fill }
            .With(Flexible(Screen()))
            .With(Box(new BoxStyle { Width = SizeValue.Fill, Background = theme.Surface },
                SafeArea(BottomNavigation(Destinations, _destination, Go), SafeEdges.Bottom)));

        // The rail brings its own fill and its own trailing edge — nothing to wrap it in.
        var wide = new Row(gap: 0) { Width = SizeValue.Fill, Height = SizeValue.Fill }
            .With(SafeArea(NavigationRail(Destinations, _destination, Go),
                SafeEdges.Start | SafeEdges.Bottom))
            .With(Flexible(Screen()));

        return Box(new BoxStyle
        {
            Width = SizeValue.Fill,
            Height = SizeValue.Fill,
            Background = theme.Background,
        },
        // The TOP inset only: each navigation keeps its own edge clear, so the shell must not
        // claim it twice.
        SafeArea(AdaptiveNode(phone, wide), SafeEdges.Top));
    }

    /// <summary>What a destination DOES, written once for both shapes. A bar and a rail that
    /// behaved differently after a rotation would be a different app, not a wider one.</summary>
    private void Go(int index) => SetState(() => _destination = index);
}
