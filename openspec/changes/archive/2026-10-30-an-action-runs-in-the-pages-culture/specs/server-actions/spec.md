## ADDED Requirements

### Requirement: A Server Action runs in the culture of its page

The runtime SHALL name the page's format culture and language on every Server Action call, in the
`culture` and `ui-culture` query keys, so the app's request localization applies them ahead of the cookie
and `Accept-Language`. A culture the app does not support SHALL be refused there, and the next provider
SHALL decide. A page with no culture installed SHALL name none.

#### Scenario: A Portuguese page with an English browser

- **WHEN** a page in `pt-BR` calls an action, with `Accept-Language: en` and no culture cookie
- **THEN** the handler runs in `pt-BR`, and `1234.5.ToString("N1")` answers `1.234,5`

#### Scenario: A cookie that says otherwise

- **WHEN** a page in `pt-BR` calls an action with a culture cookie that says `es`
- **THEN** the handler runs in `pt-BR`

#### Scenario: A switch with no reload

- **WHEN** a page switches from `pt-BR` to `es` and calls an action
- **THEN** the call names `es`

#### Scenario: A culture the app does not support

- **WHEN** a call names `de`, which the app does not support, with `Accept-Language: pt-BR`
- **THEN** the handler runs in `pt-BR`

#### Scenario: A call that names no culture

- **WHEN** a call names no culture
- **THEN** the cookie and `Accept-Language` decide, as they do for any request
