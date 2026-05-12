#!/bin/bash
# build.sh — Cloudflare Pages build script for Tonberry Tactics
# Cloudflare's build container is ephemeral and doesn't have .NET preinstalled,
# so we fetch the SDK on every build. Cached layers handle the slowness fine.

set -euo pipefail

echo "▶ Installing .NET 10 SDK..."
curl -sSL https://dot.net/v1/dotnet-install.sh > dotnet-install.sh
chmod +x dotnet-install.sh
./dotnet-install.sh -c 10.0 -InstallDir ./dotnet10
export PATH="$PWD/dotnet10:$PATH"
dotnet --version

echo "▶ Publishing Blazor WebAssembly app..."
dotnet publish -c Release -o output

echo "▶ Build complete. Cloudflare Pages will serve from output/wwwroot/"

# If you ever hit "Asset dotnet.wasm is over the 25MiB limit", uncomment:
#   rm output/wwwroot/_framework/*.wasm
# and the Blazor loader will fall back to the .br (Brotli) variants, which
# Cloudflare's CDN serves with content-encoding: br automatically.
