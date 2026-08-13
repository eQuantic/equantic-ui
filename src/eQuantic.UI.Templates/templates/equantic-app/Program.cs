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

// Culture negotiation is ASP.NET's own middleware — the SDK reads what it sets. The default
// providers already include the query string, the culture cookie (which CultureSwitcher writes)
// and Accept-Language, in that order. Ship a new language by adding Strings.<culture>.resx and
// listing it here.
app.UseRequestLocalization(options =>
{
    options.AddSupportedUICultures("en", "pt-BR");
    options.AddSupportedCultures("en", "pt-BR");
});

app.UseStaticFiles();
app.UseServerActions();
app.MapUI();

app.Run();
