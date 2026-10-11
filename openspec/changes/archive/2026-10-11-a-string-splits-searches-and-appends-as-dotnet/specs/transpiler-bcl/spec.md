## ADDED Requirements

### Requirement: A string splits as .NET splits

`string.Split` SHALL answer what the overload C# binds answers in .NET 10, each argument filling the
parameter it binds to, named or not, evaluated in the order written. A single separator, a char or a
string, SHALL split at each of its places, and a null or an empty one SHALL split nothing. A set of
separators, chars or strings, written as an array, a span's collection expression or one by one,
SHALL split at each place where one of them matches, the first in the set's order winning, a null or
an empty member skipped, and a null or an empty set SHALL split at .NET's white space. A count SHALL
cap the entries, the last one holding the rest of the text unsplit, and a count of zero SHALL answer
none. `TrimEntries` SHALL trim each entry of .NET's white space and `RemoveEmptyEntries` SHALL drop the
empty entries, the trimmed ones included. A negative count SHALL throw .NET's
`ArgumentOutOfRangeException` and an option .NET does not define its `ArgumentException`, in that
order, each with .NET's message.

#### Scenario: A set of separators

- **WHEN** `" abcd ef ".Split(new[] { 'a', 'd' })` runs
- **THEN** it answers ` `, `bc` and ` ef `, as in .NET, where the array was one text joined by commas

#### Scenario: The first separator in the set's order wins

- **WHEN** `"abcd".Split(new[] { "b", "bc" }, StringSplitOptions.None)` runs
- **THEN** it answers `a` and `cd`, as in .NET

#### Scenario: A count and options

- **WHEN** `"  a  b  c  ".Split(' ', 3, StringSplitOptions.RemoveEmptyEntries)` runs
- **THEN** it answers `a`, `b` and `c  `, as in .NET, where the count was dropped

#### Scenario: Options held in a variable

- **WHEN** `var o = StringSplitOptions.RemoveEmptyEntries; "a,,b".Split(',', o)` runs
- **THEN** it answers `a` and `b`, as in .NET, where the options were read from their spelling and the
  empty entry was kept

#### Scenario: An empty set is white space

- **WHEN** `" abcd ef ".Split(new string[0], StringSplitOptions.None)` runs
- **THEN** it answers an empty entry, `abcd`, `ef` and an empty entry, as in .NET, where the text was
  split into its characters

#### Scenario: A negative count

- **WHEN** `" abc ".Split(new[] { 'a' }, -1)` runs
- **THEN** it throws .NET's `ArgumentOutOfRangeException` with the message `count ('-1') must be a
  non-negative value. (Parameter 'count')`, the actual value on the next line

## MODIFIED Requirements

### Requirement: A string's search checks its start and its count as .NET does

`IndexOf` and `LastIndexOf` of a string given a start, or a start and a count, with a `StringComparison`
or without one, SHALL search only that range, and SHALL throw .NET's `ArgumentOutOfRangeException`,
with its message, for a start or a count outside the string. `LastIndexOf` SHALL read its start as
the last unit a match may cover, and a start one past the end as the last unit. A null value SHALL
throw `ArgumentNullException`. Without a comparison the search SHALL be ordinal, as the search without
a start is in the browser, where .NET searches by the current culture (#532).

#### Scenario: The count ends the search

- **WHEN** `"abcb".IndexOf("B", 2, 1, StringComparison.OrdinalIgnoreCase)` runs
- **THEN** it answers -1

#### Scenario: A start past the end

- **WHEN** `"abc".IndexOf("b", 4, StringComparison.Ordinal)` runs
- **THEN** it throws with the message `Index was out of range. Must be non-negative and less than or equal to the size of the collection. (Parameter 'startIndex')`

#### Scenario: One past the end steps back

- **WHEN** `"abc".LastIndexOf("b", 3, StringComparison.OrdinalIgnoreCase)` runs
- **THEN** it answers 1

#### Scenario: A match must end by the start

- **WHEN** `"abcabc".LastIndexOf("bc", 4)` runs
- **THEN** it answers 1, as in .NET, where JavaScript's `lastIndexOf` answered the match that begins at 4

#### Scenario: The count ends a search with no comparison

- **WHEN** `"abcabc".LastIndexOf("a", 5, 2)` runs
- **THEN** it answers -1, as in .NET, where the count was dropped and the search answered 3
