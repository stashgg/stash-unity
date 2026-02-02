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
import com.stash.popup.keepalive.StashKeepAliveManager;

public class StashPayCardPlugin {
    private static final String TAG = "StashPayCard";
    private static final String UNITY_GAME_OBJECT = "StashPayCard";
    private static StashPayCardPlugin instance;
    
    // JS SDK Script moved to StashWebViewUtils


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
    private float customPortraitWidthMultiplier = 1.0285f;
    private float customPortraitHeightMultiplier = 1.485f;
    private float customLandscapeWidthMultiplier = 1.2275445f;
    private float customLandscapeHeightMultiplier = 1.1385f;
    
    private long pageLoadStartTime;
    
    private class StashJavaScriptInterface {
        @JavascriptInterface
        public void onPaymentSuccess() {
            if (paymentSuccessHandled) return;
            paymentSuccessHandled = true;
            isPurchaseProcessing = false;

            new Handler(Looper.getMainLooper()).post(() -> {
                try {
                    StashUnityBridge.sendPaymentSuccess();
                    dismissCurrentDialog();
                } catch (Exception e) {
                    Log.e(TAG, "Error handling payment success: " + e.getMessage());
                    cleanupAllViews();
                }
            });
        }
        
        @JavascriptInterface
        public void onPaymentFailure() {
            if (paymentSuccessHandled) return;
            paymentSuccessHandled = true; // Treat failure as a handled final state to prevent dismiss callback
            isPurchaseProcessing = false;
            new Handler(Looper.getMainLooper()).post(() -> {
                try {
                    StashUnityBridge.sendPaymentFailure();
                    dismissCurrentDialog();
                } catch (Exception e) {
                    Log.e(TAG, "Error handling payment failure: " + e.getMessage());
                    cleanupAllViews();
                }
            });
        }
        
        @JavascriptInterface
        public void onPurchaseProcessing() {
            try {
                isPurchaseProcessing = true;
                // Update dialog dismissibility on UI thread
                new Handler(Looper.getMainLooper()).post(() -> {
                    try {
                        if (currentDialog != null && currentDialog.isShowing()) {
                            currentDialog.setCanceledOnTouchOutside(false);
                            currentDialog.setCancelable(false);
                        }
                    } catch (Exception e) {
                        Log.e(TAG, "Error updating dialog dismissibility: " + e.getMessage(), e);
                    }
                });
            } catch (Exception e) {
                Log.e(TAG, "Error in onPurchaseProcessing: " + e.getMessage(), e);
            }
        }
        
        @JavascriptInterface
        public void setPaymentChannel(String optinType) {
            new Handler(Looper.getMainLooper()).post(() -> {
                try {
                    StashUnityBridge.sendOptInResponse(optinType != null ? optinType : "");
                    dismissCurrentDialog();
                } catch (Exception e) {
                    Log.e(TAG, "Error handling payment channel: " + e.getMessage());
                }
            });
        }
        
        @JavascriptInterface
        public void expand() {
            try {
                // Expand functionality can be implemented here if needed
            } catch (Exception e) {
                Log.e(TAG, "Error in expand: " + e.getMessage(), e);
            }
        }
        
        @JavascriptInterface
        public void collapse() {
            try {
                // Collapse functionality can be implemented here if needed
            } catch (Exception e) {
                Log.e(TAG, "Error in collapse: " + e.getMessage(), e);
            }
        }
    }
    
    public static StashPayCardPlugin getInstance() {
        if (instance == null) {
            instance = new StashPayCardPlugin();
        }
        return instance;
    }
    
    private StashPayCardPlugin() {
    }
    
    public void openCheckout(String url) {
        try {
            usePopupPresentation = false;
            openURLInternal(url);
        } catch (Exception e) {
            Log.e(TAG, "Error in openCheckout: " + e.getMessage(), e);
            cleanupAllViews();
        }
    }
    
    public void openPopup(String url) {
        try {
            // Popup always uses in-app dialog, ignoring forceSafariViewController flag
            usePopupPresentation = true;
            useCustomSize = false;
            openURLInternal(url);
        } catch (Exception e) {
            Log.e(TAG, "Error in openPopup: " + e.getMessage(), e);
            cleanupAllViews();
        }
    }
    
