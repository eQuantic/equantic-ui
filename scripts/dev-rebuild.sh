#!/bin/bash
# dev-rebuild.sh - Rebuilds packages and clears NuGet cache for development

set -e

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ROOT_DIR="$(dirname "$SCRIPT_DIR")"

echo "🔄 eQuantic.UI Development Rebuild"
echo "=================================="

# Step 1: Build Runtime TypeScript
echo ""
echo "📦 Step 1/6: Building Runtime TypeScript..."
cd "$ROOT_DIR/src/eQuantic.UI.Runtime"

# Detect platform and use embedded Bun
if [[ "$OSTYPE" == "darwin"* ]]; then
    BUN_PATH="$ROOT_DIR/src/eQuantic.UI.Runtime.Osx64/tools/bun/bun-darwin"
elif [[ "$OSTYPE" == "linux-gnu"* ]]; then
    BUN_PATH="$ROOT_DIR/src/eQuantic.UI.Runtime.Linux64/tools/bun/bun-linux"
else
    echo "❌ Unsupported platform: $OSTYPE"
    exit 1
fi

# Check if Bun exists
if [ ! -f "$BUN_PATH" ]; then
    echo "❌ Embedded Bun not found at: $BUN_PATH"
    exit 1
fi

# Make Bun executable
chmod +x "$BUN_PATH"

# Install dependencies with Bun
echo "   Installing dependencies with embedded Bun..."
"$BUN_PATH" install

# Build with Bun (use bun x to execute local binaries)
echo "   Building TypeScript with embedded Bun..."
"$BUN_PATH" x tsc && "$BUN_PATH" x vite build

# Step 2: Clean ALL previous builds
echo ""
echo "🧹 Step 2/6: Cleaning ALL previous builds..."
cd "$ROOT_DIR"

# Remove all bin/obj folders (much faster than dotnet clean)
rm -rf src/*/bin src/*/obj

echo "📦 Step 3/6: Building and packing packages..."
# Build each package individually to ensure fresh compilation
dotnet build src/eQuantic.UI.Runtime.Osx64/eQuantic.UI.Runtime.Osx64.csproj -c Release
dotnet build src/eQuantic.UI.Core/eQuantic.UI.Core.csproj -c Release
dotnet build src/eQuantic.UI.Components/eQuantic.UI.Components.csproj -c Release
dotnet build src/eQuantic.UI.Compiler/eQuantic.UI.Compiler.csproj -c Release
dotnet build src/eQuantic.UI.Server/eQuantic.UI.Server.csproj -c Release
dotnet build src/eQuantic.UI.Tailwind/eQuantic.UI.Tailwind.csproj -c Release
dotnet build src/eQuantic.UI.SDK/eQuantic.UI.Sdk.csproj -c Release

# Pack platform-specific runtime first
dotnet pack src/eQuantic.UI.Runtime.Osx64/eQuantic.UI.Runtime.Osx64.csproj -c Release --no-build

# Pack meta-package (depends on platform-specific runtime)
dotnet pack src/eQuantic.UI.Runtime/eQuantic.UI.Runtime.csproj -c Release

# Pack remaining packages
dotnet pack src/eQuantic.UI.Core/eQuantic.UI.Core.csproj -c Release --no-build
dotnet pack src/eQuantic.UI.Components/eQuantic.UI.Components.csproj -c Release --no-build
dotnet pack src/eQuantic.UI.Compiler/eQuantic.UI.Compiler.csproj -c Release --no-build
dotnet pack src/eQuantic.UI.Server/eQuantic.UI.Server.csproj -c Release --no-build
dotnet pack src/eQuantic.UI.Tailwind/eQuantic.UI.Tailwind.csproj -c Release --no-build
dotnet pack src/eQuantic.UI.SDK/eQuantic.UI.Sdk.csproj -c Release  # SDK needs to build eQuantic.Build

# Step 4: Clear NuGet cache
echo ""
echo "🧹 Step 4/6: Clearing NuGet cache..."
rm -rf ~/.nuget/packages/equantic.ui.*

# Step 5: Restore sample with local packages
SAMPLE="${1:-CounterApp}"
echo ""
echo "📦 Step 5/6: Restoring $SAMPLE with local packages..."
cd "$ROOT_DIR"

# Create consolidated local package directory
LOCAL_FEED="$ROOT_DIR/.local-packages"
rm -rf "$LOCAL_FEED"
mkdir -p "$LOCAL_FEED"

# Copy all packages to single directory for NuGet resolution
cp src/eQuantic.UI.Runtime/bin/Release/*.nupkg "$LOCAL_FEED/" 2>/dev/null || true
cp src/eQuantic.UI.Runtime.Osx64/bin/Release/*.nupkg "$LOCAL_FEED/" 2>/dev/null || true
cp src/eQuantic.UI.Core/bin/Release/*.nupkg "$LOCAL_FEED/" 2>/dev/null || true
cp src/eQuantic.UI.Components/bin/Release/*.nupkg "$LOCAL_FEED/" 2>/dev/null || true
cp src/eQuantic.UI.Compiler/bin/Release/*.nupkg "$LOCAL_FEED/" 2>/dev/null || true
cp src/eQuantic.UI.Server/bin/Release/*.nupkg "$LOCAL_FEED/" 2>/dev/null || true
cp src/eQuantic.UI.Tailwind/bin/Release/*.nupkg "$LOCAL_FEED/" 2>/dev/null || true
cp src/eQuantic.UI.Sdk/bin/Release/*.nupkg "$LOCAL_FEED/" 2>/dev/null || true

cd "$ROOT_DIR/samples/$SAMPLE"

# Clean sample
rm -rf bin obj

# Create/update NuGet.config to use consolidated local package feed
cat > NuGet.config <<EOF
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources>
    <clear />
    <add key="LocalPackages" value="../../.local-packages" />
  </packageSources>
</configuration>
EOF

# Restore packages (SDK will pull dependencies automatically)
dotnet restore --force

# Step 6: Build sample
echo ""
echo "🔨 Step 6/6: Building $SAMPLE..."
dotnet build --no-incremental

echo ""
echo "✅ Done! Run 'dotnet run' in samples/$SAMPLE to test."
