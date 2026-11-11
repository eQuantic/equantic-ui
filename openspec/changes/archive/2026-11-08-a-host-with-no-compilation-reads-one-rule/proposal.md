## Why

Closes #754, a bug under #565, and #1170, a bug under #849 (the transpiler's fences hold on every path).

Both live in a host with no project's compilation, a snippet compiled on its own or a tool that hands eqc no
compilation. There a member an app's exception class declares compiled and then failed in the browser:
`Failure.Count++` named a class nothing wrote, `f.Code` read nothing and `f.Describe()` was not a function. A plain
class calling a `[RuntimeProvided]` helper wrote the call with no import, a ReferenceError. And the parser and the
dependency scan disagreed about what a component is where a base did not bind.

## What Changes

- A member an app's exception class declares, reached in such a host, fails the build with EQ2010, which says what
  to do. Constructing, throwing and catching one still build, and so does what the runtime's exception carries,
  `Message` among them.
- The plain-class path reads the scan's `[RuntimeProvided]` types, as the component path does.
- `ComponentRule` is the one answer to "is this class a component", read by the parser and the scan, with the name
  fallback in one place.
- Nothing changes for an app built with the SDK, which always hands eqc the project's compilation.

## Capabilities

### New Capabilities

(none)

### Modified Capabilities

- `transpiler-exceptions`: gains "A member of an exception class with no module fails the build".
- `module-imports`: gains "A host with no project's compilation reads one rule".

## Impact

- **`eQuantic.UI.Compiler`**, the library eqc runs: `ComponentRule` (new), `ComponentParser`, `ComponentDependencyResolver`, `TypeScriptEmitter`,
  `HostOnlySymbolExtensions`.
- **The runtime, the realizers, the Photon shells, the SDKs, the templates**: none.
- **Public surface**: none. The developer surface does not move.
