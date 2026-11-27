# Proposal

Closes #1198, a Bug under #1174.

## Why

`scripts/capture-readme.sh ios` builds the Wallet, boots a simulator, installs and launches it, and then
fails naming nothing. Measured on a Mac with the repository on an external volume, two causes, each sent
to `/dev/null`:

- `xcrun simctl io <udid> screenshot` cannot write under `/Volumes/Data`, where the raw captures go. Its
  error was hidden, so the target ended with no line at all, or with "no capture there" after a minute
  of retries.
- With no Pillow in `python3`, the check exits saying so, but the loop that waits for the first frame
  ran it with `2> /dev/null` and retried for a minute, as if the app had drawn nothing.

## What Changes

- **Pillow is asked for before anything is built.** `readme-captures.py ready` asks only whether the
  `python3` can read a capture, and `capture-readme.sh` asks it before the first target.
- **Only a picture of nothing is waited out.** The check exits with `NOTHING_DRAWN` (3) for a picture
  of nothing, and with 1 for every other refusal. The loop takes the picture again only for the first,
  and fails at once with the check's own reason for the second.
- **A screenshot that fails says why.** The simulator writes the screenshot into a folder of the
  user's temporary directory, where it may write, and the picture is moved into the raw captures from
  there. A screenshot that fails anyway fails the target with the simulator's own words, and an
  emulator's with its own.
- **A check proves it.** `scripts/check-capture-readme.sh` sources the script with stubs of `xcrun`,
  `python3`, `dotnet`, `uname` and `sleep`, and CI's `publish-packages` job runs it.

Nothing a developer using the SDK writes moves: the script is the repository's own tooling. No public or
developer surface moves.

## Impact

- `scripts/capture-readme.sh`, `scripts/readme-captures.py`, the new `scripts/check-capture-readme.sh`.
- `.github/workflows/ci.yml`: a step in the `publish-packages` job.
- `openspec/specs/working-agreement`: the requirement that a README picture is a capture.
