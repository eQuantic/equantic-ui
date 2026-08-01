#!/usr/bin/env bash
# Track N / W2 — the embedded shader toolchain, resolved with ZERO manual provisioning.
#
# Sourcing this file exports EQ_SLANGC pointing at a slangc of the PINNED version, acquiring it
# on first use. Resolution order:
#   1. $EQ_SLANGC already set and runnable  → honored verbatim (an explicit override always wins)
#   2. the toolchain package's extracted tools dir (when the repo carries one)
#   3. the local toolchain cache
#   4. download the pinned release, VERIFY ITS SHA-256, extract into the cache
#
# WHY A CACHE AND NOT COMMITTED BINARIES (the Bun precedent, deliberately not copied): bun is
# required by EVERY app build, so it ships inside the platform runtime packages that consumers
# restore. slangc is a FRAMEWORK-DEV tool — app developers consume the committed .metallib/.spv and
# never compile a shader — so committing ~57 MB per platform (≈230 MB across four) would weigh the
# repository down for a binary almost nobody fetches. The pin + checksum give the same
# reproducibility the committed binary would, at zero repo cost.

SLANG_VERSION="2026.14.1"

# Pinned artifact digests (sha256 of the release archive). A mismatch ABORTS: a shader compiler is
# the last place to accept "probably the right bytes".
SLANG_SHA256_macos_aarch64="92da7ab6226dd951037cd85397f830ae78fe40fbbb8928882e0b2654e468fdd4"

# Cache root: override with EQ_TOOLCHAIN_DIR (CI runners, sandboxes).
SLANG_CACHE_ROOT="${EQ_TOOLCHAIN_DIR:-$HOME/.local/opt}"

eq_slang_platform() {
    local os arch
    os="$(uname -s)"
    arch="$(uname -m)"
    case "$os" in
        Darwin) os="macos" ;;
        Linux)  os="linux" ;;
        *) echo "unsupported OS for the Slang toolchain: $os" >&2; return 1 ;;
    esac
    case "$arch" in
        arm64|aarch64) arch="aarch64" ;;
        x86_64)        arch="x86_64" ;;
        *) echo "unsupported architecture for the Slang toolchain: $arch" >&2; return 1 ;;
    esac
    echo "${os}-${arch}"
}

eq_slang_resolve() {
    # 1. Explicit override.
    if [ -n "${EQ_SLANGC:-}" ] && [ -x "${EQ_SLANGC}" ]; then
        return 0
    fi

    local platform archive_name install_dir candidate expected
    platform="$(eq_slang_platform)" || return 1
    install_dir="${SLANG_CACHE_ROOT}/slang-${SLANG_VERSION}"

    # 2. A toolchain package extracted inside the repo (populated by `pack`, absent in a clean tree).
    local repo_root packaged
    repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
    packaged="${repo_root}/src/eQuantic.UI.Native.Toolchain.$(eq_slang_package_suffix)/tools/slang/bin/slangc"
    if [ -x "$packaged" ]; then
        export EQ_SLANGC="$packaged"
        return 0
    fi

    # 3. Cache hit.
    candidate="${install_dir}/bin/slangc"
    if [ -x "$candidate" ]; then
        export EQ_SLANGC="$candidate"
        return 0
    fi

    # 4. Acquire — pinned version, verified digest.
    archive_name="slang-${SLANG_VERSION}-${platform}.tar.gz"
    expected="$(eval echo "\${SLANG_SHA256_${platform//-/_}:-}")"
    if [ -z "$expected" ]; then
        echo "No pinned SHA-256 for platform '${platform}'. Add one to scripts/slang-toolchain.sh" >&2
        echo "before trusting a download on this platform." >&2
        return 1
    fi

    local tmp
    tmp="$(mktemp -d)"
    echo "Slang toolchain ${SLANG_VERSION} not found locally — downloading ${archive_name}…"
    if ! curl -sSL -m 900 -o "${tmp}/${archive_name}" \
        "https://github.com/shader-slang/slang/releases/download/v${SLANG_VERSION}/${archive_name}"; then
        echo "Download failed." >&2; rm -rf "$tmp"; return 1
    fi

    local actual
    actual="$(shasum -a 256 "${tmp}/${archive_name}" | cut -d' ' -f1)"
    if [ "$actual" != "$expected" ]; then
        echo "CHECKSUM MISMATCH for ${archive_name}" >&2
        echo "  expected ${expected}" >&2
        echo "  actual   ${actual}" >&2
        rm -rf "$tmp"; return 1
    fi

    mkdir -p "$install_dir"
    tar xzf "${tmp}/${archive_name}" -C "$install_dir"
    rm -rf "$tmp"

    if [ ! -x "${install_dir}/bin/slangc" ]; then
        echo "Extraction did not produce ${install_dir}/bin/slangc" >&2; return 1
    fi
    export EQ_SLANGC="${install_dir}/bin/slangc"
    echo "Slang toolchain installed: ${EQ_SLANGC}"
}

# The NuGet package id suffix for this host (mirrors the Runtime.* naming).
eq_slang_package_suffix() {
    case "$(eq_slang_platform)" in
        macos-aarch64) echo "OsxArm64" ;;
        macos-x86_64)  echo "Osx64" ;;
        linux-aarch64) echo "LinuxArm64" ;;
        linux-x86_64)  echo "Linux64" ;;
        *) echo "Unknown" ;;
    esac
}
