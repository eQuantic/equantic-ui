#!/usr/bin/env bash
# SessionStart: prepares the session to follow the Workflow section of CLAUDE.md and AGENTS.md.
#
# Every session:
#   - the OpenSpec CLI that tools/openspec/package-lock.json pins, installed through
#     scripts/openspec.sh when it is missing, and put on PATH with telemetry off.
# A cloud container only (CLAUDE_CODE_REMOTE=true), because a laptop is already set up by its owner:
#   - the repository's git identity, Edgar Mesquita <edgar@equantic.tech>, and commit and tag
#     signing turned off, because the container's signing key is not the owner's;
#   - the .NET SDK that global.json pins;
#   - Docker, started when the container has it and allows it.
#
# Every installer this downloads is pinned by version AND SHA-256, and a mismatch refuses the
# install. The hook never fails the session: each step reports what it did or why it could not, and
# the report it prints on stdout becomes part of the session's context.
set -uo pipefail

root="${CLAUDE_PROJECT_DIR:-$(cd "$(dirname "$0")/../.." && pwd)}"
env_file="${CLAUDE_ENV_FILE:-}"
cloud=false
[ "${CLAUDE_CODE_REMOTE:-}" = "true" ] && cloud=true

# The .NET SDK global.json names, and the SHA-256 of each archive. The digests were taken from the
# archives whose SHA-512 matched Microsoft's release metadata
# (https://builds.dotnet.microsoft.com/dotnet/release-metadata/10.0/releases.json). Changing the SDK
# in global.json means changing these three lines in the same commit.
DOTNET_VERSION="10.0.101"
DOTNET_SHA256_LINUX_X64="2ba84c4f3238f4c24da2d9f6c950903e6ecbf2970aa7d1d34cbf83f24c8cdfcb"
DOTNET_SHA256_LINUX_ARM64="bfc5ab09b5cfe1061888a45ad7b4696816b2804dfb9629020aa44e632aedebe0"

OWNER_NAME="Edgar Mesquita"
OWNER_EMAIL="edgar@equantic.tech"

report=()
note() { report+=("$1"); }

# Lines for the session's later Bash commands: Claude Code sources this file before each of them.
persist() {
    [ -n "$env_file" ] && printf '%s\n' "$1" >> "$env_file"
}

sha256_of() {
    if command -v sha256sum >/dev/null 2>&1; then
        sha256sum "$1" | cut -d' ' -f1
    else
        shasum -a 256 "$1" | cut -d' ' -f1
    fi
}

prepare_openspec() {
    local bin="$root/tools/openspec/node_modules/.bin"
    if ! command -v node >/dev/null 2>&1; then
        note "OpenSpec: node is not on PATH, so the CLI is not installed (it needs Node >= 20.19)"
        return
    fi
    local version
    if ! version="$("$root/scripts/openspec.sh" --version 2>/dev/null)"; then
        note "OpenSpec: installing the pinned CLI failed; run ./scripts/openspec.sh --version to see why"
        return
    fi
    persist "export PATH=\"$bin:\$PATH\""
    persist "export OPENSPEC_TELEMETRY=0"
    note "OpenSpec $version on PATH, telemetry off"
}

configure_git() {
    git -C "$root" config user.name "$OWNER_NAME"
    git -C "$root" config user.email "$OWNER_EMAIL"
    git -C "$root" config commit.gpgsign false
    git -C "$root" config tag.gpgsign false
    # An identity given through the environment outranks every config file, so the session's own
    # commands carry the owner's too.
    persist "export GIT_AUTHOR_NAME=\"$OWNER_NAME\" GIT_AUTHOR_EMAIL=\"$OWNER_EMAIL\""
    persist "export GIT_COMMITTER_NAME=\"$OWNER_NAME\" GIT_COMMITTER_EMAIL=\"$OWNER_EMAIL\""
    # Report what git will actually use, which is what matters if something else overrides it.
    local signing
    signing="$(git -C "$root" config --get commit.gpgsign || echo unset)"
    note "git: $(git -C "$root" config --get user.name) <$(git -C "$root" config --get user.email)>, commit.gpgsign=$signing"
}

