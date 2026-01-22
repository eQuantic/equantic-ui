/**
 * eQuantic.UI Runtime - Main Entry Point
 */

// Core
export { Component, IComponent, HtmlNode, RenderContext, StyleClass, EventHandler } from './core/types';
export { StatelessComponent, StatefulComponent, ComponentState, renderToDom } from './core/component';

// Components
export {
  Container,
  Flex,
  Column,
  Row,
  Text,
  Heading,
  Button,
  TextInput,
  Link,
} from './components/index';

/**
 * Mount a component to a DOM element
 */
export function mount(component: import('./core/component').StatefulComponent, selector: string): void {
  const container = document.querySelector(selector);
  if (!container) {
    throw new Error(`Container not found: ${selector}`);
  }
  component.mount(container as HTMLElement);
}

/**
 * Create and mount a component
 */
export function createApp<T extends import('./core/component').StatefulComponent>(
  ComponentClass: new () => T,
  selector: string
): T {
  const component = new ComponentClass();
  mount(component, selector);
  return component;
}
