#!/usr/bin/env bash
# Runs dependency-free logic tests (no Terraria/tModLoader needed).
set -euo pipefail
cd "$(dirname "${BASH_SOURCE[0]}")/.."
dotnet run --project tests/LogicTests/LogicTests.csproj -c Release -v q
