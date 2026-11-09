#!/usr/bin/env bash
#
# A commit message carries the change and nothing a tool left behind.
#
# CLAUDE.md forbids agent attribution in a commit by CATEGORY — no co-authorship for an assistant,
# no session link, no tool's signature line. That rule is enforced by a person reading the message
# back, and three squash messages on main say the reading does not always happen: 9c94d148,
# fdd08b12 and 134aa34d each end with the scaffolding of the tool that composed them, and in one of
# them it is glued to the last sentence of real prose. Nothing compared, so nobody saw it for five
# days. This is the comparison.
#
# WHAT IT READS: commit messages, and only those. The pull request BODY is deliberately out of
# scope. The three measured cases were squash messages whose pull request bodies were clean, and a
# body is where this guard's own patterns have to be discussed — a guard that cannot be described
# in the pull request that introduces it is a guard someone will disable.
#
# THE SAME APPLIES TO A COMMIT ABOUT THIS FILE. Describe a shape, do not write an instance of it,
# exactly as CLAUDE.md uses placeholders rather than a real model identifier for the same reason.
#
# TWO RULES, because one of them is a category and the other is a list:
#
#   1. STRUCTURAL — a line that is nothing but an UNBALANCED markup tag: a closing tag this message
#      never opened, or an opening tag it never closes. That is what leaked scaffolding IS, and the
#      definition is not a convenience — the message was cut out of a larger document, so the other
#      half of the tag is in a document the commit never saw.
#
#      The first draft of this rule refused any tag-only line and the self-test refused it back:
#      a commit quoting a csproj puts `<PropertyGroup>` and its closer on lines of their own, and
#      that snippet is BALANCED. All 1,273 commits on main agreed with the draft, because none of
#      them happens to quote multi-line XML — the fixture found in a second what the history could
#      not, which is the whole argument for having fixtures.
#
#   2. NAMED — the scaffolding tokens by name, anywhere on a line, which is how one of the three
#      hides: `...filed as #241 and #243.` with the closing tag welded to the full stop. A rule
#      about whole lines can never see that one. The bare word "invoke" appears in thirteen commit
#      messages as ordinary prose, which is why this matches the TAG and never the word.
#
# Rule 2 is an enumerated set and enumerated sets are always incomplete — this repository has paid
# for that lesson more than once. It may only GROW: a new shape found is a new entry here, never a
# reason to coarsen rule 1 back into something that fires on a quoted csproj.
#
# It grew once already (#851). A pull request opened from a Claude Code project carries, in its BODY,
# a comment naming the attribution, a link to the conversation it was requested in and a signature
# line that links to the tool, sometimes to a session. The squash took that body, and six squash
# messages reached main with all three while this guard passed them, because none of the three was
# on the list. The squash now takes the pull request's commits' messages instead, which this job reads
# on the pull request before the merge, and the list names the three for the message that still
# reaches main another way: the comment by its token, the signature by its phrase, and any link into
# the tool's own pages, which a commit has no reason to carry (prose that names the tool, or quotes
# CLAUDE.md's first line, writes no scheme and stays clean).
#
# AND RULE 2 READS ACROSS A LINE BREAK. The squash composer wraps a body at 72 columns, and in two of
# those six it broke the signature's link text in two. A named phrase is matched once more over the
# message with its line breaks taken out, so where the wrap falls cannot decide the verdict.
#
# --self-test runs both rules against fixtures of every shape, including the ones that must PASS.
# CI runs it on every push, so the day a pattern stops matching is the day CI says so, rather than
# the day someone next reads a commit.
#
# --event reads the range from the CI event that started the run (#1308): a pull request's base and
# head, a merge group's base and head, which hold the squash commits GitHub's merge queue composed and
# is about to put on main, or a push's before and after. The workflow used to choose the range in an
# expression no test could reach, and under a merge group that expression gave an empty base, so the
# guard would have scanned the group's last commit alone and passed every other one.
#
# --file reads a message that is not a commit yet, which is the only moment anything can be stopped.
# A squash message is composed at MERGE time and handed to `gh pr merge --body-file`; on a push to
# main these same rules run again and can then only report, because main's history is not rewritten.

