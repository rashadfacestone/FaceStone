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

    // Initialize FaceStone Google Maps integration
    if (window.FaceStoneMap) {
      window.FaceStoneMap.init();
    }

    // Browser navigation (back/forward button) listener
    window.addEventListener('popstate', () => {
      this.handleRoute();
    });

    if (ChatApi.isAuthenticated()) {
      const user = ChatApi.getCurrentUser();
      if (user) {
        this.onAuthenticated(user);
      } else {
        this.fetchProfileAndStart();
      }
    } else {
      this.showView('auth');
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

    // Department Selection Elements
    this.deptSelectionRoot = document.getElementById('dept-selection-root');
    this.deptCardsGrid = document.getElementById('dept-cards-grid');
    this.cardDeptFinance = document.getElementById('card-dept-finance');
    this.cardDeptTransportation = document.getElementById('card-dept-transportation');
    this.cardDeptStorage = document.getElementById('card-dept-storage');
    this.cardDeptManagement = document.getElementById('card-dept-management');
    this.btnEnterFinance = document.getElementById('btn-enter-finance');
    this.btnEnterTransportation = document.getElementById('btn-enter-transportation');
    this.btnEnterStorage = document.getElementById('btn-enter-storage');
    this.btnEnterManagement = document.getElementById('btn-enter-management');
    this.cardDeptControl = document.getElementById('card-dept-control');
    this.btnEnterControl = document.getElementById('btn-enter-control');
    this.deptUserName = document.getElementById('dept-user-name');
    this.deptUserRole = document.getElementById('dept-user-role');
    this.deptUserAvatar = document.getElementById('dept-user-avatar');
    this.deptLogoutBtn = document.getElementById('dept-logout-btn');

    this.ensureStorageElementsInDom();
    this.ensureManagementElementsInDom();
    this.ensureFinanceElementsInDom();
    this.ensureControlMonitoringElementsInDom();

    // Transportation Page Elements
    this.transportationPageRoot = document.getElementById('transportation-page-root');
    this.transBackBtn = document.getElementById('trans-back-to-departments-btn');
    this.transPlaceholderBackBtn = document.getElementById('trans-placeholder-back-btn');

    // Storage Page Elements
    this.storagePageRoot = document.getElementById('storage-page-root');
    this.storageBackBtn = document.getElementById('storage-back-to-departments-btn');
    this.storagePlaceholderBackBtn = document.getElementById('storage-placeholder-back-btn');

    // Management Page Elements
    this.managementPageRoot = document.getElementById('management-page-root');
    this.managementBackBtn = document.getElementById('management-back-to-departments-btn');
    this.managementPlaceholderBackBtn = document.getElementById('management-placeholder-back-btn');

    // Control & Monitoring Page Elements
    this.controlMonitoringPageRoot = document.getElementById('control-monitoring-page-root');
    this.controlMonitoringBackBtn = document.getElementById('control-monitoring-back-to-departments-btn');

    // Access Denied Elements
    this.accessDeniedRoot = document.getElementById('access-denied-root');
    this.accessDeniedBackBtn = document.getElementById('access-denied-back-btn');
    this.accessDeniedMsg = document.getElementById('access-denied-msg');
    this.accessDeniedSubmsg = document.getElementById('access-denied-submsg');

    // Finance Back to Departments Navigation Buttons
    this.sidebarBackDeptBtn = document.getElementById('btn-back-to-departments');
    this.chatHeaderBackDeptBtn = document.getElementById('chat-header-back-dept-btn');

    this.financeInitialized = false;

    // Sidebar
    this.conversationsListEl = document.getElementById('conversations-list-container');
    this.searchInput = document.getElementById('search-conversations-input');
    this.searchClearBtn = document.getElementById('search-clear-btn');
    this.currentUserNameEl = document.getElementById('current-user-name');
    this.currentUserRoleEl = document.getElementById('current-user-role');
    this.currentUserAvatarEl = document.getElementById('current-user-avatar');
    this.logoutBtn = document.getElementById('user-logout-btn');

    // Floating Chat Elements
    this.floatingChatBtn = document.getElementById('fs-floating-chat-btn');
    this.floatingChatBadge = document.getElementById('fs-floating-chat-badge');
    this.chatListView = document.getElementById('fs-chat-list-view');
    this.btnNewChatToggle = document.getElementById('btn-new-chat-toggle');
    this.btnCloseFloatingChat = document.getElementById('btn-close-floating-chat');
    this.btnCloseFloatingChatConv = document.getElementById('btn-close-floating-chat-conv');
    this.usersDirectorySection = document.getElementById('fs-users-directory-section');
    this.usersResultsList = document.getElementById('fs-users-results-list');
    this.btnCancelUserSearch = document.getElementById('btn-cancel-user-search');

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

    // Location Elements
    this.locationBtn = document.getElementById('chat-location-btn');
    this.locationPopoverMenu = document.getElementById('location-popover-menu');
    this.locationPopoverClose = document.getElementById('loc-popover-close');
    this.shareCurrentLocationBtn = document.getElementById('btn-share-current-location');
    this.liveDurationBtns = document.querySelectorAll('.btn-live-duration');

    this.activeWatchId = null;
    this.activeWatchSessionId = null;
    this.activeWatchExpiresAt = null;
    this.activeWatchTimer = null;
    this.lastSentLiveUpdate = 0;

    // Lightbox Modal
    this.lightboxBackdrop = document.getElementById('chat-lightbox-backdrop');
    this.lightboxImg = document.getElementById('lightbox-img-el');
    this.lightboxFilename = document.getElementById('lightbox-filename');
    this.lightboxCloseBtn = document.getElementById('lightbox-close-btn');
    this.lightboxDownloadBtn = document.getElementById('lightbox-download-btn');

    this.emptyMobileBackBtn = document.getElementById('empty-mobile-back-btn');
    this.selectedFile = null;

    // Attach Event Listeners
    this.bindUiActions();
  }

  static bindUiActions() {
    // Auth Tabs
    this.tabLoginBtn?.addEventListener('click', () => this.switchAuthTab('login'));
    this.tabRegisterBtn?.addEventListener('click', () => this.switchAuthTab('register'));

    // Floating Chat Button & Window Actions
    this.floatingChatBtn?.addEventListener('click', () => {
      this.toggleFloatingChat();
    });

    this.btnCloseFloatingChat?.addEventListener('click', () => {
      this.closeFloatingChat();
    });

    this.btnCloseFloatingChatConv?.addEventListener('click', () => {
      this.closeFloatingChat();
    });

    this.btnNewChatToggle?.addEventListener('click', () => {
      this.toggleUserDirectory();
    });

    this.btnCancelUserSearch?.addEventListener('click', () => {
      this.hideUserDirectory();
    });

    // Department Action Buttons
    this.btnEnterFinance?.addEventListener('click', () => {
      this.enterDepartment('finance');
    });

    // Finance Actions
    this.financeMgmtBackToDeptsBtn?.addEventListener('click', () => {
      this.backToDepartments();
    });

    this.btnEnterTransportation?.addEventListener('click', () => {
      this.enterDepartment('transportation');
    });

    this.btnEnterStorage?.addEventListener('click', () => {
      this.enterDepartment('storage');
    });

    this.btnEnterManagement?.addEventListener('click', () => {
      this.enterDepartment('management');
    });

    this.btnEnterControl?.addEventListener('click', () => {
      this.enterDepartment('control-monitoring');
    });

    this.controlMonitoringBackBtn?.addEventListener('click', () => {
      this.backToDepartments();
    });

    this.transBackBtn?.addEventListener('click', () => {
      this.backToDepartments();
    });

    this.transPlaceholderBackBtn?.addEventListener('click', () => {
      this.backToDepartments();
    });

    this.storageBackBtn?.addEventListener('click', () => {
      this.backToDepartments();
    });

    this.storagePlaceholderBackBtn?.addEventListener('click', () => {
      this.backToDepartments();
    });

    this.managementBackBtn?.addEventListener('click', () => {
      this.backToDepartments();
    });

    this.managementPlaceholderBackBtn?.addEventListener('click', () => {
      this.backToDepartments();
    });

    this.accessDeniedBackBtn?.addEventListener('click', () => {
      this.backToDepartments();
    });

    this.sidebarBackDeptBtn?.addEventListener('click', () => {
      this.backToDepartments();
    });

    this.chatHeaderBackDeptBtn?.addEventListener('click', () => {
      this.backToDepartments();
    });

    this.deptLogoutBtn?.addEventListener('click', () => {
      this.logout();
    });

    // Login Form Submit
    this.authLoginForm?.addEventListener('submit', async (e) => {
      e.preventDefault();
      const id = document.getElementById('login-id-input')?.value.trim();
      const pwd = document.getElementById('login-pwd-input')?.value;
      const submitBtn = this.authLoginForm.querySelector('button[type="submit"]');

      if (!id || !pwd) {
        this.showToast('يرجى إدخال اسم المستخدم وكلمة المرور.', 'error');
        return;
      }

      this.setBtnLoading(submitBtn, true);
      const res = await ChatApi.login(id, pwd);
      this.setBtnLoading(submitBtn, false);

      if (res.success && res.data) {
        this.showToast(`مرحباً بك، ${res.data.user.fullName}!`);
        if (window.location.pathname !== '/departments') {
          history.replaceState({ view: 'departments' }, '', '/departments');
        }
        this.showView('departments');
        await this.onAuthenticated(res.data.user);
      } else {
        this.showToast(res.message || 'فشل تسجيل الدخول. يرجى التحقق من صحة البيانات.', 'error');
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
      const role = document.getElementById('reg-role-input')?.value || 'Employee';
      const submitBtn = this.authRegisterForm.querySelector('button[type="submit"]');

      if (!fullName || !username || !email || !phone || !pwd) {
        this.showToast('جميع حقول التسجيل مطلوبة.', 'error');
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
        role: role
      });
      this.setBtnLoading(submitBtn, false);

      if (res.success && res.data) {
        this.showToast(`تم إنشاء الحساب بنجاح! مرحباً بك، ${res.data.user.fullName}`);
        if (window.location.pathname !== '/departments') {
          history.replaceState({ view: 'departments' }, '', '/departments');
        }
        this.showView('departments');
        await this.onAuthenticated(res.data.user);
      } else {
        this.showToast(res.message || 'فشل إنشاء الحساب.', 'error');
      }
    });

    // Logout
    this.logoutBtn?.addEventListener('click', () => {
      this.logout();
    });

    // Search Box
    this.searchInput?.addEventListener('input', () => {
      const val = this.searchInput.value.trim();
      chatState.searchQuery = val;
      if (this.searchClearBtn) {
        this.searchClearBtn.classList.toggle('active', !!val);
      }
      this.renderConversationsList();

      if (val.length >= 1 || (this.usersDirectorySection && this.usersDirectorySection.style.display !== 'none')) {
        this.searchAndRenderUsers(val);
      }
    });

    this.searchClearBtn?.addEventListener('click', () => {
      this.searchInput.value = '';
      chatState.searchQuery = '';
      this.searchClearBtn.classList.remove('active');
      this.renderConversationsList();
      if (this.usersDirectorySection && this.usersDirectorySection.style.display !== 'none') {
        this.searchAndRenderUsers('');
      }
    });

    // Back to Conversations List
    this.mobileBackBtn?.addEventListener('click', () => {
      this.showConversationsListView();
    });

    this.emptyMobileBackBtn?.addEventListener('click', () => {
      this.showConversationsListView();
    });

    // Location Button & Popover Actions
    this.locationBtn?.addEventListener('click', (e) => {
      e.stopPropagation();
      this.toggleLocationPopover();
    });

    this.locationPopoverClose?.addEventListener('click', () => {
      this.hideLocationPopover();
    });

    document.addEventListener('click', (e) => {
      if (this.locationPopoverMenu && !this.locationPopoverMenu.contains(e.target) && e.target !== this.locationBtn) {
        this.hideLocationPopover();
      }
    });

    this.shareCurrentLocationBtn?.addEventListener('click', () => {
      this.handleShareCurrentLocation();
    });

    this.liveDurationBtns?.forEach(btn => {
      btn.addEventListener('click', () => {
        const mins = parseInt(btn.dataset.minutes || '15', 10);
        this.handleShareLiveLocation(mins);
      });
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

    // Central Session Expired Event
    window.addEventListener('chat:session:expired', () => {
      this.stopTrackingOnSessionExpired();
      if (window.realTimeChat) {
        window.realTimeChat.stop();
      }
      this.showAuthModal(true);
      this.showToast('انتهت صلاحية الجلسة. يرجى تسجيل الدخول مجدداً.', 'error');
    });
  }

  static bindStateEvents() {
    window.addEventListener('chat:state:conversations:updated', () => {
      this.renderConversationsList();
      this.updateFloatingBadge();
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

    window.addEventListener('chat:state:messages:liveLocationUpdated', (e) => {
      this.onLiveLocationUpdated(e.detail.update);
    });

    window.addEventListener('chat:state:messages:liveLocationEnded', (e) => {
      this.onLiveLocationEnded(e.detail.update);
    });
  }

  static bindRealtimeEvents() {
    window.addEventListener('realtime:status', (e) => {
      this.updateConnectionPill(e.detail.state);
    });

    window.addEventListener('realtime:live-location:started', (e) => {
      // Event is handled through realtime:message:received with normalized LIVE_LOCATION payload
    });

    window.addEventListener('realtime:live-location:updated', (e) => {
      chatState.updateLiveLocation(e.detail);
    });

    window.addEventListener('realtime:live-location:ended', (e) => {
      chatState.endLiveLocation(e.detail);
    });

    window.addEventListener('realtime:message:received', (e) => {
      const msg = e.detail;
      const currentUser = chatState.currentUser;
      if (!msg || !currentUser) return;

      const myId = currentUser.id;
      const isSentByMe = msg.senderId === myId;
      const partnerId = isSentByMe ? msg.recipientId : msg.senderId;

      const activeConv = chatState.activeConversation;
      const activePartnerId = activeConv ? (activeConv.employeeId || activeConv.id) : null;
      const belongsToCurrent = activePartnerId && (activePartnerId === partnerId);

      if (belongsToCurrent) {
        chatState.addMessage(msg);
        this.scrollToBottom();
        // If window is open and conversation is active, mark read
        if (!isSentByMe && this.chatAppRoot && this.chatAppRoot.style.display !== 'none') {
          ChatApi.getConversationMessages(partnerId);
        }
      }

      // Update snippet in conversation list
      const isCurrentlyViewing = belongsToCurrent && (this.chatAppRoot && this.chatAppRoot.style.display !== 'none');
      const rawType = (msg.messageType || '').toUpperCase();
      let snippetText = msg.messageText;
      if (rawType === 'LIVE_LOCATION' || rawType === 'LIVELOCATION' || Boolean(msg.liveSessionId)) {
        snippetText = '📍 موقع مباشر';
      } else if (rawType === 'LOCATION') {
        snippetText = '📍 موقع جغرافي';
      } else if (msg.attachment || msg.hasAttachment) {
        snippetText = '📎 [مرفق]';
      }

      const partnerInfo = isSentByMe ? null : {
        fullName: msg.senderName,
        role: msg.senderRole,
        username: msg.senderUsername || ''
      };

      const incrementUnread = !isSentByMe && !isCurrentlyViewing;
      chatState.updateConversationSnippet(partnerId, snippetText || '', msg.sentAt, incrementUnread, partnerInfo);
      this.updateFloatingBadge();
    });
  }

  static async fetchProfileAndStart() {
    const res = await ChatApi.getProfile();
    if (res.success && res.data) {
      this.onAuthenticated(res.data);
    } else {
      this.showView('auth');
    }
  }

  static showView(viewName) {
    document.body.classList.toggle('auth-open', viewName === 'auth');

    if (this.authModal) {
      if (viewName === 'auth') {
        this.authModal.classList.add('active');
        this.authModal.style.display = 'flex';
      } else {
        this.authModal.classList.remove('active');
        this.authModal.style.display = 'none';
      }
    }

    if (this.deptSelectionRoot) {
      this.deptSelectionRoot.style.display = (viewName === 'departments') ? 'flex' : 'none';
      if (viewName === 'departments') {
        const u = chatState.currentUser || ChatApi.getCurrentUser();
        if (u) {
          this.updateDepartmentCardsVisibility(u);
        }
        document.dispatchEvent(new CustomEvent('chat:view:changed', { detail: { view: 'departments', user: u } }));
      }
    }

    if (this.financeManagementRoot) {
      const isMgmt = (viewName === 'finance' || viewName === 'finance-management' || viewName === 'finance-hub');
      this.financeManagementRoot.style.display = isMgmt ? 'flex' : 'none';
      if (isMgmt) {
        const u = chatState.currentUser || ChatApi.getCurrentUser();
        if (window.FinanceManagementApp) {
          window.FinanceManagementApp.init();
          window.FinanceManagementApp.updateUserBadge(u);
          window.FinanceManagementApp.loadData();
        }
        document.dispatchEvent(new CustomEvent('chat:view:changed', { detail: { view: 'finance-management', user: u } }));
      }
    }

    // Floating Chat Button visibility control
    if (this.floatingChatBtn) {
      const isAuth = ChatApi.isAuthenticated();
      this.floatingChatBtn.style.display = (viewName === 'auth' || !isAuth) ? 'none' : 'flex';
      if (viewName === 'auth') {
        this.closeFloatingChat();
      }
    }

    if (viewName === 'finance-chat') {
      this.openFloatingChat();
    }

    if (this.transportationPageRoot) {
      this.transportationPageRoot.style.display = (viewName === 'transportation') ? 'flex' : 'none';
      if (viewName === 'transportation') {
        const u = chatState.currentUser || ChatApi.getCurrentUser();
        if (window.TransportationApp) {
          window.TransportationApp.updateUserBadge(u);
          window.TransportationApp.loadRecentTransactions();
        }
        document.dispatchEvent(new CustomEvent('chat:view:changed', { detail: { view: 'transportation', user: u } }));
      }
    }

    if (this.storagePageRoot) {
      this.storagePageRoot.style.display = (viewName === 'storage') ? 'flex' : 'none';
      if (viewName === 'storage') {
        const u = chatState.currentUser || ChatApi.getCurrentUser();
        if (window.StorageApp) {
          window.StorageApp.updateUserBadge(u);
          window.StorageApp.loadInitialData();
        }
        document.dispatchEvent(new CustomEvent('chat:view:changed', { detail: { view: 'storage', user: u } }));
      }
    }

    if (this.managementPageRoot) {
      this.managementPageRoot.style.display = (viewName === 'management') ? 'flex' : 'none';
      if (viewName === 'management') {
        const u = chatState.currentUser || ChatApi.getCurrentUser();
        if (window.ManagementApp) {
          window.ManagementApp.updateUserBadge(u);
          window.ManagementApp.loadData();
        }
        document.dispatchEvent(new CustomEvent('chat:view:changed', { detail: { view: 'management', user: u } }));
      }
    }

    if (this.controlMonitoringPageRoot) {
      this.controlMonitoringPageRoot.style.display = (viewName === 'control-monitoring') ? 'flex' : 'none';
      if (viewName === 'control-monitoring') {
        const u = chatState.currentUser || ChatApi.getCurrentUser();
        if (window.ControlMonitoringApp) {
          window.ControlMonitoringApp.init();
          window.ControlMonitoringApp.updateUserBadge(u);
          window.ControlMonitoringApp.loadStats();
          window.ControlMonitoringApp.loadForms();
        }
        document.dispatchEvent(new CustomEvent('chat:view:changed', { detail: { view: 'control-monitoring', user: u } }));
      }
    }

    if (this.accessDeniedRoot) {
      this.accessDeniedRoot.style.display = (viewName === 'access-denied') ? 'flex' : 'none';
    }
  }

  static ensureStorageElementsInDom() {
    this.cardDeptStorage = document.getElementById('card-dept-storage');
    this.btnEnterStorage = document.getElementById('btn-enter-storage');
    this.storagePageRoot = document.getElementById('storage-page-root');
    this.storageBackBtn = document.getElementById('storage-back-to-departments-btn');
    this.storagePlaceholderBackBtn = document.getElementById('storage-placeholder-back-btn');
  }

  static ensureManagementElementsInDom() {
    this.cardDeptManagement = document.getElementById('card-dept-management');
    this.btnEnterManagement = document.getElementById('btn-enter-management');
    this.managementPageRoot = document.getElementById('management-page-root');
    this.managementBackBtn = document.getElementById('management-back-to-departments-btn');
    this.managementPlaceholderBackBtn = document.getElementById('management-placeholder-back-btn');
  }

  static ensureFinanceElementsInDom() {
    this.cardDeptFinance = document.getElementById('card-dept-finance');
    this.btnEnterFinance = document.getElementById('btn-enter-finance');
    this.financeManagementRoot = document.getElementById('finance-management-page-root');
    this.financeMgmtBackToDeptsBtn = document.getElementById('finance-mgmt-back-to-departments-btn');
  }

  static ensureControlMonitoringElementsInDom() {
    this.cardDeptControl = document.getElementById('card-dept-control');
    this.btnEnterControl = document.getElementById('btn-enter-control');
    this.controlMonitoringPageRoot = document.getElementById('control-monitoring-page-root');
    this.controlMonitoringBackBtn = document.getElementById('control-monitoring-back-to-departments-btn');
  }

  static updateDepartmentCardsVisibility(user, deptRes = null) {
    this.ensureStorageElementsInDom();
    this.ensureManagementElementsInDom();
    this.ensureFinanceElementsInDom();
    this.ensureControlMonitoringElementsInDom();
    if (!user) return;
    const role = (user.role || '').trim().toUpperCase();

    // Recognized roles for departments
    const isCeo = role === 'CEO';
    const isIt = role === 'IT';
    const isAccountant = role === 'ACCOUNTANT' || role.includes('ACCOUNT');
    const isTransManager = role === 'TRANSPORTATION_MANAGER' || role === 'TRANSPORTATION MANAGER' || role.includes('TRANSPORT');
    const isStorageManager = role === 'STORAGE_MANAGER' || role === 'STORAGE MANAGER' || role === 'STORAGEMANAGER' || role.includes('STORAGE');
    const isFactoryManager = role === 'FACTORY_MANAGER' || role === 'FACTORY MANAGER' || role.includes('FACTORY');
    const isControlMonitoring = role === 'CONTROL_MONITORING' || role === 'CONTROL MONITORING' || role.includes('CONTROL');

    let hasFinance = (isCeo || isIt || isAccountant);
    let hasTrans = (isCeo || isIt || isTransManager);
    let hasStorage = (isCeo || isIt || isStorageManager);
    let hasManagement = (isCeo || isIt || isFactoryManager);
    let hasControlMonitoring = (isCeo || isIt || isControlMonitoring);

    // If server response is available, use it to confirm/override
    if (deptRes && deptRes.success && deptRes.data) {
      if (typeof deptRes.data.hasFinanceAccess === 'boolean') {
        hasFinance = deptRes.data.hasFinanceAccess;
      }
      if (typeof deptRes.data.hasTransportationAccess === 'boolean') {
        hasTrans = deptRes.data.hasTransportationAccess;
      }
      if (typeof deptRes.data.hasStorageAccess === 'boolean') {
        hasStorage = deptRes.data.hasStorageAccess;
      }
      if (typeof deptRes.data.hasManagementAccess === 'boolean') {
        hasManagement = deptRes.data.hasManagementAccess;
      }
      if (typeof deptRes.data.hasControlMonitoringAccess === 'boolean') {
        hasControlMonitoring = deptRes.data.hasControlMonitoringAccess;
      }
      if (Array.isArray(deptRes.data.departments)) {
        const availableDepts = deptRes.data.departments.filter(d => d.isAvailable !== false).map(d => (d.id || '').toLowerCase());
        hasFinance = availableDepts.includes('finance');
        hasTrans = availableDepts.includes('transportation');
        hasStorage = availableDepts.includes('storage');
        hasManagement = availableDepts.includes('management');
        hasControlMonitoring = availableDepts.includes('control-monitoring');
      }
    }

    const cardFinance = document.getElementById('card-dept-finance') || this.cardDeptFinance;
    const cardTrans = document.getElementById('card-dept-transportation') || this.cardDeptTransportation;
    const cardStorage = document.getElementById('card-dept-storage') || this.cardDeptStorage;
    const cardManagement = document.getElementById('card-dept-management') || this.cardDeptManagement;
    const cardControl = document.getElementById('card-dept-control') || this.cardDeptControl;

    if (cardFinance) {
      cardFinance.style.display = hasFinance ? 'flex' : 'none';
    }
    if (cardTrans) {
      cardTrans.style.display = hasTrans ? 'flex' : 'none';
    }
    if (cardStorage) {
      cardStorage.style.display = hasStorage ? 'flex' : 'none';
    }
    if (cardManagement) {
      cardManagement.style.display = hasManagement ? 'flex' : 'none';
    }
    if (cardControl) {
      cardControl.style.display = hasControlMonitoring ? 'flex' : 'none';
    }
  }

  static handleRoute() {
    if (!ChatApi.isAuthenticated()) {
      this.showView('auth');
      return;
    }
    const path = window.location.pathname.toLowerCase();
    const hash = window.location.hash.toLowerCase();
    if (path === '/finance/chat' || hash === '#/finance/chat') {
      this.enterFinanceChat();
    } else if (path.startsWith('/finance') || hash.startsWith('#/finance')) {
      const targetSubRoute = path.startsWith('/finance') ? window.location.pathname : hash.replace('#', '');
      this.enterDepartment('finance', targetSubRoute);
    } else if (path === '/transportation' || hash === '#/transportation') {
      this.enterDepartment('transportation');
    } else if (path === '/departments/storage' || path === '/storage' || hash === '#/departments/storage' || hash === '#/storage') {
      this.enterDepartment('storage');
    } else if (path.startsWith('/management') || hash.startsWith('#/management')) {
      const targetSubRoute = path.startsWith('/management') ? window.location.pathname : hash.replace('#', '');
      this.enterDepartment('management', targetSubRoute);
    } else if (path.startsWith('/control-monitoring') || hash.startsWith('#/control-monitoring')) {
      const targetSubRoute = path.startsWith('/control-monitoring') ? window.location.pathname : hash.replace('#', '');
      this.enterDepartment('control-monitoring', targetSubRoute);
    } else {
      this.showView('departments');
      const u = chatState.currentUser || ChatApi.getCurrentUser();
      if (u) {
        this.updateDepartmentCardsVisibility(u);
      }
    }
  }

  static formatRoleArabic(role) {
    if (!role) return 'عضو';
    const r = role.trim().toUpperCase();
    switch (r) {
      case 'CEO': return 'الرئيس التنفيذي';
      case 'IT': return 'مسؤول تكنولوجيا المعلومات';
      case 'CONTROL_MONITORING':
      case 'CONTROL MONITORING':
      case 'CONTROL_MONITORING EMPLOYEE': return 'مراقب ومراجع العمليات';
      case 'ACCOUNTANT': return 'محاسب مالي';
      case 'TRANSPORTATION_MANAGER':
      case 'TRANSPORTATION MANAGER': return 'مدير حركة النقل';
      case 'STORAGE_MANAGER':
      case 'STORAGE MANAGER':
      case 'STORAGEMANAGER': return 'مدير المستودعات';
      case 'FACTORY_MANAGER':
      case 'FACTORY MANAGER': return 'مدير المصنع';
      case 'WEIGHBRIDGE_OPERATOR':
      case 'WEIGHBRIDGE OPERATOR': return 'مشغل الميزان';
      case 'QUALITY_CONTROLLER':
      case 'QUALITY CONTROLLER': return 'مراقب الجودة';
      case 'OPERATIONS DESK':
      case 'OPERATIONS_DESK': return 'مكتب العمليات';
      case 'EMPLOYEE': return 'موظف';
      default: return role;
    }
  }

  static async onAuthenticated(user) {
    chatState.init(user);

    // Show floating chat button for all authenticated users
    if (this.floatingChatBtn) {
      this.floatingChatBtn.style.display = 'flex';
    }

    // Start real-time messaging for all users
    if (window.realTimeChat) {
      window.realTimeChat.start(user);
    }

    // Load initial conversations & update unread badge
    await this.loadConversations();

    // Update Header in Finance Chat
    if (this.currentUserNameEl) this.currentUserNameEl.textContent = user.fullName;
    if (this.currentUserRoleEl) this.currentUserRoleEl.textContent = this.formatRoleArabic(user.role);
    if (this.currentUserAvatarEl) {
      const initials = user.fullName.split(' ').map(n => n[0]).join('').substring(0, 2).toUpperCase();
      this.currentUserAvatarEl.textContent = initials || 'FS';
    }

    // Update Header in Department Selection
    if (this.deptUserName) this.deptUserName.textContent = user.fullName;
    if (this.deptUserRole) this.deptUserRole.textContent = this.formatRoleArabic(user.role);
    if (this.deptUserAvatar) {
      const initials = user.fullName.split(' ').map(n => n[0]).join('').substring(0, 2).toUpperCase();
      this.deptUserAvatar.textContent = initials || 'FS';
    }

    // Synchronously update card visibility immediately based on user role
    this.updateDepartmentCardsVisibility(user);

    // Query backend for available departments based on caller's role to confirm
    try {
      const deptRes = await ChatApi.getDepartments();
      this.updateDepartmentCardsVisibility(user, deptRes);
    } catch (e) {
      console.warn('Could not query department permissions from server:', e);
    }

    // Check URL route if user navigated directly
    const path = window.location.pathname.toLowerCase();
    const hash = window.location.hash.toLowerCase();

    if (path === '/finance/chat' || hash === '#/finance/chat') {
      await this.enterFinanceChat();
    } else if (path === '/finance/management' || hash === '#/finance/management') {
      await this.enterFinanceManagement();
    } else if (path.startsWith('/finance') || hash.startsWith('#/finance')) {
      const targetSubRoute = path.startsWith('/finance') ? window.location.pathname : hash.replace('#', '');
      await this.enterDepartment('finance', targetSubRoute);
    } else if (path === '/transportation' || hash === '#/transportation') {
      await this.enterDepartment('transportation');
    } else if (path === '/departments/storage' || path === '/storage' || hash === '#/departments/storage' || hash === '#/storage') {
      await this.enterDepartment('storage');
    } else if (path.startsWith('/management') || hash.startsWith('#/management')) {
      const targetSubRoute = path.startsWith('/management') ? window.location.pathname : hash.replace('#', '');
      await this.enterDepartment('management', targetSubRoute);
    } else if (path.startsWith('/control-monitoring') || hash.startsWith('#/control-monitoring')) {
      await this.enterDepartment('control-monitoring');
    } else {
      this.showView('departments');
      if (window.location.pathname !== '/departments') {
        history.replaceState({ view: 'departments' }, '', '/departments');
      }
    }
  }

  static async enterDepartment(dept, subRoute = null) {
    if (dept === 'finance') {
      // Backend authorization check for Finance Department
      const res = await ChatApi.verifyFinanceAccess();
      if (res.success && res.status !== 403) {
        this.showView('finance-management');
        const user = chatState.currentUser || ChatApi.getCurrentUser();
        if (window.FinanceManagementApp) {
          window.FinanceManagementApp.init();
          window.FinanceManagementApp.updateUserBadge(user);
          if (subRoute && subRoute !== '/finance' && subRoute !== '/finance/management' && subRoute !== '/finance/hub') {
            window.FinanceManagementApp.handleRoute(subRoute);
          } else {
            window.FinanceManagementApp.showHubCardsView();
          }
        }
        if (subRoute && subRoute !== '/finance' && subRoute !== '/finance/management' && subRoute !== '/finance/hub') {
          if (window.location.pathname !== subRoute) {
            history.pushState({ view: 'finance-management', subRoute }, '', subRoute);
          }
        } else if (window.location.pathname !== '/finance') {
          history.pushState({ view: 'finance-management' }, '', '/finance');
        }
      } else {
        // 403 Forbidden: Render Access Denied screen
        this.setAccessDeniedDept('Finance', 'Accountants, IT, and CEO');
        this.showView('access-denied');
        if (window.location.pathname !== '/finance') {
          history.pushState({ view: 'access-denied' }, '', '/finance');
        }
      }
    } else if (dept === 'transportation') {
      // Backend authorization check
      const res = await ChatApi.verifyTransportationAccess();
      if (res.success && res.status !== 403) {
        this.showView('transportation');
        if (window.location.pathname !== '/transportation') {
          history.pushState({ view: 'transportation' }, '', '/transportation');
        }
      } else {
        // 403 Forbidden: Render Access Denied screen
        this.setAccessDeniedDept('Transportation', 'CEO, IT, and Transportation Manager');
        this.showView('access-denied');
        if (window.location.pathname !== '/transportation') {
          history.pushState({ view: 'access-denied' }, '', '/transportation');
        }
      }
    } else if (dept === 'storage') {
      // Backend authorization check
      const res = await ChatApi.verifyStorageAccess();
      if (res.success && res.status !== 403) {
        this.showView('storage');
        if (window.location.pathname !== '/departments/storage') {
          history.pushState({ view: 'storage' }, '', '/departments/storage');
        }
      } else {
        // 403 Forbidden: Render Access Denied screen
        this.setAccessDeniedDept('Storage', 'CEO, IT, and Storage Manager');
        this.showView('access-denied');
        if (window.location.pathname !== '/departments/storage') {
          history.pushState({ view: 'access-denied' }, '', '/departments/storage');
        }
      }
    } else if (dept === 'management') {
      // Backend authorization check
      const res = await ChatApi.verifyManagementAccess();
      if (res.success && res.status !== 403) {
        this.showView('management');
        const user = chatState.currentUser || ChatApi.getCurrentUser();
        if (window.ManagementApp) {
          window.ManagementApp.init();
          window.ManagementApp.updateUserBadge(user);
          if (subRoute && subRoute !== '/management') {
            window.ManagementApp.handleRoute(subRoute);
          } else {
            window.ManagementApp.showLandingHub();
          }
        }
        if (subRoute && subRoute !== '/management') {
          if (window.location.pathname !== subRoute) {
            history.pushState({ view: 'management', subRoute }, '', subRoute);
          }
        } else if (window.location.pathname !== '/management') {
          history.pushState({ view: 'management' }, '', '/management');
        }
      } else {
        // 403 Forbidden: Render Access Denied screen
        this.setAccessDeniedDept('Management', 'CEO, IT, and Factory Manager');
        this.showView('access-denied');
        if (window.location.pathname !== '/management') {
          history.pushState({ view: 'access-denied' }, '', '/management');
        }
      }
    } else if (dept === 'control-monitoring') {
      const res = await ChatApi.verifyControlMonitoringAccess();
      if (res.success && res.status !== 403) {
        this.showView('control-monitoring');
        const user = chatState.currentUser || ChatApi.getCurrentUser();
        if (window.ControlMonitoringApp) {
          window.ControlMonitoringApp.init();
          window.ControlMonitoringApp.updateUserBadge(user);
          if (subRoute && subRoute !== '/control-monitoring') {
            window.ControlMonitoringApp.handleRoute(subRoute);
          } else {
            window.ControlMonitoringApp.showMainPage();
            window.ControlMonitoringApp.loadStats();
            window.ControlMonitoringApp.loadForms();
          }
        }
        if (subRoute && subRoute !== '/control-monitoring') {
          if (window.location.pathname !== subRoute) {
            history.pushState({ view: 'control-monitoring', subRoute }, '', subRoute);
          }
        } else if (window.location.pathname !== '/control-monitoring') {
          history.pushState({ view: 'control-monitoring' }, '', '/control-monitoring');
        }
      } else {
        this.setAccessDeniedDept('ControlMonitoring', 'Control & Monitoring Employees, IT, and CEO');
        this.showView('access-denied');
        if (window.location.pathname !== '/control-monitoring') {
          history.pushState({ view: 'access-denied' }, '', '/control-monitoring');
        }
      }
    }
  }

  static async enterFinanceChat() {
    this.openFloatingChat();
  }

  static async enterFinanceManagement() {
    await this.enterDepartment('finance');
  }

  static setAccessDeniedDept(deptName, allowedRoles) {
    let deptAr = deptName;
    if (deptName === 'Finance' || deptName === 'Finance Management') deptAr = 'الإدارة المالية';
    else if (deptName === 'Transportation') deptAr = 'إدارة النقل والموازين';
    else if (deptName === 'Storage') deptAr = 'إدارة المستودعات والمخازن';
    else if (deptName === 'Management') deptAr = 'الإدارة العامة ومراقبة الجودة';
    else if (deptName === 'ControlMonitoring') deptAr = 'قسم التحكم والمراقبة';

    let rolesAr = allowedRoles;
    if (allowedRoles.includes('Accountants')) rolesAr = 'المحاسبون، تقنية المعلومات، والرئيس التنفيذي';
    else if (allowedRoles.includes('Transportation Manager')) rolesAr = 'الرئيس التنفيذي، تقنية المعلومات، ومدير النقل';
    else if (allowedRoles.includes('Storage Manager')) rolesAr = 'الرئيس التنفيذي، تقنية المعلومات، ومدير المستودعات';
    else if (allowedRoles.includes('Factory Manager')) rolesAr = 'الرئيس التنفيذي، تقنية المعلومات، ومدير المصنع';
    else if (allowedRoles.includes('Control & Monitoring Employees')) rolesAr = 'الرئيس التنفيذي، تقنية المعلومات، وموظفو قسم التحكم والمراقبة';

    if (this.accessDeniedMsg) {
      this.accessDeniedMsg.innerHTML = `ليس لديك صلاحية للوصول إلى <strong>${deptAr}</strong>.`;
    }
    if (this.accessDeniedSubmsg) {
      this.accessDeniedSubmsg.innerHTML = `الوصول إلى ${deptAr} مقتصر حصرياً على: <strong>${rolesAr}</strong>.`;
    }
  }

  static backToDepartments() {
    this.showView('departments');
    const u = chatState.currentUser || ChatApi.getCurrentUser();
    if (u) {
      this.updateDepartmentCardsVisibility(u);
    }
    if (window.location.pathname !== '/departments') {
      history.pushState({ view: 'departments' }, '', '/departments');
    }
  }

  static toggleFloatingChat(forceOpen = null) {
    if (!this.chatAppRoot) return;
    const isHidden = this.chatAppRoot.style.display === 'none';
    const shouldOpen = forceOpen !== null ? forceOpen : isHidden;
    if (shouldOpen) {
      this.openFloatingChat();
    } else {
      this.closeFloatingChat();
    }
  }

  static openFloatingChat() {
    if (!this.chatAppRoot) return;
    this.chatAppRoot.style.display = 'flex';
    this.chatAppRoot.classList.add('active');
    if (!chatState.activeConversation) {
      this.showConversationsListView();
    } else {
      this.showActiveConversationView();
    }
  }

  static closeFloatingChat() {
    if (!this.chatAppRoot) return;
    this.chatAppRoot.style.display = 'none';
    this.chatAppRoot.classList.remove('active');
  }

  static updateFloatingBadge() {
    const badge = document.getElementById('fs-floating-chat-badge');
    if (!badge) return;
    const totalUnread = chatState.getTotalUnreadCount();
    if (totalUnread > 0) {
      badge.textContent = totalUnread > 99 ? '99+' : totalUnread;
      badge.style.display = 'flex';
    } else {
      badge.style.display = 'none';
    }
  }

  static showConversationsListView() {
    if (this.chatListView) this.chatListView.style.display = 'flex';
    if (this.chatActiveView) this.chatActiveView.style.display = 'none';
    chatState.activeConversation = null;
    window.ChatAppActiveEmployeeId = null;
    this.renderConversationsList();
  }

  static showActiveConversationView() {
    if (this.chatListView) this.chatListView.style.display = 'none';
    if (this.chatActiveView) this.chatActiveView.style.display = 'flex';
  }

  static async searchAndRenderUsers(searchTerm = '') {
    if (!this.usersResultsList) return;
    this.usersResultsList.innerHTML = '<div style="padding: 1rem; text-align: center; color: var(--text-muted); font-size: 0.8125rem;">جاري البحث عن الزملاء...</div>';
    
    try {
      const res = await ChatApi.searchUsers(searchTerm);
      if (res.success && Array.isArray(res.data)) {
        if (res.data.length === 0) {
          this.usersResultsList.innerHTML = '<div style="padding: 1rem; text-align: center; color: var(--text-muted); font-size: 0.8125rem;">لا يوجد مستخدمين مطابقين.</div>';
          return;
        }

        this.usersResultsList.innerHTML = res.data.map(u => {
          const initials = (u.fullName || u.username || 'FS').split(' ').map(n => n[0]).join('').substring(0, 2).toUpperCase();
          const roleArabic = this.formatRoleArabic(u.role);
          return `
            <div class="fs-user-item" onclick="ChatApp.startConversationWithUser('${u.id}', '${this.escape(u.fullName || u.username)}', '${this.escape(u.role || '')}', '${this.escape(u.username || '')}')">
              <div class="fs-user-item-avatar">${initials}</div>
              <div class="fs-user-item-info">
                <span class="fs-user-item-name">${this.escape(u.fullName || u.username)}</span>
                <div class="fs-user-item-meta">
                  <span class="fs-user-item-role">${roleArabic}</span>
                  <span>@${this.escape(u.username)}</span>
                </div>
              </div>
            </div>
          `;
        }).join('');
      } else {
        this.usersResultsList.innerHTML = '<div style="padding: 1rem; text-align: center; color: var(--text-muted); font-size: 0.8125rem;">تعذر جلب قائمة المستخدمين.</div>';
      }
    } catch (e) {
      console.error('searchAndRenderUsers error:', e);
      this.usersResultsList.innerHTML = '<div style="padding: 1rem; text-align: center; color: var(--text-muted); font-size: 0.8125rem;">خطأ أثناء البحث.</div>';
    }
  }

  static toggleUserDirectory() {
    if (!this.usersDirectorySection) return;
    const isHidden = this.usersDirectorySection.style.display === 'none';
    if (isHidden) {
      this.usersDirectorySection.style.display = 'flex';
      this.searchAndRenderUsers(this.searchInput?.value?.trim() || '');
      this.searchInput?.focus();
    } else {
      this.usersDirectorySection.style.display = 'none';
    }
  }

  static hideUserDirectory() {
    if (this.usersDirectorySection) {
      this.usersDirectorySection.style.display = 'none';
    }
  }

  static async startConversationWithUser(userId, fullName, role, username) {
    this.hideUserDirectory();
    if (this.searchInput) {
      this.searchInput.value = '';
      chatState.searchQuery = '';
      this.searchClearBtn?.classList.remove('active');
    }

    let conv = chatState.conversations.find(c => (c.employeeId === userId || c.id === userId));
    if (!conv) {
      conv = {
        id: userId,
        employeeId: userId,
        fullName: fullName || username,
        username: username,
        role: role,
        lastMessageText: '',
        lastMessageTime: new Date().toISOString(),
        unreadCount: 0
      };
      chatState.conversations.unshift(conv);
      chatState.emitChange('conversations:updated');
    }

    await this.selectConversation(conv);
  }

  static logout() {
    this.stopTrackingOnSessionExpired();
    if (this.floatingChatBtn) this.floatingChatBtn.style.display = 'none';
    this.closeFloatingChat();
    if (window.realTimeChat) {
      window.realTimeChat.stop();
    }
    this.financeInitialized = false;
    ChatApi.clearSession();
    this.showToast('تم تسجيل الخروج بنجاح.');
    this.showView('auth');
    if (window.location.pathname !== '/') {
      history.replaceState({}, '', '/');
    }
  }

  /* ------------------------------------------------------------------------
     Sidebar Conversation Loading
     ------------------------------------------------------------------------ */
  static async loadConversations() {
    if (!this.conversationsListEl) return;
    this.conversationsListEl.innerHTML = '<div style="padding: 1.5rem; text-align: center; color: var(--text-muted); font-size: 0.8125rem;">جاري تحميل المحادثات...</div>';
    
    try {
      const res = await ChatApi.getConversations();
      if (res.success && res.data?.conversations) {
        chatState.setConversations(res.data.conversations);
        this.updateFloatingBadge();
      } else {
        chatState.setConversations([]);
        this.renderConversationsList();
      }
    } catch (e) {
      console.error('loadConversations error:', e);
      chatState.setConversations([]);
    }
  }

  static async loadAccountantConversations() {
    return await this.loadConversations();
  }

  static async loadEmployeeDeskConversation(currentUser) {
    return await this.loadConversations();
  }

  static renderConversationsList() {
    if (!this.conversationsListEl) return;
    const list = chatState.getFilteredConversations();

    if (list.length === 0) {
      this.conversationsListEl.innerHTML = `
        <div style="padding: 2rem 1rem; text-align: center; color: var(--text-muted); font-size: 0.875rem;">
          لا توجد محادثات سابقة.<br>
          <button type="button" class="fs-chat-btn-action" style="margin-top: 0.75rem;" onclick="ChatApp.toggleUserDirectory()">بدء محادثة جديدة</button>
        </div>
      `;
      return;
    }

    this.conversationsListEl.innerHTML = list.map(c => {
      const isActive = chatState.activeConversation && (chatState.activeConversation.id === c.employeeId || chatState.activeConversation.id === c.id);
      const initials = (c.fullName || c.username || 'FS').split(' ').map(n => n[0]).join('').substring(0, 2).toUpperCase();
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
                ${c.hasAttachment ? '📎 ' : ''}${this.escape(c.lastMessageText || 'محادثة جديدة')}
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
    const targetId = conv.employeeId || conv.id;

    // In-flight guard: prevent duplicate concurrent message fetches for same conversation
    if (this.isLoadingConversation && this.loadingConversationId === targetId) {
      return;
    }
    this.isLoadingConversation = true;
    this.loadingConversationId = targetId;

    try {
      chatState.setActiveConversation(conv);
      window.ChatAppActiveEmployeeId = targetId;

      // Switch views in floating window
      this.showActiveConversationView();
      this.renderActiveConversationHeader(conv);

      // Highlight in list
      this.renderConversationsList();

      // Fetch conversation messages
      this.messagesStreamBox.innerHTML = '<div style="padding: 2rem; text-align: center; color: var(--text-muted); font-size: 0.8125rem;">جاري تحميل الرسائل...</div>';

      const res = await ChatApi.getConversationMessages(targetId);
      if (res.success && res.data?.messages) {
        conv.unreadCount = 0;
        this.renderConversationsList();
        this.updateFloatingBadge();
        chatState.setMessages(res.data.messages);
      } else {
        chatState.setMessages([]);
      }

      this.focusInput();
    } finally {
      this.isLoadingConversation = false;
      this.loadingConversationId = null;
    }
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
        <span>متصل الآن • ${this.formatRoleArabic(conv.role)}</span>
      `;
    }
  }

  /* ------------------------------------------------------------------------
     Location Sharing Methods (Current & Live)
     ------------------------------------------------------------------------ */
  static toggleLocationPopover() {
    if (!this.locationPopoverMenu) return;
    const isVisible = this.locationPopoverMenu.style.display === 'block';
    this.locationPopoverMenu.style.display = isVisible ? 'none' : 'block';
  }

  static hideLocationPopover() {
    if (this.locationPopoverMenu) {
      this.locationPopoverMenu.style.display = 'none';
    }
  }

  static async handleShareCurrentLocation() {
    this.hideLocationPopover();

    if (!('geolocation' in navigator)) {
      this.showToast('خاصية تحديد الموقع الجغرافي غير مدعومة في متصفحك.', 'error');
      return;
    }

    const activeConv = chatState.activeConversation;
    if (!activeConv) {
      this.showToast('يرجى اختيار محادثة أولاً.', 'error');
      return;
    }

    this.showToast('جاري تحديد موقعك الجغرافي...', 'info');

    const currentUser = chatState.currentUser;
    const targetEmpId = activeConv.employeeId || activeConv.id;

    navigator.geolocation.getCurrentPosition(
      async (pos) => {
        const latitude = pos.coords.latitude;
        const longitude = pos.coords.longitude;

        const clientMessageId = 'loc_' + Date.now() + '_' + Math.random().toString(36).substring(2, 9);
        const optimisticMsg = {
          tempId: clientMessageId,
          id: clientMessageId,
          clientMessageId: clientMessageId,
          senderId: currentUser.id,
          senderName: currentUser.fullName,
          senderRole: currentUser.role,
          messageType: 'LOCATION',
          latitude: latitude,
          longitude: longitude,
          messageText: '', // CRITICAL: Never 'Shared Location' text
          sentAt: new Date().toISOString(),
          isSentByMe: true
        };

        chatState.addMessage(optimisticMsg, true);
        this.scrollToBottom(true);

        try {
          const res = await ChatApi.sendLocation(latitude, longitude, targetEmpId, clientMessageId);
          if (res && res.success && res.data) {
            chatState.reconcileOptimisticMessage(clientMessageId, res.data);
          } else {
            chatState.markMessageFailed(clientMessageId);
            if (res?.status !== 401) {
              this.showToast(res?.message || 'تعذر إرسال الموقع الجغرافي.', 'error');
            }
          }
        } catch {
          chatState.markMessageFailed(clientMessageId);
          if (!ChatApi.isHandlingSessionExpiry) {
            this.showToast('خطأ في الشبكة: فشل إرسال الموقع.', 'error');
          }
        }
      },
      (err) => {
        if (err.code === 1) { // PERMISSION_DENIED
          this.showToast('تم رفض إذن الوصول إلى الموقع. يرجى السماح به لمشاركة موقعك.', 'error');
        } else if (err.code === 2) { // POSITION_UNAVAILABLE
          this.showToast('تعذر تحديد موقعك الحالي.', 'error');
        } else if (err.code === 3) { // TIMEOUT
          this.showToast('انتهت مهلة طلب تحديد الموقع. يرجى المحاولة مرة أخرى.', 'error');
        } else {
          this.showToast('تعذر تحديد موقعك الحالي.', 'error');
        }
      },
      {
        enableHighAccuracy: true,
        timeout: 10000,
        maximumAge: 0
      }
    );
  }

  static async handleShareLiveLocation(durationMinutes = 15) {
    this.hideLocationPopover();

    if (!('geolocation' in navigator)) {
      this.showToast('خاصية تحديد الموقع الجغرافي غير مدعومة في متصفحك.', 'error');
      return;
    }

    const activeConv = chatState.activeConversation;
    if (!activeConv) {
      this.showToast('يرجى اختيار محادثة أولاً.', 'error');
      return;
    }

    this.showToast('جاري تحديد موقعك الجغرافي...', 'info');

    const currentUser = chatState.currentUser;
    const targetEmpId = activeConv.employeeId || activeConv.id;

    console.log('[LiveLocation] START', {
      durationMinutes,
      targetEmpId,
      senderId: currentUser?.id
    });

    navigator.geolocation.getCurrentPosition(
      async (pos) => {
        const latitude = pos.coords ? Number(pos.coords.latitude) : NaN;
        const longitude = pos.coords ? Number(pos.coords.longitude) : NaN;

        if (isNaN(latitude) || isNaN(longitude) || latitude < -90 || latitude > 90 || longitude < -180 || longitude > 180) {
          console.error('[LiveLocation] Invalid coordinates acquired:', { latitude, longitude });
          this.showToast('إحداثيات الموقع غير صالحة.', 'error');
          return;
        }

        console.log('[LiveLocation] LOCATION_ACQUIRED', {
          latitude,
          longitude,
          accuracy: pos.coords.accuracy
        });

        const clientMessageId = 'live_' + Date.now() + '_' + Math.random().toString(36).substring(2, 9);
        const conversationId = targetEmpId || (activeConv ? (activeConv.employeeId || activeConv.id) : null);
        const optimisticMsg = {
          tempId: clientMessageId,
          id: clientMessageId,
          clientMessageId: clientMessageId,
          senderId: currentUser.id,
          senderName: currentUser.fullName,
          senderRole: currentUser.role,
          messageType: 'LIVE_LOCATION',
          status: 'ACTIVE',
          conversationId: conversationId,
          latitude: latitude,
          longitude: longitude,
          isLiveActive: true,
          liveDurationMinutes: durationMinutes,
          liveStartedAt: new Date().toISOString(),
          startedAt: new Date().toISOString(),
          liveExpiresAt: new Date(Date.now() + durationMinutes * 60 * 1000).toISOString(),
          messageText: '', // CRITICAL: Never 'Live Location' text
          sentAt: new Date().toISOString(),
          isSentByMe: true
        };

        console.log('[LiveLocation] SEND', {
          messageType: 'LIVE_LOCATION',
          clientMessageId,
          conversationId,
          latitude,
          longitude,
          durationMinutes
        });

        chatState.addMessage(optimisticMsg, true);
        this.scrollToBottom(true);

        try {
          const res = await ChatApi.sendLiveLocation(latitude, longitude, durationMinutes, targetEmpId, clientMessageId);
          if (res && res.success && res.data) {
            console.log('[LiveLocation] SERVER_SUCCESS', {
              status: 201,
              messageId: res.data.id,
              sessionId: res.data.liveSessionId
            });
            chatState.reconcileOptimisticMessage(clientMessageId, res.data);
            const sessionId = res.data.liveSessionId;
            if (sessionId) {
              this.startTrackingLiveLocation(sessionId, durationMinutes);
              this.showToast(`تم تفعيل مشاركة الموقع الحي لمدة ${durationMinutes} دقيقة.`, 'success');
            }
          } else {
            console.error('[LiveLocation] SEND_FAILED', {
              status: res?.status || 400,
              error: res?.message || (res?.errors && res.errors.join('; ')) || 'Unknown error',
              payload: {
                messageType: 'LIVE_LOCATION',
                latitude,
                longitude,
                durationMinutes,
                targetEmpId,
                clientMessageId
              }
            });
            chatState.markMessageFailed(clientMessageId);
            if (res?.status !== 401) {
              this.showToast(res?.message || 'تعذر بدء مشاركة الموقع الحي.', 'error');
            }
          }
        } catch (err) {
          console.error('[LiveLocation] SEND_FAILED', {
            status: 0,
            error: err.message || 'Network error',
            payload: {
              messageType: 'LIVE_LOCATION',
              latitude,
              longitude,
              durationMinutes,
              targetEmpId,
              clientMessageId
            }
          });
          chatState.markMessageFailed(clientMessageId);
          if (!ChatApi.isHandlingSessionExpiry) {
            this.showToast('خطأ في الشبكة: فشل بدء مشاركة الموقع الحي.', 'error');
          }
        }
      },
      (err) => {
        if (err.code === 1) { // PERMISSION_DENIED
          this.showToast('تم رفض إذن الوصول إلى الموقع. يرجى السماح به لمشاركة موقعك.', 'error');
        } else if (err.code === 2) { // POSITION_UNAVAILABLE
          this.showToast('تعذر تحديد موقعك الحالي.', 'error');
        } else if (err.code === 3) { // TIMEOUT
          this.showToast('انتهت مهلة طلب تحديد الموقع. يرجى المحاولة مرة أخرى.', 'error');
        } else {
          this.showToast('تعذر تحديد موقعك الحالي.', 'error');
        }
      },
      {
        enableHighAccuracy: true,
        timeout: 10000,
        maximumAge: 0
      }
    );
  }

  static startTrackingLiveLocation(sessionId, durationMinutes) {
    if (this.activeWatchId) {
      navigator.geolocation.clearWatch(this.activeWatchId);
      this.activeWatchId = null;
    }
    if (this.activeWatchTimer) {
      clearTimeout(this.activeWatchTimer);
      this.activeWatchTimer = null;
    }

    this.activeWatchSessionId = sessionId;
    this.activeWatchExpiresAt = Date.now() + durationMinutes * 60 * 1000;
    this.lastSentLiveUpdate = Date.now();

    // Auto expiration timer
    this.activeWatchTimer = setTimeout(() => {
      this.stopLiveLocationSharing(sessionId);
      this.showToast('انتهت مدة مشاركة الموقع الحي.', 'info');
    }, durationMinutes * 60 * 1000);

    // Watch position
    this.activeWatchId = navigator.geolocation.watchPosition(
      (pos) => {
        if (Date.now() > this.activeWatchExpiresAt) {
          this.stopLiveLocationSharing(sessionId);
          return;
        }

        const now = Date.now();
        // Throttle updates: minimum 3 seconds between GPS updates
        if (now - this.lastSentLiveUpdate >= 3000) {
          this.lastSentLiveUpdate = now;
          const lat = pos.coords.latitude;
          const lng = pos.coords.longitude;
          ChatApi.updateLiveLocation(sessionId, lat, lng).catch(() => {});
        }
      },
      (err) => {
        console.warn('[Live Location] Tracking notice:', err);
      },
      {
        enableHighAccuracy: true,
        timeout: 10000,
        maximumAge: 2000
      }
    );
  }

  static async stopLiveLocationSharing(sessionId) {
    const sid = sessionId || this.activeWatchSessionId;
    if (this.activeWatchSessionId === sid || !sessionId) {
      if (this.activeWatchId) {
        navigator.geolocation.clearWatch(this.activeWatchId);
        this.activeWatchId = null;
      }
      if (this.activeWatchTimer) {
        clearTimeout(this.activeWatchTimer);
        this.activeWatchTimer = null;
      }
      this.activeWatchSessionId = null;
    }

    if (sid) {
      try {
        await ChatApi.stopLiveLocation(sid);
      } catch (err) {
        console.warn('Stop live location API call notice:', err);
      }
      chatState.endLiveLocation({ sessionId: sid, endedAt: new Date().toISOString() });
      this.showToast('تم إنهاء مشاركة الموقع الحي.');
    }
  }

  static stopTrackingOnSessionExpired() {
    if (this.activeWatchId) {
      try {
        navigator.geolocation.clearWatch(this.activeWatchId);
      } catch {}
      this.activeWatchId = null;
    }
    if (this.activeWatchTimer) {
      clearTimeout(this.activeWatchTimer);
      this.activeWatchTimer = null;
    }
    this.activeWatchSessionId = null;
  }

  static onLiveLocationUpdated(update) {
    if (!update || !update.sessionId) return;
    const sessionId = update.sessionId;
    const cardEl = document.getElementById(`live-card-${sessionId}`);
    if (cardEl) {
      const coordsTextEl = document.getElementById(`live-coords-text-${sessionId}`);
      if (coordsTextEl) {
        coordsTextEl.textContent = `${Number(update.latitude).toFixed(5)}, ${Number(update.longitude).toFixed(5)}`;
      }
    }
    window.FaceStoneMap?.updateLiveMarker(sessionId, update.latitude, update.longitude);
  }

  static onLiveLocationEnded(update) {
    if (!update || !update.sessionId) return;
    const sessionId = update.sessionId;
    const cardEl = document.getElementById(`live-card-${sessionId}`);
    if (cardEl) {
      cardEl.classList.remove('active-session');
      cardEl.classList.add('ended-session');

      const pill = cardEl.querySelector('.live-pill');
      if (pill) {
        pill.className = 'ended-pill';
        pill.textContent = 'منتهي';
      }

      const desc = cardEl.querySelector('.live-card-desc');
      if (desc) {
        desc.textContent = 'تم إنهاء مشاركة الموقع.';
      }

      const metaTimes = cardEl.querySelector('.live-card-meta-times');
      if (metaTimes && update.endedAt) {
        const endTimeStr = new Date(update.endedAt).toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' });
        metaTimes.innerHTML = `<span>بدأ</span> <span>انتهى ${endTimeStr}</span>`;
      }

      const stopBtn = cardEl.querySelector('.btn-stop-shared-loc');
      if (stopBtn) {
        stopBtn.remove();
      }

      const viewBtn = cardEl.querySelector('.btn-view-shared-loc');
      if (viewBtn) {
        viewBtn.textContent = 'عرض آخر موقع';
      }
    }

    if (this.activeWatchSessionId === sessionId) {
      if (this.activeWatchId) {
        navigator.geolocation.clearWatch(this.activeWatchId);
        this.activeWatchId = null;
      }
      if (this.activeWatchTimer) {
        clearTimeout(this.activeWatchTimer);
        this.activeWatchTimer = null;
      }
      this.activeWatchSessionId = null;
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
          <p>💬 لا توجد رسائل في هذه المحادثة حتى الآن.</p>
          <p style="font-size: 0.75rem; margin-top: 0.25rem;">اكتب رسالة في الأسفل لبدء المحادثة.</p>
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

    let contentHtml = '';
    const rawType = (msg.messageType || '').toUpperCase();
    const isLiveLocation = rawType === 'LIVE_LOCATION' || rawType === 'LIVELOCATION' || Boolean(msg.liveSessionId);
    const isStaticLocation = rawType === 'LOCATION' || (!isLiveLocation && msg.latitude != null && msg.longitude != null && !msg.attachment && (!msg.messageText || msg.messageText === 'Shared Location'));

    if (isLiveLocation) {
      console.log('[LiveLocation] RENDER', {
        messageType: 'LIVE_LOCATION',
        id: msg.id || correlationId,
        senderId: msg.senderId,
        conversationId: msg.conversationId,
        sessionId: msg.liveSessionId,
        latitude: msg.latitude,
        longitude: msg.longitude,
        status: msg.status || (msg.isLiveActive ? 'ACTIVE' : 'ENDED')
      });
      contentHtml = this.buildLiveLocationCardHtml(msg, isSent);
    } else if (isStaticLocation) {
      contentHtml = this.buildLocationCardHtml(msg, isSent);
    } else {
      // Normal text or attachment message ONLY
      if (msg.attachment) {
        contentHtml += this.buildAttachmentHtml(msg.attachment);
      }
      if (msg.messageText) {
        contentHtml += `<div class="msg-body-text">${this.escape(msg.messageText)}</div>`;
      }
    }

    let statusCheckmark = '';
    if (isSent) {
      if (isOptimistic) {
        statusCheckmark = '<span class="checkmark-icon pending" title="جاري الإرسال...">🕒</span>';
      } else if (msg.isFailed) {
        const retryKey = correlationId || msg.id;
        statusCheckmark = `<span class="msg-failed-retry" onclick="ChatApp.retryMessage('${retryKey}')">⚠️ فشل الإرسال • إعادة المحاولة ↻</span>`;
      } else if (msg.isReadByAccountant || msg.isReadByUser) {
        statusCheckmark = '<span class="checkmark-icon read" title="تمت القراءة">✓✓</span>';
      } else {
        statusCheckmark = '<span class="checkmark-icon sent" title="تم الإرسال">✓✓</span>';
      }
    }

    const isLocationMsg = isLiveLocation || isStaticLocation;

    return `
      <div class="msg-bubble-row ${isSent ? 'sent' : 'received'} ${isLocationMsg ? 'location-row' : ''}" id="${bubbleId}">
        <div class="msg-bubble ${isLocationMsg ? 'location-bubble' : ''}">
          ${!isSent ? `<div class="msg-sender-name">${this.escape(msg.senderName)}</div>` : ''}
          ${contentHtml}
          <div class="msg-meta-row">
            <span>${timeStr}</span>
            ${statusCheckmark}
          </div>
        </div>
      </div>
    `;
  }

  static buildLocationCardHtml(msg, isSent) {
    const lat = msg.latitude != null ? Number(msg.latitude) : 0;
    const lng = msg.longitude != null ? Number(msg.longitude) : 0;
    const senderName = msg.senderName || 'المرسل';
    const rawSender = senderName.replace(/'/g, "\\'");

    return `
      <div class="chat-location-card">
        <div class="loc-card-header">
          <span class="loc-card-badge">📍 موقع مُشارك</span>
        </div>
        <div class="loc-card-body">
          <div class="loc-card-main-label">الموقع الجغرافي الحالي</div>
          <div class="loc-card-coords font-numeric" dir="ltr">${lat.toFixed(5)}, ${lng.toFixed(5)}</div>
        </div>
        <div class="loc-card-actions">
          <button type="button" class="btn-view-shared-loc" onclick="FaceStoneMap.openLocationViewer(${lat}, ${lng}, 'موقع: ${rawSender}')">
            عرض على الخريطة
          </button>
        </div>
      </div>
    `;
  }

  static buildLiveLocationCardHtml(msg, isSent) {
    const lat = msg.latitude != null ? Number(msg.latitude) : 0;
    const lng = msg.longitude != null ? Number(msg.longitude) : 0;
    const senderName = msg.senderName || 'المرسل';
    const escapedSender = this.escape(senderName);
    const rawSender = senderName.replace(/'/g, "\\'");
    const sessionId = msg.liveSessionId || '';
    const isActive = (msg.status === 'ACTIVE' || msg.isLiveActive === true) && msg.status !== 'ENDED';

    const startTimeStr = msg.liveStartedAt 
      ? new Date(msg.liveStartedAt).toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' }) 
      : (msg.startedAt ? new Date(msg.startedAt).toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' }) : (msg.sentAt ? new Date(msg.sentAt).toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' }) : ''));

    const endTimeStr = msg.liveEndedAt 
      ? new Date(msg.liveEndedAt).toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' }) 
      : (msg.endedAt ? new Date(msg.endedAt).toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' }) : (msg.liveExpiresAt ? new Date(msg.liveExpiresAt).toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' }) : ''));

    const currentUser = chatState.currentUser;
    const isSentByMe = currentUser && (msg.senderId === currentUser.id || msg.isSentByMe);

    return `
      <div class="chat-location-card live-card ${isActive ? 'active-session' : 'ended-session'}" id="live-card-${sessionId}">
        <div class="loc-card-header">
          <span class="loc-card-badge">📍 موقع مباشر حي</span>
          ${isActive 
            ? `<span class="live-pill"><span class="live-pulse">●</span> مباشر</span>`
            : `<span class="ended-pill">منتهي</span>`}
        </div>
        <div class="loc-card-body">
          <div class="live-card-desc">
            ${isActive 
              ? `${escapedSender} يشارك موقعه المباشر حالياً.` 
              : `تم إنهاء مشاركة الموقع.`}
          </div>
          <div class="live-card-meta-times">
            <span>بدأ ${startTimeStr}</span>
            ${!isActive && endTimeStr ? `<span>• انتهى ${endTimeStr}</span>` : ''}
          </div>
          <div class="loc-card-coords font-numeric" dir="ltr" id="live-coords-text-${sessionId}">${lat.toFixed(5)}, ${lng.toFixed(5)}</div>
        </div>
        <div class="loc-card-actions">
          <button type="button" class="btn-view-shared-loc" onclick="FaceStoneMap.openLocationViewer(${lat}, ${lng}, 'موقع مباشر: ${rawSender}', '${sessionId}')">
            ${isActive ? 'عرض الموقع الحي' : 'عرض آخر موقع'}
          </button>
          ${isActive && isSentByMe && sessionId ? `
            <button type="button" class="btn-stop-shared-loc" onclick="ChatApp.stopLiveLocationSharing('${sessionId}')">
              🛑 إيقاف المشاركة
            </button>
          ` : ''}
        </div>
      </div>
    `;
  }

  static buildAttachmentHtml(att) {
    if (!att) return '';
    const fileName = att.fileName || 'مرفق';
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
              <span>🔍 انقر للمعاينة</span>
            </div>
          </div>
          <div class="attachment-actions-bar">
            <span class="attachment-meta-info" title="${escapedFileName}">${escapedFileName}</span>
            <button 
              type="button" 
              class="attachment-dl-btn" 
              onclick="event.stopPropagation(); ChatApp.downloadAttachment('${attId}', '${rawFileName}', '${displayUrl}')"
              title="تحميل الصورة"
            >
              <svg width="13" height="13" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.5"><path d="M21 15v4a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2v-4"/><polyline points="7 10 12 15 17 10"/><line x1="12" y1="15" x2="12" y2="3"/></svg>
              تحميل
            </button>
          </div>
        </div>
      `;
    }

    // Document / non-image attachment card
    const fileSizeStr = att.fileSizeBytes ? this.formatBytes(att.fileSizeBytes) : 'ملف';
    return `
      <div 
        class="msg-attachment-card" 
        onclick="ChatApp.downloadAttachment('${attId}', '${rawFileName}', '${displayUrl}')"
        title="انقر لتحميل ${escapedFileName}"
      >
        <div class="attachment-icon-box">
          <svg width="22" height="22" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><path d="M14 2H6a2 2 0 0 0-2 2v16a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V8z"/><polyline points="14 2 14 8 20 8"/></svg>
        </div>
        <div class="attachment-info">
          <span class="attachment-filename">${escapedFileName}</span>
          <span class="attachment-filesize">${fileSizeStr}</span>
        </div>
        <div class="card-dl-icon-btn" title="تحميل">
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
      const confType = (confirmedMsg.messageType || '').toUpperCase();
      if (confirmedMsg.attachment || confType === 'LOCATION' || confType === 'LIVE_LOCATION' || Boolean(confirmedMsg.liveSessionId)) {
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
    if (this.lightboxFilename) this.lightboxFilename.textContent = fileName || 'معاينة الصورة';
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
      this.showToast(`جاري تحميل ${fileName || 'الملف'}...`);
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
      this.showToast('فشل التحميل. يرجى المحاولة مرة أخرى.', 'error');
    }
  }

  static async handleImgError(imgEl, attId) {
    if (!imgEl || imgEl.dataset.fallbackTried === 'true') {
      if (imgEl && imgEl.parentElement) {
        imgEl.parentElement.innerHTML = `
          <div style="padding: 1.5rem; text-align: center; color: var(--text-muted); font-size: 0.8125rem;">
            ⚠️ تعذر تحميل معاينة الصورة
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
            ⚠️ تعذر تحميل معاينة الصورة
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
        meta.innerHTML = `<span class="msg-failed-retry" onclick="ChatApp.retryMessage('${tempId}')">⚠️ فشل الإرسال • إعادة المحاولة ↻</span>`;
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
      this.showToast('يرجى اختيار محادثة أولاً.', 'error');
      return;
    }

    const currentUser = chatState.currentUser;
    const targetEmpId = activeConv.employeeId || activeConv.id;

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
        if (res?.status !== 401) {
          this.showToast(res?.message || 'تعذر تسليم الرسالة.', 'error');
        }
      }
    } catch {
      chatState.markMessageFailed(clientMessageId);
      if (!ChatApi.isHandlingSessionExpiry) {
        this.showToast('خطأ في الشبكة: فشل إرسال الرسالة.', 'error');
      }
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
      if (meta) meta.innerHTML = '<span>جاري الإرسال...</span> <span class="checkmark-icon pending">🕒</span>';
    }

    const activeConv = chatState.activeConversation;
    const targetEmpId = activeConv?.employeeId || activeConv?.id;

    // Reuse the exact same Client Correlation ID to ensure server-side idempotency
    const correlationId = msg.clientMessageId || tempId;
    const rawType = (msg.messageType || '').toUpperCase();
    const isLive = rawType === 'LIVE_LOCATION' || rawType === 'LIVELOCATION' || Boolean(msg.liveSessionId);
    const isLocation = rawType === 'LOCATION';

    try {
      let res;
      if (isLive) {
        console.log('[LiveLocation] SEND (retry)', {
          messageType: 'LIVE_LOCATION',
          correlationId,
          latitude: msg.latitude,
          longitude: msg.longitude
        });
        res = await ChatApi.sendLiveLocation(
          msg.latitude,
          msg.longitude,
          msg.liveDurationMinutes || 15,
          targetEmpId,
          correlationId
        );
      } else if (isLocation) {
        res = await ChatApi.sendLocation(msg.latitude, msg.longitude, targetEmpId, correlationId);
      } else {
        res = await ChatApi.sendMessage(msg.messageText, null, targetEmpId, correlationId);
      }

      if (res && res.success && res.data) {
        if (isLive) {
          console.log('[LiveLocation] SERVER_SUCCESS', {
            status: 201,
            sessionId: res.data.liveSessionId
          });
        }
        chatState.reconcileOptimisticMessage(correlationId, res.data);
        if (isLive && res.data.liveSessionId) {
          this.startTrackingLiveLocation(res.data.liveSessionId, msg.liveDurationMinutes || 15);
        }
      } else {
        if (isLive) {
          console.error('[LiveLocation] SEND_FAILED', {
            status: res?.status || 400,
            error: res?.message || 'Retry failed',
            payload: { messageType: 'LIVE_LOCATION', correlationId }
          });
        }
        chatState.markMessageFailed(correlationId);
        if (res?.status !== 401) {
          this.showToast(res?.message || 'فشلت إعادة المحاولة.', 'error');
        }
      }
    } catch (err) {
      if (isLive) {
        console.error('[LiveLocation] SEND_FAILED', {
          status: 0,
          error: err.message || 'Network error on retry',
          payload: { messageType: 'LIVE_LOCATION', correlationId }
        });
      }
      chatState.markMessageFailed(correlationId);
      if (!ChatApi.isHandlingSessionExpiry) {
        this.showToast('خطأ في الشبكة: فشلت إعادة المحاولة.', 'error');
      }
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
    if (show) {
      this.showView('auth');
    } else {
      if (this.authModal) {
        this.authModal.classList.remove('active');
        this.authModal.style.display = 'none';
      }
    }
  }

  static updateConnectionPill(state) {
    if (!this.connectionPill) return;
    const dot = this.connectionPill.querySelector('.connection-dot');
    const text = this.connectionPill.querySelector('span:last-child');

    if (state === 'connected') {
      if (dot) dot.className = 'connection-dot';
      if (text) text.textContent = 'متصل الآن';
      this.connectionPill.title = 'تم الاتصال المباشر بنجاح عبر SignalR';
    } else if (state === 'reconnecting') {
      if (dot) dot.className = 'connection-dot connecting';
      if (text) text.textContent = 'جاري إعادة الاتصال...';
      this.connectionPill.title = 'جاري إعادة الاتصال بخادم المحادثة...';
    } else if (state === 'connecting') {
      if (dot) dot.className = 'connection-dot connecting';
      if (text) text.textContent = 'جاري الاتصال...';
      this.connectionPill.title = 'جاري إنشاء اتصال مباشر...';
    } else {
      if (dot) dot.className = 'connection-dot offline';
      if (text) text.textContent = 'غير متصل';
      this.connectionPill.title = 'غير متصل بالخادم. التحديث التلقائي نشط.';
    }
  }

  static setBtnLoading(btn, isLoading) {
    if (!btn) return;
    if (isLoading) {
      btn.dataset.orig = btn.innerHTML;
      btn.innerHTML = 'جاري التحقق...';
      btn.disabled = true;
    } else {
      btn.innerHTML = btn.dataset.orig || 'إرسال';
      btn.disabled = false;
    }
  }

  static showToast(message, type = 'info') {
    const container = document.getElementById('chat-toast-container');
    if (!container || !message) return;

    // Deduplication check: if a toast with identical text is already displayed in container, ignore
    const existing = Array.from(container.querySelectorAll('.chat-toast-pill')).find(p => p.textContent === message);
    if (existing) return;

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
    if (!iso) return 'اليوم';
    const d = new Date(iso);
    const now = new Date();
    if (d.toDateString() === now.toDateString()) return 'اليوم';
    const yesterday = new Date(now);
    yesterday.setDate(now.getDate() - 1);
    if (d.toDateString() === yesterday.toDateString()) return 'أمس';
    return d.toLocaleDateString('ar-EG', { weekday: 'short', month: 'short', day: 'numeric' });
  }

  static formatBytes(bytes) {
    if (!bytes || bytes === 0) return '0 بايت';
    const k = 1024;
    const sizes = ['بايت', 'ك.ب', 'م.ب', 'ج.ب'];
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