    public void openPopupWithSize(String url, float portraitWidthMultiplier, float portraitHeightMultiplier, 
                                   float landscapeWidthMultiplier, float landscapeHeightMultiplier) {
        try {
            // Popup always uses in-app dialog, ignoring forceSafariViewController flag
            usePopupPresentation = true;
            customPortraitWidthMultiplier = portraitWidthMultiplier;
            customPortraitHeightMultiplier = portraitHeightMultiplier;
            customLandscapeWidthMultiplier = landscapeWidthMultiplier;
            customLandscapeHeightMultiplier = landscapeHeightMultiplier;
            useCustomSize = true;
            openURLInternal(url);
        } catch (Exception e) {
            Log.e(TAG, "Error in openPopupWithSize: " + e.getMessage(), e);
            cleanupAllViews();
        }
    }
    
    public void dismissDialog() {
        Activity activity = UnityPlayer.currentActivity;
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
        try {
            dismissDialog();
            paymentSuccessHandled = false;
            isCurrentlyPresented = false;
        } catch (Exception e) {
            Log.e(TAG, "Error in resetPresentationState: " + e.getMessage(), e);
            cleanupAllViews();
        }
    }
    
    public boolean isCurrentlyPresented() {
        try {
            return isCurrentlyPresented;
        } catch (Exception e) {
            Log.e(TAG, "Error in isCurrentlyPresented: " + e.getMessage(), e);
            return false;
        }
    }
    
    public void setCardConfiguration(float heightRatio, float verticalPosition, float widthRatio) {
        try {
            this.cardHeightRatio = heightRatio;
        } catch (Exception e) {
            Log.e(TAG, "Error in setCardConfiguration: " + e.getMessage(), e);
        }
    }
    
    public void setForceSafariViewController(boolean force) {
        try {
            this.forceSafariViewController = force;
        } catch (Exception e) {
            Log.e(TAG, "Error in setForceSafariViewController: " + e.getMessage(), e);
        }
    }
    
    public boolean getForceSafariViewController() {
        try {
            return forceSafariViewController;
        } catch (Exception e) {
            Log.e(TAG, "Error in getForceSafariViewController: " + e.getMessage(), e);
            return false;
        }
    }
    
    public boolean isPurchaseProcessing() {
        try {
            return isPurchaseProcessing;
        } catch (Exception e) {
            Log.e(TAG, "Error in isPurchaseProcessing: " + e.getMessage(), e);
            return false;
        }
    }
    
    /**
     * Stops the keep-alive foreground service.
     * 
     * Called by Unity when the app regains focus (OnApplicationPause(false)) to stop
     * the keep-alive service that was started before launching Chrome Custom Tabs.
     * 
     * Safe to call even if the service is not running - acts as a no-op.
     */
    public void stopKeepAlive() {
        try {
            Activity activity = UnityPlayer.currentActivity;
            if (activity != null) {
                StashKeepAliveManager.stop(activity);
            }
        } catch (Exception e) {
            // Swallow exceptions - service may already be stopped
            Log.w(TAG, "Error stopping keep-alive service: " + e.getMessage());
        }
    }
    
    private void openURLInternal(String url) {
        try {
            Activity activity = UnityPlayer.currentActivity;
            if (activity == null || url == null || url.isEmpty()) {
                Log.e(TAG, "Invalid activity or URL");
                return;
            }

            if (!url.startsWith("http://") && !url.startsWith("https://")) {
                url = "https://" + url;
            }

            // Append theme query parameter
            try {
                url = StashWebViewUtils.appendThemeQueryParameter(url, StashWebViewUtils.isDarkTheme(activity));
            } catch (Exception e) {
                Log.e(TAG, "Error appending theme parameter: " + e.getMessage(), e);
                // Continue without theme parameter
            }

            final String finalUrl = url;

            activity.runOnUiThread(() -> {
                try {
                    // Popup presentation always uses in-app dialog (ignores forceSafariViewController)
                    if (usePopupPresentation) {
                        createAndShowPopupDialog(finalUrl, activity);
                    } else if (forceSafariViewController) {
                        // Force web view mode for checkout
                        // Request notification permission (Android 13+) so keep-alive notification can show
                        StashKeepAliveManager.requestNotificationPermissionIfNeeded(activity);
                        // Start keep-alive service to prevent Unity from being killed while in CCT
                        StashKeepAliveManager.start(activity, "checkout", null, null, 30_000L);
                        openWithChromeCustomTabs(finalUrl, activity);
                    } else {
                        // Default: in-app card presentation
                        launchPortraitActivity(finalUrl, activity);
                    }
                } catch (Exception e) {
                    Log.e(TAG, "Error in UI thread operation: " + e.getMessage(), e);
                    cleanupAllViews();
                }
            });
        } catch (Exception e) {
            Log.e(TAG, "Error in openURLInternal: " + e.getMessage(), e);
            cleanupAllViews();
        }
    }
    
