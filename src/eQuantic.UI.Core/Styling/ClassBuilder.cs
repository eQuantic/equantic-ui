#nullable enable

using System.Collections.Generic;
using System.Linq;

namespace eQuantic.UI.Core.Styling;

/// <summary>
/// Fluent builder for combining CSS classes.
/// Provides a clean, chainable API for building class strings.
/// </summary>
/// <example>
/// var classes = ClassBuilder.Create()
///     .Add("flex", "items-center")
///     .Add("gap-4", "p-6")
///     .When(isActive, "bg-blue-600", "text-white")
///     .Build();
/// </example>
public class ClassBuilder
{
    private readonly List<string> _classes = new();
    private readonly HashSet<string> _added = new();

    /// <summary>
    /// Creates a new empty ClassBuilder instance.
    /// </summary>
    public static ClassBuilder Create() => new();

    /// <summary>
    /// Creates a new ClassBuilder with initial classes.
    /// </summary>
    public static ClassBuilder Create(params string?[] initialClasses)
    {
        var builder = new ClassBuilder();
        builder.Add(initialClasses);
        return builder;
    }

    /// <summary>
    /// Adds one or more CSS classes.
    /// Duplicate classes are automatically ignored.
    /// </summary>
    public ClassBuilder Add(params string?[] classes)
    {
        foreach (var className in classes)
        {
            if (!string.IsNullOrWhiteSpace(className) && _added.Add(className))
            {
                _classes.Add(className);
            }
        }
        return this;
    }

    /// <summary>
    /// Conditionally adds classes if the condition is true.
    /// </summary>
    public ClassBuilder When(bool condition, params string?[] classes)
    {
        if (condition)
        {
            Add(classes);
        }
        return this;
    }

    /// <summary>
    /// Adds classes with a custom prefix.
    /// </summary>
    /// <example>
    /// .WithPrefix("hover:", "bg-gray-100", "scale-110") => "hover:bg-gray-100 hover:scale-110"
    /// .WithPrefix("dark:", "from-zinc-950 via-zinc-900") => "dark:from-zinc-950 dark:via-zinc-900"
    /// </example>
    public ClassBuilder WithPrefix(string prefix, params string?[] classes)
    {
        foreach (var className in classes)
        {
            if (!string.IsNullOrWhiteSpace(className))
            {
                // Split by whitespace to handle multi-class strings
                var individualClasses = className.Split(new[] { ' ', '\t', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
                foreach (var cls in individualClasses)
                {
                    Add($"{prefix}{cls}");
                }
            }
        }
        return this;
    }

    /// <summary>
    /// Adds classes with the 'hover:' prefix.
    /// </summary>
    public ClassBuilder Hover(params string?[] classes) => WithPrefix("hover:", classes);

    /// <summary>
    /// Adds classes with the 'focus:' prefix.
    /// </summary>
    public ClassBuilder Focus(params string?[] classes) => WithPrefix("focus:", classes);

    /// <summary>
    /// Adds classes with the 'active:' prefix.
    /// </summary>
    public ClassBuilder Active(params string?[] classes) => WithPrefix("active:", classes);

    /// <summary>
    /// Adds classes with the 'focus-within:' prefix.
    /// </summary>
    public ClassBuilder FocusWithin(params string?[] classes) => WithPrefix("focus-within:", classes);

    /// <summary>
    /// Adds classes with the 'placeholder:' prefix.
    /// </summary>
    public ClassBuilder Placeholder(params string?[] classes) => WithPrefix("placeholder:", classes);

    /// <summary>
    /// Adds classes with the 'visited:' prefix.
    /// </summary>
    public ClassBuilder Visited(params string?[] classes) => WithPrefix("visited:", classes);

    /// <summary>
    /// Adds classes with the 'disabled:' prefix.
    /// </summary>
    public ClassBuilder Disabled(params string?[] classes) => WithPrefix("disabled:", classes);

    /// <summary>
    /// Adds classes with the 'odd:' prefix.
    /// </summary>
    public ClassBuilder Odd(params string?[] classes) => WithPrefix("odd:", classes);

    /// <summary>
    /// Adds classes with the 'even:' prefix.
    /// </summary>
    public ClassBuilder Even(params string?[] classes) => WithPrefix("even:", classes);

    /// <summary>
    /// Adds classes with the 'first:' prefix.
    /// </summary>
    public ClassBuilder First(params string?[] classes) => WithPrefix("first:", classes);

    /// <summary>
    /// Adds classes with the 'last:' prefix.
    /// </summary>
    public ClassBuilder Last(params string?[] classes) => WithPrefix("last:", classes);

    /// <summary>
    /// Adds classes with the 'before:' prefix.
    /// </summary>
    public ClassBuilder Before(params string?[] classes) => WithPrefix("before:", classes);

    /// <summary>
    /// Adds classes with the 'after:' prefix.
    /// </summary>
    public ClassBuilder After(params string?[] classes) => WithPrefix("after:", classes);

    /// <summary>
    /// Adds classes with the 'group-hover:' prefix.
    /// </summary>
    public ClassBuilder GroupHover(params string?[] classes) => WithPrefix("group-hover:", classes);

    /// <summary>
    /// Adds classes with the 'dark:' prefix for dark mode.
    /// </summary>
    public ClassBuilder Dark(params string?[] classes) => WithPrefix("dark:", classes);

    /// <summary>
    /// Adds classes with the 'sm:' prefix.
    /// </summary>
    public ClassBuilder Sm(params string?[] classes) => WithPrefix("sm:", classes);

    /// <summary>
    /// Adds classes with the 'md:' prefix.
    /// </summary>
    public ClassBuilder Md(params string?[] classes) => WithPrefix("md:", classes);

    /// <summary>
    /// Adds classes with the 'lg:' prefix.
    /// </summary>
    public ClassBuilder Lg(params string?[] classes) => WithPrefix("lg:", classes);

    /// <summary>
    /// Adds classes with the 'xl:' prefix.
    /// </summary>
    public ClassBuilder Xl(params string?[] classes) => WithPrefix("xl:", classes);

    /// <summary>
    /// Adds classes with the '2xl:' prefix.
    /// </summary>
    public ClassBuilder Xl2(params string?[] classes) => WithPrefix("2xl:", classes);

    /// <summary>
    /// Builds the final class string by joining all classes with spaces.
    /// </summary>
    public string Build()
    {
        return string.Join(" ", _classes);
    }

    /// <summary>
    /// Implicit conversion to string.
    /// </summary>
    public static implicit operator string(ClassBuilder builder) => builder.Build();

    /// <summary>
    /// Returns the built class string.
    /// </summary>
    public override string ToString() => Build();
}

/// <summary>
/// Extension methods for ClassBuilder.
/// </summary>
public static class ClassBuilderExtensions
{
    /// <summary>
    /// Joins multiple class strings with spaces, filtering out null or empty values.
    /// </summary>
    public static string JoinClasses(params string?[] classes)
    {
        return string.Join(" ", classes.Where(c => !string.IsNullOrWhiteSpace(c)));
    }

    /// <summary>
    /// Conditionally returns a class string.
    /// </summary>
    public static string? WhenClass(bool condition, string? className, string? fallback = null)
    {
        return condition ? className : fallback;
    }
}
