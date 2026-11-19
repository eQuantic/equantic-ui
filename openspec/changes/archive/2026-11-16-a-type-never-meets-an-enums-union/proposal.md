# Proposal

Closes #1427, a Bug under #1174.

## Why

The runtime declares a string union for every vocabulary enum, named `<Enum>Value` (`DensityValue` for
`Density`), and a module eqc writes imported it by that name. C# can spell that name, so a type of the
app or of the vocabulary could carry it. An app that declared `record DensityValue(int Rows)` beside a
component taking a `Density` had its own record taken for the union: the module imported `DensityValue`
from the runtime, which exports no such value, and the page did not load, in TypeScript and in plain
JavaScript alike. The issue saw it surface later in `tsc`, far from the cause.

## What Changes

- A module eqc writes names a vocabulary enum's union `$DensityValue`: a `$` C# cannot spell, so no
  type an app or the vocabulary declares ever meets it. The emitter annotates and imports by that name,
  and the scan of the emitted text for unions reads only that name.
- `enums.generated.ts` exports each union under that name too. The runtime's own code keeps the plain
  name, so no hand-written file changes.

## Impact

- `src/eQuantic.UI.Compiler/CodeGen/TypeScriptEmitter.cs`, `src/eQuantic.UI.Web.Build/EnumUnionsTsGenerator.cs`,
  the generated `enums.generated.ts`, and the transpiled shared components the runtime ships, whose
  annotations now read `$…Value`. No API changes, and the runtime's JavaScript is the same: a union is a
  type, which bundling strips.
