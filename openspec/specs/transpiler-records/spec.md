# transpiler-records Specification

## Purpose
How eqc builds the twin of a record, a struct or a class: its constructor, and what each member
starts as.

## Requirements

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
binds, as the bound tree binds them: each in its parameter's place and evaluated in the order it is
written, a skipped optional parameter left to the constructor's default, an array passed whole to a
`params` parameter spread, named or not, and an element packed into one passed as it is. It SHALL apply an object initializer to what the
constructor built, once the constructor has returned, element by element in source order: an
assignment sets the member, a nested collection initializer adds each element to the collection the
member holds through the `Add` the bound tree binds (an extension's through its home, a collection's
own as every call to it lowers, a dictionary's pair and an `ICollection<T>` member's included), a nested
object initializer assigns into the object the member holds, and an entry is written through the
type's indexer. Every part of the initializer SHALL be evaluated in the caller's own function, in the
order C# evaluates it, so an `await` in an element, a value or a key runs in the method it was written
in, and each element is applied before the next one's parts are evaluated, as C# applies it. A struct's
zero (`default`, an array's slot, an OrDefault, and `new S()` through the implicit parameterless
constructor) SHALL be built without its constructor, running no initializer, no constructor and no
static constructor, a generic struct's and a transpiled struct's from another assembly included.

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

#### Scenario: An awaited element

- **WHEN** an async method builds `new Holder { Items = { await G(), 4 } }`, where `G` yields and answers `3`
- **THEN** `Items.Count` is `2` and `Items[0]` is `3`, as in .NET, and the module parses

#### Scenario: An element is applied before the next is evaluated

- **WHEN** `record RLast` publishes itself in `RLast.Last` from its constructor, and is built with
  `new RLast { A = 1, B = RLast.Last.A }` or `new RLast { Items = { 1, RLast.Last.Items.Count } }`
- **THEN** `B` is `1` and `Items` is `1, 1`, as in .NET

#### Scenario: Named arguments out of the signature's order, and a params array by name

- **WHEN** `record Pair(int A, int B)` is built with `new Pair(B: Log("b", 2), A: Log("a", 1))`, and
  `record Bag(int Tag, params int[] Items)` with `new Bag(1, Items: arr)` over a three-element array
- **THEN** the log reads `ba`, the pair holds `1` and `2`, and the bag's `Count` is `3`, as in .NET

#### Scenario: A struct's zero runs nothing

- **WHEN** `record struct P(int X, int Y) { public P(int a = 1) : this(a, a) { } }`, `struct Pair<T> { public int Count; }`
  and `struct Meter0 { public int V; static Meter0() { Log.Note("cctor "); } }`
- **THEN** `default(P)`, `new P()` and an array's slot are `(0, 0)`, `new Pair<string>()` is a zero whose `Count`
  steps to `1`, and neither `new Meter0()`, `default(Meter0)` nor an array of it runs the static
  constructor, as in .NET

#### Scenario: A struct of another assembly

- **WHEN** an app reads `default(CodeCollapse).Placeholder`, where the code engine's
  `record struct CodeCollapse(int FirstLine, int LastLine, bool Placeholder = true, string? Label = null)` is metadata
- **THEN** it is false, as in .NET, and so is an array's slot's and a `new CodeCollapse()`'s

### Requirement: A default and a base clause read the constructor's parameters

A member default and a base clause's arguments SHALL read a primary-constructor parameter as that
parameter, before any member of the instance is set.

#### Scenario: An initializer reads a positional parameter

- **WHEN** `record Tagged(int Id) { public string Tag = "#" + Id; }`
- **THEN** `new Tagged(4).Tag` answers `#4`

#### Scenario: A base clause computes from a parameter

- **WHEN** `record Offset(int X) : Measure(X + 1)` with `record Measure(int Value)`
- **THEN** `new Offset(3).Value` answers 4, and building it throws nothing

### Requirement: A record's and a struct's members lower as a class's do