set -uo pipefail

NAMED='</?(commit_message|invoke|function_calls|antml:[a-zA-Z_]+)>|<(invoke|parameter)[[:space:]]+name=|🤖 Generated with|[Cc]o-[Aa]uthored-[Bb]y:[^\n]*noreply@anthropic\.com|[Gg]enerated[[:space:]]+(by|with)[[:space:]]+\[?Claude|https?://claude\.ai/code|claude-projects-attribution'

# Prints every offending line of one message, prefixed by its line number. Empty output means clean.
# Every grep here is fed by a HERESTRING and never by a pipe, and that is load-bearing rather than
# style. Under `pipefail`, `printf ... | grep -q` fails when grep exits on its first match and
# printf takes a SIGPIPE writing the rest — so a counterpart that EXISTS reads as missing, and the
# guard flags a balanced tag. Measured: a 267 KB message with a balanced pair near the top, refused
# by the pipe form and accepted by this one. Below the pipe buffer, roughly 64 KB, printf finishes
# first and the bug hides, which is why `good_large_balanced_xml` is deliberately larger than that.
scan_message() {
    local message="$1"
    {
        /usr/bin/grep -nE "$NAMED" <<<"$message"

        # Rule 1. A tag alone on a line is an offence only when its counterpart is missing from the
        # message: a closer with no opener anywhere, or an opener with no closer.
        local n=0 line name
        while IFS= read -r line; do
            n=$((n + 1))
            if [[ $line =~ ^[[:space:]]*\</([a-zA-Z][^\ \>/]*)\>[[:space:]]*$ ]]; then
                name="${BASH_REMATCH[1]}"
                /usr/bin/grep -qE "<${name}[ >]" <<<"$message" || printf '%s:%s\n' "$n" "$line"
            elif [[ $line =~ ^[[:space:]]*\<([a-zA-Z][^\ \>/]*)\>[[:space:]]*$ ]]; then
                name="${BASH_REMATCH[1]}"
                /usr/bin/grep -qE "</${name}>" <<<"$message" || printf '%s:%s\n' "$n" "$line"
            fi
        done <<<"$message"

        # Rule 2 again, over the message with its line breaks taken out. A match found that way and on
        # no single line is one a wrap split, and it is reported as such: every other match was
        # already reported above, by its line.
        local match
        while IFS= read -r match; do
            /usr/bin/grep -qF -- "$match" <<<"$message" || printf 'wrapped:%s\n' "$match"
        done < <(/usr/bin/grep -oE "$NAMED" <<<"$(/usr/bin/tr '\n' ' ' <<<"$message")")
    } | sort -t: -k1,1n -u
}

fail=0

report() {
    local label="$1" hits="$2"
    printf '\n%s\n' "$label"
    printf '%s\n' "$hits" | /usr/bin/sed 's/^/    /'
    fail=1
}

