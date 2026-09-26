/*
 * Copyright (c) 2026 diet-dost and/or its contributors.
 * Licensed under the "GNU Affero General Public License v3.0 only" and
 * the "Server Side Public License, v 1"; you may not use this file except
 * in compliance with, at your election, the "GNU Affero General Public
 * License v3.0 only" or the "Server Side Public License, v 1".
 */
/**
 * AI Quota & Telemetry Modal Controller
 * Displays the user's active tier, today's detections gauge, localized midnight reset countdown,
 * and recent AI operations history.
 */
export class QuotaModalController {
  constructor({ apiClient, toastService, eventBus }) {
    this.api = apiClient;
    this.toastService = toastService;
    this.eventBus = eventBus;

    this.initElements();
    this.bindEvents();
  }

  initElements() {
    this.modal = document.getElementById('quota-modal');
    this.btnClose = document.getElementById('btn-close-quota-modal');
    this.btnCloseBottom = document.getElementById('btn-quota-close');
    this.btnRefresh = document.getElementById('btn-quota-refresh');

    this.tierName = document.getElementById('quota-tier-name');
    this.tierPill = document.getElementById('quota-tier-pill');
    this.usedDisplay = document.getElementById('quota-used-display');
    this.progressBarFill = document.getElementById('quota-progress-bar-fill');
    this.remainingLabel = document.getElementById('quota-remaining-label');
    this.resetLabel = document.getElementById('quota-reset-label');
    this.count7d = document.getElementById('quota-7d-count');
    this.count30d = document.getElementById('quota-30d-count');
    this.recentOpsBody = document.getElementById('quota-recent-ops-body');
  }

  bindEvents() {
    this.btnClose?.addEventListener('click', () => this.close());
    this.btnCloseBottom?.addEventListener('click', () => this.close());
    this.btnRefresh?.addEventListener('click', () => this.refresh());
  }

  open() {
    if (this.modal) {
      this.modal.style.display = 'flex';
      this.refresh();
    }
  }

  close() {
    if (this.modal) {
      this.modal.style.display = 'none';
    }
  }

  async refresh() {
    try {
      const stats = await this.api.get('/api/meals/quota');
      this.render(stats);
      return stats;
    } catch (err) {
      console.warn('Could not fetch AI quota stats:', err);
    }
  }

  render(stats) {
    if (!stats) return;

    const tierNameStr = typeof stats.tier === 'number'
      ? (stats.tier === 3 ? 'SuperAdmin' : stats.tier === 2 ? 'Premium' : stats.tier === 1 ? 'Basic' : 'Free')
      : stats.tier;

    if (this.tierName) {
      this.tierName.textContent = tierNameStr === 'SuperAdmin'
        ? '👑 Super User'
        : tierNameStr === 'Premium'
          ? '⚡ Premium Tier'
          : tierNameStr === 'Basic'
            ? '⭐ Basic Tier'
            : '🆓 Free Tier';
    }

    const isUnlimited = stats.dailyLimit < 0;
    if (this.usedDisplay) {
      this.usedDisplay.textContent = isUnlimited
        ? `${stats.usedToday} / ∞`
        : `${stats.usedToday} / ${stats.dailyLimit}`;
    }

    if (this.progressBarFill) {
      const pct = isUnlimited
        ? Math.min(100, stats.usedToday * 5)
        : Math.min(100, Math.round((stats.usedToday / Math.max(1, stats.dailyLimit)) * 100));
      this.progressBarFill.style.width = `${pct}%`;
      this.progressBarFill.style.backgroundColor = pct >= 100 ? '#f87171' : pct >= 80 ? '#fbbf24' : 'var(--accent)';
    }

    if (this.remainingLabel) {
      this.remainingLabel.textContent = isUnlimited
        ? 'Unlimited detections'
        : `${stats.remainingCalls} detections remaining today`;
    }

    if (this.resetLabel && stats.resetsAtUtc) {
      const diffMs = new Date(stats.resetsAtUtc).getTime() - Date.now();
      if (diffMs > 0) {
        const hours = Math.floor(diffMs / (1000 * 60 * 60));
        const mins = Math.floor((diffMs % (1000 * 60 * 60)) / (1000 * 60));
        this.resetLabel.textContent = `Resets in ${hours}h ${mins}m (midnight)`;
      } else {
        this.resetLabel.textContent = 'Resets at midnight';
      }
    }

    if (this.count7d) this.count7d.textContent = stats.usedLast7Days;
    if (this.count30d) this.count30d.textContent = stats.usedLast30Days;

    if (this.recentOpsBody && stats.recentOperations) {
      if (stats.recentOperations.length === 0) {
        this.recentOpsBody.innerHTML = `<tr><td colspan="4" style="text-align: center; color: var(--text-muted); padding: 1rem;">No AI operations logged yet.</td></tr>`;
      } else {
        this.recentOpsBody.innerHTML = stats.recentOperations.slice(0, 10).map(op => {
          const opName = typeof op.operationType === 'number'
            ? (op.operationType === 0 ? 'Photo' : op.operationType === 1 ? 'Text' : 'Compare')
            : op.operationType;

          const timeStr = new Date(op.timestampUtc).toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' });

          return `
            <tr style="border-bottom: 1px solid var(--border-subtle);">
              <td style="padding: 4px 6px;">
                <span class="badge" style="font-size: 0.65rem; padding: 1px 4px;">${opName}</span>
              </td>
              <td style="padding: 4px 6px; font-size: 0.72rem;">${op.modelId || 'Gemini'}</td>
              <td style="padding: 4px 6px; font-family: monospace; font-size: 0.72rem;">${op.latencyMs}ms</td>
              <td style="padding: 4px 6px; font-size: 0.72rem; color: var(--text-muted);">${timeStr}</td>
            </tr>
          `;
        }).join('');
      }
    }
  }
}
