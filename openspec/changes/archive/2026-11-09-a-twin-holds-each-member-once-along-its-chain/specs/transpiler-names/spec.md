## MODIFIED Requirements

### Requirement: Every member of a type holds a key of its own on its twin

eqc SHALL refuse with EQ1007, naming both members, a second member of one type that lands on a key of
its twin another member already holds: the same lowered name and the same static-ness, whatever kind
each member is, a field, a constant, a property, an event, a method, an operator, a conversion, an
extension block's property, indexer or method, and a primary constructor's parameter the twin holds,
which on a component is every one, read or not. A member's key SHALL be the name the emitter writes it
under: a field's slot, a hiding method's own name, an operator's and a conversion's static. What the twin
holds as one member SHALL NOT be refused: the two accessors of a property, the two halves of a partial
property, indexer or event, a plain class's field that moved to a slot of its own, a static field of any
type that moved to one, a positional parameter its record redeclares, a component's parameter that is
read nowhere but as the whole initializer of the auto-property or the field of its name, in either case
and with nothing to convert between the two, and a static beside an instance
member of its name. A static class nested in a type SHALL be checked once, as the module it is, and a
`[ServerOnly]` one, which is no module, SHALL NOT be checked. Along a class chain, eqc SHALL refuse with
EQ1007, naming both members and the base's file and line, a member on the key a member of a base the
source declares holds, statics included, where one of the two is not a method: an override SHALL stand,
and so SHALL a method that hides a member and holds a name of its own, and a field that moved to a slot
of its own.

#### Scenario: A static field beside a static method of its name

- **WHEN** an app declares `static class Seq { static int next; public static int Next() => ++next; }`
- **THEN** the build succeeds, the field written in a slot of its own, and two calls of `Next()` answer 1
  and 2, as in .NET

#### Scenario: A static auto-property beside a static method of its name

- **WHEN** an app declares `class Cfg { public static int Count { get; set; } public static int count() => 1; }`
- **THEN** the build fails with EQ1007 at `count`, naming `Cfg.count()` and `Cfg.Count`

#### Scenario: A derived method on its base's property

- **WHEN** an app declares `class Base { public int Size => 3; }` and `class Derived : Base { public int size() => 4; }`
- **THEN** the build fails with EQ1007 at `size`, naming `Derived.size()` and `Base.Size` with its file and line

#### Scenario: A derived component's property on its base's field

- **WHEN** a component `Shell` declares `protected string title = "x";`, and a component derived from it
  declares `public string Title => "y";`
- **THEN** the build fails with EQ1007 at `Title`, naming `Page.Title` and `Shell.title`, since a
  component's field does not move

#### Scenario: An override and a hiding method along the chain

- **WHEN** a derived class overrides its base's `virtual int Size`, or declares `new int Size()` beneath
  its base's `int Size => 3`
- **THEN** the build succeeds: the override is the member it overrides, and the hiding method holds
  `size$1`

#### Scenario: A partial property is one member

- **WHEN** a class declares `public partial int Level { get; set; }` and
  `public partial int Level { get => field; set => field = value * 2; }`
- **THEN** the build succeeds, and no EQ1007 names the two halves

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

#### Scenario: A component's parameter that initializes its property through a conversion

- **WHEN** `class Meter(int count) : StatelessComponent` declares `public long Count { get; init; } = count;`
- **THEN** the build fails with EQ1007 at `Count`, naming `Meter.Count` and `Meter(count)`

#### Scenario: A component's parameter on an indexer's name

- **WHEN** `class Grid(string item) : StatelessComponent` declares `public int this[int i] => i * 2;`, and no member reads `item`
- **THEN** the build fails with EQ1007, naming the indexer and `Grid(item)`

#### Scenario: A component's parameter that initializes its property

- **WHEN** `class Consent(string? policyHref = null) : StatefulComponent` declares `public string? PolicyHref { get; init; } = policyHref;`
- **THEN** the build succeeds, the parameter written into the property's one slot

#### Scenario: A server-only class nested in a component

- **WHEN** a component nests `[ServerOnly] static class Backend { static int count; public static int Count => count; }`
- **THEN** the build succeeds, and no module holds `Backend`
