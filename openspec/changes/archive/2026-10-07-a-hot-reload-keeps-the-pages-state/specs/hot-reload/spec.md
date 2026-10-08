# Spec Delta

## ADDED Requirements

### Requirement: A page keeps its fields across a hot reload

A hot reload SHALL hand the reloaded page the fields it held before the save that are data, the ones its C# declares,
before it builds, each rebuilt as the type it was at every depth: the capture writes the hydration spec
of each value's runtime type beside its JSON, and the reload rebuilds it with `hydrate`, the server
payload's own machinery. A field the edit removed SHALL be left behind, a field the edit added SHALL
keep its initializer, and a field the reload cannot give back as it was SHALL keep its initializer while
the others cross: one holding a number JSON writes as null, a value with a life of its own, a record of
the app's where the reloaded page's initializer holds none of its class, or a type the edit changed.

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

- **WHEN** a page holds a `Point`, an `EdgeInsets`, a `ColorToken`, a list of `Point` and a record holding
  a `Rect`, and is reloaded
- **THEN** each comes back an instance of its own class with its values, where every one of them went
  back to its initializer

#### Scenario: Each value as its type at every depth

- **WHEN** a page holds a `long[]`, a `Dictionary<int, long>`, a dictionary keyed by `decimal`, an
  anonymous value holding a `long`, and a `long?` and a `DateTime?` its initializer left null, and is
  reloaded
- **THEN** each comes back as the type it was: the longs as longs, where they came back as strings, and
  the dictionary's keys as numbers and its values as longs, found as it found them

#### Scenario: A list of the app's records

- **WHEN** a page holds a list of records of its own and is reloaded
- **THEN** the list keeps its initializer, where its records came back as plain objects without their
  methods: only an initializer names the class of the app's record after a document reload

#### Scenario: A field whose type the edit changed

- **WHEN** the edit changes `_size` from an `int` to a `string`
- **THEN** the reloaded page's `_size` holds the initializer the edit gave it
