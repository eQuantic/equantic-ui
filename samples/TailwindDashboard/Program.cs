using eQuantic.UI.Server;
using eQuantic.UI.Tailwind;
using eQuantic.UI.Lucide;
using eQuantic.UI.Charts.ChartJs;
using eQuantic.UI.Charts.ApexCharts;
using eQuantic.UI.Lottie;
using eQuantic.UI.Images;
using eQuantic.UI.Components.Display;
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
                    // Dark mode: respect localStorage, fallback to OS preference
                    .AddHeadTag("<script>!function(){var t=localStorage.getItem('theme');var d=t?t==='dark':window.matchMedia('(prefers-color-scheme: dark)').matches;d?document.documentElement.classList.add('dark'):document.documentElement.classList.remove('dark');document.addEventListener('DOMContentLoaded',function(){var s=document.getElementById('icon-sun');var m=document.getElementById('icon-moon');if(s&&m){var dk=document.documentElement.classList.contains('dark');s.classList.toggle('hidden',!dk);m.classList.toggle('hidden',dk)}})}();</script>");
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
