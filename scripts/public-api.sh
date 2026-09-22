#!/usr/bin/env bash
# The declared public API (#242): one entry point for the two things anyone ever does to it.
#
#   ./scripts/public-api.sh update   after changing a public signature — writes what changed
#   ./scripts/public-api.sh ship     at a release — folds Unshipped into Shipped
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
    local line path entry dir shipped unshipped declared=0
    while IFS= read -r line; do
        path="${line%%(*}"
        entry="${line#*error RS0016: Symbol \'}"
        entry="${entry%%\' is not part of*}"
        [ -n "$entry" ] || continue

        # RS0016 is reported against the SOURCE file, so the project is the src/<Name> it sits in.


        dir="$ROOT/$(printf '%s' "${path#"$ROOT"/}" | cut -d/ -f1,2)"
        shipped="$dir/PublicAPI.Shipped.txt"
        unshipped="$dir/PublicAPI.Unshipped.txt"
        [ -f "$shipped" ] && [ -f "$unshipped" ] || continue

        grep -Fxq "$entry" "$shipped" && continue
        grep -Fxq "$entry" "$unshipped" && continue
        printf '%s\n' "$entry" >> "$unshipped"
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
    local line path entry dir shipped unshipped retired=0
    while IFS= read -r line; do
        path="${line%%(*}"
        entry="${line#*error RS0017: Symbol \'}"
        entry="${entry%%\' is part of*}"
        [ -n "$entry" ] && [ -f "$path" ] || continue

        dir="$(dirname "$path")"
        shipped="$dir/PublicAPI.Shipped.txt"
        unshipped="$dir/PublicAPI.Unshipped.txt"

        if grep -Fxq "$entry" "$shipped"; then
            grep -Fxq "*REMOVED*$entry" "$unshipped" || printf '*REMOVED*%s\n' "$entry" >> "$unshipped"
            echo "  retired (shipped)   ${dir##*/}: $entry"
            retired=$((retired + 1))
        elif grep -Fxq "$entry" "$unshipped"; then
            grep -Fxv "$entry" "$unshipped" > "$unshipped.tmp" && mv "$unshipped.tmp" "$unshipped"
            echo "  dropped (unshipped) ${dir##*/}: $entry"
            retired=$((retired + 1))
        fi
    done < <(rebuild_all | grep -E "error RS0017: Symbol '" | sort -u)
    echo "  $retired entries retired"
}

# Every project rebuilt, one at a time rather than the solution at once: the solution stops at the
# first project this machine cannot build — a shell needing a mobile SDK — and the analyzer then
# runs for nothing after it. Per project, that one fails and the other thirty-seven still report.
rebuild_all() {
    local csproj
    for csproj in $(projects); do
        dotnet build "$csproj" -t:Rebuild --nologo -v quiet 2>&1 || true
    done
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
    echo "Declared what changed. Read the diff before committing it — that IS the API review."
}

# THE RELEASE STEP: what was unshipped has now shipped. A `*REMOVED*` pair cancels — the retired
# entry leaves Shipped and the marker leaves Unshipped — and everything else moves across.
ship() {
    local csproj dir shipped unshipped
    for csproj in $(projects); do
        dir="$(dirname "$csproj")"
        shipped="$dir/PublicAPI.Shipped.txt"
        unshipped="$dir/PublicAPI.Unshipped.txt"
        [ -f "$shipped" ] && [ -f "$unshipped" ] || continue

        python3 - "$shipped" "$unshipped" <<'PY'
import sys

shipped_path, unshipped_path = sys.argv[1], sys.argv[2]
header = "#nullable enable"

def entries(path):
    with open(path, encoding="utf-8") as handle:
        return [line.rstrip("\n") for line in handle if line.strip() and line.strip() != header]

shipped = entries(shipped_path)
unshipped = entries(unshipped_path)

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
PY
    done
    echo "Folded Unshipped into Shipped. The *REMOVED* lines you just retired are the release's breaks."
}

case "${1:-}" in
    update) update ;;
    ship)   ship ;;
    *)      usage ;;
esac
