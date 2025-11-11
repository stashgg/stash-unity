package com.stash.popup;

import android.app.Activity;
import android.content.Context;
import android.content.Intent;
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
 * Portrait-locked transparent Activity for displaying StashPayCard when Unity game is in landscape.
 * This Activity forces portrait orientation (including keyboard) while keeping Unity visible underneath.
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
    private boolean wasLandscapeBeforeRotation;
    
    @Override
    protected void onCreate(Bundle savedInstanceState) {
        super.onCreate(savedInstanceState);
        
        Log.i(TAG, "StashPayCardPortraitActivity onCreate() called");
        
        // Get parameters from Intent
        Intent intent = getIntent();
        url = intent.getStringExtra("url");
        initialURL = intent.getStringExtra("initialURL");
        cardHeightRatio = intent.getFloatExtra("cardHeightRatio", 0.68f);
        usePopup = intent.getBooleanExtra("usePopup", false);
        wasLandscapeBeforeRotation = intent.getBooleanExtra("wasLandscape", false);
        
        Log.i(TAG, "Config - URL: " + url + ", usePopup: " + usePopup + ", wasLandscape: " + wasLandscapeBeforeRotation);
        
        // Configure window to keep Unity running
        requestWindowFeature(Window.FEATURE_NO_TITLE);
        Window window = getWindow();
        window.setBackgroundDrawable(new ColorDrawable(Color.TRANSPARENT));
        
        // Critical flags to prevent Unity from pausing
        window.addFlags(WindowManager.LayoutParams.FLAG_HARDWARE_ACCELERATED);
        window.addFlags(WindowManager.LayoutParams.FLAG_NOT_TOUCH_MODAL);
        window.addFlags(WindowManager.LayoutParams.FLAG_WATCH_OUTSIDE_TOUCH);
        
        // Keep Unity visible underneath
        window.addFlags(WindowManager.LayoutParams.FLAG_DIM_BEHIND);
        WindowManager.LayoutParams params = window.getAttributes();
        params.dimAmount = 0.3f;
        window.setAttributes(params);
        
        createUI();
    }
    
    private void createUI() {
        rootLayout = new FrameLayout(this);
        
        // For phones rotated from landscape: use solid background to hide rotated Unity
        // For tablets or phones already in portrait: use transparent overlay
        if (wasLandscapeBeforeRotation && !isTablet()) {
            int solidBg = isDarkTheme() ? Color.BLACK : Color.WHITE;
            rootLayout.setBackgroundColor(solidBg);
            Log.i(TAG, "Using solid background (phone rotated from landscape)");
        } else {
            rootLayout.setBackgroundColor(Color.parseColor("#20000000")); // Transparent overlay
            Log.i(TAG, "Using transparent background (tablet or already portrait)");
        }
        
        if (usePopup) {
            createPopup();
        } else {
            createCard();
        }
        
        // Only dismiss on tap-outside for cards, not popups
        if (!usePopup) {
            rootLayout.setOnClickListener(v -> dismissWithAnimation());
        }
        cardContainer.setOnClickListener(v -> {}); // Consume clicks on container
        
        setContentView(rootLayout);
    }
    
    private void createCard() {
        DisplayMetrics metrics = getResources().getDisplayMetrics();
        
        // If rotated from landscape: make card much larger to fill more space (95%)
        // If already portrait: use normal size (68%)
        float heightRatio = wasLandscapeBeforeRotation ? 0.95f : cardHeightRatio;
        int cardHeight = (int) (metrics.heightPixels * heightRatio);
        
        // For tablets: use narrower width for better readability (70% of screen width, max 600dp)
        // For phones: use full width
        int cardWidth;
        if (isTablet()) {
            int maxTabletWidth = dpToPx(600); // Max 600dp width on tablets
            int preferredWidth = (int)(metrics.widthPixels * 0.7f); // 70% of screen width
            cardWidth = Math.min(preferredWidth, maxTabletWidth);
        } else {
            cardWidth = FrameLayout.LayoutParams.MATCH_PARENT; // Full width on phones
        }
        
        Log.i(TAG, "Card size - height: " + cardHeight + ", width: " + cardWidth + ", isTablet: " + isTablet());
        
        cardContainer = new FrameLayout(this);
        FrameLayout.LayoutParams params = new FrameLayout.LayoutParams(cardWidth, cardHeight);
        params.gravity = Gravity.BOTTOM | Gravity.CENTER_HORIZONTAL; // Center horizontally on tablets
        cardContainer.setLayoutParams(params);
        
        GradientDrawable bg = new GradientDrawable();
        bg.setColor(isDarkTheme() ? Color.parseColor("#1C1C1E") : Color.WHITE);
        float radius = dpToPx(25);
        bg.setCornerRadii(new float[]{radius, radius, radius, radius, 0, 0, 0, 0});
        cardContainer.setBackground(bg);
        
        if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.LOLLIPOP) {
            cardContainer.setElevation(dpToPx(24));
            cardContainer.setOutlineProvider(new ViewOutlineProvider() {
                public void getOutline(View view, Outline outline) {
                    outline.setRoundRect(0, 0, view.getWidth(), view.getHeight() + dpToPx(25), dpToPx(25));
                }
            });
            cardContainer.setClipToOutline(true);
        }
        
        cardContainer.setClipChildren(true);
        cardContainer.setClipToPadding(true);
        
        // Add WebView first (bottom layer)
        addWebView();
        
        // Add drag handle on top of WebView so it receives touches
        addDragHandle();
        
        // Add home button on top
        addHomeButton();
        
        rootLayout.addView(cardContainer);
        animateSlideUp();
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
        bg.setCornerRadius(dpToPx(20));
        cardContainer.setBackground(bg);
        
        if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.LOLLIPOP) {
            cardContainer.setElevation(dpToPx(24));
        }
        
        addWebView();
        rootLayout.addView(cardContainer);
        animateFadeIn();
    }
    
    private void addDragHandle() {
        LinearLayout dragArea = new LinearLayout(this);
        dragArea.setOrientation(LinearLayout.VERTICAL);
        dragArea.setGravity(Gravity.CENTER_HORIZONTAL);
        // Larger padding for easier touch interaction - increased from 8dp to 16dp vertical
        dragArea.setPadding(dpToPx(20), dpToPx(16), dpToPx(20), dpToPx(16));
        
        View handle = new View(this);
        GradientDrawable handleBg = new GradientDrawable();
        handleBg.setColor(Color.parseColor("#D1D1D6"));
        handleBg.setCornerRadius(dpToPx(2));
        handle.setBackground(handleBg);
        // Slightly wider and taller handle for better visibility
        handle.setLayoutParams(new LinearLayout.LayoutParams(dpToPx(36), dpToPx(5)));
        dragArea.addView(handle);
        
        // Make drag area wider for easier interaction
        FrameLayout.LayoutParams params = new FrameLayout.LayoutParams(
            dpToPx(120), // Wider drag area for easier touch
            FrameLayout.LayoutParams.WRAP_CONTENT);
        params.gravity = Gravity.TOP | Gravity.CENTER_HORIZONTAL;
        dragArea.setLayoutParams(params);
        
        if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.LOLLIPOP) {
            dragArea.setElevation(dpToPx(8));
        }
        
        // Make drag area clickable and focusable to receive touch events
        dragArea.setClickable(true);
        dragArea.setFocusable(true);
        
        // Add drag-to-dismiss and drag-to-expand touch handling
        addDragTouchHandling(dragArea);
        
        cardContainer.addView(dragArea);
    }
    
    private boolean isExpanded = false;
    
    private void addDragTouchHandling(View dragArea) {
        dragArea.setOnTouchListener(new View.OnTouchListener() {
            private float initialY = 0;
            private float initialTranslationY = 0;
            private boolean isDragging = false;
            
            @Override
            public boolean onTouch(View v, MotionEvent event) {
                if (cardContainer == null) return false;
                
                switch (event.getAction()) {
                    case MotionEvent.ACTION_DOWN:
                        Log.d(TAG, "Drag handle touched - ACTION_DOWN");
                        initialY = event.getRawY();
                        initialTranslationY = cardContainer.getTranslationY();
                        isDragging = false;
                        return true;
                    
                    case MotionEvent.ACTION_MOVE:
                        float deltaY = event.getRawY() - initialY;
                        
                        if (Math.abs(deltaY) > dpToPx(10)) {
                            isDragging = true;
                            
                            if (deltaY > 0) {
                                // Downward drag - dismiss behavior (follow finger exactly)
                                float newTranslationY = initialTranslationY + deltaY;
                                cardContainer.setTranslationY(newTranslationY);
                                float progress = Math.min(deltaY / dpToPx(200), 1.0f);
                                cardContainer.setAlpha(1.0f - (progress * 0.3f));
                            } else if (deltaY < 0 && !isExpanded && !wasLandscapeBeforeRotation) {
                                // Upward drag - expand behavior (only if not already large from landscape rotation)
                                DisplayMetrics metrics = getResources().getDisplayMetrics();
                                int screenHeight = metrics.heightPixels;
                                int baseHeight = (int)(screenHeight * cardHeightRatio);
                                int maxHeight = (int)(screenHeight * 0.95f);
                                
                                // Make height follow finger drag directly (balanced multiplier)
                                int heightIncrease = (int)(Math.abs(deltaY) * 0.75f); // 75% tracking for natural feel
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
                                // Downward drag
                                int dismissThreshold = (int)(metrics.heightPixels * 0.25f);
                                if (finalDeltaY > dismissThreshold) {
                                    animateDismiss();
                                } else {
                                    animateSnapBack();
                                }
                            } else if (finalDeltaY < 0 && !isExpanded && !wasLandscapeBeforeRotation) {
                                // Upward drag (only allow expand if not already large from landscape)
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
        // When expanding, go to 95% (or already there if rotated from landscape)
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
        // If rotated from landscape, normal size is already 95%
        int normalHeight = wasLandscapeBeforeRotation ? 
            (int)(metrics.heightPixels * 0.95f) : 
            (int)(metrics.heightPixels * 0.68f);
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
        webView = new WebView(this);
        WebSettings settings = webView.getSettings();
        settings.setJavaScriptEnabled(true);
        settings.setDomStorageEnabled(true);
        
        // Enable dark mode rendering if system is in dark mode
        if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.Q) {
            if (isDarkTheme()) {
                settings.setForceDark(WebSettings.FORCE_DARK_ON);
            } else {
                settings.setForceDark(WebSettings.FORCE_DARK_OFF);
            }
        }
        
        webView.setWebViewClient(new WebViewClient() {
            public void onPageStarted(WebView view, String url, Bitmap favicon) {
                showLoading();
                view.setVisibility(View.INVISIBLE);
                injectSDK(view);
                checkProvider(url);
            }
            public void onPageFinished(WebView view, String url) {
                injectSDK(view);
                checkProvider(url);
                view.postDelayed(() -> {
                    hideLoading();
                    view.setVisibility(View.VISIBLE);
                }, 300);
            }
        });
        
        webView.addJavascriptInterface(new JSInterface(), "StashAndroid");
        webView.setBackgroundColor(Color.TRANSPARENT);
        webView.setLayoutParams(new FrameLayout.LayoutParams(
            FrameLayout.LayoutParams.MATCH_PARENT, FrameLayout.LayoutParams.MATCH_PARENT));
        webView.loadUrl(url);
        cardContainer.addView(webView);
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
            if (initialURL != null) webView.loadUrl(initialURL);
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
                
                // Use application context instead of Activity context for proper Material theme
                Context appContext = getApplicationContext();
                
                // Create ProgressBar with default style (not Large - that's the thick one)
                loadingIndicator = new ProgressBar(appContext);
                
                // Enable hardware acceleration on the ProgressBar itself
                if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.HONEYCOMB) {
                    loadingIndicator.setLayerType(View.LAYER_TYPE_HARDWARE, null);
                }
                
                // Set to indeterminate mode (spinning animation)
                loadingIndicator.setIndeterminate(true);
                
                // Apply theme color
                if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.LOLLIPOP) {
                    loadingIndicator.setIndeterminateTintList(
                        android.content.res.ColorStateList.valueOf(isDarkTheme() ? Color.WHITE : Color.DKGRAY));
                }
                
                // Same size as popup
                FrameLayout.LayoutParams params = new FrameLayout.LayoutParams(dpToPx(48), dpToPx(48));
                params.gravity = Gravity.CENTER;
                loadingIndicator.setLayoutParams(params);
                
                // Add to container
                if (cardContainer != null) {
                    cardContainer.addView(loadingIndicator);
                    loadingIndicator.bringToFront();
                    
                    // Force the animation to start after layout
                    loadingIndicator.post(() -> {
                        if (loadingIndicator != null) {
                            loadingIndicator.setVisibility(View.VISIBLE);
                            loadingIndicator.requestLayout();
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
            
            // Fade out animation before removing
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
        // Don't pause WebView - keep it running
        if (webView != null) {
            webView.onResume(); // Keep WebView active
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
        if (webView != null) {
            webView.destroy();
        }
        UnityPlayer.UnitySendMessage("StashPayCard", "OnAndroidDialogDismissed", "");
    }
    
    @Override
    public void onBackPressed() {
        dismissWithAnimation();
    }
    
    private boolean isDarkTheme() {
        if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.Q) {
            return (getResources().getConfiguration().uiMode & Configuration.UI_MODE_NIGHT_MASK) 
                == Configuration.UI_MODE_NIGHT_YES;
        }
        return false;
    }
    
    private boolean isTablet() {
        DisplayMetrics metrics = getResources().getDisplayMetrics();
        int smallerDimension = Math.min(metrics.widthPixels, metrics.heightPixels);
        float smallerDp = smallerDimension / metrics.density;
        return smallerDp >= 600; // Standard tablet threshold
    }
    
    private int dpToPx(int dp) {
        return Math.round(dp * getResources().getDisplayMetrics().density);
    }
}