install_dotnet() {
    local dest="$HOME/.dotnet"

    # An SDK global.json accepts already answers `dotnet --version` from the repository root.
    if command -v dotnet >/dev/null 2>&1 && (cd "$root" && dotnet --version >/dev/null 2>&1); then
        note ".NET SDK $(cd "$root" && dotnet --version) already present"
        return
    fi
    if [ -x "$dest/dotnet" ] && "$dest/dotnet" --list-sdks 2>/dev/null | grep -q "^$DOTNET_VERSION "; then
        persist "export DOTNET_ROOT=\"$dest\""
        persist "export PATH=\"$dest:\$PATH\""
        note ".NET SDK $DOTNET_VERSION already in $dest"
        return
    fi

    local arch sha
    case "$(uname -s)-$(uname -m)" in
        Linux-x86_64|Linux-amd64) arch="x64"; sha="$DOTNET_SHA256_LINUX_X64" ;;
        Linux-aarch64|Linux-arm64) arch="arm64"; sha="$DOTNET_SHA256_LINUX_ARM64" ;;
        *) note ".NET SDK: no pinned archive for $(uname -s)-$(uname -m)"; return ;;
    esac

    local url="https://builds.dotnet.microsoft.com/dotnet/Sdk/$DOTNET_VERSION/dotnet-sdk-$DOTNET_VERSION-linux-$arch.tar.gz"
    local archive
    archive="$(mktemp)"
    if ! curl -fsSL --retry 3 "$url" -o "$archive"; then
        rm -f "$archive"
        note ".NET SDK: the download failed ($url)"
        return
    fi
    local got
    got="$(sha256_of "$archive")"
    if [ "$got" != "$sha" ]; then
        rm -f "$archive"
        note ".NET SDK: SHA-256 mismatch, refused (expected $sha, got $got)"
        return
    fi
    mkdir -p "$dest" && tar -xzf "$archive" -C "$dest"
    rm -f "$archive"
    persist "export DOTNET_ROOT=\"$dest\""
    persist "export PATH=\"$dest:\$PATH\""
    note ".NET SDK $DOTNET_VERSION installed in $dest, SHA-256 verified"
}

start_docker() {
    if ! command -v docker >/dev/null 2>&1; then
        note "Docker: not installed in this container"
        return
    fi
    if docker info >/dev/null 2>&1; then
        note "Docker: running"
        return
    fi
    if ! command -v dockerd >/dev/null 2>&1; then
        note "Docker: the client is here and no daemon is"
        return
    fi
    local elevate=""
    if [ "$(id -u)" -ne 0 ] && command -v sudo >/dev/null 2>&1; then elevate="sudo -n"; fi
    ($elevate dockerd >/tmp/dockerd.log 2>&1 &)
    for _ in $(seq 1 20); do
        if docker info >/dev/null 2>&1; then
            note "Docker: started"
            return
        fi
        sleep 1
    done
    note "Docker: dockerd did not come up in 20 s (see /tmp/dockerd.log)"
}

report_eqs() {
    local vars=""
    [ -n "${EQS_API_URL:-}" ] && vars="EQS_API_URL"
    [ -n "${EQS_TOKEN:-}" ] && vars="${vars:+$vars, }EQS_TOKEN"
    if command -v eqs >/dev/null 2>&1; then
        note "eqs: on PATH${vars:+ ($vars set)}"
    else
        note "eqs: not installed (the environment's setup script installs it)"
    fi
}

prepare_openspec
if $cloud; then
    configure_git
    install_dotnet
    start_docker
    report_eqs
else
    note "local session: git identity, SDKs and Docker are left as the machine has them"
fi

echo "Session prepared by .claude/hooks/session-start.sh:"
for line in "${report[@]}"; do echo "- $line"; done
exit 0
