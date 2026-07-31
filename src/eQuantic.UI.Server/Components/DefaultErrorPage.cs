using eQuantic.UI.Core;

namespace eQuantic.UI.Server.Components;

/// <summary>Default 500 page — Core-only markup (DynamicElement + inline styles), no component library.</summary>
[Page("/500", Title = "500 - Server Error", DisableSsr = false)]
public class DefaultErrorPage : StatelessComponent
{
    public override IComponent Build(RenderContext context) => ErrorShell.Build(
        "500", "Something went wrong", "The server hit an unexpected error. Try again in a moment.");
}

/// <summary>The shared error-page shell: a centered column, styled inline — self-contained (no
/// stylesheet dependency, so it renders correctly even when the app ships no CSS at all).</summary>
internal static class ErrorShell
{
    public static IComponent Build(string code, string title, string body)
    {
        var shell = new DynamicElement
        {
            TagName = "div",
            CustomAttributes = new Dictionary<string, string>
            {
                ["style"] = "min-height:100vh;display:flex;flex-direction:column;align-items:center;" +
                            "justify-content:center;gap:8px;font-family:system-ui,sans-serif;",
            },
        };
        shell.Children.Add(new DynamicElement
        {
            TagName = "div",
            InnerText = code,
            CustomAttributes = new Dictionary<string, string> { ["style"] = "font-size:64px;font-weight:800;opacity:.35;" },
        });
        shell.Children.Add(new DynamicElement
        {
            TagName = "h1",
            InnerText = title,
            CustomAttributes = new Dictionary<string, string> { ["style"] = "margin:0;font-size:24px;" },
        });
        shell.Children.Add(new DynamicElement
        {
            TagName = "p",
            InnerText = body,
            CustomAttributes = new Dictionary<string, string> { ["style"] = "margin:0;opacity:.7;" },
        });
        shell.Children.Add(new DynamicElement
        {
            TagName = "a",
            InnerText = "Go home",
            CustomAttributes = new Dictionary<string, string>
            {
                ["href"] = "/",
                ["style"] = "margin-top:16px;text-decoration:underline;",
            },
        });
        return shell;
    }
}
