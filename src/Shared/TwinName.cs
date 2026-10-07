namespace eQuantic.UI;

/// <summary>
/// The name a transpiled twin gives a C# name: its first character in lower case
/// (<c>FirstName</c> is <c>firstName</c>).
///
/// <para>
/// Two assemblies that cannot reference each other have to answer this identically. eqc names every
/// member it writes into a twin by it, and the hydration manifest the source generator writes names a
/// value the server sends under it, so the browser adopts the value into the member that reads it. While
/// the server guessed this for itself, an auto-property crossed as <c>Downloads</c> into a twin that
/// declares <c>downloads</c>, and never landed. So the rule is one file, linked into both.
/// </para>
/// </summary>
internal static class TwinName
{
    /// <summary>The twin's name for <paramref name="name"/>. Empty or null comes back unchanged.</summary>
    public static string Of(string name) =>
        string.IsNullOrEmpty(name) ? name : char.ToLowerInvariant(name[0]) + name.Substring(1);

    /// <summary>
    /// The slot a twin keeps a property's own store in when its accessors read or write it through C#'s
    /// <c>field</c>: the property's twin name after a <c>$</c>, which no C# identifier can start with, so
    /// the slot never meets a member the author declared.
    /// </summary>
    public static string BackingSlot(string property) => "$" + Of(property);

    /// <summary>
    /// The name a type's twin and its module take: its own name after the twin names of the types that
    /// contain it, joined by <c>$</c> (<c>Cart$Item</c>, <c>A$B$C</c>, #584), which no C# name can hold,
    /// so a nested type's twin never meets a top-level one's. eqc names a module by it, from a symbol or a
    /// declaration, and the server names the page it serves by it (<see cref="OfType"/>): a nested page
    /// asked for a module named by its simple name, which nobody writes.
    /// </summary>
    public static string OfNested(string? ownerTwin, string name) =>
        ownerTwin is null ? name : ownerTwin + "$" + name;

    /// <summary>
    /// Whether a type's owners let it cross (#584): one declared inside a type marked <c>[ServerOnly]</c>
    /// or <c>[RuntimeProvided]</c>, or inside an exception or an attribute, has no twin, as its owner has
    /// none, the rule eqc writes modules by. The server publishes no route and maps no page for it.
    /// </summary>
    public static bool OwnersCross(System.Type type)
    {
        for (var owner = type.DeclaringType; owner is not null; owner = owner.DeclaringType)
        {
            if (owner.GetCustomAttributes(true).Any(attribute => attribute.GetType().Name is "ServerOnlyAttribute" or "RuntimeProvidedAttribute"))
                return false;
            if (typeof(System.Exception).IsAssignableFrom(owner) || typeof(System.Attribute).IsAssignableFrom(owner)) return false;
        }
        return true;
    }

    /// <summary>The twin name of a CLR type (<see cref="OfNested"/>): its declaring types' names and its
    /// own, without the generic arity the CLR spells (<c>Box`1</c> is <c>Box</c>).</summary>
    public static string OfType(System.Type type)
    {
        var name = type.Name;
        var tick = name.IndexOf('`');
        if (tick >= 0) name = name.Substring(0, tick);
        return OfNested(type.DeclaringType is { } owner ? OfType(owner) : null, name);
    }
}
