package com.stash.popup;

import android.app.Activity;
import android.app.Dialog;
import android.content.Intent;
import android.content.res.Configuration;
import android.graphics.Color;
import android.graphics.Outline;
import android.graphics.drawable.GradientDrawable;
import android.os.Build;
import android.os.Handler;
import android.os.Looper;
import android.util.DisplayMetrics;
import android.util.Log;
import android.view.Gravity;
import android.view.Surface;
import android.view.View;
import android.view.ViewGroup;
import android.view.ViewOutlineProvider;
import android.view.ViewTreeObserver;
import android.view.Window;
import android.view.WindowManager;
import android.webkit.CookieManager;
import android.webkit.JavascriptInterface;
import android.webkit.WebChromeClient;
import android.webkit.WebSettings;
import android.webkit.WebView;
import android.webkit.WebViewClient;
import android.widget.FrameLayout;
import android.widget.ProgressBar;
import android.net.Uri;
import com.unity3d.player.UnityPlayer;

public class StashPayCardPlugin {
    private static final String TAG = "StashPayCard";
    private static final String UNITY_GAME_OBJECT = "StashPayCard";
    private static StashPayCardPlugin instance;
    
    private Activity activity;
    private Dialog currentDialog;
    private WebView webView;
    private FrameLayout currentContainer;
    private ProgressBar loadingIndicator;
    private ViewTreeObserver.OnGlobalLayoutListener orientationChangeListener;
    
    private float cardHeightRatio = 0.6f;
    private boolean isCurrentlyPresented;
    private boolean paymentSuccessHandled;
    private boolean isPurchaseProcessing;
    private boolean usePopupPresentation;
    private boolean forceSafariViewController;
    private int lastOrientation = Configuration.ORIENTATION_UNDEFINED;
    
    private boolean useCustomSize;
    private float customPortraitWidthMultiplier = 0.85f;
    private float customPortraitHeightMultiplier = 1.125f;
    private float customLandscapeWidthMultiplier = 1.27075f;
    private float customLandscapeHeightMultiplier = 0.9f;
    
    private long pageLoadStartTime;
    
    private class StashJavaScriptInterface {
        @JavascriptInterface
        public void onPaymentSuccess() {
            if (paymentSuccessHandled) return;
            paymentSuccessHandled = true;
            isPurchaseProcessing = false;

            new Handler(Looper.getMainLooper()).post(() -> {
                try {
                    UnityPlayer.UnitySendMessage(UNITY_GAME_OBJECT, "OnAndroidPaymentSuccess", "");
                    dismissCurrentDialog();
                } catch (Exception e) {
                    Log.e(TAG, "Error handling payment success: " + e.getMessage());
                    cleanupAllViews();
                }
            });
        }
        
        @JavascriptInterface
        public void onPaymentFailure() {
            isPurchaseProcessing = false;
            new Handler(Looper.getMainLooper()).post(() -> {
                try {
                    UnityPlayer.UnitySendMessage(UNITY_GAME_OBJECT, "OnAndroidPaymentFailure", "");
                    dismissCurrentDialog();
                } catch (Exception e) {
                    Log.e(TAG, "Error handling payment failure: " + e.getMessage());
                    cleanupAllViews();
                }
            });
        }
        
        @JavascriptInterface
        public void onPurchaseProcessing() {
            isPurchaseProcessing = true;
        }
        
        @JavascriptInterface
        public void setPaymentChannel(String optinType) {
            new Handler(Looper.getMainLooper()).post(() -> {
                try {
                    UnityPlayer.UnitySendMessage(UNITY_GAME_OBJECT, "OnAndroidOptinResponse", 
                        optinType != null ? optinType : "");
                    dismissCurrentDialog();
                } catch (Exception e) {
                    Log.e(TAG, "Error handling payment channel: " + e.getMessage());
                }
            });
        }
        
        @JavascriptInterface
        public void expand() {
        }
        
        @JavascriptInterface
        public void collapse() {
        }
    }
    
    public static StashPayCardPlugin getInstance() {
        if (instance == null) {
            instance = new StashPayCardPlugin();
        }
        return instance;
    }
    
    private StashPayCardPlugin() {
        this.activity = UnityPlayer.currentActivity;
    }
    
