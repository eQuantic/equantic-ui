using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using eQuantic.UI.Server;

namespace eQuantic.UI.Tailwind;

/// <summary>
/// Extension methods for enabling Tailwind CSS in eQuantic.UI.
/// </summary>
public static class TailwindExtensions
{
    private static string? _cachedThemeScript;
    private static string? _cachedDarkModeScript;

    /// <summary>
    /// Enables Tailwind CSS integration by registering dynamic script endpoints.
    /// Scripts are loaded from embedded resources and cached.
    /// </summary>
    /// <param name="app">The web application.</param>
    /// <param name="cssPath">The path to the generated CSS file. Defaults to "/css/app.css".</param>
    /// <returns>The web application.</returns>
    public static WebApplication UseTailwind(this WebApplication app, string cssPath = "/css/app.css")
    {
        var options = app.Services.GetService<UIOptions>();
        
        if (options == null)
        {
            throw new InvalidOperationException("UIOptions not found. Ensure AddUI() is called before UseTailwind().");
        }

        // Disable default CSS injection since we use Tailwind
        options.EnableDefaultCss = false;

        // Add the Tailwind CSS link
        var buildId = UIExtensions.BuildId;
        var linkTag = $"<link rel=\"stylesheet\" href=\"{cssPath}?v={buildId}\">";
        if (!options.HtmlShell.HeadTags.Any(t => t.StartsWith($"<link rel=\"stylesheet\" href=\"{cssPath}")))
        {
            options.HtmlShell.HeadTags.Add(linkTag);
        }

        // Generate and cache theme script from embedded resource
        if (_cachedThemeScript == null)
        {
            var theme = new Theme.AppTheme();
            var themeJson = SerializeThemeData(theme);
            
            var themeTemplate = HtmlTemplateEngine.FromResource(
                "eQuantic.UI.Tailwind.Scripts.theme.js",
                typeof(TailwindExtensions).Assembly);
            
            _cachedThemeScript = themeTemplate.Render(new Dictionary<string, string>
            {
                ["themeJson"] = themeJson
            });
        }

        // Cache dark mode script from embedded resource
        if (_cachedDarkModeScript == null)
        {
            var darkModeTemplate = HtmlTemplateEngine.FromResource(
                "eQuantic.UI.Tailwind.Scripts.dark-mode.js",
                typeof(TailwindExtensions).Assembly);
            
            _cachedDarkModeScript = darkModeTemplate.Render(new Dictionary<string, string>());
        }

        // Register dynamic script endpoints
        var themeScript = _cachedThemeScript;
        var darkModeScript = _cachedDarkModeScript;
        
        app.MapScriptJs("/_equantic/theme.js", () => themeScript);
        app.MapScriptJs("/_equantic/dark-mode.js", () => darkModeScript);

        // Add script references to head
        options.HtmlShell.HeadTags.Add($"<script src=\"/_equantic/theme.js?v={buildId}\"></script>");
        options.HtmlShell.HeadTags.Add($"<script src=\"/_equantic/dark-mode.js?v={buildId}\"></script>");

        return app;
    }

    /// <summary>
    /// Registers Tailwind CSS services and component themes.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection.</returns>
    public static IServiceCollection AddTailwind(this IServiceCollection services)
    {
        // Register main theme
        services.AddSingleton<eQuantic.UI.Core.Theme.IAppTheme, Theme.AppTheme>();

        // Register color theme (required by other themes)
        services.AddSingleton<eQuantic.UI.Core.Theme.IColorTheme, Theme.ColorTheme>();

        // Register individual component themes
        services.AddSingleton<eQuantic.UI.Core.Theme.IButtonTheme, Theme.ButtonTheme>();
        services.AddSingleton<eQuantic.UI.Core.Theme.ICardTheme, Theme.CardTheme>();
        services.AddSingleton<eQuantic.UI.Core.Theme.IInputTheme, Theme.InputTheme>();
        services.AddSingleton<eQuantic.UI.Core.Theme.ICheckboxTheme, Theme.CheckboxTheme>();
        services.AddSingleton<eQuantic.UI.Core.Theme.IBadgeTheme, Theme.BadgeTheme>();
        services.AddSingleton<eQuantic.UI.Core.Theme.IAlertTheme, Theme.AlertTheme>();
        services.AddSingleton<eQuantic.UI.Core.Theme.ITextTheme, Theme.TextTheme>();

        return services;
    }

    #region Theme Serialization

