using EQuanticNativeApp;
using eQuantic.UI.Native.Hosting;
using eQuantic.UI.Primitives;

/// <summary>
/// The Photon entry point: CreateApp declares the app, the platform head (generated per target)
/// runs it. `dotnet run` opens a real window on this machine; the same code builds for phones.
/// </summary>
public partial class Program
{
    public static PhotonApplication CreateApp(string[] args)
    {
        var builder = PhotonApplication.CreateBuilder(args);

        builder.Configure(photon =>
        {
            photon.Theme = PhotonTheme.Instance;   // try MaterialTheme.FromSeed(...) to rebrand
            photon.Title = "EQuanticNativeApp";
            photon.Width = 900;
            photon.Height = 620;
        });

        return builder.Build().UseRoot<HomeScreen>();
    }
}
