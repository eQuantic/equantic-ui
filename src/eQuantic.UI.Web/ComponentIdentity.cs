namespace eQuantic.UI.Web;

/// <summary>
/// What a component is CALLED at the seam between the server and the browser: the name both sides
/// key its hydration state by, <c>Identity#ordinal</c>.
/// <para>
/// The FULL name, as the CLR spells a type definition: <c>MyApp.Pages.Row</c>, a nested type's
/// containing types joined by <c>+</c>, a generic definition's arity after a backtick. eqc writes
/// the same string onto every component twin as <c>static $typeId</c>, and the runtime keys by that.
/// It used to be the SIMPLE name on both sides, so two components called <c>Row</c> from different
/// namespaces were indistinguishable to the one check that makes a drift between the two trees safe
/// (#278); and in the browser it came from <c>constructor.name</c>, which held only because no
/// bundler minified identifiers. A string the compiler writes survives any bundler.
/// </para>
/// </summary>
public static class ComponentIdentity
{
    /// <summary>The identity of a component type — the CLR full name of its definition.</summary>
    public static string Of(Type type)
    {
        var definition = type.IsGenericType ? type.GetGenericTypeDefinition() : type;
        return definition.FullName ?? definition.Name;
    }

    /// <summary>The key a component's state travels under: <c>Identity#ordinal</c>.</summary>
    public static string Key(Type type, int ordinal) => $"{Of(type)}#{ordinal}";
}
