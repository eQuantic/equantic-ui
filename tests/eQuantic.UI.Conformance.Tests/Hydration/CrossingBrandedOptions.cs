namespace eQuantic.UI.Conformance.Tests.Hydration;

/// <summary>Hides its base's title with one of its own, which C# reading a CrossingBaseOptions never binds.</summary>
public sealed class CrossingBrandedOptions : CrossingBaseOptions
{
    public new string Title { get; set; } = "";
}
