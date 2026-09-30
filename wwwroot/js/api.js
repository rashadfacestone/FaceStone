/* ==========================================================================
   FACESTONE REAL-TIME CHAT — API SERVICE LAYER
   Production Client for ASP.NET Core Web API Backend
   Multi-Tab Isolated Authentication & Idempotent Messaging
   ========================================================================== */

const API_BASE_URL = window.location.origin;
const JWT_STORAGE_KEY = 'facestone_chat_jwt';
const USER_STORAGE_KEY = 'facestone_chat_user';

class ChatApi {
  /**
   * Retrieves active token, strictly scoped to tab-isolated sessionStorage
   * to ensure simultaneous multi-account browsing across separate browser tabs.
   */
  static getToken() {
    return sessionStorage.getItem(JWT_STORAGE_KEY);
  }

  static setToken(token) {
    if (token) {
      sessionStorage.setItem(JWT_STORAGE_KEY, token);
    } else {
      sessionStorage.removeItem(JWT_STORAGE_KEY);
    }
  }

  static getCurrentUser() {
    try {
      const raw = sessionStorage.getItem(USER_STORAGE_KEY);
      return raw ? JSON.parse(raw) : null;
    } catch {
      return null;
    }
  }

  static setCurrentUser(user) {
    if (user) {
      sessionStorage.setItem(USER_STORAGE_KEY, JSON.stringify(user));
    } else {
      sessionStorage.removeItem(USER_STORAGE_KEY);
    }
  }

  static isHandlingSessionExpiry = false;

  /**
   * Central Session Expiration Handler
   * Prevents duplicate toast notifications and request loops across all concurrent calls.
   */
  static handleSessionExpired() {
    if (this.isHandlingSessionExpiry) {
      return;
    }
    this.isHandlingSessionExpiry = true;

    // Clear tab-isolated authentication tokens
    sessionStorage.removeItem(JWT_STORAGE_KEY);
    sessionStorage.removeItem(USER_STORAGE_KEY);

    // Stop real-time WebSocket connection and background polling immediately
    if (window.realTimeChat) {
      window.realTimeChat.stop();
    }

    // Stop active live location tracking if running
    if (window.ChatApp && typeof window.ChatApp.stopTrackingOnSessionExpired === 'function') {
      window.ChatApp.stopTrackingOnSessionExpired();
    }

    // Dispatch central event exactly once
    window.dispatchEvent(new CustomEvent('chat:session:expired'));
  }

  static clearSession() {
    this.handleSessionExpired();
  }

  static isAuthenticated() {
    return !!this.getToken() && !this.isHandlingSessionExpiry;
  }

  /**
   * Internal fetch wrapper with secure Bearer authentication
   */
  static async request(endpoint, options = {}) {
    const isPublicAuthEndpoint = endpoint.startsWith('/api/auth/login') ||
                                 endpoint.startsWith('/api/auth/register') ||
                                 endpoint.startsWith('/api/auth/forgot-password') ||
                                 endpoint.startsWith('/api/auth/detect-identifier');

    // Prevent making authenticated requests if the session is already known to be expired
    if (!isPublicAuthEndpoint && (this.isHandlingSessionExpiry || !this.getToken())) {
      return {
        success: false,
        status: 401,
        message: 'Session expired. Please sign in again.'
      };
    }

    const url = `${API_BASE_URL}${endpoint}`;
    const token = this.getToken();

    const headers = {
      ...(options.headers || {})
    };

    if (!(options.body instanceof FormData) && !headers['Content-Type']) {
      headers['Content-Type'] = 'application/json';
    }

    if (token) {
      headers['Authorization'] = `Bearer ${token}`;
    }

    try {
      const response = await fetch(url, {
        ...options,
        headers
      });

      if (response.status === 401) {
        this.handleSessionExpired();
        return {
          success: false,
          status: 401,
          message: 'Session expired. Please sign in again.'
        };
      }

      const data = await response.json().catch(() => null);

      if (!response.ok) {
        return {
          success: false,
          status: response.status,
          message: data?.message || data?.errors?.[0] || `Request failed (${response.status})`,
          errors: data?.errors || []
        };
      }

      return data;
    } catch (err) {
      return {
        success: false,
        message: 'Could not connect to server. Please verify your connection.',
        errors: [err.message]
      };
    }
  }

