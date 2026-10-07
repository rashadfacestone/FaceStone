/* ==========================================================================
   FACESTONE REAL-TIME CHAT — SIGNALR WEBSOCKET MANAGER
   Handles Live WebSocket Stream, Idempotent Events, & Safe Fallback
   ========================================================================== */

class RealTimeChatService {
  constructor() {
    this.hubConnection = null;
    this.pollingTimer = null;
    this.retryTimer = null;
    this.connectionState = 'disconnected'; // 'connected', 'connecting', 'reconnecting', 'disconnected'
    this.lastReceivedTime = null;
    this.audioContext = null;
    this.isExplicitlyStopped = false;
    this.isStarting = false;
    this.retryCount = 0;
    this.currentUser = null;
    this.processedMessageIds = new Set();
  }

  async start(currentUser) {
    if (!currentUser) return;
    this.currentUser = currentUser;
    this.isExplicitlyStopped = false;

    // Prevent concurrent startup invocations
    if (this.isStarting) {
      return;
    }
    this.isStarting = true;

    // Clean up any pending retry timer
    if (this.retryTimer) {
      clearTimeout(this.retryTimer);
      this.retryTimer = null;
    }

    const token = ChatApi.getToken();
    if (!token || ChatApi.isHandlingSessionExpiry) {
      this.isStarting = false;
      this.setConnectionState('disconnected');
      return;
    }

    const isAccountant = currentUser.role === 'Accountant' || currentUser.role === 'CEO';

    if (typeof signalR === 'undefined') {
      console.warn('SignalR library not available. Falling back to polling.');
      this.isStarting = false;
      this.setConnectionState('disconnected');
      this.startPolling(isAccountant, currentUser);
      return;
    }

    // If an existing connection exists and is connected/connecting, do not recreate
    if (this.hubConnection) {
      if (this.hubConnection.state === signalR.HubConnectionState.Connected) {
        this.isStarting = false;
        this.setConnectionState('connected');
        return;
      }
      if (this.hubConnection.state === signalR.HubConnectionState.Connecting ||
          this.hubConnection.state === signalR.HubConnectionState.Reconnecting) {
        this.isStarting = false;
        return;
      }
      // Stop and clean up prior dead connection
      try {
        await this.hubConnection.stop();
      } catch {}
      this.hubConnection = null;
    }

    try {
      this.setConnectionState('connecting');

      // Use accessTokenFactory to prevent exposing JWT tokens in URLs and logs
      this.hubConnection = new signalR.HubConnectionBuilder()
        .withUrl('/chatHub', {
          accessTokenFactory: () => ChatApi.getToken()
        })
        .withAutomaticReconnect({
          nextRetryDelayInMilliseconds: retryContext => {
            // Controlled exponential backoff: 2s, 4s, 8s, 16s, capped at 30s
            const delay = Math.min(2000 * Math.pow(2, retryContext.previousAttempts), 30000);
            return delay;
          }
        })
        .configureLogging(signalR.LogLevel.None) // Suppress noisy internal error logs in console
        .build();

      // Register handlers ONCE
      this.hubConnection.on('OnNewMessage', (msg) => {
        this.handleIncomingMessage(msg, this.currentUser);
      });

      this.hubConnection.on('OnAccountantReply', (msg) => {
        this.handleIncomingMessage(msg, this.currentUser);
      });

      this.hubConnection.on('OnNewEmployeeMessage', (msg) => {
        this.handleIncomingMessage(msg, this.currentUser);
      });

      this.hubConnection.on('OnLiveLocationStarted', (msg) => {
        console.log('[LiveLocation] BROADCAST', {
          eventType: 'OnLiveLocationStarted',
          sessionId: msg?.liveSessionId,
          conversationId: msg?.conversationId
        });
        console.log('[LiveLocation] RECEIVED', {
          messageType: 'LIVE_LOCATION',
          id: msg?.id,
          senderId: msg?.senderId,
          sessionId: msg?.liveSessionId,
          conversationId: msg?.conversationId,
          latitude: msg?.latitude,
          longitude: msg?.longitude,
          status: msg?.status || 'ACTIVE'
        });
        this.handleIncomingMessage(msg, this.currentUser);
      });

      this.hubConnection.on('OnLiveLocationUpdate', (data) => {
        console.log('[LiveLocation] UPDATE_RECEIVED', {
          sessionId: data?.sessionId,
          latitude: data?.latitude,
          longitude: data?.longitude,
          status: data?.status
        });
        window.dispatchEvent(new CustomEvent('realtime:live-location:updated', { detail: data }));
      });

      this.hubConnection.on('OnLiveLocationEnded', (data) => {
        console.log('[LiveLocation] ENDED_RECEIVED', {
          sessionId: data?.sessionId,
          endedAt: data?.endedAt,
          status: data?.status
        });
        window.dispatchEvent(new CustomEvent('realtime:live-location:ended', { detail: data }));
      });

      this.hubConnection.on('OnDepartmentDataChanged', (data) => {
        console.log('[Management/RealTime] Department data changed via SignalR:', data);
        window.dispatchEvent(new CustomEvent('facestone:data-changed', { detail: data }));
        if (data?.department === 'control-monitoring') {
          window.dispatchEvent(new CustomEvent('facestone:realtime:control-monitoring', { detail: data }));
        }
      });

      this.hubConnection.on('OnFormSubmittedForReview', (data) => {
        console.log('[ControlMonitoring/RealTime] Form submitted for review:', data);
        window.dispatchEvent(new CustomEvent('facestone:realtime:control-monitoring', { detail: data }));
        window.dispatchEvent(new CustomEvent('facestone:data-changed', { detail: data }));
      });

      this.hubConnection.on('OnFormApprovalStatusChanged', (data) => {
        console.log('[ControlMonitoring/RealTime] Form approval status changed:', data);
        window.dispatchEvent(new CustomEvent('facestone:realtime:control-monitoring', { detail: data }));
        window.dispatchEvent(new CustomEvent('facestone:data-changed', { detail: data }));
      });

      // Lifecycle hooks
      this.hubConnection.onreconnecting(() => {
        if (ChatApi.isHandlingSessionExpiry || !ChatApi.getToken()) {
          this.stop();
          return;
        }
        this.setConnectionState('reconnecting');
        this.stopPolling(); // Keep polling off while SignalR is handling reconnection
      });

      this.hubConnection.onreconnected(() => {
        this.setConnectionState('connected');
        this.retryCount = 0;
        this.stopPolling();
      });

      this.hubConnection.onclose((err) => {
        if (this.isExplicitlyStopped || ChatApi.isHandlingSessionExpiry || !ChatApi.getToken()) {
          this.setConnectionState('disconnected');
          return;
        }
        // Check if error was 401 unauthorized
        const errMsg = String(err?.message || '');
        if (errMsg.includes('401') || errMsg.includes('Unauthorized')) {
          this.stop();
          ChatApi.handleSessionExpired();
          return;
        }
        this.setConnectionState('disconnected');
        // Start fallback polling and schedule controlled reconnection
        this.startPolling(isAccountant, this.currentUser);
        this.scheduleControlledReconnect();
      });

      await this.hubConnection.start();
      this.setConnectionState('connected');
      this.retryCount = 0;
      this.stopPolling();

    } catch (err) {
      if (this.isExplicitlyStopped) {
        this.isStarting = false;
        return;
      }

      const errMsg = String(err?.message || '');
      const isAuthErr = errMsg.includes('401') || errMsg.includes('Unauthorized') || err?.statusCode === 401;

      if (isAuthErr || ChatApi.isHandlingSessionExpiry || !ChatApi.getToken()) {
        this.isStarting = false;
        this.stop();
        ChatApi.handleSessionExpired();
        return;
      }

      this.setConnectionState('disconnected');
      this.startPolling(isAccountant, currentUser);
      this.scheduleControlledReconnect();
    } finally {
      this.isStarting = false;
    }
  }

