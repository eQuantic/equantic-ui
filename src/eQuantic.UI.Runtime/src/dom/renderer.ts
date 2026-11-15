/**
 * eQuantic.UI Runtime - DOM Renderer
 *
 * Handles initial rendering and efficient updates using the reconciler
 */

import { HtmlNode } from '../core/types';
import { getReconciler, HydrationResult } from './reconciler';
import { releaseDetachedScrollViewports } from '../shared/scroll-viewports';
import { releaseDetachedTableViewports } from '../shared/table-viewports';

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
   * Patches ONE subtree of what the page drew: `drawn`, a node of the current tree, becomes `next`,
   * and only the DOM `drawn` produced is reconciled (#1455, a render answered by what the page's owners
   * built). The DOM is found the way the reconciler finds it, the n-th node of its parent for the n-th
   * child. The current tree takes `next` in `drawn`'s place, so the next whole render diffs against
   * what the DOM now holds. False when `drawn` is not in the current tree: the caller renders whole.
   */
  patch(drawn: HtmlNode, next: HtmlNode | null): boolean {
    const tree = this.previousVirtualDom;
    if (!this.container || !tree) return false;
    const path = pathTo(tree, drawn);
    if (!path) return false;
    const reconciler = getReconciler();
    if (path.length === 0) {
      if (!next) return false;
      reconciler.reconcile(this.container, drawn, next, 0);
      this.previousVirtualDom = next;
      return true;
    }
    // Each step is CHECKED against the node the tree says stands there: hydration adopts markup in
    // which an empty text the tree holds was never written, and from there on a child index points at
    // the next sibling. A whole render tolerates that; a patch would rewrite the wrong element.
    let parent = tree;
    let parentDom: Node | null = this.container.childNodes[0] ?? null;
    if (!drew(parentDom, tree)) return false;
    for (const index of path.slice(0, -1)) {
      parent = parent.children[index];
      parentDom = parentDom?.childNodes[index] ?? null;
      if (!drew(parentDom, parent)) return false;
    }
    if (!(parentDom instanceof Element)) return false;
    const index = path[path.length - 1];
    if (!drew(parentDom.childNodes[index] ?? null, drawn)) return false;
    reconciler.reconcile(parentDom, drawn, next, index);
    if (next) parent.children[index] = next;
    else parent.children.splice(index, 1);
    return true;
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
      // No pass follows an unmount, so nothing else would notice the scroll views it took away.
      releaseDetachedScrollViewports();
      releaseDetachedTableViewports();
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

/** The child indices from `tree` down to `target`, by identity, or null when it is not in the tree. */
function pathTo(tree: HtmlNode, target: HtmlNode): number[] | null {
  const path: number[] = [];
  const find = (node: HtmlNode): boolean => {
    if (node === target) return true;
    const children = node.children ?? [];
    for (let index = 0; index < children.length; index++) {
      path.push(index);
      if (find(children[index])) return true;
      path.pop();
    }
    return false;
  };
  return find(tree) ? path : null;
}

/** Whether `dom` is the kind of node `node` draws: a text for a text, an element of its tag. */
function drew(dom: Node | null, node: HtmlNode): boolean {
  if (!dom) return false;
  if (node.tag === '#text') return dom.nodeType === 3;
  if (node.tag === '#comment') return dom.nodeType === 8;
  return dom instanceof Element && dom.localName === node.tag.toLowerCase();
}
