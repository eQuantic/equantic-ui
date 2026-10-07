# Spec Delta

## ADDED Requirements

### Requirement: A record crosses as its twin

An in-source record or struct that crosses into the browser, as a Server Action's result, a page's
state, a topic's payload or a member of any of them, SHALL be rebuilt on its twin whether or not any of
its members needs coercion, so its methods, its equality and `with` work on it there.

#### Scenario: A record none of whose members needs coercion

- **WHEN** a Server Action returns a `record Notice(string Text)` that declares a method
- **THEN** the browser receives a `Notice`, and the method answers on it as in C#

#### Scenario: A record held by a record

- **WHEN** a record that crosses holds another record among its members
- **THEN** the one it holds is rebuilt on its own twin too

#### Scenario: A generic record

- **WHEN** a `Box<long>` crosses, where `record Box<T>(T Value)`
- **THEN** its `Value` arrives as a long: the constructed type's members describe it, since the twin's
  own map cannot know `T`
