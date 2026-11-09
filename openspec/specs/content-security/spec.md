# content-security Specification

## Purpose
What the SDK writes so that a Content-Security-Policy can admit the SDK's own scripts and styles and
refuse everything else: the request's nonce on every element the shell writes, and the policy
itself, stated in C# and completed by the SDK with what its own features need (#306).

## Requirements

### Requirement: Every script and style the shell writes carries the request's nonce

The server SHALL make one nonce for each request that serves a page, 128 bits from a
cryptographic random source, and SHALL write it as the `nonce` attribute of every `<script>` and
every `<style>` element the shell writes: the template's own, the app's CSS, every head tag, and
every asset a page declares. Two requests SHALL NOT carry the same nonce. An element that is not a
script or a style SHALL NOT carry it. A head element SHALL be given to the shell as an `HtmlTag`,
so the shell writes its attributes.

#### Scenario: A page with GTM, the app's CSS and a head script

- **WHEN** an app with `UseGtm(...).WithConsent()`, `AddStyles`, `AddScript` and an inline head
  tag serves a page, in Production or in Development, rendered on the server or not
- **THEN** every `<script>` and `<style>` start tag of the document carries one and the same nonce

#### Scenario: A page's own assets

- **WHEN** a page declares an inline script and an inline style through `IRequireAssets`
- **THEN** both are written with the request's nonce

#### Scenario: Two requests

- **WHEN** the same page is requested twice
- **THEN** the two documents carry different nonces, each 16 bytes in base64

### Requirement: A page carries the strict policy the app turned on

When the app calls `UseContentSecurityPolicy`, every page the shell serves SHALL carry a
`Content-Security-Policy` header whose `script-src` admits the request's nonce and `'strict-dynamic'`
and nothing else, with `object-src 'none'`, `base-uri 'none'`, `frame-ancestors 'self'` and
`connect-src 'self'`, each followed by the sources the app and its features stated. An app that did
not turn it on SHALL be served with no policy.

#### Scenario: The strict policy

- **WHEN** an app with `UseContentSecurityPolicy()` serves a page
- **THEN** the header is `script-src 'nonce-<n>' 'strict-dynamic'; object-src 'none'; base-uri 'none'; frame-ancestors 'self'; connect-src 'self'`, where `<n>` is the nonce its scripts carry

#### Scenario: No policy

- **WHEN** an app that did not call `UseContentSecurityPolicy` serves a page
- **THEN** the page carries no `Content-Security-Policy` header

### Requirement: The app states its own sources in C# and per environment

The app SHALL state the origins its own code connects to and is framed by, report-only and a report
endpoint through `ContentSecurityPolicyBuilder`, and the same through `EQuantic:ContentSecurityPolicy`
in configuration. The two SHALL add up, the configuration's report endpoint SHALL win, and a source
that is not `scheme://host[:port][/path]` (a host may start with `*.`) SHALL stop the app at start.

#### Scenario: Code and configuration together

- **WHEN** `Program.cs` allows connections to `https://api.example.com` and framing by `https://partner.example.com`, and configuration sets `ReportOnly`, a connect source `wss://*.live.example.com` and a report URI
- **THEN** the page carries `Content-Security-Policy-Report-Only` with both connect sources, the framing partner and the configuration's report URI

#### Scenario: A source that is a policy string

- **WHEN** configuration sets a connect source `https://api.example.com; script-src *`
- **THEN** the app does not start, and the failure names the source

### Requirement: The SDK adds what its own features need

An integration SHALL declare the sources its feature needs when it is installed, whether or not the
policy is on, and the policy SHALL include them. `UseGtm` SHALL declare the hosts GTM and GA4
connect to and hand the container script the page's nonce. Under `dotnet watch` the policy SHALL be
written report-only.

#### Scenario: GTM

- **WHEN** an app with `UseGtm` and `UseContentSecurityPolicy`, in either order, serves a page
- **THEN** `connect-src` includes `https://*.googletagmanager.com`, `https://*.google-analytics.com` and `https://*.analytics.google.com`, and the container snippet sets the page's nonce on the script it loads

### Requirement: The headers that come with the policy

When the policy is on, every response SHALL carry `X-Content-Type-Options: nosniff` and
`Referrer-Policy: strict-origin-when-cross-origin`, unless something upstream already wrote them,
and only pages SHALL carry the policy.

#### Scenario: A script

- **WHEN** the app with the policy on serves `/_equantic/runtime.js`
- **THEN** the response carries `X-Content-Type-Options: nosniff` and no `Content-Security-Policy`
