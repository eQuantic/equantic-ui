# Proposal

Closes #880, a Task under #288 (The roadmap ahead).

## Why

The root README says what the SDK does and shows none of it: no picture of the dashboard in a
browser, of the component gallery in a Photon window, or of the Wallet on a phone. And a picture in a
README goes stale in silence. The screen it shows changes, every build stays green, and the page goes
on showing the screen as it was.

That README is also the readme of every package on nuget.org, which draws nothing from a relative
path and shows HTML as text.

## What Changes

- `README.md` shows the three samples: a tour of the dashboard's pages and three of its screens, the
  gallery in dark and in light, and the Wallet on an iPhone and on Android. Each picture is a
  Markdown image named by its full address, which nuget.org draws too.
- The pictures are captures. `scripts/capture-readme.sh` takes them from the samples as they build: a
  headless Chrome for the web, `--Photon:ScreenshotPath` for the desktop, a simulator of its own for
  iOS, the running emulator for Android. `scripts/readme-captures.py` makes them into
  `assets/readme/` and refuses a capture that drew nothing.
- `assets/readme/captures.json` names the files each picture's screen is written in, with the digest
  each had when the picture was taken. `ReadmeCapturesTests` fails when one changes, naming the
  picture and its target, and `EQ_UPDATE_README_CAPTURES=1` records the files as they are, after the
  picture was taken again.
- The Workflow section of `CLAUDE.md` and `AGENTS.md` says so.

For a developer who uses the SDK: nothing changes but the page they read.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `working-agreement`: a picture in the README is a capture on record, taken again when its screen
  changes.
