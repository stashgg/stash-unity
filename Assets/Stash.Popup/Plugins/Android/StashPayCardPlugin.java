package com.stash.popup;

import android.app.Activity;
import android.app.Dialog;
import android.content.Context;
import android.content.Intent;
import android.content.res.Configuration;
import android.graphics.*;
import android.graphics.drawable.*;
import android.net.Uri;
import android.os.*;
import android.util.*;
import android.view.*;
import android.webkit.*;
import android.widget.*;
import com.unity3d.player.UnityPlayer;

/**
 * Android implementation of StashPayCard
 * 
 * Architecture:
 * - Cards on phones: Use portrait-locked Activity for proper keyboard orientation
 * - Cards on tablets: Use custom Dialog (tablets handle landscape better)
 * - Popups: Use centered Dialog in current orientation
 * - Fallback: Chrome Custom Tabs for forceSafariViewController mode
 */
public class StashPayCardPlugin {
    private static final String TAG = "StashPayCard";
    private static StashPayCardPlugin instance;
    
    // ============================================================================
    // MARK: - Fields
    // ============================================================================
    
    // Core references
    private Activity activity;
    private Dialog currentDialog;
    private WebView webView;
    private FrameLayout currentContainer;
    private Button currentHomeButton;
    private ProgressBar loadingIndicator;
    
    // Configuration
    private String initialURL;
    private float cardHeightRatio = 0.6f;
    private float cardVerticalPosition = 1.0f;
    private float cardWidthRatio = 1.0f;
    
    // State flags
    private boolean isCurrentlyPresented = false;
    private boolean paymentSuccessHandled = false;
    private boolean isPurchaseProcessing = false;
    private boolean usePopupPresentation = false;
    private boolean forceSafariViewController = false;
    private boolean isExpanded = false;
    
    // Constants
    private String unityGameObjectName = "StashPayCard";
    
    // ============================================================================
    // MARK: - JavaScript Interface
    // ============================================================================
    
    private class StashJavaScriptInterface {
        @JavascriptInterface
        public void onPaymentSuccess() {
            if (paymentSuccessHandled) return;
            paymentSuccessHandled = true;
            isPurchaseProcessing = false;

            new Handler(Looper.getMainLooper()).post(() -> {
                try {
                    UnityPlayer.UnitySendMessage(unityGameObjectName, "OnAndroidPaymentSuccess", "");
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
                    UnityPlayer.UnitySendMessage(unityGameObjectName, "OnAndroidPaymentFailure", "");
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
                    UnityPlayer.UnitySendMessage(unityGameObjectName, "OnAndroidOptinResponse", 
                        optinType != null ? optinType : "");
                    dismissCurrentDialog();
                } catch (Exception e) {
                    Log.e(TAG, "Error handling payment channel: " + e.getMessage());
                }
                });
            }
        }
    
    // ============================================================================
    // MARK: - Singleton & Initialization
    // ============================================================================
    
    public static StashPayCardPlugin getInstance() {
        if (instance == null) {
            instance = new StashPayCardPlugin();
        }
        return instance;
    }
    
    private StashPayCardPlugin() {
        this.activity = UnityPlayer.currentActivity;
    }
    
    // ============================================================================
    // MARK: - Public API
    // ============================================================================

    /**
     * Opens URL in card presentation (slides up from bottom in portrait)
     */
    public void openURL(String url) {
        usePopupPresentation = false;
        openURLInternal(url);
    }
    
    /**
     * Opens URL in centered square popup with fade animation
     */
    public void openPopup(String url) {
        usePopupPresentation = true;
        openURLInternal(url);
    }
    
    /**
     * Dismisses any currently presented dialog
     */
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
    
    /**
     * Resets presentation state
     */
    public void resetPresentationState() {
        dismissDialog();
        paymentSuccessHandled = false;
        isCurrentlyPresented = false;
    }
    
    /**
     * Checks if a card/popup is currently presented
     */
    public boolean isCurrentlyPresented() {
        return isCurrentlyPresented;
    }
    
    /**
     * Sets card configuration (height, vertical position, width ratios)
     */
    public void setCardConfiguration(float heightRatio, float verticalPosition, float widthRatio) {
        this.cardHeightRatio = heightRatio;
        this.cardVerticalPosition = verticalPosition;
        this.cardWidthRatio = widthRatio;
    }
    
