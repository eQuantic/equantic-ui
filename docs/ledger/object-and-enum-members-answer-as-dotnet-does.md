- **2026-10-14 · Object and enum members answer as .NET does**: `object.Equals(a, b)` went out as
  `object.equals(a, b)` (`ReferenceError: object is not defined`,
  [#902](https://github.com/eQuantic/equantic-ui/issues/902)); `GetType()` and `MemberwiseClone` built
  with no diagnostic and threw in the browser (`getType is not a function`,
  [#903](https://github.com/eQuantic/equantic-ui/issues/903)); `Equals(object)` inherited from `object`
  was refused on a class receiver while a `List<T>` or `T[]` held as `object` compared element by
  element ([#909](https://github.com/eQuantic/equantic-ui/issues/909)); `Equals` and `ToString` on a
  null record or class instance answered `false`, `""` or `"null"` instead of throwing
  `NullReferenceException` ([#912](https://github.com/eQuantic/equantic-ui/issues/912)); an enum's
  `Equals` threw (`equals is not a function`) and its `GetHashCode` hashed its camelCase name instead
  of its underlying number ([#926](https://github.com/eQuantic/equantic-ui/issues/926)); and an enum
  member that aliases another (`Gray = Grey`) crossed as a key of its own, unequal to its target in
  `==`, `switch`, pattern matching, `ToString` and dictionary keys
  ([#927](https://github.com/eQuantic/equantic-ui/issues/927)). Static `object.Equals(a, b)` now lowers
  to `$eq.collections.sameKey` (with tuple and anonymous argument types read before boxing); inherited
  `Equals(object)` lowers on class receivers to reference equality through `$eq.equalsInstance` and
  `$eq.equalsGroup`, while `sameKey` compares unmarked arrays (`List<T>`, `T[]`) by reference;
  instance `Equals` and `ToString` on reference receivers refuse `null` through `$eq.receiver` after
  evaluating any argument; `GetType` and `MemberwiseClone` are refused with one `EQ2004` across calls,
  chains and method groups; an enum's `Equals` and `GetHashCode` compare and hash by its underlying
  value; and an enum member that shares another's value takes the first member's key and name, so
  `ToDictionary` accepts enums with aliases too. Proposed and archived through OpenSpec
  (`openspec/specs/transpiler-bcl`).
