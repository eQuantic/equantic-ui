#!/usr/bin/env bash
# The part of the NuGet cache's key that says which packages a restore needs (#1312): a hash of every
# line of the repository that declares a package or a version, and of nothing else. Hashing whole
# project files changed the key at every edit of one, a property or a new <Compile Remove>, so the cache
# missed for a reason that changes no package; this key moves only when a package or a version does.
#
# Read: the package items (PackageReference, PackageVersion, PackageDownload, GlobalPackageReference)
# and every `...Version` property in the project, props and targets files, and global.json and the
# packages.lock.json files whole. Build outputs and node_modules are skipped.
#
#   scripts/nuget-cache-key.sh              print the key for this tree
#   scripts/nuget-cache-key.sh --self-test  prove what moves the key and what does not
#
# EQ_KEY_ROOT  the tree read, `.` by default; the self-test points it at its fixture.
set -euo pipefail

key() {
  local root=${EQ_KEY_ROOT:-.} files lines
  files=$(find "$root" \( -name bin -o -name obj -o -name node_modules -o -name .git \) -prune -o \
    -type f \( -name '*.csproj' -o -name '*.props' -o -name '*.targets' -o -name global.json -o -name packages.lock.json \) \
    -print) || return 1
  [ -n "$files" ] || { echo "::error::No file under $root declares a package." >&2; return 1; }
  lines=$(while IFS= read -r file; do
    case "$file" in
      (*/global.json | */packages.lock.json) sed "s#^#${file#"$root"/}: #" "$file" ;;
      (*) /usr/bin/grep -hoE '<(PackageReference|PackageVersion|PackageDownload|GlobalPackageReference)[[:space:]][^>]*>|<[A-Za-z]*Version>[^<]*</[A-Za-z]*Version>' "$file" \
           | sed "s#^#${file#"$root"/}: #" || true ;;
    esac
  done <<< "$files" | LC_ALL=C sort)
  if command -v sha256sum > /dev/null 2>&1; then
    sha256sum <<< "$lines" | cut -c1-16
  else
    shasum -a 256 <<< "$lines" | cut -c1-16
  fi
}

self_test() {
  local work
  work=$(mktemp -d)
  # shellcheck disable=SC2064 # expanded now, on purpose: $work is local to this function
  trap "rm -rf '$work'" EXIT
  mkdir -p "$work/src/A" "$work/src/A/bin"
  printf '{ "sdk": { "version": "10.0.100" } }\n' > "$work/global.json"
  printf '<Project><PropertyGroup><RoslynVersion>4.12.0</RoslynVersion><Version>0.2.0</Version></PropertyGroup></Project>\n' > "$work/Directory.Build.props"
  printf '<Project><ItemGroup>\n  <PackageReference Include="Xunit" Version="2.9.0" />\n</ItemGroup></Project>\n' > "$work/src/A/A.csproj"
  local base got failures=0
  base=$(EQ_KEY_ROOT="$work" key)
  moves() {
    local name=$1 file=$2 from=$3 to=$4 want=$5
    local saved
    saved=$(cat "$file")
    printf '%s\n' "${saved/"$from"/"$to"}" > "$file"
    got=$(EQ_KEY_ROOT="$work" key)
    printf '%s\n' "$saved" > "$file"
    if { [ "$want" = moves ] && [ "$got" != "$base" ]; } || { [ "$want" = stays ] && [ "$got" = "$base" ]; }; then
      echo "ok: $name"
    else
      echo "FAIL: $name: the key $([ "$got" = "$base" ] && echo stayed || echo moved)" >&2
      failures=$((failures + 1))
    fi
  }
  moves "a package's version moves the key" "$work/src/A/A.csproj" 'Version="2.9.0"' 'Version="2.9.2"' moves
  moves "a new package moves the key" "$work/src/A/A.csproj" '</ItemGroup>' '<PackageReference Include="Moq" Version="4.20.0" /></ItemGroup>' moves
  moves "a version property moves the key" "$work/Directory.Build.props" '4.12.0' '4.13.0' moves
  moves "the SDK's version moves the key" "$work/global.json" '10.0.100' '10.0.200' moves
  moves "an edit that declares no package keeps the key" "$work/src/A/A.csproj" '</ItemGroup>' '<Compile Remove="X.cs" /></ItemGroup>' stays
  # A project under a build's output is not the repository's.
  printf '<Project><ItemGroup><PackageReference Include="Old" Version="1.0.0" /></ItemGroup></Project>\n' > "$work/src/A/bin/Copy.csproj"
  got=$(EQ_KEY_ROOT="$work" key)
  if [ "$got" = "$base" ]; then echo "ok: a project under bin/ keeps the key"; else echo "FAIL: a project under bin/ moved the key" >&2; failures=$((failures + 1)); fi
  if [ "$failures" -ne 0 ]; then
    echo "::error::The NuGet cache key's self-test failed $failures checks." >&2
    return 1
  fi
  echo "The NuGet cache key's self-test passed."
}

case "${1:-}" in
  "") key ;;
  --self-test) self_test ;;
  *) echo "usage: $0 [--self-test]" >&2; exit 2 ;;
esac
