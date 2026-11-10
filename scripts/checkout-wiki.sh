#!/usr/bin/env bash
# Clones the wiki the docs guards read (WikiDiagnosticsTests and its siblings in
# tests/eQuantic.UI.Web.Tests) beside this repository, at the pull request's own wiki branch when
# the wiki has one (#406).
#
# The guards compare a pull request's code with the wiki. Reading master for every run tied the two
# together when they cannot move together: a pull request that added a diagnostic failed its own
# guard until its rows were published, and once they were, every other pull request based on main
# failed the same guard from the other side until the first one merged. So a pull request that
# changes what the wiki must say carries its pages on a wiki branch named like its own branch, and
# CI reads that branch. Master moves when the pull request merges, and a run with no such branch (a
# pull request that changes no page, a push to main) reads master.
#
#   scripts/checkout-wiki.sh <destination>   clone into <destination>, which must not exist
#   scripts/checkout-wiki.sh --self-test     prove every path against a local fixture wiki
#
# A merge queue's run (#1308) holds the code of every pull request in its group, and their wiki
# branches reach master only after they merge, so it reads master with each one's wiki branch merged
# onto it, in the group's order: the wiki as it will be once the group merges. The group's pull
# requests are read from its commits through the API: a squash commit's subject ends with ` (#N)`, a
# merge commit's starts with `Merge pull request #N`, and the last one must be the pull request the
# queue's ref names, or the run fails instead of guessing. A fork's pull request is left out, as on a
# pull request's run, and two wiki branches that conflict fail the run.
#
# EQ_WIKI_BRANCH  the pull request's head ref, empty outside a pull request and for a pull request
#                 from a fork, whose branch name can repeat one of this repository's (ci.yml). It is
#                 text someone else wrote, so it is only ever compared and passed as one argument,
#                 never interpolated into a command.
# EQ_QUEUE_BASE   a merge group's base and head commits and its head ref
# EQ_QUEUE_HEAD   (`refs/heads/gh-readonly-queue/main/pr-<N>-<sha>`), empty outside a merge group.
# EQ_QUEUE_REF    The pull requests behind them are asked of the API with `gh`, for GITHUB_REPOSITORY.
# EQ_WIKI_URL     the wiki's repository; the self-test points it at its fixture.
set -euo pipefail

readonly DEFAULT_URL="https://github.com/eQuantic/equantic-ui.wiki.git"

# True when the listing of the wiki's heads holds a branch of exactly this name. An exact name,
# compared in the shell: `git ls-remote <url> <pattern>` matches a pattern against the TAIL of a ref,
# so `some-change` would have found `fix/some-change`, and a `grep -q` that stops at the first match
# can fail its pipeline under pipefail and read master by mistake.
has_branch() {
  local heads=$1 name=$2 sha ref
  [ -n "$name" ] || return 1
  while IFS=$'\t' read -r sha ref; do
    ref=${ref%$'\r'} # a Windows runner's git may end its lines with a carriage return
    if [ "$ref" = "refs/heads/$name" ]; then return 0; fi
  done <<< "$heads"
  return 1
}

# Prints the branch of each "<repository><TAB><branch>" line whose repository is this one, so a
# fork's branch, whose name can repeat one of ours, is never read. Compared in the shell rather than
# inside a jq filter, where the repository's name would have to be written into the expression.
own_branches() {
  local repo=$1 owner branch
  while IFS=$'\t' read -r owner branch; do
    branch=${branch%$'\r'} # gh on a Windows runner may end its lines with a carriage return
    if [ "$owner" = "$repo" ] && [ -n "$branch" ]; then printf '%s\n' "$branch"; fi
  done
}

