using eQuantic.UI.Server;

var builder = WebApplication.CreateBuilder(args);

// Add eQuantic.UI services
// Add eQuantic.UI services
builder.Services.AddEQuanticUI(options =>
{
    options.ScanAssembly(typeof(Program).Assembly);
    
    // Configure HTML Shell
    options.HtmlShell.Title = "Counter Demo (Dynamic)";
    options.HtmlShell.HeadTags.Add(@"<meta name=""description"" content=""eQuantic.UI Demo"">");
    options.HtmlShell.BaseStyles += @"
        :root { --primary: #6366f1; --bg: #0f172a; --text: #f8fafc; }
        body { background: var(--bg); color: var(--text); }
        .counter { padding: 2rem; } 
    ";
});

var app = builder.Build();

// Serve static files (including compiled JS)
app.UseStaticFiles();

// Enable Server Actions
app.UseEQuanticServerActions();

// Serve the SPA shell dynamically
app.MapEQuanticUi();

app.Run();
