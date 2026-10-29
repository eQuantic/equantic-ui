# Tasks

## 1. Measure

- [x] 1.1 Probe each pair with `ComponentCompiler.CompileSource` and read the emitted TypeScript: statics, properties, events, extension members, operators, conversions, components, and the names the twin writes itself

## 2. The guard

- [x] 2.1 `TwinOwnMembers`: the override that takes the place of a member the twin writes itself, read by the record emitter
- [x] 2.2 `MembersOnOneName`: every member on its key, as the emitters name it, called from `OverloadedMethods.Check` for a type and for a static class nested in a component
- [x] 2.3 Tests in `OverloadedMethodTests`, each refusal failing with the pass switched off, and the emitter's two decisions failing without `TwinOwnMembers`

## 3. Documentation

- [x] 3.1 docs/DIAGNOSTICS.md's EQ1007 row
- [x] 3.2 The wiki's Diagnostics page in English, Portuguese, Spanish, French and German
- [x] 3.3 A ledger entry under docs/ledger/ citing #1146

## 4. Proof against the real thing

- [x] 4.1 Every SDK source the runtime embeds transpiles with the guard on, and the pinned modules are unchanged
- [x] 4.2 Every sample and template transpiles with the guard on, refused nowhere
