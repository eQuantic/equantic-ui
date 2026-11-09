## ADDED Requirements

### Requirement: A page's styles are inside the policy

The policy SHALL write `style-src` admitting the request's nonce and `style-src-attr 'unsafe-inline'`.
Every style element and every stylesheet link the shell writes SHALL carry the request's nonce, and
every style element the runtime creates SHALL carry the page's nonce before it enters the document.
The runtime SHALL NOT write a style through `setAttribute('style', …)`.

#### Scenario: A stylesheet link

- **WHEN** the app adds a stylesheet with `AddStylesheet`
- **THEN** the page's `<link rel="stylesheet">` carries the request's nonce

#### Scenario: A page the server did not render

- **WHEN** the runtime creates the atomic sheet's style element on a page served with a nonce
- **THEN** the element carries that nonce

#### Scenario: The strict policy

- **WHEN** an app with `UseContentSecurityPolicy()` serves a page
- **THEN** the policy carries `style-src 'nonce-<n>'` and `style-src-attr 'unsafe-inline'`, with the nonce its scripts carry
