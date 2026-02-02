#nullable enable

using eQuantic.UI.Core.Styling;

namespace eQuantic.UI.Tailwind;

/// <summary>
/// Represents a Tailwind CSS class with type safety and implicit string conversion.
/// Supports combining multiple classes with the + operator.
/// </summary>
[CompileTimeEvaluate]
public readonly struct TailwindClass
{
    private readonly string _value;

    public TailwindClass(string value)
    {
        _value = value ?? string.Empty;
    }

    /// <summary>
    /// Implicit conversion to string for easy assignment to ClassName properties.
    /// </summary>
    public static implicit operator string(TailwindClass tw) => tw._value;

    /// <summary>
    /// Implicit conversion from string (useful for raw strings).
    /// </summary>
    public static implicit operator TailwindClass(string value) => new(value);

    /// <summary>
    /// Combines two Tailwind classes with a space separator.
    /// </summary>
    public static TailwindClass operator +(TailwindClass left, TailwindClass right)
    {
        if (string.IsNullOrEmpty(left._value)) return right;
        if (string.IsNullOrEmpty(right._value)) return left;
        return new TailwindClass($"{left._value} {right._value}");
    }

    /// <summary>
    /// Combines a Tailwind class with a raw string.
    /// </summary>
    public static TailwindClass operator +(TailwindClass left, string right)
    {
        if (string.IsNullOrEmpty(left._value)) return new TailwindClass(right);
        if (string.IsNullOrEmpty(right)) return left;
        return new TailwindClass($"{left._value} {right}");
    }

    /// <summary>
    /// Combines a raw string with a Tailwind class.
    /// </summary>
    public static TailwindClass operator +(string left, TailwindClass right)
    {
        if (string.IsNullOrEmpty(left)) return right;
        if (string.IsNullOrEmpty(right._value)) return new TailwindClass(left);
        return new TailwindClass($"{left} {right._value}");
    }

    public override string ToString() => _value;

    public override bool Equals(object? obj) => obj is TailwindClass other && _value == other._value;
    public override int GetHashCode() => _value.GetHashCode();

    public static bool operator ==(TailwindClass left, TailwindClass right) => left.Equals(right);
    public static bool operator !=(TailwindClass left, TailwindClass right) => !left.Equals(right);
}

/// <summary>
/// Conditional Tailwind class that only includes the class if the condition is true.
/// </summary>
[CompileTimeEvaluate]
public readonly struct ConditionalTailwindClass
{
    private readonly string _value;
    private readonly bool _condition;

    public ConditionalTailwindClass(bool condition, string value)
    {
        _condition = condition;
        _value = value ?? string.Empty;
    }

    /// <summary>
    /// Implicit conversion to TailwindClass - only includes value if condition is true.
    /// </summary>
    public static implicit operator TailwindClass(ConditionalTailwindClass conditional)
        => new(conditional._condition ? conditional._value : string.Empty);

    /// <summary>
    /// Implicit conversion to string.
    /// </summary>
    public static implicit operator string(ConditionalTailwindClass conditional)
        => conditional._condition ? conditional._value : string.Empty;
}

/// <summary>
/// Helper methods for creating Tailwind classes.
/// </summary>
public static partial class TW
{
    /// <summary>
    /// Creates an empty Tailwind class.
    /// </summary>
    public static TailwindClass Empty => new(string.Empty);

    /// <summary>
    /// Conditionally includes a class if the condition is true.
    /// </summary>
    /// <example>
    /// ClassName = TW.Flex.Row + TW.When(isActive, TW.Bg.Blue600)
    /// </example>
    public static ConditionalTailwindClass When(bool condition, TailwindClass className)
        => new(condition, className);

    /// <summary>
    /// Conditionally includes a class if the condition is true, otherwise uses fallback.
    /// </summary>
    public static TailwindClass When(bool condition, TailwindClass ifTrue, TailwindClass ifFalse)
        => condition ? ifTrue : ifFalse;

    /// <summary>
    /// Joins multiple Tailwind classes with spaces.
    /// </summary>
    public static TailwindClass Join(params TailwindClass[] classes)
    {
        var result = Empty;
        foreach (var c in classes)
        {
            result += c;
        }
        return result;
    }

    /// <summary>
    /// Adds a dark mode variant to the class.
    /// </summary>
    /// <example>
    /// TW.Dark(TW.Bg.Zinc900) => "dark:bg-zinc-900"
    /// </example>
    public static TailwindClass Dark(TailwindClass className)
        => new($"dark:{className}");

    /// <summary>
    /// Adds a hover variant to the class.
    /// </summary>
    public static TailwindClass Hover(TailwindClass className)
        => new($"hover:{className}");

    /// <summary>
    /// Adds a focus variant to the class.
    /// </summary>
    public static TailwindClass Focus(TailwindClass className)
        => new($"focus:{className}");

    /// <summary>
    /// Adds an active variant to the class.
    /// </summary>
    public static TailwindClass Active(TailwindClass className)
        => new($"active:{className}");

    /// <summary>
    /// Adds a group-hover variant to the class.
    /// </summary>
    public static TailwindClass GroupHover(TailwindClass className)
        => new($"group-hover:{className}");

    /// <summary>
    /// Adds a responsive breakpoint variant to the class.
    /// </summary>
    /// <param name="breakpoint">sm, md, lg, xl, or 2xl</param>
    public static TailwindClass Responsive(string breakpoint, TailwindClass className)
        => new($"{breakpoint}:{className}");

    /// <summary>
    /// Adds a responsive sm: breakpoint variant.
    /// </summary>
    public static TailwindClass Sm(TailwindClass className) => Responsive("sm", className);

    /// <summary>
    /// Adds a responsive md: breakpoint variant.
    /// </summary>
    public static TailwindClass Md(TailwindClass className) => Responsive("md", className);

    /// <summary>
    /// Adds a responsive lg: breakpoint variant.
    /// </summary>
    public static TailwindClass Lg(TailwindClass className) => Responsive("lg", className);

    /// <summary>
    /// Adds a responsive xl: breakpoint variant.
    /// </summary>
    public static TailwindClass Xl(TailwindClass className) => Responsive("xl", className);

    /// <summary>
    /// Adds a responsive 2xl: breakpoint variant.
    /// </summary>
    public static TailwindClass Xl2(TailwindClass className) => Responsive("2xl", className);
}
