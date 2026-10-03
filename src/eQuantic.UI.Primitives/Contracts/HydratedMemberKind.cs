namespace eQuantic.UI.Primitives;

/// <summary>
/// How C# declares a value a component carries from its server render to the browser, which is how the
/// server finds it on the live component (see <see cref="HydratedMemberAttribute"/>).
/// <para>
/// The values are explicit and append-only: the source generator writes them into the app's own
/// assembly, where C# bakes each one in as a constant, so renumbering one would be read silently as
/// another by an app built against the old value.
/// </para>
/// </summary>
public enum HydratedMemberKind
{
    /// <summary>A field, read as it is.</summary>
    Field = 0,

    /// <summary>A property, read through its getter, so no name the C# compiler synthesizes is guessed.</summary>
    Property = 1,

    /// <summary>
    /// A primary-constructor parameter the class captures, which the C# compiler stores in a field it
    /// synthesizes for it.
    /// </summary>
    CapturedParameter = 2,

    /// <summary>
    /// The store a property keeps through C#'s <c>field</c>, its accessors guarding what goes in and out.
    /// It crosses as the store itself, into the slot the twin keeps for it, since a setter that transforms
    /// what it is given would not give back what its getter answered.
    /// </summary>
    BackingField = 3,
}
