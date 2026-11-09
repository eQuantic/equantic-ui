## ADDED Requirements

### Requirement: A server-only override of what the runtime calls is refused where it is declared

A `[ServerOnly]` override of `ToString`, `Equals(object)` or `GetHashCode` on a type that crosses SHALL fail
the build with EQ2010 where it is declared: the runtime calls those on its own, for text and for equality,
and no twin holds it. A type that does not cross, and an overload of those names, SHALL build.

#### Scenario: A value shown in a page

- **WHEN** a class, a record or a struct overrides `ToString` with a `[ServerOnly]` method, and a page shows
  `$"{value}"`
- **THEN** the build fails with one EQ2010 at the override, where the browser printed the type's name or its
  synthesized text

#### Scenario: A type that stays on the server

- **WHEN** the type carrying the override is itself `[ServerOnly]`
- **THEN** it builds

#### Scenario: An overload

- **WHEN** a `[ServerOnly]` `ToString(string)` sits on a type that crosses
- **THEN** it builds, since nothing but the code calls it

### Requirement: A type test of a package's type is refused

`is`, `as` and a type pattern over a type of an assembly the build has no source for SHALL fail the build with
EQ2004 naming the type and its assembly, whatever kind of type it is: nothing in the browser can say a value is
one.

#### Scenario: A class, a struct, an interface and an enum of a package

- **WHEN** a page writes `o is Vendor.Gauge`, `o as Vendor.Gauge`, `o switch { Vendor.Gauge => … }`, or tests
  against a package's record, struct, interface or enum, or through an alias
- **THEN** each fails the build with one EQ2004 naming the type and `Vendor`, where `is` answered true for any
  value and `as` handed the value back

#### Scenario: A refused call told once

- **WHEN** a page calls `string.IsInterned("a")` or `String.IsInterned("a")`
- **THEN** each reports one EQ2004 at the call, and no EQ1001 for the keyword
