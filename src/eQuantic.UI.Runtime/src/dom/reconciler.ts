/**
 * eQuantic.UI Runtime - Efficient DOM Reconciliation
 *
 * Implements a virtual DOM diffing and patching algorithm
 * to minimize DOM operations and preserve state.
 */

import { HtmlNode, EventHandler } from '../core/types';



/**
 * Hydration result for debugging
 */
export interface HydrationResult {
  success: boolean;
  attachedListeners: number;
  warnings: string[];
}

/**
 * Reconciler manages efficient DOM updates
 */
export class Reconciler {
  private eventListeners: WeakMap<HTMLElement, Map<string, EventHandler>> = new WeakMap();

  /**
   * Reconcile (diff and patch) old and new virtual DOM trees
   */
  reconcile(
    parentElement: HTMLElement,
    oldNode: HtmlNode | null,
    newNode: HtmlNode | null,
    index: number = 0
  ): void {
    const currentElement = parentElement.childNodes[index] as HTMLElement | Text | null;

    // Case 1: No old node - create new element
    if (!oldNode && newNode) {
      const newElement = this.createDomElement(newNode);
      if (currentElement) {
        parentElement.insertBefore(newElement, currentElement);
      } else {
        parentElement.appendChild(newElement);
      }
      return;
    }

    // Case 2: No new node - remove old element
    if (!newNode) {
      if (currentElement) {
        this.cleanupEventListeners(currentElement);
        parentElement.removeChild(currentElement);
      }
      return;
    }

    // At this point, both oldNode and newNode are guaranteed to be non-null
    if (!oldNode || !newNode) {
      return; // This should never happen, but satisfies TypeScript
    }

    // Case 3: Different node types - replace
    if (this.isDifferentNodeType(oldNode, newNode)) {
      const newElement = this.createDomElement(newNode);
      if (currentElement) {
        this.cleanupEventListeners(currentElement);
        parentElement.replaceChild(newElement, currentElement);
      } else {
        parentElement.appendChild(newElement);
      }
      return;
    }

    // Case 4: Same node type - update in place
    if (currentElement) {
      // Text nodes
      if (newNode.tag === '#text') {
        if (oldNode.textContent !== newNode.textContent) {
          currentElement.textContent = newNode.textContent || '';
        }
        return;
      }

      // Comment nodes
      if (newNode.tag === '#comment') {
        const text = newNode.attributes?.text || '';
        if (currentElement instanceof Comment) {
          if (currentElement.textContent !== text) {
            currentElement.textContent = text;
          }
        }
        return;
      }

      // Element nodes
      if (currentElement instanceof HTMLElement) {
        this.updateAttributes(currentElement, oldNode.attributes || {}, newNode.attributes || {});
        this.updateEventListeners(currentElement, oldNode.events || {}, newNode.events || {});
        this.reconcileChildren(currentElement, oldNode.children || [], newNode.children || []);
      }
    }
  }

  /**
   * Check if nodes are different types
   */
  private isDifferentNodeType(oldNode: HtmlNode, newNode: HtmlNode): boolean {
    return oldNode.tag !== newNode.tag || oldNode.key !== newNode.key;
  }

  /**
   * Create a DOM element from virtual node
   */
  createDomElement(node: HtmlNode): Node {
    // Text node
    if (node.tag === '#text') {
      return document.createTextNode(node.textContent || '');
    }

    // Comment node
    if (node.tag === '#comment') {
      return document.createComment(node.attributes?.text || '');
    }

    // Create element
    const element = document.createElement(node.tag);

    // Set attributes
    for (const [key, value] of Object.entries(node.attributes)) {
      if (value !== undefined && value !== null) {
        this.applyAttribute(element as HTMLElement, key, value);
      }
    }

    // Attach event handlers
    this.attachEventListeners(element, node.events);

    // Render children
    for (const child of (node.children || [])) {
      element.appendChild(this.createDomElement(child));
    }

    return element;
  }

  /**
   * Apply attribute or property to element
   */
  private applyAttribute(element: HTMLElement, key: string, value: any): void {
    if (value === undefined || value === null) {
      element.removeAttribute(key);
      if (key === 'value' && (element instanceof HTMLInputElement || element instanceof HTMLTextAreaElement)) {
        element.value = '';
      }
      if (key === 'checked' && (element instanceof HTMLInputElement)) {
        element.checked = false;
      }
      return;
    }

    const strValue = String(value);
    element.setAttribute(key, strValue);

    if (key === 'value' && (element instanceof HTMLInputElement || element instanceof HTMLTextAreaElement)) {
      element.value = strValue;
    } else if (key === 'checked' && (element instanceof HTMLInputElement)) {
      element.checked = true;
    }
  }

