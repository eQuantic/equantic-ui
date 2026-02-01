using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using eQuantic.UI.Server;

namespace eQuantic.UI.Tailwind;

/// <summary>
/// Extension methods for enabling Tailwind CSS in eQuantic.UI.
/// </summary>
public static class TailwindExtensions
{
    /// <summary>
    /// Enables Tailwind CSS integration by adding the default stylesheet link to the HTML shell.
    /// </summary>
    /// <param name="app">The application builder.</param>
    /// <param name="cssPath">The path to the generated CSS file. Defaults to "/css/app.css".</param>
    /// <returns>The application builder.</returns>
    public static IApplicationBuilder UseTailwind(this IApplicationBuilder app, string cssPath = "/css/app.css")
    {
        var options = app.ApplicationServices.GetService<UIOptions>();
        if (options == null)
        {
            throw new InvalidOperationException("UIOptions not found. Ensure AddUI() is called before UseTailwind().");
        }

        // Add the Tailwind link to the head tags
        var buildId = eQuantic.UI.Server.UIExtensions.BuildId;
        var linkTag = $"<link rel=\"stylesheet\" href=\"{cssPath}?v={buildId}\">";
        if (!options.HtmlShell.HeadTags.Any(t => t.StartsWith($"<link rel=\"stylesheet\" href=\"{cssPath}")))
        {
            options.HtmlShell.HeadTags.Add(linkTag);
        }
 
        // Inject AppTheme as JS service
        var theme = new eQuantic.UI.Tailwind.Theme.AppTheme();

        // Serialize theme methods as lookup dictionaries
        var themeData = new
        {
            button = new
            {
                @base = theme.Button.Base,
                variants = new Dictionary<string, string>
                {
                    ["primary"] = theme.Button.GetVariant(Core.Theme.Types.Variant.Primary),
                    ["secondary"] = theme.Button.GetVariant(Core.Theme.Types.Variant.Secondary),
                    ["outline"] = theme.Button.GetVariant(Core.Theme.Types.Variant.Outline),
                    ["ghost"] = theme.Button.GetVariant(Core.Theme.Types.Variant.Ghost),
                    ["destructive"] = theme.Button.GetVariant(Core.Theme.Types.Variant.Destructive),
                    ["link"] = theme.Button.GetVariant(Core.Theme.Types.Variant.Link),
                    ["success"] = theme.Button.GetVariant(Core.Theme.Types.Variant.Success),
                    ["warning"] = theme.Button.GetVariant(Core.Theme.Types.Variant.Warning),
                    ["info"] = theme.Button.GetVariant(Core.Theme.Types.Variant.Info)
                },
                sizes = new Dictionary<string, string>
                {
                    ["small"] = theme.Button.GetSize(Core.Theme.Types.Size.Small),
                    ["medium"] = theme.Button.GetSize(Core.Theme.Types.Size.Medium),
                    ["large"] = theme.Button.GetSize(Core.Theme.Types.Size.Large),
                    ["xlarge"] = theme.Button.GetSize(Core.Theme.Types.Size.XLarge)
                }
            },
            typography = new
            {
                @base = theme.Typography.Base
            }
            // TODO: Add other theme components (Input, Checkbox, etc.) as needed
        };

        var jsonOptions = new System.Text.Json.JsonSerializerOptions
        {
            PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase,
            WriteIndented = false
        };
        var themeJson = System.Text.Json.JsonSerializer.Serialize(themeData, jsonOptions);

        // Register theme with method wrappers to match C# interface
        var script = $@"
<script type=""module"">
    import {{ getRootServiceProvider }} from '/_equantic/runtime.js?v={buildId}';
    const themeData = {themeJson};

    // Add method wrappers to match IButtonTheme interface
    const theme = {{
        button: {{
            base: themeData.button.base,
            getVariant: (variant) => {{
                const key = typeof variant === 'string' ? variant : variant.toString();
                return themeData.button.variants[key.toLowerCase()] || themeData.button.variants.primary;
            }},
            getSize: (size) => {{
                const key = typeof size === 'string' ? size : size.toString();
                return themeData.button.sizes[key.toLowerCase()] || themeData.button.sizes.medium;
            }}
        }},
        typography: {{
            base: themeData.typography.base
        }}
    }};

    getRootServiceProvider().registerInstance('IAppTheme', theme);
    getRootServiceProvider().registerInstance('eQuantic.UI.Core.Theme.IAppTheme', theme);
</script>";
        options.HtmlShell.HeadTags.Add(script);
 
        return app;
    }

    /// <summary>
    /// Registers Tailwind CSS services.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection.</returns>
    public static IServiceCollection AddTailwind(this IServiceCollection services)
    {
        services.AddSingleton<eQuantic.UI.Core.Theme.IAppTheme, eQuantic.UI.Tailwind.Theme.AppTheme>();
        return services;
    }
}
