# Stash SDK for Unity ![buildtest](https://github.com/stashgg/stash-unity/actions/workflows/main.yml/badge.svg)

Unity package for integrating Stash Pay checkout flows and web shop functionality. The package is lightweight and wraps Stash API endpoints without external dependencies.

## Requirements

- Unity 2019.4+ (LTS recommended)
- iOS 12.0+ / Android API 21+

## Components

All components are optional. Mix and match based on your needs.

| Component | Product | Description |
|-----------|---------|-------------|
| **[Stash.Popup](Assets/Stash.Popup/README.md)** | Stash Pay | In-game popup for Stash Pay checkout on iOS and Android. Includes Sample scene. |
| **Stash.DemoApp** | — | Stash Demo app and playground for testing. **(Don't import)** |

## Sample

You can try **Stash.DemoApp** directly in online hosted emulators:

- **iOS:** [Open in Appetize.io](https://appetize.io/app/b_eyszozcrmyt2zifoh5bjyiifha)
- **Android:** [Open in Appetize.io](https://appetize.io/app/b_e7zfxgltohxm2rd5aw4zplzmwq?device=pixel7&osVersion=13.0&toolbar=true)

## Installation

### Import package manually

1. Download the [latest release](https://github.com/stashgg/stash-unity/releases) or repository as a zip file.
2. Import the `.unitypackage` file into your Unity project
3. Select the components you need (Stash.Popup for Stash Pay)

### Import via Git URL

1. Open **Window > Package Manager**
2. Click **+** → **Add package from git URL**
3. Enter: `https://github.com/stashgg/stash-unity.git?path=Assets`

## Documentation

- [Stash Documentation](https://docs.stash.gg) - Full API reference and integration guides.
- [Stash.Popup Documentation](Assets/Stash.Popup/README.md) - Native checkout popup integration

## Versioning

This package follows [Semantic Versioning](https://semver.org/) (major.minor.patch):

- **Major**: Breaking changes
- **Minor**: New features (backward compatible)
- **Patch**: Bug fixes

## Support

- Documentation: https://docs.stash.gg
- Email: developers@stash.gg
- Issues: [GitHub Issues](https://github.com/stashgg/stash-unity/issues/new)
