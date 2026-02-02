package com.stash.popup.keepalive;

import android.app.Notification;
import android.app.NotificationChannel;
import android.app.NotificationManager;
import android.app.PendingIntent;
import android.app.Service;
import android.content.Intent;
import android.content.pm.ServiceInfo;
import android.os.Build;
import android.os.Handler;
import android.os.IBinder;
import android.os.Looper;
import android.util.Log;

import androidx.core.app.NotificationCompat;
import androidx.core.content.ContextCompat;

/**
 * Lightweight foreground service that keeps the Unity process alive while the user
 * is in Chrome Custom Tabs for checkout or payment flows.
 * 
 * The service is short-lived by design and stops when:
 * 1. The game regains focus (Unity calls stopKeepAlive via OnApplicationPause)
 * 2. A soft timeout elapses (configurable, defaults to 5 minutes on API < 34, ~2.5 minutes on API 34+)
 * 3. The platform timeout fires (Android 14+ shortService has a hard 3-minute limit)
 * 
 * CRITICAL: On Android 14+, onTimeout(int) MUST be implemented and MUST stop the service
 * immediately to avoid ANR/crash. This implementation handles that requirement.
 * 
 * The notification uses generic, low-priority text suitable for any partner app.
 */
public class StashKeepAliveService extends Service {

    private static final String TAG = "StashKeepAlive";

    // Notification channel and ID
    public static final String CHANNEL_ID = "stash_keep_alive_channel";
    public static final int NOTIFICATION_ID = 8123;

    // Intent action to stop the service
    public static final String ACTION_STOP = "com.stash.popup.keepalive.ACTION_STOP";

    // Intent extras
    public static final String EXTRA_REASON = "reason";
    public static final String EXTRA_TITLE = "title";
    public static final String EXTRA_BODY = "body";
    public static final String EXTRA_TIMEOUT_BUFFER_MS = "timeoutBufferMs";

    /**
     * Android 14+ shortService hard limit is 3 minutes (180 seconds).
     * We use this as the maximum timeout on API 34+.
     */
    private static final long PLATFORM_SHORT_SERVICE_LIMIT_MS = 180_000L;

    /**
     * Default timeout for API < 34 (no platform limit): 5 minutes.
     * This gives users more time in CCT on older devices without the shortService restriction.
     */
    private static final long DEFAULT_TIMEOUT_PRE_API34_MS = 300_000L;

    private final Handler handler = new Handler(Looper.getMainLooper());
    private Runnable softTimeoutRunnable;

    @Override
    public IBinder onBind(Intent intent) {
        // Not a bound service
        return null;
    }

    @Override
    public int onStartCommand(Intent intent, int flags, int startId) {
        // Handle stop action
        String action = intent != null ? intent.getAction() : null;
        if (ACTION_STOP.equals(action)) {
            Log.d(TAG, "Stop requested explicitly via ACTION_STOP");
            stopServiceSafely("explicit-stop");
            return START_NOT_STICKY;
        }

        // Extract intent extras
        String reason = intent != null ? intent.getStringExtra(EXTRA_REASON) : null;
        String titleOverride = intent != null ? intent.getStringExtra(EXTRA_TITLE) : null;
        String bodyOverride = intent != null ? intent.getStringExtra(EXTRA_BODY) : null;
        long timeoutBufferMs = intent != null ? intent.getLongExtra(EXTRA_TIMEOUT_BUFFER_MS, 30_000L) : 30_000L;

        Log.d(TAG, "Start requested. reason=" + reason + " timeoutBufferMs=" + timeoutBufferMs
                + " titleOverride=" + titleOverride + " bodyOverride=" + bodyOverride);

        // Start foreground with notification
        startForegroundWithNotification(titleOverride, bodyOverride);

        // Schedule soft timeout
        scheduleSoftTimeout(timeoutBufferMs);

        return START_NOT_STICKY;
    }

