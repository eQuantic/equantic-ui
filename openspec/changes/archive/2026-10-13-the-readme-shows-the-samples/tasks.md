# Tasks

## 1. The pictures

- [x] 1.1 Take the captures from the three samples: the dashboard's pages in a headless Chrome, the gallery through `--Photon:ScreenshotPath`, the Wallet on an iPhone simulator and on the Android emulator
- [x] 1.2 `scripts/capture-readme.sh` takes each of them again by target, and `scripts/readme-captures.py` makes them into `assets/readme/`, refusing a capture that drew nothing
- [x] 1.3 Run every target of the script as it is committed, and `compose` over what they wrote
- [x] 1.4 The Android target replaces no Wallet already on the emulator, uninstalls the one it installs, wakes a screen that is off and puts back what it changes, run on the emulator each way

## 2. The guard

- [x] 2.1 `assets/readme/captures.json` names each picture's target and the files its screen is written in
- [x] 2.2 `ReadmeCapturesTests`: every picture shown is on record and in the folder, the README names a picture only as a Markdown image by its path in the repository, and a changed file fails naming the picture
- [x] 2.3 Each case fails when what it guards is taken away: a changed file, a picture off the record, a link, an HTML image, a full address, a path written another way, a blank capture

## 3. The package's readme

- [x] 3.1 `WritePackageReadme` in `Directory.Build.targets` writes a package's readme when it packs: the README with each picture at its address under `v<the package's version>`, by a task MSBuild compiles
- [x] 3.2 `ReadmeCapturesTests` runs the target and compares what it wrote with the README byte for byte, but for the addresses
- [x] 3.3 `scripts/check-package-readme.cs` reads the readme inside every package, and CI's `build-packages` job runs it over the packages it packed
- [x] 3.4 Pack three packages of different kinds and read the readme inside each

## 4. The page and the agreement

- [x] 4.1 `README.md` shows the samples
- [x] 4.2 The Workflow section of `CLAUDE.md` and `AGENTS.md` says when a picture is taken again
- [x] 4.3 One `docs/LEDGER.md` line citing the issue
- [x] 4.4 Archive the change with `openspec archive the-readme-shows-the-samples --yes`
