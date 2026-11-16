## ADDED Requirements

### Requirement: An entry may select its text as it takes the keyboard

A `TextEntry` with `SelectOnFocus` SHALL select all of its text whenever it takes the keyboard, autofocused or
not, on the web as on Photon, so that what is typed first replaces it.

#### Scenario: A rename field

- **WHEN** an autofocused entry with `SelectOnFocus` holding `App.cs` is mounted and `Main.cs` typed
- **THEN** it holds `Main.cs`
