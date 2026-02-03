/**
 * eQuantic.UI Tailwind Runtime Helpers
 *
 * Provides runtime support for TailwindClass helpers that cannot be evaluated at compile-time.
 */

/**
 * TailwindClass represents a single or multiple Tailwind CSS classes.
 */
export class TailwindClass {
  constructor(private readonly value: string) {}

  /**
   * Returns the string representation of the class(es).
   */
  toString(): string {
    return this.value;
  }

  /**
   * Combines this class with another using the + operator (space-separated).
   */
  add(other: TailwindClass | string): TailwindClass {
    const otherValue = other instanceof TailwindClass ? other.toString() : other;
    return new TailwindClass(`${this.value} ${otherValue}`.trim());
  }
}

/**
 * TW namespace - runtime helpers for Tailwind CSS classes.
 */
export const TW = {
  /**
   * Conditionally applies Tailwind classes based on a boolean condition.
   *
   * @param condition - The condition to evaluate
   * @param whenTrue - Classes to apply when condition is true
   * @param whenFalse - Classes to apply when condition is false (optional)
   * @returns The appropriate TailwindClass based on the condition
   *
   * @example
   * TW.When(isActive, "bg-blue-500", "bg-gray-500")
   * TW.When(isDisabled, "opacity-50")
   */
  When(
    condition: boolean,
    whenTrue: TailwindClass | string,
    whenFalse?: TailwindClass | string
  ): TailwindClass {
    if (condition) {
      const value = whenTrue instanceof TailwindClass ? whenTrue.toString() : whenTrue;
      return new TailwindClass(value);
    } else if (whenFalse !== undefined) {
      const value = whenFalse instanceof TailwindClass ? whenFalse.toString() : whenFalse;
      return new TailwindClass(value);
    }
    return new TailwindClass("");
  },

  /**
   * Applies dark mode variant classes.
   * Shorthand for prefixing classes with 'dark:'.
   *
   * @param classes - Classes to apply in dark mode
   * @returns TailwindClass with dark: prefix
   *
   * @example
   * TW.Dark("bg-gray-900 text-white")
   */
  Dark(classes: TailwindClass | string): TailwindClass {
    const value = classes instanceof TailwindClass ? classes.toString() : classes;
    const darkClasses = value
      .split(" ")
      .filter(c => c.trim())
      .map(c => `dark:${c}`)
      .join(" ");
    return new TailwindClass(darkClasses);
  },

  /**
   * Applies hover variant classes.
   * Shorthand for prefixing classes with 'hover:'.
   *
   * @param classes - Classes to apply on hover
   * @returns TailwindClass with hover: prefix
   *
   * @example
   * TW.Hover("bg-blue-600 scale-105")
   */
  Hover(classes: TailwindClass | string): TailwindClass {
    const value = classes instanceof TailwindClass ? classes.toString() : classes;
    const hoverClasses = value
      .split(" ")
      .filter(c => c.trim())
      .map(c => `hover:${c}`)
      .join(" ");
    return new TailwindClass(hoverClasses);
  },

  /**
   * Applies focus variant classes.
   * Shorthand for prefixing classes with 'focus:'.
   *
   * @param classes - Classes to apply on focus
   * @returns TailwindClass with focus: prefix
   *
   * @example
   * TW.Focus("ring-2 ring-blue-500")
   */
  Focus(classes: TailwindClass | string): TailwindClass {
    const value = classes instanceof TailwindClass ? classes.toString() : classes;
    const focusClasses = value
      .split(" ")
      .filter(c => c.trim())
      .map(c => `focus:${c}`)
      .join(" ");
    return new TailwindClass(focusClasses);
  },

  /**
   * Applies group-hover variant classes.
   * Shorthand for prefixing classes with 'group-hover:'.
   *
   * @param classes - Classes to apply on group hover
   * @returns TailwindClass with group-hover: prefix
   *
   * @example
   * TW.GroupHover("opacity-100 translate-x-0")
   */
  GroupHover(classes: TailwindClass | string): TailwindClass {
    const value = classes instanceof TailwindClass ? classes.toString() : classes;
    const groupHoverClasses = value
      .split(" ")
      .filter(c => c.trim())
      .map(c => `group-hover:${c}`)
      .join(" ");
    return new TailwindClass(groupHoverClasses);
  },

  /**
   * Adds opacity to a Tailwind class.
   *
   * @param className - Base class name
   * @param opacity - Opacity value (0-100)
   * @returns TailwindClass with opacity
   *
   * @example
   * TW.WithOpacity("bg-white", 80) // "bg-white/80"
   */
  WithOpacity(className: string, opacity: number): TailwindClass {
    return new TailwindClass(`${className}/${opacity}`);
  }
};

// Export as global for runtime access
if (typeof window !== "undefined") {
  (window as any).TW = TW;
  (window as any).TailwindClass = TailwindClass;
}
