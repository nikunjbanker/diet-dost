/**
 * SuperAdmin & Admin Governance Console UI Controller
 * Manages user accounts, dynamic tier entitlements, and telemetry audits.
 */
export class AdminModalController {
  constructor({ adminService, toastService, eventBus }) {
    this.adminService = adminService;
    this.toastService = toastService;
    this.eventBus = eventBus;

    this.initElements();
    this.bindEvents();
  }

  initElements() {
    this.modal = document.getElementById('admin-modal');
    this.btnClose = document.getElementById('btn-close-admin-modal');

    this.tabUsers = document.getElementById('btn-admin-tab-users');
    this.tabTiers = document.getElementById('btn-admin-tab-tiers');
    this.tabLogs = document.getElementById('btn-admin-tab-logs');

    this.paneUsers = document.getElementById('admin-pane-users');
    this.paneTiers = document.getElementById('admin-pane-tiers');
    this.paneLogs = document.getElementById('admin-pane-logs');

    this.searchUsers = document.getElementById('admin-search-users');
    this.filterTier = document.getElementById('admin-filter-tier');
    this.btnRefreshUsers = document.getElementById('btn-admin-refresh-users');
    this.usersTableBody = document.getElementById('admin-users-table-body');

    this.tierCardsContainer = document.getElementById('admin-tier-cards-container');
    this.btnRefreshLogs = document.getElementById('btn-admin-refresh-logs');
    this.logsTableBody = document.getElementById('admin-logs-table-body');
  }

  bindEvents() {
    this.btnClose?.addEventListener('click', () => this.close());

    this.tabUsers?.addEventListener('click', () => this.switchTab('users'));
    this.tabTiers?.addEventListener('click', () => this.switchTab('tiers'));
    this.tabLogs?.addEventListener('click', () => this.switchTab('logs'));

    this.btnRefreshUsers?.addEventListener('click', () => this.loadUsers());
    this.searchUsers?.addEventListener('input', () => this.debounceLoadUsers());
    this.filterTier?.addEventListener('change', () => this.loadUsers());

    this.btnRefreshLogs?.addEventListener('click', () => this.loadLogs());
  }

  open() {
    if (this.modal) {
      this.modal.style.display = 'flex';
      this.switchTab('users');
    }
  }

  close() {
    if (this.modal) {
      this.modal.style.display = 'none';
    }
  }

  switchTab(tab) {
    this.tabUsers?.classList.toggle('active', tab === 'users');
    this.tabTiers?.classList.toggle('active', tab === 'tiers');
    this.tabLogs?.classList.toggle('active', tab === 'logs');

    if (this.paneUsers) this.paneUsers.style.display = tab === 'users' ? 'block' : 'none';
    if (this.paneTiers) this.paneTiers.style.display = tab === 'tiers' ? 'block' : 'none';
    if (this.paneLogs) this.paneLogs.style.display = tab === 'logs' ? 'block' : 'none';

    if (tab === 'users') this.loadUsers();
    if (tab === 'tiers') this.loadTiers();
    if (tab === 'logs') this.loadLogs();
  }

  debounceLoadUsers() {
    clearTimeout(this.searchTimer);
    this.searchTimer = setTimeout(() => this.loadUsers(), 300);
  }

