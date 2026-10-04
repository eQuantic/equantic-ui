## MODIFIED Requirements

### Requirement: A member starts as its declaration says

A twin's constructor SHALL be the C# constructor: it SHALL take that constructor's parameters, give
each member the value its declaration gives it (the positional parameter it is made from, or a
property's or a field's initializer, converted as the expression it is, or its type's default when it
declares none), run every initializer on every construction in declaration order, and then run the
constructor's body. A derived record's initializers SHALL run before its base's constructor, as in
.NET.

#### Scenario: A field initializer

- **WHEN** `record Fields { public string Log = "x"; }` is built with `new Fields()`
- **THEN** its `Log` is `"x"`, as in .NET

#### Scenario: A decimal and a long property

- **WHEN** `record Props { public decimal Price { get; init; } = 1.5m; public long Count { get; init; } = 5; }`
- **THEN** `(new Props().Price + 1m).ToString()` answers `2.5` and `(new Props().Count + 1L).ToString()` answers `6`

#### Scenario: A fresh collection per construction

- **WHEN** a property is initialized with `= new()` and two instances are built
- **THEN** adding to one instance's collection leaves the other's empty

#### Scenario: Every initializer runs, whatever the object initializer sets

- **WHEN** `record Counted { public static int N; public int A = ++N; public int B { get; init; } = ++N; }`
  is built with `new Counted { B = 10 }`
- **THEN** `Counted.N` is `2`, `A` is `1` and `B` is `10`, as in .NET

#### Scenario: A derived record's initializers run before its base's constructor

- **WHEN** `record Base0 { public int P = Log.Note("base-init "); public Base0() { Log.Note("base-ctor "); } }`
  and `record Derived(int X) : Base0 { public int Q = Log.Note("derived-init "); }` are built with `new Derived(1)`
- **THEN** the log reads `derived-init base-init base-ctor `, as in .NET

#### Scenario: An explicit constructor's body

- **WHEN** `record ExplicitCtor { public int A { get; init; } public int B = 7; public ExplicitCtor(int a) { A = a * 2; } }`
- **THEN** `new ExplicitCtor(3).A` answers `6` and its `B` answers `7`

### Requirement: A construction that skips a member leaves it to the constructor

A construction SHALL call the twin's constructor with the arguments of the C# constructor the call
binds, in its parameters' order, a skipped optional parameter left to the constructor's default and
an array passed whole to a `params` parameter spread. It SHALL apply an object initializer to what the
constructor built, once the constructor has returned, element by element in source order: an
assignment sets the member, a nested collection initializer adds each element to the collection the
member holds through its `Add`, a nested object initializer assigns into the object the member holds,
and an entry is written through the type's indexer. A struct built by its implicit parameterless
constructor SHALL be its zero, running none of its initializers.

#### Scenario: An object initializer sets one member

- **WHEN** `new Fields { N = 3 }` is built
- **THEN** its `Log` is `"x"` and its `N` is 3

#### Scenario: The object initializer's value is read after the initializers ran

- **WHEN** `var c = new Counted { B = Counted.N };`
- **THEN** `c.A` is `1` and `c.B` is `2`, as in .NET

#### Scenario: A nested collection initializer adds to what the member holds

- **WHEN** `record RBox { public List<int> Items { get; init; } = new() { 0 }; }` is built with
  `new RBox { Items = { 1, 2 } }`, or a class with the same member is
- **THEN** `Items.Count` answers `3`, as in .NET, and a dictionary member written `Map = { ["a"] = 1 }`
  keeps the entry it was initialized with

#### Scenario: A nested object initializer assigns into the member's object

- **WHEN** `var o = new ROuter { Inner = { Items = { 5 }, N = 3 } };` where `ROuter.Inner` is
  initialized with `new()` and `RInner.Items` with `new() { 1 }`
- **THEN** `o.Inner.Items.Count` is `2` and `o.Inner.N` is `3`

#### Scenario: A struct's zero

- **WHEN** `struct Tally { public int N; public Tally() { N = 3; } }` and
  `record struct Reading(int Id) { public string Unit { get; init; } = "kg"; }`
