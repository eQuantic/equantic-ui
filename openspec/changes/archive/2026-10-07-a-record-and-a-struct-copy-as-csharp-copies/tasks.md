## 1. A record's copy constructor

- [x] 1.1 A copy step per level of a record whose chain declares a copy constructor, run by `with`
- [x] 1.2 Conformance cases: a declared constructor over a base that declares one, a deep copy, a synthesized level over a declared base, a patch, a positional record

## 2. A mutable struct and a value tuple

- [x] 2.1 `ValueCopies`, settled by the dispatcher: copy on write along the path, `this` copied where it leaves its member, a mutating call on a temporary run on a copy
- [x] 2.2 `$clone()` on every mutable struct twin
- [x] 2.3 Conformance cases for a tuple, a struct, an argument, a mutating method, a nested value, an array element, a class's field, a boxing, a capture, `this` and a property's value

## 3. Proof and records

- [x] 3.1 A/B against main on the same cases, and the five suites
- [x] 3.2 The ledger line, the wiki in English and Portuguese, and the archive of this change
