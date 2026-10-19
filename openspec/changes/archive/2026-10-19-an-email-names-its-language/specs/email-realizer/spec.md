## ADDED Requirements

### Requirement: A message names its culture

`EmailRenderer.Render` SHALL take the message's culture as an optional `CultureInfo`. The document
SHALL name it on `<html lang>` by its IETF language tag, escaped as an attribute, so a legacy
.NET name such as `zh-CHS` is written `zh-Hans`, and SHALL name `en` when no culture
is given or the invariant culture is. The tree SHALL be rendered under the given culture, its formatting
and its lookup culture alike, so what a component writes in Build is in that culture, while every
length and color in the markup SHALL be written invariant. The caller's ambient cultures SHALL be in
force again when the render returns or throws.

#### Scenario: A Portuguese message

- **WHEN** an app renders a message with `culture: CultureInfo.GetCultureInfo("pt-BR")`
- **THEN** its HTML opens with `<html lang="pt-BR"`

#### Scenario: A legacy Chinese culture

- **WHEN** an app renders a message with `CultureInfo.GetCultureInfo("zh-CHS")` or `"zh-CHT"`
- **THEN** its HTML opens with `<html lang="zh-Hans"` or `<html lang="zh-Hant"`, the tags those names stand for

#### Scenario: No culture, or the invariant one

- **WHEN** an app renders a message with no culture, or with `CultureInfo.InvariantCulture`
- **THEN** its HTML opens with `<html lang="en"`

#### Scenario: The tree writes in the message's culture

- **WHEN** a component writes `$"{1.5}|{new DateTime(2024, 1, 15):d}"` in Build and the message is
  rendered in pt-BR, en-US and sv-SE
- **THEN** both parts read `1,5|15/01/2024`, `1.5|1/15/2024` and `1,5|2024-01-15`

#### Scenario: The markup does not change with the culture

- **WHEN** the same tree, with fractional lengths in its styles, is rendered in en-US, pt-BR, sv-SE,
  de-DE, ar-SA and fa-IR
- **THEN** the HTML is identical in all of them but for the `lang` attribute

#### Scenario: The ambient culture is restored

- **WHEN** a message is rendered in pt-BR while the caller's cultures are de-DE and fr-FR, and again for
  a tree whose render throws
- **THEN** the caller's cultures are de-DE and fr-FR after each
