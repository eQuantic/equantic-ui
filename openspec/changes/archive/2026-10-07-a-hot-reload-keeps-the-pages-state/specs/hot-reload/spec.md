# Spec Delta

## ADDED Requirements

### Requirement: A page keeps its fields across a hot reload

A hot reload SHALL hand the reloaded page the fields it held before the save that are data, the ones its C# declares,
before it builds, each rebuilt exactly as the type it was at every depth, and only into a field the
reloaded page still types the same way. The capture writes the hydration spec of each value's runtime
type beside its JSON, a record and a vocabulary value as their own members, a property's store
included. The reload rebuilds each value with `hydrate`, the server payload's own machinery, and runs
none of a record's accessors. The type the reloaded page gives a place is the hydration spec it
declares there, which decides over an empty or a null initializer, or else what its initializer holds
there. A field the edit removed SHALL be left behind, a field the edit added SHALL keep its
initializer, and a field the reload cannot give back exactly, or into a type the page confirms, SHALL
keep its initializer while the others cross: one holding a number JSON does not write as itself, a
value with a life of its own, a record whose class the page does not hand back or whose saved member
the edited class reaches through a setter, a type the edit changed, or a value the reload has to
build where the page neither declares a type nor initializes a value. The first render's adoption of
the server's payload SHALL NOT write a field the replay decided.

#### Scenario: A counter across a save

- **WHEN** the dashboard sample's counter page is at `Count: 3` and an edit to its `.cs` is saved
- **THEN** the page reloads with the edit and shows `Count: 3`, and the next press shows `Count: 4`

#### Scenario: A field the edit removed or added

- **WHEN** the edit removes `_label` and adds `_title`
- **THEN** the reloaded page holds no `_label`, and `_title` has its initializer

#### Scenario: A controller beside the data

- **WHEN** a page holds a record, a `long` and a controller, and is reloaded
- **THEN** the record comes back as a record and the `long` as a `long`, and the controller, whose JSON
  is not it, keeps the one its initializer made

#### Scenario: A number JSON writes as null

- **WHEN** a page holds NaN or an infinity, alone, in a list or in a record, and is reloaded
- **THEN** the field that holds it keeps its initializer, where it came back null, and the page's other
  fields cross

#### Scenario: A controller inside a dictionary

- **WHEN** a page's dictionary holds a controller, or the page holds a sorted dictionary, and is reloaded
- **THEN** the dictionary keeps the one its initializer made: a controller's JSON is not it, and a sorted
  dictionary's order is a comparer the capture cannot name

#### Scenario: The vocabulary's value types

- **WHEN** a page holds a `Point`, an `EdgeInsets`, a `ColorToken`, a list of `Point` it declares and a
  record holding a `Rect`, and is reloaded
- **THEN** each comes back an instance of its own class with its values, where every one of them went
  back to its initializer

#### Scenario: Each value as its type at every depth

- **WHEN** a page that declares its fields' types holds a `long[]`, a `Dictionary<int, long>`, a
  dictionary keyed by `decimal`, an anonymous value holding a `long`, and a `long?` and a `DateTime?`
  its initializer left null, and is reloaded
- **THEN** each comes back as the type it was: the longs as longs, where they came back as strings, and
  the dictionary's keys as numbers and its values as longs, found as it found them

#### Scenario: A list of the app's records

- **WHEN** a page holds a list of records of its own and is reloaded
- **THEN** the list keeps its initializer, where its records came back as plain objects without their
  methods: only an initializer names the class of the app's record after a document reload

#### Scenario: A field whose type the edit changed

- **WHEN** the edit changes `_size` from an `int` to a `string`
- **THEN** the reloaded page's `_size` holds the initializer the edit gave it

#### Scenario: A negative zero

- **WHEN** a page holds a negative zero, alone, in a list, in a record or as a dictionary key, and is
  reloaded
- **THEN** the field that holds it keeps its initializer, where it came back as 0 and `1 / x` answered
  the other infinity

#### Scenario: A record that keeps a property's value in a store

- **WHEN** a page holds eqc's `FRec`, whose setter doubles what it is given, set to 5, and `GRec`, whose
  getter adds one, set to 3, and is reloaded
- **THEN** they come back reading 10 and 4, equal to what they were, where `FRec` came back reading 20

#### Scenario: A record whose member the edit gave a setter

- **WHEN** the edit gives a record's member accessors with a setter over a store
- **THEN** the field keeps its initializer and the setter does not run

#### Scenario: A collection whose element type the edit changed

- **WHEN** the edit changes a `long[]`, a `Dictionary<int, long>` and a `long?` left null to text, on a
  page that declares no types
- **THEN** each keeps the initializer the edit gave it, where the `long[]` handed the edited code BigInts,
  and a list of text into a list of text still crosses

#### Scenario: The type a page declares decides

- **WHEN** a page declares a `long[]`, a `DateTime?` and a `Dictionary<int, long>` whose initializers are
  null or empty, and is reloaded once as it was and once after an edit that declares other types there
- **THEN** each comes back as the type it was the first time, and keeps its initializer the second

#### Scenario: The server's payload on the first render

- **WHEN** the replay leaves a field the server's payload lists at its initializer, and the page mounts
- **THEN** the first render does not write the saved value back, and the page's other server members and
  every other component's payload are adopted

#### Scenario: An app's class that declares equals and with

- **WHEN** a page holds an object of an app's class that overrides `Equals` and declares a `With(...)`,
  and is reloaded
- **THEN** the field keeps the object its initializer made: eqc's twin of a record or a struct says it is
  one (`static $record`), and this class's twin, carried as a record before, was rebuilt without its
  constructor

#### Scenario: A list with a hole or an undefined element

- **WHEN** a page holds a list with an undefined element (a vocabulary struct's `default`), a list
  `Array.Resize` grew, or a tuple holding an undefined, and is reloaded
- **THEN** each keeps its initializer, where JSON wrote the hole and the undefined as null
