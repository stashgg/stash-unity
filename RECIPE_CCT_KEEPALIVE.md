# Chrome Custom Tabs Keep-Alive Service

This document explains how the Stash.Popup package keeps the Unity game alive when using Chrome Custom Tabs (CCT) for checkout on Android.

## Overview

On some Android devices with aggressive memory management, opening Chrome Custom Tabs can cause the Unity game to be suspended or killed. When the user returns from CCT, the game may restart instead of resuming, losing the checkout context.

The **Keep-Alive Service** is a lightweight Android foreground service that prevents this by keeping the Unity process in the foreground while the user is in CCT.

## How It Works

### Automatic Integration (Stash.Popup Users)

If you're using the Stash.Popup package, the keep-alive service is **automatically integrated** and requires no additional setup:

1. **Service starts** when CCT is launched (web checkout or Google Pay redirect)
2. **Service stops** when:
   - The app regains focus (user returns from CCT)
   - A soft timeout elapses (5 minutes on Android 13 and below, ~2.5 minutes on Android 14+)
   - The platform timeout fires (Android 14+ enforces a 3-minute limit for shortService)

The service shows a low-priority, silent notification while active. The notification uses generic text ("Active" / "Keeping the session active.") and the app's icon.

### Android Version Compatibility

| Android Version | Behavior |
|-----------------|----------|
| **Android 13 and below** | Standard foreground service with 5-minute soft timeout. No platform-enforced limit. |
| **Android 14+ (API 34+)** | Uses `shortService` type with 3-minute platform limit. Implements `onTimeout()` callback to stop cleanly. |

The service works on all supported Android versions. The Android 14-specific code (`shortService`, `onTimeout`) is guarded by version checks.

## Manifest Changes

The Stash.Popup post-build script automatically adds the following to your `AndroidManifest.xml`:

### Permissions

```xml
<uses-permission android:name="android.permission.FOREGROUND_SERVICE" />
<uses-permission android:name="android.permission.FOREGROUND_SERVICE_SHORT_SERVICE" />
<uses-permission android:name="android.permission.POST_NOTIFICATIONS" />
```

**Android 13+ (API 33+):** Stash.Popup automatically requests the **POST_NOTIFICATIONS** runtime permission when CCT checkout (or Google Pay redirect) is about to open. The system may show a one-time prompt; if the user grants it, the keep-alive notification will appear. If the user denies it, the foreground service still runs (Unity stays alive) but the notification may not be visible.

### Service Declaration

```xml
<service
    android:name="com.stash.popup.keepalive.StashKeepAliveService"
    android:foregroundServiceType="shortService"
    android:exported="false" />
```

## Manual Integration (Without Stash.Popup)

If you want to implement the keep-alive functionality without using the full Stash.Popup package, follow these steps:

### 1. Add Manifest Entries

Add the permissions and service declaration shown above to your `AndroidManifest.xml`.

### 2. Create the Service Classes

You need two Java classes in your Android plugin:

#### StashKeepAliveManager.java

A static utility class with `start()` and `stop()` methods:

```java
public static void start(Context context, String reason, String titleOverride, 
                         String bodyOverride, long timeoutBufferMs) {
    Context appContext = context.getApplicationContext();
    Intent intent = new Intent(appContext, StashKeepAliveService.class);
    intent.putExtra("reason", reason);
    intent.putExtra("title", titleOverride);
    intent.putExtra("body", bodyOverride);
    intent.putExtra("timeoutBufferMs", timeoutBufferMs);

    if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.O) {
        ContextCompat.startForegroundService(appContext, intent);
    } else {
        appContext.startService(intent);
    }
}

public static void stop(Context context) {
    Context appContext = context.getApplicationContext();
    Intent intent = new Intent(appContext, StashKeepAliveService.class);
    intent.setAction("ACTION_STOP");
    appContext.startService(intent);
}
```

#### StashKeepAliveService.java

A foreground service that:

1. Creates a notification channel (API 26+)
2. Shows a low-priority notification
3. Schedules a soft timeout
4. Handles `ACTION_STOP` to stop explicitly
5. **CRITICAL:** Implements `onTimeout(int)` for Android 14+

