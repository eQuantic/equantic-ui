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
    index: number = 0,
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
    for (const child of node.children || []) {
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
      if (
        key === 'value' &&
        (element instanceof HTMLInputElement || element instanceof HTMLTextAreaElement)
      ) {
        element.value = '';
      }
      if (key === 'checked' && element instanceof HTMLInputElement) {
        element.checked = false;
      }
      return;
    }

    const strValue = String(value);
    element.setAttribute(key, strValue);

    if (
      key === 'value' &&
      (element instanceof HTMLInputElement || element instanceof HTMLTextAreaElement)
    ) {
      element.value = strValue;
    } else if (key === 'checked' && element instanceof HTMLInputElement) {
      element.checked = true;
    }
  }

  /**
   * Update element attributes (only changed ones)
   */
  private updateAttributes(
    element: HTMLElement,
    oldAttrs: Record<string, string | undefined>,
    newAttrs: Record<string, string | undefined>,
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
    newEvents: Record<string, EventHandler>,
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
        return Array.from(target.selectedOptions).map((opt) => opt.value);
      }
      return target.value;
    }

    // Custom elements or contenteditable could go here
    return (target as any).value;
  }

  /**
   * Reconcile children arrays
   */
  /**
   * Reconcile children using Keyed Diffing (Longest Increasing Subsequence)
   * This algorithm is optimized for moving nodes instead of recreating them.
   */
  /**
   * Reconcile children using Keyed Diffing (Longest Increasing Subsequence)
   * This algorithm is optimized for moving nodes instead of recreating them.
   */
  private reconcileChildren(
    parentElement: HTMLElement,
    oldChildren: HtmlNode[],
    newChildren: HtmlNode[],
  ): void {
    const oldCh = oldChildren || [];
    const newCh = newChildren || [];

    let oldStartIdx = 0;
    let newStartIdx = 0;
    let oldEndIdx = oldCh.length - 1;
    let newEndIdx = newCh.length - 1;

    let oldStartNode = oldCh[0];
    let newStartNode = newCh[0];
    let oldEndNode = oldCh[oldEndIdx];
    let newEndNode = newCh[newEndIdx];

    const childNodes = parentElement.childNodes;

    // 1. Sync head (prefix optimization)
    while (oldStartIdx <= oldEndIdx && newStartIdx <= newEndIdx) {
      if (!this.isSameKey(oldStartNode, newStartNode)) break;
      this.reconcile(parentElement, oldStartNode, newStartNode, oldStartIdx);
      oldStartNode = oldCh[++oldStartIdx];
      newStartNode = newCh[++newStartIdx];
    }

    // 2. Sync tail (suffix optimization)
    while (oldStartIdx <= oldEndIdx && newStartIdx <= newEndIdx) {
      if (!this.isSameKey(oldEndNode, newEndNode)) break;
      // Note: Reconcile uses index. For suffix, we need to pass the *actual* DOM index.
      // Since we haven't modified the list yet, `childNode[index]` works if index is from start.
      // But `oldEndIdx` is relative to the *whole* list.
      // Correct reconciliation needs the DOM element.
      // Ideally, `reconcile` should accept the element directly to avoid index lookup ambiguity.
      // However, reusing existing `reconcile`: it looks up `childNodes[index]`.
      // `childNodes` length matches `oldCh.length` currently.
      // So `oldEndIdx` is valid.
      this.reconcile(parentElement, oldEndNode, newEndNode, oldEndIdx);
      oldEndNode = oldCh[--oldEndIdx];
      newEndNode = newCh[--newEndIdx];
    }

    // 3. Mount new nodes if old list is exhausted
    if (oldStartIdx > oldEndIdx) {
      if (newStartIdx <= newEndIdx) {
        // Anchor is the node *after* the new range we are inserting.
        // Logic: newEndIdx has been decremented. So the node at `newEndIdx + 1` (in the new List)
        // corresponds to a node that is already consistent in the DOM (the start of the suffix).
        // BUT we need the DOM node anchor.
        // We know `newEndIdx + 1` in `newCh` is `newCh[newEndIdx + 1]`.
        // Where is it in the DOM?
        // It's the node currently at `oldEndIdx + 1`?
        // Since we processed the tail, `childNodes` at the end are sync'd.
        // So `childNodes[newEndIdx + 1]` relative to current DOM state?
        // Actually, `childNodes.length` is static so far.
        // The anchor node is `childNodes[oldEndIdx + 1]`.
        // Wait, `oldEndIdx` was decremented. So `oldEndIdx + 1` points to the first node of the suffix.
        const anchorIndex = oldEndIdx + 1;
        const anchor = anchorIndex < childNodes.length ? childNodes[anchorIndex] : null;

        while (newStartIdx <= newEndIdx) {
          const newNode = newCh[newStartIdx++];
          const dom = this.createDomElement(newNode);
          parentElement.insertBefore(dom, anchor);
        }
      }
    }
    // 4. Remove old nodes if new list is exhausted
    else if (newStartIdx > newEndIdx) {
      // Remove backwards to avoid index shifting issues
      while (oldStartIdx <= oldEndIdx) {
        const nodeToRemove = childNodes[oldEndIdx];
        if (nodeToRemove) {
          this.cleanupEventListeners(nodeToRemove);
          parentElement.removeChild(nodeToRemove);
        }
        oldEndIdx--;
      }
    } else {
      // 5. Unknown sequence - efficient move using LIS
      // s1 (old length) unused
      const s2 = newEndIdx - newStartIdx + 1;

      const keyMap = new Map<string | number, number>();
      for (let i = newStartIdx; i <= newEndIdx; i++) {
        const key = newCh[i].key;
        if (key != null) keyMap.set(key, i);
      }

      // 0 means new node must be mounted
      // >0 means (oldIndex + 1)
      const newIndexToOldIndexMap = new Int32Array(s2);

      // Map to store reference to DOM nodes for new indices (needed for moves)
      const newIndexToNodeMap = new Map<number, Node>();

      let moved = false;
      let maxNewIndexSoFar = 0;

      // Map matching nodes and Patch
      const toRemove: Node[] = [];

      for (let i = oldStartIdx; i <= oldEndIdx; i++) {
        const oldNode = oldCh[i];
        let newIndex: number | undefined;

        if (oldNode.key != null) {
          newIndex = keyMap.get(oldNode.key);
        } else {
          // Try to find keyless match
          for (let j = 0; j < s2; j++) {
            if (newIndexToOldIndexMap[j] === 0 && newCh[newStartIdx + j].key == null) {
              newIndex = newStartIdx + j;
              break;
            }
          }
        }

        if (newIndex !== undefined) {
          // Match found
          newIndexToOldIndexMap[newIndex - newStartIdx] = i + 1;
          if (newIndex >= maxNewIndexSoFar) {
            maxNewIndexSoFar = newIndex;
          } else {
            moved = true;
          }
          // Patch using the current DOM index (i)
          // Store DOM node for logic later (i corresponds to childNodes index before removals)
          const domNode = childNodes[i];
          newIndexToNodeMap.set(newIndex, domNode);

          this.reconcile(parentElement, oldNode, newCh[newIndex], i);
        } else {
          // No match, mark for removal
          const node = childNodes[i];
          if (node) toRemove.push(node);
        }
      }

      // Remove unmatched nodes
      for (const node of toRemove) {
        this.cleanupEventListeners(node);
        parentElement.removeChild(node);
      }

      // Move and Mount
      if (moved) {
        const seq = this.getSequence(newIndexToOldIndexMap);
        let j = seq.length - 1;

        // Find the initial anchor (start of Suffix)
        // Since we removed unrelated nodes, Suffix is at childNodes[something].
        // But simpler: just initialize nextAnchor from suffix.
        // We know suffix moves backwards from oldEndIdx (original).
        // If we look at childNodes, it is: [Prefix (stable), Middle (mixed), Suffix (stable)].
        // oldStartIdx now points to start of middle (after prefix).
        // Since we removed nodes, indices changed.

        // Standard Vue 3 approach:
        // iterate i from s2-1 to 0.
        // next node index = newStartIdx + i + 1.
        // If next node index < newCh.length, anchor = newIndexToNodeMap.get(next index)?
        // OR we can just keep a `nextAnchor` var that we update every iteration.

        let nextAnchor: Node | null = null;
        if (newEndIdx + 1 < newCh.length) {
          // Suffix exists.
          // The first node of suffix is...
          // We can't easily find it via map because it wasn't mapped (skipped in step 2).
          // But we know it's at the end of the current DOM list?
          // Actually, if we use `childNodes.item(childNodes.length - suffixLength)`, we can find it.
          // OR simpler:
          // Since we know Suffix is stable at the end.
          // `childNodes[childNodes.length - (newCh.length - 1 - newEndIdx)]`
          // Let's rely on `nextAnchor` updating in the loop.
          // For the first iteration (last item in middle), anchor is first item of suffix.
          // Which is `childNodes[end]`.
          // Because we only removed items from middle.
          // `childNodes` has: Prefix + RemainingMiddle + Suffix.
          // We are inserting/moving Middle items before Suffix.
          // So `nextAnchor` initially = `childNodes[childNodes.length - suffixCount]`?
          // Or simpler: `nextAnchor = parentElement.childNodes[oldEndIdx + 1 - removedCount]`.
          // Hard to calculate.

          // Better: `nextAnchor` = parentElement.childNodes[parentElement.childNodes.length - (newCh.length - 1 - newEndIdx)] ? No.

          // Let's rely on reference from `newIndexToNodeMap`? No, Suffix not in map.

          // WAIT. If we assume Suffix optimization logic worked,
          // `oldEndIdx` points to the last element of the Middle.
          // `oldEndIdx + 1` is start of Suffix.
          // But we removed nodes using `removeChild`.
          // However, `childNodes` list shrinks. Suffix shifts left.
          // But Suffix nodes are still effectively at the end.
          // Can we just grab `childNodes[childNodes.length - countOfSuffix]`? YES.
          const suffixCount = newCh.length - 1 - newEndIdx;
          if (suffixCount > 0) {
            nextAnchor = childNodes[childNodes.length - suffixCount];
          }
        }

        // Iterate backwards
        for (let i = s2 - 1; i >= 0; i--) {
          const currentNewIndex = newStartIdx + i;

          if (newIndexToOldIndexMap[i] === 0) {
            // New node - Mount
            const newNode = newCh[currentNewIndex];
            const dom = this.createDomElement(newNode);
            parentElement.insertBefore(dom, nextAnchor);
            // Update map for future anchors?
            newIndexToNodeMap.set(currentNewIndex, dom);
            nextAnchor = dom;
          } else {
            // Move or Stay
            const dom = newIndexToNodeMap.get(currentNewIndex);
            if (dom) {
              if (j < 0 || i !== seq[j]) {
                // Move!
                parentElement.insertBefore(dom, nextAnchor);
              } else {
                // Stay (in LIS)
                j--;
              }
              nextAnchor = dom;
            }
          }
        }
      } else {
        // Not moved, just mount new ones (fallback for non-moved but inserted items)
        // Wait, if !moved, logic above doesn't run?
        // But what if we have insertions?
        // `if (moved)` covers reordering.
        // If not moved, we still might have `0` in `newIndexToOldIndexMap` (insertions).
        // Does `moved` track insertions?
        // `moved` tracks `newIndex < pos`. Order violation.
        // If we just insert `[A, B]` -> `[A, C, B]`.
        // A(0->0). C(new). B(1->2).
        // B index 2. pos = 0. 2 > 0. moved = false?
        // `pos` tracks `maxNewIndex`.
        // A: 0. pos=0.
        // B: 2. pos=2.
        // `moved` false.
        // So we enter `else`.
        // We MUST verify insertions in `else` block.
        // Iterate backwards and insert missing ones.

        let nextAnchor: Node | null = null;
        // Same anchor logic as above
        const suffixCount = newCh.length - 1 - newEndIdx;
        if (suffixCount > 0) {
          nextAnchor = childNodes[childNodes.length - suffixCount];
        }

        for (let i = s2 - 1; i >= 0; i--) {
          const currentNewIndex = newStartIdx + i;
          if (newIndexToOldIndexMap[i] === 0) {
            const newNode = newCh[currentNewIndex];
            const dom = this.createDomElement(newNode);
            parentElement.insertBefore(dom, nextAnchor);
            nextAnchor = dom;
          } else {
            // Update anchor
            // We need the DOM element to be the anchor for the *previous* item.
            // Since it's not moved, it is in the DOM.
            // We can get it from Map.
            const dom = newIndexToNodeMap.get(currentNewIndex);
            if (dom) nextAnchor = dom;
          }
        }
      }
    }
  }

  // Optimized reconcile function for the 'Unknown Sequence' block with DOM mapping
  // Redefining just the block 5 logic to be actually runnable
  // Note: I will replace the whole method content in the tool call, this comment is just internal thought.
  // The actual replacement string will contain the corrected logic.

  private isSameKey(n1: HtmlNode, n2: HtmlNode): boolean {
    return n1.tag === n2.tag && n1.key === n2.key;
  }

  private getSequence(arr: Int32Array): number[] {
    const p = arr.slice();
    const result = [0];
    let i, j, u, v, c;
    const len = arr.length;
    for (i = 0; i < len; i++) {
      const arrI = arr[i];
      if (arrI !== 0) {
        j = result[result.length - 1];
        if (arr[j] < arrI) {
          p[i] = j;
          result.push(i);
          continue;
        }
        u = 0;
        v = result.length - 1;
        while (u < v) {
          c = ((u + v) / 2) | 0;
          if (arr[result[c]] < arrI) {
            u = c + 1;
          } else {
            v = c;
          }
        }
        if (arrI < arr[result[u]]) {
          if (u > 0) {
            p[i] = result[u - 1];
          }
          result[u] = i;
        }
      }
    }
    u = result.length;
    v = result[u - 1];
    while (u-- > 0) {
      result[u] = v;
      v = p[v];
    }
    return result;
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
    result: HydrationResult = { success: true, attachedListeners: 0, warnings: [] },
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
      result.warnings.push(
        `Expected HTMLElement for tag '${virtualNode.tag}', found ${existingElement.nodeName}`,
      );
      result.success = false;
      return result;
    }

    // Validate tag match
    if (existingElement.tagName.toLowerCase() !== virtualNode.tag.toLowerCase()) {
      result.warnings.push(
        `Tag mismatch: expected '${virtualNode.tag}', found '${existingElement.tagName.toLowerCase()}'`,
      );
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
      (node) =>
        node.nodeType === Node.ELEMENT_NODE ||
        (node.nodeType === Node.TEXT_NODE && node.textContent?.trim()),
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
