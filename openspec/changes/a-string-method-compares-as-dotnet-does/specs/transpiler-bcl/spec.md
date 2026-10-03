# Spec Delta

## ADDED Requirements

### Requirement: A string's own search compares by the comparison it is given

`StartsWith`, `EndsWith`, `Contains`, `IndexOf` and `LastIndexOf` given a `StringComparison`, and
`Equals(string, StringComparison)`, SHALL answer what .NET answers by that comparison, whether it is
written as a constant or held in a variable. An ordinal comparison that ignores case SHALL compare
each code point's simple upper case as .NET's ordinal casing does: the Kelvin sign, the long s and
the dotless i SHALL NOT match k, s and I, a Greek letter with an iota subscript SHALL match its title
case, a surrogate pair SHALL match by its code point, and a lone half SHALL be found inside a pair. A
comparison .NET does not define SHALL throw .NET's message.

#### Scenario: The Kelvin sign is not a k

- **WHEN** `("x" + "\u212A").Contains("k", StringComparison.OrdinalIgnoreCase)` runs
- **THEN** it answers false

#### Scenario: A comparison held in a variable

- **WHEN** `var c = StringComparison.OrdinalIgnoreCase; return "aB".StartsWith("ab", c);` runs
- **THEN** it answers true

#### Scenario: A surrogate pair by its code point

- **WHEN** `"x\uD801\uDC28y".IndexOf("\uD801\uDC00", StringComparison.OrdinalIgnoreCase)` runs
- **THEN** it answers 1

### Requirement: A string's search checks its start and its count as .NET does

`IndexOf` and `LastIndexOf` given a start, or a start and a count, with a `StringComparison` SHALL
search only that range, and SHALL throw .NET's `ArgumentOutOfRangeException`, with its message, for a
start or a count outside the string. `LastIndexOf` SHALL read a start one past the end as the last
unit. A null value SHALL throw `ArgumentNullException`.

#### Scenario: The count ends the search

- **WHEN** `"abcb".IndexOf("B", 2, 1, StringComparison.OrdinalIgnoreCase)` runs
- **THEN** it answers -1

#### Scenario: A start past the end

- **WHEN** `"abc".IndexOf("b", 4, StringComparison.Ordinal)` runs
- **THEN** it throws with the message `Index was out of range. Must be non-negative and less than or equal to the size of the collection. (Parameter 'startIndex')`

#### Scenario: One past the end steps back

- **WHEN** `"abc".LastIndexOf("b", 3, StringComparison.OrdinalIgnoreCase)` runs
- **THEN** it answers 1

### Requirement: Replace keeps its comparison and writes its replacement as text

`Replace(string, string)` SHALL replace by the ordinal comparison, and `Replace(string, string,
StringComparison)` by the one it is given, every match from left to right without overlap. The
replacement SHALL be written as text, a null replacement SHALL remove the matches, and a null or
empty old value SHALL throw .NET's exception.

#### Scenario: Ignoring case

- **WHEN** `"aBc".Replace("b", "x", StringComparison.OrdinalIgnoreCase)` runs
- **THEN** it answers `axc`

#### Scenario: A replacement that looks like a pattern

- **WHEN** `"abc".Replace("b", "$&$&")` runs
- **THEN** it answers `a$&$&c`

### Requirement: CompareTo orders by the current culture

`string.CompareTo(string)` SHALL compare as `string.Compare(a, b)` does, by the current culture, a
null ordering first. `CompareTo(object)` SHALL fail the build with EQ1004.

#### Scenario: A sort written with CompareTo

- **WHEN** a `List<string>` holding `b`, `B`, `a` and `A` runs `names.Sort((x, y) => x.CompareTo(y))`
- **THEN** the list is `a,A,b,B`

### Requirement: A search by a culture comparison is refused

A string's search (`StartsWith`, `EndsWith`, `Contains`, `IndexOf`, `LastIndexOf` or `Replace`) given
a culture comparison as a constant, and an overload that takes a `CultureInfo`, SHALL fail the build
with EQ1004. One given a culture comparison in a variable SHALL throw at run time with a message that
names the method and the comparison, after every check .NET makes before it searches. `Equals` by a
culture comparison SHALL compare the two whole strings by the culture's collator.

#### Scenario: A constant culture comparison

- **WHEN** a component calls `a.StartsWith(b, StringComparison.CurrentCulture)`
- **THEN** the build fails with EQ1004 at that call

#### Scenario: A whole-string culture comparison

- **WHEN** `"\u00ADab".Equals("ab", StringComparison.InvariantCulture)` runs
- **THEN** it answers true
