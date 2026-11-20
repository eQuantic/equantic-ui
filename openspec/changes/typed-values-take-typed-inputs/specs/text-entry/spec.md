## ADDED Requirements

### Requirement: A text entry says what kind of value it takes

A `TextEntry` SHALL take a `Kind`, a `TextEntryKind` of `Text` (the default), `Email`, `Phone`, `Url`,
`Number` or `Decimal`. On the web, a single-line entry SHALL lower to the input that opens each kind's
keyboard and offers its autofill: `Email` to `type="text"`, `inputmode="email"` and
`autocomplete="email"`; `Phone` to `type="tel"` and `autocomplete="tel"`; `Url` to `type="text"`,
`inputmode="url"` and `autocomplete="url"`; `Number` to `type="text"` and `inputmode="numeric"`;
`Decimal` to `type="text"` and `inputmode="decimal"`. Every kind but `Text` SHALL say
`spellcheck="false"` and `autocapitalize="none"`. An obscured entry SHALL stay `type="password"`, with
its kind's `inputmode`. The server's lowering and the browser's SHALL be the same element. A
multi-line entry SHALL take no kind's attributes.

#### Scenario: An e-mail field

- **WHEN** an entry of kind `Email` is lowered on the server and in the browser
- **THEN** both are an `input` with `type="text"`, `inputmode="email"`, `autocomplete="email"`,
  `spellcheck="false"` and `autocapitalize="none"`

#### Scenario: A PIN

- **WHEN** an obscured entry of kind `Number` is lowered
- **THEN** it is `type="password"` with `inputmode="numeric"`

#### Scenario: The caret of an autofocused e-mail field

- **WHEN** an autofocused entry of kind `Email` holding `a@b` is mounted in a browser
- **THEN** its caret is at the end of `a@b` and nothing throws
