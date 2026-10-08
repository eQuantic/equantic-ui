# Tasks

## 1. A field a case apart from a member

- [x] 1.1 `FieldSlotExtensions.TwinSlot` names the slot: the twin name, or the name with a `$` after it for a plain class's instance field a case apart from a property, a method, an event or another field of its type or its bases
- [x] 1.2 The instance state, `IdentifierStrategy`, `MemberAccessStrategy`, the null-conditional assignment's target and an object initializer's entry read the slot through the field's symbol
- [x] 1.3 A class with a moved field answers `toJSON` with `$eq.json`, and `twinJson` writes the property a moved field gave its name to, never the field; proved by `twin-json.spec.ts`
- [x] 1.4 A pattern reads the field in its slot too, which Copilot's first review found it did not: `PatternConverter` asks the model for every member a property subpattern names, each one along an extended path, and a positional pattern over a `Deconstruct` the app wrote, an extension's included, calls it and reads the parts it hands back

## 2. Proof and documentation

- [x] 2.1 `MemberCaseConformanceTests`: sixteen rows with and without annotations, thirty-two cases. On #661's head ten of the first fourteen fail, the getter beside its field passing by luck on both sides, and the initializer naming the field fails on the commit before its fix (.NET 5, JavaScript 10). The nine pattern rows, eighteen cases, fail on d7a87897, before Copilot's first round, and pass with its fix
- [x] 2.2 Run the five suites and build `samples/DefaultUIDashboard` with no warning
- [x] 2.3 The wiki's Diagnostics page (EQ1007) and its Supported Features page (a positional pattern), in English and Portuguese, on a wiki branch named like this pull request's
- [x] 2.4 One `docs/LEDGER.md` line citing #396
