using System;

namespace eQuantic.UI.Primitives;

/// <summary>
/// Marks a type of the vocabulary's assembly whose browser twin the compiler TRANSPILES from its C#,
/// with the runtime, as it transpiles an app's own types: its constructor is the C# constructor, an
/// object initializer is applied once it returns, and its members are the type's. The vocabulary's
/// other types have hand-written twins, which take an initializer as a trailing config object.
/// <para>
/// An app reaches both kinds as metadata, in one namespace, so the namespace cannot tell them apart:
/// every one was taken for hand-written, and <c>new CellRef(1, 2) { Col = 3 }</c> handed its
/// initializer to a config slot the transpiled twin does not have (#592). The spreadsheet's and the
/// forms' models carry it, every type of those folders and no other, which a test holds.
/// </para>
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, Inherited = false)]
public sealed class TwinIsTranspiledAttribute : Attribute
{
}
