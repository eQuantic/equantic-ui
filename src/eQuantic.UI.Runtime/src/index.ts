/**
 * eQuantic.UI Runtime - Main Entry Point
 */

// Core
export { Component } from './core/types';
export type { IComponent, HtmlNode, RenderContext, StyleClass, EventHandler } from './core/types';
export { StatelessComponent, StatefulComponent, ComponentState } from './core/component';
export { ServiceProvider, ServiceCollectionBuilder, getRootServiceProvider, configureServices, resetServiceProvider } from './core/service-provider';
export type { ServiceKey } from './core/service-provider';
export { ServiceLifetime } from './core/service-provider';

// Server Actions
export { ServerActionsClient, getServerActionsClient, configureServerActions, resetServerActionsClient } from './core/server-actions';
export type { ServerActionResponse } from './core/server-actions';

// DOM
export { Reconciler, getReconciler, resetReconciler } from './dom/reconciler';
export { RenderManager } from './dom/renderer';

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
  Checkbox,
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