  /**
   * Update element attributes (only changed ones)
   */
  private updateAttributes(
    element: HTMLElement,
    oldAttrs: Record<string, string | undefined>,
    newAttrs: Record<string, string | undefined>
  ): void {
    // Remove old attributes
    for (const key of Object.keys(oldAttrs)) {
      if (!(key in newAttrs)) {
        this.applyAttribute(element, key, null);
      }
    }

    // Set new/updated attributes
    for (const [key, value] of Object.entries(newAttrs)) {
      const oldValue = oldAttrs[key];
      if (value !== oldValue) {
        this.applyAttribute(element, key, value);
      }
    }
  }

  /**
   * Attach event listeners to element
   */
  private attachEventListeners(element: HTMLElement, events: Record<string, EventHandler>): void {
    const elementListeners = new Map<string, EventHandler>();
    for (const [eventName, handler] of Object.entries(events)) {
      if (!handler) continue;

      const wrappedHandler = this.createEventHandler(eventName, handler);
      element.addEventListener(eventName, wrappedHandler as unknown as EventListener);
      elementListeners.set(eventName, wrappedHandler as unknown as EventHandler);
    }
    
    if (elementListeners.size > 0) {
       this.eventListeners.set(element, elementListeners);
    }
  }

  /**
   * Update event listeners (remove old, add new)
   */
  private updateEventListeners(
    element: HTMLElement,
    oldEvents: Record<string, EventHandler>,
    newEvents: Record<string, EventHandler>
  ): void {
    const elementListeners = this.eventListeners.get(element) || new Map<string, EventHandler>();

    // Remove old event listeners
    for (const eventName of Object.keys(oldEvents)) {
      if (!(eventName in newEvents)) {
        const wrapped = elementListeners.get(eventName);
        if (wrapped) {
          element.removeEventListener(eventName, wrapped as unknown as EventListener);
          elementListeners.delete(eventName);
        }
      }
    }

    // Add new/updated event listeners
    for (const [eventName, handler] of Object.entries(newEvents)) {
      const oldHandler = oldEvents[eventName];

      // If handler changed, remove old and add new
      if (handler !== oldHandler) {
        // Remove old
        const oldWrapped = elementListeners.get(eventName);
        if (oldWrapped) {
          element.removeEventListener(eventName, oldWrapped as unknown as EventListener);
          elementListeners.delete(eventName);
        }

        // Add new
        if (handler) {
          const wrappedHandler = this.createEventHandler(eventName, handler);
          element.addEventListener(eventName, wrappedHandler as unknown as EventListener);
          elementListeners.set(eventName, wrappedHandler as unknown as EventHandler);
        }
      }
    }

    if (elementListeners.size > 0) {
      this.eventListeners.set(element, elementListeners);
    } else {
      this.eventListeners.delete(element);
    }
  }

  /**
   * Create a standardized event handler wrapper
   */
  private createEventHandler(eventName: string, handler: EventHandler): (e: Event) => void {
    return (e: Event) => {
      // 1. Value Change Events (Input, Change)
      if (eventName === 'change' || eventName === 'input') {
        const value = this.extractEventValue(e);
        (handler as (value: any) => void)(value);
        return;
      } 
      
      // 2. Void Events (Click, Submit) - typically often defined as Action() not Action(e)
      // We assume if it's a void C# action, we don't pass arguments, 
      // but TypeScript handler might be (e) => ... or () => ...
      // For safety, generic handlers pass the event.
      // Specific simplified handlers (like typical button clicks) might just be invoked.
      if (eventName === 'click' || eventName === 'submit') {
         // Try to detect if handler expects args? Hard in JS.
         // Pass event if it's a standard handler, but C# generation usually expects no args for simple Actions.
         // However, our unified type EventHandler might be Function.
         // Let's pass 'e' for general correctness, C# wrappers/bridging usually ignore extra args if not mapped.
         // BUT existing logic used: (handler as () => void)();
         // Let's keep that pattern for click for now to avoid breaking existing void callbacks.
         (handler as any)(); 
         return;
      }

      // 3. General Events
      handler(e);
    };
  }

  /**
   * Extract meaningful value from an event target
   */
  private extractEventValue(e: Event): any {
    const target = e.target as HTMLElement;

    if (target instanceof HTMLInputElement) {
      if (target.type === 'checkbox') {
        return target.checked;
      }
      if (target.type === 'number') {
        return target.valueAsNumber; // Native number support
      }
      if (target.type === 'file') {
        return target.files; // File support
      }
      return target.value;
    }

    if (target instanceof HTMLTextAreaElement) {
      return target.value;
    }

    if (target instanceof HTMLSelectElement) {
      if (target.multiple) {
        return Array.from(target.selectedOptions).map(opt => opt.value);
      }
      return target.value;
    }

    // Custom elements or contenteditable could go here
    return (target as any).value;
  }

