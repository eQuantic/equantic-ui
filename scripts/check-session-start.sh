#!/usr/bin/env bash
# Runs .claude/hooks/session-start.sh the way a fresh cloud container runs it, and asserts every
# promise it makes. The hook only acts in cloud sessions, so without this nothing would notice the
# day it stopped working: a laptop never takes that path.
#
#   ./scripts/check-session-start.sh     # Linux; CI's session-start job runs it on every push
#
# The container is emulated in a throwaway HOME and a throwaway clone of HEAD:
#   - a global git config with somebody else's identity and commit signing ON, through a key that
#     does not exist, which is what makes an unprepared commit fail;
#   - no dotnet on PATH, so the pinned SDK has to be downloaded and verified.
# Two controls keep the check honest: a commit made BEFORE the hook must fail (or the emulation
# proves nothing), and a copy of the hook with a wrong SHA-256 must refuse the SDK.
set -euo pipefail

root="$(cd "$(dirname "$0")/.." && pwd)"
[ "$(uname -s)" = "Linux" ] || { echo "check-session-start.sh: Linux only (it emulates a cloud container)" >&2; exit 2; }

work="$(mktemp -d)"
trap 'rm -rf "$work"' EXIT
failures=0
fail() { echo "FAIL: $1" >&2; failures=$((failures + 1)); }
pass() { echo "ok: $1"; }

# The container's own git setup: another identity, and signing required with a key that is not there.
container_home() {
    local home="$1"
    mkdir -p "$home"
    cat > "$home/.gitconfig" <<'EOF'
[user]
    name = Container Identity
    email = container@example.invalid
    signingkey = /nonexistent/signing-key
[gpg]
    format = ssh
[commit]
    gpgsign = true
EOF
}

# The PATH of a container that ships no .NET SDK. Dropping the directories named after dotnet is not
# enough, because a runner also links `dotnet` into /usr/bin; so a stub that answers like a missing
# SDK comes first, and whatever else is on PATH can no longer be the one the hook finds.
shadow="$work/shadow"
mkdir -p "$shadow"
printf '#!/bin/sh\necho "dotnet: not installed in this container" >&2\nexit 127\n' > "$shadow/dotnet"
chmod +x "$shadow/dotnet"
path_without_dotnet="$shadow:$(printf '%s' "$PATH" | tr ':' '\n' | grep -v -i dotnet | paste -sd: -)"

git clone --quiet --no-hardlinks "$root" "$work/repo"
container_home "$work/home"

# Control 0: the emulated container really has no SDK, or the install path is never exercised.
if (cd "$work/repo" && env -u DOTNET_ROOT PATH="$path_without_dotnet" dotnet --version >/dev/null 2>&1); then
    fail "dotnet answers before the hook runs, so the fixture would not exercise the pinned download"
else
    pass "before the hook, no .NET SDK answers"
fi

# Control 1: without the hook, the container's config must make a commit fail.
if (cd "$work/repo" && HOME="$work/home" git commit --allow-empty -q -m "control" 2>/dev/null); then
    fail "a commit succeeded before the hook ran, so the emulated container does not sign and proves nothing"
else
    pass "before the hook, the container's signing config refuses a commit"
fi

env_file="$work/claude-env"
: > "$env_file"
out="$(cd "$work/repo" && env -u DOTNET_ROOT HOME="$work/home" PATH="$path_without_dotnet" \
    CLAUDE_CODE_REMOTE=true CLAUDE_PROJECT_DIR="$work/repo" CLAUDE_ENV_FILE="$env_file" \
    bash .claude/hooks/session-start.sh)"
printf '%s\n' "$out"

# Every later command of the session sources the env file, so the assertions do too.
session() {
    (cd "$work/repo" && env -u DOTNET_ROOT HOME="$work/home" PATH="$path_without_dotnet" \
        bash -c 'source "$1"; shift; "$@"' _ "$env_file" "$@")
}

[ "$(session git config --get user.name)" = "Edgar Mesquita" ] && pass "user.name" || fail "user.name is not Edgar Mesquita"
[ "$(session git config --get user.email)" = "edgar@equantic.tech" ] && pass "user.email" || fail "user.email is not edgar@equantic.tech"
[ "$(session git config --get commit.gpgsign)" = "false" ] && pass "commit.gpgsign=false" || fail "commit signing is still on"

if session git commit --allow-empty -q -m "✅ test: the session-start fixture's commit"; then
    ident="$(session git log -1 --format='%an <%ae>|%cn <%ce>|%G?')"
    [ "$ident" = "Edgar Mesquita <edgar@equantic.tech>|Edgar Mesquita <edgar@equantic.tech>|N" ] \
        && pass "a commit is authored and committed by the owner, unsigned" \
        || fail "the commit reads '$ident'"
else
    fail "a commit failed after the hook ran"
fi

sdk="$(session dotnet --version 2>/dev/null || true)"
[ "$sdk" = "10.0.101" ] && pass ".NET SDK $sdk from the pinned archive" || fail "dotnet --version answered '$sdk'"
resolved="$(session bash -c 'command -v dotnet' 2>/dev/null || true)"
[ "$resolved" = "$work/home/.dotnet/dotnet" ] \
    && pass "the session's dotnet is the one installed into HOME/.dotnet" \
    || fail "the session resolves dotnet to '$resolved', not HOME/.dotnet"

want_openspec="$(P="$root/tools/openspec/package.json" node -p 'require(process.env.P).devDependencies["@fission-ai/openspec"]')"
got_openspec="$(session openspec --version 2>/dev/null || true)"
[ "$got_openspec" = "$want_openspec" ] && pass "openspec $got_openspec on PATH" || fail "openspec --version answered '$got_openspec', want $want_openspec"
[ "$(session printenv OPENSPEC_TELEMETRY)" = "0" ] && pass "OPENSPEC_TELEMETRY=0" || fail "OpenSpec telemetry is not off"

# Control 2: a hook whose pinned digest is wrong must refuse the archive and install nothing.
container_home "$work/home2"
sed 's/^DOTNET_SHA256_LINUX_X64=.*/DOTNET_SHA256_LINUX_X64="0000000000000000000000000000000000000000000000000000000000000000"/;
     s/^DOTNET_SHA256_LINUX_ARM64=.*/DOTNET_SHA256_LINUX_ARM64="0000000000000000000000000000000000000000000000000000000000000000"/' \
    "$work/repo/.claude/hooks/session-start.sh" > "$work/tampered-hook.sh"
tampered="$(cd "$work/repo" && env -u DOTNET_ROOT HOME="$work/home2" PATH="$path_without_dotnet" \
    CLAUDE_CODE_REMOTE=true CLAUDE_PROJECT_DIR="$work/repo" CLAUDE_ENV_FILE="$work/claude-env2" \
    bash "$work/tampered-hook.sh")"
if printf '%s' "$tampered" | grep -q "SHA-256 mismatch, refused" && [ ! -e "$work/home2/.dotnet" ]; then
    pass "a wrong SHA-256 refuses the archive and installs nothing"
else
    fail "a hook with a wrong SHA-256 did not refuse the archive"
fi

if [ "$failures" -ne 0 ]; then
    echo "$failures check(s) failed" >&2
    exit 1
fi
echo "session-start: every promise holds"
