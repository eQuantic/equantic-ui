using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
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

        ConfigureTailwind(app, options, cssPath);
        return app;
    }

    /// <summary>
    /// Enables Tailwind CSS integration via UIOptions fluent API.
    /// Registers services and endpoint mappings.
    /// </summary>
    public static UIOptions UseTailwind(this UIOptions options, string cssPath = "/css/app.css")
    {
        options.RegisterServices(services => services.AddTailwind());
        options.RegisterEndpoints(endpoints => ConfigureTailwind(endpoints, options, cssPath));
        return options;
    }

    /// <summary>
    /// Encapsulates the Tailwind dark mode script injection.
    /// This resolves the dark mode preference on page load (checking localStorage and OS theme)
    /// to prevent FOUC (Flash of Unstyled Content).
    /// </summary>
    public static HtmlShellOptions EnableTailwindDarkMode(this HtmlShellOptions shell)
    {
        shell.AddHeadTag("<script>!function(){var t=localStorage.getItem('theme');var d=t?t==='dark':window.matchMedia('(prefers-color-scheme: dark)').matches;d?document.documentElement.classList.add('dark'):document.documentElement.classList.remove('dark');document.addEventListener('DOMContentLoaded',function(){var s=document.getElementById('icon-sun');var m=document.getElementById('icon-moon');if(s&&m){var dk=document.documentElement.classList.contains('dark');s.classList.toggle('hidden',!dk);m.classList.toggle('hidden',dk)}})}();</script>");
        return shell;
    }

    private static void ConfigureTailwind(IEndpointRouteBuilder endpoints, UIOptions options, string cssPath)
    {
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
        
        endpoints.MapScriptJs("/_equantic/theme.js", () => themeScript);
        endpoints.MapScriptJs("/_equantic/dark-mode.js", () => darkModeScript);

        // Add script references to head
        if (!options.HtmlShell.HeadTags.Any(t => t.Contains("/_equantic/theme.js")))
        {
            options.HtmlShell.HeadTags.Add($"<script src=\"/_equantic/theme.js?v={buildId}\"></script>");
        }
        if (!options.HtmlShell.HeadTags.Any(t => t.Contains("/_equantic/dark-mode.js")))
        {
            options.HtmlShell.HeadTags.Add($"<script src=\"/_equantic/dark-mode.js?v={buildId}\"></script>");
        }
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
                    ["small"] = theme.Button.GetSize(Core.Theme.Types.SizeVariant.Small),
                    ["medium"] = theme.Button.GetSize(Core.Theme.Types.SizeVariant.Medium),
                    ["large"] = theme.Button.GetSize(Core.Theme.Types.SizeVariant.Large),
                    ["xlarge"] = theme.Button.GetSize(Core.Theme.Types.SizeVariant.XLarge)
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
                    ["small"] = theme.Input.GetSize(Core.Theme.Types.SizeVariant.Small),
                    ["medium"] = theme.Input.GetSize(Core.Theme.Types.SizeVariant.Medium),
                    ["large"] = theme.Input.GetSize(Core.Theme.Types.SizeVariant.Large)
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
            },
            dialog = new
            {
                overlay = theme.Dialog.Overlay,
                content = theme.Dialog.Content,
                header = theme.Dialog.Header,
                title = theme.Dialog.Title,
                description = theme.Dialog.Description,
                footer = theme.Dialog.Footer
            },
            table = new
            {
                wrapper = theme.Table.Wrapper,
                table = theme.Table.Table,
                header = theme.Table.Header,
                row = theme.Table.Row,
                headCell = theme.Table.HeadCell,
                cell = theme.Table.Cell
            },
            tabs = new
            {
                list = theme.Tabs.List,
                trigger = theme.Tabs.Trigger,
                content = theme.Tabs.Content,
                activeTrigger = theme.Tabs.ActiveTrigger,
                inactiveTrigger = theme.Tabs.InactiveTrigger
            },
            avatar = new
            {
                root = theme.Avatar.Root,
                image = theme.Avatar.Image,
                fallback = theme.Avatar.Fallback,
                sizes = new Dictionary<string, string>
                {
                    ["small"] = theme.Avatar.GetSize(Core.Theme.Types.SizeVariant.Small),
                    ["medium"] = theme.Avatar.GetSize(Core.Theme.Types.SizeVariant.Medium),
                    ["large"] = theme.Avatar.GetSize(Core.Theme.Types.SizeVariant.Large),
                    ["xlarge"] = theme.Avatar.GetSize(Core.Theme.Types.SizeVariant.XLarge)
                }
            },
            @switch = new
            {
                root = theme.Switch.Root,
                input = theme.Switch.Input,
                thumb = theme.Switch.Thumb,
                track = theme.Switch.Track
            },
            select = new
            {
                trigger = theme.Select.Trigger,
                content = theme.Select.Content,
                item = theme.Select.Item,
                @base = theme.Select.Base
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
