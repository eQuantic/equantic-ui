/**
 * ClassBuilder - Fluent builder for combining CSS classes
 *
 * TypeScript implementation that mirrors eQuantic.UI.Core.Styling.ClassBuilder (C#).
 * Provides a clean, chainable API for building CSS class strings with support for:
 * - Conditional classes
 * - State variants (hover, focus, active, dark mode)
 * - Responsive breakpoints
 * - Automatic duplicate removal
 */

export class ClassBuilder {
  private classes: string[] = [];
  private added: Set<string> = new Set();

  /**
   * Creates a new empty ClassBuilder instance.
   */
  static create(...initialClasses: (string | null | undefined)[]): ClassBuilder {
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
  add(...classes: (string | null | undefined)[]): this {
    for (const className of classes) {
      if (className && className.trim() && !this.added.has(className)) {
        this.added.add(className);
        this.classes.push(className);
      }
    }
    return this;
  }

  /**
   * Conditionally adds classes if the condition is true.
   * Supports optional fallback classes when condition is false.
   *
   * @example
   * .when(isActive, "bg-blue-600", "bg-gray-600")
   * .when(isDisabled, "opacity-50")
   */
  when(condition: boolean, ...classes: (string | null | undefined)[]): this;
  when(condition: boolean, whenTrue: string, whenFalse?: string): this;
  when(condition: boolean, ...args: any[]): this {
    if (condition) {
      // If first arg is string and there are exactly 1-2 args, treat as whenTrue
      if (typeof args[0] === 'string' && args.length <= 2) {
        this.add(args[0]);
      } else {
        // Otherwise, treat as multiple classes
        this.add(...args);
      }
    } else if (args.length === 2 && typeof args[1] === 'string') {
      // Second argument is the fallback (whenFalse)
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
  withPrefix(prefix: string, ...classes: (string | null | undefined)[]): this {
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
  hover(...classes: (string | null | undefined)[]): this {
    return this.withPrefix('hover:', ...classes);
  }

  /**
   * Adds classes with the 'focus:' prefix.
   */
  focus(...classes: (string | null | undefined)[]): this {
    return this.withPrefix('focus:', ...classes);
  }

  /**
   * Adds classes with the 'active:' prefix.
   */
  active(...classes: (string | null | undefined)[]): this {
    return this.withPrefix('active:', ...classes);
  }

  /**
   * Adds classes with the 'dark:' prefix for dark mode.
   */
  dark(...classes: (string | null | undefined)[]): this {
    return this.withPrefix('dark:', ...classes);
  }

  /**
   * Adds classes with the 'group-hover:' prefix.
   */
  groupHover(...classes: (string | null | undefined)[]): this {
    return this.withPrefix('group-hover:', ...classes);
  }

  /**
   * Adds classes with the 'focus-within:' prefix.
   */
  focusWithin(...classes: (string | null | undefined)[]): this {
    return this.withPrefix('focus-within:', ...classes);
  }

  /**
   * Adds responsive classes for the specified breakpoint.
   *
   * @param breakpoint Breakpoint prefix (sm, md, lg, xl, 2xl)
   * @param classes Classes to apply at this breakpoint
   */
  responsive(breakpoint: string, ...classes: (string | null | undefined)[]): this {
    return this.withPrefix(`${breakpoint}:`, ...classes);
  }

  /**
   * Adds classes for the 'sm' breakpoint (640px+).
   */
  sm(...classes: (string | null | undefined)[]): this {
    return this.responsive('sm', ...classes);
  }

  /**
   * Adds classes for the 'md' breakpoint (768px+).
   */
  md(...classes: (string | null | undefined)[]): this {
    return this.responsive('md', ...classes);
  }

  /**
   * Adds classes for the 'lg' breakpoint (1024px+).
   */
  lg(...classes: (string | null | undefined)[]): this {
    return this.responsive('lg', ...classes);
  }

  /**
   * Adds classes for the 'xl' breakpoint (1280px+).
   */
  xl(...classes: (string | null | undefined)[]): this {
    return this.responsive('xl', ...classes);
  }

  /**
   * Adds classes for the '2xl' breakpoint (1536px+).
   */
  xl2(...classes: (string | null | undefined)[]): this {
    return this.responsive('2xl', ...classes);
  }

  /**
   * Builds the final class string by joining all classes with spaces.
   */
  build(): string {
    return this.classes.join(' ');
  }

  /**
   * Returns the built class string.
   */
  toString(): string {
    return this.build();
  }
}

/**
 * Joins multiple class strings with spaces, filtering out null or empty values.
 *
 * @example
 * joinClasses("flex", "items-center", null, "gap-4")
 * // => "flex items-center gap-4"
 */
export function joinClasses(...classes: (string | null | undefined)[]): string {
  return classes.filter(c => c && c.trim()).join(' ');
}

/**
 * Conditionally returns a class string.
 *
 * @example
 * whenClass(isActive, "text-blue-600", "text-gray-600")
 */
export function whenClass(
  condition: boolean,
  className?: string,
  fallback?: string
): string | undefined {
  return condition ? className : fallback;
}
