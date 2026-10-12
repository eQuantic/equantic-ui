namespace eQuantic.UI.Compiler.Services;

/// <summary>A type of a referenced project that a written module imports: its twin's name, which is
/// its module's, and a file that declares it.</summary>
internal readonly record struct ReferencedType(string Name, string File);
