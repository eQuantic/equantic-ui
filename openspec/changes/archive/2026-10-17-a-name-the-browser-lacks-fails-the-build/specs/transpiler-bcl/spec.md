## ADDED Requirements

### Requirement: A package's type has no name in the browser

A static member of a type of an assembly the build has no source for, a package's, read qualified, read
or called through `using static`, called qualified, or taken as a method group, and a construction of
such a type, SHALL fail the build with EQ2004 naming the member or the type and the assembly it comes
from. A value of such a type SHALL keep reading its members by name, as the data the server sends, and
a constant and an enum's member SHALL be their values.

#### Scenario: A static read of a package's class

- **WHEN** a page of an app that references the package `Vendor` writes `Text(Status.Label, …)`, where
  `Vendor.Status` is a class of that package
- **THEN** the build fails with EQ2004 naming `Vendor.Status.Label` and `Vendor`, and writes no module
  naming `Status`

#### Scenario: An eQuantic package the SDK does not ship

- **WHEN** a page reads `Clock.Zone`, calls `Clock.Now()` or constructs `new Clock("a")`, where
  `eQuantic.Core.Timing.Clock` is a class of the package `eQuantic.Core`
- **THEN** the build fails with EQ2004 naming the member and `eQuantic.Core`: the SDK ships twins of its
  own assemblies and of what the runtime provides, never of a namespace that merely starts like its
  own

#### Scenario: A construction of a package's class

- **WHEN** a page writes `new Gauge("a").Name`, `new Money(1m, "EUR")` or a target-typed `new("t")` of
  `Vendor.Gauge`
- **THEN** the build fails with EQ2004 naming the type and `Vendor`

#### Scenario: What names no class

- **WHEN** a page reads `Status.Answer`, a constant, `Level.High`, an enum's member, or `nameof(Gauge)`
- **THEN** the build succeeds and the module holds their values

### Requirement: A construction of a class the browser has no twin of fails the build

A construction of a .NET type that no translation builds SHALL fail the build with EQ2004 naming the
type, a target-typed one included, where the module would have constructed a class nothing defines. A
collection the browser holds as the array of its elements is built as one.

#### Scenario: A random source and a stopwatch

- **WHEN** a component writes `new Random()`, `Random r = new()`, `new Stopwatch()`, `new Lazy<int>(…)` or
  `new Tuple<int, int>(1, 2)`
- **THEN** the build fails with EQ2004 naming `System.Random`, `System.Diagnostics.Stopwatch`,
  `System.Lazy<int>` or `System.Tuple<int, int>`

### Requirement: An interpolation is written in the culture a call names

`FormattableString.Invariant($"…")` and `string.Create(CultureInfo.InvariantCulture, $"…")` SHALL write
every hole of the interpolation in the invariant culture, and `FormattableString.CurrentCulture($"…")` and
`string.Create` with the current culture or a null provider in the page's, as .NET writes them, a hole's
format and alignment included. A hole that is itself text, an interpolation or a `ToString()`, SHALL be
the text it wrote in the page's culture, and a hole held as an object or as dynamic SHALL write the number
or the date it holds in the culture the call names. A `FormattableString` SHALL evaluate every hole
before it writes any, as it holds its arguments, where `string.Create` writes each hole as it is
evaluated. Another culture SHALL fail the build with EQ2108, and an interpolation made a
`FormattableString` or an `IFormattable` anywhere else SHALL fail the build with EQ2004.

#### Scenario: A hole whose evaluation changes an earlier one

- **WHEN** browser-side code writes `FormattableString.Invariant($"{sb}|{sb.Append("b")}")` and
  `string.Create(CultureInfo.InvariantCulture, $"{sb}|{sb.Append("b")}")` over a builder holding "a"
- **THEN** they are `ab|ab` and `a|ab`, as in .NET

#### Scenario: The invariant culture on a pt-BR page

- **WHEN** browser-side code in pt-BR writes `FormattableString.Invariant($"{1234.5}|{-3}|{d}|{x:N2}")`
- **THEN** it is `1234.5|-3|01/15/2024 21:30:05|1,234.50`, as in .NET

#### Scenario: A FormattableString made elsewhere