# Scans every commit message in base..head, or head alone when base is empty, the all-zero sha or not
# a commit here (a new branch, a tag push, the first push to a repository): there is then no range to
# walk, and scanning the head beats skipping and reporting success it did not measure.
scan_range() {
    local base="$1" head="${2:-HEAD}" commits count sha hits

    # An unresolvable HEAD is the one failure that must never read as success. A shallow checkout
    # that does not contain the commit would otherwise walk an empty range, print "all clean" and
    # go green having measured NOTHING — the shape this repository calls an instrument that never
    # ran in the condition it exists for.
    if ! git rev-parse --verify --quiet "$head^{commit}" >/dev/null; then
        printf 'cannot resolve %s in this checkout, so nothing was measured.\n' "$head" >&2
        printf 'Give the job a full history (actions/checkout with fetch-depth: 0).\n' >&2
        return 1
    fi

    if [ -z "$base" ] || [ "$base" = "0000000000000000000000000000000000000000" ] \
        || ! git rev-parse --verify --quiet "$base^{commit}" >/dev/null; then
        commits=$(git rev-parse "$head")
        printf 'no usable base (%s), scanning %s alone\n' "${base:-empty}" "$(git log -1 --format=%h "$head")"
    else
        if ! commits=$(git rev-list "$base..$head"); then
            printf 'could not walk %s..%s, so nothing was measured.\n' "$base" "$head" >&2
            return 1
        fi
    fi

    count=0
    for sha in $commits; do
        count=$((count + 1))
        hits=$(scan_message "$(git log -1 --format=%B "$sha")")
        [ -n "$hits" ] && report "$(git log -1 --format='%h %s' "$sha")" "$hits"
    done

    if [ "$fail" -ne 0 ]; then
        cat <<'EOF'

A commit message above carries text a tool left behind rather than something about the change.

CLAUDE.md: no co-authorship or agent-attribution lines, in any spelling, and that includes the
scaffolding of whatever composed the message. On a squash merge the message is composed at MERGE
time, which is why reading back the pull request body is not enough — read back what you are about
to squash with.

This cannot be undone on main, whose history is not rewritten. What it can do is stop the next one:
write the message to a file, read the file, then merge with it.
EOF
        return 1
    fi
    printf '%s commit message(s) scanned, all clean\n' "$count"
    return 0
}

# The range a CI event is about (#1308), from the event's name, its JSON and the run's commit: the
# base on one line and the head on the next, an empty base meaning the head alone. A pull request or a
# merge group that names no base or no head is refused rather than read as a shorter range: that is a
# guard measuring less than it says.
event_range() {
    local name="$1" path="$2" sha="$3" base="" head=""
    if ! command -v jq >/dev/null 2>&1; then
        printf 'jq is not on PATH, so the %s event cannot be read.\n' "${name:-unnamed}" >&2
        return 1
    fi
    case "$name" in
    pull_request)
        base=$(jq -r '.pull_request.base.sha // empty' "$path") || return 1
        head=$(jq -r '.pull_request.head.sha // empty' "$path") || return 1
        ;;
    merge_group)
        # The queue's run: base_sha is main as the queue saw it, head_sha the group's last squash.
        base=$(jq -r '.merge_group.base_sha // empty' "$path") || return 1
        head=$(jq -r '.merge_group.head_sha // empty' "$path") || return 1
        ;;
    push)
        base=$(jq -r '.before // empty' "$path") || return 1
        head=$sha
        ;;
    *)
        # A run by hand, or any event that names no range: the commit it runs on, alone.
        head=$sha
        ;;
    esac
    case "$name" in
    pull_request | merge_group)
        if [ -z "$base" ] || [ -z "$head" ]; then
            printf 'the %s event names no base or no head, so nothing would be measured.\n' "$name" >&2
            return 1
        fi
        ;;
    esac
    if [ -z "$head" ]; then
        printf 'the %s event names no commit (GITHUB_SHA is empty), so nothing would be measured.\n' "${name:-unnamed}" >&2
        return 1
    fi
    printf '%s\n%s\n' "$base" "$head"
}

