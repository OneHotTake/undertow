#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")/.."
# Run on Vault with the pinned target image available. No service lifecycle calls.
image=emby/embyserver@sha256:3aafff933d3f28d23ed0bc201022abe71c0aa80deb17177566c726b9bbc686c6
ref_container="undertow-refs-$$"
mkdir -p libs artifacts
trap 'docker rm -f "$ref_container" >/dev/null 2>&1 || true' EXIT
docker create --name "$ref_container" "$image" >/dev/null
for assembly in MediaBrowser.Controller MediaBrowser.Common MediaBrowser.Model Emby.Media.Model System.IO.Pipelines Emby.Web.GenericEdit Emby.Web.GenericUI; do
  docker cp "$ref_container:/system/$assembly.dll" "libs/$assembly.dll"
done
docker run --rm -e DOTNET_CLI_TELEMETRY_OPTOUT=1 -v "$PWD:/src" -w /src \
  mcr.microsoft.com/dotnet/sdk:8.0 dotnet publish src/Undertow/Undertow.csproj -c Release -o artifacts --nologo
docker run --rm -e DOTNET_CLI_TELEMETRY_OPTOUT=1 -v "$PWD:/src" -w /src \
  mcr.microsoft.com/dotnet/sdk:8.0 dotnet run --project tests/Contracts.csproj -c Release --no-launch-profile
sha256sum artifacts/Undertow.dll
