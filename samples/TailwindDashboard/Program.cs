using eQuantic.UI.Server;
using eQuantic.UI.Tailwind;
using eQuantic.UI.Lucide;
using eQuantic.UI.Charts.ChartJs;
using eQuantic.UI.Charts.ApexCharts;
using eQuantic.UI.Lottie;
using eQuantic.UI.Images;
using eQuantic.UI.Web.Components.Display;
using eQuantic.UI.Core.Assets;

var builder = WebApplication.CreateBuilder(args);

// Add custom services
builder.Services.AddSingleton<TailwindDashboard.Services.ITodoService, TailwindDashboard.Services.TodoService>();

builder.Services.AddUI(options =>
{
    options.ScanAssembly(typeof(Program).Assembly)
           .WithSsr()
           .UseTailwind()
           .UseLucideIcons()
           .UseChartJs()
           .UseApexCharts()
           .UseLottie()
           .UseImageOptimization(opts =>
           {
               opts.DefaultQuality = 80;
               opts.Formats = ["image/webp"];
           })
            .ConfigureHtmlShell(shell =>
            {
                shell.SetTitle("eQuantic.UI | Tailwind Dashboard")
                     .SetHtmlClass("dark")
                     .AddHeadTag("<meta name=\"theme-color\" content=\"#3b82f6\">")
                     .EnableTailwindDarkMode();
            });
});

var app = builder.Build();

// Set global service provider for UI components (thread-safe)
eQuantic.UI.Core.RenderContext.SetGlobalServiceProvider(app.Services);

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();

// Enable Server Actions
app.UseServerActions();

// Map UI (dynamic routing + package endpoints)
app.MapUI();

app.Run();
