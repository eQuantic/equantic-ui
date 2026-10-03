# Proposal

Closes #481, a sub-issue of #160 (instruments that fail, not warn).

## Why

The test suites turned the .NET framework into a new set of metadata references for every
compilation they created. `MetadataReference.CreateFromFile` copies the whole assembly into native
memory, which the GC does not count and frees only when the reference is finalized, so the copies
piled up: one compiler suite's test host peaked at 29 GB and one conformance suite's at 32.6 GB on a
48 GB machine, and on the same evening the system killed dozens of daemons to make room. A new
reference also makes Roslyn build that assembly's symbols again for each compilation.

## What Changes

- One owner, `tests/Shared/TestReferences.cs`, keeps one reference per assembly for the whole test
  process (`TestReferences.Of`), linked into the four test projects that
  compile C#, and every test compilation reads its references there.
- A guard, `TestReferencesGuardTests`, fails on a `CreateFromFile` anywhere else under `tests/`, and
  names the owner.
- Nothing a developer using the SDK writes or meets changes: eqc and the design host already build
  their references once per run.

## Capabilities

### New Capabilities

(none)

### Modified Capabilities

(none: the change is in the test suites alone, and no requirement of the product moves, so the
change sets `skip_specs`.)

## Impact

- **Tests**: the compiler, conformance, web and native engine suites, and their projects link the
  owner.
- **Public surface**: none. The developer surface does not move.
