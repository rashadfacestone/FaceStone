/* ==========================================================================
   FACESTONE REAL-TIME CHAT — MAIN APPLICATION CONTROLLER
   Bright, Fast, Minimalist Messaging Engine Inspired by WhatsApp / Messenger
   ========================================================================== */

document.addEventListener('DOMContentLoaded', () => {
  ChatApp.init();
});

class ChatApp {
  static init() {
    this.bindDomElements();
    this.bindStateEvents();
    this.bindRealtimeEvents();

    if (ChatApi.isAuthenticated()) {
      const user = ChatApi.getCurrentUser();
      if (user) {
        this.onAuthenticated(user);
      } else {
        this.fetchProfileAndStart();
      }
    } else {
      this.showAuthModal(true);
    }
  }

  static bindDomElements() {
    // Auth Modal
    this.authModal = document.getElementById('auth-modal-backdrop');
    this.authLoginForm = document.getElementById('auth-login-form');
    this.authRegisterForm = document.getElementById('auth-register-form');
    this.tabLoginBtn = document.getElementById('auth-tab-login');
    this.tabRegisterBtn = document.getElementById('auth-tab-register');
    this.authLoginView = document.getElementById('auth-view-login');
    this.authRegisterView = document.getElementById('auth-view-register');

    // Sidebar
    this.conversationsListEl = document.getElementById('conversations-list-container');
    this.searchInput = document.getElementById('search-conversations-input');
    this.searchClearBtn = document.getElementById('search-clear-btn');
    this.currentUserNameEl = document.getElementById('current-user-name');
    this.currentUserRoleEl = document.getElementById('current-user-role');
    this.currentUserAvatarEl = document.getElementById('current-user-avatar');
    this.logoutBtn = document.getElementById('user-logout-btn');

    // Chat Pane
    this.chatAppRoot = document.getElementById('chat-app-root');
    this.chatEmptyView = document.getElementById('chat-empty-view');
    this.chatActiveView = document.getElementById('chat-active-view');
    this.mobileBackBtn = document.getElementById('mobile-back-btn');
    this.chatRecipientAvatar = document.getElementById('chat-recipient-avatar');
    this.chatRecipientName = document.getElementById('chat-recipient-name');
    this.chatRecipientStatus = document.getElementById('chat-recipient-status');
    this.connectionPill = document.getElementById('connection-status-pill');

    // Messages Stream & Input
    this.messagesStreamBox = document.getElementById('messages-stream-box');
    this.msgTextarea = document.getElementById('chat-msg-textarea');
    this.sendBtn = document.getElementById('send-msg-btn');
    this.attachBtn = document.getElementById('chat-attach-btn');
    this.fileInput = document.getElementById('chat-file-input');
    this.filePreviewStrip = document.getElementById('file-preview-strip');
    this.fileNamePreview = document.getElementById('file-name-preview');
    this.fileRemoveBtn = document.getElementById('file-remove-btn');
    this.jumpBottomBtn = document.getElementById('jump-bottom-btn');

    // Lightbox Modal
    this.lightboxBackdrop = document.getElementById('chat-lightbox-backdrop');
    this.lightboxImg = document.getElementById('lightbox-img-el');
    this.lightboxFilename = document.getElementById('lightbox-filename');
    this.lightboxCloseBtn = document.getElementById('lightbox-close-btn');
    this.lightboxDownloadBtn = document.getElementById('lightbox-download-btn');

    this.selectedFile = null;

    // Attach Event Listeners
    this.bindUiActions();
  }

