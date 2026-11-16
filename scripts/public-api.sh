#!/usr/bin/env bash
# The declared public API (#242): one entry point for the two things anyone ever does to it.
#
#   ./scripts/public-api.sh update   after changing a public signature — writes what changed
#   ./scripts/public-api.sh ship     at a release — folds Unshipped into Shipped
#
# A FILE PER CHANGE (#1489). `update` writes into PublicAPI.Unshipped.<branch>.txt beside each
# project, named by the whole branch with its `/` as `-`, never into the project's own Unshipped:
# every change used to append to that one file, so any two pull requests that each added a public
# member conflicted there, and the merge queue ejected one of them. The analyzer reads every file
# whose name starts with PublicAPI.Unshipped, and `ship` folds them all, as fold-ledger.sh folds
# docs/ledger/. On main or a detached HEAD there is no branch to name the file, so `update` refuses
# unless EQ_API_CHANGE names it.
#
# WHY A SCRIPT AT ALL, when the analyzer ships a code fix: `dotnet format` applies the ADDITION
# (RS0016) and not the REMOVAL. Measured on a clean compile — "Formatted 0 of 158 files" — because
# RS0017 is reported against the .txt file rather than against any C#, so there is no document for
# the fixer to rewrite. The analyzer's own convention for a removal is to LEAVE the line in Shipped
# and add `*REMOVED*<entry>` to Unshipped, which is what the second half below does.
#
# The release notes' list of breaks is then the build's: the `*REMOVED*` lines a release folds in,
# plus the diff of the Shipped files between two tags.
set -euo pipefail

ROOT="$(cd "$(dirname "$0")/.." && pwd)"
cd "$ROOT"

# Every project the analyzer covers, DERIVED the same way Directory.Build.targets derives it — a
# project that ships an assembly packs and includes its build output. Listing them here instead
# would be a second place the rule lives, and the two would drift.
projects() {
    for csproj in src/*/[!.]*.csproj; do
        # An unmatched glob is the literal pattern, and every caller would then act on a path that
        # does not exist — creating "src/*/PublicAPI.Shipped.txt" rather than reporting nothing.
        [ -f "$csproj" ] || continue
        grep -q "<IsPackable>false</IsPackable>" "$csproj" && continue
        grep -q "<IncludeBuildOutput>false</IncludeBuildOutput>" "$csproj" && continue
        printf '%s\n' "$csproj"
    done
}

usage() {
    echo "usage: $0 {update|ship}" >&2
    exit 2
}

# The name of the change's own file: the branch, whole and with its `/` as `-`, so two branches never
# share one (`feat/a-x` and `fix/a-x` are two files). EQ_API_CHANGE names it where no branch does.
change_name() {
    local name="${EQ_API_CHANGE:-}"
    if [ -z "$name" ]; then
        name="$(git rev-parse --abbrev-ref HEAD 2>/dev/null || true)"
        case "$name" in
            ""|HEAD|main)
                echo "update writes into the change's own PublicAPI.Unshipped.<name>.txt, and ${name:-this checkout} names no change." >&2
                echo "Run it on the change's branch, or name the change: EQ_API_CHANGE=<name> $0 update" >&2
                return 1 ;;
        esac
    fi
    name="${name//\//-}"
    if ! printf '%s' "$name" | grep -Eq '^[A-Za-z0-9][A-Za-z0-9._-]*$'; then
        echo "The change's name \"$name\" is not a file name a build reads back: use letters, digits, '.', '-' and '_'." >&2
        return 1
    fi
    printf '%s' "$name"
}

# The change's file in a project's folder, created with the header the analyzer expects.
change_file() {
    local file="$1/PublicAPI.Unshipped.$CHANGE.txt"
    [ -f "$file" ] || printf '#nullable enable\n' > "$file"
    printf '%s' "$file"
}

# Every Unshipped file of a project's folder: its own and each change's.
unshipped_files() {
    local file
    for file in "$1"/PublicAPI.Unshipped.txt "$1"/PublicAPI.Unshipped.*.txt; do
        [ -f "$file" ] && printf '%s\n' "$file"
    done
}

# Whether ENTRY (exactly) is a line of any Unshipped file of the folder.
declared_unshipped() {
    local dir="$1" entry="$2" file
    while IFS= read -r file; do
        grep -Fxq -- "$entry" "$file" && return 0
    done < <(unshipped_files "$dir")
    return 1
}

