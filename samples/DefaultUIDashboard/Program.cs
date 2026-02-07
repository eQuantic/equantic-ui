using eQuantic.UI.Server;

var builder = WebApplication.CreateBuilder(args);

// Add UI services
builder.Services.AddUI(options =>
{
    options.ScanAssembly(typeof(Program).Assembly)
           .ConfigureHtmlShell(shell =>
           {
               shell.SetTitle("Counter Demo (Fluent API)")
                    .AddHeadTag("<meta name=\"description\" content=\"eQuantic.UI Demo\">")
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

