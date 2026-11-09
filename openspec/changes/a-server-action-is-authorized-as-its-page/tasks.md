## 1. One authorization rule

- [x] 1.1 `RequestAuthorization`: the attributes as the middleware reads them, decided by the app's provider and evaluator
- [x] 1.2 `PageAuthorization` and `ServerActionAuthorizationService` on it; `AddUI` registers authorization
- [x] 1.3 A test per rule, each failing on main

## 2. The id

- [x] 2.1 The component's CLR full name on both sides; two methods under one id stop the app at start
- [x] 2.2 The pins, the shared fixture and the e2e test that named the short id

## 3. The endpoint

- [x] 3.1 JSON only, every body capped and the cap configurable, reasons kept in the log, the id logged on one line
- [x] 3.2 Malformed arguments 400, a nested `UploadedFile` refused, the declared result written
- [x] 3.3 A test per behavior, each failing on main

## 4. Documentation and the suites

- [x] 4.1 The wiki's Security page in every language it is written in
- [x] 4.2 A ledger entry under `docs/ledger/`
- [ ] 4.3 The suites, each read by its exit code
