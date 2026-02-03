var __defProp = Object.defineProperty;
var __defNormalProp = (obj, key, value) => key in obj ? __defProp(obj, key, { enumerable: true, configurable: true, writable: true, value }) : obj[key] = value;
var __publicField = (obj, key, value) => __defNormalProp(obj, typeof key !== "symbol" ? key + "" : key, value);
class Component {
  constructor(props) {
    __publicField(this, "id");
    __publicField(this, "className");
    __publicField(this, "style");
    __publicField(this, "styleClass");
    __publicField(this, "title");
    __publicField(this, "hidden");
    __publicField(this, "tabIndex");
    __publicField(this, "dataAttributes");
    __publicField(this, "ariaAttributes");
    __publicField(this, "children", []);
    // Common Events
    __publicField(this, "onClick");
    __publicField(this, "onDoubleClick");
    __publicField(this, "onFocus");
    __publicField(this, "onBlur");
    __publicField(this, "onMouseEnter");
    __publicField(this, "onMouseLeave");
    __publicField(this, "onMouseDown");
    __publicField(this, "onMouseUp");
    __publicField(this, "onKeyDown");
    __publicField(this, "onKeyUp");
    __publicField(this, "onKeyPress");
    __publicField(this, "onChange");
    __publicField(this, "onInput");
    __publicField(this, "onSubmit");
    if (props && typeof props === "object") {
      Object.assign(this, props);
    }
  }
  buildAttributes() {
    const attrs = {};
    if (this.id) attrs["id"] = this.id;
    if (this.title) attrs["title"] = this.title;
    if (this.hidden) attrs["hidden"] = "true";
    if (this.tabIndex !== void 0) attrs["tabindex"] = this.tabIndex.toString();
    const classNames = [];
    if (this.className) classNames.push(this.className);
    if (this.styleClass) classNames.push(this.styleClass.generatedClassName);
    if (classNames.length > 0) attrs["class"] = classNames.join(" ");
    if (this.style) {
      attrs["style"] = Object.entries(this.style).map(([k, v]) => `${k}: ${v}`).join("; ");
    }
    if (this.dataAttributes) {
      for (const [key, value] of Object.entries(this.dataAttributes)) {
        attrs[`data-${key}`] = value;
      }
    }
    if (this.ariaAttributes) {
      for (const [key, value] of Object.entries(this.ariaAttributes)) {
        attrs[`aria-${key}`] = value;
      }
    }
    return attrs;
  }
  buildEvents() {
    const events = {};
    for (const prop of Object.keys(this)) {
      if (prop.startsWith("on") && prop.length > 2) {
        const eventName = prop.substring(2).toLowerCase();
        const handler = this[prop];
        if (handler && typeof handler === "function") {
          events[eventName] = handler;
        }
      }
    }
    return events;
  }
}
class HtmlElement extends Component {
  get htmlNode() {
    return {
      text: (content) => {
        this.content = content;
        return this.htmlNode;
      },
      attr: (name, value) => {
        if (!this.attributes) this.attributes = {};
        this.attributes[name] = value;
        return this.htmlNode;
      }
    };
  }
}
class Reconciler {
  constructor() {
    __publicField(this, "eventListeners", /* @__PURE__ */ new WeakMap());
  }
  /**
   * Reconcile (diff and patch) old and new virtual DOM trees
   */
  reconcile(parentElement, oldNode, newNode, index = 0) {
    var _a;
    const currentElement = parentElement.childNodes[index];
    if (!oldNode && newNode) {
      const newElement = this.createDomElement(newNode);
      if (currentElement) {
        parentElement.insertBefore(newElement, currentElement);
      } else {
        parentElement.appendChild(newElement);
      }
      return;
    }
    if (!newNode) {
      if (currentElement) {
        this.cleanupEventListeners(currentElement);
        parentElement.removeChild(currentElement);
      }
      return;
    }
    if (!oldNode || !newNode) {
      return;
    }
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
    if (currentElement) {
      if (newNode.tag === "#text") {
        if (oldNode.textContent !== newNode.textContent) {
          currentElement.textContent = newNode.textContent || "";
        }
        return;
      }
      if (newNode.tag === "#comment") {
        const text = ((_a = newNode.attributes) == null ? void 0 : _a.text) || "";
        if (currentElement instanceof Comment) {
          if (currentElement.textContent !== text) {
            currentElement.textContent = text;
          }
        }
        return;
      }
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
  isDifferentNodeType(oldNode, newNode) {
    return oldNode.tag !== newNode.tag || oldNode.key !== newNode.key;
  }
  /**
   * Create a DOM element from virtual node
   */
  createDomElement(node) {
    var _a;
    if (node.tag === "#text") {
      return document.createTextNode(node.textContent || "");
    }
    if (node.tag === "#comment") {
      return document.createComment(((_a = node.attributes) == null ? void 0 : _a.text) || "");
    }
    const element = document.createElement(node.tag);
    if (node.attributes) {
      for (const [key, value] of Object.entries(node.attributes)) {
        if (value !== void 0 && value !== null) {
          this.applyAttribute(element, key, value);
        }
      }
    }
    if (node.events) {
      this.attachEventListeners(element, node.events);
    }
    for (const child of node.children || []) {
      element.appendChild(this.createDomElement(child));
    }
    return element;
  }
  /**
   * Apply attribute or property to element
   */
  applyAttribute(element, key, value) {
    if (value === void 0 || value === null) {
      element.removeAttribute(key);
      if (key === "value" && (element instanceof HTMLInputElement || element instanceof HTMLTextAreaElement)) {
        element.value = "";
      }
      if (key === "checked" && element instanceof HTMLInputElement) {
        element.checked = false;
      }
      return;
    }
    const strValue = String(value);
    element.setAttribute(key, strValue);
    if (key === "value" && (element instanceof HTMLInputElement || element instanceof HTMLTextAreaElement)) {
      element.value = strValue;
    } else if (key === "checked" && element instanceof HTMLInputElement) {
      element.checked = true;
    }
  }
  /**
   * Update element attributes (only changed ones)
   */
  updateAttributes(element, oldAttrs, newAttrs) {
    for (const key of Object.keys(oldAttrs)) {
      if (!(key in newAttrs)) {
        this.applyAttribute(element, key, null);
      }
    }
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
  attachEventListeners(element, events) {
    const elementListeners = /* @__PURE__ */ new Map();
    for (const [eventName, handler] of Object.entries(events)) {
      if (!handler) continue;
      const wrappedHandler = this.createEventHandler(eventName, handler);
      element.addEventListener(eventName, wrappedHandler);
      elementListeners.set(eventName, wrappedHandler);
    }
    if (elementListeners.size > 0) {
      this.eventListeners.set(element, elementListeners);
    }
  }
  /**
   * Update event listeners (remove old, add new)
   */
  updateEventListeners(element, oldEvents, newEvents) {
    const elementListeners = this.eventListeners.get(element) || /* @__PURE__ */ new Map();
    for (const eventName of Object.keys(oldEvents)) {
      if (!(eventName in newEvents)) {
        const wrapped = elementListeners.get(eventName);
        if (wrapped) {
          element.removeEventListener(eventName, wrapped);
          elementListeners.delete(eventName);
        }
      }
    }
    for (const [eventName, handler] of Object.entries(newEvents)) {
      const oldHandler = oldEvents[eventName];
      if (handler !== oldHandler) {
        const oldWrapped = elementListeners.get(eventName);
        if (oldWrapped) {
          element.removeEventListener(eventName, oldWrapped);
          elementListeners.delete(eventName);
        }
        if (handler) {
          const wrappedHandler = this.createEventHandler(eventName, handler);
          element.addEventListener(eventName, wrappedHandler);
          elementListeners.set(eventName, wrappedHandler);
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
  createEventHandler(eventName, handler) {
    return (e) => {
      if (eventName === "change" || eventName === "input") {
        const value = this.extractEventValue(e);
        handler(value);
        return;
      }
      if (eventName === "click" || eventName === "submit") {
        handler();
        return;
      }
      handler(e);
    };
  }
  /**
   * Extract meaningful value from an event target
   */
  extractEventValue(e) {
    const target = e.target;
    if (target instanceof HTMLInputElement) {
      if (target.type === "checkbox") {
        return target.checked;
      }
      if (target.type === "number") {
        return target.valueAsNumber;
      }
      if (target.type === "file") {
        return target.files;
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
    return target.value;
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
  reconcileChildren(parentElement, oldChildren, newChildren) {
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
    while (oldStartIdx <= oldEndIdx && newStartIdx <= newEndIdx) {
      if (!this.isSameKey(oldStartNode, newStartNode)) break;
      this.reconcile(parentElement, oldStartNode, newStartNode, oldStartIdx);
      oldStartNode = oldCh[++oldStartIdx];
      newStartNode = newCh[++newStartIdx];
    }
    while (oldStartIdx <= oldEndIdx && newStartIdx <= newEndIdx) {
      if (!this.isSameKey(oldEndNode, newEndNode)) break;
      this.reconcile(parentElement, oldEndNode, newEndNode, oldEndIdx);
      oldEndNode = oldCh[--oldEndIdx];
      newEndNode = newCh[--newEndIdx];
    }
    if (oldStartIdx > oldEndIdx) {
      if (newStartIdx <= newEndIdx) {
        const anchorIndex = oldEndIdx + 1;
        const anchor = anchorIndex < childNodes.length ? childNodes[anchorIndex] : null;
        while (newStartIdx <= newEndIdx) {
          const newNode = newCh[newStartIdx++];
          const dom = this.createDomElement(newNode);
          parentElement.insertBefore(dom, anchor);
        }
      }
    } else if (newStartIdx > newEndIdx) {
      while (oldStartIdx <= oldEndIdx) {
        const nodeToRemove = childNodes[oldEndIdx];
        if (nodeToRemove) {
          this.cleanupEventListeners(nodeToRemove);
          parentElement.removeChild(nodeToRemove);
        }
        oldEndIdx--;
      }
    } else {
      const s2 = newEndIdx - newStartIdx + 1;
      const keyMap = /* @__PURE__ */ new Map();
      for (let i = newStartIdx; i <= newEndIdx; i++) {
        const key = newCh[i].key;
        if (key != null) keyMap.set(key, i);
      }
      const newIndexToOldIndexMap = new Int32Array(s2);
      const newIndexToNodeMap = /* @__PURE__ */ new Map();
      let moved = false;
      let maxNewIndexSoFar = 0;
      const toRemove = [];
      for (let i = oldStartIdx; i <= oldEndIdx; i++) {
        const oldNode = oldCh[i];
        let newIndex;
        if (oldNode.key != null) {
          newIndex = keyMap.get(oldNode.key);
        } else {
          for (let j = 0; j < s2; j++) {
            if (newIndexToOldIndexMap[j] === 0 && newCh[newStartIdx + j].key == null) {
              newIndex = newStartIdx + j;
              break;
            }
          }
        }
        if (newIndex !== void 0) {
          newIndexToOldIndexMap[newIndex - newStartIdx] = i + 1;
          if (newIndex >= maxNewIndexSoFar) {
            maxNewIndexSoFar = newIndex;
          } else {
            moved = true;
          }
          const domNode = childNodes[i];
          newIndexToNodeMap.set(newIndex, domNode);
          this.reconcile(parentElement, oldNode, newCh[newIndex], i);
        } else {
          const node = childNodes[i];
          if (node) toRemove.push(node);
        }
      }
      for (const node of toRemove) {
        this.cleanupEventListeners(node);
        parentElement.removeChild(node);
      }
      if (moved) {
        const seq = this.getSequence(newIndexToOldIndexMap);
        let j = seq.length - 1;
        let nextAnchor = null;
        if (newEndIdx + 1 < newCh.length) {
          const suffixCount = newCh.length - 1 - newEndIdx;
          if (suffixCount > 0) {
            nextAnchor = childNodes[childNodes.length - suffixCount];
          }
        }
        for (let i = s2 - 1; i >= 0; i--) {
          const currentNewIndex = newStartIdx + i;
          if (newIndexToOldIndexMap[i] === 0) {
            const newNode = newCh[currentNewIndex];
            const dom = this.createDomElement(newNode);
            parentElement.insertBefore(dom, nextAnchor);
            newIndexToNodeMap.set(currentNewIndex, dom);
            nextAnchor = dom;
          } else {
            const dom = newIndexToNodeMap.get(currentNewIndex);
            if (dom) {
              if (j < 0 || i !== seq[j]) {
                parentElement.insertBefore(dom, nextAnchor);
              } else {
                j--;
              }
              nextAnchor = dom;
            }
          }
        }
      } else {
        let nextAnchor = null;
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
  isSameKey(n1, n2) {
    return n1.tag === n2.tag && n1.key === n2.key;
  }
  getSequence(arr) {
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
          c = (u + v) / 2 | 0;
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
  cleanupEventListeners(node) {
    if (node instanceof HTMLElement) {
      const elementListeners = this.eventListeners.get(node);
      if (elementListeners) {
        for (const [eventName, handler] of elementListeners.entries()) {
          node.removeEventListener(eventName, handler);
        }
        this.eventListeners.delete(node);
      }
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
  hydrate(existingElement, virtualNode, result = { success: true, attachedListeners: 0, warnings: [] }) {
    if (virtualNode.tag === "#text") {
      if (existingElement.nodeType !== Node.TEXT_NODE) {
        result.warnings.push(`Expected text node, found ${existingElement.nodeName}`);
        result.success = false;
      }
      return result;
    }
    if (virtualNode.tag === "#comment") {
      return result;
    }
    if (!(existingElement instanceof HTMLElement)) {
      result.warnings.push(
        `Expected HTMLElement for tag '${virtualNode.tag}', found ${existingElement.nodeName}`
      );
      result.success = false;
      return result;
    }
    if (existingElement.tagName.toLowerCase() !== virtualNode.tag.toLowerCase()) {
      result.warnings.push(
        `Tag mismatch: expected '${virtualNode.tag}', found '${existingElement.tagName.toLowerCase()}'`
      );
      result.success = false;
      return result;
    }
    if (virtualNode.events) {
      this.attachEventListeners(existingElement, virtualNode.events);
      result.attachedListeners += Object.keys(virtualNode.events).length;
    }
    const virtualChildren = virtualNode.children || [];
    const existingChildren = Array.from(existingElement.childNodes).filter(
      (node) => {
        var _a;
        return node.nodeType === Node.ELEMENT_NODE || node.nodeType === Node.TEXT_NODE && ((_a = node.textContent) == null ? void 0 : _a.trim());
      }
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
  hydrateRoot(container, virtualNode) {
    const result = { success: true, attachedListeners: 0, warnings: [] };
    const existingRoot = container.firstElementChild;
    if (!existingRoot) {
      result.warnings.push("No existing element to hydrate");
      result.success = false;
      return result;
    }
    return this.hydrate(existingRoot, virtualNode, result);
  }
  /**
   * Cleanup all event listeners (Reset tracking)
   */
  dispose() {
    this.eventListeners = /* @__PURE__ */ new WeakMap();
  }
  /**
   * Get the number of tracked event listeners (no longer supported with WeakMap)
   */
  getEventListenerCount() {
    return -1;
  }
}
let globalReconciler = null;
function getReconciler() {
  if (!globalReconciler) {
    globalReconciler = new Reconciler();
  }
  return globalReconciler;
}
function resetReconciler() {
  if (globalReconciler) {
    globalReconciler.dispose();
  }
  globalReconciler = null;
}
class RenderManager {
  constructor() {
    __publicField(this, "previousVirtualDom", null);
    __publicField(this, "container", null);
  }
  /**
   * Initial mount - create and append DOM
   */
  mount(node, container) {
    this.container = container;
    this.previousVirtualDom = node;
    const reconciler = getReconciler();
    const dom = reconciler.createDomElement(node);
    container.innerHTML = "";
    container.appendChild(dom);
  }
  /**
   * Update - reconcile old and new virtual DOM
   */
  update(newNode) {
    if (!this.container) {
      throw new Error("Cannot update before mounting");
    }
    const reconciler = getReconciler();
    if (this.previousVirtualDom) {
      reconciler.reconcile(this.container, this.previousVirtualDom, newNode, 0);
    } else {
      const dom = reconciler.createDomElement(newNode);
      this.container.appendChild(dom);
    }
    this.previousVirtualDom = newNode;
  }
  /**
   * Unmount - cleanup
   */
  unmount() {
    if (this.container) {
      const reconciler = getReconciler();
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
  getContainer() {
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
  hydrate(node, container) {
    this.container = container;
    this.previousVirtualDom = node;
    const reconciler = getReconciler();
    const result = reconciler.hydrateRoot(container, node);
    if (!result.success) {
      console.warn("[eQuantic.UI] Hydration warnings:", result.warnings);
      console.warn("[eQuantic.UI] Falling back to full re-render");
      this.mount(node, container);
      return {
        success: false,
        attachedListeners: 0,
        warnings: [...result.warnings, "Fell back to full re-render"]
      };
    }
    return result;
  }
  /**
   * Check if hydration is possible (container has SSR content)
   */
  canHydrate(container) {
    return container.dataset.ssr === "true" && container.firstElementChild !== null;
  }
}
var ServiceLifetime = /* @__PURE__ */ ((ServiceLifetime2) => {
  ServiceLifetime2["Transient"] = "transient";
  ServiceLifetime2["Singleton"] = "singleton";
  ServiceLifetime2["Scoped"] = "scoped";
  return ServiceLifetime2;
})(ServiceLifetime || {});
class ServiceProvider {
  constructor(parent) {
    __publicField(this, "services", /* @__PURE__ */ new Map());
    __publicField(this, "scopedInstances", /* @__PURE__ */ new Map());
    __publicField(this, "parent", null);
    this.parent = parent || null;
  }
  /**
   * Register a singleton service
   */
  registerSingleton(key, factory) {
    this.services.set(key, {
      factory,
      lifetime: "singleton"
      /* Singleton */
    });
    return this;
  }
  /**
   * Register a transient service (new instance each time)
   */
  registerTransient(key, factory) {
    this.services.set(key, {
      factory,
      lifetime: "transient"
      /* Transient */
    });
    return this;
  }
  /**
   * Register a scoped service (per component subtree)
   */
  registerScoped(key, factory) {
    this.services.set(key, {
      factory,
      lifetime: "scoped"
      /* Scoped */
    });
    return this;
  }
  /**
   * Register an existing instance as singleton
   */
  registerInstance(key, instance) {
    const keyName = typeof key === "string" ? key : key.name;
    console.log("[ServiceProvider.registerInstance]", keyName, instance);
    this.services.set(key, {
      factory: () => instance,
      lifetime: "singleton",
      instance
    });
    return this;
  }
  /**
   * Get a service instance
   */
  getService(key) {
    const keyName = typeof key === "string" ? key : key.name;
    const descriptor = this.services.get(key);
    if (descriptor) {
      const result = this.resolveService(key, descriptor);
      console.log("[ServiceProvider.getService]", keyName, "→", result);
      return result;
    }
    if (this.parent) {
      return this.parent.getService(key);
    }
    console.log("[ServiceProvider.getService]", keyName, "→ undefined (not found)");
    return void 0;
  }
  /**
   * Get a required service (throws if not found)
   */
  getRequiredService(key) {
    const service = this.getService(key);
    if (service === void 0) {
      const keyName = typeof key === "string" ? key : key.name || "Unknown";
      throw new Error(`Required service not found: ${keyName}`);
    }
    return service;
  }
  /**
   * Check if a service is registered
   */
  hasService(key) {
    var _a;
    return this.services.has(key) || (((_a = this.parent) == null ? void 0 : _a.hasService(key)) ?? false);
  }
  /**
   * Create a child/scoped provider
   */
  createScope() {
    return new ServiceProvider(this);
  }
  /**
   * Dispose scoped instances
   */
  dispose() {
    for (const instance of this.scopedInstances.values()) {
      if (instance && typeof instance.dispose === "function") {
        instance.dispose();
      }
    }
    this.scopedInstances.clear();
  }
  /**
   * Resolve a service based on its descriptor
   */
  resolveService(key, descriptor) {
    switch (descriptor.lifetime) {
      case "singleton":
        if (!descriptor.instance) {
          descriptor.instance = descriptor.factory();
        }
        return descriptor.instance;
      case "scoped":
        if (!this.scopedInstances.has(key)) {
          this.scopedInstances.set(key, descriptor.factory());
        }
        return this.scopedInstances.get(key);
      case "transient":
      default:
        return descriptor.factory();
    }
  }
  /**
   * Get all registered service keys (for debugging)
   */
  getRegisteredServices() {
    const keys = Array.from(this.services.keys());
    if (this.parent) {
      keys.push(...this.parent.getRegisteredServices());
    }
    return Array.from(new Set(keys));
  }
}
let rootServiceProvider = null;
function getRootServiceProvider() {
  if (!rootServiceProvider) {
    rootServiceProvider = new ServiceProvider();
  }
  return rootServiceProvider;
}
function configureServices(configure) {
  const services = getRootServiceProvider();
  configure(services);
}
function resetServiceProvider() {
  if (rootServiceProvider) {
    rootServiceProvider.dispose();
  }
  rootServiceProvider = null;
}
if (typeof window !== "undefined") {
  window.getRootServiceProvider = getRootServiceProvider;
}
class ServiceCollectionBuilder {
  constructor() {
    __publicField(this, "provider", new ServiceProvider());
  }
  singleton(key, factory) {
    this.provider.registerSingleton(key, factory);
    return this;
  }
  transient(key, factory) {
    this.provider.registerTransient(key, factory);
    return this;
  }
  scoped(key, factory) {
    this.provider.registerScoped(key, factory);
    return this;
  }
  instance(key, instance) {
    this.provider.registerInstance(key, instance);
    return this;
  }
  build() {
    return this.provider;
  }
}
class StatelessComponent extends Component {
  get serviceProvider() {
    return getRootServiceProvider();
  }
  render() {
    const context = {
      getService: (key) => this.serviceProvider.getService(key),
      serviceProvider: this.serviceProvider
    };
    const component = this.build(context);
    return component.render();
  }
}
class StatefulComponent extends Component {
  constructor() {
    super(...arguments);
    __publicField(this, "_state", null);
    __publicField(this, "_mounted", false);
    __publicField(this, "_renderScheduled", false);
    __publicField(this, "_renderManager", new RenderManager());
  }
  get serviceProvider() {
    return getRootServiceProvider();
  }
  get state() {
    if (!this._state) {
      if (typeof this.createState !== "function") {
        throw new Error(`Component ${this.constructor.name} does not implement createState()`);
      }
      this._state = this.createState();
      if (!this._state) {
        throw new Error(`createState() returned null/undefined for ${this.constructor.name}`);
      }
      if (typeof this._state.setComponent === "function") {
        this._state.setComponent(this);
      } else {
        console.warn(
          `State object for ${this.constructor.name} missing setComponent method.`,
          this._state
        );
        if (typeof this._state._setComponent === "function") {
          this._state._setComponent(this);
        }
      }
      let hydrated = false;
      if (typeof window !== "undefined" && window.__INITIAL_STATE__ && this._state) {
        const initialState = window.__INITIAL_STATE__;
        Object.keys(initialState).forEach((key) => {
          if (this._state && typeof initialState[key] !== "function" && key in this._state) {
            this._state[key] = initialState[key];
          }
        });
        delete window.__INITIAL_STATE__;
        hydrated = true;
      }
      if (!hydrated) {
        this._state.onInit();
      }
    }
    return this._state;
  }
  render() {
    const context = {
      getService: (key) => this.serviceProvider.getService(key),
      serviceProvider: this.serviceProvider
    };
    this.state._context = context;
    const component = this.state.build(context);
    return component.render();
  }
  mount(container) {
    if (this._renderManager.canHydrate(container)) {
      const node = this.render();
      const result = this._renderManager.hydrate(node, container);
      if (result.success) {
        console.debug(
          `[eQuantic.UI] Hydrated ${this.constructor.name} with ${result.attachedListeners} event listeners`
        );
      }
      this._mounted = true;
      this.state.onMount();
    } else {
      const node = this.render();
      this._renderManager.mount(node, container);
      this._mounted = true;
      this.state.onMount();
    }
  }
  _scheduleRender() {
    if (this._renderScheduled) return;
    this._renderScheduled = true;
    requestAnimationFrame(() => {
      this._renderScheduled = false;
      if (this._mounted) {
        const node = this.render();
        this._renderManager.update(node);
        this.state.onUpdate();
      }
    });
  }
  unmount() {
    if (this._mounted && this._state) {
      this._state.onDispose();
      this._renderManager.unmount();
      this._mounted = false;
    }
  }
}
class ComponentState {
  constructor() {
    __publicField(this, "_component", null);
    __publicField(this, "_context", null);
    __publicField(this, "_needsRender", false);
  }
  get component() {
    if (!this._component) {
      throw new Error("State not initialized");
    }
    return this._component;
  }
  setComponent(component) {
    this._component = component;
  }
  /**
   * Update state and trigger re-render
   */
  setState(fn) {
    fn();
    this._needsRender = true;
    this.component._scheduleRender();
  }
  // Lifecycle hooks
  onInit() {
  }
  onMount() {
  }
  onUpdate() {
  }
  onDispose() {
  }
}
class HtmlStyle {
  constructor(init) {
    // Layout
    __publicField(this, "display");
    __publicField(this, "position");
    __publicField(this, "top");
    __publicField(this, "right");
    __publicField(this, "bottom");
    __publicField(this, "left");
    __publicField(this, "zIndex");
    // Flexbox
    __publicField(this, "flexDirection");
    __publicField(this, "flexWrap");
    __publicField(this, "justifyContent");
    __publicField(this, "alignItems");
    __publicField(this, "alignContent");
    __publicField(this, "gap");
    __publicField(this, "flex");
    __publicField(this, "flexGrow");
    __publicField(this, "flexShrink");
    // Grid
    __publicField(this, "gridTemplateColumns");
    __publicField(this, "gridTemplateRows");
    __publicField(this, "gridColumn");
    __publicField(this, "gridRow");
    __publicField(this, "gridAutoFlow");
    __publicField(this, "justifyItems");
    // Sizing
    __publicField(this, "width");
    __publicField(this, "height");
    __publicField(this, "minWidth");
    __publicField(this, "minHeight");
    __publicField(this, "maxWidth");
    __publicField(this, "maxHeight");
    // Spacing
    __publicField(this, "margin");
    __publicField(this, "marginTop");
    __publicField(this, "marginRight");
    __publicField(this, "marginBottom");
    __publicField(this, "marginLeft");
    __publicField(this, "padding");
    __publicField(this, "paddingTop");
    __publicField(this, "paddingRight");
    __publicField(this, "paddingBottom");
    __publicField(this, "paddingLeft");
    // Background
    __publicField(this, "background");
    __publicField(this, "backgroundColor");
    __publicField(this, "backgroundImage");
    // Border
    __publicField(this, "border");
    __publicField(this, "borderWidth");
    __publicField(this, "borderStyle");
    __publicField(this, "borderColor");
    __publicField(this, "borderRadius");
    // Typography
    __publicField(this, "color");
    __publicField(this, "fontFamily");
    __publicField(this, "fontSize");
    __publicField(this, "fontWeight");
    __publicField(this, "fontStyle");
    __publicField(this, "lineHeight");
    __publicField(this, "textAlign");
    __publicField(this, "textDecoration");
    __publicField(this, "textTransform");
    __publicField(this, "letterSpacing");
    // Effects
    __publicField(this, "boxShadow");
    __publicField(this, "opacity");
    __publicField(this, "cursor");
    __publicField(this, "overflow");
    __publicField(this, "overflowX");
    __publicField(this, "overflowY");
    __publicField(this, "transition");
    __publicField(this, "transform");
    if (init) {
      Object.assign(this, init);
    }
  }
  /**
   * Convert to CSS string for inline styles
   */
  toCssString() {
    const properties = [];
    const cssMap = {
      // Convert camelCase to kebab-case
      display: "display",
      position: "position",
      top: "top",
      right: "right",
      bottom: "bottom",
      left: "left",
      zIndex: "z-index",
      flexDirection: "flex-direction",
      flexWrap: "flex-wrap",
      justifyContent: "justify-content",
      alignItems: "align-items",
      alignContent: "align-content",
      gap: "gap",
      flex: "flex",
      flexGrow: "flex-grow",
      flexShrink: "flex-shrink",
      gridTemplateColumns: "grid-template-columns",
      gridTemplateRows: "grid-template-rows",
      gridColumn: "grid-column",
      gridRow: "grid-row",
      gridAutoFlow: "grid-auto-flow",
      justifyItems: "justify-items",
      width: "width",
      height: "height",
      minWidth: "min-width",
      minHeight: "min-height",
      maxWidth: "max-width",
      maxHeight: "max-height",
      margin: "margin",
      marginTop: "margin-top",
      marginRight: "margin-right",
      marginBottom: "margin-bottom",
      marginLeft: "margin-left",
      padding: "padding",
      paddingTop: "padding-top",
      paddingRight: "padding-right",
      paddingBottom: "padding-bottom",
      paddingLeft: "padding-left",
      background: "background",
      backgroundColor: "background-color",
      backgroundImage: "background-image",
      border: "border",
      borderWidth: "border-width",
      borderStyle: "border-style",
      borderColor: "border-color",
      borderRadius: "border-radius",
      color: "color",
      fontFamily: "font-family",
      fontSize: "font-size",
      fontWeight: "font-weight",
      fontStyle: "font-style",
      lineHeight: "line-height",
      textAlign: "text-align",
      textDecoration: "text-decoration",
      textTransform: "text-transform",
      letterSpacing: "letter-spacing",
      boxShadow: "box-shadow",
      opacity: "opacity",
      cursor: "cursor",
      overflow: "overflow",
      overflowX: "overflow-x",
      overflowY: "overflow-y",
      transition: "transition",
      transform: "transform"
    };
    for (const [key, cssName] of Object.entries(cssMap)) {
      const value = this[key];
      if (value !== void 0 && value !== null) {
        properties.push(`${cssName}: ${value}`);
      }
    }
    return properties.join("; ");
  }
}
class ServerActionsClient {
  constructor(baseUrl = "/api/_equantic/actions") {
    __publicField(this, "baseUrl");
    this.baseUrl = baseUrl;
  }
  /**
   * Invoke a server action.
   * @param actionId The action identifier (e.g., "Counter/Increment")
   * @param args Arguments to pass to the action
   */
  async invoke(actionId, args = []) {
    const response = await fetch(this.baseUrl, {
      method: "POST",
      headers: {
        "Content-Type": "application/json"
      },
      body: JSON.stringify({
        actionId,
        arguments: args
      })
    });
    const data = await response.json();
    if (!data.success) {
      throw new Error(data.error || `Server action failed: ${actionId}`);
    }
    return data.result;
  }
}
let globalServerActionsClient = null;
function getServerActionsClient() {
  if (!globalServerActionsClient) {
    globalServerActionsClient = new ServerActionsClient();
  }
  return globalServerActionsClient;
}
function configureServerActions(baseUrl) {
  globalServerActionsClient = new ServerActionsClient(baseUrl);
}
function resetServerActionsClient() {
  globalServerActionsClient = null;
}
function format(value, format2, alignment) {
  if (value === null || value === void 0) return "";
  let result = String(value);
  if (format2) {
    if (typeof value === "number") {
      result = formatNumber(value, format2);
    } else if (value instanceof Date) {
      result = formatDate(value, format2);
    }
  }
  if (alignment) {
    const width = Math.abs(alignment);
    if (result.length < width) {
      const padding = " ".repeat(width - result.length);
      return alignment > 0 ? padding + result : result + padding;
    }
  }
  return result;
}
function formatNumber(value, format2) {
  const specifier = format2[0].toUpperCase();
  const precision = format2.length > 1 ? parseInt(format2.slice(1)) : 2;
  switch (specifier) {
    case "C":
      return value.toLocaleString(void 0, {
        style: "currency",
        currency: "USD",
        minimumFractionDigits: precision,
        maximumFractionDigits: precision
      });
    case "N":
      return value.toLocaleString(void 0, {
        minimumFractionDigits: precision,
        maximumFractionDigits: precision
      });
    case "P":
      return value.toLocaleString(void 0, {
        style: "percent",
        minimumFractionDigits: precision,
        maximumFractionDigits: precision
      });
    case "F":
      return value.toFixed(precision);
    case "D":
      return Math.round(value).toString().padStart(precision, "0");
    case "X":
      return Math.floor(value).toString(16).toUpperCase().padStart(precision, "0");
    default:
      return value.toString();
  }
}
function formatDate(value, format2) {
  const yyyy = value.getFullYear().toString();
  const MM = (value.getMonth() + 1).toString().padStart(2, "0");
  const dd = value.getDate().toString().padStart(2, "0");
  const HH = value.getHours().toString().padStart(2, "0");
  const mm = value.getMinutes().toString().padStart(2, "0");
  const ss = value.getSeconds().toString().padStart(2, "0");
  return format2.replace(/yyyy/g, yyyy).replace(/MM/g, MM).replace(/dd/g, dd).replace(/HH/g, HH).replace(/mm/g, mm).replace(/ss/g, ss);
}
function parseEnum(value, enumType) {
  if (typeof value === "number") {
    return enumType[value] !== void 0 ? enumType[value] : void 0;
  }
  const strValue = String(value);
  if (enumType[strValue] !== void 0) {
    return enumType[strValue];
  }
  const keys = Object.keys(enumType);
  const matchedKey = keys.find((k) => k.toLowerCase() === strValue.toLowerCase());
  if (matchedKey) {
    return enumType[matchedKey];
  }
  const values = Object.values(enumType);
  const matchedValue = values.find(
    (v) => String(v).toLowerCase() === strValue.toLowerCase()
  );
  if (matchedValue !== void 0) {
    return matchedValue;
  }
  return void 0;
}
class StyleBuilder {
  constructor(baseClass) {
    __publicField(this, "classes", []);
    if (baseClass) this.classes.push(baseClass);
  }
  static create(baseClass) {
    return new StyleBuilder(baseClass);
  }
  add(className, condition = true) {
    if (condition && className) {
      const parts = className.split(" ").filter((c) => c.trim().length > 0);
      this.classes.push(...parts);
    }
    return this;
  }
  // Alias for legacy/generated code compatibility
  push(className, condition = true) {
    return this.add(className, condition);
  }
  addVariant(key, lookup) {
    const cls = lookup(key);
    if (cls) this.add(cls);
    return this;
  }
  build() {
    return this.classes.join(" ");
  }
  toString() {
    return this.build();
  }
}
class ClassBuilder {
  constructor() {
    __publicField(this, "classes", []);
    __publicField(this, "added", /* @__PURE__ */ new Set());
  }
  /**
   * Creates a new empty ClassBuilder instance.
   */
  static create(...initialClasses) {
    const builder = new ClassBuilder();
    if (initialClasses.length > 0) {
      builder.add(...initialClasses);
    }
    return builder;
  }
  /**
   * Adds one or more CSS classes.
   * Duplicate classes are automatically ignored.
   */
  add(...classes) {
    for (const className of classes) {
      if (className && className.trim() && !this.added.has(className)) {
        this.added.add(className);
        this.classes.push(className);
      }
    }
    return this;
  }
  when(condition, ...args) {
    if (condition) {
      if (typeof args[0] === "string" && args.length <= 2) {
        this.add(args[0]);
      } else {
        this.add(...args);
      }
    } else if (args.length === 2 && typeof args[1] === "string") {
      this.add(args[1]);
    }
    return this;
  }
  /**
   * Adds classes with a custom prefix.
   *
   * @example
   * .withPrefix("hover:", "bg-gray-100", "scale-110")
   * // => "hover:bg-gray-100 hover:scale-110"
   */
  withPrefix(prefix, ...classes) {
    for (const className of classes) {
      if (className && className.trim()) {
        this.add(`${prefix}${className}`);
      }
    }
    return this;
  }
  /**
   * Adds classes with the 'hover:' prefix.
   */
  hover(...classes) {
    return this.withPrefix("hover:", ...classes);
  }
  /**
   * Adds classes with the 'focus:' prefix.
   */
  focus(...classes) {
    return this.withPrefix("focus:", ...classes);
  }
  /**
   * Adds classes with the 'active:' prefix.
   */
  active(...classes) {
    return this.withPrefix("active:", ...classes);
  }
  /**
   * Adds classes with the 'dark:' prefix for dark mode.
   */
  dark(...classes) {
    return this.withPrefix("dark:", ...classes);
  }
  /**
   * Adds classes with the 'group-hover:' prefix.
   */
  groupHover(...classes) {
    return this.withPrefix("group-hover:", ...classes);
  }
  /**
   * Adds classes with the 'focus-within:' prefix.
   */
  focusWithin(...classes) {
    return this.withPrefix("focus-within:", ...classes);
  }
  /**
   * Adds responsive classes for the specified breakpoint.
   *
   * @param breakpoint Breakpoint prefix (sm, md, lg, xl, 2xl)
   * @param classes Classes to apply at this breakpoint
   */
  responsive(breakpoint, ...classes) {
    return this.withPrefix(`${breakpoint}:`, ...classes);
  }
  /**
   * Adds classes for the 'sm' breakpoint (640px+).
   */
  sm(...classes) {
    return this.responsive("sm", ...classes);
  }
  /**
   * Adds classes for the 'md' breakpoint (768px+).
   */
  md(...classes) {
    return this.responsive("md", ...classes);
  }
  /**
   * Adds classes for the 'lg' breakpoint (1024px+).
   */
  lg(...classes) {
    return this.responsive("lg", ...classes);
  }
  /**
   * Adds classes for the 'xl' breakpoint (1280px+).
   */
  xl(...classes) {
    return this.responsive("xl", ...classes);
  }
  /**
   * Adds classes for the '2xl' breakpoint (1536px+).
   */
  xl2(...classes) {
    return this.responsive("2xl", ...classes);
  }
  /**
   * Builds the final class string by joining all classes with spaces.
   */
  build() {
    return this.classes.join(" ");
  }
  /**
   * Returns the built class string.
   */
  toString() {
    return this.build();
  }
}
function joinClasses(...classes) {
  return classes.filter((c) => c && c.trim()).join(" ");
}
function whenClass(condition, className, fallback) {
  return condition ? className : fallback;
}
function mount(component, selector) {
  const container = document.querySelector(selector);
  if (!container) {
    throw new Error(`Container not found: ${selector}`);
  }
  component.mount(container);
}
function createApp(ComponentClass, selector) {
  const component = new ComponentClass();
  mount(component, selector);
  return component;
}
async function boot() {
  if (window.__EQ_DEV__) {
    await Promise.resolve().then(() => errorOverlay$1);
  }
  const { logger: logger2 } = await Promise.resolve().then(() => logger$1);
  logger2.debug("Starting boot process...");
  const config = window.__EQ_CONFIG;
  if (!config || !config.page) {
    logger2.warn("No page configured in __EQ_CONFIG");
    return;
  }
  const container = document.getElementById("app");
  if (!container) {
    logger2.error("Container #app not found");
    return;
  }
  if (typeof window.__registerTheme === "function") {
    logger2.debug("Registering theme...");
    window.__registerTheme();
  }
  try {
    const modulePath = `/_equantic/${config.page}.js?v=${config.version}`;
    const pageModule = await import(
      /* @vite-ignore */
      modulePath
    );
    const PageClass = pageModule.default || pageModule[config.page];
    if (!PageClass) {
      console.error(`[eQuantic.UI] Page class '${config.page}' not found in module`);
      return;
    }
    const component = new PageClass();
    if (config.ssr) {
      console.debug(`[eQuantic.UI] Hydrating SSR page: ${config.page}`);
    } else {
      console.debug(`[eQuantic.UI] Client-side rendering page: ${config.page}`);
    }
    component.mount(container);
  } catch (error) {
    console.error(`[eQuantic.UI] Failed to boot page '${config.page}':`, error);
    if (container.dataset.ssr !== "true") {
      container.innerHTML = `<div style="color: red; padding: 20px;">
        <h2>Failed to load page</h2>
        <pre>${error instanceof Error ? error.message : String(error)}</pre>
      </div>`;
    }
  }
}
const isDev$1 = typeof window !== "undefined" && window.__EQ_DEV__;
class ErrorOverlay {
  constructor() {
    __publicField(this, "overlay", null);
    __publicField(this, "errors", []);
  }
  show(error) {
    if (!isDev$1) return;
    this.errors.push(error);
    this.render();
  }
  clear() {
    this.errors = [];
    if (this.overlay) {
      this.overlay.remove();
      this.overlay = null;
    }
  }
  render() {
    if (!this.overlay) {
      this.overlay = document.createElement("div");
      this.overlay.id = "equantic-error-overlay";
      document.body.appendChild(this.overlay);
    }
    const error = this.errors[this.errors.length - 1];
    this.overlay.innerHTML = `
      <style>
        #equantic-error-overlay {
          position: fixed;
          top: 0;
          left: 0;
          right: 0;
          bottom: 0;
          background: rgba(0, 0, 0, 0.9);
          color: #fff;
          z-index: 999999;
          display: flex;
          flex-direction: column;
          font-family: 'Menlo', 'Monaco', 'Courier New', monospace;
          font-size: 14px;
        }

        #equantic-error-overlay .header {
          background: #e53e3e;
          padding: 20px 30px;
          font-size: 18px;
          font-weight: bold;
          display: flex;
          justify-content: space-between;
          align-items: center;
        }

        #equantic-error-overlay .close {
          background: rgba(255, 255, 255, 0.2);
          border: none;
          color: white;
          padding: 8px 16px;
          border-radius: 4px;
          cursor: pointer;
          font-size: 14px;
        }

        #equantic-error-overlay .close:hover {
          background: rgba(255, 255, 255, 0.3);
        }

        #equantic-error-overlay .content {
          flex: 1;
          overflow: auto;
          padding: 30px;
        }

        #equantic-error-overlay .message {
          font-size: 16px;
          margin-bottom: 20px;
          line-height: 1.6;
        }

        #equantic-error-overlay .stack {
          background: rgba(255, 255, 255, 0.05);
          border-left: 3px solid #e53e3e;
          padding: 15px;
          overflow-x: auto;
          white-space: pre-wrap;
          word-break: break-word;
          font-size: 13px;
          line-height: 1.5;
        }

        #equantic-error-overlay .footer {
          background: rgba(255, 255, 255, 0.05);
          padding: 15px 30px;
          font-size: 12px;
          color: rgba(255, 255, 255, 0.6);
        }
      </style>

      <div class="header">
        <span>⚠️ Build Error</span>
        <button class="close" onclick="document.getElementById('equantic-error-overlay').remove()">
          Close (Esc)
        </button>
      </div>

      <div class="content">
        <div class="message">${this.escapeHtml(error.message)}</div>
        ${error.stack ? `<div class="stack">${this.escapeHtml(error.stack)}</div>` : ""}
      </div>

      <div class="footer">
        This error overlay only appears in development. Fix the error to continue.
      </div>
    `;
    const closeHandler = (e) => {
      if (e.key === "Escape") {
        this.clear();
        document.removeEventListener("keydown", closeHandler);
      }
    };
    document.addEventListener("keydown", closeHandler);
  }
  escapeHtml(text) {
    const div = document.createElement("div");
    div.textContent = text;
    return div.innerHTML;
  }
}
const errorOverlay = new ErrorOverlay();
if (isDev$1 && typeof window !== "undefined") {
  window.addEventListener("error", (event) => {
    var _a;
    errorOverlay.show({
      message: event.message,
      stack: (_a = event.error) == null ? void 0 : _a.stack
    });
  });
  window.addEventListener("unhandledrejection", (event) => {
    var _a;
    errorOverlay.show({
      message: `Unhandled Promise Rejection: ${event.reason}`,
      stack: (_a = event.reason) == null ? void 0 : _a.stack
    });
  });
}
const errorOverlay$1 = /* @__PURE__ */ Object.freeze(/* @__PURE__ */ Object.defineProperty({
  __proto__: null,
  errorOverlay
}, Symbol.toStringTag, { value: "Module" }));
const isDev = typeof window !== "undefined" && window.__EQ_DEV__;
const logger = {
  debug(...args) {
    if (isDev) console.debug("[eQuantic.UI]", ...args);
  },
  info(...args) {
    if (isDev) console.info("[eQuantic.UI]", ...args);
  },
  warn(...args) {
    console.warn("[eQuantic.UI]", ...args);
  },
  error(...args) {
    console.error("[eQuantic.UI]", ...args);
  }
};
const logger$1 = /* @__PURE__ */ Object.freeze(/* @__PURE__ */ Object.defineProperty({
  __proto__: null,
  logger
}, Symbol.toStringTag, { value: "Module" }));
export {
  ClassBuilder,
  Component,
  ComponentState,
  HtmlElement,
  HtmlStyle,
  Reconciler,
  RenderManager,
  ServerActionsClient,
  ServiceCollectionBuilder,
  ServiceLifetime,
  ServiceProvider,
  StatefulComponent,
  StatelessComponent,
  StyleBuilder,
  boot,
  configureServerActions,
  configureServices,
  createApp,
  format,
  getReconciler,
  getRootServiceProvider,
  getServerActionsClient,
  joinClasses,
  mount,
  parseEnum,
  resetReconciler,
  resetServerActionsClient,
  resetServiceProvider,
  whenClass
};
//# sourceMappingURL=index.js.map
