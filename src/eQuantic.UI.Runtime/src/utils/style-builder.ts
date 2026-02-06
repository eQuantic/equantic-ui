export class StyleBuilder {
  private classes: string[] = [];
  private added: Set<string> = new Set();

  constructor(baseClass?: string | null) {
    if (baseClass) this.add(baseClass);
  }

  static create(baseClass?: string | null): StyleBuilder {
    return new StyleBuilder(baseClass);
  }

  add(...styles: (string | null | undefined)[]): this {
    for (const style of styles) {
      if (style && style.trim()) {
        if (!this.added.has(style)) {
          this.added.add(style);
          this.classes.push(style);
        }
      }
    }
    return this;
  }

  // Alias for legacy/generated code compatibility
  push(className?: string | null, condition: boolean = true): StyleBuilder {
    if (condition && className) {
      return this.add(className);
    }
    return this;
  }

  addVariant<T>(key: T, lookup: (k: T) => string | null | undefined): StyleBuilder {
    const cls = lookup(key);
    if (cls) this.add(cls);
    return this;
  }

  build(): string {
    return this.classes.join(' ');
  }

  toString(): string {
    return this.build();
  }
}
