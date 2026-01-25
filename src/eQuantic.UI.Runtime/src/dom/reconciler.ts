/**
 * eQuantic.UI Runtime - Efficient DOM Reconciliation
 *
 * Implements a virtual DOM diffing and patching algorithm
 * to minimize DOM operations and preserve state.
 */

import { HtmlNode, EventHandler } from '../core/types';

/**
 * Event listener tracker for cleanup
 */
interface EventListener {
  element: HTMLElement;
  eventName: string;
  handler: EventListenerOrEventListenerObject;
}

/**
 * Reconciler manages efficient DOM updates
 */
export class Reconciler {
  private eventListeners: EventListener[] = [];

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
    return oldNode.tag !== newNode.tag;
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
        element.setAttribute(key, String(value));
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
        element.removeAttribute(key);
      }
    }

    // Set new/updated attributes
    for (const [key, value] of Object.entries(newAttrs)) {
      const oldValue = oldAttrs[key];
      if (value !== oldValue) {
        if (value !== undefined && value !== null) {
          element.setAttribute(key, String(value));
        } else {
          element.removeAttribute(key);
        }
      }
    }
  }

  /**
   * Attach event listeners to element
   */
  private attachEventListeners(element: HTMLElement, events: Record<string, EventHandler>): void {
    for (const [eventName, handler] of Object.entries(events)) {
      if (!handler) continue;

      const wrappedHandler = this.createEventHandler(eventName, handler);

      element.addEventListener(eventName, wrappedHandler);

      // Track for cleanup
      this.eventListeners.push({
        element,
        eventName,
        handler: wrappedHandler,
      });
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
    // Remove old event listeners
    for (const eventName of Object.keys(oldEvents)) {
      if (!(eventName in newEvents)) {
        this.removeEventListener(element, eventName);
      }
    }

    // Add new/updated event listeners
    for (const [eventName, handler] of Object.entries(newEvents)) {
      const oldHandler = oldEvents[eventName];

      // If handler changed, remove old and add new
      if (handler !== oldHandler) {
        // Remove old
        this.removeEventListener(element, eventName);

        // Add new
        if (handler) {
          const wrappedHandler = this.createEventHandler(eventName, handler);
          element.addEventListener(eventName, wrappedHandler);

          this.eventListeners.push({
            element,
            eventName,
            handler: wrappedHandler,
          });
        }
      }
    }
  }

  /**
   * Safely remove an event listener from tracking and DOM
   */
  private removeEventListener(element: HTMLElement, eventName: string): void {
    const listener = this.eventListeners.find(
      (l) => l.element === element && l.eventName === eventName
    );
    if (listener) {
      element.removeEventListener(eventName, listener.handler);
      this.eventListeners = this.eventListeners.filter((l) => l !== listener);
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
    const maxLength = Math.max(oldChildren.length, newChildren.length);

    for (let i = 0; i < maxLength; i++) {
      const oldChild = oldChildren[i] || null;
      const newChild = newChildren[i] || null;

      if (!newChild && oldChild) {
        // Remove extra old children
        const childElement = parentElement.childNodes[i];
        if (childElement) {
          this.cleanupEventListeners(childElement);
          parentElement.removeChild(childElement);
        }
      } else if (newChild) {
        this.reconcile(parentElement, oldChild, newChild, i);
      }
    }
  }

  /**
   * Cleanup event listeners for element and its children
   */
  private cleanupEventListeners(node: Node): void {
    if (node instanceof HTMLElement) {
      // Remove listeners for this element
      this.eventListeners = this.eventListeners.filter((listener) => {
        if (listener.element === node) {
          node.removeEventListener(listener.eventName, listener.handler);
          return false;
        }
        return true;
      });

      // Recursively cleanup children
      for (let i = 0; i < node.childNodes.length; i++) {
        this.cleanupEventListeners(node.childNodes[i]);
      }
    }
  }

  /**
   * Cleanup all event listeners
   */
  dispose(): void {
    for (const listener of this.eventListeners) {
      listener.element.removeEventListener(listener.eventName, listener.handler);
    }
    this.eventListeners = [];
  }

  /**
   * Get the number of tracked event listeners (for debugging/testing)
   */
  getEventListenerCount(): number {
    return this.eventListeners.length;
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
