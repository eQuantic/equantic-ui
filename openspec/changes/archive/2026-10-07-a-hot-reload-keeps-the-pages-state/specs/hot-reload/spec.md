# Spec Delta

## ADDED Requirements

### Requirement: A page keeps its fields across a hot reload

A hot reload SHALL hand the reloaded page the fields it held before the save that are data, the ones its C# declares,
each rebuilt in the shape its initializer gives it, before it builds. A field the edit removed SHALL
be left behind, a field the edit added SHALL keep its initializer, and a field the reload cannot carry
SHALL be left behind alone.

#### Scenario: A counter across a save

- **WHEN** the dashboard sample's counter page is at `Count: 3` and an edit to its `.cs` is saved
- **THEN** the page reloads with the edit and shows `Count: 3`, and the next press shows `Count: 4`

#### Scenario: A field the edit removed or added

- **WHEN** the edit removes `_label` and adds `_title`
- **THEN** the reloaded page holds no `_label`, and `_title` has its initializer

#### Scenario: A controller beside the data

- **WHEN** a page holds a record, a `long` and a controller, and is reloaded
- **THEN** the record comes back as a record and the `long` as a `long`, and the controller, whose JSON
  is not it, keeps the one its initializer made
