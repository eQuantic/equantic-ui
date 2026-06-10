/**
 * eQuantic.UI Runtime - DOM Renderer
 *
 * Handles initial rendering and efficient updates using the reconciler
 */

import { HtmlNode } from '../core/types';
import { getReconciler, HydrationResult } from './reconciler';

/**
 * Render context for tracking component state
 */
export class RenderManager {
  private previousVirtualDom: HtmlNode | null = null;
  private container: HTMLElement | null = null;

  /**
   * Initial mount - create and append DOM
   */
  mount(node: HtmlNode, container: HTMLElement): void {
    this.container = container;
    this.previousVirtualDom = node;

    const reconciler = getReconciler();
    const dom = reconciler.createDomElement(node);
    container.innerHTML = '';
    container.appendChild(dom);
  }

  /**
   * SPA-navigation mount: take over an existing root by reconciling against the outgoing page's tree
   * (`previousNode`) instead of wiping it. When the two pages share a layout shell, the reconciler keeps
   * the shell's DOM (and its listeners/scroll/focus) and only patches the changed content — the M2
   * "persistent layout" without any explicit layout/outlet concept. With no previous tree it falls back
   * to a clean {@link mount}.
   */
  adopt(node: HtmlNode, container: HTMLElement, previousNode: HtmlNode | null): void {
    if (!previousNode) {
      this.mount(node, container);
      return;
    }
    this.container = container;
    getReconciler().reconcile(container, previousNode, node, 0);
    this.previousVirtualDom = node;
  }

  /** The virtual tree currently reflected in the DOM (kept in sync by mount/hydrate/update/adopt). */
  getCurrentNode(): HtmlNode | null {
    return this.previousVirtualDom;
  }

  /**
   * Update - reconcile old and new virtual DOM
   */
  update(newNode: HtmlNode): void {
    if (!this.container) {
      throw new Error('Cannot update before mounting');
    }

    const reconciler = getReconciler();

    // Reconcile at the root level
    if (this.previousVirtualDom) {
      reconciler.reconcile(this.container, this.previousVirtualDom, newNode, 0);
    } else {
      // First render after mount
      const dom = reconciler.createDomElement(newNode);
      this.container.appendChild(dom);
    }

    this.previousVirtualDom = newNode;
  }

  /**
   * Unmount - cleanup
   */
  unmount(): void {
    if (this.container) {
      const reconciler = getReconciler();

      // Cleanup all children
      while (this.container.firstChild) {
        reconciler.reconcile(this.container, this.previousVirtualDom, null, 0);
      }

      this.previousVirtualDom = null;
      this.container = null;
    }
  }

  /**
   * Get the current container
   */
  getContainer(): HTMLElement | null {
    return this.container;
  }

  /**
   * Hydrate existing SSR-rendered DOM with event listeners.
   * Unlike mount(), this does NOT replace the DOM - it walks the existing
   * elements and attaches event listeners based on the virtual DOM.
   *
   * @param node - The virtual DOM representing the component
   * @param container - The container with SSR-rendered HTML
   * @returns HydrationResult with success status and diagnostics
   */
  hydrate(node: HtmlNode, container: HTMLElement): HydrationResult {
    this.container = container;
    this.previousVirtualDom = node;

    const reconciler = getReconciler();
    const result = reconciler.hydrateRoot(container, node);

    if (!result.success) {
      console.warn('[eQuantic.UI] Hydration warnings:', result.warnings);
      console.warn('[eQuantic.UI] Falling back to full re-render');
      // Fallback: do a full mount if hydration fails
      this.mount(node, container);
      return {
        success: false,
        attachedListeners: 0,
        warnings: [...result.warnings, 'Fell back to full re-render'],
      };
    }

    return result;
  }

  /**
   * Check if hydration is possible (container has SSR content)
   */
  canHydrate(container: HTMLElement): boolean {
    return container.dataset.ssr === 'true' && container.firstElementChild !== null;
  }
}
