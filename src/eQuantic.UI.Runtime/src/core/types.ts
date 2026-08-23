/**
 * eQuantic.UI Runtime - Core types and interfaces
 */

export interface IComponent {

  id?: string;
  className?: string;
  style?: Record<string, string>;
  styleClass?: StyleClass;
  dataAttributes?: Record<string, string>;
  ariaAttributes?: Record<string, string>;
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
  /** Active route data — parameters + query string (C# `context.Route`). */
  route?: import('../router/current-route').RouteData;
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
/**
 * TYPE-ONLY, and that is the whole reason it is allowed to point at the vocabulary: `import type`
 * is erased before anything runs, so it adds no edge to the evaluation graph and cannot reopen the
 * cycle the seam below exists to avoid. Dropping the `type` keyword would.
 *
 * It earns its place by making the hierarchy say what C# says — `UiComponent : VisualNode` — so a
 * component is assignable to a VisualNode on this side too. Without it `centered()` answered
 * `unknown`, which is assignable to nothing, and eqc's own `VisualNode x = new SomeComponent()`
 * emitted TypeScript that did not typecheck.
 */
import type { VisualNode } from '../shared/vocabulary';

/**
 * How a node is CENTRED, registered by the vocabulary at import time. Inverted rather than
 * imported: a static import here would close a module cycle (types → vocabulary → types) at
 * evaluation, and a lazy `require` does not exist in ESM. The vocabulary owns the wrapper's
 * shape; this module only owns the seam.
 */
type CenterWrapper = (child: unknown) => VisualNode;
let centerWrapper: CenterWrapper | null = null;
export function setCenterWrapper(wrapper: CenterWrapper): void {
  centerWrapper = wrapper;
}

export abstract class Component implements IComponent {
  /**
   * C# twin of `VisualNodeExtensions.Centered()`. It lives on the vocabulary's VisualNode AND
   * here, because in C# it is an EXTENSION on VisualNode — and a component IS one, so
   * `Card(…).Centered()` compiles there and called nothing here: the page mounted with
   * "centered is not a function" and a blank frame. The wrapper is built through the ambient
   * lowering rather than imported, so this base keeps no dependency on the vocabulary module.
   */
  centered(): VisualNode {
    // No vocabulary registered means nothing to wrap WITH — a client-only mount before the
    // vocabulary module has evaluated. The node comes back unchanged, which is the same object
    // the caller already held, so the cast states what is already true of every caller's value.
    return centerWrapper ? centerWrapper(this) : (this as unknown as VisualNode);
  }

  id?: string;
  className?: string;
  style?: Record<string, string>;
  styleClass?: StyleClass;
  title?: string;
  hidden?: boolean;
  tabIndex?: number;
  dataAttributes?: Record<string, string>;
  ariaAttributes?: Record<string, string>;
  children: IComponent[] = [];

  constructor(props?: any) {
    if (props && typeof props === 'object') {
      Object.assign(this, props);
    }
  }

  // Common Events
  onClick?: Action;
  onDoubleClick?: Action;
  onFocus?: Action;
  onBlur?: Action;
  onMouseEnter?: Action<any>;
  onMouseLeave?: Action<any>;
  onMouseDown?: Action<any>;
  onMouseUp?: Action<any>;
  onKeyDown?: Action<any>;
  onKeyUp?: Action<any>;
  onKeyPress?: Action<any>;
  onChange?: Action<any>;
  onInput?: Action<any>;
  // A transpiled component may DECLARE one of these with the null its C# signature carries — the
  // transpiled world produces null wherever C# produced null — so the base accepts null too.
  onSubmit?: Action<any> | null;

  abstract render(): HtmlNode;

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
}

type Action<T = void> = (args: T) => void;

/** Where the DOM's event name is not the property name lowercased: the one divergence in the C#
 * `HtmlElement.EventNameMap` is double-click, which the DOM spells `dblclick` — a listener
 * registered as "doubleclick" attaches fine and fires never. */
const EVENT_NAME_EXCEPTIONS: Record<string, string> = {
  doubleclick: 'dblclick',
};

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
