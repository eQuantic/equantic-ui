/**
 * eQuantic.UI Runtime - Stateful Component Support
 */

import { Component, HtmlNode, RenderContext } from './types';
import { RenderManager } from '../dom/renderer';
import { getRootServiceProvider, ServiceProvider } from './service-provider';
import { hydrateValue } from '../utils/hydrate-value';

/**
 * Base class for stateless components
 */
export abstract class StatelessComponent extends Component {
  private _renderManager: RenderManager = new RenderManager();

  protected get serviceProvider(): ServiceProvider {
    return getRootServiceProvider();
  }

  abstract build(context: RenderContext): Component;

  render(): HtmlNode {
    const context: RenderContext = {
      getService: <T>(key: import('./types').ServiceKey<T>) =>
        this.serviceProvider.getService<T>(key),
      serviceProvider: this.serviceProvider,
    };
    const component = this.build(context);
    return component.render();
  }

  mount(container: HTMLElement): void {
    // Check if we should hydrate (SSR content exists)
    if (this._renderManager.canHydrate(container)) {
      const node = this.render();
      this._renderManager.hydrate(node, container);
    } else {
      const node = this.render();
      this._renderManager.mount(node, container);
    }
  }

  getVirtualNode(): HtmlNode {
    return this.render();
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
  protected get serviceProvider(): ServiceProvider {
    return getRootServiceProvider();
  }

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
        console.warn(
          `State object for ${this.constructor.name} missing setComponent method.`,
          this._state,
        );
        // Fallback legacy name check
        if (typeof (this._state as any)._setComponent === 'function') {
          (this._state as any)._setComponent(this);
        }
      }

      // Hydrate state from SSR if available
      let hydrated = false;
      if (typeof window !== 'undefined' && (window as any).__INITIAL_STATE__ && this._state) {
        const initialState = (window as any).__INITIAL_STATE__;

        // Copy data properties from SSR state to client state.
        // Server preserves original field names (including underscore prefix).
        // The existing field's default value reveals its runtime type, so values that crossed the
        // wire as strings (decimal -> Decimal, long -> bigint) are coerced back to that type —
        // see hydrateValue. Plain fields are assigned verbatim.
        const state = this._state as unknown as Record<string, unknown>;
        Object.keys(initialState).forEach((key) => {
          if (this._state && typeof initialState[key] !== 'function' && key in this._state) {
            state[key] = hydrateValue(state[key], initialState[key]);
          }
        });
        // Clear initial state to prevent reuse
        delete (window as any).__INITIAL_STATE__;
        hydrated = true;
      }

      // Only call onInit if we didn't hydrate (client-only render or SSR on server)
      if (!hydrated) {
        this._state.onInit();
      }
    }
    return this._state;
  }

  render(): HtmlNode {
    const context: RenderContext = {
      getService: <T>(key: import('./types').ServiceKey<T>) =>
        this.serviceProvider.getService<T>(key),
      serviceProvider: this.serviceProvider,
    };
    this.state._context = context;

    const component = this.state.build(context);
    return component.render();
  }

  mount(container: HTMLElement): void {
    // Check if we should hydrate (SSR content exists)
    if (this._renderManager.canHydrate(container)) {
      // For hydration, render to get the virtual DOM for event attachment
      const node = this.render();
      const result = this._renderManager.hydrate(node, container);
      if (result.success) {
        console.debug(
          `[eQuantic.UI] Hydrated ${this.constructor.name} with ${result.attachedListeners} event listeners`,
        );
      }
      // After successful hydration, set this._mounted BEFORE onMount to prevent render loops
      this._mounted = true;
      this.state.onMount();
    } else {
      // For client-only rendering, render first then mount
      const node = this.render();
      this._renderManager.mount(node, container);
      this._mounted = true;
      this.state.onMount();
    }
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