  static bindUiActions() {
    // Auth Tabs
    this.tabLoginBtn?.addEventListener('click', () => this.switchAuthTab('login'));
    this.tabRegisterBtn?.addEventListener('click', () => this.switchAuthTab('register'));

    // Login Form Submit
    this.authLoginForm?.addEventListener('submit', async (e) => {
      e.preventDefault();
      const id = document.getElementById('login-id-input')?.value.trim();
      const pwd = document.getElementById('login-pwd-input')?.value;
      const submitBtn = this.authLoginForm.querySelector('button[type="submit"]');

      if (!id || !pwd) {
        this.showToast('Please enter both identifier and password.', 'error');
        return;
      }

      this.setBtnLoading(submitBtn, true);
      const res = await ChatApi.login(id, pwd);
      this.setBtnLoading(submitBtn, false);

      if (res.success && res.data) {
        this.showToast(`Welcome, ${res.data.user.fullName}!`);
        this.showAuthModal(false);
        this.onAuthenticated(res.data.user);
      } else {
        this.showToast(res.message || 'Login failed. Verify credentials.', 'error');
      }
    });

    // Register Form Submit
    this.authRegisterForm?.addEventListener('submit', async (e) => {
      e.preventDefault();
      const fullName = document.getElementById('reg-name-input')?.value.trim();
      const username = document.getElementById('reg-user-input')?.value.trim();
      const email = document.getElementById('reg-email-input')?.value.trim();
      const phone = document.getElementById('reg-phone-input')?.value.trim();
      const pwd = document.getElementById('reg-pwd-input')?.value;
      const submitBtn = this.authRegisterForm.querySelector('button[type="submit"]');

      if (!fullName || !username || !email || !phone || !pwd) {
        this.showToast('All registration fields are required.', 'error');
        return;
      }

      this.setBtnLoading(submitBtn, true);
      const res = await ChatApi.register({
        fullName,
        username,
        email,
        phoneNumber: phone,
        countryCode: '+20',
        password: pwd,
        role: 'Employee'
      });
      this.setBtnLoading(submitBtn, false);

      if (res.success && res.data) {
        this.showToast(`Account created! Welcome, ${res.data.user.fullName}`);
        this.showAuthModal(false);
        this.onAuthenticated(res.data.user);
      } else {
        this.showToast(res.message || 'Registration failed.', 'error');
      }
    });

    // Logout
    this.logoutBtn?.addEventListener('click', () => {
      ChatApi.clearSession();
      realTimeChat.stop();
      this.showToast('Signed out successfully.');
      this.showAuthModal(true);
    });

    // Search Box
    this.searchInput?.addEventListener('input', () => {
      const val = this.searchInput.value;
      chatState.searchQuery = val;
      if (this.searchClearBtn) {
        this.searchClearBtn.classList.toggle('active', !!val);
      }
      this.renderConversationsList();
    });

    this.searchClearBtn?.addEventListener('click', () => {
      this.searchInput.value = '';
      chatState.searchQuery = '';
      this.searchClearBtn.classList.remove('active');
      this.renderConversationsList();
    });

    // Mobile Back Button
    this.mobileBackBtn?.addEventListener('click', () => {
      this.chatAppRoot.classList.remove('mobile-chat-open');
    });

    // Message Input (Enter to send, Shift+Enter for newline)
    this.msgTextarea?.addEventListener('keydown', (e) => {
      if (e.key === 'Enter' && !e.shiftKey) {
        e.preventDefault();
        this.sendCurrentMessage();
      }
    });

    // Auto-grow textarea
    this.msgTextarea?.addEventListener('input', () => {
      this.msgTextarea.style.height = 'auto';
      this.msgTextarea.style.height = Math.min(this.msgTextarea.scrollHeight, 120) + 'px';
    });

    // Send Button
    this.sendBtn?.addEventListener('click', () => {
      this.sendCurrentMessage();
    });

    // Attachment Button
    this.attachBtn?.addEventListener('click', () => {
      this.fileInput?.click();
    });

    this.fileInput?.addEventListener('change', () => {
      if (this.fileInput.files.length > 0) {
        this.selectedFile = this.fileInput.files[0];
        if (this.fileNamePreview) this.fileNamePreview.textContent = this.selectedFile.name;
        this.filePreviewStrip?.classList.add('active');
      }
    });

    this.fileRemoveBtn?.addEventListener('click', () => {
      this.clearSelectedFile();
    });

    // Scroll and Jump to bottom
    this.messagesStreamBox?.addEventListener('scroll', () => {
      const threshold = 120;
      const atBottom = (this.messagesStreamBox.scrollHeight - this.messagesStreamBox.scrollTop - this.messagesStreamBox.clientHeight) < threshold;
      if (atBottom && this.jumpBottomBtn) {
        this.jumpBottomBtn.classList.remove('visible');
      }
    });

    this.jumpBottomBtn?.addEventListener('click', () => {
      this.scrollToBottom(true);
      this.jumpBottomBtn.classList.remove('visible');
    });

    // Lightbox Modal Actions
    this.lightboxCloseBtn?.addEventListener('click', () => this.closeLightbox());
    this.lightboxBackdrop?.addEventListener('click', (e) => {
      if (e.target === this.lightboxBackdrop || e.target.id === 'lightbox-image-wrap') {
        this.closeLightbox();
      }
    });
    document.addEventListener('keydown', (e) => {
      if (e.key === 'Escape' && this.lightboxBackdrop?.style.display !== 'none') {
        this.closeLightbox();
      }
    });
    this.lightboxDownloadBtn?.addEventListener('click', () => {
      const id = this.lightboxDownloadBtn.dataset.attachmentId;
      const name = this.lightboxDownloadBtn.dataset.fileName;
      if (id) {
        this.downloadAttachment(id, name);
      }
    });

    // Session Expired
    window.addEventListener('chat:session:expired', () => {
      realTimeChat.stop();
      this.showAuthModal(true);
      this.showToast('Your session expired. Please sign in again.', 'error');
    });
  }

