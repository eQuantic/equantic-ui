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
    /// </example>
    public ClassBuilder WithPrefix(string prefix, params string?[] classes)
    {
        foreach (var className in classes)
        {
            if (!string.IsNullOrWhiteSpace(className))
            {
                Add($"{prefix}{className}");
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
    /// Adds classes with the 'group-hover:' prefix.
    /// </summary>
    public ClassBuilder GroupHover(params string?[] classes) => WithPrefix("group-hover:", classes);

    /// <summary>
    /// Adds classes with the 'dark:' prefix for dark mode.
    /// </summary>
    public ClassBuilder Dark(params string?[] classes) => WithPrefix("dark:", classes);



    /// <summary>
    /// Adds responsive classes for the specified breakpoint.
    /// </summary>
    /// <param name="breakpoint">Breakpoint prefix (sm, md, lg, xl, 2xl)</param>
    /// <param name="classes">Classes to apply at this breakpoint</param>
    public ClassBuilder Responsive(string breakpoint, params string?[] classes)
        => WithPrefix($"{breakpoint}:", classes);

    /// <summary>
    /// Adds classes for the 'sm' breakpoint (640px+).
    /// </summary>
    public ClassBuilder Sm(params string?[] classes) => Responsive("sm", classes);

    /// <summary>
    /// Adds classes for the 'md' breakpoint (768px+).
    /// </summary>
    public ClassBuilder Md(params string?[] classes) => Responsive("md", classes);

    /// <summary>
    /// Adds classes for the 'lg' breakpoint (1024px+).
    /// </summary>
    public ClassBuilder Lg(params string?[] classes) => Responsive("lg", classes);

    /// <summary>
    /// Adds classes for the 'xl' breakpoint (1280px+).
    /// </summary>
    public ClassBuilder Xl(params string?[] classes) => Responsive("xl", classes);

    /// <summary>
    /// Adds classes for the '2xl' breakpoint (1536px+).
    /// </summary>
    public ClassBuilder Xl2(params string?[] classes) => Responsive("2xl", classes);

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
