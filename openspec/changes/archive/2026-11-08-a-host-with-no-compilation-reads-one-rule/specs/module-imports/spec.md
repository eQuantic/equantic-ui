## ADDED Requirements

### Requirement: A host with no project's compilation reads one rule

The parser and the dependency scan SHALL answer "is this class a component" by one rule. The chain of bases SHALL
decide wherever it binds, a non-private instance `Build` or `Render` where a base does not, and the base's written
name as well where there is no model. A plain class SHALL import a `[RuntimeProvided]` type through the scan where
the host has no project's compilation, as a component does.

#### Scenario: A private Build over a base that does not bind

- **WHEN** the scan reads `class Legacy : MissingBase { private int Build(int c) => c; }`, or a static `Render`, or
  a `CreateState`
- **THEN** it files a plain class, as the parser writes one

#### Scenario: A plain class over a runtime-provided helper

- **WHEN** a host with a scan and no compilation compiles `public class Probe { public int Measure() => PretendHelper.MinWidth(); }`
- **THEN** its module imports `PretendHelper` from `@equantic/runtime`
