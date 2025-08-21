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
 * Android implementation of StashPayCard using WebView with card-like appearance
 * Provides card-style WebView interface with payment callbacks
 */
public class StashPayCardPlugin {
    private static final String TAG = "StashPayCard";
    private static StashPayCardPlugin instance;
    // Current dialog and WebView references
    private Dialog currentDialog;
    private Object currentBottomSheetDialog; // Reflection-based access
    private WebView webView;
    private Activity activity;
    // View references that need cleanup
    private FrameLayout currentContainer;
    private FrameLayout currentControlsOverlay;
    private Button currentCloseButton;
    private Button currentBackButton;
    private Button currentHomeButton;
    private View currentBackClickTarget;
    private View currentDragHandleArea;
    // Store initial URL for back button navigation
    private String initialURL;
    // Card drawer configuration
    private boolean useCardDrawer = true; // Default to card drawer experience
    private boolean isExpanded = false; // Track if card is currently expanded
    private Object bottomSheetBehavior; // Reflection-based access
    // Configuration values
    private float cardHeightRatio = 0.6f;
    private float cardVerticalPosition = 1.0f;
    private float cardWidthRatio = 1.0f;
    // Callback flags and handlers
    private boolean isCurrentlyPresented = false;
    private boolean paymentSuccessHandled = false;
    private boolean isPurchaseProcessing = false;
    private String unityGameObjectName = "StashPayCard";
    // Chrome Custom Tabs support
    private boolean forceSafariViewController = false;
    // JavaScript interface for payment callbacks
    private class StashJavaScriptInterface {
        @JavascriptInterface
        public void onPaymentSuccess() {
            // Prevent multiple calls
            if (paymentSuccessHandled) {
                return;
            }
            paymentSuccessHandled = true;

            // Re-enable dismissal since processing is complete
            isPurchaseProcessing = false;

            // Call Unity callback on main thread
            new Handler(Looper.getMainLooper()).post(() -> {
                try {
                    // Show close button again
                    showCloseButton();

                    // Show home button as well
                    if (currentHomeButton != null && activity != null) {
                        activity.runOnUiThread(() -> {
                            currentHomeButton.setVisibility(View.VISIBLE);
                        });
                    }

                    UnityPlayer.UnitySendMessage(unityGameObjectName, "OnAndroidPaymentSuccess", "");

                    // Auto-dismiss the dialog
                    if (currentBottomSheetDialog != null) {
                        // Bottom sheet dialog (Material Components)
                        dismissBottomSheetDialog();
                    } else if (currentDialog != null && currentDialog.isShowing()) {
                        // Check if it's the custom card dialog that needs animation
                        if (currentContainer != null && currentContainer.getParent() != null) {
                            // Custom card dialog - animate dismissal
                            dismissCustomCardDialogDirect();
                        } else {
                            // Regular dialog
                            currentDialog.dismiss();
                        }
                    }
                } catch (Exception e) {
                    Log.e(TAG, "Error handling payment success: " + e.getMessage());
                    // Force cleanup if dismiss fails
                    cleanupAllViews();
                }
            });
        }
        @JavascriptInterface
        public void onPaymentFailure() {
            // Re-enable dismissal since processing is complete
            isPurchaseProcessing = false;

            new Handler(Looper.getMainLooper()).post(() -> {
                try {
                    // Show close button again
                    showCloseButton();

                    // Show home button as well
                    if (currentHomeButton != null && activity != null) {
                        activity.runOnUiThread(() -> {
                            currentHomeButton.setVisibility(View.VISIBLE);
                        });
                    }

                    UnityPlayer.UnitySendMessage(unityGameObjectName, "OnAndroidPaymentFailure", "");

                    // Auto-dismiss the dialog
                    if (currentBottomSheetDialog != null) {
                        // Bottom sheet dialog (Material Components)
                        dismissBottomSheetDialog();
                    } else if (currentDialog != null && currentDialog.isShowing()) {
                        // Check if it's the custom card dialog that needs animation
                        if (currentContainer != null && currentContainer.getParent() != null) {
                            // Custom card dialog - animate dismissal
                            dismissCustomCardDialogDirect();
                        } else {
                            // Regular dialog
                            currentDialog.dismiss();
                        }
                    }
                } catch (Exception e) {
                    Log.e(TAG, "Error handling payment failure: " + e.getMessage());
                    // Force cleanup if dismiss fails
                    cleanupAllViews();
                }
            });
        }
        @JavascriptInterface
        public void onPurchaseProcessing() {
            // Set processing state
            isPurchaseProcessing = true;

            // Hide close button during processing using dedicated helper method
            hideCloseButton();

            // Hide home button as well
            if (currentHomeButton != null && activity != null) {
                activity.runOnUiThread(() -> {
                    currentHomeButton.setVisibility(View.GONE);
                });
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
        this.activity = UnityPlayer.currentActivity;
    }

    /**
     * Opens a URL in a card-like WebView dialog
     */
    public void openURL(String url) {
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

        // initialURL stored after cleanup
        // Final variable for lambda
        final String finalUrl = url;

        activity.runOnUiThread(() -> {
            if (forceSafariViewController) {
                openWithChromeCustomTabs(finalUrl);
            } else if (useCardDrawer) {
                // Try card-style popup first, fallback to regular dialog if it fails
                if (isMaterialComponentsAvailable()) {
                    Log.i(TAG, "Material Components available, using card drawer");
                    createAndShowCardDrawer(finalUrl);
                } else {
                    Log.i(TAG, "Material Components not available, using custom card dialog");
                    createAndShowCardStyleDialog(finalUrl);
                }
            } else {
                createAndShowDialog(finalUrl);
            }
        });
    }
    private void createAndShowDialog(String url) {
        // Clean up existing views and dialog
        cleanupAllViews();

        // Store the initial URL for back button navigation (AFTER cleanup)
        initialURL = url;

        // Reset payment handling flag
        paymentSuccessHandled = false;

        try {
            // Create completely new dialog
            currentDialog = new Dialog(activity);
            currentDialog.requestWindowFeature(Window.FEATURE_NO_TITLE);

            // Create fresh container
            currentContainer = new FrameLayout(activity);
            setupContainerAppearance(currentContainer);

            // Create fresh WebView
            webView = new WebView(activity);
            setupWebView(webView, url);

            // Create fresh close button
            currentCloseButton = new Button(activity);
            setupCloseButton(currentCloseButton);

            // Add views to container (all fresh, no parents)
            currentContainer.addView(webView);
            currentContainer.addView(currentCloseButton);

        // Create Home button (initially hidden, shown on PayPal/Klarna)
        if (currentHomeButton != null && currentHomeButton.getParent() != null) {
            ((ViewGroup) currentHomeButton.getParent()).removeView(currentHomeButton);
        }
        currentHomeButton = new Button(activity);
        setupHomeButton(currentHomeButton);
        currentHomeButton.setVisibility(View.GONE);
        currentContainer.addView(currentHomeButton);

            // Set dialog content
            currentDialog.setContentView(currentContainer);
        } catch (Exception e) {
            Log.e(TAG, "Error creating dialog: " + e.getMessage());
            e.printStackTrace();
            return;
        }

        // Configure dialog window
        configureDialogWindow();

        // Set dismiss listener
        currentDialog.setOnDismissListener(dialog -> {
            isCurrentlyPresented = false;

            // Clean up all views
            cleanupAllViews();

            // Notify Unity of dismissal
            try {
                UnityPlayer.UnitySendMessage(unityGameObjectName, "OnAndroidDialogDismissed", "");
            } catch (Exception e) {
                Log.e(TAG, "Error sending Unity message: " + e.getMessage());
            }
        });

        // Show dialog
        currentDialog.show();
        isCurrentlyPresented = true;
    }

    /**
     * Checks if Material Components library is available and functional
     */
    private boolean isMaterialComponentsAvailable() {
        try {
            // Check if the main class exists
            Class.forName("com.google.android.material.bottomsheet.BottomSheetDialog");
            
            // Also check if the callback interface exists and is properly defined
            try {
                Class<?> callbackClass = Class.forName("com.google.android.material.bottomsheet.BottomSheetBehavior$BottomSheetCallback");
                if (!callbackClass.isInterface()) {
                    Log.w(TAG, "Material Components available but BottomSheetCallback is not an interface");
                    return false;
                }
            } catch (ClassNotFoundException e) {
                Log.w(TAG, "Material Components available but BottomSheetCallback not found");
                return false;
            }
            
            return true;
        } catch (ClassNotFoundException e) {
            return false;
        }
    }

    /**
     * Creates and shows a card drawer (BottomSheetDialog) - card-style experience
     */
    private void createAndShowCardDrawer(String url) {
        // Clean up existing views and dialog
        cleanupAllViews();

        // Store the initial URL for back button navigation (AFTER cleanup)
        initialURL = url;

        // Reset payment handling flag
        paymentSuccessHandled = false;

        try {
            // Create BottomSheetDialog using reflection
            currentBottomSheetDialog = createBottomSheetDialogWithReflection(activity);
            if (currentBottomSheetDialog == null) {
                Log.e(TAG, "Failed to create BottomSheetDialog, falling back to regular dialog");
                createAndShowDialog(url);
                return;
            }

            // Create the card-style layout
            currentContainer = createCardLayout();

            // Create drag handle area (as seen in screenshot)
            currentDragHandleArea = createCardDragHandleArea();
            // Reduce width so edges remain tappable for back/close
            FrameLayout.LayoutParams dhParams = (FrameLayout.LayoutParams) currentDragHandleArea.getLayoutParams();
            if (dhParams == null) {
                dhParams = new FrameLayout.LayoutParams(FrameLayout.LayoutParams.WRAP_CONTENT, FrameLayout.LayoutParams.WRAP_CONTENT);
                currentDragHandleArea.setLayoutParams(dhParams);
            }
            dhParams.width = FrameLayout.LayoutParams.WRAP_CONTENT;
            currentContainer.addView(currentDragHandleArea);

            // Create WebView that fills the card properly
            webView = new WebView(activity);
            setupCardWebViewLarger(webView, url);
            currentContainer.addView(webView);

            // Create controls overlay to host buttons above WebView
            currentControlsOverlay = new FrameLayout(activity);
            FrameLayout.LayoutParams overlayParams = new FrameLayout.LayoutParams(
                FrameLayout.LayoutParams.MATCH_PARENT,
                FrameLayout.LayoutParams.MATCH_PARENT
            );
            currentControlsOverlay.setLayoutParams(overlayParams);
            currentControlsOverlay.setClickable(false);
            currentControlsOverlay.setImportantForAccessibility(View.IMPORTANT_FOR_ACCESSIBILITY_NO);
            currentContainer.addView(currentControlsOverlay);

            // Create close button in top-right (card style)
            currentCloseButton = createCardStyleCloseButton();
            currentControlsOverlay.addView(currentCloseButton);

            // Create Home button in top-left (card style), initially hidden
            if (currentHomeButton != null && currentHomeButton.getParent() != null) {
                ((ViewGroup) currentHomeButton.getParent()).removeView(currentHomeButton);
            }
            currentHomeButton = new Button(activity);
            setupHomeButton(currentHomeButton);
            currentHomeButton.setVisibility(View.GONE);
            currentControlsOverlay.addView(currentHomeButton);

            // Set the card as dialog content
            setBottomSheetContentView(currentContainer);

            // Configure card-style behavior
            configureCardBehavior();

            // Set window properties for proper card appearance
            setBottomSheetWindowFlags();
            setBottomSheetBackgroundDim();
        } catch (Exception e) {
            Log.e(TAG, "Error creating card drawer: " + e.getMessage());
            e.printStackTrace();
            // Fallback to custom card dialog (more reliable than regular dialog)
            Log.i(TAG, "Falling back to custom card dialog");
            createAndShowCardStyleDialog(url);
            return;
        }

        // Set dismiss listener for card drawer
        setBottomSheetDismissListener();

        // Show card drawer using reflection
        showBottomSheetDialog();
        isCurrentlyPresented = true;
    }

    /**
     * Creates card-style dialog without Material Components dependency
     */
    private void createAndShowCardStyleDialog(String url) {
        // Clean up any existing views
        cleanupAllViews();

        // Store the initial URL for back button navigation (AFTER cleanup)
        initialURL = url;

        paymentSuccessHandled = false;

        try {
            // Create a custom dialog with transparent background so app shows through
            currentDialog = new Dialog(activity, android.R.style.Theme_Translucent_NoTitleBar_Fullscreen);
            currentDialog.requestWindowFeature(Window.FEATURE_NO_TITLE);

            // Create the main frame that will hold everything
            FrameLayout mainFrame = new FrameLayout(activity);
            mainFrame.setLayoutParams(new FrameLayout.LayoutParams(
                FrameLayout.LayoutParams.MATCH_PARENT,
                FrameLayout.LayoutParams.MATCH_PARENT
            ));

            // Add very subtle overlay so app is still visible but card is prominent
            mainFrame.setBackgroundColor(Color.parseColor("#20000000")); // Very light overlay (12.5% opacity)
            // Create the card-style layout
            currentContainer = createCardLayoutCustom();

            // Add drag handle at the top
            LinearLayout dragHandleArea = createCardDragHandleArea();
            currentContainer.addView(dragHandleArea);

            // Create WebView with explicit sizing and positioning
            webView = new WebView(activity);
            setupCardWebViewLarger(webView, url);
            currentContainer.addView(webView);

            // Create close button
            currentCloseButton = createCardStyleCloseButton();
            currentContainer.addView(currentCloseButton);

            // Create Home button (initially hidden)
            if (currentHomeButton != null && currentHomeButton.getParent() != null) {
                ((ViewGroup) currentHomeButton.getParent()).removeView(currentHomeButton);
            }
            currentHomeButton = new Button(activity);
            setupHomeButton(currentHomeButton);
            currentHomeButton.setVisibility(View.GONE);
            currentContainer.addView(currentHomeButton);

                    // Position the card at the bottom with explicit sizing
        DisplayMetrics metrics = activity.getResources().getDisplayMetrics();
        int screenHeight = metrics.heightPixels;
        int cardHeight = (int) (screenHeight * 0.68f); // 68% of screen height
        FrameLayout.LayoutParams cardParams = new FrameLayout.LayoutParams(
            FrameLayout.LayoutParams.MATCH_PARENT,
            cardHeight // Explicit height, not WRAP_CONTENT
        );
        cardParams.gravity = Gravity.BOTTOM;
        currentContainer.setLayoutParams(cardParams);

            // Add card to main frame
            mainFrame.addView(currentContainer);

            // Set dialog content
            currentDialog.setContentView(mainFrame);

            // Configure dialog window for transparency and proper behavior
            Window window = currentDialog.getWindow();
            if (window != null) {
                window.setLayout(ViewGroup.LayoutParams.MATCH_PARENT, ViewGroup.LayoutParams.MATCH_PARENT);
                window.setFlags(WindowManager.LayoutParams.FLAG_HARDWARE_ACCELERATED,
                               WindowManager.LayoutParams.FLAG_HARDWARE_ACCELERATED);

                // Make background transparent so app shows through
                window.setBackgroundDrawableResource(android.R.color.transparent);

                // Add dim effect but keep app visible
                window.addFlags(WindowManager.LayoutParams.FLAG_DIM_BEHIND);
                WindowManager.LayoutParams params = window.getAttributes();
                params.dimAmount = 0.3f; // Light dimming - app still clearly visible
                window.setAttributes(params);
            }

            // Add dismiss behavior when clicking overlay
            mainFrame.setOnClickListener(v -> {
                // Only dismiss if clicked outside the card
                dismissCustomCardDialog();
            });

            // Prevent card clicks from dismissing
            currentContainer.setOnClickListener(v -> {
                // Consume click to prevent bubbling to mainFrame
            });

            // Set dismiss listener
            currentDialog.setOnDismissListener(dialog -> {
                if (!paymentSuccessHandled) {
                    activity.runOnUiThread(() ->
                        UnityPlayer.UnitySendMessage("StashPayCard", "OnAndroidDialogDismissed", "Dialog dismissed")
                    );
                }
                cleanupAllViews();
                isCurrentlyPresented = false;
            });

            // Show with slide-up animation
            currentDialog.show();
            animateSlideUp();

            isCurrentlyPresented = true;
        } catch (Exception e) {
            Log.e(TAG, "Error creating custom card-style dialog: " + e.getMessage());
            e.printStackTrace();
            // Final fallback to regular dialog
            createAndShowDialog(url);
        }
    }

    /**
     * Creates the card-style layout for custom dialog
     */
    private FrameLayout createCardLayoutCustom() {
        FrameLayout cardContainer = new FrameLayout(activity);

        // Get screen dimensions
        DisplayMetrics metrics = activity.getResources().getDisplayMetrics();
        int screenHeight = metrics.heightPixels;

        // Make card take moderate portion of screen - 68% (20% smaller than before)
        int cardHeight = (int) (screenHeight * 0.68f);

        // Use MATCH_PARENT for width and explicit height
        FrameLayout.LayoutParams params = new FrameLayout.LayoutParams(
            FrameLayout.LayoutParams.MATCH_PARENT,
            cardHeight
        );
        cardContainer.setLayoutParams(params);

        // Create card-style background with theme-aware color
        GradientDrawable cardBackground = new GradientDrawable();
        cardBackground.setColor(getThemeBackgroundColor());

        // Card corner radius - only top corners
        float cornerRadius = dpToPx(25);
        cardBackground.setCornerRadii(new float[]{
            cornerRadius, cornerRadius, // top-left
            cornerRadius, cornerRadius, // top-right
            0, 0,                       // bottom-right (square)
            0, 0                        // bottom-left (square)
        });

        cardContainer.setBackground(cardBackground);

        // Add elevation shadow for card effect
        if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.LOLLIPOP) {
            cardContainer.setElevation(dpToPx(24)); // Strong shadow
        }

        return cardContainer;
    }