  async loadUsers() {
    if (!this.usersTableBody) return;
    this.usersTableBody.innerHTML = `<tr><td colspan="7" style="text-align: center; color: var(--text-muted); padding: 2rem;">Loading users...</td></tr>`;

    try {
      const search = this.searchUsers?.value.trim() || '';
      const tier = this.filterTier?.value || null;
      const users = await this.adminService.getUsers(search, tier);

      if (!users || users.length === 0) {
        this.usersTableBody.innerHTML = `<tr><td colspan="7" style="text-align: center; color: var(--text-muted); padding: 2rem;">No matching users found.</td></tr>`;
        return;
      }

      this.usersTableBody.innerHTML = users.map(u => {
        const isSuper = u.role === 2 || u.role === 'SuperAdmin';
        const roleStr = typeof u.role === 'number' ? (u.role === 2 ? 'SuperAdmin' : u.role === 1 ? 'Admin' : 'User') : u.role;
        const tierStr = typeof u.tier === 'number' ? (u.tier === 3 ? 'SuperAdmin' : u.tier === 2 ? 'Premium' : u.tier === 1 ? 'Basic' : 'Free') : u.tier;
        const statusBadge = u.isActive
          ? `<span class="badge badge-success" style="font-size: 0.7rem; padding: 2px 6px;">Active</span>`
          : `<span class="badge badge-danger" style="font-size: 0.7rem; padding: 2px 6px; background: rgba(239, 68, 68, 0.2); color: #f87171;">Locked</span>`;

        const emailBadge = u.isEmailVerified
          ? `<span title="Email Verified" style="color: var(--accent); margin-right: 4px;">✓ Email</span>`
          : `<span title="Email Unverified" style="color: var(--text-muted); margin-right: 4px;">✗ Email</span>`;

        const dpdpaDate = u.termsAcceptedAtUtc
          ? new Date(u.termsAcceptedAtUtc).toLocaleDateString()
          : 'Pending';

        return `
          <tr data-user-id="${u.id}" style="border-bottom: 1px solid var(--border-subtle);">
            <td style="padding: 8px 10px;">
              <div style="font-weight: 600; font-size: 0.85rem;">${u.email}</div>
              <div style="font-size: 0.75rem; color: var(--text-muted); font-family: monospace;">${u.mobileNumber}</div>
            </td>
            <td style="padding: 8px 10px;">
              <select class="admin-user-role-select text-input" style="padding: 2px 6px; font-size: 0.75rem;" ${isSuper ? 'disabled' : ''}>
                <option value="0" ${roleStr === 'User' ? 'selected' : ''}>User</option>
                <option value="1" ${roleStr === 'Admin' ? 'selected' : ''}>Admin</option>
                <option value="2" ${roleStr === 'SuperAdmin' ? 'selected' : ''}>SuperAdmin</option>
              </select>
            </td>
            <td style="padding: 8px 10px;">
              <select class="admin-user-tier-select text-input" style="padding: 2px 6px; font-size: 0.75rem;" ${isSuper ? 'disabled' : ''}>
                <option value="0" ${tierStr === 'Free' ? 'selected' : ''}>Free</option>
                <option value="1" ${tierStr === 'Basic' ? 'selected' : ''}>Basic</option>
                <option value="2" ${tierStr === 'Premium' ? 'selected' : ''}>Premium</option>
                <option value="3" ${tierStr === 'SuperAdmin' ? 'selected' : ''}>SuperAdmin</option>
              </select>
            </td>
            <td style="padding: 8px 10px; font-family: 'JetBrains Mono', monospace; font-size: 0.85rem;">
              <span class="badge" style="background: var(--surface-subtle); padding: 2px 6px; border-radius: 4px;">
                ${u.todayAiDetectionsCount}
              </span>
            </td>
            <td style="padding: 8px 10px; font-size: 0.75rem;">
              <div>${emailBadge}</div>
              <div style="color: var(--text-muted); font-size: 0.7rem; margin-top: 2px;">DPDPA: ${dpdpaDate}</div>
            </td>
            <td style="padding: 8px 10px;">
              ${statusBadge}
            </td>
            <td style="padding: 8px 10px;">
              ${isSuper ? '<span style="font-size: 0.72rem; color: var(--text-muted);">Protected</span>' : `
                <button type="button" class="btn btn-xs ${u.isActive ? 'btn-outline' : 'btn-primary'} btn-toggle-status" data-active="${u.isActive}">
                  ${u.isActive ? 'Lock' : 'Unlock'}
                </button>
              `}
            </td>
          </tr>
        `;
      }).join('');

      this.bindUserActionHandlers();
    } catch (err) {
      this.usersTableBody.innerHTML = `<tr><td colspan="7" style="text-align: center; color: #f87171; padding: 2rem;">Error loading users: ${err.message}</td></tr>`;
    }
  }