    public void openCheckout(String url) {
        usePopupPresentation = false;
        openURLInternal(url);
    }
    
    public void openPopup(String url) {
        usePopupPresentation = true;
        useCustomSize = false;
        openURLInternal(url);
    }
    
    public void openPopupWithSize(String url, float portraitWidthMultiplier, float portraitHeightMultiplier, 
                                   float landscapeWidthMultiplier, float landscapeHeightMultiplier) {
        usePopupPresentation = true;
        customPortraitWidthMultiplier = portraitWidthMultiplier;
        customPortraitHeightMultiplier = portraitHeightMultiplier;
        customLandscapeWidthMultiplier = landscapeWidthMultiplier;
        customLandscapeHeightMultiplier = landscapeHeightMultiplier;
        useCustomSize = true;
        openURLInternal(url);
    }
    
    public void dismissDialog() {
        if (activity != null) {
            activity.runOnUiThread(() -> {
                try {
                    dismissCurrentDialog();
                } catch (Exception e) {
                    Log.e(TAG, "Error dismissing dialog: " + e.getMessage());
                    cleanupAllViews();
                }
            });
        }
    }
    
    public void resetPresentationState() {
        dismissDialog();
        paymentSuccessHandled = false;
        isCurrentlyPresented = false;
    }
    
    public boolean isCurrentlyPresented() {
        return isCurrentlyPresented;
    }
    
    public void setCardConfiguration(float heightRatio, float verticalPosition, float widthRatio) {
        this.cardHeightRatio = heightRatio;
    }
    
    public void setForceSafariViewController(boolean force) {
        this.forceSafariViewController = force;
    }
    
    public boolean getForceSafariViewController() {
        return forceSafariViewController;
    }
    
    private void openURLInternal(String url) {
        if (activity == null || url == null || url.isEmpty()) {
            Log.e(TAG, "Invalid activity or URL");
            return;
        }

        if (!url.startsWith("http://") && !url.startsWith("https://")) {
            url = "https://" + url;
        }

        final String finalUrl = url;

        activity.runOnUiThread(() -> {
            if (forceSafariViewController) {
                openWithChromeCustomTabs(finalUrl);
            } else if (usePopupPresentation) {
                createAndShowPopupDialog(finalUrl);
            } else {
                launchPortraitActivity(finalUrl);
            }
        });
    }
    
    private void launchPortraitActivity(String url) {
        try {
            android.view.Display display = activity.getWindowManager().getDefaultDisplay();
            int rotation = display.getRotation();
            boolean isLandscape = (rotation == Surface.ROTATION_90 || rotation == Surface.ROTATION_270);
            
            Intent intent = new Intent();
            intent.setClassName(activity, "com.stash.popup.StashPayCardPortraitActivity");
            intent.putExtra("url", url);
            intent.putExtra("initialURL", url);
            intent.putExtra("cardHeightRatio", cardHeightRatio);
            intent.putExtra("usePopup", usePopupPresentation);
            intent.putExtra("wasLandscape", isLandscape);
            intent.addFlags(Intent.FLAG_ACTIVITY_NO_ANIMATION | Intent.FLAG_ACTIVITY_REORDER_TO_FRONT);
            
            activity.startActivity(intent);
            activity.overridePendingTransition(0, 0);
            isCurrentlyPresented = true;
        } catch (Exception e) {
            Log.e(TAG, "Failed to launch Activity: " + e.getMessage());
        }
    }
    
