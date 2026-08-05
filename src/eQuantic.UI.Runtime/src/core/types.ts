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
  getService<T>(key: ServiceKey<T>): T | undefined;
  serviceProvider?: ServiceProvider;
  /** Active route data — parameters + query string (C# `context.Route`). */
  route?: import('../router/current-route').RouteData;
  /** The active Photon theme (C# `context.Theme`) — what transpiled SHARED components read. The import
   * is TYPE-ONLY (erased at compile time), so core/types carries the vocabulary's types without taking
   * any runtime dependency on it — the constraint the previous `unknown` was protecting. */
  theme: import('../shared/value-types').AppTheme;
  /** How tight this target's controls are — 'comfortable' | 'compact' (C# `context.Density`).
   * Optional so a host that predates the axis still type-checks; the components read it through
   * the ladder, which defaults to comfortable. */
  density?: string;
}

/**
 * Re-export ServiceKey and ServiceProvider type for use in RenderContext
 * (actual implementation is in service-provider.ts to avoid circular deps)
 */
export type ServiceKey<T = unknown> = (new (...args: unknown[]) => T) | string;
export type ServiceProvider = {
  getService<T>(key: ServiceKey<T>): T | undefined;
  getRequiredService<T>(key: ServiceKey<T>): T;
  hasService(key: ServiceKey): boolean;
  createScope(): ServiceProvider;
  dispose(): void;
};

/**
 * Base class for all components
 */
export abstract class Component implements IComponent {
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
        // e.g. onClick -> click, onMouseEnter -> mouseenter
        const eventName = prop.substring(2).toLowerCase();

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
