- **2026-10-17 · Object and enum members answer as .NET does**: `object.Equals(a, b)` went out as
  `object.equals(a, b)` (`ReferenceError: object is not defined`,
  [#902](https://github.com/eQuantic/equantic-ui/issues/902)); `GetType()` and `MemberwiseClone` built
  with no diagnostic and threw in the browser (`getType is not a function`,
  [#903](https://github.com/eQuantic/equantic-ui/issues/903)); `Equals(object)` inherited from `object`
  was refused on a class receiver, and a `List<T>` held as `object` compared by its items where .NET
  compares it by reference ([#909](https://github.com/eQuantic/equantic-ui/issues/909)); `Equals` and
  `ToString` on a null record, class instance or string answered `false`, `""` or `"null"` instead of
  throwing `NullReferenceException` ([#912](https://github.com/eQuantic/equantic-ui/issues/912)); an
  enum's `Equals` threw (`equals is not a function`) and its `GetHashCode` hashed its camelCase name
  instead of its underlying number ([#926](https://github.com/eQuantic/equantic-ui/issues/926)); and an
  enum member that aliases another (`Gray = Grey`) crossed as a key of its own, unequal to its target in
  `==`, `switch`, pattern matching, `ToString` and dictionary keys
  ([#927](https://github.com/eQuantic/equantic-ui/issues/927)). The static `object.Equals(a, b)` now
  answers as its first parameter's own `Equals`, chosen from that operand's type before C# boxed it and
  bound by parameter, so `Equals(objB: x, objA: y)` asks `y`, with the second operand's type read only
  where the first one's says nothing, and a value of a type that refuses every other is unequal to one
  known to be another (a byte and an int, a char and a string, a tuple and an array), both still
  evaluated; an inherited `Equals(object)` compares an array, a list and a class that does not override
  it by reference wherever either value's type is in sight, and through an override where one exists;
  instance `Equals` and `ToString` refuse a null receiver after evaluating any argument; `GetType` and
  `MemberwiseClone` are refused with one `EQ2004` across calls, chains and method groups; an enum's
  `Equals`, a `Nullable` enum's method group included, compares by its underlying value, and its
  `GetHashCode` hashes that value as the underlying type does, a `long` enum's by the long's two halves;
  a `Nullable` tuple's `Equals` method group, which threw where it was made, is its value's; and an
  aliased member takes the first member's key and name, so `ToDictionary` accepts an enum with
  aliases. A list, an array, a tuple or an enum held as `object` on both sides has lost the type .NET
  reads, since the browser holds the first three as one JavaScript array and an enum as its member's key,
  so two lists or two arrays held so still compare by their elements, which a tuple held as `object`
  needs: the first version of this change compared every array by reference and broke a tuple in a
  `HashSet<object>`, a `Dictionary<object, …>`, `Distinct`, `Equals` and a generic record's `Equals`,
  five cases that now pin it. Copilot's first review found seven of the defects fixed here (a `Nullable`
  enum's group, a null string's `ToString`, a long enum's hash, the operands' order, the first operand's
  override, two primitive types the browser holds alike, a tuple beside an array); the other two are that
  gap. Proposed and archived through OpenSpec (`openspec/specs/transpiler-bcl`).
