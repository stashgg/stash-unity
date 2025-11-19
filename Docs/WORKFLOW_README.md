# GitHub Actions Workflow Overview

This workflow builds the Unity project for multiple platforms and handles distribution.

## Triggers

- **Push to `main`**: Builds all platforms
- **Manual dispatch**: Build specific platform(s)
- **Workflow run**: Triggered by "Create Release" workflow completion

## Platforms

- Android
- iOS (device)
- iOS Simulator - For Simulator/Appetize
- macOS (StandaloneOSX)
- Windows (StandaloneWindows64)

## Key Features

### Build Number Management
- Generates a shared Unix timestamp build number for all platforms
- Ensures Android and iOS builds have identical build numbers
- Uses Unity Editor script (`SetAndroidVersionCode.cs`) to set Android version code

### Build Process
- Uses `game-ci/unity-builder` for Unity builds
- Caches Unity Library folder for faster builds
- Handles platform-specific setup (iOS certificates, Android keystores, etc.)

### Distribution
- **Google Cloud Storage**: Uploads builds to bucket `stash_sdk_demo/{platform}/latest/`
- **TestFlight**: Uploads iOS builds automatically (on push/workflow_run)
- **Appetize**: Uploads Android and iOS Simulator builds for web-based testing
- **Stash Studio**: Uploads macOS and Windows builds for launcher testing

## Jobs

1. **determine-platforms**: Determines which platforms to build and generates shared build number
2. **build**: Matrix job that builds for each platform in parallel
3. **upload-macos-to-stash**: Uploads macOS build to Stash Studio
4. **upload-windows-to-stash**: Uploads Windows build to Stash Studio
5. **upload-to-appetize**: Uploads Android and iOS Simulator builds to Appetize

## Secrets Required

- Unity license credentials (`UNITY_LICENSE`, `UNITY_EMAIL`, `UNITY_PASSWORD`)
- iOS signing (`IOS_P12_BASE64`, `IOS_P12_PASSWORD`, `IOS_TEAM_ID`, `IOS_PROFILE_UUID`)
- Android signing (`ANDROID_KEYSTORE_BASE64`, `ANDROID_KEYSTORE_PASS`, etc.)
- App Store Connect (`APPSTORE_PRIVATE_KEY`, `APPSTORE_KEY_ID`, `APPSTORE_ISSUER_ID`)
- GCS credentials (`PIE_PROD_GCP_SERVICE_ACCOUNT_CREDENTIALS_FOR_UPLOAD_DEMO`)
- Appetize API key (`APPETIZE_API_KEY`)
- Stash API key (`STASH_API_KEY`)

