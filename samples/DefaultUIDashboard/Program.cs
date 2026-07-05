using eQuantic.UI.Primitives;
using eQuantic.UI.Server;
using eQuantic.UI.Web;

var builder = WebApplication.CreateBuilder(args);

// Add UI services
builder.Services.AddUI(options =>
{
    options.ScanAssembly(typeof(Program).Assembly)
           .ConfigureHtmlShell(shell =>
           {
               shell.SetTitle("Counter Demo (Fluent API)")
                    .AddHeadTag("<meta name=\"description\" content=\"eQuantic.UI Demo\">")
                    // The NORMATIVE Photon design-system stylesheet (generated from the C# tokens —
                    // never hand-written) backing the write-once showcase page (/shared).
                    .AddHeadTag($"<style>{PhotonCssGenerator.Generate(PhotonTheme.Instance)}</style>")
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
                    .SetBaseStyles(@"
                        :root { --primary: #6366f1; --bg: #0f172a; --text: #f8fafc; }
                        body { background: var(--bg); color: var(--text); }
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

