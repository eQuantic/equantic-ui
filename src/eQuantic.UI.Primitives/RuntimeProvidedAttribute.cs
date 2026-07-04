namespace eQuantic.UI.Primitives;

/// <summary>
/// Marks a type the BROWSER RUNTIME provides (an export of <c>@equantic/runtime</c>): eqc imports it
/// from the runtime package instead of emitting/expecting a per-app module. Types in the
/// <c>eQuantic.UI.Primitives</c> namespace get this routing implicitly (the shared vocabulary);
/// the attribute extends it to runtime-backed types living elsewhere — e.g. the web adapter
/// <c>VisualNodeComponent</c>. The TS export must carry the SAME name.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Enum, Inherited = false)]
public sealed class RuntimeProvidedAttribute : Attribute
{
}