# A change's file that holds nothing but its header declares nothing, so it goes.
remove_empty_change_files() {
    local csproj dir file
    for csproj in $(projects); do
        dir="$(dirname "$csproj")"
        for file in "$dir"/PublicAPI.Unshipped.*.txt; do
            [ -f "$file" ] || continue
            grep -qvxE '#nullable enable|[[:space:]]*' "$file" && continue
            rm -f "$file"
            echo "  removed ${file#"$ROOT"/}: it declares nothing"
        done
    done
}

# WHAT APPEARED: RS0016 names a signature that is not declared, and the entry it wants is quoted in
# the message — so the entry is read from the build rather than written by the analyzer's own fix.
#
# `dotnet format analyzers --diagnostics RS0016` is the obvious tool and it does not work here,
# measured twice: with any RS0017 outstanding it writes NOTHING (eQuantic.UI.Web, 204 additions and
# 68 removals pending, wrote 0 entries; with the declared file emptied so no removal could be
# reported, the same command wrote 893), and with the declared file POPULATED it writes nothing
# either — Primitives, 142 additions outstanding and no removals, wrote 0 across three passes.
# Seeding an empty file is the one case it serves, which is why it seeded the .55 surface and does
# not maintain it.
#
# Reading both directions out of the build instead means ONE mechanism and one parser, and neither
# half can drift from what the compiler actually said.
declare_new_entries() {
    local line path entry dir shipped declared=0
    while IFS= read -r line; do
        path="${line%%(*}"
        entry="${line#*error RS0016: Symbol \'}"
        entry="${entry%%\' is not part of*}"
        [ -n "$entry" ] || continue

        # RS0016 is reported against the SOURCE file, so the project is the src/<Name> it sits in.
        dir="$ROOT/$(printf '%s' "${path#"$ROOT"/}" | cut -d/ -f1,2)"
        shipped="$dir/PublicAPI.Shipped.txt"
        [ -f "$shipped" ] && [ -f "$dir/PublicAPI.Unshipped.txt" ] || continue

        # Declared once, across every file: an entry two files hold is RS0025.
        grep -Fxq -- "$entry" "$shipped" && continue
        declared_unshipped "$dir" "$entry" && continue
        printf '%s\n' "$entry" >> "$(change_file "$dir")"
        declared=$((declared + 1))
    done < <(rebuild_all | grep -E "error RS0016: Symbol '" | sort -u)
    echo "  $declared entries declared"
}

# WHAT WENT: RS0017 names an entry that is declared and no longer exists. A SHIPPED entry is
# retired with a `*REMOVED*` line in Unshipped, so the release can still list it; an UNSHIPPED one
# never shipped, so it simply goes.
#
# ONE BUILD FOR ALL OF THEM, and that is not an optimisation — it is where the answer is. The
# diagnostic is reported against the .txt FILE, so building one project reports the removals of
# every project it references too, under their paths. Reading it per project and editing the
# project being built retires other people's entries into the wrong file, or none: measured, a
# build of eQuantic.UI.Web reported sixty-four removals, every one of them Primitives'.
#
# REBUILT, because an up-to-date project does not recompile and no analyzer runs — and the
# diagnostics stay ERRORS (TreatWarningsAsErrors is the repo's default since #126), because
# `-v quiet` prints errors and swallows warnings. The build fails on purpose; only its output is
# wanted.
retire_missing_entries() {
    local line path entry dir shipped retired=0
    while IFS= read -r line; do
        path="${line%%(*}"
        entry="${line#*error RS0017: Symbol \'}"
        entry="${entry%%\' is part of*}"
        [ -n "$entry" ] && [ -f "$path" ] || continue

        # The diagnostic names the FILE that declares the entry: Shipped, the project's own
        # Unshipped, or a change's.
        dir="$(dirname "$path")"
        shipped="$dir/PublicAPI.Shipped.txt"

        if [ "$path" = "$shipped" ] && grep -Fxq -- "$entry" "$shipped"; then
            declared_unshipped "$dir" "*REMOVED*$entry" \
                || printf '*REMOVED*%s\n' "$entry" >> "$(change_file "$dir")"
            echo "  retired (shipped)   ${dir##*/}: $entry"
            retired=$((retired + 1))
        elif grep -Fxq -- "$entry" "$path"; then
            grep -Fxv -- "$entry" "$path" > "$path.tmp" && mv "$path.tmp" "$path"
            echo "  dropped (unshipped) ${dir##*/}/${path##*/}: $entry"
            retired=$((retired + 1))
        fi
    done < <(rebuild_all | grep -E "error RS0017: Symbol '" | sort -u)
    echo "  $retired entries retired"
}

# THE MACHINE CANNOT BUILD IT, or the build is broken — and `|| true` said neither. A project whose
# restore or compile dies before the analyzer runs emits no RS diagnostic at all, so both parsers
# read silence and `update` reported success over an API change nobody declared.
#
# The one legitimate silence is a target this HOST cannot build: Shell.iOS wants the iOS workload
# (NETSDK1178) and Shell.Android the Android SDK (XA5300), and neither exists off macOS. Those are
# named, and their surfaces come from CI instead. Every other failure is a failure: it is reported
# with the build's own last words and `update` exits non-zero rather than declaring a surface it
# never saw.
# THE ONLY ERRORS A FAILED BUILD MAY CARRY. RS0016 and RS0017 are the two this script reads, so a
# build reporting them did the job it was asked to do. The four platform codes say this HOST cannot
# build that target at all — Shell.iOS wants the iOS workload, Shell.Android the Android SDK — and
# their surfaces come from CI instead.
#
# EVERY code must be on this list, not merely one of them: "contains an RS00 error" was the first
# attempt and it let RS0026, RS0027, RS0041 — real analyzer findings that neither parser reads —
# stand in for a build that did its job, and a compile error riding beside an RS0016 would have
# gone the same way.
EXPECTED_ERROR='^(RS0016|RS0017|NETSDK1147|NETSDK1178|NETSDK1100|XA5300)$'
UNEXPLAINED="$(mktemp)"
trap 'rm -f "$UNEXPLAINED"' EXIT

# stdout stays the build output, because that is what both parsers read. An unexplained failure
# goes to a file instead, so noticing one cannot disturb what they see.
rebuild_all() {
    local csproj output status codes unexpected
    for csproj in $(projects); do
        output="$(dotnet build "$csproj" -t:Rebuild --nologo -v quiet 2>&1)" && status=0 || status=$?
        printf '%s\n' "$output"
        [ "$status" -eq 0 ] && continue

        # Case-insensitively, because a diagnostic id is not always shouted: xUnit2013 is an id,
        # and TreatWarningsAsErrors makes it fail a build like any other.
        # `|| true` on BOTH: under `set -o pipefail` a grep that matches nothing fails the whole
        # pipeline, and `set -e` then kills the function mid-sweep — silently, which is the one
        # outcome this code exists to prevent. An uncoded error matches neither grep.
        codes="$(printf '%s' "$output" | grep -oE 'error [A-Za-z]+[0-9]+' | sed 's/^error //' | sort -u || true)"
        unexpected="$(printf '%s' "$codes" | grep -viE "$EXPECTED_ERROR" || true)"

        # NO CODE AT ALL is unexplained too, and not hypothetically: an MSBuild <Error> without a
        # Code prints "error :", which is exactly what this file's own missing-declaration check
        # emits. A rule that classifies by code must treat what it cannot classify as unexplained,
        # or the one failure it was written beside would be the one it waves through.
        [ -n "$codes" ] && [ -z "$unexpected" ] && continue

        {
            printf '%s\n' "${csproj#"$ROOT"/}"
            printf '%s' "$output" | grep -oE 'error ([A-Za-z]+[0-9]+)?.*' | sort -u | head -3 | sed 's/^/      /'
        } >> "$UNEXPLAINED"
    done
}

# Named once, read after each round: a project reported here was never analysed, so whatever its
# public surface did this run is undeclared and unnoticed.
report_unexplained_failures() {
    [ -s "$UNEXPLAINED" ] || return 0
    echo >&2
    echo "These projects did not build, and not for a reason this host explains:" >&2
    sort -u "$UNEXPLAINED" >&2
    echo >&2
    echo "Their public API is undeclared rather than unchanged: either the analyzer never ran," >&2
    echo "or it said something this script does not read. Neither is an unchanged surface." >&2
    echo "Fix the build and run update again." >&2
    return 1
}

# A DERIVED PROJECT WITH NO DECLARATION FILES is a new project, and the analyzer's answer to it is
# RS0016 on every symbol it has — loud, but not fixable by this script, which would find no file to
# write and skip it in silence. So the pair is created empty first: the same run that reports the
# surface then declares it, and adding a project stays one command rather than two plus a tip.
ensure_declaration_files() {
    local csproj dir file created=0
    for csproj in $(projects); do
        dir="$(dirname "$csproj")"
        for file in "$dir/PublicAPI.Shipped.txt" "$dir/PublicAPI.Unshipped.txt"; do
            [ -f "$file" ] && continue
            printf '#nullable enable\n' > "$file"
            echo "  created ${file#"$ROOT"/}"
            created=$((created + 1))
        done
    done
    [ "$created" -eq 0 ] || echo "  $created declaration file(s) created"
}

# UNTIL IT SETTLES, because declaring a type reveals its members: the analyzer reports the TYPE
# first and its constructors, properties and operators only once the type itself is declared.
# Measured on this tree from the .55 surface — 111 entries, then 3, then 0 — so a single pass
# leaves a build that still fails, and the person running it would have no way to know except by
# running it again.
update() {
    local round=0 retired declared
    CHANGE="$(change_name)" || return 1
    echo "Writing into PublicAPI.Unshipped.$CHANGE.txt beside each project that changed."
    : > "$UNEXPLAINED"
    ensure_declaration_files
    while :; do
        round=$((round + 1))
        echo "Round $round"
        retired="$(retire_missing_entries)"
        printf '%s\n' "$retired"
        declared="$(declare_new_entries)"
        printf '%s\n' "$declared"

        case "$retired$declared" in
            *"  0 entries retired"*) case "$declared" in *"  0 entries declared"*) break ;; esac ;;
        esac
        [ "$round" -lt 8 ] || { echo "Stopped at $round rounds — something is not settling." >&2; return 1; }
    done
    remove_empty_change_files
    report_unexplained_failures || return 1
    echo "Declared what changed. Read the diff before committing it — that IS the API review."
}

