# Stash SDK for Unity ![buildtest](https://github.com/stashgg/stash-unity/actions/workflows/main.yml/badge.svg)

This package is designed to get your Unity project up and running with the Stash web shop in just a few steps. The package is lightweight and wraps Stash API endpoints without any external dependencies.

To get started, you need to import the latest package from the [releases](https://github.com/stashgg/stash-unity/releases) section and follow our [Unity guide](https://docs.stash.gg/docs/configure-unity-project).

## Requirements

The Stash package always targets the latest LTS version of Unity. We recommend you use the LTS version of Unity to build projects that are in production or about to ship. However, you should not encounter any issues when integrating or migrating into any other versions of Unity above the targeted release.

## Components

All components are optional, you can mix and match based on your needs.

| Component                                                                          | Description                                                           |
| ---------------------------------------------------------------------------------- | --------------------------------------------------------------------- |
| Stash.Core                                                                         | Provides core functionalities, API wrappers, and analytics for Stash. |
| [Stash.Popup](https://github.com/stashgg/stash-unity/tree/main/Assets/Stash.Popup) | Offers a customizable card-style popup for Stash Pay.                 |
| Stash.Samples                                                                      | Includes sample scene using the Stash SDK.                            |

### Event Tracking

Track user interactions and behavior in your game with built-in analytics:

```csharp
// Initialize SDK with analytics
StashSDK.Initialize("your-shop-handle", StashEnvironment.Production);

// Track click events
StashSDK.TrackClick("shop_button", "main_menu");

// Track with custom data
var customData = new Dictionary<string, object>
{
    { "itemId", "premium_pack" },
    { "price", 9.99f }
};
StashSDK.TrackClick("purchase_button", "store", customData);
```

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
