## MODIFIED Requirements

### Requirement: A package's type has no name in the browser

A static member of a type of an assembly the build has no source for, a package's, of any kind of type,
an interface's included, and whatever its name, `Count` included, read qualified, read
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
