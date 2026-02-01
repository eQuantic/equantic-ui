#!/bin/bash

# Script para executar testes E2E com rebuild completo
# Usage: ./run-tests.sh [test-file-pattern]

set -e  # Exit on error

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ROOT_DIR="$SCRIPT_DIR/../.."
SAMPLE_DIR="$ROOT_DIR/samples/TodoListApp"

echo "==================================="
echo "eQuantic.UI E2E Test Runner"
echo "==================================="

# Check if rebuild is needed
REBUILD="${REBUILD:-auto}"

if [ "$REBUILD" = "always" ] || [ "$REBUILD" = "auto" ]; then
    echo ""
    echo "📦 Step 1: Building Runtime..."
    cd "$ROOT_DIR/src/eQuantic.UI.Runtime"
    npm run build

    echo ""
    echo "📦 Step 2: Packing Runtime.Osx64..."
    cd "$ROOT_DIR"
    dotnet pack src/eQuantic.UI.Runtime.Osx64/eQuantic.UI.Runtime.Osx64.csproj -c Release --no-restore -v quiet

    echo ""
    echo "📦 Step 3: Packing Tailwind..."
    dotnet pack src/eQuantic.UI.Tailwind/eQuantic.UI.Tailwind.csproj -c Release --no-restore -v quiet

    echo ""
    echo "🧹 Step 4: Clearing NuGet cache..."
    rm -rf ~/.nuget/packages/equantic.ui.runtime.osx64/0.1.2
    rm -rf ~/.nuget/packages/equantic.ui.tailwind/0.1.2

    echo ""
    echo "🔨 Step 5: Building TodoListApp..."
    cd "$SAMPLE_DIR"
    dotnet restore --force --no-cache > /dev/null
    dotnet build -v quiet

    echo ""
    echo "✅ Build complete!"
else
    echo "⏭️  Skipping rebuild (set REBUILD=always to force)"
fi

echo ""
echo "🧪 Running Playwright tests..."
cd "$SCRIPT_DIR"

# Determine which tests to run
TEST_PATTERN="${1:-ssr-csr-diff.spec.ts}"

# Kill any existing dotnet processes
pkill -f "dotnet.*TodoListApp" 2>/dev/null || true
sleep 1

# Run tests
npm test -- "$TEST_PATTERN" --reporter=list

echo ""
echo "==================================="
echo "✅ Tests completed!"
echo "==================================="

# Show report location
if [ -f "test-results/ssr-csr-diff-report.json" ]; then
    echo ""
    echo "📄 Detailed report: tests/e2e/test-results/ssr-csr-diff-report.json"
fi
