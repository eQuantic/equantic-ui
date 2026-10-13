# Spec Delta

## ADDED Requirements

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
