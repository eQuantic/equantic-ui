namespace eQuantic.UI.CLI.Commands;

public static class CreateCommand
{
    public static void Execute(string name, string template)
    {
        Console.WriteLine($"📁 Creating new eQuantic.UI project: {name}");
        Console.WriteLine($"   Template: {template}");
        Console.WriteLine();
        
        var projectDir = Path.Combine(Directory.GetCurrentDirectory(), name);
        
        if (Directory.Exists(projectDir))
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"❌ Directory already exists: {projectDir}");
            Console.ResetColor();
            return;
        }
        
        try
        {
            // Create directory structure
            Directory.CreateDirectory(projectDir);
            Directory.CreateDirectory(Path.Combine(projectDir, "src"));
            Directory.CreateDirectory(Path.Combine(projectDir, "src", "components"));
            Directory.CreateDirectory(Path.Combine(projectDir, "src", "styles"));
            
            // Create eqx.json project file
            var eqxJson = @"{
  ""name"": """ + name + @""",
  ""version"": ""1.0.0"",
  ""entry"": ""src/App.eqx"",
  ""output"": ""dist"",
  ""runtime"": ""@equantic/ui-runtime""
}";
            File.WriteAllText(Path.Combine(projectDir, "eqx.json"), eqxJson);
            Console.WriteLine("   ✓ Created eqx.json");
            
            // Create App.eqx
            var appContent = template == "counter" ? GetCounterTemplate(name) : GetBlankTemplate(name);
            File.WriteAllText(Path.Combine(projectDir, "src", "App.eqx"), appContent);
            Console.WriteLine("   ✓ Created src/App.eqx");
            
            // Create styles
            var stylesContent = GetStylesTemplate();
            File.WriteAllText(Path.Combine(projectDir, "src", "styles", "AppStyles.cs"), stylesContent);
            Console.WriteLine("   ✓ Created src/styles/AppStyles.cs");
            
            // Create index.html
            var htmlContent = GetIndexHtml(name);
            File.WriteAllText(Path.Combine(projectDir, "index.html"), htmlContent);
            Console.WriteLine("   ✓ Created index.html");
            
            Console.WriteLine();
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("✅ Project created successfully!");
            Console.ResetColor();
            Console.WriteLine();
            Console.WriteLine("   Next steps:");
            Console.WriteLine($"   $ cd {name}");
            Console.WriteLine("   $ eqx dev");
            Console.WriteLine();
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"❌ Error creating project: {ex.Message}");
            Console.ResetColor();
        }
    }
    
    private static string GetCounterTemplate(string name) => $@"// App.eqx - {name}
using eQuantic.UI.Core;
using eQuantic.UI.Components;

namespace {SanitizeName(name)};

[Component]
public class App : StatefulComponent
{{
    public override ComponentState CreateState() => new AppState();
}}

public class AppState : ComponentState<App>
{{
    private int _count = 0;
    private string _message = """";

    private void _increment() => SetState(() => _count++);
    private void _decrement() => SetState(() => _count--);

    public override IComponent Build(RenderContext context)
    {{
        return new Container
        {{
            Id = ""app"",
            ClassName = ""app-container"",
            Children =
            {{
                new Heading(""Welcome to {name}"", 1),
                
                new TextInput
                {{
                    Value = _message,
                    Placeholder = ""Type something..."",
                    OnChange = (v) => SetState(() => _message = v)
                }},
                
                new Row
                {{
                    Gap = ""16px"",
                    Justify = ""center"",
                    Children =
                    {{
                        new Button {{ Text = ""-"", OnClick = _decrement }},
                        new Text($""{{_count}}""),
                        new Button {{ Text = ""+"", OnClick = _increment }}
                    }}
                }},
                
                _count > 0 && !string.IsNullOrEmpty(_message)
                    ? new Text($""Message: {{_message}}"")
                    : null
            }}
        }};
    }}
}}
";

    private static string GetBlankTemplate(string name) => $@"// App.eqx - {name}
using eQuantic.UI.Core;
using eQuantic.UI.Components;

namespace {SanitizeName(name)};

[Component]
public class App : StatelessComponent
{{
    public override IComponent Build(RenderContext context)
    {{
        return new Container
        {{
            Id = ""app"",
            Children =
            {{
                new Heading(""Hello, {name}!"", 1),
                new Text(""Edit src/App.eqx to get started."")
            }}
        }};
    }}
}}
";

    private static string GetStylesTemplate() => @"using eQuantic.UI.Core;
using eQuantic.UI.Core.Styling;

namespace AppStyles;

public static class Styles
{
    public static readonly StyleClass Container = new()
    {
        MaxWidth = ""800px"",
        Margin = Spacing.Horizontal(0), // auto
        Padding = Spacing.All(24)
    };
    
    public static readonly StyleClass Button = new()
    {
        BackgroundColor = Colors.Blue[500],
        Color = Colors.White,
        Padding = Spacing.Symmetric(8, 16),
        BorderRadius = 8,
        Cursor = ""pointer"",
        
        Hover = new()
        {
            BackgroundColor = Colors.Blue[600]
        }
    };
}
";

    private static string GetIndexHtml(string name) => $@"<!DOCTYPE html>
<html lang=""en"">
<head>
    <meta charset=""UTF-8"">
    <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">
    <title>{name}</title>
    <link rel=""stylesheet"" href=""dist/styles.css"">
</head>
<body>
    <div id=""app""></div>
    <script type=""module"" src=""dist/App.js""></script>
</body>
</html>
";

    private static string SanitizeName(string name)
    {
        return string.Concat(name.Split(Path.GetInvalidFileNameChars()))
            .Replace("-", "")
            .Replace(" ", "");
    }
}
