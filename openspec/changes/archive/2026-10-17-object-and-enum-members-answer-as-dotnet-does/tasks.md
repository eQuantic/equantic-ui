## 1. Runtime Equality Helpers (`key-equality.ts`)

- [x] 1.1 Update `sameItem` and `sameKey` in `src/eQuantic.UI.Runtime/src/utils/key-equality.ts` so `null` and `undefined` compare equal to each other, unmarked arrays (`List<T>`, `T[]`) held as `object` compare by reference (`===`) instead of element-by-element, and plain objects compare their keys in declaration order.
- [x] 1.2 Add unit tests in `src/eQuantic.UI.Runtime/src/utils/key-equality.spec.ts` covering `sameKey` / `sameItem` on `null` vs `undefined`, unmarked arrays, marked tuples, records, and plain objects.

## 2. Compiler Strategies for `object` and `enum` Members

- [x] 2.1 Update `StructuralEqualsStrategy` (`src/eQuantic.UI.Compiler/CodeGen/Strategies/Types/StructuralEqualsStrategy.cs`), `MemberAccessStrategy`, and `IdentifierStrategy` to lower static `object.Equals(a, b)` (#902), enum `Equals` and `Equals` method groups (#926), inherited `object.Equals(object)` and `Equals` method groups on class receivers (#909), and null-checked `Equals` on reference-type records (#912).
- [x] 2.2 Update `StringConversion` (`src/eQuantic.UI.Compiler/CodeGen/Strategies/StringConversion.cs`) so instance `ToString()` on a reference-type receiver (record class, plain class, `object`, interface, or type parameter) checks the receiver for `null` via `$eq.receiver` (#912).
- [x] 2.3 Update `GetHashCodeStrategy` (`src/eQuantic.UI.Compiler/CodeGen/Strategies/Types/GetHashCodeStrategy.cs`) so non-flags enum values and method groups hash by their underlying numeric value rather than their camelCase string key (#926), and `List<T>` hashes by reference identity (#909).
- [x] 2.4 Update `EnumShape` (`src/eQuantic.UI.Compiler/CodeGen/Strategies/Types/EnumShape.cs`), `EnumStrategy`, `IsPatternStrategy`, `ToStringStrategy`, `LinqTableStrategy`, and `LinqKeys` so enum members that alias an earlier member share the first member's camelCase key and `.NET` name (#927).
- [x] 2.5 Update `PlatformMemberRead`, `InvocationStrategy`, `MemberAccessStrategy`, and `IdentifierStrategy` so `GetType` and `MemberwiseClone` method groups and chained accesses (`x.GetType().Name`) report a single `EQ2004` diagnostic (#903).

## 3. Conformance & Compiler Tests

- [x] 3.1 Add compiler tests in `tests/eQuantic.UI.Compiler.Tests/` for `GetType` / `MemberwiseClone` `EQ2004` diagnostics (#903), `object.Equals(a, b)` lowering (#902), and `ToDictionary` over an enum with aliases (#927).
- [x] 3.2 Add conformance tests in `tests/eQuantic.UI.Conformance.Tests/` proving runtime parity with .NET for `object.Equals(a, b)` (#902), inherited `Equals(object)` on class receivers and `List<T>` / `T[]` behind `object` (#909), `Equals` and `ToString` on `null` records and classes (#912), enum `Equals` and `GetHashCode` (#926), and aliased enum members (#927).
