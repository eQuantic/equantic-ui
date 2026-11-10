using eQuantic.UI.Native.Hosting;
using eQuantic.UI.Primitives;
using eQuantic.Wallet;
using Microsoft.Extensions.DependencyInjection;

/// <summary>
/// eQUANTIC WALLET — the same C# on a phone and on a desktop, described once, and in the browser
/// through <c>samples/WalletWeb</c>: both heads draw the screens of <c>samples/WalletShared</c>. The half of
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

        // What this app asks of the device, said the same way everything else about the app is
        // said. The generator reads these calls at compile time and writes the Info.plist keys and
        // the Android permissions from them — nobody opens a platform file.
        builder.Capabilities
            .UsePhotoLibrary("Attach a receipt to a payment.")
            .UseBiometrics("Confirm a transfer with Face ID.");

        // The RELEASE-time half, in the same place and the same language: what the system must
        // permit once this ships signed. Only what THIS app's code needs — what the .NET runtime
        // needs under the hardened runtime, the SDK declares by itself.
        builder.Entitlements.RequireNetworkClient();

        builder.Configure(photon =>
        {
            photon.Theme = PhotonTheme.Instance;
            photon.Title = "eQuantic Wallet";
            // The phone this was drawn for, unless the configuration names a window of its own
            // (`--Photon:Width 1100 --Photon:Height 720` opens the desktop layout, with its rail).
            // A device reports its own size and ignores these.
            if (builder.Configuration["Photon:Width"] is null) photon.Width = 390;
            if (builder.Configuration["Photon:Height"] is null) photon.Height = 844;
        });

        // The screen to open on, from the configuration the host already assembled out of
        // appsettings.json, the environment and the command line: `--screen cards` opens on the
        // cards screen. The shared screens take the value and know nothing of where it came from.
        var screen = Enum.TryParse<WalletScreen>(builder.Configuration["screen"], ignoreCase: true, out var named)
            ? named
            : WalletScreen.Home;

        // `--tour true` has the app walk its own screens, which is how its video is recorded.
        var tour = bool.TryParse(builder.Configuration["tour"], out var asked) && asked;

        var app = builder.Build();
        return app.UseRoot(() => ActivatorUtilities.CreateInstance<WalletApp>(app.Services, screen, tour));
    }
}
