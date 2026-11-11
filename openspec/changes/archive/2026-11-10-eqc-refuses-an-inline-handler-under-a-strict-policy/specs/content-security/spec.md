## ADDED Requirements

### Requirement: The build refuses an inline handler under the app's policy

When the app calls `UseContentSecurityPolicy`, its build SHALL refuse with EQ2115 every
`HtmlElement.OnClientClick` and `OnLoad`, and every constant `DynamicElement.CustomAttributes` key
that starts with `on`, in every source the build compiles, the projects the app references
included. An app that does not call it SHALL build as before.

#### Scenario: A handler in a referenced library

- **WHEN** an app that turned the policy on references a library that sets `OnClientClick` on a `DynamicElement`
- **THEN** the build fails with EQ2115 at the line that sets it

#### Scenario: A data attribute

- **WHEN** a `DynamicElement` carries the custom attribute `data-kind`
- **THEN** no EQ2115 is reported for it

#### Scenario: No policy

- **WHEN** an app that did not turn the policy on sets `OnClientClick`
- **THEN** its build reports no EQ2115
