/**
 * FaceStone Transportation Department Management - Arabic RTL Localization
 * Handles Transaction Entry, Auto Weight Calculations, Validation, and API Persistence.
 * 
 * NOTE: Functional logic, formulas, API endpoints, DTO models, and stored database 
 * values remain completely preserved. Only visible UI text and direction are localized to Arabic.
 */

(function () {
  'use strict';

  // Localization Dictionary for Dropdown Display Text
  const ARABIC_LABELS = {
    // Material Types
    'Ready-Mix Concrete (C25/30)': 'خرسانة جاهزة (C25/30)',
    'Reinforced Concrete Mix (C35/45)': 'خرسانة مسلحة عالية المقاومة (C35/45)',
    'High-Performance Concrete (HPC)': 'خرسانة فائقة الأداء (HPC)',
    'Portland Cement (CEM I 42.5N)': 'أسمنت بورتلاندي عادي (CEM I 42.5N)',
    'Aggregate & Gravel (Coarse 20mm)': 'سن / ركام خشن (20 مم)',
    'Fine Sand (Silica / Concrete Sand)': 'رمل ناعم (رمل سيليكا / خرسانة)',
    'Fly Ash / Silica Fume Slurry': 'خبث أفران / بودرة سيليكا',
    'Precast Concrete Elements': 'عناصر خرسانية مسبقة الصب',

    // Destinations
    'Factory Sector A - Batching Plant 1': 'قطاع المصنع أ - محطة الخلط 1',
    'Factory Sector B - Precast Yard': 'قطاع المصنع ب - ساحة مسبق الصب',
    'Factory Sector C - Silos & Aggregate Storage': 'قطاع المصنع ج - صوامع وتخزين الركام',
    'New Cairo Construction Site - Zone 4': 'موقع القاهرة الجديدة - المنطقة 4',
    'New Administrative Capital - Tower Site A': 'العاصمة الإدارية الجديدة - موقع الأبراج أ',
    '6th of October Industrial Complex': 'مجمع 6 أكتوبر الصناعي',
    'Ain Sokhna Port Logistics Hub': 'ميناء العين السخنة - المركز اللوجستي',

    // Transaction Statuses
    'Pending': 'قيد الانتظار',
    'In Transit': 'في الطريق / جاري النقل',
    'Entered Factory': 'دخلت المصنع',
    'Under Inspection': 'قيد الفحص والميزان',
    'Completed': 'مكتملة / تم التفريغ',
    'Cancelled': 'ملغاة',

    // Receiving Statuses
    'Approved / Accepted': 'مقبول / معتمد',
    'Partially Accepted': 'قبول جزئي',
    'Rejected': 'مرفوض',
    'Under Quality Test': 'قيد اختبارات الجودة'
  };

  function getArabicText(str) {
    if (!str) return '';
    return ARABIC_LABELS[str] || str;
  }

  class TransportationManager {
    constructor() {
      this.isSubmitting = false;
      this.initialized = false;
      this.config = null;
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
      this.pageRoot = document.getElementById('transportation-page-root');
      if (!this.pageRoot) return;

      // Header Elements
      this.btnBackToDepts = document.getElementById('trans-back-to-departments-btn');
      this.userNameEl = document.getElementById('trans-user-name');
      this.userRoleEl = document.getElementById('trans-user-role');
      this.userAvatarEl = document.getElementById('trans-user-avatar');

      // Form Elements
      this.form = document.getElementById('trans-entry-form');
      this.txNumberInput = document.getElementById('trans-number-input');
      this.txDateInput = document.getElementById('trans-date-input');
      this.poInput = document.getElementById('trans-po-input');
      this.statusSelect = document.getElementById('trans-status-select');

      this.carInput = document.getElementById('trans-car-input');
      this.driverInput = document.getElementById('trans-driver-input');
      this.sourceInput = document.getElementById('trans-source-input');
      this.destinationSelect = document.getElementById('trans-destination-select');

      this.materialSelect = document.getElementById('trans-material-select');
      this.weightBeforeInput = document.getElementById('trans-weight-before-input');
      this.weightEnterInput = document.getElementById('trans-weight-enter-input');
      this.weightExitInput = document.getElementById('trans-weight-exit-input');

      this.expectedWeightDisplay = document.getElementById('trans-expected-weight-display');
      this.netWeightDisplay = document.getElementById('trans-net-weight-display');
      this.weightWarningEl = document.getElementById('trans-weight-warning');
      this.weightWarningText = document.getElementById('trans-weight-warning-text');

      this.receivingStatusSelect = document.getElementById('trans-receiving-status-select');
      this.notesInput = document.getElementById('trans-notes-input');
      this.submissionDateDisplay = document.getElementById('trans-submission-date-display');

      // Buttons
      this.submitBtn = document.getElementById('trans-submit-btn');
      this.submitIcon = document.getElementById('trans-submit-icon');
      this.submitSpinner = document.getElementById('trans-submit-spinner');
      this.submitBtnText = document.getElementById('trans-submit-btn-text');
      this.clearBtn = document.getElementById('trans-clear-btn');
      this.refreshBtn = document.getElementById('trans-refresh-btn');

      // Alert Banner
      this.alertBanner = document.getElementById('trans-form-alert');
      this.alertIcon = document.getElementById('trans-alert-icon');
      this.alertTitle = document.getElementById('trans-alert-title');
      this.alertDesc = document.getElementById('trans-alert-desc');
      this.alertCloseBtn = document.getElementById('trans-alert-close');

      // Confirmation Modal
      this.confirmModal = document.getElementById('trans-confirm-modal');
      this.confirmCancelBtn = document.getElementById('trans-confirm-cancel-btn');
      this.confirmProceedBtn = document.getElementById('trans-confirm-proceed-btn');

      // Table Body
      this.recentTableBody = document.getElementById('trans-recent-tbody');

      this.attachEventListeners();
      this.setDefaultDateTime();
      this.startLiveClock();
      this.loadConfig();
      this.loadRecentTransactions();
      this.initialized = true;
    }

    attachEventListeners() {
      // Back to departments
      this.btnBackToDepts?.addEventListener('click', () => {
        if (window.ChatApp && typeof window.ChatApp.showDepartmentSelectionView === 'function') {
          window.ChatApp.showDepartmentSelectionView();
        } else {
          window.location.href = '/departments';
        }
      });

      // Weight inputs change & input events for real-time calculations
      const weightInputs = [this.weightBeforeInput, this.weightEnterInput, this.weightExitInput];
      weightInputs.forEach(input => {
        if (!input) return;
        input.addEventListener('input', () => {
          this.recalculateWeights();
          this.clearFieldError(input);
        });
        input.addEventListener('change', () => {
          this.recalculateWeights();
        });
      });

      // Clear field errors on user interaction
      const allInputs = [
        this.txNumberInput, this.txDateInput, this.poInput, this.statusSelect,
        this.carInput, this.driverInput, this.sourceInput, this.destinationSelect,
        this.materialSelect, this.receivingStatusSelect, this.notesInput
      ];

      allInputs.forEach(el => {
        if (!el) return;
        el.addEventListener('input', () => this.clearFieldError(el));
        el.addEventListener('change', () => this.clearFieldError(el));
      });

      // Form submit
      this.form?.addEventListener('submit', (e) => {
        e.preventDefault();
        this.handleSubmit();
      });

      // Clear button
      this.clearBtn?.addEventListener('click', () => {
        if (this.hasFormData()) {
          this.showClearConfirmation();
        } else {
          this.resetForm();
        }
      });

      // Confirmation modal
      this.confirmCancelBtn?.addEventListener('click', () => {
        this.hideClearConfirmation();
      });

      this.confirmProceedBtn?.addEventListener('click', () => {
        this.hideClearConfirmation();
        this.resetForm();
      });

      // Alert close button
      this.alertCloseBtn?.addEventListener('click', () => {
        this.hideAlert();
      });

      // Refresh recent transactions
      this.refreshBtn?.addEventListener('click', () => {
        this.loadRecentTransactions();
      });
    }

    /**
     * Automatic Weight Calculations:
     * Expected Weight = Weight Before Loading - Weight When Exiting Factory
     * Net Weight = Weight When Entering Factory - Weight When Exiting Factory
     */
    recalculateWeights() {
      const beforeStr = this.weightBeforeInput?.value?.trim() || '';
      const enterStr = this.weightEnterInput?.value?.trim() || '';
      const exitStr = this.weightExitInput?.value?.trim() || '';

      const beforeVal = parseFloat(beforeStr);
      const enterVal = parseFloat(enterStr);
      const exitVal = parseFloat(exitStr);

      const hasBefore = !isNaN(beforeVal) && beforeStr !== '';
      const hasEnter = !isNaN(enterVal) && enterStr !== '';
      const hasExit = !isNaN(exitVal) && exitStr !== '';

      let warningMsg = null;

      // 1. Calculate Expected Weight (Before Loading - Exit Factory)
      if (hasBefore && hasExit) {
        const expected = beforeVal - exitVal;
        if (this.expectedWeightDisplay) {
          this.expectedWeightDisplay.value = expected.toFixed(3);
        }
        if (exitVal > beforeVal) {
          warningMsg = `وزن خروج المصنع (${exitVal.toFixed(3)} طن) لا يمكن أن يتجاوز الوزن قبل التحميل (${beforeVal.toFixed(3)} طن).`;
        }
      } else {
        if (this.expectedWeightDisplay) {
          this.expectedWeightDisplay.value = '0.000';
        }
      }

      // 2. Calculate Net Weight (Enter Factory - Exit Factory)
      if (hasEnter && hasExit) {
        const net = enterVal - exitVal;
        if (this.netWeightDisplay) {
          this.netWeightDisplay.value = net.toFixed(3);
        }
        if (exitVal > enterVal) {
          warningMsg = `وزن خروج المصنع (${exitVal.toFixed(3)} طن) لا يمكن أن يتجاوز وزن دخول المصنع (${enterVal.toFixed(3)} طن).`;
        }
      } else {
        if (this.netWeightDisplay) {
          this.netWeightDisplay.value = '0.000';
        }
      }

      // 3. Display or hide weight logic warning
      if (warningMsg) {
        this.showWeightWarning(warningMsg);
      } else {
        this.hideWeightWarning();
      }
    }

    showWeightWarning(msg) {
      if (this.weightWarningEl && this.weightWarningText) {
        this.weightWarningText.textContent = msg;
        this.weightWarningEl.style.display = 'flex';
      }
    }

    hideWeightWarning() {
      if (this.weightWarningEl) {
        this.weightWarningEl.style.display = 'none';
      }
    }

    /**
     * Set default local date-time in the date picker
     */
    setDefaultDateTime() {
      if (this.txDateInput && !this.txDateInput.value) {
        const now = new Date();
        now.setMinutes(now.getMinutes() - now.getTimezoneOffset());
        this.txDateInput.value = now.toISOString().slice(0, 16);
      }
    }

    startLiveClock() {
      const updateClock = () => {
        if (!this.submissionDateDisplay) return;
        const now = new Date();
        const dateStr = now.toLocaleDateString('ar-EG', {
          year: 'numeric', month: 'short', day: 'numeric'
        });
        const timeStr = now.toLocaleTimeString('ar-EG', {
          hour: '2-digit', minute: '2-digit', second: '2-digit', hour12: true
        });
        this.submissionDateDisplay.value = `${dateStr} - ${timeStr} (توقيت الخادم)`;
      };

      updateClock();
      setInterval(updateClock, 1000);
    }

    /**
     * Load configurable options from server and populate dropdowns
     * NOTE: Dropdown values sent to API remain English constants, visible labels are Arabic.
     */
    async loadConfig() {
      try {
        const res = await ChatApi.getTransportationConfig();
        if (res.success && res.data) {
          this.config = res.data;
          this.populateSelect(this.materialSelect, res.data.materialTypes, '-- اختر نوع الخامة --');
          this.populateSelect(this.destinationSelect, res.data.destinations, '-- اختر الوجهة --');
          this.populateSelect(this.statusSelect, res.data.transactionStatuses, '-- اختر حالة النقلة --');
          this.populateSelect(this.receivingStatusSelect, res.data.receivingStatuses, '-- اختر حالة الاستلام / الاعتماد --');
        } else {
          this.populateFallbackOptions();
        }
      } catch (err) {
        console.warn('Could not load transportation config from API, using defaults:', err);
        this.populateFallbackOptions();
      }
    }

    populateSelect(selectEl, items, placeholderText) {
      if (!selectEl || !Array.isArray(items)) return;
      const currentValue = selectEl.value;
      selectEl.innerHTML = `<option value="" disabled ${!currentValue ? 'selected' : ''}>${placeholderText}</option>`;
      items.forEach(item => {
        const opt = document.createElement('option');
        opt.value = item; // Preserves backend API enum value
        opt.textContent = getArabicText(item); // Displays Arabic text to user
        if (currentValue && currentValue === item) {
          opt.selected = true;
        }
        selectEl.appendChild(opt);
      });
    }

    populateFallbackOptions() {
      const materials = [
        'Ready-Mix Concrete (C25/30)',
        'Reinforced Concrete Mix (C35/45)',
        'High-Performance Concrete (HPC)',
        'Portland Cement (CEM I 42.5N)',
        'Aggregate & Gravel (Coarse 20mm)',
        'Fine Sand (Silica / Concrete Sand)',
        'Fly Ash / Silica Fume Slurry',
        'Precast Concrete Elements'
      ];
      const destinations = [
        'Factory Sector A - Batching Plant 1',
        'Factory Sector B - Precast Yard',
        'Factory Sector C - Silos & Aggregate Storage',
        'New Cairo Construction Site - Zone 4',
        'New Administrative Capital - Tower Site A',
        '6th of October Industrial Complex',
        'Ain Sokhna Port Logistics Hub'
      ];
      const txStatuses = [
        'Pending',
        'In Transit',
        'Entered Factory',
        'Under Inspection',
        'Completed',
        'Cancelled'
      ];
      const recvStatuses = [
        'Pending',
        'Approved / Accepted',
        'Partially Accepted',
        'Rejected',
        'Under Quality Test'
      ];

      this.populateSelect(this.materialSelect, materials, '-- اختر نوع الخامة --');
      this.populateSelect(this.destinationSelect, destinations, '-- اختر الوجهة --');
      this.populateSelect(this.statusSelect, txStatuses, '-- اختر حالة النقلة --');
      this.populateSelect(this.receivingStatusSelect, recvStatuses, '-- اختر حالة الاستلام / الاعتماد --');
    }

    /**
     * Check if form currently has user-entered data
     */
    hasFormData() {
      return !!(
        this.txNumberInput?.value?.trim() ||
        this.poInput?.value?.trim() ||
        this.carInput?.value?.trim() ||
        this.driverInput?.value?.trim() ||
        this.sourceInput?.value?.trim() ||
        this.materialSelect?.value ||
        this.weightBeforeInput?.value?.trim() ||
        this.weightEnterInput?.value?.trim() ||
        this.weightExitInput?.value?.trim() ||
        this.destinationSelect?.value ||
        this.statusSelect?.value ||
        this.receivingStatusSelect?.value ||
        this.notesInput?.value?.trim()
      );
    }

    showClearConfirmation() {
      if (this.confirmModal) {
        this.confirmModal.style.display = 'flex';
      }
    }

    hideClearConfirmation() {
      if (this.confirmModal) {
        this.confirmModal.style.display = 'none';
      }
    }

    resetForm() {
      if (this.form) {
        this.form.reset();
      }
      this.clearAllFieldErrors();
      this.hideWeightWarning();
      if (this.expectedWeightDisplay) this.expectedWeightDisplay.value = '0.000';
      if (this.netWeightDisplay) this.netWeightDisplay.value = '0.000';
      this.setDefaultDateTime();
      this.hideAlert();
    }

    clearFieldError(element) {
      if (!element) return;
      element.classList.remove('input-invalid');
      const group = element.closest('.trans-form-group');
      if (group) {
        group.classList.remove('has-error');
        const errSpan = group.querySelector('.trans-field-error');
        if (errSpan) {
          errSpan.textContent = '';
        }
      }
    }

    setFieldError(element, message) {
      if (!element) return;
      element.classList.add('input-invalid');
      const group = element.closest('.trans-form-group');
      if (group) {
        group.classList.add('has-error');
        const errSpan = group.querySelector('.trans-field-error');
        if (errSpan) {
          errSpan.textContent = message;
        }
      }
    }

    clearAllFieldErrors() {
      const inputs = this.form?.querySelectorAll('.trans-input, .trans-select, .trans-textarea') || [];
      inputs.forEach(el => el.classList.remove('input-invalid'));
      const groups = this.form?.querySelectorAll('.trans-form-group') || [];
      groups.forEach(g => {
        g.classList.remove('has-error');
        const errSpan = g.querySelector('.trans-field-error');
        if (errSpan) errSpan.textContent = '';
      });
    }

    /**
     * Validate all fields according to business rules with Arabic messages
     */
    validateForm() {
      this.clearAllFieldErrors();
      let isValid = true;
      let firstInvalidEl = null;

      const markInvalid = (el, msg) => {
        this.setFieldError(el, msg);
        isValid = false;
        if (!firstInvalidEl) firstInvalidEl = el;
      };

      // 1. Transaction Number (رقم الحركة)
      const txNumStr = this.txNumberInput?.value?.trim() || '';
      if (!txNumStr) {
        markInvalid(this.txNumberInput, 'رقم الحركة مطلوب.');
      } else {
        const txNum = parseInt(txNumStr, 10);
        if (isNaN(txNum) || txNum <= 0 || !/^\d+$/.test(txNumStr)) {
          markInvalid(this.txNumberInput, 'يرجى إدخال رقم صحيح موجب.');
        }
      }

      // 2. Transaction Date (التاريخ)
      const dateStr = this.txDateInput?.value?.trim() || '';
      if (!dateStr) {
        markInvalid(this.txDateInput, 'التاريخ مطلوب.');
      } else {
        const d = new Date(dateStr);
        if (isNaN(d.getTime())) {
          markInvalid(this.txDateInput, 'يرجى اختيار تاريخ ووقت صحيح.');
        }
      }

      // 3. PO Number (رقم أمر الشراء)
      const poStr = this.poInput?.value?.trim() || '';
      if (!poStr) {
        markInvalid(this.poInput, 'رقم أمر الشراء (PO) مطلوب.');
      }

      // 4. Transaction Status (حالة النقلة)
      const statusStr = this.statusSelect?.value || '';
      if (!statusStr) {
        markInvalid(this.statusSelect, 'يرجى اختيار حالة النقلة.');
      }

      // 5. Car Number (رقم السيارة)
      const carStr = this.carInput?.value?.trim() || '';
      if (!carStr) {
        markInvalid(this.carInput, 'رقم السيارة مطلوب.');
      }

      // 6. Driver's Name (اسم السائق)
      const driverStr = this.driverInput?.value?.trim() || '';
      if (!driverStr) {
        markInvalid(this.driverInput, 'اسم السائق مطلوب.');
      }

      // 7. Source (المورد / المصدر)
      const sourceStr = this.sourceInput?.value?.trim() || '';
      if (!sourceStr) {
        markInvalid(this.sourceInput, 'المورد / المصدر مطلوب.');
      }

      // 8. Destination (الوجهة)
      const destStr = this.destinationSelect?.value || '';
      if (!destStr) {
        markInvalid(this.destinationSelect, 'يرجى اختيار الوجهة.');
      }

      // 9. Material Type (نوع الخامة)
      const matStr = this.materialSelect?.value || '';
      if (!matStr) {
        markInvalid(this.materialSelect, 'يرجى اختيار نوع الخامة.');
      }

      // 10. Weight Before Loading (الوزن قبل التحميل)
      const beforeStr = this.weightBeforeInput?.value?.trim() || '';
      const beforeVal = parseFloat(beforeStr);
      if (!beforeStr) {
        markInvalid(this.weightBeforeInput, 'الوزن قبل التحميل مطلوب.');
      } else if (isNaN(beforeVal) || beforeVal <= 0) {
        markInvalid(this.weightBeforeInput, 'يجب أن يكون الوزن قيمة رقمية موجبة.');
      }

      // 11. Weight When Entering Factory (وزن دخول المصنع)
      const enterStr = this.weightEnterInput?.value?.trim() || '';
      const enterVal = parseFloat(enterStr);
      if (!enterStr) {
        markInvalid(this.weightEnterInput, 'وزن دخول المصنع مطلوب.');
      } else if (isNaN(enterVal) || enterVal <= 0) {
        markInvalid(this.weightEnterInput, 'يجب أن يكون الوزن قيمة رقمية موجبة.');
      }

      // 12. Weight When Exiting Factory (وزن خروج المصنع)
      const exitStr = this.weightExitInput?.value?.trim() || '';
      const exitVal = parseFloat(exitStr);
      if (!exitStr) {
        markInvalid(this.weightExitInput, 'وزن خروج المصنع مطلوب.');
      } else if (isNaN(exitVal) || exitVal <= 0) {
        markInvalid(this.weightExitInput, 'يجب أن يكون الوزن قيمة رقمية موجبة.');
      }

      // 13. Logical Weight Rules
      if (!isNaN(enterVal) && !isNaN(exitVal) && enterVal > 0 && exitVal > 0) {
        if (exitVal > enterVal) {
          markInvalid(this.weightExitInput, 'وزن خروج المصنع لا يمكن أن يتجاوز وزن دخول المصنع.');
        }
      }
      if (!isNaN(beforeVal) && !isNaN(exitVal) && beforeVal > 0 && exitVal > 0) {
        if (exitVal > beforeVal) {
          markInvalid(this.weightExitInput, 'وزن خروج المصنع لا يمكن أن يتجاوز الوزن قبل التحميل.');
        }
      }

      // 14. Receiving Status (حالة الاستلام / الاعتماد)
      const recvStr = this.receivingStatusSelect?.value || '';
      if (!recvStr) {
        markInvalid(this.receivingStatusSelect, 'يرجى اختيار حالة الاستلام / الاعتماد.');
      }

      if (!isValid && firstInvalidEl) {
        firstInvalidEl.focus();
        firstInvalidEl.scrollIntoView({ behavior: 'smooth', block: 'center' });
      }

      return isValid;
    }

    /**
     * Submit Form to API
     */
    async handleSubmit() {
      if (this.isSubmitting) return;

      if (!this.validateForm()) {
        this.showAlert('يرجى تصحيح الحقول المحددة باللون الأحمر قبل الحفظ.', 'error');
        return;
      }

      this.isSubmitting = true;
      this.setSubmitLoading(true);
      this.hideAlert();

      const txNumber = parseInt(this.txNumberInput.value.trim(), 10);
      const txDateIso = new Date(this.txDateInput.value.trim()).toISOString();
      const carNumber = this.carInput.value.trim();
      const driverName = this.driverInput.value.trim();
      const source = this.sourceInput.value.trim();
      const poNumber = this.poInput.value.trim();
      const materialType = this.materialSelect.value;
      const weightBefore = parseFloat(this.weightBeforeInput.value.trim());
      const weightEnter = parseFloat(this.weightEnterInput.value.trim());
      const weightExit = parseFloat(this.weightExitInput.value.trim());
      const destination = this.destinationSelect.value;
      const transactionStatus = this.statusSelect.value;
      const receivingStatus = this.receivingStatusSelect.value;
      const notes = this.notesInput.value.trim() || null;

      const payload = {
        transactionNumber: txNumber,
        transactionDate: txDateIso,
        carNumber: carNumber,
        driverName: driverName,
        source: source,
        poNumber: poNumber,
        materialType: materialType,
        weightBeforeLoading: weightBefore,
        weightWhenEnteringFactory: weightEnter,
        weightWhenExitingFactory: weightExit,
        destination: destination,
        transactionStatus: transactionStatus,
        receivingStatus: receivingStatus,
        notes: notes
      };

      try {
        const res = await ChatApi.submitTransportation(payload);

        if (res.success && res.data) {
          const created = res.data;
          this.showAlert(
            `تم حفظ الحركة بنجاح! رقم الحركة: #${created.transactionNumber} - الوزن الفعلي: ${created.netWeight.toFixed(3)} طن.`,
            'success'
          );

          // Reset form fields
          this.form.reset();
          this.clearAllFieldErrors();
          this.hideWeightWarning();
          if (this.expectedWeightDisplay) this.expectedWeightDisplay.value = '0.000';
          if (this.netWeightDisplay) this.netWeightDisplay.value = '0.000';
          this.setDefaultDateTime();

          // Refresh recent transactions
          await this.loadRecentTransactions();

          // Scroll to alert banner smoothly
          this.alertBanner?.scrollIntoView({ behavior: 'smooth', block: 'nearest' });
        } else {
          // Check for duplicate transaction number error
          const msg = res.message || 'فشل في حفظ الحركة.';
          if (msg.toLowerCase().includes('already exists') || (res.status === 400 && msg.includes('Transaction Number'))) {
            this.setFieldError(this.txNumberInput, 'رقم الحركة مسجل بالفعل.');
            this.txNumberInput.focus();
            this.showAlert('رقم الحركة مسجل بالفعل. يرجى إدخال رقم حركة فريد.', 'error');
          } else {
            this.showAlert(msg, 'error');
          }
        }
      } catch (err) {
        console.error('Error submitting transportation transaction:', err);
        this.showAlert('حدث خطأ في الاتصال بالخادم. يرجى المحاولة مرة أخرى.', 'error');
      } finally {
        this.isSubmitting = false;
        this.setSubmitLoading(false);
      }
    }

    setSubmitLoading(isLoading) {
      if (!this.submitBtn) return;
      this.submitBtn.disabled = isLoading;
      if (this.submitSpinner) this.submitSpinner.style.display = isLoading ? 'inline-block' : 'none';
      if (this.submitIcon) this.submitIcon.style.display = isLoading ? 'none' : 'inline-block';
      if (this.submitBtnText) this.submitBtnText.textContent = isLoading ? 'جاري الحفظ...' : 'حفظ';
    }

    showAlert(message, type = 'success') {
      if (!this.alertBanner) return;
      this.alertBanner.className = `trans-alert-banner ${type}`;
      if (this.alertTitle) {
        this.alertTitle.textContent = type === 'success' ? 'نجاح' : 'خطأ';
      }
      if (this.alertDesc) {
        this.alertDesc.textContent = message;
      }
      if (this.alertIcon) {
        this.alertIcon.textContent = type === 'success' ? '✓' : '!';
      }
      this.alertBanner.style.display = 'flex';
    }

    hideAlert() {
      if (this.alertBanner) {
        this.alertBanner.style.display = 'none';
      }
    }

    /**
     * Load recent transactions from database
     */
    async loadRecentTransactions() {
      if (!this.recentTableBody) return;
      try {
        const res = await ChatApi.getRecentTransportationTransactions(10);
        if (res.success && Array.isArray(res.data) && res.data.length > 0) {
          this.renderRecentTable(res.data);
        } else {
          this.recentTableBody.innerHTML = `
            <tr>
              <td colspan="11" class="trans-empty-row">لا توجد حركات مسجلة حتى الآن. قم بتسجيل حركة جديدة أعلاه لإضافتها للسجل.</td>
            </tr>
          `;
        }
      } catch (err) {
        console.warn('Failed to fetch recent transportation transactions:', err);
      }
    }

    renderRecentTable(items) {
      if (!this.recentTableBody) return;
      this.recentTableBody.innerHTML = items.map(tx => {
        const dateFormatted = new Date(tx.transactionDate).toLocaleDateString('ar-EG', {
          month: 'short', day: 'numeric', year: 'numeric'
        });
        const submitFormatted = new Date(tx.dateOfSubmit).toLocaleTimeString('ar-EG', {
          hour: '2-digit', minute: '2-digit', hour12: true
        });

        const statusClass = (tx.transactionStatus || '').toLowerCase().replace(/\s+/g, '-');
        const recvClass = (tx.receivingStatus || '').toLowerCase().replace(/[^a-z0-9]/g, '-');

        const displayMaterial = getArabicText(tx.materialType);
        const displayDestination = getArabicText(tx.destination);
        const displayStatus = getArabicText(tx.transactionStatus);
        const displayReceiving = getArabicText(tx.receivingStatus);

        return `
          <tr>
            <td><strong class="trans-tx-cell">#${tx.transactionNumber}</strong></td>
            <td>${dateFormatted}</td>
            <td><span class="trans-code-badge">${this.escapeHtml(tx.carNumber)}</span></td>
            <td>${this.escapeHtml(tx.driverName)}</td>
            <td><span class="trans-material-cell">${this.escapeHtml(displayMaterial)}</span></td>
            <td><strong class="trans-net-weight-cell">${tx.netWeight.toFixed(3)} طن</strong></td>
            <td>${tx.expectedWeight.toFixed(3)} طن</td>
            <td class="trans-dest-cell">${this.escapeHtml(displayDestination)}</td>
            <td><span class="trans-status-pill status-${statusClass}">${this.escapeHtml(displayStatus)}</span></td>
            <td><span class="trans-status-pill recv-${recvClass}">${this.escapeHtml(displayReceiving)}</span></td>
            <td class="trans-time-cell">${submitFormatted}</td>
          </tr>
        `;
      }).join('');
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

    /**
     * Updates user information badge when viewing Transportation department
     */
    updateUserBadge(user) {
      if (!user) return;
      if (this.userNameEl) {
        this.userNameEl.textContent = user.fullName || user.username || 'مستخدم مصرح له';
      }
      if (this.userRoleEl) {
        let roleArabic = user.role || 'إدارة النقل';
        if (user.role === 'CEO') roleArabic = 'الرئيس التنفيذي';
        else if (user.role === 'IT') roleArabic = 'الدعم الفني / IT';
        else if (user.role === 'Transportation Manager') roleArabic = 'مدير إدارة النقل';
        this.userRoleEl.textContent = roleArabic;
      }
      if (this.userAvatarEl) {
        const initials = (user.fullName || user.username || 'FS')
          .split(' ')
          .filter(Boolean)
          .map(w => w[0])
          .slice(0, 2)
          .join('')
          .toUpperCase();
        this.userAvatarEl.textContent = initials || 'FS';
      }
    }
  }

  // Create global instance
  window.TransportationApp = new TransportationManager();

  // Hook into ChatApp view transitions if available
  document.addEventListener('chat:view:changed', (e) => {
    if (e.detail && e.detail.view === 'transportation') {
      const user = window.ChatApi?.getCurrentUser();
      window.TransportationApp.updateUserBadge(user);
      window.TransportationApp.loadRecentTransactions();
    }
  });

})();
