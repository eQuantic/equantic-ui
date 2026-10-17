## Context

In eQuantic.UI's browser runtime, C# values are represented by native JavaScript primitives (`number`, `string`, `boolean`), runtime twins (`Decimal`, `TimeSpan`, `DateOnly`, `TimeOnly`, `StringBuilder`, collection classes), plain objects (anonymous types), arrays (`T[]`, `List<T>`, and value tuples), and compiler-generated classes (`record`, `struct`, and plain `class`).

Several gaps in how `object` and `Enum` methods lower caused divergence from .NET 10:
1. Two-argument `object.Equals(a, b)` had no lowering strategy and emitted invalid `object.equals(a, b)` or `this.equals(a, b)`.
2. `GetType()` was excluded from `InvocationStrategy.ReportIfUntranslatable` by `method.ContainingType.SpecialType != SpecialType.System_Object`, letting `x.getType()` reach the browser and throw at runtime.
3. One-argument `Equals(object)` inherited from `object` on classes, arrays, lists and `StringBuilder` either failed the build or emitted `.equals(...)` on prototypes without `equals`, and `isPlainValue` in `key-equality.ts` treated every JavaScript `Array` as a value type, comparing `List<T>` and `T[]` structurally when held as `object`.
4. `Equals` on a `record class` lowered to `$eq.equals(receiver, other)` instead of `$eq.equalsInstance(receiver, other)`, returning `false` on a `null` receiver instead of throwing `NullReferenceException` after evaluating `other`; and `ToString()` on a `null` `record class`, plain class, `string` or `object` returned `"null"` or `""` instead of throwing `NullReferenceException`.
5. `Enum.Equals` had no lowering strategy, and `Enum.GetHashCode` hashed the camelCase string representation of non-`[Flags]` enums instead of their underlying integer value.
6. `EnumShape` gave every declared enum field its own camelCase key even when two fields shared the same constant value, making aliased enum members distinct values in the browser.

## Decisions

### 1. Canonical representation for aliased enum members (#927)
In .NET, enum members are named constants of the enum's underlying integer type: two members with the same numeric value are indistinguishable at runtime, and `Enum.GetName` / `ToString()` returns the first member in declaration value order with that numeric value.
- `EnumShape.Members(INamedTypeSymbol)` assigns each member's `Key` as the camelCase name of the first member with that numeric `Value`, while keeping `Name` as the member's own declared C# identifier so `Enum.GetNames`, `Enum.Parse("B")` and `Enum.IsDefined("B")` continue to recognize every declared name.
- `EnumShape.KeyToValue` and `ValueToKey` deduplicate by `Value` via `.DistinctBy(m => m.Value)` so generated lookup maps have one entry per distinct value.
- `EnumStrategy.MemberLiteral`, `IsPatternStrategy`, and constant folding in `ToStringStrategy` resolve enum members through `EnumShape.KeyOf` / `EnumShape.NameOf`, so `Alias.A` and `Alias.B` lower to the same key `'a'` and format as `'A'`.
- `LinqTableStrategy`'s `ToDictionary` refusal for `LinqKeys.HasAliases` is removed.

### 2. Enum `Equals` and `GetHashCode` (#926)
- `GetHashCodeStrategy` checks whether the receiver (or its unwrapped nullable type) is an enum and hashes `EnumShape.ValueOf(enumType, receiver, context)` with `$eq.hash(...)` (returning `0` when a `Nullable<TEnum>` is `null`). The `s.GetHashCode` method group evaluates the enum's numeric value when the delegate is created.
- `StructuralEqualsStrategy` handles `Enum` and `Nullable<TEnum>` receivers: when the unboxed argument type is a different concrete type (e.g. `int`, `string`, or another enum), it lowers to `$eq.equalsInstance(receiver, other, null)` (or `$eq.logic.and(receiver == null, other == null)` for `Nullable<TEnum>`), and otherwise to `$eq.equals(receiver, other)`.
- `MemberAccessStrategy` lowers the `s.Equals` method group on an enum to `$eq.equalsGroup(s)`.

### 3. `object.Equals(a, b)`, inherited `Equals(object)` and reference equality on collections (#902, #909, #912)
- In `key-equality.ts`, `isPlainValue(value)` no longer returns `true` for `Array.isArray(value)`. An unmarked JavaScript array behind `object` is a `List<T>` or `T[]`, which in .NET has reference equality (`sameItem`). Value tuples are compared structurally where their static tuple type is known (on a tuple receiver, a tuple member of a record/tuple/anonymous type, or an unboxed tuple argument to `Equals`).
- `sameItem(item, value)` treats `null` and `undefined` as equal (`item == null && value == null`) consistently with `equals`.
- Two-argument `object.Equals(a, b)` (qualified or bare) is lowered in `StructuralEqualsStrategy`: when both arguments have incompatible concrete unboxed types and at least one is a value type or anonymous type, it lowers to `$eq.logic.and(a == null, b == null)`; when either unboxed type is a tuple or anonymous type, it uses `ElementEquality.OwnEquals`; when either is a structural value type, it uses `$eq.equals`; otherwise it uses `$eq.collections.sameKey`.
- One-argument `receiver.Equals(other)` on any reference type uses `$eq.equalsInstance(receiver, other, ...)` so a `null` receiver throws `NullReferenceException` after `other` is evaluated:
  - `record class`: `$eq.equalsInstance(receiver, other)` (or with `null` comparator when the argument's unboxed type is incompatible).
  - Anonymous type: `$eq.equalsInstance(receiver, other, comparison)`.
  - `object` receiver: `$eq.equalsInstance(receiver, other, ...)` using the unboxed argument's structural comparator when known, or `$eq.collections.sameKey`.
  - Any other class, array, list, delegate or `StringBuilder` inheriting `object.Equals(object?)`: `$eq.equalsInstance(receiver, other, $eq.collections.sameItem)` (unless `receiver` overrides `Equals`, in which case its own `equals` method is called on `$eq.receiver(receiver)`).
- `ToStringStrategy` / `StringConversion.ToText` wraps reference-type receivers (`record class`, plain class, interface, `string`, `object`) in `$eq.receiver(...)` unless the receiver syntax is a non-null literal, `new`, `this` or `base`, so `x.ToString()` on `null` throws `NullReferenceException` while interpolation and concatenation continue to use `ToDotNetString` (`null` -> `""`).

### 4. Refusing `GetType()` and deduplicating chained `EQ2004` diagnostics (#903)
- `InvocationStrategy.ReportIfUntranslatable` no longer exempts `System.Object` methods (which were only exempted before `Equals`, `ReferenceEquals`, `ToString` and `GetHashCode` had dedicated strategies). Untranslated `object` methods like `GetType()` now report `EQ2004`.
- `PlatformMemberRead` exposes `IsFromRefusedRead` and `RecordRefused` so `InvocationStrategy.ReportIfUntranslatable` suppresses cascading `EQ2004` diagnostics when the invocation's receiver was already refused (e.g. `typeof(string).Name.Trim()`) and records refused invocations so subsequent member reads (`x.GetType().Name`) report only the root `EQ2004`.
- `MemberAccessStrategy` and `IdentifierStrategy` check platform method groups (`"a".GetType`, `GetType`) via `InvocationStrategy.ReportIfUntranslatable` so taking an untranslated platform method as a delegate also fails the build with `EQ2004`.
