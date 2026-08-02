using eQuantic.UI.Native.Hosting;
using eQuantic.UI.Primitives;
using Microsoft.Extensions.DependencyInjection;

namespace eQuantic.Wallet;

/// <summary>
/// eQUANTIC WALLET — the same C# on a phone and on a desktop, described once. A program is a METHOD
/// rather than a <c>Main</c> because an Android app has no <c>Main</c> at all: the system launches
/// an Activity. Every head calls this, and which device is running is not a question asked here.
/// </summary>
public static class WalletProgram
{
    public static PhotonApplication CreateApp(string[] args)
    {
        var builder = PhotonApplication.CreateBuilder(args);

        // An ordinary service, taken by the component through its constructor. Nothing about a UI
        // framework should make dependency injection a special case.
        builder.Services.AddSingleton<IWalletLedger, WalletLedger>();

        builder.Configure(photon =>
        {
            photon.Theme = PhotonTheme.Instance;
            photon.Title = "eQuantic Wallet";
            // The phone this was drawn for. A device reports its own size and ignores these.
            photon.Width = 390;
            photon.Height = 844;
        });

        return builder.Build().UseRoot<WalletApp>();
    }
}
