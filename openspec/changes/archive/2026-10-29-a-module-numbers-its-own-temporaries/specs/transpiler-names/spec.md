## ADDED Requirements

### Requirement: A module names its temporaries by its own code alone

The temporaries a module declares SHALL be numbered by the module's own code, from `$n0`, whatever the build
emitted before it: a component's module, a plain class's, a static helper's and a record's alike. A module's
text SHALL be the text it has when it is emitted alone.

#### Scenario: A plain class compiled after one that binds two temporaries

- **WHEN** one compiler compiles a plain class whose method binds two temporaries, then a plain class whose
  method writes `return Name()?.ToUpperInvariant();`
- **THEN** the second module's first temporary is `$n0`, and its text is the text it has when a compiler
  compiles it alone

#### Scenario: A static helper compiled after one that binds two temporaries

- **WHEN** one compiler compiles a plain class whose method binds two temporaries, then a static class whose
  method writes `return Name()?.ToUpperInvariant();`
- **THEN** the static class's first temporary is `$n0`, and its module is the one it has alone

#### Scenario: Two records through one converter

- **WHEN** the record emitter writes the module of a record that binds two temporaries, then the module of
  another, through one converter
- **THEN** the second module's first temporary is `$n0`, and its text is the text it has through a converter
  of its own
