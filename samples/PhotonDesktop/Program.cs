using eQuantic.UI.Native.Hosting;
using eQuantic.UI.Primitives;
using eQuantic.Studio;

/// <summary>
/// eQUANTIC STUDIO — the component gallery. `--Photon:MaxFrames 120` presents that many frames and
/// exits, which is what the self-test runs; every other setting reaches the app from appsettings.json
/// or the command line the way a .NET developer already expects.
/// </summary>
public partial class Program
{
    public static PhotonApplication CreateApp(string[] args)
    {
        var builder = PhotonApplication.CreateBuilder(args);

        builder.Configure(photon =>
        {
            photon.Theme = PhotonTheme.Instance;
            photon.Title = "eQuantic Studio";
            photon.Width = 1180;
            photon.Height = 820;
        });

        return builder.Build().UseRoot<StudioShell>();
    }
}
