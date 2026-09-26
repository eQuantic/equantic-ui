# Tasks

## 1. One writer

- [ ] 1.1 `ConstantLiteral` writes a constant's value in its C# type: a decimal as the exact Decimal, a long or ulong as a BigInt, a float as its double, a char and a string escaped
- [ ] 1.2 The inlining strategy writes through it, and claims a bare name for a const with no source
- [ ] 1.3 A decimal literal and a folded negation are written from their values
- [ ] 1.4 A skipped parameter's default goes through it, for creations and invocations
- [ ] 1.5 The primitive table keeps only what is not a constant
- [ ] 1.6 A narrow integer and a ulong annotate in their JavaScript type

## 2. Proof

- [ ] 2.1 Conformance cases on both sides for decimal's five constants, a user's const, the long constants, a decimal literal with separators and the skipped defaults, failing on main
- [ ] 2.2 Emission tests for a const reached through its type, by `using static` and by its own class, for a skipped default and for a constant's static annotation
- [ ] 2.3 The BCL audit grades decimal's static surface, and each translated member is proved

## 3. Documentation

- [ ] 3.1 One `docs/LEDGER.md` line citing #444
- [ ] 3.2 The wiki's SupportedFeatures rows in English and Portuguese, on the wiki branch named like the pull request's
- [ ] 3.3 Archive this change in the same pull request
