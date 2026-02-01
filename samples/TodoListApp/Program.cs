using eQuantic.UI.Server;
using eQuantic.UI.Tailwind;

var builder = WebApplication.CreateBuilder(args);

// Add UI services
builder.Services.AddSingleton<TodoListApp.Services.ITodoService, TodoListApp.Services.TodoService>();

// Add Tailwind theme services (must be before AddUI for SSR)
builder.Services.AddTailwind();

builder.Services.AddUI(options =>
{
    options.ScanAssembly(typeof(Program).Assembly)
           .WithSsr()
           .ConfigureHtmlShell(shell =>
           {
               shell.SetTitle("Todo List App | eQuantic.UI")
                    .AddHeadTag("<meta name=\"theme-color\" content=\"#3b82f6\">");
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

// Enable Tailwind CSS
app.UseTailwind();

// Map UI (dynamic routing)
app.MapUI();

app.Run();
