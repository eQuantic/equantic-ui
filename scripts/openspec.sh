#!/usr/bin/env bash
# Runs the OpenSpec CLI this repository pins, and nothing else: the version is the one
# tools/openspec/package.json names and tools/openspec/package-lock.json locks, integrity hashes
# included, so every machine and every CI run validates the specs with the same CLI.
#
#   ./scripts/openspec.sh validate --all --strict
#   ./scripts/openspec.sh list --specs
#
# The CLI is installed from the lockfile on first use, and again whenever the installed version is
# not the pinned one. Telemetry is off. It needs Node >= 20.19 (the CLI's own engine range); the
# build of the SDK itself still needs nothing but the .NET SDK.
set -euo pipefail

root="$(cd "$(dirname "$0")/.." && pwd)"
dir="$root/tools/openspec"

command -v node >/dev/null 2>&1 || {
    echo "openspec.sh: Node >= 20.19 is required to run the OpenSpec CLI, and node is not on PATH" >&2
    exit 1
}
# The CLI's own engine range. npm treats an engine mismatch as advisory, so an older Node would
# install and then run it on a runtime it does not support; refuse that here, in the same words.
node -e 'const [a, b] = process.versions.node.split(".").map(Number); process.exit(a > 20 || (a === 20 && b >= 19) ? 0 : 1)' || {
    echo "openspec.sh: Node >= 20.19 is required to run the OpenSpec CLI, and this is Node $(node -p process.versions.node)" >&2
    exit 1
}

# Read both versions through the environment, never by splicing a path into a script.
want="$(P="$dir/package.json" node -p 'require(process.env.P).devDependencies["@fission-ai/openspec"]')"

# An install is healthy when the CLI RUNS and answers the pinned version. Reading its package.json
# alone passed a node_modules that kept the manifest and lost the executable, or a dependency; running
# it is the one check that covers every way the tree can be incomplete.
have="$(OPENSPEC_TELEMETRY=0 "$dir/node_modules/.bin/openspec" --version 2>/dev/null || true)"
if [ "$have" != "$want" ]; then
    # npm ci installs exactly what the lockfile names and fails on an integrity mismatch. Its output
    # goes to stderr so the CLI's own stdout stays the only stdout.
    npm ci --prefix "$dir" --no-audit --no-fund --loglevel=error >&2
fi

export OPENSPEC_TELEMETRY=0
exec "$dir/node_modules/.bin/openspec" "$@"
