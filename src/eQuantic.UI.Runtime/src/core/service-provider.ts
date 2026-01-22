/**
 * eQuantic.UI Runtime - Service Provider & Dependency Injection
 *
 * Lightweight DI container for managing services across the component tree
 */

/**
 * Service lifecycle types
 */
export enum ServiceLifetime {
  /** New instance created every time */
  Transient = 'transient',

  /** Single instance shared across all requests */
  Singleton = 'singleton',

  /** Scoped instance per component subtree */
  Scoped = 'scoped',
}

/**
 * Service descriptor
 */
interface ServiceDescriptor<T = unknown> {
  factory: () => T;
  lifetime: ServiceLifetime;
  instance?: T;
}

/**
 * Service identifier (constructor function or string key)
 */
export type ServiceKey<T = unknown> = (new (...args: unknown[]) => T) | string;

/**
 * Service Provider - Manages service registration and resolution
 */
export class ServiceProvider {
  private services = new Map<ServiceKey, ServiceDescriptor>();
  private scopedInstances = new Map<ServiceKey, unknown>();
  private parent: ServiceProvider | null = null;

  constructor(parent?: ServiceProvider) {
    this.parent = parent || null;
  }

  /**
   * Register a singleton service
   */
  registerSingleton<T>(key: ServiceKey<T>, factory: () => T): this {
    this.services.set(key, {
      factory,
      lifetime: ServiceLifetime.Singleton,
    });
    return this;
  }

  /**
   * Register a transient service (new instance each time)
   */
  registerTransient<T>(key: ServiceKey<T>, factory: () => T): this {
    this.services.set(key, {
      factory,
      lifetime: ServiceLifetime.Transient,
    });
    return this;
  }

  /**
   * Register a scoped service (per component subtree)
   */
  registerScoped<T>(key: ServiceKey<T>, factory: () => T): this {
    this.services.set(key, {
      factory,
      lifetime: ServiceLifetime.Scoped,
    });
    return this;
  }

  /**
   * Register an existing instance as singleton
   */
  registerInstance<T>(key: ServiceKey<T>, instance: T): this {
    this.services.set(key, {
      factory: () => instance,
      lifetime: ServiceLifetime.Singleton,
      instance,
    });
    return this;
  }

  /**
   * Get a service instance
   */
  getService<T>(key: ServiceKey<T>): T | undefined {
    // Try to find in current provider
    const descriptor = this.services.get(key) as ServiceDescriptor<T> | undefined;

    if (descriptor) {
      return this.resolveService(key, descriptor);
    }

    // Try parent provider
    if (this.parent) {
      return this.parent.getService<T>(key);
    }

    return undefined;
  }

  /**
   * Get a required service (throws if not found)
   */
  getRequiredService<T>(key: ServiceKey<T>): T {
    const service = this.getService(key);
    if (service === undefined) {
      const keyName = typeof key === 'string' ? key : key.name || 'Unknown';
      throw new Error(`Required service not found: ${keyName}`);
    }
    return service;
  }

  /**
   * Check if a service is registered
   */
  hasService(key: ServiceKey): boolean {
    return this.services.has(key) || (this.parent?.hasService(key) ?? false);
  }

  /**
   * Create a child/scoped provider
   */
  createScope(): ServiceProvider {
    return new ServiceProvider(this);
  }

  /**
   * Dispose scoped instances
   */
  dispose(): void {
    // Dispose scoped instances that implement IDisposable
    for (const instance of this.scopedInstances.values()) {
      if (instance && typeof (instance as { dispose?: () => void }).dispose === 'function') {
        (instance as { dispose: () => void }).dispose();
      }
    }
    this.scopedInstances.clear();
  }

  /**
   * Resolve a service based on its descriptor
   */
  private resolveService<T>(key: ServiceKey<T>, descriptor: ServiceDescriptor<T>): T {
    switch (descriptor.lifetime) {
      case ServiceLifetime.Singleton:
        if (!descriptor.instance) {
          descriptor.instance = descriptor.factory();
        }
        return descriptor.instance;

      case ServiceLifetime.Scoped:
        if (!this.scopedInstances.has(key)) {
          this.scopedInstances.set(key, descriptor.factory());
        }
        return this.scopedInstances.get(key) as T;

      case ServiceLifetime.Transient:
      default:
        return descriptor.factory();
    }
  }

  /**
   * Get all registered service keys (for debugging)
   */
  getRegisteredServices(): ServiceKey[] {
    const keys: ServiceKey[] = Array.from(this.services.keys());
    if (this.parent) {
      keys.push(...this.parent.getRegisteredServices());
    }
    return Array.from(new Set(keys));
  }
}

/**
 * Global root service provider
 */
let rootServiceProvider: ServiceProvider | null = null;

/**
 * Get the root service provider
 */
export function getRootServiceProvider(): ServiceProvider {
  if (!rootServiceProvider) {
    rootServiceProvider = new ServiceProvider();
  }
  return rootServiceProvider;
}

/**
 * Configure services for the application
 */
export function configureServices(configure: (services: ServiceProvider) => void): void {
  const services = getRootServiceProvider();
  configure(services);
}

/**
 * Reset the root service provider (useful for testing)
 */
export function resetServiceProvider(): void {
  if (rootServiceProvider) {
    rootServiceProvider.dispose();
  }
  rootServiceProvider = null;
}

/**
 * Service collection builder (fluent API)
 */
export class ServiceCollectionBuilder {
  private provider = new ServiceProvider();

  singleton<T>(key: ServiceKey<T>, factory: () => T): this {
    this.provider.registerSingleton(key, factory);
    return this;
  }

  transient<T>(key: ServiceKey<T>, factory: () => T): this {
    this.provider.registerTransient(key, factory);
    return this;
  }

  scoped<T>(key: ServiceKey<T>, factory: () => T): this {
    this.provider.registerScoped(key, factory);
    return this;
  }

  instance<T>(key: ServiceKey<T>, instance: T): this {
    this.provider.registerInstance(key, instance);
    return this;
  }

  build(): ServiceProvider {
    return this.provider;
  }
}