# THE RELEASE STEP: what was unshipped has now shipped. A `*REMOVED*` pair cancels — the retired
# entry leaves Shipped and the marker leaves Unshipped — and everything else moves across. Every
# Unshipped file folds, the project's own and each change's, and a change's file goes once folded.
ship() {
    local csproj dir shipped unshipped missing=""
    for csproj in $(projects); do
        dir="$(dirname "$csproj")"
        [ -f "$dir/PublicAPI.Shipped.txt" ] && [ -f "$dir/PublicAPI.Unshipped.txt" ] \
            || missing="$missing${missing:+$'\n'}  ${dir#"$ROOT"/}"
    done
    # A RELEASE MAY NOT SKIP A PROJECT. `update` creates the pair for a project that has none, so a
    # pair still missing here means update was never run since that project was added — and the
    # release would otherwise fold every other project and exit 0, silently leaving one assembly's
    # whole surface out of the notes. That is the failure this file exists to make impossible.
    if [ -n "$missing" ]; then
        echo "No declared API for:" >&2
        printf '%s\n' "$missing" >&2
        echo "Run ./scripts/public-api.sh update first: a release cannot leave a project's surface undeclared." >&2
        return 1
    fi

    for csproj in $(projects); do
        dir="$(dirname "$csproj")"
        shipped="$dir/PublicAPI.Shipped.txt"
        unshipped="$dir/PublicAPI.Unshipped.txt"

        python3 - "$shipped" "$unshipped" "$dir"/PublicAPI.Unshipped.*.txt <<'PY'
import os
import sys

shipped_path, unshipped_path = sys.argv[1], sys.argv[2]
# A glob that matched nothing is its own pattern: only files that exist are a change's.
changes = [path for path in sys.argv[3:] if os.path.isfile(path)]
header = "#nullable enable"

def entries(path):
    with open(path, encoding="utf-8") as handle:
        return [line.rstrip("\n") for line in handle if line.strip() and line.strip() != header]

shipped = entries(shipped_path)
unshipped = entries(unshipped_path) + [entry for path in changes for entry in entries(path)]

removed = {line[len("*REMOVED*"):] for line in unshipped if line.startswith("*REMOVED*")}
added = [line for line in unshipped if not line.startswith("*REMOVED*")]

# A retired entry leaves Shipped; everything unshipped joins it. Sorted, because the file is read
# as a diff between two tags and an unstable order would drown the change in noise.
kept = sorted(set(shipped) - removed | set(added))

with open(shipped_path, "w", encoding="utf-8") as handle:
    handle.write(header + "\n")
    handle.writelines(entry + "\n" for entry in kept)

with open(unshipped_path, "w", encoding="utf-8") as handle:
    handle.write(header + "\n")

for path in changes:
    os.remove(path)
PY
    done
    echo "Folded every Unshipped file into Shipped. The *REMOVED* lines you just retired are the release's breaks."
}

case "${1:-}" in
    update) update ;;
    ship)   ship ;;
    *)      usage ;;
esac