A record's and a struct's method, operator, conversion and computed property SHALL be lowered
through the same lowering as a class's members, their own and a default an interface supplies: an
async method SHALL run as an async function, an iterator SHALL return its sequence, an `out` or
`ref` parameter SHALL carry its value back, and a variable an expression body's pattern binds SHALL
be declared before its use. A parameter SHALL be declared by the name every reference to it uses.
Whether a method is async SHALL be decided by its `async` modifier or its return type's symbol, never
by the type's name, and a getter that yields SHALL return its sequence.

#### Scenario: An async method and an iterator

- **WHEN** `record R(int V)` declares `async Task<int> Own() => await Task.FromResult(V);` and
  `IEnumerable<int> Mine() { yield return V; yield return V + 1; }`
- **THEN** `await new R(3).Own()` answers 3 and `string.Join(",", new R(3).Mine())` answers `3,4`, as in .NET

#### Scenario: A pattern variable in an expression body

- **WHEN** `struct SE` declares `public override bool Equals(object o) => o is SE m && m.V == V;`
- **THEN** `new SE(1).Equals(new SE(1))` answers true, and `List<SE>.Remove(new SE(1))` removes it

#### Scenario: A return type named like a task

- **WHEN** `record R(int V)` declares `TaskItem First() => new TaskItem(V);` with `record TaskItem(int N)`
- **THEN** `new R(4).First().N` answers 4, the method not being async

#### Scenario: A reserved parameter name

- **WHEN** a record's method takes `int package` or `int @class`
- **THEN** it runs, the parameter declared as `package$` or `class$`, the name every use of it has

### Requirement: A record's twin says what C# declares

A record's twin SHALL annotate a nullable delegate as a function that may be missing, and SHALL
import every runtime name its annotations use, `Decimal` included.

#### Scenario: A record with a fold label and an amount

- **WHEN** a record has a `decimal Amount`, a method returning `(decimal Net, decimal Tax)`, and a
  parameter `Func<int, string>? label`
- **THEN** its module imports `Decimal`, the method is annotated `[Decimal, Decimal]`, and the
  parameter `((value: number) => string) | null`

#### Scenario: A tuple holding an enum

- **WHEN** a record's method returns `(Side Side, int Line)`, `Side` an enum of the app
- **THEN** it is annotated `[string, number]`, the member string the enum crosses as

### Requirement: Every deconstruction reads a record or a struct through its Deconstruct

A deconstruction SHALL bind each part the way the bound tree says .NET does, in every shape it takes:
a declaration (`var (a, b) = p`, `(var a, var b) = p`), an assignment (`(a, b) = p`), a mixed one
(`(var a, b) = p`) and a `foreach (var (a, b) in ps)`. A tuple and a dictionary's pair SHALL bind by
position. Any other value SHALL bind through the `Deconstruct` each level calls: a record's own and a
BCL type's by the members its out parameters name, and one the app wrote by calling it. A nested
deconstruction SHALL bind each level by its own type, and a discard SHALL bind nothing.

#### Scenario: Existing locals

- **WHEN** browser-side code declares `int a, b;` and writes `(a, b) = new Point(1, 2)`
- **THEN** `a` is `1` and `b` is `2`, as in .NET

#### Scenario: A declaration written as a tuple, and a loop

- **WHEN** browser-side code writes `(var a, var b) = new Point(1, 2)`, or sums `a * 10 + b` over
  `foreach (var (a, b) in new[] { new Point(1, 2), new Point(3, 4) })`
- **THEN** it reads `12`, and the loop's sum is `46`, as in .NET

#### Scenario: A struct's own Deconstruct

- **WHEN** a struct's `Deconstruct(out double celsius, out double fahrenheit)` computes the second
  part, and browser-side code writes `var (c, f) = new Temperature(100)`
- **THEN** `c + f` is `312`, as in .NET

#### Scenario: A nested deconstruction

- **WHEN** browser-side code writes `var ((x1, y1), (x2, y2)) = new Line(new Point(1, 2), new Point(3, 4))`
- **THEN** the four names hold `1`, `2`, `3` and `4`, as in .NET

### Requirement: A static field store lives on the type

A static property whose accessors use `field` SHALL keep its store on the type, in a component, a
plain class and a record alike, and a record SHALL carry such a property with its accessors.

