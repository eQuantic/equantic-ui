using System.CommandLine;
using eQuantic.UI.CLI.Commands;

namespace eQuantic.UI.CLI;

class Program
{
    static async Task<int> Main(string[] args)
    {
        var rootCommand = new RootCommand("eQuantic.UI - Component-based UI Framework for .NET Web")
        {
            Name = "eqx"
        };

        // Build command
        var buildCommand = new Command("build", "Compile .eqx files to JavaScript and CSS");
        var inputOption = new Option<string>(
            new[] { "-i", "--input" },
            "Input directory or file path")
        { IsRequired = false };
        inputOption.SetDefaultValue("./src");
        
        var outputOption = new Option<string>(
            new[] { "-o", "--output" },
            "Output directory")
        { IsRequired = false };
        outputOption.SetDefaultValue("./dist");
        
        var watchOption = new Option<bool>(
            new[] { "-w", "--watch" },
            "Watch for changes");
        
        buildCommand.AddOption(inputOption);
        buildCommand.AddOption(outputOption);
        buildCommand.AddOption(watchOption);
        buildCommand.SetHandler(BuildCommand.Execute, inputOption, outputOption, watchOption);
        rootCommand.AddCommand(buildCommand);

        // Dev command
        var devCommand = new Command("dev", "Start development server with hot reload");
        var portOption = new Option<int>(
            new[] { "-p", "--port" },
            () => 3000,
            "Port for the dev server");
        devCommand.AddOption(portOption);
        devCommand.AddOption(inputOption);
        devCommand.SetHandler(DevCommand.Execute, portOption, inputOption);
        rootCommand.AddCommand(devCommand);

        // Create command
        var createCommand = new Command("create", "Create a new eQuantic.UI project");
        var projectNameArgument = new Argument<string>(
            "name",
            "Name of the project to create");
        var templateOption = new Option<string>(
            new[] { "-t", "--template" },
            () => "counter",
            "Template to use (counter, blank)");
        createCommand.AddArgument(projectNameArgument);
        createCommand.AddOption(templateOption);
        createCommand.SetHandler(CreateCommand.Execute, projectNameArgument, templateOption);
        rootCommand.AddCommand(createCommand);

        // Version command
        var versionCommand = new Command("version", "Show version information");
        versionCommand.SetHandler(() =>
        {
            Console.WriteLine("eQuantic.UI CLI v0.1.0");
            Console.WriteLine("Runtime: @equantic/ui-runtime v0.1.0");
        });
        rootCommand.AddCommand(versionCommand);

        return await rootCommand.InvokeAsync(args);
    }
}
