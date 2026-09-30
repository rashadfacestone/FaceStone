/**
 * FaceStone Storage Department Management - Arabic RTL Application Module
 * Handles Storage Transaction Entry, Service Type Reactive Logic, Uniqueness Validation,
 * Dynamic Multiple Transportation Movement Linkages, Auto Balance Calculations, and API Persistence.
 */

(function () {
  'use strict';

  class StorageManager {
    constructor() {
      this.isSubmitting = false;
      this.initialized = false;
      this.config = null;
      this.recentTransactions = [];
      this.activeMovements = [0]; // Array of movement numbers
      this.refCheckTimeout = null;
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
      this.overallBalanceChip = document.getElementById('storage-overall-balance-chip');

      // Banner / Alert
      this.alertBanner = document.getElementById('storage-alert-banner');
      this.alertIcon = document.getElementById('storage-alert-icon');
      this.alertTitle = document.getElementById('storage-alert-title');
      this.alertMsg = document.getElementById('storage-alert-msg');
      this.alertCloseBtn = document.getElementById('storage-alert-close-btn');

      // Form Elements
      this.form = document.getElementById('storage-entry-form');
      this.movementNumberInput = document.getElementById('storage-movement-number');
      this.dateInput = document.getElementById('storage-date');
      this.serviceTypeSelect = document.getElementById('storage-service-type');
      this.referenceNumberInput = document.getElementById('storage-reference-number');
      this.refStatusEl = document.getElementById('storage-ref-status');

      this.poNumberInput = document.getElementById('storage-po-number');
      this.movementsListEl = document.getElementById('storage-movements-list');
      this.movementsCountEl = document.getElementById('storage-movements-count');
      this.btnAddMovement = document.getElementById('btn-storage-add-movement');
      this.quickChipsContainer = document.getElementById('storage-quick-chips');

      this.incomingQtyInput = document.getElementById('storage-incoming-qty');
      this.outgoingQtyInput = document.getElementById('storage-outgoing-qty');
      this.openingBalanceInput = document.getElementById('storage-opening-balance');
      this.closingBalanceInput = document.getElementById('storage-closing-balance');
      this.starIncoming = document.getElementById('star-incoming');
      this.starOutgoing = document.getElementById('star-outgoing');

      this.locationSelect = document.getElementById('storage-location');
      this.destinationGroup = document.getElementById('storage-destination-group');
      this.destinationSelect = document.getElementById('storage-destination-location');
      this.receivingEmployeeSelect = document.getElementById('storage-receiving-employee');
      this.notesTextarea = document.getElementById('storage-notes');

      // Action Buttons
      this.btnSubmit = document.getElementById('btn-storage-submit');
      this.submitSpinner = document.getElementById('storage-submit-spinner');
      this.submitText = document.getElementById('storage-submit-text');
      this.btnClear = document.getElementById('btn-storage-clear');

      // Confirmation Modal
      this.confirmModal = document.getElementById('storage-confirm-modal');
      this.confirmCancelBtn = document.getElementById('storage-confirm-cancel-btn');
      this.confirmProceedBtn = document.getElementById('storage-confirm-proceed-btn');

      // Recent Table
      this.tableBody = document.getElementById('storage-table-body');
      this.btnRefresh = document.getElementById('btn-storage-refresh');

      this.attachEventListeners();
      this.setDefaultDateTime();
      this.renderMovementsList();
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

      // Service Type Change
      this.serviceTypeSelect?.addEventListener('change', () => {
        this.handleServiceTypeChange();
        this.calculateBalances();
      });

      // Storage Location Change -> Retrieve opening balance for that location
      this.locationSelect?.addEventListener('change', () => {
        const loc = this.locationSelect.value;
        this.fetchBalanceForLocation(loc);
      });

      // Quantity Inputs -> Calculate Closing Balance
      this.incomingQtyInput?.addEventListener('input', () => this.calculateBalances());
      this.outgoingQtyInput?.addEventListener('input', () => this.calculateBalances());

      // Reference Number check on input / blur
      this.referenceNumberInput?.addEventListener('input', () => {
        clearTimeout(this.refCheckTimeout);
        this.refCheckTimeout = setTimeout(() => this.checkReferenceUniqueness(), 400);
      });
      this.referenceNumberInput?.addEventListener('blur', () => this.checkReferenceUniqueness());

      // Add Transportation Movement button
      this.btnAddMovement?.addEventListener('click', () => {
        this.addTransportationMovementRow();
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

      // Confirm Modal buttons
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

    setDefaultDateTime() {
      if (!this.dateInput) return;
      const now = new Date();
      // Format as YYYY-MM-DDTHH:mm
      const yyyy = now.getFullYear();
      const mm = String(now.getMonth() + 1).padStart(2, '0');
      const dd = String(now.getDate()).padStart(2, '0');
      const hh = String(now.getHours()).padStart(2, '0');
      const min = String(now.getMinutes()).padStart(2, '0');
      this.dateInput.value = `${yyyy}-${mm}-${dd}T${hh}:${min}`;
    }

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
          this.populateConfigDropdowns(json.data);
        }
      } catch (err) {
        console.error('Failed to load storage config:', err);
      }
    }

    populateConfigDropdowns(config) {
      // 1. Populate Storage Locations
      if (this.locationSelect && config.storageLocations) {
        const currentVal = this.locationSelect.value;
        this.locationSelect.innerHTML = '<option value="" disabled selected>-- اختر مكان التخزين --</option>';
        config.storageLocations.forEach(loc => {
          const opt = document.createElement('option');
          opt.value = loc;
          opt.textContent = loc;
          this.locationSelect.appendChild(opt);
        });
        if (currentVal && config.storageLocations.includes(currentVal)) {
          this.locationSelect.value = currentVal;
        } else if (config.storageLocations.length > 0) {
          this.locationSelect.value = config.storageLocations[0];
          this.fetchBalanceForLocation(config.storageLocations[0]);
        }
      }

      // 2. Populate Destination Storage Location (for transfer)
      if (this.destinationSelect && config.storageLocations) {
        const currentDestVal = this.destinationSelect.value;
        this.destinationSelect.innerHTML = '<option value="" disabled selected>-- اختر المستودع المحول إليه --</option>';
        config.storageLocations.forEach(loc => {
          const opt = document.createElement('option');
          opt.value = loc;
          opt.textContent = loc;
          this.destinationSelect.appendChild(opt);
        });
        if (currentDestVal && config.storageLocations.includes(currentDestVal)) {
          this.destinationSelect.value = currentDestVal;
        }
      }

      // 3. Populate Employees
      if (this.receivingEmployeeSelect && config.employees) {
        const currentEmpVal = this.receivingEmployeeSelect.value;
        this.receivingEmployeeSelect.innerHTML = '<option value="" disabled selected>-- اختر الموظف المستلم من القائمة --</option>';
        config.employees.forEach(emp => {
          const opt = document.createElement('option');
          opt.value = emp.fullName;
          opt.textContent = `${emp.fullName} (${this.formatRoleArabic(emp.role)})`;
          this.receivingEmployeeSelect.appendChild(opt);
        });

        // Set default to current user if available
        const currentUser = window.chatState?.currentUser || window.ChatApi?.getCurrentUser();
        if (currentUser && currentUser.fullName) {
          const found = config.employees.find(e => e.fullName === currentUser.fullName);
          if (found) {
            this.receivingEmployeeSelect.value = found.fullName;
          }
        }
      }

      // 4. Populate Available Transportation Movements Quick Chips
      if (this.quickChipsContainer && config.availableTransportationMovements) {
        this.quickChipsContainer.innerHTML = '';
        const recent = config.availableTransportationMovements.slice(0, 5);
        if (recent.length === 0) {
          this.quickChipsContainer.innerHTML = '<span style="font-size:0.75rem; color:#9CA3AF;">لا توجد نقلات مسجلة حالياً</span>';
        } else {
          recent.forEach(m => {
            const chip = document.createElement('button');
            chip.type = 'button';
            chip.className = 'storage-quick-chip';
            chip.innerHTML = `<span>نقلة #${m.transactionNumber}</span><span style="opacity:0.7;">(${m.carNumber || m.poNumber})</span>`;
            chip.title = `إضافة النقلة #${m.transactionNumber} - ${m.materialType || ''} (${m.netWeight} طن)`;
            chip.addEventListener('click', () => {
              this.addSpecificTransportationMovement(m.transactionNumber, m.poNumber);
            });
            this.quickChipsContainer.appendChild(chip);
          });
        }
      }

      // 5. Update overall balance chip
      if (this.overallBalanceChip) {
        const bal = parseFloat(config.currentOverallBalance || 0);
        this.overallBalanceChip.textContent = `الرصيد العام الحالي: ${bal.toFixed(3)} طن`;
      }
    }

    async fetchBalanceForLocation(locationName) {
      if (!locationName) return;
      try {
        const url = `/api/storage/balance?storageLocation=${encodeURIComponent(locationName)}`;
        const res = await fetch(url, {
          headers: {
            'Accept': 'application/json',
            'Authorization': 'Bearer ' + (window.ChatApi?.getToken() || '')
          }
        });
        const json = await res.json();
        if (json.success && json.data) {
          const bal = parseFloat(json.data.currentBalance || 0);
          if (this.openingBalanceInput) {
            this.openingBalanceInput.value = bal.toFixed(3);
          }
          this.calculateBalances();
        }
      } catch (err) {
        console.error('Failed to fetch balance for location:', err);
      }
    }

    async checkReferenceUniqueness() {
      if (!this.referenceNumberInput) return true;
      const refVal = parseInt(this.referenceNumberInput.value, 10);
      if (!refVal || isNaN(refVal) || refVal <= 0) {
        if (this.refStatusEl) this.refStatusEl.textContent = '';
        return false;
      }

      try {
        const res = await fetch(`/api/storage/check-reference/${refVal}`, {
          headers: {
            'Accept': 'application/json',
            'Authorization': 'Bearer ' + (window.ChatApi?.getToken() || '')
          }
        });
        const json = await res.json();
        if (json.success && json.data) {
          if (json.data.exists) {
            this.referenceNumberInput.classList.add('is-invalid');
            this.showFieldError('storage-reference-number', 'رقم المرجع مستخدم بالفعل');
            if (this.refStatusEl) {
              this.refStatusEl.textContent = '❌ غير متاح';
              this.refStatusEl.className = 'storage-ref-status invalid';
            }
            return false;
          } else {
            this.referenceNumberInput.classList.remove('is-invalid');
            this.clearFieldError('storage-reference-number');
            if (this.refStatusEl) {
              this.refStatusEl.textContent = '✓ متاح';
              this.refStatusEl.className = 'storage-ref-status valid';
            }
            return true;
          }
        }
      } catch (err) {
        console.error('Reference check error:', err);
      }
      return true;
    }

    handleServiceTypeChange() {
      const type = this.serviceTypeSelect?.value;
      if (!type) return;

      // Reset requirements and styles
      if (this.destinationGroup) {
        this.destinationGroup.style.display = (type === 'تحويل') ? 'flex' : 'none';
        if (this.destinationSelect) {
          this.destinationSelect.required = (type === 'تحويل');
        }
      }

      const labelLoc = document.getElementById('label-storage-location');
      if (labelLoc) {
        labelLoc.textContent = (type === 'تحويل') ? 'مكان التخزين (المصدر / المحول منه)' : 'مكان التخزين';
      }

      switch (type) {
        case 'إضافة':
          this.incomingQtyInput.disabled = false;
          this.incomingQtyInput.classList.remove('storage-readonly-input');
          this.outgoingQtyInput.disabled = true;
          this.outgoingQtyInput.classList.add('storage-readonly-input');
          this.outgoingQtyInput.value = '0.000';
          if (parseFloat(this.incomingQtyInput.value) === 0) {
            this.incomingQtyInput.value = '';
          }
          break;

        case 'صرف':
          this.outgoingQtyInput.disabled = false;
          this.outgoingQtyInput.classList.remove('storage-readonly-input');
          this.incomingQtyInput.disabled = true;
          this.incomingQtyInput.classList.add('storage-readonly-input');
          this.incomingQtyInput.value = '0.000';
          if (parseFloat(this.outgoingQtyInput.value) === 0) {
            this.outgoingQtyInput.value = '';
          }
          break;

        case 'تحويل':
          // Transfer removes from source location
          this.outgoingQtyInput.disabled = false;
          this.outgoingQtyInput.classList.remove('storage-readonly-input');
          this.incomingQtyInput.disabled = true;
          this.incomingQtyInput.classList.add('storage-readonly-input');
          this.incomingQtyInput.value = '0.000';
          if (parseFloat(this.outgoingQtyInput.value) === 0) {
            this.outgoingQtyInput.value = '';
          }
          break;

        case 'تسوية':
          this.incomingQtyInput.disabled = false;
          this.incomingQtyInput.classList.remove('storage-readonly-input');
          this.outgoingQtyInput.disabled = false;
          this.outgoingQtyInput.classList.remove('storage-readonly-input');
          break;
      }
    }

    calculateBalances() {
      const openBal = parseFloat(this.openingBalanceInput?.value || 0) || 0;
      const incoming = parseFloat(this.incomingQtyInput?.value || 0) || 0;
      const outgoing = parseFloat(this.outgoingQtyInput?.value || 0) || 0;

      // Formula: ClosingBalance = OpeningBalance + IncomingQuantity - OutgoingQuantity
      const closing = openBal + incoming - outgoing;

      if (this.closingBalanceInput) {
        this.closingBalanceInput.value = isNaN(closing) ? '0.000' : closing.toFixed(3);
      }
    }

    // =========================================================================
    // Multiple Transportation Movements (رقم النقلة) Repeater Logic
    // =========================================================================
    renderMovementsList() {
      if (!this.movementsListEl) return;
      this.movementsListEl.innerHTML = '';

      if (this.activeMovements.length === 0) {
        this.activeMovements = [0];
      }

      this.activeMovements.forEach((movNum, index) => {
        const row = document.createElement('div');
        row.className = 'storage-movement-row';
        row.dataset.index = index;

        const val = movNum > 0 ? movNum : '';
        row.innerHTML = `
          <span class="storage-movement-row-prefix">نقلة ${index + 1}:</span>
          <input type="number" class="storage-movement-input" min="1" step="1" placeholder="رقم النقلة (مثال: ${1001 + index})" value="${val}">
          ${this.activeMovements.length > 1 ? `
            <button type="button" class="btn-storage-remove-movement" title="حذف هذه النقلة">
              <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.5"><line x1="18" y1="6" x2="6" y2="18"/><line x1="6" y1="6" x2="18" y2="18"/></svg>
              <span>حذف</span>
            </button>
          ` : ''}
        `;

        const input = row.querySelector('.storage-movement-input');
        input?.addEventListener('input', () => {
          const n = parseInt(input.value, 10);
          this.activeMovements[index] = (!isNaN(n) && n > 0) ? n : 0;
          this.updateMovementsCounter();
        });

        const btnRemove = row.querySelector('.btn-storage-remove-movement');
        btnRemove?.addEventListener('click', () => {
          this.removeTransportationMovementRow(index);
        });

        this.movementsListEl.appendChild(row);
      });

      this.updateMovementsCounter();
    }

    addTransportationMovementRow() {
      this.activeMovements.push(0);
      this.renderMovementsList();
      // Focus on new input
      const inputs = this.movementsListEl.querySelectorAll('.storage-movement-input');
      if (inputs.length > 0) {
        inputs[inputs.length - 1].focus();
      }
    }

    addSpecificTransportationMovement(movNumber, poNumber) {
      if (!movNumber) return;
      // If first row is empty, replace it
      const emptyIdx = this.activeMovements.findIndex(m => m === 0);
      if (emptyIdx !== -1) {
        this.activeMovements[emptyIdx] = movNumber;
      } else if (!this.activeMovements.includes(movNumber)) {
        this.activeMovements.push(movNumber);
      }

      // If PO is empty, automatically populate from transportation
      if (poNumber && this.poNumberInput && !this.poNumberInput.value.trim()) {
        this.poNumberInput.value = poNumber;
      }

      this.renderMovementsList();
    }

    removeTransportationMovementRow(index) {
      if (this.activeMovements.length <= 1) {
        this.activeMovements = [0];
      } else {
        this.activeMovements.splice(index, 1);
      }
      this.renderMovementsList();
    }

    updateMovementsCounter() {
      const validCount = this.activeMovements.filter(n => n > 0).length;
      if (this.movementsCountEl) {
        this.movementsCountEl.textContent = `${validCount} نقلة محددة`;
      }
    }

    // =========================================================================
    // Form Validation & Submission
    // =========================================================================
    async handleFormSubmit() {
      if (this.isSubmitting) return;

      this.clearAllErrors();
      this.hideAlert();

      // Read values
      const movementNumber = parseInt(this.movementNumberInput?.value, 10);
      const transactionDate = this.dateInput?.value;
      const serviceType = this.serviceTypeSelect?.value;
      const referenceNumber = parseInt(this.referenceNumberInput?.value, 10);
      const poNumber = this.poNumberInput?.value?.trim();

      const validMovements = this.activeMovements.filter(n => n > 0);
      const incomingQty = parseFloat(this.incomingQtyInput?.value || 0) || 0;
      const outgoingQty = parseFloat(this.outgoingQtyInput?.value || 0) || 0;
      const openingBalance = parseFloat(this.openingBalanceInput?.value || 0) || 0;
      const closingBalance = openingBalance + incomingQty - outgoingQty;

      const storageLocation = this.locationSelect?.value;
      const destinationLocation = this.destinationSelect?.value;
      const receivingEmployee = this.receivingEmployeeSelect?.value;
      const notes = this.notesTextarea?.value?.trim() || null;

      // Validate
      let hasError = false;

      if (!movementNumber || isNaN(movementNumber) || movementNumber <= 0) {
        this.showFieldError('storage-movement-number', 'يرجى إدخال رقم حركة صحيح.');
        hasError = true;
      }

      if (!transactionDate) {
        this.showFieldError('storage-date', 'التاريخ مطلوب.');
        hasError = true;
      }

      if (!serviceType) {
        this.showFieldError('storage-service-type', 'يرجى اختيار نوع الخدمة.');
        hasError = true;
      }

      if (!referenceNumber || isNaN(referenceNumber) || referenceNumber <= 0) {
        this.showFieldError('storage-reference-number', 'رقم المرجع مطلوب.');
        hasError = true;
      }

      if (!poNumber) {
        this.showFieldError('storage-po-number', 'رقم أمر الشراء (PO) مطلوب.');
        hasError = true;
      }

      if (validMovements.length === 0) {
        this.showFieldError('storage-movements', 'يجب إضافة رقم نقلة واحد على الأقل.');
        hasError = true;
      }

      // Quantity validations based on service type
      if (serviceType === 'إضافة') {
        if (incomingQty <= 0) {
          this.showFieldError('storage-incoming-qty', 'في حالة الإضافة، يجب أن تكون الكمية الواردة أكبر من الصفر.');
          hasError = true;
        }
      } else if (serviceType === 'صرف') {
        if (outgoingQty <= 0) {
          this.showFieldError('storage-outgoing-qty', 'في حالة الصرف، يجب أن تكون الكمية المنصرفة أكبر من الصفر.');
          hasError = true;
        }
      } else if (serviceType === 'تحويل') {
        if (!destinationLocation) {
          this.showFieldError('storage-destination-location', 'يرجى اختيار المستودع المحول إليه.');
          hasError = true;
        } else if (destinationLocation === storageLocation) {
          this.showFieldError('storage-destination-location', 'المستودع المحول إليه يجب أن يختلف عن مستودع المصدر.');
          hasError = true;
        }
        if (outgoingQty <= 0) {
          this.showFieldError('storage-outgoing-qty', 'يرجى تحديد الكمية المحولة.');
          hasError = true;
        }
      } else if (serviceType === 'تسوية') {
        if (incomingQty === 0 && outgoingQty === 0) {
          this.showFieldError('storage-incoming-qty', 'يرجى إدخال كمية التسوية الجردية (إضافة فائض أو صرف عجز).');
          hasError = true;
        }
      }

      if (!storageLocation) {
        this.showFieldError('storage-location', 'يرجى اختيار مكان التخزين.');
        hasError = true;
      }

      if (!receivingEmployee) {
        this.showFieldError('storage-receiving-employee', 'اسم الموظف المستلم مطلوب.');
        hasError = true;
      }

      if (hasError) {
        this.showAlert('error', 'خطأ في التحقق', 'يرجى تصحيح الأخطاء الموضحة أعلاه قبل المتابعة.');
        return;
      }

      // Pre-check reference uniqueness
      const isRefUnique = await this.checkReferenceUniqueness();
      if (!isRefUnique) {
        this.showAlert('error', 'رقم المرجع مكرر', 'رقم المرجع مستخدم بالفعل، يرجى إدخال رقم مرجع جديد.');
        return;
      }

      // Payload
      const payload = {
        movementNumber,
        transactionDate: new Date(transactionDate).toISOString(),
        serviceType,
        referenceNumber,
        poNumber,
        transportationMovementNumbers: validMovements,
        incomingQuantity: incomingQty,
        outgoingQuantity: outgoingQty,
        openingBalance,
        closingBalance,
        storageLocation,
        destinationStorageLocation: (serviceType === 'تحويل') ? destinationLocation : null,
        receivingEmployee,
        notes
      };

      this.setSubmitting(true);

      try {
        const res = await fetch('/api/storage', {
          method: 'POST',
          headers: {
            'Content-Type': 'application/json',
            'Accept': 'application/json',
            'Authorization': 'Bearer ' + (window.ChatApi?.getToken() || '')
          },
          body: JSON.stringify(payload)
        });

        const json = await res.json();

        if (res.status === 201 && json.success) {
          this.showAlert('success', 'نجاح الحفظ', 'تم حفظ حركة المخزن بنجاح');
          this.resetForm(false); // Don't clear success alert
          await this.loadRecentTransactions();
          await this.fetchConfig(); // refresh balance chip
        } else {
          const errMsg = json.message || 'حدث خطأ أثناء حفظ حركة المخزن.';
          if (errMsg.includes('رقم المرجع مستخدم بالفعل')) {
            this.showFieldError('storage-reference-number', 'رقم المرجع مستخدم بالفعل');
          }
          this.showAlert('error', 'فشل الحفظ', errMsg);
        }
      } catch (err) {
        console.error('Submission error:', err);
        this.showAlert('error', 'خطأ في الاتصال', 'تعذر الاتصال بالخادم. يرجى التحقق من اتصال الشبكة.');
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
        this.submitText.textContent = loading ? 'جاري الحفظ...' : 'حفظ';
      }
    }

    // =========================================================================
    // Form Clear & Confirmation
    // =========================================================================
    handleClearRequest() {
      const hasData = Boolean(
        this.movementNumberInput?.value ||
        this.referenceNumberInput?.value ||
        this.poNumberInput?.value ||
        parseFloat(this.incomingQtyInput?.value || 0) > 0 ||
        parseFloat(this.outgoingQtyInput?.value || 0) > 0 ||
        this.notesTextarea?.value ||
        this.activeMovements.some(n => n > 0)
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

      this.setDefaultDateTime();
      this.activeMovements = [0];
      this.renderMovementsList();

      if (this.refStatusEl) {
        this.refStatusEl.textContent = '';
      }

      if (this.destinationGroup) {
        this.destinationGroup.style.display = 'none';
      }

      if (this.incomingQtyInput) {
        this.incomingQtyInput.disabled = false;
        this.incomingQtyInput.classList.remove('storage-readonly-input');
        this.incomingQtyInput.value = '0.000';
      }

      if (this.outgoingQtyInput) {
        this.outgoingQtyInput.disabled = false;
        this.outgoingQtyInput.classList.remove('storage-readonly-input');
        this.outgoingQtyInput.value = '0.000';
      }

      // Refresh location opening balance
      if (this.locationSelect && this.locationSelect.value) {
        this.fetchBalanceForLocation(this.locationSelect.value);
      } else {
        if (this.openingBalanceInput) this.openingBalanceInput.value = '0.000';
        if (this.closingBalanceInput) this.closingBalanceInput.value = '0.000';
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
            <td colspan="12" style="text-align:center; padding: 2rem; color: #DC2626; font-weight:700;">
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
            <td colspan="12" style="text-align:center; padding: 2.5rem; color: #6B7280; font-weight: 700;">
              لا توجد حركات مخزن مسجلة حالياً. استخدم النموذج أعلاه لتسجيل أول حركة.
            </td>
          </tr>
        `;
        return;
      }

      transactions.forEach(tx => {
        const tr = document.createElement('tr');

        const dateStr = new Date(tx.transactionDate).toLocaleString('ar-EG', {
          year: 'numeric',
          month: 'short',
          day: 'numeric',
          hour: '2-digit',
          minute: '2-digit'
        });

        const movementsBadges = (tx.transportationMovementNumbers || [])
          .map(n => `<span style="display:inline-block; background:#000; color:#FFCC00; padding:2px 7px; border-radius:10px; font-size:0.75rem; font-weight:800; margin:1px;">#${n}</span>`)
          .join(' ') || '<span style="color:#9CA3AF;">-</span>';

        const destinationText = (tx.serviceType === 'تحويل' && tx.destinationStorageLocation)
          ? `<span style="font-size:0.75rem; color:#1E40AF; display:block;">إلى: ${tx.destinationStorageLocation}</span>`
          : '';

        tr.innerHTML = `
          <td><strong>#${tx.movementNumber}</strong></td>
          <td dir="ltr" style="text-align:right;">${dateStr}</td>
          <td><span class="storage-badge-service ${tx.serviceType}">${tx.serviceType}</span></td>
          <td><strong>REF-${tx.referenceNumber}</strong></td>
          <td dir="ltr" style="text-align:right;">${tx.poNumber || '-'}</td>
          <td>${movementsBadges}</td>
          <td dir="ltr" style="font-weight:700; color:${tx.incomingQuantity > 0 ? '#166534' : '#6B7280'};">${tx.incomingQuantity.toFixed(3)} طن</td>
          <td dir="ltr" style="font-weight:700; color:${tx.outgoingQuantity > 0 ? '#991B1B' : '#6B7280'};">${tx.outgoingQuantity.toFixed(3)} طن</td>
          <td dir="ltr" style="font-weight:800; color:#000;">${tx.closingBalance.toFixed(3)} طن</td>
          <td>${tx.storageLocation}${destinationText}</td>
          <td>${tx.receivingEmployee}</td>
          <td style="max-width:180px; overflow:hidden; text-overflow:ellipsis; white-space:nowrap;" title="${tx.notes || ''}">
            ${tx.notes || '<span style="color:#9CA3AF;">-</span>'}
          </td>
        `;

        this.tableBody.appendChild(tr);
      });
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
      const inputs = this.pageRoot?.querySelectorAll('.storage-input.is-invalid');
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
