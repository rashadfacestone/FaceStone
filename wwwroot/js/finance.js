/**
 * FaceStone Web - Finance Management & Statistics Application
 * Professional Arabic RTL Financial Dashboard
 */
(function () {
  'use strict';

  class FinanceManagementApp {
    constructor() {
      this.initialized = false;
      this.currentPage = 1;
      this.pageSize = 10;
      this.totalPages = 1;
      this.totalRecords = 0;
      this.isLoading = false;
      this.isLoadingStatistics = false;

      // Primary & Major filters
      this.selectedDate = '2026-10-05';
      this.selectedPo = 'all';
      this.availablePos = [];
      this.statisticsData = null;

      // Table filter state
      this.filters = {
        search: '',
        po: '',
        supplier: '',
        paymentStatus: 'all',
        invoiceNumber: '',
        dateFrom: '',
        dateTo: ''
      };

      this.currentRecords = [];
    }

    init() {
      if (this.initialized) return;
      this.initialized = true;

      this.bindDOMElements();
      this.initDefaultDate();
      this.bindEvents();
      this.loadPoDropdown();
      this.loadStatistics();
      this.loadData();
    }

    initDefaultDate() {
      // Default to today's date according to business/server date
      const today = new Date();
      const y = today.getFullYear();
      const m = String(today.getMonth() + 1).padStart(2, '0');
      const d = String(today.getDate()).padStart(2, '0');
      const todayStr = `${y}-${m}-${d}`;
      
      this.selectedDate = todayStr || '2026-10-05';
      if (this.mainDateInput) {
        this.mainDateInput.value = this.selectedDate;
      }
    }

    bindDOMElements() {
      // Containers
      this.root = document.getElementById('finance-management-page-root');
      this.tableBody = document.getElementById('finance-table-body');
      this.tableContainer = document.getElementById('finance-table-scroll');
      this.loadingEl = document.getElementById('finance-table-loading');
      this.emptyEl = document.getElementById('finance-table-empty');
      this.errorEl = document.getElementById('finance-table-error');

      // User Profile Header
      this.userNameEl = document.getElementById('finance-mgmt-user-name');
      this.userRoleEl = document.getElementById('finance-mgmt-user-role');
      this.userAvatarEl = document.getElementById('finance-mgmt-user-avatar');

      // 1. Main Direct Filters (Primary Date + Major PO)
      this.mainDateInput = document.getElementById('finance-filter-main-date');
      this.mainPoSelect = document.getElementById('finance-filter-main-po');
      this.btnClearPo = document.getElementById('finance-btn-clear-po');
      this.filterStatusText = document.getElementById('finance-filter-status-text');

      // Table quick search & table filters
      this.searchInput = document.getElementById('finance-search-input');
      this.searchClearBtn = document.getElementById('finance-search-clear-btn');
      this.filterPoInput = document.getElementById('finance-filter-po');
      this.filterSupplierSelect = document.getElementById('finance-filter-supplier');
      this.filterPaymentStatusSelect = document.getElementById('finance-filter-payment-status');
      this.filterInvoiceInput = document.getElementById('finance-filter-invoice');
      this.filterDateFrom = document.getElementById('finance-filter-date-from');
      this.filterDateTo = document.getElementById('finance-filter-date-to');
      this.resetFiltersBtn = document.getElementById('finance-reset-filters-btn');
      this.refreshBtn = document.getElementById('finance-refresh-btn');

      // 2. Factory Performance Highlight Elements
      this.perfPercentage = document.getElementById('finance-perf-percentage');
      this.perfBadge = document.getElementById('finance-perf-badge');
      this.perfBadgeText = document.getElementById('finance-perf-badge-text');
      this.perfExpectedQty = document.getElementById('finance-perf-expected-qty');
      this.perfExpectedPrice = document.getElementById('finance-perf-expected-price');
      this.perfActualQty = document.getElementById('finance-perf-actual-qty');
      this.perfActualPrice = document.getElementById('finance-perf-actual-price');
      this.perfWasteQty = document.getElementById('finance-perf-waste-qty');
      this.perfWastePrice = document.getElementById('finance-perf-waste-price');
      this.perfRemainingQty = document.getElementById('finance-perf-remaining-qty');
      this.perfRemainingPrice = document.getElementById('finance-perf-remaining-price');

      // 3. Daily Activity Elements
      this.dailyDateBadge = document.getElementById('finance-daily-date-badge');
      this.dailyTransCount = document.getElementById('finance-daily-trans-count');
      this.dailyTransNet = document.getElementById('finance-daily-trans-net');
      this.dailyTransWaste = document.getElementById('finance-daily-trans-waste');
      this.dailyTransWastePrice = document.getElementById('finance-daily-trans-waste-price');
      this.dailyStorageImportQty = document.getElementById('finance-daily-storage-import-qty');
      this.dailyStorageImportPrice = document.getElementById('finance-daily-storage-import-price');
      this.dailyStorageWaste = document.getElementById('finance-daily-storage-waste');
      this.dailyStorageWastePrice = document.getElementById('finance-daily-storage-waste-price');
      this.dailyTotalWaste = document.getElementById('finance-daily-total-waste');
      this.dailyTotalWastePrice = document.getElementById('finance-daily-total-waste-price');

      // 4. PO Cumulative Summary Elements
      this.summaryScopeBadge = document.getElementById('finance-summary-scope-badge');
      this.summaryPoNumber = document.getElementById('finance-summary-po-number');
      this.summarySupplier = document.getElementById('finance-summary-supplier');
      this.summaryDest = document.getElementById('finance-summary-dest');
      this.summaryTonPrice = document.getElementById('finance-summary-ton-price');
      this.summaryMovementsCount = document.getElementById('finance-summary-movements-count');
      this.summaryPerfRate = document.getElementById('finance-summary-perf-rate');
      this.summaryExpectedQty = document.getElementById('finance-summary-expected-qty');
      this.summaryExpectedPrice = document.getElementById('finance-summary-expected-price');
      this.summaryDeliveredQty = document.getElementById('finance-summary-delivered-qty');
      this.summaryDeliveredPrice = document.getElementById('finance-summary-delivered-price');
      this.summaryRemainingQty = document.getElementById('finance-summary-remaining-qty');
      this.summaryRemainingPrice = document.getElementById('finance-summary-remaining-price');
      this.summaryTotalWaste = document.getElementById('finance-summary-total-waste');
      this.summaryTotalWastePrice = document.getElementById('finance-summary-total-waste-price');
      this.summaryTransWaste = document.getElementById('finance-summary-trans-waste');
      this.summaryTransWastePrice = document.getElementById('finance-summary-trans-waste-price');
      this.summaryStorageWaste = document.getElementById('finance-summary-storage-waste');
      this.summaryStorageWastePrice = document.getElementById('finance-summary-storage-waste-price');
      this.summaryPaidAmount = document.getElementById('finance-summary-paid-amount');
      this.summaryOutstandingAmount = document.getElementById('finance-summary-outstanding-amount');

      // Pagination
      this.paginationInfo = document.getElementById('finance-pagination-info');
      this.pageSizeSelect = document.getElementById('finance-page-size-select');
      this.btnPrevPage = document.getElementById('finance-btn-prev-page');
      this.btnNextPage = document.getElementById('finance-btn-next-page');
      this.pageNumbersContainer = document.getElementById('finance-page-numbers');

      // Modal
      this.paymentModal = document.getElementById('finance-payment-modal');
      this.modalPoNumber = document.getElementById('finance-modal-po-number');
      this.modalPaymentsList = document.getElementById('finance-modal-payments-list');
      this.modalCloseBtn = document.getElementById('finance-modal-close-btn');
      this.modalBackdrop = document.getElementById('finance-modal-backdrop');
    }

    bindEvents() {
      // 1. Primary Filter: Date Change (Refreshes Daily Statistics dynamically)
      this.mainDateInput?.addEventListener('change', (e) => {
        this.selectedDate = e.target.value || '';
        this.loadStatistics();
      });

      // 2. Major Filter: PO Dropdown Change
      this.mainPoSelect?.addEventListener('change', (e) => {
        const val = e.target.value;
        this.selectedPo = val;
        this.filters.po = (val === 'all' ? '' : val);
        if (this.filterPoInput) this.filterPoInput.value = this.filters.po;
        this.highlightSelectedTableRow(this.selectedPo);
        this.loadStatistics();
        this.currentPage = 1;
        this.loadData();
      });

      // 3. Clear PO Filter Button
      this.btnClearPo?.addEventListener('click', () => {
        this.selectedPo = 'all';
        if (this.mainPoSelect) this.mainPoSelect.value = 'all';
        this.filters.po = '';
        if (this.filterPoInput) this.filterPoInput.value = '';
        this.highlightSelectedTableRow('all');
        this.loadStatistics();
        this.currentPage = 1;
        this.loadData();
      });

      // Search with debounce
      let searchTimeout = null;
      this.searchInput?.addEventListener('input', (e) => {
        const val = e.target.value.trim();
        if (this.searchClearBtn) {
          this.searchClearBtn.style.display = val ? 'flex' : 'none';
        }
        clearTimeout(searchTimeout);
        searchTimeout = setTimeout(() => {
          this.filters.search = val;
          this.currentPage = 1;
          this.loadData();
        }, 350);
      });

      this.searchClearBtn?.addEventListener('click', () => {
        if (this.searchInput) this.searchInput.value = '';
        if (this.searchClearBtn) this.searchClearBtn.style.display = 'none';
        this.filters.search = '';
        this.currentPage = 1;
        this.loadData();
      });

      // Reset & Refresh
      this.resetFiltersBtn?.addEventListener('click', () => this.resetFilters());
      this.refreshBtn?.addEventListener('click', () => {
        this.loadPoDropdown();
        this.loadStatistics();
        this.loadData();
      });

      // Page size change
      this.pageSizeSelect?.addEventListener('change', (e) => {
        this.pageSize = parseInt(e.target.value, 10) || 10;
        this.currentPage = 1;
        this.loadData();
      });

      // Pagination clicks
      this.btnPrevPage?.addEventListener('click', () => {
        if (this.currentPage > 1) {
          this.currentPage--;
          this.loadData();
        }
      });

      this.btnNextPage?.addEventListener('click', () => {
        if (this.currentPage < this.totalPages) {
          this.currentPage++;
          this.loadData();
        }
      });

      // Modal close handlers
      this.modalCloseBtn?.addEventListener('click', () => this.closePaymentModal());
      this.modalBackdrop?.addEventListener('click', () => this.closePaymentModal());
      document.addEventListener('keydown', (e) => {
        if (e.key === 'Escape' && this.paymentModal?.style.display === 'flex') {
          this.closePaymentModal();
        }
      });
    }

    updateUserBadge(user) {
      if (!user) return;
      const fullName = user.fullName || 'المستخدم';
      const role = window.ChatApp?.formatRoleArabic ? window.ChatApp.formatRoleArabic(user.role) : (user.role || 'محاسب مالي');
      const initials = fullName.split(' ').map(n => n[0]).join('').substring(0, 2).toUpperCase() || 'FS';

      if (this.userNameEl) this.userNameEl.textContent = fullName;
      if (this.userRoleEl) this.userRoleEl.textContent = role;
      if (this.userAvatarEl) this.userAvatarEl.textContent = initials;

      // Update Hub user pills as well if present
      const hubUserName = document.getElementById('finance-hub-user-name');
      const hubUserRole = document.getElementById('finance-hub-user-role');
      const hubUserAvatar = document.getElementById('finance-hub-user-avatar');
      if (hubUserName) hubUserName.textContent = fullName;
      if (hubUserRole) hubUserRole.textContent = role;
      if (hubUserAvatar) hubUserAvatar.textContent = initials;
    }

    async loadPoDropdown() {
      try {
        const res = await window.ChatApi.getFinancePos();
        if (res && res.success && Array.isArray(res.data)) {
          this.availablePos = res.data;
          if (this.mainPoSelect) {
            const currentVal = this.selectedPo || 'all';
            this.mainPoSelect.innerHTML = '<option value="all">-- جميع أوامر الشراء (كافة العقود) --</option>';
            this.availablePos.forEach(p => {
              const opt = document.createElement('option');
              opt.value = p.poNumber;
              const qtyFormatted = Number(p.quantity || 0).toLocaleString('en-US');
              const priceFormatted = Number(p.tonPrice || 0).toLocaleString('en-US', { minimumFractionDigits: 2, maximumFractionDigits: 2 });
              opt.textContent = `${p.poNumber} — ${p.supplier || 'مورد'} (${qtyFormatted} طن @ ${priceFormatted} ج.م)`;
              this.mainPoSelect.appendChild(opt);
            });
            this.mainPoSelect.value = currentVal;
          }
        }
      } catch (err) {
        console.warn('Could not load PO dropdown options:', err);
      }
    }

    async loadStatistics() {
      this.isLoadingStatistics = true;
      try {
        const poParam = (this.selectedPo && this.selectedPo !== 'all') ? this.selectedPo : '';
        const dateParam = this.selectedDate || '';
        const res = await window.ChatApi.getFinanceStatistics(dateParam, poParam);
        if (res && res.success && res.data) {
          this.statisticsData = res.data;
          this.renderStatistics(res.data);
        }
      } catch (err) {
        console.error('Failed to load finance statistics:', err);
      } finally {
        this.isLoadingStatistics = false;
      }
    }

    renderStatistics(stats) {
      if (!stats) return;

      const daily = stats.dailyActivity || {};
      const sum = stats.cumulativePoSummary || {};
      const perf = stats.factoryPerformance || {};

      // 1. Status Indicator Chip
      if (this.filterStatusText) {
        if (stats.filterPoNumber) {
          this.filterStatusText.textContent = `التاريخ: ${stats.filterDate} | أمر الشراء: ${stats.filterPoNumber}`;
        } else {
          const count = sum.totalPosCount || this.availablePos.length || 0;
          this.filterStatusText.textContent = `التاريخ: ${stats.filterDate} | كافة أوامر الشراء (${count} عقود)`;
        }
      }

      // 2. Factory Performance Highlight Card
      const perfRate = Number(perf.performancePercentage || 0);
      if (this.perfPercentage) {
        this.perfPercentage.textContent = perfRate.toFixed(2) + '%';
      }

      if (this.perfBadge && this.perfBadgeText) {
        this.perfBadge.className = 'finance-perf-badge';
        if (perfRate >= 95) {
          this.perfBadge.classList.add('perf-positive');
          this.perfBadgeText.textContent = 'أداء ممتاز';
        } else if (perfRate >= 75) {
          this.perfBadge.classList.add('medium');
          this.perfBadgeText.textContent = 'أداء متوسط / قيد التوريد';
        } else {
          this.perfBadge.classList.add('low');
          this.perfBadgeText.textContent = 'أداء منخفض / يتطلب متابعة';
        }
      }

      if (this.perfExpectedQty) this.perfExpectedQty.textContent = this.formatWeight(perf.expectedQuantity) + ' طن';
      if (this.perfExpectedPrice) this.perfExpectedPrice.textContent = this.formatCurrency(perf.expectedPrice);
      if (this.perfActualQty) this.perfActualQty.textContent = this.formatWeight(perf.actualDeliveredQuantity) + ' طن';
      if (this.perfActualPrice) this.perfActualPrice.textContent = this.formatCurrency(perf.actualDeliveredPrice);
      if (this.perfWasteQty) this.perfWasteQty.textContent = this.formatWeight(perf.totalWasteQuantity) + ' طن';
      if (this.perfWastePrice) this.perfWastePrice.textContent = this.formatCurrency(perf.totalWastePrice);
      if (this.perfRemainingQty) this.perfRemainingQty.textContent = this.formatWeight(perf.remainingQuantity) + ' طن';
      if (this.perfRemainingPrice) this.perfRemainingPrice.textContent = this.formatCurrency(perf.remainingPrice);

      // 3. Daily Activity Section
      if (this.dailyDateBadge) {
        this.dailyDateBadge.textContent = stats.filterDate || this.selectedDate;
      }
      if (this.dailyTransCount) {
        this.dailyTransCount.textContent = Number(daily.transportationTransactionsCount || 0).toLocaleString('ar-EG');
      }
      if (this.dailyTransNet) {
        this.dailyTransNet.textContent = this.formatWeight(daily.transportationNetWeight) + ' طن صافي';
      }
      if (this.dailyTransWaste) {
        this.dailyTransWaste.textContent = this.formatWeight(daily.transportationWasteQuantity) + ' طن';
      }
      if (this.dailyTransWastePrice) {
        this.dailyTransWastePrice.textContent = this.formatCurrency(daily.transportationWastePrice);
      }
      if (this.dailyStorageImportQty) {
        this.dailyStorageImportQty.textContent = this.formatWeight(daily.storageImportedQuantity) + ' طن';
      }
      if (this.dailyStorageImportPrice) {
        if (daily.storageImportedPrice !== null && daily.storageImportedPrice !== undefined) {
          this.dailyStorageImportPrice.textContent = this.formatCurrency(daily.storageImportedPrice);
        } else {
          this.dailyStorageImportPrice.textContent = 'غير مسعر';
        }
      }
      if (this.dailyStorageWaste) {
        this.dailyStorageWaste.textContent = this.formatWeight(daily.storageWasteQuantity) + ' طن';
      }
      if (this.dailyStorageWastePrice) {
        this.dailyStorageWastePrice.textContent = this.formatCurrency(daily.storageWastePrice);
      }
      if (this.dailyTotalWaste) {
        this.dailyTotalWaste.textContent = this.formatWeight(daily.totalWasteQuantity) + ' طن';
      }
      if (this.dailyTotalWastePrice) {
        this.dailyTotalWastePrice.textContent = this.formatCurrency(daily.totalWastePrice);
      }

      // 4. Cumulative PO Financial Summary Section
      if (this.summaryScopeBadge) {
        this.summaryScopeBadge.textContent = sum.scope === 'SinglePo' ? 'عقد محدد' : 'إجمالي كافة العقود';
      }
      if (this.summaryPoNumber) {
        this.summaryPoNumber.textContent = sum.poNumber || 'كافة العقود';
      }
      if (this.summarySupplier) {
        this.summarySupplier.textContent = sum.supplier || (sum.scope === 'AllPos' ? 'جميع الموردين المتعاقد معهم' : '-');
      }
      if (this.summaryDest) {
        this.summaryDest.textContent = sum.destination ? ('الوجهة: ' + sum.destination) : 'كافة الوجهات المعتمدة';
      }
      if (this.summaryTonPrice) {
        this.summaryTonPrice.textContent = sum.scope === 'SinglePo' ? this.formatCurrency(sum.tonPrice) : 'متنوع حسب العقد';
      }
      if (this.summaryMovementsCount) {
        const moves = sum.transportationTransactionsCount !== undefined ? sum.transportationTransactionsCount : (sum.transportationMovementsCount || 0);
        this.summaryMovementsCount.textContent = Number(moves).toLocaleString('ar-EG');
      }
      if (this.summaryPerfRate) {
        const pRate = sum.performancePercentage !== undefined ? sum.performancePercentage : (sum.factoryPerformancePercentage || 0);
        this.summaryPerfRate.textContent = Number(pRate).toFixed(2) + '%';
      }
      if (this.summaryExpectedQty) this.summaryExpectedQty.textContent = this.formatWeight(sum.expectedQuantity) + ' طن';
      if (this.summaryExpectedPrice) this.summaryExpectedPrice.textContent = 'السعر: ' + this.formatCurrency(sum.expectedPrice);
      if (this.summaryDeliveredQty) this.summaryDeliveredQty.textContent = this.formatWeight(sum.deliveredQuantity) + ' طن';
      if (this.summaryDeliveredPrice) this.summaryDeliveredPrice.textContent = 'القيمة: ' + this.formatCurrency(sum.deliveredPrice);
      if (this.summaryRemainingQty) this.summaryRemainingQty.textContent = this.formatWeight(sum.remainingQuantity) + ' طن';
      if (this.summaryRemainingPrice) this.summaryRemainingPrice.textContent = 'القيمة: ' + this.formatCurrency(sum.remainingPrice);
      if (this.summaryTotalWaste) this.summaryTotalWaste.textContent = this.formatWeight(sum.totalWasteQuantity) + ' طن';
      if (this.summaryTotalWastePrice) this.summaryTotalWastePrice.textContent = 'القيمة: ' + this.formatCurrency(sum.totalWastePrice);
      if (this.summaryTransWaste) this.summaryTransWaste.textContent = this.formatWeight(sum.transportationWasteQuantity) + ' طن';
      if (this.summaryTransWastePrice) this.summaryTransWastePrice.textContent = 'القيمة: ' + this.formatCurrency(sum.transportationWastePrice);
      if (this.summaryStorageWaste) this.summaryStorageWaste.textContent = this.formatWeight(sum.storageWasteQuantity) + ' طن';
      if (this.summaryStorageWastePrice) this.summaryStorageWastePrice.textContent = 'القيمة: ' + this.formatCurrency(sum.storageWastePrice);
      if (this.summaryPaidAmount) this.summaryPaidAmount.textContent = this.formatCurrency(sum.paidAmount);
      if (this.summaryOutstandingAmount) this.summaryOutstandingAmount.textContent = this.formatCurrency(sum.outstandingAmount);
    }

    highlightSelectedTableRow(po) {
      if (!this.tableBody) return;
      const rows = this.tableBody.querySelectorAll('tr[data-po]');
      rows.forEach(tr => {
        const rPo = tr.getAttribute('data-po');
        if (po && po !== 'all' && rPo === po) {
          tr.classList.add('selected-po-row');
        } else {
          tr.classList.remove('selected-po-row');
        }
      });
    }

    async loadData() {
      this.setLoadingState(true);
      try {
        const params = {
          search: this.filters.search,
          po: this.filters.po,
          supplier: this.filters.supplier,
          paymentStatus: this.filters.paymentStatus,
          invoiceNumber: this.filters.invoiceNumber,
          dateFrom: this.filters.dateFrom,
          dateTo: this.filters.dateTo,
          page: this.currentPage,
          pageSize: this.pageSize
        };

        const res = await window.ChatApi.getFinanceManagementData(params);
        if (res && res.success && res.data) {
          const data = res.data;
          this.currentRecords = data.records || [];
          this.totalRecords = data.totalCount || 0;
          this.currentPage = data.page || 1;
          this.totalPages = data.totalPages || 1;

          this.updateFilterDropdowns(data.filterOptions);
          this.renderTable(this.currentRecords);
          this.renderPagination();
          this.setEmptyState(this.currentRecords.length === 0);
          this.setErrorState(false);
        } else {
          this.setErrorState(true, res?.message || 'فشل في استرجاع البيانات المالية');
        }
      } catch (err) {
        console.error('Error loading finance data:', err);
        this.setErrorState(true, 'حدث خطأ في الاتصال بالخادم.');
      } finally {
        this.setLoadingState(false);
      }
    }

    updateFilterDropdowns(opts) {
      if (!opts) return;
      if (this.filterSupplierSelect && opts.suppliers) {
        const currentVal = this.filterSupplierSelect.value;
        const currentOptions = Array.from(this.filterSupplierSelect.options).map(o => o.value);
        opts.suppliers.forEach(s => {
          if (!currentOptions.includes(s)) {
            const opt = document.createElement('option');
            opt.value = s;
            opt.textContent = s;
            this.filterSupplierSelect.appendChild(opt);
          }
        });
        if (currentVal) this.filterSupplierSelect.value = currentVal;
      }
    }

    renderTable(records) {
      if (!this.tableBody) return;
      this.tableBody.innerHTML = '';

      records.forEach((r, idx) => {
        const tr = document.createElement('tr');
        tr.className = idx % 2 === 0 ? 'finance-row-even' : 'finance-row-odd';
        tr.setAttribute('data-po', r.poNumber || '');

        if (this.selectedPo && this.selectedPo !== 'all' && r.poNumber === this.selectedPo) {
          tr.classList.add('selected-po-row');
        }

        // Payment status badge class & label
        let badgeClass = 'badge-unpaid';
        if (r.paymentStatus === 'مدفوع بالكامل') {
          badgeClass = 'badge-paid';
        } else if (r.paymentStatus === 'مدفوع جزئياً') {
          badgeClass = 'badge-partial';
        }

        // Paid amount display: clickable badge/button to open CEO payment details
        const paidFormatted = this.formatCurrency(r.paidAmount);
        const paidHtml = r.paidAmount > 0 
          ? `<span class="finance-paid-link" data-po="${this.escapeHtml(r.poNumber)}" title="عرض تفاصيل الدفعات">${paidFormatted} <svg width="12" height="12" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.5"><path d="M18 13v6a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2V8a2 2 0 0 1 2-2h6"/><polyline points="15 3 21 3 21 9"/><line x1="10" y1="14" x2="21" y2="3"/></svg></span>`
          : `<span>0.00 ج.م</span>`;

        tr.innerHTML = `
          <!-- 1. PO -->
          <td class="cell-po">
            <span class="finance-po-badge">${this.escapeHtml(r.poNumber || '-')}</span>
          </td>

          <!-- 2. المورد -->
          <td class="cell-supplier font-medium">
            ${this.escapeHtml(r.supplier || '-')}
          </td>

          <!-- 3. تاريخ أمر الشراء -->
          <td class="cell-date font-numeric" dir="ltr">
            ${this.formatDate(r.orderDate)}
          </td>

          <!-- 4. كمية أمر الشراء (الكمية المتوقعة) -->
          <td class="cell-qty font-numeric font-semibold">
            ${this.formatWeight(r.poQuantity)} طن
          </td>

          <!-- 5. قيمة أمر الشراء (القيمة المتوقعة) -->
          <td class="cell-val font-numeric font-semibold color-primary">
            ${this.formatCurrency(r.poValue)}
          </td>

          <!-- 6. سعر الطن -->
          <td class="cell-price font-numeric">
            ${this.formatCurrency(r.tonPrice)}
          </td>

          <!-- 7. الكمية التي تم توريدها (المسلمة) -->
          <td class="cell-supplied-qty font-numeric font-semibold color-info">
            ${this.formatWeight(r.suppliedQuantity)} طن
          </td>

          <!-- 8. قيمة الكمية التي تم توريدها -->
          <td class="cell-supplied-val font-numeric font-semibold">
            ${this.formatCurrency(r.suppliedValue)}
          </td>

          <!-- 9. الكمية المتبقية -->
          <td class="cell-remaining-qty font-numeric font-semibold ${r.remainingQuantity > 0 ? 'color-warning' : 'color-muted'}">
            ${this.formatWeight(r.remainingQuantity)} طن
          </td>

          <!-- 10. القيمة المتبقية -->
          <td class="cell-remaining-val font-numeric ${r.remainingValue > 0 ? 'color-warning' : 'color-muted'}">
            ${this.formatCurrency(r.remainingValue)}
          </td>

          <!-- 11. عدد النقلات -->
          <td class="cell-movements font-numeric">
            <span class="finance-count-badge">${Number(r.movementsCount || 0).toLocaleString('ar-EG')} نقلة</span>
          </td>

          <!-- 12. الكمية المعتمدة -->
          <td class="cell-approved font-numeric font-semibold color-success">
            ${this.formatWeight(r.approvedQuantity)} طن
          </td>

          <!-- 13. الكمية الهالك / الغير معتمدة -->
          <td class="cell-unapproved font-numeric ${r.unapprovedQuantity > 0 ? 'color-danger font-semibold' : 'color-muted'}">
            ${this.formatWeight(r.unapprovedQuantity)} طن
          </td>

          <!-- 14. المبلغ المدفوع -->
          <td class="cell-paid font-numeric font-semibold color-success">
            ${paidHtml}
          </td>

          <!-- 15. المبلغ المستحق -->
          <td class="cell-outstanding font-numeric font-semibold ${r.outstandingAmount > 0 ? 'color-danger' : 'color-success'}">
            ${this.formatCurrency(r.outstandingAmount)}
          </td>

          <!-- 16. حالة السداد -->
          <td class="cell-status">
            <span class="finance-status-badge ${badgeClass}">${this.escapeHtml(r.paymentStatus)}</span>
          </td>

          <!-- 17. رقم الفاتورة -->
          <td class="cell-invoice font-numeric">
            ${r.invoiceNumber ? `<span class="finance-invoice-chip" dir="ltr">${this.escapeHtml(r.invoiceNumber)}</span>` : '<span class="color-muted">غير محدد</span>'}
          </td>
        `;

        // Click row to filter statistics for this PO
        tr.addEventListener('click', (e) => {
          if (e.target.closest('.finance-paid-link')) return;
          const poNum = tr.getAttribute('data-po');
          if (!poNum || poNum === '-') return;

          if (this.selectedPo === poNum) {
            // Toggle off / reset to all
            this.selectedPo = 'all';
            if (this.mainPoSelect) this.mainPoSelect.value = 'all';
            this.filters.po = '';
          } else {
            this.selectedPo = poNum;
            if (this.mainPoSelect) this.mainPoSelect.value = poNum;
            this.filters.po = poNum;
          }
          if (this.filterPoInput) this.filterPoInput.value = this.filters.po;
          this.highlightSelectedTableRow(this.selectedPo);
          this.loadStatistics();
        });

        // Attach click listener for payment details link
        const paidLink = tr.querySelector('.finance-paid-link');
        if (paidLink) {
          paidLink.addEventListener('click', (e) => {
            e.preventDefault();
            e.stopPropagation();
            const po = paidLink.getAttribute('data-po');
            if (po) this.showPaymentDetailsModal(po);
          });
        }

        this.tableBody.appendChild(tr);
      });
    }

    renderPagination() {
      if (this.paginationInfo) {
        const from = this.totalRecords === 0 ? 0 : (this.currentPage - 1) * this.pageSize + 1;
        const to = Math.min(this.currentPage * this.pageSize, this.totalRecords);
        this.paginationInfo.textContent = `عرض ${from} إلى ${to} من إجمالي ${this.totalRecords} أمر شراء`;
      }

      if (this.btnPrevPage) {
        this.btnPrevPage.disabled = this.currentPage <= 1;
      }
      if (this.btnNextPage) {
        this.btnNextPage.disabled = this.currentPage >= this.totalPages;
      }

      if (this.pageNumbersContainer) {
        this.pageNumbersContainer.innerHTML = '';
        const maxVisible = 5;
        let startPage = Math.max(1, this.currentPage - Math.floor(maxVisible / 2));
        let endPage = Math.min(this.totalPages, startPage + maxVisible - 1);

        if (endPage - startPage + 1 < maxVisible) {
          startPage = Math.max(1, endPage - maxVisible + 1);
        }

        for (let i = startPage; i <= endPage; i++) {
          const btn = document.createElement('button');
          btn.type = 'button';
          btn.className = `finance-page-btn ${i === this.currentPage ? 'active' : ''}`;
          btn.textContent = Number(i).toLocaleString('ar-EG');
          btn.addEventListener('click', () => {
            if (i !== this.currentPage) {
              this.currentPage = i;
              this.loadData();
            }
          });
          this.pageNumbersContainer.appendChild(btn);
        }
      }
    }

    async showPaymentDetailsModal(poNumber) {
      if (!this.paymentModal) return;
      if (this.modalPoNumber) this.modalPoNumber.textContent = poNumber;
      if (this.modalPaymentsList) {
        this.modalPaymentsList.innerHTML = '<div class="finance-modal-loading">جاري تحميل سجل الدفعات...</div>';
      }

      this.paymentModal.style.display = 'flex';

      try {
        const res = await window.ChatApi.getFinancePayments(poNumber);
        if (res && res.success && Array.isArray(res.data) && res.data.length > 0) {
          let html = `
            <div class="finance-payments-table-wrap">
              <table class="finance-payments-subtable">
                <thead>
                  <tr>
                    <th>تاريخ الدفعة</th>
                    <th>المبلغ المدفوع</th>
                    <th>طريقة السداد</th>
                    <th>رقم العملية / الشيك</th>
                    <th>المعتمد من</th>
                    <th>ملاحظات</th>
                  </tr>
                </thead>
                <tbody>
          `;
          res.data.forEach(p => {
            html += `
              <tr>
                <td dir="ltr" class="font-numeric">${this.formatDate(p.paymentDate)}</td>
                <td class="font-numeric font-semibold color-success">${this.formatCurrency(p.amount)}</td>
                <td>${this.escapeHtml(p.paymentMethod || 'تحويل بنكي')}</td>
                <td class="font-numeric" dir="ltr">${this.escapeHtml(p.referenceNumber || '-')}</td>
                <td><span class="ceo-badge">${this.escapeHtml(p.paidBy || 'مكتب الرئيس التنفيذي')}</span></td>
                <td>${this.escapeHtml(p.notes || '-')}</td>
              </tr>
            `;
          });
          html += '</tbody></table></div>';
          this.modalPaymentsList.innerHTML = html;
        } else {
          this.modalPaymentsList.innerHTML = `
            <div class="finance-modal-empty">
              <p>لا توجد دفعات مالية مسجلة بعد لهذا الأمر.</p>
              <span class="color-muted font-sm">يتم تسجيل الدفعات واعتمادها من خلال لوحة تحكم الرئيس التنفيذي.</span>
            </div>
          `;
        }
      } catch (e) {
        this.modalPaymentsList.innerHTML = '<div class="finance-modal-error">حدث خطأ أثناء تحميل الدفعات.</div>';
      }
    }

    closePaymentModal() {
      if (this.paymentModal) {
        this.paymentModal.style.display = 'none';
      }
    }

    resetFilters() {
      this.initDefaultDate();
      this.selectedPo = 'all';
      if (this.mainPoSelect) this.mainPoSelect.value = 'all';

      this.filters = {
        search: '',
        po: '',
        supplier: '',
        paymentStatus: 'all',
        invoiceNumber: '',
        dateFrom: '',
        dateTo: ''
      };

      if (this.searchInput) this.searchInput.value = '';
      if (this.searchClearBtn) this.searchClearBtn.style.display = 'none';
      if (this.filterPoInput) this.filterPoInput.value = '';
      if (this.filterSupplierSelect) this.filterSupplierSelect.value = 'all';
      if (this.filterPaymentStatusSelect) this.filterPaymentStatusSelect.value = 'all';
      if (this.filterInvoiceInput) this.filterInvoiceInput.value = '';
      if (this.filterDateFrom) this.filterDateFrom.value = '';
      if (this.filterDateTo) this.filterDateTo.value = '';

      this.highlightSelectedTableRow('all');
      this.currentPage = 1;
      this.loadStatistics();
      this.loadData();
    }

    setLoadingState(loading) {
      this.isLoading = loading;
      if (this.loadingEl) this.loadingEl.style.display = loading ? 'flex' : 'none';
      if (loading) {
        if (this.emptyEl) this.emptyEl.style.display = 'none';
        if (this.errorEl) this.errorEl.style.display = 'none';
      }
    }

    setEmptyState(empty) {
      if (this.emptyEl) this.emptyEl.style.display = empty && !this.isLoading ? 'flex' : 'none';
    }

    setErrorState(show, msg = '') {
      if (this.errorEl) {
        this.errorEl.style.display = show ? 'flex' : 'none';
        const msgEl = this.errorEl.querySelector('.finance-error-msg');
        if (msgEl && msg) msgEl.textContent = msg;
      }
    }

    formatCurrency(val) {
      const num = Number(val) || 0;
      return num.toLocaleString('en-US', {
        minimumFractionDigits: 2,
        maximumFractionDigits: 2
      }) + ' ج.م';
    }

    formatWeight(val) {
      const num = Number(val) || 0;
      return num.toLocaleString('en-US', {
        minimumFractionDigits: 3,
        maximumFractionDigits: 3
      });
    }

    formatDate(dateStr) {
      if (!dateStr) return '-';
      try {
        const d = new Date(dateStr);
        if (isNaN(d.getTime())) return dateStr;
        const year = d.getFullYear();
        const month = String(d.getMonth() + 1).padStart(2, '0');
        const day = String(d.getDate()).padStart(2, '0');
        return `${year}-${month}-${day}`;
      } catch {
        return dateStr;
      }
    }

    escapeHtml(str) {
      if (str === null || str === undefined) return '';
      return String(str)
        .replace(/&/g, '&amp;')
        .replace(/</g, '&lt;')
        .replace(/>/g, '&gt;')
        .replace(/"/g, '&quot;')
        .replace(/'/g, '&#039;');
    }
  }

  // Register instance globally
  window.FinanceManagementApp = new FinanceManagementApp();
})();
