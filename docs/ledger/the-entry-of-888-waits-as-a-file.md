- **2026-10-13 · The entry of #888 waits as a file, and main's chronology is what the last fold
  left**: #891 and #893 merged 39 seconds apart, #891 first, and the branch of #893 had not taken
  main in after it. Main held the entry of #888 in the chronology while `docs/ledger/folded.json`
  recorded the chronology without it, and `LedgerTests` failed on main: 189 entries where the last
  fold left 188. The entry left the chronology for a file under `docs/ledger/`, which put the
  chronology back to the record byte for byte, as the rule asks of an event
  ([#894](https://github.com/eQuantic/equantic-ui/issues/894)).
