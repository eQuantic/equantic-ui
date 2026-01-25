/**
 * eQuantic.UI Runtime - Stateful Component Support
 */

import { Component, HtmlNode, RenderContext } from './types';
import { RenderManager } from '../dom/renderer';
import { getRootServiceProvider, ServiceProvider } from './service-provider';

/**
 * Base class for stateless components
 */
export abstract class StatelessComponent extends Component {
  protected serviceProvider: ServiceProvider = getRootServiceProvider();

  abstract build(context: RenderContext): Component;

  render(): HtmlNode {
    const context: RenderContext = {
      getService: <T>(key: import('./types').ServiceKey<T>) => this.serviceProvider.getService<T>(key),
      serviceProvider: this.serviceProvider,
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
  private _renderManager: RenderManager = new RenderManager();
  protected serviceProvider: ServiceProvider = getRootServiceProvider();

  abstract createState(): ComponentState;

  get state(): ComponentState {
    if (!this._state) {
      if (typeof this.createState !== 'function') {
         throw new Error(`Component ${this.constructor.name} does not implement createState()`);
      }
      this._state = this.createState();
      
      if (!this._state) {
        throw new Error(`createState() returned null/undefined for ${this.constructor.name}`);
      }

      if (typeof this._state.setComponent === 'function') {
         this._state.setComponent(this);
      } else {
         console.warn(`State object for ${this.constructor.name} missing setComponent method.`, this._state);
         // Fallback legacy name check
         if (typeof (this._state as any)._setComponent === 'function') {
            (this._state as any)._setComponent(this);
         }
      }
      this._state.onInit();
    }
    return this._state;
  }

  render(): HtmlNode {
    const context: RenderContext = {
      getService: <T>(key: import('./types').ServiceKey<T>) => this.serviceProvider.getService<T>(key),
      serviceProvider: this.serviceProvider,
    };
    this.state._context = context;
    const component = this.state.build(context);
    return component.render();
  }

  mount(container: HTMLElement): void {
    const node = this.render();

    // Check if we should hydrate (SSR content exists)
    if (this._renderManager.canHydrate(container)) {
      const result = this._renderManager.hydrate(node, container);
      if (result.success) {
        console.debug(`[eQuantic.UI] Hydrated ${this.constructor.name} with ${result.attachedListeners} event listeners`);
      }
    } else {
      this._renderManager.mount(node, container);
    }

    this._mounted = true;
    this.state.onMount();
  }

  _scheduleRender(): void {
    if (this._renderScheduled) return;
    this._renderScheduled = true;

    requestAnimationFrame(() => {
      this._renderScheduled = false;
      if (this._mounted) {
        // Efficient update using reconciler
        const node = this.render();
        this._renderManager.update(node);

        // Call lifecycle hook
        this.state.onUpdate();
      }
    });
  }

  unmount(): void {
    if (this._mounted && this._state) {
      this._state.onDispose();
      this._renderManager.unmount();
      this._mounted = false;
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

  setComponent(component: StatefulComponent): void {
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


