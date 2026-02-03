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
/// Namespace containing Tailwind CSS utility classes organized by category.
/// Use ClassBuilder for conditional logic, state variants, and responsive utilities.
/// </summary>
/// <example>
/// // Simple concatenation with + operator
/// ClassName = TW.Flex.Row + TW.Items.Center + TW.Gap(4)
///
/// // Use ClassBuilder for complex scenarios
/// ClassName = ClassBuilder.Create()
///     .Add(TW.Flex.Row, TW.Items.Center)
///     .When(isActive, TW.Bg.Blue600, TW.Bg.Gray600)
///     .Dark(TW.Bg.Zinc900)
///     .Hover(TW.Scale(105))
///     .Build()
/// </example>
public static partial class TW
{
    /// <summary>
    /// Creates an empty Tailwind class.
    /// </summary>
    public static TailwindClass Empty => new(string.Empty);
}
