# Design

## Flutter has no answer to borrow

Dart has no default interface members: an abstract class carries implementations, and a class that
`implements` one must write every member, while one that `extends` or mixes it in inherits them
through the class chain the compiler emits. There is no row for this in `docs/FLUTTER-PARITY.md`.
The nearest shape is Dart's mixin application, which is what this does: the default becomes a
member of each class that takes it.

## Written into the class, not held by the interface

The alternative is a runtime object per interface holding its defaults, with each class delegating
to it. That needs a module for a type the emitters treat as erased everywhere else (the import scan
skips interfaces on purpose), and it would still not reach an interface compiled into another
assembly. Writing each default into the class reuses what already writes a class's own members, so
a default is converted by the same code as a member the class declares, and a class that declares
the member keeps its own.

`INamedTypeSymbol.FindImplementationForInterfaceMember` decides which implementation applies, so a
derived interface's explicit override wins over a base interface's default exactly as C# resolves
it. A base class that implements the interface already carries the default in its own twin, and
JavaScript's prototype chain hands it down, so it is written once.

## The body lives in another file

A semantic model answers only for its own syntax tree, and a default's body is in the interface's
file. `CSharpToJsConverter.InFileOf` converts it with that file's model in force and puts the
class's model back, with the correspondences it held. The emitter's own type lookups ask the
node's file's model for the same reason.

## When the body is not there

An interface from a referenced assembly has metadata and no source, and that is the normal case for
the SDK's own interfaces: an app compiles against `eQuantic.UI.Primitives` and `eQuantic.UI.Code`,
which ship no sources. Measured on the documentation site with this branch's eqc before the next
step: three EQ1008 on every build, all from its theme, a class no client module even imports. A
warning every themed app sees is noise that teaches people to skip EQ codes.

The runtime is the twin of the vocabulary's assemblies, so it carries their defaults: one function
per default, taking the instance, in `interface-defaults.ts`, exported from `@equantic/runtime` under
the interface's name. A class relying on a default of an interface the runtime provides delegates
to it, and the import follows the name the twin writes. The copies are written by hand, which is
the risk, so a pair of tests holds them: `VocabularyInterfaceDefaultsTests` lists every default of
every public interface of the vocabulary by reflection into a fixture, and fails when the list
changes; `interface-defaults.spec.ts` fails for any line with no function, and checks each value
against the C# default.

The copy of `IAppTheme.Code` is `codeTokenColor`, one switch transcribed by hand, so the fixture
also carries the C# default's colour over the reference theme for every token kind, and the spec
checks every one: a slip in any arm fails there.

EQ1008 remains for an interface from any other assembly, which has neither a body nor a runtime
copy, and there it is an error: the developer can act on it, and with the SDK's own defaults
carried, it fires nowhere in the SDK, its samples, its template or the documentation site.
