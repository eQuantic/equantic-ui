# Proposal

Closes #847, a Bug under #845 (The transpiler's fences hold on every path, third part).

## Why

A property or a field of a .NET type that no strategy translated went out as its camel-cased name
on whatever its receiver became, and the build said nothing. `CultureInfo.CurrentCulture.Name` was
`CultureInfo.currentCulture.name`, a name the runtime does not export, so the page threw a
ReferenceError where .NET answers; `Environment.NewLine`, `TimeZoneInfo.Local`, `typeof(T).Name` and a
list's `Capacity` read undefined. A CALL of such a member was already refused (EQ2004) by the
invocation fallback; a READ was not, and a fence that guards calls and not reads is none.

The BCL audit counted 17 of these reads as `native`, plain JavaScript, though each named a member
nothing in the browser defines: a `List`'s, a `HashSet`'s and a `Dictionary`'s `Capacity`, the
`Comparer` of a `HashSet`, a `Dictionary` and a `SortedDictionary`, an array's `Rank`, `LongLength`,
`IsFixedSize`, `IsReadOnly`, `IsSynchronized`, `SyncRoot` and `Array.MaxLength`, a `Guid`'s
`Version` and `Variant`, `decimal.Scale` and `Convert.DBNull`. The wiki said `typeof(T).Name` was a
supported constant; it read undefined.

## What Changes

For a developer, every property and field of a .NET type either answers or fails the build:

- A read of one that no strategy translates fails the build with EQ2004, naming the member, unless a
  value the browser holds carries it by its own name: a string's, an array's and a span's `Length`,
  the members an exception's twin carries (`Message`, `InnerException`, `ParamName`, `ActualValue`,
  `ObjectName`, `TypeName`, `InnerExceptions`), and a `KeyValuePair`'s `Key` and `Value`. A member
  an interface declares is not asked: the value read through it may be a type of the app's own,
  whose twin carries it by that name.
- A chain is refused once, at its first such member, through `?.` as through `.`:
  `CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator` reports `CurrentCulture`.
- The culture handed to a char's `ToUpper` or `ToLower` is read from how the call names it and no
  longer converted as a value, which the browser has none of, so `char.ToUpper(c,
  CultureInfo.InvariantCulture)` keeps building.
- Migration for an app that read one of these: it compiled and failed in the browser, and it now
  fails the build. Read a culture's or the machine's data in a `[ServerAction]` and hand the browser
  its answer; for a type's name, write `nameof(T)`.

## Impact

- eqc: `PlatformMemberRead` is new, and `MemberAccessStrategy` asks it of every read it reaches;
  `CharMethodStrategy` no longer converts a culture argument. The BCL audit's 17 reads above move
  from `native` to `fenced:EQ2004`. Neither the public surface (the new class is internal) nor the
  developer surface moves.
- Documentation: `docs/DIAGNOSTICS.md`'s EQ2004 row, and the wiki's Diagnostics (English and
  Portuguese) and SupportedFeatures (five languages) pages, where `typeof(T).Name` is no longer
  called a constant.
- Not reached: a property pattern (`culture is { Name: "en-US" }`) names its members through
  `PatternConverter` and is not asked (#920). A `Dictionary`'s and a `HashSet`'s capacity, which the
  runtime keeps, could answer rather than be refused; that is a feature, not this fix.
