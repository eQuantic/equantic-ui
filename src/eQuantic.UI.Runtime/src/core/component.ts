/**
 * eQuantic.UI Runtime - Stateful Component Support
 */

import { Component, HtmlNode, RenderContext } from './types';

/**
 * Base class for stateless components
 */
export abstract class StatelessComponent extends Component {
  abstract build(context: RenderContext): Component;

  render(): HtmlNode {
    const context: RenderContext = {
      getService: () => undefined,
    };
    const component = this.build(context);
    return component.render();
  }
}

/**
 * Base class for stateful components
 */
export abstract class StatefulComponent extends Component {
  private _state: ComponentState | null = null;
  private _mounted = false;
  private _renderScheduled = false;
  private _element: HTMLElement | null = null;

  abstract createState(): ComponentState;

  get state(): ComponentState {
    if (!this._state) {
      this._state = this.createState();
      this._state._setComponent(this);
      this._state.onInit();
    }
    return this._state;
  }

  render(): HtmlNode {
    const context: RenderContext = {
      getService: () => undefined,
    };
    this.state._context = context;
    const component = this.state.build(context);
    return component.render();
  }

  mount(container: HTMLElement): void {
    this._element = container;
    const node = this.render();
    const dom = renderToDom(node);
    container.appendChild(dom);
    this._mounted = true;
    this.state.onMount();
  }

  _scheduleRender(): void {
    if (this._renderScheduled) return;
    this._renderScheduled = true;

    requestAnimationFrame(() => {
      this._renderScheduled = false;
      if (this._element && this._mounted) {
        // Clear and re-render
        this._element.innerHTML = '';
        const node = this.render();
        const dom = renderToDom(node);
        this._element.appendChild(dom);
        this.state.onUpdate();
      }
    });
  }

  unmount(): void {
    if (this._mounted && this._state) {
      this._state.onDispose();
      this._mounted = false;
      this._element = null;
    }
  }
}

/**
 * Base class for component state
 */
export abstract class ComponentState {
  private _component: StatefulComponent | null = null;
  _context: RenderContext | null = null;
  _needsRender = false;

  get component(): StatefulComponent {
    if (!this._component) {
      throw new Error('State not initialized');
    }
    return this._component;
  }

  _setComponent(component: StatefulComponent): void {
    this._component = component;
  }

  /**
   * Update state and trigger re-render
   */
  protected setState(fn: () => void): void {
    fn();
    this._needsRender = true;
    this.component._scheduleRender();
  }

  /**
   * Build the component tree
   */
  abstract build(context: RenderContext): Component;

  // Lifecycle hooks
  onInit(): void {}
  onMount(): void {}
  onUpdate(): void {}
  onDispose(): void {}
}

/**
 * Render HtmlNode to actual DOM element
 */
function renderToDom(node: HtmlNode): Node {
  // Text node
  if (node.tag === '#text') {
    return document.createTextNode(node.textContent || '');
  }

  // Create element
  const element = document.createElement(node.tag);

  // Set attributes
  for (const [key, value] of Object.entries(node.attributes)) {
    if (value !== undefined) {
      element.setAttribute(key, value);
    }
  }

  // Attach event handlers
  for (const [eventName, handler] of Object.entries(node.events)) {
    element.addEventListener(eventName, (e) => {
      if (eventName === 'change' || eventName === 'input') {
        const target = e.target as HTMLInputElement;
        (handler as (value: string) => void)(target.value);
      } else {
        (handler as () => void)();
      }
    });
  }

  // Render children
  for (const child of node.children) {
    element.appendChild(renderToDom(child));
  }

  return element;
}

export { renderToDom };
