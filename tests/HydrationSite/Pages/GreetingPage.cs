using eQuantic.UI.Primitives;
using HydrationSite.Resources;

namespace HydrationSite.Pages;

/// <summary>
/// A greeting cached in a static field the first time the page builds: the server keeps it in the language
/// of the first request it served, and the browser reads it in the page's. The build refuses the same
/// mistake written as a static initializer (EQ3107); a cache filled on first use is one it cannot see.
/// </summary>
[Page("/greeting")]
public sealed class GreetingPage : StatelessComponent
{
    private static string? _greeting;

    public override VisualNode Build(ComponentContext context) => Text(_greeting ??= Strings.Hello, TypeRole.Heading);
}
