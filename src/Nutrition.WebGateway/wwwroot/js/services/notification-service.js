/*
 * Copyright (c) 2026 diet-dost and/or its contributors.
 * Licensed under the "GNU Affero General Public License v3.0 only" and
 * the "Server Side Public License, v 1"; you may not use this file except
 * in compliance with, at your election, the "GNU Affero General Public
 * License v3.0 only" or the "Server Side Public License, v 1".
 */

/**
 * Lightweight SignalR & In-App Real-Time Notification Client Service.
 * Implements native WebSocket transport adhering to ASP.NET Core SignalR JSON Hub Protocol.
 * Handles automatic handshake, ping/pong keepalives, applicant channel subscription,
 * SuperAdmin real-time alerts, and notification REST persistence endpoints.
 */
export class NotificationService {
  constructor(apiClient) {
    this.api = apiClient;
    this.ws = null;
    this.isConnected = false;
    this.isHandshakeComplete = false;
    this.listeners = new Map();
    this.applicantTicket = null;
    this.reconnectAttempts = 0;
    this.maxReconnectAttempts = 10;
    this.reconnectTimer = null;
    this.pingInterval = null;
    this.shouldReconnect = true;
    this.RECORD_SEPARATOR = String.fromCharCode(0x1e); // 0x1E record separator
  }

  /**
   * Register an event listener for hub messages or connection events.
   * e.g., 'NewRegistration', 'AccountApproved', 'connected', 'disconnected'.
   */
  on(event, callback) {
    if (!this.listeners.has(event)) {
      this.listeners.set(event, new Set());
    }
    this.listeners.get(event).add(callback);
    return () => this.off(event, callback);
  }

  off(event, callback) {
    if (this.listeners.has(event)) {
      this.listeners.get(event).delete(callback);
    }
  }

  emit(event, data) {
    const cbs = this.listeners.get(event);
    if (cbs) {
      cbs.forEach(cb => {
        try {
          cb(data);
        } catch (err) {
          console.error(`[NotificationService] Error in listener for ${event}:`, err);
        }
      });
    }
  }

  /**
   * Connect to SignalR notification hub via native WebSocket.
   */
  connect() {
    if (this.ws && (this.ws.readyState === WebSocket.OPEN || this.ws.readyState === WebSocket.CONNECTING)) {
      return;
    }

    this.shouldReconnect = true;
    const protocol = window.location.protocol === 'https:' ? 'wss:' : 'ws:';
    let url = `${protocol}//${window.location.host}/hubs/notifications`;

    const token = localStorage.getItem('dd_jwt_token');
    if (token) {
      url += `?access_token=${encodeURIComponent(token)}`;
    }

    try {
      this.ws = new WebSocket(url);
    } catch (err) {
      console.warn('[NotificationService] Failed to create WebSocket:', err);
      this._scheduleReconnect();
      return;
    }

    this.ws.onopen = () => {
      // Send SignalR JSON protocol handshake
      const handshake = JSON.stringify({ protocol: 'json', version: 1 }) + this.RECORD_SEPARATOR;
      this.ws.send(handshake);
    };

    this.ws.onmessage = (event) => {
      this._handleIncomingMessage(event.data);
    };

    this.ws.onerror = (err) => {
      console.warn('[NotificationService] WebSocket error:', err);
    };

    this.ws.onclose = () => {
      this.isConnected = false;
      this.isHandshakeComplete = false;
      this._stopPing();
      this.emit('disconnected');
      if (this.shouldReconnect) {
        this._scheduleReconnect();
      }
    };
  }

  disconnect() {
    this.shouldReconnect = false;
    if (this.reconnectTimer) {
      clearTimeout(this.reconnectTimer);
      this.reconnectTimer = null;
    }
    this._stopPing();
    if (this.ws) {
      this.ws.close();
      this.ws = null;
    }
    this.isConnected = false;
    this.isHandshakeComplete = false;
  }

