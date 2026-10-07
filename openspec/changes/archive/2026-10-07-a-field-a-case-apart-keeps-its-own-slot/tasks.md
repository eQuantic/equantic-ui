# Tasks

## 1. A field a case apart from a member

- [x] 1.1 `FieldSlotExtensions.TwinSlot` names the slot: the twin name, or the name with a `$` after it for a plain class's instance field a case apart from a property, a method, an event or another field of its type or its bases
- [x] 1.2 The instance state, `IdentifierStrategy`, `MemberAccessStrategy` and the null-conditional assignment's target read the slot through the field's symbol
- [x] 1.3 A class with a moved field answers `toJSON` with `$eq.json`, and `twinJson` writes the property a moved field gave its name to, never the field; proved by `twin-json.spec.ts`

## 2. Proof and documentation

- [x] 2.1 `MemberCaseConformanceTests`: six cases with and without annotations, ten of the twelve failing before the fix, the getter beside its field passing by luck on both sides
- [x] 2.2 Run the five suites and build `samples/DefaultUIDashboard` with no warning
- [x] 2.3 The wiki's Compiler page, in English and Portuguese, on a wiki branch named like this pull request's
- [x] 2.4 One `docs/LEDGER.md` line citing #396
