namespace eQuantic.UI.Primitives;

/// <summary>
/// Marks a model whose <c>System.ComponentModel.DataAnnotations</c> attributes should become a
/// <see cref="FormController"/> — read at BUILD time, emitted as calls to the same
/// <see cref="Rules"/> a hand-written form uses.
/// <para>
/// The build is what makes this possible at all: DataAnnotations validates by REFLECTION, and
/// reflection is exactly what the transpiler cannot carry into a browser. So the attributes are
/// read where they can be, and what crosses is a lambda over a string — the same one either way.
/// </para>
/// <para>
/// Opt-in, deliberately. A model annotated for a server API is not automatically a form: the
/// generator would otherwise emit a controller for every DTO in the project, and a screen would
/// inherit rules nobody meant to show a user.
/// </para>
/// <example>
/// <code>
/// [FormModel]
/// public sealed class SignUp
/// {
///     [Required, EmailAddress] public string Email { get; set; } = "";
///     [Required, MinLength(8)] public string Password { get; set; } = "";
///     [Compare(nameof(Password))] public string Confirm { get; set; } = "";
/// }
///
/// // …generated:
/// var form = SignUpForm.Create();
/// </code>
/// </example>
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class FormModelAttribute : Attribute
{
    /// <summary>
    /// The generated class's name — <c>SignUpForm</c> by default. Given here when the default
    /// would collide with a type the app already has, which is the only reason to say it.
    /// </summary>
    public string? Name { get; set; }
}
