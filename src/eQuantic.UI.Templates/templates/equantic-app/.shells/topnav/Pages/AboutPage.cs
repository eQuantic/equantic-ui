using eQuantic.UI.Components;
using eQuantic.UI.Core;
using eQuantic.UI.Primitives;

namespace EQuanticApp.Pages;

/// <summary>
/// The second route, and the whole point of having a shell: this page names the frame and its own
/// path, and the header lights the right link with no router lookup and no shared state.
/// <para>
/// Navigating here does NOT reload the document — the client router swaps the page and the shell
/// above it is reconciled, not rebuilt. Implement <c>IHandleMetadata</c> to give the route its own
/// title and description.
/// </para>
/// </summary>
[Page("/about", Title = "About")]
public sealed class AboutPage : StatelessComponent
{
    public override VisualNode Build(ComponentContext context)
    {
        var theme = context.Theme;

        return AppShell("/about", Column(gap: Space.S3, children: [
            Text("About", TypeRole.Heading, theme.TextPrimary, maxLines: 1),
            Text("Add a page by dropping a class with [Page(\"/route\")] into Pages/. There is no "
                + "route table to update: the build finds it, the server serves it and the client "
                + "router learns it.", TypeRole.BodyM, theme.TextSecondary, maxLines: 4),
        ]));
    }
}
