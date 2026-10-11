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
# naming what is still owed. A push nuget.org refuses for the key (401, 403) is not tried again,
# since waiting changes nothing, but the list goes on: a key whose scope lacks one new package id
# is refused for that package alone.
#
#   scripts/publish-packages.sh <packages-dir> <version> [package-id ...]
#
# The version is the one the release was given (the tag without its v). A package's file carries it
# as NuGet normalizes it, which is how the script looks for it: `dotnet pack` writes 1.0 as
# X.1.0.0.nupkg and 1.0.0-rc.1+build.7 as X.1.0.0-rc.1.nupkg.
#
# With no package id it takes every `*.<version>.nupkg` of the directory, and refuses a directory
# that holds a package at any other version: a release publishes everything its run packed, and a
# package left out without a word is a release that went out incomplete. An id that has no package
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
pause=${PUBLISH_PAUSE:-15}

[ -d "$dir" ] || { echo "::error::$dir is not a directory." >&2; exit 2; }
# A version and an id become part of a file name and of a URL, and an id can be text a person typed
# into the manual workflow: anything outside a package's own alphabet is refused, never escaped.
case "$version" in ""|*[!A-Za-z0-9.+-]*) echo "::error::'$version' is not a version." >&2; exit 2 ;; esac

# The version as NuGet writes it in a package's file name (NuGetVersion.ToNormalizedString, measured
# with `dotnet pack`): no build metadata, three numeric parts at least, a fourth only when it is not
# zero, and no leading zero. Matching the version as typed would call every package of such a
# release a stray, and the publish would stop before its first push.
normalized() {
  local whole=${1%%+*} release label="" part out="" count=0
  case "$whole" in
    *-*) release=${whole%%-*}; label="-${whole#*-}" ;;
    *) release=$whole ;;
  esac
  local IFS=.
  for part in $release; do
    case "$part" in ""|*[!0-9]*) return 1 ;; esac
    part=$((10#$part))
    count=$((count + 1))
    if [ "$count" -eq 4 ] && [ "$part" -eq 0 ]; then continue; fi
    out="$out${out:+.}$part"
  done
  if [ "$count" -lt 1 ] || [ "$count" -gt 4 ]; then return 1; fi
  while [ "$count" -lt 3 ]; do
    out="$out.0"
    count=$((count + 1))
  done
  printf '%s%s' "$out" "$label"
}
if ! packed=$(normalized "$version"); then
  echo "::error::'$version' is not a version NuGet packs." >&2
  exit 2
fi
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
    if [ -f "$dir/$id.$packed.nupkg" ]; then
      packages="$packages$dir/$id.$packed.nupkg"$'\n'
    else
      unknown="$unknown $id"
    fi
  done
  if [ -n "$unknown" ]; then
    echo "::error::No package at $packed in $dir for:$unknown. Nothing was pushed." >&2
    exit 1
  fi
else
  strays=""
  for file in "$dir"/*.nupkg; do
    [ -f "$file" ] || continue
    case "${file##*/}" in
      *."$packed".nupkg) packages="$packages$file"$'\n' ;;
      *) strays="$strays ${file##*/}" ;;
    esac
  done
  if [ -n "$strays" ]; then
    echo "::error::$dir holds packages that are not at $packed:$strays. Nothing was pushed." >&2
    exit 1
  fi
  if [ -z "$packages" ]; then
    echo "::error::No package at $packed in $dir. Nothing was pushed." >&2
    exit 1
  fi
fi

# Whether nuget.org lists <id> at the version already. An id it has never seen answers 404, and an
# index that cannot be read says nothing: both are "not listed", and the push then tells.
# The index writes the normalized version lower-cased, so it is compared that way.
listed_version=$(printf '%s' "$packed" | tr '[:upper:]' '[:lower:]')
listed() {
  local lower body
  lower=$(printf '%s' "$1" | tr '[:upper:]' '[:lower:]')
  # Read whole, then matched in the shell: a `grep -q` that stops at its first match can fail the
  # pipeline under pipefail, and a listed package would then be pushed again.
  body=$(curl -fsS --max-time 30 "$index_url/$lower/index.json" 2>/dev/null) || return 1
  case "$body" in *"\"$listed_version\""*) return 0 ;; esac
  return 1
}

output=$(mktemp)
trap 'rm -f "$output"' EXIT

# One push. Answers 0 when nuget.org has the package (pushed now, or a duplicate), 1 when the try
# failed and another is worth making, and 2 when nuget.org refused it for the key. The client reads
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
refused=""
first=1

while IFS= read -r package <&3; do
  [ -n "$package" ] || continue
  file=${package##*/}
  id=${file%."$packed".nupkg}

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
    2) refused="$refused $id" ;;
    *) owed="$owed $id" ;;
  esac
done 3<<EOF
$packages
EOF

echo "listed on nuget.org already: $already"
echo "pushed now, or found there by the push: $pushed"
failed=0
if [ -n "$refused" ]; then
  echo "::error::nuget.org refused the key (401 or 403), and these were not tried again:$refused. Check that the key is valid and that its scope covers them." >&2
  failed=1
fi
if [ -n "$owed" ]; then
  echo "::error::Not pushed after every try:$owed. Run this again once nuget.org answers: what is listed by then is skipped." >&2
  failed=1
fi
exit "$failed"
