using eQuantic.UI.Native.Hosting;
using eQuantic.UI.Primitives;
using eQuantic.Wallet;
using Microsoft.Extensions.DependencyInjection;

/// <summary>
/// eQUANTIC WALLET — the same C# on a phone and on a desktop, described once. The half of
/// <c>Program</c> that is this app's; the SDK writes the other half, which is the <c>Main</c> the
/// platforms that have one need. An Android app has none at all — the system launches an Activity —
/// which is why what an app describes is a METHOD rather than an entry point.
/// <para>
/// A MEMBER, not top-level statements: global code IS an entry point, so it would both pre-empt the
/// generated Main and fail outright on Android, where the SDK compiles the app as a library.
/// </para>
/// </summary>
public partial class Program
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
