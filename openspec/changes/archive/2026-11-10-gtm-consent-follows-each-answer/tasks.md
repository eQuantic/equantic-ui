# Tasks

## 1. Consent

- [x] 1.1 `ConsentCookie.SecureName` and `Resolve` read the secure cookie over HTTPS, the plain one only over HTTP. Verify: `SsrConsentTests`, a planted plain cookie over HTTPS is unanswered
- [x] 1.2 `WebConsent` keeps and reads `__Host-eq-consent` on an HTTPS page. Verify: `consent.spec.ts`
- [x] 1.3 The installer updates the signals on every answer, grants only `ConsentGrants`, and writes names as JSON. Verify: `GtmTests`
- [x] 1.4 The owner's defaults: consent by default with `WithoutConsent()` to opt out, and `analytics_storage` alone granted unless the app names more. Verify, both ways: with the old defaults the four default cases fail; with `WithoutConsent` and `WithConsentGrants` inert their two cases fail

## 2. Proof and documentation

- [x] 2.1 Each new case fails on the code as it was: the denial and the named signals, a plain cookie over HTTPS, and a name written by hand (3 of the 28 GTM and consent cases)
- [ ] 2.2 The Server, Web and runtime suites pass whole
- [x] 2.3 The wiki's Analytics page in all five languages, on the wiki branch named like this one
- [x] 2.4 One ledger entry citing #1294, as a file of its own under `docs/ledger/`