    /**
     * Forces Chrome Custom Tabs instead of card UI
     */
    public void setForceSafariViewController(boolean force) {
        this.forceSafariViewController = force;
    }
    
    public boolean getForceSafariViewController() {
        return forceSafariViewController;
    }
    
    // ============================================================================
    // MARK: - URL Opening Logic
    // ============================================================================
    
    private void openURLInternal(String url) {
        if (activity == null) {
            Log.e(TAG, "Activity is null, cannot open URL");
            return;
        }

        if (url == null || url.isEmpty()) {
            Log.e(TAG, "URL is null or empty");
            return;
        }

        // Ensure URL has protocol
        if (!url.startsWith("http://") && !url.startsWith("https://")) {
            url = "https://" + url;
        }

        final String finalUrl = url;

        activity.runOnUiThread(() -> {
            if (forceSafariViewController) {
                openWithChromeCustomTabs(finalUrl);
            } else if (shouldUsePortraitActivity()) {
                // Use portrait Activity for cards on phones (better keyboard handling)
                launchPortraitActivity(finalUrl);
            } else if (usePopupPresentation) {
                // Centered square popup
                createAndShowPopupDialog(finalUrl);
                } else {
                // Fallback: custom card dialog for tablets or as fallback
                    createAndShowCardStyleDialog(finalUrl);
            }
        });
    }
    
    private boolean shouldUsePortraitActivity() {
        // Use portrait Activity for all card presentations (phones and tablets, not popups)
        if (usePopupPresentation) {
            return false;
        }
        return true; // Use Activity for all cards (phones and tablets)
    }
    
    private void launchPortraitActivity(String url) {
        try {
            int currentOrientation = activity.getResources().getConfiguration().orientation;
            boolean isCurrentlyLandscape = currentOrientation == Configuration.ORIENTATION_LANDSCAPE;
            
            Intent intent = new Intent();
            intent.setClassName(activity, "com.stash.popup.StashPayCardPortraitActivity");
            intent.putExtra("url", url);
            intent.putExtra("initialURL", url);
            intent.putExtra("cardHeightRatio", cardHeightRatio);
            intent.putExtra("usePopup", usePopupPresentation);
            intent.putExtra("wasLandscape", isCurrentlyLandscape);
            intent.addFlags(Intent.FLAG_ACTIVITY_NO_ANIMATION | Intent.FLAG_ACTIVITY_REORDER_TO_FRONT);
            
            activity.startActivity(intent);
            isCurrentlyPresented = true;
        } catch (Exception e) {
            Log.e(TAG, "Portrait Activity failed: " + e.getMessage());
            createAndShowCardStyleDialog(url);
        }
    }
    
    // ============================================================================
    // MARK: - Popup Dialog (Centered Square)
    // ============================================================================
    
