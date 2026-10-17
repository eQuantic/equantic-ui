## Why

Closes #902, #903, #909, #912, #926 and #927, sub-issues of Feature #849 (Runtime and BCL Conformance).

Today several `object` and `Enum` operations diverge from .NET 10 in the browser:

- `object.Equals(a, b)`, `Object.Equals(a, b)`, `System.Object.Equals(a, b)` and bare `Equals(a, b)` in a class emit `object.equals(a, b)`, `Object.equals(a, b)` or `this.equals(a, b)` and throw `ReferenceError` or `TypeError` in the browser (#902).
- `x.GetType()` and the `x.GetType` / `GetType` method groups compile and throw `TypeError: x.getType is not a function` in the browser instead of failing the build with `EQ2004`, while chaining a member on `x.GetType().Name` reports two `EQ2004` diagnostics instead of one (#903).
- Calling `Equals(object)` inherited from `object` on a class, array, list or `StringBuilder` is either refused with `EQ2004` (`StringBuilder`) or throws `equals is not a function` in the browser, and comparing two `List<T>` or arrays held as `object` in `Set`, `Dictionary`, `IndexOf` or `object.Equals` compares their items structurally because `isPlainValue` treats every JavaScript `Array` as a tuple (#909).
- Calling `p.Equals(other)` or `p.ToString()` on a `null` `record class`, plain class, `string` or `object` returns `false`, `"null"` or `""` instead of throwing `NullReferenceException` after evaluating arguments (#912).
- Calling `Equals` on an enum (or `Nullable<TEnum>`, or taking `s.Equals` as a method group) throws `s.equals is not a function` in the browser, and calling `GetHashCode()` on a non-`[Flags]` enum hashes its camelCase string key instead of its underlying numeric value, disagreeing with `(int)s.GetHashCode()` (#926).
- An enum member that aliases an earlier member's numeric value lowers to its own camelCase name instead of the canonical member's key that .NET gives its value, causing equality, sets, dictionaries, `ToString()` and `ToDictionary` to disagree with .NET (#927).

## What Changes

- Lower two-argument `object.Equals(a, b)` (qualified or bare) through `StructuralEqualsStrategy` as .NET answers it: the first parameter's own `Equals`, chosen from its type before C# boxed it and bound by parameter rather than by position, the second operand's type read only where the first one's says nothing, and false where a type that refuses every other meets one known to be another (a byte and an int, a char and a string, a tuple and an array), both arguments still evaluated as written (#902).
- Refuse `GetType()` and untranslated platform method groups (`"a".GetType`, `GetType`) at build time with `EQ2004`, and record refused platform invocations so chained reads (`x.GetType().Name`, `typeof(string).Name.Trim()`) report a single `EQ2004` at the root (#903).
- Lower one-argument `Equals(object)` inherited from `object` on any reference type through `$eq.equalsInstance` (using `$eq.collections.sameItem` for classes, collections and arrays without a value override, and on an `object` receiver the comparison the static type of either value, read before C# boxed it, still says, `$eq.collections.sameKey` where neither says anything), so an array, a list or a class that does not override `Equals` compares by reference wherever its type is in sight (#909). A list or an array held as `object` on both sides stays compared by its elements in `key-equality.ts`: the browser holds it as the same JavaScript array as a value tuple, which compares by its elements, and telling them apart needs a mark set where the value is created, a change of its own.
- Guard `Equals` on reference-type records with `$eq.equalsInstance` and guard `ToString()` on reference-type receivers (`record class`, plain class, interface, `string`, `object`) with `$eq.receiver` so a `null` receiver throws `NullReferenceException` after argument evaluation, while interpolation and concatenation of `null` continue to write `""` (#912).
- Lower `Equals` and `s.Equals` on enums and `Nullable<TEnum>` through `StructuralEqualsStrategy` / `MemberAccessStrategy` (checking boxed argument type compatibility, and refusing an empty `Nullable` enum's group where it is made), and lower `GetHashCode()` and `s.GetHashCode` on enums by hashing the enum's numeric value via `EnumShape.ValueOf` in its underlying type's representation, a 64-bit enum's as a BigInt (#926).
- Give an aliased enum member the canonical camelCase key and canonical `.NET` name of the first member with its numeric value in `EnumShape`, deduplicate keys in `KeyToValue` and `ValueToKey`, fold constant enum `ToString()` to the canonical name, and remove the `ToDictionary` refusal on enums with aliases (#927).

## Impact

- Reaches `eQuantic.UI.Compiler` (`StructuralEqualsStrategy`, `ElementEquality`, `GetHashCodeStrategy`, `EnumShape`, `EnumStrategy`, `IsPatternStrategy`, `ToStringStrategy`, `StringConversion`, `LinqTableStrategy`, `LinqKeys`, `MemberAccessStrategy`, `IdentifierStrategy`, `InvocationStrategy`, `PlatformMemberRead`) and `eQuantic.UI.Runtime` (`key-equality.ts`).
- Neither `PublicAPI.Shipped.txt` / `PublicAPI.Unshipped.txt` nor `developer-surface.baseline.txt` moves.