    private void launchPortraitActivity(String url, Activity activity) {
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
    
    private class PopupOrientationListener implements ViewTreeObserver.OnGlobalLayoutListener {
        private final Activity activity;

        PopupOrientationListener(Activity activity) {
            this.activity = activity;
        }

        @Override
        public void onGlobalLayout() {
            try {
                if (currentContainer != null && currentDialog != null && currentDialog.isShowing() && activity != null) {
                    int currentOrientation = activity.getResources().getConfiguration().orientation;
                    
                    if (currentOrientation != lastOrientation && currentOrientation != Configuration.ORIENTATION_UNDEFINED) {
                        lastOrientation = currentOrientation;
                        
                        try {
                            int[] newDimensions = calculatePopupDimensions(activity);
                            FrameLayout.LayoutParams params = (FrameLayout.LayoutParams) currentContainer.getLayoutParams();
                            
                            if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.HONEYCOMB) {
                                currentContainer.animate()
                                    .scaleX(0.95f)
                                    .scaleY(0.95f)
                                    .setDuration(100)
                                    .withEndAction(() -> {
                                        try {
                                            params.width = newDimensions[0];
                                            params.height = newDimensions[1];
                                            currentContainer.setLayoutParams(params);
                                            currentContainer.animate()
                                                .scaleX(1.0f)
                                                .scaleY(1.0f)
                                                .setDuration(200)
                                                .start();
                                        } catch (Exception e) {
                                            Log.e(TAG, "Error in animation end action: " + e.getMessage(), e);
                                        }
                                    })
                                    .start();
                            } else {
                                params.width = newDimensions[0];
                                params.height = newDimensions[1];
                                currentContainer.setLayoutParams(params);
                            }
                        } catch (Exception e) {
                            Log.e(TAG, "Error calculating or applying dimensions: " + e.getMessage(), e);
                        }
                    }
                }
            } catch (Exception e) {
                Log.e(TAG, "Error in onGlobalLayout: " + e.getMessage(), e);
            }
        }
    }

