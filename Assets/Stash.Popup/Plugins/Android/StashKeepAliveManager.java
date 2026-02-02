package com.stash.popup.keepalive;

import android.app.Activity;
import android.content.Context;
import android.content.Intent;
import android.content.pm.PackageManager;
import android.os.Build;
import android.util.Log;

import androidx.core.app.ActivityCompat;
import androidx.core.content.ContextCompat;

/**
 * Unity-facing helper for managing the session keep-alive foreground service.
 * 
 * This manager provides static methods to start and stop the StashKeepAliveService,
 * which keeps the Unity process alive while the user is in Chrome Custom Tabs.
 * 
 * Usage from C# via AndroidJavaClass:
 * - Call start() before launching Chrome Custom Tabs
 * - Call stop() when the app regains focus (OnApplicationPause(false))
 * 
 * The service automatically stops via soft timeout or platform timeout (Android 14+),
 * so calling stop() when not running is safe and acts as a no-op.
 */
public final class StashKeepAliveManager {

    private static final String TAG = "StashKeepAliveMgr";

    /** Android 13 (API 33) introduced runtime permission for notifications. */
    private static final int API_LEVEL_NEEDS_POST_NOTIFICATIONS = 33;
    private static final String PERMISSION_POST_NOTIFICATIONS = "android.permission.POST_NOTIFICATIONS";

    private StashKeepAliveManager() {
        // Utility class - prevent instantiation
    }

    /**
     * Requests the POST_NOTIFICATIONS permission if needed (Android 13+).
     * Call this before start() so the keep-alive notification can be shown.
     * Safe to call on older API levels (no-op). Does not block; permission result is async.
     *
     * @param activity The activity used to request the permission (e.g. UnityPlayer.currentActivity)
     */
    public static void requestNotificationPermissionIfNeeded(Activity activity) {
        if (activity == null) {
            return;
        }
        if (Build.VERSION.SDK_INT < API_LEVEL_NEEDS_POST_NOTIFICATIONS) {
            return;
        }
        try {
            if (ContextCompat.checkSelfPermission(activity, PERMISSION_POST_NOTIFICATIONS)
                    != PackageManager.PERMISSION_GRANTED) {
                ActivityCompat.requestPermissions(activity, new String[]{PERMISSION_POST_NOTIFICATIONS}, 0);
                Log.d(TAG, "POST_NOTIFICATIONS permission requested");
            }
        } catch (Throwable t) {
            Log.w(TAG, "Could not request POST_NOTIFICATIONS", t);
        }
    }

    /**
     * Starts the keep-alive foreground service.
     * 
     * @param context         Application or activity context
     * @param reason          Debug reason for starting (e.g., "checkout", "gpay_redirect")
     * @param titleOverride   Optional notification title (null = use default "Active")
     * @param bodyOverride    Optional notification body (null = use default message)
     * @param timeoutBufferMs Buffer time to subtract from platform limit for soft timeout.
     *                        Recommended: 30000 (30 seconds) to stop safely before platform limit.
     *                        On API < 34, a longer timeout (5 minutes) is used since there's no platform limit.
     */
    public static void start(Context context, String reason, String titleOverride, String bodyOverride, long timeoutBufferMs) {
        if (context == null) {
            Log.w(TAG, "Cannot start keep-alive: context is null");
            return;
        }

        try {
            Context appContext = context.getApplicationContext();
            Intent intent = new Intent(appContext, StashKeepAliveService.class);
            intent.putExtra(StashKeepAliveService.EXTRA_REASON, reason);
            intent.putExtra(StashKeepAliveService.EXTRA_TITLE, titleOverride);
            intent.putExtra(StashKeepAliveService.EXTRA_BODY, bodyOverride);
            intent.putExtra(StashKeepAliveService.EXTRA_TIMEOUT_BUFFER_MS, timeoutBufferMs);

            // Use startForegroundService on API 26+ (required for foreground services)
            if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.O) {
                ContextCompat.startForegroundService(appContext, intent);
            } else {
                appContext.startService(intent);
            }

            Log.d(TAG, "Keep-alive service start requested. reason=" + reason);
        } catch (Throwable t) {
            Log.e(TAG, "Failed to start keep-alive service", t);
        }
    }

    /**
     * Stops the keep-alive foreground service.
     * 
     * Safe to call even if the service is not running - acts as a no-op.
     * 
     * @param context Application or activity context
     */
    public static void stop(Context context) {
        if (context == null) {
            Log.w(TAG, "Cannot stop keep-alive: context is null");
            return;
        }

        try {
            Context appContext = context.getApplicationContext();
            Intent intent = new Intent(appContext, StashKeepAliveService.class);
            intent.setAction(StashKeepAliveService.ACTION_STOP);

            // startService with ACTION_STOP is safe; the service interprets this action and shuts down.
            // If service is not running, this is ignored.
            appContext.startService(intent);

            Log.d(TAG, "Keep-alive service stop requested");
        } catch (Throwable t) {
            // Swallow exceptions - service may already be stopped or never started
            Log.w(TAG, "Stop requested but service could not be started/stopped", t);
        }
    }
}
