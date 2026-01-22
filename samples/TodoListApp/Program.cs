using eQuantic.UI.Server;

var builder = WebApplication.CreateBuilder(args);

// Add eQuantic.UI services
builder.Services.AddSingleton<TodoListApp.Services.ITodoService, TodoListApp.Services.TodoService>();
builder.Services.AddEQuanticUI(options =>
{
    // Configure your UI options here
    // options.ScanAssembly(typeof(Program).Assembly);
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();

// Enable eQuantic Server Actions
app.UseEQuanticServerActions();

// Map eQuantic Pages (dynamic routing)
app.MapEQuanticUi();

app.Run();
