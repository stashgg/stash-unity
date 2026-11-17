# Stash for Unity - QR Code Linking

Optional module for [Stash for Unity](https://github.com/stashgg/stash-unity). It bundles the native `StashNativeCodeLink` 3.0.0 binaries (Android AAR, iOS XCFramework) that enable `StashNative.Instance.OpenScanner(...)`.

Install it only if your game links webshop accounts by scanning the QR code shown in the Stash webshop. Without this package, `OpenScanner` presents nothing and `StashNative.Instance.OnScannerError` fires with `StashNativeScannerError.ScannerUnavailable`. The core package declares no camera permission.

## Installation

Install the core package first, then add this one from the same repository:

```
https://github.com/stashgg/stash-unity.git?path=Packages/gg.stash.unity.codelink
```

## Platform setup

**iOS**: set **Camera Usage Description** in *Player Settings → iOS → Other Settings* (this is `NSCameraUsageDescription`). The scanner reports `MissingCameraUsageDescription` if it is empty. `StashNativeCodeLink.xcframework` is marked *Add to Embedded Binaries* by the package.

**Android**: the module's manifest adds `android.permission.CAMERA` and the optional `android.hardware.camera` feature. It needs these libraries on the classpath:

```gradle
implementation 'androidx.camera:camera-camera2:1.3.4'
implementation 'androidx.camera:camera-lifecycle:1.3.4'
implementation 'androidx.camera:camera-view:1.3.4'
implementation 'com.google.zxing:core:3.5.3'
```

Add them to your `mainTemplate.gradle`, or let the External Dependency Manager (EDM4U) resolve `Editor/StashNativeCodeLinkDependencies.xml` from this package.

CameraX is pinned to 1.3.4 on purpose. The D8 dexer in Unity 2022.3's Android Gradle Plugin 7.4 cannot process CameraX 1.4.x (`D8: java.lang.NullPointerException` during `mergeExtDexRelease`). A plain `implementation` line is only a request, so force the version in case another plugin asks for 1.4.x:

```gradle
configurations.all {
    resolutionStrategy {
        force 'androidx.camera:camera-core:1.3.4'
        force 'androidx.camera:camera-camera2:1.3.4'
        force 'androidx.camera:camera-lifecycle:1.3.4'
        force 'androidx.camera:camera-view:1.3.4'
    }
}
```

Unity 6 (AGP 8.x) can use CameraX 1.4.2, the version the native SDK is built against, and drop the pin. The module uses no CameraX API newer than 1.3.0.

## Usage

```csharp
using Stash.Native;

StashNative.Instance.OnQrCodeScanned += code => LinkAccount(code);
StashNative.Instance.OnScannerDismissed += () => Debug.Log("Scanner closed");
StashNative.Instance.OnScannerError += err => Debug.LogWarning("Scanner: " + err);

var config = StashNativeScannerConfig.Default;
config.headerText = "Scan the webshop code";
StashNative.Instance.OpenScanner(config);
```

See the [QR Code Linking guide](https://stashgg.github.io/stash-native/usage/codelink.html) for the account-linking flow.
