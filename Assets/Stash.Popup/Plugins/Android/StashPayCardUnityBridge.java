package com.stash.popup;

import android.app.Activity;
import android.util.Log;
import com.unity3d.player.UnityPlayer;

/**
 * Unity bridge for the Stash Pay Android SDK (AAR).
 * Forwards Unity C# calls to StashPayCard and sends SDK callbacks back to Unity via UnitySendMessage.
 * Requires StashPay-1.2.4.aar (or compatible) in libs/.
 */
public class StashPayCardUnityBridge implements StashPayCard.StashPayListener {
    private static final String TAG = "StashPayCardUnityBridge";
    private static final String UNITY_GAME_OBJECT = "StashPayCard";

    private static StashPayCardUnityBridge instance;
    private StashPayCard stashPayCard;
    private boolean listenerSet;
    private boolean forceWebBasedCheckout;

    public static StashPayCardUnityBridge getInstance() {
        if (instance == null) {
            instance = new StashPayCardUnityBridge();
        }
        return instance;
    }

    private StashPayCardUnityBridge() {
        try {
            stashPayCard = StashPayCard.getInstance();
        } catch (Throwable t) {
            Log.e(TAG, "Failed to get StashPayCard instance: " + t.getMessage());
        }
    }

    /**
     * Ensures activity and listener are set. Call before openCheckout/openModal.
     */
    private void ensureInit() {
        if (stashPayCard == null) return;
        try {
            Activity activity = UnityPlayer.currentActivity;
            if (activity != null) {
                stashPayCard.setActivity(activity);
            }
            if (!listenerSet) {
                stashPayCard.setListener(this);
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

    // --- StashPayListener callbacks ---

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

    // --- API called from Unity ---

    public void setActivity(Activity activity) {
        if (stashPayCard != null && activity != null) {
            stashPayCard.setActivity(activity);
        }
    }

    public void openCheckout(String url) {
        if (stashPayCard == null) return;
        ensureInit();
        try {
            stashPayCard.openCheckout(url);
        } catch (Throwable t) {
            Log.e(TAG, "openCheckout failed: " + t.getMessage());
        }
    }

    public void openModal(String url) {
        if (stashPayCard == null) return;
        ensureInit();
        try {
            stashPayCard.openModal(url);
        } catch (Throwable t) {
            Log.e(TAG, "openModal failed: " + t.getMessage());
        }
    }

    public void openModalWithConfig(String url, boolean showDragBar, boolean allowDismiss,
            float phoneWidthRatioPortrait, float phoneHeightRatioPortrait,
            float phoneWidthRatioLandscape, float phoneHeightRatioLandscape,
            float tabletWidthRatioPortrait, float tabletHeightRatioPortrait,
            float tabletWidthRatioLandscape, float tabletHeightRatioLandscape) {
        if (stashPayCard == null) return;
        ensureInit();
        try {
            StashPayCard.ModalConfig config = new StashPayCard.ModalConfig();
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
            stashPayCard.openModal(url, config);
        } catch (Throwable t) {
            Log.e(TAG, "openModalWithConfig failed: " + t.getMessage());
        }
    }

    public void dismiss() {
        if (stashPayCard != null) {
            try {
                stashPayCard.dismiss();
            } catch (Throwable t) {
                Log.e(TAG, "dismiss failed: " + t.getMessage());
            }
        }
    }

    public void resetPresentationState() {
        if (stashPayCard != null) {
            try {
                stashPayCard.resetPresentationState();
            } catch (Throwable t) {
                Log.e(TAG, "resetPresentationState failed: " + t.getMessage());
            }
        }
    }

    public boolean isCurrentlyPresented() {
        if (stashPayCard == null) return false;
        try {
            return stashPayCard.isCurrentlyPresented();
        } catch (Throwable t) {
            return false;
        }
    }

    public boolean isPurchaseProcessing() {
        if (stashPayCard == null) return false;
        try {
            return stashPayCard.isPurchaseProcessing();
        } catch (Throwable t) {
            return false;
        }
    }

    // Checkout config
    public void setForcePortraitOnCheckout(boolean force) {
        if (stashPayCard != null) stashPayCard.setForcePortraitOnCheckout(force);
    }

    public void setCardHeightRatioPortrait(float value) {
        if (stashPayCard != null) stashPayCard.setCardHeightRatioPortrait(value);
    }

    public void setCardWidthRatioLandscape(float value) {
        if (stashPayCard != null) stashPayCard.setCardWidthRatioLandscape(value);
    }

    public void setCardHeightRatioLandscape(float value) {
        if (stashPayCard != null) stashPayCard.setCardHeightRatioLandscape(value);
    }

    public void setTabletWidthRatioPortrait(float value) {
        if (stashPayCard != null) stashPayCard.setTabletWidthRatioPortrait(value);
    }

    public void setTabletHeightRatioPortrait(float value) {
        if (stashPayCard != null) stashPayCard.setTabletHeightRatioPortrait(value);
    }

    public void setTabletWidthRatioLandscape(float value) {
        if (stashPayCard != null) stashPayCard.setTabletWidthRatioLandscape(value);
    }

    public void setTabletHeightRatioLandscape(float value) {
        if (stashPayCard != null) stashPayCard.setTabletHeightRatioLandscape(value);
    }

    public void setForceWebBasedCheckout(boolean force) {
        this.forceWebBasedCheckout = force;
        if (stashPayCard != null) stashPayCard.setForceWebBasedCheckout(force);
    }

    /** Returns the cached value set by Unity; the AAR does not expose a getter. */
    public boolean getForceWebBasedCheckout() {
        return forceWebBasedCheckout;
    }
}
