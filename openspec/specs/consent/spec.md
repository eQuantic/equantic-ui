# consent Specification

## Purpose
TBD - created by archiving change gtm-consent-follows-each-answer. Update Purpose after archive.

## Requirements

### Requirement: The answer is kept where only the page's host can write it

On a page served over HTTPS the visitor's answer SHALL be kept in the cookie `__Host-eq-consent`, written
with `Secure`, and the browser, the server and the GTM installer's script SHALL read only that cookie
there. A page served over HTTP SHALL keep it in `eq-consent`. A value other than `granted` or `denied`
SHALL read as unanswered.

#### Scenario: A cookie another host wrote

- **WHEN** a request over HTTPS carries `eq-consent=granted` and no `__Host-eq-consent`
- **THEN** the server reads the answer as unanswered

#### Scenario: A grant over HTTPS

- **WHEN** a visitor grants on a page served over HTTPS
- **THEN** the answer is kept in `__Host-eq-consent`, and that is the answer the next read returns

### Requirement: The GTM installer waits for consent and follows every answer

Unless the app says `WithoutConsent()`, the installer SHALL declare every Consent Mode signal `denied` by
default, SHALL load the container on the first grant and turn on `analytics_storage` alone, or the signals
the app named with `WithConsentGrants`, and SHALL turn every signal back to `denied` on a denial. With
`WithoutConsent()` it SHALL load the container on every page.

#### Scenario: A plain UseGtm

- **WHEN** an app says `UseGtm("GTM-…")` and nothing else
- **THEN** the container is fetched only on a granted answer, and a grant turns on `analytics_storage`
  while the three advertising signals stay `denied`

#### Scenario: An app that asks nothing

- **WHEN** an app says `WithoutConsent()`
- **THEN** the container loads on every page and no consent default is declared

#### Scenario: A denial after a grant

- **WHEN** a visitor who granted then denies, on the same page
- **THEN** every Consent Mode signal is updated to `denied`

#### Scenario: A grant that covers advertising too

- **WHEN** an app says `WithConsentGrants(GtmConsentSignals.All)` and the visitor grants
- **THEN** all four signals are updated to `granted`

### Requirement: A configured name stays inside its string

The installer's scripts SHALL write every configured name as a JSON string literal.

#### Scenario: A name with a quote, a line separator and an end tag

- **WHEN** the data layer's name holds a quote, U+2028 and an end tag
- **THEN** none of them appears unescaped in the page's head
