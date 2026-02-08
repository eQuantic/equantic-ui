using eQuantic.UI.Server;
using eQuantic.UI.Tailwind;
using eQuantic.UI.Lucide;
using eQuantic.UI.Charts.ChartJs;
using eQuantic.UI.Charts.ApexCharts;

var builder = WebApplication.CreateBuilder(args);

// Add UI services
builder.Services.AddSingleton<TailwindDashboard.Services.ITodoService, TailwindDashboard.Services.TodoService>();

// Add Tailwind theme services (must be before AddUI for SSR)
builder.Services.AddTailwind();

// Add Lucide icon set
builder.Services.AddLucideIcons();

// Add chart libraries
builder.Services.AddChartJs();
builder.Services.AddApexCharts();

builder.Services.AddUI(options =>
{
    options.ScanAssembly(typeof(Program).Assembly)
           .WithSsr()
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

// Set global service provider for UI components
eQuantic.UI.Core.RenderContext.ServiceProvider = app.Services;

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

// Map Tailwind CSS dynamic endpoints (theme.js, dark-mode.js)
app.UseTailwind();

// Map Chart.js and ApexCharts (CDN + initialization scripts)
app.UseChartJs();
app.UseApexCharts();

// Map UI (dynamic routing)
app.MapUI();

app.Run();
