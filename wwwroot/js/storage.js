/**
 * FaceStone Storage Department Management - Arabic RTL Application Module
 * Redesigned Storage Transaction Workflow:
 *  - Section 1: النوع (Material: powder/slurry, Action: import/export, Incoming/Outgoing Quantity, Export PO Linkage)
 *  - Section 2: التاريخ (Transaction Date & Time)
 *  - Section 3: الميزان (Internal Weighbridge, External Weighbridge Toggle & Fields, Receipt Attachment)
 *  - Section 4: الموظف المسؤول (Controlled selection from existing Users database)
 */

(function () {
  'use strict';

  class StorageManager {
    constructor() {
      this.isSubmitting = false;
      this.initialized = false;
      this.config = null;
      this.recentTransactions = [];
      this.currentReceiptFile = null;
      this.poCheckTimeout = null;
      this.poValidated = false;
      this.init();
    }

    init() {
      if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', () => this.bindElements());
      } else {
        this.bindElements();
      }
    }

    bindElements() {
      this.pageRoot = document.getElementById('storage-page-root');
      if (!this.pageRoot) return;

      // Header Elements
      this.btnBackToDepts = document.getElementById('storage-back-to-departments-btn');
      this.userNameEl = document.getElementById('storage-user-name');
      this.userRoleEl = document.getElementById('storage-user-role');
      this.userAvatarEl = document.getElementById('storage-user-avatar');

      // Banner / Alert
      this.alertBanner = document.getElementById('storage-alert-banner');
      this.alertIcon = document.getElementById('storage-alert-icon');
      this.alertTitle = document.getElementById('storage-alert-title');
      this.alertMsg = document.getElementById('storage-alert-msg');
      this.alertCloseBtn = document.getElementById('storage-alert-close-btn');

      // Main Form
      this.form = document.getElementById('storage-entry-form');

      // SECTION 1 — TYPE (النوع)
      this.materialSelect = document.getElementById('storage-material-select');
      this.actionSelect = document.getElementById('storage-action-select');

      this.incomingGroup = document.getElementById('group-storage-incoming-qty');
      this.incomingQtyInput = document.getElementById('storage-incoming-qty');

      this.outgoingGroup = document.getElementById('group-storage-outgoing-qty');
      this.outgoingQtyInput = document.getElementById('storage-outgoing-qty');

      this.poContainer = document.getElementById('group-storage-po-container');
      this.exportPoCheckbox = document.getElementById('storage-export-po-checkbox');
      this.poInputWrap = document.getElementById('storage-po-input-wrap');
      this.poNumberInput = document.getElementById('storage-po-number');
      this.poStatusEl = document.getElementById('storage-po-status');

      // SECTION 2 — DATE (التاريخ)
      this.dateInput = document.getElementById('storage-date');

      // SECTION 3 — WEIGHBRIDGE (الميزان)
      // Internal Weighbridge
      this.internalRefInput = document.getElementById('storage-wb-int-ref');
      this.internalLoadedInput = document.getElementById('storage-wb-int-loaded');
      this.internalEmptyInput = document.getElementById('storage-wb-int-empty');
      this.internalNetInput = document.getElementById('storage-wb-int-net');
      this.internalEmptyTimeInput = document.getElementById('storage-wb-int-empty-time');
      this.internalLoadedTimeInput = document.getElementById('storage-wb-int-loaded-time');

      // External Weighbridge Toggle & Fields
      this.hasExtWbCheckbox = document.getElementById('storage-has-ext-wb-checkbox');
      this.extWbCard = document.getElementById('storage-ext-wb-card');
      this.extRefInput = document.getElementById('storage-wb-ext-ref');
      this.extLoadedInput = document.getElementById('storage-wb-ext-loaded');
      this.extEmptyInput = document.getElementById('storage-wb-ext-empty');
      this.extNetInput = document.getElementById('storage-wb-ext-net');
      this.extEmptyTimeInput = document.getElementById('storage-wb-ext-empty-time');
      this.extLoadedTimeInput = document.getElementById('storage-wb-ext-loaded-time');

      // Weighbridge Receipt Attachment
      this.receiptSection = document.getElementById('storage-receipt-attachment-section');
      this.receiptRequiredStar = document.getElementById('storage-receipt-required-star');
      this.dropzone = document.getElementById('storage-wb-dropzone');
      this.fileInput = document.getElementById('storage-wb-attachment-input');
      this.placeholder = document.getElementById('storage-wb-placeholder');
      this.filePreview = document.getElementById('storage-wb-file-preview');
      this.fileNameEl = document.getElementById('storage-wb-file-name');
      this.fileSizeEl = document.getElementById('storage-wb-file-size');
      this.fileIconEl = document.getElementById('storage-wb-file-icon');
      this.btnRemoveFile = document.getElementById('storage-wb-remove-btn');

      // SECTION 4 — RESPONSIBLE EMPLOYEE (الموظف المسؤول)
      this.responsibleEmployeeSelect = document.getElementById('storage-responsible-employee');
      this.notesTextarea = document.getElementById('storage-notes');

      // Form Action Buttons
      this.btnSubmit = document.getElementById('btn-storage-submit');
      this.submitSpinner = document.getElementById('storage-submit-spinner');
      this.submitText = document.getElementById('storage-submit-text');
      this.btnClear = document.getElementById('btn-storage-clear');

      // Clear Confirmation Modal
      this.confirmModal = document.getElementById('storage-confirm-modal');
      this.confirmCancelBtn = document.getElementById('storage-confirm-cancel-btn');
      this.confirmProceedBtn = document.getElementById('storage-confirm-proceed-btn');

      // Recent Transactions Table
      this.tableBody = document.getElementById('storage-table-body');
      this.btnRefresh = document.getElementById('btn-storage-refresh');

      this.attachEventListeners();
      this.setDefaultDateTime();
      this.handleActionChange();
      this.handleExternalWbToggle();
      this.initialized = true;
    }

    attachEventListeners() {
      // Back to departments button
      this.btnBackToDepts?.addEventListener('click', () => {
        if (window.FaceStoneApp) {
          window.FaceStoneApp.backToDepartments();
        } else {
          window.location.hash = '#/departments';
        }
      });

      // Alert close
      this.alertCloseBtn?.addEventListener('click', () => {
        this.hideAlert();
      });

      // Section 1: Material change
      this.materialSelect?.addEventListener('change', () => {
        this.clearFieldError('storage-material-select');
      });

      // Section 1: Action change (Import vs Export)
      this.actionSelect?.addEventListener('change', () => {
        this.clearFieldError('storage-action-select');
        this.handleActionChange();
      });

      // Section 1: Export PO Checkbox change
      this.exportPoCheckbox?.addEventListener('change', () => {
        this.handleExportPoToggle();
      });

      // Section 1: PO Number input with live validation
      this.poNumberInput?.addEventListener('input', () => {
        clearTimeout(this.poCheckTimeout);
        this.clearFieldError('storage-po-number');
        this.poCheckTimeout = setTimeout(() => this.validatePoNumberLive(), 450);
      });
      this.poNumberInput?.addEventListener('blur', () => {
        this.validatePoNumberLive();
      });

      // Section 2: Date change
      this.dateInput?.addEventListener('change', () => {
        this.clearFieldError('storage-date');
      });

      // Section 3: Internal Weighbridge live net calculation
      this.internalLoadedInput?.addEventListener('input', () => this.calculateInternalNet());
      this.internalEmptyInput?.addEventListener('input', () => this.calculateInternalNet());
      this.internalRefInput?.addEventListener('input', () => this.clearFieldError('storage-wb-int-ref'));
      this.internalLoadedTimeInput?.addEventListener('change', () => this.clearFieldError('storage-wb-int-loaded-time'));

      // Section 3: External Weighbridge checkbox toggle
      this.hasExtWbCheckbox?.addEventListener('change', () => {
        this.handleExternalWbToggle();
      });

      // Section 3: External Weighbridge live net calculation
      this.extLoadedInput?.addEventListener('input', () => this.calculateExternalNet());
      this.extEmptyInput?.addEventListener('input', () => this.calculateExternalNet());
      this.extRefInput?.addEventListener('input', () => this.clearFieldError('storage-wb-ext-ref'));
      this.extLoadedTimeInput?.addEventListener('change', () => this.clearFieldError('storage-wb-ext-loaded-time'));

      // Section 3: Weighbridge Receipt File Attachment
      this.setupAttachmentListeners();

      // Section 4: Responsible Employee change
      this.responsibleEmployeeSelect?.addEventListener('change', () => {
        this.clearFieldError('storage-responsible-employee');
      });

      // Form Submit
      this.form?.addEventListener('submit', (e) => {
        e.preventDefault();
        this.handleFormSubmit();
      });

      // Clear Button
      this.btnClear?.addEventListener('click', () => {
        this.handleClearRequest();
      });

      // Confirmation Modal buttons
      this.confirmCancelBtn?.addEventListener('click', () => {
        this.hideConfirmModal();
      });

      this.confirmProceedBtn?.addEventListener('click', () => {
        this.hideConfirmModal();
        this.resetForm();
      });

      // Refresh recent transactions
      this.btnRefresh?.addEventListener('click', () => {
        this.loadRecentTransactions();
      });
    }

    // =========================================================================
    // Attachment Handling (Drag & Drop, Validation, Preview)
    // =========================================================================
    setupAttachmentListeners() {
      if (!this.dropzone) return;

      // File input change
      this.fileInput?.addEventListener('change', (e) => {
        const file = e.target.files?.[0];
        if (file) {
          this.handleSelectedReceiptFile(file);
        }
      });

      // Drag and drop
      ['dragenter', 'dragover'].forEach(eventName => {
        this.dropzone.addEventListener(eventName, (e) => {
          e.preventDefault();
          e.stopPropagation();
          this.dropzone.classList.add('dragover');
        });
      });

      ['dragleave', 'drop'].forEach(eventName => {
        this.dropzone.addEventListener(eventName, (e) => {
          e.preventDefault();
          e.stopPropagation();
          this.dropzone.classList.remove('dragover');
        });
      });

      this.dropzone.addEventListener('drop', (e) => {
        const file = e.dataTransfer?.files?.[0];
        if (file) {
          this.handleSelectedReceiptFile(file);
        }
      });

      // Remove button
      this.btnRemoveFile?.addEventListener('click', (e) => {
        e.stopPropagation();
        this.clearReceiptAttachment();
      });
    }

    handleSelectedReceiptFile(file) {
      this.clearFieldError('storage-wb-receipt');
      if (!file) return;

      // Validate format
      const validExtensions = ['.pdf', '.jpg', '.jpeg'];
      const fileNameLower = file.name.toLowerCase();
      const isValidExt = validExtensions.some(ext => fileNameLower.endsWith(ext));

      if (!isValidExt) {
        this.showFieldError('storage-wb-receipt', 'الملفات المسموحة فقط: PDF, JPG, JPEG.');
        this.clearReceiptAttachment();
        return;
      }

      // Validate size (max 25 MB)
      const maxBytes = 25 * 1024 * 1024;
      if (file.size > maxBytes) {
        this.showFieldError('storage-wb-receipt', 'حجم الملف يتجاوز الحد الأقصى المسموح به (25 ميجابايت).');
        this.clearReceiptAttachment();
        return;
      }

      this.currentReceiptFile = file;

      // Update Preview
      if (this.fileNameEl) this.fileNameEl.textContent = file.name;
      if (this.fileSizeEl) this.fileSizeEl.textContent = this.formatFileSize(file.size);
      if (this.fileIconEl) {
        this.fileIconEl.textContent = fileNameLower.endsWith('.pdf') ? '📄' : '🖼️';
      }

      if (this.placeholder) this.placeholder.style.display = 'none';
      if (this.filePreview) this.filePreview.style.display = 'flex';
    }

    clearReceiptAttachment() {
      this.currentReceiptFile = null;
      if (this.fileInput) this.fileInput.value = '';
      if (this.placeholder) this.placeholder.style.display = 'flex';
      if (this.filePreview) this.filePreview.style.display = 'none';
      if (this.fileNameEl) this.fileNameEl.textContent = '—';
      if (this.fileSizeEl) this.fileSizeEl.textContent = '—';
    }

    formatFileSize(bytes) {
      if (!bytes || bytes === 0) return '0 بايت';
      const k = 1024;
      const sizes = ['بايت', 'كيلوبايت', 'ميجابايت', 'جيجابايت'];
      const i = Math.floor(Math.log(bytes) / Math.log(k));
      return parseFloat((bytes / Math.pow(k, i)).toFixed(2)) + ' ' + sizes[i];
    }

    // =========================================================================
    // Reactive Form Dynamics
    // =========================================================================
    setDefaultDateTime() {
      const now = new Date();
      const yyyy = now.getFullYear();
      const mm = String(now.getMonth() + 1).padStart(2, '0');
      const dd = String(now.getDate()).padStart(2, '0');
      const hh = String(now.getHours()).padStart(2, '0');
      const min = String(now.getMinutes()).padStart(2, '0');
      const formatted = `${yyyy}-${mm}-${dd}T${hh}:${min}`;

      if (this.dateInput && !this.dateInput.value) {
        this.dateInput.value = formatted;
      }
      if (this.internalLoadedTimeInput && !this.internalLoadedTimeInput.value) {
        this.internalLoadedTimeInput.value = formatted;
      }
    }

    handleActionChange() {
      const action = (this.actionSelect?.value || '').toLowerCase();

      // Clear quantity validation errors
      this.clearFieldError('storage-incoming-qty');
      this.clearFieldError('storage-outgoing-qty');
      this.clearFieldError('storage-po-number');

      if (action === 'import' || action === 'إيداع') {
        // Show Incoming Quantity, Hide Outgoing Quantity & PO Checkbox
        if (this.incomingGroup) this.incomingGroup.style.display = 'flex';
        if (this.outgoingGroup) this.outgoingGroup.style.display = 'none';
        if (this.outgoingQtyInput) this.outgoingQtyInput.value = '';

        if (this.poContainer) this.poContainer.style.display = 'none';
        if (this.exportPoCheckbox) this.exportPoCheckbox.checked = false;
        if (this.poInputWrap) this.poInputWrap.style.display = 'none';
        if (this.poNumberInput) this.poNumberInput.value = '';
        if (this.poStatusEl) this.poStatusEl.textContent = '';
        this.poValidated = false;
      } else if (action === 'export' || action === 'صرف') {
        // Show Outgoing Quantity & PO Checkbox, Hide Incoming Quantity
        if (this.outgoingGroup) this.outgoingGroup.style.display = 'flex';
        if (this.incomingGroup) this.incomingGroup.style.display = 'none';
        if (this.incomingQtyInput) this.incomingQtyInput.value = '';

        if (this.poContainer) this.poContainer.style.display = 'flex';
        this.handleExportPoToggle();
      } else {
        // Neither selected
        if (this.incomingGroup) this.incomingGroup.style.display = 'none';
        if (this.outgoingGroup) this.outgoingGroup.style.display = 'none';
        if (this.poContainer) this.poContainer.style.display = 'none';
      }
    }

    handleExportPoToggle() {
      const isChecked = this.exportPoCheckbox?.checked || false;
      if (this.poInputWrap) {
        this.poInputWrap.style.display = isChecked ? 'block' : 'none';
      }

      if (!isChecked) {
        if (this.poNumberInput) this.poNumberInput.value = '';
        if (this.poStatusEl) this.poStatusEl.textContent = '';
        this.clearFieldError('storage-po-number');
        this.poValidated = false;
      } else {
        if (this.poNumberInput && !this.poNumberInput.value.trim()) {
          this.poNumberInput.focus();
        } else {
          this.validatePoNumberLive();
        }
      }
    }

    async validatePoNumberLive() {
      if (!this.exportPoCheckbox?.checked) return true;
      const rawPo = this.poNumberInput?.value?.trim() || '';

      if (!rawPo) {
        if (this.poStatusEl) this.poStatusEl.textContent = '';
        this.poValidated = false;
        return false;
      }

      // Clean prefix if entered without "PO-"
      let poQuery = rawPo;
      if (!poQuery.toUpperCase().startsWith('PO-') && !poQuery.toUpperCase().startsWith('PO')) {
        poQuery = 'PO-' + poQuery;
      }

      if (this.poStatusEl) {
        this.poStatusEl.textContent = '⏳ جاري التحقق...';
        this.poStatusEl.className = 'storage-ref-status';
      }

      try {
        const res = await fetch(`/api/storage/po-lookup/${encodeURIComponent(poQuery)}`, {
          headers: {
            'Accept': 'application/json',
            'Authorization': 'Bearer ' + (window.ChatApi?.getToken() || '')
          }
        });

        const json = await res.json();
        if (res.ok && json.success && json.data) {
          const po = json.data;
          this.poNumberInput.classList.remove('is-invalid');
          this.clearFieldError('storage-po-number');
          if (this.poStatusEl) {
            this.poStatusEl.textContent = `✓ معتمد: ${po.poNumber} (${po.supplier || 'مورد'})`;
            this.poStatusEl.className = 'storage-ref-status valid';
          }
          this.poValidated = true;
          return true;
        } else {
          this.poNumberInput.classList.add('is-invalid');
          this.showFieldError('storage-po-number', 'Purchase Order not found.');
          if (this.poStatusEl) {
            this.poStatusEl.textContent = '❌ Purchase Order not found.';
            this.poStatusEl.className = 'storage-ref-status invalid';
          }
          this.poValidated = false;
          return false;
        }
      } catch (err) {
        console.error('PO verification error:', err);
        return false;
      }
    }

    handleExternalWbToggle() {
      const isChecked = this.hasExtWbCheckbox?.checked || false;
      if (this.extWbCard) {
        this.extWbCard.style.display = isChecked ? 'block' : 'none';
      }
      if (this.receiptRequiredStar) {
        this.receiptRequiredStar.style.display = isChecked ? 'inline' : 'none';
      }

      if (!isChecked) {
        // Clear external weighbridge fields to prevent fake values
        if (this.extRefInput) this.extRefInput.value = '';
        if (this.extLoadedInput) this.extLoadedInput.value = '';
        if (this.extEmptyInput) this.extEmptyInput.value = '';
        if (this.extNetInput) this.extNetInput.value = '0.000';
        if (this.extEmptyTimeInput) this.extEmptyTimeInput.value = '';
        if (this.extLoadedTimeInput) this.extLoadedTimeInput.value = '';

        this.clearFieldError('storage-wb-ext-ref');
        this.clearFieldError('storage-wb-ext-loaded');
        this.clearFieldError('storage-wb-ext-empty');
        this.clearFieldError('storage-wb-ext-loaded-time');
        this.clearFieldError('storage-wb-receipt');
      } else {
        // Set default loaded time for external weighbridge
        if (this.extLoadedTimeInput && !this.extLoadedTimeInput.value) {
          const now = new Date();
          const yyyy = now.getFullYear();
          const mm = String(now.getMonth() + 1).padStart(2, '0');
          const dd = String(now.getDate()).padStart(2, '0');
          const hh = String(now.getHours()).padStart(2, '0');
          const min = String(now.getMinutes()).padStart(2, '0');
          this.extLoadedTimeInput.value = `${yyyy}-${mm}-${dd}T${hh}:${min}`;
        }
      }
    }

    calculateInternalNet() {
      const loaded = parseFloat(this.internalLoadedInput?.value || 0);
      const empty = parseFloat(this.internalEmptyInput?.value || 0);

      this.clearFieldError('storage-wb-int-loaded');
      this.clearFieldError('storage-wb-int-empty');

      if (!isNaN(loaded) && !isNaN(empty) && loaded > 0 && empty > 0) {
        if (loaded <= empty) {
          this.showFieldError('storage-wb-int-loaded', 'الوزن الداخلي قائم يجب أن يكون أكبر من الوزن الداخلي فارغ.');
          if (this.internalNetInput) this.internalNetInput.value = '0.000';
          return;
        }

        const net = loaded - empty;
        if (this.internalNetInput) {
          this.internalNetInput.value = net.toFixed(3);
        }
      } else {
        if (this.internalNetInput) {
          this.internalNetInput.value = '0.000';
        }
      }
    }

    calculateExternalNet() {
      if (!this.hasExtWbCheckbox?.checked) return;

      const loaded = parseFloat(this.extLoadedInput?.value || 0);
      const empty = parseFloat(this.extEmptyInput?.value || 0);

      this.clearFieldError('storage-wb-ext-loaded');
      this.clearFieldError('storage-wb-ext-empty');

      if (!isNaN(loaded) && !isNaN(empty) && loaded > 0 && empty > 0) {
        if (loaded <= empty) {
          this.showFieldError('storage-wb-ext-loaded', 'الوزن الخارجي قائم يجب أن يكون أكبر من الوزن الخارجي فارغ.');
          if (this.extNetInput) this.extNetInput.value = '0.000';
          return;
        }

        const net = loaded - empty;
        if (this.extNetInput) {
          this.extNetInput.value = net.toFixed(3);
        }
      } else {
        if (this.extNetInput) {
          this.extNetInput.value = '0.000';
        }
      }
    }

    // =========================================================================
    // User Badge & Initial Data Loading
    // =========================================================================
    updateUserBadge(user) {
      if (!user) return;
      if (this.userNameEl) {
        this.userNameEl.textContent = user.fullName || user.username || 'مستخدم مصرح له';
      }
      if (this.userRoleEl) {
        this.userRoleEl.textContent = this.formatRoleArabic(user.role);
      }
      if (this.userAvatarEl) {
        const initials = (user.fullName || user.username || 'FS')
          .split(' ')
          .map(w => w[0])
          .slice(0, 2)
          .join('')
          .toUpperCase();
        this.userAvatarEl.textContent = initials || 'FS';
      }
    }

    formatRoleArabic(role) {
      if (!role) return 'إدارة المخازن';
      const r = role.toUpperCase();
      if (r === 'CEO') return 'الرئيس التنفيذي';
      if (r === 'IT') return 'مسؤول الدعم الفني';
      if (r.includes('STORAGE')) return 'مدير المخازن والمستودعات';
      if (r.includes('TRANSPORT')) return 'مدير النقل';
      if (r.includes('FACTORY')) return 'مدير عام المصنع';
      return role;
    }

    async loadInitialData() {
      try {
        await Promise.all([
          this.fetchConfig(),
          this.loadRecentTransactions()
        ]);
      } catch (err) {
        console.error('Error loading Storage initial data:', err);
      }
    }

    async fetchConfig() {
      try {
        const res = await fetch('/api/storage/config', {
          headers: {
            'Accept': 'application/json',
            'Authorization': 'Bearer ' + (window.ChatApi?.getToken() || '')
          }
        });
        const json = await res.json();
        if (json.success && json.data) {
          this.config = json.data;
          this.populateResponsibleEmployees(json.data.employees || []);
        }
      } catch (err) {
        console.error('Failed to load storage config:', err);
      }
    }

    populateResponsibleEmployees(employees) {
      if (!this.responsibleEmployeeSelect) return;
      const currentVal = this.responsibleEmployeeSelect.value;
      this.responsibleEmployeeSelect.innerHTML = '<option value="" disabled selected>-- اختر الموظف المسؤول من القائمة --</option>';

      employees.forEach(emp => {
        const opt = document.createElement('option');
        opt.value = emp.fullName || emp.username;
        opt.textContent = `${emp.fullName} (${this.formatRoleArabic(emp.role)})`;
        this.responsibleEmployeeSelect.appendChild(opt);
      });

      // Default to current user if available
      const currentUser = window.chatState?.currentUser || window.ChatApi?.getCurrentUser();
      if (currentUser && (currentUser.fullName || currentUser.username)) {
        const targetName = currentUser.fullName || currentUser.username;
        const found = employees.find(e => e.fullName === targetName || e.username === targetName);
        if (found) {
          this.responsibleEmployeeSelect.value = found.fullName || found.username;
        }
      } else if (currentVal) {
        this.responsibleEmployeeSelect.value = currentVal;
      }
    }

    // =========================================================================
    // Form Validation & Submission
    // =========================================================================
    async handleFormSubmit() {
      if (this.isSubmitting) return;

      this.clearAllErrors();
      this.hideAlert();

      // Read form values
      const material = this.materialSelect?.value;
      const action = this.actionSelect?.value;
      const transactionDate = this.dateInput?.value;

      const incomingQty = parseFloat(this.incomingQtyInput?.value || 0);
      const outgoingQty = parseFloat(this.outgoingQtyInput?.value || 0);

      const isExportPo = (action === 'export' || action === 'صرف') && (this.exportPoCheckbox?.checked || false);
      const poNumber = this.poNumberInput?.value?.trim() || '';

      // Internal Weighbridge
      const internalRef = this.internalRefInput?.value?.trim() || '';
      const internalLoaded = parseFloat(this.internalLoadedInput?.value || 0);
      const internalEmpty = parseFloat(this.internalEmptyInput?.value || 0);
      const internalEmptyTime = this.internalEmptyTimeInput?.value || null;
      const internalLoadedTime = this.internalLoadedTimeInput?.value || null;

      // External Weighbridge
      const hasExtWb = this.hasExtWbCheckbox?.checked || false;
      const extRef = this.extRefInput?.value?.trim() || '';
      const extLoaded = parseFloat(this.extLoadedInput?.value || 0);
      const extEmpty = parseFloat(this.extEmptyInput?.value || 0);
      const extEmptyTime = this.extEmptyTimeInput?.value || null;
      const extLoadedTime = this.extLoadedTimeInput?.value || null;

      // Responsible Employee & Notes
      const responsibleEmployee = this.responsibleEmployeeSelect?.value;
      const notes = this.notesTextarea?.value?.trim() || '';

      // Validation
      let hasError = false;

      // Section 1 Validation
      if (!material) {
        this.showFieldError('storage-material-select', 'نوع الخامة مطلوب (powder أو slurry).');
        hasError = true;
      }

      if (!action) {
        this.showFieldError('storage-action-select', 'نوع الحركة مطلوب (إيداع أو صرف).');
        hasError = true;
      }

      if (action === 'import' || action === 'إيداع') {
        if (isNaN(incomingQty) || incomingQty <= 0) {
          this.showFieldError('storage-incoming-qty', 'الكمية الواردة مطلوبة ويجب أن تكون أكبر من الصفر.');
          hasError = true;
        }
      } else if (action === 'export' || action === 'صرف') {
        if (isNaN(outgoingQty) || outgoingQty <= 0) {
          this.showFieldError('storage-outgoing-qty', 'الكمية الصادرة مطلوبة ويجب أن تكون أكبر من الصفر.');
          hasError = true;
        }

        if (isExportPo) {
          if (!poNumber) {
            this.showFieldError('storage-po-number', 'رقم أمر الشراء مطلوب عند تحديد أن الصادر مربوط بأمر شراء.');
            hasError = true;
          }
        }
      }

      // Section 2 Validation
      if (!transactionDate) {
        this.showFieldError('storage-date', 'تاريخ ووقت الحركة مطلوب.');
        hasError = true;
      }

      // Section 3: Internal Weighbridge Validation
      if (!internalRef) {
        this.showFieldError('storage-wb-int-ref', 'رقم مرجع الميزان الداخلي مطلوب ويجب إدخاله يدوياً.');
        hasError = true;
      }

      if (isNaN(internalLoaded) || internalLoaded <= 0) {
        this.showFieldError('storage-wb-int-loaded', 'الوزن الداخلي قائم مطلوب ويجب أن يكون أكبر من الصفر.');
        hasError = true;
      }

      if (isNaN(internalEmpty) || internalEmpty <= 0) {
        this.showFieldError('storage-wb-int-empty', 'الوزن الداخلي فارغ مطلوب ويجب أن يكون أكبر من الصفر.');
        hasError = true;
      }

      if (!isNaN(internalLoaded) && !isNaN(internalEmpty) && internalLoaded <= internalEmpty) {
        this.showFieldError('storage-wb-int-loaded', 'الوزن الداخلي قائم يجب أن يكون أكبر من الوزن الداخلي فارغ.');
        hasError = true;
      }

      if (!internalLoadedTime) {
        this.showFieldError('storage-wb-int-loaded-time', 'تاريخ ووقت الوزن القائم الداخلي مطلوب.');
        hasError = true;
      }

      // Section 3: External Weighbridge Validation
      if (hasExtWb) {
        if (!extRef) {
          this.showFieldError('storage-wb-ext-ref', 'رقم مرجع الميزان الخارجي مطلوب عند تفعيل الميزان الخارجي.');
          hasError = true;
        }

        if (isNaN(extLoaded) || extLoaded <= 0) {
          this.showFieldError('storage-wb-ext-loaded', 'الوزن الخارجي قائم مطلوب ويجب أن يكون أكبر من الصفر.');
          hasError = true;
        }

        if (isNaN(extEmpty) || extEmpty <= 0) {
          this.showFieldError('storage-wb-ext-empty', 'الوزن الخارجي فارغ مطلوب ويجب أن يكون أكبر من الصفر.');
          hasError = true;
        }

        if (!isNaN(extLoaded) && !isNaN(extEmpty) && extLoaded <= extEmpty) {
          this.showFieldError('storage-wb-ext-loaded', 'الوزن الخارجي قائم يجب أن يكون أكبر من الوزن الخارجي فارغ.');
          hasError = true;
        }

        if (!extLoadedTime) {
          this.showFieldError('storage-wb-ext-loaded-time', 'تاريخ ووقت الوزن القائم الخارجي مطلوب.');
          hasError = true;
        }

        if (!this.currentReceiptFile) {
          this.showFieldError('storage-wb-receipt', 'إرفاق إيصال الميزان مطلوب في حالة وجود ميزان خارجي.');
          hasError = true;
        }
      }

      // Section 4 Validation
      if (!responsibleEmployee) {
        this.showFieldError('storage-responsible-employee', 'الموظف المسؤول مطلوب من القائمة المعتمدة.');
        hasError = true;
      }

      if (hasError) {
        this.showAlert('error', 'خطأ في التحقق', 'يرجى مراجعة الحقول وتصحيح الأخطاء الموضحة أعلاه قبل المتابعة.');
        return;
      }

      // Live PO existence check before network submit
      if (isExportPo) {
        const poValid = await this.validatePoNumberLive();
        if (!poValid) {
          this.showAlert('error', 'خطأ في أمر الشراء', 'Purchase Order not found.');
          return;
        }
      }

      // Build FormData payload
      const formData = new FormData();
      formData.append('material', material);
      formData.append('action', action);

      if (action === 'import' || action === 'إيداع') {
        formData.append('incomingQuantity', incomingQty.toFixed(3));
      } else {
        formData.append('outgoingQuantity', outgoingQty.toFixed(3));
        formData.append('isExportRelatedToPO', isExportPo ? 'true' : 'false');
        if (isExportPo && poNumber) {
          formData.append('poNumber', poNumber);
        }
      }

      formData.append('transactionDate', new Date(transactionDate).toISOString());

      // Internal Weighbridge
      formData.append('internalWeighbridgeReference', internalRef);
      formData.append('internalLoadedWeight', internalLoaded.toFixed(3));
      formData.append('internalEmptyWeight', internalEmpty.toFixed(3));
      if (internalEmptyTime) {
        formData.append('internalEmptyWeightTime', new Date(internalEmptyTime).toISOString());
      }
      formData.append('internalLoadedWeightTime', new Date(internalLoadedTime).toISOString());

      // External Weighbridge
      formData.append('hasExternalWeighbridge', hasExtWb ? 'true' : 'false');
      if (hasExtWb) {
        formData.append('externalWeighbridgeReference', extRef);
        formData.append('externalLoadedWeight', extLoaded.toFixed(3));
        formData.append('externalEmptyWeight', extEmpty.toFixed(3));
        if (extEmptyTime) {
          formData.append('externalEmptyWeightTime', new Date(extEmptyTime).toISOString());
        }
        formData.append('externalLoadedWeightTime', new Date(extLoadedTime).toISOString());
      }

      // Attachment
      if (this.currentReceiptFile) {
        formData.append('weighbridgeAttachment', this.currentReceiptFile);
      }

      // Section 4
      formData.append('responsibleEmployee', responsibleEmployee);
      if (notes) {
        formData.append('notes', notes);
      }

      this.setSubmitting(true);

      try {
        const res = await fetch('/api/storage', {
          method: 'POST',
          headers: {
            'Accept': 'application/json',
            'Authorization': 'Bearer ' + (window.ChatApi?.getToken() || '')
          },
          body: formData
        });

        const json = await res.json();

        if (res.status === 201 && json.success) {
          this.showAlert('success', 'نجاح الإرسال', 'تم إرسال وحفظ حركة المخزن بنجاح وهي الآن قيد مراجعة وتدقيق قسم التحكم والمراقبة.');
          this.resetForm(false); // Don't clear success alert
          await this.loadRecentTransactions();
        } else {
          const errMsg = json.message || 'حدث خطأ أثناء حفظ حركة المخزن.';
          if (errMsg.includes('Purchase Order not found') || errMsg.includes('أمر الشراء')) {
            this.showFieldError('storage-po-number', 'Purchase Order not found.');
          }
          this.showAlert('error', 'فشل الحفظ', errMsg);
        }
      } catch (err) {
        console.error('Storage submission error:', err);
        this.showAlert('error', 'خطأ في الاتصال', 'تعذر الاتصال بالخادم. يرجى التحقق من اتصال الشبكة والمحاولة مرة أخرى.');
      } finally {
        this.setSubmitting(false);
      }
    }

    setSubmitting(loading) {
      this.isSubmitting = loading;
      if (this.btnSubmit) {
        this.btnSubmit.disabled = loading;
      }
      if (this.submitSpinner) {
        this.submitSpinner.style.display = loading ? 'inline-block' : 'none';
      }
      if (this.submitText) {
        this.submitText.textContent = loading ? 'جاري الحفظ...' : 'حفظ الحركة';
      }
    }

    // =========================================================================
    // Form Clear & Confirmation Modal
    // =========================================================================
    handleClearRequest() {
      const hasData = Boolean(
        this.materialSelect?.value ||
        this.actionSelect?.value ||
        parseFloat(this.incomingQtyInput?.value || 0) > 0 ||
        parseFloat(this.outgoingQtyInput?.value || 0) > 0 ||
        this.poNumberInput?.value ||
        this.internalRefInput?.value ||
        parseFloat(this.internalLoadedInput?.value || 0) > 0 ||
        parseFloat(this.internalEmptyInput?.value || 0) > 0 ||
        this.hasExtWbCheckbox?.checked ||
        this.currentReceiptFile ||
        this.notesTextarea?.value
      );

      if (hasData) {
        this.showConfirmModal();
      } else {
        this.resetForm();
      }
    }

    showConfirmModal() {
      if (this.confirmModal) {
        this.confirmModal.style.display = 'flex';
      }
    }

    hideConfirmModal() {
      if (this.confirmModal) {
        this.confirmModal.style.display = 'none';
      }
    }

    resetForm(clearAlert = true) {
      if (clearAlert) {
        this.hideAlert();
      }
      this.clearAllErrors();

      if (this.form) {
        this.form.reset();
      }

      this.clearReceiptAttachment();
      this.setDefaultDateTime();
      this.handleActionChange();
      this.handleExternalWbToggle();

      if (this.internalNetInput) this.internalNetInput.value = '0.000';
      if (this.extNetInput) this.extNetInput.value = '0.000';
      if (this.poStatusEl) this.poStatusEl.textContent = '';
      this.poValidated = false;

      // Re-populate default employee
      if (this.config?.employees) {
        this.populateResponsibleEmployees(this.config.employees);
      }
    }

    // =========================================================================
    // Recent Transactions Table
    // =========================================================================
    async loadRecentTransactions() {
      if (!this.tableBody) return;

      try {
        const res = await fetch('/api/storage/recent?limit=20', {
          headers: {
            'Accept': 'application/json',
            'Authorization': 'Bearer ' + (window.ChatApi?.getToken() || '')
          }
        });
        const json = await res.json();
        if (json.success && json.data) {
          this.recentTransactions = json.data;
          this.renderRecentTable(json.data);
        }
      } catch (err) {
        console.error('Failed to load recent storage transactions:', err);
        this.tableBody.innerHTML = `
          <tr>
            <td colspan="11" style="text-align:center; padding: 2rem; color: #DC2626; font-weight:700;">
              تعذر تحميل بيانات حركات المخزن. اضغط تحديث للمحاولة مرة أخرى.
            </td>
          </tr>
        `;
      }
    }

    renderRecentTable(transactions) {
      if (!this.tableBody) return;
      this.tableBody.innerHTML = '';

      if (!transactions || transactions.length === 0) {
        this.tableBody.innerHTML = `
          <tr>
            <td colspan="11" style="text-align:center; padding: 2.5rem; color: #6B7280; font-weight: 700;">
              لا توجد حركات مخزن مسجلة حالياً. استخدم النموذج أعلاه لتسجيل أول حركة.
            </td>
          </tr>
        `;
        return;
      }

      transactions.forEach(tx => {
        const tr = document.createElement('tr');

        // Date format
        const dateStr = new Date(tx.transactionDate).toLocaleString('ar-EG', {
          year: 'numeric',
          month: 'short',
          day: 'numeric',
          hour: '2-digit',
          minute: '2-digit'
        });

        // Material badge
        const mat = (tx.material || '').toLowerCase();
        let matBadge = `<span class="storage-badge-service powder">بودرة (powder)</span>`;
        if (mat === 'slurry') {
          matBadge = `<span class="storage-badge-service slurry">معلق (slurry)</span>`;
        }

        // Action badge
        const act = (tx.action || tx.serviceType || '').toLowerCase();
        const isImport = act === 'import' || act === 'إيداع';
        const actBadge = isImport
          ? `<span class="storage-badge-service import">إيداع (Import)</span>`
          : `<span class="storage-badge-service export">صرف (Export)</span>`;

        // Quantity (tons)
        let qtyDisplay = '-';
        if (isImport && tx.incomingQuantity) {
          qtyDisplay = `<span dir="ltr" style="font-weight:800; color:#166534;">+${parseFloat(tx.incomingQuantity).toFixed(3)} طن</span>`;
        } else if (!isImport && tx.outgoingQuantity) {
          qtyDisplay = `<span dir="ltr" style="font-weight:800; color:#991B1B;">-${parseFloat(tx.outgoingQuantity).toFixed(3)} طن</span>`;
        } else if (tx.incomingQuantity) {
          qtyDisplay = `<span dir="ltr" style="font-weight:800; color:#166534;">+${parseFloat(tx.incomingQuantity).toFixed(3)} طن</span>`;
        }

        // PO Number
        const poDisplay = tx.poNumber
          ? `<span class="storage-badge-po">${tx.poNumber}</span>`
          : `<span style="color:#9CA3AF;">-</span>`;

        // Internal Weighbridge
        const intNet = (tx.internalNetWeight !== undefined && tx.internalNetWeight !== null)
          ? parseFloat(tx.internalNetWeight).toFixed(3)
          : (parseFloat(tx.internalLoadedWeight || 0) - parseFloat(tx.internalEmptyWeight || 0)).toFixed(3);

        const intWbDisplay = `
          <div style="font-size:0.8rem;">
            <span style="font-weight:700;">#${tx.internalWeighbridgeReference || tx.referenceNumber || '-'}</span>
            <div style="color:#0F172A; font-weight:800; dir:ltr;">صافي: ${intNet} طن</div>
          </div>
        `;

        // External Weighbridge
        let extWbDisplay = `<span style="color:#9CA3AF; font-size:0.75rem;">غير مفعل</span>`;
        if (tx.hasExternalWeighbridge) {
          const extNet = (tx.externalNetWeight !== undefined && tx.externalNetWeight !== null)
            ? parseFloat(tx.externalNetWeight).toFixed(3)
            : (parseFloat(tx.externalLoadedWeight || 0) - parseFloat(tx.externalEmptyWeight || 0)).toFixed(3);

          extWbDisplay = `
            <div style="font-size:0.8rem;">
              <span style="font-weight:700; color:#1E40AF;">#${tx.externalWeighbridgeReference || '-'}</span>
              <div style="color:#1E40AF; font-weight:800; dir:ltr;">صافي: ${extNet} طن</div>
            </div>
          `;
        }

        // Weighbridge Receipt Attachment
        let receiptDisplay = `<span style="color:#9CA3AF; font-size:0.75rem;">لا يوجد</span>`;
        const fileUrl = tx.weighbridgeAttachmentPath || (tx.weighbridgeFileName ? `/api/storage/attachments/${encodeURIComponent(tx.weighbridgeFileName)}` : null);
        if (fileUrl) {
          receiptDisplay = `
            <a href="${fileUrl}" target="_blank" rel="noopener noreferrer" class="storage-btn-attachment-link" title="تحميل / عرض الإيصال المرفق">
              📎 <span>عرض الإيصال</span>
            </a>
          `;
        }

        // Control & Monitoring Approval Badge
        let approvalBadge = '';
        const st = (tx.approvalStatus || 'PENDING').toUpperCase();
        if (st === 'APPROVED') {
          approvalBadge = '<span class="cm-badge cm-badge-approved">✓ معتمد</span>';
        } else if (st === 'REJECTED') {
          const reasonText = tx.rejectionReason ? `<div style="font-size:0.75rem; color:#dc2626; margin-top:2px;">السبب: ${this.escapeHtml(tx.rejectionReason)}</div>` : '';
          approvalBadge = `
            <div>
              <span class="cm-badge cm-badge-rejected">✕ مرفوض</span>
              ${reasonText}
              <button type="button" class="storage-resubmit-btn" data-id="${tx.id}" style="display:inline-block; margin-top:4px; padding:3px 8px; font-size:0.75rem; background:#2563eb; color:#fff; border:none; border-radius:4px; cursor:pointer;">إعادة تقديم</button>
            </div>
          `;
        } else {
          approvalBadge = '<span class="cm-badge cm-badge-pending">⏳ قيد مراجعة الرقابة</span>';
        }

        tr.innerHTML = `
          <td><strong>#${tx.movementNumber || tx.id}</strong></td>
          <td dir="ltr" style="text-align:right;">${dateStr}</td>
          <td>${matBadge}</td>
          <td>${actBadge}</td>
          <td>${qtyDisplay}</td>
          <td>${poDisplay}</td>
          <td>${intWbDisplay}</td>
          <td>${extWbDisplay}</td>
          <td>${tx.responsibleEmployee || tx.createdBy || '-'}</td>
          <td>${receiptDisplay}</td>
          <td>${approvalBadge}</td>
        `;

        this.tableBody.appendChild(tr);
      });

      this.tableBody.querySelectorAll('.storage-resubmit-btn').forEach(btn => {
        btn.addEventListener('click', async (e) => {
          const txId = e.currentTarget.getAttribute('data-id');
          const comment = prompt('هل ترغب بإضافة توضيح للمراجعة عند إعادة التقديم؟ (اختياري)');
          if (comment !== null) {
            btn.disabled = true;
            btn.textContent = 'جاري التقديم...';
            try {
              const res = await window.ChatApi?.resubmitControlMonitoringForm('Storage', txId, comment);
              if (res && res.success) {
                alert('تمت إعادة تقديم النموذج بنجاح لقسم التحكم والمراقبة.');
                this.loadRecentTransactions();
              } else {
                alert('تعذر إعادة التقديم: ' + (res?.message || 'خطأ غير معروف'));
                btn.disabled = false;
                btn.textContent = 'إعادة تقديم';
              }
            } catch (err) {
              console.error(err);
              alert('حدث خطأ أثناء الاتصال بالخادم.');
              btn.disabled = false;
              btn.textContent = 'إعادة تقديم';
            }
          }
        });
      });
    }

    escapeHtml(str) {
      if (!str) return '';
      return String(str)
        .replace(/&/g, '&amp;')
        .replace(/</g, '&lt;')
        .replace(/>/g, '&gt;')
        .replace(/"/g, '&quot;')
        .replace(/'/g, '&#039;');
    }

    // =========================================================================
    // UI Helpers & Error Alerts
    // =========================================================================
    showFieldError(fieldId, errorText) {
      const errEl = document.getElementById(`err-${fieldId}`);
      if (errEl) {
        errEl.textContent = errorText;
        errEl.classList.add('visible');
      }
      const inputEl = document.getElementById(fieldId);
      inputEl?.classList.add('is-invalid');
    }

    clearFieldError(fieldId) {
      const errEl = document.getElementById(`err-${fieldId}`);
      if (errEl) {
        errEl.textContent = '';
        errEl.classList.remove('visible');
      }
      const inputEl = document.getElementById(fieldId);
      inputEl?.classList.remove('is-invalid');
    }

    clearAllErrors() {
      const errs = this.pageRoot?.querySelectorAll('.storage-error-msg');
      errs?.forEach(e => {
        e.textContent = '';
        e.classList.remove('visible');
      });
      const inputs = this.pageRoot?.querySelectorAll('.is-invalid');
      inputs?.forEach(i => i.classList.remove('is-invalid'));
    }

    showAlert(type, title, message) {
      if (!this.alertBanner) return;
      this.alertBanner.className = `storage-alert-banner ${type}`;
      if (this.alertTitle) this.alertTitle.textContent = title;
      if (this.alertMsg) this.alertMsg.textContent = message;
      if (this.alertIcon) {
        this.alertIcon.textContent = (type === 'success') ? '✓' : '✕';
      }
      this.alertBanner.style.display = 'flex';
      this.alertBanner.scrollIntoView({ behavior: 'smooth', block: 'nearest' });
    }

    hideAlert() {
      if (this.alertBanner) {
        this.alertBanner.style.display = 'none';
      }
    }
  }

  // Export Singleton
  window.StorageApp = new StorageManager();

})();