  static bindStateEvents() {
    window.addEventListener('chat:state:conversations:updated', () => {
      this.renderConversationsList();
    });

    window.addEventListener('chat:state:activeConversation:changed', (e) => {
      this.renderActiveConversationHeader(e.detail);
    });

    window.addEventListener('chat:state:messages:updated', (e) => {
      this.renderMessagesStream(e.detail);
      this.scrollToBottom(false);
    });

    window.addEventListener('chat:state:messages:added', (e) => {
      this.appendMessageBubble(e.detail.message, e.detail.isOptimistic);
      this.handleAutoScrollOnNewMessage();
    });

    window.addEventListener('chat:state:messages:reconciled', (e) => {
      this.updateOptimisticBubble(e.detail.tempId, e.detail.confirmedMsg);
    });

    window.addEventListener('chat:state:messages:failed', (e) => {
      this.markBubbleFailed(e.detail.tempId);
    });
  }

  static bindRealtimeEvents() {
    window.addEventListener('realtime:status', (e) => {
      this.updateConnectionPill(e.detail.state);
    });

    window.addEventListener('realtime:message:received', (e) => {
      const msg = e.detail;
      const activeConv = chatState.activeConversation;
      const isAccountant = chatState.isAccountantUser();

      // Check if this message belongs to the current thread
      let belongsToCurrent = false;
      if (activeConv) {
        if (isAccountant) {
          const activeEmpId = activeConv.employeeId || activeConv.id;
          belongsToCurrent = (msg.senderId === activeEmpId || msg.recipientId === activeEmpId);
        } else {
          belongsToCurrent = true; // Employee thread is with Desk
        }
      }

      if (belongsToCurrent) {
        chatState.addMessage(msg);
      }

      // Update snippet in conversation list
      const partnerId = isAccountant ? (msg.senderRole === 'Accountant' ? msg.recipientId : msg.senderId) : 'desk';
      if (partnerId && partnerId !== 'desk') {
        const activeEmpId = activeConv ? (activeConv.employeeId || activeConv.id) : null;
        const isCurrentlyViewing = activeEmpId === partnerId;
        chatState.updateConversationSnippet(partnerId, msg.messageText || '[Attachment]', msg.sentAt, !isCurrentlyViewing);
      }
    });
  }

  static async fetchProfileAndStart() {
    const res = await ChatApi.getProfile();
    if (res.success && res.data) {
      this.onAuthenticated(res.data);
    } else {
      this.showAuthModal(true);
    }
  }

  static async onAuthenticated(user) {
    chatState.init(user);

    // Update Header
    if (this.currentUserNameEl) this.currentUserNameEl.textContent = user.fullName;
    if (this.currentUserRoleEl) this.currentUserRoleEl.textContent = user.role;
    if (this.currentUserAvatarEl) {
      const initials = user.fullName.split(' ').map(n => n[0]).join('').substring(0, 2).toUpperCase();
      this.currentUserAvatarEl.textContent = initials || 'FS';
    }

    // Start Real-Time WebSockets
    realTimeChat.start(user);

    // Load Conversations based on Role
    if (chatState.isAccountantUser()) {
      await this.loadAccountantConversations();
    } else {
      await this.loadEmployeeDeskConversation(user);
    }
  }

  /* ------------------------------------------------------------------------
     Sidebar Conversation Loading
     ------------------------------------------------------------------------ */
  static async loadAccountantConversations() {
    this.conversationsListEl.innerHTML = '<div style="padding: 1.5rem; text-align: center; color: var(--text-muted); font-size: 0.8125rem;">Loading client threads...</div>';
    const res = await ChatApi.getConversations();

    if (res.success && res.data?.conversations) {
      chatState.setConversations(res.data.conversations);

      // Auto-open first conversation on desktop
      if (window.innerWidth >= 768 && res.data.conversations.length > 0 && !chatState.activeConversation) {
        this.selectConversation(res.data.conversations[0]);
      }
    } else {
      this.conversationsListEl.innerHTML = '<div style="padding: 1.5rem; text-align: center; color: var(--text-muted); font-size: 0.8125rem;">No active conversations found.</div>';
    }
  }

  static async loadEmployeeDeskConversation(currentUser) {
    const deskConv = {
      id: 'desk',
      employeeId: 'desk',
      fullName: 'FaceStone Operations & Accounting Desk',
      username: 'accounting_team',
      role: 'Operations Desk',
      lastMessageText: 'Official communication channel for engineering orders, inquiries & billing.',
      lastMessageTime: new Date().toISOString(),
      unreadCount: 0,
      isDesk: true
    };

    chatState.setConversations([deskConv]);
    this.selectConversation(deskConv);
  }

