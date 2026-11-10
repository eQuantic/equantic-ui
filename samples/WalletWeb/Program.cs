using eQuantic.UI.Primitives;
using eQuantic.UI.Server;
using eQuantic.Wallet;

// eQUANTIC WALLET on the web: the screens of samples/WalletShared, the same ones samples/WalletMobile
// draws on macOS, iOS and Android. This head is the server and nothing else: what a phone asks of its
// device stays in the native head.
var builder = WebApplication.CreateBuilder(args);

// The app's own service, registered once. The server's container hands it to the page, and the
// browser builds the same class from this line: no second registration, and nothing written for it.
builder.Services.AddSingleton<IWalletLedger, WalletLedger>();

builder.Services.AddUI(options =>
{
    options.ScanAssembly(typeof(Program).Assembly)
           .UseTheme(PhotonTheme.Instance)
           .ConfigureHtmlShell(shell => shell.SetTitle("eQuantic Wallet"));
});

var app = builder.Build();

app.UseStaticFiles();
app.UseServerActions();
app.MapUI();

app.Run();
