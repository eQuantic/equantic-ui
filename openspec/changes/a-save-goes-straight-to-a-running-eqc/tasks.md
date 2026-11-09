# Tasks

## 1. eqc serves

- [x] 1.1 `eqc @<file> --serve` reads `compile` on its standard input and answers each in one `@eqc` line, loading the project's compilation again, and ends with its input. `--watch` gives way. Verify: the arguments tests read `--serve`, and the arguments after a response file follow its own
- [x] 1.2 Load the compiler once before the first request

## 2. The server keeps it running

- [x] 2.1 Ask MSBuild once for a run how to start eqc, while the app starts, and keep it running. Verify: two compiles go to one process, and MSBuild is asked once
- [x] 2.2 Own the process: a compile past its limit and a stop kill it, a stop while MSBuild is asked starts none, and the next save starts it again after it died. Verify: `RunningCompilerTests`
- [x] 2.3 Start eqc without dotnet watch's variables. Verify: the started process carries none of them

## 3. The proof

- [ ] 3.1 The measurement on the dashboard sample: the "msbuild" part gone, and eqc's part without a process start
- [ ] 3.2 A broken edit shows in the overlay, and the fix after it reaches the page with no restart, proved in a browser
- [ ] 3.3 Lower the web's ceiling by what was measured, once #1261 has set it from CI

## 4. Documentation

- [ ] 4.1 One entry for the ledger citing #1276, as a file of its own under `docs/ledger/`
- [ ] 4.2 The wiki's hot reload pages in five languages
