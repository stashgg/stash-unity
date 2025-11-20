package com.stash.popup;

import android.view.animation.Interpolator;

/**
 * Spring-like interpolator that closely matches iOS spring animations with damping 0.85.
 * This creates fluid, natural motion similar to iOS UIView spring animations.
 * Uses a damped harmonic oscillator model to simulate spring physics.
 */
public class SpringInterpolator implements Interpolator {
    private static final float DAMPING = 0.85f;
    private static final float STIFFNESS = 400f; // Increased for more responsive feel
    private static final float MASS = 1f;
    
    // Pre-calculated values for performance
    private static final float OMEGA = (float) Math.sqrt(STIFFNESS / MASS);
    private static final float DAMPED_OMEGA = OMEGA * (float) Math.sqrt(1 - DAMPING * DAMPING);
    
    @Override
    public float getInterpolation(float input) {
        if (input <= 0f) return 0f;
        if (input >= 1f) return 1f;
        
        // Spring physics simulation (damped harmonic oscillator)
        // Equation: x(t) = 1 - e^(-ζωt) * (cos(ωd*t) + (ζω/ωd) * sin(ωd*t))
        // where ζ = damping, ω = natural frequency, ωd = damped frequency
        float t = input;
        float expTerm = (float) Math.exp(-DAMPING * OMEGA * t);
        float cosTerm = (float) Math.cos(DAMPED_OMEGA * t);
        float sinTerm = (float) Math.sin(DAMPED_OMEGA * t);
        float dampingRatio = DAMPING * OMEGA / DAMPED_OMEGA;
        
        float result = 1f - expTerm * (cosTerm + dampingRatio * sinTerm);
        
        // Clamp to valid range
        return Math.max(0f, Math.min(1f, result));
    }
}