  bindUserActionHandlers() {
    this.usersTableBody?.querySelectorAll('.admin-user-role-select').forEach(sel => {
      sel.addEventListener('change', async (e) => {
        const row = e.target.closest('tr');
        const userId = row?.dataset.userId;
        const newRole = e.target.value;
        if (!userId) return;

        try {
          await this.adminService.updateUserRole(userId, newRole);
          this.toastService?.success('User role updated successfully.');
        } catch (err) {
          this.toastService?.error(err.data?.error || 'Failed to update role.');
          this.loadUsers();
        }
      });
    });

    this.usersTableBody?.querySelectorAll('.admin-user-tier-select').forEach(sel => {
      sel.addEventListener('change', async (e) => {
        const row = e.target.closest('tr');
        const userId = row?.dataset.userId;
        const newTier = e.target.value;
        if (!userId) return;

        try {
          await this.adminService.updateUserTier(userId, newTier);
          this.toastService?.success('User tier updated successfully.');
        } catch (err) {
          this.toastService?.error(err.data?.error || 'Failed to update tier.');
          this.loadUsers();
        }
      });
    });

    this.usersTableBody?.querySelectorAll('.btn-toggle-status').forEach(btn => {
      btn.addEventListener('click', async (e) => {
        const row = e.target.closest('tr');
        const userId = row?.dataset.userId;
        const isCurrentlyActive = btn.dataset.active === 'true';
        if (!userId) return;

        try {
          await this.adminService.updateUserStatus(userId, !isCurrentlyActive);
          this.toastService?.success(`User ${!isCurrentlyActive ? 'unlocked' : 'locked'} successfully.`);
          this.loadUsers();
        } catch (err) {
          this.toastService?.error(err.data?.error || 'Failed to change user status.');
        }
      });
    });
  }

  async loadTiers() {
    if (!this.tierCardsContainer) return;
    this.tierCardsContainer.innerHTML = `<div style="grid-column: 1/-1; text-align: center; color: var(--text-muted); padding: 2rem;">Loading tier configurations...</div>`;

    try {
      const configs = await this.adminService.getTierConfigs();
      this.tierCardsContainer.innerHTML = configs.map(c => {
        const tierName = typeof c.tier === 'number'
          ? (c.tier === 3 ? 'SuperAdmin' : c.tier === 2 ? 'Premium' : c.tier === 1 ? 'Basic' : 'Free')
          : c.tier;

        const isSuper = tierName === 'SuperAdmin';

        return `
          <div class="tier-admin-card" data-tier="${c.tier}" style="background: var(--surface-subtle); border: 1px solid var(--border-subtle); border-radius: var(--radius-md); padding: 1rem;">
            <div style="display: flex; justify-content: space-between; align-items: center; margin-bottom: 0.75rem;">
              <h3 style="font-size: 1rem; font-weight: 700; margin: 0;">${tierName} Tier</h3>
              <span class="badge" style="font-size: 0.7rem; padding: 2px 6px;">Tier #${c.tier}</span>
            </div>

            <div class="form-group" style="margin-bottom: 0.5rem;">
              <label style="font-size: 0.75rem; color: var(--text-secondary);">Daily AI Detection Limit (-1 for Unlimited)</label>
              <input type="number" class="text-input inp-daily-limit" value="${c.dailyAiDetectionLimit}" style="width: 100%;" ${isSuper ? 'readonly' : ''}>
            </div>

            <div class="form-group" style="margin-bottom: 0.5rem;">
              <label style="font-size: 0.75rem; color: var(--text-secondary);">Analytics History Days</label>
              <input type="number" class="text-input inp-history-days" value="${c.analyticsHistoryDays}" style="width: 100%;">
            </div>

            <div style="margin-bottom: 0.5rem;">
              <label class="legal-checkbox-label" style="font-size: 0.8rem;">
                <input type="checkbox" class="cb-photo-compare" ${c.allowPhotoCompare ? 'checked' : ''} ${isSuper ? 'disabled' : ''}>
                <span>Allow Photo Compare</span>
              </label>
            </div>

            <div style="margin-bottom: 0.75rem;">
              <label class="legal-checkbox-label" style="font-size: 0.8rem;">
                <input type="checkbox" class="cb-data-export" ${c.allowDataExport ? 'checked' : ''} ${isSuper ? 'disabled' : ''}>
                <span>Allow Data Export (Excel / CSV)</span>
              </label>
            </div>

            <div class="form-group" style="margin-bottom: 0.75rem;">
              <label style="font-size: 0.75rem; color: var(--text-secondary);">Description</label>
              <input type="text" class="text-input inp-description" value="${c.description || ''}" style="width: 100%;">
            </div>

            <button type="button" class="btn btn-sm btn-primary btn-save-tier" style="width: 100%;">
              Save Configuration
            </button>
          </div>
        `;
      }).join('');

      this.bindTierActionHandlers();
    } catch (err) {
      this.tierCardsContainer.innerHTML = `<div style="grid-column: 1/-1; text-align: center; color: #f87171; padding: 2rem;">Failed to load tier configs: ${err.message}</div>`;
    }
  }

