# Proposal

Closes #822 and #839, two Bugs under #565 (The transpiler's fences hold on every path, continued).

## Why

A name that reaches the browser is either a translation, as .NET answers it, or a refusal at the build
naming the member (EQ2004). Several paths wrote a name nothing in the browser defines, and the build said
nothing:

- A method GROUP of a .NET member went out as a name. `Func<string, string> f = ts.ToString` was
  `ts.toString`, unbound, a TypeError reading `this.ticks` when called; `items.Select(DateTime.Parse)`
  called the twin's parse once with no text; `items.Select(Convert.ToDateTime)` named a `Convert` nothing
  defines; `Select(Math.Round)` was JavaScript's `Math.round`, which rounds a half up; `str.Contains`,
  `list.Add` and `int.Parse` named members no string, array or number has. Each type strategy that saw a
  group wrote it its own way, and most wrote a name.
- A type strategy claimed every member of its type and camel-cased it: `ts.TryFormat(…)` was
  `ts.tryFormat(…)`, `ts.Subtract(…)` named a `subtract` the twin calls `sub`, `ts.Multiply(…)`,
  `ts.Microseconds`, `DateTime.Compare(…)`, `d.ToBinary()`, `t.IsBetween(…)` and an instant's
  `ToUniversalTime()` were members no twin had, and `ts * 2` and `-ts` were JavaScript's own operators,
  NaN. Measured by executing every public member of DateTime, DateOnly, TimeOnly, DateTimeOffset and
  Guid against the runtime: 29 threw or read undefined, beside the 11 TimeSpan members its twin did not
  carry.
- `FormattableString.Invariant($"…")` and `string.Create(provider, $"…")` were refused as calls nothing
  translates, and a `FormattableString` made elsewhere held the text of the page's culture, which a
  provider handed to its `ToString` later could not write again.
- A static member of a PACKAGE's type (an assembly the build has no source for) was written as its
  name: `Status.Label` was `Status.label`, a ReferenceError at the page's first render, and so was a
  construction, `new Gauge("a")`. A call of the same type was refused already. A construction of a
  .NET type no strategy builds was the same: `new Random()`, `new Stopwatch()`, `new Lazy<int>(…)`, and
  `new Func<int>(…)`, which named a `Func` nothing defines.

## What Changes

For a developer, every one of these names either answers as .NET does or fails the build naming it:

- A method group of a member no twin carries (the platform's, a package's) is the delegate its call is:
  an arrow over the method's own parameters, whose body is the call those parameters make, translated as
  any call is. `items.Select(DateTime.Parse)` reads each text in the page's culture, and `Select`'s index
  never reaches the twin's culture flag; `Func<string, string> f = ts.ToString` writes the format it is
  handed; `Math.Round` as a delegate rounds a half to even. The receiver is read once, where the
  delegate is made, and a null one is refused there as .NET refuses it (ArgumentException for a method
  .NET calls directly, NullReferenceException for a virtual one). A group whose call has no translation
  fails the build with the call's own refusal, and a group whose method hands a value back through an
  out or a ref fails with EQ2004. The app's and the framework's own groups bind as they did. Three
  answers the specs held move with it: a group of a BCL extension whose call translates, `list.Any`,
  is that call, where it was EQ2004; a char's casing with a culture taken as a delegate is refused as
  the call with a culture it cannot name is (EQ2108, where it was EQ1004); and a queue's `q.Enqueue`
  handed to `ForEach` is the delegate its call is, where it was the member bound to `q`, which
  enqueued the same.