#### Scenario: A record's halving setter

- **WHEN** browser-side code sets `Shapes.Half = 9` on a record declaring
  `public static int Half { get; set => field = value / 2; }` and reads it back
- **THEN** it reads `4`, as in .NET

### Requirement: A static store starts as its declaration says

A static property's store SHALL start as its initializer, written into the store directly, or as its
type's default when it declares none, in a component, a plain class and a record alike, before any
code writes it.

#### Scenario: An initializer behind a doubling setter

- **WHEN** a class declares `public static int Total { get; set => field = value * 2; } = 5;`
- **THEN** reading `Total` before any write answers `5`, as in .NET

#### Scenario: No initializer

- **WHEN** a component declares `public static int Hits { get; set; }`
- **THEN** reading `Hits` before any write answers `0`, as in .NET

### Requirement: A copy runs nothing

A `with` expression SHALL copy the record's members and then set the members it names, running no
initializer and no constructor, as .NET's copy does, through the twin's own copy for a struct and for a
record whose twin eqc writes in a namespace of the vocabulary. An assignment to `this` in a struct
SHALL copy the value's state onto the instance.

#### Scenario: A with over a counted record

- **WHEN** `var s = new Counted(); var t = s with { A = 5 };`
- **THEN** `Counted.N` is `2`, `t.A` is `5` and `t.B` is `2`, as in .NET

#### Scenario: A with over a struct and over the code engine's record

- **WHEN** `struct Pt { public int X; public int Y; public int Sum() => X + Y; }` is copied with `a with { Y = 5 }`,
  and `CodeLanguageRules.Default with { LineComment = "--", IndentWidth = 2 }`
- **THEN** the copy's `Sum()` answers, and the rules hold `--` and `2` while `CodeLanguageRules.Default` keeps `4`

#### Scenario: An assignment to this

- **WHEN** `struct P5 { public int X, Y; public P5(int x) { this = default; X = x; } public void Reset() { this = new P5(9); } }`
- **THEN** `new P5(3)` holds `3` and `0`, and `Reset()` leaves `9` and `0`, as in .NET

### Requirement: Every constructor a record or a struct declares is reached or refused

A twin SHALL reach each constructor a record or a struct declares by the counts of arguments it
takes, its optional parameters counted. A constructor that does its own work (the primary one, or an
explicit one that does not chain with `: this(…)`) SHALL bind its parameters, set the members, call
its base's constructor with its own arguments and run its body. One that chains with `: this(…)` SHALL
evaluate the chain's arguments from the arguments that arrived, give a parameter the chain leaves out
its default, run the constructor it chains to, and then its own body, even when that constructor's
body returns early. Every argument of a chain and of a base's constructor SHALL land in its
parameter's place and be evaluated in the order it is written, a base clause's where the primary
constructor's parameters are in scope, and a variable an argument declares (`out var n`) SHALL be
declared where it is evaluated. A constructor's parameters SHALL be variables its body may assign. A
record's copy constructor is no branch, and `new` never reaches it. eqc SHALL refuse with EQ1009,
naming it, a constructor that takes a count of arguments another constructor takes too, one that
chains to a constructor that chains in turn, and one that chains `: this()` to a struct's implicit
constructor beside constructors of its own.

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

- **WHEN** `struct Money { public decimal A; public string C; public Money(decimal a) { A = a; C = "EUR"; } public Money(decimal a, string c) { A = a; C = c; } }`
- **THEN** `new Money(1.5m)` holds `1.5` and `EUR`, `new Money(2m, "USD")` holds `2` and `USD`, and
  `default(Money).C` is null, as in .NET

#### Scenario: A derived record's constructors, each with its base's arguments

- **WHEN** `record Rect : Shape` declares `Rect(int side) : base("square")`, `Rect(int w, int h) : base("rect" + (w * h))`
  and `Rect() : this(1)`, over `record Shape(string Kind)`, each logging its initializers and bodies
- **THEN** `new Rect(2).Kind` is `square`, `new Rect(2, 3).Kind` is `rect6`, `new Rect().W` is `1`, and the
  log of initializers, base constructors and bodies reads as .NET's

