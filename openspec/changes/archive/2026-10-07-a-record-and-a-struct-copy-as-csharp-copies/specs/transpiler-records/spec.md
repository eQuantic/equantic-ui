## MODIFIED Requirements

### Requirement: A copy runs nothing

A `with` expression SHALL copy the record's members and then set the members it names, running no
initializer and no constructor, as .NET's copy does, through the twin's own copy for a struct and for a
record whose twin eqc writes in a namespace of the vocabulary. A record whose chain declares a copy
constructor SHALL be copied through it, as C#'s `with` is: a level whose copy constructor is
synthesized copies its own members after its base's step, and a declared one starts the level's
members at their zero, runs its base's step with what its `: base(…)` passes, and then its body. An
assignment to `this` in a struct SHALL copy the value's state onto the instance.

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

#### Scenario: A declared copy constructor over a base that declares one

- **WHEN** `record Base1 { public int B = 7; public int Copied; protected Base1(Base1 o) { Copied = o.B + 100; } }`
  and `record Derived1 : Base1 { public int D = 3; protected Derived1(Derived1 o) : base(o) { D = o.D * 10; } }`,
  and `x.B = 1; var y = x with { };` over a `new Derived1()`
- **THEN** `y` holds `B = 0`, `Copied = 101` and `D = 30`, as in .NET

#### Scenario: A deep copy a copy constructor writes

- **WHEN** a record's copy constructor builds its own list from the original's, and the copy's list is added to
- **THEN** the original's list keeps its count

## ADDED Requirements

### Requirement: A mutable struct and a value tuple are copied where C# copies them

A value tuple, and a struct of the compilation that is not readonly, SHALL behave as values: a write of
a member through one name, or a call of a method that writes the value's own state, SHALL NOT show
through any other name the value was assigned, passed, returned or boxed to. The twin SHALL copy on
write: before such a write, the variable, parameter, field or array element that holds the value is
given a copy of it, each value along the path first. `this` SHALL be copied where it leaves its struct's
member for a variable, an argument, a return or a boxing, and a mutating call on a property's or a
call's result SHALL run on a copy. A capture SHALL read the variable, as a C# closure does. A write
through a readonly field outside its constructor, a `foreach` or `using` variable, an `in` or `ref`
parameter, or a path whose evaluation has effects is done in place.

#### Scenario: A tuple and a struct assigned to another variable

- **WHEN** `var u = t; t.Item1 = 9;` over `var t = (1, 2);`, and `var b = a; a.X = 9;` over a mutable struct
- **THEN** `u.Item1` stays `1` and `b.X` keeps its value, as in .NET

#### Scenario: An argument and a mutating method

- **WHEN** a method writes a field of the struct it is passed, and `c.Move(4)` writes `c` after `var d = c;`
- **THEN** the caller's struct keeps its value, and `d` keeps the value `c` had

#### Scenario: A value inside a value, an array element and a class's field

- **WHEN** `l.A.X = 5` after `var m = l;`, `arr[0].X = 7` after `var p = arr[0];`, and `h.P.X = 3` after `var q = h.P;`
- **THEN** `m.A.X`, `p.X` and `q.X` keep their values, as in .NET

#### Scenario: this leaving its member

- **WHEN** `public int Snap() { var copy = this; V = 5; return copy.V; }` runs on a value whose `V` is `1`
- **THEN** it answers `1`, and the value holds `5`
