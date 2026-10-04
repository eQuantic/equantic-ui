# Tasks

## 1. A run that links is a link

- [x] 1.1 Resolve a run's destination through the language resolver and mark an app-internal one for prefetch, on both producers, with one rule for what warms. Verify: the parity case `links-in-portuguese` lowers a Link and a run under `pt-BR` on both sides and compares them, and fails against main on each side

## 2. Documentation and the suites

- [x] 2.1 The wiki's Localization page (EN + pt-BR) on a branch named like this one. Verify: the wiki guards pass with `EQ_WIKI_DIR` on it
- [x] 2.2 The suites, each alone and read by its exit code. Verify: Web and the runtime's `TestRuntime` exit 0
