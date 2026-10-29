# Spec Delta

## ADDED Requirements

### Requirement: Every member of a type holds a key of its own on its twin

eqc SHALL refuse with EQ1007, naming both members, a second member of one type that lands on a key of
its twin another member already holds: the same lowered name and the same static-ness, whatever kind
each member is, a field, a constant, a property, an event, a method, an operator, a conversion, an
extension block's property, indexer or method, and a primary constructor's parameter the twin holds,
which on a component is every one, read or not. A member's key SHALL be the name the emitter writes it
under: a field's slot, a hiding method's own name, an operator's and a conversion's static. What the twin
holds as one member SHALL NOT be refused: the two accessors of a property, a plain class's field that
moved to a slot of its own, a positional parameter its record redeclares, a component's parameter that is
read nowhere but as the whole initializer of the member of its name, and a static beside an instance
member of its name. A static class nested in a type SHALL be checked once, as the module it is, and a
`[ServerOnly]` one, which is no module, SHALL NOT be checked.

#### Scenario: A static field beside a static method of its name

- **WHEN** an app declares `static class Seq { static int next; public static int Next() => ++next; }`
- **THEN** the build fails with EQ1007 at `Next`, naming `Seq.Next()` and `Seq.next`

#### Scenario: A component's field beside its property

- **WHEN** a stateless component declares `readonly string label = "x";` and `public string Label => label.ToUpper();`
- **THEN** the build fails with EQ1007 at `Label`, naming `Page.Label` and `Page.label`

#### Scenario: Two operators of one symbol

- **WHEN** a record declares `operator +(Sum, Sum)` and `operator +(Sum, int)`
- **THEN** the build fails with EQ1007, naming both operators and asking to keep one

#### Scenario: A plain class's field beside its property

- **WHEN** a class declares `private int level;` and `public int Level { get => level; set => level = value * 2; }`
- **THEN** the build succeeds, the field written in a slot of its own

#### Scenario: A component's unread parameter beside a method of its name

- **WHEN** a component declares `class Tile(string caption) : StatelessComponent` and `public string Caption()`, and no member reads `caption`
- **THEN** the build fails with EQ1007 at `Caption`, naming `Tile.Caption()` and `Tile(caption)`

#### Scenario: A component's parameter that initializes its property

- **WHEN** `class Consent(string? policyHref = null) : StatefulComponent` declares `public string? PolicyHref { get; init; } = policyHref;`
- **THEN** the build succeeds, the parameter written into the property's one slot

#### Scenario: A server-only class nested in a component

- **WHEN** a component nests `[ServerOnly] static class Backend { static int count; public static int Count => count; }`
- **THEN** the build succeeds, and no module holds `Backend`

### Requirement: A member never takes a name its twin holds itself

eqc SHALL refuse with EQ1007 a member that lands on a name the twin holds itself: an instance
`constructor` on any class but a component, whose `constructor` is EQ2011's, a static `prototype` on any
class, `with` on a record or a struct, and `equals`, `getHashCode`, `toString` and `toJSON` on a class, a
record, a struct or a component. An override whose chain of overridden methods ends at object's
`Equals(object)`, `GetHashCode()` or `ToString()` SHALL stand, and the twin SHALL write it in place of its
own member of that name and write nothing else there. An override of a typed `Equals` a base declares
SHALL leave the twin's `equals` in place.

#### Scenario: A record's With method

- **WHEN** a record declares `public Style With(int size) => this with { Size = size };`
- **THEN** the build fails with EQ1007, naming `Style.With(int)` and `with`

#### Scenario: An overload of ToString

- **WHEN** a class declares `public string ToString(string unit)`
- **THEN** the build fails with EQ1007, naming `Temp.ToString(string)` and `toString`

#### Scenario: A struct's Equals override

- **WHEN** a struct declares `public override bool Equals(object? o)`
- **THEN** the build succeeds, and the struct's twin holds one `equals`, the override's

#### Scenario: A record's static ToString

- **WHEN** a record declares `public static string ToString(int v)`
- **THEN** the build succeeds, and the record's text is still the one C# synthesizes

#### Scenario: A component's overload of ToString

- **WHEN** a component declares `public string ToString(string prefix)`
- **THEN** the build fails with EQ1007, naming `Page.ToString(string)` and `toString`

#### Scenario: A derived record's typed Equals override

- **WHEN** `record Base(int X)` declares `public virtual bool Equals(int x)`, and `record Derived(int X, int Y) : Base(X)` overrides it
- **THEN** the build succeeds, and the twin of `Derived` writes an `equals` that compares `Y`
