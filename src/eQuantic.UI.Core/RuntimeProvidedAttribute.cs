namespace eQuantic.UI.Core;

/// <summary>
/// Marks a CORE type whose client half ships INSIDE the runtime bundle: eqc routes references to an
/// <c>@equantic/runtime</c> import instead of transpiling the C# source (the compiler's scanner
/// matches this attribute BY NAME, so this Core-local twin of
/// <c>eQuantic.UI.Primitives.RuntimeProvidedAttribute</c> needs no cross-reference).
/// </summary>
[AttributeUsage(AttributeTargets.Class)]
public sealed class RuntimeProvidedAttribute : Attribute
{
}
