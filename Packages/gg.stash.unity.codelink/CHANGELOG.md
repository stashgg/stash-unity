# Changelog

## [3.0.0-preview.1] - 2026-10-19

### Added

- Initial release. Bundles **StashNativeCodeLink [3.0.0](https://github.com/stashgg/stash-native/releases/tag/3.0.0)** (`StashNativeCodeLink-3.0.0.aar`, `StashNativeCodeLink.xcframework`) and an EDM4U dependency manifest for CameraX 1.3.4 and ZXing 3.5.3. CameraX is held at 1.3.x because Unity 2022.3's Android Gradle Plugin cannot dex 1.4.x. Enables `StashNative.Instance.OpenScanner` in `gg.stash.unity` 3.0.0-preview.1.
