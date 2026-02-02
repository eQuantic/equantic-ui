#nullable enable

using eQuantic.UI.Core.Styling;

namespace eQuantic.UI.Tailwind;

/// <summary>
/// Extension to TW class for creating builders.
/// Uses the core ClassBuilder directly without unnecessary inheritance.
/// </summary>
public static partial class TW
{
    /// <summary>
    /// Creates a new ClassBuilder instance for fluent Tailwind class composition.
    /// </summary>
    /// <example>
    /// var classes = TW.Build()
    ///     .Add(TW.Display.Flex, TW.Gap(4))
    ///     .Hover(TW.Bg.Blue100)
    ///     .Dark(TW.Bg.Zinc900)
    ///     .Build();
    /// </example>
    public static ClassBuilder Build() => ClassBuilder.Create();

    /// <summary>
    /// Creates a new ClassBuilder with initial Tailwind classes.
    /// </summary>
    public static ClassBuilder Build(params string?[] initialClasses)
        => ClassBuilder.Create(initialClasses);
}
