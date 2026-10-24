## Context

The runtime cases a text with the engine's own mappings, one code point at a time where .NET's simple
mappings and the culture's i's differ from them, and kept a list of the code points the engine cases and
.NET 10 did not over the ICU of the servers of its year (ICU 74, Unicode 15.1): `ƛ`, `ɤ` and the scripts
Unicode 16 cased. `ubuntu-latest` moved to Ubuntu 26.04, whose ICU cases them, so the servers no longer
agree with each other: Linux's newest and macOS 26 case them, Ubuntu 24.04 and Windows do not.

## Goals / Non-Goals

**Goals:** the runtime cases as .NET 10 does over an ICU of Unicode 16; the suite proves it on every host
that has one, and says so where it skips.

**Non-Goals:** one casing on every server, which would take the SDK pinning the server's ICU (.NET's
app-local ICU) and every app carrying it, a decision of its own.

## Decisions

- **The newer Unicode.** The Linux a new server runs is Ubuntu 26.04, whose ICU is 78, Unicode 17, and the
  Mac a developer runs is macOS 26; the older ICUs are the ones leaving. Edgar's decision on #1032, which
  named Unicode 16: the ICU measured is a version further, and so is the engine.
- **The pairs are written out.** With the list of the code points to keep emptied, the 48 cases that read
  every code point pass on macOS 26, so the engine this repository ships already cases those letters as
  ICU 78 does. A browser whose Unicode is older does not, so the runtime writes their case pairs out
  (`NEWER_PAIRS`: two singles, six Latin pairs, Garay and Beria Erfe) and every engine answers as the
  servers do. The rest is the engine's, as before.
- **The suite follows the host's Unicode, not its operating system.** On a host whose ICU leaves Beria
  Erfe as it is, the cases that read every code point leave the newer letters out and compare the rest, so
  Windows and Ubuntu 24.04 still hold every other rule, and only the case of a letter a later Unicode cased
  is skipped. A host whose ICU moves past Unicode 17 fails them, which is when the runtime has to move.

How Flutter solves it: Dart's `toUpperCase` is the engine's, with no culture and the full mappings, and
a server has no say. Here the server renders the page first, so its .NET decides, and the runtime follows
the newest ICU a server runs.

## Risks / Trade-offs

- A page rendered on a server with an older ICU and hydrated in a browser can case `ƛ` and its kin two
  ways. → Stated in the spec and the wiki; the letters are Unicode 16's additions alone.
- The Windows job compares every code point but the newer letters. → It said nothing of them before
  either, and the Linux job and a Mac compare them.
