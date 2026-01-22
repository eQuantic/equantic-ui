using eQuantic.UI.Server;

var builder = WebApplication.CreateBuilder(args);

// Add eQuantic.UI services
builder.Services.AddEQuanticUI(options =>
{
    options.ScanAssembly(typeof(Program).Assembly);
});

var app = builder.Build();

// Serve static files (including compiled JS)
app.UseStaticFiles();

// Enable Server Actions
app.UseEQuanticServerActions();

// Serve the SPA index.html for all routes
app.MapFallbackToFile("index.html");

app.Run();