    private void createAndShowPopupDialog(String url) {
        boolean preserveUseCustomSize = useCustomSize;
        cleanupAllViews();
        useCustomSize = preserveUseCustomSize;
        paymentSuccessHandled = false;

        try {
            currentDialog = new Dialog(activity, android.R.style.Theme_Translucent_NoTitleBar_Fullscreen);
            currentDialog.requestWindowFeature(Window.FEATURE_NO_TITLE);

            FrameLayout mainFrame = new FrameLayout(activity);
            mainFrame.setBackgroundColor(Color.parseColor("#20000000"));
            
            int[] dimensions = calculatePopupDimensions();
            currentContainer = new FrameLayout(activity);
            FrameLayout.LayoutParams containerParams = new FrameLayout.LayoutParams(dimensions[0], dimensions[1]);
            containerParams.gravity = Gravity.CENTER;
            currentContainer.setLayoutParams(containerParams);
            
            lastOrientation = activity.getResources().getConfiguration().orientation;
            
            orientationChangeListener = new ViewTreeObserver.OnGlobalLayoutListener() {
                @Override
                public void onGlobalLayout() {
                    if (currentContainer != null && currentDialog != null && currentDialog.isShowing()) {
                        int currentOrientation = activity.getResources().getConfiguration().orientation;
                        
                        if (currentOrientation != lastOrientation && currentOrientation != Configuration.ORIENTATION_UNDEFINED) {
                            lastOrientation = currentOrientation;
                            
                            int[] newDimensions = calculatePopupDimensions();
                            FrameLayout.LayoutParams params = (FrameLayout.LayoutParams) currentContainer.getLayoutParams();
                            
                            if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.HONEYCOMB) {
                                currentContainer.animate()
                                    .scaleX(0.95f)
                                    .scaleY(0.95f)
                                    .setDuration(100)
                                    .withEndAction(() -> {
                                        params.width = newDimensions[0];
                                        params.height = newDimensions[1];
                                        currentContainer.setLayoutParams(params);
                                        currentContainer.animate()
                                            .scaleX(1.0f)
                                            .scaleY(1.0f)
                                            .setDuration(200)
                                            .start();
                                    })
                                    .start();
                            } else {
                                params.width = newDimensions[0];
                                params.height = newDimensions[1];
                                currentContainer.setLayoutParams(params);
                            }
                        }
                    }
                }
            };
            mainFrame.getViewTreeObserver().addOnGlobalLayoutListener(orientationChangeListener);
            
            GradientDrawable popupBg = new GradientDrawable();
            popupBg.setColor(getThemeBackgroundColor());
            float radius = dpToPx(12);
            popupBg.setCornerRadius(radius);
            currentContainer.setBackground(popupBg);
            
            if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.LOLLIPOP) {
                currentContainer.setElevation(dpToPx(24));
                currentContainer.setOutlineProvider(new ViewOutlineProvider() {
                    @Override
                    public void getOutline(View view, Outline outline) {
                        outline.setRoundRect(0, 0, view.getWidth(), view.getHeight(), radius);
                    }
                });
                currentContainer.setClipToOutline(true);
            }
            
            webView = new WebView(activity);
            FrameLayout.LayoutParams webViewParams = new FrameLayout.LayoutParams(
                FrameLayout.LayoutParams.MATCH_PARENT, FrameLayout.LayoutParams.MATCH_PARENT);
            webView.setLayoutParams(webViewParams);
            currentContainer.addView(webView);
            
            setupPopupWebView(webView, url);
            
            mainFrame.addView(currentContainer);
            currentDialog.setContentView(mainFrame);

            Window window = currentDialog.getWindow();
            if (window != null) {
                window.setLayout(ViewGroup.LayoutParams.MATCH_PARENT, ViewGroup.LayoutParams.MATCH_PARENT);
                window.setFlags(WindowManager.LayoutParams.FLAG_HARDWARE_ACCELERATED,
                               WindowManager.LayoutParams.FLAG_HARDWARE_ACCELERATED);
                window.setBackgroundDrawableResource(android.R.color.transparent);
                window.addFlags(WindowManager.LayoutParams.FLAG_DIM_BEHIND);
                WindowManager.LayoutParams windowParams = window.getAttributes();
                windowParams.dimAmount = 0.3f;
                window.setAttributes(windowParams);
            }
            
            currentContainer.setOnClickListener(v -> {});

            currentDialog.setOnDismissListener(dialog -> {
                if (!paymentSuccessHandled) {
                    UnityPlayer.UnitySendMessage(UNITY_GAME_OBJECT, "OnAndroidDialogDismissed", "");
                }
                cleanupAllViews();
                isCurrentlyPresented = false;
            });
            
