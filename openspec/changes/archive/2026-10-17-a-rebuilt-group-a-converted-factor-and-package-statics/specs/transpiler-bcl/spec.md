## MODIFIED Requirements

### Requirement: A package's type has no name in the browser

A static member of a type of an assembly the build has no source for, a package's, of any kind of type,
an interface's included, and whatever its name, `Count` included, read qualified, through an alias named
like any other type, read or called through `using static`, called qualified, or taken as a method group, and a construction of
such a type, SHALL fail the build with EQ2004 naming the member or the type and the assembly it comes
from, and what is read or called from one SHALL be refused with it, told once at its root. A value of
such a type SHALL keep reading its members by name, as the data the server sends, and a constant and an
enum's member SHALL be their values.

#### Scenario: A static through an alias named like a platform type

- **WHEN** a page reads `Int32.MaxValue` under `using Int32 = Vendor.Status`, `DateTimeOffset.Now` under
  `using DateTimeOffset = Vendor.DateTime`, or `String.Empty` under `using String = Vendor.Status`
- **THEN** the build fails with EQ2004 naming the package's member and `Vendor`, where a strategy that
  claims the platform type by its spelling wrote its member: `Int32.MaxValue` was 2147483647

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

#### Scenario: A chain from a package's static or construction

- **WHEN** a page writes `Status.Make().GetType().Name`, `Status.Culture().Name` or
  `new Gauge("a").GetType().Name`, where `Status.Culture()` answers a `CultureInfo`
- **THEN** the build fails with one EQ2004, naming `Vendor.Status.Make`, `Vendor.Status.Culture` or
  `Vendor.Gauge` and `Vendor`, where `object.GetType` and `CultureInfo.Name` were told again

#### Scenario: What names no class

- **WHEN** a page reads `Status.Answer`, a constant, `Level.High`, an enum's member, or `nameof(Gauge)`
- **THEN** the build succeeds and the module holds their values

### Requirement: A construction of a class the browser has no twin of fails the build

A construction of a .NET type that no translation builds SHALL fail the build with EQ2004 naming the
type, a target-typed one included, where the module would have constructed a class nothing defines, and
what is read or called from it SHALL be refused with it, told once at the construction. A collection the
browser holds as the array of its elements is built as one.

#### Scenario: A random source and a stopwatch

- **WHEN** a component writes `new Random()`, `Random r = new()`, `new Stopwatch()`, `new Lazy<int>(…)` or
  `new Tuple<int, int>(1, 2)`
- **THEN** the build fails with EQ2004 naming `System.Random`, `System.Diagnostics.Stopwatch`,
  `System.Lazy<int>` or `System.Tuple<int, int>`

#### Scenario: A chain from a construction

- **WHEN** a component writes `new Stopwatch().Elapsed` or `new Random().GetType()`
- **THEN** the build fails with one EQ2004, naming `System.Diagnostics.Stopwatch` or `System.Random`,
  where `Stopwatch.Elapsed` and `object.GetType` were told again
