#!/usr/bin/env bash
# The ledger's fold (#892): a release's step, beside ./scripts/public-api.sh ship.
#
#   ./scripts/fold-ledger.sh
#
# Every event waiting under docs/ledger/ goes into the chronology of docs/LEDGER.md, after its last
# entry and in order of date, its file is removed, and docs/ledger/folded.json records the chronology
# as it is now.
#
# WHY AN EVENT WAITS AS A FILE: every pull request used to write its entry at the chronology's end,
# so any two open pull requests conflicted there, and a merge GitHub performs reads no attribute that
# could tell it the file only grows. A file nobody else adds never conflicts. The chronology then
# needs one writer, and a release is one.
#
# WHY THE FOLD IS A TEST RUN: what an entry is has one reader, tests/eQuantic.UI.Web.Tests/
# LedgerEntry.cs, which the guard and the fold share, and the fold's own cases run on every host with
# the rest of the suite. This script sets the variable that makes LedgerTests fold instead of compare.
#
# It folds nothing and fails when one waiting file is not an entry. A deliberate change to the
# chronology itself is NOT this script: EQ_UPDATE_LEDGER=1 on the same test records it and folds
# nothing.
set -euo pipefail

ROOT="$(cd "$(dirname "$0")/.." && pwd)"
cd "$ROOT"

# `dotnet test` answers 0 when its filter matches no test, so its exit code alone would call a fold
# that never ran a success. The fold writes the record LAST, after it removed every file it folded,
# and it writes nothing when it refuses one. So a record newer than this run's start is a fold that
# ran to its end, whatever it found waiting. Which files wait is the fold's to say, in one place
# (Ledger.Waiting): this script lists none of its own.
started="$(mktemp)"
trap 'rm -f "$started"' EXIT

EQ_FOLD_LEDGER=1 dotnet test tests/eQuantic.UI.Web.Tests --nologo \
    --filter "FullyQualifiedName~eQuantic.UI.Web.Tests.LedgerTests"

if [ ! docs/ledger/folded.json -nt "$started" ]; then
    echo "The fold did not run: docs/ledger/folded.json was not written. Does LedgerTests still fold under EQ_FOLD_LEDGER=1?" >&2
    exit 1
fi

echo
echo "What the fold changed. Read it before committing:"
git status --short -- docs/LEDGER.md docs/ledger
