## ADDED Requirements

### Requirement: A value whose type writes no text writes its full name

A plain class or a plain struct whose type declares no `ToString`, and derives none from a base the app
declares, SHALL write its type's full name as its text, as `Object.ToString` and `ValueType.ToString`
do: its namespace, then the types it is nested in and its own name, joined by `+`. A record SHALL keep
the text it synthesizes, and a type that declares `ToString`, or derives it, SHALL keep its own. A
generic type is outside this requirement: its name carries type arguments the browser does not hold.

#### Scenario: A plain struct and a plain class

- **WHEN** `public struct Pt { public int X; }` and `public class Plain { }` are declared in `My.App`
- **THEN** `new My.App.Pt().ToString()` writes `My.App.Pt` and `new My.App.Plain().ToString()` writes
  `My.App.Plain`, where the twins wrote `Pt { X = 0 }` and `[object Object]`

#### Scenario: A nested struct

- **WHEN** `public class Outer { public struct In { public int Y; } }` is declared in `My.App`
- **THEN** `new My.App.Outer.In().ToString()` writes `My.App.Outer+In`

#### Scenario: A class over a base that writes its own text

- **WHEN** `class Named { public override string ToString() => "named"; }` and `class FromNamed : Named { }`
- **THEN** both write `named`, and a class over a plain base writes its own full name
