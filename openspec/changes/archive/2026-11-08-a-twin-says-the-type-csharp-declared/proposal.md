## Why

Closes #793, a bug under #565, and #1106, a bug under #849 (the transpiler's fences hold on every path).

The runtime's `tsc` reads the modules eqc transpiles for the shared components, and refused legal C# in two shapes. A
local function kept its parameters' types and lost its return, so TypeScript inferred the DERIVED type its body
returned, and a variable it initialized refused the sibling C# lets it take (TS2740). The temporary an object
initializer lowers through was declared `any`, so a local built by one was `any` too, and a lambda with a parameter
subscribed to its event had nothing to infer from (TS7006).

## What Changes

- A local function says its declared return type where the type has a name in TypeScript, the rule a local's annotation
  already followed, now in `TsNamedType` for the three who read it.
- An object initializer's temporary is declared as the type it holds.
- Nothing a developer using the SDK writes or meets changes: an app's module is bundled by bun, which reads no types.
  What moves is the emitted TypeScript, and the JavaScript is the same. No requirement moves, so no spec does.

## Capabilities

### New Capabilities

(none)

### Modified Capabilities

(none)

## Impact

- **eqc**: `LocalFunctionStatementStrategy`, `ObjectInitializer`, `Temporaries`, `JsArrowBlock`. `eQuantic.UI.Compiler` is a
  tool (#899), and its `JsArrowBlock` widens.
- **The runtime**: 24 of the transpiled shared components change in TypeScript only.
- **The realizers, the Photon shells, the SDKs, the templates**: none. The developer surface does not move.
