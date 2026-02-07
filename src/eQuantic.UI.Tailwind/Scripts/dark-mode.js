// eQuantic UI Dark Mode Script
// Served via /_equantic/dark-mode.js

(function() {
    // Apply dark mode based on system preference
    if (window.matchMedia('(prefers-color-scheme: dark)').matches) {
        console.log('[eQuantic.UI] Dark mode detected and applied');
        document.documentElement.classList.add('dark');
    } else {
        console.log('[eQuantic.UI] Light mode detected');
    }

    // Listen for changes in system preference
    window.matchMedia('(prefers-color-scheme: dark)').addEventListener('change', (e) => {
        if (e.matches) {
            document.documentElement.classList.add('dark');
        } else {
            document.documentElement.classList.remove('dark');
        }
    });
})();
