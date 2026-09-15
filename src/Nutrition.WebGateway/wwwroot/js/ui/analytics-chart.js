/**
 * AnalyticsChartController
 * Manages periodic deficit projections and bar chart visualization (1D, 7D, 30D, 90D, 365D).
 */
export class AnalyticsChartController {
  /**
   * @param {Object} options
   * @param {import('../services/analytics-service.js').AnalyticsService} options.analyticsService
   * @param {import('../core/state.js').AppState} options.appState
   * @param {import('../core/event-bus.js').EventBus} options.eventBus
   */
  constructor({ analyticsService, appState, eventBus }) {
    this._analytics = analyticsService;
    this._state = appState;
    this._bus = eventBus;

    this._bindEvents();

    // Listen to global changes
    this._bus.on('meal:logged', () => this.refresh());
    this._bus.on('profile:updated', () => this.refresh());
    this._bus.on('analytics:switch-period', (period) => this.switchPeriod(period));
  }

  get elements() {
    return {
      barChartContainer: document.getElementById('bar-chart-container'),
      statTotalDeficit: document.getElementById('stat-total-deficit'),
      statProjectedWeight: document.getElementById('stat-projected-weight'),
      statProteinCompliance: document.getElementById('stat-protein-compliance'),
      periodTitle: document.getElementById('stat-period-title')
    };
  }

  _bindEvents() {
    document.querySelectorAll('.tab-btn').forEach(btn => {
      btn.addEventListener('click', () => {
        const period = btn.dataset.period;
        this.switchPeriod(period);
      });
    });
  }

  /**
   * Switch the active analytics timeline period.
   * @param {'1D'|'7D'|'30D'|'90D'|'365D'} period
   */
  async switchPeriod(period) {
    document.querySelectorAll('.tab-btn').forEach(b => {
      if (b.dataset.period === period) b.classList.add('active');
      else b.classList.remove('active');
    });

    this._state.activePeriod = period;
    await this.refresh();
  }

  /**
   * Fetch projection data and render bar chart.
   */
  async refresh() {
    try {
      const period = this._state.activePeriod || '7D';
      const el = this.elements;

      if (el.periodTitle) {
        const titles = {
          '1D': 'Daily',
          '7D': '7-Day',
          '30D': '30-Day',
          '90D': 'Quarterly (90D)',
          '365D': 'Yearly (1Y)'
        };
        el.periodTitle.textContent = titles[period] || period;
      }

      const data = await this._analytics.getProjections(this._state.userId, period);
      if (!data) return;

      if (el.statTotalDeficit) {
        el.statTotalDeficit.textContent = `${Math.round(data.totalDeficitKcal).toLocaleString()} kcal`;
      }
      if (el.statProjectedWeight) {
        el.statProjectedWeight.textContent = `${data.projectedWeightLossKg} kg`;
      }
      if (el.statProteinCompliance) {
        el.statProteinCompliance.textContent = `${Math.round(data.proteinCompliancePercent)}%`;
      }

      if (el.barChartContainer && data.dailyTrends && data.dailyTrends.length > 0) {
        const maxKcal = Math.max(
          ...data.dailyTrends.map(d => Math.max(d.consumedCalories, d.budgetCalories)),
          2000
        );

        el.barChartContainer.innerHTML = data.dailyTrends.map(d => {
          const heightPct = Math.max(4, Math.round((d.consumedCalories / maxKcal) * 100));
          const color = d.consumedCalories > d.budgetCalories
            ? 'var(--status-rose)'
            : d.consumedCalories === 0
            ? 'rgba(255,255,255,0.06)'
            : 'var(--accent-brand)';
          const dateLabel = d.date || d.Date || '';

          return `
            <div class="chart-bar-group">
              <div class="chart-bar" style="height: ${heightPct}%; background: ${color};" 
                   data-tooltip="${dateLabel}: ${Math.round(d.consumedCalories)} / ${Math.round(d.budgetCalories)} kcal"></div>
              <div class="chart-label">${dateLabel}</div>
            </div>
          `;
        }).join('');
      }
    } catch (err) {
      console.error('[AnalyticsChartController] Failed to load projections:', err);
    }
  }
}
