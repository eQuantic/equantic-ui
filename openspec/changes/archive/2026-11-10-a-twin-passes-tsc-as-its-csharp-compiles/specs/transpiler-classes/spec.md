## ADDED Requirements

### Requirement: A class's TypeScript twin passes the runtime's type check

A class's twin, written as TypeScript, SHALL pass the runtime's strict `tsc` for every C# shape below, and SHALL
write the same JavaScript it wrote before. An abstract method SHALL be declared in its class's twin whatever its
accessibility. A downcast to a class with a twin SHALL keep its target's type for the checker, the target imported,
without checking the value. A class's module SHALL import every app type its text names, whether or not its compile
was handed a scan of the app. A parameter an alternate constructor may leave missing, a lambda stored in a static, a
value a derived type evaluates before its base's constructor, and a primary constructor's parameter that defaults to
null SHALL each hold the type C# declares for it.

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
