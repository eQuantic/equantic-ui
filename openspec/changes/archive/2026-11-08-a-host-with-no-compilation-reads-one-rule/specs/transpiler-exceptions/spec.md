## ADDED Requirements

### Requirement: A member of an exception class with no module fails the build

In a host with no project's compilation, a member an app's exception class declares SHALL fail the build with
EQ2010 where it is reached: such a class has no module there, and is built as the runtime's exception. Its
construction, a `throw`, a typed `catch` and what the runtime's exception carries SHALL build.

#### Scenario: A snippet compiled on its own

- **WHEN** a snippet declares `class Failure : Exception` with a static `Count`, a field `Code` and a method
  `Describe()`, and another class reads `Failure.Count`, `f.Code` or `f.Describe()`
- **THEN** each fails the build with one EQ2010 naming the member, where it failed in the browser

#### Scenario: What the runtime's exception carries

- **WHEN** the same snippet throws `new Failure("m")`, catches it by its type and reads `Message`
- **THEN** it builds