  static renderConversationsList() {
    if (!this.conversationsListEl) return;
    const list = chatState.getFilteredConversations();

    if (list.length === 0) {
      this.conversationsListEl.innerHTML = `
        <div style="padding: 2rem 1rem; text-align: center; color: var(--text-muted); font-size: 0.875rem;">
          No conversations found
        </div>
      `;
      return;
    }

    this.conversationsListEl.innerHTML = list.map(c => {
      const isActive = chatState.activeConversation && (chatState.activeConversation.id === c.employeeId || chatState.activeConversation.id === c.id);
      const initials = c.fullName.split(' ').map(n => n[0]).join('').substring(0, 2).toUpperCase();
      const timeStr = c.lastMessageTime ? this.formatTimeSnippet(c.lastMessageTime) : '';
      const isDesk = c.isDesk || c.role === 'Operations Desk';

      return `
        <div class="conversation-item ${isActive ? 'active' : ''}" onclick="ChatApp.onConversationItemClick('${c.employeeId || c.id}')">
          <div class="conv-avatar-wrap">
            <div class="conv-avatar-img ${isDesk ? 'desk-avatar' : ''}">
              ${isDesk ? '🏢' : initials}
            </div>
            <div class="online-status-badge"></div>
          </div>
          <div class="conv-info">
            <div class="conv-row-top">
              <span class="conv-name">${this.escape(c.fullName)}</span>
              <span class="conv-time">${timeStr}</span>
            </div>
            <div class="conv-row-bottom">
              <span class="conv-preview">
                ${c.hasAttachment ? '📎 ' : ''}${this.escape(c.lastMessageText || 'New conversation')}
              </span>
              ${c.unreadCount > 0 ? `<span class="conv-unread-pill">${c.unreadCount}</span>` : ''}
            </div>
          </div>
        </div>
      `;
    }).join('');
  }

  static onConversationItemClick(convId) {
    const list = chatState.conversations;
    const conv = list.find(c => (c.employeeId || c.id) === convId);
    if (conv) {
      this.selectConversation(conv);
    }
  }

  static async selectConversation(conv) {
    if (!conv) return;
    if (!conv.id && conv.employeeId) conv.id = conv.employeeId;
    chatState.setActiveConversation(conv);
    window.ChatAppActiveEmployeeId = conv.isDesk ? null : (conv.employeeId || conv.id);

    // On mobile, trigger slide
    this.chatAppRoot.classList.add('mobile-chat-open');

    // Switch views
    if (this.chatEmptyView) this.chatEmptyView.style.display = 'none';
    if (this.chatActiveView) this.chatActiveView.style.display = 'flex';

    // Highlight in list
    this.renderConversationsList();

    // Fetch conversation messages
    this.messagesStreamBox.innerHTML = '<div style="padding: 2rem; text-align: center; color: var(--text-muted); font-size: 0.8125rem;">Loading messages...</div>';

    if (conv.isDesk) {
      const res = await ChatApi.getChatHistory();
      if (res.success && res.data) {
        chatState.setMessages(res.data);
      }
    } else {
      const empId = conv.employeeId || conv.id;
      const res = await ChatApi.getConversationMessages(empId);
      if (res.success && res.data?.messages) {
        conv.unreadCount = 0;
        this.renderConversationsList();
        chatState.setMessages(res.data.messages);
      }
    }

    this.focusInput();
  }

  static renderActiveConversationHeader(conv) {
    if (!conv) return;
    const initials = conv.fullName.split(' ').map(n => n[0]).join('').substring(0, 2).toUpperCase();
    const isDesk = conv.isDesk || conv.role === 'Operations Desk';

    if (this.chatRecipientAvatar) {
      this.chatRecipientAvatar.className = `conv-avatar-img ${isDesk ? 'desk-avatar' : ''}`;
      this.chatRecipientAvatar.textContent = isDesk ? '🏢' : initials;
    }

    if (this.chatRecipientName) this.chatRecipientName.textContent = conv.fullName;
    if (this.chatRecipientStatus) {
      this.chatRecipientStatus.innerHTML = `
        <span style="width: 7px; height: 7px; border-radius: 50%; background: var(--status-online); display: inline-block;"></span>
        <span>Online • ${conv.role || 'Member'}</span>
      `;
    }
  }

  /* ------------------------------------------------------------------------
     Message Stream Rendering & Optimistic UI
     ------------------------------------------------------------------------ */
  static renderMessagesStream(messages) {
    if (!this.messagesStreamBox) return;

    if (!messages || messages.length === 0) {
      this.messagesStreamBox.innerHTML = `
        <div style="margin: auto; text-align: center; color: var(--text-muted); font-size: 0.875rem;">
          <p>💬 No messages in this thread yet.</p>
          <p style="font-size: 0.75rem; margin-top: 0.25rem;">Type a message below to start chatting.</p>
        </div>
      `;
      return;
    }

    let html = '';
    let lastDateStr = '';

    messages.forEach(m => {
      const dateStr = this.formatDateSeparator(m.sentAt);
      if (dateStr !== lastDateStr) {
        html += `<div class="date-separator">${dateStr}</div>`;
        lastDateStr = dateStr;
      }
      html += this.buildMessageBubbleHtml(m);
    });

    this.messagesStreamBox.innerHTML = html;
  }

