## ADDED Requirements

### Requirement: A shipped component carries every setting a parent hands it

Every `StatefulComponent` the SDK ships SHALL carry, through `AdoptConfig`, every public `set` or `init`
property and every constructor argument of the fresh instance a parent's rebuild hands its position, so the
kept instance answers what the fresh one does. An input read once on purpose SHALL be named, with its
reason, in `tests/eQuantic.UI.Web.Tests/Coverage/adopt-config.baseline.txt`, and that list SHALL only shrink.
A component that keeps the fresh instance whole as its configuration SHALL count as carrying every input.

#### Scenario: A form disables its fields while it submits

- **WHEN** a page holds a `DatePicker`, and rebuilds handing it `Disabled = true`
- **THEN** the picker its position keeps is disabled

#### Scenario: A component drops a setting

- **WHEN** a shipped component's `AdoptConfig` does not carry one of its `init` properties
- **THEN** `AdoptConfigCoverageTests` fails, naming the component and the property

#### Scenario: A setting read once on purpose

- **WHEN** a page rebuilds handing a `CodeEditor` another `InitialCode`
- **THEN** the editor keeps the document someone is typing in, and the baseline names `CodeEditor.InitialCode`
  with its reason
