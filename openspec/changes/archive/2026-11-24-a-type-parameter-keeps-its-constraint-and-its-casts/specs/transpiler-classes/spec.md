## MODIFIED Requirements

### Requirement: A class's TypeScript twin passes the runtime's type check

A class's twin, written as TypeScript, SHALL pass the runtime's strict `tsc` for every C# shape below, and SHALL
write the same JavaScript it wrote before. An abstract method SHALL be declared in its class's twin whatever its
accessibility. A downcast to a class with a twin SHALL keep its target's type for the checker, the target imported,
without checking the value. A class's module SHALL import every app type its text names, whether or not its compile
was handed a scan of the app. A parameter an alternate constructor may leave missing, a lambda stored in a static, a
value a derived type evaluates before its base's constructor, and a primary constructor's parameter that defaults to
null SHALL each hold the type C# declares for it. A generic class, a plain one, a record, a struct or a
component, SHALL declare its type parameters in TypeScript and none in plain JavaScript, and a record's members
SHALL name its own type by its closure. A type parameter constrained to a class that is not generic and has a twin
SHALL be declared `T extends` that class, the class imported; any other constraint, an interface's included, since
an interface has no twin, SHALL leave it unconstrained. A cast
to a type parameter SHALL assert it in TypeScript, `(value as T)`, checking nothing, as a downcast checks nothing.

#### Scenario: A base calls its abstract members

- **WHEN** an abstract class's method calls a public, an internal, a private protected and a protected abstract method
- **THEN** its twin declares all four, and `tsc` takes the calls

#### Scenario: A downcast reaches a member only the target has

- **WHEN** a static method returns `((Square)shape).Side` for a parameter of the abstract type `Shape`
- **THEN** its module imports `Shape` and `Square`, writes `(shape as Square).side`, and `tsc` takes it

#### Scenario: A constructor chains to a wider one

- **WHEN** a class's one-parameter constructor chains with `: this(step, live: true)` to a private constructor that
  stores both parameters in members
- **THEN** `tsc` takes the stores

#### Scenario: A static comparison is a lambda

- **WHEN** a class declares `static readonly Comparison<Item> ByDepth = static (a, b) => a.Depth.CompareTo(b.Depth);`
- **THEN** `tsc` reports no parameter of an implicit `any`

#### Scenario: A derived class's field holds a list

- **WHEN** a class over a base declares `private List<string?> _items = new();` and a constructor calling `: base(name)`
- **THEN** the local its constructor evaluates the list into is declared `(string | null)[]`, and `tsc` takes it

#### Scenario: A primary constructor takes a string

- **WHEN** a class declares `class Base(string name)`
- **THEN** its twin's constructor is `constructor(name: string = null!)`, and `tsc` takes it

#### Scenario: A plain generic class, a record and a struct declare their type parameters

- **WHEN** an app or the library declares `class Factory<T>` storing a `Func<T>`, `record Pair<TKey>(TKey Key, int Rank)`
  and `struct Slot<TValue>` with a `TValue` field
- **THEN** their modules declare `class Factory<T>`, `class Pair<TKey>` and `class Slot<TValue>`, a record's `with` and
  `$clone` answer `Slot<TValue>`, and `tsc` takes them

#### Scenario: Plain JavaScript declares no type parameters

- **WHEN** the same classes and a generic component are compiled with type annotations off
- **THEN** no class header carries a type parameter list, and the modules parse

#### Scenario: A type parameter constrained to a class

- **WHEN** `class Measured<T> where T : ShapeBase` reads `_shape.Area` on a `T`, and `record Tagged<T>(T Shape, string Tag)
  where T : ShapeBase` reads `Shape.Area`
- **THEN** their modules declare `class Measured<T extends ShapeBase>` and `class Tagged<T extends ShapeBase>`, import
  `ShapeBase`, and `tsc` takes the reads; it refused them with TS2339

#### Scenario: A cast to a type parameter

- **WHEN** `class Handler<T> where T : ShapeBase` holds an `Action<T>` and calls it with `(T)shape` for a `ShapeBase`
- **THEN** the twin writes `(shape as T)`, and `tsc` takes the call; it refused it with TS2345

#### Scenario: A constraint with no twin

- **WHEN** a type parameter is constrained to an interface, to `struct`, `class`, `new()` or `notnull`
- **THEN** its class header declares it unconstrained, as before
