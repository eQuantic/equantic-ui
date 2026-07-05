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
               shell.SetTitle("Counter Demo (Fluent API)")
                    .AddHeadTag("<meta name=\"description\" content=\"eQuantic.UI Demo\">")
                    // Route-guard demo: register a client navigation guard via the documented
                    // window.__eqGuards hook (read once when the router boots). It gates /admin behind a
                    // demo auth flag, and interprets ?login=1 / ?logout=1 so the C# pages can drive auth
                    // with plain links. A real app would pair this with server-side [Authorize].
                    .AddHeadTag(@"<script>
                      window.__eqGuards = [function (to) {
                        var authed = function () { return localStorage.getItem('eq-demo-authed') === '1'; };
                        if (to.url.searchParams.get('login') === '1') {
                          localStorage.setItem('eq-demo-authed', '1');
                          return to.url.pathname;            // redirect to the clean path, now signed in
                        }
                        if (to.url.searchParams.get('logout') === '1') {
                          localStorage.removeItem('eq-demo-authed');
                          return '/login';                   // redirect after signing out
                        }
                        if (to.url.pathname.indexOf('/admin') === 0 && !authed()) {
                          return '/login';                   // cancel + redirect: protected route
                        }
                        return true;                         // allow
                      }];
                    </script>")
                    // Page chrome follows the SELECTED theme: the UseTheme token stylesheet emits
                    // --eq-color-* as light-dark() pairs, so body background/text track Photon (or
                    // Material, or a seed) AND light/dark via color-scheme — no hardcoded palette.
                    .SetBaseStyles(@"
                        body { margin: 0; font-family: system-ui, -apple-system, sans-serif;
                               background: var(--eq-color-background); color: var(--eq-color-text-primary); }
                        .counter { padding: 2rem; }
                    ");
           });
});

var app = builder.Build();

// Serve static files (including compiled JS)
app.UseStaticFiles();

// Enable Server Actions
app.UseServerActions();

// Serve the SPA shell dynamically
app.MapUI();

app.Run();