# --- self-test -------------------------------------------------------------------------------
# Each fixture is a verdict this guard must reach. The three BAD shapes are the ones measured on
# main; the GOOD ones are what a commit in this repository legitimately says, and they are here
# because a guard that only ever sees bad input cannot tell you it still discriminates.
self_test() {
    local bad_tag_only='♻️ refactor: something real

A body that says something.
</commit_message>'

    local bad_glued='✨ feat: something real

Two follow-ups came out of these rounds and are filed as #241 and #243.</commit_message>'

    local bad_signature='🔧 chore: something real

🤖 Generated with SomeTool'

    # The case rule 1 EXISTS for, and the only fixture that isolates it: a wrapper nobody has seen
    # yet, so rule 2 cannot know its name and only the missing counterpart gives it away. Without
    # this fixture every bad case above would still be refused by rule 2, and rule 1 could rot green.
    local bad_unknown_scaffolding='📝 docs: something real

A body that says something.
</some_future_wrapper>'

    # The shapes of #851 as they reached main: the signature on one line, and the same signature with
    # its link text wrapped, linking to a session. Each is refused by two entries at once, so the
    # three fixtures after these are the ones that isolate an entry.
    local bad_signature_link='🐛 fix: something real

_Generated by [Claude Code](https://claude.ai/code)_'

    local bad_wrapped_session_link='🐛 fix: something real

_Generated by [Claude
Code](https://claude.ai/code/session_0000000000000000)_'

    # The body's opening comment alone, with no link beside it to give it away; a link to the
    # conversation alone; and a signature wrapped where only the reading across a line break can see
    # it: no link, and the break inside the phrase, so no single line holds a match.
    local bad_attribution_comment='🐛 fix: something real

<!-- claude-projects-attribution: {"github_login":"someone"} -->'

    local bad_conversation_link='🐛 fix: something real

_Requested by **someone** · [project thread](https://claude.ai/code/project/chan_0000?thread=cmsg_0000)_'

    local bad_phrase_across_a_break='🐛 fix: something real

A body that says something. Generated
by Claude Code'

    local good_xml='🔧 chore: the csproj declares its own publish shape

The property goes beside the others:

  <PropertyGroup>
    <PublishAot>true</PublishAot>
  </PropertyGroup>

and the SDK reads it from there.'

    local good_prose='♻️ refactor: the SDK invokes the bundler once

The build used to invoke it per page. One invoke, one bundle.'

    local good_human_coauthor='🐛 fix: the fallback resolves a runtime-provided type

Co-Authored-By: A Contributor <person@example.com>'

    # Prose that names the tool, wrapped as the squash composer wraps it, beside CLAUDE.md's first line
    # quoted and a file that is regenerated: what main's own messages say, and must keep saying.
    local good_naming_the_tool='📝 docs: the checklist names the review

- [x] The diff reviewed by its author before the PR opened (in Claude
Code, `/code-review high` on the branch)

It provides guidance to Claude Code (claude.ai/code), and the pins are
regenerated with `EQ_UPDATE_TRANSPILED=1`.'

    # Balanced and UNINDENTED, so the discrimination cannot be indentation doing the work.
    local good_unindented_xml='👷 ci: the workflow gains a step

<step>
runs the guard
</step>'

    # Bigger than the pipe buffer, with the balanced pair at the TOP so a reader of the counterpart
    # check would match early and stop. That combination is the only one that shows the SIGPIPE
    # bug, and a fixture of ordinary size passes whether or not the bug is there.
    local line='a line of ordinary prose that says something about the change' i big=''
    for i in 1 2 3 4 5 6 7 8 9 10 11 12; do big="$big$big$line
"; done
    local good_large_balanced_xml='🔧 chore: a body longer than a pipe buffer

<step>
runs the guard
</step>

'"$big"

    local errors=0
    local name expected hits
    local refused=0 accepted=0
    for name in bad_tag_only bad_glued bad_signature bad_unknown_scaffolding \
        bad_signature_link bad_wrapped_session_link bad_attribution_comment bad_conversation_link \
        bad_phrase_across_a_break \
        good_xml good_prose good_human_coauthor good_naming_the_tool good_unindented_xml \
        good_large_balanced_xml; do
        case "$name" in
        bad_*) expected=dirty refused=$((refused + 1)) ;;
        *) expected=clean accepted=$((accepted + 1)) ;;
        esac
        hits=$(scan_message "${!name}")
        if [ -n "$hits" ] && [ "$expected" = clean ]; then
            printf 'SELF-TEST FAILED: fixture %s should be clean and matched:\n%s\n' "$name" "$hits"
            errors=1
        elif [ -z "$hits" ] && [ "$expected" = dirty ]; then
            printf 'SELF-TEST FAILED: fixture %s should be refused and matched nothing\n' "$name"
            errors=1
        fi
    done

    # The report, not only the verdict: what only the reading across a line break finds is labelled
    # wrapped, and a match that sits on one line is reported by its line and never again as wrapped.
    hits=$(scan_message "$bad_phrase_across_a_break")
    if [ "$hits" != 'wrapped:Generated by Claude' ]; then
        printf 'SELF-TEST FAILED: bad_phrase_across_a_break should be reported as wrapped and was:\n%s\n' "$hits"
        errors=1
    fi
    hits=$(scan_message "$bad_signature_link")
    if [ "$hits" != '3:_Generated by [Claude Code](https://claude.ai/code)_' ]; then
        printf 'SELF-TEST FAILED: bad_signature_link should be reported once, by its line, and was:\n%s\n' "$hits"
        errors=1
    fi

    # The range each CI event gives (#1308): what a pull request, a merge queue's group, a push and a
    # run by hand are about, and a refusal for a pull request or a group that names no base. Each is
    # asserted by what event_range prints, read as the --event entry point reads it.
    local events ranges=0 got want
    events=$(mktemp -d)
    printf '{"pull_request":{"base":{"sha":"b1"},"head":{"sha":"h1"}}}' >"$events/pull_request.json"
    printf '{"merge_group":{"base_sha":"b2","head_sha":"h2","head_ref":"refs/heads/gh-readonly-queue/main/pr-7-h2"}}' >"$events/merge_group.json"
    printf '{"before":"b3","after":"h3"}' >"$events/push.json"
    printf '{"inputs":{}}' >"$events/workflow_dispatch.json"
    printf '{"merge_group":{"head_sha":"h4"}}' >"$events/merge_group_without_base.json"
    printf '{"pull_request":{"base":{"sha":"b5"},"head":{}}}' >"$events/pull_request_without_head.json"
    expect_range() {
        local name="$1" file="$2" sha="$3" want="$4"
        ranges=$((ranges + 1))
        if ! got=$(event_range "$name" "$events/$file.json" "$sha" 2>/dev/null); then
            got="(refused)"
        fi
        if [ "$got" != "$want" ]; then
            printf 'SELF-TEST FAILED: the %s event (%s) gave the range [%s], expected [%s]\n' \
                "$name" "$file" "${got//$'\n'/..}" "${want//$'\n'/..}"
            errors=1
        fi
    }
    expect_range pull_request pull_request sha0 $'b1\nh1'
    expect_range merge_group merge_group h2 $'b2\nh2'
    expect_range push push h3 $'b3\nh3'
    expect_range workflow_dispatch workflow_dispatch h5 $'\nh5'
    expect_range merge_group merge_group_without_base h4 "(refused)"
    expect_range pull_request pull_request_without_head sha0 "(refused)"

    # End to end, on a repository of its own: a merge group of two squash commits whose FIRST carries a
    # tool's signature. The head alone is clean, so a range of the head alone, which is what the
    # workflow's expression gave a merge group, passes it; the event's range refuses it. Both are run
    # as CI runs them, this script in a process of its own.
    local self repo base_sha head_sha
    self="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)/$(basename "${BASH_SOURCE[0]}")"
    repo="$events/repo"
    git init --quiet "$repo"
    git -C "$repo" config user.name "Self Test"
    git -C "$repo" config user.email "self-test@example.invalid"
    git -C "$repo" config commit.gpgsign false
    git -C "$repo" commit --quiet --allow-empty -m "🔧 chore: main as the queue saw it"
    base_sha=$(git -C "$repo" rev-parse HEAD)
    git -C "$repo" commit --quiet --allow-empty -m "$bad_signature"
    git -C "$repo" commit --quiet --allow-empty -m "✅ test: the group's last squash (#2)"
    head_sha=$(git -C "$repo" rev-parse HEAD)
    printf '{"merge_group":{"base_sha":"%s","head_sha":"%s","head_ref":"refs/heads/gh-readonly-queue/main/pr-2-%s"}}' \
        "$base_sha" "$head_sha" "$head_sha" >"$events/group.json"
    ranges=$((ranges + 1))
    if (cd "$repo" && bash "$self" --range "" "$head_sha") >/dev/null 2>&1; then :; else
        printf 'SELF-TEST FAILED: the group'"'"'s head alone was refused, so the fixture proves nothing about the range\n'
        errors=1
    fi
    ranges=$((ranges + 1))
    if (cd "$repo" && GITHUB_EVENT_NAME=merge_group GITHUB_EVENT_PATH="$events/group.json" GITHUB_SHA="$head_sha" \
        bash "$self" --event) >/dev/null 2>&1; then
        printf 'SELF-TEST FAILED: a merge group whose first squash carries a signature passed --event\n'
        errors=1
    fi
    rm -rf "$events"

    if [ "$errors" -ne 0 ]; then
        printf '\nThe guard no longer discriminates. Fix it before trusting a green run.\n'
        return 1
    fi
    printf 'self-test: %s fixtures, %s refused and %s accepted, and %s event ranges, as expected\n' \
        "$((refused + accepted))" "$refused" "$accepted" "$ranges"
    return 0
}

# --- entry point -----------------------------------------------------------------------------
case "${1:---help}" in
--self-test)
    self_test
    exit $?
    ;;
