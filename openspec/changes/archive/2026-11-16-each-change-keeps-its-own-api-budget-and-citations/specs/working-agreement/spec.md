## ADDED Requirements

### Requirement: A change declares its public API in a file of its own

A change SHALL declare the public members it adds and the ones it retires in
`PublicAPI.Unshipped.<branch>.txt` beside each project it touches, named by the whole branch with its
`/` written as `-`. The build SHALL read every `PublicAPI.Unshipped.*.txt` beside the project's own
`PublicAPI.Unshipped.txt`. `scripts/public-api.sh update` SHALL write into that file, declare an entry
once across every file, take an entry RS0017 names out of the file that declares it, remove a change's
file left with nothing but its header, and refuse on `main` or a detached HEAD unless `EQ_API_CHANGE`
names the change. `scripts/public-api.sh ship` SHALL fold every Unshipped file into `Shipped` and remove
the changes' files.

#### Scenario: Two changes each add a public member

- **WHEN** two pull requests each add a public member to the same project and land one after the other
- **THEN** neither conflicts with the other, since each declared its member in its own file

#### Scenario: update on main

- **WHEN** `update` runs on `main` with no `EQ_API_CHANGE`
- **THEN** it refuses and names `EQ_API_CHANGE`, writing nothing

#### Scenario: A release folds every file

- **WHEN** `ship` runs with entries in the project's own file and in two changes' files, one of them a
  `*REMOVED*` line
- **THEN** `Shipped` holds every addition and not the retired entry, the project's own file holds
  nothing but its header, and the changes' files are gone

### Requirement: A citation names the file and the code, never the line

A fenced citation in the repository's documents SHALL be written `File.cs  code` and SHALL be found in
the file it names exactly once: its first forty characters once, or, when those occur more than once,
the whole quote once. A prose citation that names a member SHALL be written `File.cs Member`, and the
file SHALL declare the member. A line number beside a quote or a member SHALL be refused.

#### Scenario: A line moves in a cited file

- **WHEN** a pull request adds lines above code the audit quotes
- **THEN** the audit does not change, and the guard finds the quote where the code now stands

#### Scenario: A quote occurs twice

- **WHEN** a quote's text occurs twice in the file it names
- **THEN** the guard fails, naming the citation and the count

#### Scenario: A number comes back

- **WHEN** a fenced citation is written `File.cs:12  code`
- **THEN** the guard fails, asking for the number to be dropped

### Requirement: The served runtime's budget is a base and a delta per change

The served runtime's budget SHALL be the base the last release folded, in
`Budgets/served-runtime.json`, plus a delta per change in `Budgets/served-runtime/<branch>.json`, each
holding `deltaBytes` and what they `bought`. Growth past 1% of that sum and a shrink past 5% of it
SHALL fail. `EQ_UPDATE_RUNTIME_BUDGET=1` SHALL write the change's delta, measured against the base and
every other change's delta, and SHALL refuse without `EQ_RUNTIME_BOUGHT`. A delta that says nothing it
bought, or records no bytes, SHALL fail. `scripts/fold-runtime-budget.sh` SHALL fold every delta into
the base and remove the deltas, and SHALL write nothing when one file is not a delta.

#### Scenario: Two changes each cross the ceiling

- **WHEN** two pull requests each take the runtime past 1% and each record its delta
- **THEN** neither conflicts with the other, and the budget after both lands is the base and both deltas

#### Scenario: A shrink with no negative delta

- **WHEN** a change takes the runtime more than 5% under the base and every delta
- **THEN** the budget test fails until the change records a negative delta

#### Scenario: A release folds the deltas

- **WHEN** the fold runs over a base of 1,000 bytes and deltas of +30 and -10
- **THEN** the base is 1,020 and the deltas are gone
