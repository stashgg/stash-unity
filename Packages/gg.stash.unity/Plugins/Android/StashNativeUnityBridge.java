package com.stash.unity;

import android.app.Activity;
import android.os.Handler;
import android.os.Looper;
import android.util.Log;
import androidx.annotation.Keep;
import com.stash.stashnative.StashNative;
import com.unity3d.player.UnityPlayer;

/**
 * Forwards Unity C# calls to StashNative (3.x) and listener callbacks back to Unity.
 * Native calls run on the Android main thread; Unity invokes this bridge from its own thread.
 *
 * Session-scoped callbacks are tagged "<generation>|<payload>" with the generation C# passed to the open,
 * so C# can drop events from a session it has already reset.
 *
 * @Keep: C# reaches this class only by name through JNI, so R8 must not strip or rename it.
 */
@Keep
public class StashNativeUnityBridge implements StashNative.StashNativeListener {
    private static final String TAG = "StashNativeUnityBridge";
    private static final String UNITY_GAME_OBJECT = "StashNative";
    private static final String SCANNER_CLASS = "com.stash.stashnative.codelink.StashScannerSupport";

    private static StashNativeUnityBridge instance;
    private static final Handler MAIN = new Handler(Looper.getMainLooper());

    private StashNative stashNative;
    private volatile int generation;
    private volatile boolean detached;
    /** Set when an open is posted to main and cleared once native has taken it, so isCurrentlyPresented() is true in the frame that opened. */
    private volatile boolean openPending;

    @Keep
    public static synchronized StashNativeUnityBridge getInstance() {
        if (instance == null) {
            instance = new StashNativeUnityBridge();
        }
        return instance;
    }

    private StashNativeUnityBridge() {
        try {
            stashNative = StashNative.getInstance();
        } catch (Throwable t) {
            Log.e(TAG, "Failed to get StashNative instance: " + t.getMessage());
        }
    }

    // ---- helpers -------------------------------------------------------------------------

    private static void runOnMain(Runnable r) {
        if (Looper.myLooper() == Looper.getMainLooper()) {
            r.run();
        } else {
            MAIN.post(r);
        }
    }

    private static Activity currentActivity() {
        Activity a = UnityPlayer.currentActivity;
        if (a == null) Log.e(TAG, "UnityPlayer.currentActivity is null; the native SDK will ignore this open");
        return a;
    }

    /** Must run on the main thread. Re-asserts the listener in case other native code replaced it. */
    private void ensureListener() {
        if (stashNative == null) return;
        detached = false;
        try {
            if (stashNative.getListener() != this) stashNative.setListener(this);
        } catch (Throwable t) {
            Log.e(TAG, "setListener failed: " + t.getMessage());
        }
    }

    private void send(String methodName, String message) {
        if (detached) return;
        try {
            UnityPlayer.UnitySendMessage(UNITY_GAME_OBJECT, methodName, message != null ? message : "");
        } catch (Throwable t) {
            Log.e(TAG, "UnitySendMessage failed: " + methodName + " " + t.getMessage());
        }
    }

    private void sendSessionEvent(String methodName, String payload) {
        send(methodName, generation + "|" + (payload != null ? payload : ""));
    }

    private static String emptyToNull(String s) {
        return (s == null || s.isEmpty()) ? null : s;
    }

    // ---- StashNativeListener --------------------------------------------------------------

    @Override public void onPaymentSuccess(String order) { sendSessionEvent("OnAndroidPaymentSuccess", order); }
    @Override public void onPaymentFailure() { sendSessionEvent("OnAndroidPaymentFailure", null); }
    @Override public void onCardDismissed() { sendSessionEvent("OnAndroidCardDismissed", null); }
    @Override public void onScannerDismissed() { sendSessionEvent("OnAndroidScannerDismissed", null); }
    @Override public void onPageLoaded(long loadTimeMs) { sendSessionEvent("OnAndroidPageLoaded", String.valueOf(loadTimeMs)); }
    @Override public void onNetworkError() { sendSessionEvent("OnAndroidNetworkError", null); }
    @Override public void onExternalPayment(String url) { sendSessionEvent("OnAndroidExternalPayment", url); }
    /** Not session-scoped: a browser opened with openBrowser has no card session, and the event must survive a reset. */
    @Override public void onBrowserDismissed() { send("OnAndroidBrowserDismissed", ""); }
    @Override public void onQrCodeScanned(String content) { sendSessionEvent("OnAndroidQrCodeScanned", content); }

