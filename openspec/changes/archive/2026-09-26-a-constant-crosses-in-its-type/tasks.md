# Tasks

## 1. One writer

- [x] 1.1 `ConstantLiteral` writes a constant's value in its C# type: a decimal as the exact Decimal, a long or ulong as a BigInt, a float as its double, a char and a string escaped
- [x] 1.2 The inlining strategy writes through it, and claims a bare name for a const with no source
- [x] 1.3 A decimal literal and a folded negation are written from their values
- [x] 1.4 A skipped parameter's default goes through it, for creations and invocations
- [x] 1.5 The primitive table keeps only what is not a constant
- [x] 1.6 A narrow integer and a ulong annotate in their JavaScript type
- [x] 1.7 An enum-typed constant is its enum's representation, inlined or as a skipped default
- [x] 1.8 A decimal constant matches by value in a pattern, a switch arm and a case label
- [x] 1.9 A constant's text escapes a lone surrogate, a control character and a line separator, and a string literal takes the same writer

## 2. Proof

- [x] 2.1 Conformance cases on both sides for decimal's five constants, a user's const, the long constants, a decimal literal with separators and the skipped defaults, failing on main
- [x] 2.2 Emission tests for a const reached through its type, by `using static` and by its own class, for a skipped default and for a constant's static annotation
- [x] 2.3 The BCL audit grades decimal's static surface, and each translated member is proved
- [x] 2.4 The review's three defects proved both ways: conformance cases and emission tests failing on the previous head

## 3. Documentation

- [x] 3.1 One `docs/LEDGER.md` line citing #444
- [x] 3.2 The wiki's SupportedFeatures rows in English and Portuguese, on the wiki branch named like the pull request's
- [x] 3.3 Archive this change in the same pull request
