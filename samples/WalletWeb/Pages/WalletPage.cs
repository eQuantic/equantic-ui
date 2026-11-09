using eQuantic.UI.Components;
using eQuantic.UI.Primitives;
using eQuantic.Wallet;

namespace WalletWeb.Pages;

/// <summary>
/// The Wallet in the browser. The screens are the class library's, the same ones the native head
/// draws on a phone and on a desktop. This page only hands them what the native head resolves from
/// its container: the ledger, built here, and the screen to open on.
/// </summary>
[Page("/", Title = "eQuantic Wallet")]
public sealed class WalletPage : StatelessComponent
{
    public override VisualNode Build(ComponentContext context) => new WalletApp(new WalletLedger());
}
