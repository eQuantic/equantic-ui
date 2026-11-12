## Why

Closes #1064 and #1065, bugs under #849 (the transpiler's fences hold on every path, part three).

The runtime's strict `tsc` reads the twin of every class a transpiled library declares. Two field shapes were
written with a type it refuses:

- A field typed as an array of an enum named the enum's C# name: `private static readonly CodeCommand[] NotACellsCommand`
  became `static get notACellsCommand(): CodeCommand[]`, and no module exports `CodeCommand` (TS2304). Everywhere
  else an enum crosses as what the runtime calls it: a vocabulary enum's `<Enum>Value` union, an app's own enum's
  member string. The swap looked inside a generic's arguments (`List<TypeRole>`) and never inside an array's
  element, so `TypeRole[]` and `Shade[]` kept the C# spelling (#1064).
- A component's field of a vocabulary struct whose twin builds no zero, `private readonly ColorToken _ink;`, was
  written `_ink: ColorToken = undefined;` (TS2322, #1065). #1142 settled that for every such struct, declaring the
  field definitely assigned (`_ink!: ColorToken;`), and nothing pinned the struct the issue was found on.

## What Changes

- An array of an enum, at any depth (`TypeRole[]`, `TypeRole[][]`), is typed as the enum crosses: `TypeRoleValue[]`,
  imported from the runtime, for a vocabulary enum, and `string[]` for an app's own. The annotation a class's field,
  a static's accessors and a local read (`DeclaredType`) looks inside an array's element as it looks inside a
  generic's arguments.
- A test pins a component's `ColorToken` field as #1142 writes it.
- An app writes nothing new. No public surface and no developer surface moves.

## Capabilities

### Modified Capabilities

- `transpiler-classes`: an array of an enum is typed as the enum crosses.

## Impact

`eQuantic.UI.Compiler` (`TypeScriptEmitter.DeclaredType` and its `Nested` walk). The runtime, the realizers, the
shells and the SDKs are untouched.
