package com.stash.popup;

import android.app.Activity;
import android.content.Context;
import android.content.Intent;
import android.content.pm.ActivityInfo;
import android.content.res.Configuration;
import android.graphics.*;
import android.graphics.drawable.*;
import android.os.*;
import android.util.*;
import android.view.*;
import android.webkit.*;
import android.widget.*;
import com.unity3d.player.UnityPlayer;

/**
 * Transparent Activity for displaying StashPayCard checkout dialogs.
 * Supports both phones and tablets with appropriate sizing and rotation handling.
 */
public class StashPayCardPortraitActivity extends Activity {
    private static final String TAG = "StashPayCardPortrait";
    
    private FrameLayout rootLayout;
    private FrameLayout cardContainer;
    private WebView webView;
    private ProgressBar loadingIndicator;
    private Button homeButton;
    
    private String url;
    private String initialURL;
    private float cardHeightRatio;
    private boolean usePopup;
    private boolean isExpanded = false;
    private boolean isInitializing = true; // NOTE: Prevents orientation changes during setup
    private boolean wasLandscapeBeforePortrait = false; // NOTE: Track if device was landscape before forcing portrait
    
    @Override
    protected void onCreate(Bundle savedInstanceState) {
        super.onCreate(savedInstanceState);
        
        Intent intent = getIntent();
        url = intent.getStringExtra("url");
        initialURL = intent.getStringExtra("initialURL");
        cardHeightRatio = intent.getFloatExtra("cardHeightRatio", 0.68f);
        usePopup = intent.getBooleanExtra("usePopup", false);
        // NOTE: Get landscape state from Intent (detected in plugin before Activity launch)
        wasLandscapeBeforePortrait = intent.getBooleanExtra("wasLandscape", false);
        
        if (url == null || url.isEmpty()) {
            finish();
            return;
        }
        
        boolean isTablet = isTablet();
        
        // NOTE: Force portrait on phone checkout - Unity Activity is locked separately to prevent rotation
        if (usePopup || isTablet) {
            setRequestedOrientation(ActivityInfo.SCREEN_ORIENTATION_FULL_SENSOR);
        } else {
            setRequestedOrientation(ActivityInfo.SCREEN_ORIENTATION_PORTRAIT);
        }
        
        // NOTE: Window setup to keep Unity running underneath
        requestWindowFeature(Window.FEATURE_NO_TITLE);
        Window window = getWindow();
        
        // NOTE: Use black background when triggered from landscape on phones (not tablets)
        // This ensures consistent appearance regardless of system theme
        if (wasLandscapeBeforePortrait && !isTablet && !usePopup) {
            window.setBackgroundDrawable(new ColorDrawable(Color.BLACK));
        } else {
            window.setBackgroundDrawable(new ColorDrawable(Color.TRANSPARENT));
        }
        
        window.addFlags(WindowManager.LayoutParams.FLAG_HARDWARE_ACCELERATED);
        window.addFlags(WindowManager.LayoutParams.FLAG_NOT_TOUCH_MODAL);
        window.addFlags(WindowManager.LayoutParams.FLAG_WATCH_OUTSIDE_TOUCH);
        window.addFlags(WindowManager.LayoutParams.FLAG_DIM_BEHIND);
        
        WindowManager.LayoutParams params = window.getAttributes();
        params.dimAmount = 0.3f;
        window.setAttributes(params);
        
        createUI();
        
        // NOTE: Mark initialization complete after UI is created to allow orientation changes
        isInitializing = false;
    }
    
    private void createUI() {
        rootLayout = new FrameLayout(this);
        
        // NOTE: Use black background when triggered from landscape on phones (not tablets)
        // Always black, regardless of system theme
        boolean isTablet = isTablet();
        if (wasLandscapeBeforePortrait && !isTablet && !usePopup) {
            rootLayout.setBackgroundColor(Color.BLACK);
        } else {
            // NOTE: Use transparent overlay for other cases - matches iOS approach
            rootLayout.setBackgroundColor(Color.parseColor("#20000000"));
        }
        
        if (usePopup) {
            createPopup();
        } else {
            createCard();
        }
        
        // Dismiss on tap-outside for cards only
        if (!usePopup) {
            rootLayout.setOnClickListener(v -> dismissWithAnimation());
        }
        cardContainer.setOnClickListener(v -> {});
        
        setContentView(rootLayout);
    }
    
