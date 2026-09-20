/**
 * eQuantic.UI Runtime - Core types and interfaces
 */

/**
 * What a component IS to this runtime: a subtree it can render, and the children it holds.
 *
 * It used to declare nine DOM fields beside those — id, className, style, styleClass, the two
 * attribute bags — mirrored on the base class and read by `buildAttributes`, which nothing called.
 * Every component carried them, so every one of those names was a collision waiting for a C#
 * parameter to be spelled the same (#245): a primary-constructor parameter lowers to a field, and
 * a field silently overwrote the runtime's own. A page shows nothing different and a value is
 * gone. They are deleted rather than fenced — the DOM escape hatch (`HtmlElement`) builds its
 * attributes through `htmlNode` and read none of them.
 */
export interface IComponent {
  children: IComponent[];
  render(): HtmlNode;
}

export interface HtmlNode {
  key?: string;
  tag: string;
  attributes: Record<string, string | undefined>;
  events: Record<string, EventHandler>;
  children: HtmlNode[];
  textContent?: string;
}

export type EventHandler = (...args: unknown[]) => void;

export interface StyleClass {
  generatedClassName: string;
}

export interface RenderContext {
  // `T = any`, deliberately: eqc erases the C# type argument (a capability resolves by interface
  // NAME at runtime), so a string-keyed lookup infers nothing and would land on `{}` — making
  // every legitimate `getService('IX')?.doThing()` in a transpiled twin a type error with no cast
  // to escape through. The C# compiler IS the type layer for this call.
  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  getService<T = any>(key: ServiceKey<T>): T | undefined;
  serviceProvider?: ServiceProvider;
  /**
   * Whether this component was asked to draw WHERE IT STANDS rather than over everything (C#
   * `context.InFlow`). Only the overlays answer it.
   */
  inFlow?: boolean;
  /** What the route said — parameters + query string (C# `context.Route`). */
  route?: import('../shared/route-values').RouteValues;
  /** The active Photon theme (C# `context.Theme`) — what transpiled SHARED components read. The import
   * is TYPE-ONLY (erased at compile time), so core/types carries the vocabulary's types without taking
   * any runtime dependency on it — the constraint the previous `unknown` was protecting. */
  theme: import('../shared/value-types').AppTheme;
  /** How tight this target's controls are (C# `context.Density`). Named rather than a bare string
   * and REQUIRED like its C# original: a transpiled component reads it into a local of this type,
   * and an optional mirror of a non-nullable property is a twin that cannot compile. */
  density: import('../shared/enums.generated').DensityValue;
  /** The type scale multiplier the target is rendering at (C# `context.TypeScale`). */
  typeScale?: number;
  /** How wide a string WOULD be, in dp, at a given type style (C# `context.MeasureText`). A
   * component that has to size a column to its content — a code gutter, a numeric field — cannot
   * guess it, and guessing is how a gutter ends up too narrow for a four-digit line number. */
  measureText(text: string, style: import('../shared/nodes').TypeStyleValue): number;
  /** One character's advance in the monospaced face (C# `context.MonoAdvance`) — with a fixed
   * pitch, one measurement answers for every column. */
  monoAdvance(style: import('../shared/nodes').TypeStyleValue): number;
}

/**
 * Re-export ServiceKey and ServiceProvider type for use in RenderContext
 * (actual implementation is in service-provider.ts to avoid circular deps)
 */
export type ServiceKey<T = unknown> = (new (...args: unknown[]) => T) | string;
export type ServiceProvider = {
  // `T = any`, deliberately: eqc erases the C# type argument (a capability resolves by interface
  // NAME at runtime), so a string-keyed lookup infers nothing and would land on `{}` — making
  // every legitimate `getService('IX')?.doThing()` in a transpiled twin a type error with no cast
  // to escape through. The C# compiler IS the type layer for this call.
  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  getService<T = any>(key: ServiceKey<T>): T | undefined;
  getRequiredService<T>(key: ServiceKey<T>): T;
  hasService(key: ServiceKey): boolean;
  createScope(): ServiceProvider;
  dispose(): void;
};

/**
 * Base class for all components
 */

export abstract class Component implements IComponent {
  children: IComponent[] = [];

  constructor(props?: any) {
    if (props && typeof props === 'object') {
      Object.assign(this, props);
    }
  }

  abstract render(): HtmlNode;

}

/**
 * The DOM escape hatch's base. A STUB on purpose: the C# `HtmlElement` carries sixty-odd typed DOM
 * properties, and this side never mirrored them — it builds attributes through `htmlNode` and an
 * untyped bag, so what a subclass needs it declares (see `DynamicElement`). What it used to have
 * was the nine the COMPONENT base happened to declare, inherited by accident rather than by
 * design, and paid for by every component in the tree (#245).
 */
export abstract class HtmlElement extends Component {
  protected get htmlNode() {
    return {
      text: (content: string) => {
        (this as any).content = content;
        return this.htmlNode;
      },
      attr: (name: string, value: any) => {
        if (!(this as any).attributes) (this as any).attributes = {};
        (this as any).attributes[name] = value;
        return this.htmlNode;
      },
    };
  }
}
