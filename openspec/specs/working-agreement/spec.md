# working-agreement Specification

## Purpose
How a change reaches `main` in this repository, and what every working session starts from: the
written rules, the checks that fail when they are broken, and the environment a cloud session is
given before its first command.

## Requirements

### Requirement: One written working agreement for every agent

The repository SHALL carry its working agreement as a `## Workflow` section that is the same text in
`CLAUDE.md` and in `AGENTS.md`, and the test suite SHALL fail when the two copies differ or when
either file lacks the section.

#### Scenario: One copy is edited alone

- **WHEN** a commit changes the Workflow section of `CLAUDE.md` and leaves `AGENTS.md` as it was
- **THEN** the test that compares the two sections fails and names the first line that differs

#### Scenario: A file loses the section

- **WHEN** either file no longer has a `## Workflow` heading
- **THEN** the same test fails, saying which file has no section to compare

### Requirement: No attribution is written by the tooling

The project's Claude Code settings SHALL disable every attribution Claude Code adds by default: the
commit trailer, the pull request footer, and the session link that cloud and Remote Control
sessions append.

#### Scenario: A session commits and opens a pull request

- **WHEN** a session running with the project's settings creates a commit and a pull request
- **THEN** the commit message carries no co-authorship or session trailer and the pull request body
  carries no generated-with footer or session link

### Requirement: A cloud session starts with the owner's identity and no foreign signature

A session that starts in a cloud container SHALL be prepared, before its first command, to commit
as `Edgar Mesquita <edgar@equantic.tech>` with commit and tag signing turned off in the repository,
whatever identity and signing configuration the container itself provides.

#### Scenario: The container signs with a key that is not the owner's

- **WHEN** the container's global git configuration names another identity and requires signing
  with a key that does not exist
- **THEN** a commit made before the session is prepared fails
- **AND** a commit made after it succeeds, unsigned, authored and committed by the owner

### Requirement: A cloud session has the pinned toolchain

A session that starts in a cloud container SHALL have on PATH the .NET SDK version that
`global.json` pins and the OpenSpec CLI version that its lockfile pins, with OpenSpec telemetry off.
Every archive the preparation downloads SHALL be verified against a pinned SHA-256 and refused on a
mismatch, leaving nothing installed.

#### Scenario: A container with no .NET SDK

- **WHEN** a session starts in a container that has no `dotnet` on PATH
- **THEN** `dotnet --version` answers the version `global.json` pins
- **AND** `openspec --version` answers the pinned CLI version and `OPENSPEC_TELEMETRY` is `0`

#### Scenario: The archive is not the one pinned

- **WHEN** the downloaded SDK archive's SHA-256 differs from the pinned digest
- **THEN** the preparation reports the mismatch and installs nothing

### Requirement: A preparation that falls short says so to the person

When a tool the working agreement makes mandatory cannot be prepared (the OpenSpec CLI, and in a
cloud container the .NET SDK or signing turned off), the preparation SHALL warn the person in the
session, not only report it to the agent, and SHALL NOT block the session.

#### Scenario: The pinned SDK is refused

- **WHEN** the SDK archive's SHA-256 differs from the pinned digest
- **THEN** the session starts, and the person in it sees a warning naming the .NET SDK

### Requirement: A local session is left as its owner set it up

A session on a developer's own machine SHALL NOT change the git identity, the signing
configuration, the installed SDKs or Docker; it SHALL only make the pinned OpenSpec CLI available.

#### Scenario: A session starts on a laptop

- **WHEN** a session starts outside a cloud container
- **THEN** the preparation reports that the identity, the SDKs and Docker were left as they are
- **AND** the pinned OpenSpec CLI is on the session's PATH

### Requirement: Specs validate strictly, and their absence is a failure

CI SHALL validate every OpenSpec change and spec with the pinned CLI in strict mode, and SHALL fail
when a change or a spec is invalid and also when there is nothing to validate at all.

#### Scenario: A spec breaks the format

