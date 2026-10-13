# Proposal

Closes #880, a Task under #288 (The roadmap ahead).

## Why

The root README says what the SDK does and shows none of it: no picture of the dashboard in a
browser, of the component gallery in a Photon window, or of the Wallet on a phone. And a picture in a
README goes stale in silence. The screen it shows changes, every build stays green, and the page goes
on showing the screen as it was.

That README is read in two places, and each draws one form of a picture. GitHub draws a path in the
repository on every branch, and a full address only on the branch the address names. nuget.org,
where the README is every package's readme, draws nothing from a path and shows HTML as text.

## What Changes

- `README.md` shows the three samples: the dashboard's first page and three more of its screens, the
  gallery in dark and in light, and the Wallet on an iPhone and on Android. Each picture is a still
  PNG, written as a Markdown image named by its path in the repository.
- A package's readme is written when it packs: the README, with each picture at its full address
  under the tag of the package's own release. `scripts/check-package-readme.cs` reads it inside
  every package CI packs.
- The pictures are captures. `scripts/capture-readme.sh` takes them from the samples as they build: a
  headless Chrome for the web, `--Photon:ScreenshotPath` for the desktop, a simulator of its own for
  iOS, the running emulator for Android. `scripts/readme-captures.py` makes them into
  `assets/readme/` and refuses a capture that drew nothing.
- `assets/readme/captures.json` names the files each picture's screen is written in, with the digest
  each had when the picture was taken. `ReadmeCapturesTests` fails when one changes, naming the
  picture and its target, and `EQ_UPDATE_README_CAPTURES=1` records the files as they are, after the
  picture was taken again.
- The Workflow section of `CLAUDE.md` and `AGENTS.md` says so.

For a developer who uses the SDK: nothing changes but the page they read, on GitHub and on nuget.org.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `working-agreement`: a picture in the README is a still capture on record, taken again when its
  screen changes, named by its path in the README and by its address in a package's readme.
