/**
 * eQuantic.UI Runtime - Base Components
 */

import { Component, HtmlNode, EventHandler } from '../core/types';

/**
 * Container component - renders as div
 */
export class Container extends Component {
  render(): HtmlNode {
    return {
      tag: 'div',
      attributes: this.buildAttributes(),
      events: {},
      children: this.children.map((c) => c.render()),
    };
  }
}

/**
 * Flex container
 */
export class Flex extends Component {
  direction = 'row';
  justify?: string;
  align?: string;
  gap?: string;
  wrap = false;

  render(): HtmlNode {
    const attrs = this.buildAttributes();
    const styles: string[] = [
      'display: flex',
      `flex-direction: ${this.direction}`,
    ];

    if (this.justify) styles.push(`justify-content: ${this.justify}`);
    if (this.align) styles.push(`align-items: ${this.align}`);
    if (this.gap) styles.push(`gap: ${this.gap}`);
    if (this.wrap) styles.push('flex-wrap: wrap');

    const existingStyle = attrs['style'] || '';
    attrs['style'] = existingStyle
      ? `${existingStyle}; ${styles.join('; ')}`
      : styles.join('; ');

    return {
      tag: 'div',
      attributes: attrs,
      events: {},
      children: this.children.map((c) => c.render()),
    };
  }
}

/**
 * Column (vertical flex)
 */
export class Column extends Flex {
  constructor() {
    super();
    this.direction = 'column';
  }
}

/**
 * Row (horizontal flex)
 */
export class Row extends Flex {
  constructor() {
    super();
    this.direction = 'row';
  }
}

/**
 * Text component
 */
export class Text extends Component {
  content = '';
  paragraph = false;

  constructor(content?: string) {
    super();
    if (content) this.content = content;
  }

  render(): HtmlNode {
    return {
      tag: this.paragraph ? 'p' : 'span',
      attributes: this.buildAttributes(),
      events: {},
      children: [{ tag: '#text', attributes: {}, events: {}, children: [], textContent: this.content }],
    };
  }
}

/**
 * Heading component
 */
export class Heading extends Component {
  content = '';
  level = 1;

  constructor(content?: string, level = 1) {
    super();
    if (content) this.content = content;
    this.level = Math.min(6, Math.max(1, level));
  }

  render(): HtmlNode {
    return {
      tag: `h${this.level}`,
      attributes: this.buildAttributes(),
      events: {},
      children: [{ tag: '#text', attributes: {}, events: {}, children: [], textContent: this.content }],
    };
  }
}

/**
 * Button component
 */
export class Button extends Component {
  type = 'button';
  disabled = false;
  text?: string;
  onClick?: EventHandler;

  render(): HtmlNode {
    const attrs = this.buildAttributes();
    attrs['type'] = this.type;
    if (this.disabled) attrs['disabled'] = 'true';

    const events: Record<string, EventHandler> = {};
    if (this.onClick) events['click'] = this.onClick;

    const children =
      this.children.length > 0
        ? this.children.map((c) => c.render())
        : this.text
          ? [{ tag: '#text', attributes: {}, events: {}, children: [], textContent: this.text }]
          : [];

    return {
      tag: 'button',
      attributes: attrs,
      events,
      children,
    };
  }
}

/**
 * Text input component
 */
export class TextInput extends Component {
  type = 'text';
  value = '';
  placeholder?: string;
  disabled = false;
  readOnly = false;
  name?: string;
  onChange?: (value: string) => void;
  onInput?: (value: string) => void;

  render(): HtmlNode {
    const attrs = this.buildAttributes();
    attrs['type'] = this.type;
    attrs['value'] = this.value;

    if (this.placeholder) attrs['placeholder'] = this.placeholder;
    if (this.disabled) attrs['disabled'] = 'true';
    if (this.readOnly) attrs['readonly'] = 'true';
    if (this.name) attrs['name'] = this.name;

    const events: Record<string, EventHandler> = {};
    if (this.onChange) events['change'] = this.onChange as EventHandler;
    if (this.onInput) events['input'] = this.onInput as EventHandler;

    return {
      tag: 'input',
      attributes: attrs,
      events,
      children: [],
    };
  }
}

/**
 * Link component
 */
export class Link extends Component {
  href = '#';
  target?: string;
  text?: string;

  render(): HtmlNode {
    const attrs = this.buildAttributes();
    attrs['href'] = this.href;
    if (this.target) attrs['target'] = this.target;
    if (this.target === '_blank') attrs['rel'] = 'noopener noreferrer';

    const children =
      this.children.length > 0
        ? this.children.map((c) => c.render())
        : this.text
          ? [{ tag: '#text', attributes: {}, events: {}, children: [], textContent: this.text }]
          : [];

    return {
      tag: 'a',
      attributes: attrs,
      events: {},
      children,
    };
  }
}
