/**
 * The ambient Photon context on the client: which theme transpiled shared components read through
 * `context.theme`, and the `LoweringContext` the vocabulary's self-lowering `render()` uses. Defaults
 * to the generated `photonTheme` — an app only calls `setPhotonTheme` to swap a custom theme in.
 */

import type { LoweringContext } from './lowering';
import type { RenderContext } from '../core/types';
import { resolveService } from '../utils/services';
import { RouteValues } from './route-values';
import { cssFontWeight, isWellFormedFace, type AppTheme } from './value-types';
import type { TypeStyleValue } from './nodes';
import type { DensityValue } from './enums.generated';
import { photonTheme } from './design-system.generated';
import { DENSITY_COOKIE } from './markers';
import { passInvalidator } from './pass-invalidator';
import { declareFamily, dependOnFamily } from './face-declarations';
import { textElementStarts } from '../utils/text-elements';

/**
 * What a context made for a component's position reads of it: the element engine's
 * `ComponentElement`, by the members the C# context reads. Declared here rather than imported, so
 * this module takes no dependency on the engine's, which reaches it through `runtime-exports`.
 */
interface ContextPosition {
  readonly isActive: boolean;
  readonly underInFlow: boolean;
  dependOn(matches: (value: object) => boolean): object | null;
  find(matches: (value: object) => boolean): object | null;
  readonly owner: {
    readonly context: RenderContext;
    readFrame(reader: ContextPosition): void;
    readRoute(reader: ContextPosition): void;
  };
  visitChildren(visitor: unknown): void;
}

/** Whether the constructor was handed a position rather than a theme: only an element visits children. */
function isPosition(value: AppTheme | ContextPosition | null): value is ContextPosition {
  return typeof (value as Partial<ContextPosition> | null)?.visitChildren === 'function';
}

/**
 * Mirror of the C# `ComponentContext` — what a shared component's `build()` may read (mode-free).
 *
 * Made two ways, as C# makes it. A FRAME'S context holds the theme, type scale and density it was
 * given. A POSITION'S (the C# internal constructor, which the element engine calls once per
 * component position) holds none of its own: it reads them from its owner's frame and registers the
 * position as their reader, so the position builds again when they change, and it answers `inFlow`
 * from the tree above it.
 */
export class ComponentContext {
  private readonly _position: ContextPosition | null;
  private readonly _theme: AppTheme;
  private readonly _typeScale: number;
  private readonly _density: DensityValue;
  private readonly _route: RouteValues;
  private readonly _inFlow: boolean;

  constructor(
    theme: AppTheme | ContextPosition,
    typeScale = 1,
    /** How tight this target's controls are — the C# `Density`, in its own words rather than in
        a `string` a component would then have to be trusted with. */
    density: DensityValue = 'comfortable',
    /** The C# measurer: the browser measures with its own canvas, so what is handed here is not read. */
    _measureText?: unknown,
    /** The route the frame is drawn for (C# `route`); the empty one when none is handed. */
    route: RouteValues | null = null,
    /** Whether the frame's components were asked to draw in the flow (C# `inFlow`). */
    inFlow = false,
  ) {
    this._route = route ?? RouteValues.empty;
    this._inFlow = inFlow;
    if (isPosition(theme)) {
      this._position = theme;
      this._theme = null!;
    } else {
      this._position = null;
      this._theme = theme;
    }
    this._typeScale = typeScale;
    this._density = density;
  }

  /** The frame `position` builds in, with the position registered as one that read it. */
  private readFrame(position: ContextPosition): RenderContext {
    // A position that left the tree (a handler that kept its context) reads, and registers nothing.
    if (position.isActive) position.owner.readFrame(position);
    return position.owner.context;
  }

  get theme(): AppTheme {
    return this._position ? this.readFrame(this._position).theme : this._theme;
  }

  get typeScale(): number {
    return this._position ? this.readFrame(this._position).typeScale : this._typeScale;
  }

  get density(): DensityValue {
    return this._position ? this.readFrame(this._position).density : this._density;
  }

  /**
   * How WIDE this text would be in this style, in dp — the C# `ComponentContext.MeasureText`.
   * The browser's own measurer, through a canvas: the same numbers it will lay the text out with,
   * which is what a code editor mapping a click to a column depends on. A position measures with its
   * owner's frame, as every build of it does.
   */
  measureText(text: string, style: TypeStyleValue): number {
    if (this._position) return this._position.owner.context.measureText(text, style);
    return measurePhotonText(text, style, this._typeScale);
  }