- **WHEN** a component writes `FormattableString text = $"{x}";`
- **THEN** the build fails with EQ2004 naming `System.FormattableString`

## MODIFIED Requirements

### Requirement: A text changes case as .NET changes it

`ToUpper` and `ToLower` and their invariant pair, of a `string` and of a `char`, SHALL change each
UTF-16 code unit, or surrogate pair, to exactly one other, by .NET 10's simple case mappings, so a
text's length never changes: a letter whose full mapping lengthens SHALL keep its case, but for the
Greek letters with an iota subscript, which SHALL upper-case to the capital with a prosgegrammeni; a
capital sigma SHALL lower-case to the medial one wherever it stands; a lone surrogate SHALL stay as it
is; and a code point .NET 10 does not case over a Linux or Windows server's ICU SHALL keep its case
on an engine of a later Unicode, where .NET on macOS 26 cases it by Apple's ICU. The two
i's SHALL be the culture's: tr and az, by their language, SHALL upper-case `i` to `İ` and lower-case
`I` to `ı`, every other culture SHALL upper-case `ı` to `I` and lower-case `İ` to `i`, and the
invariant culture SHALL leave `İ` and `ı` as they are. No culture, `CultureInfo.CurrentCulture` and a
null SHALL read the page's culture, which is the current one, and `CultureInfo.InvariantCulture` and
the invariant pair the invariant one; any other culture SHALL fail the build (EQ2108). A char's culture
handed as null SHALL throw .NET's ArgumentNullException after the char is evaluated, and a char's
casing with a culture taken as a delegate SHALL fail the build as its call with a culture it cannot
name does (EQ2108).

#### Scenario: A letter whose full upper case lengthens

- **WHEN** `"straße".ToUpper()` runs
- **THEN** it answers `STRAßE`, as in .NET, where it answered `STRASSE`

#### Scenario: A final sigma

- **WHEN** `"ΟΔΟΣ".ToLower()` runs
- **THEN** it answers `οδοσ`, as in .NET, where it answered `οδος`

#### Scenario: The i of a Turkish page

- **WHEN** `"i".ToUpper()` runs in tr-TR
- **THEN** it answers `İ`, as in .NET, where it answered `I`

#### Scenario: The invariant culture keeps the dotless i

- **WHEN** `"ı".ToUpperInvariant()` runs
- **THEN** it answers `ı`, as in .NET, where it answered `I`

#### Scenario: A char's null culture

- **WHEN** `char.ToUpper('a', null)` runs
- **THEN** it throws ArgumentNullException for `culture`, as in .NET, where it answered `A`

#### Scenario: A culture the browser does not know

- **WHEN** `text.ToUpper(CultureInfo.GetCultureInfo("tr-TR"))` is built
- **THEN** the build fails with EQ2108, where the culture was dropped

#### Scenario: A char's casing with a culture, as a delegate

- **WHEN** `new Func<char, CultureInfo, char>(char.ToUpper)` is built
- **THEN** the build fails with EQ2108, as the call with a culture it cannot name does

### Requirement: A member a collection's twin does not carry fails the build

eqc SHALL lower a member of `Queue<T>`, `Stack<T>`, `LinkedList<T>`, `LinkedListNode<T>` or
`SortedSet<T>` to the runtime twin's member only when the twin carries it, and SHALL refuse any other
member of the five with EQ2004, naming the member; it used to go out under its camel-cased name and
fail in the browser. `ToString`, `Equals` and `GetHashCode` SHALL be translated as every object's. A
member reached as a method group SHALL keep its receiver, and a take reached as one SHALL be refused.
A sorted set's `GetViewBetween` SHALL be the twin's.

#### Scenario: A sorted set's comparer

- **WHEN** a component reads `s.Comparer` on a `SortedSet<int>`
- **THEN** the build fails with EQ2004

#### Scenario: A view of a sorted set

- **WHEN** a component calls `s.GetViewBetween(1, 2)` on a `SortedSet<int>`
- **THEN** the lowering is `s.getViewBetween(1, 2)`, where the build failed with EQ2004

#### Scenario: A method group

- **WHEN** a component passes `q.Enqueue` to `List<int>.ForEach`
- **THEN** it is the delegate `q.Enqueue(item)` is, made over `q`, and each element is enqueued on `q`
