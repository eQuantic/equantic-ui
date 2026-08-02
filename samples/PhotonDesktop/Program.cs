using eQuantic.UI.Native.Hosting;
using eQuantic.UI.Primitives;
using eQuantic.Studio;

// eQUANTIC STUDIO — the component gallery, and the desktop half of the same host every Photon app
// is built through. `--Photon:MaxFrames 120` presents that many frames and exits, which is what the
// self-test runs; every other setting reaches here from appsettings.json or the command line the
// way a .NET developer already expects.
var builder = PhotonApplication.CreateBuilder(args);

builder.Configure(photon =>
{
    photon.Theme = PhotonTheme.Instance;
    photon.Title = "eQuantic Studio";
    photon.Width = 1180;
    photon.Height = 820;
});

var app = builder.Build();
app.Run<StudioShell>();
