package com.stash.popup;

import android.util.Log;
import com.unity3d.player.UnityPlayer;

public class StashUnityBridge {
    private static final String TAG = "StashUnityBridge";
    private static final String UNITY_GAME_OBJECT = "StashPayCard";

    // Unity Message Methods
    public static final String MSG_ON_PAYMENT_SUCCESS = "OnAndroidPaymentSuccess";
    public static final String MSG_ON_PAYMENT_FAILURE = "OnAndroidPaymentFailure";
    public static final String MSG_ON_PURCHASE_PROCESSING = "OnAndroidPurchaseProcessing"; // Note: Not always sent to Unity directly in current code, but handled locally
    public static final String MSG_ON_OPTIN_RESPONSE = "OnAndroidOptinResponse";
    public static final String MSG_ON_DIALOG_DISMISSED = "OnAndroidDialogDismissed";
    public static final String MSG_ON_PAGE_LOADED = "OnAndroidPageLoaded";

    public static void sendMessage(String methodName, String message) {
        try {
            UnityPlayer.UnitySendMessage(UNITY_GAME_OBJECT, methodName, message != null ? message : "");
        } catch (Exception e) {
            Log.e(TAG, "Failed to send Unity message: " + methodName + " Error: " + e.getMessage());
        }
    }

    public static void sendPaymentSuccess() {
        sendMessage(MSG_ON_PAYMENT_SUCCESS, "");
    }

    public static void sendPaymentFailure() {
        sendMessage(MSG_ON_PAYMENT_FAILURE, "");
    }

    public static void sendOptInResponse(String optinType) {
        sendMessage(MSG_ON_OPTIN_RESPONSE, optinType);
    }

    public static void sendDialogDismissed() {
        sendMessage(MSG_ON_DIALOG_DISMISSED, "");
    }

    public static void sendPageLoaded(long loadTimeMs) {
        sendMessage(MSG_ON_PAGE_LOADED, String.valueOf(loadTimeMs));
    }
}

