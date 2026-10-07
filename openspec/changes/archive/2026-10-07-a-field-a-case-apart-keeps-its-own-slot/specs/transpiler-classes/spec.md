## ADDED Requirements

### Requirement: A field a case apart from a member keeps its own slot

A plain class's instance field whose twin name is another member's, a property, a method, an event or another field of its type or its bases, SHALL live in a slot of its own, its name with a `$` after it, and every read and write of the field SHALL reach that slot. The other member SHALL keep its name. A class with such a field SHALL be written to JSON as System.Text.Json writes it: the property, read through its getter, and never the field.

#### Scenario: A setter beside its field

- **WHEN** `int value;` stands beside `int Value { get => value; set => this.value = value * 2; }`, and `Value` is set to 3
- **THEN** `Value` reads 6, as in .NET

#### Scenario: A method beside a field

- **WHEN** `int size = 3;` stands beside `int Size() => size * 2`, and `Size()` is called
- **THEN** it answers 6, as in .NET

#### Scenario: The JSON of a class with a moved field

- **WHEN** an instance whose field moved is written to JSON
- **THEN** the property is written under its name, read through its getter, and the field's slot is not written
