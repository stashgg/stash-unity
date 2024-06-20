# Stash SDK for Unity ![buildtest](https://github.com/stashgg/stash-unity/actions/workflows/main.yml/badge.svg)

<img src="https://i.ibb.co/LYPjvRj/3-D-Yellow.png" width="40%"></img>

## About

This package is designed to get your Unity project up and running with Stash web shop in a few easy steps. Package is wrapping Stash API endpoints and is very lightweight with no external dependencies. 

To start with the SDK, you need to import the package from the releases tab and read [getting started guide](https://docs.stash.gg/docs/configure-unity-project).

## Usage

To interact with Stash API and handle responses, the SDK offers the `StashAuth`, `StashLauncher` and other classes, named according to the product you plan to use.
All classes are static, with no inheritance to the MonoBehaviour, so they can only be called when needed.

## Requirements

Stash package always targets the latest LTS version of Unity. We recommend you use the LTS version of Unity to build projects that are in production or about to ship. However, you should not encounter any issues when integrating or migrating into any other versions of Unity above the targeted release.

## Installation

### Import package manually
1. Download [the latest build from the release page](https://github.com/stashgg/stash-unity/releases)
2. Import the `.unitypackage` file into your Unity game using the [local asset package import](https://docs.unity3d.com/Manual/AssetPackagesImport.html) process.
3. Optionally select the `Scenes` folder to test out our reference implementations.

### Import package via repository
1. In the Unity editor main menu, click Window > Package Manager.
2. Click the + icon and select Add package from git URL.
3. Specify the git repository URL: https://github.com/stashgg/stash-unity.git?path=Assets/Stash.

## Changelog

Package follows Semantic Versioning `(major.minor.patch)`. Any potential breaking changes will always cause a major version increment, non-breaking new features will cause a minor version increment, and bugfixes will cause a patch version increment.
A full version changelog is available in the [changelog](/CHANGELOG.md) file.

## Feedback and troubleshooting

If you run into any problems or have a feature request, open up a [new issue](https://github.com/stashgg/stash-unity/issues/new) in the repository. Please follow the issue/request template.
