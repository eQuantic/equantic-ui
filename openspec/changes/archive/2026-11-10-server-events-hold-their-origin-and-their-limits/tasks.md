# Tasks

## 1. Server

- [x] 1.1 The stream, the bind and the release ask `AllowedRequestOrigins`. Verify: a bind, a release and a stream from another site are refused, and a bind from an allowed origin binds
- [x] 1.2 `MaxConnectionLifetime` ends a stream, and `MaxConnections` answers 503 past the instance's cap; both validated at start. Verify: the stream ends and its connection holds nothing, the second stream past a cap of one is 503, and a zero for either stops the app
- [x] 1.3 The endpoints are one group and `ConfigureEndpoints` reaches all three. Verify: a convention's metadata is on each of them
- [x] 1.4 A backplane envelope is written only when a frame can carry it, the publisher sharing the check. Verify: six envelopes no publisher could send reach no page, and the next one arrives
- [x] 1.5 Each new case fails with its fix turned off (10 of 12; the two left are the allowed origin and the conventions, which hold either way)

## 2. Proof and documentation

- [ ] 2.1 The Server, Web, Compiler and Conformance suites pass whole, and the runtime's
- [x] 2.2 The wiki's ServerEvents page in all five languages, on the wiki branch named like this one
- [x] 2.3 One ledger entry citing #1292, as a file of its own under `docs/ledger/`