- **WHEN** a pull request adds a requirement without a scenario
- **THEN** the OpenSpec job fails and names the invalid item

#### Scenario: The specs are gone

- **WHEN** `openspec/` holds no change and no spec
- **THEN** the OpenSpec job fails instead of reporting that nothing was found

### Requirement: A pull request's docs guards read its own wiki pages

CI SHALL check out the wiki for the documentation guards at the branch named exactly like the pull
request's head branch when the wiki has one and the pull request comes from this repository, and at
the wiki's default branch otherwise. The branch name SHALL be read as data and never interpolated
into a command. When the wiki's branches cannot be
listed, the checkout SHALL fail and leave nothing behind, rather than read the default branch.

#### Scenario: A pull request that carries its wiki pages

- **WHEN** the wiki has a branch named like the pull request's head branch
- **THEN** the guards read that branch's pages

#### Scenario: A run with no wiki branch of its own

- **WHEN** the wiki has no branch of that name, or the run is not a pull request
- **THEN** the guards read the wiki's default branch

#### Scenario: A pull request from a fork

- **WHEN** a fork's pull request has a head branch named like one of the wiki's branches
- **THEN** the guards read the wiki's default branch, since that wiki branch belongs to another
  pull request

#### Scenario: A name that is only the tail of a branch

- **WHEN** the head branch is `some-change`, and the wiki has `fix/some-change` and no `some-change`
- **THEN** the guards read the wiki's default branch

#### Scenario: The wiki's branches cannot be listed

- **WHEN** listing the wiki's branches fails, though cloning it would work
- **THEN** the checkout fails and leaves no directory behind

#### Scenario: A branch name written to run

- **WHEN** the head branch's name holds `$(touch pwned)` and a backquoted command
- **THEN** nothing it names runs

### Requirement: Every docs guard reads one wiki, which a local run can name

Every test that reads the wiki SHALL find it through one locator: the directory `EQ_WIKI_DIR` names
when the variable is set, and `equantic-ui.wiki` beside the repository otherwise. An `EQ_WIKI_DIR`
that is set and empty, that names no directory, or that names a directory that is not a checkout of
the wiki, SHALL fail every guard rather than let it skip, and so SHALL a directory beside the
repository that is not a checkout of the wiki. A failing guard SHALL name the wiki it read: the
directory, and the branch and commit checked out there.

#### Scenario: A local run against a pull request's wiki branch

- **WHEN** `EQ_WIKI_DIR` names a worktree of a pull request's wiki branch
- **THEN** every guard reads that worktree, and the clone beside the repository is left as it is

#### Scenario: A typo in the variable

- **WHEN** `EQ_WIKI_DIR` is set and empty, names a directory that does not exist, or names one
  without the wiki's `Home.md`
- **THEN** every guard fails, saying what the variable names

#### Scenario: A stale directory beside the repository

- **WHEN** `EQ_WIKI_DIR` is unset and the directory beside the repository has no `Home.md`
- **THEN** every guard fails instead of reading no page

#### Scenario: A guard fails

- **WHEN** a guard finds a row the docs do not have
- **THEN** its message names the directory it read, and the branch and commit checked out there

### Requirement: The wiki's master moves with main

A pull request that changes what the wiki must say SHALL carry its pages in every language the wiki
reads them in, English, Portuguese, Spanish, French and German, a new page in all five, in one commit,
on a wiki branch named like its own branch, and that branch SHALL be merged into the wiki's master
when the pull request merges, and not before.

#### Scenario: A pull request adds a diagnostic

- **WHEN** a pull request adds a diagnostic code and carries its wiki rows on its wiki branch
- **THEN** its own guards pass, and every other pull request's guards, which read master, still
  pass without the rows

#### Scenario: The pull request merges

- **WHEN** the pull request merges into main
- **THEN** its wiki branch is merged into the wiki's master and deleted

### Requirement: The Copilot loop stops on its own