  /* ------------------------------------------------------------------------
     Authentication Endpoints (/api/auth)
     ------------------------------------------------------------------------ */
  static async login(identifier, password) {
    const res = await this.request('/api/auth/login', {
      method: 'POST',
      body: JSON.stringify({ identifier, password })
    });

    if (res.success && res.data) {
      this.isHandlingSessionExpiry = false;
      this.setToken(res.data.token);
      this.setCurrentUser(res.data.user);
    }
    return res;
  }

  static async register(userData) {
    const res = await this.request('/api/auth/register', {
      method: 'POST',
      body: JSON.stringify(userData)
    });

    if (res.success && res.data) {
      this.isHandlingSessionExpiry = false;
      this.setToken(res.data.token);
      this.setCurrentUser(res.data.user);
    }
    return res;
  }

  static async getProfile() {
    const res = await this.request('/api/auth/me', {
      method: 'GET'
    });

    if (res.success && res.data) {
      this.setCurrentUser(res.data);
    }
    return res;
  }

  static async detectIdentifier(identifier) {
    return await this.request('/api/auth/detect-identifier', {
      method: 'POST',
      body: JSON.stringify({ identifier })
    });
  }

  /* ------------------------------------------------------------------------
     Chat Endpoints (/api/chat)
     ------------------------------------------------------------------------ */
  static async sendMessage(messageText, attachmentFile = null, targetEmployeeId = null, clientMessageId = null) {
    const formData = new FormData();
    if (messageText) formData.append('Message', messageText);
    if (attachmentFile) formData.append('Attachment', attachmentFile);
    if (targetEmployeeId && targetEmployeeId !== 'desk') {
      formData.append('TargetEmployeeId', targetEmployeeId);
    }
    if (clientMessageId) {
      formData.append('ClientMessageId', clientMessageId);
    }

    return await this.request('/api/chat/send', {
      method: 'POST',
      body: formData
    });
  }

  static async getChatHistory() {
    return await this.request('/api/chat/history', {
      method: 'GET'
    });
  }

  static async getConversations() {
    return await this.request('/api/chat/conversations', {
      method: 'GET'
    });
  }

  static async getConversationMessages(employeeId) {
    return await this.request(`/api/chat/conversations/${employeeId}`, {
      method: 'GET'
    });
  }

  static async pollChat(activeEmployeeId = null, afterIsoDate = null) {
    const params = new URLSearchParams();
    if (activeEmployeeId && activeEmployeeId !== 'desk') {
      params.append('activeEmployeeId', activeEmployeeId);
    }
    if (afterIsoDate) {
      params.append('after', afterIsoDate);
    }
    const qs = params.toString() ? `?${params.toString()}` : '';

    return await this.request(`/api/chat/poll${qs}`, {
      method: 'GET'
    });
  }

  /* ------------------------------------------------------------------------
     Attachment Endpoints (/api/chat/attachments)
     ------------------------------------------------------------------------ */
  static getAttachmentUrl(attachmentId, download = false) {
    if (!attachmentId) return '';
    const token = this.getToken();
    const base = `${API_BASE_URL}/api/chat/attachments/${attachmentId}${download ? '/download' : ''}`;
    return token ? `${base}?token=${encodeURIComponent(token)}` : base;
  }

  static async fetchAttachmentBlob(attachmentId) {
    const token = this.getToken();
    const url = `${API_BASE_URL}/api/chat/attachments/${attachmentId}`;
    const headers = {};
    if (token) headers['Authorization'] = `Bearer ${token}`;

    const res = await fetch(url, { headers });
    if (!res.ok) {
      throw new Error(`Failed to load attachment (${res.status})`);
    }
    return await res.blob();
  }

