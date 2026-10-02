/*
 * Copyright (c) 2026 diet-dost and/or its contributors.
 * Licensed under the "GNU Affero General Public License v3.0 only" and
 * the "Server Side Public License, v 1"; you may not use this file except
 * in compliance with, at your election, the "GNU Affero General Public
 * License v3.0 only" or the "Server Side Public License, v 1".
 */

/**
 * ChartRenderer
 * Single Responsibility: Pure SVG / CSS bar chart rendering, tooltip calculation,
 * coordinate mapping, and interactive bar selection for analytics timelines (1D, 7D, 30D, 90D, 365D).
 */
export class ChartRenderer {
  /**
   * @param {Object} options
   * @param {HTMLElement} options.container - The DOM element where bars are rendered (#bar-chart-container)
   * @param {Function} [options.onBarClick] - Callback invoked when a bar is clicked: (label, period) => void
   */
  constructor({ container, onBarClick = null }) {
    this._container = container;
    this._onBarClick = onBarClick;
    this._selectedLabel = null;
  }

  /**
   * Render daily trend bars inside the container.
   * @param {Array<Object>} dailyTrends - Array of { date, consumedCalories, budgetCalories }
   * @param {string} [period='7D'] - Active timeline period
   */
  render(dailyTrends = [], period = '7D') {
    if (!this._container) return;

    if (!dailyTrends || dailyTrends.length === 0) {
      this._container.innerHTML = '<div class="chart-empty-placeholder">No trend data available for this period.</div>';
      return;
    }

    const maxKcal = Math.max(
      ...dailyTrends.map(d => Math.max(d.consumedCalories || 0, d.budgetCalories || 0)),
      2000
    );

    this._container.innerHTML = dailyTrends.map((d, idx) => {
      const heightPct = Math.max(4, Math.round(((d.consumedCalories || 0) / maxKcal) * 100));
      const color = (d.consumedCalories || 0) > (d.budgetCalories || 0)
        ? 'var(--status-rose)'
        : (d.consumedCalories || 0) === 0
        ? 'rgba(255,255,255,0.06)'
        : 'var(--accent-brand)';
      const dateLabel = d.date || d.Date || '';
      const isSelected = this._selectedLabel === dateLabel;

      return `
        <div class="chart-bar-group" data-index="${idx}" data-label="${dateLabel}">
          <div class="chart-bar${isSelected ? ' selected' : ''}" style="height: ${heightPct}%; background: ${color};" 
               data-tooltip="${dateLabel}: ${Math.round(d.consumedCalories || 0)} / ${Math.round(d.budgetCalories || 0)} kcal"
               data-label="${dateLabel}"></div>
          <div class="chart-label">${dateLabel}</div>
        </div>
      `;
    }).join('');

    // Wire interactive clicks
    this._container.querySelectorAll('.chart-bar-group').forEach(group => {
      group.addEventListener('click', () => {
        const label = group.dataset.label;
        const bar = group.querySelector('.chart-bar');
        this._handleBarClick(label, bar, period);
      });
    });
  }

  /**
   * Internal click router that toggles selection and invokes callback.
   * @param {string} label
   * @param {HTMLElement} barEl
   * @param {string} period
   */
  _handleBarClick(label, barEl, period) {
    const isAlreadySelected = barEl?.classList.contains('selected');

    // Deselect all bars in container
    this.clearSelection();

    if (isAlreadySelected) {
      this._selectedLabel = null;
      if (typeof this._onBarClick === 'function') {
        this._onBarClick(null, period);
      }
    } else {
      if (barEl) barEl.classList.add('selected');
      this._selectedLabel = label;
      if (typeof this._onBarClick === 'function') {
        this._onBarClick(label, period);
      }
    }
  }

  /**
   * Clear active bar selection visual state.
   */
  clearSelection() {
    this._selectedLabel = null;
    if (this._container) {
      this._container.querySelectorAll('.chart-bar.selected').forEach(b => b.classList.remove('selected'));
    }
  }

  /**
   * Parse a chart label (e.g. "Sep 15") into an approximate ISO date string for filtering.
   * @param {string} label
   * @returns {string|null}
   */
  static parseLabelToDate(label) {
    if (!label) return null;
    const currentYear = new Date().getFullYear();
    const candidate = new Date(`${label}, ${currentYear}`);
    if (!isNaN(candidate.getTime())) {
      return candidate.toLocaleDateString('en-CA'); // 'YYYY-MM-DD'
    }
    return null;
  }
}