  /**
   * Reconcile children arrays
   */
  private reconcileChildren(
    parentElement: HTMLElement,
    oldChildren: HtmlNode[],
    newChildren: HtmlNode[]
  ): void {
    const safeOld = oldChildren || [];
    const safeNew = newChildren || [];
    const maxLength = Math.max(safeOld.length, safeNew.length);

    for (let i = 0; i < maxLength; i++) {
        const oldChild = safeOld[i];
        const newChild = safeNew[i];
        this.reconcile(parentElement, oldChild, newChild, i);
    }
  }

  /**
   * Cleanup event listeners for element and its children
   */
  private cleanupEventListeners(node: Node): void {
    if (node instanceof HTMLElement) {
      // Remove listeners for this element
      const elementListeners = this.eventListeners.get(node);
      if (elementListeners) {
        for (const [eventName, handler] of elementListeners.entries()) {
          node.removeEventListener(eventName, handler as unknown as EventListener);
        }
        this.eventListeners.delete(node);
      }

      // Recursively cleanup children
      for (let i = 0; i < node.childNodes.length; i++) {
        this.cleanupEventListeners(node.childNodes[i]);
      }
    }
  }

  /**
   * Hydrate existing DOM with event listeners from virtual DOM.
   * This is used for SSR hydration where the HTML is already rendered by the server.
   * Instead of replacing the DOM, we walk the existing elements and attach event listeners.
   */
  hydrate(
    existingElement: Node,
    virtualNode: HtmlNode,
    result: HydrationResult = { success: true, attachedListeners: 0, warnings: [] }
  ): HydrationResult {
    // Text node - nothing to hydrate
    if (virtualNode.tag === '#text') {
      if (existingElement.nodeType !== Node.TEXT_NODE) {
        result.warnings.push(`Expected text node, found ${existingElement.nodeName}`);
        result.success = false;
      }
      return result;
    }

    // Comment node - nothing to hydrate
    if (virtualNode.tag === '#comment') {
      return result;
    }

    // Element node - attach events and recurse children
    if (!(existingElement instanceof HTMLElement)) {
      result.warnings.push(`Expected HTMLElement for tag '${virtualNode.tag}', found ${existingElement.nodeName}`);
      result.success = false;
      return result;
    }

    // Validate tag match
    if (existingElement.tagName.toLowerCase() !== virtualNode.tag.toLowerCase()) {
      result.warnings.push(`Tag mismatch: expected '${virtualNode.tag}', found '${existingElement.tagName.toLowerCase()}'`);
      result.success = false;
      return result;
    }

    // Attach event listeners
    if (virtualNode.events) {
      this.attachEventListeners(existingElement, virtualNode.events);
      result.attachedListeners += Object.keys(virtualNode.events).length;
    }

    // Recursively hydrate children
    const virtualChildren = virtualNode.children || [];
    const existingChildren = Array.from(existingElement.childNodes).filter(
      node => node.nodeType === Node.ELEMENT_NODE ||
              (node.nodeType === Node.TEXT_NODE && node.textContent?.trim())
    );

    for (let i = 0; i < virtualChildren.length; i++) {
      const virtualChild = virtualChildren[i];
      const existingChild = existingChildren[i];

      if (!existingChild) {
        result.warnings.push(`Missing child at index ${i} for tag '${virtualNode.tag}'`);
        result.success = false;
        continue;
      }

      this.hydrate(existingChild, virtualChild, result);
    }

    return result;
  }

  /**
   * Hydrate a container with the root virtual node.
   * The container should have SSR-rendered HTML as its content.
   */
  hydrateRoot(container: HTMLElement, virtualNode: HtmlNode): HydrationResult {
    const result: HydrationResult = { success: true, attachedListeners: 0, warnings: [] };

    // Get the first element child of the container (the SSR root)
    const existingRoot = container.firstElementChild;
    if (!existingRoot) {
      result.warnings.push('No existing element to hydrate');
      result.success = false;
      return result;
    }

    return this.hydrate(existingRoot, virtualNode, result);
  }

  /**
   * Cleanup all event listeners (Reset tracking)
   */
  dispose(): void {
    // Note: WeakMap is not iterable, so we cannot manually remove all listeners from the DOM elements themselves here.
    // However, re-initializing the map releases our references, allowing the nodes to be garbage collected.
    this.eventListeners = new WeakMap();
  }

  /**
   * Get the number of tracked event listeners (no longer supported with WeakMap)
   */
  getEventListenerCount(): number {
    return -1;
  }
}

/**
 * Global reconciler instance
 */
let globalReconciler: Reconciler | null = null;

/**
 * Get or create the global reconciler
 */
export function getReconciler(): Reconciler {
  if (!globalReconciler) {
    globalReconciler = new Reconciler();
  }
  return globalReconciler;
}

/**
 * Reset the global reconciler (useful for testing)
 */
export function resetReconciler(): void {
  if (globalReconciler) {
    globalReconciler.dispose();
  }
  globalReconciler = null;
}