    @Override
    public void onScannerError(StashNative.ScannerError error) {
        // Enum name, e.g. "CAMERA_PERMISSION_DENIED"; C# maps it to StashNativeScannerError.
        sendSessionEvent("OnAndroidScannerError", error != null ? error.name() : "");
    }

    // ---- static SDK settings --------------------------------------------------------------

    public String getVersion() {
        try { return StashNative.getVersion(); } catch (Throwable t) { return ""; }
    }

    /** True when the optional StashNativeCodeLink module and its CameraX/ZXing dependencies are present. */
    public boolean isScannerAvailable() {
        try {
            Class.forName(SCANNER_CLASS, false, getClass().getClassLoader());
            Class.forName("androidx.camera.core.ImageAnalysis", false, getClass().getClassLoader());
            Class.forName("com.google.zxing.MultiFormatReader", false, getClass().getClassLoader());
            return true;
        } catch (Throwable t) {
            return false;
        }
    }

    public void setInspectableWebViewsEnabled(boolean enabled) {
        try {
            StashNative.setInspectableWebViewsEnabled(enabled);
        } catch (Throwable t) {
            Log.e(TAG, "setInspectableWebViewsEnabled failed: " + t.getMessage());
        }
    }

    public boolean isInspectableWebViewsEnabled() {
        try { return StashNative.isInspectableWebViewsEnabled(); } catch (Throwable t) { return false; }
    }

    /** JPEG/PNG bytes shown behind the portrait card on landscape hosts; consumed by the next accepted open. */
    public void setBackdropBytes(byte[] bytes) {
        try {
            StashNative.setBackdropBytes(bytes);
        } catch (Throwable t) {
            Log.e(TAG, "setBackdropBytes failed: " + t.getMessage());
        }
    }

    /** Drops a backdrop set for an open that never happened. Separate method so no null has to cross JNI. */
    public void clearBackdrop() {
        try {
            StashNative.setBackdropBytes(null);
        } catch (Throwable t) {
            Log.e(TAG, "clearBackdrop failed: " + t.getMessage());
        }
    }

    // ---- keep-alive -----------------------------------------------------------------------

    public void setKeepAliveEnabled(final boolean enabled) {
        if (stashNative == null) return;
        try {
            stashNative.setKeepAliveEnabled(enabled);
        } catch (Throwable t) {
            Log.e(TAG, "setKeepAliveEnabled failed: " + t.getMessage());
        }
    }

    public boolean isKeepAliveEnabled() {
        if (stashNative == null) return false;
        try { return stashNative.isKeepAliveEnabled(); } catch (Throwable t) { return false; }
    }

    public void setKeepAliveConfig(String notificationTitle, String notificationText, int notificationIconResId) {
        if (stashNative == null) return;
        try {
            StashNative.KeepAliveConfig cfg = new StashNative.KeepAliveConfig();
            cfg.notificationTitle = notificationTitle;
            cfg.notificationText = notificationText;
            cfg.notificationIconResId = notificationIconResId;
            stashNative.setKeepAliveConfig(cfg);
        } catch (Throwable t) {
            Log.e(TAG, "setKeepAliveConfig failed: " + t.getMessage());
        }
    }

    // ---- card -----------------------------------------------------------------------------

    public void openCard(final int gen, final String url) {
        openCardInternal(gen, url, null);
    }

    public void openCardWithConfig(final int gen, final String url,
            final float preferredContentWidth, final float preferredContentHeight,
            final float maximumContentHeight, final float edgeMargin,
            final boolean allowDismiss, final boolean autoClose, final int orientationPreference) {
        StashNative.CardConfig config = new StashNative.CardConfig();
        config.preferredContentWidth = preferredContentWidth;
        config.preferredContentHeight = preferredContentHeight;
        config.maximumContentHeight = maximumContentHeight;
        config.edgeMargin = edgeMargin;
        config.allowDismiss = allowDismiss;
        config.autoClose = autoClose;
        config.orientationPreference = orientationPreference == 1
                ? StashNative.CardConfig.ORIENTATION_PORTRAIT
                : StashNative.CardConfig.ORIENTATION_FOLLOW_HOST;
        openCardInternal(gen, url, config);
    }

