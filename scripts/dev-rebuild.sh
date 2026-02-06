#!/bin/bash
# dev-rebuild.sh - Rebuilds runtime and samples using direct project references
# This script is optimized for local development within the monorepo.

set -e

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ROOT_DIR="$(dirname "$SCRIPT_DIR")"

echo "🔄 eQuantic.UI Development Rebuild (Project References)"
echo "========================================================"

# Step 1: Build Runtime TypeScript
echo ""
echo "📦 Step 1/3: Building Runtime TypeScript..."
cd "$ROOT_DIR/src/eQuantic.UI.Runtime"

# Detect platform and use embedded Bun
if [[ "$OSTYPE" == "darwin"* ]]; then
    BUN_PLATFORM="Osx64"
    BUN_BINARY="bun-darwin"
elif [[ "$OSTYPE" == "linux-gnu"* ]]; then
    BUN_PLATFORM="Linux64"
    BUN_BINARY="bun-linux"
else
    echo "❌ Unsupported platform: $OSTYPE"
    exit 1
fi

BUN_DIR="$ROOT_DIR/src/eQuantic.UI.Runtime.$BUN_PLATFORM/tools/bun"
BUN_PATH="$BUN_DIR/$BUN_BINARY"

# Extract Bun binary if it's still zipped
if [ ! -f "$BUN_PATH" ]; then
    echo "   Extracting Bun binary..."
    if [ -f "$BUN_DIR/$BUN_BINARY.zip" ]; then
        unzip -q "$BUN_DIR/$BUN_BINARY.zip" -d "$BUN_DIR/"
    else
        echo "❌ Embedded Bun not found at: $BUN_PATH"
        exit 1
    fi
fi

# Make Bun executable
chmod +x "$BUN_PATH"

# Install dependencies and build
echo "   Building with embedded Bun..."
"$BUN_PATH" install
"$BUN_PATH" x tsc && "$BUN_PATH" x vite build

# Step 2: Build Compiler CLI (needed by Sdk.targets)
echo ""
echo "🔨 Step 2/3: Building Compiler CLI..."
cd "$ROOT_DIR"
dotnet build src/eQuantic.Build/eQuantic.Build.csproj -c Release

# Step 3: Build Samples
ARG_SAMPLE="${1}"
echo ""
if [ -n "$ARG_SAMPLE" ] && [ -d "samples/$ARG_SAMPLE" ]; then
    echo "🚀 Step 3/3: Building requested sample: $ARG_SAMPLE..."
    (cd "samples/$ARG_SAMPLE" && rm -rf bin obj && dotnet build)
else
    echo "🚀 Step 3/3: Building all samples..."
    for sample_dir in samples/*; do
        if [ -d "$sample_dir" ] && ls "$sample_dir"/*.csproj 1> /dev/null 2>&1; then
            SAMPLE_NAME=$(basename "$sample_dir")
            echo "   Building $SAMPLE_NAME..."
            (cd "$sample_dir" && rm -rf bin obj && dotnet build)
        fi
    done
fi

echo ""
echo "✅ Done! All components and samples built using project references."