    private void createAndShowPopupDialog(String url) {
        cleanupAllViews();
        initialURL = url;
        paymentSuccessHandled = false;

        try {
            currentDialog = new Dialog(activity, android.R.style.Theme_Translucent_NoTitleBar_Fullscreen);
            currentDialog.requestWindowFeature(Window.FEATURE_NO_TITLE);

            FrameLayout mainFrame = new FrameLayout(activity);
            mainFrame.setBackgroundColor(Color.parseColor("#20000000"));
            
            // Calculate square popup size
            DisplayMetrics metrics = activity.getResources().getDisplayMetrics();
            int smallerDimension = Math.min(metrics.widthPixels, metrics.heightPixels);
            boolean isTablet = smallerDimension > 600;
            int minSize = isTablet ? dpToPx(400) : dpToPx(300);
            int maxSize = isTablet ? dpToPx(500) : dpToPx(500);
            float percentage = isTablet ? 0.5f : 0.75f;
            int squareSize = Math.max(minSize, Math.min(maxSize, (int)(smallerDimension * percentage)));
            
            // Create square container
            currentContainer = new FrameLayout(activity);
            FrameLayout.LayoutParams containerParams = new FrameLayout.LayoutParams(squareSize, squareSize);
            containerParams.gravity = Gravity.CENTER;
            currentContainer.setLayoutParams(containerParams);
            
            GradientDrawable popupBg = new GradientDrawable();
            popupBg.setColor(getThemeBackgroundColor());
            popupBg.setCornerRadius(dpToPx(20));
            currentContainer.setBackground(popupBg);
            
            if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.LOLLIPOP) {
                currentContainer.setElevation(dpToPx(24));
            }
            
            // Create WebView
            webView = new WebView(activity);
            setupPopupWebView(webView, url);
            currentContainer.addView(webView);
            
            mainFrame.addView(currentContainer);
            currentDialog.setContentView(mainFrame);
            
            // Configure window
            Window window = currentDialog.getWindow();
            if (window != null) {
                window.setLayout(ViewGroup.LayoutParams.MATCH_PARENT, ViewGroup.LayoutParams.MATCH_PARENT);
                window.setFlags(WindowManager.LayoutParams.FLAG_HARDWARE_ACCELERATED,
                               WindowManager.LayoutParams.FLAG_HARDWARE_ACCELERATED);
                window.setBackgroundDrawableResource(android.R.color.transparent);
                window.addFlags(WindowManager.LayoutParams.FLAG_DIM_BEHIND);
                WindowManager.LayoutParams params = window.getAttributes();
                params.dimAmount = 0.3f;
                window.setAttributes(params);
            }
            
            // Popup is modal - no tap-outside-to-dismiss
            // Consume clicks on popup container to prevent them from bubbling
            currentContainer.setOnClickListener(v -> {});

        // Set dismiss listener
        currentDialog.setOnDismissListener(dialog -> {
                if (!paymentSuccessHandled) {
                    UnityPlayer.UnitySendMessage("StashPayCard", "OnAndroidDialogDismissed", "");
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
                .setInterpolator(new android.view.animation.DecelerateInterpolator(1.2f))
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
                    .setDuration(150)
                    .withEndAction(() -> {
                        if (currentDialog != null) currentDialog.dismiss();
                    })
                    .start();
            } else {
                currentDialog.dismiss();
            }
        }
    }
    
    // ============================================================================
    // MARK: - Card Dialog (Bottom Sheet Style - Fallback for Tablets)
    // ============================================================================
    
    private void createAndShowCardStyleDialog(String url) {
        cleanupAllViews();
        initialURL = url;
        paymentSuccessHandled = false;

        try {
            currentDialog = new Dialog(activity, android.R.style.Theme_Translucent_NoTitleBar_Fullscreen);
            currentDialog.requestWindowFeature(Window.FEATURE_NO_TITLE);

            FrameLayout mainFrame = new FrameLayout(activity);
            mainFrame.setBackgroundColor(Color.parseColor("#20000000"));
            
            // Calculate card size
            DisplayMetrics metrics = activity.getResources().getDisplayMetrics();
            int screenHeight = metrics.heightPixels;
            int cardHeight = (int) (screenHeight * 0.68f);
            
            // Create card container
            currentContainer = createCardContainer(cardHeight);
            
            // Add drag handle
            LinearLayout dragHandle = createDragHandleArea();
            currentContainer.addView(dragHandle);
            
            // Add WebView
            webView = new WebView(activity);
            setupCardWebView(webView, url);
            currentContainer.addView(webView);

            // Add home button
            currentHomeButton = new Button(activity);
            setupHomeButton(currentHomeButton);
            currentHomeButton.setVisibility(View.GONE);
            currentContainer.addView(currentHomeButton);

            mainFrame.addView(currentContainer);
            currentDialog.setContentView(mainFrame);

            // Configure window
            Window window = currentDialog.getWindow();
            if (window != null) {
                window.setLayout(ViewGroup.LayoutParams.MATCH_PARENT, ViewGroup.LayoutParams.MATCH_PARENT);
                window.setFlags(WindowManager.LayoutParams.FLAG_HARDWARE_ACCELERATED,
                               WindowManager.LayoutParams.FLAG_HARDWARE_ACCELERATED);
                window.setBackgroundDrawableResource(android.R.color.transparent);
                window.addFlags(WindowManager.LayoutParams.FLAG_DIM_BEHIND);
                WindowManager.LayoutParams params = window.getAttributes();
                params.dimAmount = 0.3f;
                window.setAttributes(params);
            }

            // Dismiss on overlay tap
            mainFrame.setOnClickListener(v -> dismissCustomCardDialog());
            currentContainer.setOnClickListener(v -> {});

            // Set dismiss listener
            currentDialog.setOnDismissListener(dialog -> {
                if (!paymentSuccessHandled) {
                    UnityPlayer.UnitySendMessage("StashPayCard", "OnAndroidDialogDismissed", "");
                }
                cleanupAllViews();
                isCurrentlyPresented = false;
            });

            currentDialog.show();
            animateSlideUp();
            isCurrentlyPresented = true;
        } catch (Exception e) {
            Log.e(TAG, "Error creating card dialog: " + e.getMessage());
        }
    }
    
    private FrameLayout createCardContainer(int cardHeight) {
        FrameLayout container = new FrameLayout(activity);
        FrameLayout.LayoutParams params = new FrameLayout.LayoutParams(
            FrameLayout.LayoutParams.MATCH_PARENT, cardHeight);
        params.gravity = Gravity.BOTTOM;
        container.setLayoutParams(params);
        
        // Card background with top-only rounded corners
        GradientDrawable bg = new GradientDrawable();
        bg.setColor(getThemeBackgroundColor());
        float radius = dpToPx(25);
        bg.setCornerRadii(new float[]{
            radius, radius, // top-left
            radius, radius, // top-right
            0, 0,           // bottom-right (square)
            0, 0            // bottom-left (square)
        });
        container.setBackground(bg);
        
        // Elevation and clipping
        if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.LOLLIPOP) {
            container.setElevation(dpToPx(24));
            container.setOutlineProvider(new ViewOutlineProvider() {
                @Override
                public void getOutline(View view, Outline outline) {
                    // Extend bottom to create square bottom corners
                    outline.setRoundRect(0, 0, view.getWidth(), view.getHeight() + dpToPx(25), dpToPx(25));
                }
            });
            container.setClipToOutline(true);
        }
        
        container.setClipChildren(true);
        container.setClipToPadding(true);
        
        return container;
    }
    