#### Scenario: A root that returns early

- **WHEN** `record Early` declares `Early(int a) { A = a; if (a < 0) return; Trail += "body "; }` and
  `Early() : this(-1) { Trail += "alt"; }`
- **THEN** `new Early().Trail` is `alt`, as in .NET

#### Scenario: Two constructors with bodies and one count

- **WHEN** `record Twins` declares `Twins(int a) { A = a; }` and `Twins(string s) { A = s.Length; }`
- **THEN** the build fails with EQ1009, naming `Twins(string s)`

#### Scenario: A base's arguments by name, and a base clause naming a constant

- **WHEN** `record Square : Shape { public Square() : base("square", Color: "red") { } }` over
  `record Shape(string Kind, int Sides = 0, string Color = "black")`, and
  `record Circle3(double Radius) : Shape3(DefaultKind)` with `public const string DefaultKind = "circle";`
- **THEN** `new Square()` holds `Sides` `0` and `Color` `red`, and `new Circle3(1).Kind` is `circle`, as in .NET

#### Scenario: A chain's out variable, and a parameter the body assigns

- **WHEN** `record Size(int W, int H) { public Size(string text) : this(int.TryParse(text, out var n) ? n : 0, n) { } }`
  and `record Tag(string Name) { public Tag(string raw, bool trim) : this(raw) { raw = raw.Trim(); Clean = raw; } … }`
- **THEN** `new Size("4")` is `4` by `4`, and `new Tag(" x ", true).Clean` is `x`, as in .NET

#### Scenario: A record's own copy constructor

- **WHEN** `record Doc { public Doc(int capacity) { … } protected Doc(Doc original) { … } }`
- **THEN** the build succeeds and `new Doc(2)` runs the first one

### Requirement: A record compares and prints as .NET does

A record's equality SHALL compare its runtime type, what its base compares, and every instance field
it declares, a private one and a property's store included. Its text SHALL name the members .NET's
`PrintMembers` writes, in its order: its base's members first, then the properties its positional
parameters make, then its public fields and its public properties with a getter, whatever the
getter's own accessibility, in declaration order, a computed one included and an override of a property
its base prints left to the base, each once, and `Name { }` for a record with none.

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

#### Scenario: A getter of its own accessibility, and an override

- **WHEN** `record R { public int Hidden { private get; set; } = 3; public int Shown { get; set; } = 4; }`, and
  `record Derived : Base { public override int V { get; init; } = 2; public int W { get; init; } = 3; }` over
  `record Base { public virtual int V { get; init; } = 1; }`
- **THEN** `new R()` prints `R { Hidden = 3, Shown = 4 }` and `new Derived()` prints `Derived { V = 2, W = 3 }`, as in .NET

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
that declares nothing beside another declaration of its type (alone, it is the whole type, and gets
its twin or its module), a static class, a nested class, a class marked `[ServerOnly]` or
`[RuntimeProvided]`, and a class whose chain of bases reaches an attribute, an exception or a type that
never crosses SHALL get no plain-class module, and no module SHALL import one for them. The chain of
base classes decides, by symbol, an interface never on it, not the name a class or its base has, and the
parser and the resolver read one rule. A name SHALL be a module wherever any declaration of it is one:
a nested class or a refused class of a component's name leaves the component's import as it is. A
record or a struct marked `[ServerOnly]`, or a record over one, SHALL get no twin.

#### Scenario: A partial type declared once and empty

- **WHEN** `public partial class Hollow { }` and `public partial struct Lone { }` are each the only
  declaration of their type
- **THEN** `new Hollow() is Hollow` is true, and `default(Lone)` and `new Lone()` are `Lone`s, as in .NET

#### Scenario: A record with only a method, extended

- **WHEN** `record Animal { public string Kind() => "animal"; }` and `record Dog : Animal;`
- **THEN** `new Dog() is Animal` is true and `new Dog().Kind()` answers `animal`, as in .NET

#### Scenario: A class with no member

- **WHEN** `class Mute : IGreeting { }` where `IGreeting` declares `string Greet() => "hello";`, and
  another module constructs it
