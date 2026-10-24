# Tasks

## 1. The runtime

- [x] 1.1 The list of the code points to keep their case becomes their case pairs, written out
- [x] 1.2 The runtime's own spec cases them as ICU 78 does, on an engine that leaves them as they are too
- [x] 1.3 Check: the cases that read every code point, in eight cultures, pass on macOS 26, and fail on
      main's runtime there, 48 of them, as on the Linux job; the pull request's Linux job runs them

## 2. The suite

- [x] 2.1 `CasingConformanceTests` leaves the newer letters out on a host whose ICU is older, and skips only
      the case of a letter a later Unicode cased there
- [x] 2.2 Check: the whole conformance suite, and the CI's jobs on Linux and Windows

## 3. Documentation

- [x] 3.1 The wiki's SupportedFeatures page in every language it is written in
- [x] 3.2 One ledger entry, a file under docs/ledger/, citing #1032
- [x] 3.3 Archive this change before the merge
