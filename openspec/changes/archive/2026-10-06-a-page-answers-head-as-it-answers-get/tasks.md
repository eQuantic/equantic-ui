# Tasks

## 1. HEAD reaches the page and the module

- [x] 1.1 Map the page routes (`[Page]`, its culture twin, `MapPage<T>`) and the modules under `/_equantic/` for GET and HEAD with the GET's handler; proved by `HeadRequestTests` on Kestrel, whose four HEAD cases fail against main's routes
- [x] 1.2 Measure that the test host does not leave a HEAD's body out (it answers 200 with the whole body), so the proof runs on Kestrel, the server an app ships on

## 2. Proof and documentation

- [x] 2.1 Run the server and web suites, and build `samples/DefaultUIDashboard` with no warning
- [x] 2.2 The wiki's ServerIntegration page, in English and Portuguese, on a wiki branch named like this pull request's
- [x] 2.3 One `docs/LEDGER.md` line citing #575
