using eQuantic.UI.Primitives;
using eQuantic.UI.Server;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddUI(options =>
{
    options.ScanAssembly(typeof(Program).Assembly)
           .UseTheme(PhotonTheme.Instance)
           .ConfigureHtmlShell(shell => shell.SetTitle("Shared library"));
});

var app = builder.Build();

// Two Portuguese cultures on purpose: the app and the library each have a catalogue named Strings,
// and each read has to show its own project's text in every one of them.
app.UseRequestLocalization(options =>
{
    options.AddSupportedUICultures("en", "pt-BR", "pt-PT");
    options.AddSupportedCultures("en", "pt-BR", "pt-PT");
});

app.UseStaticFiles();
app.UseServerActions();
app.MapUI();

app.Run();
