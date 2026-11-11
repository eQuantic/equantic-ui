# Tasks

## 1. The declaration

- [x] 1.1 `ContentSecurityPolicyAttribute` in the server, and `ContentSecurityPolicyGenerator` writing it from `UseContentSecurityPolicy`. Verify: `ContentSecurityPolicyGeneratorTests`, the attribute bound to the server's type, and nothing for an app that does not call it or calls a method of the same name

## 2. The refusal

- [x] 2.1 `InlineHandlers` in eqc, run by `AppBuild` over the app's compilation and its referenced projects'. Verify: `InlineHandlerUnderAPolicyTests` through the real `AppBuild`, six refusals in the app and six in a library, none for a data, an aria or a role attribute, none without the policy
- [x] 2.2 The A/B. Verify: with the check returning nothing, both refusal cases fail

## 3. Documentation

- [x] 3.1 EQ2115 in `docs/DIAGNOSTICS.md` and on the wiki's Diagnostics page, in five languages
- [x] 3.2 One entry for the ledger citing #1269, as a file of its own under `docs/ledger/`
