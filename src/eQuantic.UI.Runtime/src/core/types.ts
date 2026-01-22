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
  getService<T>(key: string): T | undefined;
}

/**
 * Base class for all components
 */
export abstract class Component implements IComponent {
  id?: string;
  className?: string;
  style?: Record<string, string>;
  styleClass?: StyleClass;
  dataAttributes?: Record<string, string>;
  ariaAttributes?: Record<string, string>;
  children: IComponent[] = [];

  abstract render(): HtmlNode;

  protected buildAttributes(): Record<string, string | undefined> {
    const attrs: Record<string, string | undefined> = {};

    if (this.id) attrs['id'] = this.id;

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
}
