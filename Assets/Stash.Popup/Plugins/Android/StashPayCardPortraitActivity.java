package com.stash.popup;

import android.app.Activity;
import android.content.Intent;
import android.content.pm.ActivityInfo;
import android.content.res.Configuration;
import android.graphics.Color;
import android.graphics.Outline;
import android.graphics.drawable.ColorDrawable;
import android.graphics.drawable.GradientDrawable;
import android.os.Build;
import android.os.Bundle;
import android.util.DisplayMetrics;
import android.util.Log;
import android.view.Gravity;
import android.view.MotionEvent;
import android.view.View;
import android.view.ViewGroup;
import android.view.ViewOutlineProvider;
import android.view.Window;
import android.view.WindowManager;
import android.webkit.CookieManager;
import android.webkit.JavascriptInterface;
import android.webkit.WebChromeClient;
import android.webkit.WebSettings;
import android.webkit.WebView;
import android.webkit.WebViewClient;
import android.widget.Button;
import android.widget.FrameLayout;
import android.widget.LinearLayout;
import android.widget.ProgressBar;
import android.net.Uri;
import com.unity3d.player.UnityPlayer;

public class StashPayCardPortraitActivity extends Activity {
    private static final String TAG = "StashPayCard";
    private static final float CARD_HEIGHT_NORMAL = 0.68f;
    private static final float CARD_HEIGHT_EXPANDED = 0.95f;
    
    // JS SDK Script moved to StashWebViewUtils

    private FrameLayout rootLayout;
    private FrameLayout cardContainer;
    private WebView webView;
    private ProgressBar loadingIndicator;
    private Button homeButton;
    
    private String url;
    private String initialURL;
    private boolean usePopup;
    private boolean isExpanded;
    private boolean wasLandscapeBeforePortrait;
    private boolean isDismissing;
    private boolean callbackSent;
    private boolean googlePayRedirectHandled;
    private boolean isPurchaseProcessing;
    
    private static final String COLOR_LIGHT_BG = "#F2F2F7"; // Apple system gray 6
    private static final String COLOR_DARK_STROKE = "#38383A";
    private static final String COLOR_LIGHT_STROKE = "#E5E5EA";
    private static final String COLOR_DRAG_HANDLE = "#D1D1D6";
    private static final String COLOR_HOME_TEXT = "#8E8E93";
    
    private static final int ANIMATION_DURATION_SHORT = 200;
    private static final int ANIMATION_DURATION_MEDIUM = 300;
    private static final int ANIMATION_DURATION_LONG = 400;
    private static final float CORNER_RADIUS_DP = 12f;
    private static final float ELEVATION_DP = 24f;

    @Override
    protected void onCreate(Bundle savedInstanceState) {
        super.onCreate(savedInstanceState);
        
        Intent intent = getIntent();
        url = intent.getStringExtra("url");
        initialURL = intent.getStringExtra("initialURL");
        usePopup = intent.getBooleanExtra("usePopup", false);
        wasLandscapeBeforePortrait = intent.getBooleanExtra("wasLandscape", false);
        
        if (url == null || url.isEmpty()) {
            finish();
            return;
        }
        
        boolean isTablet = StashWebViewUtils.isTablet(this);
        
        if (usePopup) {
            setRequestedOrientation(ActivityInfo.SCREEN_ORIENTATION_FULL_SENSOR);
        } else if (!isTablet) {
            setRequestedOrientation(ActivityInfo.SCREEN_ORIENTATION_PORTRAIT);
        }
        
        Window window = getWindow();
        if (wasLandscapeBeforePortrait && !isTablet && !usePopup) {
            window.setBackgroundDrawable(new ColorDrawable(Color.BLACK));
        } else {
            window.setBackgroundDrawable(new ColorDrawable(Color.TRANSPARENT));
        }
        
        requestWindowFeature(Window.FEATURE_NO_TITLE);
        window.addFlags(WindowManager.LayoutParams.FLAG_HARDWARE_ACCELERATED);
        window.addFlags(WindowManager.LayoutParams.FLAG_NOT_TOUCH_MODAL);
        window.addFlags(WindowManager.LayoutParams.FLAG_WATCH_OUTSIDE_TOUCH);
        window.addFlags(WindowManager.LayoutParams.FLAG_DIM_BEHIND);
        
        WindowManager.LayoutParams params = window.getAttributes();
        params.dimAmount = 0.3f;
        window.setAttributes(params);
        
        createUI();
    }
    
