#nullable enable

using System;

namespace eQuantic.UI.Core.Styling;

/// <summary>
/// Marks a type whose instances should be evaluated at compile-time by the eQuantic.UI compiler.
/// This is typically used for CSS class builders and similar constructs that produce constant strings.
/// </summary>
/// <remarks>
/// When the compiler encounters expressions involving types marked with this attribute,
/// it will evaluate them during compilation and emit the resulting constant string value
/// instead of transpiling the expression to JavaScript.
///
/// Requirements for types marked with this attribute:
/// - Must have an implicit or explicit conversion to string
/// - All operations must be deterministic and side-effect free
/// - Should produce constant or compile-time computable values
/// </remarks>
/// <example>
/// <code>
/// [CompileTimeEvaluate]
/// public readonly struct TailwindClass
/// {
///     private readonly string _value;
///
///     public TailwindClass(string value) => _value = value;
///
///     public static implicit operator string(TailwindClass tw) => tw._value;
///
///     public static TailwindClass operator +(TailwindClass left, TailwindClass right)
///         => new($"{left._value} {right._value}");
/// }
///
/// // Usage in component:
/// ClassName = TW.Display.Flex + TW.Gap(4) + TW.P(6)
///
/// // Compiler output (TypeScript):
/// className: "flex gap-4 p-6"  // ✅ Evaluated at compile-time
/// </code>
/// </example>
[AttributeUsage(AttributeTargets.Struct | AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
public sealed class CompileTimeEvaluateAttribute : Attribute
{
    /// <summary>
    /// Initializes a new instance of the CompileTimeEvaluateAttribute.
    /// </summary>
    public CompileTimeEvaluateAttribute()
    {
    }

    /// <summary>
    /// Gets or sets a value indicating whether the compiler should warn if evaluation fails.
    /// Default is true.
    /// </summary>
    public bool WarnOnEvaluationFailure { get; set; } = true;

    /// <summary>
    /// Gets or sets the fallback behavior when compile-time evaluation is not possible.
    /// Default is "EmitRuntimeCode" which transpiles the expression to JavaScript.
    /// Other options: "Error" (fail compilation), "EmitNull" (emit null/empty string).
    /// </summary>
    public string FallbackBehavior { get; set; } = "EmitRuntimeCode";
}
