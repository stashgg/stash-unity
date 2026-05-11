package com.stash.popup;

import android.content.Intent;
import com.unity3d.player.UnityPlayerActivity;

/**
 * Optional drop-in replacement for UnityPlayerActivity.
 *
 * Extend this activity instead of UnityPlayerActivity to get reliable
 * OnBrowserClosed callbacks when Chrome Custom Tabs use startActivityForResult
 * (StashNativeCard.REQUEST_CODE_CUSTOM_TAB). ACTION_VIEW fallback already uses
 * lifecycle-based detection and works without this.
 *
 * To opt in, set android:name="com.stash.popup.StashNativeUnityActivity" on the
 * main <activity> in your AndroidManifest.xml (Assets/Plugins/Android/).
 */
public class StashNativeUnityActivity extends UnityPlayerActivity {
    @Override
    protected void onActivityResult(int requestCode, int resultCode, Intent data) {
        if (!StashNativeCardUnityBridge.getInstance().onActivityResult(requestCode, resultCode, data)) {
            super.onActivityResult(requestCode, resultCode, data);
        }
    }
}