- **THEN** that module imports `./Mute`, the module exists, and `((IGreeting)new Mute()).Greet()`
  answers `hello`, as in .NET

#### Scenario: An exception three levels down

- **WHEN** `class Failure : Exception { }`, `class Retry : Failure { }` and `class LastRetry : Retry { }`,
  across files, and `class Underline : Mark { }` over `class Mark : System.Attribute { }`
- **THEN** none of them gets a module and no module imports one, while `class FakeException { }`,
  which derives from no exception, gets its own

#### Scenario: A component beside a nested class of its name

- **WHEN** a component `Header` and `class Api { public class Header { } }` in another file
- **THEN** a module that builds `new Header()` imports `./Header`

#### Scenario: A class over an interface named like an attribute, and a server-only struct

- **WHEN** `class ColorAttribute : IProductAttribute { }` over `interface IProductAttribute { }`, and
  `[ServerOnly] struct TokenHasher` over HMACSHA256
- **THEN** `ColorAttribute` gets a module that its users import, and `TokenHasher` gets no twin and no diagnostic

### Requirement: An instance indexer reaches its twin

An instance indexer of a type whose twin eqc writes SHALL be its twin's `item` method for the getter
and `setItem` for the setter, which answers the value it wrote, and every element access bound to it
SHALL call them: a read, a write, a compound assignment, a step of any type, a coalescing
assignment, a null-conditional access, a deconstruction's target, a key from the end (`^n`, at the count
its type names) and an object initializer's entry, its receiver and keys evaluated once each, each key
in its parameter's place, an omitted optional key as its default and a `params` key packed as C# packs
it. An assignment through it SHALL answer the value it assigned, whatever the setter does with its copy.
eqc SHALL refuse with EQ1007 a second indexer, or any member the twin holds on `item` or `setItem`
beside one, in the same type; an explicit implementation of an interface's indexer holds those names,
and the type's own indexer beside it takes names of its own.

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

#### Scenario: An optional key, a named key and a key from the end

- **WHEN** `this[int row, int col = 0]` is written with `g[1] = 5` and read with `g[col: 1, row: 2]`, and
  `r[^1]` reads and writes a type with `Count` and `this[int]`
- **THEN** each reaches the cell .NET reaches

#### Scenario: A step of an enum, a swap and a clamping setter

- **WHEN** an indexer of an enum is stepped with `m[0]++`, `(g[0], g[1]) = (g[1], g[0])` swaps two entries,
  and a setter that clamps its `value` is assigned with `var y = (g[0] = 250)`
- **THEN** the module loads, each answers as in .NET, and `y` is `250`

### Requirement: A type's statics initialize as .NET runs them

A type whose statics can observe one another (an initializer that is not a constant, or a static
constructor) SHALL start every static at its type's zero, then run the initializers in declaration
order, then the static constructor's body, once, the first time one of its statics is read or written;
a record, a struct, a class, a static class and a component alike. A type that declares a static
constructor SHALL run it before its first instance and the first use of any of its static members, a
method, a computed property and an operator included. A static with no initializer SHALL hold its
type's zero in a type of any kind. The statics SHALL initialize in declaration order across fields,
properties and field-like events. The static constructor's body SHALL run in a function of its own after
the initializers, a re-entrant access during the initialization seeing what C# sees, and a failure SHALL
be kept and thrown as a TypeInitializationException at every use. A static whose initializer C# folds to
a constant SHALL be its value. A component's static constructor SHALL never be its instance constructor.

#### Scenario: An initializer reads a static declared after it

- **WHEN** `record Chain { public static int A = B + 1; public static int B = 2; }`
- **THEN** `Chain.A` is `1` and `Chain.B` is `2`, as in .NET

#### Scenario: A static instance built before a static it reads

- **WHEN** `record Early { public static Early First = new(); public static int Seed = 3; public int Value = Seed * 2; }`
- **THEN** `Early.First.Value` is `0` and `new Early().Value` is `6`, as in .NET

#### Scenario: A static the static constructor sets

- **WHEN** `class Catalog { public static List<string> Names; public static int Size { get; private set; } static Catalog() { Names = new List<string> { "a", "b" }; Size = Names.Count; } }`
- **THEN** `Catalog.Size` read first answers `2`, and `Catalog.Names[1]` answers `b`

