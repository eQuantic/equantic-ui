# Tasks

## 1. A base is named after its twin (#479)

- [x] 1.1 Name a base type from its symbol, with the rightmost simple name where there is no model, in the component parser, the plain-class emitter and the record emitter. Verify: `QualifiedBaseEmissionTests` passes for a primitive, a component, a record and a plain class, qualified, aliased and `global::`, with the model and without it, and fails against `main`

## 2. A member through using static answers as its qualified spelling (#485)

- [x] 2.1 Inline a bare constant of an external type, and route a bare `Math`, `string` and enum member through the strategies their qualified spelling reaches. Verify: `UsingStaticConformanceTests` runs every row of #485 and the enum cases on both sides, and fails against `main`
- [x] 2.2 Fail the build with EQ2004 for a .NET member reached bare that no strategy translates. Verify: `UsingStaticFenceTests` reports it for `Console.WriteLine` and builds `Math` and `string` members clean

## 3. A type's own Count is its own (#517)

- [x] 3.1 Spell a `Count` by the receiver's symbol: .NET's collections keep the array's `length`, and any other type reads its twin's `count`, wherever it is declared. Verify: `ReferencedCountTests` compiles a component against a library image, reads the library type's `count` and a list's `length`, and fails against the previous rule

## 4. Documentation and the suites

- [x] 4.1 The wiki's SupportedFeatures and Diagnostics pages (EN + pt-BR) and `docs/DIAGNOSTICS.md`'s EQ2004 row, on a wiki branch named like this one. Verify: the docs guards pass with `EQ_WIKI_DIR` on that branch
- [x] 4.2 One `docs/LEDGER.md` line citing #479, #485 and #517. Verify: `./scripts/check-openspec.sh` passes
- [ ] 4.3 The suites, each alone and read by its exit code. Verify: Compiler, Web, Server, Conformance and the runtime's `TestRuntime` all exit 0