    /**
     * Animates the card sliding up from bottom
     */
    private void animateSlideUp() {
        if (currentContainer != null && Build.VERSION.SDK_INT >= Build.VERSION_CODES.HONEYCOMB) {
            // Start from below screen
            currentContainer.setTranslationY(currentContainer.getHeight());

            // Animate to position with smooth timing
            currentContainer.animate()
                .translationY(0)
                .setDuration(300) // Smooth animation duration
                .setInterpolator(new android.view.animation.DecelerateInterpolator(1.5f))
                .start();
        }
    }

    /**
     * Dismisses the custom card-style dialog with animation
     */
    private void dismissCustomCardDialog() {
        if (currentDialog != null && currentContainer != null) {
            if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.HONEYCOMB) {
                            // Get the container height for animation
            int containerHeight = currentContainer.getHeight();
            if (containerHeight == 0) {
                // If height not measured yet, use screen height as fallback
                DisplayMetrics metrics = activity.getResources().getDisplayMetrics();
                containerHeight = (int) (metrics.heightPixels * 0.75f);
            }

            // Animate slide down
            currentContainer.animate()
                .translationY(containerHeight)
                .setDuration(250)
                .setInterpolator(new android.view.animation.AccelerateInterpolator(1.5f))
                .withEndAction(() -> {
                    if (currentDialog != null) {
                        currentDialog.dismiss();
                    }
                })
                .start();
            } else {
                // No animation on older devices
                currentDialog.dismiss();
            }
        }
    }

