/* ==========================================================================
   FACESTONE REAL-TIME CHAT — REACTIVE STATE STORE
   Tracks Active User, Conversations, Messages, Correlation IDs & Optimistic UI
   ========================================================================== */

class ChatStateStore {
  constructor() {
    this.currentUser = null;
    this.activeConversation = null; // { id, name, role, isDesk }
    this.conversations = [];
    this.messages = [];
    this.seenMessageIds = new Set();
    this.seenClientMessageIds = new Set();
    this.pendingOptimisticMessages = new Map(); // key: clientMessageId/tempId -> message object
    this.searchQuery = '';
  }

  init(user) {
    this.currentUser = user;
    this.activeConversation = null;
    this.conversations = [];
    this.messages = [];
    this.seenMessageIds.clear();
    this.seenClientMessageIds.clear();
    this.pendingOptimisticMessages.clear();
    this.searchQuery = '';
  }

  isAccountantUser() {
    const role = this.currentUser?.role;
    return role === 'Accountant' || role === 'CEO';
  }

  setConversations(list) {
    this.conversations = list || [];
    this.emitChange('conversations:updated');
  }

  getFilteredConversations() {
    if (!this.searchQuery.trim()) return this.conversations;
    const q = this.searchQuery.trim().toLowerCase();
    return this.conversations.filter(c => 
      c.fullName.toLowerCase().includes(q) ||
      c.username.toLowerCase().includes(q) ||
      (c.lastMessageText && c.lastMessageText.toLowerCase().includes(q))
    );
  }

  setActiveConversation(conv) {
    this.activeConversation = conv;
    this.messages = [];
    this.seenMessageIds.clear();
    this.seenClientMessageIds.clear();
    this.pendingOptimisticMessages.clear();
    this.emitChange('activeConversation:changed', conv);
  }

  setMessages(messagesList) {
    this.messages = [];
    this.seenMessageIds.clear();
    this.seenClientMessageIds.clear();
    this.pendingOptimisticMessages.clear();

    (messagesList || []).forEach(m => {
      if (m && m.id && !this.seenMessageIds.has(m.id)) {
        this.seenMessageIds.add(m.id);
        if (m.clientMessageId) {
          this.seenClientMessageIds.add(m.clientMessageId);
        }
        this.messages.push(m);
      }
    });

    this.emitChange('messages:updated', this.messages);
  }

  addMessage(msg, isOptimistic = false) {
    if (!msg) return;

    if (isOptimistic) {
      const correlationId = msg.clientMessageId || msg.tempId;
      if (correlationId) {
        this.pendingOptimisticMessages.set(correlationId, msg);
        this.seenClientMessageIds.add(correlationId);
      }
      this.messages.push(msg);
      this.emitChange('messages:added', { message: msg, isOptimistic: true });
      return;
    }

    // Incoming confirmed message (from SignalR broadcast or Polling fallback)
    const correlationId = msg.clientMessageId;

    // Check if this incoming message matches a pending optimistic message sent by this tab
    if (correlationId && this.pendingOptimisticMessages.has(correlationId)) {
      this.reconcileOptimisticMessage(correlationId, msg);
      return;
    }

    // Deduplication check: Discard if already rendered by server ID or correlation ID
    if (msg.id && this.seenMessageIds.has(msg.id)) {
      return;
    }
    if (correlationId && this.seenClientMessageIds.has(correlationId)) {
      return;
    }

    if (msg.id) this.seenMessageIds.add(msg.id);
    if (correlationId) this.seenClientMessageIds.add(correlationId);

    this.messages.push(msg);
    this.emitChange('messages:added', { message: msg, isOptimistic: false });
  }

  reconcileOptimisticMessage(correlationId, confirmedMsg) {
    if (!correlationId || !confirmedMsg) return;

    this.pendingOptimisticMessages.delete(correlationId);
    if (confirmedMsg.id) this.seenMessageIds.add(confirmedMsg.id);
    if (confirmedMsg.clientMessageId) this.seenClientMessageIds.add(confirmedMsg.clientMessageId);

    const idx = this.messages.findIndex(m => 
      m.clientMessageId === correlationId || m.tempId === correlationId || m.id === correlationId
    );

    if (idx !== -1) {
      this.messages[idx] = confirmedMsg;
      this.emitChange('messages:reconciled', { tempId: correlationId, confirmedMsg });
    }
  }

  markMessageFailed(correlationId) {
    const msg = this.messages.find(m => 
      m.clientMessageId === correlationId || m.tempId === correlationId || m.id === correlationId
    );
    if (msg) {
      msg.isFailed = true;
      this.emitChange('messages:failed', { tempId: correlationId });
    }
  }

  updateConversationSnippet(empId, lastText, sentAt, incrementUnread = false) {
    const conv = this.conversations.find(c => c.employeeId === empId);
    if (conv) {
      conv.lastMessageText = lastText;
      conv.lastMessageTime = sentAt;
      if (incrementUnread) conv.unreadCount = (conv.unreadCount || 0) + 1;

      // Sort: active or newest first
      this.conversations.sort((a, b) => {
        const timeA = a.lastMessageTime ? new Date(a.lastMessageTime).getTime() : 0;
        const timeB = b.lastMessageTime ? new Date(b.lastMessageTime).getTime() : 0;
        return timeB - timeA;
      });

      this.emitChange('conversations:updated');
    }
  }

  emitChange(eventName, data = null) {
    window.dispatchEvent(new CustomEvent(`chat:state:${eventName}`, { detail: data }));
  }
}

window.chatState = new ChatStateStore();