- **THEN** `new Tally().N` is `3`, `default(Tally).N` is `0`, `new Reading().Unit` is null and an
  array's `Reading` slot's `Unit` is null, as in .NET

#### Scenario: A params parameter

- **WHEN** `record Bag(params int[] Items) { public int Count => Items.Length; }`
- **THEN** `new Bag(1, 2, 3).Count` is `3`, `new Bag(new[] { 4, 5 }).Count` is `2` and `new Bag().Count` is `0`

## ADDED Requirements

### Requirement: A copy runs nothing

A `with` expression SHALL copy the record's members and then set the members it names, running no
initializer and no constructor, as .NET's copy does.

#### Scenario: A with over a counted record

- **WHEN** `var s = new Counted(); var t = s with { A = 5 };`
- **THEN** `Counted.N` is `2`, `t.A` is `5` and `t.B` is `2`, as in .NET

### Requirement: Every constructor a record or a struct declares is reached or refused

A twin SHALL reach each constructor that chains to the C# constructor it is, with `: this(…)`, by the
counts of arguments that constructor takes, its optional parameters counted: it SHALL evaluate the
chain's arguments from the arguments that arrived, give a parameter the chain leaves out its default,
and run the chaining constructor's body after the main one's. eqc SHALL refuse with EQ1009 a
constructor that takes a count of arguments the main constructor or another chaining one takes too,
one that chains to any constructor but the main one, and a second constructor that runs a body of its
own without chaining, naming that constructor.

#### Scenario: An alternate with an optional parameter

- **WHEN** `record Box3(int A, int B, int C) { public Box3(int a, int b = 5) : this(a, b, 0) { } }`
- **THEN** `new Box3(1)` prints `Box3 { A = 1, B = 5, C = 0 }`, `new Box3(1, 2)` sets `B` to `2`, and
  `new Box3(1, 2, 3)` reaches the primary constructor

#### Scenario: A parameter the chain leaves out

- **WHEN** `record Opt(int A, int B = 4, int C = 6) { public Opt(string s, string t, string u, string v) : this(s.Length, C: t.Length) { } }`
- **THEN** `new Opt("ab", "c", "d", "e")` prints `Opt { A = 2, B = 4, C = 1 }`, as in .NET

#### Scenario: Two constructors of one count

- **WHEN** `record Pair(int A, int B)` declares `Pair(int a) : this(a, a)` and `Pair(string s) : this(s.Length, 0)`
- **THEN** the build fails with EQ1009, naming `Pair(string s)`

#### Scenario: Two constructors with bodies

- **WHEN** `record Twice` declares `Twice() { A = 1; }` and `Twice(int a) { A = a; }`
- **THEN** the build fails with EQ1009, naming `Twice()`

### Requirement: A record compares and prints as .NET does

A record's equality SHALL compare its runtime type, what its base compares, and every instance field
it declares, a private one and a property's store included. Its text SHALL be what .NET's
`PrintMembers` writes: its base's members first, then the properties its positional parameters make,
then its public fields and readable public properties in declaration order, a computed one included,
each once, and `Name { }` for a record with none.

#### Scenario: A derived record against its base

- **WHEN** `record Animal(string Name);` and `record Dog(string Name, string Breed) : Animal(Name);`
- **THEN** `new Animal("a") == new Dog("a", "b")` is false, `new Dog("a", "b") == new Dog("a", "b")`
  is true, and `new Dog("Rex", "Lab")` prints `Dog { Name = Rex, Breed = Lab }`

#### Scenario: A record with no member

- **WHEN** `record Unit;`
- **THEN** `new Unit()` prints `Unit { }`

#### Scenario: A private field and a computed property

- **WHEN** `record Secret(int Shown) { private int _hidden = Shown * 2; public int Hidden => _hidden; }`
- **THEN** `new Secret(2)` prints `Secret { Shown = 2, Hidden = 4 }` and equals `new Secret(2)`

### Requirement: A positional parameter whose property the record declares is one member

A member a record's body declares under a positional parameter's name SHALL replace the property the
record would have made of that parameter: the twin SHALL hold, compare, copy, print and deconstruct it
once, as the declared member, which the parameter initializes.

#### Scenario: A settable property over a parameter

