# Stash for Unity ![buildtest](https://github.com/stashgg/stash-unity/actions/workflows/main.yml/badge.svg)

<p align="left">
  <img src="https://github.com/stashgg/stash-native/raw/main/.github/assets/stash_unity.png" width="128" height="128" alt="Stash Unity Logo"/>
</p>

Unity package wrapper for [stash-native](https://github.com/stashgg/stash-native) for integrating Stash services and in-app Stash Pay checkout and webshop. 

## Requirements

- Unity 2019.4+ (LTS recommended)
- iOS 12.0+ / Android API 21+

## Package Components

All components are optional. Mix and match based on your needs.

| Component | Product | Description |
|-----------|---------|-------------|
| **[Stash.Popup](Assets/Stash.Popup/README.md)** | Stash Pay | In-game popup for Stash Pay checkout on iOS and Android. Includes Sample scene. |
| **Stash.DemoApp** | — | Stash Demo app and playground for testing. **(Don't import)** |

## Sample Apps

You can build **Stash.DemoApp** in Unity to test Stash flows in your own project, or try them right away in your browser using the Appetize online emulator:

- **iOS:** [Open in Appetize.io](https://appetize.io/app/b_eyszozcrmyt2zifoh5bjyiifha)
- **Android:** [Open in Appetize.io](https://appetize.io/app/b_e7zfxgltohxm2rd5aw4zplzmwq?device=pixel7&osVersion=13.0&toolbar=true)

## Downloads

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
- [Stash.Popup Readme](Assets/Stash.Popup/README.md) - Native checkout popup integration

## Versioning

This package follows [Semantic Versioning](https://semver.org/) (major.minor.patch):

- **Major**: Breaking changes
- **Minor**: New features (backward compatible)
- **Patch**: Bug fixes

## Support

- Documentation: https://docs.stash.gg
- Email: developers@stash.gg
- Issues: [GitHub Issues](https://github.com/stashgg/stash-unity/issues/new)
