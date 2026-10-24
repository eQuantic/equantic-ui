## Why

Closes #1032, a bug under #849. `main` and every pull request are red on `test (ubuntu-latest)`, which
now names Ubuntu 26.04: its ICU is 78, Unicode 17, and .NET 10 there upper-cases `ƛ` to `Ꟍ`, where the
runtime kept the letters Unicode 16 and 17 cased as the servers of Ubuntu 24.04 and Windows keep them. The
suite compares the runtime with the .NET of the host it runs on, so 48 cases of
`CasingConformanceTests.EveryCodePoint_ChangesCaseAsAServersDotNetDoes` fail there.

## What Changes

For a developer using the SDK, and for the person whose page cases a text:

- `ToUpper` and `ToLower` and their invariant pair, of a string and of a char, change the case of the
  letters Unicode 16 and 17 cased (`ƛ` and `ɤ`, the capitals Unicode 16 gave them, its Latin pairs, and the
  Garay and Beria Erfe scripts) as .NET 10 does over ICU 78, Ubuntu 26.04's, and over macOS 26's ICU. A
  server whose ICU is older, Ubuntu 24.04's or Windows' own, still leaves them as they are, so a page
  rendered there and hydrated in a browser can disagree on those letters alone.
- The runtime's list of the code points to keep their case becomes the list of their case pairs, written
  out, so a browser whose own Unicode is older answers as such a server does too. Measured over every code
  unit of the BMP and every code point of the first supplementary plane, in eight cultures, the runtime
  agrees with .NET over macOS 26's ICU on every one.
- `CasingConformanceTests` compares every code point on a host whose ICU has those letters. On one whose
  ICU is older it leaves the letters out and compares every other one, and skips only the case of a letter
  a later Unicode cased, where it skipped every case that read every code point on a Mac.

The parts it reaches: the runtime (`utils/casing.ts`), and the conformance suite. Neither the public
surface nor the developer surface moves.

## Capabilities

### Modified Capabilities

- `transpiler-bcl`: a text changes case by Unicode 16.