    private LinearLayout createDragHandleArea() {
        LinearLayout dragArea = new LinearLayout(activity);
        dragArea.setOrientation(LinearLayout.VERTICAL);
        dragArea.setGravity(Gravity.CENTER_HORIZONTAL);
        // Larger padding for easier touch interaction - increased from 8dp to 16dp vertical
        dragArea.setPadding(dpToPx(20), dpToPx(16), dpToPx(20), dpToPx(16));
        dragArea.setBackgroundColor(Color.TRANSPARENT);
        
        // Create drag handle indicator
        View handle = new View(activity);
        GradientDrawable handleDrawable = new GradientDrawable();
        handleDrawable.setColor(Color.parseColor("#D1D1D6"));
        handleDrawable.setCornerRadius(dpToPx(2));
        // Slightly wider handle for better visibility
        handle.setLayoutParams(new LinearLayout.LayoutParams(dpToPx(36), dpToPx(5)));
        dragArea.addView(handle);
        
        // Make drag area wider for easier interaction
        FrameLayout.LayoutParams params = new FrameLayout.LayoutParams(
            dpToPx(120), // Wider drag area - was WRAP_CONTENT
            FrameLayout.LayoutParams.WRAP_CONTENT);
        params.gravity = Gravity.TOP | Gravity.CENTER_HORIZONTAL;
        dragArea.setLayoutParams(params);
        
        if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.LOLLIPOP) {
            dragArea.setElevation(dpToPx(8));
        }
        
        // Add drag-to-dismiss touch handling
        addDragToDismissTouch(dragArea);
        