    /**
     * Starts the service in foreground mode with a low-priority notification.
     * On Android 14+, uses FOREGROUND_SERVICE_TYPE_SHORT_SERVICE.
     */
    private void startForegroundWithNotification(String titleOverride, String bodyOverride) {
        createNotificationChannel();
        Notification notification = buildNotification(titleOverride, bodyOverride);

        try {
            if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.UPSIDE_DOWN_CAKE) {
                // Android 14 (API 34+): Use shortService type
                startForeground(
                        NOTIFICATION_ID,
                        notification,
                        ServiceInfo.FOREGROUND_SERVICE_TYPE_SHORT_SERVICE
                );
            } else {
                // Pre-Android 14: Regular foreground service
                startForeground(NOTIFICATION_ID, notification);
            }
        } catch (Throwable t) {
            Log.e(TAG, "Failed to start foreground", t);
            // If we can't start foreground, stop immediately
            stopSelf();
        }
    }

    /**
     * Builds the foreground notification with generic, partner-neutral text.
     */
    private Notification buildNotification(String titleOverride, String bodyOverride) {
        // Create pending intent to return to the app when notification is tapped
        Intent launchIntent = getPackageManager().getLaunchIntentForPackage(getPackageName());
        if (launchIntent != null) {
            launchIntent.setFlags(Intent.FLAG_ACTIVITY_NEW_TASK | Intent.FLAG_ACTIVITY_RESET_TASK_IF_NEEDED);
        }

        PendingIntent pendingIntent = null;
        if (launchIntent != null) {
            int flags = PendingIntent.FLAG_UPDATE_CURRENT;
            if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.M) {
                flags |= PendingIntent.FLAG_IMMUTABLE;
            }
            pendingIntent = PendingIntent.getActivity(this, 0, launchIntent, flags);
        }

        // Use provided text or generic defaults
        String title = titleOverride != null && !titleOverride.isEmpty() ? titleOverride : "Active";
        String contentText = bodyOverride != null && !bodyOverride.isEmpty() ? bodyOverride : "Keeping the session active.";

        // Get notification icon - use app icon as fallback
        int iconResId = getApplicationInfo().icon;
        if (iconResId == 0) {
            // Use Android's default icon if app icon not available
            iconResId = android.R.drawable.ic_dialog_info;
        }

        NotificationCompat.Builder builder = new NotificationCompat.Builder(this, CHANNEL_ID)
                .setContentTitle(title)
                .setContentText(contentText)
                .setSmallIcon(iconResId)
                .setCategory(Notification.CATEGORY_SERVICE)
                .setOngoing(true)
                .setOnlyAlertOnce(true)
                .setVisibility(NotificationCompat.VISIBILITY_PRIVATE)
                .setPriority(NotificationCompat.PRIORITY_DEFAULT);

        if (pendingIntent != null) {
            builder.setContentIntent(pendingIntent);
        }

        return builder.build();
    }

    /**
     * Creates the notification channel for Android 8.0+ (API 26+).
     */
    private void createNotificationChannel() {
        if (Build.VERSION.SDK_INT < Build.VERSION_CODES.O) {
            return;
        }

        // Use IMPORTANCE_DEFAULT so the notification appears in the status bar.
        // IMPORTANCE_LOW can cause the notification to be hidden on some devices/OEMs.
        // We keep it non-intrusive via setOnlyAlertOnce and no sound.
        NotificationChannel channel = new NotificationChannel(
                CHANNEL_ID,
                "Active session",
                NotificationManager.IMPORTANCE_DEFAULT
        );
        channel.setDescription("Keeps the app session briefly active in the background.");
        channel.setLockscreenVisibility(Notification.VISIBILITY_PRIVATE);
        channel.setShowBadge(false);
        channel.setSound(null, null);
        channel.enableVibration(false);

        NotificationManager manager = ContextCompat.getSystemService(this, NotificationManager.class);
        if (manager != null) {
            manager.createNotificationChannel(channel);
        }
    }

    /**
     * Schedules a soft timeout to stop the service before the platform limit (Android 14+)
     * or after a reasonable duration (older Android versions).
     * 
     * @param timeoutBufferMs Buffer to subtract from platform limit on API 34+
     */
    private void scheduleSoftTimeout(long timeoutBufferMs) {
        long totalTimeoutMs;

        if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.UPSIDE_DOWN_CAKE) {
            // Android 14+: Must finish before platform 3-minute limit
            long safeBuffer = Math.max(0L, timeoutBufferMs);
            long clampedBuffer = Math.min(safeBuffer, PLATFORM_SHORT_SERVICE_LIMIT_MS);
            totalTimeoutMs = Math.max(0L, PLATFORM_SHORT_SERVICE_LIMIT_MS - clampedBuffer);
        } else {
            // Pre-Android 14: Use longer timeout (5 minutes) since there's no platform limit
            totalTimeoutMs = DEFAULT_TIMEOUT_PRE_API34_MS;
        }

        // Cancel any existing timeout
        if (softTimeoutRunnable != null) {
            handler.removeCallbacks(softTimeoutRunnable);
        }

        softTimeoutRunnable = new Runnable() {
            @Override
            public void run() {
                Log.d(TAG, "Soft timeout reached after " + totalTimeoutMs + "ms; stopping service");
                stopServiceSafely("soft-timeout");
            }
        };

        handler.postDelayed(softTimeoutRunnable, totalTimeoutMs);
        Log.d(TAG, "Scheduled soft timeout in " + totalTimeoutMs + "ms");
    }

    /**
     * Safely stops the foreground service, cleaning up resources.
     * 
     * @param trigger Debug string indicating why the service is stopping
     */
    private void stopServiceSafely(String trigger) {
        Log.d(TAG, "Stopping foreground service. trigger=" + trigger);

        // Remove pending soft timeout
        if (softTimeoutRunnable != null) {
            handler.removeCallbacks(softTimeoutRunnable);
            softTimeoutRunnable = null;
        }

        // Stop foreground mode
        try {
            if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.N) {
                stopForeground(STOP_FOREGROUND_REMOVE);
            } else {
                stopForeground(true);
            }
        } catch (Throwable t) {
            Log.w(TAG, "stopForeground failed", t);
        }

        // Stop the service
        stopSelf();
    }

    /**
     * CRITICAL: Android 14+ calls this when the shortService exceeds the platform time limit (~3 minutes).
     * 
     * This method MUST complete extremely quickly to avoid ANR. We immediately stop foreground
     * mode and the service. The OS timeout always wins even if our soft timeout has not fired yet.
     * 
     * If this callback is not handled properly, the app will crash with an ANR.
     * 
     * @param timeoutType The type of timeout (START_STICKY, etc.)
     */
    @Override
    public void onTimeout(int timeoutType) {
        if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.UPSIDE_DOWN_CAKE) {
            Log.w(TAG, "OS shortService timeout triggered (timeoutType=" + timeoutType + "); stopping immediately");

            // Remove soft timeout runnable immediately
            if (softTimeoutRunnable != null) {
                handler.removeCallbacks(softTimeoutRunnable);
                softTimeoutRunnable = null;
            }

            // Stop foreground immediately - MUST be fast to avoid ANR
            try {
                stopForeground(STOP_FOREGROUND_REMOVE);
            } catch (Throwable t) {
                Log.w(TAG, "stopForeground in onTimeout failed", t);
            }

            // Stop the service
            stopSelf();
        }
    }

    @Override
    public void onDestroy() {
        super.onDestroy();
        Log.d(TAG, "Service onDestroy");

        // Cleanup: remove any pending callbacks
        if (softTimeoutRunnable != null) {
            handler.removeCallbacks(softTimeoutRunnable);
            softTimeoutRunnable = null;
        }
    }
}