  static buildMessageBubbleHtml(msg, isOptimistic = false) {
    const currentUser = chatState.currentUser;
    const isSent = currentUser && (msg.senderId === currentUser.id || msg.isSentByMe);
    const timeStr = msg.sentAt ? new Date(msg.sentAt).toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' }) : '';
    const correlationId = msg.clientMessageId || msg.tempId;
    const bubbleId = correlationId ? `bubble-temp-${correlationId}` : `bubble-${msg.id}`;

    let attachmentHtml = '';
    if (msg.attachment) {
      attachmentHtml = this.buildAttachmentHtml(msg.attachment);
    }

    let statusCheckmark = '';
    if (isSent) {
      if (isOptimistic) {
        statusCheckmark = '<span class="checkmark-icon pending" title="Sending...">🕒</span>';
      } else if (msg.isFailed) {
        const retryKey = correlationId || msg.id;
        statusCheckmark = `<span class="msg-failed-retry" onclick="ChatApp.retryMessage('${retryKey}')">⚠️ Failed • Retry ↻</span>`;
      } else if (msg.isReadByAccountant || msg.isReadByUser) {
        statusCheckmark = '<span class="checkmark-icon read" title="Read">✓✓</span>';
      } else {
        statusCheckmark = '<span class="checkmark-icon sent" title="Sent">✓✓</span>';
      }
    }

    return `
      <div class="msg-bubble-row ${isSent ? 'sent' : 'received'}" id="${bubbleId}">
        <div class="msg-bubble">
          ${!isSent ? `<div class="msg-sender-name">${this.escape(msg.senderName)}</div>` : ''}
          ${attachmentHtml}
          ${msg.messageText ? `<div class="msg-body-text">${this.escape(msg.messageText)}</div>` : ''}
          <div class="msg-meta-row">
            <span>${timeStr}</span>
            ${statusCheckmark}
          </div>
        </div>
      </div>
    `;
  }

