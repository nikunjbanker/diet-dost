/**
 * Application State Store
 * Manages global application state cleanly with notification listeners.
 */
export class AppState {
  constructor(initialState = {}) {
    this._state = {
      userId: 'user-default',
      userTimezone: 'Asia/Kolkata',
      currentMeal: null,
      activePeriod: '7D',
      addedGhee: 0,
      addedTadka: 0,
      ...initialState
    };
    this._subscribers = new Set();
  }

  get userId() { return this._state.userId; }
  set userId(val) { this._update('userId', val); }

  get userTimezone() { return this._state.userTimezone || 'Asia/Kolkata'; }
  set userTimezone(val) { this._update('userTimezone', val); }

  get currentMeal() { return this._state.currentMeal; }
  set currentMeal(val) { this._update('currentMeal', val); }

  get activePeriod() { return this._state.activePeriod; }
  set activePeriod(val) { this._update('activePeriod', val); }

  get addedGhee() { return this._state.addedGhee; }
  set addedGhee(val) { this._update('addedGhee', val); }

  get addedTadka() { return this._state.addedTadka; }
  set addedTadka(val) { this._update('addedTadka', val); }

  /**
   * Snapshot of full state.
   */
  getSnapshot() {
    return { ...this._state };
  }

  /**
   * Subscribe to state mutations.
   * @param {Function} listener (key, value, state) => void
   * @returns {Function} Unsubscribe function
   */
  subscribe(listener) {
    this._subscribers.add(listener);
    return () => this._subscribers.delete(listener);
  }

  _update(key, value) {
    this._state[key] = value;
    for (const sub of this._subscribers) {
      try {
        sub(key, value, this.getSnapshot());
      } catch (err) {
        console.error('[AppState] Error in subscriber:', err);
      }
    }
  }
}

export const appState = new AppState();