        return dragArea;
    }
    
    private void addDragToDismissTouch(View dragArea) {
        dragArea.setOnTouchListener(new View.OnTouchListener() {
            private float initialY = 0;
            private float initialTranslationY = 0;
            private boolean isDragging = false;
            
            @Override
            public boolean onTouch(View v, MotionEvent event) {
                if (currentContainer == null) return false;

                switch (event.getAction()) {
                    case MotionEvent.ACTION_DOWN:
                        initialY = event.getRawY();
                        initialTranslationY = currentContainer.getTranslationY();
                        isDragging = false;
                        return true;

                    case MotionEvent.ACTION_MOVE:
                        float deltaY = event.getRawY() - initialY;
                        if (Math.abs(deltaY) > dpToPx(10)) {
                            isDragging = true;
                            if (deltaY > 0) {
                                // Downward drag - dismiss behavior (follow finger exactly)
                                float newTranslationY = initialTranslationY + deltaY;
                                currentContainer.setTranslationY(newTranslationY);
                                float progress = Math.min(deltaY / dpToPx(200), 1.0f);
                                currentContainer.setAlpha(1.0f - (progress * 0.3f));
                            } else if (deltaY < 0 && !isExpanded) {
                                // Upward drag - expand behavior
                                DisplayMetrics metrics = activity.getResources().getDisplayMetrics();
                                int screenHeight = metrics.heightPixels;
                                int baseHeight = (int)(screenHeight * 0.68f);
                                int maxHeight = (int)(screenHeight * 0.88f);
                                
                                // Make height follow finger drag directly (balanced multiplier)
                                int heightIncrease = (int)(Math.abs(deltaY) * 0.75f); // 75% tracking for natural feel
                                int newHeight = Math.min(baseHeight + heightIncrease, maxHeight);
                                
                                FrameLayout.LayoutParams cardParams = 
                                    (FrameLayout.LayoutParams)currentContainer.getLayoutParams();
                                cardParams.height = newHeight;
                                currentContainer.setLayoutParams(cardParams);
                            }
                        }
                        return true;

                    case MotionEvent.ACTION_UP:
                    case MotionEvent.ACTION_CANCEL:
                        if (isDragging) {
                            float finalDeltaY = event.getRawY() - initialY;
                            DisplayMetrics metrics = activity.getResources().getDisplayMetrics();

                            if (finalDeltaY > 0) {
                                int dismissThreshold = (int)(metrics.heightPixels * 0.25f);
                                if (finalDeltaY > dismissThreshold) {
                                    animateDismissCard();
                                } else {
                                    animateSnapBack();
                                }
                            } else if (finalDeltaY < 0 && !isExpanded) {
                                if (Math.abs(finalDeltaY) > dpToPx(80)) {
                                    animateExpandCard();
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

    private void animateSlideUp() {
        if (currentContainer != null && Build.VERSION.SDK_INT >= Build.VERSION_CODES.HONEYCOMB) {
            currentContainer.setTranslationY(currentContainer.getHeight());
            currentContainer.animate()
                .translationY(0)
                .setDuration(300)
                .setInterpolator(new android.view.animation.DecelerateInterpolator(1.5f))
                .start();
        }
    }
    
    private void dismissCustomCardDialog() {
        if (currentDialog != null && currentContainer != null) {
            if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.HONEYCOMB) {
        int containerHeight = currentContainer.getHeight();
        if (containerHeight == 0) {
                    containerHeight = (int) (activity.getResources().getDisplayMetrics().heightPixels * 0.75f);
        }

            currentContainer.animate()
                    .translationY(containerHeight)
                    .setDuration(250)
                .withEndAction(() -> {
                        if (currentDialog != null) currentDialog.dismiss();
                })
                .start();
        } else {
                currentDialog.dismiss();
            }
        }
    }

    private void animateDismissCard() {
        if (currentContainer == null) return;
        int containerHeight = currentContainer.getHeight();
        if (containerHeight == 0) {
            containerHeight = (int)(activity.getResources().getDisplayMetrics().heightPixels * 0.68f);
        }

        if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.HONEYCOMB) {
            currentContainer.animate()
                .translationY(containerHeight)
                .alpha(0.0f)
                .setDuration(300)
                .withEndAction(() -> {
                    if (currentDialog != null) currentDialog.dismiss();
                })
                .start();
        } else {
            if (currentDialog != null) currentDialog.dismiss();
        }
    }
    
    private void animateExpandCard() {
        if (currentContainer == null) return;
        DisplayMetrics metrics = activity.getResources().getDisplayMetrics();
        int expandedHeight = (int)(metrics.heightPixels * 0.88f);
        
        FrameLayout.LayoutParams params = (FrameLayout.LayoutParams)currentContainer.getLayoutParams();
        if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.HONEYCOMB) {
            android.animation.ValueAnimator animator = android.animation.ValueAnimator.ofInt(params.height, expandedHeight);
            animator.setDuration(300);
            animator.addUpdateListener(animation -> {
                params.height = (Integer)animation.getAnimatedValue();
                currentContainer.setLayoutParams(params);
            });
            animator.start();
            
            currentContainer.animate()
                .translationY(0)
                .alpha(1.0f)
                .scaleX(1.0f)
                .scaleY(1.0f)
                .setDuration(300)
                .start();
        } else {
            params.height = expandedHeight;
            currentContainer.setLayoutParams(params);
        }

        isExpanded = true;
    }

    private void animateSnapBack() {
        if (currentContainer == null) return;
        DisplayMetrics metrics = activity.getResources().getDisplayMetrics();
        int targetHeight = isExpanded ?
            (int)(metrics.heightPixels * 0.88f) : 
            (int)(metrics.heightPixels * 0.68f);
        
        FrameLayout.LayoutParams params = (FrameLayout.LayoutParams)currentContainer.getLayoutParams();
        if (params.height != targetHeight) {
            if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.HONEYCOMB) {
                android.animation.ValueAnimator animator = android.animation.ValueAnimator.ofInt(params.height, targetHeight);
                animator.setDuration(250);
                animator.addUpdateListener(animation -> {
                    params.height = (Integer)animation.getAnimatedValue();
                    currentContainer.setLayoutParams(params);
                });
                animator.start();
            } else {
                params.height = targetHeight;
                currentContainer.setLayoutParams(params);
            }
        }

        if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.HONEYCOMB) {
            currentContainer.animate()
                .translationY(0)
                .alpha(1.0f)
                .scaleX(1.0f)
                .scaleY(1.0f)
                .setDuration(250)
                .start();
        }
    }

    // ============================================================================
    // MARK: - WebView Setup
    // ============================================================================
    
    private void setupPopupWebView(WebView webView, String url) {
        WebSettings settings = webView.getSettings();
        settings.setJavaScriptEnabled(true);
        settings.setDomStorageEnabled(true);
        settings.setLoadWithOverviewMode(true);
        settings.setUseWideViewPort(true);
        settings.setBuiltInZoomControls(false);
        settings.setDisplayZoomControls(false);
        settings.setSupportZoom(false);
        
        // Enable dark mode rendering if system is in dark mode
        if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.Q) {
            if (isDarkTheme()) {
                settings.setForceDark(WebSettings.FORCE_DARK_ON);
                    } else {
                settings.setForceDark(WebSettings.FORCE_DARK_OFF);
            }
        }
        
        webView.setWebViewClient(new WebViewClient() {
            @Override
            public void onPageStarted(WebView view, String url, Bitmap favicon) {
                super.onPageStarted(view, url, favicon);
                showLoadingIndicator();
                view.setVisibility(View.INVISIBLE);
                injectStashSDKFunctions();
            }
            
            @Override
            public void onPageFinished(WebView view, String url) {
                super.onPageFinished(view, url);
                injectStashSDKFunctions();
                view.postDelayed(() -> {
                    hideLoadingIndicator();
                    view.setVisibility(View.VISIBLE);
                }, 300);
            }
        });
        
        webView.setWebChromeClient(new WebChromeClient());
        webView.addJavascriptInterface(new StashJavaScriptInterface(), "StashAndroid");
        
        FrameLayout.LayoutParams params = new FrameLayout.LayoutParams(
            FrameLayout.LayoutParams.MATCH_PARENT, FrameLayout.LayoutParams.MATCH_PARENT);
        webView.setLayoutParams(params);
        webView.setBackgroundColor(Color.TRANSPARENT);

        if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.LOLLIPOP) {
            webView.setOutlineProvider(new ViewOutlineProvider() {
                @Override
                public void getOutline(View view, Outline outline) {
                    outline.setRoundRect(0, 0, view.getWidth(), view.getHeight(), dpToPx(20));
                }
            });
            webView.setClipToOutline(true);
        }
        
        webView.loadUrl(url);
    }
    
    private void setupCardWebView(WebView webView, String url) {
        WebSettings settings = webView.getSettings();
        settings.setJavaScriptEnabled(true);
        settings.setDomStorageEnabled(true);
        settings.setLoadWithOverviewMode(true);
        settings.setUseWideViewPort(true);
        settings.setBuiltInZoomControls(false);
        settings.setDisplayZoomControls(false);
        settings.setSupportZoom(false);
        settings.setJavaScriptCanOpenWindowsAutomatically(true);
        settings.setSupportMultipleWindows(true);

        // Enable dark mode rendering if system is in dark mode
        if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.Q) {
            if (isDarkTheme()) {
                settings.setForceDark(WebSettings.FORCE_DARK_ON);
            } else {
                settings.setForceDark(WebSettings.FORCE_DARK_OFF);
            }
        }
        
        webView.setWebViewClient(new WebViewClient() {
            @Override
            public boolean shouldOverrideUrlLoading(WebView view, String url) {
                handleProviderButtons(url);
                return false;
            }
            
            @Override
            public void onPageStarted(WebView view, String url, Bitmap favicon) {
                super.onPageStarted(view, url, favicon);
                showLoadingIndicator();
                view.setVisibility(View.INVISIBLE);
                injectStashSDKFunctions();
            }
            
            @Override
            public void onPageFinished(WebView view, String url) {
                super.onPageFinished(view, url);
                handleProviderButtons(url);
                injectStashSDKFunctions();
                view.postDelayed(() -> {
                    hideLoadingIndicator();
                        view.setVisibility(View.VISIBLE);
                }, 300);
            }
        });

        webView.setWebChromeClient(new WebChromeClient() {
            @Override
            public boolean onCreateWindow(WebView view, boolean isDialog, boolean isUserGesture, android.os.Message resultMsg) {
                // Open popup links in external browser
                activity.runOnUiThread(() -> {
                    if (initialURL != null && !initialURL.isEmpty()) {
                        openWithChromeCustomTabs(initialURL);
                    }
                    dismissCurrentDialog();
                });
                return true;
            }
        });

        webView.addJavascriptInterface(new StashJavaScriptInterface(), "StashAndroid");

        FrameLayout.LayoutParams params = new FrameLayout.LayoutParams(
            FrameLayout.LayoutParams.MATCH_PARENT, FrameLayout.LayoutParams.MATCH_PARENT);
        webView.setLayoutParams(params);
        webView.setBackgroundColor(Color.TRANSPARENT);
        webView.loadUrl(url);
    }
    
    // ============================================================================
    // MARK: - JavaScript SDK Injection
    // ============================================================================
    
    private void injectStashSDKFunctions() {
        if (webView == null) return;
        
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
            "})();";
        
        webView.evaluateJavascript(script, null);
    }
    
    // ============================================================================
    // MARK: - UI Components
    // ============================================================================
    
    private void setupHomeButton(Button homeButton) {
        homeButton.setText("⌂");
        homeButton.setTextSize(18);
        homeButton.setTextColor(getThemeSecondaryTextColor());
        homeButton.setGravity(Gravity.CENTER);
        homeButton.setPadding(0, 0, 0, 0);
        
        GradientDrawable bg = new GradientDrawable();
        bg.setColor(getThemeButtonBackgroundColor());
        bg.setCornerRadius(dpToPx(20));
        bg.setStroke(dpToPx(1), getThemeBorderColor());
        homeButton.setBackground(bg);
        
        if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.LOLLIPOP) {
            homeButton.setElevation(dpToPx(6));
        }
        
        FrameLayout.LayoutParams params = new FrameLayout.LayoutParams(dpToPx(36), dpToPx(36));
        params.gravity = Gravity.TOP | Gravity.START;
        params.setMargins(dpToPx(12), dpToPx(12), 0, 0);
        homeButton.setLayoutParams(params);
        homeButton.setOnClickListener(v -> navigateHome());
    }
    
    private void navigateHome() {
        if (webView != null && initialURL != null && !initialURL.isEmpty()) {
            activity.runOnUiThread(() -> {
                try {
                    webView.loadUrl(initialURL);
                } catch (Exception e) {
                    Log.e(TAG, "Error navigating home: " + e.getMessage());
                }
            });
        }
    }
    
    private void handleProviderButtons(String url) {
        if (url == null || currentHomeButton == null) return;
        String lowerUrl = url.toLowerCase();
        boolean isProvider = lowerUrl.contains("klarna") || 
                            lowerUrl.contains("paypal") || 
                            lowerUrl.contains("stripe");
                activity.runOnUiThread(() -> {
            try {
                currentHomeButton.setVisibility(isProvider ? View.VISIBLE : View.GONE);
                    } catch (Exception e) {
                Log.e(TAG, "Error toggling home button: " + e.getMessage());
            }
        });
    }
    
    // ============================================================================
    // MARK: - Loading Indicator
    // ============================================================================
    
    private void showLoadingIndicator() {
        if (currentContainer == null || activity == null) return;
        activity.runOnUiThread(() -> {
            try {
                if (loadingIndicator != null && loadingIndicator.getParent() != null) {
                    ((ViewGroup)loadingIndicator.getParent()).removeView(loadingIndicator);
                }
                
                // Use application context for proper Material theme
                Context appContext = activity.getApplicationContext();
                
                // Create ProgressBar with default style (not Large - matches Activity implementation)
                loadingIndicator = new ProgressBar(appContext);
                
                // Enable hardware acceleration
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
                
                // Same size as Activity
                FrameLayout.LayoutParams params = new FrameLayout.LayoutParams(dpToPx(48), dpToPx(48));
                params.gravity = Gravity.CENTER;
                loadingIndicator.setLayoutParams(params);
                
                currentContainer.addView(loadingIndicator);
                loadingIndicator.bringToFront();
                
                // Force animation to start after layout
                loadingIndicator.post(() -> {
                    if (loadingIndicator != null) {
                        loadingIndicator.setVisibility(View.VISIBLE);
                        loadingIndicator.requestLayout();
                    }
                });
            } catch (Exception e) {
                Log.e(TAG, "Error showing loading indicator: " + e.getMessage());
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
                Log.e(TAG, "Error hiding loading indicator: " + e.getMessage());
            }
        });
    }
    
    // ============================================================================
    // MARK: - Chrome Custom Tabs
    // ============================================================================
    
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
            UnityPlayer.UnitySendMessage(unityGameObjectName, "OnAndroidDialogDismissed", "");
        }, 1000);
    }
    
    private void openWithDefaultBrowser(String url) {
        Intent browserIntent = new Intent(Intent.ACTION_VIEW, Uri.parse(url));
        browserIntent.addFlags(Intent.FLAG_ACTIVITY_NEW_TASK);
        activity.startActivity(browserIntent);
        isCurrentlyPresented = true;

        new Handler(Looper.getMainLooper()).postDelayed(() -> {
            UnityPlayer.UnitySendMessage(unityGameObjectName, "OnAndroidDialogDismissed", "");
        }, 1000);
    }

    // ============================================================================
    // MARK: - Cleanup & Dismissal
    // ============================================================================
    
    private void dismissCurrentDialog() {
        if (usePopupPresentation) {
            dismissPopupDialog();
        } else if (currentDialog != null) {
            dismissCustomCardDialog();
        }
    }
    
    private void cleanupAllViews() {
        try {
            // Clean up loading indicator
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
            
            // Dismiss dialog
            if (currentDialog != null) {
                if (currentDialog.isShowing()) {
                    currentDialog.dismiss();
                }
                currentDialog = null;
            }
            
            // Clean up WebView
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
            
            // Clean up home button
            if (currentHomeButton != null) {
                try {
                    if (currentHomeButton.getParent() != null) {
                        ((ViewGroup)currentHomeButton.getParent()).removeView(currentHomeButton);
                }
            } catch (Exception e) {
                    Log.e(TAG, "Error cleaning up home button: " + e.getMessage());
                }
                currentHomeButton = null;
            }
            
            // Clean up container
            if (currentContainer != null) {
                try {
                    if (currentContainer.getParent() != null) {
                        ((ViewGroup)currentContainer.getParent()).removeView(currentContainer);
                    }
                    currentContainer.removeAllViews();
            } catch (Exception e) {
                    Log.e(TAG, "Error cleaning up container: " + e.getMessage());
                }
                currentContainer = null;
            }
        } catch (Exception e) {
            Log.e(TAG, "Error during cleanup: " + e.getMessage());
        }
        
        // Reset state flags
        isExpanded = false;
        isPurchaseProcessing = false;
        usePopupPresentation = false;
        initialURL = null;
    }
    
    // ============================================================================
    // MARK: - Helper Methods
    // ============================================================================
    
    private boolean isTablet() {
        DisplayMetrics metrics = activity.getResources().getDisplayMetrics();
        int smallerDimension = Math.min(metrics.widthPixels, metrics.heightPixels);
        float smallerDp = smallerDimension / metrics.density;
        return smallerDp >= 600;
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
    
    private int getThemeSecondaryTextColor() {
        return Color.parseColor("#8E8E93");
    }

    private int getThemeButtonBackgroundColor() {
        return isDarkTheme() ? Color.parseColor("#2C2C2E") : Color.parseColor("#F2F2F7");
    }

    private int getThemeBorderColor() {
        return isDarkTheme() ? Color.parseColor("#38383A") : Color.parseColor("#E5E5EA");
    }
}

