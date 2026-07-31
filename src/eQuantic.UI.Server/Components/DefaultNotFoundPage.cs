using eQuantic.UI.Core;

namespace eQuantic.UI.Server.Components;

/// <summary>Default 404 page — Core-only markup (DynamicElement + inline styles), no component library.</summary>
[Page("/404", Title = "404 - Page Not Found", DisableSsr = false)]
public class DefaultNotFoundPage : StatelessComponent
{
    public override IComponent Build(RenderContext context) => ErrorShell.Build(
        "404", "Page not found", "The address you followed does not exist.");
}
