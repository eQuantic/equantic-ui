/**
 * eQuantic.UI Runtime - DOM Renderer
 *
 * Handles initial rendering and efficient updates using the reconciler
 */

import { HtmlNode } from '../core/types';
import { getReconciler } from './reconciler';

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
    container.appendChild(dom);
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
}
