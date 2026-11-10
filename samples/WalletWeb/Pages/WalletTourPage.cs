using eQuantic.UI.Components;
using eQuantic.UI.Primitives;
using eQuantic.Wallet;

namespace WalletWeb.Pages;

/// <summary>
/// The Wallet walking its own screens: the same tour the native head plays with `--tour true`, at a
/// route of its own. Nothing is tapped, so a recording of this page and one of the phone's show the
/// same thing at the same moment.
/// </summary>
[Page("/tour", Title = "eQuantic Wallet")]
public sealed class WalletTourPage(IWalletLedger ledger) : StatelessComponent
{
    public override VisualNode Build(ComponentContext context) => new WalletApp(ledger, tour: true);
}
