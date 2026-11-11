# Tasks

## 1. The registration crosses

- [x] 1.1 `ServiceRegistrations` reads the app's and its referenced projects' registrations; `BrowserServices` writes `$eq.services.provide(…)` at every browser resolution and builds the implementation as the container does
- [x] 1.2 The runtime's `provideService` registers once by lifetime, and what is registered already wins
- [x] 1.3 EQ2116 for a required service the browser cannot build, renumbered from EQ2115, which main's Content-Security-Policy took (#1269)
- [x] 1.4 Verify: `AnAppsOwnServiceTests` over a solution on disk, and the Compiler and Conformance suites

## 2. Documentation

- [x] 2.1 `docs/DIAGNOSTICS.md`, and the message `CapabilityScope.Require` throws names `builder.Services.AddSingleton<…>()`
- [x] 2.2 The wiki's ServerIntegration and Diagnostics pages, in five languages, on the wiki branch of this pull request
- [x] 2.3 One entry for the ledger citing #1335, as a file of its own under `docs/ledger/`
