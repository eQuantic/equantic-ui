/**
 * The ambient Photon context on the client: which theme transpiled shared components read through
 * `context.theme`, and the `LoweringContext` the vocabulary's self-lowering `render()` uses. Defaults
 * to the generated `photonTheme` — an app only calls `setPhotonTheme` to swap a custom theme in.
 */

import type { LoweringContext } from './lowering';
import { getCurrentRoute, type RouteData } from '../router/current-route';
import { cssFontWeight, type AppTheme } from './value-types';
import type { TypeStyleValue } from './nodes';
import { photonTheme } from './design-system.generated';

/** Mirror of the C# `ComponentContext` — what a shared component's `build()` may read (mode-free). */
export class ComponentContext {
  constructor(
    readonly theme: AppTheme,
    readonly typeScale = 1,
    /** 'comfortable' | 'compact' — how tight this target's controls are (C# `Density`). */
    readonly density = 'comfortable',
  ) {}

  /**
   * How WIDE this text would be in this style, in dp — the C# `ComponentContext.MeasureText`.
   * The browser's own measurer, through a canvas: the same numbers it will lay the text out with,
   * which is what a code editor mapping a click to a column depends on.
   */
  measureText(text: string, style: TypeStyleValue): number {
    return measurePhotonText(text, style, this.typeScale);
  }

  /**
   * What the ROUTE said — `context.Route.Param("slug")` transpiles to `context.route.param("slug")`.
   * Reads the ambient current route, which the router updates before mounting a page and boot seeds
   * from the initial URL: the same values the server built with, so a param page hydrates matching.
   */
  get route(): RouteData {
    return getCurrentRoute();
  }

  /** The advance of ONE character in a monospaced style (C# `MonoAdvance`). */
  monoAdvance(style: TypeStyleValue): number {
    return photonMonoAdvance(style, this.typeScale);
  }
}

/**
 * The same measurement WITHOUT a context object — the DOM render path builds its context as a plain
 * literal, and a component that has to size a column to its content needs the answer either way.
 */
export function measurePhotonText(text: string, style: TypeStyleValue, typeScale = 1): number {
  if (!text) return 0;
  const context = measuringContext();
  if (!context) return 0;
  const family = style.mono ? monoStack() : SANS_STACK;
  // NUMERIC weight: the enum arrives as a member name, and a name makes the whole shorthand
  // invalid — see cssFontWeight for what that cost.
  context.font = `${cssFontWeight(style.weight)} ${style.size * typeScale}px ${family}`;
  return context.measureText(text).width;
}

export function photonMonoAdvance(style: TypeStyleValue, typeScale = 1): number {
  const sample = '0000000000';
  const width = measurePhotonText(sample, { ...style, mono: true } as TypeStyleValue, typeScale);
  return width > 0 ? width / sample.length : 0;
}

/** The font stacks the CSS uses — measuring with anything else measures the wrong text. */
const SANS_STACK =
  'system-ui, -apple-system, "Segoe UI", Roboto, "Helvetica Neue", Arial, sans-serif';
const MONO_STACK =
  'ui-monospace, SFMono-Regular, "SF Mono", Menlo, Consolas, "Liberation Mono", monospace';

/**
 * The mono face as the page will actually DRAW it: the lowering emits
 * `var(--eq-font-mono, …)`, so an app that set that variable is drawing in a face this measurer
 * would otherwise know nothing about — and a code editor places its caret from a measured column
 * advance. Different face, different advance, caret beside the character rather than on it.
 *
 * Resolved once and cached, like the canvas: the variable's VALUE is static CSS. (A web font that
 * loads late changes metrics without changing this string, which is the pre-existing hazard for
 * every stack here, not one this introduces.)
 */
let monoResolved: string | undefined;

function monoStack(): string {
  if (monoResolved !== undefined) return monoResolved;
  monoResolved = MONO_STACK;
  if (typeof document !== 'undefined' && document.documentElement) {
    const declared = getComputedStyle(document.documentElement)
      .getPropertyValue('--eq-font-mono')
      .trim();
    if (declared) monoResolved = declared;
  }
  return monoResolved;
}

let measuring: CanvasRenderingContext2D | null | undefined;

/** One canvas for the whole page — creating one per call is how a measurer becomes the slow part. */
function measuringContext(): CanvasRenderingContext2D | null {
  if (measuring !== undefined) return measuring;
  measuring = typeof document === 'undefined' ? null : document.createElement('canvas').getContext('2d');
  return measuring;
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

/** The active type scale — a page's context has to carry the SAME one its subtree is lowered with. */
export function getPhotonTypeScale(): number {
  return activeTypeScale;
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
