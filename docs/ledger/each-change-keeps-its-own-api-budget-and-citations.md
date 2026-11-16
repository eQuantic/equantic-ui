- **2026-11-16 · Each change keeps its own API file, budget delta and citations**: three shared
  places stopped making any two pull requests conflict (#1489). A change declares its public API in
  `PublicAPI.Unshipped.<branch>.txt` beside each project, which the analyzer reads beside the
  project's own file and `public-api.sh ship` folds, and the stable-version guard reads every one.
  The served runtime's budget is the base a release folds plus a delta per change, saying what its
  bytes bought, folded by `fold-runtime-budget.sh`. The audit's and the security review's 468 anchored
  citations dropped their line number: a fenced one is found by its quote, exactly once, and a prose
  one by its member. `check-release-folds.sh` runs both folds against stubs in CI. The unanchored
  citation counter also stopped hiding the citation after another: the audit holds 345 such, not 292.
