# page-culture Specification

## Purpose
The format culture a page is in: the data the server writes from .NET's `NumberFormatInfo` and
`DateTimeFormatInfo` for the request's culture on every page, how a culture switch fetches it, what
the browser draws every number, date and calendar name from, and the invariant culture a page with no
culture installed is in.

## Requirements

### Requirement: The format culture travels with every page

The server SHALL write the request's format culture on every page it renders
(`window.__EQ_CULTURE__.format`): the number symbols, separators, group sizes, digits and patterns, and
the date patterns, separators, era, first day of the week and names the browser reads, from .NET's own
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

### Requirement: A standard number format is laid out from the culture's data

`N`, `F`, `C` and `P` SHALL be laid out from the culture's `NumberFormatInfo` as .NET lays them out: the
value rounded to the precision written or to the culture's own digits for the specifier, its whole part
grouped by the specifier's group sizes (`F` groups nothing), in the culture's pattern for its sign, with
the culture's currency and percent symbols. A culture whose data did not travel SHALL be laid out by
`Intl`.

#### Scenario: A culture whose own digits are not ASCII

- **WHEN** `(-1234.5).ToString("N2")` runs in ar-EG
- **THEN** it writes ASCII digits between the culture's separators, `1٬234٫50` after its negative
  sign, as .NET does, where `Intl` wrote ar-EG's own digits, `١٬٢٣٤٫٥٠`

#### Scenario: A currency's space

- **WHEN** `(-1234.5).ToString("C2")` runs in pt-BR
- **THEN** it writes `-R$ 1.234,50` with the U+0020 space of .NET's pattern, where `Intl` wrote U+00A0

#### Scenario: No precision

- **WHEN** `(1234.5).ToString("N")` runs in en-US on a server whose .NET reads its culture data from ICU
- **THEN** it writes `1,234.500`, the culture's three digits, where the browser wrote two

### Requirement: A calendar says what the culture's data says

A calendar's first day of the week and its day and month names SHALL be the format culture's own
`DateTimeFormatInfo`, the data a date's names come from: one copy, travelling with the format data. With
no culture installed they SHALL be the invariant culture's, and for a culture whose data did not travel,
`Intl`'s.

#### Scenario: A page with no culture installed

- **WHEN** a calendar renders with no culture installed, on a host whose default locale is pt-BR
- **THEN** its first day is Sunday and it names the invariant culture's days and months, as a date's
  text does
