namespace eQuantic.UI.Primitives;

/// <summary>
/// ELECTS the constructor the generated factory surface mirrors for this component.
/// <para>
/// A component gets one factory, because the factory transpiles to a JS twin and JS methods do not
/// overload — the same reason the emitter collapses constructor overloads to the widest. The widest
/// is therefore the default, and it is right whenever the extra parameters are optional. Mark a
/// constructor with this when it is not: the one the DESIGN considers the way to build the thing,
/// rather than the one that happens to take the most arguments.
/// </para>
/// <para>
/// Marking two constructors of the same type is an error the generator reports (EQ3101) — an
/// election with two winners has no answer, and picking one silently is how a component ends up
/// built the wrong way in a build nobody suspected.
/// </para>
/// </summary>
[AttributeUsage(AttributeTargets.Constructor, Inherited = false, AllowMultiple = false)]
public sealed class UiFactoryAttribute : Attribute
{
}
