# Proposal

Closes #1270, a Task under #306, slice 6 of 6, the last. It builds on #1266 (the nonce), #1267
(`UseContentSecurityPolicy`), #1268 (the styles) and #1269 (EQ2115).

## Why

The five slices before this one are each held by a unit test that reads what the server wrote: a
nonce on a tag, a directive in a header, a refusal in a build. None of them asks the one party whose
answer counts, a browser enforcing the policy. A script the runtime injects without the nonce, a
style a component creates on its own, a page whose module loads something the policy does not
admit: each passes every unit test and is refused the moment a visitor opens the page.

## What Changes

- The dashboard sample turns the strict policy on with `UseContentSecurityPolicy()`, and names no
  source of its own: it is the app a developer would write.
- A Playwright fixture, `tests/e2e/tests/content-security.spec.ts`, loads every page the sample's
  home page links to under the enforced policy, then reaches each of them again by client
  navigation, and fails on any `securitypolicyviolation` event, naming the page, the directive and
  what it refused. It also asserts the policy is the enforced one with `'strict-dynamic'`, so a
  report-only header cannot pass it.
- The wiki's Security page, in five languages, says what the developer writes and what the SDK
  derives, and never gives a policy string to copy.

Nothing an app writes changes. Neither the public surface nor the developer surface moves.

## Capabilities

### New Capabilities

(none)

### Modified Capabilities

- `content-security`: every page of an app that turned the policy on runs under it in a browser
  without a violation.

## Impact

`samples/DefaultUIDashboard/Program.cs`, `tests/e2e` (the fixture and its README), and the wiki's
Security page in five languages.
