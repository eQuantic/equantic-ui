## ADDED Requirements

### Requirement: Every route a comparison takes meets the server-only fence

A comparison the compiler lowers SHALL ask the server-only fence of the equality it reaches by every route: a collection's comparer (`HashSet<T>`, a `Dictionary<TKey, TValue>`'s key, `List<T>.Contains`), a tuple's and an anonymous object's member, and a call through a contract. One that reaches a `[ServerOnly]` `Equals`, or generic code that may reach a server-only typed `Equals`, SHALL be refused with EQ2010 where it is written.

#### Scenario: A collection over a server-only Equals

- **WHEN** client code writes `new HashSet<Vaulted> { c }.Contains(c)`, where `Vaulted` overrides `Equals` with `[ServerOnly]`
- **THEN** the build fails with EQ2010 at that call

#### Scenario: A comparison that reaches none

- **WHEN** client code compares values whose `Equals` is not server-only by the same routes
- **THEN** the build succeeds