- A TimeSpan, a DateTime, a DateOnly, a TimeOnly and a DateTimeOffset answer every member their twins
  carry, and the members they did not carry and that are plain arithmetic now do: a span's
  `Microseconds`, `Nanoseconds`, `TotalMicroseconds`, `TotalNanoseconds`, `Multiply`, `Divide`,
  `Subtract`, `Compare` and the static `Equals`, its `*`, `/` and sign, with .NET's rounding and its
  refusals; a date's `Compare`, `Equals` and `UnixEpoch`; a `DateOnly`'s `ToDateTime`; a `TimeOnly`'s
  `Add`, `IsBetween`, `ToTimeSpan`, `Microsecond` and `Nanosecond`; an instant's `DayOfYear`,
  `TotalOffsetMinutes`, `ToUniversalTime`, `EqualsExact`, `Compare`, `Equals` and `UnixEpoch`; and
  subtracting a date or an instant is the span between them. A span's sum, difference, negation and
  duration refuse what overflows as .NET does. The interop forms (`ToBinary`, `FromBinary`,
  `ToFileTime`, `FromFileTime`, `ToOADate`, `FromOADate`), `IsDaylightSavingTime`, `GetTypeCode` and
  `TryFormat` fail the build with EQ2004.
- `FormattableString.Invariant($"…")` and `string.Create(CultureInfo.InvariantCulture, $"…")` write every
  hole in the invariant culture, and `FormattableString.CurrentCulture($"…")` and `string.Create` with the
  current culture or a null provider in the page's. Another culture is EQ2108. An interpolation made a
  `FormattableString` or an `IFormattable` anywhere else is EQ2004: the browser holds the text, not the
  format and the arguments.
- A static of a package's type, read qualified or through `using static` and called through `using
  static`, and a construction of one, fail the build with EQ2004 naming the member, the type and the
  assembly; a call already did, and now names the assembly too. A value of a package's type keeps
  reading its members, as the data the server sends; a constant and an enum's member are their values.
- A construction of a .NET type no strategy builds fails the build with EQ2004 naming the type. A
  delegate built with `new` is the function it is built from.
- Migration for an app that used one of these: it compiled and failed in the browser, and it now
  fails the build or answers. Construct a package's type, or read its statics, in a `[ServerAction]`,
  or reference the project it is written in, whose types the build compiles.

## Impact

- eqc: `MethodGroupStrategy` is new and lowers every group of a member no twin carries, asked before
  every other strategy; the call it stands for is bound where the group is written, by a speculative
  model `SemanticHelper` now answers for (`Speculate`). `TwinlessTypeExtensions` is the one predicate the
  call fence, the read fence, the construction fence and the group rule ask. `TimeSpanStrategy`,
  `DateTimeStrategy`, `DateOnlyTimeOnlyStrategy` and `DateTimeOffsetStrategy` claim only what their
  twins carry; `InterpolationInACultureStrategy` is new; `InterpolatedStringStrategy` and `ValueFlow`
  write the holes of an interpolation a call names the invariant culture for in it;
  `PlatformMemberRead`, `UsingStaticSymbolExtensions`, `InvocationStrategy` and
  `ObjectCreationStrategy` refuse a package's statics and constructions; the group paths of
  `CharMethodStrategy` and `CancellationStrategy` go, now the rule's. The public surface gains
  `MethodGroupStrategy`, `InterpolationInACultureStrategy` and `Eq.DelegateTarget`; the developer
  surface does not move.
- The runtime: the TimeSpan, DateOnly, TimeOnly and DateTimeOffset twins and the date and instant
  factories gain the members above, and `$eq.delegateTarget` refuses a delegate's null target.
- The BCL audit's 10 verdicts of DateTime's and DateTimeOffset's interop forms and of
  `IsDaylightSavingTime` move from `eq` and `native` to `fenced:EQ2004`: each named a member no twin had.
- Documentation: `docs/DIAGNOSTICS.md`'s EQ2004 row, the wiki's Diagnostics and SupportedFeatures
  pages in every language each is written in, and one ledger entry.
- Not reached: a type test or an `as` of a package's type answers as a null check, where .NET answers
  by the type (reported apart); a group of a member of `object` made on a value of an anonymous type,
  whose type no source can name, keeps the lowering it had.
