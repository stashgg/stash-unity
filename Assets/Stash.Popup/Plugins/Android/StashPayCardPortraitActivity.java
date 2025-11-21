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
        
        boolean isTablet = isTablet();
        
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
        boolean isTablet = isTablet();
        
        if (wasLandscapeBeforePortrait && !isTablet && !usePopup) {
            rootLayout.setBackgroundColor(Color.BLACK);
        } else {
            rootLayout.setBackgroundColor(Color.parseColor("#20000000"));
        }
        
        if (usePopup) {
            createPopup();
        } else {
            createCard();
        }
        
        if (!usePopup) {
            rootLayout.setOnClickListener(v -> {
                if (!isDismissing && v == rootLayout) {
                    dismissWithAnimation();
                }
            });
        }
        cardContainer.setOnClickListener(v -> {});
        
        setContentView(rootLayout);
    }
    
    private void createCard() {
        DisplayMetrics metrics = getResources().getDisplayMetrics();
        boolean isTablet = isTablet();
        
        float effectiveHeightRatio;
        if (wasLandscapeBeforePortrait && !isTablet) {
            effectiveHeightRatio = CARD_HEIGHT_EXPANDED;
            isExpanded = true;
        } else {
            effectiveHeightRatio = CARD_HEIGHT_NORMAL;
            isExpanded = false;
        }
        
        int cardHeight = (int)(metrics.heightPixels * effectiveHeightRatio);
        int cardWidth = isTablet ? Math.min(dpToPx(600), (int)(metrics.widthPixels * 0.7f)) 
                                  : FrameLayout.LayoutParams.MATCH_PARENT;
        
        cardContainer = new FrameLayout(this);
        FrameLayout.LayoutParams params = new FrameLayout.LayoutParams(cardWidth, cardHeight);
        params.gravity = isTablet ? Gravity.CENTER : (Gravity.BOTTOM | Gravity.CENTER_HORIZONTAL);
        cardContainer.setLayoutParams(params);
        
        GradientDrawable bg = new GradientDrawable();
        bg.setColor(isDarkTheme() ? Color.parseColor("#1C1C1E") : Color.WHITE);
        float radius = dpToPx(12);
        
        if (isTablet) {
            bg.setCornerRadius(radius);
        } else {
            bg.setCornerRadii(new float[]{radius, radius, radius, radius, 0, 0, 0, 0});
        }
        cardContainer.setBackground(bg);
        
        if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.LOLLIPOP) {
            cardContainer.setElevation(dpToPx(24));
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
        
        cardContainer = new FrameLayout(this);
        FrameLayout.LayoutParams params = new FrameLayout.LayoutParams(size, size);
        params.gravity = Gravity.CENTER;
        cardContainer.setLayoutParams(params);
        
        GradientDrawable bg = new GradientDrawable();
        bg.setColor(isDarkTheme() ? Color.parseColor("#1C1C1E") : Color.WHITE);
        float radius = dpToPx(12);
        bg.setCornerRadius(radius);
        cardContainer.setBackground(bg);
        
        if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.LOLLIPOP) {
            cardContainer.setElevation(dpToPx(24));
            cardContainer.setOutlineProvider(new ViewOutlineProvider() {
                @Override
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
        
        addDragTouchHandling(dragArea);
        cardContainer.addView(dragArea);
    }
    
    private void addDragTouchHandling(View dragArea) {
        dragArea.setOnTouchListener(new View.OnTouchListener() {
            private float initialY;
            private float initialTranslationY;
            private boolean isDragging;
            
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
                                float newTranslationY = initialTranslationY + deltaY;
                                cardContainer.setTranslationY(newTranslationY);
                                DisplayMetrics metrics = getResources().getDisplayMetrics();
                                float progress = Math.min(deltaY / metrics.heightPixels, 1.0f);
                                cardContainer.setAlpha(1.0f - (progress * 0.5f));
                            } else if (deltaY < 0 && !isTablet && !isExpanded && !wasLandscapeBeforePortrait) {
                                DisplayMetrics metrics = getResources().getDisplayMetrics();
                                float dragProgress = Math.min(Math.abs(deltaY) / dpToPx(100), 1.0f);
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
                                if (Math.abs(finalDeltaY) > dpToPx(80)) {
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
        });
    }
    
    private void animateDismiss() {
        if (cardContainer == null) return;
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
    
    private void animateExpand() {
        if (cardContainer == null) return;
        DisplayMetrics metrics = getResources().getDisplayMetrics();
        int expandedHeight = (int)(metrics.heightPixels * CARD_HEIGHT_EXPANDED);
        
        FrameLayout.LayoutParams params = (FrameLayout.LayoutParams)cardContainer.getLayoutParams();
        android.animation.ValueAnimator animator = android.animation.ValueAnimator.ofInt(params.height, expandedHeight);
        animator.setDuration(450);
        animator.setInterpolator(new SpringInterpolator());
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
            .setDuration(450)
            .setInterpolator(new SpringInterpolator())
            .start();
        
        isExpanded = true;
    }
    
    private void animateSnapBack() {
        if (cardContainer == null) return;
        DisplayMetrics metrics = getResources().getDisplayMetrics();
        boolean isTablet = isTablet();
        
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
            android.animation.ValueAnimator animator = android.animation.ValueAnimator.ofInt(params.height, targetHeight);
            animator.setDuration(450);
            animator.setInterpolator(new SpringInterpolator());
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
            .setDuration(450)
            .setInterpolator(new SpringInterpolator())
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
        webView.setBackgroundColor(isDarkTheme() ? Color.parseColor("#1C1C1E") : Color.WHITE);
        
        FrameLayout.LayoutParams params = new FrameLayout.LayoutParams(
            FrameLayout.LayoutParams.MATCH_PARENT, FrameLayout.LayoutParams.MATCH_PARENT);
        webView.setLayoutParams(params);
        cardContainer.addView(webView);
        webView.loadUrl(url);
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
    
    private void checkGooglePayRedirect(String url) {
        if (url == null || googlePayRedirectHandled || initialURL == null || initialURL.isEmpty()) {
            return;
        }
        
        String lower = url.toLowerCase();
        if (lower.contains("pay.google.com")) {
            googlePayRedirectHandled = true;
            Log.d(TAG, "Google Pay detected, opening initial URL in system browser: " + initialURL);
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
            try {
                if (loadingIndicator != null && loadingIndicator.getParent() != null) {
                    ((ViewGroup)loadingIndicator.getParent()).removeView(loadingIndicator);
                }
                
                loadingIndicator = new ProgressBar(getApplicationContext());
                
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
    
    private class JSInterface {
        @JavascriptInterface
        public void onPaymentSuccess() {
            runOnUiThread(() -> {
                UnityPlayer.UnitySendMessage("StashPayCard", "OnAndroidPaymentSuccess", "");
                dismissWithAnimation();
            });
        }
        
        @JavascriptInterface
        public void onPaymentFailure() {
            runOnUiThread(() -> {
                UnityPlayer.UnitySendMessage("StashPayCard", "OnAndroidPaymentFailure", "");
                dismissWithAnimation();
            });
        }
        
        @JavascriptInterface
        public void onPurchaseProcessing() {}
        
        @JavascriptInterface
        public void setPaymentChannel(String optinType) {
            runOnUiThread(() -> {
                UnityPlayer.UnitySendMessage("StashPayCard", "OnAndroidOptinResponse", 
                    optinType != null ? optinType : "");
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
        
        if (webView != null) {
            webView.destroy();
            webView = null;
        }
        
        if (!callbackSent) {
            callbackSent = true;
            UnityPlayer.UnitySendMessage("StashPayCard", "OnAndroidDialogDismissed", "");
        }
    }
    
    @Override
    public void onBackPressed() {
        dismissWithAnimation();
    }
    
    @Override
    public void onConfigurationChanged(Configuration newConfig) {
        super.onConfigurationChanged(newConfig);
        
        if (!usePopup && cardContainer != null && rootLayout != null) {
            boolean isTablet = isTablet();
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
    
    private boolean isTablet() {
        DisplayMetrics metrics = getResources().getDisplayMetrics();
        int smallerDimension = Math.min(metrics.widthPixels, metrics.heightPixels);
        float smallerDp = smallerDimension / metrics.density;
        
        boolean isTabletBySize = smallerDp >= 600;
        
        boolean isTabletByConfig = false;
        if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.HONEYCOMB_MR2) {
            int screenSize = getResources().getConfiguration().screenLayout & Configuration.SCREENLAYOUT_SIZE_MASK;
            isTabletByConfig = (screenSize == Configuration.SCREENLAYOUT_SIZE_LARGE || 
                               screenSize == Configuration.SCREENLAYOUT_SIZE_XLARGE);
        }
        
        float aspectRatio = (float)Math.max(metrics.widthPixels, metrics.heightPixels) / 
                           Math.min(metrics.widthPixels, metrics.heightPixels);
        boolean isTabletByAspect = aspectRatio < 2.0f && smallerDp >= 500;
        
        return isTabletBySize || isTabletByConfig || isTabletByAspect;
    }
    
    private int dpToPx(int dp) {
        return Math.round(dp * getResources().getDisplayMetrics().density);
    }
}
