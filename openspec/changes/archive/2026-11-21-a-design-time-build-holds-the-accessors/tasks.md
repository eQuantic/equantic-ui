# Tasks

## 1. The accessors in a design-time compilation

- [x] 1.1 `EQuanticResourceAccessorsCompiled` in both SDKs: `PrepareResources` before `CoreCompile` for a project with embedded resources
- [x] 1.2 Verify: the shipped targets, in a design-time build of `Compile` alone of a plain project with no Designer, carry the accessor in `Compile`, in both SDKs, and without the new target neither does; the other accessor cases still pass
- [x] 1.3 Verify against the real thing: `samples`' web "Edit to view" under `dotnet watch` applies each edit and hydrates, its warm median back inside the ceiling

## 2. Documentation

- [x] 2.1 A ledger entry under `docs/ledger/`
