## ADDED Requirements

### Requirement: An accessor is never a Server Action

The server SHALL register as a Server Action a component's method alone, never an accessor, whatever the
assembly carries: an event's `add` or `remove`, or a property's or an indexer's `get` or `set`, marked
`[ServerAction]` SHALL have no id, a request naming one SHALL be answered as an action that does not
exist, and its body SHALL never run. An app whose only `[ServerAction]` marks are on accessors SHALL be
an app with no Server Actions.

#### Scenario: The registry skips an accessor

- **WHEN** a component declares `public event Action Fired { [ServerAction] add …; remove …; }`,
  `public string Label { [ServerAction] get => "x"; }` and `[ServerAction] public string Load() => "ok";`, and its
  assembly is scanned
- **THEN** `<Component>/Load` is an action, and `<Component>/add_Fired` and `<Component>/get_Label` are none, where
  both were registered

#### Scenario: A request naming an accessor

- **WHEN** a request posts to `<Component>/add_Fired` with `[null]` or `[]`, or to `<Component>/get_Label` with `[]`
- **THEN** each is answered 404 and no accessor's body runs, where `add_Fired` was answered 500 and `get_Label` ran
  its body and answered its value

#### Scenario: An app whose only mark is on an accessor

- **WHEN** the only `[ServerAction]` an app's assemblies carry is on a getter
- **THEN** the app reports no Server Actions
