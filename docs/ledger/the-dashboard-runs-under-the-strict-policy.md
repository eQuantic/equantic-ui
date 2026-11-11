- **2026-11-10 · The strict policy is proven in a real browser**: the five slices of #306 before it
  were each held by a unit test reading what the server wrote, and none asked a browser enforcing
  the policy. The dashboard sample turns `UseContentSecurityPolicy()` on, and a Playwright fixture
  loads every page it links to, by address and by navigation, failing on any
  `securitypolicyviolation` with its directive and source. Its A/B, an inline script without the
  nonce in the shell, failed it on every route
  ([#1270](https://github.com/eQuantic/equantic-ui/issues/1270)). Proposed and archived through
  OpenSpec (`openspec/specs/content-security`).
