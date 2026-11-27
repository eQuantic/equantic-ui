## ADDED Requirements

### Requirement: A static generic class keeps one set of statics per closed type

A static generic class SHALL keep one set of statics per closed type, as C# does: each closed type SHALL hold its own
fields and run its type initializer, its static constructor included, on its own first use. The runtime SHALL keep one
object per closed type, made from the class the first time it is used, and the class's static members SHALL read their
statics through the closed type they were reached through. Code outside the class SHALL reach the closed type it names.
The twin SHALL declare no type parameters, since TypeScript refuses a static member that reads them, and SHALL pass the
runtime's `tsc`. A static member reached through a type argument known only at run time SHALL be refused (EQ2018).

#### Scenario: Two closed types hold their own statics

- **WHEN** an app calls `Cache<int>.Keep(1)`, `Cache<int>.Keep(2)` and `Cache<string>.Keep("a")`
- **THEN** `Cache<int>.Hits` is 2, `Cache<string>.Hits` is 1, and each closed type's static constructor ran once, as
  .NET answers, with type annotations and without

#### Scenario: The twin passes tsc

- **WHEN** a static generic class's static members name its type parameter, and another class calls two of its closed types
- **THEN** the class's module declares no type parameters, the other's imports it, and the runtime's `tsc` takes both

#### Scenario: A type argument known only at run time

- **WHEN** a generic method calls `Cache<U>.Keep(u)`
- **THEN** the build refuses it with EQ2018, naming `Cache<U>`
