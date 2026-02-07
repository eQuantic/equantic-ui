using eQuantic.UI.Server;
using eQuantic.UI.Material;

var builder = WebApplication.CreateBuilder(args);

// Add Material Design 3 theme services
builder.Services.AddMaterialTheme();

// Add UI services
builder.Services.AddUI(options =>
{
    options.ScanAssembly(typeof(Program).Assembly)
           .ConfigureHtmlShell(shell =>
           {
               shell.SetTitle("Material Dashboard | eQuantic.UI")
                    .AddHeadTag("<link href=\"https://fonts.googleapis.com/icon?family=Material+Icons\" rel=\"stylesheet\">")
                    .AddHeadTag("<link href=\"https://fonts.googleapis.com/css2?family=Roboto:wght@300;400;500;700&display=swap\" rel=\"stylesheet\">");
           });
});

var app = builder.Build();

app.UseStaticFiles();
app.UseServerActions();
app.MapUI();

app.Run();

public partial class Program { }
