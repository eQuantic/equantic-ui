# Tasks

## 1. The work

- [x] 1.1 eqc writes each compile's work to EQC_TIMINGS: kept, walked, compiled, modules, bundled files and bytes
- [x] 1.2 `AppBuild` counts the files it walked and the modules it wrote; `ModuleBundler` counts the files and bytes bun wrote

## 2. The hold

- [x] 2.1 The budget's `work` holds the most any warm save did; a warm save that loaded cold fails
- [x] 2.2 The budget's `time` holds the fastest warm save; the medians are still reported
- [x] 2.3 A `dotnet watch` that exits before the app answers is started once more, its exit named with its output, its code and the load
- [x] 2.4 The budget's comment says what the hold is now and why

## 3. Proof

- [x] 3.1 The web sample measured here with the new budget: warm saves compile 1 of 159 files, write 1 module and 2 bundle files, and pass
- [x] 3.2 Every save recompiling every file fails on the count, 159 over 2
- [x] 3.3 The two queue runs' warm saves pass on their fastest (1074 and 1471ms under 1500)
- [x] 3.4 A dotnet watch killed during the start is started again and the run goes on
- [x] 3.5 `ASaveOfOneScreen_WritesThatScreensModule_AndSaysSo`; fails when every write is counted

## 4. Documentation

- [x] 4.1 One entry under `docs/ledger/`
