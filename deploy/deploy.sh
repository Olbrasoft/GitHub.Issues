#!/usr/bin/env bash
set -e

# GitHub.Issues Deploy Script
# Deploys GitHub.Issues web application according to deployment-guide-cz.md

# Ensure .NET 10 SDK is in PATH (required when running with sudo)
export PATH="/home/jirka/.dotnet:/home/jirka/.local/bin:$PATH"

PROJECT_PATH="/home/jirka/Olbrasoft/GitHub.Issues"

# Deploy script DOSTÁVÁ base directory jako argument (SINGLE SOURCE OF TRUTH)
BASE_DIR="$1"

if [ -z "$BASE_DIR" ]; then
  echo "❌ Usage: deploy.sh <base-directory>"
  echo ""
  echo "Examples:"
  echo "  Production: sudo ./deploy.sh /opt/olbrasoft/github-issues"
  echo ""
  exit 1
fi

# Safety guard: BASE_DIR must be an absolute path under /opt/olbrasoft or /home — we rm -rf
# $BASE_DIR/app later, so an unexpected value like "/" or "" would be catastrophic.
case "$BASE_DIR" in
    /opt/olbrasoft/*|/home/*)
        ;;
    *)
        echo "❌ BASE_DIR must be an absolute path under /opt/olbrasoft/ or /home/ (got: $BASE_DIR)"
        exit 1
        ;;
esac

echo "╔══════════════════════════════════════════════════════════════╗"
echo "║           GitHub.Issues Deploy Script                        ║"
echo "╚══════════════════════════════════════════════════════════════╝"
echo ""
echo "Target: $BASE_DIR"
echo ""

cd "$PROJECT_PATH"

# Step 1: Run tests
echo "📋 Running tests..."
dotnet test --verbosity minimal --filter "FullyQualifiedName!~IntegrationTests"
if [ $? -ne 0 ]; then
    echo "❌ Tests failed! Aborting deployment."
    exit 1
fi
echo "✅ All tests passed"
echo ""

# Step 2: Clean previous publish output
# dotnet publish is incremental and will not always overwrite stale framework/dependency DLLs
# (e.g. when NuGet resolves a floating version to a newer patch than last deploy). Wipe the
# output directory so every deploy starts from a known-empty state — fixes runtime
# FileNotFoundException for assemblies whose version changed between deploys.
echo "🧹 Cleaning previous publish output..."
if [ -d "$BASE_DIR/app" ]; then
    rm -rf "$BASE_DIR/app"
    echo "✅ Removed $BASE_DIR/app"
fi
echo ""

# Step 3: Build and publish
echo "🔨 Building and publishing..."
dotnet publish src/Olbrasoft.GitHub.Issues.AspNetCore.RazorPages/Olbrasoft.GitHub.Issues.AspNetCore.RazorPages.csproj \
  -c Release \
  -o "$BASE_DIR/app" \
  --no-self-contained

echo "✅ Published to $BASE_DIR/app"
echo ""

# Step 4: Create directory structure
echo "📁 Creating directory structure..."
mkdir -p "$BASE_DIR/config"
mkdir -p "$BASE_DIR/data"
mkdir -p "$BASE_DIR/logs"

echo "✅ Directory structure created"
echo ""

# Step 5: Copy config if not exists
if [ ! -f "$BASE_DIR/config/appsettings.json" ]; then
    echo "📝 Creating default appsettings.json..."
    if [ -f "$BASE_DIR/app/appsettings.json" ]; then
        cp "$BASE_DIR/app/appsettings.json" "$BASE_DIR/config/appsettings.json"
        echo "✅ Config copied from app/"
    else
        echo "⚠️  No appsettings.json found - you'll need to create it manually"
    fi
else
    echo "ℹ️  Config already exists (keeping existing)"
fi
echo ""

echo "╔══════════════════════════════════════════════════════════════╗"
echo "║               ✅ Deployment completed!                        ║"
echo "╠══════════════════════════════════════════════════════════════╣"
echo "║  Structure:                                                  ║"
echo "║    $BASE_DIR/app/      - Binaries                            ║"
echo "║    $BASE_DIR/config/   - Configuration                       ║"
echo "║    $BASE_DIR/data/     - Runtime data                        ║"
echo "║    $BASE_DIR/logs/     - Logs                                ║"
echo "╠══════════════════════════════════════════════════════════════╣"
echo "║  Next steps:                                                 ║"
echo "║  1. Setup systemd service                                    ║"
echo "║  2. Configure appsettings in config/                         ║"
echo "╚══════════════════════════════════════════════════════════════╝"
