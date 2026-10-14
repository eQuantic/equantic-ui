## ADDED Requirements

### Requirement: A .NET property or field nothing carries fails the build

A read of a property or a field of a .NET type that no translation answers SHALL fail the build with
EQ2004, naming the member, unless a value the browser holds carries the member by its own name: a
string's, an array's and a span's `Length`, the members an exception's twin carries, and a
`KeyValuePair`'s `Key` and `Value`. A member an interface declares SHALL NOT be refused, since the
value read through the interface may be a type of the app's own. A chain of such reads SHALL be
refused once, at its first member, whether it is read through `.` or through `?.`. A culture handed
to a call that reads it from how the call names it SHALL NOT be read as a value.

#### Scenario: A culture's property

- **WHEN** a component reads `CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator`
- **THEN** the build fails with one EQ2004 naming `System.Globalization.CultureInfo.CurrentCulture`;
  in the browser `CultureInfo` was not defined

#### Scenario: A member the runtime does not carry

- **WHEN** a component reads `Environment.NewLine`, `typeof(string).Name` or `new List<int>().Capacity`
- **THEN** the build fails with EQ2004 naming the member; each read undefined in the browser

#### Scenario: A member the browser carries

- **WHEN** a component reads `"ab".Length`, an exception's `ParamName`, a dictionary pair's `Key`, an
  `IGrouping`'s `Key` or `((ICollection<string>)items).IsReadOnly`, or calls
  `char.ToUpper('i', CultureInfo.InvariantCulture)`
- **THEN** the build reports nothing
