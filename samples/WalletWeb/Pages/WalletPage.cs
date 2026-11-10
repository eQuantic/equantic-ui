using eQuantic.UI.Components;
using eQuantic.UI.Primitives;
using eQuantic.Wallet;

namespace WalletWeb.Pages;

/// <summary>
/// The Wallet in the browser. The screens are the class library's, the same ones the native head
/// draws on a phone and on a desktop. This page takes the ledger from the container, as the native
/// head's root does, and hands it to them.
/// </summary>
[Page("/", Title = "eQuantic Wallet")]
public sealed class WalletPage(IWalletLedger ledger) : StatelessComponent
{
    public override VisualNode Build(ComponentContext context) => new WalletApp(ledger);
}
