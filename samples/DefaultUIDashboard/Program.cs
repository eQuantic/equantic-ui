using eQuantic.UI.Primitives;
using eQuantic.UI.Server;

var builder = WebApplication.CreateBuilder(args);

// Add UI services
builder.Services.AddUI(options =>
{
    options.ScanAssembly(typeof(Program).Assembly)
           // Select the write-once design-system theme for the whole app (SSR + client). The server now
           // emits the NORMATIVE token stylesheet AND bridges the theme to the client (boot calls
           // setPhotonTheme) — swap this for MaterialTheme.Instance / MaterialTheme.FromSeed(seed) to
           // rebrand every shared component with nothing else changed.
           .UseTheme(PhotonTheme.Instance)
           .ConfigureHtmlShell(shell =>
           {
               shell.SetTitle("eQuantic Console")
                    .AddHeadTag("<meta name=\"description\" content=\"Admin console on the eQuantic.UI web layer\">")
                    // The shell tracks the SELECTED theme: UseTheme emits --eq-color-* as
                    // light-dark() pairs, so the page follows the OS without a second render.
                    .SetBaseStyles(@"
                        html, body { height: 100%; }
                        body { margin: 0; font-family: system-ui, -apple-system, sans-serif;
                               background: var(--eq-color-background); color: var(--eq-color-text-primary); }
                    ");
           });

});

var app = builder.Build();

// Track L D5: culture negotiation is ASP.NET's, wired by the APP — the SDK only reads the
// statics this middleware sets. Try /i18n with Accept-Language: pt-BR.
app.UseRequestLocalization(options =>
{
    options.AddSupportedUICultures("pt-BR");
    options.AddSupportedCultures("pt-BR");
});

// Serve static files (including compiled JS)
app.UseStaticFiles();

// Enable Server Actions
app.UseServerActions();

// Serve the SPA shell dynamically
app.MapUI();

app.Run();