    /**
     * Dismisses the custom card-style dialog directly without runOnUiThread (for use when already on UI thread)
     */
    private void dismissCustomCardDialogDirect() {
        if (currentDialog != null && currentContainer != null) {
            if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.HONEYCOMB) {
                // Get the container height for animation
                int containerHeight = currentContainer.getHeight();
                if (containerHeight == 0) {
                    // If height not measured yet, use screen height as fallback
                    DisplayMetrics metrics = activity.getResources().getDisplayMetrics();
                    containerHeight = (int) (metrics.heightPixels * 0.75f);
                }

                // Animate slide down
                currentContainer.animate()
                    .translationY(containerHeight)
                    .setDuration(250)
                    .setInterpolator(new android.view.animation.AccelerateInterpolator(1.5f))
                    .withEndAction(() -> {
                        if (currentDialog != null) {
                            currentDialog.dismiss();
                        }
                    })
                    .start();
            } else {
                // No animation on older devices
                currentDialog.dismiss();
            }
        }
    }

    /**
     * Adds drag-to-dismiss touch handling to the drag handle area
     */
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
                        // Record initial touch position
                        initialY = event.getRawY();
                        initialTranslationY = currentContainer.getTranslationY();
                        isDragging = false;
                        return true;

                    case MotionEvent.ACTION_MOVE:
                        float deltaY = event.getRawY() - initialY;

                        if (Math.abs(deltaY) > dpToPx(10)) { // Minimum movement threshold
                            isDragging = true;

                            if (deltaY > 0) {
                                // Downward drag - dismiss behavior
                                float newTranslationY = initialTranslationY + (deltaY * 0.7f);
                                currentContainer.setTranslationY(newTranslationY);

                                // Add visual feedback - fade the card slightly as it's dragged down
                                float progress = Math.min(deltaY / dpToPx(200), 1.0f);
                                float alpha = 1.0f - (progress * 0.3f); // Fade to 70% opacity
                                currentContainer.setAlpha(alpha);
                            } else if (deltaY < 0 && !isExpanded) {
                                // Upward drag - expand behavior (only if not already expanded)
                                float upwardProgress = Math.min(Math.abs(deltaY) / dpToPx(150), 1.0f); // 150dp threshold for expansion
                                // Calculate new height during drag
                                DisplayMetrics metrics = activity.getResources().getDisplayMetrics();
                                int screenHeight = metrics.heightPixels;
                                int baseHeight = (int) (screenHeight * 0.68f); // Current 68%
                                int maxExpandedHeight = (int) (screenHeight * 0.88f); // Target 88% (30% increase)
                                int newHeight = (int) (baseHeight + (maxExpandedHeight - baseHeight) * upwardProgress);

                                // Update card height dynamically during drag
                                FrameLayout.LayoutParams cardParams = (FrameLayout.LayoutParams) currentContainer.getLayoutParams();
                                cardParams.height = newHeight;
                                currentContainer.setLayoutParams(cardParams);

                                // Update WebView height fluidly during drag
                                if (webView != null) {
                                    FrameLayout.LayoutParams webViewParams = (FrameLayout.LayoutParams) webView.getLayoutParams();
                                    webViewParams.height = newHeight; // Match card height exactly
                                    webView.setLayoutParams(webViewParams);
                                    
                                    // Force webview to reflow content after size change
                                    webView.post(() -> {
                                        webView.requestLayout();
                                        webView.invalidate();
                                    });
                                }

                                // Visual feedback - slight scale effect
                                float scale = 1.0f + (upwardProgress * 0.02f); // Subtle scale increase
                                currentContainer.setScaleX(scale);
                                currentContainer.setScaleY(scale);
                            }
                        }
                        return true;

                    case MotionEvent.ACTION_UP:
                    case MotionEvent.ACTION_CANCEL:
                        if (isDragging) {
                            float finalDeltaY = event.getRawY() - initialY;
                            DisplayMetrics metrics = activity.getResources().getDisplayMetrics();

                            if (finalDeltaY > 0) {
                                // Downward drag - check for dismiss
                                int dismissThreshold = (int) (metrics.heightPixels * 0.25f); // 25% of screen
                                if (finalDeltaY > dismissThreshold) {
                                    // Dismiss with smooth animation
                                    animateDismissCard();
                                } else {
                                    // Snap back to current size (normal or expanded)
                                    animateSnapBack();
                                }
                            } else if (finalDeltaY < 0 && !isExpanded) {
                                // Upward drag - check for expansion
                                int expandThreshold = dpToPx(80); // 80dp upward movement to trigger expansion
                                if (Math.abs(finalDeltaY) > expandThreshold) {
                                    // Expand the card
                                    animateExpandCard();
                                } else {
                                    // Snap back to normal size
                                    animateSnapBack();
                                }
                            } else {
                                // No significant movement or already expanded - snap back
                                animateSnapBack();
                            }
                        }
                        return true;
                }
                return false;
            }
        });
    }

    /**
     * Animates the card dismissal when drag-to-dismiss threshold is reached
     */
    private void animateDismissCard() {
        if (currentContainer == null) return;

        // Get the distance to animate off-screen
        int containerHeight = currentContainer.getHeight();
        if (containerHeight == 0) {
            DisplayMetrics metrics = activity.getResources().getDisplayMetrics();
            containerHeight = (int) (metrics.heightPixels * 0.68f);
        }

        if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.HONEYCOMB) {
            currentContainer.animate()
                .translationY(containerHeight) // Slide completely off-screen
                .alpha(0.0f) // Fade out
                .setDuration(300) // Smooth animation duration
                .setInterpolator(new android.view.animation.AccelerateInterpolator(1.2f))
                .withEndAction(() -> {
                    // Dismiss the dialog after animation
                    if (currentDialog != null) {
                        currentDialog.dismiss();
                    }
                })
                .start();
        } else {
            // Fallback for older devices
            if (currentDialog != null) {
                currentDialog.dismiss();
            }
        }
    }

    /**
     * Animates the card expanding to 88% height (30% increase)
     */
    private void animateExpandCard() {
        if (currentContainer == null) return;

        DisplayMetrics metrics = activity.getResources().getDisplayMetrics();
        int screenHeight = metrics.heightPixels;
        int expandedHeight = (int) (screenHeight * 0.88f); // 88% of screen (30% increase from 68%)
        // Update card container height
        FrameLayout.LayoutParams cardParams = (FrameLayout.LayoutParams) currentContainer.getLayoutParams();

        if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.HONEYCOMB) {
            // Animate both card and WebView height together fluidly
            android.animation.ValueAnimator heightAnimator = android.animation.ValueAnimator.ofInt(cardParams.height, expandedHeight);
            heightAnimator.setDuration(300);
            heightAnimator.setInterpolator(new android.view.animation.DecelerateInterpolator(1.2f));
            heightAnimator.addUpdateListener(animation -> {
                int animatedHeight = (Integer) animation.getAnimatedValue();

                // Update card height
                cardParams.height = animatedHeight;
                currentContainer.setLayoutParams(cardParams);

                // Update WebView height simultaneously for fluid expansion
                if (webView != null) {
                    FrameLayout.LayoutParams webViewParams = (FrameLayout.LayoutParams) webView.getLayoutParams();
                    webViewParams.height = animatedHeight; // Match exactly
                    webView.setLayoutParams(webViewParams);
                    
                    // Force webview to reflow content after size change
                    webView.post(() -> {
                        webView.requestLayout();
                        webView.invalidate();
                    });
                }
            });
            heightAnimator.start();

            // Reset any visual effects
            currentContainer.animate()
                .translationY(0)
                .alpha(1.0f)
                .scaleX(1.0f)
                .scaleY(1.0f)
                .setDuration(300)
                .setInterpolator(new android.view.animation.DecelerateInterpolator(1.2f))
                .start();
        } else {
            // Fallback for older devices - update both heights together
            cardParams.height = expandedHeight;
            currentContainer.setLayoutParams(cardParams);

            if (webView != null) {
                FrameLayout.LayoutParams webViewParams = (FrameLayout.LayoutParams) webView.getLayoutParams();
                webViewParams.height = expandedHeight;
                webView.setLayoutParams(webViewParams);
                
                // Force webview to reflow content after size change
                webView.post(() -> {
                    webView.requestLayout();
                    webView.invalidate();
                });
            }
        }

        isExpanded = true;
    }

    /**
     * Animates the card snapping back to position when drag threshold not reached
     */
    private void animateSnapBack() {
        if (currentContainer == null) return;

        // Get target height based on current state
        DisplayMetrics metrics = activity.getResources().getDisplayMetrics();
        int screenHeight = metrics.heightPixels;
        int targetHeight = isExpanded ?
            (int) (screenHeight * 0.88f) : // Keep expanded if already expanded
            (int) (screenHeight * 0.68f);   // Return to normal size
        // Update height if needed with fluid WebView animation
        FrameLayout.LayoutParams cardParams = (FrameLayout.LayoutParams) currentContainer.getLayoutParams();
        if (cardParams.height != targetHeight) {
            if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.HONEYCOMB) {
                android.animation.ValueAnimator heightAnimator = android.animation.ValueAnimator.ofInt(cardParams.height, targetHeight);
                heightAnimator.setDuration(250);
                heightAnimator.setInterpolator(new android.view.animation.DecelerateInterpolator(1.5f));
                heightAnimator.addUpdateListener(animation -> {
                    int animatedHeight = (Integer) animation.getAnimatedValue();

                    // Update card height
                    cardParams.height = animatedHeight;
                    currentContainer.setLayoutParams(cardParams);

                    // Update WebView height fluidly during snap-back
                    if (webView != null) {
                        FrameLayout.LayoutParams webViewParams = (FrameLayout.LayoutParams) webView.getLayoutParams();
                        webViewParams.height = animatedHeight; // Always match card height
                        webView.setLayoutParams(webViewParams);
                        
                        // Force webview to reflow content after size change
                        webView.post(() -> {
                            webView.requestLayout();
                            webView.invalidate();
                        });
                    }
                });
                heightAnimator.start();
            } else {
                // Fallback for older devices - update both together
                cardParams.height = targetHeight;
                currentContainer.setLayoutParams(cardParams);

                if (webView != null) {
                    FrameLayout.LayoutParams webViewParams = (FrameLayout.LayoutParams) webView.getLayoutParams();
                    webViewParams.height = targetHeight;
                    webView.setLayoutParams(webViewParams);
                    
                    // Force webview to reflow content after size change
                    webView.post(() -> {
                        webView.requestLayout();
                        webView.invalidate();
                    });
                }
            }
        }

        if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.HONEYCOMB) {
            currentContainer.animate()
                .translationY(0) // Return to original position
                .alpha(1.0f) // Restore full opacity
                .scaleX(1.0f) // Reset scale
                .scaleY(1.0f)
                .setDuration(250) // Quick snap-back
                .setInterpolator(new android.view.animation.DecelerateInterpolator(1.5f))
                .start();
        }
    }

    /**
     * Creates BottomSheetDialog using reflection
     */
    private Object createBottomSheetDialogWithReflection(Activity activity) {
        try {
            Class<?> bottomSheetDialogClass = Class.forName("com.google.android.material.bottomsheet.BottomSheetDialog");
            return bottomSheetDialogClass.getConstructor(Context.class).newInstance(activity);
        } catch (Exception e) {
            Log.e(TAG, "Failed to create BottomSheetDialog via reflection: " + e.getMessage());
            return null;
        }
    }

    /**
     * Sets window flags for BottomSheetDialog using reflection
     */
    private void setBottomSheetWindowFlags() {
        try {
            if (currentBottomSheetDialog != null) {
                java.lang.reflect.Method getWindowMethod = currentBottomSheetDialog.getClass().getMethod("getWindow");
                Window window = (Window) getWindowMethod.invoke(currentBottomSheetDialog);
                if (window != null) {
                    // REMOVED FLAG_NOT_FOCUSABLE to allow proper input focus and keyboard
                    // The dialog should be focusable for WebView input to work properly
                    window.setFlags(
                        WindowManager.LayoutParams.FLAG_HARDWARE_ACCELERATED,
                        WindowManager.LayoutParams.FLAG_HARDWARE_ACCELERATED
                    );
                }
            }
        } catch (Exception e) {
            Log.e(TAG, "Failed to set window flags: " + e.getMessage());
        }
    }

    /**
     * Sets background dimming for card-like overlay
     */
    private void setBottomSheetBackgroundDim() {
        try {
            if (currentBottomSheetDialog != null) {
                java.lang.reflect.Method getWindowMethod = currentBottomSheetDialog.getClass().getMethod("getWindow");
                Window window = (Window) getWindowMethod.invoke(currentBottomSheetDialog);
                if (window != null) {
                    // Set dim amount for card overlay (slightly less than full black)
                    window.setDimAmount(0.4f);
                    window.addFlags(WindowManager.LayoutParams.FLAG_DIM_BEHIND);
                }
            }
        } catch (Exception e) {
            Log.e(TAG, "Failed to set background dim: " + e.getMessage());
        }
    }

    /**
     * Sets content view for BottomSheetDialog using reflection
     */
    private void setBottomSheetContentView(View view) {
        try {
            if (currentBottomSheetDialog != null) {
                java.lang.reflect.Method setContentViewMethod = currentBottomSheetDialog.getClass().getMethod("setContentView", View.class);
                setContentViewMethod.invoke(currentBottomSheetDialog, view);
            }
        } catch (Exception e) {
            Log.e(TAG, "Failed to set content view: " + e.getMessage());
        }
    }

    /**
     * Sets dismiss listener for BottomSheetDialog using reflection
     */
    private void setBottomSheetDismissListener() {
        try {
            if (currentBottomSheetDialog != null) {
                // Create dismiss listener
                Object dismissListener = java.lang.reflect.Proxy.newProxyInstance(
                    currentBottomSheetDialog.getClass().getClassLoader(),
                    new Class[]{Class.forName("android.content.DialogInterface$OnDismissListener")},
                    (proxy, method, args) -> {
                        if ("onDismiss".equals(method.getName())) {
                            isCurrentlyPresented = false;
                            cleanupAllViews();
                            try {
                                UnityPlayer.UnitySendMessage(unityGameObjectName, "OnAndroidDialogDismissed", "");
                            } catch (Exception e) {
                                Log.e(TAG, "Error sending Unity message: " + e.getMessage());
                            }
                        }
                        return null;
                    }
                );

                // Set the dismiss listener
                java.lang.reflect.Method setOnDismissListenerMethod = currentBottomSheetDialog.getClass().getMethod("setOnDismissListener", Class.forName("android.content.DialogInterface$OnDismissListener"));
                setOnDismissListenerMethod.invoke(currentBottomSheetDialog, dismissListener);
            }
        } catch (Exception e) {
            Log.e(TAG, "Failed to set dismiss listener: " + e.getMessage());
        }
    }

    /**
     * Shows BottomSheetDialog using reflection
     */
    private void showBottomSheetDialog() {
        try {
            if (currentBottomSheetDialog != null) {
                java.lang.reflect.Method showMethod = currentBottomSheetDialog.getClass().getMethod("show");
                showMethod.invoke(currentBottomSheetDialog);
            }
        } catch (Exception e) {
            Log.e(TAG, "Failed to show BottomSheetDialog: " + e.getMessage());
        }
    }

    /**
     * Creates the card-style layout matching the screenshot
     */
    private FrameLayout createCardLayout() {
        FrameLayout cardContainer = new FrameLayout(activity);

        // Get screen dimensions for proper sizing
        DisplayMetrics metrics = activity.getResources().getDisplayMetrics();
        int screenHeight = metrics.heightPixels;
        int screenWidth = metrics.widthPixels;

        // Make the card take most of the screen for card-style presentation
        int maxCardHeight = (int) (screenHeight * 0.95f); // 95% of screen height
        // Set container layout parameters
        FrameLayout.LayoutParams params = new FrameLayout.LayoutParams(
            FrameLayout.LayoutParams.MATCH_PARENT,
            maxCardHeight
        );
        cardContainer.setLayoutParams(params);

        // Create the theme-aware card background with rounded corners
        GradientDrawable cardBackground = new GradientDrawable();
        cardBackground.setColor(getThemeBackgroundColor());

        // Card corner radius - rounded only at top
        float cornerRadius = dpToPx(25); // Larger radius for card-like appearance
        cardBackground.setCornerRadii(new float[]{
            cornerRadius, cornerRadius, // top-left
            cornerRadius, cornerRadius, // top-right
            0, 0,                       // bottom-right (square)
            0, 0                        // bottom-left (square)
        });

        cardContainer.setBackground(cardBackground);

        // Add proper elevation for card-like shadow
        if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.LOLLIPOP) {
            cardContainer.setElevation(dpToPx(20)); // Strong shadow for card
        }

        // No padding - let content fill properly
        cardContainer.setPadding(0, 0, 0, 0);

        return cardContainer;
    }

    /**
     * Creates the card-style drag handle area at the top of the card
     */
    private LinearLayout createCardDragHandleArea() {
        LinearLayout dragArea = new LinearLayout(activity);
        dragArea.setOrientation(LinearLayout.VERTICAL);
        dragArea.setGravity(Gravity.CENTER_HORIZONTAL);

        // Reduced padding to minimize obstruction of input fields
        dragArea.setPadding(0, dpToPx(8), 0, dpToPx(8));

        // Completely transparent background - no white overlay
        dragArea.setBackgroundColor(Color.TRANSPARENT);

        // Create the drag handle indicator (gray bar)
        View dragHandle = new View(activity);
        GradientDrawable handleDrawable = new GradientDrawable();
        handleDrawable.setColor(Color.parseColor("#D1D1D6")); // System gray color
        handleDrawable.setCornerRadius(dpToPx(2));
        dragHandle.setBackground(handleDrawable);

        // Smaller drag handle dimensions to reduce obstruction
        LinearLayout.LayoutParams handleParams = new LinearLayout.LayoutParams(
            dpToPx(32), // Smaller width to reduce obstruction
            dpToPx(4)   // Thinner for less visual interference
        );
        dragHandle.setLayoutParams(handleParams);

        dragArea.addView(dragHandle);

        // Background already set above with semi-transparent overlay
        // Position at the very top of the card with higher z-index to float over WebView
        // IMPORTANT: limit width so it doesn't block taps on back/close buttons at the edges
        FrameLayout.LayoutParams areaParams = new FrameLayout.LayoutParams(
            FrameLayout.LayoutParams.WRAP_CONTENT,
            FrameLayout.LayoutParams.WRAP_CONTENT
        );
        areaParams.gravity = Gravity.TOP | Gravity.CENTER_HORIZONTAL;
        dragArea.setLayoutParams(areaParams);

        // Ensure drag handle appears on top of WebView
        if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.LOLLIPOP) {
            dragArea.setElevation(dpToPx(8)); // Higher elevation than WebView
        }

        // Add touch handling for drag-to-dismiss functionality
        addDragToDismissTouch(dragArea);

        return dragArea;
    }

    /**
     * Configures the BottomSheetBehavior for card-like experience using reflection
     */
    private void configureCardBehavior() {
        try {
            if (currentBottomSheetDialog == null) return;

            // Find the bottom sheet view using reflection
            java.lang.reflect.Method findViewByIdMethod = currentBottomSheetDialog.getClass().getMethod("findViewById", int.class);

            // Get the design_bottom_sheet ID using reflection
            Class<?> materialRClass = Class.forName("com.google.android.material.R$id");
            java.lang.reflect.Field designBottomSheetField = materialRClass.getField("design_bottom_sheet");
            int designBottomSheetId = designBottomSheetField.getInt(null);

            View bottomSheet = (View) findViewByIdMethod.invoke(currentBottomSheetDialog, designBottomSheetId);

            if (bottomSheet != null) {
                // Get BottomSheetBehavior using reflection
                Class<?> bottomSheetBehaviorClass = Class.forName("com.google.android.material.bottomsheet.BottomSheetBehavior");
                java.lang.reflect.Method fromMethod = bottomSheetBehaviorClass.getMethod("from", View.class);
                bottomSheetBehavior = fromMethod.invoke(null, bottomSheet);

                if (bottomSheetBehavior != null) {
                    // Configure behavior for card-like experience
                    setBottomSheetProperty("setDraggable", boolean.class, true);
                    setBottomSheetProperty("setHideable", boolean.class, true);
                    setBottomSheetProperty("setSkipCollapsed", boolean.class, false);

                    // Set fit to contents for better card behavior
                    setBottomSheetProperty("setFitToContents", boolean.class, false);

                    // Set half expanded ratio for card-like behavior
                    setBottomSheetProperty("setHalfExpandedRatio", float.class, cardHeightRatio);

                    // Calculate peek height based on screen size - show substantial card content
                    DisplayMetrics metrics = activity.getResources().getDisplayMetrics();
                    int screenHeight = metrics.heightPixels;

                    // Card-style peek height - show significant content like in screenshot
                    int peekHeight = (int) (screenHeight * 0.7f); // 70% of screen initially visible
                    int maxPeekHeight = (int) (screenHeight * 0.8f); // Max 80%
                    peekHeight = Math.min(peekHeight, maxPeekHeight);

                    setBottomSheetProperty("setPeekHeight", int.class, peekHeight);

                    // Configure for card-like smooth behavior
                    setBottomSheetProperty("setFitToContents", boolean.class, false);
                    setBottomSheetProperty("setSkipCollapsed", boolean.class, false);
                    setBottomSheetProperty("setHideable", boolean.class, true);

                    // Card-style expansion ratios
                    setBottomSheetProperty("setHalfExpandedRatio", float.class, 0.7f);
                    setBottomSheetProperty("setExpandedOffset", int.class, dpToPx(50)); // Small gap at top for card style
                    // Set initial state based on cardVerticalPosition
                    Class<?> stateClass = bottomSheetBehaviorClass;
                    if (cardVerticalPosition >= 0.8f) {
                        // STATE_COLLAPSED = 4
                        setBottomSheetProperty("setState", int.class, 4);
                    } else {
                        // STATE_EXPANDED = 3
                        setBottomSheetProperty("setState", int.class, 3);
                    }

                    // Add callback for state changes and card-like behavior
                    addBottomSheetCallback(bottomSheet);

                    // Enable smooth animation (only if method exists)
                    try {
                        setBottomSheetProperty("setUpdateImportantForAccessibility", boolean.class, true);
                    } catch (Exception e) {
                        // Method doesn't exist in this version, ignore gracefully
                        Log.d(TAG, "setUpdateImportantForAccessibility not available in this Material Components version");
                    }
                }
            }
        } catch (Exception e) {
            Log.e(TAG, "Failed to configure BottomSheetBehavior: " + e.getMessage());
            // This is not critical, the dialog will still work without the behavior configuration
        }
    }

    /**
     * Sets a property on the BottomSheetBehavior using reflection
     */
    private void setBottomSheetProperty(String methodName, Class<?> paramType, Object value) {
        try {
            if (bottomSheetBehavior != null) {
                java.lang.reflect.Method method = bottomSheetBehavior.getClass().getMethod(methodName, paramType);
                method.invoke(bottomSheetBehavior, value);
            }
        } catch (NoSuchMethodException e) {
            // Method doesn't exist in this version, log as debug info
            Log.d(TAG, "BottomSheetBehavior method " + methodName + " not available in this version");
        } catch (Exception e) {
            Log.d(TAG, "Failed to set BottomSheetBehavior property " + methodName + " (non-critical): " + e.getMessage());
        }
    }

    /**
     * Adds a callback to the BottomSheetBehavior using reflection
     */
    private void addBottomSheetCallback(View bottomSheet) {
        try {
            if (bottomSheetBehavior == null) return;

            // Try to find the callback class - handle different versions gracefully
            Class<?> callbackClass = null;
            try {
                callbackClass = Class.forName("com.google.android.material.bottomsheet.BottomSheetBehavior$BottomSheetCallback");
            } catch (ClassNotFoundException e) {
                // Try alternative class name for older versions
                try {
                    callbackClass = Class.forName("com.google.android.material.bottomsheet.BottomSheetBehavior$BottomSheetCallback");
                } catch (ClassNotFoundException e2) {
                    Log.d(TAG, "BottomSheetCallback class not found, skipping callback registration");
                    return;
                }
            }

            // Verify it's an interface before creating proxy
            if (!callbackClass.isInterface()) {
                Log.d(TAG, "BottomSheetCallback is not an interface, skipping callback registration");
                return;
            }

            Object callback = java.lang.reflect.Proxy.newProxyInstance(
                callbackClass.getClassLoader(),
                new Class[]{callbackClass},
                (proxy, method, args) -> {
                    if ("onStateChanged".equals(method.getName()) && args.length >= 2) {
                        int newState = (Integer) args[1];

                        if (newState == 5) { // STATE_HIDDEN = 5
                            // Auto-dismiss when hidden (card drag-to-dismiss)
                            dismissBottomSheetDialog();
                        }
                    } else if ("onSlide".equals(method.getName()) && args.length >= 2) {
                        float slideOffset = (Float) args[1];
                        // Optional: Can add slide animations here for enhanced UX
                        // Responsive animations during drag
                    }
                    return null;
                }
            );

            // Add the callback
            java.lang.reflect.Method addCallbackMethod = bottomSheetBehavior.getClass().getMethod("addBottomSheetCallback", callbackClass);
            addCallbackMethod.invoke(bottomSheetBehavior, callback);
        } catch (Exception e) {
            Log.d(TAG, "Failed to add BottomSheetBehavior callback (non-critical): " + e.getMessage());
        }
    }

    /**
     * Dismisses BottomSheetDialog using reflection
     */
    private void dismissBottomSheetDialog() {
        try {
            if (currentBottomSheetDialog != null) {
                // Check if showing first
                java.lang.reflect.Method isShowingMethod = currentBottomSheetDialog.getClass().getMethod("isShowing");
                Boolean isShowing = (Boolean) isShowingMethod.invoke(currentBottomSheetDialog);

                if (isShowing != null && isShowing) {
                    java.lang.reflect.Method dismissMethod = currentBottomSheetDialog.getClass().getMethod("dismiss");
                    dismissMethod.invoke(currentBottomSheetDialog);
                }
            }
        } catch (Exception e) {
            Log.e(TAG, "Failed to dismiss BottomSheetDialog: " + e.getMessage());
        }
    }

    /**
     * Creates card-style close button for the card
     */
    private Button createCardStyleCloseButton() {
        Button closeButton = new Button(activity);

        // Card close button text and styling
        closeButton.setText("✕");
        closeButton.setTextSize(18);
        closeButton.setTextColor(getThemeSecondaryTextColor()); // Theme-aware secondary text color
        closeButton.setTypeface(null, android.graphics.Typeface.NORMAL);

        // Center the icon perfectly
        closeButton.setGravity(Gravity.CENTER);
        closeButton.setPadding(0, 0, 0, 0);

        // Create subtle card-style button background
        GradientDrawable buttonBackground = new GradientDrawable();
        buttonBackground.setColor(getThemeButtonBackgroundColor()); // Theme-aware button background
        buttonBackground.setCornerRadius(dpToPx(20)); // Perfect circle
        // Subtle border matching card style
        buttonBackground.setStroke(dpToPx(1), getThemeBorderColor()); // Theme-aware border
        closeButton.setBackground(buttonBackground);

        // Subtle elevation
        if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.LOLLIPOP) {
            closeButton.setElevation(dpToPx(6));
        }

        // Position in top-right corner of card - moved higher to avoid input field obstruction
        FrameLayout.LayoutParams buttonParams = new FrameLayout.LayoutParams(
            dpToPx(36), // Slightly smaller to reduce obstruction
            dpToPx(36)
        );
        buttonParams.gravity = Gravity.TOP | Gravity.END;
        buttonParams.setMargins(0, dpToPx(12), dpToPx(12), 0); // Moved higher and closer to edge
        closeButton.setLayoutParams(buttonParams);

        // Set click listener to dismiss the card
        closeButton.setOnClickListener(v -> {
            if (currentBottomSheetDialog != null) {
                dismissCardDrawer();
            } else if (currentDialog != null) {
                dismissCustomCardDialog();
            }
        });

        return closeButton;
    }

    /**
     * Creates card-style home button for returning to the initial URL
     */
    private void setupHomeButton(Button homeButton) {
        // Use a simple home glyph. If emoji rendering varies, replace with "⌂" or "🏠"
        homeButton.setText("⌂");
        homeButton.setTextSize(18);
        homeButton.setTextColor(getThemeSecondaryTextColor()); // Theme-aware secondary text color
        homeButton.setTypeface(null, android.graphics.Typeface.NORMAL);
        homeButton.setGravity(Gravity.CENTER);
        homeButton.setPadding(0, 0, 0, 0);

        GradientDrawable buttonBackground = new GradientDrawable();
        buttonBackground.setColor(getThemeButtonBackgroundColor()); // Theme-aware button background
        buttonBackground.setCornerRadius(dpToPx(20));
        buttonBackground.setStroke(dpToPx(1), getThemeBorderColor()); // Theme-aware border
        homeButton.setBackground(buttonBackground);

        if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.LOLLIPOP) {
            homeButton.setElevation(dpToPx(6));
        }

        FrameLayout.LayoutParams buttonParams = new FrameLayout.LayoutParams(
            dpToPx(36), // Slightly smaller to reduce obstruction
            dpToPx(36)
        );
        buttonParams.gravity = Gravity.TOP | Gravity.START;
        buttonParams.setMargins(dpToPx(12), dpToPx(12), 0, 0); // Moved higher and closer to edge
        homeButton.setLayoutParams(buttonParams);

        homeButton.setOnClickListener(v -> navigateHome());
    }

    /**
     * Navigate back to the initially opened URL
     */
    private void navigateHome() {
        if (activity == null || webView == null) return;
        final String urlToLoad = initialURL;
        if (urlToLoad == null || urlToLoad.isEmpty()) return;
        activity.runOnUiThread(() -> {
            try {
                webView.loadUrl(urlToLoad);
            } catch (Exception e) {
                Log.e(TAG, "Failed to navigate home: " + e.getMessage());
            }
        });
    }

    /**
     * Dismisses the card drawer
     */
    public void dismissCardDrawer() {
        if (activity != null) {
            activity.runOnUiThread(() -> {
                try {
                    dismissBottomSheetDialog();
                } catch (Exception e) {
                    Log.e(TAG, "Error dismissing card drawer: " + e.getMessage());
                    cleanupAllViews();
                }
            });
        }
    }
    private void configureDialogWindow() {
        Window window = currentDialog.getWindow();
        if (window == null) return;

        // Set transparent background
        window.setBackgroundDrawable(new ColorDrawable(Color.TRANSPARENT));

        // Get screen dimensions
        DisplayMetrics metrics = new DisplayMetrics();
        activity.getWindowManager().getDefaultDisplay().getMetrics(metrics);

        int screenWidth = metrics.widthPixels;
        int screenHeight = metrics.heightPixels;

        // Calculate dialog dimensions based on configuration
        int dialogWidth = (int) (screenWidth * cardWidthRatio);
        int dialogHeight = (int) (screenHeight * cardHeightRatio);

        // Set dialog size
        WindowManager.LayoutParams layoutParams = window.getAttributes();
        layoutParams.width = dialogWidth;
        layoutParams.height = dialogHeight;

        // Set position based on cardVerticalPosition
        layoutParams.gravity = Gravity.CENTER_HORIZONTAL;
        if (cardVerticalPosition <= 0.1f) {
            // Top
            layoutParams.gravity |= Gravity.TOP;
            layoutParams.y = dpToPx(20);
        } else if (cardVerticalPosition >= 0.9f) {
            // Bottom
            layoutParams.gravity |= Gravity.BOTTOM;
            layoutParams.y = dpToPx(20);
        } else {
            // Center
            layoutParams.gravity |= Gravity.CENTER_VERTICAL;
        }

        window.setAttributes(layoutParams);

        // Add dim background
        window.addFlags(WindowManager.LayoutParams.FLAG_DIM_BEHIND);
        window.setDimAmount(0.4f);
    }
    private void injectPaymentCallbackScript() {
        if (webView == null) return;

        // JavaScript to set up window.stash_sdk functions
        String stashSDKScript =
            "(function() {" +
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
            "})();";

        // Inject the SDK functions immediately
        webView.evaluateJavascript(stashSDKScript, null);

        // Also include fallback detection for legacy websites that don't use the SDK
        String fallbackScript =
            "(function() {" +
            "  // Listen for success indicators" +
            "  function checkForSuccess() {" +
            "    var successIndicators = [" +
            "      'payment-success', 'payment_success', 'success'," +
            "      'thank-you', 'confirmation', 'completed'" +
            "    ];" +
            "    " +
            "    var bodyText = document.body.textContent.toLowerCase();" +
            "    var currentUrl = window.location.href.toLowerCase();" +
            "    " +
            "    for (var i = 0; i < successIndicators.length; i++) {" +
            "      if (bodyText.indexOf(successIndicators[i]) !== -1 || " +
            "         currentUrl.indexOf(successIndicators[i]) !== -1) {" +
            "        try {" +
            "          StashAndroid.onPaymentSuccess();" +
            "        } catch(e) {}" +
            "        return true;" +
            "      }" +
            "    }" +
            "    return false;" +
            "  }" +
            "  " +
            "  // Listen for failure indicators" +
            "  function checkForFailure() {" +
            "    var failureIndicators = [" +
            "      'payment-failed', 'payment_failed', 'failed'," +
            "      'error', 'declined', 'cancelled'" +
            "    ];" +
            "    " +
            "    var bodyText = document.body.textContent.toLowerCase();" +
            "    var currentUrl = window.location.href.toLowerCase();" +
            "    " +
            "    for (var i = 0; i < failureIndicators.length; i++) {" +
            "      if (bodyText.indexOf(failureIndicators[i]) !== -1 || " +
            "         currentUrl.indexOf(failureIndicators[i]) !== -1) {" +
            "        try {" +
            "          StashAndroid.onPaymentFailure();" +
            "        } catch(e) {}" +
            "        return true;" +
            "      }" +
            "    }" +
            "    return false;" +
            "  }" +
            "  " +
            "  // Check immediately and set up polling (as fallback)" +
            "  if (!checkForSuccess() && !checkForFailure()) {" +
            "    var checkInterval = setInterval(function() {" +
            "      if (checkForSuccess() || checkForFailure()) {" +
            "        clearInterval(checkInterval);" +
            "      }" +
            "    }, 1000);" +
            "  }" +
            "})();";

        // Inject fallback script as well
        webView.evaluateJavascript(fallbackScript, null);
    }

    /**
     * Dismisses the current dialog (works for both regular dialog and card drawer)
     */
    public void dismissDialog() {
        if (activity != null) {
            activity.runOnUiThread(() -> {
                try {
                    if (useCardDrawer) {
                        dismissCardDrawer();
                    } else if (currentDialog != null && currentDialog.isShowing()) {
                        currentDialog.dismiss();
                    }
                } catch (Exception e) {
                    Log.e(TAG, "Error dismissing dialog: " + e.getMessage());
                    // Force cleanup if dismiss fails
                    cleanupAllViews();
                }
            });
        }
    }

    /**
     * Sets whether to use the card drawer experience (default: true)
     */
    public void setUseCardDrawer(boolean useCardDrawer) {
        this.useCardDrawer = useCardDrawer;
    }

    /**
     * Gets whether card drawer experience is enabled
     */
    public boolean getUseCardDrawer() {
        return useCardDrawer;
    }
    private void cleanupAllViews() {
        try {
            // Dismiss dialog first
            if (currentDialog != null) {
                if (currentDialog.isShowing()) {
                    currentDialog.dismiss();
                }
                currentDialog = null;
            }

            // Dismiss bottom sheet dialog using reflection
            if (currentBottomSheetDialog != null) {
                try {
                    java.lang.reflect.Method isShowingMethod = currentBottomSheetDialog.getClass().getMethod("isShowing");
                    Boolean isShowing = (Boolean) isShowingMethod.invoke(currentBottomSheetDialog);
                    if (isShowing != null && isShowing) {
                        java.lang.reflect.Method dismissMethod = currentBottomSheetDialog.getClass().getMethod("dismiss");
                        dismissMethod.invoke(currentBottomSheetDialog);
                    }
                } catch (Exception e) {
                    Log.e(TAG, "Error dismissing BottomSheetDialog: " + e.getMessage());
                }
                currentBottomSheetDialog = null;
                bottomSheetBehavior = null;
            }

            // Clean up WebView
            if (webView != null) {
                try {
                    if (webView.getParent() != null) {
                        ((ViewGroup) webView.getParent()).removeView(webView);
                    }
                    webView.stopLoading();
                    webView.destroy();
                } catch (Exception e) {
                    Log.e(TAG, "Error cleaning up WebView: " + e.getMessage());
                }
                webView = null;
            }

            // Clean up close button
            if (currentCloseButton != null) {
                try {
                    if (currentCloseButton.getParent() != null) {
                        ((ViewGroup) currentCloseButton.getParent()).removeView(currentCloseButton);
                    }
                } catch (Exception e) {
                    Log.e(TAG, "Error cleaning up close button: " + e.getMessage());
                }
                currentCloseButton = null;
            }

            // Clean up back button
            if (currentBackButton != null) {
                try {
                    if (currentBackButton.getParent() != null) {
                        ((ViewGroup) currentBackButton.getParent()).removeView(currentBackButton);
                    }
                } catch (Exception e) {
                    Log.e(TAG, "Error cleaning up back button: " + e.getMessage());
                }
                currentBackButton = null;
            }

            // Clean up home button
            if (currentHomeButton != null) {
                try {
                    if (currentHomeButton.getParent() != null) {
                        ((ViewGroup) currentHomeButton.getParent()).removeView(currentHomeButton);
                    }
                } catch (Exception e) {
                    Log.e(TAG, "Error cleaning up home button: " + e.getMessage());
                }
                currentHomeButton = null;
            }

            // Clean up back click target
            if (currentBackClickTarget != null) {
                try {
                    if (currentBackClickTarget.getParent() != null) {
                        ((ViewGroup) currentBackClickTarget.getParent()).removeView(currentBackClickTarget);
                    }
                } catch (Exception e) {
                    Log.e(TAG, "Error cleaning up back click target: " + e.getMessage());
                }
                currentBackClickTarget = null;
            }

            // Clean up container
            if (currentContainer != null) {
                try {
                    if (currentContainer.getParent() != null) {
                        ((ViewGroup) currentContainer.getParent()).removeView(currentContainer);
                    }
                    currentContainer.removeAllViews();
                } catch (Exception e) {
                    Log.e(TAG, "Error cleaning up container: " + e.getMessage());
                }
                currentContainer = null;
            }

            // Clean up controls overlay
            if (currentControlsOverlay != null) {
                try {
                    if (currentControlsOverlay.getParent() != null) {
                        ((ViewGroup) currentControlsOverlay.getParent()).removeView(currentControlsOverlay);
                    }
                } catch (Exception e) {
                    Log.e(TAG, "Error cleaning up controls overlay: " + e.getMessage());
                }
                currentControlsOverlay = null;
            }
        } catch (Exception e) {
            Log.e(TAG, "Error during cleanup: " + e.getMessage());
        }

        // Reset expansion state for next use
        isExpanded = false;

        // Reset processing flag
        isPurchaseProcessing = false;

        // Clear initial URL
        initialURL = null;
    }
    private void setupContainerAppearance(FrameLayout container) {
        // Create shadow background with modern card appearance - no borders
        GradientDrawable background = new GradientDrawable();
        background.setColor(Color.TRANSPARENT); // Transparent background
        background.setCornerRadius(dpToPx(16));

        // Add elevation/shadow for modern look
        if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.LOLLIPOP) {
            container.setElevation(dpToPx(12));
        }
        container.setBackground(background);

        // No padding - let WebView fill the container
        container.setPadding(0, 0, 0, 0);
    }

    /**
     * Sets up WebView for larger card-style layout with explicit visibility
     */
    private void setupCardWebViewLarger(WebView webView, String url) {
        // Configure WebView settings for optimal performance
        WebSettings settings = webView.getSettings();
        settings.setJavaScriptEnabled(true);
        settings.setDomStorageEnabled(true);
        settings.setLoadWithOverviewMode(true);
        settings.setUseWideViewPort(true);
        settings.setBuiltInZoomControls(false);
        settings.setDisplayZoomControls(false);
        settings.setSupportZoom(false);
        settings.setAllowFileAccess(false);
        settings.setAllowContentAccess(false);
        settings.setAllowFileAccessFromFileURLs(false);
        settings.setAllowUniversalAccessFromFileURLs(false);

        // Enable popup window support for PayPal/Klarna target="_blank" links
        settings.setJavaScriptCanOpenWindowsAutomatically(true);
        settings.setSupportMultipleWindows(true);

        // Set WebView client
        webView.setWebViewClient(new WebViewClient() {
            @Override
            public boolean shouldOverrideUrlLoading(WebView view, String url) {
                // Check if URL requires back button (PayPal, Klarna, Stripe)
                handleProviderButtons(url);
                return false;
            }
            @Override
            public void onPageStarted(WebView view, String url, android.graphics.Bitmap favicon) {
                super.onPageStarted(view, url, favicon);

                // Keep webview hidden while loading to prevent screen flashing
                view.setVisibility(View.INVISIBLE);
            }
            @Override
            public void onPageFinished(WebView view, String url) {
                super.onPageFinished(view, url);

            // Check if current page requires provider controls
                handleProviderButtons(url);

                injectPaymentCallbackScript();

                // Wait a bit to ensure page is fully rendered before showing
                view.postDelayed(new Runnable() {
                    @Override
                    public void run() {
                        view.setVisibility(View.VISIBLE);
                    }
                }, 300); // 300ms delay to ensure content is rendered
            }
        });

        // Set WebChrome client with popup window support
        webView.setWebChromeClient(new WebChromeClient() {
            @Override
            public boolean onCreateWindow(WebView view, boolean isDialog, boolean isUserGesture, android.os.Message resultMsg) {
                // Handle popup windows (PayPal/Klarna target="_blank" links)
                // Instead of creating a new window, we'll handle the navigation in the same WebView
                // This prevents popup windows while still allowing the payment flow to work
                WebView.WebViewTransport transport = (WebView.WebViewTransport) resultMsg.obj;
                transport.setWebView(view);
                resultMsg.sendToTarget();
                return true;
            }
        });

        // Add JavaScript interface
        webView.addJavascriptInterface(new StashJavaScriptInterface(), "StashAndroid");

        // Add touch listener to ensure proper focus handling
        webView.setOnTouchListener(new View.OnTouchListener() {
            @Override
            public boolean onTouch(View v, MotionEvent event) {
                if (event.getAction() == MotionEvent.ACTION_DOWN) {
                    // Request focus when user touches the WebView
                    v.requestFocus();
                }
                return false; // Don't consume the touch event
            }
        });

        // Use MATCH_PARENT for height to allow seamless expansion with card
        FrameLayout.LayoutParams params = new FrameLayout.LayoutParams(
            FrameLayout.LayoutParams.MATCH_PARENT,
            FrameLayout.LayoutParams.MATCH_PARENT // Allow webview to expand with card
        );

        // Fill entire card - no margins at all
        params.topMargin = 0;      // No top margin - fill from top
        params.bottomMargin = 0;   // No bottom margin - fill to bottom
        params.leftMargin = 0;     // No left margin - fill edge to edge
        params.rightMargin = 0;    // No right margin - fill edge to edge
        webView.setLayoutParams(params);

        // Ensure WebView is properly configured for input handling
        webView.setVisibility(View.INVISIBLE);
        webView.setFocusable(true);
        webView.setFocusableInTouchMode(true);
        
        // Ensure content clipping works properly
        webView.setClipChildren(true);
        webView.setClipToPadding(true);
        
        // Force hardware acceleration for better clipping performance
        if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.HONEYCOMB) {
            webView.setLayerType(View.LAYER_TYPE_HARDWARE, null);
        }
        
        // Ensure WebView can receive input events properly
        webView.setClickable(true);
        webView.setLongClickable(true);

        // Set clean theme-aware background to match card
        webView.setBackgroundColor(getThemeBackgroundColor());

        // Apply corner radius to WebView - ONLY top corners for card style
        if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.LOLLIPOP) {
            // Use clipToOutline for API 21+ to properly clip webview content to rounded corners
            webView.setOutlineProvider(new ViewOutlineProvider() {
                @Override
                public void getOutline(View view, Outline outline) {
                    // Rounded corners for WebView content area - match card corner radius
                    outline.setRoundRect(0, 0, view.getWidth(), view.getHeight(), dpToPx(25));
                }
            });
            webView.setClipToOutline(true);
        } else {
            // Fallback for older Android versions
            GradientDrawable background = new GradientDrawable();
            background.setColor(getThemeBackgroundColor());
            background.setCornerRadii(new float[]{
                dpToPx(25), dpToPx(25), // top-left corner - rounded
                dpToPx(25), dpToPx(25), // top-right corner - rounded
                0, 0,                   // bottom-right corner - square (card style)
                0, 0                    // bottom-left corner - square (card style)
            });
            webView.setBackground(background);
        }

        // Add elevation for modern look
        if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.LOLLIPOP) {
            webView.setElevation(dpToPx(2)); // Slightly lower than drag handle
        }

        // Force visibility and load URL
        webView.setVisibility(View.INVISIBLE);

        webView.loadUrl(url);
        
        // Ensure proper input focus after a short delay
        webView.postDelayed(() -> ensureWebViewInputFocus(webView), 500);
    }

    /**
     * Apply rounded corners specifically for card WebView
     */
    private void applyCardRoundedCornersToWebView(WebView webView) {
        if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.LOLLIPOP) {
            // Use clipToOutline for API 21+ with card-specific radius
            webView.setOutlineProvider(new ViewOutlineProvider() {
                @Override
                public void getOutline(View view, Outline outline) {
                    // Rounded corners for WebView content area - match card corner radius
                    outline.setRoundRect(0, 0, view.getWidth(), view.getHeight(), dpToPx(25));
                }
            });
            webView.setClipToOutline(true);
        } else {
            // Fallback for older Android versions
            GradientDrawable background = new GradientDrawable();
            background.setColor(getThemeBackgroundColor());
            background.setCornerRadius(dpToPx(25));
            webView.setBackground(background);
        }
    }
    private void setupWebView(WebView webView, String url) {
        // Configure WebView settings
        WebSettings settings = webView.getSettings();
        settings.setJavaScriptEnabled(true);
        settings.setDomStorageEnabled(true);
        settings.setLoadWithOverviewMode(true);
        settings.setUseWideViewPort(true);
        settings.setBuiltInZoomControls(false);
        settings.setDisplayZoomControls(false);
        settings.setSupportZoom(false);
        settings.setAllowFileAccess(false);
        settings.setAllowContentAccess(false);
        settings.setAllowFileAccessFromFileURLs(false);
        settings.setAllowUniversalAccessFromFileURLs(false);

        // Enable popup window support for PayPal/Klarna target="_blank" links
        settings.setJavaScriptCanOpenWindowsAutomatically(true);
        settings.setSupportMultipleWindows(true);

        // Apply rounded corners to WebView
        applyRoundedCornersToWebView(webView);

        // Set WebView client
        webView.setWebViewClient(new WebViewClient() {
            @Override
            public boolean shouldOverrideUrlLoading(WebView view, String url) {
                // Check if URL requires back button (PayPal, Klarna, Stripe)
                handleProviderButtons(url);
                return false;
            }
            @Override
            public void onPageStarted(WebView view, String url, android.graphics.Bitmap favicon) {
                super.onPageStarted(view, url, favicon);

                // Keep webview hidden while loading to prevent screen flashing
                view.setVisibility(View.INVISIBLE);
            }
            @Override
            public void onPageFinished(WebView view, String url) {
                super.onPageFinished(view, url);

            // Check if current page requires provider controls
                handleProviderButtons(url);

                injectPaymentCallbackScript();

                // Wait a bit to ensure page is fully rendered before showing
                view.postDelayed(new Runnable() {
                    @Override
                    public void run() {
                        view.setVisibility(View.VISIBLE);
                    }
                }, 300); // 300ms delay to ensure content is rendered
            }
        });

        // Set WebChrome client with popup window support
        webView.setWebChromeClient(new WebChromeClient() {
            @Override
            public boolean onCreateWindow(WebView view, boolean isDialog, boolean isUserGesture, android.os.Message resultMsg) {
                // Handle popup windows (PayPal/Klarna target="_blank" links)
                // Instead of creating a new window, we'll handle the navigation in the same WebView
                // This prevents popup windows while still allowing the payment flow to work
                WebView.WebViewTransport transport = (WebView.WebViewTransport) resultMsg.obj;
                transport.setWebView(view);
                resultMsg.sendToTarget();
                return true;
            }
        });

        // Add JavaScript interface
        webView.addJavascriptInterface(new StashJavaScriptInterface(), "StashAndroid");

        // Configure layout with padding for drag handle in card drawer
        FrameLayout.LayoutParams params = new FrameLayout.LayoutParams(
            FrameLayout.LayoutParams.MATCH_PARENT,
            FrameLayout.LayoutParams.MATCH_PARENT
        );

        // Add top margin for card drawer to account for drag handle and header
        if (useCardDrawer) {
            params.topMargin = dpToPx(32); // Space for drag handle area
            params.bottomMargin = dpToPx(16); // Bottom spacing
            params.leftMargin = dpToPx(8);   // Side margins for card appearance
            params.rightMargin = dpToPx(8);
        }

        webView.setLayoutParams(params);

        // Load URL
        webView.loadUrl(url);
    }
    private void setupCloseButton(Button closeButton) {
        closeButton.setText("✕");
        closeButton.setTextSize(18);
        closeButton.setTextColor(Color.parseColor("#FFFFFF")); // White text for better contrast
        // Center the icon perfectly
        closeButton.setGravity(Gravity.CENTER);
        closeButton.setPadding(0, 0, 0, 0);

        // Create modern circular background with semi-transparent overlay
        GradientDrawable background = new GradientDrawable();
        background.setColor(Color.parseColor("#80000000")); // Semi-transparent black
        background.setCornerRadius(dpToPx(22));
        closeButton.setBackground(background);

        // Add elevation for modern look
        if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.LOLLIPOP) {
            closeButton.setElevation(dpToPx(6)); // Slightly higher elevation
        }

        // Set layout params (top-right corner)
        FrameLayout.LayoutParams params = new FrameLayout.LayoutParams(
            dpToPx(44),
            dpToPx(44)
        );
        params.gravity = Gravity.TOP | Gravity.END;
        params.setMargins(0, dpToPx(16), dpToPx(16), 0); // Slightly more margin
        closeButton.setLayoutParams(params);

        // Set click listener
        closeButton.setOnClickListener(v -> dismissDialog());
    }

    /**
     * Apply rounded corners to WebView using clipping
     */
    private void applyRoundedCornersToWebView(WebView webView) {
        if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.LOLLIPOP) {
            // Use clipToOutline for API 21+ with card-specific radius
            webView.setOutlineProvider(new ViewOutlineProvider() {
                @Override
                public void getOutline(View view, Outline outline) {
                    // Rounded corners for WebView content area - match card corner radius
                    outline.setRoundRect(0, 0, view.getWidth(), view.getHeight(), dpToPx(25));
                }
            });
            webView.setClipToOutline(true);
        } else {
            // Fallback: Use a rounded background drawable for older versions
            GradientDrawable background = new GradientDrawable();
            background.setColor(getThemeBackgroundColor());
            background.setCornerRadius(dpToPx(25));
            webView.setBackground(background);
        }
    }

    /**
     * Chrome Custom Tabs implementation (Android equivalent of Safari WebView)
     * Falls back to default browser if Chrome Custom Tabs is not available
     */
    private void openWithChromeCustomTabs(String url) {
        try {
            // Try to use Chrome Custom Tabs if available
            if (isChromeCustomTabsAvailable()) {
                openWithReflectionChromeCustomTabs(url);
            } else {
                // Fallback to default browser
                openWithDefaultBrowser(url);
            }
        } catch (Exception e) {
            Log.e(TAG, "Failed to open native browser, falling back to WebView: " + e.getMessage());
            // Final fallback to WebView dialog
            createAndShowDialog(url);
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
        // Use reflection to safely access Chrome Custom Tabs
        Class<?> customTabsIntentClass = Class.forName("androidx.browser.customtabs.CustomTabsIntent");
        Class<?> builderClass = Class.forName("androidx.browser.customtabs.CustomTabsIntent$Builder");

        // Create builder instance using reflection
        Object builder = builderClass.newInstance();

        // Set toolbar color to match Stash brand
        java.lang.reflect.Method setToolbarColor = builderClass.getMethod("setToolbarColor", int.class);
        setToolbarColor.invoke(builder, Color.parseColor("#000000"));

        // Set title and URL bar settings
        java.lang.reflect.Method setShowTitle = builderClass.getMethod("setShowTitle", boolean.class);
        setShowTitle.invoke(builder, true);

        // Build the intent
        java.lang.reflect.Method build = builderClass.getMethod("build");
        Object customTabsIntent = build.invoke(builder);

        // Launch Chrome Custom Tabs
        java.lang.reflect.Method launchUrl = customTabsIntentClass.getMethod("launchUrl", android.content.Context.class, Uri.class);
        launchUrl.invoke(customTabsIntent, activity, Uri.parse(url));

        isCurrentlyPresented = true;

        // Notify Unity of dismissal after delay
        new Handler(Looper.getMainLooper()).postDelayed(() -> {
            UnityPlayer.UnitySendMessage(unityGameObjectName, "OnAndroidDialogDismissed", "");
        }, 1000);
    }
    private void openWithDefaultBrowser(String url) {
        // Fallback to system default browser
        Intent browserIntent = new Intent(Intent.ACTION_VIEW, Uri.parse(url));
        browserIntent.addFlags(Intent.FLAG_ACTIVITY_NEW_TASK);

        activity.startActivity(browserIntent);
        isCurrentlyPresented = true;

        // Notify Unity of dismissal after delay
        new Handler(Looper.getMainLooper()).postDelayed(() -> {
            UnityPlayer.UnitySendMessage(unityGameObjectName, "OnAndroidDialogDismissed", "");
        }, 1000);
    }

    /**
     * Sets card configuration
     */
    public void setCardConfiguration(float heightRatio, float verticalPosition, float widthRatio) {
        this.cardHeightRatio = heightRatio;
        this.cardVerticalPosition = verticalPosition;
        this.cardWidthRatio = widthRatio;
    }

    /**
     * Sets whether to force Chrome Custom Tabs instead of WebView dialog
     */
    public void setForceSafariViewController(boolean force) {
        this.forceSafariViewController = force;
    }

    /**
     * Gets whether Chrome Custom Tabs is forced
     */
    public boolean getForceSafariViewController() {
        return forceSafariViewController;
    }

    /**
     * Checks if a card is currently presented
     */
    public boolean isCurrentlyPresented() {
        return isCurrentlyPresented;
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
     * Hides the close button (called during purchase processing)
     */
    private void hideCloseButton() {
        if (currentCloseButton != null && activity != null) {
            activity.runOnUiThread(() -> {
                currentCloseButton.setVisibility(View.GONE);
            });
        }
    }

    /**
     * Shows the close button (called when processing completes)
     */
    private void showCloseButton() {
        if (currentCloseButton != null && activity != null) {
            activity.runOnUiThread(() -> {
                currentCloseButton.setVisibility(View.VISIBLE);
            });
        }
    }

    /**
     * Shows back button for PayPal/Klarna pages
     */
    private void showBackButton() {
        // Back button disabled by request – ensure removed if present
        hideBackButton();
    }

    /**
     * Expands the tappable area of a child view using TouchDelegate while keeping visuals unchanged
     */
    private void expandViewHitRect(View parent, View child, int extraPx) {
        if (parent == null || child == null) return;
        parent.post(() -> {
            try {
                Rect rect = new Rect();
                child.getHitRect(rect);
                rect.top -= extraPx;
                rect.bottom += extraPx;
                rect.left -= extraPx;
                rect.right += extraPx;
                TouchDelegate delegate = new TouchDelegate(rect, child);
                if (parent instanceof View) {
                    ((View) parent).setTouchDelegate(delegate);
                }
            } catch (Exception e) {
                Log.e(TAG, "Failed to expand hit rect: " + e.getMessage());
            }
        });
    }

    /**
     * Hides back button
     */
    private void hideBackButton() {
        if (activity == null) return;
        activity.runOnUiThread(() -> {
            try {
                if (currentBackButton != null) {
                    ViewParent p = currentBackButton.getParent();
                    if (p instanceof ViewGroup) {
                        ((ViewGroup) p).removeView(currentBackButton);
                    }
                    currentBackButton = null;
                }
                if (currentBackClickTarget != null) {
                    ViewParent p2 = currentBackClickTarget.getParent();
                    if (p2 instanceof ViewGroup) {
                        ((ViewGroup) p2).removeView(currentBackClickTarget);
                    }
                    currentBackClickTarget = null;
                }
                // Re-enable drag handle input
                if (currentDragHandleArea != null) {
                    currentDragHandleArea.setClickable(true);
                    currentDragHandleArea.setOnTouchListener(null);
                }
            } catch (Exception e) {
                Log.e(TAG, "Error removing back button: " + e.getMessage());
            }
        });
    }

    /**
     * Shows/hides the back and home buttons based on provider domain presence
     */
    private void handleProviderButtons(String url) {
        boolean isProvider = shouldShowBackButton(url);
        if (activity == null) return;
        activity.runOnUiThread(() -> {
            try {
                // Show/hide back button based on provider
                if (isProvider) {
                    showBackButton();
                } else {
                    hideBackButton();
                }

                // Show/hide home button based on provider (keep existing logic)
                if (currentHomeButton != null) {
                    currentHomeButton.setVisibility(isProvider ? View.VISIBLE : View.GONE);
                }
            } catch (Exception e) {
                Log.e(TAG, "Error toggling provider buttons: " + e.getMessage());
            }
        });
    }

    /**
     * Creates back button for PayPal/Klarna pages - EXACT COPY OF CLOSE BUTTON PATTERN
     */
    private Button createBackButton() {
        Button backButton = new Button(activity);

        // Card back button text and styling - use left arrow
        backButton.setText("←");
        backButton.setTextSize(18);
        backButton.setTextColor(getThemeSecondaryTextColor()); // Theme-aware secondary text color
        backButton.setTypeface(null, android.graphics.Typeface.NORMAL);

        // Center the icon perfectly
        backButton.setGravity(Gravity.CENTER);
        backButton.setPadding(0, 0, 0, 0);

        // Create subtle card-style button background
        GradientDrawable buttonBackground = new GradientDrawable();
        buttonBackground.setColor(getThemeButtonBackgroundColor()); // Theme-aware button background
        buttonBackground.setCornerRadius(dpToPx(20)); // Perfect circle
        // Subtle border matching card style
        buttonBackground.setStroke(dpToPx(1), getThemeBorderColor()); // Theme-aware border
        backButton.setBackground(buttonBackground);

        // Subtle elevation
        if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.LOLLIPOP) {
            backButton.setElevation(dpToPx(6));
        }

        // Position in top-left corner of card (opposite of close button)
        FrameLayout.LayoutParams buttonParams = new FrameLayout.LayoutParams(
            dpToPx(40), // Touch-friendly size
            dpToPx(40)
        );
        buttonParams.gravity = Gravity.TOP | Gravity.START;
        buttonParams.setMargins(dpToPx(16), dpToPx(20), 0, 0); // Top and left margins
        backButton.setLayoutParams(buttonParams);

        // Set click listener to navigate back to initial URL
        backButton.setOnClickListener(v -> navigateHome());

        return backButton;
    }

    /**
     * Checks if current URL requires back button (PayPal, Klarna, Stripe)
     */
    private boolean shouldShowBackButton(String url) {
        if (url == null) {
            return false;
        }

        String lowerUrl = url.toLowerCase();
        boolean shouldShow = lowerUrl.contains("klarna") ||
                            lowerUrl.contains("paypal") ||
                            lowerUrl.contains("stripe");

        return shouldShow;
    }
    private int dpToPx(int dp) {
        DisplayMetrics metrics = activity.getResources().getDisplayMetrics();
        return Math.round(dp * metrics.density);
    }

    /**
     * Detects if the system is in dark mode
     */
    private boolean isDarkTheme() {
        if (activity == null) return false;

        if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.Q) {
            // Android 10+ (API 29+) - use UI_MODE_NIGHT_MASK
            int nightModeFlags = activity.getResources().getConfiguration().uiMode & Configuration.UI_MODE_NIGHT_MASK;
            boolean isDark = nightModeFlags == Configuration.UI_MODE_NIGHT_YES;

            return isDark;
        } else {
            // For older versions, default to light theme
            return false;
        }
    }

    /**
     * Gets the appropriate background color based on system theme
     */
    private int getThemeBackgroundColor() {
        boolean isDark = isDarkTheme();
        int color = isDark ? Color.parseColor("#1C1C1E") : Color.WHITE; // Dark mode background vs white
        return color;
    }

    /**
     * Gets the appropriate text color based on system theme
     */
    private int getThemeTextColor() {
        return isDarkTheme() ? Color.WHITE : Color.parseColor("#1C1C1E"); // White text on dark, dark text on light
    }

    /**
     * Gets the appropriate secondary text color based on system theme
     */
    private int getThemeSecondaryTextColor() {
        return isDarkTheme() ? Color.parseColor("#8E8E93") : Color.parseColor("#8E8E93"); // Secondary text color for both themes
    }

    /**
     * Gets the appropriate button background color based on system theme
     */
    private int getThemeButtonBackgroundColor() {
        return isDarkTheme() ? Color.parseColor("#2C2C2E") : Color.parseColor("#F2F2F7"); // Theme-aware button backgrounds
    }

    /**
     * Gets the appropriate border color based on system theme
     */
    private int getThemeBorderColor() {
        return isDarkTheme() ? Color.parseColor("#38383A") : Color.parseColor("#E5E5EA"); // Theme-aware border colors
    }
    
    /**
     * Ensures proper input focus handling for WebView
     */
    private void ensureWebViewInputFocus(WebView webView) {
        if (webView != null && activity != null) {
            activity.runOnUiThread(() -> {
                try {
                    // Request focus for the WebView
                    webView.requestFocus();
                    
                    // Ensure the WebView can receive input events
                    webView.setFocusable(true);
                    webView.setFocusableInTouchMode(true);
                    
                    // Force the input method to show when needed
                    android.view.inputmethod.InputMethodManager imm = 
                        (android.view.inputmethod.InputMethodManager) activity.getSystemService(Context.INPUT_METHOD_SERVICE);
                    if (imm != null) {
                        imm.showSoftInput(webView, android.view.inputmethod.InputMethodManager.SHOW_IMPLICIT);
                    }
                } catch (Exception e) {
                    Log.e(TAG, "Error ensuring WebView input focus: " + e.getMessage());
                }
            });
        }
    }
}