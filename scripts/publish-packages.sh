#!/usr/bin/env bash
# Pushes a release's packages to nuget.org, and finishes the list whatever nuget.org does on the
# way (#759).
#
# The loop this replaces ran one `dotnet nuget push` per package under `set -e`. The client tries a
# failed PUT three times within seconds, so on 2026-10-11 three 500s in a row stopped the release
# of 0.2.0-preview.61 after 33 of 50 packages, and every rerun died on a package that was already
# there before it reached one still owed: nuget.org answered 500 where a duplicate answers 409.
# So this one reads what nuget.org already lists and leaves it alone, waits before it tries a
# failed push again, goes on to the next package when one never goes through, and fails at the END,
# naming what is still owed. Only a refused key stops it at once, since every other push would be
# refused the same way.
#
#   scripts/publish-packages.sh <packages-dir> <version> [package-id ...]
#
# With no package id it takes every `*.<version>.nupkg` of the directory. An id that has no package
# there at that version is refused before anything is pushed.
#
# NUGET_API_KEY   the key. Read from the environment, handed to the client, never printed.
# PUBLISH_SOURCE  where to push. nuget.org's v3 index by default.
# PUBLISH_INDEX   the package index a restore reads, which says what is published already.
#                 scripts/check-publish-packages.sh points it at files.
# PUBLISH_WAITS   seconds to wait before each further try of one package ("30 60 120 240").
# PUBLISH_PAUSE   seconds between one package and the next (15).
#
# It runs on macos-latest in the tag's pipeline, whose /bin/bash is 3.2: no bash 4 feature here.
set -uo pipefail

usage() {
  echo "usage: publish-packages.sh <packages-dir> <version> [package-id ...]" >&2
  exit 2
}

[ $# -ge 2 ] || usage
dir=$1
version=$2
shift 2

source_url=${PUBLISH_SOURCE:-https://api.nuget.org/v3/index.json}
index_url=${PUBLISH_INDEX:-https://api.nuget.org/v3-flatcontainer}
waits=${PUBLISH_WAITS-30 60 120 240}
pause=${PUBLISH_PAUSE-15}

[ -d "$dir" ] || { echo "::error::$dir is not a directory." >&2; exit 2; }
# A version and an id become part of a file name and of a URL, and an id can be text a person typed
# into the manual workflow: anything outside a package's own alphabet is refused, never escaped.
case "$version" in ""|*[!A-Za-z0-9.+-]*) echo "::error::'$version' is not a version." >&2; exit 2 ;; esac
for seconds in $waits $pause; do
  case "$seconds" in *[!0-9]*) echo "::error::'$seconds' is not a number of seconds." >&2; exit 2 ;; esac
done
if [ -z "${NUGET_API_KEY:-}" ]; then
  echo "::error::NUGET_API_KEY is not set." >&2
  exit 1
fi

# The packages to publish, one path per line (no array: an empty one is an error under bash 3.2's
# `set -u`).
packages=""
if [ $# -gt 0 ]; then
  unknown=""
  for id in "$@"; do
    case "$id" in ""|.*|*[!A-Za-z0-9._-]*) echo "::error::'$id' is not a package id." >&2; exit 2 ;; esac
    if [ -f "$dir/$id.$version.nupkg" ]; then
      packages="$packages$dir/$id.$version.nupkg"$'\n'
    else
      unknown="$unknown $id"
    fi
  done
  if [ -n "$unknown" ]; then
    echo "::error::No package at $version in $dir for:$unknown. Nothing was pushed." >&2
    exit 1
  fi
else
  for file in "$dir"/*."$version".nupkg; do
    [ -f "$file" ] && packages="$packages$file"$'\n'
  done
  if [ -z "$packages" ]; then
    echo "::error::No package at $version in $dir. Nothing was pushed." >&2
    exit 1
  fi
fi

# Whether nuget.org lists <id> at the version already. An id it has never seen answers 404, and an
# index that cannot be read says nothing: both are "not listed", and the push then tells.
listed() {
  local lower body
  lower=$(printf '%s' "$1" | tr '[:upper:]' '[:lower:]')
  # Read whole, then matched in the shell: a `grep -q` that stops at its first match can fail the
  # pipeline under pipefail, and a listed package would then be pushed again.
  body=$(curl -fsS --max-time 30 "$index_url/$lower/index.json" 2>/dev/null) || return 1
  case "$body" in *"\"$version\""*) return 0 ;; esac
  return 1
}

output=$(mktemp)
trap 'rm -f "$output"' EXIT

# One push. Answers 0 when nuget.org has the package (pushed now, or a duplicate), 1 when the try
# failed and another is worth making, and 2 when nuget.org refused the key. The client reads
# nothing from its input, which is closed so that it cannot eat the list the loop below reads.
push() {
  dotnet nuget push "$1" --source "$source_url" --api-key "$NUGET_API_KEY" --skip-duplicate < /dev/null 2>&1 | tee "$output"
  local status=${PIPESTATUS[0]}
  [ "$status" -eq 0 ] && return 0
  # The gallery's own sentence, and the status the client prints for it.
  if grep -qE 'API key is invalid|\(Forbidden\)|\(Unauthorized\)|Forbidden https?://|Unauthorized https?://' "$output"; then
    return 2
  fi
  return 1
}

already=0
pushed=0
owed=""
first=1

while IFS= read -r package <&3; do
  [ -n "$package" ] || continue
  file=${package##*/}
  id=${file%."$version".nupkg}

  if listed "$id"; then
    echo "on nuget.org already: $id"
    already=$((already + 1))
    continue
  fi

  if [ "$first" -eq 0 ] && [ "$pause" -gt 0 ]; then sleep "$pause"; fi
  first=0

  result=1
  for delay in 0 $waits; do
    if [ "$delay" -gt 0 ]; then
      echo "waiting ${delay}s before trying $id again"
      sleep "$delay"
    fi
    push "$package"
    result=$?
    [ "$result" -eq 1 ] || break
  done

  case "$result" in
    0) pushed=$((pushed + 1)) ;;
    2)
      echo "::error::nuget.org refused the key on $id (401 or 403). Nothing else was tried: every other push would be refused the same way." >&2
      exit 1
      ;;
    *) owed="$owed $id" ;;
  esac
done 3<<EOF
$packages
EOF

echo "listed on nuget.org already: $already"
echo "pushed now, or found there by the push: $pushed"
if [ -n "$owed" ]; then
  echo "::error::Not pushed after every try:$owed. Run this again once nuget.org answers: what is listed by then is skipped." >&2
  exit 1
fi
