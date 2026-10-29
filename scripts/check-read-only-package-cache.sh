#!/usr/bin/env bash
# A web app builds from packages whose cache nobody may write (#364).
#
#   scripts/check-read-only-package-cache.sh <folder of .nupkg files>
#
# The SDK unpacked the embedded bun beside its zip, inside the NuGet package cache, on an app's first
# build. A cache restored read-only by a CI, baked into a container image or shared on a volume failed
# that build at the unzip. So this scaffolds the web template from the packages in the folder, restores
# it into a cache of its own, takes write permission away from that cache, and builds. It fails unless
# the build succeeds, the cache holds exactly the files it held before, the bun the build ran sits
# outside the cache, and the build wrote the app's modules. macOS and Linux: chmod is the read-only.
set -euo pipefail

feed=$(cd "${1:?usage: $0 <folder of .nupkg files>}" && pwd)
work=$(mktemp -d)
packages="$work/packages"
trap 'chmod -R u+w "$work" 2>/dev/null || true; rm -rf "$work"' EXIT

export NUGET_PACKAGES="$packages"
export DOTNET_NOLOGO=1

# A hive of its own, so a developer's installed templates are not touched.
hive="$work/hive"
dotnet new install "$feed"/eQuantic.UI.Templates.[0-9]*.nupkg --debug:custom-hive "$hive" > /dev/null
dotnet new equantic-app -n ReadOnlyCache -o "$work/app" --debug:custom-hive "$hive" > /dev/null

# Every eQuantic package from the folder alone: between releases the version in the tree is one
# nuget.org already lists, and a restore that could take it from there would build the released SDK.
cat > "$work/app/nuget.config" <<EOF
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources>
    <clear />
    <add key="feed" value="$feed" />
    <add key="nuget.org" value="https://api.nuget.org/v3/index.json" />
  </packageSources>
  <packageSourceMapping>
    <packageSource key="feed">
      <package pattern="eQuantic.*" />
    </packageSource>
    <packageSource key="nuget.org">
      <package pattern="*" />
    </packageSource>
  </packageSourceMapping>
</configuration>
EOF

dotnet restore "$work/app" > /dev/null
test -d "$packages/equantic.ui.sdk" || { echo "::error::the restore put no SDK into $packages"; exit 1; }

chmod -R a-w "$packages"
before=$(cd "$packages" && find . | LC_ALL=C sort)

if ! dotnet build "$work/app" --no-restore -c Release -nologo > "$work/build.log" 2>&1; then
    cat "$work/build.log"
    echo "::error::the app did not build against a read-only package cache"
    exit 1
fi

after=$(cd "$packages" && find . | LC_ALL=C sort)
if [ "$before" != "$after" ]; then
    diff <(echo "$before") <(echo "$after") || true
    echo "::error::the build wrote into the package cache"
    exit 1
fi

# The bun the build resolved, which Sdk.targets says at high importance, is a file outside the cache.
bun=$(sed -n "s/.*BunPath resolved to '\(.*\)'.*/\1/p" "$work/build.log" | head -1)
if [ -z "$bun" ] || [ ! -x "$bun" ]; then
    cat "$work/build.log"
    echo "::error::the build resolved no bun that runs ('$bun')"
    exit 1
fi
case "$bun" in "$packages"/*)
    echo "::error::the bun ran from inside the package cache: $bun"
    exit 1
esac

# And the bun did its work: the app's modules, beside the runtime the Server package ships.
modules=$(find "$work/app/wwwroot/_equantic" -maxdepth 1 -name '*.js' ! -name runtime.js | wc -l | tr -d ' ')
if [ "$modules" -eq 0 ]; then
    echo "::error::the build wrote no module of the app under wwwroot/_equantic"
    exit 1
fi

echo "Built against a read-only package cache: $modules module(s), bun from $bun."
