using eQuantic.UI.Primitives;
using eQuantic.UI.Server;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddUI(options =>
{
    options.ScanAssembly(typeof(Program).Assembly)
           // ONE line rebrands every component (SSR + client). Try MaterialTheme.FromSeed(...).
           .UseTheme(PhotonTheme.Instance)
           .ConfigureHtmlShell(shell => shell.SetTitle("EQuanticApp"));
});

var app = builder.Build();

app.UseStaticFiles();
app.UseServerActions();
app.MapUI();

app.Run();
