## ADDED Requirements

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

### Requirement: The GTM installer follows every answer

With `WithConsent`, the installer SHALL declare every Consent Mode signal `denied` by default, SHALL load
the container on the first grant and turn on only the signals the app named, and SHALL turn every signal
back to `denied` on a denial.

#### Scenario: A denial after a grant

- **WHEN** a visitor who granted then denies, on the same page
- **THEN** every Consent Mode signal is updated to `denied`

#### Scenario: A grant that covers analytics alone

- **WHEN** an app says `WithConsent(GtmConsentSignals.AnalyticsStorage)` and the visitor grants
- **THEN** `analytics_storage` is updated to `granted` and the three advertising signals stay `denied`

### Requirement: A configured name stays inside its string

The installer's scripts SHALL write every configured name as a JSON string literal.

#### Scenario: A name with a quote, a line separator and an end tag

- **WHEN** the data layer's name holds a quote, U+2028 and an end tag
- **THEN** none of them appears unescaped in the page's head
