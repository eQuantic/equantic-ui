## ADDED Requirements

### Requirement: A comparison that reaches a server-only Equals is refused where it is written

Client code that calls `EqualityComparer<T>.Default.Equals`, `Nullable.Equals`, or `Equals` on a struct or a
nullable struct value, each of which asks the value's own equality, SHALL fail the build with one EQ2010 where
the call is written when that equality is marked `[ServerOnly]`, which no twin holds, naming it as a call bound
to it names it: an `Equals(object)` override the value's type declares or inherits, and a typed `Equals`
answering the type's `IEquatable<T>`, which the default comparer asks first and a record's synthesized
`Equals(object)` asks. Where the type does not say whose equality its value answers, a base class that
overrides nothing, an interface, `object` or a type parameter, whose comparer asks the type argument's
`IEquatable<T>` too, the comparison SHALL be refused as a call through the contract it reaches is. A
comparison that can reach no such member SHALL build.

#### Scenario: The default comparer over a class whose Equals is server-only

- **WHEN** a sealed class overrides `Equals(object)` with a `[ServerOnly]` method that answers false, and a page
  calls `EqualityComparer<Vaulted>.Default.Equals(c, c)`, or the same over a class that inherits such an
  override, or over a class whose typed `Equals` answering `IEquatable<T>` is `[ServerOnly]`
- **THEN** each fails the build with one EQ2010 at the call naming the method, as `c.Equals(c)` does, where it
  built, fell back to identity and answered True

#### Scenario: A comparison that may reach one

- **WHEN** `Closed : Open` overrides `Equals(object)` with a `[ServerOnly]` method where `Open` overrides
  nothing, and a page calls `EqualityComparer<Open>.Default.Equals(x, x)`, or the same through an interface
  `Closed` implements, `object` or a type parameter of generic code
- **THEN** each fails the build with one EQ2010 at the call, saying `'object.Equals' may reach 'Closed.Equals'`,
  and generic code in a build whose one server-only equality is a typed one is refused through
  `IEquatable<T>.Equals`

#### Scenario: A struct whose equality is server-only

- **WHEN** a struct overrides `Equals(object)` with a `[ServerOnly]` method, or a record struct's typed `Equals`
  is `[ServerOnly]`, and a page calls `v.Equals(v)`, `a.Equals(b)` over two nullable values,
  `Nullable.Equals(a, b)` or the default comparer of the struct or of its nullable values
- **THEN** each fails the build with one EQ2010 at the call naming the method, where it built and compared the
  struct's members, or called a typed method no twin carries

#### Scenario: A comparison that can reach none

- **WHEN** the same build compares a sealed class with no override and a struct with no `Equals` of its own by
  their default comparer, two nullable values of that struct by `Equals` and `Nullable.Equals`, and two `int`s
  by theirs
- **THEN** each builds and lowers as it did