  /**
   * What the ROUTE said — `context.Route.Param("slug")` transpiles to `context.route.param("slug")`.
   * A position reads its owner's frame, which the page hands the router's route (the same values the
   * server built with, so a param page hydrates matching), and is registered as one of the route's
   * readers, apart from the theme's: a navigation within one page type builds what read the route
   * and nothing else (#1395). A frame's context answers the route it was handed.
   */
  get route(): RouteValues {
    const position = this._position;
    if (!position) return this._route;
    if (position.isActive) position.owner.readRoute(position);
    return position.owner.context.route ?? RouteValues.empty;
  }

  /**
   * Whether this component was asked to draw WHERE IT STANDS rather than over everything (the C#
   * `ComponentContext.InFlow`). Only the overlays answer it. A position answers from the tree above
   * it, a frame's context what it was handed.
   */
  get inFlow(): boolean {
    return this._position ? this._position.underInFlow : this._inFlow;
  }

  /**
   * The nearest inherited component of `type` above, registering this component as its dependent (the
   * C# `DependOn<T>()`, which eqc lowers with `T`'s twin, #1395): the component builds again when that
   * value says it changed. Null when none is above, and on a context with no position.
   */
  dependOn<T extends object>(type: abstract new (...args: never[]) => T): T | null {
    return (this._position?.dependOn((value) => value instanceof type) as T | null | undefined) ?? null;
  }

