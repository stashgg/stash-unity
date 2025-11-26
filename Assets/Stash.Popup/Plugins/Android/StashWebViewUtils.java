package com.stash.popup;

import android.app.Activity;
import android.content.Context;
import android.content.res.Configuration;
import android.graphics.Color;
import android.net.Uri;
import android.os.Build;
import android.util.DisplayMetrics;
import android.util.Log;
import android.view.Gravity;
import android.view.View;
import android.view.ViewGroup;
import android.webkit.CookieManager;
import android.webkit.WebSettings;
import android.webkit.WebView;
import android.widget.FrameLayout;
import android.widget.ProgressBar;

public class StashWebViewUtils {
    private static final String TAG = "StashWebViewUtils";
    
    public static final String COLOR_BACKGROUND_DIM = "#20000000";
    public static final String COLOR_DARK_BG = "#1C1C1E";
    
    public static final String JS_SDK_SCRIPT = "(function() {" +
        "  window.stash_sdk = window.stash_sdk || {};" +
        "  window.stash_sdk.onPaymentSuccess = function(data) {" +
        "    try { StashAndroid.onPaymentSuccess(); } catch(e) {}" +
        "  };" +
        "  window.stash_sdk.onPaymentFailure = function(data) {" +
        "    try { StashAndroid.onPaymentFailure(); } catch(e) {}" +
        "  };" +
        "  window.stash_sdk.onPurchaseProcessing = function(data) {" +
        "    try { StashAndroid.onPurchaseProcessing(); } catch(e) {}" +
        "  };" +
        "  window.stash_sdk.setPaymentChannel = function(optinType) {" +
        "    try { StashAndroid.setPaymentChannel(optinType || ''); } catch(e) {}" +
        "  };" +
        "  window.stash_sdk.expand = function() {" +
        "    try { StashAndroid.expand(); } catch(e) {}" +
        "  };" +
        "  window.stash_sdk.collapse = function() {" +
        "    try { StashAndroid.collapse(); } catch(e) {}" +
        "  };" +
        "})();";

