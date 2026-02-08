package com.stash.popup;

import android.app.Activity;
import android.util.Log;
import com.unity3d.player.UnityPlayer;

/** Forwards Unity C# to StashNativeCard and callbacks to Unity. */
public class StashNativeCardUnityBridge implements com.stash.stashnative.StashNativeCard.StashNativeCardListener {
    private static final String TAG = "StashNativeCardUnityBridge";
    private static final String UNITY_GAME_OBJECT = "StashNative";

    private static StashNativeCardUnityBridge instance;
    private com.stash.stashnative.StashNativeCard stashNativeCard;
    private boolean listenerSet;

    public static StashNativeCardUnityBridge getInstance() {
        if (instance == null) {
            instance = new StashNativeCardUnityBridge();
        }
        return instance;
    }

    private StashNativeCardUnityBridge() {
        try {
            stashNativeCard = com.stash.stashnative.StashNativeCard.getInstance();
        } catch (Throwable t) {
            Log.e(TAG, "Failed to get StashNativeCard instance: " + t.getMessage());
        }
    }

    private void ensureInit() {
        if (stashNativeCard == null) return;
        try {
            Activity activity = UnityPlayer.currentActivity;
            if (activity != null) {
                stashNativeCard.setActivity(activity);
            }
            if (!listenerSet) {
                stashNativeCard.setListener(this);
                listenerSet = true;
            }
        } catch (Throwable t) {
            Log.e(TAG, "ensureInit failed: " + t.getMessage());
        }
    }

    private static void sendMessage(String methodName, String message) {
        try {
            UnityPlayer.UnitySendMessage(UNITY_GAME_OBJECT, methodName, message != null ? message : "");
        } catch (Exception e) {
            Log.e(TAG, "UnitySendMessage failed: " + methodName + " " + e.getMessage());
        }
    }

    @Override
    public void onPaymentSuccess() {
        sendMessage("OnAndroidPaymentSuccess", "");
    }

    @Override
    public void onPaymentFailure() {
        sendMessage("OnAndroidPaymentFailure", "");
    }

    @Override
    public void onDialogDismissed() {
        sendMessage("OnAndroidDialogDismissed", "");
    }

    @Override
    public void onOptInResponse(String optinType) {
        sendMessage("OnAndroidOptinResponse", optinType != null ? optinType : "");
    }

    @Override
    public void onPageLoaded(long loadTimeMs) {
        sendMessage("OnAndroidPageLoaded", String.valueOf(loadTimeMs));
    }

    @Override
    public void onNetworkError() {
        sendMessage("OnAndroidNetworkError", "");
    }

    public void setActivity(Activity activity) {
        if (stashNativeCard != null && activity != null) {
            stashNativeCard.setActivity(activity);
        }
    }

    public void openCard(String url) {
        if (stashNativeCard == null || url == null) return;
        ensureInit();
        try {
            stashNativeCard.openCard(url, (com.stash.stashnative.StashNativeCard.CardConfig) null);
        } catch (Throwable t) {
            Log.e(TAG, "openCard failed: " + t.getMessage());
        }
    }

    public void openCardWithConfig(String url, boolean forcePortrait,
            float cardHeightRatioPortrait, float cardWidthRatioLandscape, float cardHeightRatioLandscape,
            float tabletWidthRatioPortrait, float tabletHeightRatioPortrait,
            float tabletWidthRatioLandscape, float tabletHeightRatioLandscape) {
        if (stashNativeCard == null || url == null) return;
        ensureInit();
        try {
            com.stash.stashnative.StashNativeCard.CardConfig config = new com.stash.stashnative.StashNativeCard.CardConfig();
            config.forcePortrait = forcePortrait;
            config.cardHeightRatioPortrait = cardHeightRatioPortrait;
            config.cardWidthRatioLandscape = cardWidthRatioLandscape;
            config.cardHeightRatioLandscape = cardHeightRatioLandscape;
            config.tabletWidthRatioPortrait = tabletWidthRatioPortrait;
            config.tabletHeightRatioPortrait = tabletHeightRatioPortrait;
            config.tabletWidthRatioLandscape = tabletWidthRatioLandscape;
            config.tabletHeightRatioLandscape = tabletHeightRatioLandscape;
            stashNativeCard.openCard(url, config);
        } catch (Throwable t) {
            Log.e(TAG, "openCardWithConfig failed: " + t.getMessage());
        }
    }

