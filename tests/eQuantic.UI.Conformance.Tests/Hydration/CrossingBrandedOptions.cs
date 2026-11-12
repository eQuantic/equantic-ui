namespace eQuantic.UI.Conformance.Tests.Hydration;

/// <summary>
/// Hides its base's title with one of its own, which C# reading a CrossingBaseOptions never binds. The
/// server hands it as a CrossingBaseOptions and the browser builds only that, so it never crosses: a twin of
/// it would hold both titles under one key, which the build refuses (EQ1007, #1173).
/// </summary>
[eQuantic.UI.Primitives.ServerOnly]
public sealed class CrossingBrandedOptions : CrossingBaseOptions
{
    public new string Title { get; set; } = "";
}
