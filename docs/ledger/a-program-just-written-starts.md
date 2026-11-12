- **2026-11-03 · A program a test just wrote starts once nothing holds it open for writing**: a bug under
  #1174 ([#1206](https://github.com/eQuantic/equantic-ui/issues/1206)). Linux refuses to start a program that
  is open for writing (ETXTBSY, "Text file busy"), and a process another test forks in that moment holds
  the program a test just wrote until it execs: `ProgramArchiveTests` failed on PR #1194's CI on that race.
  `ChildProcess`, the one way a test starts a process, tries the start again on that error alone, for ten
  seconds at most. Proved both ways on GitHub's Linux runner (run 41626049947), where macOS answers no
  such error: with the retry, `ChildProcessTests.AProgramHeldOpenForWriting_StartsOnceItIsLetGo` passed in
  314 ms, and without it the start failed with "Text file busy".
