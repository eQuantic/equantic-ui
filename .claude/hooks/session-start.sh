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
# install. The hook never blocks the session: each step reports what it did or why it could not, and
# the report becomes part of the session's context. What the Workflow section makes mandatory (the
# OpenSpec CLI, and in a cloud container the .NET SDK and signing off) is also REQUIRED here: when
# one of them could not be prepared, the person in the session is told, not only the model. Docker
# stays best-effort.
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
missing=()
note() { report+=("$1"); }
# A step the Workflow section makes mandatory, and that did not happen.
required() { note "$2"; missing+=("$1"); }

# Lines for the session's later Bash commands: Claude Code sources this file before each of them.
# It FAILS when the file cannot be written, and every caller treats that as the step failing: a
# tool installed but left off the session's PATH is a tool the session does not have.
persist() {
    [ -n "$env_file" ] || return 0
    # Once: a session that starts again (a resume, a clear, a compact) must not grow the file.
    grep -qxF -- "$1" "$env_file" 2>/dev/null && return 0
    # Grouped, so a redirection that fails is silenced too (the report already says it failed).
    { printf '%s\n' "$1" >> "$env_file"; } 2>/dev/null
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
        # The CLI is mandatory in every session, a laptop's included: without Node the person is told,
        # even though building and testing the SDK need no Node at all.
        required "the OpenSpec CLI" "OpenSpec: node is not on PATH, so the CLI is not installed (it needs Node >= 20.19)"
        return
    fi
    local version
    if ! version="$("$root/scripts/openspec.sh" --version 2>/dev/null)"; then
        required "the OpenSpec CLI" "OpenSpec: installing the pinned CLI failed; run ./scripts/openspec.sh --version to see why"
        return
    fi
    if ! persist "export PATH=\"$bin:\$PATH\"" || ! persist "export OPENSPEC_TELEMETRY=0"; then
        required "the OpenSpec CLI" "OpenSpec $version installed, but the session environment ($env_file) could not be written, so it is not on PATH"
        return
    fi
    note "OpenSpec $version on PATH, telemetry off"
}

configure_git() {
    git -C "$root" config user.name "$OWNER_NAME"
    git -C "$root" config user.email "$OWNER_EMAIL"
    git -C "$root" config commit.gpgsign false
    git -C "$root" config tag.gpgsign false
    # An identity given through the environment outranks every config file, so the session's own
    # commands carry the owner's too.
    if ! persist "export GIT_AUTHOR_NAME=\"$OWNER_NAME\" GIT_AUTHOR_EMAIL=\"$OWNER_EMAIL\"" \
        || ! persist "export GIT_COMMITTER_NAME=\"$OWNER_NAME\" GIT_COMMITTER_EMAIL=\"$OWNER_EMAIL\""; then
        required "the owner's git identity" "git: the session environment ($env_file) could not be written, so the identity rests on the repository's config alone"
    fi
    # Report what git will actually use, which is what matters if something else overrides it.
    local commit_signing tag_signing summary
    commit_signing="$(git -C "$root" config --get commit.gpgsign || echo unset)"
    tag_signing="$(git -C "$root" config --get tag.gpgsign || echo unset)"
    summary="git: $(git -C "$root" config --get user.name) <$(git -C "$root" config --get user.email)>, commit.gpgsign=$commit_signing, tag.gpgsign=$tag_signing"
    if [ "$commit_signing" = "false" ] && [ "$tag_signing" = "false" ]; then
        note "$summary"
    else
        required "commit and tag signing off" "$summary (something outranks the repository's config)"
    fi
}