--file)
    # A message BEFORE it becomes a commit. The three leaks this guard was written for were squash
    # bodies composed at merge time and passed straight to `gh pr merge --body-file`, so the only
    # moment a check could have stopped one is between writing that file and merging with it. On a
    # push to main the same rules run again and can only report.
    path="${2:-}"
    if [ -z "$path" ] || [ ! -f "$path" ]; then
        printf 'usage: check-commit-messages.sh --file <path to a message>\n' >&2
        exit 2
    fi

    # The read is checked rather than trusted, and that is the same hole as the unresolvable head
    # below. `scan_message "$(cat …)"` inside a substitution DISCARDS cat's status, so an unreadable
    # or half-read file arrives as an empty string, matches nothing, and the guard says "is clean"
    # on a message it never saw — green, having measured nothing, at the one moment it could have
    # stopped a merge.
    if ! message=$(cat -- "$path"); then
        printf 'cannot read %s, so nothing was measured.\n' "$path" >&2
        exit 1
    fi
    hits=$(scan_message "$message")
    if [ -n "$hits" ]; then
        report "$path" "$hits"
        printf '\nDo not merge with this file. Delete those lines first.\n'
        exit 1
    fi
    printf '%s is clean\n' "$path"
    exit 0
    ;;
--range)
    # A..B, as a push event or a pull request gives it.
    scan_range "${2:-}" "${3:-HEAD}"
    exit $?
    ;;
--event)
    # The range from the CI event that started this run, read from GITHUB_EVENT_NAME, the event's JSON
    # at GITHUB_EVENT_PATH and GITHUB_SHA, all three set by GitHub Actions.
    if ! range=$(event_range "${GITHUB_EVENT_NAME:-}" "${GITHUB_EVENT_PATH:-}" "${GITHUB_SHA:-}"); then
        exit 1
    fi
    { IFS= read -r base; IFS= read -r head; } <<<"$range"
    scan_range "$base" "$head"
    exit $?
    ;;
*)
    cat <<'EOF'
usage:
  check-commit-messages.sh --self-test            run the rules against their fixtures
  check-commit-messages.sh --file <path>          scan a message BEFORE it becomes a commit
  check-commit-messages.sh --range <base> [head]  scan every commit message in base..head
  check-commit-messages.sh --event                the range of the CI event that started this run
EOF
    exit 2
    ;;
esac
