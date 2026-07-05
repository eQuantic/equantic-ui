using eQuantic.UI.Core;
using eQuantic.UI.Web;

namespace DefaultUIDashboard.Pages;

/// <summary>
/// The home page — a WRITE-ONCE SHOWROOM. A CORE page whose hoisted state drives the shared
/// (Photon-vocabulary) subtree in <see cref="DashboardView"/> through the <see cref="VisualNodeComponent"/>
/// bridge: SSR renders it via the WebRealizer and the client hydrates/re-renders it with the app-selected
/// theme (Program.cs → <c>UseTheme</c>). No legacy Web.Components here — the whole page is the new library.
/// </summary>
[Page("/", Title = "eQuantic — Components Showroom")]
public class Dashboard : StatefulComponent
{
    public override ComponentState CreateState() => new DashboardState();
}

public class DashboardState : ComponentState<Dashboard>
{
    private int _tab;
    private int _nav;
    private int _refreshes;
    private bool _realtime = true;
    private bool _emailAlerts;
    private bool _confirming;
    private string _outcome = "none";

    public override IComponent Build(RenderContext context) =>
        new VisualNodeComponent(DashboardView.Build(
            _tab, _nav, _refreshes, _realtime, _emailAlerts, _confirming, _outcome,
            selectTab: i => SetState(() => _tab = i),
            selectNav: i => SetState(() => _nav = i),
            refresh: () => SetState(() => _refreshes++),
            toggleRealtime: () => SetState(() => _realtime = !_realtime),
            toggleEmail: () => SetState(() => _emailAlerts = !_emailAlerts),
            openReset: () => SetState(() => _confirming = true),
            resolveReset: outcome => SetState(() =>
            {
                _outcome = outcome;
                if (outcome == "done")
                {
                    _refreshes = 0;
                    _tab = 0;
                }
                _confirming = false;
            })));
}
