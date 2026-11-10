## ADDED Requirements

### Requirement: Nothing a style element holds can end the element

Every style element the server writes (the atomic sheet, the theme's sheet and an app's own styles)
SHALL write each `</` in its text as `\3c /`, so a value can never end the element.

#### Scenario: A style value that holds the element's end tag

- **WHEN** an `HtmlStyle` value holds a style element's end tag
- **THEN** the atomic sheet's element is closed once, by its own end, and the value's `</` is written
  `\3c /`

#### Scenario: An app's own styles

- **WHEN** an app adds styles whose text holds a style element's end tag
- **THEN** the page holds them with every `</` written `\3c /`, and their element is closed once
