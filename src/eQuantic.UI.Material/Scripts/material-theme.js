/**
 * eQuantic UI Material Design 3 Theme Script
 * Handles dark mode, theme switching, and dynamic color generation
 * Served via /_equantic/material-theme.js
 */

(function() {
    'use strict';

    const STORAGE_KEY = 'equantic-material-theme';
    const DARK_CLASS = 'dark';

    /**
     * Get the current theme preference
     * @returns {'light' | 'dark' | 'system'}
     */
    function getStoredTheme() {
        try {
            return localStorage.getItem(STORAGE_KEY) || 'system';
        } catch {
            return 'system';
        }
    }

    /**
     * Save theme preference
     * @param {'light' | 'dark' | 'system'} theme 
     */
    function setStoredTheme(theme) {
        try {
            localStorage.setItem(STORAGE_KEY, theme);
        } catch {
            // Storage not available
        }
    }

    /**
     * Check if dark mode should be active
     * @param {'light' | 'dark' | 'system'} preference 
     * @returns {boolean}
     */
    function isDarkMode(preference) {
        if (preference === 'dark') return true;
        if (preference === 'light') return false;
        return window.matchMedia('(prefers-color-scheme: dark)').matches;
    }

    /**
     * Apply the theme to the document
     * @param {boolean} dark 
     */
    function applyTheme(dark) {
        if (dark) {
            document.documentElement.classList.add(DARK_CLASS);
            document.documentElement.setAttribute('data-theme', 'dark');
        } else {
            document.documentElement.classList.remove(DARK_CLASS);
            document.documentElement.setAttribute('data-theme', 'light');
        }
        console.log(`[eQuantic.UI.Material] Theme applied: ${dark ? 'dark' : 'light'}`);
    }

    /**
     * Set theme and persist preference
     * @param {'light' | 'dark' | 'system'} theme 
     */
    function setTheme(theme) {
        setStoredTheme(theme);
        applyTheme(isDarkMode(theme));
        // Dispatch custom event for components that need to react
        window.dispatchEvent(new CustomEvent('equantic:theme-change', { 
            detail: { theme, isDark: isDarkMode(theme) } 
        }));
    }

    /**
     * Toggle between light and dark mode
     */
    function toggleTheme() {
        const current = getStoredTheme();
        const isDark = isDarkMode(current);
        setTheme(isDark ? 'light' : 'dark');
    }

    /**
     * Generate Material Design 3 tonal palette from a source color
     * Uses HCT color space approximation for better perceptual results
     * @param {string} sourceHex - Hex color (e.g., '#6750A4')
     * @returns {Object} Palette object with tonal values
     */
    function generateTonalPalette(sourceHex) {
        // Parse hex to RGB
        const r = parseInt(sourceHex.slice(1, 3), 16);
        const g = parseInt(sourceHex.slice(3, 5), 16);
        const b = parseInt(sourceHex.slice(5, 7), 16);
        
        // Convert to HSL for tonal manipulation
        const hsl = rgbToHsl(r, g, b);
        
        // Generate tonal values (0-100 in steps)
        const tones = {};
        const toneSteps = [0, 10, 20, 30, 40, 50, 60, 70, 80, 90, 95, 99, 100];
        
        toneSteps.forEach(tone => {
            const rgb = hslToRgb(hsl.h, hsl.s, tone / 100);
            tones[tone] = rgbToHex(rgb.r, rgb.g, rgb.b);
        });
        
        return tones;
    }

    /**
     * Apply a custom source color to generate theme
     * @param {string} sourceHex - Hex color
     */
    function applySourceColor(sourceHex) {
        const palette = generateTonalPalette(sourceHex);
        const root = document.documentElement;
        
        // Apply primary palette
        Object.entries(palette).forEach(([tone, hex]) => {
            root.style.setProperty(`--md-ref-palette-primary${tone}`, hex);
        });
        
        console.log(`[eQuantic.UI.Material] Applied source color: ${sourceHex}`);
        
        // Store for persistence
        try {
            localStorage.setItem('equantic-material-source-color', sourceHex);
        } catch {
            // Storage not available
        }
    }

    // Color space conversion utilities
    function rgbToHsl(r, g, b) {
        r /= 255; g /= 255; b /= 255;
        const max = Math.max(r, g, b), min = Math.min(r, g, b);
        let h, s, l = (max + min) / 2;

        if (max === min) {
            h = s = 0;
        } else {
            const d = max - min;
            s = l > 0.5 ? d / (2 - max - min) : d / (max + min);
            switch (max) {
                case r: h = ((g - b) / d + (g < b ? 6 : 0)) / 6; break;
                case g: h = ((b - r) / d + 2) / 6; break;
                case b: h = ((r - g) / d + 4) / 6; break;
            }
        }
        return { h, s, l };
    }

    function hslToRgb(h, s, l) {
        let r, g, b;
        if (s === 0) {
            r = g = b = l;
        } else {
            const hue2rgb = (p, q, t) => {
                if (t < 0) t += 1;
                if (t > 1) t -= 1;
                if (t < 1/6) return p + (q - p) * 6 * t;
                if (t < 1/2) return q;
                if (t < 2/3) return p + (q - p) * (2/3 - t) * 6;
                return p;
            };
            const q = l < 0.5 ? l * (1 + s) : l + s - l * s;
            const p = 2 * l - q;
            r = hue2rgb(p, q, h + 1/3);
            g = hue2rgb(p, q, h);
            b = hue2rgb(p, q, h - 1/3);
        }
        return { 
            r: Math.round(r * 255), 
            g: Math.round(g * 255), 
            b: Math.round(b * 255) 
        };
    }

    function rgbToHex(r, g, b) {
        return '#' + [r, g, b].map(x => x.toString(16).padStart(2, '0')).join('');
    }

    // Initialize on load
    const storedTheme = getStoredTheme();
    applyTheme(isDarkMode(storedTheme));

    // Listen for system preference changes
    window.matchMedia('(prefers-color-scheme: dark)').addEventListener('change', (e) => {
        if (getStoredTheme() === 'system') {
            applyTheme(e.matches);
        }
    });

    // Restore custom source color if set
    try {
        const storedColor = localStorage.getItem('equantic-material-source-color');
        if (storedColor) {
            applySourceColor(storedColor);
        }
    } catch {
        // Storage not available
    }

    // Expose API globally
    window.eQuantic = window.eQuantic || {};
    window.eQuantic.Material = {
        setTheme,
        toggleTheme,
        getTheme: getStoredTheme,
        isDarkMode: () => isDarkMode(getStoredTheme()),
        applySourceColor,
        generateTonalPalette
    };

    console.log('[eQuantic.UI.Material] Theme script initialized');
})();