    public void openModal(String url) {
        if (stashNativeCard == null || url == null) return;
        ensureInit();
        try {
            stashNativeCard.openModal(url, (com.stash.stashnative.StashNativeCard.ModalConfig) null);
        } catch (Throwable t) {
            Log.e(TAG, "openModal failed: " + t.getMessage());
        }
    }

    public void openModalWithConfig(String url, boolean showDragBar, boolean allowDismiss,
            float phoneWidthRatioPortrait, float phoneHeightRatioPortrait,
            float phoneWidthRatioLandscape, float phoneHeightRatioLandscape,
            float tabletWidthRatioPortrait, float tabletHeightRatioPortrait,
            float tabletWidthRatioLandscape, float tabletHeightRatioLandscape) {
        if (stashNativeCard == null || url == null) return;
        ensureInit();
        try {
            com.stash.stashnative.StashNativeCard.ModalConfig config = new com.stash.stashnative.StashNativeCard.ModalConfig();
            config.showDragBar = showDragBar;
            config.allowDismiss = allowDismiss;
            config.phoneWidthRatioPortrait = phoneWidthRatioPortrait;
            config.phoneHeightRatioPortrait = phoneHeightRatioPortrait;
            config.phoneWidthRatioLandscape = phoneWidthRatioLandscape;
            config.phoneHeightRatioLandscape = phoneHeightRatioLandscape;
            config.tabletWidthRatioPortrait = tabletWidthRatioPortrait;
            config.tabletHeightRatioPortrait = tabletHeightRatioPortrait;
            config.tabletWidthRatioLandscape = tabletWidthRatioLandscape;
            config.tabletHeightRatioLandscape = tabletHeightRatioLandscape;
            stashNativeCard.openModal(url, config);
        } catch (Throwable t) {
            Log.e(TAG, "openModalWithConfig failed: " + t.getMessage());
        }
    }

    public void openBrowser(String url) {
        if (stashNativeCard == null || url == null) return;
        ensureInit();
        try {
            stashNativeCard.openBrowser(url);
        } catch (Throwable t) {
            Log.e(TAG, "openBrowser failed: " + t.getMessage());
        }
    }

    public void closeBrowser() {
        if (stashNativeCard != null) {
            try {
                stashNativeCard.closeBrowser();
            } catch (Throwable t) {
                Log.e(TAG, "closeBrowser failed: " + t.getMessage());
            }
        }
    }

    public void dismiss() {
        if (stashNativeCard != null) {
            try {
                stashNativeCard.dismiss();
            } catch (Throwable t) {
                Log.e(TAG, "dismiss failed: " + t.getMessage());
            }
        }
    }

    public void resetPresentationState() {
        if (stashNativeCard != null) {
            try {
                stashNativeCard.resetPresentationState();
            } catch (Throwable t) {
                Log.e(TAG, "resetPresentationState failed: " + t.getMessage());
            }
        }
    }

    public boolean isCurrentlyPresented() {
        if (stashNativeCard == null) return false;
        try {
            return stashNativeCard.isCurrentlyPresented();
        } catch (Throwable t) {
            return false;
        }
    }

    public boolean isPurchaseProcessing() {
        if (stashNativeCard == null) return false;
        try {
            return stashNativeCard.isPurchaseProcessing();
        } catch (Throwable t) {
            return false;
        }
    }
}
