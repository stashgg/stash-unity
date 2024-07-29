# Stash SDK for Unity ![buildtest](https://github.com/stashgg/stash-unity/actions/workflows/main.yml/badge.svg)

This package is designed to get your Unity project up and running with the Stash web shop in just a few steps. The package is lightweight and wraps Stash API endpoints without any external dependencies. 

To get started, you need to import the latest package from the [releases](https://github.com/stashgg/stash-unity/releases) section and follow our [Unity guide](https://docs.stash.gg/docs/configure-unity-project).

## Usage

To interact with the Stash API and handle responses, the SDK offers the `StashAuth`, `StashLauncher`, and other classes (named according to the product you want to use).
All classes are static, with no inheritance to [MonoBehaviour](https://docs.unity3d.com/Manual/class-MonoBehaviour.html), so there's need to place them in the scene, and can be called only when needed.

## Sample

This repository contains a sample project. The provided scene is always up to date and pre-built for testing in the **Actions** tab.
For more samples, see the [playground repository](https://github.com/stashgg/stash-playground). Keep in mind that the samples in the playground repository are just examples, and that they shouldn't be used as-is in production.

## Requirements

The Stash package always targets the latest LTS version of Unity. We recommend you use the LTS version of Unity to build projects that are in production or about to ship. However, you should not encounter any issues when integrating or migrating into any other versions of Unity above the targeted release.

## Installation

### Import package manually

1. Download [the latest build from the release page](https://github.com/stashgg/stash-unity/releases).
2. Import the `.unitypackage` file into your Unity game using the [local asset package import](https://docs.unity3d.com/Manual/AssetPackagesImport.html) process.
3. Optionally select the `Scenes` folder to test out our reference implementations.

### Import package via repository

1. In the Unity editor main menu, click **Window > Package Manager**.
2. Click the **+** icon and select **Add package from git URL**.
3. Specify the git repository URL: https://github.com/stashgg/stash-unity.git?path=Assets/Stash.

## Changelog

This package follows Semantic Versioning `(major.minor.patch)`:

- Breaking changes always result in a major version increment.
- Non-breaking new features result in a minor version increment
- Bug fixes result in a patch version increment.

A full version changelog is available in the [changelog](/CHANGELOG.md) file.

## Feedback and troubleshooting

If you run into any problems or have a feature request, open up a [new issue](https://github.com/stashgg/stash-unity/issues/new) in the repository. Please follow the issue template.