            currentDialog.show();
            animateFadeIn();
            isCurrentlyPresented = true;
        } catch (Exception e) {
            Log.e(TAG, "Error creating popup: " + e.getMessage());
        }
    }
    
    private void animateFadeIn() {
        if (currentContainer != null && Build.VERSION.SDK_INT >= Build.VERSION_CODES.HONEYCOMB) {
            currentContainer.setAlpha(0.0f);
            currentContainer.setScaleX(0.9f);
            currentContainer.setScaleY(0.9f);
            currentContainer.animate()
                .alpha(1.0f)
                .scaleX(1.0f)
                .scaleY(1.0f)
                .setDuration(200)
                .setInterpolator(new android.view.animation.AccelerateDecelerateInterpolator())
                .start();
        }
    }
    
    private void dismissPopupDialog() {
        if (currentDialog != null && currentContainer != null) {
            if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.HONEYCOMB) {
                currentContainer.animate()
                    .alpha(0.0f)
                    .scaleX(0.9f)
                    .scaleY(0.9f)
                    .setDuration(250)
                    .setInterpolator(new SpringInterpolator())
                    .withEndAction(() -> {
                        if (currentDialog != null) currentDialog.dismiss();
                    })
                    .start();
            } else {
                currentDialog.dismiss();
            }
        }
    }
    
    private void setupPopupWebView(WebView webView, String url) {
        WebSettings settings = webView.getSettings();
        settings.setJavaScriptEnabled(true);
        settings.setDomStorageEnabled(true);
        settings.setLoadWithOverviewMode(true);
        settings.setUseWideViewPort(true);
        settings.setBuiltInZoomControls(false);
        settings.setDisplayZoomControls(false);
        settings.setSupportZoom(false);
        
        // Enable cookies for payment flows (PayPal, etc.)
        if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.LOLLIPOP) {
            CookieManager.getInstance().setAcceptThirdPartyCookies(webView, true);
        }
        CookieManager.getInstance().setAcceptCookie(true);
        
        if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.Q) {
            settings.setForceDark(isDarkTheme() ? WebSettings.FORCE_DARK_ON : WebSettings.FORCE_DARK_OFF);
        }
        
        webView.setWebViewClient(new WebViewClient() {
            @Override
            public void onPageStarted(WebView view, String url, android.graphics.Bitmap favicon) {
                super.onPageStarted(view, url, favicon);
                pageLoadStartTime = System.currentTimeMillis();
                showLoadingIndicator();
                injectStashSDKFunctions();
            }
            
            @Override
            public void onPageFinished(WebView view, String url) {
                super.onPageFinished(view, url);
                
                if (pageLoadStartTime > 0) {
                    long loadTimeMs = System.currentTimeMillis() - pageLoadStartTime;
                    UnityPlayer.UnitySendMessage(UNITY_GAME_OBJECT, "OnAndroidPageLoaded", String.valueOf(loadTimeMs));
                    pageLoadStartTime = 0;
                }
                
                injectStashSDKFunctions();
                view.postDelayed(() -> {
                    hideLoadingIndicator();
                    view.setVisibility(View.VISIBLE);
                }, 300);
            }
        });
        
        webView.setWebChromeClient(new WebChromeClient());
        webView.addJavascriptInterface(new StashJavaScriptInterface(), "StashAndroid");
        webView.setVerticalScrollBarEnabled(false);
        webView.setHorizontalScrollBarEnabled(false);
        webView.setBackgroundColor(Color.TRANSPARENT);
        webView.loadUrl(url);
    }
    
    private void injectStashSDKFunctions() {
        if (webView == null) return;
        
        if (usePopupPresentation) {
            String disableScrollScript = "(function() {" +
                "  document.body.style.overflow = 'hidden';" +
                "  document.documentElement.style.overflow = 'hidden';" +
                "  document.body.style.position = 'fixed';" +
                "  document.body.style.width = '100%';" +
                "  document.body.style.height = '100%';" +
                "  if (document.body) {" +
                "    document.body.addEventListener('touchmove', function(e) { e.preventDefault(); }, { passive: false });" +
                "    document.body.addEventListener('wheel', function(e) { e.preventDefault(); }, { passive: false });" +
                "  }" +
                "})();";
            webView.evaluateJavascript(disableScrollScript, null);
        }
        
        String script = "(function() {" +
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
        
        webView.evaluateJavascript(script, null);
    }
    
    private void showLoadingIndicator() {
        if (currentContainer == null || activity == null) return;
        activity.runOnUiThread(() -> {
            try {
                if (loadingIndicator != null && loadingIndicator.getParent() != null) {
                    ((ViewGroup)loadingIndicator.getParent()).removeView(loadingIndicator);
                }
                
                loadingIndicator = new ProgressBar(activity.getApplicationContext());
                
                if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.HONEYCOMB) {
                    loadingIndicator.setLayerType(View.LAYER_TYPE_HARDWARE, null);
                }
                
                loadingIndicator.setIndeterminate(true);
                
                if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.LOLLIPOP) {
                    loadingIndicator.setIndeterminateTintList(
                        android.content.res.ColorStateList.valueOf(isDarkTheme() ? Color.WHITE : Color.DKGRAY));
                }
                
                FrameLayout.LayoutParams params = new FrameLayout.LayoutParams(dpToPx(48), dpToPx(48));
                params.gravity = Gravity.CENTER;
                loadingIndicator.setLayoutParams(params);
                
                currentContainer.addView(loadingIndicator);
                loadingIndicator.bringToFront();
            } catch (Exception e) {
                Log.e(TAG, "Error showing loading: " + e.getMessage());
            }
        });
    }
    
    private void hideLoadingIndicator() {
        if (loadingIndicator == null || activity == null) return;
        activity.runOnUiThread(() -> {
            try {
                if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.HONEYCOMB) {
                    loadingIndicator.animate()
                        .alpha(0.0f)
                        .setDuration(200)
                        .withEndAction(() -> {
                            if (loadingIndicator != null && loadingIndicator.getParent() != null) {
                                ((ViewGroup)loadingIndicator.getParent()).removeView(loadingIndicator);
                            }
                            loadingIndicator = null;
                        })
                        .start();
                } else {
                    if (loadingIndicator.getParent() != null) {
                        ((ViewGroup)loadingIndicator.getParent()).removeView(loadingIndicator);
                    }
                    loadingIndicator = null;
                }
            } catch (Exception e) {
                Log.e(TAG, "Error hiding loading: " + e.getMessage());
            }
        });
    }
    
    private void openWithChromeCustomTabs(String url) {
        try {
            if (isChromeCustomTabsAvailable()) {
                openWithReflectionChromeCustomTabs(url);
            } else {
                openWithDefaultBrowser(url);
            }
        } catch (Exception e) {
            Log.e(TAG, "Failed to open browser: " + e.getMessage());
        }
    }
    
    private boolean isChromeCustomTabsAvailable() {
        try {
            Class.forName("androidx.browser.customtabs.CustomTabsIntent");
            return true;
        } catch (ClassNotFoundException e) {
            return false;
        }
    }
    
    private void openWithReflectionChromeCustomTabs(String url) throws Exception {
        Class<?> customTabsIntentClass = Class.forName("androidx.browser.customtabs.CustomTabsIntent");
        Class<?> builderClass = Class.forName("androidx.browser.customtabs.CustomTabsIntent$Builder");

        Object builder = builderClass.newInstance();
        java.lang.reflect.Method setToolbarColor = builderClass.getMethod("setToolbarColor", int.class);
        setToolbarColor.invoke(builder, Color.parseColor("#000000"));

        java.lang.reflect.Method setShowTitle = builderClass.getMethod("setShowTitle", boolean.class);
        setShowTitle.invoke(builder, true);

        java.lang.reflect.Method build = builderClass.getMethod("build");
        Object customTabsIntent = build.invoke(builder);

        java.lang.reflect.Method launchUrl = customTabsIntentClass.getMethod("launchUrl", 
            android.content.Context.class, Uri.class);
        launchUrl.invoke(customTabsIntent, activity, Uri.parse(url));

        isCurrentlyPresented = true;
        new Handler(Looper.getMainLooper()).postDelayed(() -> {
            UnityPlayer.UnitySendMessage(UNITY_GAME_OBJECT, "OnAndroidDialogDismissed", "");
        }, 1000);
    }
    
    private void openWithDefaultBrowser(String url) {
        Intent browserIntent = new Intent(Intent.ACTION_VIEW, Uri.parse(url));
        browserIntent.addFlags(Intent.FLAG_ACTIVITY_NEW_TASK);
        activity.startActivity(browserIntent);
        isCurrentlyPresented = true;

        new Handler(Looper.getMainLooper()).postDelayed(() -> {
            UnityPlayer.UnitySendMessage(UNITY_GAME_OBJECT, "OnAndroidDialogDismissed", "");
        }, 1000);
    }
    
    private void dismissCurrentDialog() {
        if (currentDialog != null) {
            dismissPopupDialog();
        }
    }
    
    private void cleanupAllViews() {
        try {
            if (loadingIndicator != null) {
                try {
                    if (loadingIndicator.getParent() != null) {
                        ((ViewGroup)loadingIndicator.getParent()).removeView(loadingIndicator);
                    }
                } catch (Exception e) {
                    Log.e(TAG, "Error cleaning up loading indicator: " + e.getMessage());
                }
                loadingIndicator = null;
            }
            
            if (currentDialog != null) {
                if (currentDialog.isShowing()) {
                    currentDialog.dismiss();
                }
                currentDialog = null;
            }
            
            if (webView != null) {
                try {
                    if (webView.getParent() != null) {
                        ((ViewGroup)webView.getParent()).removeView(webView);
                    }
                    webView.stopLoading();
                    webView.destroy();
                } catch (Exception e) {
                    Log.e(TAG, "Error cleaning up WebView: " + e.getMessage());
                }
                webView = null;
            }
            
            if (currentContainer != null) {
                try {
                    if (orientationChangeListener != null && currentContainer.getParent() != null) {
                        View parent = (View) currentContainer.getParent();
                        if (parent.getViewTreeObserver().isAlive()) {
                            parent.getViewTreeObserver().removeOnGlobalLayoutListener(orientationChangeListener);
                        }
                    }
                    if (currentContainer.getParent() != null) {
                        ((ViewGroup)currentContainer.getParent()).removeView(currentContainer);
                    }
                    currentContainer.removeAllViews();
                } catch (Exception e) {
                    Log.e(TAG, "Error cleaning up container: " + e.getMessage());
                }
                currentContainer = null;
            }
            
            orientationChangeListener = null;
        } catch (Exception e) {
            Log.e(TAG, "Error during cleanup: " + e.getMessage());
        }
        
        isPurchaseProcessing = false;
        usePopupPresentation = false;
    }
    
    private boolean isTablet() {
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
    
    private int[] calculatePopupDimensions() {
        DisplayMetrics metrics = activity.getResources().getDisplayMetrics();
        boolean isLandscape = activity.getResources().getConfiguration().orientation == Configuration.ORIENTATION_LANDSCAPE;
        
        int smallerDimension = Math.min(metrics.widthPixels, metrics.heightPixels);
        boolean isTablet = isTablet();
        int baseSize = Math.max(
            isTablet ? dpToPx(400) : dpToPx(300),
            Math.min(isTablet ? dpToPx(500) : dpToPx(500), (int)(smallerDimension * (isTablet ? 0.5f : 0.75f)))
        );
        
        float widthMultiplier = isLandscape ? 
            (useCustomSize ? customLandscapeWidthMultiplier : 1.27075f) :
            (useCustomSize ? customPortraitWidthMultiplier : 0.85f);
        float heightMultiplier = isLandscape ? 
            (useCustomSize ? customLandscapeHeightMultiplier : 0.9f) :
            (useCustomSize ? customPortraitHeightMultiplier : 1.125f);

        int popupWidth = (int)(baseSize * widthMultiplier);
        int popupHeight = (int)(baseSize * heightMultiplier);

        return new int[]{popupWidth, popupHeight};
    }
    
    private int dpToPx(int dp) {
        return Math.round(dp * activity.getResources().getDisplayMetrics().density);
    }

    private boolean isDarkTheme() {
        if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.Q) {
            int nightModeFlags = activity.getResources().getConfiguration().uiMode & Configuration.UI_MODE_NIGHT_MASK;
            return nightModeFlags == Configuration.UI_MODE_NIGHT_YES;
        }
        return false;
    }

    private int getThemeBackgroundColor() {
        return isDarkTheme() ? Color.parseColor("#1C1C1E") : Color.WHITE;
    }
}
