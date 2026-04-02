# Changelog

All notable changes to this package will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/).

## [2.1.1] - 2026-04-02

### Changed

- **Embedded Stash Native SDK [2.1.1](https://github.com/stashgg/stash-native/releases/tag/2.1.1)** (`StashNative-2.1.1.aar`, `StashNative.xcframework`).

### Breaking Notes

- **`StashNativeModalConfig`**: removed `showDragBar` (no longer present in native modal config).
- **Payment success**: `OnPaymentSuccess` and per-call `successCallback` are now `Action<string>` — optional order payload from checkout (may be null or empty).

### Added

- **`OnExternalPayment`** (`Action<string>`): fired when checkout continues outside the app (GPay, Klarna, crypto, etc.).
- **`backgroundColor`** on **`StashNativeCardConfig`** and **`StashNativeModalConfig`** (optional hex string; null/empty keeps native default theme).
- **Android keep-alive**: `SetKeepAliveEnabled`, `SetKeepAliveConfig` / **`StashNativeKeepAliveConfig`** — short foreground service during external browser flows ([stash-native README](https://github.com/stashgg/stash-native/blob/main/README.md)).

## [2.0.0] - 2025-03-16

### Changed

- **UPM (Unity Package Manager) support**: Package restructured for full UPM compatibility. Content moved from `Assets/Stash.Native/` to `Packages/gg.stash.unity/` with standard layout: `Runtime/`, `Editor/`, `Plugins/`, `Samples~/`.
- Added assembly definitions (`Stash.Unity`, `Stash.Unity.Editor`) for compilation isolation.
- Samples are now importable via Package Manager (Window > Package Manager > Stash for Unity > Samples).

### Installation

- **Git URL**: Add package from git URL: `https://github.com/stashgg/stash-unity.git?path=Packages/gg.stash.unity`
- **manifest.json**: Add `"gg.stash.unity": "https://github.com/stashgg/stash-unity.git?path=Packages/gg.stash.unity"` to dependencies.
