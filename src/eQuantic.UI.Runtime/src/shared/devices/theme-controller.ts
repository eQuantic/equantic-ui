/**
 * The web realization of C# `IThemeController` — the app's hand on the light/dark switch.
 *
 * The whole web palette is authored as `light-dark()` pairs under `color-scheme: light dark`
 * (see C# TokenCss), so the browser already owns the swap: forcing a mode is ONE declaration on
 * the root element, and every token — custom property, atomic class, SSR-baked value — resolves
 * to the other half in the same paint. No re-render, no theme rebuild.
 */
export class WebThemeController {
  /** 'light' | 'dark' — the forced mode if one is set, else what the OS prefers. */
  get mode(): string {
    const forced = document.documentElement.style.colorScheme;
    if (forced === 'dark' || forced === 'light') return forced;
    return typeof window.matchMedia === 'function' &&
      window.matchMedia('(prefers-color-scheme: dark)').matches
      ? 'dark'
      : 'light';
  }

  apply(mode: string): void {
    const resolved = mode === 'dark' ? 'dark' : 'light';
    document.documentElement.style.colorScheme = resolved;
    // …and the flag anything CSS cannot express through color-scheme reads: a themed image PAIR
    // (Image.DarkSource) picks its artwork from this, because no CSS function switches a URL.
    document.documentElement.setAttribute('data-theme', resolved);
  }
}
