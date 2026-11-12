#!/usr/bin/env bash
# What a change can affect, so CI runs that and skips what cannot fail because of it (#1421).
#
# Reads the paths a change touches, one a line on stdin, and prints its scope for $GITHUB_OUTPUT:
#
#   scope=docs        every path is a document: the build, the conformance suite, the runtime's
#                     tests, the Mac jobs, the samples and the extension cannot fail because of it.
#                     The `web` and `rest` test groups still run: Web.Tests, Compiler.Tests and
#                     Native.Engine.Tests read docs/, the ledger, the design handoff, the wiki and
#                     CLAUDE.md, and hold them.
#   scope=extension   every path is the VS Code extension's: only the extension is built.
#   scope=full        anything else, and whenever this cannot tell. It fails OPEN: a path it does
#                     not know, an empty list (a diff that could not be read) or a mix is `full`.
#
# and three switches the jobs read, each "false" only when the job's work cannot be affected, so an
# empty or missing output (this job failed) runs everything:
#
#   code          the build, the runtime's tests, conformance, the Mac jobs and the samples
#   dotnet_tests  the .NET test groups at all
#   extension     the VS Code extension
#
#   scripts/ci-changes.sh < paths       classify
#   scripts/ci-changes.sh --self-test   prove the classification against its cases
set -euo pipefail

# A document: read by people and by the docs guards, built into nothing.
#   - Markdown anywhere but where it is packed or compiled (src/, tests/, samples/, templates/,
#     extensions/), and never the root README.md, which every package carries as its readme;
#   - docs/ whole (the design handoff's tokens included: the tests that read them run in `docs`);
#   - openspec/ and promotion/;
#   - the issue and pull request templates and Copilot's instructions;
#   - the skills and commands of .claude/ (its hooks and settings are the session-start job's).
is_doc() {
  case "$1" in
    README.md) return 1 ;;
    src/*|tests/*|samples/*|templates/*|extensions/*) return 1 ;;
    docs/*|openspec/*|promotion/*) return 0 ;;
    .github/ISSUE_TEMPLATE/*|.github/pull_request_template.md) return 0 ;;
    .github/copilot-instructions.md|.github/instructions/*) return 0 ;;
    .claude/skills/*|.claude/commands/*) return 0 ;;
    *.md) return 0 ;;
    *) return 1 ;;
  esac
}

is_extension() {
  case "$1" in
    extensions/vscode/*) return 0 ;;
    *) return 1 ;;
  esac
}

# The scope of the paths on stdin.
classify() {
  local path docs=0 extension=0 other=0 total=0
  while IFS= read -r path || [ -n "$path" ]; do
    [ -z "$path" ] && continue
    total=$((total + 1))
    if is_doc "$path"; then docs=$((docs + 1))
    elif is_extension "$path"; then extension=$((extension + 1))
    else other=$((other + 1))
    fi
  done
  if [ "$total" -eq 0 ] || [ "$other" -gt 0 ]; then echo full
  elif [ "$docs" -eq "$total" ]; then echo docs
  elif [ "$extension" -eq "$total" ]; then echo extension
  else echo full
  fi
}

outputs() {
  local scope=$1
  echo "scope=$scope"
  case "$scope" in
    docs) echo "code=false"; echo "dotnet_tests=true"; echo "extension=false" ;;
    extension) echo "code=false"; echo "dotnet_tests=false"; echo "extension=true" ;;
    *) echo "code=true"; echo "dotnet_tests=true"; echo "extension=true" ;;
  esac
}

self_test() {
  local failures=0 cases=0
  check() {
    local expected=$1 actual
    shift
    cases=$((cases + 1))
    actual=$(printf '%s\n' "$@" | classify)
    if [ "$actual" != "$expected" ]; then
      echo "FAIL: expected $expected, got $actual, for: $*" >&2
      failures=$((failures + 1))
    fi
  }
  check_empty() {
    cases=$((cases + 1))
    local actual
    actual=$(printf '' | classify)
    [ "$actual" = full ] || { echo "FAIL: an empty list is $actual, not full" >&2; failures=$((failures + 1)); }
  }

  # Documents.
  check docs docs/PRINCIPLES.md
  check docs docs/ledger/an-entry.md CONTRIBUTING.md
  check docs docs/design/tokens.json
  check docs openspec/specs/working-agreement/spec.md openspec/changes/archive/2026-11-11-x/proposal.md
  check docs promotion/wallet-video/effectcraft/wallet.ecproj
  check docs .github/pull_request_template.md .github/ISSUE_TEMPLATE/bug_report.yml
  check docs .github/copilot-instructions.md .github/instructions/compiler.instructions.md
  check docs .claude/skills/roadmap/SKILL.md
  check docs CLAUDE.md AGENTS.md ROADMAP.md

  # The extension.
  check extension extensions/vscode/package.json extensions/vscode/src/extension.ts

  # Code, and everything this cannot vouch for.
  check full README.md
  check full src/eQuantic.UI.Compiler/README.md
  check full tests/eQuantic.UI.Web.Tests/Fixtures/notes.md
  check full samples/WalletWeb/README.md
  check full templates/equantic-app/README.md
  check full .github/workflows/ci.yml
  check full .claude/hooks/session-start.sh
  check full .claude/settings.json
  check full scripts/ci-changes.sh
  check full Directory.Build.props
  check full docs/PRINCIPLES.md src/eQuantic.UI.Web/WebRealizer.cs
  check full docs/PRINCIPLES.md extensions/vscode/package.json
  check full assets/readme/web-dashboard.png
  check full LICENSE
  check_empty

  # The switches: "false" only where the work cannot be affected.
  [ "$(outputs docs | grep '^code=')" = "code=false" ] || { echo "FAIL: docs builds code" >&2; failures=$((failures + 1)); }
  [ "$(outputs docs | grep '^dotnet_tests=')" = "dotnet_tests=true" ] || { echo "FAIL: docs skips the tests that read documents" >&2; failures=$((failures + 1)); }
  [ "$(outputs extension | grep '^dotnet_tests=')" = "dotnet_tests=false" ] || { echo "FAIL: the extension runs the .NET tests" >&2; failures=$((failures + 1)); }
  [ "$(outputs full | grep -c '=false')" -eq 0 ] || { echo "FAIL: full skips something" >&2; failures=$((failures + 1)); }
  cases=$((cases + 4))

  if [ "$failures" -ne 0 ]; then
    echo "ci-changes self-test: $failures of $cases cases failed" >&2
    exit 1
  fi
  echo "ci-changes self-test: $cases cases pass"
}

case "${1:-}" in
  --self-test) self_test ;;
  "") outputs "$(classify)" ;;
  *) echo "usage: scripts/ci-changes.sh [--self-test] < paths" >&2; exit 2 ;;
esac