  static async downloadAttachment(attachmentId, fileName) {
    try {
      const blob = await this.fetchAttachmentBlob(attachmentId);
      const blobUrl = URL.createObjectURL(blob);
      const a = document.createElement('a');
      a.href = blobUrl;
      a.download = fileName || 'attachment';
      document.body.appendChild(a);
      a.click();
      document.body.removeChild(a);
      setTimeout(() => URL.revokeObjectURL(blobUrl), 15000);
    } catch (err) {
      console.warn('Authenticated blob download failed, falling back to direct token URL:', err);
      const directUrl = this.getAttachmentUrl(attachmentId, true);
      const a = document.createElement('a');
      a.href = directUrl;
      a.download = fileName || 'attachment';
      a.target = '_blank';
      document.body.appendChild(a);
      a.click();
      document.body.removeChild(a);
    }
  }

  /* ------------------------------------------------------------------------
     Location Endpoints (/api/chat/location & /api/chat/live-location)
     ------------------------------------------------------------------------ */
  static async sendLocation(latitude, longitude, targetEmployeeId = null, clientMessageId = null) {
    return await this.request('/api/chat/location/send', {
      method: 'POST',
      body: JSON.stringify({
        latitude,
        longitude,
        targetEmployeeId: targetEmployeeId && targetEmployeeId !== 'desk' ? targetEmployeeId : null,
        clientMessageId
      })
    });
  }

  static async sendLiveLocation(latitude, longitude, durationMinutes = 15, targetEmployeeId = null, clientMessageId = null) {
    const payload = {
      messageType: 'LIVE_LOCATION',
      latitude,
      longitude,
      location: {
        latitude,
        longitude
      },
      durationMinutes,
      targetEmployeeId: targetEmployeeId && targetEmployeeId !== 'desk' ? targetEmployeeId : null,
      conversationId: targetEmployeeId && targetEmployeeId !== 'desk' ? targetEmployeeId : null,
      status: 'ACTIVE',
      startedAt: new Date().toISOString(),
      clientMessageId
    };

    return await this.request('/api/chat/send', {
      method: 'POST',
      body: JSON.stringify(payload)
    });
  }

  static async startLiveLocation(latitude, longitude, durationMinutes = 15, targetEmployeeId = null, clientMessageId = null) {
    return await this.sendLiveLocation(latitude, longitude, durationMinutes, targetEmployeeId, clientMessageId);
  }

  static async updateLiveLocation(sessionId, latitude, longitude) {
    return await this.request('/api/chat/live-location/update', {
      method: 'POST',
      body: JSON.stringify({
        sessionId,
        latitude,
        longitude
      })
    });
  }

  static async stopLiveLocation(sessionId) {
    return await this.request('/api/chat/live-location/stop', {
      method: 'POST',
      body: JSON.stringify({
        sessionId
      })
    });
  }

  /* ------------------------------------------------------------------------
     Users Endpoints (/api/users)
     ------------------------------------------------------------------------ */
  static async getUsers(role = null, search = null) {
    const params = new URLSearchParams();
    if (role) params.append('role', role);
    if (search) params.append('search', search);
    const qs = params.toString() ? `?${params.toString()}` : '';

    return await this.request(`/api/users${qs}`, {
      method: 'GET'
    });
  }

  /* ------------------------------------------------------------------------
     Department Endpoints (/api/departments, /api/department/transportation)
     ------------------------------------------------------------------------ */
  static async getDepartments() {
    return await this.request('/api/departments', {
      method: 'GET'
    });
  }

  static async verifyTransportationAccess() {
    return await this.request('/api/department/transportation', {
      method: 'GET'
    });
  }

  static async verifyStorageAccess() {
    return await this.request('/api/department/storage', {
      method: 'GET'
    });
  }

  static async verifyManagementAccess() {
    return await this.request('/api/department/management', {
      method: 'GET'
    });
  }

  static async verifyFinanceAccess() {
    return await this.request('/api/department/finance', {
      method: 'GET'
    });
  }