    private void createUI() {
        rootLayout = new FrameLayout(this);
        boolean isTablet = StashWebViewUtils.isTablet(this);
        
        if (wasLandscapeBeforePortrait && !isTablet && !usePopup) {
            rootLayout.setBackgroundColor(Color.BLACK);
        } else {
            rootLayout.setBackgroundColor(Color.parseColor(StashWebViewUtils.COLOR_BACKGROUND_DIM));
        }
        
        if (usePopup) {
            createPopup();
        } else {
            createCard();
        }
        
        if (!usePopup) {
            rootLayout.setOnClickListener(v -> {
                if (!isDismissing && v == rootLayout && !isPurchaseProcessing) {
                    dismissWithAnimation();
                }
            });
        }
        cardContainer.setOnClickListener(v -> {});
        
        setContentView(rootLayout);
    }
    
    private void configureCardContainer(boolean isTablet, int cardWidth, int cardHeight) {
        cardContainer = new FrameLayout(this);
        FrameLayout.LayoutParams params = new FrameLayout.LayoutParams(cardWidth, cardHeight);
        params.gravity = isTablet ? Gravity.CENTER : (Gravity.BOTTOM | Gravity.CENTER_HORIZONTAL);
        cardContainer.setLayoutParams(params);
        
        GradientDrawable bg = new GradientDrawable();
        bg.setColor(StashWebViewUtils.isDarkTheme(this) ? Color.parseColor(StashWebViewUtils.COLOR_DARK_BG) : Color.WHITE);
        float radius = StashWebViewUtils.dpToPx(this, (int)CORNER_RADIUS_DP);
        
        if (isTablet) {
            bg.setCornerRadius(radius);
        } else {
            bg.setCornerRadii(new float[]{radius, radius, radius, radius, 0, 0, 0, 0});
        }
        cardContainer.setBackground(bg);
        
        if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.LOLLIPOP) {
            cardContainer.setElevation(StashWebViewUtils.dpToPx(this, (int)ELEVATION_DP));
            cardContainer.setOutlineProvider(new ViewOutlineProvider() {
                @Override
                public void getOutline(View view, Outline outline) {
                    if (isTablet) {
                        outline.setRoundRect(0, 0, view.getWidth(), view.getHeight(), radius);
                    } else {
                        outline.setRoundRect(0, 0, view.getWidth(), view.getHeight() + (int)radius, radius);
                    }
                }
            });
            cardContainer.setClipToOutline(true);
        }
    }

    private void createCard() {
        DisplayMetrics metrics = getResources().getDisplayMetrics();
        boolean isTablet = StashWebViewUtils.isTablet(this);
        
        float effectiveHeightRatio;
        if (wasLandscapeBeforePortrait && !isTablet) {
            effectiveHeightRatio = CARD_HEIGHT_EXPANDED;
            isExpanded = true;
        } else {
            effectiveHeightRatio = CARD_HEIGHT_NORMAL;
            // On tablets, default size is the "expanded" state
            isExpanded = isTablet;
        }
        
        int cardHeight = (int)(metrics.heightPixels * effectiveHeightRatio);
        int cardWidth = isTablet ? Math.min(StashWebViewUtils.dpToPx(this, 600), (int)(metrics.widthPixels * 0.7f)) 
                                  : FrameLayout.LayoutParams.MATCH_PARENT;
        
        configureCardContainer(isTablet, cardWidth, cardHeight);
        
        addWebView();
        addDragHandle();
        addHomeButton();
        rootLayout.addView(cardContainer);
        
        if (isTablet) {
            animateFadeIn();
        } else {
            animateSlideUp();
        }
    }
    
    private void createPopup() {
        DisplayMetrics metrics = getResources().getDisplayMetrics();
        int size = (int)(Math.min(metrics.widthPixels, metrics.heightPixels) * 0.75f);
        
        // Reuse configuration logic: isTablet=true forces CENTER gravity and rounded corners on all sides
        configureCardContainer(true, size, size);
        
        addWebView();
        rootLayout.addView(cardContainer);
        animateFadeIn();
    }
    
    private void addDragHandle() {
        LinearLayout dragArea = new LinearLayout(this);
        dragArea.setOrientation(LinearLayout.VERTICAL);
        dragArea.setGravity(Gravity.CENTER_HORIZONTAL);
        dragArea.setPadding(StashWebViewUtils.dpToPx(this, 20), StashWebViewUtils.dpToPx(this, 16), StashWebViewUtils.dpToPx(this, 20), StashWebViewUtils.dpToPx(this, 16));
        
        View handle = new View(this);
        GradientDrawable handleBg = new GradientDrawable();
        handleBg.setColor(Color.parseColor(COLOR_DRAG_HANDLE));
        handleBg.setCornerRadius(StashWebViewUtils.dpToPx(this, 2));
        handle.setBackground(handleBg);
        handle.setLayoutParams(new LinearLayout.LayoutParams(StashWebViewUtils.dpToPx(this, 36), StashWebViewUtils.dpToPx(this, 5)));
        dragArea.addView(handle);
        
        FrameLayout.LayoutParams params = new FrameLayout.LayoutParams(
            StashWebViewUtils.dpToPx(this, 120), FrameLayout.LayoutParams.WRAP_CONTENT);
        params.gravity = Gravity.TOP | Gravity.CENTER_HORIZONTAL;
        dragArea.setLayoutParams(params);
        
        if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.LOLLIPOP) {
            dragArea.setElevation(StashWebViewUtils.dpToPx(this, 8));
        }
        
        addDragTouchHandling(dragArea);
        cardContainer.addView(dragArea);
    }
    
    private class DragHandleTouchListener implements View.OnTouchListener {
        private float initialY;
        private float initialTranslationY;
        private boolean isDragging;
        
        @Override
        public boolean onTouch(View v, MotionEvent event) {
            if (cardContainer == null) return false;
            
            // Check if purchase is processing - prevent drag dismissal
            if (isPurchaseProcessing) {
                return false; // Don't handle drag when purchase is processing
            }
            
            boolean isTablet = StashWebViewUtils.isTablet(StashPayCardPortraitActivity.this);
            
            switch (event.getAction()) {
                case MotionEvent.ACTION_DOWN:
                    initialY = event.getRawY();
                    initialTranslationY = cardContainer.getTranslationY();
                    isDragging = false;
                    return true;
                
                case MotionEvent.ACTION_MOVE:
                    float deltaY = event.getRawY() - initialY;
                    
                    if (Math.abs(deltaY) > StashWebViewUtils.dpToPx(StashPayCardPortraitActivity.this, 10)) {
                        isDragging = true;
                        
                        if (deltaY > 0) {
                            float newTranslationY = initialTranslationY + deltaY;
                            cardContainer.setTranslationY(newTranslationY);
                            DisplayMetrics metrics = getResources().getDisplayMetrics();
                            float progress = Math.min(deltaY / metrics.heightPixels, 1.0f);
                            cardContainer.setAlpha(1.0f - (progress * 0.5f));
                        } else if (deltaY < 0 && !isTablet && !isExpanded && !wasLandscapeBeforePortrait) {
                            DisplayMetrics metrics = getResources().getDisplayMetrics();
                            float dragProgress = Math.min(Math.abs(deltaY) / StashWebViewUtils.dpToPx(StashPayCardPortraitActivity.this, 100), 1.0f);
                            cardContainer.setScaleX(1.0f + (dragProgress * 0.02f));
                            cardContainer.setScaleY(1.0f + (dragProgress * 0.02f));
                        }
                    }
                    return true;
                
                case MotionEvent.ACTION_UP:
                case MotionEvent.ACTION_CANCEL:
                    if (isDragging) {
                        float finalDeltaY = event.getRawY() - initialY;
                        DisplayMetrics metrics = getResources().getDisplayMetrics();
                        
                        if (finalDeltaY > 0) {
                            int dismissThreshold = isTablet ? (int)(metrics.heightPixels * 0.2f) 
                                                             : (int)(metrics.heightPixels * 0.25f);
                            if (finalDeltaY > dismissThreshold) {
                                animateDismiss();
                            } else {
                                animateSnapBack();
                            }
                        } else if (finalDeltaY < 0 && !isTablet && !isExpanded && !wasLandscapeBeforePortrait) {
                            if (Math.abs(finalDeltaY) > StashWebViewUtils.dpToPx(StashPayCardPortraitActivity.this, 80)) {
                                animateExpand();
                            } else {
                                animateSnapBack();
                            }
                        } else {
                            cardContainer.setScaleX(1.0f);
                            cardContainer.setScaleY(1.0f);
                            animateSnapBack();
                        }
                    }
                    return true;
            }
            return false;
        }
    }

    private void addDragTouchHandling(View dragArea) {
        dragArea.setOnTouchListener(new DragHandleTouchListener());
    }
    
    private void animateDismiss() {
        if (cardContainer == null) return;
        // Prevent dismissal when purchase is processing
        if (isPurchaseProcessing) return;
        int height = cardContainer.getHeight();
        if (height == 0) {
            height = (int)(getResources().getDisplayMetrics().heightPixels * CARD_HEIGHT_NORMAL);
        }
        
        cardContainer.animate()
            .translationY(height)
            .alpha(0f)
            .setDuration(400)
            .setInterpolator(new SpringInterpolator())
            .withEndAction(this::finish)
            .start();
    }
    
    private void animateCardHeight(int targetHeight, int duration) {
        FrameLayout.LayoutParams params = (FrameLayout.LayoutParams)cardContainer.getLayoutParams();
        android.animation.ValueAnimator heightAnimator = android.animation.ValueAnimator.ofInt(params.height, targetHeight);
        heightAnimator.setDuration(duration);
        heightAnimator.setInterpolator(new SpringInterpolator());
        heightAnimator.addUpdateListener(animation -> {
            params.height = (Integer)animation.getAnimatedValue();
            cardContainer.setLayoutParams(params);
        });
        heightAnimator.start();
    }

    private void animateCardWidth(int targetWidth, int duration) {
        FrameLayout.LayoutParams params = (FrameLayout.LayoutParams)cardContainer.getLayoutParams();
        android.animation.ValueAnimator widthAnimator = android.animation.ValueAnimator.ofInt(params.width, targetWidth);
        widthAnimator.setDuration(duration);
        widthAnimator.setInterpolator(new SpringInterpolator());
        widthAnimator.addUpdateListener(animation -> {
            params.width = (Integer)animation.getAnimatedValue();
            cardContainer.setLayoutParams(params);
        });
        widthAnimator.start();
    }

    private void animateExpand() {
        if (cardContainer == null) return;
        DisplayMetrics metrics = getResources().getDisplayMetrics();
        boolean isTablet = StashWebViewUtils.isTablet(this);
        
        FrameLayout.LayoutParams params = (FrameLayout.LayoutParams)cardContainer.getLayoutParams();
        
        int expandedHeight = (int)(metrics.heightPixels * CARD_HEIGHT_EXPANDED);
        int expandedWidth;
        
        if (isTablet) {
            // On tablets, Expand = default size (normal size)
            expandedWidth = Math.min(StashWebViewUtils.dpToPx(this, 600), (int)(metrics.widthPixels * 0.7f));
            expandedHeight = (int)(metrics.heightPixels * CARD_HEIGHT_NORMAL);
        } else {
            expandedWidth = params.width;
        }
        
        animateCardHeight(expandedHeight, isTablet ? 350 : 450);
        
        if (isTablet) {
            animateCardWidth(expandedWidth, 350);
        }
        
        cardContainer.animate()
            .translationY(0)
            .alpha(1f)
            .scaleX(1f)
            .scaleY(1f)
            .setDuration(isTablet ? 350 : 450)
            .setInterpolator(new SpringInterpolator())
            .start();
        
        isExpanded = true;
    }
    
    private void animateCollapse() {
        if (cardContainer == null || !isExpanded) return;
        DisplayMetrics metrics = getResources().getDisplayMetrics();
        boolean isTablet = StashWebViewUtils.isTablet(this);
        
        FrameLayout.LayoutParams params = (FrameLayout.LayoutParams)cardContainer.getLayoutParams();
        
        int collapsedHeight;
        int collapsedWidth;
        
        if (isTablet) {
            // On tablets, Collapse = 30% smaller than default size (0.7x)
            int defaultWidth = Math.min(StashWebViewUtils.dpToPx(this, 600), (int)(metrics.widthPixels * 0.7f));
            int defaultHeight = (int)(metrics.heightPixels * CARD_HEIGHT_NORMAL);
            collapsedWidth = (int)(defaultWidth * 0.7f);
            collapsedHeight = (int)(defaultHeight * 0.7f);
            
            animateCardWidth(collapsedWidth, 320);
        } else {
            collapsedHeight = (int)(metrics.heightPixels * CARD_HEIGHT_NORMAL);
            collapsedWidth = params.width;
        }
        
        animateCardHeight(collapsedHeight, isTablet ? 320 : 380);
        
        cardContainer.animate()
            .translationY(0)
            .alpha(1f)
            .scaleX(1f)
            .scaleY(1f)
            .setDuration(isTablet ? 320 : 380)
            .setInterpolator(new SpringInterpolator())
            .start();
        
        isExpanded = false;
    }
    
    private void animateSnapBack() {
        if (cardContainer == null) return;
        DisplayMetrics metrics = getResources().getDisplayMetrics();
        boolean isTablet = StashWebViewUtils.isTablet(this);
        
        int targetHeight;
        if (wasLandscapeBeforePortrait && !isTablet) {
            targetHeight = (int)(metrics.heightPixels * CARD_HEIGHT_EXPANDED);
            isExpanded = true;
        } else if (isExpanded) {
            targetHeight = (int)(metrics.heightPixels * CARD_HEIGHT_EXPANDED);
        } else {
            targetHeight = (int)(metrics.heightPixels * CARD_HEIGHT_NORMAL);
        }
        
        FrameLayout.LayoutParams params = (FrameLayout.LayoutParams)cardContainer.getLayoutParams();
        if (params.height != targetHeight) {
            animateCardHeight(targetHeight, 450);
        }
        
        cardContainer.animate()
            .translationY(0)
            .alpha(1f)
            .scaleX(1f)
            .scaleY(1f)
            .setDuration(450)
            .setInterpolator(new SpringInterpolator())
            .start();
    }

    private void addWebView() {
        if (url == null || url.isEmpty() || cardContainer == null) {
            return;
        }
        
        webView = new WebView(this);
        StashWebViewUtils.configureWebViewSettings(webView, StashWebViewUtils.isDarkTheme(this));
        
        webView.setWebViewClient(new WebViewClient() {
            @Override
            public void onPageStarted(WebView view, String url, android.graphics.Bitmap favicon) {
                super.onPageStarted(view, url, favicon);
                showLoading();
                injectSDK(view);
                checkProvider(url);
                checkGooglePayRedirect(url);
            }
            
            @Override
            public void onPageFinished(WebView view, String url) {
                super.onPageFinished(view, url);
                hideLoading();
                injectSDK(view);
                checkProvider(url);
                checkGooglePayRedirect(url);
            }
            
            @Override
            public void onReceivedError(WebView view, android.webkit.WebResourceRequest request, 
                                        android.webkit.WebResourceError error) {
                super.onReceivedError(view, request, error);
                Log.e(TAG, "WebView error: " + error.getDescription());
            }
        });
        
        webView.setWebChromeClient(new WebChromeClient());
        webView.addJavascriptInterface(new JSInterface(), "StashAndroid");
        webView.setBackgroundColor(StashWebViewUtils.isDarkTheme(this) ? Color.parseColor(StashWebViewUtils.COLOR_DARK_BG) : Color.WHITE);
        
        FrameLayout.LayoutParams params = new FrameLayout.LayoutParams(
            FrameLayout.LayoutParams.MATCH_PARENT, FrameLayout.LayoutParams.MATCH_PARENT);
        webView.setLayoutParams(params);
        cardContainer.addView(webView);
        // Append theme query parameter before loading
        String urlWithTheme = StashWebViewUtils.appendThemeQueryParameter(url, StashWebViewUtils.isDarkTheme(this));
        webView.loadUrl(urlWithTheme);
    }
    
    private void addHomeButton() {
        homeButton = new Button(this);
        homeButton.setText("⌂");
        homeButton.setTextSize(18);
        homeButton.setTextColor(Color.parseColor(COLOR_HOME_TEXT));
        homeButton.setGravity(Gravity.CENTER);
        homeButton.setPadding(0, 0, 0, 0);
        
        GradientDrawable bg = new GradientDrawable();
        bg.setColor(StashWebViewUtils.isDarkTheme(this) ? Color.parseColor("#2C2C2E") : Color.parseColor(COLOR_LIGHT_BG));
        bg.setCornerRadius(StashWebViewUtils.dpToPx(this, 20));
        bg.setStroke(StashWebViewUtils.dpToPx(this, 1), StashWebViewUtils.isDarkTheme(this) ? Color.parseColor(COLOR_DARK_STROKE) : Color.parseColor(COLOR_LIGHT_STROKE));
        homeButton.setBackground(bg);
        
        if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.LOLLIPOP) {
            homeButton.setElevation(StashWebViewUtils.dpToPx(this, 6));
        }
        
        FrameLayout.LayoutParams params = new FrameLayout.LayoutParams(StashWebViewUtils.dpToPx(this, 36), StashWebViewUtils.dpToPx(this, 36));
        params.gravity = Gravity.TOP | Gravity.START;
        params.setMargins(StashWebViewUtils.dpToPx(this, 12), StashWebViewUtils.dpToPx(this, 12), 0, 0);
        homeButton.setLayoutParams(params);
        homeButton.setVisibility(View.GONE);
        homeButton.setOnClickListener(v -> {
            if (initialURL != null && webView != null) {
                // Append theme query parameter before loading
                String urlWithTheme = StashWebViewUtils.appendThemeQueryParameter(initialURL, StashWebViewUtils.isDarkTheme(this));
                webView.loadUrl(urlWithTheme);
            }
        });
        
        cardContainer.addView(homeButton);
    }
    
    private void injectSDK(WebView view) {
        view.evaluateJavascript(StashWebViewUtils.JS_SDK_SCRIPT, null);
    }
    
    private void checkProvider(String url) {
        if (homeButton == null || url == null) return;
        String lower = url.toLowerCase();
        boolean show = lower.contains("klarna") || lower.contains("paypal") || lower.contains("stripe");
        runOnUiThread(() -> homeButton.setVisibility(show ? View.VISIBLE : View.GONE));
    }
    
    private void checkGooglePayRedirect(String url) {
        if (url == null || googlePayRedirectHandled || initialURL == null || initialURL.isEmpty()) {
            return;
        }
        
        String lower = url.toLowerCase();
        if (lower.contains("pay.google.com")) {
            googlePayRedirectHandled = true;
            openInSystemBrowser(initialURL);
        }
    }
    
    private void openInSystemBrowser(String url) {
        try {
            // Add dpm=gpay query parameter
            String urlWithParam = url;
            if (url != null && !url.isEmpty()) {
                Uri uri = Uri.parse(url);
                String existingQuery = uri.getQuery();
                if (existingQuery != null && !existingQuery.isEmpty()) {
                    urlWithParam = url + "&dpm=gpay";
                } else {
                    urlWithParam = url + "?dpm=gpay";
                }
            }
            
            Intent browserIntent = new Intent(Intent.ACTION_VIEW, Uri.parse(urlWithParam));
            browserIntent.addFlags(Intent.FLAG_ACTIVITY_NEW_TASK);
            startActivity(browserIntent);
            // Dismiss the card after opening browser
            dismissWithAnimation();
        } catch (Exception e) {
            Log.e(TAG, "Failed to open URL in system browser: " + e.getMessage());
        }
    }
    
    private void showLoading() {
        runOnUiThread(() -> {
            // Remove existing if any
                if (loadingIndicator != null && loadingIndicator.getParent() != null) {
                    ((ViewGroup)loadingIndicator.getParent()).removeView(loadingIndicator);
                }
                
                if (cardContainer != null) {
                loadingIndicator = StashWebViewUtils.createAndShowLoading(getApplicationContext(), cardContainer);
                // Ensure visibility if utils didn't set it (it does, but safe check or if we need to force layout)
                        if (loadingIndicator != null) {
                            loadingIndicator.setVisibility(View.VISIBLE);
                            loadingIndicator.requestLayout();
                        }
            }
        });
    }
    
    private void hideLoading() {
        runOnUiThread(() -> {
            StashWebViewUtils.hideLoading(loadingIndicator);
                        loadingIndicator = null;
        });
    }
    
    private void animateSlideUp() {
        DisplayMetrics metrics = getResources().getDisplayMetrics();
        cardContainer.setTranslationY(metrics.heightPixels);
        
        cardContainer.post(() -> {
            cardContainer.animate()
                .translationY(0)
                .setDuration(300)
                .setInterpolator(new android.view.animation.AccelerateDecelerateInterpolator())
                .start();
        });
    }
    
    private void animateFadeIn() {
        cardContainer.setAlpha(0f);
        cardContainer.setScaleX(0.9f);
        cardContainer.setScaleY(0.9f);
        cardContainer.animate()
            .alpha(1f)
            .scaleX(1f)
            .scaleY(1f)
            .setDuration(200)
            .setInterpolator(new android.view.animation.AccelerateDecelerateInterpolator())
            .start();
    }
    
    private void dismissWithAnimation() {
        if (isDismissing) return;
        isDismissing = true;
        
        setRequestedOrientation(ActivityInfo.SCREEN_ORIENTATION_LOCKED);
        
        Window window = getWindow();
        if (window != null) {
            window.clearFlags(WindowManager.LayoutParams.FLAG_DIM_BEHIND);
            WindowManager.LayoutParams params = window.getAttributes();
            params.dimAmount = 0f;
            window.setAttributes(params);
        }
        
        if (usePopup) {
            cardContainer.animate()
                .alpha(0f)
                .scaleX(0.9f)
                .scaleY(0.9f)
                .setDuration(250)
                .setInterpolator(new SpringInterpolator())
                .withEndAction(this::finishActivityWithNoAnimation)
                .start();
        } else {
            cardContainer.animate()
                .translationY(cardContainer.getHeight())
                .setDuration(400)
                .setInterpolator(new SpringInterpolator())
                .withEndAction(this::finishActivityWithNoAnimation)
                .start();
        }
    }
    
    private void finishActivityWithNoAnimation() {
        if (rootLayout != null) {
            rootLayout.setVisibility(View.INVISIBLE);
        }
        
        Window window = getWindow();
        if (window != null) {
            window.setBackgroundDrawable(new ColorDrawable(Color.TRANSPARENT));
            window.clearFlags(WindowManager.LayoutParams.FLAG_DIM_BEHIND);
            window.clearFlags(WindowManager.LayoutParams.FLAG_TRANSLUCENT_NAVIGATION);
            window.clearFlags(WindowManager.LayoutParams.FLAG_TRANSLUCENT_STATUS);
        }
        
        overridePendingTransition(0, 0);
        finish();
    }
    
    private void notifyUnityAndDismiss(String messageName, String messageBody, boolean success) {
        runOnUiThread(() -> {
            if (success) {
                // If success or failure (final state), we mark as sent so we don't fire dismiss callback in onDestroy
                callbackSent = true;
                isPurchaseProcessing = false;
            }
            StashUnityBridge.sendMessage(messageName, messageBody);
            dismissWithAnimation();
        });
    }

    private class JSInterface {
        @JavascriptInterface
        public void onPaymentSuccess() {
            // Mark processing as false (success)
            notifyUnityAndDismiss(StashUnityBridge.MSG_ON_PAYMENT_SUCCESS, "", true);
        }
        
        @JavascriptInterface
        public void onPaymentFailure() {
            // Mark processing as false (failure is final)
            notifyUnityAndDismiss(StashUnityBridge.MSG_ON_PAYMENT_FAILURE, "", true);
        }
        
        @JavascriptInterface
        public void onPurchaseProcessing() {
            runOnUiThread(() -> {
                isPurchaseProcessing = true;
            });
        }
        
        @JavascriptInterface
        public void setPaymentChannel(String optinType) {
            notifyUnityAndDismiss(StashUnityBridge.MSG_ON_OPTIN_RESPONSE, optinType != null ? optinType : "", false);
        }
        
        @JavascriptInterface
        public void expand() {
            runOnUiThread(() -> {
                if (!usePopup && !isExpanded) {
                    animateExpand();
                }
            });
        }
        
        @JavascriptInterface
        public void collapse() {
            runOnUiThread(() -> {
                if (!usePopup && isExpanded) {
                    animateCollapse();
                }
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
        
        if (webView != null) {
            webView.destroy();
            webView = null;
        }
        
        if (!callbackSent) {
            callbackSent = true;
            StashUnityBridge.sendDialogDismissed();
        }
    }
    
    @Override
    public void onBackPressed() {
        // Prevent dismissal when purchase is processing
        if (isPurchaseProcessing) {
            return; // Don't allow back button to dismiss when purchase is processing
        }
        dismissWithAnimation();
    }
    
    @Override
    public void onConfigurationChanged(Configuration newConfig) {
        super.onConfigurationChanged(newConfig);
        
        if (!usePopup && cardContainer != null && rootLayout != null) {
            boolean isTablet = StashWebViewUtils.isTablet(this);
            if (isTablet) {
                rootLayout.removeAllViews();
                createUI();
            } else {
                // For phones: if launched from landscape, always ensure card is expanded after rotation
                if (wasLandscapeBeforePortrait) {
                    // Always expand the card after rotation if it was launched from landscape
                    if (!isExpanded) {
                        // Expand the card to match the expanded state it should have
                        animateExpand();
                    } else {
                        // Card is already expanded, but update dimensions to match new screen size
                        DisplayMetrics metrics = getResources().getDisplayMetrics();
                        FrameLayout.LayoutParams params = (FrameLayout.LayoutParams) cardContainer.getLayoutParams();
                        int expandedHeight = (int)(metrics.heightPixels * CARD_HEIGHT_EXPANDED);
                        params.height = expandedHeight;
                        cardContainer.setLayoutParams(params);
                    }
                }
            }
        }
    }
    
}
