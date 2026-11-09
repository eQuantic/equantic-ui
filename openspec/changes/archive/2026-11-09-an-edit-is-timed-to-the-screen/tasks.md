# Tasks

## 1. Measured by hand first

- [x] 1.1 Read the path a save takes on the web and on the desktop. Verify: on the web a 400ms debounce, a new `dotnet msbuild -t:CompileEQuanticUI` per save (eqc, then bun), an SSE `reload`, and a client mount; on the desktop a metadata update `dotnet watch` applies in process and a wake of the host
- [x] 1.2 Time the web rebuild alone, six saves in a row on this machine. Verify: 11 to 48 seconds at a load average of 22, so the absolute number has to come from a clean machine

## 2. The instrument

- [x] 2.1 Time an edit from the save to the screen on both samples, each part on its own. Verify: `scripts/measure-edit-to-view.cs web` and `desktop` report a cold run and a warm distribution, and their parts add up to the total
- [x] 2.2 Run it last in CI's samples job, with the budget. Verify: a budget below the desktop's median fails the run, naming the part

## 3. The desktop's restart

- [x] 3.1 Keep the startup hook on for the Debug build of a trimmed desktop head that is not a publish (#1258). Verify: the evaluation test, and the desktop sample applying an edit in place under `dotnet watch`
- [x] 3.2 Keep it off the phones, whose Debug build links. Verify: the WalletMobile iOS head builds

## 4. The numbers

- [x] 4.1 The first clean run on CI. Verify: the desktop's warm median is 329ms (257 to 416ms, cold 2.7s), inside the bar; the web's is 22.9s (22.9 to 39.2s, cold 34.8s), with two documents per save
- [x] 4.2 Hold the desktop to the bar and the web to a ceiling of 45s that only comes down, and file the web's slices (#1274 to #1281) with the numbers
- [x] 4.3 `ROADMAP.md` says the measured numbers, and a ledger entry cites #900 and #1258
