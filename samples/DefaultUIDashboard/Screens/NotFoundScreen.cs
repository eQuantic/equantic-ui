using eQuantic.UI.Components;
using eQuantic.UI.Core;
using eQuantic.UI.Primitives;

namespace eQuantic.Console;

/// <summary>
/// The console's BRANDED not-found page — an ordinary write-once page routed at "/404". The server
/// resolves it for every unknown route (still answering HTTP 404) and SSRs it; the client mounts it
/// like any other page. Declaring this page is ALL an app does to own its 404.
/// </summary>
[Page("/404", Title = "Not found — eQuantic Console")]
public sealed class NotFoundScreen : StatelessComponent
{
    public override VisualNode Build(ComponentContext context)
    {
        var theme = context.Theme;

        // Fill height gives the main axis room to actually center in the viewport.
        var body = new Column(gap: Space.S3)
        {
            Width = SizeValue.Fill,
            Height = SizeValue.Fill,
            Cross = CrossAlign.Center,
            Main = MainAlign.Center,
        };
        body.Add(new Text("404", TypeRole.Display, theme.TextPrimary, maxLines: 1));
        body.Add(new Text("This console has no such screen.", TypeRole.Title, theme.TextPrimary, maxLines: 1));
        body.Add(new Text("Check the address, or head back to your payments.", TypeRole.BodyM, theme.TextMuted, maxLines: 2));
        body.Add(new Link("/", new Button("Back to Payments", Variant.Primary, SizeVariant.Medium)));

        return new Box(new BoxStyle
        {
            Width = SizeValue.Fill,
            Height = SizeValue.Fill,
            Background = theme.Background,
            Padding = EdgeInsets.All(Space.S6),
        }, body);
    }
}
