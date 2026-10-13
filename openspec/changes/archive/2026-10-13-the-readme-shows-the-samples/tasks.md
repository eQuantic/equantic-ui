# Tasks

## 1. The pictures

- [x] 1.1 Take the captures from the three samples: the dashboard's pages in a headless Chrome, the gallery through `--Photon:ScreenshotPath`, the Wallet on an iPhone simulator and on the Android emulator
- [x] 1.2 `scripts/capture-readme.sh` takes each of them again by target, and `scripts/readme-captures.py` makes them into `assets/readme/`, refusing a capture that drew nothing
- [x] 1.3 Run every target of the script as it is committed, and `compose` over what they wrote

## 2. The guard

- [x] 2.1 `assets/readme/captures.json` names each picture's target and the files its screen is written in
- [x] 2.2 `ReadmeCapturesTests`: every picture shown is on record and in the folder, a picture is a Markdown image named by its full address, and a changed file fails naming the picture
- [x] 2.3 Each case fails when what it guards is taken away: a changed file, a picture off the record, a relative path, an HTML image, a blank capture

## 3. The page and the agreement

- [x] 3.1 `README.md` shows the samples
- [x] 3.2 The Workflow section of `CLAUDE.md` and `AGENTS.md` says when a picture is taken again
- [x] 3.3 One `docs/LEDGER.md` line citing the issue
- [x] 3.4 Archive the change with `openspec archive the-readme-shows-the-samples --yes`