  static buildAttachmentHtml(att) {
    if (!att) return '';
    const fileName = att.fileName || 'attachment';
    const escapedFileName = this.escape(fileName);
    const rawFileName = fileName.replace(/'/g, "\\'");
    const attId = att.id || '';
    
    // Determine image type
    const isImg = att.fileType === 'image' || 
      /\.(jpg|jpeg|png|webp|gif|bmp|svg)$/i.test(fileName) ||
      (att.filePath && /\.(jpg|jpeg|png|webp|gif|bmp|svg)$/i.test(att.filePath));

    // Determine the source URL
    let displayUrl = '';
    if (att.localBlobUrl) {
      displayUrl = att.localBlobUrl;
    } else if (attId) {
      displayUrl = ChatApi.getAttachmentUrl(attId);
    } else if (att.url) {
      const token = ChatApi.getToken();
      displayUrl = token ? `${att.url}${att.url.includes('?') ? '&' : '?'}token=${encodeURIComponent(token)}` : att.url;
    } else if (att.filePath) {
      displayUrl = att.filePath;
    }

    if (isImg) {
      return `
        <div class="msg-img-container">
          <div class="img-preview-wrapper" onclick="ChatApp.openLightbox('${attId}', '${rawFileName}', '${displayUrl}')">
            <img 
              src="${displayUrl}" 
              alt="${escapedFileName}" 
              class="attachment-img-preview" 
              loading="lazy" 
              onerror="ChatApp.handleImgError(this, '${attId}')"
            />
            <div class="img-overlay-zoom">
              <span>🔍 Click to view</span>
            </div>
          </div>
          <div class="attachment-actions-bar">
            <span class="attachment-meta-info" title="${escapedFileName}">${escapedFileName}</span>
            <button 
              type="button" 
              class="attachment-dl-btn" 
              onclick="event.stopPropagation(); ChatApp.downloadAttachment('${attId}', '${rawFileName}', '${displayUrl}')"
              title="Download image"
            >
              <svg width="13" height="13" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.5"><path d="M21 15v4a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2v-4"/><polyline points="7 10 12 15 17 10"/><line x1="12" y1="15" x2="12" y2="3"/></svg>
              Download
            </button>
          </div>
        </div>
      `;
    }

    // Document / non-image attachment card
    const fileSizeStr = att.fileSizeBytes ? this.formatBytes(att.fileSizeBytes) : 'File';
    return `
      <div 
        class="msg-attachment-card" 
        onclick="ChatApp.downloadAttachment('${attId}', '${rawFileName}', '${displayUrl}')"
        title="Click to download ${escapedFileName}"
      >
        <div class="attachment-icon-box">
          <svg width="22" height="22" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><path d="M14 2H6a2 2 0 0 0-2 2v16a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V8z"/><polyline points="14 2 14 8 20 8"/></svg>
        </div>
        <div class="attachment-info">
          <span class="attachment-filename">${escapedFileName}</span>
          <span class="attachment-filesize">${fileSizeStr}</span>
        </div>
        <div class="card-dl-icon-btn" title="Download">
          <svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><path d="M21 15v4a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2v-4"/><polyline points="7 10 12 15 17 10"/><line x1="12" y1="15" x2="12" y2="3"/></svg>
        </div>
      </div>
    `;
  }

  static appendMessageBubble(msg, isOptimistic = false) {
    if (!this.messagesStreamBox) return;

    // Check date separator
    const dateStr = this.formatDateSeparator(msg.sentAt);
    const lastDateSep = this.messagesStreamBox.querySelector('.date-separator:last-of-type');
    if (!lastDateSep || lastDateSep.textContent !== dateStr) {
      const sep = document.createElement('div');
      sep.className = 'date-separator';
      sep.textContent = dateStr;
      this.messagesStreamBox.appendChild(sep);
    }

    const wrapper = document.createElement('div');
    wrapper.innerHTML = this.buildMessageBubbleHtml(msg, isOptimistic).trim();
    const bubbleEl = wrapper.firstElementChild;
    if (bubbleEl) {
      this.messagesStreamBox.appendChild(bubbleEl);
    }
  }

  static updateOptimisticBubble(tempId, confirmedMsg) {
    const el = document.getElementById(`bubble-temp-${tempId}`) || document.getElementById(`bubble-${tempId}`);
    if (el) {
      el.id = `bubble-${confirmedMsg.id}`;
      if (confirmedMsg.attachment) {
        const newHtml = this.buildMessageBubbleHtml(confirmedMsg, false);
        el.outerHTML = newHtml;
        return;
      }
      const meta = el.querySelector('.msg-meta-row');
      if (meta) {
        const timeStr = confirmedMsg.sentAt ? new Date(confirmedMsg.sentAt).toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' }) : '';
        meta.innerHTML = `<span>${timeStr}</span> <span class="checkmark-icon sent" title="Sent">✓✓</span>`;
      }
    }
  }

  static openLightbox(attId, fileName, fallbackSrc = null) {
    if (!this.lightboxBackdrop || !this.lightboxImg) return;
    const src = attId ? ChatApi.getAttachmentUrl(attId) : (fallbackSrc || '');
    this.lightboxImg.src = src;
    if (this.lightboxFilename) this.lightboxFilename.textContent = fileName || 'Image Preview';
    if (this.lightboxDownloadBtn) {
      this.lightboxDownloadBtn.dataset.attachmentId = attId || '';
      this.lightboxDownloadBtn.dataset.fileName = fileName || 'image.jpg';
    }
    this.lightboxBackdrop.style.display = 'flex';
  }

  static closeLightbox() {
    if (!this.lightboxBackdrop) return;
    this.lightboxBackdrop.style.display = 'none';
    if (this.lightboxImg) this.lightboxImg.src = '';
  }

  static async downloadAttachment(attId, fileName, fallbackUrl = null) {
    if (!attId && !fallbackUrl) return;
    try {
      this.showToast(`Downloading ${fileName || 'file'}...`);
      if (attId) {
        await ChatApi.downloadAttachment(attId, fileName);
      } else if (fallbackUrl) {
        const a = document.createElement('a');
        a.href = fallbackUrl;
        a.download = fileName || 'download';
        a.target = '_blank';
        document.body.appendChild(a);
        a.click();
        document.body.removeChild(a);
      }
    } catch (err) {
      console.error('Download error:', err);
      this.showToast('Download failed. Please try again.', 'error');
    }
  }

  static async handleImgError(imgEl, attId) {
    if (!imgEl || imgEl.dataset.fallbackTried === 'true') {
      if (imgEl && imgEl.parentElement) {
        imgEl.parentElement.innerHTML = `
          <div style="padding: 1.5rem; text-align: center; color: var(--text-muted); font-size: 0.8125rem;">
            ⚠️ Image preview unavailable
          </div>
        `;
      }
      return;
    }

    imgEl.dataset.fallbackTried = 'true';
    if (!attId) return;

    try {
      const blob = await ChatApi.fetchAttachmentBlob(attId);
      const blobUrl = URL.createObjectURL(blob);
      imgEl.src = blobUrl;
    } catch (err) {
      console.warn('Bearer blob fallback also failed:', err);
      if (imgEl.parentElement) {
        imgEl.parentElement.innerHTML = `
          <div style="padding: 1.5rem; text-align: center; color: var(--text-muted); font-size: 0.8125rem;">
            ⚠️ Image preview unavailable
          </div>
        `;
      }
    }
  }

  static markBubbleFailed(tempId) {
    const el = document.getElementById(`bubble-temp-${tempId}`) || document.getElementById(`bubble-${tempId}`);
    if (el) {
      const meta = el.querySelector('.msg-meta-row');
      if (meta) {
        meta.innerHTML = `<span class="msg-failed-retry" onclick="ChatApp.retryMessage('${tempId}')">⚠️ Failed • Retry ↻</span>`;
      }
    }
  }

  /* ------------------------------------------------------------------------
     Sending Messages with Optimistic UI & Idempotency
     ------------------------------------------------------------------------ */
  static async sendCurrentMessage() {
    const text = this.msgTextarea?.value?.trim() || '';
    const file = this.selectedFile;
    const activeConv = chatState.activeConversation;

    if (!text && !file) return;
    if (!activeConv) {
      this.showToast('Please select a conversation first.', 'error');
      return;
    }

    const currentUser = chatState.currentUser;
    const isAccountant = chatState.isAccountantUser();
    let targetEmpId = null;

    if (isAccountant) {
      const rawId = activeConv.employeeId || activeConv.id;
      if (!rawId || rawId === 'desk') {
        this.showToast('Please select an employee thread before sending.', 'error');
        return;
      }
      targetEmpId = rawId;
    }

    // 1. Generate unique Client Correlation ID for idempotency
    const clientMessageId = 'cmsg_' + Date.now() + '_' + Math.random().toString(36).substring(2, 9);
    const optimisticMsg = {
      tempId: clientMessageId,
      id: clientMessageId,
      clientMessageId: clientMessageId,
      senderId: currentUser.id,
      senderName: currentUser.fullName,
      senderRole: currentUser.role,
      messageText: text,
      sentAt: new Date().toISOString(),
      isSentByMe: true,
      hasAttachment: !!file,
      attachment: file ? {
        fileName: file.name,
        fileSizeBytes: file.size,
        fileType: file.type.startsWith('image/') ? 'image' : 'document',
        filePath: URL.createObjectURL(file)
      } : null
    };

    // Add to state & render immediately (Optimistic UI)
    chatState.addMessage(optimisticMsg, true);

    // Reset inputs
    if (this.msgTextarea) {
      this.msgTextarea.value = '';
      this.msgTextarea.style.height = 'auto';
    }
    this.clearSelectedFile();
    this.scrollToBottom(true);

    // 2. Dispatch to API with correlation ID
    try {
      const res = await ChatApi.sendMessage(text, file, targetEmpId, clientMessageId);
      if (res && res.success && res.data) {
        chatState.reconcileOptimisticMessage(clientMessageId, res.data);
      } else {
        chatState.markMessageFailed(clientMessageId);
        this.showToast(res?.message || 'Message could not be delivered.', 'error');
      }
    } catch {
      chatState.markMessageFailed(clientMessageId);
      this.showToast('Network error: Message failed to send.', 'error');
    }
  }

  static async retryMessage(tempId) {
    const msg = chatState.messages.find(m => 
      m.id === tempId || m.tempId === tempId || m.clientMessageId === tempId
    );
    if (!msg) return;

    msg.isFailed = false;
    const el = document.getElementById(`bubble-temp-${tempId}`) || document.getElementById(`bubble-${tempId}`);
    if (el) {
      const meta = el.querySelector('.msg-meta-row');
      if (meta) meta.innerHTML = '<span>Sending...</span> <span class="checkmark-icon pending">🕒</span>';
    }

    const isAccountant = chatState.isAccountantUser();
    const activeConv = chatState.activeConversation;
    let targetEmpId = null;

    if (isAccountant) {
      const rawId = activeConv?.employeeId || activeConv?.id;
      if (!rawId || rawId === 'desk') {
        this.showToast('Please select an employee thread before retrying.', 'error');
        return;
      }
      targetEmpId = rawId;
    }

    // Reuse the exact same Client Correlation ID to ensure server-side idempotency
    const correlationId = msg.clientMessageId || tempId;
    const res = await ChatApi.sendMessage(msg.messageText, null, targetEmpId, correlationId);
    if (res && res.success && res.data) {
      chatState.reconcileOptimisticMessage(correlationId, res.data);
    } else {
      chatState.markMessageFailed(correlationId);
      this.showToast(res?.message || 'Retry failed.', 'error');
    }
  }

  /* ------------------------------------------------------------------------
     Helpers & Utilities
     ------------------------------------------------------------------------ */
  static scrollToBottom(smooth = true) {
    if (!this.messagesStreamBox) return;
    this.messagesStreamBox.scrollTo({
      top: this.messagesStreamBox.scrollHeight,
      behavior: smooth ? 'smooth' : 'auto'
    });
  }

  static handleAutoScrollOnNewMessage() {
    if (!this.messagesStreamBox) return;
    const threshold = 150;
    const atBottom = (this.messagesStreamBox.scrollHeight - this.messagesStreamBox.scrollTop - this.messagesStreamBox.clientHeight) < threshold;

    if (atBottom) {
      this.scrollToBottom(true);
    } else if (this.jumpBottomBtn) {
      this.jumpBottomBtn.classList.add('visible');
    }
  }

  static clearSelectedFile() {
    this.selectedFile = null;
    if (this.fileInput) this.fileInput.value = '';
    if (this.filePreviewStrip) this.filePreviewStrip.classList.remove('active');
  }

  static focusInput() {
    setTimeout(() => this.msgTextarea?.focus(), 100);
  }

  static switchAuthTab(tab) {
    if (tab === 'login') {
      this.tabLoginBtn?.classList.add('active');
      this.tabRegisterBtn?.classList.remove('active');
      this.authLoginView?.style.setProperty('display', 'block');
      this.authRegisterView?.style.setProperty('display', 'none');
    } else {
      this.tabLoginBtn?.classList.remove('active');
      this.tabRegisterBtn?.classList.add('active');
      this.authLoginView?.style.setProperty('display', 'none');
      this.authRegisterView?.style.setProperty('display', 'block');
    }
  }

  static showAuthModal(show) {
    if (this.authModal) {
      this.authModal.classList.toggle('active', show);
    }
  }

  static updateConnectionPill(state) {
    if (!this.connectionPill) return;
    const dot = this.connectionPill.querySelector('.connection-dot');
    const text = this.connectionPill.querySelector('span:last-child');

    if (state === 'connected') {
      if (dot) dot.className = 'connection-dot';
      if (text) text.textContent = 'Connected';
      this.connectionPill.title = 'SignalR Live WebSockets Connected';
    } else if (state === 'reconnecting') {
      if (dot) dot.className = 'connection-dot connecting';
      if (text) text.textContent = 'Reconnecting...';
      this.connectionPill.title = 'Reconnecting to SignalR Hub...';
    } else if (state === 'connecting') {
      if (dot) dot.className = 'connection-dot connecting';
      if (text) text.textContent = 'Connecting...';
      this.connectionPill.title = 'Establishing real-time connection...';
    } else {
      if (dot) dot.className = 'connection-dot offline';
      if (text) text.textContent = 'Disconnected';
      this.connectionPill.title = 'Disconnected. Fallback polling active.';
    }
  }

  static setBtnLoading(btn, isLoading) {
    if (!btn) return;
    if (isLoading) {
      btn.dataset.orig = btn.innerHTML;
      btn.innerHTML = 'Connecting...';
      btn.disabled = true;
    } else {
      btn.innerHTML = btn.dataset.orig || 'Submit';
      btn.disabled = false;
    }
  }

  static showToast(message, type = 'info') {
    const container = document.getElementById('chat-toast-container');
    if (!container) return;

    const pill = document.createElement('div');
    pill.className = `chat-toast-pill ${type}`;
    pill.textContent = message;
    container.appendChild(pill);

    setTimeout(() => {
      pill.style.opacity = '0';
      pill.style.transform = 'translateY(-10px)';
      setTimeout(() => pill.remove(), 250);
    }, 3500);
  }

  static formatTimeSnippet(iso) {
    const d = new Date(iso);
    const now = new Date();
    if (d.toDateString() === now.toDateString()) {
      return d.toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' });
    }
    return d.toLocaleDateString([], { month: 'short', day: 'numeric' });
  }

  static formatDateSeparator(iso) {
    if (!iso) return 'Today';
    const d = new Date(iso);
    const now = new Date();
    if (d.toDateString() === now.toDateString()) return 'Today';
    const yesterday = new Date(now);
    yesterday.setDate(now.getDate() - 1);
    if (d.toDateString() === yesterday.toDateString()) return 'Yesterday';
    return d.toLocaleDateString([], { weekday: 'short', month: 'short', day: 'numeric' });
  }

  static formatBytes(bytes) {
    if (!bytes || bytes === 0) return '0 KB';
    const k = 1024;
    const sizes = ['Bytes', 'KB', 'MB', 'GB'];
    const i = Math.floor(Math.log(bytes) / Math.log(k));
    return parseFloat((bytes / Math.pow(k, i)).toFixed(1)) + ' ' + sizes[i];
  }

  static escape(str) {
    if (!str) return '';
    const p = document.createElement('p');
    p.textContent = str;
    return p.innerHTML;
  }
}

// Global Demo Credential Auto-Fill
window.fillDemoAccount = function(identifier, password) {
  const idInput = document.getElementById('login-id-input');
  const pwdInput = document.getElementById('login-pwd-input');
  if (idInput && pwdInput) {
    idInput.value = identifier;
    pwdInput.value = password;
  }
};

window.ChatApp = ChatApp;
