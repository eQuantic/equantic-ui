using eQuantic.UI.Server;
using eQuantic.UI.Tailwind;

var builder = WebApplication.CreateBuilder(args);

// Add UI services
builder.Services.AddSingleton<TodoListApp.Services.ITodoService, TodoListApp.Services.TodoService>();
builder.Services.AddUI(options =>
{
    // Configure your UI options here
    options.ScanAssembly(typeof(Program).Assembly);
});

var app = builder.Build();

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

