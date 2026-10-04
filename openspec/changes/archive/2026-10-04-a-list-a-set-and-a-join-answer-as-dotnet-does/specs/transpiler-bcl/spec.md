## MODIFIED Requirements

### Requirement: List.Remove answers as .NET does

`list.Remove(item)` SHALL remove the first item `EqualityComparer<T>.Default` finds equal to the value
and answer whether it found one, evaluating the list and the item once each, in that order. The
comparison SHALL be the one every search of the list takes (`IndexOf`, `LastIndexOf`, `Contains`):
a value tuple, a record and a struct by value, and so a nullable one of those and an anonymous type, a
tuple's or an anonymous type's array member by reference. Through `ICollection<T>`, a HashSet or a
LinkedList held when the call runs SHALL remove as it does when called directly, and so SHALL a
dictionary through `ICollection<KeyValuePair<K, V>>`: the pair leaves only when its key is there with
an equal value.

#### Scenario: A present item and a missing one

- **WHEN** `var list = new List<int> { 0, 1, 2 };` runs `list.Remove(1)` and then `list.Remove(5)`
- **THEN** the calls answer true and false, and the list is `0, 2`

#### Scenario: Equality by the type's rule

- **WHEN** a `List<Point>` of records removes `new Point(3, 4)`, and a `List<double>` removes `double.NaN`
- **THEN** each removes its equal item and answers true

#### Scenario: A tuple element by element

- **WHEN** a `List<(int, string)>` holding `(1, "a")` and `(2, "b")` removes `(2, "b")`
- **THEN** it answers true, and one item is left

#### Scenario: A tuple's array by reference

- **WHEN** a `List<(int[], int)>` holding `(a, 1)` removes `(new[] { 1 }, 1)`
- **THEN** it answers false, and the item stays, as in .NET

#### Scenario: A HashSet through ICollection

- **WHEN** `ICollection<int> c = new HashSet<int> { 1, 2 };` runs `c.Remove(1)` and then `c.Remove(5)`
- **THEN** the calls answer true and false, and one item is left

## ADDED Requirements

### Requirement: A search compares as the default comparer

A list's and an array's `IndexOf`, `LastIndexOf` and `Contains`, `Array.IndexOf` and
`Array.LastIndexOf`, and LINQ's `Contains` SHALL compare as `EqualityComparer<T>.Default` does for
the element type, by the one comparison a set's elements and a dictionary's keys take: a NaN equal to
a NaN, a record, a struct, a tuple, a decimal and a date by value, a class by its own `Equals` where it
has one and by reference otherwise, an element typed `object` by what it holds. `Contains`, `IndexOf`
and `Remove` SHALL agree about one list. The ranged overloads SHALL search their range and refuse one
that leaves the list with .NET's exception and words.

#### Scenario: A NaN, a record and a tuple

- **WHEN** `new List<double> { double.NaN }.IndexOf(double.NaN)`, `new List<Point> { new Point(1, 2) }.IndexOf(new Point(1, 2))` and `new List<(int, int)> { (1, 2), (1, 2) }.LastIndexOf((1, 2))` run
- **THEN** they answer 0, 0 and 1, as in .NET

#### Scenario: A range that leaves the list

- **WHEN** `new List<int> { 5, 6, 5 }.IndexOf(5, 1, 3)` runs
- **THEN** it throws "Count must be positive and count must refer to a location within the string/array/collection. (Parameter 'count')", as in .NET

### Requirement: A sort is .NET's introspective sort

`List<T>.Sort` and `Array.Sort` SHALL sort as .NET's introspective sort does, step for step, so that
equal elements land where .NET's unstable sort leaves them: by the element type's default comparer
(a number by value, a string in the current culture, an enum by its value, a null first, a double's
and a float's NaNs moved to the front before the sort), a `Comparison<T>`, a `StringComparer`'s six,
`Comparer<T>.Create` or a comparer of the app's own. A comparison that throws SHALL be wrapped in .NET's
InvalidOperationException, an inconsistent one that reads past the span SHALL be .NET's
ArgumentException naming the comparer, and a type .NET's default comparer cannot order SHALL throw
once two of its elements are compared. A comparer with no form here SHALL fail the build (EQ2007).

#### Scenario: Numbers by value

- **WHEN** `var l = new List<int> { 10, 9, 1 }; l.Sort();` runs
- **THEN** the list is "1,9,10", as in .NET

#### Scenario: Equal elements where .NET leaves them

- **WHEN** twenty records keyed `i % 3` are sorted by their key with a `Comparison`
- **THEN** their ids read "0 15 12 18 6 9 3 4 7 10 13 1 16 19 8 11 2 14 17 5", as in .NET

#### Scenario: A comparer that crosses

- **WHEN** `new List<string> { "b", "a" }` is sorted with `StringComparer.Ordinal`
- **THEN** the list is "a,b", as in .NET

### Requirement: A binary search answers as .NET's

`BinarySearch` SHALL search by .NET's midpoint, by the element type's default comparer or the comparer
it is handed, answer the index of an element it finds (among equal ones, the one .NET's midpoint meets
first) and the complement of the insertion point of one it does not, and check its range as .NET does.

#### Scenario: A hit and a miss

- **WHEN** `new List<int> { 1, 3, 5 }` searches for 3 and for 4
- **THEN** it answers 1 and -3, as in .NET

### Requirement: A list's finds, removals and copies answer as .NET's

`Find` and `FindLast` SHALL answer the element type's default where nothing matches; `FindIndex` and
`FindLastIndex` SHALL take their start index and count; `RemoveAll` SHALL remove in one pass, asking
the predicate once per element in order, and answer how many it removed; `CopyTo` SHALL write into the
array it is handed. Each SHALL check its arguments as .NET does, in its order and in its words.

#### Scenario: A find with no match

- **WHEN** `$"[{new List<int> { 1, 2 }.Find(x => x > 5)}]"` runs
- **THEN** it answers "[0]", as in .NET

#### Scenario: RemoveAll and CopyTo

- **WHEN** `new List<int> { 1, 2, 3, 4 }.RemoveAll(x => x % 2 == 0)` runs, and `new List<int> { 1, 2 }.CopyTo(a)` into `new int[3]`
- **THEN** the first answers 2 and leaves "1,3", and the array is "1,2,0", as in .NET

### Requirement: string.Join writes each value as .NET's ToString

`string.Join` SHALL read any sequence, an array, a list, a set, a linked list, a dictionary's keys and
a sequence behind an interface alike, and write each value as .NET's `ToString` writes it (a bool as
True or False, an enum by its member's name, a float by its own digits, a double in .NET's notation), a
null value as nothing, with the separator between them and a null separator as none. Values passed one
by one SHALL each be written by their own type. A null sequence SHALL be refused by the name of its
overload's parameter.

#### Scenario: A set and a linked list

- **WHEN** `string.Join(",", new HashSet<int> { 1, 2 })` and `string.Join(",", new LinkedList<int>(new[] { 1, 2 }))` run
- **THEN** each answers "1,2", as in .NET

#### Scenario: Values written as .NET writes them

- **WHEN** `string.Join(",", new[] { true, false })` and `string.Join(",", new[] { 1e21, 0.1 })` run
- **THEN** they answer "True,False" and "1E+21,0.1", as in .NET