  /* ------------------------------------------------------------------------
     Finance Department Endpoints (/api/finance)
     ------------------------------------------------------------------------ */
  static async getFinanceManagementData(params = {}) {
    const qs = new URLSearchParams();
    if (params.search) qs.append('search', params.search);
    if (params.po && params.po !== 'all') qs.append('po', params.po);
    if (params.supplier && params.supplier !== 'all') qs.append('supplier', params.supplier);
    if (params.paymentStatus && params.paymentStatus !== 'all') qs.append('paymentStatus', params.paymentStatus);
    if (params.invoiceNumber && params.invoiceNumber !== 'all') qs.append('invoiceNumber', params.invoiceNumber);
    if (params.dateFrom) qs.append('dateFrom', params.dateFrom);
    if (params.dateTo) qs.append('dateTo', params.dateTo);
    if (params.page) qs.append('page', params.page);
    if (params.pageSize) qs.append('pageSize', params.pageSize);

    const queryStr = qs.toString() ? `?${qs.toString()}` : '';
    return await this.request(`/api/finance/management-data${queryStr}`, {
      method: 'GET'
    });
  }

  static async getFinancePayments(poNumber) {
    return await this.request(`/api/finance/payments/${encodeURIComponent(poNumber)}`, {
      method: 'GET'
    });
  }

  /* ------------------------------------------------------------------------
     Transportation Department Endpoints (/api/transportation)
     ------------------------------------------------------------------------ */
  static async submitTransportation(data) {
    return await this.request('/api/transportation', {
      method: 'POST',
      body: JSON.stringify(data)
    });
  }

  static async getTransportationConfig() {
    return await this.request('/api/transportation/config', {
      method: 'GET'
    });
  }

  static async getRecentTransportationTransactions(limit = 10) {
    return await this.request(`/api/transportation/recent?limit=${limit}`, {
      method: 'GET'
    });
  }

  static async checkTransportationNumber(txNumber) {
    return await this.request(`/api/transportation/check-number/${txNumber}`, {
      method: 'GET'
    });
  }

  /* ------------------------------------------------------------------------
     Storage Department Endpoints (/api/storage)
     ------------------------------------------------------------------------ */
  static async submitStorage(data) {
    return await this.request('/api/storage', {
      method: 'POST',
      body: JSON.stringify(data)
    });
  }

  static async getStorageConfig() {
    return await this.request('/api/storage/config', {
      method: 'GET'
    });
  }

  static async getRecentStorageTransactions(limit = 20) {
    return await this.request(`/api/storage/recent?limit=${limit}`, {
      method: 'GET'
    });
  }

  static async checkStorageReferenceNumber(refNumber) {
    return await this.request(`/api/storage/check-reference/${refNumber}`, {
      method: 'GET'
    });
  }

  static async getStorageBalance(storageLocation = null) {
    const qs = storageLocation ? `?storageLocation=${encodeURIComponent(storageLocation)}` : '';
    return await this.request(`/api/storage/balance${qs}`, {
      method: 'GET'
    });
  }

  /* ------------------------------------------------------------------------
     Management Department Endpoints (/api/management)
     ------------------------------------------------------------------------ */
  static async getManagementData(params = {}) {
    const qs = new URLSearchParams();
    if (params.search) qs.append('search', params.search);
    if (params.materialType && params.materialType !== 'all') qs.append('materialType', params.materialType);
    if (params.receivingStatus && params.receivingStatus !== 'all') qs.append('receivingStatus', params.receivingStatus);
    if (params.destination && params.destination !== 'all') qs.append('destination', params.destination);
    if (params.fromDate) qs.append('fromDate', params.fromDate);
    if (params.toDate) qs.append('toDate', params.toDate);
    if (params.sortBy) qs.append('sortBy', params.sortBy);
    if (params.sortOrder) qs.append('sortOrder', params.sortOrder);
    if (params.page) qs.append('page', params.page);
    if (params.pageSize) qs.append('pageSize', params.pageSize);

    const queryStr = qs.toString() ? `?${qs.toString()}` : '';
    return await this.request(`/api/management/data${queryStr}`, {
      method: 'GET'
    });
  }

  static async getManagementSummary() {
    return await this.request('/api/management/summary', {
      method: 'GET'
    });
  }

  static async syncManagement() {
    return await this.request('/api/management/sync', {
      method: 'POST'
    });
  }
}

window.ChatApi = ChatApi;
