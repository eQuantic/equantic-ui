## ADDED Requirements

### Requirement: An empty collection expression builds the dictionary its target names

A collection expression whose target is a `Dictionary`, a `SortedDictionary` or a `SortedList` SHALL build on
the web the runtime's class `new()` builds for that type, wherever it is written: a local, a field, a
property's initializer, an object initializer's member, an argument, a return and a branch of a conditional.
The capacity `with(…)` hands the constructor SHALL size a `Dictionary` as `new(capacity)` sizes it, and a sorted
one SHALL drop it as its construction does. An element, which only an `Add` of the app's own can add to one of
these types, SHALL fail the build with EQ1004.

#### Scenario: The empty dictionary the IDE suggests

- **WHEN** browser-side code runs `Dictionary<string, int> d = []; d["a"] = 1; return d.Count;`
- **THEN** it answers 1, as in .NET, where the write threw `map.set is not a function`

#### Scenario: A capacity handed by with

- **WHEN** browser-side code runs `Dictionary<string, int> d = [with(10)]; return d.EnsureCapacity(0);`
- **THEN** it answers 11, as in .NET

#### Scenario: A sorted dictionary started empty

- **WHEN** browser-side code runs `SortedDictionary<string, int> d = []; d["b"] = 1; d["a"] = 2;` and joins its keys
- **THEN** they read "a,b", as in .NET

#### Scenario: An element an extension adds

- **WHEN** an app declares `Add(this Dictionary<string, int>, KeyValuePair<string, int>)` and writes `Dictionary<string, int> d = [new("a", 1)];` in code that crosses
- **THEN** the build fails with EQ1004

### Requirement: A key named like what every object inherits is a key like any other

A dictionary SHALL store, find, count, enumerate and remove the keys `__proto__`, `constructor`, `toString`,
`hasOwnProperty` and `valueOf` as it does any other key, through every form that builds one: `new()`, both
initializers, a copy of a dictionary or of pairs, `ToDictionary`, `GroupBy(…).ToDictionary`, a member's
initializer, a collection expression, and the sorted ones.

#### Scenario: A key that a plain object would hand to its prototype

- **WHEN** browser-side code writes `d["__proto__"] = 1` on a `Dictionary<string, int>` and reads `d.Count`, `d["__proto__"]` and `d.ContainsKey("toString")`
- **THEN** they answer 1, 1 and false, as in .NET

#### Scenario: A diff of lines of inherited names

- **WHEN** the code diff engine compares `__proto__`, `constructor`, `toString` with `__proto__`, `valueOf`, `toString`
- **THEN** the web marks only the middle line changed, as .NET does
