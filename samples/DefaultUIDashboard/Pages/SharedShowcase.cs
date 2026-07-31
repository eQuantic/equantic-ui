using eQuantic.UI.Core;
using eQuantic.UI.Web;

namespace DefaultUIDashboard.Pages;

/// <summary>
/// The write-once showcase page: a CORE page whose state drives a SHARED (Photon-vocabulary) subtree
/// through the <see cref="VisualNodeComponent"/> bridge — SSR renders it via the WebRealizer and the
/// client hydrates/re-renders it via the runtime lowering. State is HOISTED to the page (v1: nested
/// shared component instances are rebuilt per render pass; positional state retention arrives with
/// the reconciler slice).
/// </summary>
[Page("/shared", Title = "Write-Once Showcase")]
public class SharedShowcase : StatefulComponent
{
    public override ComponentState CreateState() => new SharedShowcaseState();
}

public class SharedShowcaseState : ComponentState<SharedShowcase>
{
    private int _count = 0;
    private bool _confirming;
    private bool _sharing;
    private string _outcome = "none";

    public override IComponent Build(RenderContext context) =>
        new VisualNodeComponent(SharedCounterView.Build(_count, () => SetState(() => _count++),
            _confirming, _outcome,
            () => SetState(() => _confirming = true),
            outcome => SetState(() =>
            {
                _outcome = outcome;
                _confirming = false;
            }),
            _sharing,
            () => SetState(() => _sharing = true),
            () => SetState(() => _sharing = false)));
}