#### Scenario: A static method and the first instance

- **WHEN** `class Pinger { static Pinger() { Log.Note("cctor "); } public static int Ping() { Log.Note("ping "); return 1; } }`
  and `class Maker { static Maker() { Log.Note("cctor "); } public Maker() { Log.Note("ctor "); } }`
- **THEN** two calls of `Pinger.Ping()` log `cctor ping ping ` and two `new Maker()` log `cctor ctor ctor `, as in .NET

#### Scenario: Two types whose statics read each other

- **WHEN** `class Ping { public static int A = Pong.B + 1; }` and `class Pong { public static int B = Ping.A + 10; }`,
  and `Ping.A` is read first
- **THEN** `Ping.A` is `11` and `Pong.B` is `10`, as in .NET

#### Scenario: A static constructor that returns early, and one that throws

- **WHEN** `static Config() { Seen = "cctor"; if (X > 0) return; Seen = "late"; }`, and a static whose initializer throws
- **THEN** `Config.X` reads `5` and `Seen` `cctor`, and every read of the other type throws a
  TypeInitializationException, as in .NET

#### Scenario: A property before a field, and a constant read before it is declared

- **WHEN** `static int Base { get; } = Compute();` above `static readonly int Doubled = Base * 2;`, and
  `static readonly int Max = Default * 2;` above `const int Default = 50;`
- **THEN** `Doubled` is twice `Base`, and `Max` is `100`, as in .NET

### Requirement: A record's or a struct's members on one name are refused

A record's or a struct's twin holds the state of each instance as properties of its own and its
methods and computed properties on its prototype, one member per name. eqc SHALL refuse with EQ1007,
naming both, two instance members of a record or a struct that land on one name: two states, or a
state and a method or a computed property. A member the body declares under a positional parameter's
own name is that parameter's property, one member. An abstract property SHALL hold no state, and a
primary constructor's parameter of a class or a struct SHALL be held by an instance only when a member
reads it outside an initializer.

#### Scenario: A captured parameter and a property of its name

- **WHEN** `struct S(int x) { public int X { get; } = x * 2; public int Raw() => x; }`, or
  `readonly struct Money(decimal amount) { public decimal Amount => amount; }`
- **THEN** the build fails with EQ1007, naming both members

#### Scenario: A parameter read only by an initializer, and an abstract property

- **WHEN** `readonly struct Point2(int x, int y) { public int X { get; } = x; public int Y { get; } = y; }`, and
  `record Circle2(double R) : Shape2 { public override string Name => "circle"; }` over
  `abstract record Shape2 { public abstract string Name { get; } }`
- **THEN** both build, `new Point2(3, 4)` holds `3` and `4`, and `new Circle2(2).Name` is `circle`, as in .NET

### Requirement: A deconstruction's part is converted to its target's type and written by what its target is

A deconstruction SHALL write each part as C# writes it: converted to its target's type through the
conversion the bound tree names for that part, and written by what the target is. A dictionary's
entry SHALL be written by its class, its receiver and key evaluated before the value, and a part a
declaration or a loop converts SHALL be declared in its own type. A deconstruction whose parts need
neither SHALL keep its destructuring.

#### Scenario: A dictionary's entries as targets

- **WHEN** browser-side code writes `(map["a"], map["b"]) = new Point(1, 2)` over a
  `Dictionary<string, int>`
- **THEN** `map["a"]` is `1` and `map["b"]` is `2`, as in .NET, where the module did not parse

#### Scenario: An int part into a long

- **WHEN** browser-side code writes `long total; int n; (total, n) = new Point(1, 2);` and then
  `(total + 1L).ToString()`
- **THEN** it answers `2`, as in .NET, where the long arithmetic threw

#### Scenario: A converted declaration and a converted loop

- **WHEN** browser-side code writes `(long t, int m) = pair` for an `(int, int)` pair, and sums
  `a * 10 + b` over `foreach ((long a, int b) in pairs)`
- **THEN** both compute in long, as in .NET
