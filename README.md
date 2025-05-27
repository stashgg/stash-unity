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

# Stash Pay Card for Unity

This plugin provides a native iOS modal card view for displaying web content in Unity apps.

## Features

- Open URLs in a customizable card view with native iOS styling
- Configure card height and vertical position on screen
- Supports multiple presentation styles: bottom sheet, center dialog, or custom position
- Swipe-to-dismiss gesture with proper animation
- Callback support when card is dismissed
- Graceful fallback to system browser on non-iOS platforms

## Usage

### Basic Usage

```csharp
// Open a URL in the default bottom sheet style
StashPayCard.Instance.OpenURL("https://example.com", () => {
    Debug.Log("Card was dismissed!");
});
```

### Customizing Card Position

#### Bottom Sheet Style (Default)

```csharp
// Configure as bottom sheet with 40% screen height (default)
StashPayCard.Instance.ConfigureAsBottomSheet();

// Or specify a custom height (30% of screen)
StashPayCard.Instance.ConfigureAsBottomSheet(0.3f);

// Open URL with the configured style
StashPayCard.Instance.OpenURL("https://example.com");
```

#### Center Dialog Style

```csharp
// Configure as center dialog with 40% screen height
StashPayCard.Instance.ConfigureAsDialog();

// Or specify a custom height (50% of screen)
StashPayCard.Instance.ConfigureAsDialog(0.5f);

// Open URL with the configured style
StashPayCard.Instance.OpenURL("https://example.com");
```

#### Custom Position

```csharp
// Set card height (20% of screen height)
StashPayCard.Instance.SetCardHeightRatio(0.2f);

// Set vertical position (0.0 = top, 0.5 = middle, 1.0 = bottom)
StashPayCard.Instance.SetCardVerticalPosition(0.7f); // 70% from top

// Open URL with the configured style
StashPayCard.Instance.OpenURL("https://example.com");
```

### Available Configuration Options

- **SetCardHeightRatio(float heightRatio)**: Sets the height of the card as a percentage of screen height (0.1 to 0.9).
- **SetCardVerticalPosition(float verticalPosition)**: Sets the vertical position of the card (0.0 = top, 0.5 = middle, 1.0 = bottom).
- **ConfigureAsBottomSheet(float heightRatio = 0.4f)**: Configure as bottom sheet with optional height ratio.
- **ConfigureAsDialog(float heightRatio = 0.4f)**: Configure as centered dialog with optional height ratio.

## Platform Support

- iOS: Native card implementation
- Other platforms: Falls back to opening URL in default browser

## Sample

See the `StashPaySample.cs` script for a complete example of how to use the card with different configuration options.