A pull request's author SHALL review the whole diff before opening it, and SHALL then sort every
finding of a Copilot round, those of the review's body included, as a defect (the code the pull
request changes does the wrong thing, or a guard passes where it should fail), as hardening,
documentation or a nit, or as wrong. A defect SHALL be fixed and proved both ways. Hardening,
documentation or a nit SHALL be fixed in the same push or filed as an issue, and a wrong finding
SHALL be answered with what shows so. Every thread SHALL be answered and resolved, since the ruleset
refuses a merge while one is open. A new round SHALL be requested only after a round that found a
defect, once per push of fixes, and never after the third round.

#### Scenario: A round that finds only nits

- **WHEN** every finding of a round is hardening, documentation or a nit
- **THEN** each is fixed in the same push or filed as an issue, and no new round is requested

#### Scenario: A round that finds a defect

- **WHEN** the second round finds a defect in the code the pull request changes
- **THEN** it is fixed and proved both ways, and a third round is requested after the push that carries every fix

#### Scenario: A defect after the third round

- **WHEN** the third round finds a defect
- **THEN** it is fixed and proved both ways, and no fourth round is requested

#### Scenario: A finding that does not hold

- **WHEN** a finding claims a failure that a measurement disproves
- **THEN** it is answered with that measurement and resolved, and no round is requested

#### Scenario: A draft

- **WHEN** a pull request is opened as a draft
- **THEN** no round starts until it is ready for review, and the author requests one if none has started a few minutes after

#### Scenario: A push with nothing to review

- **WHEN** a push only merges main, or only edits the pull request's body
- **THEN** it starts no round, since the ruleset does not review on push

### Requirement: A pull request merges on its own CI, against the current main

The repository's ruleset SHALL require the CI's jobs (`test-runtime`, both `test` legs, `samples`, the
three `build-platform-runtimes`, `build-packages`, `commit-messages`, `openspec`, `session-start`,
`wiki-checkout` and `package-extension`) to pass on a pull request's head, and SHALL require that head
to be up to date with main, so a pull request merges only on a CI run that saw the main it lands on.
Every job in a dependency chain SHALL be required, since GitHub counts a job skipped for a failed
dependency as passed.

#### Scenario: main moved after the CI ran

- **WHEN** a pull request passed CI, and another pull request merged into main after it
- **THEN** GitHub refuses the merge until main is merged into the pull request and CI passes again

#### Scenario: A workflow that never ran

- **WHEN** a workflow fails before it creates a job
- **THEN** the required checks never report, and the pull request stays blocked

### Requirement: A commit message carries no tool's attribution

CI SHALL fail when a commit message of a pull request, or of a push to main, carries a tool's
attribution in a shape the guard names: a wrapper's tags, a generated-with or generated-by line
naming the tool, an assistant's co-authorship, the attribution comment a project's pull request
opens with, or a link into the tool's own pages, a session's or a conversation's. A named phrase
SHALL be refused wherever a line break falls in it. Prose that names the tool, and a balanced markup
snippet a message quotes, SHALL pass. The guard's self-test SHALL hold a fixture that isolates each
named shape, and CI SHALL run it on every push. The repository's squash default SHALL take a pull
request's title and its commits' messages, never its body, so the message a squash writes on main is
one the job already read on the pull request.

#### Scenario: A squash message that took a project's pull request body

- **WHEN** the squash message of a pull request opened from a project carries the body's attribution
  comment, its link to the conversation and its signature line
- **THEN** the `commit-messages` job fails on the push to main, naming each line

#### Scenario: A signature the squash composer wrapped

- **WHEN** a signature line's phrase is broken across two lines, with no link on either
- **THEN** the guard refuses it, reported as wrapped

#### Scenario: A pull request whose body carries a tool's attribution

- **WHEN** it is squash-merged with the repository's default
- **THEN** the commit on main carries its title and its commits' messages, and nothing of its body

#### Scenario: Prose that names the tool

- **WHEN** a commit message says the diff was reviewed in Claude Code, wrapped after the tool's first word, and quotes CLAUDE.md's first line
- **THEN** the guard passes it