  scheduleControlledReconnect() {
    if (this.isExplicitlyStopped || this.retryTimer || ChatApi.isHandlingSessionExpiry || !ChatApi.getToken()) return;

    // Controlled exponential backoff: 2s, 4s, 8s, 16s, up to 30s max
    const delay = Math.min(2000 * Math.pow(2, this.retryCount), 30000);
    this.retryCount++;

    this.retryTimer = setTimeout(() => {
      this.retryTimer = null;
      if (!this.isExplicitlyStopped && !ChatApi.isHandlingSessionExpiry && ChatApi.getToken() && this.currentUser) {
        this.start(this.currentUser);
      }
    }, delay);
  }

  handleIncomingMessage(msg, currentUser) {
    if (!msg || !msg.id) return;

    // Network layer deduplication check
    if (this.processedMessageIds.has(msg.id)) {
      return;
    }
    if (msg.clientMessageId && this.processedMessageIds.has(msg.clientMessageId)) {
      return;
    }

    this.processedMessageIds.add(msg.id);
    if (msg.clientMessageId) {
      this.processedMessageIds.add(msg.clientMessageId);
    }

    // Keep set bounded to avoid memory growth
    if (this.processedMessageIds.size > 1000) {
      const iter = this.processedMessageIds.values();
      for (let i = 0; i < 200; i++) {
        this.processedMessageIds.delete(iter.next().value);
      }
    }

    // Defensive message type and content normalization
    const rawType = (msg.messageType || '').toUpperCase();
    const isLiveLocation = rawType === 'LIVE_LOCATION' || rawType === 'LIVELOCATION' || Boolean(msg.liveSessionId);
    const isStaticLocation = rawType === 'LOCATION' || (!isLiveLocation && msg.latitude != null && msg.longitude != null && !msg.attachment && (!msg.messageText || msg.messageText === 'Shared Location'));

    if (isLiveLocation) {
      msg.messageType = 'LIVE_LOCATION';
      msg.messageText = ''; // CRITICAL: Never treat Live Location as text payload
      if (!msg.status) {
        msg.status = (msg.isLiveActive === false || msg.liveEndedAt) ? 'ENDED' : 'ACTIVE';
      }
    } else if (isStaticLocation) {
      msg.messageType = 'LOCATION';
      msg.messageText = '';
    }

    this.lastReceivedTime = msg.sentAt || new Date().toISOString();

    // Play chime if message received from another user
    if (currentUser && msg.senderId && msg.senderId !== currentUser.id) {
      this.playChime();
    }

    window.dispatchEvent(new CustomEvent('realtime:message:received', { detail: msg }));
  }

