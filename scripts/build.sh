#!/usr/bin/env bash
# Builds TPO with the real tModLoader workflow (dotnet build -> tMLMod.targets -> "tModLoader -build")
# and copies the resulting .tmod into dist/ with the commit in its name plus a SHA-256 file.
#
# Requirements: .NET 8 SDK, a tModLoader install (see Documentation/Environment.md),
# and either the repo inside ModSources or a tml.local.props file.
#
# Usage: scripts/build.sh [Debug|Release]   (default: Release)
set -euo pipefail

CONFIGURATION="${1:-Release}"
REPO_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
MOD_NAME="TerrariaPerformanceOverhaul"
# tModLoader save directory on Linux; "tModLoader -build" writes the .tmod to its Mods folder.
TML_SAVE_DIR="${TML_SAVE_DIR:-$HOME/.local/share/Terraria/tModLoader}"
TMOD_OUT="$TML_SAVE_DIR/Mods/$MOD_NAME.tmod"

cd "$REPO_DIR"

VERSION="$(sed -n 's/^version *= *//p' build.txt | tr -d '[:space:]')"
COMMIT="$(git rev-parse --short=12 HEAD 2>/dev/null || echo unknown)"
if [ "$COMMIT" != "unknown" ] && [ -n "$(git status --porcelain)" ]; then
	COMMIT="$COMMIT-dirty"
fi

echo "== Building $MOD_NAME $VERSION (commit $COMMIT, $CONFIGURATION)"
BUILD_START="$(date +%s)"
dotnet build "$MOD_NAME.csproj" -c "$CONFIGURATION" -nologo

if [ ! -f "$TMOD_OUT" ] || [ "$(stat -c %Y "$TMOD_OUT")" -lt "$BUILD_START" ]; then
	echo "ERROR: expected fresh $TMOD_OUT after build, but it is missing or old." >&2
	exit 1
fi

mkdir -p dist
ARTIFACT="dist/$MOD_NAME-$VERSION-$COMMIT.tmod"
cp "$TMOD_OUT" "$ARTIFACT"
(cd dist && sha256sum "$(basename "$ARTIFACT")" > "$(basename "$ARTIFACT").sha256")

echo "== Artifact: $REPO_DIR/$ARTIFACT"
cat "$ARTIFACT.sha256"
if [[ "$COMMIT" == *-dirty ]]; then
	echo "WARNING: built from uncommitted changes. Commit before producing a build for Windows testing." >&2
fi