### Requirement: A picture in the README is a capture, taken again when its screen changes

Every picture the root `README.md` shows of a sample SHALL be a capture on record in
`assets/readme/captures.json`, which names the files the pictured screen is written in and the digest
each had when the picture was taken. The test suite SHALL fail when one of those files changes,
naming the picture and the target of `scripts/capture-readme.sh` that takes it again, and SHALL pass
again only when the record holds the files as they are. A picture SHALL be still, since nuget.org
gives an animated image no control to stop it, and SHALL be written in the README as a Markdown
image named by its path in the repository, which GitHub draws on every branch and in a fork. A package's readme SHALL be that README with each picture at its full address under the tag of
the package's own release, written when the package packs, since nuget.org shows a readme's HTML as
text and draws nothing from a path.

#### Scenario: A pictured screen changes

- **WHEN** a file a picture's screen is written in changes
- **THEN** `ReadmeCapturesTests` fails, naming the picture, its target and the file

#### Scenario: The picture was taken again

- **WHEN** the picture is taken again and the files are recorded with `EQ_UPDATE_README_CAPTURES=1`
- **THEN** the test passes

#### Scenario: A picture nobody recorded

- **WHEN** `README.md` shows a picture that has no entry in the record, or the folder holds a file
  the README does not show
- **THEN** the test fails, naming it

#### Scenario: A picture named another way

- **WHEN** `README.md` names a file of `assets/readme` through an `<img>` tag, a link, a full address
  or a path written another way
- **THEN** the test fails, naming it, since GitHub or nuget.org would not draw it

#### Scenario: A picture that moves

- **WHEN** the folder holds a picture that moves, an animated GIF or a PNG with an animation chunk
- **THEN** the test fails, naming it

#### Scenario: A package packs