  /**
   * Join private channel for pending applicant using registration ticket.
   */
  joinApplicantChannel(ticket) {
    if (!ticket) return;
    this.applicantTicket = ticket;
    if (this.isHandshakeComplete) {
      this._sendInvocation('JoinApplicantChannel', [ticket]);
    }
  }

  _sendInvocation(target, args = []) {
    if (!this.ws || this.ws.readyState !== WebSocket.OPEN) return;
    const invocation = JSON.stringify({
      type: 1, // Invocation
      target: target,
      arguments: args
    }) + this.RECORD_SEPARATOR;
    this.ws.send(invocation);
  }

  _handleIncomingMessage(data) {
    if (typeof data !== 'string') return;
    const messages = data.split(this.RECORD_SEPARATOR).filter(m => m.trim().length > 0);

    for (const raw of messages) {
      try {
        const msg = JSON.parse(raw);

        // Handshake response is empty object {}
        if (!this.isHandshakeComplete) {
          if (msg.error) {
            console.error('[NotificationService] SignalR handshake error:', msg.error);
            this.disconnect();
            return;
          }
          this.isHandshakeComplete = true;
          this.isConnected = true;
          this.reconnectAttempts = 0;
          this._startPing();
          this.emit('connected');

          // If applicant ticket was queued, join now
          if (this.applicantTicket) {
            this._sendInvocation('JoinApplicantChannel', [this.applicantTicket]);
          }
          continue;
        }

        // Ping message from server (type: 6) -> respond with ping
        if (msg.type === 6) {
          if (this.ws && this.ws.readyState === WebSocket.OPEN) {
            this.ws.send(JSON.stringify({ type: 6 }) + this.RECORD_SEPARATOR);
          }
          continue;
        }

        // Server invocation message (type: 1)
        if (msg.type === 1 && msg.target) {
          const args = msg.arguments || [];
          this.emit(msg.target, args[0]);
        }
      } catch (err) {
        console.warn('[NotificationService] Failed to parse message chunk:', err, raw);
      }
    }
  }

  _startPing() {
    this._stopPing();
    this.pingInterval = setInterval(() => {
      if (this.ws && this.ws.readyState === WebSocket.OPEN && this.isHandshakeComplete) {
        this.ws.send(JSON.stringify({ type: 6 }) + this.RECORD_SEPARATOR);
      }
    }, 15000);
  }

  _stopPing() {
    if (this.pingInterval) {
      clearInterval(this.pingInterval);
      this.pingInterval = null;
    }
  }

  _scheduleReconnect() {
    if (this.reconnectTimer || !this.shouldReconnect) return;
    if (this.reconnectAttempts >= this.maxReconnectAttempts) {
      console.warn('[NotificationService] Max reconnect attempts reached.');
      return;
    }

    const delay = Math.min(1000 * Math.pow(1.5, this.reconnectAttempts), 30000);
    this.reconnectAttempts++;
    this.reconnectTimer = setTimeout(() => {
      this.reconnectTimer = null;
      this.connect();
    }, delay);
  }

  // ── REST Persistence API Helpers ──────────────────────────────────────────

  /**
   * Fetch persisted notifications for current user/role.
   */
  async fetchNotifications(limit = 20, unreadOnly = false) {
    try {
      const res = await this.api.get(`/api/notifications?limit=${limit}&unreadOnly=${unreadOnly}`);
      return res?.notifications || res || [];
    } catch (err) {
      console.warn('[NotificationService] Failed to fetch notifications:', err);
      return [];
    }
  }

  /**
   * Mark single notification as read.
   */
  async markAsRead(notificationId) {
    try {
      return await this.api.put(`/api/notifications/${notificationId}/read`, {});
    } catch (err) {
      console.warn(`[NotificationService] Failed to mark notification ${notificationId} as read:`, err);
      return null;
    }
  }

  /**
   * Mark all notifications as read.
   */
  async markAllAsRead() {
    try {
      return await this.api.put('/api/notifications/read-all', {});
    } catch (err) {
      console.warn('[NotificationService] Failed to mark all notifications as read:', err);
      return null;
    }
  }
}

