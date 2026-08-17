using eQuantic.UI.Components;
using eQuantic.UI.Core;
using eQuantic.UI.Primitives;

namespace EQuanticApp.Pages;

/// <summary>
/// A second route, and the whole point of a shell: this page names the frame and its own path, and
/// the sidebar lights the right entry with no router lookup and no shared state.
/// <para>
/// Fetch real rows with a <c>[ServerAction]</c> method here — it runs on the SERVER and the client
/// calls it like a local method, which is why this page needs no API client and no fetch.
/// </para>
/// </summary>
[Page("/reports", Title = "Reports")]
public sealed class ReportsPage : StatelessComponent
{
    private static readonly (string Month, string Revenue, string Change)[] Rows =
    [
        ("January", "18,240", "+4.1%"),
        ("February", "21,905", "+20.1%"),
        ("March", "19,870", "-9.3%"),
    ];

    public override VisualNode Build(ComponentContext context)
    {
        var theme = context.Theme;

        return AppShell("/reports", Column(gap: Space.S4, children: [
            Text("Reports", TypeRole.Heading, theme.TextPrimary, maxLines: 1),
            new Table(
                ["Month", "Revenue", "Change"],
                [.. Rows.Select(row => new string[] { row.Month, row.Revenue, row.Change })]),
        ]));
    }
}