    public static boolean isDarkTheme(Context context) {
        if (context == null) return false;
        if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.Q) {
            int nightModeFlags = context.getResources().getConfiguration().uiMode & Configuration.UI_MODE_NIGHT_MASK;
            return nightModeFlags == Configuration.UI_MODE_NIGHT_YES;
        }
        return false;
    }

    public static int dpToPx(Context context, int dp) {
        if (context == null) return 0;
        return Math.round(dp * context.getResources().getDisplayMetrics().density);
    }

    public static boolean isTablet(Activity activity) {
        if (activity == null) return false;
        DisplayMetrics metrics = activity.getResources().getDisplayMetrics();
        int smallerDimension = Math.min(metrics.widthPixels, metrics.heightPixels);
        float smallerDp = smallerDimension / metrics.density;
        
        boolean isTabletBySize = smallerDp >= 600;
        
        boolean isTabletByConfig = false;
        if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.HONEYCOMB_MR2) {
            int screenSize = activity.getResources().getConfiguration().screenLayout & Configuration.SCREENLAYOUT_SIZE_MASK;
            isTabletByConfig = (screenSize == Configuration.SCREENLAYOUT_SIZE_LARGE || 
                               screenSize == Configuration.SCREENLAYOUT_SIZE_XLARGE);
        }
        
        float aspectRatio = (float)Math.max(metrics.widthPixels, metrics.heightPixels) / 
                           Math.min(metrics.widthPixels, metrics.heightPixels);
        boolean isTabletByAspect = aspectRatio < 2.0f && smallerDp >= 500;
        
        return isTabletBySize || isTabletByConfig || isTabletByAspect;
    }

    public static void configureWebViewSettings(WebView webView, boolean isDarkTheme) {
        if (webView == null) return;
        WebSettings settings = webView.getSettings();
        // Security: Disable file access to prevent local file attacks
        settings.setAllowFileAccess(false);
        settings.setAllowContentAccess(false);
        
        settings.setJavaScriptEnabled(true);
        settings.setDomStorageEnabled(true);
        settings.setLoadWithOverviewMode(true);
        settings.setUseWideViewPort(true);
        settings.setBuiltInZoomControls(false);
        settings.setDisplayZoomControls(false);
        settings.setSupportZoom(false);
        
        // Enable support for multiple windows (required for iframes like Adyen)
        settings.setSupportMultipleWindows(true);
        settings.setJavaScriptCanOpenWindowsAutomatically(true);
        
        // Enable database storage (may be needed for payment providers)
        settings.setDatabaseEnabled(true);
        
        // Allow mixed content mode for payment iframes (Adyen, etc.)
        // This allows HTTPS pages to load HTTP resources, which some payment providers may use
        if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.LOLLIPOP) {
            settings.setMixedContentMode(WebSettings.MIXED_CONTENT_COMPATIBILITY_MODE);
        }
        
        // Allow media playback without user gesture (needed for some payment flows)
        if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.JELLY_BEAN_MR1) {
            settings.setMediaPlaybackRequiresUserGesture(false);
        }
        
        // Set cache mode to default (allows caching for better performance)
        settings.setCacheMode(WebSettings.LOAD_DEFAULT);
        
        // Enable cookies for payment flows (PayPal, Adyen, etc.)
        if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.LOLLIPOP) {
            CookieManager.getInstance().setAcceptThirdPartyCookies(webView, true);
        }
        CookieManager.getInstance().setAcceptCookie(true);
        
        if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.Q) {
            settings.setForceDark(isDarkTheme ? WebSettings.FORCE_DARK_ON : WebSettings.FORCE_DARK_OFF);
        }
    }

    public static String appendThemeQueryParameter(String url, boolean isDarkTheme) {
        if (url == null || url.isEmpty()) {
            return url;
        }
        
        try {
            Uri uri = Uri.parse(url);
            Uri.Builder builder = uri.buildUpon();
            
            // Append or replace theme parameter
            String theme = isDarkTheme ? "dark" : "light";
            builder.appendQueryParameter("theme", theme);
            
            return builder.build().toString();
        } catch (Exception e) {
            Log.e(TAG, "Error appending theme parameter: " + e.getMessage());
            // If URL parsing fails, try simple string append
            String separator = url.contains("?") ? "&" : "?";
            String theme = isDarkTheme ? "dark" : "light";
            return url + separator + "theme=" + theme;
        }
    }

    public static int getThemeBackgroundColor(Context context) {
        if (context == null) return Color.WHITE;
        return isDarkTheme(context) ? Color.parseColor(COLOR_DARK_BG) : Color.WHITE;
    }

    public static ProgressBar createAndShowLoading(Context context, ViewGroup container) {
        if (context == null || container == null) return null;
        
        try {
            ProgressBar loadingIndicator = new ProgressBar(context);
            
            if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.HONEYCOMB) {
                loadingIndicator.setLayerType(View.LAYER_TYPE_HARDWARE, null);
            }
            
            loadingIndicator.setIndeterminate(true);
            
            if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.LOLLIPOP) {
                loadingIndicator.setIndeterminateTintList(
                    android.content.res.ColorStateList.valueOf(isDarkTheme(context) ? Color.WHITE : Color.DKGRAY));
            }
            
            FrameLayout.LayoutParams params = new FrameLayout.LayoutParams(dpToPx(context, 48), dpToPx(context, 48));
            params.gravity = Gravity.CENTER;
            loadingIndicator.setLayoutParams(params);
            
            container.addView(loadingIndicator);
            loadingIndicator.bringToFront();
            
            return loadingIndicator;
        } catch (Exception e) {
            Log.e(TAG, "Error showing loading: " + e.getMessage());
            return null;
        }
    }

    public static void hideLoading(final ProgressBar loadingIndicator) {
        if (loadingIndicator == null) return;
        
        if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.HONEYCOMB) {
            loadingIndicator.animate()
                .alpha(0.0f)
                .setDuration(200)
                .withEndAction(() -> {
                    if (loadingIndicator.getParent() != null) {
                        ((ViewGroup)loadingIndicator.getParent()).removeView(loadingIndicator);
                    }
                })
                .start();
        } else {
            if (loadingIndicator.getParent() != null) {
                ((ViewGroup)loadingIndicator.getParent()).removeView(loadingIndicator);
            }
        }
    }
}

