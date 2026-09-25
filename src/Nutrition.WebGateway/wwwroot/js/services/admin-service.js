/**
 * Admin and SuperAdmin Management Service
 * Provides client methods to interact with AdminController endpoints.
 */
export class AdminService {
  constructor(apiClient) {
    this.api = apiClient;
  }

  async getUsers(search = '', tier = null, role = null) {
    let url = '/api/admin/users?';
    const params = new URLSearchParams();
    if (search) params.append('search', search);
    if (tier !== null && tier !== undefined) params.append('tier', tier);
    if (role !== null && role !== undefined) params.append('role', role);
    return await this.api.get(url + params.toString());
  }

  async updateUserTier(userId, tier) {
    return await this.api.put(`/api/admin/users/${userId}/tier`, { tier: Number(tier) });
  }

  async updateUserRole(userId, role) {
    return await this.api.put(`/api/admin/users/${userId}/role`, { role: Number(role) });
  }

  async updateUserStatus(userId, isActive) {
    return await this.api.put(`/api/admin/users/${userId}/status`, { isActive });
  }

  async getTierConfigs() {
    return await this.api.get('/api/admin/tier-configs');
  }

  async updateTierConfig(tier, data) {
    return await this.api.put(`/api/admin/tier-configs/${tier}`, data);
  }

  async getAiLogs(userId = null, limit = 50) {
    let url = `/api/admin/ai-logs?limit=${limit}`;
    if (userId) url += `&userId=${encodeURIComponent(userId)}`;
    return await this.api.get(url);
  }
}
