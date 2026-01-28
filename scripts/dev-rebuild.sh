#!/bin/bash
# dev-rebuild.sh - Rebuilds packages and clears NuGet cache for development

set -e

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ROOT_DIR="$(dirname "$SCRIPT_DIR")"

echo "🔄 eQuantic.UI Development Rebuild"
echo "=================================="

# Step 1: Build Runtime TypeScript
echo ""
echo "📦 Step 1/5: Building Runtime TypeScript..."
cd "$ROOT_DIR/src/eQuantic.UI.Runtime"
npm run build

# Step 2: Pack all packages
echo ""
echo "📦 Step 2/5: Packing NuGet packages..."
cd "$ROOT_DIR"
dotnet pack -c Release

# Step 3: Clear specific packages from NuGet cache
echo ""
echo "🧹 Step 3/5: Clearing eQuantic packages from NuGet cache..."
NUGET_CACHE=$(dotnet nuget locals global-packages -l | sed 's/global-packages: //')

# Remove only eQuantic packages (faster than clearing entire cache)
rm -rf "$NUGET_CACHE/equantic.ui.core"
rm -rf "$NUGET_CACHE/equantic.ui.components"
rm -rf "$NUGET_CACHE/equantic.ui.compiler"
rm -rf "$NUGET_CACHE/equantic.ui.server"
rm -rf "$NUGET_CACHE/equantic.ui.sdk"
rm -rf "$NUGET_CACHE/equantic.ui.runtime"
rm -rf "$NUGET_CACHE/equantic.ui.runtime.osx64"
rm -rf "$NUGET_CACHE/equantic.ui.runtime.win64"
rm -rf "$NUGET_CACHE/equantic.ui.runtime.linux64"
rm -rf "$NUGET_CACHE/equantic.ui.tailwind"

echo "   Cleared eQuantic packages from: $NUGET_CACHE"

# Step 4: Restore and build sample (default: CounterApp)
SAMPLE="${1:-CounterApp}"
echo ""
echo "📦 Step 4/5: Restoring $SAMPLE..."
cd "$ROOT_DIR/samples/$SAMPLE"
dotnet restore --force

echo ""
echo "🔨 Step 5/5: Building $SAMPLE..."
dotnet build

echo ""
echo "✅ Done! Run 'dotnet run' in samples/$SAMPLE to test."
