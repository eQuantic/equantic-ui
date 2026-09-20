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

type Action<T = void> = (args: T) => void;

/** Where the DOM's event name is not the property name lowercased: the one divergence in the C#
 * `HtmlElement.EventNameMap` is double-click, which the DOM spells `dblclick` — a listener
 * registered as "doubleclick" attaches fine and fires never. */
const EVENT_NAME_EXCEPTIONS: Record<string, string> = {
  doubleclick: 'dblclick',
};

/**
 * The DOM escape hatch's base, and the home of the DOM surface.
 *
 * These nine properties, the fourteen `on*` handlers and the two builders that read them used to
 * sit on `Component`, where every component in the tree paid for them: a C# member of the same name
 * lowers to the same key and overwrites silently (#245). They are not a component's, and they never
 * were — the C# side puts them exactly here (`Web/Dom/HtmlElement`), which is the shape this file
 * should have mirrored from the start.
 *
 * They are not dead either, which is the correction: nothing in the RUNTIME calls
 * `buildAttributes`, but a consumer's own `class MyTag : HtmlElement` does, and eqc lowers
 * `BuildAttributes()` to `this.buildAttributes()`. Deleting them broke the escape hatch for exactly
 * the code it exists to serve.
 */
export abstract class HtmlElement extends Component {
  // DECLARE, not a field: a subclass's field declarations run AFTER `super()`, so defining these
  // here would overwrite whatever `Component`'s constructor just took from `props` — measured, it
  // turned `buildEvents()` into `{}`. They were plain declarations while they sat on `Component`,
  // where the constructor runs after that class's own initialisers; one class down, the order
  // reverses. `declare` emits nothing and keeps the types.
  declare id?: string;
  declare className?: string;
  declare style?: Record<string, string>;
  declare styleClass?: StyleClass;
  declare title?: string;
  declare hidden?: boolean;
  declare tabIndex?: number;
  declare dataAttributes?: Record<string, string>;
  declare ariaAttributes?: Record<string, string>;
  // Common Events
  declare onClick?: Action;
  declare onDoubleClick?: Action;
  declare onFocus?: Action;
  declare onBlur?: Action;
  declare onMouseEnter?: Action<any>;
  declare onMouseLeave?: Action<any>;
  declare onMouseDown?: Action<any>;
  declare onMouseUp?: Action<any>;
  declare onKeyDown?: Action<any>;
  declare onKeyUp?: Action<any>;
  declare onKeyPress?: Action<any>;
  declare onChange?: Action<any>;
  declare onInput?: Action<any>;
  // A transpiled component may DECLARE one of these with the null its C# signature carries — the
  // transpiled world produces null wherever C# produced null — so the base accepts null too.
  declare onSubmit?: Action<any> | null;

  protected buildAttributes(): Record<string, string | undefined> {
    const attrs: Record<string, string | undefined> = {};

    if (this.id) attrs['id'] = this.id;
    if (this.title) attrs['title'] = this.title;
    if (this.hidden) attrs['hidden'] = 'true';
    if (this.tabIndex !== undefined) attrs['tabindex'] = this.tabIndex.toString();

    // Build className from className + styleClass
    const classNames: string[] = [];
    if (this.className) classNames.push(this.className);
    if (this.styleClass) classNames.push(this.styleClass.generatedClassName);
    if (classNames.length > 0) attrs['class'] = classNames.join(' ');

    // Style
    if (this.style) {
      attrs['style'] = Object.entries(this.style)
        .map(([k, v]) => `${k}: ${v}`)
        .join('; ');
    }

    // Data attributes
    if (this.dataAttributes) {
      for (const [key, value] of Object.entries(this.dataAttributes)) {
        attrs[`data-${key}`] = value;
      }
    }

    // ARIA attributes
    if (this.ariaAttributes) {
      for (const [key, value] of Object.entries(this.ariaAttributes)) {
        attrs[`aria-${key}`] = value;
      }
    }

    return attrs;
  }

  protected buildEvents(): Record<string, EventHandler> {
    const events: Record<string, EventHandler> = {};

    // Dynamic discovery of events (all props starting with 'on')
    for (const prop of Object.keys(this)) {
      if (prop.startsWith('on') && prop.length > 2) {
        // e.g. onClick -> click, onMouseEnter -> mouseenter — with the DOM's own spelling where
        // lowercasing alone is wrong (C# twin: HtmlElement.EventNameMap).
        const lowered = prop.substring(2).toLowerCase();
        const eventName = EVENT_NAME_EXCEPTIONS[lowered] ?? lowered;

        const handler = (this as any)[prop];
        if (handler && typeof handler === 'function') {
          events[eventName] = handler as EventHandler;
        }
      }
    }

    // Merge explicit custom events (already keyed by DOM event name). Composite components such as
    // Button forward their resolved handler set to a child element via `customEvents`; without this
    // merge the child's render would rebuild events from its own (absent) on* props and silently drop
    // the handler. Mirrors HtmlElement.BuildEvents() in C# (eQuantic.UI.Core).
    const custom = (this as Record<string, unknown>).customEvents as
      | Record<string, EventHandler>
      | undefined;
    if (custom) {
      for (const [eventName, handler] of Object.entries(custom)) {
        if (handler && typeof handler === 'function') {
          events[eventName] = handler;
        }
      }
    }

    return events;
  }

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