  startPolling(isAccountant, currentUser) {
    this.stopPolling();

    if (this.isExplicitlyStopped || ChatApi.isHandlingSessionExpiry || !ChatApi.getToken()) {
      return;
    }

    // Polling only runs as fallback when SignalR is NOT connected or reconnecting
    this.pollingTimer = setInterval(async () => {
      if (ChatApi.isHandlingSessionExpiry || !ChatApi.getToken() || this.isExplicitlyStopped) {
        this.stopPolling();
        return;
      }

      // If SignalR is connected or actively reconnecting, terminate polling
      if (this.hubConnection && 
         (this.hubConnection.state === signalR.HubConnectionState.Connected ||
          this.hubConnection.state === signalR.HubConnectionState.Reconnecting)) {
        this.stopPolling();
        return;
      }

      const activeEmployeeId = window.ChatAppActiveEmployeeId || null;
      const res = await ChatApi.pollChat(activeEmployeeId, this.lastReceivedTime);

      if (res && res.success && res.data) {
        // Only mark connected if SignalR is not active and polling succeeded
        if (this.connectionState !== 'connected') {
          this.setConnectionState('connected');
        }
        const list = isAccountant
          ? (res.data.activeMessages || res.data.newBroadcasts || [])
          : (res.data.newMessages || []);

        list.forEach(msg => {
          this.handleIncomingMessage(msg, currentUser);
        });
      } else if (res && res.status === 401) {
        this.stop();
        ChatApi.handleSessionExpired();
        return;
      } else {
        // Server unreachable or network error
        if (this.connectionState !== 'reconnecting') {
          this.setConnectionState('disconnected');
        }
      }
    }, 5000);
  }

  stopPolling() {
    if (this.pollingTimer) {
      clearInterval(this.pollingTimer);
      this.pollingTimer = null;
    }
  }

  setConnectionState(state) {
    if (this.connectionState === state) return;
    this.connectionState = state;
    window.dispatchEvent(new CustomEvent('realtime:status', { detail: { state } }));
  }

  stop() {
    this.isExplicitlyStopped = true;
    this.isStarting = false;
    this.stopPolling();

    if (this.retryTimer) {
      clearTimeout(this.retryTimer);
      this.retryTimer = null;
    }

    if (this.hubConnection) {
      try {
        this.hubConnection.off('OnNewMessage');
        this.hubConnection.off('OnAccountantReply');
        this.hubConnection.off('OnNewEmployeeMessage');
        this.hubConnection.off('OnLiveLocationStarted');
        this.hubConnection.off('OnLiveLocationUpdate');
        this.hubConnection.off('OnLiveLocationEnded');
        this.hubConnection.stop();
      } catch {}
      this.hubConnection = null;
    }
    this.setConnectionState('disconnected');
  }

  playChime() {
    try {
      if (!this.audioContext) {
        this.audioContext = new (window.AudioContext || window.webkitAudioContext)();
      }
      if (this.audioContext.state === 'suspended') {
        this.audioContext.resume();
      }
      const osc = this.audioContext.createOscillator();
      const gain = this.audioContext.createGain();
      osc.type = 'sine';
      osc.frequency.setValueAtTime(587.33, this.audioContext.currentTime); // D5
      osc.frequency.exponentialRampToValueAtTime(880, this.audioContext.currentTime + 0.12); // A5
      gain.gain.setValueAtTime(0.08, this.audioContext.currentTime);
      gain.gain.exponentialRampToValueAtTime(0.001, this.audioContext.currentTime + 0.3);
      osc.connect(gain);
      gain.connect(this.audioContext.destination);
      osc.start();
      osc.stop(this.audioContext.currentTime + 0.3);
    } catch {}
  }

  async broadcastDepartmentChange(department, action) {
    try {
      if (this.hubConnection && typeof signalR !== 'undefined' && this.hubConnection.state === signalR.HubConnectionState.Connected) {
        await this.hubConnection.invoke('NotifyDepartmentChange', department, action || 'Updated');
      }
    } catch (e) {
      console.warn('Could not broadcast department change via SignalR:', e);
    }
    window.dispatchEvent(new CustomEvent('facestone:data-changed', {
      detail: { department, action: action || 'Updated', timestamp: new Date().toISOString() }
    }));
  }
}

window.realTimeChat = new RealTimeChatService();
