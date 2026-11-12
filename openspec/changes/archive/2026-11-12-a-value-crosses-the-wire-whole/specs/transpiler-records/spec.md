## ADDED Requirements

### Requirement: A hydrated value holds what the wire does not carry

A record that declares a field SHALL be rebuilt from the wire through its primary constructor, and a struct from its zero, before the payload's members are adopted, so a hydrated value equals the same value built in the browser and its private state answers. A private field the server changed SHALL NOT cross.

#### Scenario: A record with private fields

- **WHEN** a page's state holds `record Product(int Id, string Name) { private int _views; private string _tag = Name + "!"; }`
- **THEN** after hydration it equals `new Product(1, "x")`, its hash is that value's, and its private fields hold what their initializers say

### Requirement: A dictionary's key hydrates as a value

A dictionary keyed by a record or a struct SHALL arrive with each key on its twin, so the browser finds it by a key built there.

#### Scenario: A record key

- **WHEN** a page prefetches `Dictionary<Point, int> { [new Point(1, 2)] = 5 }` and reads `d[new Point(1, 2)]`
- **THEN** the first build in the browser reads 5, as the server drew

### Requirement: A twin writes the JSON the server's serializer writes

A twin that holds a field, private state or a parameter of a primary constructor SHALL write in JSON only what `System.Text.Json` writes under `EqJson.Options`: public instance properties with a public getter, what `[JsonInclude]` adds and `[JsonIgnore]` takes out, under the name `[JsonPropertyName]` gives, each read through its member.

#### Scenario: A public field

- **WHEN** a server action receives a class with `public int Total = 7;`
- **THEN** its JSON is `{}`, as the server writes it
