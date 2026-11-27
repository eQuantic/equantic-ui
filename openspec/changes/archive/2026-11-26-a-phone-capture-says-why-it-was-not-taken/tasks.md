# Tasks

## 1. The script

- [x] 1.1 `readme-captures.py ready`, asked before the first target; a picture of nothing exits with `NOTHING_DRAWN`
- [x] 1.2 `when_drawn` waits out only a picture of nothing, and fails at once with any other refusal
- [x] 1.3 The simulator's screenshot is written into the temporary directory and moved, and a refused one fails with its own words; the emulator's says so too

## 2. Proof

- [x] 2.1 `scripts/check-capture-readme.sh`: no Pillow, a refused screenshot, a refusing check, a screenshot moved from the temporary directory, a picture of nothing waited out, and one `NOTHING_DRAWN` in both files
- [x] 2.2 The check fails on main's script, sourced the same way, in every case but the picture of nothing, which both wait out
- [x] 2.3 CI's `publish-packages` job runs it

## 3. Documentation

- [x] 3.1 The ledger entry, under `docs/ledger/`
