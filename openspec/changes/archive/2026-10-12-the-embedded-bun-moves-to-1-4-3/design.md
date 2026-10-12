# Design

## Context

See proposal.md. bun is committed, six archives pinned by `bun-toolchain.json`, and `BunToolchainTests`
holds the digests on every system and the version on the host's own binary. Flutter has no part to
ask: this is which version of a build tool the SDK embeds, and which Unicode a table of its own
follows.

## Goals / Non-Goals

**Goals:**

- The six archives are 1.4.3, each the build it replaces, and provably the release's bytes.
- Every suite that runs bun passes in it: the runtime's own, the conformance suite, the Server's
  budget, and the samples in a browser.

**Non-Goals:**

- A byte comparison of a sample with main: bun writes other bytes at another version, so the proof
  is what the modules do.

## Decisions

**Like for like, read from the binaries.** Each committed binary names the release archive it came
from in its own upgrade text: `bun-darwin-x64`, `bun-darwin-aarch64`, `bun-linux-x64-baseline`,
`bun-linux-aarch64`, `bun-windows-x64-baseline` and `bun-windows-aarch64`. The same six are taken at
1.4.3.

**Repacked the same way, by a script that gives the same bytes again.** A package's archive holds one
file, named as the SDK extracts it (`bun-darwin`, `bun-linux`, `bun.exe`). The release's archive is
unpacked, the file renamed and zipped with the time the release gave it and no extra field, so the
digests in the pin are the ones anyone gets from the same release.

**The width table follows the oracle, but for what a font shows it wrong on.** The table is held to
`Bun.stringWidth` for every character that can begin a text element, so a new oracle is a new table.
What moved was read as a list first, old answer against new: format characters that now take no
cell, the Hangul vowels and finals, and what Unicode 16 and 17 made or added wide. The format
characters and the wide ones follow. Ranges were widened or merged only over characters that never
begin an element, and the three Indic letters the oracle counts as no cell stay named as its errors.

**A Hangul vowel or final that begins an element keeps its cell.** The oracle counts one as no cell.
That is its width after its consonant, where it is part of the consonant's element and the table is
never asked. One that begins an element was measured in a browser, in the dashboard sample's code
font on macOS: every one from U+1161 to U+11FF is drawn with an advance, 1 to 1.66 cells, the widths
the leading consonants have, and U+D7B0 to U+D7FB one cell. The block draws only a two-cell element
in a box, so counting none would draw the rest of the line more than a cell right of its caret. It
keeps the one cell it had, the test names the parting beside the Indic letters and holds it on both
sides, and what Hangul that is not a syllable should take, consonants in a row among it, is #860.
The vowel filler (U+1160) follows the oracle: nothing is drawn for it.

**The regeneration writes `${1}`.** The replacement text was `$1` followed by the digest, and a digest
that starts with a digit read as part of the group's number. It runs once an update, and the pin had
one commit before this one, the one that wrote it: this was its first.

## Risks / Trade-offs

- A shared chunk's name changes for every app, once: bun's hash is not the same across the two
  versions. The page's modules import it by the new name, so nothing is left pointing at the old one.
- The repository grows by the six archives, about 223 MB against the 187 MB they replace.
