## MODIFIED Requirements

### Requirement: The served runtime's budget is a base and a delta per change

The served runtime's budget SHALL be the base the last release folded, in `Budgets/served-runtime.json`, plus a delta
per change in `Budgets/served-runtime/<branch>.json`, each holding `deltaBytes` and what they `bought`. A change SHALL
be measured against the commit it is measured from: `EQ_RUNTIME_BASE`, its fork from `origin/main` on a named branch,
or the checkout's first parent, whose runtime SHALL be built as the Server's `BundleRuntime` builds its own. A change
that grows the runtime by any byte SHALL record a delta of its own, and SHALL fail without one, and the delta it
records SHALL be what it grew, within 1% of the budget. Growth past 1% of the base and every delta and a shrink past
5% of it SHALL fail, the 1% being the margin for measurement noise. `EQ_UPDATE_RUNTIME_BUDGET=1` SHALL write the bytes
the change grew the runtime by against the commit it is measured from, and SHALL refuse without `EQ_RUNTIME_BOUGHT`. A
delta that says nothing it bought, or records no bytes, SHALL fail. `scripts/fold-runtime-budget.sh` SHALL fold every
delta into the base, write each as a row of `docs/SERVED-RUNTIME-GROWTH.md` under the release's version with the pull
request whose commit added it, and remove the deltas, and SHALL write nothing when one file is not a delta.

#### Scenario: Two changes each cross the ceiling

- **WHEN** two pull requests each take the runtime past 1% and each record its delta
- **THEN** neither conflicts with the other, and the budget after both lands is the base and both deltas

#### Scenario: A change grows the runtime under the margin

- **WHEN** a change grows the runtime by 63 bytes, under 1% of the budget, and records no delta
- **THEN** the budget test fails, naming the 63 bytes, and passes once the change records its 63 bytes

#### Scenario: A change grows nothing while main did

- **WHEN** a change touches no source of the runtime, and `main` stands over the base and its deltas
- **THEN** the change owes no delta

#### Scenario: A shrink with no negative delta

- **WHEN** a change takes the runtime more than 5% under the base and every delta
- **THEN** the budget test fails until the change records a negative delta

#### Scenario: A release folds the deltas

- **WHEN** the fold runs over a base of 1,000 bytes and deltas of +30, added by #101, and -10, added by #102
- **THEN** the base is 1,020, the growth page holds a row for each with its pull request under the release's version,
  and the deltas are gone