- **WHEN** `sealed record Box(int X, int Y) { public int X { get; set; } = X; }`
- **THEN** its module loads, `new Box(1, 2)` prints `Box { Y = 2, X = 1 }`, setting `X` to `5` reads
  back `5`, and `var (x, y) = new Box(3, 4)` binds `3` and `4`

### Requirement: Every record and struct has a twin, and a plain class a module

eqc SHALL write a twin for every record and every struct whatever it declares, a record that declares
only methods, only an indexer or nothing included, and a module for every top-level plain class whatever
it declares, so that every module naming such a type imports one that exists. A partial declaration
that declares nothing, a static class, a nested class, an attribute, an exception, a class over a base
that never crosses and a class marked `[ServerOnly]` or `[RuntimeProvided]` SHALL get no plain-class
module, and no module SHALL import one for them.

#### Scenario: A record with only a method, extended

- **WHEN** `record Animal { public string Kind() => "animal"; }` and `record Dog : Animal;`
- **THEN** `new Dog() is Animal` is true and `new Dog().Kind()` answers `animal`, as in .NET

#### Scenario: A class with no member

- **WHEN** `class Mute : IGreeting { }` where `IGreeting` declares `string Greet() => "hello";`, and
  another module constructs it
- **THEN** that module imports `./Mute`, the module exists, and `((IGreeting)new Mute()).Greet()`
  answers `hello`, as in .NET

### Requirement: An instance indexer reaches its twin

An instance indexer of a type whose twin eqc writes SHALL be its twin's `item` method for the getter
and `setItem` for the setter, which answers the value it wrote, and every element access bound to it
SHALL call them: a read, a write, a compound assignment, a step, a coalescing assignment, a
null-conditional access and an object initializer's entry, its receiver and keys evaluated once each.
eqc SHALL refuse with EQ1007 a second indexer, or a method named `Item` or `SetItem` beside one, in the
same type.

#### Scenario: A computed indexer

- **WHEN** `record Grid { public int this[int i] => i * 2; }` and `record Board { public int this[int r, int c] => r * 10 + c; }`
- **THEN** `new Grid()[3]` answers `6` and `new Board()[2, 3]` answers `23`

#### Scenario: A setter that returns early, a compound and a step

- **WHEN** an indexer's setter counts its calls and returns early for a negative value
- **THEN** `c[2] = 1; c[2] += 5; c[2]++; ++c[2];` leaves `c[2]` at `8` with four calls, and
  `var y = (c[1] = -1);` answers `-1`, as in .NET

#### Scenario: Two indexers

- **WHEN** a class declares `this[int i]` and `this[string s]`
- **THEN** the build fails with EQ1007, naming the indexer

### Requirement: A type's statics initialize as .NET runs them

A type whose statics can observe one another (an initializer that is not a constant, or a static
constructor) SHALL start every static at its type's zero, then run the initializers in declaration
order, then the static constructor's body, once, the first time one of its statics is read or written;
a record, a struct, a class, a static class and a component alike. A static with no initializer SHALL
hold its type's zero in a type of any kind.

#### Scenario: An initializer reads a static declared after it

- **WHEN** `record Chain { public static int A = B + 1; public static int B = 2; }`
- **THEN** `Chain.A` is `1` and `Chain.B` is `2`, as in .NET

#### Scenario: A static instance built before a static it reads

- **WHEN** `record Early { public static Early First = new(); public static int Seed = 3; public int Value = Seed * 2; }`
- **THEN** `Early.First.Value` is `0` and `new Early().Value` is `6`, as in .NET

#### Scenario: A static the static constructor sets

- **WHEN** `class Catalog { public static List<string> Names; public static int Size { get; private set; } static Catalog() { Names = new List<string> { "a", "b" }; Size = Names.Count; } }`
- **THEN** `Catalog.Size` read first answers `2`, and `Catalog.Names[1]` answers `b`

#### Scenario: Two types whose statics read each other

- **WHEN** `class Ping { public static int A = Pong.B + 1; }` and `class Pong { public static int B = Ping.A + 10; }`,
  and `Ping.A` is read first
- **THEN** `Ping.A` is `11` and `Pong.B` is `10`, as in .NET
