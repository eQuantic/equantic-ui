export class StyleBuilder {
  private classes: string[] = [];

  constructor(baseClass?: string | null) {
    if (baseClass) this.classes.push(baseClass);
  }

  static create(baseClass?: string | null): StyleBuilder {
    return new StyleBuilder(baseClass);
  }

  add(className?: string | null, condition: boolean = true): StyleBuilder {
    if (condition && className) {
      // Split by space to handle multiple classes in one string
      const parts = className.split(' ').filter((c) => c.trim().length > 0);
      this.classes.push(...parts);
    }
    return this;
  }

  // Alias for legacy/generated code compatibility
  push(className?: string | null, condition: boolean = true): StyleBuilder {
    return this.add(className, condition);
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
