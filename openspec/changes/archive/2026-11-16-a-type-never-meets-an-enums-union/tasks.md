# Tasks

## 1. The proof

- [x] 1.1 A conformance case whose app declares `record DensityValue` beside a component taking a `Density` runs on both sides in both modes: on the code before the cure the module imports `DensityValue` from the runtime and bun refuses to load it

## 2. The cure

- [x] 2.1 The emitter names a vocabulary enum's union `$<Enum>Value`, and the scan of the emitted text reads only that name
- [x] 2.2 `enums.generated.ts` exports each union under the `$` name, regenerated, and the shared components the runtime ships are transpiled again
- [x] 2.3 The runtime's `tsc` and tests pass with the components that import the `$` names
