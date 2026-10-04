/**
 * FaceStone Transportation Department Management - 4-Section Ordered Workflow
 * Strictly adheres to FaceStone business requirements:
 * 1. الميزان (Internal & External Weighbridge, Departure/Arrival times entered ONLY here, Shipping Time calculation box, mandatory Attachment)
 * 2. يومية التوريد (Sequential Transaction Number per PO, Shipment Date, read-only weights from Section 1, Driver Phone, read-only Departure & Arrival times, Source, PO, mandatory Attachment)
 * 3. بوليصة الشحن (Company, Destination, Shipping Date, Quantity = Net Weight, Material Type powder/slurry, Supervisors, Plate, Driver, Recipient Name & Title, Delivery Status Checklist, Receiving Date, read-only Shipping Time, mandatory Attachment)
 * 4. اكرامية / ملاحظات (Decimal Ikramiya, Notes, Submit & Clear)
 * 
 * NOTE: Total Quantity and Remaining Quantity are NEVER displayed on this page.
 */
(function () {
  'use strict';

  class TransportationManager {
    constructor() {
      this.isSubmitting = false;
      this.config = null;
      this.currentPo = null;

      // Section file attachments
      this.wbAttachmentFile = null;
      this.dsAttachmentFile = null;
      this.bolAttachmentFile = null;

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

      // Steps
      this.stepPoEntry = document.getElementById('trans-step-po-entry');
      this.stepForm = document.getElementById('trans-step-form');
      this.stepSuccess = document.getElementById('trans-step-success');

      // User pill & Nav
      this.btnBackToDepts = document.getElementById('trans-back-to-departments-btn');
      this.userNameEl = document.getElementById('trans-user-name');
      this.userRoleEl = document.getElementById('trans-user-role');
      this.userAvatarEl = document.getElementById('trans-user-avatar');

      // Step 1: PO Entry
      this.poLookupForm = document.getElementById('trans-po-lookup-form');
      this.poEntryInput = document.getElementById('trans-po-entry-input');
      this.poContinueBtn = document.getElementById('trans-po-continue-btn');
      this.poSpinner = document.getElementById('trans-po-spinner');
      this.poContinueText = document.getElementById('trans-po-continue-text');
      this.poErrorAlert = document.getElementById('trans-po-error-alert');
      this.poErrorTitle = document.getElementById('trans-po-error-title');
      this.poErrorDesc = document.getElementById('trans-po-error-desc');
      this.poErrorClose = document.getElementById('trans-po-error-close');

      // Step 2: Form Header & Summary Bar
      this.btnChangePo = document.getElementById('trans-btn-change-po');
      this.verifiedPoBadge = document.getElementById('trans-verified-po-badge');
      this.summaryPo = document.getElementById('trans-summary-po');
      this.summarySupplier = document.getElementById('trans-summary-supplier');
      this.summaryDestination = document.getElementById('trans-summary-destination');
      this.summarySource = document.getElementById('trans-summary-source');

      // Form Container & Alert
      this.form = document.getElementById('trans-entry-form');
      this.alertBanner = document.getElementById('trans-form-alert');
      this.alertIcon = document.getElementById('trans-alert-icon');
      this.alertTitle = document.getElementById('trans-alert-title');
      this.alertDesc = document.getElementById('trans-alert-desc');
      this.alertCloseBtn = document.getElementById('trans-alert-close');

      // ==============================================================
      // 1. SECTION 1: الميزان
      // ==============================================================
      // 1.1 Internal Weighbridge
      this.wbInternalCodeInput = document.getElementById('trans-wb-internal-code');
      this.wbIntLoaded = document.getElementById('trans-wb-int-loaded');
      this.wbIntEmpty = document.getElementById('trans-wb-int-empty');
      this.wbIntNet = document.getElementById('trans-wb-int-net');
      this.wbIntEmptyTime = document.getElementById('trans-wb-int-empty-time');
      this.wbIntLoadedTime = document.getElementById('trans-wb-int-loaded-time');

      // 1.2 External Weighbridge
      this.wbExternalCodeInput = document.getElementById('trans-wb-external-code');
      this.wbExtLoaded = document.getElementById('trans-wb-ext-loaded');
      this.wbExtEmpty = document.getElementById('trans-wb-ext-empty');
      this.wbExtNet = document.getElementById('trans-wb-ext-net');
      this.wbExtEmptyTime = document.getElementById('trans-wb-ext-empty-time');
      this.wbExtLoadedTime = document.getElementById('trans-wb-ext-loaded-time');

      // Movement & Shipment Dates (Derived automatically from Weighbridge Timestamps)
      this.txDateInput = document.getElementById('trans-date-input');
      this.departureDisplay = document.getElementById('trans-departure-display');
      this.arrivalDisplay = document.getElementById('trans-arrival-display');

      // Shipping Time Box
      this.shippingTimeDisplay = document.getElementById('trans-shipping-time-display');
      this.shippingTimeError = document.getElementById('trans-shipping-time-error');

      // Section 1 Attachment: Weighbridge
      this.wbAttachmentInput = document.getElementById('trans-wb-attachment-input');
      this.wbDropzone = document.getElementById('trans-wb-dropzone');
      this.wbPlaceholder = document.getElementById('trans-wb-placeholder');
      this.wbFilePreview = document.getElementById('trans-wb-file-preview');
      this.wbFileName = document.getElementById('trans-wb-file-name');
      this.wbFileSize = document.getElementById('trans-wb-file-size');
      this.wbRemoveBtn = document.getElementById('trans-wb-remove-btn');
      this.wbAttachmentError = document.getElementById('trans-wb-attachment-error');

      // ==============================================================
      // 2. SECTION 2: يومية التوريد
      // ==============================================================
      this.txNumberInput = document.getElementById('trans-number-input');
      this.dailyShipDateDisplay = document.getElementById('trans-daily-ship-date-display');
      this.internalNetDisplay = document.getElementById('trans-internal-net-display');
      this.externalNetDisplay = document.getElementById('trans-external-net-display');
      this.netWeightDisplay = document.getElementById('trans-net-weight-display');
      this.wasteDisplay = document.getElementById('trans-waste-display');
      this.driverPhoneInput = document.getElementById('trans-driver-phone-input');
      this.dailyDepartureDisplay = document.getElementById('trans-daily-departure-display');
      this.dailyArrivalDisplay = document.getElementById('trans-daily-arrival-display');
      this.sourceDisplay = document.getElementById('trans-source-display');
      this.dailyPoDisplay = document.getElementById('trans-daily-po-display');

      // Section 2 Attachment: Daily Supply
      this.dsAttachmentInput = document.getElementById('trans-ds-attachment-input');
      this.dsDropzone = document.getElementById('trans-ds-dropzone');
      this.dsPlaceholder = document.getElementById('trans-ds-placeholder');
      this.dsFilePreview = document.getElementById('trans-ds-file-preview');
      this.dsFileName = document.getElementById('trans-ds-file-name');
      this.dsFileSize = document.getElementById('trans-ds-file-size');
      this.dsRemoveBtn = document.getElementById('trans-ds-remove-btn');
      this.dsAttachmentError = document.getElementById('trans-ds-attachment-error');

      // ==============================================================
      // 3. SECTION 3: بوليصة الشحن
      // ==============================================================
      this.companyInput = document.getElementById('trans-company-input');
      this.destinationInput = document.getElementById('trans-destination-input');
      this.shipDateDisplay = document.getElementById('trans-ship-date-display');
      this.quantityDisplay = document.getElementById('trans-quantity-display');
      this.materialSelect = document.getElementById('trans-material-select');
      this.shipSupSelect = document.getElementById('trans-ship-sup-select');
      this.deptSupSelect = document.getElementById('trans-dept-sup-select');
      this.carInput = document.getElementById('trans-car-input');
      this.driverSelect = document.getElementById('trans-driver-select');
      this.recNameInput = document.getElementById('trans-rec-name-input');
      this.recTitleSelect = document.getElementById('trans-rec-title-select');
      this.statusSafeCheckbox = document.getElementById('trans-status-safe');
      this.statusIntactCheckbox = document.getElementById('trans-status-intact');
      this.recStatusError = document.getElementById('trans-rec-status-error');
      this.recDateInput = document.getElementById('trans-rec-date-input');
      this.bolShippingTimeDisplay = document.getElementById('trans-bol-shipping-time-display');

      // Section 3 Attachment: Bill of Lading
      this.bolAttachmentInput = document.getElementById('trans-bol-attachment-input');
      this.bolDropzone = document.getElementById('trans-bol-dropzone');
      this.bolPlaceholder = document.getElementById('trans-bol-placeholder');
      this.bolFilePreview = document.getElementById('trans-bol-file-preview');
      this.bolFileName = document.getElementById('trans-bol-file-name');
      this.bolFileSize = document.getElementById('trans-bol-file-size');
      this.bolRemoveBtn = document.getElementById('trans-bol-remove-btn');
      this.bolAttachmentError = document.getElementById('trans-bol-attachment-error');

      // ==============================================================
      // 4. SECTION 4: اكرامية / ملاحظات
      // ==============================================================
      this.custodyInput = document.getElementById('trans-custody-input');
      this.ikramiyaInput = document.getElementById('trans-ikramiya-input');
      this.notesInput = document.getElementById('trans-notes-input');

      // Actions
      this.submitBtn = document.getElementById('trans-submit-btn');
      this.submitIcon = document.getElementById('trans-submit-icon');
      this.submitSpinner = document.getElementById('trans-submit-spinner');
      this.submitBtnText = document.getElementById('trans-submit-btn-text');
      this.clearBtn = document.getElementById('trans-clear-btn');
      this.refreshBtn = document.getElementById('trans-refresh-btn');

      // Confirmation Modal
      this.confirmModal = document.getElementById('trans-confirm-modal');
      this.confirmCancelBtn = document.getElementById('trans-confirm-cancel-btn');
      this.confirmProceedBtn = document.getElementById('trans-confirm-proceed-btn');

      // Success Step Elements
      this.successTxNumber = document.getElementById('trans-success-tx-number');
      this.successPoNumber = document.getElementById('trans-success-po-number');
      this.successCompany = document.getElementById('trans-success-company');
      this.successDestination = document.getElementById('trans-success-destination');
      this.successMaterial = document.getElementById('trans-success-material');
      this.successIntWeight = document.getElementById('trans-success-int-weight');
      this.successExtWeight = document.getElementById('trans-success-ext-weight');
      this.successNetWeight = document.getElementById('trans-success-net-weight');
      this.successWaste = document.getElementById('trans-success-waste');
      this.successShippingTime = document.getElementById('trans-success-shipping-time');
      this.btnNewPo = document.getElementById('trans-btn-new-po');
      this.btnViewRecentFromSuccess = document.getElementById('trans-btn-view-recent-from-success');

      // Recent Transactions Table
      this.recentTableBody = document.getElementById('trans-recent-tbody');

      this.bindEvents();
      this.setupAttachmentHandlers();
      this.loadConfig();
    }

    bindEvents() {
      // Step 1: PO Lookup
      this.poLookupForm?.addEventListener('submit', (e) => {
        e.preventDefault();
        this.handlePoLookup();
      });

      this.poErrorClose?.addEventListener('click', () => {
        if (this.poErrorAlert) this.poErrorAlert.style.display = 'none';
      });

      // Quick PO Chips
      document.querySelectorAll('.trans-po-chip').forEach(btn => {
        btn.addEventListener('click', () => {
          const po = btn.getAttribute('data-po');
          if (this.poEntryInput && po) {
            this.poEntryInput.value = po;
            this.handlePoLookup();
          }
        });
      });

      // Change PO button
      this.btnChangePo?.addEventListener('click', () => {
        this.showPoEntryStep();
      });

      // Navigation back to departments
      this.btnBackToDepts?.addEventListener('click', () => {
        if (window.ChatApp?.showView) {
          window.ChatApp.showView('departments');
        }
      });

      // Weight inputs in Section 1 (ONLY location for weight input)
      [this.wbIntLoaded, this.wbIntEmpty, this.wbExtLoaded, this.wbExtEmpty].forEach(input => {
        input?.addEventListener('input', () => this.recalculateAllWeights());
      });

      // Movement & Shipment Dates: Populating Sections 2 & 3
      this.txDateInput?.addEventListener('input', () => {
        const val = this.formatDateTimeDisplay(this.txDateInput.value);
        if (this.dailyShipDateDisplay) this.dailyShipDateDisplay.value = val;
        if (this.shipDateDisplay) this.shipDateDisplay.value = val;
      });

      // Weighbridge Timestamps auto-deriving Departure, Arrival, and Shipping Time
      const onIntLoadedChange = () => {
        const val = this.formatDateTimeDisplay(this.wbIntLoadedTime.value);
        if (this.departureDisplay) this.departureDisplay.value = val;
        if (this.dailyDepartureDisplay) this.dailyDepartureDisplay.value = val;
        this.recalculateShippingTime();
      };
      this.wbIntLoadedTime?.addEventListener('input', onIntLoadedChange);
      this.wbIntLoadedTime?.addEventListener('change', onIntLoadedChange);

      const onExtLoadedChange = () => {
        const val = this.formatDateTimeDisplay(this.wbExtLoadedTime.value);
        if (this.arrivalDisplay) this.arrivalDisplay.value = val;
        if (this.dailyArrivalDisplay) this.dailyArrivalDisplay.value = val;
        this.recalculateShippingTime();
      };
      this.wbExtLoadedTime?.addEventListener('input', onExtLoadedChange);
      this.wbExtLoadedTime?.addEventListener('change', onExtLoadedChange);

      // Form Submission
      this.form?.addEventListener('submit', (e) => {
        e.preventDefault();
        this.handleFormSubmit();
      });

      // Form Clear
      this.clearBtn?.addEventListener('click', () => {
        if (this.confirmModal) {
          this.confirmModal.style.display = 'flex';
        } else {
          this.resetFormKeepPo();
        }
      });
      this.confirmCancelBtn?.addEventListener('click', () => {
        if (this.confirmModal) this.confirmModal.style.display = 'none';
      });
      this.confirmProceedBtn?.addEventListener('click', () => {
        if (this.confirmModal) this.confirmModal.style.display = 'none';
        this.resetFormKeepPo();
      });

      // Alert close
      this.alertCloseBtn?.addEventListener('click', () => {
        this.hideAlert();
      });

      // Refresh recent table
      this.refreshBtn?.addEventListener('click', () => {
        this.loadRecentTransactions();
      });

      // Success Actions
      this.btnNewPo?.addEventListener('click', () => {
        this.showPoEntryStep();
      });
      this.btnViewRecentFromSuccess?.addEventListener('click', () => {
        const recentSec = document.getElementById('trans-recent-section');
        if (recentSec) {
          recentSec.scrollIntoView({ behavior: 'smooth' });
        }
      });
    }

    setupAttachmentHandlers() {
      // 1. Weighbridge Attachment
      this.setupSingleAttachment(
        this.wbAttachmentInput,
        this.wbDropzone,
        this.wbPlaceholder,
        this.wbFilePreview,
        this.wbFileName,
        this.wbFileSize,
        this.wbRemoveBtn,
        this.wbAttachmentError,
        (file) => { this.wbAttachmentFile = file; }
      );

      // 2. Daily Supply Attachment
      this.setupSingleAttachment(
        this.dsAttachmentInput,
        this.dsDropzone,
        this.dsPlaceholder,
        this.dsFilePreview,
        this.dsFileName,
        this.dsFileSize,
        this.dsRemoveBtn,
        this.dsAttachmentError,
        (file) => { this.dsAttachmentFile = file; }
      );

      // 3. Bill of Lading Attachment
      this.setupSingleAttachment(
        this.bolAttachmentInput,
        this.bolDropzone,
        this.bolPlaceholder,
        this.bolFilePreview,
        this.bolFileName,
        this.bolFileSize,
        this.bolRemoveBtn,
        this.bolAttachmentError,
        (file) => { this.bolAttachmentFile = file; }
      );
    }

    setupSingleAttachment(inputEl, dropzoneEl, placeholderEl, previewEl, nameEl, sizeEl, removeBtnEl, errorEl, setFileCallback) {
      if (!inputEl) return;

      const allowedExts = ['.pdf', '.jpg', '.jpeg'];
      const maxSizeBytes = 25 * 1024 * 1024; // 25 MB

      const handleSelectedFile = (file) => {
        if (!file) return;

        if (errorEl) {
          errorEl.textContent = '';
          errorEl.style.display = 'none';
        }

        const ext = '.' + file.name.split('.').pop().toLowerCase();
        if (!allowedExts.includes(ext)) {
          if (errorEl) {
            errorEl.textContent = 'نوع الملف غير مدعوم. الملفات المسموحة فقط: PDF, JPG, JPEG.';
            errorEl.style.display = 'block';
          }
          return;
        }

        if (file.size > maxSizeBytes) {
          if (errorEl) {
            errorEl.textContent = 'حجم الملف يتجاوز الحد الأقصى المسموح (25 ميجابايت).';
            errorEl.style.display = 'block';
          }
          return;
        }

        setFileCallback(file);

        if (nameEl) nameEl.textContent = file.name;
        if (sizeEl) sizeEl.textContent = this.formatFileSize(file.size);
        if (placeholderEl) placeholderEl.style.display = 'none';
        if (previewEl) previewEl.style.display = 'flex';
      };

      inputEl.addEventListener('change', () => {
        if (inputEl.files && inputEl.files[0]) {
          handleSelectedFile(inputEl.files[0]);
        }
      });

      removeBtnEl?.addEventListener('click', (e) => {
        e.stopPropagation();
        inputEl.value = '';
        setFileCallback(null);
        if (placeholderEl) placeholderEl.style.display = 'flex';
        if (previewEl) previewEl.style.display = 'none';
        if (errorEl) {
          errorEl.textContent = '';
          errorEl.style.display = 'none';
        }
      });

      // Drag & Drop
      if (dropzoneEl) {
        ['dragenter', 'dragover'].forEach(eventName => {
          dropzoneEl.addEventListener(eventName, (e) => {
            e.preventDefault();
            e.stopPropagation();
            dropzoneEl.classList.add('dragover');
          });
        });

        ['dragleave', 'drop'].forEach(eventName => {
          dropzoneEl.addEventListener(eventName, (e) => {
            e.preventDefault();
            e.stopPropagation();
            dropzoneEl.classList.remove('dragover');
          });
        });

        dropzoneEl.addEventListener('drop', (e) => {
          if (e.dataTransfer && e.dataTransfer.files && e.dataTransfer.files[0]) {
            inputEl.files = e.dataTransfer.files;
            handleSelectedFile(e.dataTransfer.files[0]);
          }
        });
      }
    }

    formatFileSize(bytes) {
      if (bytes < 1024) return `${bytes} B`;
      if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(1)} KB`;
      return `${(bytes / (1024 * 1024)).toFixed(2)} MB`;
    }

    async loadConfig() {
      try {
        const res = await window.ChatApi?.getTransportationConfig();
        if (res && res.success && res.data) {
          this.config = res.data;
          this.populateDropdowns(res.data);
        }
      } catch (err) {
        console.error('Failed to load transportation config:', err);
      }
    }

    populateDropdowns(cfg) {
      if (this.driverSelect) {
        const currentVal = this.driverSelect.value;
        this.driverSelect.innerHTML = '<option value="" disabled selected>-- اختر السائق من السجلات المعتمدة --</option>';
        if (cfg.drivers && cfg.drivers.length > 0) {
          cfg.drivers.forEach(d => {
            const opt = document.createElement('option');
            opt.value = d;
            opt.textContent = d;
            this.driverSelect.appendChild(opt);
          });
        }
        if (currentVal) this.driverSelect.value = currentVal;
      }
    }

    showPoEntryStep() {
      if (this.stepPoEntry) this.stepPoEntry.style.display = 'block';
      if (this.stepForm) this.stepForm.style.display = 'none';
      if (this.stepSuccess) this.stepSuccess.style.display = 'none';
      if (this.poErrorAlert) this.poErrorAlert.style.display = 'none';
      if (this.poEntryInput) {
        this.poEntryInput.value = '';
        setTimeout(() => this.poEntryInput.focus(), 100);
      }
    }

    showFormStep(poData) {
      this.currentPo = poData;
      if (this.stepPoEntry) this.stepPoEntry.style.display = 'none';
      if (this.stepSuccess) this.stepSuccess.style.display = 'none';
      if (this.stepForm) this.stepForm.style.display = 'block';

      const companyName = poData.companyName || poData.supplier || 'FaceStone Partner';
      const destination = poData.destination || 'Cairo Factory';

      // Summary Bar (Strictly NO Total Quantity and NO Remaining Quantity)
      if (this.summaryPo) this.summaryPo.textContent = poData.poNumber;
      if (this.summarySupplier) this.summarySupplier.textContent = companyName;
      if (this.summaryDestination) this.summaryDestination.textContent = destination;
      if (this.verifiedPoBadge) this.verifiedPoBadge.textContent = poData.poNumber;

      // Section 2: Daily Supply PO display
      if (this.dailyPoDisplay) this.dailyPoDisplay.value = poData.poNumber;

      // Section 3: Prepopulated & Read-only PO fields
      if (this.companyInput) this.companyInput.value = companyName;
      if (this.destinationInput) this.destinationInput.value = destination;

      // Source fixed
      if (this.sourceDisplay) this.sourceDisplay.value = 'Face stone (Full Concrete Solutions)';

      // Auto-generate Transaction Number & Weighbridge Codes PER PO
      this.assignNextTransactionAndCodes(poData);

      // Set default current timestamps
      this.setDefaultTimestamps();

      // Recalculate weights
      this.recalculateAllWeights();
      this.hideAlert();
    }

    assignNextTransactionAndCodes(poData) {
      const nextNum = poData?.nextTransactionNumber || 1;
      if (this.txNumberInput) {
        this.txNumberInput.value = nextNum;
      }

      // Requirements 1, 1A, 1B: Weighbridge Reference numbers must be entered MANUALLY by employee.
      // Must NOT be generated or driven automatically by the system.
      if (this.wbInternalCodeInput) this.wbInternalCodeInput.value = '';
      if (this.wbExternalCodeInput) this.wbExternalCodeInput.value = '';
    }

    setDefaultTimestamps() {
      const now = new Date();
      const pad = (n) => String(n).padStart(2, '0');
      const nowLocal = `${now.getFullYear()}-${pad(now.getMonth() + 1)}-${pad(now.getDate())}T${pad(now.getHours())}:${pad(now.getMinutes())}`;

      if (this.txDateInput && !this.txDateInput.value) {
        this.txDateInput.value = nowLocal;
      }

      const formatted = this.formatDateTimeDisplay(this.txDateInput?.value || nowLocal);
      if (this.dailyShipDateDisplay) this.dailyShipDateDisplay.value = formatted;
      if (this.shipDateDisplay) this.shipDateDisplay.value = formatted;

      // Default weighbridge times
      if (this.wbIntEmptyTime && !this.wbIntEmptyTime.value) {
        this.wbIntEmptyTime.value = nowLocal;
      }
      if (this.wbIntLoadedTime && !this.wbIntLoadedTime.value) {
        this.wbIntLoadedTime.value = nowLocal;
      }
      if (this.wbExtLoadedTime && !this.wbExtLoadedTime.value) {
        this.wbExtLoadedTime.value = nowLocal;
      }

      // Derive initial Departure & Arrival times from Loaded Timestamps
      const depStr = this.formatDateTimeDisplay(this.wbIntLoadedTime?.value || nowLocal);
      if (this.departureDisplay) this.departureDisplay.value = depStr;
      if (this.dailyDepartureDisplay) this.dailyDepartureDisplay.value = depStr;

      const arrStr = this.formatDateTimeDisplay(this.wbExtLoadedTime?.value || nowLocal);
      if (this.arrivalDisplay) this.arrivalDisplay.value = arrStr;
      if (this.dailyArrivalDisplay) this.dailyArrivalDisplay.value = arrStr;
    }

    formatDateTimeDisplay(dtString) {
      if (!dtString) return '—';
      try {
        const d = new Date(dtString);
        return d.toLocaleString('ar-EG', {
          year: 'numeric',
          month: 'short',
          day: 'numeric',
          hour: '2-digit',
          minute: '2-digit'
        });
      } catch {
        return dtString;
      }
    }

    async handlePoLookup() {
      const poNum = this.poEntryInput?.value?.trim();
      if (!poNum) {
        this.showPoError('رقم أمر الشراء مطلوب', 'يرجى إدخال رقم أمر الشراء للمتابعة.');
        return;
      }

      this.setPoLookupLoading(true);
      if (this.poErrorAlert) this.poErrorAlert.style.display = 'none';

      try {
        const res = await window.ChatApi?.getTransportationPurchaseOrder(poNum);
        if (res && res.success && res.data) {
          this.showFormStep(res.data);
        } else {
          const msg = res?.message || 'Purchase Order not found.';
          if (msg.includes('no remaining quantity') || msg.includes('الكمية المتبقية')) {
            this.showPoError('This Purchase Order has no remaining quantity.', 'هذا الأمر مستنفد بالكامل ولا توجد به كمية متبقية قابلة للتوريد.');
          } else {
            this.showPoError('Purchase Order not found.', 'أمر الشراء غير مسجل في قاعدة البيانات. يرجى التحقق من الرقم والمحاولة مرة أخرى.');
          }
        }
      } catch (err) {
        console.error('Error looking up PO:', err);
        this.showPoError('Purchase Order not found.', 'أمر الشراء غير مسجل في قاعدة البيانات.');
      } finally {
        this.setPoLookupLoading(false);
      }
    }

    showPoError(title, desc) {
      if (this.poErrorTitle) this.poErrorTitle.textContent = title;
      if (this.poErrorDesc) this.poErrorDesc.textContent = desc;
      if (this.poErrorAlert) this.poErrorAlert.style.display = 'flex';
      this.poEntryInput?.focus();
    }

    setPoLookupLoading(isLoading) {
      if (this.poContinueBtn) this.poContinueBtn.disabled = isLoading;
      if (this.poSpinner) this.poSpinner.style.display = isLoading ? 'inline-block' : 'none';
      if (this.poContinueText) this.poContinueText.textContent = isLoading ? 'جاري التحقق...' : 'Continue';
    }

    /**
     * Weight Calculations (Driven strictly and exclusively from Section 1: الميزان)
     * Internal Net Weight = Internal Loaded - Internal Empty
     * External Net Weight = External Loaded - External Empty
     * Net Weight (الوزن الصافي) = MIN(Internal Net, External Net)
     * Waste (كمية هالك) = ABS(Internal Net - External Net)
     * Quantity in Bill of Lading (الكمية) = Net Weight
     */
    recalculateAllWeights() {
      const intLoaded = parseFloat(this.wbIntLoaded?.value);
      const intEmpty = parseFloat(this.wbIntEmpty?.value);
      const extLoaded = parseFloat(this.wbExtLoaded?.value);
      const extEmpty = parseFloat(this.wbExtEmpty?.value);

      const hasIntLoaded = !isNaN(intLoaded) && intLoaded > 0;
      const hasIntEmpty = !isNaN(intEmpty) && intEmpty > 0;
      const hasExtLoaded = !isNaN(extLoaded) && extLoaded > 0;
      const hasExtEmpty = !isNaN(extEmpty) && extEmpty > 0;

      // 1. Internal Net Weight
      let intNet = 0;
      if (hasIntLoaded && hasIntEmpty) {
        intNet = intLoaded - intEmpty;
        const valStr = intNet.toFixed(3);
        if (this.wbIntNet) this.wbIntNet.value = valStr;
        if (this.internalNetDisplay) this.internalNetDisplay.value = valStr;
      } else {
        if (this.wbIntNet) this.wbIntNet.value = '0.000';
        if (this.internalNetDisplay) this.internalNetDisplay.value = '0.000';
      }

      // 2. External Net Weight
      let extNet = 0;
      if (hasExtLoaded && hasExtEmpty) {
        extNet = extLoaded - extEmpty;
        const valStr = extNet.toFixed(3);
        if (this.wbExtNet) this.wbExtNet.value = valStr;
        if (this.externalNetDisplay) this.externalNetDisplay.value = valStr;
      } else {
        if (this.wbExtNet) this.wbExtNet.value = '0.000';
        if (this.externalNetDisplay) this.externalNetDisplay.value = '0.000';
      }

      // 3. Net Weight and Waste Quantity
      if (hasIntLoaded && hasIntEmpty && hasExtLoaded && hasExtEmpty) {
        let waste = 0;
        let netWeight = 0;

        if (intNet === extNet) {
          waste = 0;
          netWeight = intNet;
        } else {
          waste = Math.abs(intNet - extNet);
          netWeight = Math.min(intNet, extNet);
        }

        if (this.wasteDisplay) this.wasteDisplay.value = waste.toFixed(3);
        if (this.netWeightDisplay) this.netWeightDisplay.value = netWeight.toFixed(3);
        if (this.quantityDisplay) this.quantityDisplay.value = netWeight.toFixed(3);
      } else {
        if (this.wasteDisplay) this.wasteDisplay.value = '0.000';
        if (this.netWeightDisplay) this.netWeightDisplay.value = '0.000';
        if (this.quantityDisplay) this.quantityDisplay.value = '0.000';
      }
    }

    /**
     * Shipping Time Calculation:
     * Shipping Time = External Loaded Weighing Date/Time − Internal Loaded Weighing Date/Time
     * Format: HH:MM
     */
    recalculateShippingTime() {
      const startTimeVal = this.wbIntLoadedTime?.value;
      const endTimeVal = this.wbExtLoadedTime?.value;

      if (!startTimeVal || !endTimeVal) {
        if (this.shippingTimeDisplay) this.shippingTimeDisplay.textContent = '—';
        if (this.bolShippingTimeDisplay) this.bolShippingTimeDisplay.value = '—';
        if (this.shippingTimeError) this.shippingTimeError.style.display = 'none';
        return;
      }

      const start = new Date(startTimeVal).getTime();
      const end = new Date(endTimeVal).getTime();

      if (end < start) {
        if (this.shippingTimeDisplay) this.shippingTimeDisplay.textContent = 'خطأ في التوقيت';
        if (this.bolShippingTimeDisplay) this.bolShippingTimeDisplay.value = 'خطأ في التوقيت';
        if (this.shippingTimeError) {
          this.shippingTimeError.textContent = 'وقت الوزن الخارجي القائم لا يمكن أن يكون قبل وقت الوزن الداخلي القائم.';
          this.shippingTimeError.style.display = 'block';
        }
      } else {
        if (this.shippingTimeError) this.shippingTimeError.style.display = 'none';
        const diffMs = end - start;
        const totalMinutes = Math.floor(diffMs / (1000 * 60));
        const hours = Math.floor(totalMinutes / 60);
        const mins = totalMinutes % 60;

        const pad = (n) => String(n).padStart(2, '0');
        const formattedTime = `${pad(hours)}:${pad(mins)}`;

        if (this.shippingTimeDisplay) this.shippingTimeDisplay.textContent = formattedTime;
        if (this.bolShippingTimeDisplay) this.bolShippingTimeDisplay.value = formattedTime;
      }
    }

    validateForm() {
      let isValid = true;
      this.clearAllFieldErrors();

      // SECTION 1 VALIDATIONS:
      // Manual Reference Codes (Requirements 1, 1A, 1B, 15)
      if (!this.wbInternalCodeInput?.value?.trim()) {
        this.setFieldError(this.wbInternalCodeInput, 'رقم مرجع الميزان الداخلي مطلوب ويجب إدخاله يدوياً.');
        isValid = false;
      }
      if (!this.wbExternalCodeInput?.value?.trim()) {
        this.setFieldError(this.wbExternalCodeInput, 'رقم مرجع الميزان الخارجي مطلوب ويجب إدخاله يدوياً.');
        isValid = false;
      }

      // Internal Weights
      const intLoaded = parseFloat(this.wbIntLoaded?.value);
      const intEmpty = parseFloat(this.wbIntEmpty?.value);
      if (isNaN(intLoaded) || intLoaded <= 0) {
        this.setFieldError(this.wbIntLoaded, 'الوزن الداخلي قائم مطلوب ويجب أن يكون أكبر من الصفر.');
        isValid = false;
      }
      if (isNaN(intEmpty) || intEmpty <= 0) {
        this.setFieldError(this.wbIntEmpty, 'الوزن الداخلي فارغ مطلوب ويجب أن يكون أكبر من الصفر.');
        isValid = false;
      }
      if (!isNaN(intLoaded) && !isNaN(intEmpty) && intLoaded <= intEmpty) {
        this.setFieldError(this.wbIntLoaded, 'الوزن الداخلي قائم يجب أن يكون أكبر من الوزن الداخلي فارغ.');
        isValid = false;
      }

      // External Weights
      const extLoaded = parseFloat(this.wbExtLoaded?.value);
      const extEmpty = parseFloat(this.wbExtEmpty?.value);
      if (isNaN(extLoaded) || extLoaded <= 0) {
        this.setFieldError(this.wbExtLoaded, 'الوزن الخارجي قائم مطلوب ويجب أن يكون أكبر من الصفر.');
        isValid = false;
      }
      if (isNaN(extEmpty) || extEmpty <= 0) {
        this.setFieldError(this.wbExtEmpty, 'الوزن الخارجي فارغ مطلوب ويجب أن يكون أكبر من الصفر.');
        isValid = false;
      }
      if (!isNaN(extLoaded) && !isNaN(extEmpty) && extLoaded <= extEmpty) {
        this.setFieldError(this.wbExtLoaded, 'الوزن الخارجي قائم يجب أن يكون أكبر من الوزن الخارجي فارغ.');
        isValid = false;
      }

      // Shipment Date
      if (!this.txDateInput?.value) {
        this.setFieldError(this.txDateInput, 'تاريخ الشحنة مطلوب.');
        isValid = false;
      }

      // Loaded Timestamps (Single Source of Truth for Departure & Arrival)
      if (!this.wbIntLoadedTime?.value) {
        this.setFieldError(this.wbIntLoadedTime, 'تاريخ ووقت الوزن القائم الداخلي مطلوب لتحديد تاريخ المغادرة.');
        isValid = false;
      }
      if (!this.wbExtLoadedTime?.value) {
        this.setFieldError(this.wbExtLoadedTime, 'تاريخ ووقت الوزن القائم الخارجي مطلوب لتحديد تاريخ الوصول.');
        isValid = false;
      }

      // Shipping Time check: External Loaded must be >= Internal Loaded
      const startTimeVal = this.wbIntLoadedTime?.value;
      const endTimeVal = this.wbExtLoadedTime?.value;
      if (startTimeVal && endTimeVal) {
        const start = new Date(startTimeVal).getTime();
        const end = new Date(endTimeVal).getTime();
        if (end < start) {
          if (this.shippingTimeError) {
            this.shippingTimeError.textContent = 'وقت الوزن الخارجي القائم لا يمكن أن يكون قبل وقت الوزن الداخلي القائم.';
            this.shippingTimeError.style.display = 'block';
          }
          isValid = false;
        }
      }

      // Section 1 Mandatory Attachment Check
      if (!this.wbAttachmentFile) {
        this.setAttachmentError(this.wbAttachmentError, 'Attachment is required for this section. (مرفق قسم الميزان مطلوب)');
        isValid = false;
      }

      // SECTION 2 VALIDATIONS:
      // Section 2 Mandatory Attachment Check
      if (!this.dsAttachmentFile) {
        this.setAttachmentError(this.dsAttachmentError, 'Attachment is required for this section. (مرفق قسم يومية التوريد مطلوب)');
        isValid = false;
      }

      // SECTION 3 VALIDATIONS:
      // Material Type
      const mat = this.materialSelect?.value;
      if (!mat || (mat !== 'powder' && mat !== 'slurry')) {
        this.setFieldError(this.materialSelect, 'يرجى اختيار نوع الخامة (powder أو slurry فقط).');
        isValid = false;
      }

      // Car Number
      if (!this.carInput?.value?.trim()) {
        this.setFieldError(this.carInput, 'رقم السيارة مطلوب.');
        isValid = false;
      }

      // Driver
      if (!this.driverSelect?.value?.trim()) {
        this.setFieldError(this.driverSelect, 'يرجى اختيار السائق من القائمة المعتمدة.');
        isValid = false;
      }

      // Delivery Status Checklist (Requirement 37)
      const isSafe = this.statusSafeCheckbox?.checked;
      const isIntact = this.statusIntactCheckbox?.checked;
      if (!isSafe && !isIntact) {
        if (this.recStatusError) {
          this.recStatusError.textContent = 'حالة الاستلام مطلوبة. يجب تحديد (safe أو intact أو كلاهما).';
          this.recStatusError.style.display = 'block';
        }
        isValid = false;
      }

      // Section 3 Mandatory Attachment Check
      if (!this.bolAttachmentFile) {
        this.setAttachmentError(this.bolAttachmentError, 'Attachment is required for this section. (مرفق قسم بوليصة الشحن مطلوب)');
        isValid = false;
      }

      // SECTION 4 VALIDATIONS:
      // Custody (العهدة - Required decimal >= 0, Requirements 6, 7, 15)
      const custodyVal = parseFloat(this.custodyInput?.value);
      if (isNaN(custodyVal) || custodyVal < 0) {
        this.setFieldError(this.custodyInput, 'العهدة مطلوبة ويجب إدخال قيمة رقمية صحيحة (صفر أو أكثر).');
        isValid = false;
      }

      // PO Quantity Check: Net Weight <= PO Remaining Quantity (Requirement 9)
      if (this.currentPo) {
        const netWeight = parseFloat(this.netWeightDisplay?.value) || 0;
        const remaining = parseFloat(this.currentPo.remainingQuantity) || 0;
        if (netWeight > remaining) {
          this.showAlert(`Net weight exceeds the remaining quantity of this PO. (الوزن الصافي ${netWeight} طن يتجاوز الكمية المتبقية ${remaining} طن).`, 'error');
          isValid = false;
        }
      }

      return isValid;
    }

    setAttachmentError(el, msg) {
      if (el) {
        el.textContent = msg;
        el.style.display = 'block';
      }
    }

    async handleFormSubmit() {
      if (this.isSubmitting) return;

      if (!this.validateForm()) {
        this.showAlert('يرجى تصحيح الأخطاء الموضحة وتوفير المرفقات الإلزامية لكل قسم قبل الحفظ.', 'error');
        return;
      }

      this.setSubmitLoading(true);
      this.hideAlert();

      try {
        const formData = new FormData();

        const txNum = parseInt(this.txNumberInput?.value, 10) || 0;
        formData.append('transactionNumber', txNum);
        formData.append('transactionDate', this.txDateInput.value);
        formData.append('carNumber', this.carInput.value.trim());
        formData.append('driverName', this.driverSelect.value.trim());
        formData.append('source', 'Face stone (Full Concrete Solutions)');
        formData.append('poNumber', this.currentPo?.poNumber || '');
        formData.append('destination', this.destinationInput?.value || this.currentPo?.destination || 'Cairo Factory');
        formData.append('materialType', this.materialSelect.value.trim());

        // Weights (Entered ONLY in Section 1)
        formData.append('weightBeforeLoading', this.wbIntEmpty.value);
        formData.append('weightWhenExitingFactory', this.wbIntLoaded.value);
        formData.append('externalLoadedWeight', this.wbExtLoaded.value);
        formData.append('externalEmptyWeight', this.wbExtEmpty.value);

        // Movement Dates
        if (this.departureInput?.value) {
          formData.append('departureTime', this.departureInput.value);
        }
        if (this.arrivalInput?.value) {
          formData.append('arrivalTime', this.arrivalInput.value);
        }

        // Driver Phone
        if (this.driverPhoneInput?.value?.trim()) {
          formData.append('driverPhone', this.driverPhoneInput.value.trim());
        }

        // Supervisors & Recipient
        formData.append('shippingSupervisor', 'محمد ناجح');
        formData.append('departmentSupervisor', 'ازهر محمد');

        // Checklist Delivery Status (Requirement 37)
        const checklist = [];
        if (this.statusSafeCheckbox?.checked) checklist.push('safe');
        if (this.statusIntactCheckbox?.checked) checklist.push('intact');
        formData.append('receivingStatus', checklist.join(', '));

        if (this.recNameInput?.value?.trim()) {
          formData.append('recipientName', this.recNameInput.value.trim());
        }
        if (this.recTitleSelect?.value?.trim()) {
          formData.append('recipientTitle', this.recTitleSelect.value.trim());
        }
        if (this.recDateInput?.value) {
          formData.append('receivedDate', this.recDateInput.value);
        }

        // Weighbridge codes (Entered manually by employee - Requirements 1A, 1B)
        formData.append('internalWeighbridgeCode', this.wbInternalCodeInput.value.trim());
        formData.append('externalWeighbridgeCode', this.wbExternalCodeInput.value.trim());

        if (this.wbIntEmptyTime?.value) formData.append('internalEmptyWeightTime', this.wbIntEmptyTime.value);
        if (this.wbIntLoadedTime?.value) formData.append('internalLoadedWeightTime', this.wbIntLoadedTime.value);
        if (this.wbExtEmptyTime?.value) formData.append('externalEmptyWeightTime', this.wbExtEmptyTime.value);
        if (this.wbExtLoadedTime?.value) formData.append('externalLoadedWeightTime', this.wbExtLoadedTime.value);

        // Section 4: Custody (العهدة), Ikramiya & Notes
        const custodyAmount = parseFloat(this.custodyInput?.value) || 0;
        formData.append('custodyAmount', custodyAmount);

        if (this.ikramiyaInput?.value) {
          formData.append('ikramiya', parseFloat(this.ikramiyaInput.value) || 0);
        }
        if (this.notesInput?.value?.trim()) {
          formData.append('notes', this.notesInput.value.trim());
        }

        // 3 Separate Mandatory Section Attachments
        if (this.wbAttachmentFile) {
          formData.append('weighbridgeAttachment', this.wbAttachmentFile);
        }
        if (this.dsAttachmentFile) {
          formData.append('dailySupplyAttachment', this.dsAttachmentFile);
        }
        if (this.bolAttachmentFile) {
          formData.append('billOfLadingAttachment', this.bolAttachmentFile);
          formData.append('invoiceFile', this.bolAttachmentFile); // Backwards compatibility
        }

        const res = await window.ChatApi?.submitTransportationTransaction(formData);
        if (res && res.success && res.data) {
          this.showSuccessStep(res.data);
          this.loadRecentTransactions();
          this.loadConfig();
        } else {
          const msg = res?.message || 'حدث خطأ أثناء حفظ حركة النقل.';
          this.showAlert(msg, 'error');
        }
      } catch (err) {
        console.error('Error submitting transportation transaction:', err);
        this.showAlert('حدث خطأ في الاتصال بالخادم أثناء حفظ الحركة.', 'error');
      } finally {
        this.setSubmitLoading(false);
      }
    }

    showSuccessStep(txData) {
      if (this.stepPoEntry) this.stepPoEntry.style.display = 'none';
      if (this.stepForm) this.stepForm.style.display = 'none';
      if (this.stepSuccess) this.stepSuccess.style.display = 'block';

      if (this.successTxNumber) this.successTxNumber.textContent = `#${txData.transactionNumber}`;
      if (this.successPoNumber) this.successPoNumber.textContent = txData.poNumber;
      if (this.successCompany) this.successCompany.textContent = txData.companyName || this.currentPo?.supplier || 'FaceStone Partner';
      if (this.successDestination) this.successDestination.textContent = txData.destination;
      if (this.successMaterial) this.successMaterial.textContent = txData.materialType;
      if (this.successIntWeight) this.successIntWeight.textContent = `${Number(txData.internalWeight || 0).toFixed(3)} طن`;
      if (this.successExtWeight) this.successExtWeight.textContent = `${Number(txData.externalNetWeight || 0).toFixed(3)} طن`;
      if (this.successNetWeight) this.successNetWeight.textContent = `${Number(txData.netWeight || txData.quantity || 0).toFixed(3)} طن`;
      if (this.successWaste) this.successWaste.textContent = `${Number(txData.wasteQuantity || 0).toFixed(3)} طن`;

      if (this.successShippingTime) {
        const mins = txData.shippingDurationMinutes;
        if (mins !== null && mins !== undefined) {
          const h = Math.floor(mins / 60);
          const m = mins % 60;
          this.successShippingTime.textContent = h > 0 ? `${h} ساعة و ${m} دقيقة` : `${m} دقيقة`;
        } else {
          this.successShippingTime.textContent = '—';
        }
      }

      this.stepSuccess.scrollIntoView({ behavior: 'smooth' });
    }

    async loadRecentTransactions() {
      if (!this.recentTableBody) return;
      this.recentTableBody.innerHTML = '<tr><td colspan="16" class="trans-empty-row">جاري تحميل سجل الحركات...</td></tr>';

      try {
        const res = await window.ChatApi?.getRecentTransportationTransactions(20);
        if (res && res.success && res.data) {
          this.renderRecentTable(res.data);
        } else {
          this.recentTableBody.innerHTML = '<tr><td colspan="16" class="trans-empty-row">لا توجد حركات مسجلة حالياً.</td></tr>';
        }
      } catch (err) {
        console.error('Failed to load recent transportation records:', err);
        this.recentTableBody.innerHTML = '<tr><td colspan="16" class="trans-empty-row error">تعذر تحميل السجل. يرجى إعادة المحاولة.</td></tr>';
      }
    }

    renderRecentTable(records) {
      if (!this.recentTableBody) return;
      if (!records || records.length === 0) {
        this.recentTableBody.innerHTML = '<tr><td colspan="16" class="trans-empty-row">لا توجد حركات مسجلة حالياً.</td></tr>';
        return;
      }

      const rowsHtml = records.map(tx => {
        const intWeight = Number(tx.internalWeight || 0).toFixed(3);
        const extWeight = Number(tx.externalNetWeight || 0).toFixed(3);
        const netWeight = Number(tx.netWeight || tx.quantity || 0).toFixed(3);
        const waste = Number(tx.wasteQuantity || 0).toFixed(3);

        let shipTimeStr = '—';
        if (tx.shippingDurationMinutes !== null && tx.shippingDurationMinutes !== undefined) {
          const h = Math.floor(tx.shippingDurationMinutes / 60);
          const m = tx.shippingDurationMinutes % 60;
          shipTimeStr = h > 0 ? `${h}س ${m}د` : `${m}د`;
        }

        const dateStr = this.formatDateTimeDisplay(tx.transactionDate);
        const submitDateStr = this.formatDateTimeDisplay(tx.dateOfSubmit);

        return `
          <tr>
            <td class="font-numeric" style="font-weight: 800; color: #2563EB;">#${tx.transactionNumber}</td>
            <td class="font-numeric">${dateStr}</td>
            <td class="font-numeric" dir="ltr" style="font-weight: 700;">${this.escapeHtml(tx.poNumber)}</td>
            <td>${this.escapeHtml(tx.companyName || '—')}</td>
            <td>${this.escapeHtml(tx.destination)}</td>
            <td class="font-numeric">${this.escapeHtml(tx.carNumber)}</td>
            <td>${this.escapeHtml(tx.driverName)}</td>
            <td><span class="trans-tag-badge">${this.escapeHtml(tx.materialType)}</span></td>
            <td class="font-numeric">${intWeight}</td>
            <td class="font-numeric">${extWeight}</td>
            <td class="font-numeric" style="font-weight: 800; color: #16A34A;">${netWeight}</td>
            <td class="font-numeric" style="color: ${Number(waste) > 0 ? '#DC2626' : '#6B7280'};">${waste}</td>
            <td class="font-numeric">${shipTimeStr}</td>
            <td>${this.escapeHtml(tx.shippingSupervisor || 'محمد ناجح')}</td>
            <td><span class="trans-status-badge safe">${this.escapeHtml(tx.receivingStatus || 'safe')}</span></td>
            <td class="font-numeric" style="color: #6B7280; font-size: 0.8rem;">${submitDateStr}</td>
          </tr>
        `;
      }).join('');

      this.recentTableBody.innerHTML = rowsHtml;
    }

    resetFormKeepPo() {
      // Clear file attachments
      this.wbAttachmentFile = null;
      this.dsAttachmentFile = null;
      this.bolAttachmentFile = null;

      [
        { input: this.wbAttachmentInput, placeholder: this.wbPlaceholder, preview: this.wbFilePreview, err: this.wbAttachmentError },
        { input: this.dsAttachmentInput, placeholder: this.dsPlaceholder, preview: this.dsFilePreview, err: this.dsAttachmentError },
        { input: this.bolAttachmentInput, placeholder: this.bolPlaceholder, preview: this.bolFilePreview, err: this.bolAttachmentError }
      ].forEach(item => {
        if (item.input) item.input.value = '';
        if (item.placeholder) item.placeholder.style.display = 'flex';
        if (item.preview) item.preview.style.display = 'none';
        if (item.err) {
          item.err.textContent = '';
          item.err.style.display = 'none';
        }
      });

      // Clear weights
      if (this.wbIntLoaded) this.wbIntLoaded.value = '';
      if (this.wbIntEmpty) this.wbIntEmpty.value = '';
      if (this.wbExtLoaded) this.wbExtLoaded.value = '';
      if (this.wbExtEmpty) this.wbExtEmpty.value = '';

      // Clear operational inputs
      if (this.wbInternalCodeInput) this.wbInternalCodeInput.value = '';
      if (this.wbExternalCodeInput) this.wbExternalCodeInput.value = '';
      if (this.driverPhoneInput) this.driverPhoneInput.value = '';
      if (this.carInput) this.carInput.value = '';
      if (this.driverSelect) this.driverSelect.selectedIndex = 0;
      if (this.materialSelect) this.materialSelect.selectedIndex = 0;
      if (this.recNameInput) this.recNameInput.value = '';
      if (this.recTitleSelect) this.recTitleSelect.selectedIndex = 0;
      if (this.recDateInput) this.recDateInput.value = '';
      if (this.custodyInput) this.custodyInput.value = '';
      if (this.ikramiyaInput) this.ikramiyaInput.value = '0.00';
      if (this.notesInput) this.notesInput.value = '';

      if (this.statusSafeCheckbox) this.statusSafeCheckbox.checked = true;
      if (this.statusIntactCheckbox) this.statusIntactCheckbox.checked = true;

      this.recalculateAllWeights();
      this.recalculateShippingTime();
      this.clearAllFieldErrors();
      this.hideAlert();
    }

    setFieldError(inputEl, msg) {
      if (!inputEl) return;
      inputEl.classList.add('error');
      const group = inputEl.closest('.trans-form-group');
      if (group) {
        let errEl = group.querySelector('.trans-field-error');
        if (errEl) {
          errEl.textContent = msg;
          errEl.style.display = 'block';
        }
      }
    }

    clearAllFieldErrors() {
      document.querySelectorAll('.trans-input.error').forEach(el => el.classList.remove('error'));
      document.querySelectorAll('.trans-field-error').forEach(el => {
        el.textContent = '';
        el.style.display = 'none';
      });
      if (this.shippingTimeError) {
        this.shippingTimeError.style.display = 'none';
      }
      if (this.recStatusError) {
        this.recStatusError.style.display = 'none';
      }
      if (this.wbAttachmentError) this.wbAttachmentError.style.display = 'none';
      if (this.dsAttachmentError) this.dsAttachmentError.style.display = 'none';
      if (this.bolAttachmentError) this.bolAttachmentError.style.display = 'none';
    }

    showAlert(msg, type = 'success') {
      if (!this.alertBanner) return;
      this.alertBanner.className = `trans-alert-banner ${type}`;
      if (this.alertIcon) this.alertIcon.textContent = type === 'error' ? '⚠️' : '✓';
      if (this.alertTitle) this.alertTitle.textContent = type === 'error' ? 'تنبيه' : 'نجاح';
      if (this.alertDesc) this.alertDesc.textContent = msg;
      this.alertBanner.style.display = 'flex';
      this.alertBanner.scrollIntoView({ behavior: 'smooth' });
    }

    hideAlert() {
      if (this.alertBanner) this.alertBanner.style.display = 'none';
    }

    setSubmitLoading(isLoading) {
      this.isSubmitting = isLoading;
      if (this.submitBtn) this.submitBtn.disabled = isLoading;
      if (this.submitSpinner) this.submitSpinner.style.display = isLoading ? 'inline-block' : 'none';
      if (this.submitIcon) this.submitIcon.style.display = isLoading ? 'none' : 'inline-block';
      if (this.submitBtnText) this.submitBtnText.textContent = isLoading ? 'جاري الحفظ...' : 'حفظ';
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

    updateUserBadge(user) {
      if (!user) return;
      if (this.userNameEl) {
        this.userNameEl.textContent = user.fullName || user.username || 'مستخدم مصرح له';
      }
      if (this.userRoleEl) {
        let roleArabic = user.role || 'إدارة النقل';
        if (user.role === 'CEO') roleArabic = 'الرئيس التنفيذي';
        else if (user.role === 'IT') roleArabic = 'الدعم الفني / IT';
        else if (user.role === 'Transportation Manager' || user.role === 'TRANSPORTATION_MANAGER') roleArabic = 'مدير إدارة النقل';
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

  // Hook into ChatApp view transitions
  document.addEventListener('chat:view:changed', (e) => {
    if (e.detail && e.detail.view === 'transportation') {
      const user = window.ChatApi?.getCurrentUser();
      window.TransportationApp.updateUserBadge(user);
      window.TransportationApp.showPoEntryStep();
      window.TransportationApp.loadRecentTransactions();
    }
  });

})();