  bindTierActionHandlers() {
    this.tierCardsContainer?.querySelectorAll('.btn-save-tier').forEach(btn => {
      btn.addEventListener('click', async (e) => {
        const card = e.target.closest('.tier-admin-card');
        const tier = card?.dataset.tier;
        if (!card || tier === undefined) return;

        const limit = Number(card.querySelector('.inp-daily-limit')?.value);
        const historyDays = Number(card.querySelector('.inp-history-days')?.value);
        const allowPhotoCompare = card.querySelector('.cb-photo-compare')?.checked ?? false;
        const allowDataExport = card.querySelector('.cb-data-export')?.checked ?? false;
        const description = card.querySelector('.inp-description')?.value || '';

        try {
          btn.disabled = true;
          btn.textContent = 'Saving...';
          await this.adminService.updateTierConfig(tier, {
            dailyAiDetectionLimit: limit,
            allowPhotoCompare,
            allowDataExport,
            analyticsHistoryDays: historyDays,
            description
          });
          this.toastService?.success('Tier configuration updated and cache invalidated.');
        } catch (err) {
          this.toastService?.error(err.data?.error || 'Failed to update tier configuration.');
        } finally {
          btn.disabled = false;
          btn.textContent = 'Save Configuration';
        }
      });
    });
  }

  async loadLogs() {
    if (!this.logsTableBody) return;
    this.logsTableBody.innerHTML = `<tr><td colspan="7" style="text-align: center; color: var(--text-muted); padding: 2rem;">Loading telemetry...</td></tr>`;

    try {
      const logs = await this.adminService.getAiLogs(null, 50);
      if (!logs || logs.length === 0) {
        this.logsTableBody.innerHTML = `<tr><td colspan="7" style="text-align: center; color: var(--text-muted); padding: 2rem;">No telemetry events recorded yet.</td></tr>`;
        return;
      }

      this.logsTableBody.innerHTML = logs.map(l => {
        const opName = typeof l.operationType === 'number'
          ? (l.operationType === 0 ? 'Photo' : l.operationType === 1 ? 'Text' : 'Compare')
          : l.operationType;

        const timeStr = new Date(l.timestampUtc).toLocaleTimeString();
        const dateStr = new Date(l.timestampUtc).toLocaleDateString();

        return `
          <tr style="border-bottom: 1px solid var(--border-subtle); font-size: 0.75rem;">
            <td style="padding: 6px 8px; font-family: monospace;">${dateStr} ${timeStr}</td>
            <td style="padding: 6px 8px; font-family: monospace;">${l.userId.substring(0, 8)}...</td>
            <td style="padding: 6px 8px;">
              <span class="badge" style="font-size: 0.68rem; padding: 1px 5px;">${opName}</span>
            </td>
            <td style="padding: 6px 8px;">${l.modelId}</td>
            <td style="padding: 6px 8px; font-family: monospace;">${l.estimatedTokensUsed}</td>
            <td style="padding: 6px 8px; font-family: monospace;">${l.latencyMs}ms</td>
            <td style="padding: 6px 8px;">
              <span style="color: ${l.isSuccess ? 'var(--accent)' : '#f87171'};">
                ${l.isSuccess ? '✓ OK' : '✗ ' + (l.errorReason || 'Failed')}
              </span>
            </td>
          </tr>
        `;
      }).join('');
    } catch (err) {
      this.logsTableBody.innerHTML = `<tr><td colspan="7" style="text-align: center; color: #f87171; padding: 2rem;">Failed to load logs: ${err.message}</td></tr>`;
    }
  }
}
