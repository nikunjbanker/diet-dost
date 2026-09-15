/**
 * Decoupled Event Bus (Pub/Sub)
 * Enables loose coupling between independent UI controllers.
 */
export class EventBus {
  constructor() {
    this._listeners = new Map();
  }

  /**
   * Subscribe to an event.
   * @param {string} eventName
   * @param {Function} callback
   * @returns {Function} Unsubscribe function
   */
  on(eventName, callback) {
    if (!this._listeners.has(eventName)) {
      this._listeners.set(eventName, new Set());
    }
    this._listeners.get(eventName).add(callback);

    return () => this.off(eventName, callback);
  }

  /**
   * Unsubscribe from an event.
   * @param {string} eventName
   * @param {Function} callback
   */
  off(eventName, callback) {
    if (this._listeners.has(eventName)) {
      this._listeners.get(eventName).delete(callback);
    }
  }

  /**
   * Emit an event to all subscribers.
   * @param {string} eventName
   * @param {*} [data]
   */
  emit(eventName, data) {
    if (this._listeners.has(eventName)) {
      for (const callback of this._listeners.get(eventName)) {
        try {
          callback(data);
        } catch (err) {
          console.error(`[EventBus] Error in listener for event "${eventName}":`, err);
        }
      }
    }
  }
}

export const eventBus = new EventBus();
