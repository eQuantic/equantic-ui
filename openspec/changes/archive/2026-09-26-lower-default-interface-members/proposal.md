# Proposal

Closes #414, a sub-issue of #295, and #415.

## Why

JavaScript has no interfaces, and eqc wrote no default interface member into the twins of the
classes that take one. `ICodeLanguage.Rules` is a default property, `PlainTextLanguage` relies on
it, and its twin had no `rules`. On 0.2.0-preview.58, where `CodeBlock.TabSize` reads
`Language.Rules.IndentWidth`, every plain-text `CodeBlock` threw on `indentWidth` in the browser and
was contained, and the documentation site stayed on 0.2.0-preview.57. A `CodeEditor` on plain text
was already broken on .57, because its controller read the same member.

## What Changes

- **eqc writes a default interface member into the twin of every class that takes it** without
  declaring it: a property with a body, and a method, converted from the interface's source under
  that file's semantic model, in the three places that write a class (a plain class, a record or a
  struct, and a component). A member a base class already takes is left to the base's twin, which
  the prototype chain hands down. A derived interface's override of a base interface's member wins,
  as the language picks it, and a private member of the interface that a default calls travels with
  it. The types a default names join the module's imports.
- **The runtime carries the vocabulary's defaults.** An app compiles against the SDK's assemblies,
  where `IAppTheme`, `ICodeLanguage` and `ICodeCompletionProvider` have signatures and no bodies, so
  an app's theme, language or completion provider that relies on a default delegates it to the
  runtime's copy (`interface-defaults.ts`): `get data() { return IAppTheme.data(this); }`. Measured
  on the documentation site before this: its theme took three warnings on every build, and an app's
  own language without `Rules` would have met the same undefined as `PlainTextLanguage`.
- **A default nothing can supply refuses the class.** An interface compiled into any other
  referenced assembly has neither source nor a runtime copy. Such a class gets the new error EQ1008,
  which names the member and the two ways out: declare it in the class, or keep the class out of
  client code. Measured, nothing in the SDK, its samples, its template or the site raises it.
- The runtime's `PlainTextLanguage` twin gains `rules`, the only twin in the SDK this changes.

For a developer using the SDK: an interface they write with a default member now behaves on the web
as it does in C#; their theme, language or completion provider takes the SDK's defaults in the
browser as it does on the server; and a class relying on a default from any other referenced
assembly stops the build, where the browser used to meet undefined. Nothing is removed.

The parts reached are eqc and the runtime, which exports `IAppTheme`, `ICodeLanguage` and
`ICodeCompletionProvider` as its copies of their defaults. The public surface gains
`CSharpToJsConverter.InFileOf`. The developer surface does not move. The wiki's Diagnostics page
gains the EQ1008 row in both languages.

## Capabilities

### New Capabilities

- `transpiler-interfaces`: what eqc does with a C# interface, which has no JavaScript form.

### Modified Capabilities

None.

## Impact

`src/eQuantic.UI.Compiler` (a new `DefaultInterfaceMembers`, the three emitters, the converter),
the runtime's `interface-defaults.ts` and its fixture, the regenerated `PlainTextLanguage` pin,
`docs/DIAGNOSTICS.md`, `docs/LEDGER.md`, the diagnostics
baseline, and tests in the conformance, compiler and runtime suites.
