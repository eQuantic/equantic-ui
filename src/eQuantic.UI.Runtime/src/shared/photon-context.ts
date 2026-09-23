/**
 * The ambient Photon context on the client: which theme transpiled shared components read through
 * `context.theme`, and the `LoweringContext` the vocabulary's self-lowering `render()` uses. Defaults
 * to the generated `photonTheme` — an app only calls `setPhotonTheme` to swap a custom theme in.
 */

import type { LoweringContext } from './lowering';
import { resolveService } from '../utils/services';
import { RouteValues } from './route-values';
import { cssFontWeight, isWellFormedFace, type AppTheme } from './value-types';
import type { TypeStyleValue } from './nodes';
import type { DensityValue } from './enums.generated';
import { photonTheme } from './design-system.generated';

/** Mirror of the C# `ComponentContext` — what a shared component's `build()` may read (mode-free). */
export class ComponentContext {
  constructor(
    readonly theme: AppTheme,
    readonly typeScale = 1,
    /** How tight this target's controls are — the C# `Density`, in its own words rather than in
        a `string` a component would then have to be trusted with. */
    readonly density: DensityValue = 'comfortable',
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
  get route(): RouteValues {
    return RouteValues.current;
  }

  /**
   * Whether this component was asked to draw WHERE IT STANDS rather than over everything (the C#
   * `ComponentContext.InFlow`). Only the overlays answer it.
   */
  get inFlow(): boolean {
    return inFlowState;
  }

  /**
   * A capability, for a component in the MIDDLE of a tree — the C# `context.GetService<T>()`, which
   * eqc emits as `getService('IThing')`. Null when this target does not have it.
   *
   * The return type is deliberately open. eqc erases the C# type argument (resolution is by
   * interface NAME at runtime), so nothing here could check it — and `unknown` would make every
   * legitimate `getService('IX')?.doThing()` a type error the transpiled twin has no way to cast
   * away. The C# compiler is the type layer for this call; TypeScript's job is to let the twin
   * through.
   */
  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  getService(interfaceName: string): any {
    return resolveService(interfaceName);
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
  // The NAMED face first, then the stack that would have been used — the same order the lowering
  // writes into `font-family`. Measuring with a different face than the one that draws is what the
  // comment beside MONO_STACK calls a caret beside the character it is on, and a brand face is
  // exactly the case where the two advance differently.
  const base = style.mono ? monoStack() : sansStack();
  const family = isWellFormedFace(style.family) ? `"${style.family}", ${base}` : base;
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

/**
 * The font stacks the CSS uses — measuring with anything else measures the wrong text.
 *
 * These are the FALLBACKS the lowering declares inside its `var(...)`, character for character. The
 * variables themselves are resolved below, because an app may set either on `:root`, and a constant
 * that ignores the variable measures a stack such a page never draws with. That is what this used to
 * do: it carried a list of its own, two entries longer than any of the three the CSS side declares.
 */
const SANS_FALLBACK = 'system-ui, -apple-system, sans-serif';
// Character for character the fallback inside the CSS `var(...)`, quotes included. `--eq-font-mono`,
// like `--eq-font-family`, is a hook an APP sets and the SDK declares nothing, so this literal is what
// the page normally paints with — a list of this measurer's own would be the common case, not the edge.
const MONO_FALLBACK = "ui-monospace, 'SFMono-Regular', Menlo, Consolas, monospace";

/**
 * The faces as the page will actually DRAW them. The lowering emits `var(--eq-font-family, …)` and
 * `var(--eq-font-mono, …)`, and an app that sets either paints with its own face, so a measurer that
 * reads only the fallback measures a stack that page never paints. A code editor places its caret
 * from a measured column advance: different face, different advance, caret beside the character.
 * (A base stylesheet once declared the first. No page ever linked it, and it is gone: #335.)
 *
 * Resolved once and cached, like the canvas: the variable's VALUE is static CSS. (A web font that
 * loads late changes metrics without changing this string, which is the pre-existing hazard for
 * every stack here, not one this introduces.)
 *
 * ONE reader with two callers rather than one per axis — the sans half was missing for as long as
 * the mono half existed, which is what two pieces answering the same question costs.
 */
let sansResolved: string | undefined;
let monoResolved: string | undefined;

function sansStack(): string {
  return (sansResolved ??= declaredStack('--eq-font-family', SANS_FALLBACK));
}

function monoStack(): string {
  return (monoResolved ??= declaredStack('--eq-font-mono', MONO_FALLBACK));
}

/** The custom property's VALUE when the document declares one, else the CSS's own fallback. */
function declaredStack(variable: string, fallback: string): string {
  if (typeof document === 'undefined' || !document.documentElement) return fallback;
  const declared = getComputedStyle(document.documentElement).getPropertyValue(variable).trim();
  return declared !== '' ? declared : fallback;
}

let measuring: CanvasRenderingContext2D | null | undefined;

/** One canvas for the whole page — creating one per call is how a measurer becomes the slow part. */
function measuringContext(): CanvasRenderingContext2D | null {
  if (measuring !== undefined) return measuring;
  measuring =
    typeof document === 'undefined' ? null : document.createElement('canvas').getContext('2d');
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
let activeDensity: DensityValue = 'comfortable';

export function setPhotonDensity(density: string): void {
  activeDensity = density === 'compact' ? 'compact' : 'comfortable';
}

export function getPhotonDensity(): DensityValue {
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

/**
 * The ambient in-flow intent, armed while an `InFlow` subtree builds — the C# `InFlow.Current`
 * twin. Read through `ComponentContext.inFlow`.
 */
let inFlowState = false;

/** The ambient in-flow intent — what a RenderContext carries into a transpiled build. */
export function getInFlow(): boolean {
  return inFlowState;
}

export function setInFlow(value: boolean): boolean {
  const previous = inFlowState;
  inFlowState = value;
  return previous;
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
