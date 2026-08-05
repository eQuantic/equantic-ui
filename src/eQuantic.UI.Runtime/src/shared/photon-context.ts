/**
 * The ambient Photon context on the client: which theme transpiled shared components read through
 * `context.theme`, and the `LoweringContext` the vocabulary's self-lowering `render()` uses. Defaults
 * to the generated `photonTheme` — an app only calls `setPhotonTheme` to swap a custom theme in.
 */

import type { LoweringContext } from './lowering';
import type { AppTheme } from './value-types';
import { photonTheme } from './design-system.generated';

/** Mirror of the C# `ComponentContext` — what a shared component's `build()` may read (mode-free). */
export class ComponentContext {
  constructor(
    readonly theme: AppTheme,
    readonly typeScale = 1,
    /** 'comfortable' | 'compact' — how tight this target's controls are (C# `Density`). */
    readonly density = 'comfortable',
  ) {}
}

let activeTheme: AppTheme = photonTheme;
let activeTypeScale = 1;

/**
 * The DENSITY this page runs at. A mouse is precise and a fingertip is not, so a desktop browser
 * gets the tight controls a native desktop app has and a touch screen keeps the comfortable ones —
 * the same decision the native shells make, taken here from the pointer the browser reports.
 * A page never asks: it reads `context.density` like it reads the theme.
 */
let activeDensity = 'comfortable';

export function setPhotonDensity(density: string): void {
  activeDensity = density === 'compact' ? 'compact' : 'comfortable';
}

export function getPhotonDensity(): string {
  return activeDensity;
}

/** Resolves the density from the pointer the browser reports (coarse = finger = comfortable). */
export function detectPhotonDensity(): void {
  if (typeof window === 'undefined' || typeof window.matchMedia !== 'function') return;
  setPhotonDensity(window.matchMedia('(pointer: fine)').matches ? 'compact' : 'comfortable');
}

export function setPhotonTheme(theme: AppTheme, typeScale = 1): void {
  activeTheme = theme;
  activeTypeScale = typeScale;
}

export function getPhotonTheme(): AppTheme {
  return activeTheme;
}

/** The context handed to shared components' `build(context)` (directly or via lowering expansion). */
export function photonComponentContext(): ComponentContext {
  return new ComponentContext(activeTheme, activeTypeScale, activeDensity);
}

/** The lowering context for the active theme — default Text color + component expansion context. */
export function ambientLoweringContext(): LoweringContext {
  return {
    textPrimary: activeTheme.textPrimary,
    componentContext: photonComponentContext(),
  };
}
