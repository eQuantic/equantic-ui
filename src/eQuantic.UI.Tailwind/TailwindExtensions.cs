using System;
using System.Linq;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using eQuantic.UI.Server;

namespace eQuantic.UI.Tailwind;

/// <summary>
/// Tailwind CSS integration: the package's job is the BUILD-TIME CSS generation (embedded Bun runs
/// the Tailwind CLI — see the .targets); these extensions only wire the generated stylesheet into
/// the HTML shell. Styling components remains the write-once theme's job (UseTheme) — Tailwind is
/// the utility-CSS path for app-authored markup.
/// </summary>
public static class TailwindExtensions
{
    /// <summary>Adds the generated Tailwind stylesheet link to the shell.</summary>
    public static WebApplication UseTailwind(this WebApplication app, string cssPath = "/css/app.css")
    {
        var options = app.Services.GetService<UIOptions>();
        if (options == null)
        {
            throw new InvalidOperationException("UIOptions not found. Ensure AddUI() is called before UseTailwind().");
        }

        AddStylesheetLink(options, cssPath);
        return app;
    }

    /// <summary>Adds the generated Tailwind stylesheet link via the UIOptions fluent API.</summary>
    public static UIOptions UseTailwind(this UIOptions options, string cssPath = "/css/app.css")
    {
        AddStylesheetLink(options, cssPath);
        return options;
    }

    /// <summary>
    /// Injects the dark-mode boot script (localStorage + OS preference, applied before first paint
    /// to prevent FOUC) for Tailwind's <c>dark:</c> variant strategy.
    /// </summary>
    public static HtmlShellOptions EnableTailwindDarkMode(this HtmlShellOptions shell)
    {
        shell.AddHeadTag(HtmlTag.InlineScript(
            "!function(){var t=localStorage.getItem('theme');var d=t?t==='dark':window.matchMedia('(prefers-color-scheme: dark)').matches;d?document.documentElement.classList.add('dark'):document.documentElement.classList.remove('dark');document.addEventListener('DOMContentLoaded',function(){var s=document.getElementById('icon-sun');var m=document.getElementById('icon-moon');if(s&&m){var dk=document.documentElement.classList.contains('dark');s.classList.toggle('hidden',!dk);m.classList.toggle('hidden',dk)}})}();", defer: false));
        return shell;
    }

    private static void AddStylesheetLink(UIOptions options, string cssPath)
    {
        var linkTag = HtmlTag.Link("stylesheet", $"{cssPath}?v={UIExtensions.BuildId}").Render();
        if (!options.HtmlShell.HeadTags.Any(t => t.StartsWith($"<link rel=\"stylesheet\" href=\"{cssPath}")))
        {
            options.HtmlShell.HeadTags.Add(linkTag);
        }
    }
}
