using eQuantic.UI.Primitives;
using eQuantic.UI.Server;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddUI(options => options.ScanAssembly(typeof(Program).Assembly).UseTheme(PhotonTheme.Instance));

var app = builder.Build();

app.UseRequestLocalization(options =>
{
    options.SetDefaultCulture("en");
    options.AddSupportedUICultures("en", "pt-BR");
    options.AddSupportedCultures("en", "pt-BR");
});

// What a real site did to serve its catalogue at another path (#1778): the server draws CataloguePage for
// /promo, while the browser's router gives /promo to PromoPage.
app.Use(async (context, next) =>
{
    if (context.Request.Path == "/promo") context.Request.Path = "/catalogue";
    await next();
});
// A redirect, which a page's test follows to where the page is served: the home page's path, asked with
// `go`, sends the browser to the catalogue.
app.Use(async (context, next) =>
{
    if (context.Request.Path == "/" && context.Request.Query.ContainsKey("go"))
    {
        context.Response.Redirect("/catalogue");
        return;
    }
    await next();
});
// After the rewrite, as a rewrite has to be: the routing a minimal host adds by itself runs first otherwise.
app.UseRouting();

app.UseStaticFiles();
app.UseServerActions();
app.MapUI();

app.Run();

/// <summary>The site's entry point, which the tests' WebApplicationFactory starts.</summary>
public partial class Program;