    private static string SerializeThemeData(Theme.AppTheme theme)
    {
        var themeData = new
        {
            button = new
            {
                @base = theme.Button.Base,
                variants = new Dictionary<string, string>
                {
                    ["default"] = theme.Button.GetVariant(Core.Theme.Types.Variant.Default),
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
                @base = theme.Typography.Base,
                variants = new Dictionary<string, string>
                {
                    ["default"] = theme.Typography.GetVariant(Core.Theme.Types.Variant.Default),
                    ["primary"] = theme.Typography.GetVariant(Core.Theme.Types.Variant.Primary),
                    ["secondary"] = theme.Typography.GetVariant(Core.Theme.Types.Variant.Secondary),
                    ["ghost"] = theme.Typography.GetVariant(Core.Theme.Types.Variant.Ghost),
                    ["custom"] = theme.Typography.GetVariant(Core.Theme.Types.Variant.Custom)
                },
                headings = new Dictionary<string, string>
                {
                    ["h1"] = theme.Typography.GetHeading(1),
                    ["h2"] = theme.Typography.GetHeading(2),
                    ["h3"] = theme.Typography.GetHeading(3),
                    ["h4"] = theme.Typography.GetHeading(4),
                    ["h5"] = theme.Typography.GetHeading(5),
                    ["h6"] = theme.Typography.GetHeading(6)
                }
            },
            card = new
            {
                container = theme.Card.Container,
                header = theme.Card.Header,
                body = theme.Card.Body,
                footer = theme.Card.Footer,
                title = theme.Card.Title,
                description = theme.Card.Description,
                variants = new Dictionary<string, string>
                {
                    ["default"] = theme.Card.GetVariant(Core.Theme.Types.CardVariant.Default),
                    ["outline"] = theme.Card.GetVariant(Core.Theme.Types.CardVariant.Outline),
                    ["elevated"] = theme.Card.GetVariant(Core.Theme.Types.CardVariant.Elevated),
                    ["subtle"] = theme.Card.GetVariant(Core.Theme.Types.CardVariant.Subtle),
                    ["ghost"] = theme.Card.GetVariant(Core.Theme.Types.CardVariant.Ghost)
                },
                shadows = theme.Card.Shadows
            },
            input = new
            {
                @base = theme.Input.Base,
                variants = new Dictionary<string, string>
                {
                    ["success"] = theme.Input.GetVariant(Core.Theme.Types.Variant.Success),
                    ["warning"] = theme.Input.GetVariant(Core.Theme.Types.Variant.Warning),
                    ["destructive"] = theme.Input.GetVariant(Core.Theme.Types.Variant.Destructive),
                    ["ghost"] = theme.Input.GetVariant(Core.Theme.Types.Variant.Ghost)
                },
                sizes = new Dictionary<string, string>
                {
                    ["small"] = theme.Input.GetSize(Core.Theme.Types.Size.Small),
                    ["medium"] = theme.Input.GetSize(Core.Theme.Types.Size.Medium),
                    ["large"] = theme.Input.GetSize(Core.Theme.Types.Size.Large)
                }
            },
            checkbox = new
            {
                @base = theme.Checkbox.Base,
                root = theme.Checkbox.Root,
                indicator = theme.Checkbox.Indicator,
                @checked = theme.Checkbox.Checked,
                @unchecked = theme.Checkbox.Unchecked
            },
            badge = new
            {
                @base = theme.Badge.Base,
                variants = new Dictionary<string, string>
                {
                    ["default"] = theme.Badge.GetVariant(Core.Theme.Types.Variant.Default),
                    ["primary"] = theme.Badge.GetVariant(Core.Theme.Types.Variant.Primary),
                    ["secondary"] = theme.Badge.GetVariant(Core.Theme.Types.Variant.Secondary),
                    ["outline"] = theme.Badge.GetVariant(Core.Theme.Types.Variant.Outline),
                    ["destructive"] = theme.Badge.GetVariant(Core.Theme.Types.Variant.Destructive),
                    ["success"] = theme.Badge.GetVariant(Core.Theme.Types.Variant.Success),
                    ["warning"] = theme.Badge.GetVariant(Core.Theme.Types.Variant.Warning),
                    ["info"] = theme.Badge.GetVariant(Core.Theme.Types.Variant.Info)
                }
            },
            alert = new
            {
                @base = theme.Alert.Base,
                title = theme.Alert.Title,
                description = theme.Alert.Description,
                icon = theme.Alert.Icon,
                variants = new Dictionary<string, string>
                {
                    ["default"] = theme.Alert.GetVariant(Core.Theme.Types.Variant.Default),
                    ["destructive"] = theme.Alert.GetVariant(Core.Theme.Types.Variant.Destructive),
                    ["success"] = theme.Alert.GetVariant(Core.Theme.Types.Variant.Success),
                    ["warning"] = theme.Alert.GetVariant(Core.Theme.Types.Variant.Warning),
                    ["info"] = theme.Alert.GetVariant(Core.Theme.Types.Variant.Info)
                }
            }
        };

        var jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false
        };

        return JsonSerializer.Serialize(themeData, jsonOptions);
    }

    #endregion
}
