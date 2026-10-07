/* ==========================================================================
   FACESTONE CONTROL & MONITORING DEPARTMENT CLIENT CONTROLLER
   قسم التحكم والمراقبة
   Review, Validate, Accept, or Reject Transportation and Storage Forms
   Full Redesign: Summary Dashboard + Dedicated Request Details Page
   ========================================================================== */

(function () {
  'use strict';

  class ControlMonitoringApp {
    constructor() {
      this.isInitialized = false;
      this.currentFilterStatus = 'PENDING'; // Default view prioritizes PENDING
      this.currentFilterType = 'all';
      this.searchDebounceTimer = null;

      // Active request details state
      this.activeItem = null;
      this.activeFormType = null;
      this.activeFormId = null;

      this.filters = {
        status: 'PENDING',
        formType: 'all',
        po: '',
        employee: '',
        search: '',
        fromDate: '',
        toDate: ''
      };
    }

    init() {
      if (this.isInitialized) return;
      this.bindElements();
      this.bindEvents();
      this.isInitialized = true;
    }

    bindElements() {
      this.pageRoot = document.getElementById('control-monitoring-page-root');
      if (!this.pageRoot) return;

      // Views
      this.mainView = document.getElementById('control-monitoring-main-view');
      this.detailsView = document.getElementById('control-monitoring-request-details-view');

      // Top Header Elements
      this.userNameEl = document.getElementById('cm-user-name');
      this.userRoleEl = document.getElementById('cm-user-role');
      this.userAvatarEl = document.getElementById('cm-user-avatar');
      this.backToDeptsBtn = document.getElementById('control-monitoring-back-to-departments-btn');

      // Main Dashboard: Stat Card Values
      this.statPendingEl = document.getElementById('cm-stat-pending-val');
      this.statTransEl = document.getElementById('cm-stat-trans-val');
      this.statStorageEl = document.getElementById('cm-stat-storage-val');
      this.statApprovedEl = document.getElementById('cm-stat-approved-val');
      this.statRejectedEl = document.getElementById('cm-stat-rejected-val');

      // Main Dashboard: Filter Controls
      this.tabBtns = document.querySelectorAll('.cm-filter-tab-btn');
      this.typeSelect = document.getElementById('cm-filter-type-select');
      this.poInput = document.getElementById('cm-filter-po-input');
      this.employeeInput = document.getElementById('cm-filter-employee-input');
      this.searchInput = document.getElementById('cm-filter-search-input');
      this.fromDateInput = document.getElementById('cm-filter-from-date');
      this.toDateInput = document.getElementById('cm-filter-to-date');
      this.btnApply = document.getElementById('cm-btn-filter-apply');
      this.btnReset = document.getElementById('cm-btn-filter-reset');

      // Main Dashboard: Table & Status Containers
      this.tableBody = document.getElementById('cm-forms-table-body');
      this.tableLoading = document.getElementById('cm-table-loading');
      this.tableEmpty = document.getElementById('cm-table-empty');
      this.refreshBtn = document.getElementById('cm-btn-refresh-table');

      // Main Dashboard: Table Horizontal Slider & Carousel Elements
      this.tableWrapper = document.getElementById('cm-finance-table-wrapper') || document.querySelector('.finance-table-wrapper');
      this.btnSliderPrev = document.getElementById('cm-table-slider-prev');
      this.btnSliderNext = document.getElementById('cm-table-slider-next');
      this.btnSidePrev = document.getElementById('cm-slider-side-prev');
      this.btnSideNext = document.getElementById('cm-slider-side-next');

      // Details Page: Navigation & Breadcrumbs
      this.detailsBackBtn = document.getElementById('cm-details-back-btn');
      this.crumbDept = document.getElementById('cm-details-crumb-dept');
      this.crumbTx = document.getElementById('cm-details-crumb-tx');

      // Details Page: Hero Summary Card Elements
      this.heroBadge = document.getElementById('cm-details-hero-badge');
      this.heroTitle = document.getElementById('cm-details-hero-title');
      this.heroTypeText = document.getElementById('cm-details-hero-type-text');
      this.heroIdText = document.getElementById('cm-details-hero-id-text');
      this.heroPoText = document.getElementById('cm-details-hero-po-text');
      this.heroSubmitterText = document.getElementById('cm-details-hero-submitter-text');
      this.heroDeptText = document.getElementById('cm-details-hero-dept-text');
      this.heroDateText = document.getElementById('cm-details-hero-date-text');
      this.heroTimeText = document.getElementById('cm-details-hero-time-text');
      this.heroStatusBadge = document.getElementById('cm-details-hero-status-badge');
      this.detailsStatusBanner = document.getElementById('cm-details-status-banner');

      // Details Page: Form Data Body & Attachments List
      this.formDataBody = document.getElementById('cm-details-form-data-body');
      this.formSourceBadge = document.getElementById('cm-details-form-source-badge');
      this.attachmentCountPill = document.getElementById('cm-details-attachment-count-pill');
      this.attachmentsList = document.getElementById('cm-details-attachments-list');

      // Details Page: Review & Decision Elements
      this.decisionCard = document.getElementById('cm-details-decision-card');
      this.pendingActionsWrap = document.getElementById('cm-details-pending-actions-wrap');
      this.btnAccept = document.getElementById('cm-details-btn-accept');
      this.btnReject = document.getElementById('cm-details-btn-reject');
      this.acceptBox = document.getElementById('cm-details-accept-box');
      this.btnConfirmAccept = document.getElementById('cm-details-btn-confirm-accept');
      this.btnCancelAccept = document.getElementById('cm-details-btn-cancel-accept');
      this.rejectBox = document.getElementById('cm-details-reject-box');
      this.rejectReasonInput = document.getElementById('cm-details-reject-reason-input');
      this.btnConfirmReject = document.getElementById('cm-details-btn-confirm-reject');
      this.btnCancelReject = document.getElementById('cm-details-btn-cancel-reject');
      this.finalDecisionInfo = document.getElementById('cm-details-final-decision-info');

      // Details Page: Audit Timeline
      this.detailsTimeline = document.getElementById('cm-details-timeline');

      // Full-screen Attachment Preview Overlay
      this.previewOverlay = document.getElementById('cm-preview-overlay');
      this.previewClose = document.getElementById('cm-preview-close');
      this.previewTitle = document.getElementById('cm-preview-title');
      this.previewBody = document.getElementById('cm-preview-body');
      this.previewDownloadLink = document.getElementById('cm-preview-download-link');
      this.previewPopoutLink = document.getElementById('cm-preview-popout-link');
    }

    bindEvents() {
      // 1. Back to departments
      if (this.backToDeptsBtn) {
        this.backToDeptsBtn.addEventListener('click', () => {
          if (window.ChatApp) {
            window.ChatApp.backToDepartments();
          }
        });
      }

      // 2. Return from Request Details to Main Requests Dashboard
      if (this.detailsBackBtn) {
        this.detailsBackBtn.addEventListener('click', () => {
          this.showMainPage();
          if (window.location.pathname !== '/control-monitoring') {
            history.pushState({ view: 'control-monitoring' }, '', '/control-monitoring');
          }
        });
      }

      // 3. Quick Status Filter Tabs
      if (this.tabBtns) {
        this.tabBtns.forEach(btn => {
          btn.addEventListener('click', (e) => {
            this.tabBtns.forEach(b => b.classList.remove('active'));
            e.currentTarget.classList.add('active');
            const status = e.currentTarget.getAttribute('data-status') || 'PENDING';
            this.currentFilterStatus = status;
            this.filters.status = status;
            this.loadForms();
          });
        });
      }

      // 4. Department Type Filter
      if (this.typeSelect) {
        this.typeSelect.addEventListener('change', (e) => {
          this.filters.formType = e.target.value;
          this.loadForms();
        });
      }

      // 5. Debounced Search Inputs (PO, Employee, General Search)
      const handleSearchChange = () => {
        clearTimeout(this.searchDebounceTimer);
        this.searchDebounceTimer = setTimeout(() => {
          this.filters.search = this.searchInput ? this.searchInput.value.trim() : '';
          this.filters.po = this.poInput ? this.poInput.value.trim() : '';
          this.filters.employee = this.employeeInput ? this.employeeInput.value.trim() : '';
          this.loadForms();
        }, 350);
      };

      if (this.searchInput) this.searchInput.addEventListener('input', handleSearchChange);
      if (this.poInput) this.poInput.addEventListener('input', handleSearchChange);
      if (this.employeeInput) this.employeeInput.addEventListener('input', handleSearchChange);

      // 6. Date Range Filters
      if (this.fromDateInput) {
        this.fromDateInput.addEventListener('change', () => {
          this.filters.fromDate = this.fromDateInput.value;
          this.loadForms();
        });
      }
      if (this.toDateInput) {
        this.toDateInput.addEventListener('change', () => {
          this.filters.toDate = this.toDateInput.value;
          this.loadForms();
        });
      }

      // 7. Filter Apply, Reset & Refresh Table
      if (this.btnApply) {
        this.btnApply.addEventListener('click', () => this.loadForms());
      }
      if (this.btnReset) {
        this.btnReset.addEventListener('click', () => this.resetFilters());
      }
      if (this.refreshBtn) {
        this.refreshBtn.addEventListener('click', () => {
          this.loadStats();
          this.loadForms();
        });
      }

      // 8. Accept Form Decision: Toggle inline accept confirmation drawer
      if (this.btnAccept) {
        this.btnAccept.addEventListener('click', () => {
          if (this.rejectBox) this.rejectBox.style.display = 'none';
          if (this.acceptBox) {
            this.acceptBox.style.display = this.acceptBox.style.display === 'none' ? 'block' : 'none';
          }
        });
      }

      if (this.btnCancelAccept) {
        this.btnCancelAccept.addEventListener('click', () => {
          if (this.acceptBox) this.acceptBox.style.display = 'none';
        });
      }

      if (this.btnConfirmAccept) {
        this.btnConfirmAccept.addEventListener('click', () => this.handleConfirmAccept());
      }

      // 9. Reject Form Decision: Toggle inline rejection drawer
      if (this.btnReject) {
        this.btnReject.addEventListener('click', () => {
          if (this.acceptBox) this.acceptBox.style.display = 'none';
          if (this.rejectBox) {
            this.rejectBox.style.display = this.rejectBox.style.display === 'none' ? 'block' : 'none';
            if (this.rejectBox.style.display === 'block' && this.rejectReasonInput) {
              this.rejectReasonInput.value = '';
              this.rejectReasonInput.focus();
            }
          }
        });
      }

      if (this.btnCancelReject) {
        this.btnCancelReject.addEventListener('click', () => {
          if (this.rejectBox) this.rejectBox.style.display = 'none';
        });
      }

      if (this.btnConfirmReject) {
        this.btnConfirmReject.addEventListener('click', () => this.handleConfirmReject());
      }

      // 10. Attachment Preview Overlay Close
      if (this.previewClose) {
        this.previewClose.addEventListener('click', () => this.closePreviewOverlay());
      }
      if (this.previewOverlay) {
        this.previewOverlay.addEventListener('click', (e) => {
          if (e.target === this.previewOverlay) this.closePreviewOverlay();
        });
      }

      // 11. Keyboard Esc
      document.addEventListener('keydown', (e) => {
        if (e.key === 'Escape') {
          if (this.previewOverlay && this.previewOverlay.style.display !== 'none') {
            this.closePreviewOverlay();
          }
        }
      });

      // 12. Horizontal Slider & Carousel inside finance-table-wrapper
      this.setupTableSlider();
      window.addEventListener('facestone:realtime:control-monitoring', (e) => {
        if (!this.pageRoot || this.pageRoot.style.display === 'none') return;

        // If on main dashboard: refresh numbers and table
        if (this.mainView && this.mainView.style.display !== 'none') {
          this.loadStats();
          this.loadForms();
        }

        // If currently viewing details for the affected request: refresh details live
        if (this.detailsView && this.detailsView.style.display !== 'none') {
          const detail = e.detail;
          if (detail && detail.formId && String(detail.formId) === String(this.activeFormId)) {
            this.loadRequestDetails(this.activeFormType, this.activeFormId, false);
          }
        }
      });
    }

    updateUserBadge(user) {
      if (!user) return;
      if (this.userNameEl) this.userNameEl.textContent = user.fullName || user.username || 'مراقب العمليات';
      if (this.userRoleEl) this.userRoleEl.textContent = 'قسم التحكم والمراقبة (Control & Monitoring)';
      if (this.userAvatarEl) {
        const initials = (user.fullName || user.username || 'CM')
          .split(' ')
          .map(n => n[0])
          .join('')
          .substring(0, 2)
          .toUpperCase();
        this.userAvatarEl.textContent = initials || 'CM';
      }
    }

    /* ------------------------------------------------------------------------
       ROUTING & VIEW SWITCHING
       ------------------------------------------------------------------------ */
    showMainPage() {
      if (this.detailsView) this.detailsView.style.display = 'none';
      if (this.mainView) this.mainView.style.display = 'block';
      this.activeItem = null;
      this.activeFormType = null;
      this.activeFormId = null;
      window.scrollTo({ top: 0, behavior: 'smooth' });
    }

    handleRoute(subRoute) {
      const cleanRoute = (subRoute || window.location.pathname || '').trim().toLowerCase();
      // Match patterns:
      // /control-monitoring/request/{type}/{id}
      // /control-monitoring/request/{id}
      const matchTypeAndId = cleanRoute.match(/\/control-monitoring\/request\/(transportation|storage)\/(\d+)/i);
      if (matchTypeAndId) {
        const formType = matchTypeAndId[1];
        const formId = parseInt(matchTypeAndId[2], 10);
        this.showRequestDetails(formType, formId, false);
        return;
      }

      const matchIdOnly = cleanRoute.match(/\/control-monitoring\/request\/(\d+)/i);
      if (matchIdOnly) {
        const formId = parseInt(matchIdOnly[1], 10);
        this.showRequestDetails(null, formId, false);
        return;
      }

      // Default to main view
      this.showMainPage();
      this.loadStats();
      this.loadForms();
    }

    /* ------------------------------------------------------------------------
       FILTERS & STATS
       ------------------------------------------------------------------------ */
    resetFilters() {
      this.currentFilterStatus = 'PENDING';
      this.filters = {
        status: 'PENDING',
        formType: 'all',
        po: '',
        employee: '',
        search: '',
        fromDate: '',
        toDate: ''
      };

      if (this.tabBtns) {
        this.tabBtns.forEach(btn => {
          btn.classList.toggle('active', btn.getAttribute('data-status') === 'PENDING');
        });
      }
      if (this.typeSelect) this.typeSelect.value = 'all';
      if (this.poInput) this.poInput.value = '';
      if (this.employeeInput) this.employeeInput.value = '';
      if (this.searchInput) this.searchInput.value = '';
      if (this.fromDateInput) this.fromDateInput.value = '';
      if (this.toDateInput) this.toDateInput.value = '';

      this.loadForms();
    }

    async loadStats() {
      try {
        const res = await window.ChatApi.getControlMonitoringStats();
        if (res && res.success && res.data) {
          const d = res.data;
          if (this.statPendingEl) this.statPendingEl.textContent = d.pendingReviewsCount ?? 0;
          if (this.statTransEl) this.statTransEl.textContent = d.transportationPendingCount ?? 0;
          if (this.statStorageEl) this.statStorageEl.textContent = d.storagePendingCount ?? 0;
          if (this.statApprovedEl) this.statApprovedEl.textContent = d.approvedTodayCount ?? 0;
          if (this.statRejectedEl) this.statRejectedEl.textContent = d.rejectedTodayCount ?? 0;
        }
      } catch (err) {
        console.warn('Error loading Control & Monitoring stats:', err);
      }
    }

    /* ------------------------------------------------------------------------
       MAIN DASHBOARD: LOAD & RENDER REQUESTS SUMMARY TABLE
       Important: Only summary information is rendered here!
       ------------------------------------------------------------------------ */
    async loadForms() {
      if (!this.tableBody) return;

      if (this.tableLoading) this.tableLoading.style.display = 'block';
      if (this.tableEmpty) this.tableEmpty.style.display = 'none';
      this.tableBody.innerHTML = '';

      try {
        const res = await window.ChatApi.getControlMonitoringForms(this.filters);
        if (this.tableLoading) this.tableLoading.style.display = 'none';

        if (res && res.success && Array.isArray(res.data) && res.data.length > 0) {
          this.renderFormsTable(res.data);
        } else {
          if (this.tableEmpty) this.tableEmpty.style.display = 'block';
        }
      } catch (err) {
        if (this.tableLoading) this.tableLoading.style.display = 'none';
        if (this.tableEmpty) {
          this.tableEmpty.style.display = 'block';
          const p = this.tableEmpty.querySelector('p');
          if (p) p.textContent = 'تعذر تحميل الطلبات حالياً. يرجى إعادة المحاولة.';
        }
        console.error('Error loading Control & Monitoring requests:', err);
      }
    }

    renderFormsTable(items) {
      if (!this.tableBody) return;
      this.tableBody.innerHTML = '';

      items.forEach(item => {
        const tr = document.createElement('tr');
        tr.className = 'finance-table-row';

        // 1. Request Type & ID
        const isTrans = (item.formType || '').toLowerCase() === 'transportation';
        const requestId = item.formId || item.id || '-';
        const typeBadge = isTrans
          ? `<div style="display:flex; align-items:center; gap:6px;">
               <span class="cm-badge cm-badge-trans">🚛 نقل</span>
               <span style="font-weight:800; font-size:0.8rem; color:#2563EB; font-feature-settings:'tnum';">#${requestId}</span>
             </div>`
          : `<div style="display:flex; align-items:center; gap:6px;">
               <span class="cm-badge cm-badge-storage">📦 مستودع</span>
               <span style="font-weight:800; font-size:0.8rem; color:#7C3AED; font-feature-settings:'tnum';">#${requestId}</span>
             </div>`;

        // 2. PO Number
        const poText = item.poNumber && item.poNumber.trim() ? this.escapeHtml(item.poNumber) : '<span style="color:#94A3B8;">-</span>';

        // 3. Transaction Number
        const txText = item.transactionNumber || `#${requestId}`;

        // 4. Identifying Information (Material / Vehicle Plate / Supplier / Store)
        let identifyingInfo = '';
        if (isTrans) {
          const mat = item.materialType || '-';
          const plate = item.carNumber ? `لوحة: ${this.escapeHtml(item.carNumber)}` : '';
          const comp = item.companyName ? ` • ${this.escapeHtml(item.companyName)}` : '';
          identifyingInfo = `
            <div style="font-size:0.85rem; font-weight:700; color:#1E293B;">${this.escapeHtml(mat)}</div>
            <div style="font-size:0.75rem; color:#64748B;">${plate}${comp}</div>
          `;
        } else {
          const mat = item.material || '-';
          const act = item.action ? (item.action.toLowerCase() === 'import' ? '📥 توريد/إيداع' : '📤 صرف/تصدير') : '-';
          const loc = item.storageLocation ? ` • ${this.escapeHtml(item.storageLocation)}` : '';
          identifyingInfo = `
            <div style="font-size:0.85rem; font-weight:700; color:#1E293B;">${this.escapeHtml(mat)}</div>
            <div style="font-size:0.75rem; color:#64748B;">${act}${loc}</div>
          `;
        }

        // 5. Submitted By
        const submitterText = this.escapeHtml(item.submittedBy || '-');

        // 6. Submission Date & Time
        const dateStr = item.submissionDate
          ? new Date(item.submissionDate).toLocaleDateString('ar-EG', {
              year: 'numeric',
              month: 'short',
              day: 'numeric',
              hour: '2-digit',
              minute: '2-digit'
            })
          : '-';

        // 7. Status Badge
        const status = (item.status || 'PENDING').toUpperCase();
        let statusBadge = `<span class="cm-badge cm-badge-pending">⏳ قيد المراجعة</span>`;
        if (status === 'APPROVED') {
          statusBadge = `<span class="cm-badge cm-badge-approved">✓ معتمد</span>`;
        } else if (status === 'REJECTED') {
          statusBadge = `<span class="cm-badge cm-badge-rejected" title="${this.escapeHtml(item.rejectionReason || 'تم الرفض')}">✕ مرفوض</span>`;
        }

        // 8. Number of Attachments
        const attCount = item.attachmentCount || (item.attachments ? item.attachments.length : 0);
        const attBadge = attCount > 0
          ? `<span class="finance-badge-pill" style="font-size:0.775rem; padding:3px 8px; color:#0F172A; font-weight:700;">📎 ${attCount} مرفقات</span>`
          : `<span style="color:#94A3B8; font-size:0.75rem;">بدون مرفقات</span>`;

        // 9. Action: [ View Details → ]
        tr.innerHTML = `
          <td>${typeBadge}</td>
          <td style="font-weight:700; color:#0F172A;" dir="ltr">${poText}</td>
          <td style="font-weight:700; color:#0284C7; font-feature-settings:'tnum';">${txText}</td>
          <td>${identifyingInfo}</td>
          <td><div style="font-weight:600; color:#334155;">${submitterText}</div></td>
          <td style="font-size:0.8rem; color:#64748B;">${dateStr}</td>
          <td>${statusBadge}</td>
          <td>${attBadge}</td>
          <td>
            <button type="button" class="cm-btn-view-details cm-btn-open-request-details" data-type="${item.formType}" data-id="${requestId}">
              <span>عرض التفاصيل</span>
              <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.5"><line x1="19" y1="12" x2="5" y2="12"/><polyline points="12 19 5 12 12 5"/></svg>
            </button>
          </td>
        `;

        const btnDetails = tr.querySelector('.cm-btn-open-request-details');
        if (btnDetails) {
          btnDetails.addEventListener('click', () => {
            this.showRequestDetails(item.formType, requestId, true);
          });
        }

        this.tableBody.appendChild(tr);
      });
    }

    /* ------------------------------------------------------------------------
       DEDICATED REQUEST DETAILS PAGE
       Opens separate page view and loads ALL database fields & attachments
       ------------------------------------------------------------------------ */
    async showRequestDetails(formType, id, pushHistory = true) {
      if (!this.detailsView) return;

      this.activeFormType = formType;
      this.activeFormId = id;
      this.activeItem = null;

      // Switch view: Hide main dashboard, show dedicated details page
      if (this.mainView) this.mainView.style.display = 'none';
      this.detailsView.style.display = 'block';
      window.scrollTo({ top: 0, behavior: 'smooth' });

      // Update URL route
      if (pushHistory) {
        const urlType = formType ? formType.toLowerCase() : 'request';
        const newPath = formType 
          ? `/control-monitoring/request/${urlType}/${id}`
          : `/control-monitoring/request/${id}`;
        if (window.location.pathname !== newPath) {
          history.pushState({ view: 'control-monitoring-details', formType, id }, '', newPath);
        }
      }

      await this.loadRequestDetails(formType, id);
    }

    async loadRequestDetails(formType, id, showLoading = true) {
      if (showLoading && this.formDataBody) {
        this.formDataBody.innerHTML = `
          <div style="padding: 3rem; text-align: center;">
            <div class="finance-spinner"></div>
            <p style="margin-top: 12px; color: #64748B; font-weight: 600;">جاري تحميل بيانات الطلب الأصلية والمرفقات الثبوتية...</p>
          </div>
        `;
      }

      try {
        let res;
        if (formType) {
          res = await window.ChatApi.getControlMonitoringFormDetail(formType, id);
        } else {
          res = await window.ChatApi.getControlMonitoringFormDetail(id);
        }

        if (res && res.success && res.data) {
          this.activeItem = res.data;
          this.activeFormType = res.data.formType || formType;
          this.activeFormId = res.data.formId || res.data.id || id;
          this.renderRequestDetails(res.data);
        } else {
          if (this.formDataBody) {
            this.formDataBody.innerHTML = `
              <div style="padding: 2.5rem; text-align: center; color: #DC2626; font-weight: 700;">
                ${res?.message || 'تعذر تحميل بيانات الطلب المحدد.'}
              </div>
            `;
          }
        }
      } catch (err) {
        console.error('Error loading request details:', err);
        if (this.formDataBody) {
          this.formDataBody.innerHTML = `
            <div style="padding: 2.5rem; text-align: center; color: #DC2626; font-weight: 700;">
              حدث خطأ أثناء الاتصال بالخادم لتحميل تفاصيل الطلب.
            </div>
          `;
        }
      }
    }

    renderRequestDetails(item) {
      const isTrans = (item.formType || '').toLowerCase() === 'transportation';
      const typeAr = isTrans ? 'النقل واللوجستيات' : 'المستودعات والمخازن';
      const txNum = item.transactionNumber || `#${item.formId || item.id}`;
      const status = (item.status || 'PENDING').toUpperCase();

      // 1. Breadcrumbs
      if (this.crumbDept) this.crumbDept.textContent = `طلب ${typeAr}`;
      if (this.crumbTx) this.crumbTx.textContent = txNum;

      // 2. Hero Identity Card
      if (this.heroBadge) this.heroBadge.textContent = txNum;
      if (this.heroTitle) this.heroTitle.textContent = `طلب تدقيق واعتماد: ${typeAr} (${txNum})`;
      if (this.heroTypeText) this.heroTypeText.textContent = typeAr;
      if (this.heroIdText) this.heroIdText.textContent = item.formId || item.id || '-';
      if (this.heroPoText) this.heroPoText.textContent = item.poNumber && item.poNumber.trim() ? item.poNumber : 'غير مرتبط بأمر شراء';
      if (this.heroSubmitterText) this.heroSubmitterText.textContent = item.submittedBy || '-';
      if (this.heroDeptText) this.heroDeptText.textContent = typeAr;

      // Submission date and time formatted
      if (item.submissionDate) {
        const d = new Date(item.submissionDate);
        if (this.heroDateText) {
          this.heroDateText.textContent = d.toLocaleDateString('ar-EG', { year: 'numeric', month: 'long', day: 'numeric' });
        }
        if (this.heroTimeText) {
          this.heroTimeText.textContent = d.toLocaleTimeString('ar-EG', { hour: '2-digit', minute: '2-digit', second: '2-digit' });
        }
      } else {
        if (this.heroDateText) this.heroDateText.textContent = '-';
        if (this.heroTimeText) this.heroTimeText.textContent = '-';
      }

      // Hero Status Badge
      if (this.heroStatusBadge) {
        this.heroStatusBadge.className = 'cm-badge';
        if (status === 'APPROVED') {
          this.heroStatusBadge.classList.add('cm-badge-approved');
          this.heroStatusBadge.innerHTML = '✓ معتمد (APPROVED)';
        } else if (status === 'REJECTED') {
          this.heroStatusBadge.classList.add('cm-badge-rejected');
          this.heroStatusBadge.innerHTML = '✕ مرفوض (REJECTED)';
        } else {
          this.heroStatusBadge.classList.add('cm-badge-pending');
          this.heroStatusBadge.innerHTML = '⏳ قيد المراجعة (PENDING)';
        }
      }

      // 3. Status Notification Banner
      if (this.detailsStatusBanner) {
        if (status === 'APPROVED') {
          this.detailsStatusBanner.style.display = 'block';
          this.detailsStatusBanner.className = 'cm-decision-approved-box';
          this.detailsStatusBanner.innerHTML = `
            <div style="display:flex; align-items:center; gap:8px; font-weight:800; font-size:1rem; margin-bottom:4px;">
              <span>✓ تم قبول واعتماد هذا النموذج رسمياً</span>
            </div>
            <div>
              تم الاعتماد بواسطة: <strong>${this.escapeHtml(item.reviewedBy || 'مراجع العمليات')}</strong>
              بتاريخ <strong>${item.reviewedAt ? new Date(item.reviewedAt).toLocaleString('ar-EG') : '-'}</strong>.
              البيانات مرحلة للحسابات والتقارير التنفيذية ولا يمكن التعديل عليها.
            </div>
          `;
        } else if (status === 'REJECTED') {
          this.detailsStatusBanner.style.display = 'block';
          this.detailsStatusBanner.className = 'cm-decision-rejected-box';
          this.detailsStatusBanner.innerHTML = `
            <div style="display:flex; align-items:center; gap:8px; font-weight:800; font-size:1rem; margin-bottom:4px;">
              <span>✕ تم رفض هذا النموذج للتصحيح</span>
            </div>
            <div>
              تم الرفض بواسطة: <strong>${this.escapeHtml(item.reviewedBy || 'مراجع العمليات')}</strong>
              بتاريخ <strong>${item.reviewedAt ? new Date(item.reviewedAt).toLocaleString('ar-EG') : '-'}</strong>.
            </div>
            <div style="margin-top:6px; background:#FFFFFF; border:1px solid #FECACA; padding:8px 12px; border-radius:6px; color:#991B1B;">
              <strong>سبب الرفض:</strong> "${this.escapeHtml(item.rejectionReason || 'غير محدد')}"
            </div>
            <div style="margin-top:6px; font-size:0.8rem; color:#7F1D1D;">
              * بانتظار قيام الموظف المختص بإجراء التعديلات المطلوبة وإعادة تقديم النموذج.
            </div>
          `;
        } else {
          this.detailsStatusBanner.style.display = 'none';
        }
      }

      // 4. Render EVERY Original Field (Adapts automatically to Transportation or Storage)
      this.renderFullFormData(item, isTrans);

      // 5. Render Attachments Section
      this.renderAttachmentsSection(item);

      // 6. Render Review Decision & Actions
      this.renderDecisionSection(item, status);

      // 7. Render Audit Trail Timeline
      this.renderAuditTimeline(item.auditTrail || []);
    }

    /* ------------------------------------------------------------------------
       FORM DATA SECTION: EVERY FIELD SUBMITTED IN ORIGINAL FORM
       ------------------------------------------------------------------------ */
    renderFullFormData(item, isTrans) {
      if (!this.formDataBody) return;

      if (isTrans) {
        // Transportation Form Data
        this.formDataBody.innerHTML = `
          <div class="cm-form-groups-container">
            <!-- Group 1: Trip & Vehicle Info -->
            <div class="cm-form-group">
              <div class="cm-form-group-header">
                <span class="cm-form-group-icon">🚛</span>
                <span>بيانات الرحلة والشاحنة (Trip & Truck Details)</span>
              </div>
              <div class="cm-fields-grid">
                <div class="cm-field-item">
                  <span class="cm-field-label">رقم أمر الشراء (PO)</span>
                  <span class="cm-field-value highlight" dir="ltr">${this.escapeHtml(item.poNumber || '-')}</span>
                </div>
                <div class="cm-field-item">
                  <span class="cm-field-label">اسم الشركة / المورد</span>
                  <span class="cm-field-value">${this.escapeHtml(item.companyName || '-')}</span>
                </div>
                <div class="cm-field-item">
                  <span class="cm-field-label">رقم لوحة الشاحنة</span>
                  <span class="cm-field-value highlight">${this.escapeHtml(item.carNumber || '-')}</span>
                </div>
                <div class="cm-field-item">
                  <span class="cm-field-label">اسم السائق</span>
                  <span class="cm-field-value">${this.escapeHtml(item.driverName || '-')}</span>
                </div>
                <div class="cm-field-item">
                  <span class="cm-field-label">رقم هاتف السائق</span>
                  <span class="cm-field-value" dir="ltr">${this.escapeHtml(item.driverPhone || '-')}</span>
                </div>
                <div class="cm-field-item">
                  <span class="cm-field-label">نوع الخامة</span>
                  <span class="cm-field-value">${this.escapeHtml(item.materialType || '-')}</span>
                </div>
                <div class="cm-field-item">
                  <span class="cm-field-label">نقطة التحميل (المصدر)</span>
                  <span class="cm-field-value">${this.escapeHtml(item.source || '-')}</span>
                </div>
                <div class="cm-field-item">
                  <span class="cm-field-label">وجهة الشحن</span>
                  <span class="cm-field-value">${this.escapeHtml(item.destination || '-')}</span>
                </div>
              </div>
            </div>

            <!-- Group 2: Weighbridge & Official Weights -->
            <div class="cm-form-group">
              <div class="cm-form-group-header">
                <span class="cm-form-group-icon">⚖️</span>
                <span>بيانات الميزان والأوزان الرسمية (Official Weighbridge Weights)</span>
              </div>
              <div class="cm-fields-grid">
                <div class="cm-field-item">
                  <span class="cm-field-label">الوزن المتوقع (Expected Weight)</span>
                  <span class="cm-field-value">${this.formatWeight(item.expectedWeight)} طن</span>
                </div>
                <div class="cm-field-item">
                  <span class="cm-field-label">الوزن قبل التحميل (فارغ خارجي)</span>
                  <span class="cm-field-value">${this.formatWeight(item.weightBeforeLoading)} طن</span>
                </div>
                <div class="cm-field-item">
                  <span class="cm-field-label">وزن الدخول للمصنع (قائم داخلي)</span>
                  <span class="cm-field-value">${this.formatWeight(item.weightWhenEnteringFactory)} طن</span>
                </div>
                <div class="cm-field-item">
                  <span class="cm-field-label">وزن الخروج من المصنع (فارغ داخلي)</span>
                  <span class="cm-field-value">${this.formatWeight(item.weightWhenExitingFactory)} طن</span>
                </div>
                <div class="cm-field-item">
                  <span class="cm-field-label">الوزن الصافي الداخلي (Internal Net)</span>
                  <span class="cm-field-value highlight">${this.formatWeight(item.internalWeight)} طن</span>
                </div>
                <div class="cm-field-item">
                  <span class="cm-field-label">كود مرجع الميزان الداخلي</span>
                  <span class="cm-field-value" dir="ltr">${this.escapeHtml(item.internalWeighbridgeCode || '-')}</span>
                </div>
                <div class="cm-field-item">
                  <span class="cm-field-label">الوزن الخارجي قائم</span>
                  <span class="cm-field-value">${this.formatWeight(item.externalLoadedWeight)} طن</span>
                </div>
                <div class="cm-field-item">
                  <span class="cm-field-label">الوزن الخارجي فارغ</span>
                  <span class="cm-field-value">${this.formatWeight(item.externalEmptyWeight)} طن</span>
                </div>
                <div class="cm-field-item">
                  <span class="cm-field-label">الوزن الصافي الخارجي (External Net)</span>
                  <span class="cm-field-value highlight">${this.formatWeight(item.externalNetWeight)} طن</span>
                </div>
                <div class="cm-field-item">
                  <span class="cm-field-label">كود مرجع الميزان الخارجي</span>
                  <span class="cm-field-value" dir="ltr">${this.escapeHtml(item.externalWeighbridgeCode || '-')}</span>
                </div>
                <div class="cm-field-item">
                  <span class="cm-field-label">الوزن الصافي المعتمد (Net Weight)</span>
                  <span class="cm-field-value success" style="font-size:1.05rem;">${this.formatWeight(item.netWeight)} طن</span>
                </div>
                <div class="cm-field-item">
                  <span class="cm-field-label">كمية الهالك المحسوبة (Waste)</span>
                  <span class="cm-field-value danger">${this.formatWeight(item.wasteQuantity)} طن</span>
                </div>
              </div>
            </div>

            <!-- Group 3: Timestamps & Durations -->
            <div class="cm-form-group">
              <div class="cm-form-group-header">
                <span class="cm-form-group-icon">⏱️</span>
                <span>سجلات التوقيتات ومدة الشحن (Timestamps & Shipping Times)</span>
              </div>
              <div class="cm-fields-grid">
                <div class="cm-field-item">
                  <span class="cm-field-label">وقت الوزن الفارغ الداخلي</span>
                  <span class="cm-field-value">${this.formatDateTime(item.internalEmptyWeightTime)}</span>
                </div>
                <div class="cm-field-item">
                  <span class="cm-field-label">وقت الوزن القائم الداخلي</span>
                  <span class="cm-field-value">${this.formatDateTime(item.internalLoadedWeightTime)}</span>
                </div>
                <div class="cm-field-item">
                  <span class="cm-field-label">وقت الوزن الفارغ الخارجي</span>
                  <span class="cm-field-value">${this.formatDateTime(item.externalEmptyWeightTime)}</span>
                </div>
                <div class="cm-field-item">
                  <span class="cm-field-label">وقت الوزن القائم الخارجي</span>
                  <span class="cm-field-value">${this.formatDateTime(item.externalLoadedWeightTime)}</span>
                </div>
                <div class="cm-field-item">
                  <span class="cm-field-label">تاريخ ووقت المغادرة</span>
                  <span class="cm-field-value">${this.formatDateTime(item.departureTime)}</span>
                </div>
                <div class="cm-field-item">
                  <span class="cm-field-label">تاريخ ووقت الوصول</span>
                  <span class="cm-field-value">${this.formatDateTime(item.arrivalTime)}</span>
                </div>
                <div class="cm-field-item">
                  <span class="cm-field-label">مدة الشحن المحسوبة</span>
                  <span class="cm-field-value highlight">${item.shippingDurationMinutes ? item.shippingDurationMinutes + ' دقيقة' : '-'}</span>
                </div>
              </div>
            </div>

            <!-- Group 4: Supervisors, Receipt & Financials -->
            <div class="cm-form-group">
              <div class="cm-form-group-header">
                <span class="cm-form-group-icon">👤</span>
                <span>الإشراف والاستلام والملاحظات (Supervisors & Notes)</span>
              </div>
              <div class="cm-fields-grid">
                <div class="cm-field-item">
                  <span class="cm-field-label">المسؤول عن الشحن</span>
                  <span class="cm-field-value">${this.escapeHtml(item.shippingSupervisor || '-')}</span>
                </div>
                <div class="cm-field-item">
                  <span class="cm-field-label">مسؤول القسم</span>
                  <span class="cm-field-value">${this.escapeHtml(item.departmentSupervisor || '-')}</span>
                </div>
                <div class="cm-field-item">
                  <span class="cm-field-label">اسم مستلم الشحنة</span>
                  <span class="cm-field-value">${this.escapeHtml(item.recipientName || '-')}</span>
                </div>
                <div class="cm-field-item">
                  <span class="cm-field-label">مسمى مستلم الشحنة</span>
                  <span class="cm-field-value">${this.escapeHtml(item.recipientTitle || '-')}</span>
                </div>
                <div class="cm-field-item">
                  <span class="cm-field-label">تاريخ الاستلام الفعلي</span>
                  <span class="cm-field-value">${this.formatDateTime(item.receivedDate)}</span>
                </div>
                <div class="cm-field-item">
                  <span class="cm-field-label">حالة الاستلام</span>
                  <span class="cm-field-value">${this.escapeHtml(item.receivingStatus || '-')}</span>
                </div>
                <div class="cm-field-item">
                  <span class="cm-field-label">حالة الحركة الميدانية</span>
                  <span class="cm-field-value">${this.escapeHtml(item.transactionStatus || '-')}</span>
                </div>
                <div class="cm-field-item">
                  <span class="cm-field-label">مبلغ الإكرامية</span>
                  <span class="cm-field-value">${Number(item.ikramiya || 0).toLocaleString('ar-EG', { minimumFractionDigits: 2 })} ج.م</span>
                </div>
                <div class="cm-field-item">
                  <span class="cm-field-label">مبلغ العهدة المالية</span>
                  <span class="cm-field-value highlight">${Number(item.custodyAmount || 0).toLocaleString('ar-EG', { minimumFractionDigits: 2 })} ج.م</span>
                </div>
              </div>
              ${item.notes ? `
                <div class="cm-field-item" style="margin-top:0.75rem;">
                  <span class="cm-field-label">ملاحظات النموذج المرفقة</span>
                  <span class="cm-field-value" style="font-weight:500;">${this.escapeHtml(item.notes)}</span>
                </div>
              ` : ''}
            </div>
          </div>
        `;
      } else {
        // Storage Form Data
        this.formDataBody.innerHTML = `
          <div class="cm-form-groups-container">
            <!-- Group 1: Movement & Material Info -->
            <div class="cm-form-group">
              <div class="cm-form-group-header">
                <span class="cm-form-group-icon">📦</span>
                <span>بيانات الحركة المخزنية والخامة (Storage Movement & Material)</span>
              </div>
              <div class="cm-fields-grid">
                <div class="cm-field-item">
                  <span class="cm-field-label">رقم الحركة المخزنية</span>
                  <span class="cm-field-value highlight">#${item.transactionNumber || item.formId}</span>
                </div>
                <div class="cm-field-item">
                  <span class="cm-field-label">رقم المرجع (Reference)</span>
                  <span class="cm-field-value" dir="ltr">${item.referenceNumber || '-'}</span>
                </div>
                <div class="cm-field-item">
                  <span class="cm-field-label">نوع الحركة (Action)</span>
                  <span class="cm-field-value highlight">${this.escapeHtml(item.action || '-')}</span>
                </div>
                <div class="cm-field-item">
                  <span class="cm-field-label">نوع الخدمة (Service Type)</span>
                  <span class="cm-field-value">${this.escapeHtml(item.serviceType || '-')}</span>
                </div>
                <div class="cm-field-item">
                  <span class="cm-field-label">أمر الشراء المرتبط (PO)</span>
                  <span class="cm-field-value highlight" dir="ltr">${this.escapeHtml(item.poNumber || 'غير مرتبط بأمر شراء')}</span>
                </div>
                <div class="cm-field-item">
                  <span class="cm-field-label">مربوط بأمر شراء رسمي؟</span>
                  <span class="cm-field-value">${item.isExportRelatedToPO ? 'نعم' : 'لا'}</span>
                </div>
                <div class="cm-field-item">
                  <span class="cm-field-label">نوع المادة / الخامة</span>
                  <span class="cm-field-value">${this.escapeHtml(item.material || '-')}</span>
                </div>
                <div class="cm-field-item">
                  <span class="cm-field-label">موقع المستودع</span>
                  <span class="cm-field-value">${this.escapeHtml(item.storageLocation || '-')}</span>
                </div>
                ${item.destinationStorageLocation ? `
                  <div class="cm-field-item">
                    <span class="cm-field-label">موقع المستودع المحول إليه</span>
                    <span class="cm-field-value highlight">${this.escapeHtml(item.destinationStorageLocation)}</span>
                  </div>
                ` : ''}
              </div>
            </div>

            <!-- Group 2: Quantities & Inventory Balances -->
            <div class="cm-form-group">
              <div class="cm-form-group-header">
                <span class="cm-form-group-icon">📊</span>
                <span>الكميات والأرصدة المخزنية (Quantities & Balances)</span>
              </div>
              <div class="cm-fields-grid">
                <div class="cm-field-item">
                  <span class="cm-field-label">الكمية الواردة (Incoming)</span>
                  <span class="cm-field-value success" style="font-size:1.05rem;">${this.formatWeight(item.incomingQuantity)} طن</span>
                </div>
                <div class="cm-field-item">
                  <span class="cm-field-label">الكمية المنصرفة (Outgoing)</span>
                  <span class="cm-field-value danger" style="font-size:1.05rem;">${this.formatWeight(item.outgoingQuantity)} طن</span>
                </div>
                <div class="cm-field-item">
                  <span class="cm-field-label">الرصيد الافتتاحي (Opening Balance)</span>
                  <span class="cm-field-value">${this.formatWeight(item.openingBalance)} طن</span>
                </div>
                <div class="cm-field-item">
                  <span class="cm-field-label">الرصيد الختامي بعد الحركة (Closing Balance)</span>
                  <span class="cm-field-value highlight" style="font-size:1.05rem;">${this.formatWeight(item.closingBalance)} طن</span>
                </div>
              </div>
            </div>

            <!-- Group 3: Weighbridge Data -->
            <div class="cm-form-group">
              <div class="cm-form-group-header">
                <span class="cm-form-group-icon">⚖️</span>
                <span>بيانات الميزان الداخلي والخارجي (Weighbridge Measurements)</span>
              </div>
              <div class="cm-fields-grid">
                <div class="cm-field-item">
                  <span class="cm-field-label">مرجع الميزان الداخلي</span>
                  <span class="cm-field-value" dir="ltr">${this.escapeHtml(item.internalWeighbridgeReference || '-')}</span>
                </div>
                <div class="cm-field-item">
                  <span class="cm-field-label">الوزن الداخلي قائم (Loaded)</span>
                  <span class="cm-field-value">${this.formatWeight(item.storageInternalLoadedWeight)} طن</span>
                </div>
                <div class="cm-field-item">
                  <span class="cm-field-label">الوزن الداخلي فارغ (Empty)</span>
                  <span class="cm-field-value">${this.formatWeight(item.storageInternalEmptyWeight)} طن</span>
                </div>
                <div class="cm-field-item">
                  <span class="cm-field-label">الوزن الداخلي الصافي (Net)</span>
                  <span class="cm-field-value highlight">${this.formatWeight(item.storageInternalNetWeight)} طن</span>
                </div>
                <div class="cm-field-item">
                  <span class="cm-field-label">تاريخ ووقت الوزن الفارغ الداخلي</span>
                  <span class="cm-field-value">${this.formatDateTime(item.storageInternalEmptyWeightTime)}</span>
                </div>
                <div class="cm-field-item">
                  <span class="cm-field-label">تاريخ ووقت الوزن القائم الداخلي</span>
                  <span class="cm-field-value">${this.formatDateTime(item.storageInternalLoadedWeightTime)}</span>
                </div>
                <div class="cm-field-item">
                  <span class="cm-field-label">هل يوجد ميزان خارجي؟</span>
                  <span class="cm-field-value">${item.hasExternalWeighbridge ? 'نعم' : 'لا'}</span>
                </div>
                <div class="cm-field-item">
                  <span class="cm-field-label">مرجع الميزان الخارجي</span>
                  <span class="cm-field-value" dir="ltr">${this.escapeHtml(item.externalWeighbridgeReference || '-')}</span>
                </div>
                <div class="cm-field-item">
                  <span class="cm-field-label">الوزن الخارجي قائم</span>
                  <span class="cm-field-value">${this.formatWeight(item.storageExternalLoadedWeight)} طن</span>
                </div>
                <div class="cm-field-item">
                  <span class="cm-field-label">الوزن الخارجي فارغ</span>
                  <span class="cm-field-value">${this.formatWeight(item.storageExternalEmptyWeight)} طن</span>
                </div>
                <div class="cm-field-item">
                  <span class="cm-field-label">الوزن الخارجي الصافي</span>
                  <span class="cm-field-value highlight">${this.formatWeight(item.storageExternalNetWeight)} طن</span>
                </div>
              </div>
            </div>

            <!-- Group 4: Responsible Staff & Notes -->
            <div class="cm-form-group">
              <div class="cm-form-group-header">
                <span class="cm-form-group-icon">👤</span>
                <span>المسؤولين وملاحظات الحركة (Responsible Staff & Notes)</span>
              </div>
              <div class="cm-fields-grid">
                <div class="cm-field-item">
                  <span class="cm-field-label">الموظف المسؤول / المستلم</span>
                  <span class="cm-field-value">${this.escapeHtml(item.responsibleEmployee || item.receivingEmployee || '-')}</span>
                </div>
                <div class="cm-field-item">
                  <span class="cm-field-label">سجل بواسطة (Created By)</span>
                  <span class="cm-field-value">${this.escapeHtml(item.createdBy || item.submittedBy || '-')}</span>
                </div>
                <div class="cm-field-item">
                  <span class="cm-field-label">تاريخ التسجيل والإنشاء</span>
                  <span class="cm-field-value">${this.formatDateTime(item.createdAt || item.submissionDate)}</span>
                </div>
              </div>
              ${item.notes ? `
                <div class="cm-field-item" style="margin-top:0.75rem;">
                  <span class="cm-field-label">ملاحظات الحركة المخزنية</span>
                  <span class="cm-field-value" style="font-weight:500;">${this.escapeHtml(item.notes)}</span>
                </div>
              ` : ''}
            </div>
          </div>
        `;
      }
    }

    /* ------------------------------------------------------------------------
       ATTACHMENTS SECTION: SHOW EVERY ATTACHMENT WITH PREVIEW & DOWNLOAD
       ------------------------------------------------------------------------ */
    renderAttachmentsSection(item) {
      if (!this.attachmentsList) return;

      const attachments = item.attachments || [];
      const count = attachments.length;

      if (this.attachmentCountPill) {
        this.attachmentCountPill.textContent = count > 0 ? `${count} مرفقات` : 'بدون مرفقات';
      }

      if (count === 0) {
        this.attachmentsList.innerHTML = `
          <div style="grid-column: 1 / -1; padding: 2.5rem; text-align: center; background: #F8FAFC; border: 1px dashed #CBD5E1; border-radius: 12px; color: #64748B;">
            <div style="font-size: 2rem; margin-bottom: 6px;">📎</div>
            <h4 style="margin: 0 0 4px; color: #334155;">لا توجد مرفقات ثبوتية مسجلة لهذا الطلب</h4>
            <p style="margin: 0; font-size: 0.825rem; color: #94A3B8;">لم يتم إرفاق ملفات إضافية أثناء تقديم هذا النموذج.</p>
          </div>
        `;
        return;
      }

      let html = '';
      attachments.forEach((att, idx) => {
        const fileTypeLower = (att.fileType || '').toLowerCase();
        const isPdf = fileTypeLower.includes('pdf');
        const isImage = fileTypeLower.includes('image') || fileTypeLower.includes('png') || fileTypeLower.includes('jpg') || fileTypeLower.includes('jpeg');
        const sizeFormatted = this.formatFileSize(att.fileSize);
        const uploadDateFormatted = att.uploadDate ? new Date(att.uploadDate).toLocaleString('ar-EG') : '-';
        const uploader = att.uploadedBy || item.submittedBy || 'الموظف المسجل';

        const viewUrl = att.viewUrl || att.downloadUrl;
        const downloadUrl = att.downloadUrl || att.viewUrl;
        const authedViewUrl = this.getAttachmentUrlWithToken(viewUrl);

        // Thumbnail / Icon
        let thumbHtml = '';
        if (isImage && viewUrl) {
          thumbHtml = `
            <div class="cm-attachment-thumb-wrap cm-trigger-preview" data-index="${idx}">
              <img src="${this.escapeHtml(authedViewUrl)}" alt="${this.escapeHtml(att.title || att.fileName)}" loading="lazy" onerror="this.onerror=null; this.src='/images/image-placeholder.png';" />
              <div class="cm-attachment-thumb-overlay">
                <span>🔍 انقر للمعاينة المكبرة</span>
              </div>
            </div>
          `;
        } else {
          thumbHtml = `
            <div class="cm-attachment-thumb-wrap cm-trigger-preview" data-index="${idx}" style="cursor: pointer;">
              <div class="cm-attachment-doc-placeholder">
                <div class="doc-icon">${isPdf ? '📄' : '📁'}</div>
                <span>${isPdf ? 'مستند PDF رسمي' : 'مستند ثبوتي'}</span>
              </div>
              <div class="cm-attachment-thumb-overlay">
                <span>👁️ انقر لفتح المستند</span>
              </div>
            </div>
          `;
        }

        html += `
          <div class="cm-attachment-card">
            ${thumbHtml}
            <div class="cm-attachment-meta-info">
              <span class="cm-attachment-title" title="${this.escapeHtml(att.fileName)}">${this.escapeHtml(att.title || att.fileName)}</span>
              <div class="cm-attachment-meta-sub">
                <span>${sizeFormatted}</span>
                <span class="sep">•</span>
                <span>${isPdf ? 'PDF' : isImage ? 'صورة' : 'ملف'}</span>
              </div>
              <div class="cm-attachment-uploader">
                <span>رفع بواسطة: <strong>${this.escapeHtml(uploader)}</strong></span>
                <span class="sep">•</span>
                <span>${uploadDateFormatted}</span>
              </div>
            </div>
            <div class="cm-attachment-actions">
              <button type="button" class="cm-btn-view cm-trigger-preview" data-index="${idx}" title="معاينة الملف">
                <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.5"><path d="M1 12s4-8 11-8 11 8 11 8-4 8-11 8-11-8-11-8z"/><circle cx="12" cy="12" r="3"/></svg>
                <span>معاينة</span>
              </button>
              <button type="button" class="cm-btn-download cm-trigger-download" data-index="${idx}" title="تحميل الملف الأصلي">
                <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.5"><path d="M21 15v4a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2v-4"/><polyline points="7 10 12 15 17 10"/><line x1="12" y1="15" x2="12" y2="3"/></svg>
                <span>تحميل</span>
              </button>
            </div>
          </div>
        `;
      });

      this.attachmentsList.innerHTML = html;

      // Bind preview triggers
      const triggers = this.attachmentsList.querySelectorAll('.cm-trigger-preview');
      triggers.forEach(el => {
        el.addEventListener('click', (e) => {
          const idx = parseInt(e.currentTarget.getAttribute('data-index'), 10);
          if (attachments[idx]) {
            this.openPreviewOverlay(attachments[idx]);
          }
        });
      });

      // Bind authenticated download triggers
      const dlTriggers = this.attachmentsList.querySelectorAll('.cm-trigger-download');
      dlTriggers.forEach(el => {
        el.addEventListener('click', async (e) => {
          const idx = parseInt(e.currentTarget.getAttribute('data-index'), 10);
          if (attachments[idx]) {
            await this.downloadAttachment(attachments[idx], e.currentTarget);
          }
        });
      });
    }

    /* ------------------------------------------------------------------------
       REVIEW & APPROVAL DECISION SECTION
       ------------------------------------------------------------------------ */
    renderDecisionSection(item, status) {
      if (!this.pendingActionsWrap || !this.finalDecisionInfo) return;

      if (this.acceptBox) this.acceptBox.style.display = 'none';
      if (this.rejectBox) this.rejectBox.style.display = 'none';

      if (status === 'PENDING') {
        this.pendingActionsWrap.style.display = 'block';
        this.finalDecisionInfo.style.display = 'none';
        if (this.btnAccept) this.btnAccept.disabled = false;
        if (this.btnReject) this.btnReject.disabled = false;
      } else {
        this.pendingActionsWrap.style.display = 'none';
        this.finalDecisionInfo.style.display = 'block';

        if (status === 'APPROVED') {
          this.finalDecisionInfo.className = 'cm-decision-approved-box';
          this.finalDecisionInfo.innerHTML = `
            <div style="font-weight: 800; font-size: 1rem; margin-bottom: 6px;">
              ✓ النموذج معتمد رسمياً
            </div>
            <div>
              تم الاعتماد بواسطة: <strong>${this.escapeHtml(item.reviewedBy || 'مراجع معتمد')}</strong>
              بتاريخ <strong>${item.reviewedAt ? new Date(item.reviewedAt).toLocaleString('ar-EG') : '-'}</strong>.
            </div>
            <div style="font-size: 0.8rem; color: #047857; margin-top: 6px;">
              الحسابات والأرصدة محدثة بناءً على هذا الاعتماد ولا يمكن إعادة معالجة الطلب مرة أخرى.
            </div>
          `;
        } else if (status === 'REJECTED') {
          this.finalDecisionInfo.className = 'cm-decision-rejected-box';
          this.finalDecisionInfo.innerHTML = `
            <div style="font-weight: 800; font-size: 1rem; margin-bottom: 6px;">
              ✕ تم رفض النموذج وتوثيق سبب الرفض
            </div>
            <div>
              تم الرفض بواسطة: <strong>${this.escapeHtml(item.reviewedBy || 'مراجع العمليات')}</strong>
              بتاريخ <strong>${item.reviewedAt ? new Date(item.reviewedAt).toLocaleString('ar-EG') : '-'}</strong>.
            </div>
            ${item.rejectionReason && item.rejectionReason.trim() ? `
              <div style="margin-top: 6px;">
                <strong>سبب الرفض:</strong> "${this.escapeHtml(item.rejectionReason)}".
              </div>
            ` : ''}
            <div style="font-size: 0.8rem; color: #7F1D1D; margin-top: 6px;">
              تم إرسال الإشعار للموظف المختص للتصحيح وإعادة الإرسال.
            </div>
          `;
        }
      }
    }

    async handleConfirmAccept() {
      if (!this.activeFormType || !this.activeFormId) return;

      try {
        if (this.btnConfirmAccept) this.btnConfirmAccept.disabled = true;
        if (this.btnAccept) this.btnAccept.disabled = true;

        const res = await window.ChatApi.acceptControlMonitoringForm(this.activeFormType, this.activeFormId);

        if (res && res.success) {
          if (this.acceptBox) this.acceptBox.style.display = 'none';
          
          // Immediate visible UI state update
          if (this.activeItem) {
            this.activeItem.status = 'APPROVED';
            this.activeItem.reviewedBy = (window.currentUser && window.currentUser.fullName) || 'موظف التحكم والمراقبة';
            this.activeItem.reviewedAt = new Date().toISOString();
          }

          await this.loadRequestDetails(this.activeFormType, this.activeFormId, false);
          this.loadStats();
          this.loadForms();
        } else {
          alert('تعذر اعتماد النموذج: ' + (res?.message || 'حدث خطأ غير معروف'));
        }
      } catch (err) {
        console.error('Error accepting form:', err);
        alert('حدث خطأ أثناء إرسال قرار الاعتماد.');
      } finally {
        if (this.btnConfirmAccept) this.btnConfirmAccept.disabled = false;
        if (this.btnAccept) this.btnAccept.disabled = false;
      }
    }

    async handleConfirmReject() {
      if (!this.activeFormType || !this.activeFormId) return;

      const reason = this.rejectReasonInput ? this.rejectReasonInput.value.trim() : '';

      try {
        if (this.btnConfirmReject) this.btnConfirmReject.disabled = true;
        if (this.btnReject) this.btnReject.disabled = true;

        const res = await window.ChatApi.rejectControlMonitoringForm(
          this.activeFormType,
          this.activeFormId,
          reason
        );

        if (res && res.success) {
          if (this.rejectBox) this.rejectBox.style.display = 'none';

          // Immediate visible UI state update
          if (this.activeItem) {
            this.activeItem.status = 'REJECTED';
            this.activeItem.reviewedBy = (window.currentUser && window.currentUser.fullName) || 'موظف التحكم والمراقبة';
            this.activeItem.reviewedAt = new Date().toISOString();
            this.activeItem.rejectionReason = reason;
          }

          await this.loadRequestDetails(this.activeFormType, this.activeFormId, false);
          this.loadStats();
          this.loadForms();
        } else {
          alert('تعذر رفض النموذج: ' + (res?.message || 'حدث خطأ غير معروف'));
        }
      } catch (err) {
        console.error('Error rejecting form:', err);
        alert('حدث خطأ أثناء تسجيل قرار الرفض.');
      } finally {
        if (this.btnConfirmReject) this.btnConfirmReject.disabled = false;
        if (this.btnReject) this.btnReject.disabled = false;
      }
    }

    /* ------------------------------------------------------------------------
       AUDIT HISTORY TIMELINE: SHOW COMPLETE HISTORY OF REQUEST
       ------------------------------------------------------------------------ */
    renderAuditTimeline(history) {
      if (!this.detailsTimeline) return;

      if (!history || history.length === 0) {
        this.detailsTimeline.innerHTML = `
          <div style="color: #94A3B8; font-size: 0.85rem; padding: 0.5rem 0;">لا توجد سجلات تدقيق سابقة مسجلة لهذا الطلب.</div>
        `;
        return;
      }

      let timelineHtml = '';
      history.forEach(h => {
        const timeStr = h.timestamp
          ? new Date(h.timestamp).toLocaleString('ar-EG', {
              hour: '2-digit',
              minute: '2-digit',
              second: '2-digit',
              year: 'numeric',
              month: 'numeric',
              day: 'numeric'
            })
          : '';

        const action = (h.action || '').toUpperCase();
        let dotClass = '';
        let actionBadge = `<span class="cm-badge cm-badge-pending">${action}</span>`;

        if (action === 'ACCEPTED' || action === 'APPROVED') {
          dotClass = 'approved';
          actionBadge = `<span class="cm-badge cm-badge-approved">✓ موافقة واعتماد</span>`;
        } else if (action === 'REJECTED') {
          dotClass = 'rejected';
          actionBadge = `<span class="cm-badge cm-badge-rejected">✕ رفض للتدقيق</span>`;
        } else if (action === 'RESUBMITTED') {
          actionBadge = `<span class="cm-badge cm-badge-pending">🔄 إعادة تقديم</span>`;
        } else if (action === 'SUBMITTED') {
          actionBadge = `<span class="cm-badge cm-badge-trans">📤 تقديم أولي</span>`;
        }

        timelineHtml += `
          <div class="cm-timeline-item">
            <div class="cm-timeline-dot ${dotClass}"></div>
            <div class="cm-timeline-header">
              ${actionBadge}
              <span>${this.escapeHtml(h.actorName || 'النظام')} ${h.actorRole ? '(' + this.escapeHtml(h.actorRole) + ')' : ''}</span>
              <span class="cm-timeline-time">${timeStr}</span>
            </div>
            ${h.comment ? `
              <div class="cm-timeline-comment ${action === 'REJECTED' ? 'rejected' : ''}">
                ${this.escapeHtml(h.comment)}
              </div>
            ` : ''}
          </div>
        `;
      });

      this.detailsTimeline.innerHTML = timelineHtml;
    }

    /* ------------------------------------------------------------------------
       TABLE HORIZONTAL SLIDER & DRAG SETUP INSIDE finance-table-wrapper
       ------------------------------------------------------------------------ */
    setupTableSlider() {
      if (!this.tableWrapper) return;

      const scrollStep = 360;

      // In Arabic RTL, scrolling left advances columns, scrolling right returns
      const scrollNext = () => {
        this.tableWrapper.scrollBy({ left: -scrollStep, behavior: 'smooth' });
      };

      const scrollPrev = () => {
        this.tableWrapper.scrollBy({ left: scrollStep, behavior: 'smooth' });
      };

      if (this.btnSliderNext) this.btnSliderNext.addEventListener('click', scrollNext);
      if (this.btnSliderPrev) this.btnSliderPrev.addEventListener('click', scrollPrev);
      if (this.btnSideNext) this.btnSideNext.addEventListener('click', scrollNext);
      if (this.btnSidePrev) this.btnSidePrev.addEventListener('click', scrollPrev);

      // Desktop Mouse Drag
      let isDragging = false;
      let startX = 0;
      let scrollLeftPos = 0;
      let draggedDistance = 0;

      this.tableWrapper.addEventListener('mousedown', (e) => {
        if (e.button !== 0) return;
        if (e.target.closest('button, a, input, select, textarea')) return;

        isDragging = true;
        draggedDistance = 0;
        startX = e.pageX - this.tableWrapper.offsetLeft;
        scrollLeftPos = this.tableWrapper.scrollLeft;
        this.tableWrapper.classList.add('is-dragging');
      });

      window.addEventListener('mouseup', () => {
        if (isDragging) {
          isDragging = false;
          this.tableWrapper.classList.remove('is-dragging');
          setTimeout(() => { draggedDistance = 0; }, 60);
        }
      });

      this.tableWrapper.addEventListener('mousemove', (e) => {
        if (!isDragging) return;
        e.preventDefault();
        const currentX = e.pageX - this.tableWrapper.offsetLeft;
        const walk = (currentX - startX) * 1.5;
        draggedDistance += Math.abs(walk);
        this.tableWrapper.scrollLeft = scrollLeftPos - walk;
      });

      // Suppress clicks on child elements if user was dragging
      this.tableWrapper.addEventListener('click', (e) => {
        if (draggedDistance > 8) {
          e.preventDefault();
          e.stopPropagation();
        }
      }, true);
    }

    /* ------------------------------------------------------------------------
       AUTHENTICATION TOKEN HELPER
       Retrieves valid JWT access token from ChatApi or storage
       ------------------------------------------------------------------------ */
    getToken() {
      if (window.ChatApi && typeof window.ChatApi.getToken === 'function') {
        const t = window.ChatApi.getToken();
        if (t) return t;
      }
      return sessionStorage.getItem('facestone_chat_jwt') || 
             localStorage.getItem('facestone_chat_jwt') || 
             sessionStorage.getItem('facestone_token') || 
             localStorage.getItem('facestone_token') || '';
    }

    /* ------------------------------------------------------------------------
       AUTHENTICATED ATTACHMENT URL HELPER
       ------------------------------------------------------------------------ */
    getAttachmentUrlWithToken(rawUrl) {
      if (!rawUrl) return '';
      if (window.ChatApi && typeof window.ChatApi.getAuthenticatedUrl === 'function') {
        return window.ChatApi.getAuthenticatedUrl(rawUrl);
      }
      const token = this.getToken();
      if (!token) return rawUrl;
      const separator = rawUrl.includes('?') ? '&' : '?';
      return `${rawUrl}${separator}token=${encodeURIComponent(token)}`;
    }

    /* ------------------------------------------------------------------------
       AUTHENTICATED ATTACHMENT DOWNLOAD HANDLER
       Retrieves real uploaded file via HTTP and triggers native download
       Guarantees zero 401 "File wasn't available on site" errors
       ------------------------------------------------------------------------ */
    async downloadAttachment(att, btnElement) {
      if (!att) return;
      const originalText = btnElement ? btnElement.innerHTML : '';
      if (btnElement) {
        btnElement.disabled = true;
        btnElement.innerHTML = `<span>جاري التحميل...</span>`;
      }

      try {
        const rawUrl = att.downloadUrl || att.viewUrl;
        if (window.ChatApi && typeof window.ChatApi.downloadAttachmentFromUrl === 'function') {
          await window.ChatApi.downloadAttachmentFromUrl(rawUrl, att.fileName);
          return;
        }

        const token = this.getToken();
        const url = this.getAttachmentUrlWithToken(rawUrl);
        const headers = {};
        if (token) {
          headers['Authorization'] = `Bearer ${token}`;
        }

        const res = await fetch(url, { headers });
        if (!res.ok) {
          throw new Error(`تعذر تنزيل الملف (رمز الاستجابة: ${res.status})`);
        }

        let downloadFileName = att.fileName || 'attachment';
        const cd = res.headers.get('content-disposition');
        if (cd) {
          const fnMatch = cd.match(/filename\*=UTF-8''([^;]+)|filename="?([^";]+)"?/i);
          if (fnMatch) {
            downloadFileName = decodeURIComponent(fnMatch[1] || fnMatch[2] || downloadFileName);
          }
        }

        const blob = await res.blob();
        const blobUrl = URL.createObjectURL(blob);
        const a = document.createElement('a');
        a.style.display = 'none';
        a.href = blobUrl;
        a.download = downloadFileName;
        document.body.appendChild(a);
        a.click();
        setTimeout(() => {
          document.body.removeChild(a);
          URL.revokeObjectURL(blobUrl);
        }, 5000);
      } catch (err) {
        console.error('Download error:', err);
        alert(err.message || 'حدث خطأ أثناء تحميل الملف من الخادم.');
      } finally {
        if (btnElement) {
          btnElement.disabled = false;
          btnElement.innerHTML = originalText;
        }
      }
    }

    /* ------------------------------------------------------------------------
       FULL-SCREEN PREVIEW OVERLAY FOR IMAGES & DOCUMENTS
       ------------------------------------------------------------------------ */
    async openPreviewOverlay(att) {
      if (!this.previewOverlay) return;
      if (this.previewTitle) this.previewTitle.textContent = att.title || att.fileName;

      const token = this.getToken();
      const authedViewUrl = this.getAttachmentUrlWithToken(att.viewUrl || att.downloadUrl);

      if (this.previewDownloadLink) {
        this.previewDownloadLink.onclick = (e) => {
          e.preventDefault();
          this.downloadAttachment(att, this.previewDownloadLink);
        };
      }

      if (this.previewPopoutLink) {
        this.previewPopoutLink.href = authedViewUrl;
      }

      if (this.previewBody) {
        this.previewBody.innerHTML = `
          <div style="display:flex; flex-direction:column; align-items:center; gap:10px; color:#94A3B8; padding:3rem;">
            <div class="finance-spinner"></div>
            <span>جاري تجهيز وعرض الملف...</span>
          </div>
        `;

        const fileType = (att.fileType || '').toLowerCase();
        const fileNameLower = (att.fileName || '').toLowerCase();
        const isPdf = fileType.includes('pdf') || fileNameLower.endsWith('.pdf');

        try {
          if (isPdf) {
            this.previewBody.innerHTML = `
              <iframe src="${this.escapeHtml(authedViewUrl)}" title="${this.escapeHtml(att.title || att.fileName)}" style="width:100%; height:82vh; border:none; background:#FFFFFF; border-radius:4px;"></iframe>
            `;
          } else {
            const img = document.createElement('img');
            img.alt = att.title || att.fileName;
            img.style.maxWidth = '100%';
            img.style.maxHeight = '82vh';
            img.style.objectFit = 'contain';
            img.style.borderRadius = '4px';

            let loaded = false;
            img.onload = () => {
              if (loaded) return;
              loaded = true;
              this.previewBody.innerHTML = '';
              this.previewBody.appendChild(img);
            };

            img.onerror = async () => {
              if (loaded) return;
              try {
                const headers = token ? { 'Authorization': `Bearer ${token}` } : {};
                const res = await fetch(authedViewUrl, { headers });
                if (res.ok) {
                  const blob = await res.blob();
                  const blobUrl = URL.createObjectURL(blob);
                  img.src = blobUrl;
                  loaded = true;
                  this.previewBody.innerHTML = '';
                  this.previewBody.appendChild(img);
                } else {
                  throw new Error(`Status ${res.status}`);
                }
              } catch {
                this.previewBody.innerHTML = `
                  <div style="padding:2.5rem; text-align:center; color:#EF4444;">
                    <div style="font-size:2.5rem; margin-bottom:8px;">⚠️</div>
                    <p style="margin:0 0 10px; font-weight:700;">تعذر عرض المعاينة المباشرة لهذا الملف.</p>
                    <button type="button" class="cm-btn-download" style="background:#0284C7; color:#fff;" id="cm-preview-fallback-dl">تحميل الملف لفتحه</button>
                  </div>
                `;
                const fallbackDl = document.getElementById('cm-preview-fallback-dl');
                if (fallbackDl) fallbackDl.onclick = () => this.downloadAttachment(att, fallbackDl);
              }
            };

            img.src = authedViewUrl;
          }
        } catch (err) {
          console.error('Preview error:', err);
          this.previewBody.innerHTML = `
            <div style="padding:2rem; text-align:center; color:#EF4444;">
              <p>حدث خطأ أثناء تحميل معاينة الملف.</p>
            </div>
          `;
        }
      }

      this.previewOverlay.style.display = 'flex';
    }

    closePreviewOverlay() {
      if (!this.previewOverlay) return;
      this.previewOverlay.style.display = 'none';
      if (this.previewBody) this.previewBody.innerHTML = '';
    }

    /* ------------------------------------------------------------------------
       HELPERS & FORMATTERS
       ------------------------------------------------------------------------ */
    formatWeight(val) {
      if (val === null || val === undefined || isNaN(val)) return '0.000';
      return Number(val).toFixed(3);
    }

    formatDateTime(dateStr) {
      if (!dateStr) return '-';
      const d = new Date(dateStr);
      if (isNaN(d.getTime())) return '-';
      return d.toLocaleString('ar-EG', {
        year: 'numeric',
        month: 'short',
        day: 'numeric',
        hour: '2-digit',
        minute: '2-digit'
      });
    }

    formatFileSize(bytes) {
      if (!bytes || bytes <= 0) return '0 B';
      const k = 1024;
      const sizes = ['B', 'KB', 'MB', 'GB'];
      const i = Math.floor(Math.log(bytes) / Math.log(k));
      return parseFloat((bytes / Math.pow(k, i)).toFixed(2)) + ' ' + sizes[i];
    }

    escapeHtml(str) {
      if (str === null || str === undefined) return '';
      return String(str)
        .replace(/&/g, '&amp;')
        .replace(/</g, '&lt;')
        .replace(/>/g, '&gt;')
        .replace(/"/g, '&quot;')
        .replace(/'/g, '&#39;');
    }
  }

  // Expose singleton to window
  window.ControlMonitoringApp = new ControlMonitoringApp();
})();
