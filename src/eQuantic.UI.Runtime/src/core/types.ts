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
  onSubmit?: Action<any>;

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
      }
    };
  }
}

