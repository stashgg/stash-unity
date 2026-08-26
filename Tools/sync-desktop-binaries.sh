#!/bin/bash
# Copies the stash-native desktop binaries into the package. They are vendored like the AAR and
# the xcframework and come from a stash-native release:
#   StashNativeDesktop-<version>-win64.zip  -> Plugins/Windows/x86_64/StashNativeDesktop.dll
#   StashNativeDesktop-<version>-macos.zip  -> Plugins/macOS/StashNativeDesktop.bundle
#
#   Tools/sync-desktop-binaries.sh <version>            downloads both release assets
#   Tools/sync-desktop-binaries.sh --local <stash-native checkout>   uses local builds
#     (Desktop/Windows/build/Release/StashNativeDesktop.dll, Desktop/macOS/build/StashNativeDesktop.bundle)
set -euo pipefail

SCRIPT_DIR="$( cd "$( dirname "${BASH_SOURCE[0]}" )" && pwd )"
PACKAGE="$SCRIPT_DIR/../Packages/gg.stash.unity"
WIN_DIR="$PACKAGE/Plugins/Windows/x86_64"
MAC_DIR="$PACKAGE/Plugins/macOS"
mkdir -p "$WIN_DIR" "$MAC_DIR"

if [ "${1:-}" = "--local" ]; then
    NATIVE="${2:?path to the stash-native checkout}"
    cp "$NATIVE/Desktop/Windows/build/Release/StashNativeDesktop.dll" "$WIN_DIR/"
    cp "$NATIVE/Desktop/macOS/build/StashNativeDesktop.bundle" "$MAC_DIR/"
else
    VERSION="${1:?stash-native release version, e.g. 2.4.0}"
    TMP="$(mktemp -d)"
    BASE="https://github.com/stashgg/stash-native/releases/download/$VERSION"
    curl -sSL -o "$TMP/win.zip" "$BASE/StashNativeDesktop-$VERSION-win64.zip"
    curl -sSL -o "$TMP/mac.zip" "$BASE/StashNativeDesktop-$VERSION-macos.zip"
    unzip -q -o "$TMP/win.zip" -d "$TMP/win"
    unzip -q -o "$TMP/mac.zip" -d "$TMP/mac"
    cp "$TMP/win/StashNativeDesktop-$VERSION-win64/StashNativeDesktop.dll" "$WIN_DIR/"
    cp "$TMP/mac/StashNativeDesktop-$VERSION-macos/StashNativeDesktop.bundle" "$MAC_DIR/"
    rm -rf "$TMP"
fi

echo "Synced:"
ls -la "$WIN_DIR/StashNativeDesktop.dll" "$MAC_DIR/StashNativeDesktop.bundle"