# The wiki branches a merge group's run reads, one a line, in the order they merge onto master.
#
# First those of the pull requests already on main whose wiki branch is still waiting: the queue
# merges a pull request on its own, and its wiki branch reaches master only when someone merges it,
# so a group that starts in between holds that code and would read master without its pages. They are
# read from the base branch's last hundred closed pull requests, one call, oldest merge first; a wiki
# branch merged into master already merges as nothing.
#
# Then those of the pull requests the group puts on main, from its commits: a squash commit's subject
# ends with ` (#N)`; with the merge method, only `Merge pull request #N` subjects count, since the
# range then also holds each pull request's own commits, whose subjects may cite an issue. The last
# one must be the pull request the queue's ref names, or the run fails instead of guessing.
queue_branches() {
  local repo=${GITHUB_REPOSITORY:-} base=${EQ_QUEUE_BASE:-} head=${EQ_QUEUE_HEAD:-} ref=${EQ_QUEUE_REF:-}
  if [ -z "$repo" ] || [ -z "$base" ] || [ -z "$head" ]; then
    echo "::error::A merge group's run needs GITHUB_REPOSITORY, EQ_QUEUE_BASE and EQ_QUEUE_HEAD." >&2
    return 1
  fi

  local target="" named=""
  if [[ $ref =~ gh-readonly-queue/(.+)/pr-([0-9]+)-[0-9a-fA-F]+$ ]]; then
    target=${BASH_REMATCH[1]}
    named=${BASH_REMATCH[2]}
  else
    echo "::error::'$ref' is not a merge queue's ref." >&2
    return 1
  fi

  local subjects
  if ! subjects=$(gh api "repos/$repo/compare/$base...$head" --jq '.commits[].commit.message | split("\n")[0]'); then
    echo "::error::The merge group's commits could not be read ($base...$head)." >&2
    return 1
  fi

  # Arrays, and the last commit's number kept apart: the macOS runner's bash is 3.2, which has no
  # negative index. `last_*` hold the LAST commit's number only, reset at every commit, so a group
  # whose final commit names no pull request ends with none. In a squash group every commit is a
  # pull request's squash, so one without a number is a group this cannot read, and fails; with the
  # merge method a pull request's own commits carry none, and only the merge commits count.
  local squashed=() merged=() last_squashed="" last_merged="" unnumbered="" subject
  while IFS= read -r subject; do
    subject=${subject%$'\r'}
    last_squashed=""
    last_merged=""
    if [[ $subject =~ ^Merge\ pull\ request\ \#([0-9]+)\  ]]; then
      merged+=("${BASH_REMATCH[1]}")
      last_merged=${BASH_REMATCH[1]}
    elif [[ $subject =~ \(#([0-9]+)\)$ ]]; then
      squashed+=("${BASH_REMATCH[1]}")
      last_squashed=${BASH_REMATCH[1]}
    else
      unnumbered=yes
    fi
  done <<< "$subjects"
  local numbers=() last=""
  if [ -n "$last_merged" ]; then
    numbers=("${merged[@]}")
    last=$last_merged
  elif [ -n "$last_squashed" ] && [ -z "$unnumbered" ]; then
    numbers=("${squashed[@]}")
    last=$last_squashed
  fi
  if [ -z "$last" ] || [ "$last" != "$named" ]; then
    echo "::error::The merge group's commits end with pull request '${last:-none}', and its ref names '$named'." >&2
    return 1
  fi

  local landed
  if ! landed=$(gh api "repos/$repo/pulls?state=closed&base=$target&sort=updated&direction=desc&per_page=100" \
    --jq '[.[] | select(.merged_at != null)] | sort_by(.merged_at) | .[] | [.head.repo.full_name // "", .head.ref] | @tsv'); then
    echo "::error::The pull requests already merged into '$target' could not be read." >&2
    return 1
  fi
  own_branches "$repo" <<< "$landed"

  local number line
  for number in "${numbers[@]}"; do
    if ! line=$(gh api "repos/$repo/pulls/$number" --jq '[.head.repo.full_name // "", .head.ref] | @tsv'); then
      echo "::error::Pull request #$number of the merge group could not be read." >&2
      return 1
    fi
    own_branches "$repo" <<< "$line"
  done
}

# Clones the wiki into $1 for a merge group: its default branch, with each branch queue_branches names
# merged onto it in order, once. Nothing is left behind when it fails.
queue_checkout() {
  local destination=$1
  local url=${EQ_WIKI_URL:-$DEFAULT_URL}
  local branches heads

  branches=$(queue_branches) || return 1
  if ! heads=$(git ls-remote --heads "$url"); then
    echo "::error::The wiki's branches could not be listed at $url." >&2
    return 1
  fi
  git clone --quiet -- "$url" "$destination" || return 1
  git -C "$destination" config user.name "eQuantic CI"
  git -C "$destination" config user.email "ci@equantic.invalid"
  git -C "$destination" config commit.gpgsign false

  local branch merged="" seen=$'\n'
  while IFS= read -r branch; do
    has_branch "$heads" "$branch" || continue
    case "$seen" in *$'\n'"$branch"$'\n'*) continue ;; esac
    seen="$seen$branch"$'\n'
    if ! git -C "$destination" fetch --quiet origin "refs/heads/$branch" \
      || ! git -C "$destination" merge --quiet --no-edit --no-ff FETCH_HEAD > /dev/null; then
      echo "::error::The wiki branch '$branch' does not merge with master and the ones before it." >&2
      rm -rf "$destination"
      return 1
    fi
    merged="$merged '$branch'"
  done <<< "$branches"

  if [ -n "$merged" ]; then
    echo "A merge group: the wiki is read at its default branch with$merged merged onto it."
  else
    echo "A merge group with no wiki branch, so the wiki is read at its default branch."
  fi
  echo "Checked out: $(git -C "$destination" rev-parse --abbrev-ref HEAD) at $(git -C "$destination" rev-parse --short HEAD)."
}

# Clones the wiki into $1, at EQ_WIKI_BRANCH when the wiki has a branch of exactly that name, and at
# its default branch otherwise, then says which one it read. A merge group reads it as queue_checkout
# says.
checkout() {
  local destination=$1
  if [ -n "${EQ_QUEUE_HEAD:-}" ]; then
    queue_checkout "$destination"
    return
  fi
  local url=${EQ_WIKI_URL:-$DEFAULT_URL}
  local branch=${EQ_WIKI_BRANCH:-}
  local heads

  # The listing decides which branch is read, so a wiki that cannot be listed fails here rather
  # than falling back: master would be the wrong pages for a pull request that carries its own.
  if ! heads=$(git ls-remote --heads "$url"); then
    echo "::error::The wiki's branches could not be listed at $url." >&2
    return 1
  fi

  if has_branch "$heads" "$branch"; then
    git clone --quiet --depth 1 --branch="$branch" -- "$url" "$destination" || return 1
    echo "The wiki is read at its branch '$branch', named like this pull request's."
  else
    git clone --quiet --depth 1 -- "$url" "$destination" || return 1
    if [ -n "$branch" ]; then
      echo "The wiki has no branch named '$branch', so it is read at its default branch."
    else
      echo "Not a pull request, so the wiki is read at its default branch."
    fi
  fi
  echo "Checked out: $(git -C "$destination" rev-parse --abbrev-ref HEAD) at $(git -C "$destination" rev-parse --short HEAD)."
}

# Builds a fixture wiki whose default branch and whose pull request branch hold different text in
# one page, and asserts which text each run reads: a run proves what it read by the page, not by an
# exit code. Each run is this script in a process of its own, as CI runs it: called as a function
# inside an `if`, bash would switch `set -e` off in it, and the test would run a different script.
self_test() {
  local self=${BASH_SOURCE[0]}
  local work
  work=$(mktemp -d)
  # shellcheck disable=SC2064 # expanded now, on purpose: $work is local to this function
  trap "rm -rf '$work'" EXIT

  local fixture="$work/wiki"
  git init --quiet "$fixture"
  git -C "$fixture" symbolic-ref HEAD refs/heads/master
  git -C "$fixture" config user.name "Self Test"
  git -C "$fixture" config user.email "self-test@example.invalid"
  git -C "$fixture" config commit.gpgsign false
  printf 'master\n' > "$fixture/Page.md"
  git -C "$fixture" add Page.md
  git -C "$fixture" commit --quiet -m "the default branch"
  git -C "$fixture" checkout --quiet -b fix/some-change
  printf 'branch\n' > "$fixture/Page.md"
  git -C "$fixture" commit --quiet -am "a pull request's pages"
  git -C "$fixture" checkout --quiet master
  # Two more pull requests' pages, for a merge group: one that adds a page beside the first's, and one
  # that changes the first's line another way, which cannot merge with it.
  git -C "$fixture" checkout --quiet -b feat/second
  printf 'second\n' > "$fixture/Second.md"
  git -C "$fixture" add Second.md
  git -C "$fixture" commit --quiet -m "another pull request's page"
  git -C "$fixture" checkout --quiet master
  git -C "$fixture" checkout --quiet -b feat/conflict
  printf 'conflict\n' > "$fixture/Page.md"
  git -C "$fixture" commit --quiet -am "the same line, another way"
  git -C "$fixture" checkout --quiet master
  # And the pages of a pull request the queue already put on main, whose wiki branch nobody has merged yet.
  git -C "$fixture" checkout --quiet -b feat/landed
  printf 'landed\n' > "$fixture/Landed.md"
  git -C "$fixture" add Landed.md
  git -C "$fixture" commit --quiet -m "a merged pull request's page, waiting"
  git -C "$fixture" checkout --quiet master

  # `gh api` stands in with the answers each case writes, already as its --jq prints them, and refuses
  # a call for another repository or another range, so the arguments are checked too.
  mkdir -p "$work/stub"
  cat > "$work/stub/gh" <<'STUB'
#!/usr/bin/env bash
[ "$1" = api ] || exit 2
case "$2" in
  "repos/eQuantic/equantic-ui/compare/base1...head1") cat "$EQ_STUB/subjects" ;;
  "repos/eQuantic/equantic-ui/pulls?state=closed&base=main&sort=updated&direction=desc&per_page=100") cat "$EQ_STUB/landed" ;;
  repos/eQuantic/equantic-ui/pulls/*) number=${2##*/}; [ -f "$EQ_STUB/pull-$number" ] || exit 1; cat "$EQ_STUB/pull-$number" ;;
  *) exit 1 ;;
esac
STUB
  chmod +x "$work/stub/gh"

  local failures=0 runs=0
  expect() {
    local name=$1 branch=$2 want=$3 got
    runs=$((runs + 1))
    rm -rf "$work/out"
    if ! EQ_WIKI_URL="file://$fixture" EQ_WIKI_BRANCH="$branch" bash "$self" "$work/out" > /dev/null 2>&1; then
      echo "FAIL: $name: the checkout failed" >&2
      failures=$((failures + 1))
      return
    fi
    got=$(cat "$work/out/Page.md" 2> /dev/null) || got="(no page)"
    if [ "$got" = "$want" ]; then
      echo "ok: $name"
    else
      echo "FAIL: $name: read '$got', expected '$want'" >&2
      failures=$((failures + 1))
    fi
  }

  expect "a pull request with a wiki branch reads it" "fix/some-change" "branch"
  expect "a pull request without one reads the default branch" "feat/nothing-here" "master"
  expect "a run outside a pull request reads the default branch" "" "master"
  expect "a name that is only the tail of a branch is not that branch" "some-change" "master"
  expect "a name that is not a ref at all reads the default branch" '$(touch pwned) `id` ;x' "master"

  # A merge group (#1308): the pull requests its commits name, each one's head from the API, after the
  # ones already merged whose wiki branch still waits. The pages read are asserted, a fork's branch is
  # never merged, and a failure leaves nothing behind.
  queue() {
    local name=$1 ref=$2 want_page=$3 want_second=${4:-} want_landed=${5:-"(no page)"}
    runs=$((runs + 1))
    rm -rf "$work/out"
    if ! PATH="$work/stub:$PATH" EQ_STUB="$work/answers" GITHUB_REPOSITORY="eQuantic/equantic-ui" \
      EQ_WIKI_URL="file://$fixture" EQ_QUEUE_BASE=base1 EQ_QUEUE_HEAD=head1 EQ_QUEUE_REF="$ref" \
      bash "$self" "$work/out" > /dev/null 2>&1; then
      if [ "$want_page" = "(fails)" ] && [ ! -e "$work/out" ]; then
        echo "ok: $name"
      elif [ "$want_page" = "(fails)" ]; then
        echo "FAIL: $name: it failed and left a directory behind" >&2
        failures=$((failures + 1))
      else
        echo "FAIL: $name: the checkout failed" >&2
        failures=$((failures + 1))
      fi
      return
    fi
    if [ "$want_page" = "(fails)" ]; then
      echo "FAIL: $name: the checkout succeeded" >&2
      failures=$((failures + 1))
      return
    fi
    local page second landed
    page=$(cat "$work/out/Page.md" 2> /dev/null) || page="(no page)"
    second=$(cat "$work/out/Second.md" 2> /dev/null) || second="(no page)"
    landed=$(cat "$work/out/Landed.md" 2> /dev/null) || landed="(no page)"
    if [ "$page" = "$want_page" ] && [ "$second" = "$want_second" ] && [ "$landed" = "$want_landed" ]; then
      echo "ok: $name"
    else
      echo "FAIL: $name: read Page '$page', Second '$second' and Landed '$landed'," \
        "expected '$want_page', '$want_second' and '$want_landed'" >&2
      failures=$((failures + 1))
    fi
  }
  # The group's commits for the next case, and the pull requests already merged (none unless a case
  # says so); the answers about each pull request stay, as the API's would.
  answers() {
    mkdir -p "$work/answers"
    printf '%s\n' "$@" > "$work/answers/subjects"
    : > "$work/answers/landed"
  }
  pull() { printf '%s\t%s\n' "$2" "$3" > "$work/answers/pull-$1"; }

  answers "🔧 chore: the first (#1)" "✨ feat: the second (#2)"
  pull 1 eQuantic/equantic-ui fix/some-change
  pull 2 eQuantic/equantic-ui feat/second
  queue "a merge group reads master with each pull request's pages merged" \
    "refs/heads/gh-readonly-queue/main/pr-2-abc123" "branch" "second"

  answers "🔧 chore: the first (#1)" "✨ feat: the fourth (#4)" "🐛 fix: the seventh (#7)"
  pull 4 someone/fork feat/second
  pull 7 eQuantic/equantic-ui '$(touch pwned) `id` ;x'
  queue "a fork's pull request in a group is not merged, and a name that is not a branch is not run" \
    "refs/heads/gh-readonly-queue/main/pr-7-abc123" "branch" "(no page)"

  answers "🐛 fix: an inner commit that cites an issue (#406)" "Merge pull request #2 from eQuantic/feat/second"
  queue "with the merge method, only the merge commits name the group's pull requests" \
    "refs/heads/gh-readonly-queue/main/pr-2-abc123" "master" "second"

  answers "✨ feat: the second (#2)"
  printf '%s\t%s\n' "eQuantic/equantic-ui" "feat/landed" "someone/fork" "feat/conflict" > "$work/answers/landed"
  queue "a pull request already merged, whose wiki branch waits, is read too, and a fork's is not" \
    "refs/heads/gh-readonly-queue/main/pr-2-abc123" "master" "second" "landed"

  answers "✨ feat: the eighth (#8)"
  printf '%s\t%s\r\n' "eQuantic/equantic-ui" "feat/second" > "$work/answers/pull-8"
  queue "a branch the API answers with a carriage return, as on Windows, is still the branch" \
    "refs/heads/gh-readonly-queue/main/pr-8-abc123" "master" "second"

  answers "🔧 chore: the first (#1)" "✨ feat: the second (#2)"
  queue "a group whose commits do not end with the pull request its ref names fails" \
    "refs/heads/gh-readonly-queue/main/pr-9-abc123" "(fails)"

  answers "🔧 chore: a commit no pull request wrote"
  queue "a group whose commits name no pull request fails" \
    "refs/heads/gh-readonly-queue/main/pr-1-abc123" "(fails)"

  answers "🔧 chore: the first (#1)" "🔧 chore: a commit no pull request wrote"
  queue "a group whose last commit names no pull request fails, though an earlier one names the ref's" \
    "refs/heads/gh-readonly-queue/main/pr-1-abc123" "(fails)"

  answers "🔧 chore: a squash with no number" "✨ feat: the second (#2)"
  queue "a squash group with a commit that names no pull request fails" \
    "refs/heads/gh-readonly-queue/main/pr-2-abc123" "(fails)"

  answers "✨ feat: the second (#2)"
  queue "a ref that is not a merge queue's fails" "refs/heads/main" "(fails)"

  answers "🔧 chore: the first (#1)" "🐛 fix: the fifth (#5)"
  pull 5 eQuantic/equantic-ui feat/conflict
  queue "two wiki branches that conflict fail, and leave nothing behind" \
    "refs/heads/gh-readonly-queue/main/pr-5-abc123" "(fails)"

  # A wiki that cannot be reached fails, and leaves nothing behind that a guard could read as master.
  runs=$((runs + 1))
  if EQ_WIKI_URL="file://$work/absent" EQ_WIKI_BRANCH="fix/some-change" bash "$self" "$work/none" > /dev/null 2>&1; then
    echo "FAIL: an unreachable wiki was checked out" >&2
    failures=$((failures + 1))
  elif [ -e "$work/none" ]; then
    echo "FAIL: an unreachable wiki left a directory behind" >&2
    failures=$((failures + 1))
  else
    echo "ok: an unreachable wiki fails, and leaves nothing behind"
  fi

  # A wiki whose branches cannot be listed fails even when it could be cloned: reading master then
  # would be the wrong pages for a pull request that carries its own. A `git` that refuses only
  # `ls-remote` stands in for a listing that fails while the clone would work.
  runs=$((runs + 1))
  local real_git
  real_git=$(command -v git)
  mkdir -p "$work/bin"
  printf '#!/usr/bin/env bash\n[ "$1" = ls-remote ] && exit 128\nexec "%s" "$@"\n' "$real_git" > "$work/bin/git"
  chmod +x "$work/bin/git"
  if PATH="$work/bin:$PATH" EQ_WIKI_URL="file://$fixture" EQ_WIKI_BRANCH="fix/some-change" bash "$self" "$work/unlisted" > /dev/null 2>&1; then
    echo "FAIL: a wiki whose branches could not be listed was read at its default branch" >&2
    failures=$((failures + 1))
  elif [ -e "$work/unlisted" ]; then
    echo "FAIL: a wiki whose branches could not be listed left a directory behind" >&2
    failures=$((failures + 1))
  else
    echo "ok: a wiki whose branches cannot be listed fails, even when it could be cloned"
  fi

  # The branch name above was never run: nothing it names exists.
  runs=$((runs + 1))
  if [ -e pwned ] || [ -e "$work/pwned" ]; then
    echo "FAIL: a branch name was executed" >&2
    failures=$((failures + 1))
  else
    echo "ok: a branch name is never executed"
  fi

  if [ "$failures" -ne 0 ]; then
    echo "::error::The wiki checkout's self-test failed $failures of $runs checks." >&2
    return 1
  fi
  echo "The wiki checkout's self-test passed all $runs checks."
}

case "${1:-}" in
  --self-test) self_test ;;
  "" | -*) echo "usage: $0 <destination> | --self-test" >&2; exit 2 ;;
  *) checkout "$1" ;;
esac
