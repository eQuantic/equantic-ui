#!/usr/bin/env bash
# The test projects under a tree, as CI finds them: every .csproj outside a build's output, sorted.
# Sourced by scripts/test-groups.sh (the Linux and Windows test jobs) and scripts/macos-tests.sh (the
# macOS one), so both job families agree on which projects exist. A `find` that fails fails the caller,
# rather than handing it the part of the tree it reached.
test_projects() {
  local root=${1:-tests} found
  found=$(find "$root" -name '*.csproj' -not -path '*/bin/*' -not -path '*/obj/*') || return 1
  if [ -n "$found" ]; then sort <<< "$found"; fi
}
