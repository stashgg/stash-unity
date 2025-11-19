#!/bin/bash
# Build script for WebViewLauncher.bundle

SCRIPT_DIR="$( cd "$( dirname "${BASH_SOURCE[0]}" )" && pwd )"
OUTPUT_DIR="$SCRIPT_DIR"
SOURCE_FILE="$SCRIPT_DIR/WebViewLauncher.mm"
BUNDLE_NAME="WebViewLauncher.bundle"

echo "Building WebViewLauncher.bundle..."

# Compile the Objective-C++ file into a bundle (without ARC for compatibility)
# Use -Wl,-export_dynamic to ensure all symbols are exported and visible to dlsym
clang++ -std=c++11 -fno-objc-arc -framework Cocoa -framework WebKit -framework Foundation \
    -pthread -bundle -Wl,-export_dynamic -o "$OUTPUT_DIR/$BUNDLE_NAME" \
    "$SOURCE_FILE"

if [ $? -eq 0 ]; then
    echo "Successfully built $BUNDLE_NAME"
    chmod +x "$OUTPUT_DIR/$BUNDLE_NAME"
else
    echo "Failed to build $BUNDLE_NAME"
    exit 1
fi