install_dotnet() {
    local dest="$HOME/.dotnet"

    # An SDK global.json accepts already answers `dotnet --version` from the repository root.
    if command -v dotnet >/dev/null 2>&1 && (cd "$root" && dotnet --version >/dev/null 2>&1); then
        note ".NET SDK $(cd "$root" && dotnet --version) already present"
        return
    fi
    if [ -x "$dest/dotnet" ] && "$dest/dotnet" --list-sdks 2>/dev/null | grep -q "^$DOTNET_VERSION "; then
        if ! persist "export DOTNET_ROOT=\"$dest\"" || ! persist "export PATH=\"$dest:\$PATH\""; then
            required "the .NET SDK" ".NET SDK $DOTNET_VERSION is in $dest, but the session environment ($env_file) could not be written, so it is not on PATH"
            return
        fi
        note ".NET SDK $DOTNET_VERSION already in $dest"
        return
    fi

    local arch sha
    case "$(uname -s)-$(uname -m)" in
        Linux-x86_64|Linux-amd64) arch="x64"; sha="$DOTNET_SHA256_LINUX_X64" ;;
        Linux-aarch64|Linux-arm64) arch="arm64"; sha="$DOTNET_SHA256_LINUX_ARM64" ;;
        *) required "the .NET SDK" ".NET SDK: no pinned archive for $(uname -s)-$(uname -m)"; return ;;
    esac

    local url="https://builds.dotnet.microsoft.com/dotnet/Sdk/$DOTNET_VERSION/dotnet-sdk-$DOTNET_VERSION-linux-$arch.tar.gz"
    local archive
    archive="$(mktemp)"
    if ! curl -fsSL --retry 3 "$url" -o "$archive"; then
        rm -f "$archive"
        required "the .NET SDK" ".NET SDK: the download failed ($url)"
        return
    fi
    local got
    got="$(sha256_of "$archive")"
    if [ "$got" != "$sha" ]; then
        rm -f "$archive"
        required "the .NET SDK" ".NET SDK: SHA-256 mismatch, refused (expected $sha, got $got)"
        return
    fi
    # Extract beside the destination and check what came out before anything points at it: a
    # failed extraction (a full disk, a truncated archive) must not leave a PATH to half an SDK. The
    # staging directory sits on the same filesystem, so moving it into place is a rename.
    local staging
    staging="$(mktemp -d "${dest}.staging.XXXXXX")"
    if ! tar -xzf "$archive" -C "$staging"; then
        rm -rf "$staging" "$archive"
        required "the .NET SDK" ".NET SDK: extracting the archive failed, nothing installed"
        return
    fi
    rm -f "$archive"
    if ! "$staging/dotnet" --list-sdks 2>/dev/null | grep -q "^$DOTNET_VERSION "; then
        rm -rf "$staging"
        required "the .NET SDK" ".NET SDK: the extracted archive does not answer SDK $DOTNET_VERSION, nothing installed"
        return
    fi
    if [ ! -e "$dest" ]; then
        mv "$staging" "$dest"
    elif ! cp -a "$staging/." "$dest/"; then
        # $dest already exists (it may hold global tools), so the SDK joins it rather than replacing it.
        rm -rf "$staging"
        required "the .NET SDK" ".NET SDK: copying into $dest failed"
        return
    fi
    rm -rf "$staging"
    if ! "$dest/dotnet" --list-sdks 2>/dev/null | grep -q "^$DOTNET_VERSION "; then
        required "the .NET SDK" ".NET SDK: $dest does not answer SDK $DOTNET_VERSION after the install"
        return
    fi
    # The pin is only right if global.json accepts it: from the repository root, `dotnet --version`
    # resolves through global.json and fails when no installed SDK satisfies it.
    if ! (cd "$root" && DOTNET_ROOT="$dest" "$dest/dotnet" --version >/dev/null 2>&1); then
        required "the .NET SDK" ".NET SDK: $DOTNET_VERSION is installed but global.json does not accept it; update the pin with global.json"
        return
    fi
    if ! persist "export DOTNET_ROOT=\"$dest\"" || ! persist "export PATH=\"$dest:\$PATH\""; then
        required "the .NET SDK" ".NET SDK $DOTNET_VERSION installed in $dest, but the session environment ($env_file) could not be written, so it is not on PATH"
        return
    fi
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

text="Session prepared by .claude/hooks/session-start.sh:"
for line in "${report[@]}"; do text+=$'\n'"- $line"; done

warning=""
if [ "${#missing[@]}" -gt 0 ]; then
    list="${missing[0]}"
    for ((i = 1; i < ${#missing[@]}; i++)); do
        if [ "$i" -eq $((${#missing[@]} - 1)) ]; then list="$list and ${missing[$i]}"; else list="$list, ${missing[$i]}"; fi
    done
    warning="This session is not fully prepared: $list could not be set up (the SessionStart report in the context says why)."
fi

# JSON, so the report reaches the model (additionalContext) and a failure reaches the person
# (systemMessage). Without python3 to encode it, the plain report still reaches the model, and the
# warning goes to stderr.
if command -v python3 >/dev/null 2>&1; then
    TEXT="$text" WARNING="$warning" python3 -c '
import json, os
out = {"hookSpecificOutput": {"hookEventName": "SessionStart", "additionalContext": os.environ["TEXT"]}}
if os.environ["WARNING"]:
    out["systemMessage"] = os.environ["WARNING"]
print(json.dumps(out))'
else
    printf '%s\n' "$text"
    [ -n "$warning" ] && echo "$warning" >&2
fi
exit 0
