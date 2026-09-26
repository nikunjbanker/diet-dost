/*
 * Copyright (c) 2026 diet-dost and/or its contributors.
 * Licensed under the "GNU Affero General Public License v3.0 only" and
 * the "Server Side Public License, v 1"; you may not use this file except
 * in compliance with, at your election, the "GNU Affero General Public
 * License v3.0 only" or the "Server Side Public License, v 1".
 */
/**
 * Dependency Injection (DI) Container
 * Adheres to Dependency Inversion Principle (DIP).
 * Allows services and UI controllers to be registered and injected without tight coupling.
 */
export class ServiceContainer {
  constructor() {
    this._services = new Map();
    this._singletons = new Map();
  }

  /**
   * Register a service factory or instance.
   * @param {string} name - Service name key
   * @param {Function|Object} definition - Factory function (container => instance) or direct instance
   * @param {boolean} [isSingleton=true] - Whether to cache and return a single instance
   */
  register(name, definition, isSingleton = true) {
    this._services.set(name, { definition, isSingleton });
    return this;
  }

  /**
   * Resolve a service by name.
   * @param {string} name - Service name key
   * @returns {*} Resolved service instance
   */
  resolve(name) {
    if (!this._services.has(name)) {
      throw new Error(`[ServiceContainer] Service not found: ${name}`);
    }

    const { definition, isSingleton } = this._services.get(name);

    if (isSingleton) {
      if (!this._singletons.has(name)) {
        const instance = typeof definition === 'function' ? definition(this) : definition;
        this._singletons.set(name, instance);
      }
      return this._singletons.get(name);
    }

    return typeof definition === 'function' ? definition(this) : definition;
  }

  /**
   * Helper to check if a service is registered.
   * @param {string} name
   * @returns {boolean}
   */
  has(name) {
    return this._services.has(name);
  }
}

// Global container instance
export const container = new ServiceContainer();
