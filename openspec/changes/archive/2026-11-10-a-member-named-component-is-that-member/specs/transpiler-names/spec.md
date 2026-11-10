## ADDED Requirements

### Requirement: A member named Component is that member

A bare `Component` SHALL be read by its symbol, as every other name is: a member of that name SHALL be
written as that member, and a local, a parameter or a local function of that name SHALL be that binding.
No name SHALL be mapped to a field the runtime does not declare.

#### Scenario: A property named Component, read inside and outside

- **WHEN** a class sets its `Component` property in its constructor, and one method reads it bare while another class reads it through an instance
- **THEN** both read the value set, as in .NET, where the bare read was a field nothing declared
