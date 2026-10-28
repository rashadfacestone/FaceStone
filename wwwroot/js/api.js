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
    return sessionStorage.getItem(JWT_STORAGE_KEY) || 
           localStorage.getItem(JWT_STORAGE_KEY) ||
           sessionStorage.getItem('facestone_token') || 
           localStorage.getItem('facestone_token') || '';
  }

  static setToken(token) {
    if (token) {
      sessionStorage.setItem(JWT_STORAGE_KEY, token);
      localStorage.setItem(JWT_STORAGE_KEY, token);
      sessionStorage.setItem('facestone_token', token);
      localStorage.setItem('facestone_token', token);
    } else {
      sessionStorage.removeItem(JWT_STORAGE_KEY);
      localStorage.removeItem(JWT_STORAGE_KEY);
      sessionStorage.removeItem('facestone_token');
      localStorage.removeItem('facestone_token');
    }
  }

  static getCurrentUser() {
    try {
      const raw = sessionStorage.getItem(USER_STORAGE_KEY) || localStorage.getItem(USER_STORAGE_KEY);
      return raw ? JSON.parse(raw) : null;
    } catch {
      return null;
    }
  }

  static setCurrentUser(user) {
    if (user) {
      sessionStorage.setItem(USER_STORAGE_KEY, JSON.stringify(user));
      localStorage.setItem(USER_STORAGE_KEY, JSON.stringify(user));
    } else {
      sessionStorage.removeItem(USER_STORAGE_KEY);
      localStorage.removeItem(USER_STORAGE_KEY);
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

    // Clear persistent and tab authentication tokens
    sessionStorage.removeItem(JWT_STORAGE_KEY);
    sessionStorage.removeItem(USER_STORAGE_KEY);
    localStorage.removeItem(JWT_STORAGE_KEY);
    localStorage.removeItem(USER_STORAGE_KEY);

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
      formData.append('RecipientId', targetEmployeeId);
    }
    if (clientMessageId) {
      formData.append('ClientMessageId', clientMessageId);
    }

    return await this.request('/api/chat/send', {
      method: 'POST',
      body: formData
    });
  }

  static async searchUsers(searchTerm = '') {
    const qs = searchTerm ? `?search=${encodeURIComponent(searchTerm)}` : '';
    return await this.request(`/api/chat/users${qs}`, {
      method: 'GET'
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

  /**
   * Appends active JWT authentication token to any attachment or resource URL.
   */
  static getAuthenticatedUrl(rawUrl) {
    if (!rawUrl) return '';
    const token = this.getToken();
    if (!token) return rawUrl;
    const fullUrl = (rawUrl.startsWith('http://') || rawUrl.startsWith('https://'))
      ? rawUrl
      : `${API_BASE_URL}${rawUrl.startsWith('/') ? '' : '/'}${rawUrl}`;
    const separator = fullUrl.includes('?') ? '&' : '?';
    return `${fullUrl}${separator}token=${encodeURIComponent(token)}`;
  }

  /**
   * Securely fetches attachment as a Blob using Bearer token Authorization header
   * and fallbacks to query parameter token.
   */
  static async fetchAuthenticatedBlob(urlOrEndpoint) {
    const token = this.getToken();
    const rawUrl = (urlOrEndpoint.startsWith('http://') || urlOrEndpoint.startsWith('https://'))
      ? urlOrEndpoint
      : `${API_BASE_URL}${urlOrEndpoint.startsWith('/') ? '' : '/'}${urlOrEndpoint}`;
    const authedUrl = this.getAuthenticatedUrl(rawUrl);
    const headers = {};
    if (token) {
      headers['Authorization'] = `Bearer ${token}`;
    }

    const res = await fetch(authedUrl, { headers });
    if (!res.ok) {
      if (res.status === 401) {
        this.handleSessionExpired();
      }
      throw new Error(`تعذر تنزيل الملف (رمز الاستجابة: ${res.status})`);
    }

    // Try to extract original file name from Content-Disposition header if available
    let dispositionFileName = '';
    const cd = res.headers.get('content-disposition');
    if (cd) {
      const fnMatch = cd.match(/filename\*=UTF-8''([^;]+)|filename="?([^";]+)"?/i);
      if (fnMatch) {
        dispositionFileName = decodeURIComponent(fnMatch[1] || fnMatch[2] || '');
      }
    }

    const blob = await res.blob();
    return { blob, fileName: dispositionFileName, contentType: res.headers.get('content-type') };
  }

  /**
   * Triggers native browser download for any authenticated attachment endpoint.
   */
  static async downloadAttachmentFromUrl(urlOrEndpoint, fallbackFileName) {
    const token = this.getToken();
    const authedUrl = this.getAuthenticatedUrl(urlOrEndpoint);

    try {
      const { blob, fileName } = await this.fetchAuthenticatedBlob(urlOrEndpoint);
      const downloadName = fileName || fallbackFileName || 'attachment';
      const blobUrl = URL.createObjectURL(blob);
      const a = document.createElement('a');
      a.style.display = 'none';
      a.href = blobUrl;
      a.download = downloadName;
      document.body.appendChild(a);
      a.click();
      setTimeout(() => {
        document.body.removeChild(a);
        URL.revokeObjectURL(blobUrl);
      }, 5000);
      return true;
    } catch (err) {
      console.warn('Authenticated blob fetch failed, falling back to direct authed link:', err);
      if (!token) throw err;
      const a = document.createElement('a');
      a.style.display = 'none';
      a.href = authedUrl;
      a.download = fallbackFileName || 'attachment';
      a.target = '_blank';
      document.body.appendChild(a);
      a.click();
      setTimeout(() => document.body.removeChild(a), 3000);
      return true;
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

  static async verifyControlMonitoringAccess() {
    return await this.request('/api/department/control-monitoring', {
      method: 'GET'
    });
  }

  /* ------------------------------------------------------------------------
     Control & Monitoring Department Endpoints (/api/control-monitoring)
     ------------------------------------------------------------------------ */
  static async getControlMonitoringStats() {
    return await this.request('/api/control-monitoring/stats', {
      method: 'GET'
    });
  }

  static async getControlMonitoringForms(params = {}) {
    const qs = new URLSearchParams();
    if (params.status && params.status !== 'all') qs.append('status', params.status);
    if (params.formType && params.formType !== 'all') qs.append('formType', params.formType);
    if (params.po && params.po !== 'all') qs.append('po', params.po);
    if (params.employee) qs.append('employee', params.employee);
    if (params.search) qs.append('search', params.search);
    if (params.fromDate) qs.append('fromDate', params.fromDate);
    if (params.toDate) qs.append('toDate', params.toDate);
    const queryStr = qs.toString() ? `?${qs.toString()}` : '';
    return await this.request(`/api/control-monitoring/forms${queryStr}`, {
      method: 'GET'
    });
  }

  static async getControlMonitoringFormDetail(formType, id) {
    if (id === undefined || id === null) {
      return await this.request(`/api/control-monitoring/forms/${encodeURIComponent(formType)}`, {
        method: 'GET'
      });
    }
    return await this.request(`/api/control-monitoring/forms/${encodeURIComponent(formType)}/${encodeURIComponent(id)}`, {
      method: 'GET'
    });
  }

  static async acceptControlMonitoringForm(formType, id) {
    return await this.request(`/api/control-monitoring/forms/${encodeURIComponent(formType)}/${encodeURIComponent(id)}/accept`, {
      method: 'POST'
    });
  }

  static async rejectControlMonitoringForm(formType, id, reason) {
    return await this.request(`/api/control-monitoring/forms/${encodeURIComponent(formType)}/${encodeURIComponent(id)}/reject`, {
      method: 'POST',
      body: JSON.stringify({ reason: reason || '' })
    });
  }

  static async approveAttendanceRecord(recordId) {
    return await this.request(`/api/control-monitoring/attendance/records/${encodeURIComponent(recordId)}/approve`, {
      method: 'POST'
    });
  }

  static async rejectAttendanceRecord(recordId, reason) {
    return await this.request(`/api/control-monitoring/attendance/records/${encodeURIComponent(recordId)}/reject`, {
      method: 'POST',
      body: JSON.stringify({ reason: reason || '', rejectionReason: reason || '' })
    });
  }

  static async resubmitControlMonitoringForm(formType, id, comment) {
    return await this.request(`/api/control-monitoring/forms/${encodeURIComponent(formType)}/${encodeURIComponent(id)}/resubmit`, {
      method: 'POST',
      body: JSON.stringify({ comment: comment || '' })
    });
  }

  static async getControlMonitoringHistory(formType, id) {
    return await this.request(`/api/control-monitoring/forms/${encodeURIComponent(formType)}/${encodeURIComponent(id)}/history`, {
      method: 'GET'
    });
  }

  static async getControlMonitoringFullHistory(params = {}) {
    const qs = new URLSearchParams();
    if (params.status && params.status !== 'all') qs.append('status', params.status);
    if (params.department && params.department !== 'all') qs.append('department', params.department);
    else if (params.formType && params.formType !== 'all') qs.append('formType', params.formType);
    if (params.po) qs.append('po', params.po);
    if (params.employee) qs.append('employee', params.employee);
    if (params.reviewer) qs.append('reviewer', params.reviewer);
    if (params.search) qs.append('search', params.search);
    if (params.fromDate) qs.append('fromDate', params.fromDate);
    if (params.toDate) qs.append('toDate', params.toDate);
    if (params.page) qs.append('page', params.page);
    if (params.pageSize) qs.append('pageSize', params.pageSize);
    if (params.sortBy) qs.append('sortBy', params.sortBy);
    if (params.sortOrder) qs.append('sortOrder', params.sortOrder);
    const queryStr = qs.toString() ? `?${qs.toString()}` : '';
    return await this.request(`/api/control-monitoring/history${queryStr}`, {
      method: 'GET'
    });
  }

  static async resubmitTransportation(id, formData) {
    const token = this.getToken();
    const headers = {};
    if (token) {
      headers['Authorization'] = `Bearer ${token}`;
    }
    try {
      const response = await fetch(`${API_BASE_URL}/api/transportation/${id}/resubmit`, {
        method: 'POST',
        headers: headers,
        body: formData
      });
      let json = {};
      try {
        json = await response.json();
      } catch {
        json = { success: response.ok, message: response.statusText };
      }
      return {
        success: response.ok,
        status: response.status,
        ...json
      };
    } catch (err) {
      return {
        success: false,
        message: err.message || 'فشل الاتصال بالخادم.'
      };
    }
  }

  static async resubmitStorage(id, formData) {
    const token = this.getToken();
    const headers = {};
    if (token) {
      headers['Authorization'] = `Bearer ${token}`;
    }
    try {
      const response = await fetch(`${API_BASE_URL}/api/storage/${id}/resubmit`, {
        method: 'POST',
        headers: headers,
        body: formData
      });
      let json = {};
      try {
        json = await response.json();
      } catch {
        json = { success: response.ok, message: response.statusText };
      }
      return {
        success: response.ok,
        status: response.status,
        ...json
      };
    } catch (err) {
      return {
        success: false,
        message: err.message || 'فشل الاتصال بالخادم.'
      };
    }
  }

  /* ------------------------------------------------------------------------
     Attendance Department Endpoints (/api/attendance)
     ------------------------------------------------------------------------ */
  static async getAttendanceEmployees() {
    return await this.request('/api/attendance/employees', {
      method: 'GET'
    });
  }

  static async getAttendanceSheet(dateStr) {
    const url = dateStr ? `/api/attendance/date/${encodeURIComponent(dateStr)}` : '/api/attendance/sheet';
    return await this.request(url, {
      method: 'GET'
    });
  }

  static async getAttendanceSubmission(id) {
    return await this.request(`/api/attendance/submissions/${encodeURIComponent(id)}`, {
      method: 'GET'
    });
  }

  static async submitAttendance(formData) {
    const token = this.getToken();
    const headers = {};
    if (token) {
      headers['Authorization'] = `Bearer ${token}`;
    }
    try {
      const response = await fetch(`${API_BASE_URL}/api/attendance/submit`, {
        method: 'POST',
        headers: headers,
        body: formData
      });
      let json = {};
      try {
        json = await response.json();
      } catch {
        json = { success: response.ok, message: response.statusText };
      }
      return {
        success: response.ok,
        status: response.status,
        ...json
      };
    } catch (err) {
      return {
        success: false,
        message: err.message || 'فشل الاتصال بالخادم.'
      };
    }
  }

  static async resubmitAttendance(id, formData) {
    const token = this.getToken();
    const headers = {};
    if (token) {
      headers['Authorization'] = `Bearer ${token}`;
    }
    try {
      const response = await fetch(`${API_BASE_URL}/api/attendance/submissions/${encodeURIComponent(id)}/resubmit`, {
        method: 'POST',
        headers: headers,
        body: formData
      });
      let json = {};
      try {
        json = await response.json();
      } catch {
        json = { success: response.ok, message: response.statusText };
      }
      return {
        success: response.ok,
        status: response.status,
        ...json
      };
    } catch (err) {
      return {
        success: false,
        message: err.message || 'فشل الاتصال بالخادم.'
      };
    }
  }

  static async getTransportationById(id) {
    return await this.request(`/api/transportation/${encodeURIComponent(id)}`, {
      method: 'GET'
    });
  }

  static async getStorageById(id) {
    return await this.request(`/api/storage/${encodeURIComponent(id)}`, {
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

  static async getFinanceStatistics(fromDate, toDate, po) {
    const qs = new URLSearchParams();
    // Allow either (fromDate, toDate, po) or legacy (date, po)
    if (fromDate && !toDate && typeof fromDate === 'string' && fromDate.length === 10) {
      qs.append('date', fromDate);
    } else {
      if (fromDate) qs.append('fromDate', fromDate);
      if (toDate) qs.append('toDate', toDate);
    }
    if (po && po !== 'all') qs.append('po', po);
    const queryStr = qs.toString() ? `?${qs.toString()}` : '';
    return await this.request(`/api/finance/statistics${queryStr}`, {
      method: 'GET'
    });
  }

  static async getFinancePos() {
    return await this.request('/api/finance/pos', {
      method: 'GET'
    });
  }

  static async getFinancePurchaseOrders() {
    return await this.request('/api/finance/purchase-orders', {
      method: 'GET'
    });
  }

  static async getFinancePurchaseOrderDetails(poNumber) {
    return await this.request(`/api/finance/purchase-orders/${encodeURIComponent(poNumber)}`, {
      method: 'GET'
    });
  }

  static async updateFinancePaymentStatus(poNumber, paymentStatus, notes = '') {
    return await this.request(`/api/finance/purchase-orders/${encodeURIComponent(poNumber)}/payment-status`, {
      method: 'PUT',
      headers: {
        'Content-Type': 'application/json'
      },
      body: JSON.stringify({
        paymentStatus,
        notes
      })
    });
  }

  static async getFinancePerformanceDetails(fromDate, toDate, po) {
    const qs = new URLSearchParams();
    if (fromDate) qs.append('fromDate', fromDate);
    if (toDate) qs.append('toDate', toDate);
    if (po && po !== 'all') qs.append('po', po);
    const queryStr = qs.toString() ? `?${qs.toString()}` : '';
    return await this.request(`/api/finance/performance${queryStr}`, {
      method: 'GET'
    });
  }

  static async getFinanceHistory(params = {}) {
    const qs = new URLSearchParams();
    if (params.search) qs.append('search', params.search);
    if (params.type && params.type !== 'all') qs.append('type', params.type);
    if (params.po && params.po !== 'all') qs.append('po', params.po);
    if (params.supplier && params.supplier !== 'all') qs.append('supplier', params.supplier);
    if (params.fromDate) qs.append('fromDate', params.fromDate);
    if (params.toDate) qs.append('toDate', params.toDate);
    if (params.page) qs.append('page', params.page);
    if (params.pageSize) qs.append('pageSize', params.pageSize);

    const queryStr = qs.toString() ? `?${qs.toString()}` : '';
    return await this.request(`/api/finance/history${queryStr}`, {
      method: 'GET'
    });
  }

  static async getFinanceSuppliers() {
    return await this.request('/api/finance/suppliers', {
      method: 'GET'
    });
  }

  static async getFinanceSupplierDetail(id) {
    return await this.request(`/api/finance/suppliers/${encodeURIComponent(id)}`, {
      method: 'GET'
    });
  }

  static async getFinanceBankSummary() {
    return await this.request('/api/finance/bank', {
      method: 'GET'
    });
  }

  static async createFinanceBankTransaction(data) {
    return await this.request('/api/finance/bank', {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json'
      },
      body: JSON.stringify(data)
    });
  }

  static async getFinanceExpenses(params = {}) {
    const qs = new URLSearchParams();
    if (params.category && params.category !== 'all') qs.append('category', params.category);
    if (params.fromDate) qs.append('fromDate', params.fromDate);
    if (params.toDate) qs.append('toDate', params.toDate);

    const queryStr = qs.toString() ? `?${qs.toString()}` : '';
    return await this.request(`/api/finance/expenses${queryStr}`, {
      method: 'GET'
    });
  }

  static async createFinanceExpense(data) {
    return await this.request('/api/finance/expenses', {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json'
      },
      body: JSON.stringify(data)
    });
  }

  static async getFinanceEmployees() {
    return await this.request('/api/finance/employees', {
      method: 'GET'
    });
  }

  static async getFinanceEmployeeDetail(id) {
    return await this.request(`/api/finance/employees/${encodeURIComponent(id)}`, {
      method: 'GET'
    });
  }

  /* ------------------------------------------------------------------------
     Transportation Department Endpoints (/api/transportation)
     ------------------------------------------------------------------------ */
  static async getTransportationPurchaseOrder(poNumber) {
    if (!poNumber) return { success: false, message: 'رقم أمر الشراء مطلوب' };
    return await this.request(`/api/transportation/po/${encodeURIComponent(poNumber.trim())}`, {
      method: 'GET'
    });
  }

  static async submitTransportationTransaction(formData) {
    const token = this.getToken();
    const headers = {};
    if (token) {
      headers['Authorization'] = `Bearer ${token}`;
    }

    try {
      const response = await fetch(`${API_BASE_URL}/api/transportation`, {
        method: 'POST',
        headers: headers,
        body: formData
      });

      let json = {};
      try {
        json = await response.json();
      } catch {
        json = { success: response.ok, message: response.statusText };
      }

      return {
        success: response.ok,
        status: response.status,
        ...json
      };
    } catch (err) {
      return {
        success: false,
        message: err.message || 'حدث خطأ في الاتصال بالخادم.'
      };
    }
  }

  static getTransportationInvoiceUrl(fileName) {
    if (!fileName) return '#';
    return `${API_BASE_URL}/api/transportation/invoices/${encodeURIComponent(fileName)}`;
  }

  static async getTransportationPurchaseOrder(poNumber) {
    return await this.request(`/api/transportation/po/${encodeURIComponent(poNumber)}`, {
      method: 'GET'
    });
  }

  static async submitTransportationTransaction(formData) {
    return await this.request('/api/transportation', {
      method: 'POST',
      body: formData
    });
  }

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

  static async getManagementOverview() {
    return await this.request('/api/management/overview', {
      method: 'GET'
    });
  }

  static async getManagementSearch(query) {
    const qs = query ? `?q=${encodeURIComponent(query)}` : '';
    return await this.request(`/api/management/search${qs}`, {
      method: 'GET'
    });
  }

  static async getManagementTransportation(params = {}) {
    const qs = new URLSearchParams();
    if (params.fromDate) qs.append('fromDate', params.fromDate);
    if (params.toDate) qs.append('toDate', params.toDate);
    if (params.search) qs.append('search', params.search);
    if (params.po) qs.append('po', params.po);
    if (params.driver) qs.append('driver', params.driver);
    if (params.material) qs.append('material', params.material);
    if (params.destination) qs.append('destination', params.destination);
    const queryStr = qs.toString() ? `?${qs.toString()}` : '';
    return await this.request(`/api/management/transportation${queryStr}`, {
      method: 'GET'
    });
  }

  static async getManagementTransportationDetail(id) {
    return await this.request(`/api/management/transportation/${encodeURIComponent(id)}`, {
      method: 'GET'
    });
  }

  static async getManagementStorage(params = {}) {
    const qs = new URLSearchParams();
    if (params.fromDate) qs.append('fromDate', params.fromDate);
    if (params.toDate) qs.append('toDate', params.toDate);
    if (params.search) qs.append('search', params.search);
    if (params.material) qs.append('material', params.material);
    if (params.action) qs.append('action', params.action);
    if (params.po) qs.append('po', params.po);
    const queryStr = qs.toString() ? `?${qs.toString()}` : '';
    return await this.request(`/api/management/storage${queryStr}`, {
      method: 'GET'
    });
  }

  static async getManagementStorageDetail(id) {
    return await this.request(`/api/management/storage/${encodeURIComponent(id)}`, {
      method: 'GET'
    });
  }

  static async getManagementFinance(params = {}) {
    const qs = new URLSearchParams();
    if (params.fromDate) qs.append('fromDate', params.fromDate);
    if (params.toDate) qs.append('toDate', params.toDate);
    if (params.search) qs.append('search', params.search);
    if (params.po) qs.append('po', params.po);
    if (params.paymentStatus) qs.append('paymentStatus', params.paymentStatus);
    const queryStr = qs.toString() ? `?${qs.toString()}` : '';
    return await this.request(`/api/management/finance${queryStr}`, {
      method: 'GET'
    });
  }

  static async getManagementPurchaseOrderDetail(poNumber) {
    return await this.request(`/api/management/purchase-orders/${encodeURIComponent(poNumber)}`, {
      method: 'GET'
    });
  }

  static async getManagementEmployees(params = {}) {
    const qs = new URLSearchParams();
    if (params.search) qs.append('search', params.search);
    if (params.department) qs.append('department', params.department);
    if (params.position) qs.append('position', params.position);
    const queryStr = qs.toString() ? `?${qs.toString()}` : '';
    return await this.request(`/api/management/employees${queryStr}`, {
      method: 'GET'
    });
  }

  static async getManagementEmployeeDetail(id) {
    return await this.request(`/api/management/employees/${encodeURIComponent(id)}`, {
      method: 'GET'
    });
  }

  static async getManagementSuppliers(params = {}) {
    const qs = new URLSearchParams();
    if (params.search) qs.append('search', params.search);
    if (params.po) qs.append('po', params.po);
    if (params.material) qs.append('material', params.material);
    const queryStr = qs.toString() ? `?${qs.toString()}` : '';
    return await this.request(`/api/management/suppliers${queryStr}`, {
      method: 'GET'
    });
  }

  static async getManagementSupplierDetail(id) {
    return await this.request(`/api/management/suppliers/${encodeURIComponent(id)}`, {
      method: 'GET'
    });
  }

  static async syncManagement() {
    return await this.request('/api/management/sync', {
      method: 'POST'
    });
  }

  /* =========================================================================
     Attendance Department Endpoints (/api/attendance)
     ========================================================================= */

  static async verifyAttendanceAccess() {
    return await this.request('/api/department/attendance', {
      method: 'GET'
    });
  }

  static async getAttendanceEmployees() {
    return await this.request('/api/attendance/employees', {
      method: 'GET'
    });
  }

  static async getAttendanceSheet(date) {
    const qs = date ? `?date=${encodeURIComponent(date)}` : '';
    return await this.request(`/api/attendance/sheet${qs}`, {
      method: 'GET'
    });
  }

  static async getAttendanceSubmission(id) {
    return await this.request(`/api/attendance/submissions/${encodeURIComponent(id)}`, {
      method: 'GET'
    });
  }

  static async submitAttendance(formData) {
    const token = this.getToken();
    const headers = {};
    if (token) {
      headers['Authorization'] = `Bearer ${token}`;
    }
    try {
      const response = await fetch(`${API_BASE_URL}/api/attendance/submit`, {
        method: 'POST',
        headers: headers,
        body: formData
      });
      let json = {};
      try {
        json = await response.json();
      } catch {
        json = { success: response.ok, message: response.statusText };
      }
      return {
        success: response.ok,
        status: response.status,
        ...json
      };
    } catch (err) {
      return {
        success: false,
        message: err.message || 'فشل الاتصال بالخادم.'
      };
    }
  }

  static async resubmitAttendance(id, formData) {
    const token = this.getToken();
    const headers = {};
    if (token) {
      headers['Authorization'] = `Bearer ${token}`;
    }
    try {
      const response = await fetch(`${API_BASE_URL}/api/attendance/submissions/${encodeURIComponent(id)}/resubmit`, {
        method: 'POST',
        headers: headers,
        body: formData
      });
      let json = {};
      try {
        json = await response.json();
      } catch {
        json = { success: response.ok, message: response.statusText };
      }
      return {
        success: response.ok,
        status: response.status,
        ...json
      };
    } catch (err) {
      return {
        success: false,
        message: err.message || 'فشل الاتصال بالخادم.'
      };
    }
  }

  /* =========================================================================
     Storage Department Endpoints (/api/storage) - Redesign
     ========================================================================= */

  static async getStorageLocations() {
    return await this.request('/api/storage/locations', {
      method: 'GET'
    });
  }

  static async getStorageClients() {
    return await this.request('/api/storage/clients', {
      method: 'GET'
    });
  }

  static async getStorageMaterialsStock(locationId = null, type = null) {
    const qs = new URLSearchParams();
    if (locationId) qs.append('locationId', locationId);
    if (type) qs.append('type', type);
    const queryStr = qs.toString() ? `?${qs.toString()}` : '';
    return await this.request(`/api/storage/materials-stock${queryStr}`, {
      method: 'GET'
    });
  }

  static async getRawMaterials(type = null) {
    const qs = type ? `?type=${encodeURIComponent(type)}` : '';
    return await this.request(`/api/storage/materials${qs}`, {
      method: 'GET'
    });
  }

  static async getStorageNextNumbers() {
    return await this.request('/api/storage/next-numbers', {
      method: 'GET'
    });
  }

  static async submitStorageImport(data) {
    return await this.request('/api/storage/import', {
      method: 'POST',
      body: data instanceof FormData ? data : JSON.stringify(data)
    });
  }

  static async submitStorageExport(data) {
    return await this.request('/api/storage/export', {
      method: 'POST',
      body: data instanceof FormData ? data : JSON.stringify(data)
    });
  }

  static async submitStorageTransfer(data) {
    return await this.request('/api/storage/transfer', {
      method: 'POST',
      body: data instanceof FormData ? data : JSON.stringify(data)
    });
  }

  static async getStorageOperations(params = {}) {
    const qs = new URLSearchParams();
    if (params.type && params.type !== 'ALL') qs.append('type', params.type);
    if (params.status && params.status !== 'ALL') qs.append('status', params.status);
    if (params.page) qs.append('page', params.page);
    if (params.pageSize) qs.append('pageSize', params.pageSize);
    const queryStr = qs.toString() ? `?${qs.toString()}` : '';
    return await this.request(`/api/storage/operations${queryStr}`, {
      method: 'GET'
    });
  }

  static async getStorageOperationDetails(id) {
    return await this.request(`/api/storage/operations/${encodeURIComponent(id)}`, {
      method: 'GET'
    });
  }

  static async getStorageProductionBatches() {
    return await this.request('/api/storage/production-batches', {
      method: 'GET'
    });
  }

  static async recordStorageProductionOutput(batchId, data) {
    return await this.request(`/api/storage/production-batches/${encodeURIComponent(batchId)}/record-output`, {
      method: 'POST',
      body: JSON.stringify(data)
    });
  }

  /* ------------------------------------------------------------------------
     Laboratory Department Endpoints (/api/laboratory)
     ------------------------------------------------------------------------ */
  static async getLaboratoryStats() {
    return await this.request('/api/laboratory/stats', { method: 'GET' });
  }

  static async getLaboratoryReferences(params = {}) {
    const qs = new URLSearchParams();
    if (params.section && params.section !== 'ALL') qs.append('section', params.section);
    if (params.decision && params.decision !== 'ALL') qs.append('decision', params.decision);
    if (params.status && params.status !== 'ALL') qs.append('status', params.status);
    if (params.search) qs.append('search', params.search);
    const queryStr = qs.toString() ? `?${qs.toString()}` : '';
    return await this.request(`/api/laboratory/references${queryStr}`, { method: 'GET' });
  }

  static async getLaboratoryReferencesForStorageImport() {
    return await this.request('/api/laboratory/references/for-storage-import', { method: 'GET' });
  }

  static async getLaboratoryReferencesForProduction() {
    return await this.request('/api/laboratory/references/for-production', { method: 'GET' });
  }

  static async getLaboratoryReferencesForInProcess(section) {
    const qs = (section && section !== 'ALL') ? `?section=${encodeURIComponent(section)}` : '';
    return await this.request(`/api/laboratory/references/for-in-process${qs}`, { method: 'GET' });
  }

  static async getLaboratoryReferencesForFinalCertificate(section) {
    const qs = (section && section !== 'ALL') ? `?section=${encodeURIComponent(section)}` : '';
    return await this.request(`/api/laboratory/references/for-final-certificate${qs}`, { method: 'GET' });
  }

  static async getLaboratoryTraceability(referenceNumber) {
    return await this.request(`/api/laboratory/traceability/${encodeURIComponent(referenceNumber)}`, { method: 'GET' });
  }

  static async getLaboratoryNextSampleNumber() {
    return await this.request('/api/laboratory/next-sample-number', { method: 'GET' });
  }

  static async getLaboratoryBatchReferences(params = {}) {
    const qs = new URLSearchParams();
    if (params.search) qs.append('search', params.search);
    const queryStr = qs.toString() ? `?${qs.toString()}` : '';
    return await this.request(`/api/laboratory/batch-references${queryStr}`, { method: 'GET' });
  }

  static async getLaboratoryRawMaterialReports(params = {}) {
    const qs = new URLSearchParams();
    if (params.section && params.section !== 'ALL') qs.append('section', params.section);
    if (params.decision && params.decision !== 'ALL') qs.append('decision', params.decision);
    if (params.status && params.status !== 'ALL') qs.append('status', params.status);
    if (params.search) qs.append('search', params.search);
    if (params.fromDate) qs.append('fromDate', params.fromDate);
    if (params.toDate) qs.append('toDate', params.toDate);
    const queryStr = qs.toString() ? `?${qs.toString()}` : '';
    return await this.request(`/api/laboratory/raw-material-reports${queryStr}`, { method: 'GET' });
  }

  static async submitLaboratoryRawMaterialReport(formData) {
    return await this.request('/api/laboratory/raw-material-reports', {
      method: 'POST',
      body: formData instanceof FormData ? formData : JSON.stringify(formData)
    });
  }

  static async getLaboratoryFinalCertificates(params = {}) {
    const qs = new URLSearchParams();
    if (params.section && params.section !== 'ALL') qs.append('section', params.section);
    if (params.decision && params.decision !== 'ALL') qs.append('decision', params.decision);
    if (params.status && params.status !== 'ALL') qs.append('status', params.status);
    if (params.search) qs.append('search', params.search);
    const queryStr = qs.toString() ? `?${qs.toString()}` : '';
    return await this.request(`/api/laboratory/final-certificates${queryStr}`, { method: 'GET' });
  }

  static async submitLaboratoryFinalCertificate(formData) {
    return await this.request('/api/laboratory/final-certificates', {
      method: 'POST',
      body: formData instanceof FormData ? formData : JSON.stringify(formData)
    });
  }

  static async getLaboratoryInProcessReports(params = {}) {
    const qs = new URLSearchParams();
    if (params.section && params.section !== 'ALL') qs.append('section', params.section);
    if (params.referenceNumber) qs.append('referenceNumber', params.referenceNumber);
    if (params.stage && params.stage !== 'ALL') qs.append('stage', params.stage);
    if (params.decision && params.decision !== 'ALL') qs.append('decision', params.decision);
    if (params.search) qs.append('search', params.search);
    const queryStr = qs.toString() ? `?${qs.toString()}` : '';
    return await this.request(`/api/laboratory/in-process-reports${queryStr}`, { method: 'GET' });
  }

  static async submitLaboratoryInProcessReport(formData) {
    return await this.request('/api/laboratory/in-process-reports', {
      method: 'POST',
      body: formData instanceof FormData ? formData : JSON.stringify(formData)
    });
  }

  static async verifyLaboratoryAccess() {
    return await this.request('/api/department/laboratory', { method: 'GET' });
  }

  /* ------------------------------------------------------------------------
     Production Department Endpoints (/api/production & storage integration)
     ------------------------------------------------------------------------ */
  static async verifyProductionAccess() {
    return await this.request('/api/departments/production', { method: 'GET' });
  }

  static async getProductionStats() {
    return await this.request('/api/production/stats', { method: 'GET' });
  }

  static async getProductionInventory(params = {}) {
    const qs = new URLSearchParams();
    if (params.locationId) qs.append('locationId', params.locationId);
    if (params.materialType && params.materialType !== 'all') qs.append('materialType', params.materialType);
    if (params.search) qs.append('search', params.search);
    if (params.onlyAvailable) qs.append('onlyAvailable', 'true');
    const queryStr = qs.toString() ? `?${qs.toString()}` : '';
    return await this.request(`/api/production/inventory${queryStr}`, { method: 'GET' });
  }

  static async getEligibleRawMaterials(locationId = null) {
    const qs = locationId ? `?locationId=${encodeURIComponent(locationId)}` : '';
    return await this.request(`/api/production/eligible-raw-materials${qs}`, { method: 'GET' });
  }

  static async getProductionEligibleReferences() {
    return await this.request('/api/production/eligible-references', { method: 'GET' });
  }

  static async getProductionOrders(params = {}) {
    const qs = new URLSearchParams();
    if (params.status && params.status !== 'ALL') qs.append('status', params.status);
    if (params.referenceNumber) qs.append('referenceNumber', params.referenceNumber);
    if (params.search) qs.append('search', params.search);
    const queryStr = qs.toString() ? `?${qs.toString()}` : '';
    return await this.request(`/api/production/orders${queryStr}`, { method: 'GET' });
  }

  static async getProductionOrder(id) {
    return await this.request(`/api/production/orders/${encodeURIComponent(id)}`, { method: 'GET' });
  }

  static async createProductionOrder(orderData) {
    return await this.request('/api/production/orders', {
      method: 'POST',
      body: JSON.stringify(orderData)
    });
  }

  static async getProductionBatches(params = {}) {
    const qs = new URLSearchParams();
    if (params.status && params.status !== 'ALL') qs.append('status', params.status);
    if (params.orderId) qs.append('orderId', params.orderId);
    if (params.referenceNumber) qs.append('referenceNumber', params.referenceNumber);
    const queryStr = qs.toString() ? `?${qs.toString()}` : '';
    return await this.request(`/api/production/batches${queryStr}`, { method: 'GET' });
  }

  static async getProductionBatch(id) {
    return await this.request(`/api/production/batches/${encodeURIComponent(id)}`, { method: 'GET' });
  }

  static async startProductionBatch(batchData) {
    return await this.request('/api/production/batches', {
      method: 'POST',
      body: JSON.stringify(batchData)
    });
  }

  static async advanceBatchStage(batchId, stageData) {
    return await this.request(`/api/production/batches/${encodeURIComponent(batchId)}/advance-stage`, {
      method: 'POST',
      body: JSON.stringify(stageData)
    });
  }

  static async releaseBatchHold(batchId, notesData) {
    return await this.request(`/api/production/batches/${encodeURIComponent(batchId)}/release-hold`, {
      method: 'POST',
      body: JSON.stringify(notesData)
    });
  }

  static async createProductionOutput(batchId, outputData) {
    return await this.request(`/api/production/batches/${encodeURIComponent(batchId)}/outputs`, {
      method: 'POST',
      body: JSON.stringify(outputData)
    });
  }

  static async getProductionOutputs(params = {}) {
    const qs = new URLSearchParams();
    if (params.classification && params.classification !== 'ALL') qs.append('classification', params.classification);
    if (params.referenceNumber) qs.append('referenceNumber', params.referenceNumber);
    if (params.batchId) qs.append('batchId', params.batchId);
    const queryStr = qs.toString() ? `?${qs.toString()}` : '';
    return await this.request(`/api/production/outputs${queryStr}`, { method: 'GET' });
  }

  static async getProductionReworkOrders(params = {}) {
    const qs = new URLSearchParams();
    if (params.referenceNumber) qs.append('referenceNumber', params.referenceNumber);
    const queryStr = qs.toString() ? `?${qs.toString()}` : '';
    return await this.request(`/api/production/rework-orders${queryStr}`, { method: 'GET' });
  }

  static async createProductionReworkOrder(reworkData) {
    return await this.request('/api/production/rework-orders', {
      method: 'POST',
      body: JSON.stringify(reworkData)
    });
  }

  static async getOutputsForStorageReceiving() {
    return await this.request('/api/production/for-storage-receiving', { method: 'GET' });
  }

  static async receiveOutputInStorage(receiveData) {
    return await this.request('/api/production/receive-output-in-storage', {
      method: 'POST',
      body: JSON.stringify(receiveData)
    });
  }

  static async getProductionHistory(params = {}) {
    const qs = new URLSearchParams();
    if (params.referenceNumber) qs.append('referenceNumber', params.referenceNumber);
    if (params.search) qs.append('search', params.search);
    const queryStr = qs.toString() ? `?${qs.toString()}` : '';
    return await this.request(`/api/production/history${queryStr}`, { method: 'GET' });
  }

  static async getProductionTraceability(referenceNumber) {
    return await this.request(`/api/production/traceability/${encodeURIComponent(referenceNumber)}`, { method: 'GET' });
  }

  static async getPendingProductionRequests() {
    return await this.request('/api/storage/production-requests', { method: 'GET' });
  }

  static async issueMaterialsForProduction(orderId, payload = {}) {
    return await this.request(`/api/storage/production-requests/${encodeURIComponent(orderId)}/issue`, {
      method: 'POST',
      body: JSON.stringify(payload)
    });
  }

  static async rejectProductionRequest(orderId, payload = {}) {
    return await this.request(`/api/storage/production-requests/${encodeURIComponent(orderId)}/reject`, {
      method: 'POST',
      body: JSON.stringify(payload)
    });
  }

  // =========================================================================
  // DAILY PRODUCTION TRACKING SYSTEM APIS (تسجيل الإنتاج اليومي)
  // =========================================================================

  static async getCurrentShiftInfo() {
    return await this.request('/api/production/current-shift-info', { method: 'GET' });
  }

  static async getProductionCatalog() {
    return await this.request('/api/production/products', { method: 'GET' });
  }

  static async createDailyProductionSubmission(submissionData) {
    return await this.request('/api/production/daily-submissions', {
      method: 'POST',
      body: JSON.stringify(submissionData)
    });
  }

  static async getDailyProductionSubmissions(params = {}) {
    const qs = new URLSearchParams();
    if (params.date) qs.set('date', params.date);
    if (params.startDate) qs.set('startDate', params.startDate);
    if (params.endDate) qs.set('endDate', params.endDate);
    if (params.shift) qs.set('shift', params.shift);
    if (params.employee) qs.set('employee', params.employee);
    if (params.product) qs.set('product', params.product);
    const queryStr = qs.toString() ? `?${qs.toString()}` : '';
    return await this.request(`/api/production/daily-submissions${queryStr}`, { method: 'GET' });
  }

  static async getDailyProductionSubmission(id) {
    return await this.request(`/api/production/daily-submissions/${encodeURIComponent(id)}`, { method: 'GET' });
  }

  static async updateDailyProductionSubmission(id, submissionData) {
    return await this.request(`/api/production/daily-submissions/${encodeURIComponent(id)}`, {
      method: 'PUT',
      body: JSON.stringify(submissionData)
    });
  }

  static async getDailyProductionSummaryStats(params = {}) {
    const qs = new URLSearchParams();
    if (params.date) qs.set('date', params.date);
    if (params.shift) qs.set('shift', params.shift);
    const queryStr = qs.toString() ? `?${qs.toString()}` : '';
    return await this.request(`/api/production/daily-submissions/summary-stats${queryStr}`, { method: 'GET' });
  }
}

window.ChatApi = ChatApi;
