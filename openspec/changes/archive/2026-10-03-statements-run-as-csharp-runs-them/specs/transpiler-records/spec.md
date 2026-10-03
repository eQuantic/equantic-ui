## ADDED Requirements

### Requirement: A record deconstructed by assignment fills its targets

`(a, b) = value`, where the value is a record or a struct that deconstructs, SHALL assign each target
the member its position names, a discard assigning nothing.

#### Scenario: Existing locals

- **WHEN** browser-side code declares `int a, b;` and writes `(a, b) = new Point(1, 2)`
- **THEN** `a` is `1` and `b` is `2`, as in .NET

### Requirement: A static field store lives on the type

A static property whose accessors use `field` SHALL keep its store on the type, in a component, a
plain class and a record alike, and a record SHALL carry such a property with its accessors.

#### Scenario: A record's halving setter

- **WHEN** browser-side code sets `Shapes.Half = 9` on a record declaring
  `public static int Half { get; set => field = value / 2; }` and reads it back
- **THEN** it reads `4`, as in .NET
