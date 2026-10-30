# server-actions Specification

## Purpose
What the SDK's Server Action endpoint accepts: a call the app's own pages make, and none another
site makes for them.

## Requirements

### Requirement: A Server Action refuses a request from another site

The Server Action endpoint SHALL refuse with 403, before the action runs, a request whose `Origin`
is neither the app's own host nor an origin the app allows, the opaque `null` included, and a request
without an `Origin` whose `Sec-Fetch-Site` is `cross-site` or `same-site`. A request with neither
header SHALL run. The app's own host SHALL be the request's `Host`, compared by host and port, a `Host` without a
port standing for the origin scheme's default one, and a raw `X-Forwarded-Host` SHALL NOT be read.

#### Scenario: The app's own page

- **WHEN** an action is posted with `Origin: https://app.example` to the host `app.example`
- **THEN** it runs

#### Scenario: The app's own page at an IPv6 address

- **WHEN** an action is posted with `Origin: https://[::1]:8443` to the host `[::1]:8443`
- **THEN** it runs

#### Scenario: The same host on another port

- **WHEN** an action is posted with `Origin: https://app.example` to the host `app.example:80`
- **THEN** the answer is 403 and the action does not run

#### Scenario: Another site

- **WHEN** an action is posted with `Origin: https://evil.example` to the host `app.example`
- **THEN** the answer is 403 and the action does not run

#### Scenario: An opaque origin

- **WHEN** an action is posted with `Origin: null`
- **THEN** the answer is 403 and the action does not run

#### Scenario: No origin, and the browser says cross-site

- **WHEN** an action is posted with no `Origin` and `Sec-Fetch-Site: cross-site`
- **THEN** the answer is 403 and the action does not run

#### Scenario: Not a browser

- **WHEN** an action is posted with neither `Origin` nor `Sec-Fetch-Site`
- **THEN** it runs

#### Scenario: A forwarded host the request writes itself

- **WHEN** an action is posted with `Origin: https://evil.example` and `X-Forwarded-Host: evil.example` to the host `app.example`
- **THEN** the answer is 403 and the action does not run

### Requirement: An app allows the origins its pages are served from

An app SHALL allow named origins in `EQuantic:ServerActions:AllowedOrigins` and with
`UIOptions.AllowServerActionOrigins`, and an action posted from an allowed origin SHALL run. An
allowed origin that is not an absolute `http` or `https` URI with a host and nothing else SHALL stop
the app at start, naming the value.

#### Scenario: An allowed origin

- **WHEN** `https://admin.example` is allowed and an action is posted with that `Origin` to the host `api.example`
- **THEN** it runs

#### Scenario: A malformed origin

- **WHEN** `https://admin.example/login` is allowed
- **THEN** the app does not start, and the error names `https://admin.example/login`

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
