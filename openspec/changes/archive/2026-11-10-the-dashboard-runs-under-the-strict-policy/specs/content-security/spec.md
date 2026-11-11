## ADDED Requirements

### Requirement: A page runs under the enforced policy without a violation

Every page of an app that called `UseContentSecurityPolicy` SHALL run in a browser enforcing the
policy without a single `securitypolicyviolation`, whether it is loaded by its address or reached
by the runtime's navigation.

#### Scenario: Every page by its address

- **WHEN** the dashboard sample, which turned the policy on and names no source, is loaded page by page in Chromium
- **THEN** each page carries the enforced policy with `'strict-dynamic'` and no violation is raised

#### Scenario: Every page by navigation

- **WHEN** each page of the sample is reached by clicking its link from a page already running
- **THEN** no violation is raised

#### Scenario: A script without the nonce

- **WHEN** an inline script with no nonce is put into the shell
- **THEN** the fixture fails, naming `script-src-elem` and the page