    private void createCard() {
        DisplayMetrics metrics = getResources().getDisplayMetrics();
        boolean isTablet = isTablet(); // Cache result to avoid multiple calls
        
        // NOTE: Expand card in landscape mode on phones (not tablets)
        // Use larger height ratio when device was in landscape before forcing portrait
        float effectiveHeightRatio = cardHeightRatio;
        if (wasLandscapeBeforePortrait && !isTablet) {
            // Always use 0.85 (85% of screen height) for landscape expansion
            effectiveHeightRatio = 0.85f;
        }
        
        int cardWidth, cardHeight;
        cardHeight = (int) (metrics.heightPixels * effectiveHeightRatio);
        if (isTablet) {
            int maxTabletWidth = dpToPx(600);
            int preferredWidth = (int)(metrics.widthPixels * 0.7f);
            cardWidth = Math.min(preferredWidth, maxTabletWidth);
        } else {
            cardWidth = FrameLayout.LayoutParams.MATCH_PARENT;
        }
        
        cardContainer = new FrameLayout(this);
        FrameLayout.LayoutParams params = new FrameLayout.LayoutParams(cardWidth, cardHeight);
        params.gravity = isTablet ? Gravity.CENTER : (Gravity.BOTTOM | Gravity.CENTER_HORIZONTAL);
        cardContainer.setLayoutParams(params);
        
        GradientDrawable bg = new GradientDrawable();
        bg.setColor(isDarkTheme() ? Color.parseColor("#1C1C1E") : Color.WHITE);
        float radius = dpToPx(25);
        
        if (isTablet) {
            bg.setCornerRadius(radius);
        } else {
            bg.setCornerRadii(new float[]{radius, radius, radius, radius, 0, 0, 0, 0});
        }
        cardContainer.setBackground(bg);
        
        // NOTE: Rounded corners with proper outline for elevation shadow
        if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.LOLLIPOP) {
            cardContainer.setElevation(dpToPx(24));
            
            if (isTablet) {
                cardContainer.setOutlineProvider(new ViewOutlineProvider() {
                    public void getOutline(View view, Outline outline) {
                        outline.setRoundRect(0, 0, view.getWidth(), view.getHeight(), radius);
                    }
                });
            } else {
                cardContainer.setOutlineProvider(new ViewOutlineProvider() {
                    public void getOutline(View view, Outline outline) {
                        outline.setRoundRect(0, 0, view.getWidth(), view.getHeight() + dpToPx(25), dpToPx(25));
                    }
                });
            }
            cardContainer.setClipToOutline(true);
        }
        
        cardContainer.setClipChildren(false);
        cardContainer.setClipToPadding(false);
        
        rootLayout.addView(cardContainer);
        
        addWebView();
        addDragHandle();
        addHomeButton();
        
