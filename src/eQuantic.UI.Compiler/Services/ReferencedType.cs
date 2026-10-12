namespace eQuantic.UI.Compiler.Services;

/// <summary>
/// A type of a referenced project that a written module imports: its twin's name, which is its
/// module's, what the type IS, and a file that declares it.
/// <para>
/// The name alone does not say which type: a file may declare a <c>Status</c> in each of two
/// namespaces, and a page that reaches one has not reached the other. So the type's CLR name travels
/// with it (its namespaces, its owners joined by <c>+</c>, its arity), which is what a declaration
/// says of itself as written (<see cref="Parser.ComponentParser.ClrIdentity"/>), and the build
/// compiles the declaration that is that type and no other of its name.
/// </para>
/// </summary>
internal readonly record struct ReferencedType(string Name, string Identity, string File);
