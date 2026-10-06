## ADDED Requirements

### Requirement: The format culture travels with every page

The server SHALL write the request's format culture on every page it renders
(`window.__EQ_CULTURE__.format`): the number symbols and the date patterns, separators, era and names
the browser's formatter reads, and the ISO code of the culture's currency, read from .NET's own
`NumberFormatInfo` and `DateTimeFormatInfo`, whether or not the app has a string to translate. Boot SHALL
install it before hydration. A culture switch with no reload SHALL fetch the same document for the
culture it switches to from `/_equantic/culture/{name}.json`, and a name .NET does not know SHALL be
not found.

#### Scenario: An app with no string catalog

- **WHEN** a page renders in pt-BR for an app that has no `.resx`
- **THEN** `__EQ_CULTURE__` carries pt-BR's format data, and the hydrated page writes `1,5` where the
  server wrote `1,5`

#### Scenario: A culture switch

- **WHEN** `/_equantic/culture/de-DE.json` and `/_equantic/culture/xx-XX.json` are requested
- **THEN** the first answers de-DE's format document and the second is not found

### Requirement: A page with no culture installed is in the invariant culture

A page or a test that has installed no culture SHALL format in the invariant culture, never in its
host's locale, and a conformance case SHALL run both sides in the culture it names, or in the invariant
one when it names none.

#### Scenario: A host whose locale is pt-BR

- **WHEN** `(1234.5).ToString("N2")` runs in the browser with no culture installed, on a host whose
  default locale is pt-BR
- **THEN** it writes `1,234.50`, the invariant culture's text