  /** The nearest inherited component of `type` above, read once (the C# `Find<T>()`): null as above. */
  find<T extends object>(type: abstract new (...args: never[]) => T): T | null {
    return (this._position?.find((value) => value instanceof type) as T | null | undefined) ?? null;
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

  /** The advance of ONE character in a monospaced style (C# `MonoAdvance`), with the owner's frame
      for a position, as `measureText`. */
  monoAdvance(style: TypeStyleValue): number {
    if (this._position) return this._position.owner.context.monoAdvance(style);
    return photonMonoAdvance(style, this._typeScale);
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
  // A family that ships files is declared before it is measured (#1056), so a face still on its way
  // is one the tracker below waits for, rather than one no rule declared yet, and the page that measured
  // it rebuilds should the family be taken back after it loaded.
  const rebuild = passInvalidator();
  if (isWellFormedFace(style.family?.name)) {
    declareFamily(style.family);
    dependOnFamily(style.family, rebuild);
  }
  // The theme's own face closes the stack, through the variable its stylesheet declares, and sets
  // whatever the named family does not: all of it when none is named, and when the named one is not
  // installed. The measure depends on it in every case: taken back, its text falls back, and the page
  // measures again.
  dependOnFamily(style.mono ? activeTheme.monoFamily : activeTheme.textFamily, rebuild);
  const family = isWellFormedFace(style.family?.name) ? `"${style.family!.name}", ${base}` : base;
  // NUMERIC weight: the enum arrives as a member name, and a name makes the whole shorthand
  // invalid — see cssFontWeight for what that cost. The SLANT names the cut that draws the text:
  // without it italic text was measured upright, and the load it waits for was the upright file's,
  // already there while the italic one was still on its way.
  context.font = `${style.italic ? 'italic ' : ''}${cssFontWeight(style.weight)} ${style.size * typeScale}px ${family}`;
  rebuildWhenFaceLoads(context.font);
  // The TRACKING, which the lowering writes as `letter-spacing` (an em ratio for a fluid size, pixels
  // otherwise) and the browser adds after every character it draws, as Photon's measurer adds it: a
  // canvas knows nothing of it, so a tracked face measured short, and a cell's editor cut its own
  // text with an ellipsis and stood its caret before the character it was after.
  const tracking = style.tracking ?? 0;
  if (tracking === 0) return context.measureText(text).width;
  const spacing = style.fluid && style.size > 0 ? tracking * typeScale : tracking;
  return context.measureText(text).width + spacing * textElementStarts(text).length;
}

/**
 * The pages that measured text in a face the document declares and has not loaded yet, by the font
 * they measured: a canvas measures such a face as its fallback, and the text is drawn in the face
 * once it arrives, so a code editor would place its caret on the fallback's columns. Each page
 * rebuilds when its face has loaded, once however many times it measured (#1055).
 */
const awaitingFace = new Map<string, Set<() => void>>();

/**
 * The fonts whose face failed to load. Such a face never arrives, so `check` stays false for it and
 * its text is drawn in the fallback it was measured in: the measurement already agrees with the
 * drawing, nothing rebuilds, and nothing asks for it again. A rebuild here would measure, ask, fail
 * and rebuild without end.
 */
const failedFaces = new Set<string>();

function rebuildWhenFaceLoads(font: string): void {
  const fonts = typeof document === 'undefined' ? undefined : document.fonts;
  const rebuild = passInvalidator();
  if (!fonts || !rebuild || failedFaces.has(font)) return;
  let loaded: boolean;
  try {
    // True for a face that has loaded and for one no `@font-face` declares: only a declared face
    // still on its way is false.
    loaded = fonts.check(font);
  } catch {
    return;
  }
  if (loaded) return;
  const pages = awaitingFace.get(font);
  if (pages) {
    pages.add(rebuild);
    return;
  }
  awaitingFace.set(font, new Set([rebuild]));
  fonts.load(font).then(
    () => {
      const waiting = awaitingFace.get(font);
      awaitingFace.delete(font);
      waiting?.forEach((page) => page());
    },
    () => {
      awaitingFace.delete(font);
      failedFaces.add(font);
    },
  );
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
 * variables themselves are resolved below, because the theme's stylesheet declares them from its
 * faces, and a constant that ignores the variable measures a stack such a page never draws with. That
 * is what this used to do: it carried a list of its own, two entries longer than any of the three the
 * CSS side declares.
 */
const SANS_FALLBACK = 'system-ui, -apple-system, sans-serif';
// Character for character the fallback inside the CSS `var(...)`, quotes included: what the page
// paints with when its theme names no code face, which is the common case, not the edge.
const MONO_FALLBACK = "ui-monospace, 'SFMono-Regular', Menlo, Consolas, monospace";

/**
 * The faces as the page will actually DRAW them. The lowering emits `var(--eq-font-family, …)` and
 * `var(--eq-font-mono, …)`, and the theme's stylesheet declares both from the theme's text and code
 * faces, so a measurer that reads only the fallback measures a stack that page never paints. A code
 * editor places its caret from a measured column advance: different face, different advance, caret
 * beside the character.
 *
 * Resolved once and cached, like the canvas: the variable's VALUE is static CSS. A face that loads
 * late changes metrics without changing this string, so a page that measured it still loading
 * rebuilds when it arrives (rebuildWhenFaceLoads).
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

/** The density the pointer the browser reports asks for (coarse = finger = comfortable), or null
 * where nothing can be asked. */
export function pointerDensity(): DensityValue | null {
  if (typeof window === 'undefined' || typeof window.matchMedia !== 'function') return null;
  return window.matchMedia('(pointer: fine)').matches ? 'compact' : 'comfortable';
}

/** Resolves the density from the pointer the browser reports (coarse = finger = comfortable). */
export function detectPhotonDensity(): void {
  const density = pointerDensity();
  if (density) setPhotonDensity(density);
}

/**
 * The density hydration lowers at (#623): the one the server built the page at, while hydrating, so
 * the served markup is adopted as it is; the pointer's otherwise.
 */
export function densityForHydration(
  served: string | undefined,
  hydrating: boolean,
  own: DensityValue | null,
): DensityValue | null {
  if (hydrating && (served === 'compact' || served === 'comfortable')) return served;
  return own;
}

/**
 * After hydration the whole page switches to the pointer's density AT ONCE when it was hydrated at
 * another (#623): one re-render of the page, never each component as it next re-renders, which showed
 * a mix of both. Answers whether it switched.
 */
export function settleDensity(own: DensityValue | null, rerender: () => void): boolean {
  if (!own || activeDensity === own) return false;
  setPhotonDensity(own);
  rerender();
  return true;
}

/**
 * Leaves the density for the SERVER, which cannot see the pointer: every later request of the
 * session renders at it from its first byte (#623). A session cookie holding a display fact about
 * the device, gone when the browser closes, and written only when it changes.
 */
export function rememberDensity(density: DensityValue): void {
  if (typeof document === 'undefined') return;
  try {
    if (document.cookie.split('; ').includes(`${DENSITY_COOKIE}=${density}`)) return;
    document.cookie = `${DENSITY_COOKIE}=${density}; path=/; samesite=lax`;
  } catch {
    /* cookies blocked by the browser: every load hydrates and then switches, as without one */
  }
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

/**
 * The lowering context for a bridge given its OWN theme: that theme's tokens at the page's density,
 * in the same ComponentContext the ambient one hands out. A bare `{ theme, typeScale }` carried no
 * density, so a stateful component it expanded drew Comfortable in a Compact page, where the server,
 * which builds every bridge at the request's density (#623), drew Compact.
 */
export function themedLoweringContext(theme: AppTheme, typeScale = 1): LoweringContext {
  return {
    textPrimary: theme.textPrimary,
    componentContext: new ComponentContext(theme, typeScale, activeDensity),
  };
}
