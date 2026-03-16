# Changelog

All notable changes to this package will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/).

## [2.0.0] - 2025-03-16

### Changed

- **UPM (Unity Package Manager) support**: Package restructured for full UPM compatibility. Content moved from `Assets/Stash.Native/` to `Packages/gg.stash.unity/` with standard layout: `Runtime/`, `Editor/`, `Plugins/`, `Samples~/`.
- Added assembly definitions (`Stash.Unity`, `Stash.Unity.Editor`) for compilation isolation.
- Samples are now importable via Package Manager (Window > Package Manager > Stash for Unity > Samples).

### Installation

- **Git URL**: Add package from git URL: `https://github.com/stashgg/stash-unity.git?path=Packages/gg.stash.unity`
- **manifest.json**: Add `"gg.stash.unity": "https://github.com/stashgg/stash-unity.git?path=Packages/gg.stash.unity"` to dependencies.