```java
@Override
public void onTimeout(int timeoutType) {
    if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.UPSIDE_DOWN_CAKE) {
        // Remove pending callbacks
        handler.removeCallbacks(softTimeoutRunnable);
        // Stop foreground immediately
        stopForeground(STOP_FOREGROUND_REMOVE);
        // Stop the service
        stopSelf();
    }
}
```

### 3. Start Before Launching CCT

Call `StashKeepAliveManager.start()` immediately before launching Chrome Custom Tabs:

```java
// Before opening CCT
StashKeepAliveManager.start(activity, "checkout", null, null, 30_000L);
// Then launch CCT
customTabsIntent.launchUrl(activity, Uri.parse(url));
```

The `timeoutBufferMs` parameter (30 seconds recommended) is subtracted from the platform limit on Android 14+ to ensure the service stops safely before the system forces it.

### 4. Stop on App Resume

Call `StashKeepAliveManager.stop()` when the app regains focus. In Unity, use `OnApplicationPause`:

```csharp
private void OnApplicationPause(bool pauseStatus)
{
#if UNITY_ANDROID && !UNITY_EDITOR
    if (!pauseStatus) // App is resuming
    {
        androidPluginInstance?.Call("stopKeepAlive");
    }
#endif
}
```

## Troubleshooting

### No Notification Shown on Android Device

1. **Android 13+ (API 33+):** Stash.Popup requests **POST_NOTIFICATIONS** automatically when opening CCT. If the user previously denied the permission, they must enable notifications in system Settings → Apps → [Your app] → Notifications, or reinstall and grant when prompted.
2. **Channel importance:** The service uses `IMPORTANCE_DEFAULT` so the notification appears in the status bar when notifications are allowed. If you previously built with an older Stash.Popup version that used `IMPORTANCE_LOW`, uninstall the app and reinstall so the notification channel is recreated.
3. **Device settings:** Ensure the app is allowed to show notifications in system Settings → Apps → [Your app] → Notifications.

### App Still Killed When Returning from CCT

1. Verify the service is declared in AndroidManifest.xml with `foregroundServiceType="shortService"`
2. Check that both permissions are declared
3. Ensure `StashKeepAliveManager.start()` is called **before** launching CCT
4. Verify `stopKeepAlive()` is called in `OnApplicationPause(false)`

### App Crashes/ANR After ~3 Minutes in CCT (Android 14+)

The `onTimeout(int)` callback is not being handled correctly. This method **must**:

1. Remove any pending timeout callbacks
2. Call `stopForeground(STOP_FOREGROUND_REMOVE)` 
3. Call `stopSelf()`

All of this must happen quickly (within seconds) to avoid an ANR.

### Notification Shows Indefinitely

The service should stop when the app resumes. Check that:

1. `OnApplicationPause` is implemented and calls `stopKeepAlive()`
2. The Android plugin's `stopKeepAlive()` method correctly calls `StashKeepAliveManager.stop()`

### Service Doesn't Start on Older Android Versions

On API < 26, use `context.startService()` instead of `ContextCompat.startForegroundService()`. The Stash.Popup implementation handles this automatically.

## Technical Details

### Notification Configuration

- **Channel ID:** `stash_keep_alive_channel`
- **Channel Name:** "Active session"
- **Importance:** Low (no sound/vibration)
- **Visibility:** Private
- **Category:** Service
- **Ongoing:** Yes (cannot be dismissed by user)

### Timeouts

| API Level | Soft Timeout | Platform Limit |
|-----------|--------------|----------------|
| < 34 | 5 minutes | None |
| >= 34 | ~2.5 minutes (3 min - buffer) | 3 minutes (hard limit) |

### Files

| File | Purpose |
|------|---------|
| `StashKeepAliveManager.java` | Unity-facing start/stop API |
| `StashKeepAliveService.java` | Foreground service implementation |
| `StashPopupAndroidPostProcess.cs` | Manifest injection at build time |
| `StashPayCard.cs` | `OnApplicationPause` to stop service |
