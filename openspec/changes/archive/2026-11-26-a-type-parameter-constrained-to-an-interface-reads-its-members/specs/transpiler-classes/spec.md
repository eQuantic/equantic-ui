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
SHALL be declared `T extends` that class, the class imported, a nullable one (`ShapeBase?`) by its class, in TypeScript
only. A type parameter constrained to an interface that has a twin SHALL be declared `T extends` that interface, the
interface imported, in TypeScript only. Every in-source interface SHALL have a twin when its twin can be written: it
declares no type parameter and stands in no generic type, each member is an
instance property or method with no body, no member is static, an event or an indexer, it extends no
interface, and the build does not keep it on the server. Its twin SHALL be a module holding `export interface` with each member
under its twin's name and type, read-only where the property has no setter, and a value of the same name so a plain
import resolves, as a union's alias module holds; plain JavaScript SHALL write the value alone. A value typed as the
interface SHALL stay `any`. Any other constraint SHALL leave the parameter unconstrained. A cast
to a type parameter SHALL assert it in TypeScript, `(value as T)`, checking nothing, as a downcast checks nothing,
where TypeScript has the parameter in scope: a method's own, and a class's in an instance member of that class. A
local function's own, a class's in a static member, which TypeScript forbids to read it, and a class's in a type nested
in it, which is a module of its own, SHALL write the value alone, as before.

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
- **THEN** their modules declare `class Factory<T>`, `class Pair<TKey>` and `class Slot<TValue>`; `Pair`'s `with` answers
  `Pair<TKey>`, `Slot`'s `with` and `$clone` answer `Slot<TValue>` and its static `$zero` answers `Slot<any>`; and `tsc`
  takes them

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

#### Scenario: A cast to a type parameter TypeScript cannot see

- **WHEN** the same class's static member returns `(T)value`, and an instance member's local function
  `static TOut Cast<TOut>(object held) => (TOut)held` casts to its own parameter
- **THEN** both write the value alone, and `tsc` takes them; asserted, it refused them with TS2302 and TS2304

#### Scenario: A nullable class constraint and a type nested in a generic one

- **WHEN** `class Maybe<T> where T : ShapeBase?` reads `_shape?.Area`, and `class Owner<T>` declares a nested `class Held`
  whose instance member returns `(T)value`
- **THEN** `Maybe`'s header is `Maybe<T extends ShapeBase>` and imports `ShapeBase`, `Held` writes the value alone, plain
  JavaScript imports no class only a constraint names, and `tsc` takes them

#### Scenario: A type parameter constrained to an interface

- **WHEN** `interface IShaped { int Area { get; } string Describe(string unit); }` and
  `class Measured<T> where T : IShaped` reads `_shape.Area` and calls `_shape.Describe("cm")` on a `T`
- **THEN** the build writes a module `IShaped` holding `export interface IShaped { readonly area: number;
  describe(unit: string): string; }`, `Measured`'s header is `Measured<T extends IShaped>` with
  `import { IShaped }`, and the runtime's `tsc` takes the reads; it refused them with TS2339

#### Scenario: An interface whose twin cannot be written

- **WHEN** a type parameter is constrained to a generic interface, to one with a static member, a default member, an
  event or an indexer, or to one that extends another interface, an in-source one or the BCL's
- **THEN** no module is written for it and the class header declares the parameter unconstrained, as before

#### Scenario: A constraint with no twin

- **WHEN** a type parameter is constrained to `struct`, `class`, `new()` or `notnull`
- **THEN** its class header declares it unconstrained, as before