    private void openCardInternal(final int gen, final String url, final StashNative.CardConfig config) {
        if (stashNative == null || url == null) return;
        openPending = true;
        runOnMain(() -> {
            try {
                generation = gen;
                ensureListener();
                stashNative.openCard(currentActivity(), url, config);
            } catch (Throwable t) {
                Log.e(TAG, "openCard failed: " + t.getMessage());
            } finally {
                openPending = false;
            }
        });
    }

    // ---- scanner (requires the StashNativeCodeLink AAR; otherwise onScannerError SCANNER_UNAVAILABLE) --

    public void openScanner(final int gen) {
        openScannerInternal(gen, null);
    }

    public void openScannerWithConfig(final int gen, final String headerText, final String calloutTitle, final String calloutSubtitle,
            final String cameraUnavailableText, final String connectedText, final boolean showCallout) {
        StashNative.ScannerConfig config = new StashNative.ScannerConfig();
        config.headerText = emptyToNull(headerText);
        config.calloutTitle = emptyToNull(calloutTitle);
        config.calloutSubtitle = emptyToNull(calloutSubtitle);
        config.cameraUnavailableText = emptyToNull(cameraUnavailableText);
        config.connectedText = emptyToNull(connectedText);
        config.showCallout = showCallout;
        openScannerInternal(gen, config);
    }

    private void openScannerInternal(final int gen, final StashNative.ScannerConfig config) {
        if (stashNative == null) return;
        openPending = true;
        runOnMain(() -> {
            try {
                generation = gen;
                ensureListener();
                if (config != null) stashNative.openScanner(currentActivity(), config);
                else stashNative.openScanner(currentActivity());
            } catch (Throwable t) {
                Log.e(TAG, "openScanner failed: " + t.getMessage());
            } finally {
                openPending = false;
            }
        });
    }

    // ---- browser --------------------------------------------------------------------------

    public void openBrowser(final String url) {
        if (stashNative == null || url == null) return;
        runOnMain(() -> {
            try {
                ensureListener();
                stashNative.openBrowser(currentActivity(), url);
            } catch (Throwable t) {
                Log.e(TAG, "openBrowser failed: " + t.getMessage());
            }
        });
    }

    public void closeBrowser() {
        if (stashNative == null) return;
        runOnMain(() -> {
            try { stashNative.closeBrowser(); } catch (Throwable t) { Log.e(TAG, "closeBrowser failed: " + t.getMessage()); }
        });
    }

    // ---- session --------------------------------------------------------------------------

    public void dismiss() {
        if (stashNative == null) return;
        runOnMain(() -> {
            try { stashNative.dismiss(); } catch (Throwable t) { Log.e(TAG, "dismiss failed: " + t.getMessage()); }
        });
    }

    public void resetPresentationState() {
        if (stashNative == null) return;
        runOnMain(() -> {
            try { stashNative.resetPresentationState(); } catch (Throwable t) { Log.e(TAG, "resetPresentationState failed: " + t.getMessage()); }
        });
    }

    /** Stops forwarding to Unity and releases the SDK listener; called when the receiving GameObject is destroyed. */
    public void detach() {
        detached = true;
        if (stashNative == null) return;
        runOnMain(() -> {
            try { if (stashNative.getListener() == this) stashNative.setListener(null); } catch (Throwable t) { Log.e(TAG, "detach failed: " + t.getMessage()); }
        });
    }

    /** True while the SDK presents a card or scanner, or an open is still pending on the main thread. Custom Tabs are not tracked. */
    public boolean isCurrentlyPresented() {
        if (stashNative == null) return false;
        try { return openPending || stashNative.isCurrentlyPresented(); } catch (Throwable t) { return openPending; }
    }

    public boolean isPurchaseProcessing() {
        if (stashNative == null) return false;
        try { return stashNative.isPurchaseProcessing(); } catch (Throwable t) { return false; }
    }
}