        if (isTablet) {
            animateFadeIn();
        } else {
            animateSlideUp();
        }
    }
    
    private void createPopup() {
        DisplayMetrics metrics = getResources().getDisplayMetrics();
        int size = (int)(Math.min(metrics.widthPixels, metrics.heightPixels) * 0.75f);
        
        cardContainer = new FrameLayout(this);
        FrameLayout.LayoutParams params = new FrameLayout.LayoutParams(size, size);
        params.gravity = Gravity.CENTER;
        cardContainer.setLayoutParams(params);
        
        GradientDrawable bg = new GradientDrawable();
        bg.setColor(isDarkTheme() ? Color.parseColor("#1C1C1E") : Color.WHITE);
        float radius = dpToPx(20);
        bg.setCornerRadius(radius);
        cardContainer.setBackground(bg);
        
        if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.LOLLIPOP) {
            cardContainer.setElevation(dpToPx(24));
            cardContainer.setOutlineProvider(new ViewOutlineProvider() {
                public void getOutline(View view, Outline outline) {
                    outline.setRoundRect(0, 0, view.getWidth(), view.getHeight(), radius);
                }
            });
            cardContainer.setClipToOutline(true);
        }
        
        addWebView();
        rootLayout.addView(cardContainer);
        animateFadeIn();
    }
    
    private void addDragHandle() {
        LinearLayout dragArea = new LinearLayout(this);
        dragArea.setOrientation(LinearLayout.VERTICAL);
        dragArea.setGravity(Gravity.CENTER_HORIZONTAL);
        dragArea.setPadding(dpToPx(20), dpToPx(16), dpToPx(20), dpToPx(16));
        
        View handle = new View(this);
        GradientDrawable handleBg = new GradientDrawable();
        handleBg.setColor(Color.parseColor("#D1D1D6"));
        handleBg.setCornerRadius(dpToPx(2));
        handle.setBackground(handleBg);
        handle.setLayoutParams(new LinearLayout.LayoutParams(dpToPx(36), dpToPx(5)));
        dragArea.addView(handle);
        
        FrameLayout.LayoutParams params = new FrameLayout.LayoutParams(
            dpToPx(120), FrameLayout.LayoutParams.WRAP_CONTENT);
        params.gravity = Gravity.TOP | Gravity.CENTER_HORIZONTAL;
        dragArea.setLayoutParams(params);
        
        if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.LOLLIPOP) {
            dragArea.setElevation(dpToPx(8));
        }
        
        dragArea.setClickable(true);
        dragArea.setFocusable(true);
        
        addDragTouchHandling(dragArea);
        
        cardContainer.addView(dragArea);
    }
    
    private void addDragTouchHandling(View dragArea) {
        dragArea.setOnTouchListener(new View.OnTouchListener() {
            private float initialY = 0;
            private float initialTranslationY = 0;
            private boolean isDragging = false;
            
            @Override
            public boolean onTouch(View v, MotionEvent event) {
                if (cardContainer == null) return false;
                
                boolean isTablet = isTablet();
                
                switch (event.getAction()) {
                    case MotionEvent.ACTION_DOWN:
                        initialY = event.getRawY();
                        initialTranslationY = cardContainer.getTranslationY();
                        isDragging = false;
                        return true;
                    
                    case MotionEvent.ACTION_MOVE:
                        float deltaY = event.getRawY() - initialY;
                        
                        if (Math.abs(deltaY) > dpToPx(10)) {
                            isDragging = true;
                            
                            if (deltaY > 0) {
                                // Downward drag - dismiss
                                float newTranslationY = initialTranslationY + deltaY;
                                cardContainer.setTranslationY(newTranslationY);
                                DisplayMetrics metrics = getResources().getDisplayMetrics();
                                float progress = Math.min(deltaY / metrics.heightPixels, 1.0f);
                                cardContainer.setAlpha(1.0f - (progress * 0.5f));
                            } else if (deltaY < 0 && !isTablet && !isExpanded) {
                                // Upward drag - expand (phones only)
                                DisplayMetrics metrics = getResources().getDisplayMetrics();
                                int screenHeight = metrics.heightPixels;
                                int baseHeight = (int)(screenHeight * cardHeightRatio);
                                int maxHeight = (int)(screenHeight * 0.95f);
                                
                                int heightIncrease = (int)(Math.abs(deltaY) * 0.75f);
                                int newHeight = Math.min(baseHeight + heightIncrease, maxHeight);
                                
                                FrameLayout.LayoutParams cardParams = 
                                    (FrameLayout.LayoutParams)cardContainer.getLayoutParams();
                                cardParams.height = newHeight;
                                cardContainer.setLayoutParams(cardParams);
                            }
                        }
                        return true;
                    
                    case MotionEvent.ACTION_UP:
                    case MotionEvent.ACTION_CANCEL:
                        if (isDragging) {
                            float finalDeltaY = event.getRawY() - initialY;
                            DisplayMetrics metrics = getResources().getDisplayMetrics();
                            
                            if (finalDeltaY > 0) {
                                int dismissThreshold = isTablet ? (int)(metrics.heightPixels * 0.2f) : (int)(metrics.heightPixels * 0.25f);
                                if (finalDeltaY > dismissThreshold) {
                                    animateDismiss();
                                } else {
                                    animateSnapBack();
                                }
                            } else if (finalDeltaY < 0 && !isTablet && !isExpanded) {
                                if (Math.abs(finalDeltaY) > dpToPx(80)) {
                                    animateExpand();
                                } else {
                                    animateSnapBack();
                                }
                            } else {
                                animateSnapBack();
                            }
                        }
                        return true;
                }
                return false;
            }
        });
    }
    
    private void animateDismiss() {
        if (cardContainer == null) return;
        int height = cardContainer.getHeight();
        if (height == 0) height = (int)(getResources().getDisplayMetrics().heightPixels * 0.68f);
        
        cardContainer.animate()
            .translationY(height)
            .alpha(0f)
            .setDuration(300)
            .withEndAction(this::finish)
            .start();
    }
    
    private void animateExpand() {
        if (cardContainer == null) return;
        DisplayMetrics metrics = getResources().getDisplayMetrics();
        int expandedHeight = (int)(metrics.heightPixels * 0.95f);
        
        FrameLayout.LayoutParams params = (FrameLayout.LayoutParams)cardContainer.getLayoutParams();
        android.animation.ValueAnimator animator = android.animation.ValueAnimator.ofInt(params.height, expandedHeight);
        animator.setDuration(300);
        animator.addUpdateListener(animation -> {
            params.height = (Integer)animation.getAnimatedValue();
            cardContainer.setLayoutParams(params);
        });
        animator.start();
        
        cardContainer.animate()
            .translationY(0)
            .alpha(1f)
            .scaleX(1f)
            .scaleY(1f)
            .setDuration(300)
            .start();
        
        isExpanded = true;
    }
    
    private void animateSnapBack() {
        if (cardContainer == null) return;
        DisplayMetrics metrics = getResources().getDisplayMetrics();
        
        // NOTE: Use effective height ratio (accounts for landscape expansion)
        float effectiveHeightRatio = cardHeightRatio;
        boolean isTablet = isTablet();
        if (wasLandscapeBeforePortrait && !isTablet) {
            // Always use 0.85 (85% of screen height) for landscape expansion
            effectiveHeightRatio = 0.85f;
        }
        
        int normalHeight = (int)(metrics.heightPixels * effectiveHeightRatio);
        int targetHeight = isExpanded ? 
            (int)(metrics.heightPixels * 0.95f) : 
            normalHeight;
        
        FrameLayout.LayoutParams params = (FrameLayout.LayoutParams)cardContainer.getLayoutParams();
        if (params.height != targetHeight) {
            android.animation.ValueAnimator animator = android.animation.ValueAnimator.ofInt(params.height, targetHeight);
            animator.setDuration(250);
            animator.addUpdateListener(animation -> {
                params.height = (Integer)animation.getAnimatedValue();
                cardContainer.setLayoutParams(params);
            });
            animator.start();
        }
        
        cardContainer.animate()
            .translationY(0)
            .alpha(1f)
            .scaleX(1f)
            .scaleY(1f)
            .setDuration(250)
            .start();
    }
    
    private void addWebView() {
        if (url == null || url.isEmpty() || cardContainer == null) {
            return;
        }
        
        webView = new WebView(this);
        
        WebSettings settings = webView.getSettings();
        settings.setJavaScriptEnabled(true);
        settings.setDomStorageEnabled(true);
        settings.setLoadWithOverviewMode(true);
        settings.setUseWideViewPort(true);
        settings.setBuiltInZoomControls(false);
        settings.setDisplayZoomControls(false);
        settings.setSupportZoom(false);
        
        if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.Q) {
            settings.setForceDark(isDarkTheme() ? WebSettings.FORCE_DARK_ON : WebSettings.FORCE_DARK_OFF);
        }
        
        webView.setWebViewClient(new WebViewClient() {
            @Override
            public void onPageStarted(WebView view, String url, Bitmap favicon) {
                super.onPageStarted(view, url, favicon);
                showLoading();
                injectSDK(view);
                checkProvider(url);
            }
            
            @Override
            public void onPageFinished(WebView view, String url) {
                super.onPageFinished(view, url);
                hideLoading();
                injectSDK(view);
                checkProvider(url);
            }
            
            @Override
            public void onReceivedError(WebView view, WebResourceRequest request, WebResourceError error) {
                super.onReceivedError(view, request, error);
                Log.e(TAG, "WebView error: " + error.getDescription());
            }
        });
        
        webView.setWebChromeClient(new WebChromeClient());
        webView.addJavascriptInterface(new JSInterface(), "StashAndroid");
        
        int bgColor = isDarkTheme() ? Color.parseColor("#1C1C1E") : Color.WHITE;
        webView.setBackgroundColor(bgColor);
        
        FrameLayout.LayoutParams params = new FrameLayout.LayoutParams(
            FrameLayout.LayoutParams.MATCH_PARENT, FrameLayout.LayoutParams.MATCH_PARENT);
        webView.setLayoutParams(params);
        
        cardContainer.addView(webView);
        
        // NOTE: Load URL after container is laid out to ensure WebView has proper dimensions
        cardContainer.post(() -> {
            if (webView != null) {
                webView.loadUrl(url);
            }
        });
    }
    
    private void addHomeButton() {
        homeButton = new Button(this);
        homeButton.setText("⌂");
        homeButton.setTextSize(18);
        homeButton.setTextColor(Color.parseColor("#8E8E93"));
        homeButton.setGravity(Gravity.CENTER);
        homeButton.setPadding(0, 0, 0, 0);
        
        GradientDrawable bg = new GradientDrawable();
        bg.setColor(isDarkTheme() ? Color.parseColor("#2C2C2E") : Color.parseColor("#F2F2F7"));
        bg.setCornerRadius(dpToPx(20));
        bg.setStroke(dpToPx(1), isDarkTheme() ? Color.parseColor("#38383A") : Color.parseColor("#E5E5EA"));
        homeButton.setBackground(bg);
        
        if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.LOLLIPOP) {
            homeButton.setElevation(dpToPx(6));
        }
        
        FrameLayout.LayoutParams params = new FrameLayout.LayoutParams(dpToPx(36), dpToPx(36));
        params.gravity = Gravity.TOP | Gravity.START;
        params.setMargins(dpToPx(12), dpToPx(12), 0, 0);
        homeButton.setLayoutParams(params);
        homeButton.setVisibility(View.GONE);
        homeButton.setOnClickListener(v -> {
            if (initialURL != null && webView != null) {
                webView.loadUrl(initialURL);
            }
        });
        
        cardContainer.addView(homeButton);
    }
    
    private void injectSDK(WebView view) {
        view.evaluateJavascript("(function(){" +
            "window.stash_sdk=window.stash_sdk||{};" +
            "window.stash_sdk.onPaymentSuccess=function(){try{StashAndroid.onPaymentSuccess()}catch(e){}};" +
            "window.stash_sdk.onPaymentFailure=function(){try{StashAndroid.onPaymentFailure()}catch(e){}};" +
            "window.stash_sdk.onPurchaseProcessing=function(){try{StashAndroid.onPurchaseProcessing()}catch(e){}};" +
            "window.stash_sdk.setPaymentChannel=function(t){try{StashAndroid.setPaymentChannel(t||'')}catch(e){}};" +
            "})();", null);
    }
    
    private void checkProvider(String url) {
        if (homeButton == null || url == null) return;
        String lower = url.toLowerCase();
        boolean show = lower.contains("klarna") || lower.contains("paypal") || lower.contains("stripe");
        runOnUiThread(() -> homeButton.setVisibility(show ? View.VISIBLE : View.GONE));
    }
    
    private void showLoading() {
        runOnUiThread(() -> {
            try {
                if (loadingIndicator != null && loadingIndicator.getParent() != null) {
                    ((ViewGroup)loadingIndicator.getParent()).removeView(loadingIndicator);
                }
                
                Context appContext = getApplicationContext();
                loadingIndicator = new ProgressBar(appContext);
                
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
                
                if (cardContainer != null) {
                    cardContainer.addView(loadingIndicator);
                    loadingIndicator.bringToFront();
                    loadingIndicator.post(() -> {
                        if (loadingIndicator != null) {
                            loadingIndicator.setVisibility(View.VISIBLE);
                        }
                    });
                }
            } catch (Exception e) {
                Log.e(TAG, "Error showing loading: " + e.getMessage());
            }
        });
    }
    
    private void hideLoading() {
        runOnUiThread(() -> {
            if (loadingIndicator == null) return;
            
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
        });
    }
    
    private void animateSlideUp() {
        cardContainer.setTranslationY(cardContainer.getHeight());
        cardContainer.post(() -> cardContainer.animate().translationY(0).setDuration(300).start());
    }
    
    private void animateFadeIn() {
        cardContainer.setAlpha(0f);
        cardContainer.setScaleX(0.9f);
        cardContainer.setScaleY(0.9f);
        cardContainer.animate().alpha(1f).scaleX(1f).scaleY(1f).setDuration(200).start();
    }
    
    private void dismissWithAnimation() {
        if (usePopup) {
            cardContainer.animate().alpha(0f).scaleX(0.9f).scaleY(0.9f).setDuration(150)
                .withEndAction(this::finish).start();
        } else {
            cardContainer.animate().translationY(cardContainer.getHeight()).setDuration(250)
                .withEndAction(this::finish).start();
        }
    }
    
    private class JSInterface {
        @android.webkit.JavascriptInterface
        public void onPaymentSuccess() {
            runOnUiThread(() -> {
                UnityPlayer.UnitySendMessage("StashPayCard", "OnAndroidPaymentSuccess", "");
                dismissWithAnimation();
            });
        }
        
        @android.webkit.JavascriptInterface
        public void onPaymentFailure() {
            runOnUiThread(() -> {
                UnityPlayer.UnitySendMessage("StashPayCard", "OnAndroidPaymentFailure", "");
                dismissWithAnimation();
            });
        }
        
        @android.webkit.JavascriptInterface
        public void onPurchaseProcessing() {}
        
        @android.webkit.JavascriptInterface
        public void setPaymentChannel(String optinType) {
            runOnUiThread(() -> {
                UnityPlayer.UnitySendMessage("StashPayCard", "OnAndroidOptinResponse", optinType != null ? optinType : "");
                dismissWithAnimation();
            });
        }
    }
    
    @Override
    protected void onPause() {
        super.onPause();
        if (webView != null) {
            webView.onPause();
        }
    }
    
    @Override
    protected void onResume() {
        super.onResume();
        if (webView != null) {
            webView.onResume();
        }
    }
    
    @Override
    protected void onDestroy() {
        super.onDestroy();
        
        // NOTE: Clean up WebView to prevent memory leaks
        if (webView != null) {
            webView.destroy();
            webView = null;
        }
        
        UnityPlayer.UnitySendMessage("StashPayCard", "OnAndroidDialogDismissed", "");
    }
    
    @Override
    public void onBackPressed() {
        dismissWithAnimation();
    }
    
    @Override
    public void onConfigurationChanged(Configuration newConfig) {
        super.onConfigurationChanged(newConfig);
        
        // NOTE: Ignore orientation changes during initial setup to prevent flicker
        if (isInitializing) {
            return;
        }
        
        // NOTE: Recreate UI on rotation for tablet checkout (seamless transformation)
        // Popup uses Dialog-based approach and doesn't need this
        if (!usePopup && cardContainer != null && rootLayout != null) {
            boolean isTablet = isTablet(); // Cache result
            if (isTablet) {
                rootLayout.removeAllViews();
                createUI();
            }
        }
    }
    
    private boolean isDarkTheme() {
        if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.Q) {
            return (getResources().getConfiguration().uiMode & Configuration.UI_MODE_NIGHT_MASK) 
                == Configuration.UI_MODE_NIGHT_YES;
        }
        return false;
    }
    
    // NOTE: Improved tablet detection using multiple methods for better accuracy
    private boolean isTablet() {
        DisplayMetrics metrics = getResources().getDisplayMetrics();
        int smallerDimension = Math.min(metrics.widthPixels, metrics.heightPixels);
        float smallerDp = smallerDimension / metrics.density;
        
        // Method 1: Screen size in dp (standard Android approach)
        boolean isTabletBySize = smallerDp >= 600;
        
        // Method 2: Check screen configuration (more reliable on some devices)
        boolean isTabletByConfig = false;
        if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.HONEYCOMB_MR2) {
            int screenSize = getResources().getConfiguration().screenLayout & Configuration.SCREENLAYOUT_SIZE_MASK;
            isTabletByConfig = (screenSize == Configuration.SCREENLAYOUT_SIZE_LARGE || 
                               screenSize == Configuration.SCREENLAYOUT_SIZE_XLARGE);
        }
        
        // Method 3: Aspect ratio check (tablets typically have different aspect ratios)
        float aspectRatio = (float) Math.max(metrics.widthPixels, metrics.heightPixels) / 
                           Math.min(metrics.widthPixels, metrics.heightPixels);
        boolean isTabletByAspect = aspectRatio < 2.0f && smallerDp >= 500;
        
        // Return true if any method indicates tablet (most permissive approach)
        return isTabletBySize || isTabletByConfig || isTabletByAspect;
    }
    
    private int dpToPx(int dp) {
        return Math.round(dp * getResources().getDisplayMetrics().density);
    }
}
