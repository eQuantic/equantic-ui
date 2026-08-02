using eQuantic.UI.Native.Hosting;
using eQuantic.UI.Primitives;
using eQuantic.Wallet;
using Microsoft.Extensions.DependencyInjection;

// eQUANTIC WALLET — the same C# on a phone and on a desktop, and the same shape as any other modern
// .NET program: create a builder, register what the app needs, build, run. Which device this runs on
// is not a question the program answers — the SDK referenced one shell for the target framework, and
// `Run` asks it.
var builder = PhotonApplication.CreateBuilder(args);

// An ordinary service, taken by the component through its constructor. Nothing about a UI framework
// should make dependency injection a special case.
builder.Services.AddSingleton<IWalletLedger, WalletLedger>();

builder.Configure(photon =>
{
    photon.Theme = PhotonTheme.Instance;
    photon.Title = "eQuantic Wallet";
    // The phone this was drawn for. A device reports its own size and ignores these.
    photon.Width = 390;
    photon.Height = 844;
});

var app = builder.Build();
app.Run<WalletApp>();