    private void createAndShowPopupDialog(String url, final Activity activity) {
        if (activity == null || url == null || url.isEmpty()) {
            Log.e(TAG, "Invalid activity or URL in createAndShowPopupDialog");
            return;
        }

        boolean preserveUseCustomSize = useCustomSize;
        cleanupAllViews();
        useCustomSize = preserveUseCustomSize;
        paymentSuccessHandled = false;

        try {
            currentDialog = new Dialog(activity, android.R.style.Theme_Translucent_NoTitleBar_Fullscreen);
            currentDialog.requestWindowFeature(Window.FEATURE_NO_TITLE);

            FrameLayout mainFrame = new FrameLayout(activity);
            try {
                mainFrame.setBackgroundColor(Color.parseColor(StashWebViewUtils.COLOR_BACKGROUND_DIM));
            } catch (Exception e) {
                Log.e(TAG, "Error setting background color: " + e.getMessage(), e);
                mainFrame.setBackgroundColor(Color.parseColor("#80000000")); // Fallback
            }
            
            // Handle tap-outside dismissal (only if not processing)
            mainFrame.setOnClickListener(v -> {
                try {
                    // Only dismiss if not processing and click is on the background (not the container)
                    if (!isPurchaseProcessing && currentDialog != null && currentDialog.isShowing() && v == mainFrame) {
                        currentDialog.dismiss();
                    }
                } catch (Exception e) {
                    Log.e(TAG, "Error in click handler: " + e.getMessage(), e);
                }
            });
            
            int[] dimensions;
            try {
                dimensions = calculatePopupDimensions(activity);
            } catch (Exception e) {
                Log.e(TAG, "Error calculating dimensions: " + e.getMessage(), e);
                // Fallback dimensions
                DisplayMetrics metrics = activity.getResources().getDisplayMetrics();
                dimensions = new int[]{
                    (int)(metrics.widthPixels * 0.9f),
                    (int)(metrics.heightPixels * 0.7f)
                };
            }
            
            currentContainer = new FrameLayout(activity);
            FrameLayout.LayoutParams containerParams = new FrameLayout.LayoutParams(dimensions[0], dimensions[1]);
            containerParams.gravity = Gravity.CENTER;
            currentContainer.setLayoutParams(containerParams);
            
            try {
                lastOrientation = activity.getResources().getConfiguration().orientation;
            } catch (Exception e) {
                Log.e(TAG, "Error getting orientation: " + e.getMessage(), e);
                lastOrientation = Configuration.ORIENTATION_PORTRAIT;
            }
            
            orientationChangeListener = new PopupOrientationListener(activity);
            try {
                mainFrame.getViewTreeObserver().addOnGlobalLayoutListener(orientationChangeListener);
            } catch (Exception e) {
                Log.e(TAG, "Error adding layout listener: " + e.getMessage(), e);
            }
            
            try {
                GradientDrawable popupBg = new GradientDrawable();
                popupBg.setColor(StashWebViewUtils.getThemeBackgroundColor(activity));
                float radius = StashWebViewUtils.dpToPx(activity, 12);
                popupBg.setCornerRadius(radius);
                currentContainer.setBackground(popupBg);
                
                if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.LOLLIPOP) {
                    currentContainer.setElevation(StashWebViewUtils.dpToPx(activity, 24));
                    currentContainer.setOutlineProvider(new ViewOutlineProvider() {
                        @Override
                        public void getOutline(View view, Outline outline) {
                            try {
                                outline.setRoundRect(0, 0, view.getWidth(), view.getHeight(), radius);
                            } catch (Exception e) {
                                Log.e(TAG, "Error setting outline: " + e.getMessage(), e);
                            }
                        }
                    });
                    currentContainer.setClipToOutline(true);
                }
            } catch (Exception e) {
                Log.e(TAG, "Error setting container background: " + e.getMessage(), e);
                // Continue without custom background
            }
            
            try {
                webView = new WebView(activity);
                FrameLayout.LayoutParams webViewParams = new FrameLayout.LayoutParams(
                    FrameLayout.LayoutParams.MATCH_PARENT, FrameLayout.LayoutParams.MATCH_PARENT);
                webView.setLayoutParams(webViewParams);
                currentContainer.addView(webView);
                
                setupPopupWebView(webView, url, activity);
            } catch (Exception e) {
                Log.e(TAG, "Error creating WebView: " + e.getMessage(), e);
                cleanupAllViews();
                return;
            }
            
            mainFrame.addView(currentContainer);
            currentDialog.setContentView(mainFrame);

            Window window = currentDialog.getWindow();
            if (window != null) {
                try {
                    window.setLayout(ViewGroup.LayoutParams.MATCH_PARENT, ViewGroup.LayoutParams.MATCH_PARENT);
                    window.setFlags(WindowManager.LayoutParams.FLAG_HARDWARE_ACCELERATED,
                                   WindowManager.LayoutParams.FLAG_HARDWARE_ACCELERATED);
                    window.setBackgroundDrawableResource(android.R.color.transparent);
                    window.addFlags(WindowManager.LayoutParams.FLAG_DIM_BEHIND);
                    WindowManager.LayoutParams windowParams = window.getAttributes();
                    windowParams.dimAmount = 0.3f;
                    window.setAttributes(windowParams);
                } catch (Exception e) {
                    Log.e(TAG, "Error configuring window: " + e.getMessage(), e);
                }
            }
            
            currentContainer.setOnClickListener(v -> {});
            
            // Prevent dismissal when purchase is processing
            currentDialog.setCanceledOnTouchOutside(!isPurchaseProcessing);
            currentDialog.setCancelable(!isPurchaseProcessing);

            currentDialog.setOnDismissListener(dialog -> {
                try {
                    if (!paymentSuccessHandled) {
                        StashUnityBridge.sendDialogDismissed();
                    }
                } catch (Exception e) {
                    Log.e(TAG, "Error in dismiss listener: " + e.getMessage(), e);
                }
                cleanupAllViews();
                isCurrentlyPresented = false;
            });
            
            try {
                currentDialog.show();
                animateFadeIn();
                isCurrentlyPresented = true;
            } catch (Exception e) {
                Log.e(TAG, "Error showing dialog: " + e.getMessage(), e);
                cleanupAllViews();
            }
        } catch (Exception e) {
            Log.e(TAG, "Error creating popup: " + e.getMessage(), e);
            cleanupAllViews();
        }
    }
    
    private void animateFadeIn() {
        try {
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
        } catch (Exception e) {
            Log.e(TAG, "Error in animateFadeIn: " + e.getMessage(), e);
        }
    }
    
    private void dismissPopupDialog() {
        try {
            if (currentDialog != null && currentContainer != null) {
                if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.HONEYCOMB) {
                    currentContainer.animate()
                        .alpha(0.0f)
                        .scaleX(0.9f)
                        .scaleY(0.9f)
                        .setDuration(250)
                        .setInterpolator(new SpringInterpolator())
                        .withEndAction(() -> {
                            try {
                                if (currentDialog != null) currentDialog.dismiss();
                            } catch (Exception e) {
                                Log.e(TAG, "Error dismissing dialog in animation: " + e.getMessage(), e);
                            }
                        })
                        .start();
                } else {
                    currentDialog.dismiss();
                }
            } else if (currentDialog != null) {
                currentDialog.dismiss();
            }
        } catch (Exception e) {
            Log.e(TAG, "Error in dismissPopupDialog: " + e.getMessage(), e);
            try {
                if (currentDialog != null) {
                    currentDialog.dismiss();
                }
            } catch (Exception e2) {
                Log.e(TAG, "Error force dismissing dialog: " + e2.getMessage(), e2);
            }
        }
    }
    
    private void setupPopupWebView(WebView webView, String url, final Activity activity) {
        if (webView == null || activity == null || url == null || url.isEmpty()) {
            Log.e(TAG, "Invalid parameters in setupPopupWebView");
            return;
        }

        try {
            StashWebViewUtils.configureWebViewSettings(webView, StashWebViewUtils.isDarkTheme(activity));
        } catch (Exception e) {
            Log.e(TAG, "Error configuring WebView settings: " + e.getMessage(), e);
        }
        
        webView.setWebViewClient(new WebViewClient() {
            @Override
            public void onPageStarted(WebView view, String url, android.graphics.Bitmap favicon) {
                try {
                    super.onPageStarted(view, url, favicon);
                    pageLoadStartTime = System.currentTimeMillis();
                    showLoadingIndicator(activity);
                    injectStashSDKFunctions();
                } catch (Exception e) {
                    Log.e(TAG, "Error in onPageStarted: " + e.getMessage(), e);
                }
            }
            
            @Override
            public void onPageFinished(WebView view, String url) {
                try {
                    super.onPageFinished(view, url);
                    
                    if (pageLoadStartTime > 0) {
                        long loadTimeMs = System.currentTimeMillis() - pageLoadStartTime;
                        try {
                            StashUnityBridge.sendPageLoaded(loadTimeMs);
                        } catch (Exception e) {
                            Log.e(TAG, "Error sending page loaded message: " + e.getMessage(), e);
                        }
                        pageLoadStartTime = 0;
                    }
                    
                    injectStashSDKFunctions();
                    view.postDelayed(() -> {
                        try {
                            hideLoadingIndicator(activity);
                            view.setVisibility(View.VISIBLE);
                        } catch (Exception e) {
                            Log.e(TAG, "Error in delayed page finished handler: " + e.getMessage(), e);
                        }
                    }, 300);
                } catch (Exception e) {
                    Log.e(TAG, "Error in onPageFinished: " + e.getMessage(), e);
                }
            }
            
            @Override
            public void onReceivedError(WebView view, android.webkit.WebResourceRequest request, 
                                        android.webkit.WebResourceError error) {
                try {
                    super.onReceivedError(view, request, error);
                    // Log only if strictly necessary for debugging production issues
                    if (error != null) {
                        Log.e(TAG, "WebView error: " + error.getDescription());
                    }
                } catch (Exception e) {
                    Log.e(TAG, "Error in onReceivedError: " + e.getMessage(), e);
                }
            }
        });
        
        try {
            webView.setWebChromeClient(new WebChromeClient());
            webView.addJavascriptInterface(new StashJavaScriptInterface(), "StashAndroid");
            webView.setVerticalScrollBarEnabled(false);
            webView.setHorizontalScrollBarEnabled(false);
            webView.setBackgroundColor(Color.TRANSPARENT);
            webView.loadUrl(url);
        } catch (Exception e) {
            Log.e(TAG, "Error setting up WebView: " + e.getMessage(), e);
            cleanupAllViews();
        }
    }
    
    private void injectStashSDKFunctions() {
        if (webView == null) return;
        
        try {
      //  if (usePopupPresentation) {
      //      String disableScrollScript = "(function() {" +
      //          "  document.body.style.overflow = 'hidden';" +
      //          "  document.documentElement.style.overflow = 'hidden';" +
      //          "  document.body.style.position = 'fixed';" +
      //          "  document.body.style.width = '100%';" +
      //          "  document.body.style.height = '100%';" +
      //          "  if (document.body) {" +
      //          "    document.body.addEventListener('touchmove', function(e) { e.preventDefault(); }, { passive: false });" +
      //          "    document.body.addEventListener('wheel', function(e) { e.preventDefault(); }, { passive: false });" +
      //          "  }" +
      //          "})();";
      //      webView.evaluateJavascript(disableScrollScript, null);
      //  }
        
            webView.evaluateJavascript(StashWebViewUtils.JS_SDK_SCRIPT, null);
        } catch (Exception e) {
            Log.e(TAG, "Error injecting SDK functions: " + e.getMessage(), e);
        }
    }
    
    private void showLoadingIndicator(Activity activity) {
        if (currentContainer == null || activity == null) return;
        try {
            activity.runOnUiThread(() -> {
                try {
                    // Remove existing if any (cleanup)
                    if (loadingIndicator != null && loadingIndicator.getParent() != null) {
                        ((ViewGroup)loadingIndicator.getParent()).removeView(loadingIndicator);
                    }
                    loadingIndicator = StashWebViewUtils.createAndShowLoading(activity, currentContainer);
                } catch (Exception e) {
                    Log.e(TAG, "Error showing loading indicator: " + e.getMessage(), e);
                }
            });
        } catch (Exception e) {
            Log.e(TAG, "Error scheduling loading indicator: " + e.getMessage(), e);
        }
    }
    
    private void hideLoadingIndicator(Activity activity) {
        if (loadingIndicator == null || activity == null) return;
        try {
            activity.runOnUiThread(() -> {
                try {
                    StashWebViewUtils.hideLoading(loadingIndicator);
                    loadingIndicator = null;
                } catch (Exception e) {
                    Log.e(TAG, "Error hiding loading indicator: " + e.getMessage(), e);
                    loadingIndicator = null;
                }
            });
        } catch (Exception e) {
            Log.e(TAG, "Error scheduling hide loading indicator: " + e.getMessage(), e);
        }
    }
    
    private void openWithChromeCustomTabs(String url, Activity activity) {
        try {
            if (isChromeCustomTabsAvailable()) {
                Log.d(TAG, "Opening URL with Chrome Custom Tabs");
                openWithReflectionChromeCustomTabs(url, activity);
            } else {
                Log.w(TAG, "Chrome Custom Tabs not available (androidx.browser library missing). Falling back to default browser.");
                openWithDefaultBrowser(url, activity);
            }
        } catch (Exception e) {
            Log.e(TAG, "Failed to open browser: " + e.getMessage());
            // Fallback to default browser on any error
            try {
                openWithDefaultBrowser(url, activity);
            } catch (Exception fallbackException) {
                Log.e(TAG, "Failed to open default browser: " + fallbackException.getMessage());
            }
        }
    }
    
    private boolean isChromeCustomTabsAvailable() {
        try {
            Class.forName("androidx.browser.customtabs.CustomTabsIntent");
            return true;
        } catch (ClassNotFoundException e) {
            // AndroidX Browser library is not included in the project
            // This is expected if the game doesn't include androidx.browser:browser dependency
            return false;
        }
    }
    
    private void openWithReflectionChromeCustomTabs(String url, Activity activity) throws Exception {
        if (activity == null || url == null || url.isEmpty()) {
            throw new IllegalArgumentException("Invalid activity or URL");
        }

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
            try {
                StashUnityBridge.sendDialogDismissed();
            } catch (Exception e) {
                Log.e(TAG, "Error sending dialog dismissed: " + e.getMessage(), e);
            }
        }, 1000);
    }
    
    private void openWithDefaultBrowser(String url, Activity activity) {
        if (activity == null || url == null || url.isEmpty()) {
            Log.e(TAG, "Invalid activity or URL in openWithDefaultBrowser");
            return;
        }

        try {
            Intent browserIntent = new Intent(Intent.ACTION_VIEW, Uri.parse(url));
            browserIntent.addFlags(Intent.FLAG_ACTIVITY_NEW_TASK);
            activity.startActivity(browserIntent);
            isCurrentlyPresented = true;

            new Handler(Looper.getMainLooper()).postDelayed(() -> {
                try {
                    StashUnityBridge.sendDialogDismissed();
                } catch (Exception e) {
                    Log.e(TAG, "Error sending dialog dismissed: " + e.getMessage(), e);
                }
            }, 1000);
        } catch (Exception e) {
            Log.e(TAG, "Error opening default browser: " + e.getMessage(), e);
            isCurrentlyPresented = false;
        }
    }
    
    private void dismissCurrentDialog() {
        try {
            if (currentDialog != null) {
                dismissPopupDialog();
            }
        } catch (Exception e) {
            Log.e(TAG, "Error in dismissCurrentDialog: " + e.getMessage(), e);
            cleanupAllViews();
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
    
    private int[] calculatePopupDimensions(Activity activity) {
        if (activity == null) {
            Log.e(TAG, "Activity is null in calculatePopupDimensions");
            // Return default dimensions
            return new int[]{800, 600};
        }

        try {
            DisplayMetrics metrics = activity.getResources().getDisplayMetrics();
            boolean isLandscape = activity.getResources().getConfiguration().orientation == Configuration.ORIENTATION_LANDSCAPE;
            
            int smallerDimension = Math.min(metrics.widthPixels, metrics.heightPixels);
            boolean isTablet = StashWebViewUtils.isTablet(activity);
            int baseSize = Math.max(
                isTablet ? StashWebViewUtils.dpToPx(activity, 400) : StashWebViewUtils.dpToPx(activity, 300),
                Math.min(isTablet ? StashWebViewUtils.dpToPx(activity, 500) : StashWebViewUtils.dpToPx(activity, 500), (int)(smallerDimension * (isTablet ? 0.5f : 0.75f)))
            );
            
            float widthMultiplier = isLandscape ? 
                (useCustomSize ? customLandscapeWidthMultiplier : 1.2275445f) :
                (useCustomSize ? customPortraitWidthMultiplier : 1.0285f);
            float heightMultiplier = isLandscape ? 
                (useCustomSize ? customLandscapeHeightMultiplier : 1.1385f) :
                (useCustomSize ? customPortraitHeightMultiplier : 1.485f);

            int popupWidth = (int)(baseSize * widthMultiplier);
            int popupHeight = (int)(baseSize * heightMultiplier);

            return new int[]{popupWidth, popupHeight};
        } catch (Exception e) {
            Log.e(TAG, "Error calculating popup dimensions: " + e.getMessage(), e);
            // Return safe fallback dimensions
            try {
                DisplayMetrics metrics = activity.getResources().getDisplayMetrics();
                return new int[]{
                    (int)(metrics.widthPixels * 0.9f),
                    (int)(metrics.heightPixels * 0.7f)
                };
            } catch (Exception e2) {
                Log.e(TAG, "Error getting fallback dimensions: " + e2.getMessage(), e2);
                return new int[]{800, 600};
            }
        }
    }
}
