# Design

## The accessors: MSBuild's own generator, where eqc already looks

The issue proposed a source generator. .NET already has the writer: `GenerateResource` writes a
strongly typed class from a `.resx` when its item carries `StronglyTypedLanguage` and its siblings, with
the same builder Visual Studio's `ResXFileCodeGenerator` uses, and adds the class to the compile. So the
SDK sets that metadata, after `CreateManifestResourceNames` and before `CoreResGen`, on each neutral
`.resx` that has no Designer beside it and no such metadata of its own. A culture's `.resx` carries no
keys of its own and gets nothing.

Two names are chosen apart, as Visual Studio chooses them. The class's namespace is the root namespace
and the folder, which is what the code names. The `ResourceManager`'s base name is the manifest name
MSBuild gave the resource, which is what the satellite assemblies answer to. They are usually the same
string; they part where MSBuild names the resource without its folder, which it does for a project built
through a symbolic link to its folder (measured: a temp folder on macOS). There a hand-kept Designer
compiles and finds no resource, and the generated one works.

eqc reads a catalog's `.resx` and its cultures beside the file that declares the class (`ResxFiles`, the
`ResXFileCodeGenerator` contract). Rather than teach eqc a second rule, the web SDK keeps the contract:
the class lands in a mirror of the project's folders inside the compiler's generated tree, which eqc
already reads, and the `.resx` files are copied beside it, unchanged ones skipped, before `CoreResGen`
and before eqc, a hot reload's compile included. A `.resx` the project names from outside its folders
gets no mirror and no accessors. Measured on the samples, the catalogs and modules eqc writes with the
generated class are byte for byte those it wrote with the hand-kept one.

Flutter's answer is `gen-l10n`, which writes Dart from ARB files at build time: the same shape, a
generator the build runs, with no file committed.

## The version: compared, not only defaulted

The SDK already read its version from its nuspec, but only when the project set none, so a project that
set another was obeyed. Now the SDK always reads it, defaults `EQuanticUIVersion` to it, and a target
before `CollectPackageReferences` (the restore's first question) and before `PrepareForBuild` fails with
EQ4010 when the two differ. A failure beats a warning here: the packages would be restored at the wrong
version in the same run. Beside the source tree there is no nuspec, the packages are projects, and
nothing is compared.

## The factories: the build's set of projects, handed to the generator

The generator sees a referenced project only as metadata, and cannot tell a project from a package by
looking at a reference. It must: eqc compiles a referenced project from its source (#624), so a factory
naming its component imports a module that exists, and has no source for a package. The build already
knows the set (the references that carry `MSBuildSourceProjectFile`, without the SDK's own projects and
icon packs: the set eqc is handed), so a target writes their assembly names into a property the compiler
hands to analyzers (`CompilerVisibleProperty`), before it writes their options. The generator walks those
assemblies' public types with the same rules as the app's own components.

The surface's namespace stays the one the app's own components share: a library's namespace must not
narrow it to nothing. An app whose only components are a library's has its surface in the namespace its
assembly is named by. A name both declare goes to the app's own, with EQ3102 for the other, as between
two of the app's own.