- **WHEN** a package packs
- **THEN** the readme inside it is `README.md` byte for byte, but for each picture's path, which is
  the picture's address under `v<the package's version>`

#### Scenario: A capture of nothing

- **WHEN** a capture holds almost no colour under its status bar
- **THEN** `scripts/capture-readme.sh` refuses it where it is taken

### Requirement: A docs guard reads every language a page is written in

A docs guard that holds a wiki page to the tree SHALL read every copy of the page the wiki has: the
English page at its root and each translation at `locale/<culture>/<Page>-<culture>.md`. The English
and the Portuguese page SHALL stay named as the contract, so that a rename that lost one fails rather
than leave the guard reading nothing. A guard that reads a language by its words SHALL fail on a
language it has no words for, rather than leave the page unread. An allowance to carry a retired
spelling SHALL name the English page and cover every translation of it, and SHALL be stale only when
no copy of the page carries the word.

#### Scenario: A translation tells the history its page is allowed to tell

- **WHEN** the German, Spanish and French copies of `PackageArchitecture` name `eQuantic.UI.Core`, which the English page is allowed to name
- **THEN** `WikiVocabularyTests` passes, where it failed every pull request reading the wiki's master

#### Scenario: A translation short of a row

- **WHEN** the wiki has a German Diagnostics page without a code `docs/DIAGNOSTICS.md` documents
- **THEN** `WikiDiagnosticsTests` fails, naming `de` and the code

#### Scenario: A language the guard cannot read

- **WHEN** the wiki has a French Capabilities page and the guard has no French word for an unavailable contract
- **THEN** `WikiShellRealizationsTests` fails, naming `fr` and the words it lacks

### Requirement: An event of the ledger is a file of its own until a release folds it

An event SHALL be recorded as a file of its own under `docs/ledger/`, holding the entry as the
chronology of `docs/LEDGER.md` will hold it: one list item that opens with its date and what
happened, with every further line a continuation of it, and that cites an issue or a pull request.
The test suite SHALL fail on a file there that is not such an entry, naming the file and what it
lacks. A release SHALL fold the files into the chronology, after its last entry, in order of date
and then of file name, and SHALL remove them. A fold SHALL write nothing when it refuses one entry.
`docs/ledger/folded.json` SHALL record the chronology as the last fold left it, and the test suite
SHALL fail when the chronology differs from the record, saying where an event is written.

#### Scenario: Two pull requests record an event each

- **WHEN** two branches cut from one commit each add a file under `docs/ledger/`
- **THEN** a merge of one into the other conflicts on nothing, on GitHub as on a developer's machine

#### Scenario: A file that is not one dated event

- **WHEN** a file under `docs/ledger/` opens with no date, holds two entries, holds a line that is
  no continuation of its entry, or cites no issue or pull request
- **THEN** `LedgerTests` fails, naming the file and what it lacks

#### Scenario: An entry written into the chronology by hand

- **WHEN** an entry is added to the chronology of `docs/LEDGER.md` and no fold wrote it
- **THEN** `LedgerTests` fails, saying an event is a file under `docs/ledger/`

#### Scenario: A release folds

- **WHEN** `./scripts/fold-ledger.sh` runs with files waiting
- **THEN** the chronology ends with their entries in order of date and then of name, every other
  line of `docs/LEDGER.md` is as it was, the files are gone, and the record holds the chronology as
  it is

#### Scenario: A fold with nothing waiting

- **WHEN** the fold runs with no file waiting
- **THEN** `docs/LEDGER.md` is byte for byte as it was

#### Scenario: A fold meets an entry it refuses

- **WHEN** one waiting file is not an entry, or the chronology already holds an entry's first line
- **THEN** the fold fails naming it, and `docs/LEDGER.md` and every waiting file are as they were

#### Scenario: A deliberate change to the chronology

- **WHEN** the chronology itself is changed on purpose and recorded with `EQ_UPDATE_LEDGER=1`
- **THEN** the test passes

### Requirement: A conformance case runs only C# the build accepts

The conformance harness SHALL read, for every case it transpiles, what the build refuses in the
case's C#, where the build reads it and by the build's own rule: the converter's diagnostics over
the text the case would run, an error refusing and a warning not, and the build's own verdict on the
declarations of the case's prelude, every check a build runs beside the converter included, which
SHALL owe nothing to a prelude asked before it. A case
whose C# the build refuses SHALL fail before either side runs, naming each refusal by its id and
text, unless the case declares the refusal it pins. A case that declares one SHALL pass only when its
C# compiles, the build refuses the case's own C# with that id, and every other refusal of the case or
of its prelude carries the same id, and SHALL run nothing on either side.
A path of the harness that compiles a source as an app's build does SHALL read the errors of every
module before it keeps the modules that wrote text.

#### Scenario: A construct the build refuses, measured as a translation

- **WHEN** a case compares `return (from a in new[] { 1, 2 } let f = a * 2 select f).Sum();` with
  .NET
- **THEN** it fails before anything runs, naming EQ2008 and its text, where it ran the query's
  source and answered 3 against .NET's 6

#### Scenario: A case written for a fence

- **WHEN** a case declares that the build refuses that query with EQ2008
- **THEN** it passes, and nothing runs on either side

#### Scenario: The fence lifts

- **WHEN** a case declares a refusal and the build accepts the case's own C#
- **THEN** it fails, saying the build accepts the case's own C# and showing the JavaScript the build
  writes

#### Scenario: A refusal the case does not name

- **WHEN** a case declares EQ2004 and the build refuses its C# with EQ2008
- **THEN** it fails, naming the EQ2008 refusal

#### Scenario: A refusal of the prelude alone

- **WHEN** a case declares EQ1007, its prelude declares a record with two methods of one name, and
  the build accepts the case's own C#
- **THEN** it fails, saying the build accepts the case's own C#, where the prelude's refusal used to
  stand in for it and would have kept the case passing after its own fence lifted

#### Scenario: A prelude refused for the same reason

- **WHEN** a case declares EQ2008, its own C# holds a query with a `let`, and its prelude declares a
  record whose method holds one too
- **THEN** it passes

#### Scenario: A prelude refused for another reason

- **WHEN** a case declares EQ2008, its own C# holds a query with a `let`, and its prelude declares a
  record with two methods of one name
- **THEN** it fails, naming EQ1007 and the record

#### Scenario: A case that is not C#

- **WHEN** a case declares a refusal and its C# does not compile
- **THEN** it fails, saying the case must be C# that compiles

#### Scenario: A warning

- **WHEN** the converter reports only a warning for a case's C#, as EQ2110 for a number read with no
  culture
- **THEN** the case runs and is compared with .NET

#### Scenario: A prelude the build refuses

- **WHEN** a case's prelude declares a record with two methods of one name
- **THEN** the case fails before anything runs, naming EQ1007 and the record

#### Scenario: A prelude only the build's scan refuses

- **WHEN** a case's prelude declares a class with a field typed as a nested type of a server-only owner
- **THEN** the case fails before anything runs, naming EQ2010 and the class

#### Scenario: Two preludes that declare one type name

- **WHEN** two preludes declare one type name differently, one refused (with EQ1007, or with EQ2010
  by the build's scan) and one accepted, and the build's verdict is asked of each in both orders
- **THEN** each answers its own verdict in either order

#### Scenario: A module refused before it writes text

- **WHEN** a source the harness compiles as an app's build does holds a class with two methods of
  one name, and no case names the class
- **THEN** the source fails, naming EQ1007 and the class, where the module was dropped for having no
  text and every case passed

### Requirement: Issues carry points, and a delivered issue is pointed by what it delivered

An issue on the board SHALL carry its points in the board's `Estimate` field, one of 1, 2, 3, 5, 8 or
13, and an issue bigger than 13 SHALL be split into sub-issues. A delivered issue that carries no
points SHALL be given the points the size of what it delivered says: the lines changed in the merged
pull requests that closed it, a pull request's lines shared equally among the issues it closes, the
files a tool writes left out, read against a scale whose limits double at each step (under 200 lines
is 1, under 400 is 2, under 800 is 3, under 1,600 is 5, under 3,200 is 8, under 6,400 is 13, and 21
past it).

#### Scenario: A pull request closes two issues

- **WHEN** a merged pull request that changed 1,000 lines closed two issues, and neither was pointed
- **THEN** each is given the points of 500 lines, 3

#### Scenario: A tool's lines are not work

- **WHEN** a pull request changed 120 lines of a source file and 900 of a `PublicAPI.Unshipped.txt`
- **THEN** the size it gives the issue it closes is 120 lines

#### Scenario: A person's points stand

- **WHEN** a delivered issue was given 8 points before its work began
- **THEN** it keeps 8, whatever its pull requests' size says

### Requirement: The board's Roadmap shows real dates and a measured forecast

`scripts/roadmap.py` SHALL measure the pace as the points delivered over the last 28 calendar days,
never divided by fewer than seven, and plan the open work with one lane per epic, each working at the
share of the pace it had, a lane that runs out of work handing its place to the next one waiting. With
`--apply` it SHALL write on every epic and feature the Start date its work really began, the Target
date it really ended once delivered, and the plan's dates otherwise, and SHALL show In progress a
parent whose sub-issues show it under way and Done a closed issue. It SHALL never close an issue, and
its report SHALL give each open milestone the plan's date, a range from repeated runs, and the date it
would land with all the pace.

#### Scenario: A finished lane hands its place on

- **WHEN** an epic that delivered in the window runs out of work, and another epic that did not deliver
  has work waiting
- **THEN** the waiting epic starts the next day, at the share the finished one had

#### Scenario: A parent's sub-issues are all closed

- **WHEN** a feature is open and every sub-issue it has is closed
- **THEN** the report lists it for a person to close, the plan counts it as work of its own, and the
  board is not told to close it

#### Scenario: The board already shows the plan

- **WHEN** the script is run with `--apply` and then run again the same day
- **THEN** the second run reports "The board already shows this."
