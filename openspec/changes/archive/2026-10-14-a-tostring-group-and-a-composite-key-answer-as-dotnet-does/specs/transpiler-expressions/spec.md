## ADDED Requirements

### Requirement: A ToString method group writes what its call writes

The `ToString` method group of a value whose type writes its text SHALL write what the call writes, of
the value as it is when the delegate is made: an array's, a list's, a dictionary's, a set's and a
delegate's type name (`System.Int32[]`), an enum's member name, and a tuple's, an anonymous object's
and a data value's text. Its receiver SHALL be read once, when the delegate is made, and a null
reference SHALL be refused there with the NullReferenceException .NET throws.

#### Scenario: An array's group

- **WHEN** `var xs = new[] { 1, 2 }; Func<string> text = xs.ToString; xs = new[] { 3 };` and `text()` runs
- **THEN** it writes `System.Int32[]`, as in .NET, where the browser wrote `1,2`

#### Scenario: An enum's group

- **WHEN** `Func<string> f = Scale.Large.ToString` is made and called
- **THEN** it writes `Large`, as in .NET

#### Scenario: A receiver that is a call

- **WHEN** `Func<string> t = Make().ToString` is made and called twice, where `Make` counts its calls
- **THEN** `Make` has run once, and both calls write `System.Int32[]`
