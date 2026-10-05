/**
 * FaceStone Web - Finance Management & Statistics Application
 * Professional Arabic RTL Financial Dashboard with Live Database Aggregation,
 * Date-Range Filtering, PO History & Details, and Transparent Performance Mathematics
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

      // Primary Date-Range Filter State (Default to Today)
      this.fromDate = '';
      this.toDate = '';
      this.selectedPo = 'all';
      this.availablePos = [];
      this.statisticsData = null;
      this.currentUser = null;
      this.isFinanceDirector = false;

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

      // Active Subview ('dashboard' | 'po-details' | 'performance')
      this.currentView = 'dashboard';
      this.currentPoNumber = null;

      // Performance View Range
      this.perfFromDate = '';
      this.perfToDate = '';
    }

    init() {
      if (this.initialized) return;
      this.initialized = true;

      this.bindDOMElements();
      this.initDefaultDates();
      this.bindEvents();
      this.bindNewHubAndSubviewsEvents();
      this.loadPoDropdown();
      this.loadStatistics();
      this.loadPurchaseOrdersHistory();
      this.loadData();
    }

    initDefaultDates() {
      // Default behavior: fromDate = today's date, toDate = today's date
      const today = new Date();
      const y = today.getFullYear();
      const m = String(today.getMonth() + 1).padStart(2, '0');
      const d = String(today.getDate()).padStart(2, '0');
      const todayStr = `${y}-${m}-${d}`;

      this.fromDate = todayStr;
      this.toDate = todayStr;

      if (this.filterDateFrom) this.filterDateFrom.value = this.fromDate;
      if (this.filterDateTo) this.filterDateTo.value = this.toDate;

      this.perfFromDate = this.fromDate;
      this.perfToDate = this.toDate;
      if (this.perfDateFrom) this.perfDateFrom.value = this.perfFromDate;
      if (this.perfDateTo) this.perfDateTo.value = this.perfToDate;
    }

    bindDOMElements() {
      // Root & Subviews
      this.root = document.getElementById('finance-management-page-root');
      this.hubCardsView = document.getElementById('finance-hub-cards-view');
      this.historyView = document.getElementById('finance-history-view');
      this.suppliersView = document.getElementById('finance-suppliers-view');
      this.supplierDetailsView = document.getElementById('finance-supplier-details-view');
      this.posView = document.getElementById('finance-pos-view');
      this.bankView = document.getElementById('finance-bank-view');
      this.expensesView = document.getElementById('finance-expenses-view');
      this.employeesView = document.getElementById('finance-employees-view');
      this.employeeDetailsView = document.getElementById('finance-employee-details-view');
      this.dashboardView = document.getElementById('finance-dashboard-view');
      this.poDetailsView = document.getElementById('finance-po-details-view');
      this.performanceView = document.getElementById('finance-performance-view');

      // User Profile Header
      this.userNameEl = document.getElementById('finance-mgmt-user-name');
      this.userRoleEl = document.getElementById('finance-mgmt-user-role');
      this.userAvatarEl = document.getElementById('finance-mgmt-user-avatar');

      // 1. Date Range & Search Filters
      this.filterDateFrom = document.getElementById('finance-filter-date-from');
      this.filterDateTo = document.getElementById('finance-filter-date-to');
      this.btnApplyFilters = document.getElementById('finance-btn-apply-filters');
      this.mainPoSelect = document.getElementById('finance-filter-main-po');
      this.btnClearPo = document.getElementById('finance-btn-clear-po');
      this.resetFiltersBtn = document.getElementById('finance-reset-filters-btn');
      this.refreshBtn = document.getElementById('finance-refresh-btn');

      this.searchInput = document.getElementById('finance-search-input');
      this.searchClearBtn = document.getElementById('finance-search-clear-btn');
      this.filterSupplierSelect = document.getElementById('finance-filter-supplier');
      this.filterPaymentStatusSelect = document.getElementById('finance-filter-payment-status');
      this.filterInvoiceInput = document.getElementById('finance-filter-invoice');

      // 2. Daily / Range Activity Cards
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

      // 3. PO Historical Summary Table
      this.posCountBadge = document.getElementById('finance-pos-count-badge');
      this.posHistoryTable = document.getElementById('finance-pos-history-table');
      this.posHistoryBody = document.getElementById('finance-pos-history-body');

      // 4. Factory Performance Card
      this.btnPerfBannerDetails = document.getElementById('finance-btn-perf-banner-details');
      this.btnPerfDetails = document.getElementById('finance-btn-perf-details');
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

      // 5. Detailed Transactions Table
      this.tableBody = document.getElementById('finance-table-body');
      this.tableContainer = document.getElementById('finance-table-scroll');
      this.loadingEl = document.getElementById('finance-table-loading');
      this.emptyEl = document.getElementById('finance-table-empty');
      this.errorEl = document.getElementById('finance-table-error');

      this.paginationInfo = document.getElementById('finance-pagination-info');
      this.pageSizeSelect = document.getElementById('finance-page-size-select');
      this.btnPrevPage = document.getElementById('finance-btn-prev-page');
      this.btnNextPage = document.getElementById('finance-btn-next-page');
      this.pageNumbersContainer = document.getElementById('finance-page-numbers');

      // 6. Dedicated PO Details View Elements
      this.poDetailsBackBtn = document.getElementById('finance-po-details-back-btn');
      this.poDetailTitlePo = document.getElementById('po-detail-title-po-number');
      this.poDetailBadgePo = document.getElementById('po-detail-po-number');
      this.poDetailCompany = document.getElementById('po-detail-company');
      this.poDetailDestination = document.getElementById('po-detail-destination');
      this.poDetailMaterial = document.getElementById('po-detail-material');
      this.poDetailOrderDate = document.getElementById('po-detail-order-date');
      this.poDetailStatus = document.getElementById('po-detail-status');
      this.poDetailPaymentStatus = document.getElementById('po-detail-payment-status');

      this.poDetailExpectedQty = document.getElementById('po-detail-expected-qty');
      this.poDetailExpectedVal = document.getElementById('po-detail-expected-val');
      this.poDetailDeliveredQty = document.getElementById('po-detail-delivered-qty');
      this.poDetailDeliveredVal = document.getElementById('po-detail-delivered-val');
      this.poDetailRemainingQty = document.getElementById('po-detail-remaining-qty');
      this.poDetailRemainingVal = document.getElementById('po-detail-remaining-val');
      this.poDetailTotalWaste = document.getElementById('po-detail-total-waste');
      this.poDetailWasteVal = document.getElementById('po-detail-waste-val');
      this.poDetailTransWaste = document.getElementById('po-detail-trans-waste');
      this.poDetailTransMoves = document.getElementById('po-detail-trans-moves');
      this.poDetailStorageWaste = document.getElementById('po-detail-storage-waste');
      this.poDetailStorageQty = document.getElementById('po-detail-storage-qty');
      this.poDetailTonPrice = document.getElementById('po-detail-ton-price');
      this.poDetailPerfPct = document.getElementById('po-detail-perf-pct');

      // Payment controls on PO Details View
      this.poDetailPaidAmt = document.getElementById('po-detail-paid-amt');
      this.poDetailOutstandingAmt = document.getElementById('po-detail-outstanding-amt');
      this.poDetailCurrentPaymentBadge = document.getElementById('po-detail-current-payment-badge');
      this.poDetailDirectorControls = document.getElementById('po-detail-director-controls');
      this.poDetailPaymentSelect = document.getElementById('po-detail-payment-select');
      this.poDetailPaymentNotes = document.getElementById('po-detail-payment-notes');
      this.poDetailBtnSavePayment = document.getElementById('po-detail-btn-save-payment');
      this.poDetailReadonlyControls = document.getElementById('po-detail-readonly-controls');
      this.poDetailPaymentAlert = document.getElementById('po-detail-payment-alert');
      this.poDetailAuditTbody = document.getElementById('po-detail-audit-tbody');

      this.poDetailTimelineList = document.getElementById('po-detail-timeline-list');
      this.poDetailTransCountBadge = document.getElementById('po-detail-trans-count-badge');
      this.poDetailTransTbody = document.getElementById('po-detail-trans-tbody');
      this.poDetailStorageCountBadge = document.getElementById('po-detail-storage-count-badge');
      this.poDetailStorageTbody = document.getElementById('po-detail-storage-tbody');

      // 7. Dedicated Performance Details View Elements
      this.perfBackBtn = document.getElementById('finance-perf-back-btn');
      this.perfDateFrom = document.getElementById('perf-date-from');
      this.perfDateTo = document.getElementById('perf-date-to');
      this.perfBtnApplyDate = document.getElementById('perf-btn-apply-date');
      this.perfRangeLabel = document.getElementById('perf-range-label');

      this.perfDetailPercentage = document.getElementById('perf-detail-percentage');
      this.perfDetailBadge = document.getElementById('perf-detail-badge');
      this.perfDetailBadgeText = document.getElementById('perf-detail-badge-text');
      this.perfDetailFormulaSub = document.getElementById('perf-detail-formula-sub');
      this.perfDetailExpectedQty = document.getElementById('perf-detail-expected-qty');
      this.perfDetailExpectedPrice = document.getElementById('perf-detail-expected-price');
      this.perfDetailActualQty = document.getElementById('perf-detail-actual-qty');
      this.perfDetailActualPrice = document.getElementById('perf-detail-actual-price');
      this.perfDetailTotalWaste = document.getElementById('perf-detail-total-waste');
      this.perfDetailWastePrice = document.getElementById('perf-detail-waste-price');
      this.perfDetailRemainingQty = document.getElementById('perf-detail-remaining-qty');
      this.perfDetailRemainingPrice = document.getElementById('perf-detail-remaining-price');

      this.perfCalcQtySteps = document.getElementById('perf-calc-qty-steps');
      this.perfCalcFinSteps = document.getElementById('perf-calc-fin-steps');
      this.perfCalcWasteSteps = document.getElementById('perf-calc-waste-steps');
      this.perfCalcComparisonSteps = document.getElementById('perf-calc-comparison-steps');
      this.perfPosTbody = document.getElementById('perf-pos-tbody');

      // Legacy Payments Modal (CEO modal)
      this.paymentModal = document.getElementById('finance-payment-modal');
      this.modalPoNumber = document.getElementById('finance-modal-po-number');
      this.modalPaymentsList = document.getElementById('finance-modal-payments-list');
      this.modalCloseBtn = document.getElementById('finance-modal-close-btn');
      this.modalBackdrop = document.getElementById('finance-modal-backdrop');
    }

    bindEvents() {
      // Apply filters button
      this.btnApplyFilters?.addEventListener('click', () => {
        this.applyDateRangeFilter();
      });

      // Date changes
      this.filterDateFrom?.addEventListener('change', () => {
        this.fromDate = this.filterDateFrom.value || '';
      });

      this.filterDateTo?.addEventListener('change', () => {
        this.toDate = this.filterDateTo.value || '';
      });

      // PO Select
      this.mainPoSelect?.addEventListener('change', (e) => {
        this.selectedPo = e.target.value;
        this.filters.po = (this.selectedPo === 'all' ? '' : this.selectedPo);
        this.loadStatistics();
        this.currentPage = 1;
        this.loadData();
      });

      // Clear PO
      this.btnClearPo?.addEventListener('click', () => {
        this.selectedPo = 'all';
        if (this.mainPoSelect) this.mainPoSelect.value = 'all';
        this.filters.po = '';
        this.loadStatistics();
        this.currentPage = 1;
        this.loadData();
      });

      // Reset
      this.resetFiltersBtn?.addEventListener('click', () => this.resetFilters());

      // Refresh
      this.refreshBtn?.addEventListener('click', () => {
        this.loadPoDropdown();
        this.loadStatistics();
        this.loadPurchaseOrdersHistory();
        this.loadData();
      });

      // View details buttons on Performance Card
      this.btnPerfBannerDetails?.addEventListener('click', () => {
        this.navigateTo('/finance/performance');
      });

      this.btnPerfDetails?.addEventListener('click', () => {
        this.navigateTo('/finance/performance');
      });

      // Back buttons on subviews
      this.poDetailsBackBtn?.addEventListener('click', () => {
        this.navigateTo('/finance');
      });

      this.perfBackBtn?.addEventListener('click', () => {
        this.navigateTo('/finance');
      });

      // Save Payment Status button (Finance Director)
      this.poDetailBtnSavePayment?.addEventListener('click', () => {
        this.handleSavePaymentStatus();
      });

      // Performance View Date Range Apply
      this.perfBtnApplyDate?.addEventListener('click', () => {
        this.perfFromDate = this.perfDateFrom?.value || '';
        this.perfToDate = this.perfDateTo?.value || '';
        this.loadPerformanceDetails();
      });

      // Quick search in table
      let searchTimeout = null;
      this.searchInput?.addEventListener('input', (e) => {
        const val = e.target.value.trim();
        if (this.searchClearBtn) this.searchClearBtn.style.display = val ? 'flex' : 'none';
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

      // Pagination
      this.pageSizeSelect?.addEventListener('change', (e) => {
        this.pageSize = parseInt(e.target.value, 10) || 10;
        this.currentPage = 1;
        this.loadData();
      });

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

    applyDateRangeFilter() {
      this.fromDate = this.filterDateFrom?.value || '';
      this.toDate = this.filterDateTo?.value || '';
      this.filters.dateFrom = this.fromDate;
      this.filters.dateTo = this.toDate;

      this.loadStatistics();
      this.currentPage = 1;
      this.loadData();
    }

    updateUserBadge(user) {
      if (!user) return;
      this.currentUser = user;
      const fullName = user.fullName || 'المستخدم';
      const role = window.ChatApp?.formatRoleArabic ? window.ChatApp.formatRoleArabic(user.role) : (user.role || 'محاسب مالي');
      const initials = fullName.split(' ').map(n => n[0]).join('').substring(0, 2).toUpperCase() || 'FS';

      if (this.userNameEl) this.userNameEl.textContent = fullName;
      if (this.userRoleEl) this.userRoleEl.textContent = role;
      if (this.userAvatarEl) this.userAvatarEl.textContent = initials;

      const normRole = (user.role || '').toUpperCase();
      this.isFinanceDirector = [
        'FINANCE_DIRECTOR', 'FINANCE DIRECTOR', 'DIRECTOR_FINANCE', 'FINANCE_MANAGER',
        'FINANCE MANAGER', 'CFO', 'CEO', 'IT'
      ].includes(normRole);

      const addBankBtn = document.getElementById('finance-btn-add-bank-trans');
      if (addBankBtn) addBankBtn.style.display = this.isFinanceDirector ? 'inline-flex' : 'none';
      const addExpBtn = document.getElementById('finance-btn-add-expense');
      if (addExpBtn) addExpBtn.style.display = this.isFinanceDirector ? 'inline-flex' : 'none';
    }

    /* ------------------------------------------------------------------------
       Routing & View Switching (7 Finance Sections & Main Hub)
       ------------------------------------------------------------------------ */
    navigateTo(urlPath) {
      if (window.location.pathname !== urlPath) {
        history.pushState({ view: 'finance-management', subRoute: urlPath }, '', urlPath);
      }
      this.handleRoute(urlPath);
    }

    hideAllSubViews() {
      const views = [
        this.hubCardsView,
        this.historyView,
        this.suppliersView,
        this.supplierDetailsView,
        this.posView,
        this.poDetailsView,
        this.performanceView,
        this.bankView,
        this.expensesView,
        this.employeesView,
        this.employeeDetailsView,
        this.dashboardView
      ];
      views.forEach(v => {
        if (v) v.style.display = 'none';
      });
    }

    handleRoute(path) {
      const cleanPath = (path || window.location.pathname || '').trim().toLowerCase();
      if (cleanPath === '/finance/history' || cleanPath.endsWith('/history')) {
        this.showHistoryView();
      } else if (cleanPath.startsWith('/finance/suppliers/')) {
        const supId = decodeURIComponent(cleanPath.replace('/finance/suppliers/', '').trim());
        if (supId) {
          this.showSupplierDetailsView(supId);
        } else {
          this.showSuppliersView();
        }
      } else if (cleanPath === '/finance/suppliers' || cleanPath.endsWith('/suppliers')) {
        this.showSuppliersView();
      } else if (cleanPath.startsWith('/finance/purchase-orders/')) {
        const poNum = decodeURIComponent(cleanPath.replace('/finance/purchase-orders/', '').trim());
        if (poNum) {
          this.showPoDetailsView(poNum);
        } else {
          this.showPurchaseOrdersView();
        }
      } else if (cleanPath === '/finance/purchase-orders' || cleanPath.endsWith('/purchase-orders')) {
        this.showPurchaseOrdersView();
      } else if (cleanPath === '/finance/performance' || cleanPath.endsWith('/performance')) {
        this.showPerformanceView();
      } else if (cleanPath === '/finance/bank' || cleanPath.endsWith('/bank')) {
        this.showBankView();
      } else if (cleanPath === '/finance/expenses' || cleanPath.endsWith('/expenses')) {
        this.showExpensesView();
      } else if (cleanPath.startsWith('/finance/employees/')) {
        const empId = decodeURIComponent(cleanPath.replace('/finance/employees/', '').trim());
        if (empId) {
          this.showEmployeeDetailsView(empId);
        } else {
          this.showEmployeesView();
        }
      } else if (cleanPath === '/finance/employees' || cleanPath.endsWith('/employees')) {
        this.showEmployeesView();
      } else {
        this.showHubCardsView();
      }
    }

    showHubCardsView() {
      this.currentView = 'hub';
      this.hideAllSubViews();
      if (this.hubCardsView) this.hubCardsView.style.display = 'block';
      window.scrollTo({ top: 0, behavior: 'smooth' });
    }

    showDashboardView() {
      this.showHubCardsView();
    }

    showHistoryView() {
      this.currentView = 'history';
      this.hideAllSubViews();
      if (this.historyView) this.historyView.style.display = 'block';
      window.scrollTo({ top: 0, behavior: 'smooth' });
      this.loadHistoryData();
    }

    showSuppliersView() {
      this.currentView = 'suppliers';
      this.hideAllSubViews();
      if (this.suppliersView) this.suppliersView.style.display = 'block';
      window.scrollTo({ top: 0, behavior: 'smooth' });
      this.loadSuppliersData();
    }

    showSupplierDetailsView(supplierId) {
      this.currentView = 'supplier-detail';
      this.hideAllSubViews();
      if (this.supplierDetailsView) this.supplierDetailsView.style.display = 'block';
      window.scrollTo({ top: 0, behavior: 'smooth' });
      this.loadSupplierDetail(supplierId);
    }

    showPurchaseOrdersView() {
      this.currentView = 'purchase-orders';
      this.hideAllSubViews();
      if (this.posView) this.posView.style.display = 'block';
      window.scrollTo({ top: 0, behavior: 'smooth' });
      this.loadAllPurchaseOrdersData();
    }

    showPoDetailsView(poNumber) {
      this.currentView = 'po-details';
      this.currentPoNumber = poNumber;
      this.hideAllSubViews();
      if (this.poDetailsView) this.poDetailsView.style.display = 'block';
      window.scrollTo({ top: 0, behavior: 'smooth' });
      this.loadPoDetails(poNumber);
    }

    showPerformanceView() {
      this.currentView = 'performance';
      this.hideAllSubViews();
      if (this.performanceView) this.performanceView.style.display = 'block';
      window.scrollTo({ top: 0, behavior: 'smooth' });

      this.perfFromDate = this.fromDate;
      this.perfToDate = this.toDate;
      if (this.perfDateFrom) this.perfDateFrom.value = this.perfFromDate;
      if (this.perfDateTo) this.perfDateTo.value = this.perfToDate;

      this.loadPerformanceDetails();
    }

    showBankView() {
      this.currentView = 'bank';
      this.hideAllSubViews();
      if (this.bankView) this.bankView.style.display = 'block';
      window.scrollTo({ top: 0, behavior: 'smooth' });
      this.loadBankData();
    }

    showExpensesView() {
      this.currentView = 'expenses';
      this.hideAllSubViews();
      if (this.expensesView) this.expensesView.style.display = 'block';
      window.scrollTo({ top: 0, behavior: 'smooth' });
      this.loadExpensesData();
    }

    showEmployeesView() {
      this.currentView = 'employees';
      this.hideAllSubViews();
      if (this.employeesView) this.employeesView.style.display = 'block';
      window.scrollTo({ top: 0, behavior: 'smooth' });
      this.loadEmployeesData();
    }

    showEmployeeDetailsView(employeeId) {
      this.currentView = 'employee-detail';
      this.hideAllSubViews();
      if (this.employeeDetailsView) this.employeeDetailsView.style.display = 'block';
      window.scrollTo({ top: 0, behavior: 'smooth' });
      this.loadEmployeeDetail(employeeId);
    }

    /* ------------------------------------------------------------------------
       1. Dropdowns & Statistics Aggregation
       ------------------------------------------------------------------------ */
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
        const res = await window.ChatApi.getFinanceStatistics(this.fromDate, this.toDate, poParam);
        if (res && res.success && res.data) {
          this.statisticsData = res.data;
          if (res.data.isCurrentUserFinanceDirector !== undefined) {
            this.isFinanceDirector = res.data.isCurrentUserFinanceDirector;
          }
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
      const perf = stats.factoryPerformance || {};

      // 1. Daily/Range Badge
      if (this.dailyDateBadge) {
        if (stats.filterFromDate === stats.filterToDate) {
          this.dailyDateBadge.textContent = stats.filterFromDate || this.fromDate || 'اليوم';
        } else {
          this.dailyDateBadge.textContent = `من: ${stats.filterFromDate} إلى: ${stats.filterToDate}`;
        }
      }

      // 2. Daily / Range Activity Cards (Live Aggregated Real DB Data)
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

      // 3. Factory Performance Highlight Card
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
    }

    /* ------------------------------------------------------------------------
       2. PO Historical List (Requirements 7 & 8)
       ------------------------------------------------------------------------ */
    async loadPurchaseOrdersHistory() {
      try {
        const res = await window.ChatApi.getFinancePurchaseOrders();
        if (res && res.success && Array.isArray(res.data)) {
          this.renderPosHistoryTable(res.data);
          if (this.posCountBadge) {
            this.posCountBadge.textContent = `${res.data.length} أوامر شراء مسجلة`;
          }
        }
      } catch (err) {
        console.error('Failed to load purchase orders history:', err);
      }
    }

    renderPosHistoryTable(pos) {
      if (!this.posHistoryBody) return;
      this.posHistoryBody.innerHTML = '';

      if (pos.length === 0) {
        const tr = document.createElement('tr');
        tr.innerHTML = '<td colspan="11" style="text-align: center; color: #64748B; padding: 2rem;">لا توجد أوامر شراء مسجلة في قاعدة البيانات.</td>';
        this.posHistoryBody.appendChild(tr);
        return;
      }

      pos.forEach((p, idx) => {
        const tr = document.createElement('tr');
        tr.className = idx % 2 === 0 ? 'finance-row-even' : 'finance-row-odd';

        // Payment status badge
        const payStatus = p.paymentStatus || 'غير مدفوع';
        let payBadgeClass = 'badge-unpaid';
        if (payStatus === 'مدفوع بالكامل') payBadgeClass = 'badge-paid';
        else if (payStatus === 'مدفوع جزئياً') payBadgeClass = 'badge-partial';

        tr.innerHTML = `
          <!-- 1. PO -->
          <td>
            <strong style="font-family: monospace; color: #0F172A; font-size: 0.95rem;">${this.escapeHtml(p.poNumber)}</strong>
          </td>
          <!-- 2. تاريخ الإنشاء -->
          <td dir="ltr" style="font-family: monospace; font-size: 0.8rem; color: #64748B;">
            ${this.formatDate(p.orderDate)}
          </td>
          <!-- 3. المورد -->
          <td style="font-weight: 700; color: #1E293B;">
            ${this.escapeHtml(p.supplier || '-')}
          </td>
          <!-- 4. الكمية المتعاقدة -->
          <td style="font-weight: 700; color: #0F172A;">
            ${this.formatWeight(p.quantity)} طن
          </td>
          <!-- 5. سعر الطن -->
          <td style="color: #D97706; font-weight: 700;">
            ${this.formatCurrency(p.tonPrice)}
          </td>
          <!-- 6. الكمية المسلمة -->
          <td style="color: #059669; font-weight: 700;">
            ${this.formatWeight(p.deliveredQuantity)} طن
          </td>
          <!-- 7. الكمية المتبقية -->
          <td style="color: ${p.remainingQuantity > 0 ? '#B45309' : '#64748B'}; font-weight: 700;">
            ${this.formatWeight(p.remainingQuantity)} طن
          </td>
          <!-- 8. إجمالي الهالك -->
          <td style="color: ${p.totalWasteQuantity > 0 ? '#DC2626' : '#64748B'}; font-weight: 700;">
            ${this.formatWeight(p.totalWasteQuantity)} طن
          </td>
          <!-- 9. حالة السداد -->
          <td>
            <span class="finance-status-badge ${payBadgeClass}">${this.escapeHtml(payStatus)}</span>
          </td>
          <!-- 10. حالة العقد -->
          <td>
            <span class="finance-status-badge badge-neutral">${this.escapeHtml(p.status || 'نشط')}</span>
          </td>
          <!-- 11. زر عرض التفاصيل (Requirement 8) -->
          <td style="text-align: center;">
            <button type="button" class="finance-btn-table-details" data-po="${this.escapeHtml(p.poNumber)}" title="عرض تفاصيل أمر الشراء">
              <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.2"><path d="M1 12s4-8 11-8 11 8 11 8-4 8-11 8-11-8-11-8z"/><circle cx="12" cy="12" r="3"/></svg>
              <span>عرض التفاصيل</span>
            </button>
          </td>
        `;

        // Click handler for details button
        const btn = tr.querySelector('.finance-btn-table-details');
        btn?.addEventListener('click', (e) => {
          e.stopPropagation();
          const po = btn.getAttribute('data-po');
          this.navigateTo(`/finance/purchase-orders/${encodeURIComponent(po)}`);
        });

        this.posHistoryBody.appendChild(tr);
      });
    }

    /* ------------------------------------------------------------------------
       3. Dedicated PO Details View Logic (Requirements 8, 9, 14-18, 26)
       ------------------------------------------------------------------------ */
    async loadPoDetails(poNumber) {
      if (!poNumber) return;
      try {
        const res = await window.ChatApi.getFinancePurchaseOrderDetails(poNumber);
        if (res && res.success && res.data) {
          this.renderPoDetails(res.data);
        } else {
          alert('تعذر تحميل تفاصيل أمر الشراء: ' + (res?.message || 'غير موجود'));
          this.showDashboardView();
        }
      } catch (err) {
        console.error('Failed to load PO details:', err);
        alert('حدث خطأ أثناء جلب تفاصيل أمر الشراء.');
      }
    }

    renderPoDetails(data) {
      // 1. Identity
      if (this.poDetailTitlePo) this.poDetailTitlePo.textContent = data.poNumber;
      if (this.poDetailBadgePo) this.poDetailBadgePo.textContent = data.poNumber;
      if (this.poDetailCompany) this.poDetailCompany.textContent = data.company || data.supplier || 'المورد';
      if (this.poDetailDestination) this.poDetailDestination.textContent = `الوجهة: ${data.destination || 'غير محدد'}`;
      if (this.poDetailMaterial) this.poDetailMaterial.textContent = `المادة: ${data.material || 'مواد تعاقدية'}`;
      if (this.poDetailOrderDate) this.poDetailOrderDate.textContent = this.formatDate(data.orderDate);

      // Badges
      if (this.poDetailStatus) this.poDetailStatus.textContent = data.status || 'نشط';
      if (this.poDetailPaymentStatus) {
        this.poDetailPaymentStatus.textContent = data.paymentStatus || 'غير مدفوع';
        let cls = 'badge-unpaid';
        if (data.paymentStatus === 'مدفوع بالكامل') cls = 'badge-paid';
        else if (data.paymentStatus === 'مدفوع جزئياً') cls = 'badge-partial';
        this.poDetailPaymentStatus.className = `finance-status-badge ${cls}`;
      }

      // 2. Quantitative & Financial KPI Tiles
      if (this.poDetailExpectedQty) this.poDetailExpectedQty.textContent = this.formatWeight(data.originalQuantity) + ' طن';
      if (this.poDetailExpectedVal) this.poDetailExpectedVal.textContent = 'القيمة المتوقعة: ' + this.formatCurrency(data.expectedPrice);
      if (this.poDetailDeliveredQty) this.poDetailDeliveredQty.textContent = this.formatWeight(data.deliveredQuantity) + ' طن';
      if (this.poDetailDeliveredVal) this.poDetailDeliveredVal.textContent = 'القيمة المسلمة: ' + this.formatCurrency(data.deliveredValue);
      if (this.poDetailRemainingQty) this.poDetailRemainingQty.textContent = this.formatWeight(data.remainingQuantity) + ' طن';
      if (this.poDetailRemainingVal) this.poDetailRemainingVal.textContent = 'القيمة المتبقية: ' + this.formatCurrency(data.remainingValue);
      if (this.poDetailTotalWaste) this.poDetailTotalWaste.textContent = this.formatWeight(data.totalWasteQuantity) + ' طن';
      if (this.poDetailWasteVal) this.poDetailWasteVal.textContent = 'قيمة الهالك: ' + this.formatCurrency(data.totalWasteValue);
      if (this.poDetailTransWaste) this.poDetailTransWaste.textContent = this.formatWeight(data.transportationWasteQuantity) + ' طن';
      if (this.poDetailTransMoves) this.poDetailTransMoves.textContent = `عدد النقلات: ${Number(data.transportationTransactionsCount || 0).toLocaleString('ar-EG')}`;
      if (this.poDetailStorageWaste) this.poDetailStorageWaste.textContent = this.formatWeight(data.storageWasteQuantity) + ' طن';
      if (this.poDetailStorageQty) this.poDetailStorageQty.textContent = `إجمالي التخزين: ${this.formatWeight(data.storageQuantity)} طن`;
      if (this.poDetailTonPrice) this.poDetailTonPrice.textContent = this.formatCurrency(data.tonPrice);
      if (this.poDetailPerfPct) this.poDetailPerfPct.textContent = Number(data.performancePercentage || 0).toFixed(2) + '%';

      // 3. Payment Status Controls & Authorization (Requirements 14-18)
      if (this.poDetailPaidAmt) this.poDetailPaidAmt.textContent = this.formatCurrency(data.paidAmount);
      if (this.poDetailOutstandingAmt) this.poDetailOutstandingAmt.textContent = this.formatCurrency(data.outstandingAmount);
      if (this.poDetailCurrentPaymentBadge) {
        this.poDetailCurrentPaymentBadge.textContent = data.paymentStatus || 'غير مدفوع';
        let cls = 'badge-unpaid';
        if (data.paymentStatus === 'مدفوع بالكامل') cls = 'badge-paid';
        else if (data.paymentStatus === 'مدفوع جزئياً') cls = 'badge-partial';
        this.poDetailCurrentPaymentBadge.className = `finance-status-badge ${cls}`;
      }

      // Check if current user is authorized Finance Director
      const canEditPayment = (data.isCurrentUserFinanceDirector === true) || this.isFinanceDirector;
      if (this.poDetailDirectorControls) {
        this.poDetailDirectorControls.style.display = canEditPayment ? 'flex' : 'none';
      }
      if (this.poDetailReadonlyControls) {
        this.poDetailReadonlyControls.style.display = canEditPayment ? 'none' : 'inline-flex';
      }
      if (this.poDetailPaymentSelect) {
        this.poDetailPaymentSelect.value = data.paymentStatus || 'غير مدفوع';
      }
      if (this.poDetailPaymentAlert) {
        this.poDetailPaymentAlert.style.display = 'none';
      }

      // Payment Audit Trail Table
      this.renderPaymentAudits(data.paymentAudits || []);

      // 4. Chronological Timeline (Requirement 9)
      this.renderChronologicalTimeline(data.timeline || []);

      // 5. Transportation Records Table
      this.renderPoTransportationTable(data.transportationTransactions || []);
      if (this.poDetailTransCountBadge) {
        this.poDetailTransCountBadge.textContent = `${(data.transportationTransactions || []).length} نقلات`;
      }

      // 6. Storage Records Table
      this.renderPoStorageTable(data.storageTransactions || []);
      if (this.poDetailStorageCountBadge) {
        this.poDetailStorageCountBadge.textContent = `${(data.storageTransactions || []).length} حركات`;
      }
    }

    renderPaymentAudits(audits) {
      if (!this.poDetailAuditTbody) return;
      this.poDetailAuditTbody.innerHTML = '';

      if (audits.length === 0) {
        const tr = document.createElement('tr');
        tr.innerHTML = '<td colspan="5" style="text-align: center; color: #64748B; padding: 1rem;">لا توجد تغييرات سابقة مسجلة في سجل تدقيق الدفع.</td>';
        this.poDetailAuditTbody.appendChild(tr);
        return;
      }

      audits.forEach(a => {
        const tr = document.createElement('tr');
        tr.innerHTML = `
          <td><span class="finance-status-badge badge-neutral">${this.escapeHtml(a.previousStatus || '-')}</span></td>
          <td><span class="finance-status-badge badge-neutral">${this.escapeHtml(a.newStatus || '-')}</span></td>
          <td style="font-weight: 700; color: #0F172A;">${this.escapeHtml(a.changedBy || 'مدير مالي')}</td>
          <td dir="ltr" style="font-family: monospace; font-size: 0.8rem; color: #64748B;">${this.formatDateTime(a.changedAt)}</td>
          <td style="color: #475569;">${this.escapeHtml(a.notes || '-')}</td>
        `;
        this.poDetailAuditTbody.appendChild(tr);
      });
    }

    renderChronologicalTimeline(events) {
      if (!this.poDetailTimelineList) return;
      this.poDetailTimelineList.innerHTML = '';

      if (events.length === 0) {
        this.poDetailTimelineList.innerHTML = '<div style="color: #64748B; padding: 1rem;">لا توجد حركات مسجلة لهذا العقد.</div>';
        return;
      }

      events.forEach(ev => {
        const item = document.createElement('div');
        item.className = 'finance-timeline-item';

        let dotClass = 'status';
        if (ev.eventType === 'PoCreated') dotClass = 'created';
        else if (ev.eventType === 'Transportation') dotClass = 'transport';
        else if (ev.eventType === 'Storage') dotClass = 'storage';
        else if (ev.eventType === 'PaymentStatusChange') dotClass = 'payment';

        item.innerHTML = `
          <div class="finance-timeline-dot ${dotClass}"></div>
          <div class="finance-timeline-box">
            <div class="finance-timeline-header">
              <span class="finance-timeline-title">${this.escapeHtml(ev.title)}</span>
              <span class="finance-timeline-date">${this.formatDateTime(ev.eventDate)}</span>
            </div>
            <div class="finance-timeline-desc">${this.escapeHtml(ev.description || '')}</div>
          </div>
        `;
        this.poDetailTimelineList.appendChild(item);
      });
    }

    renderPoTransportationTable(transactions) {
      if (!this.poDetailTransTbody) return;
      this.poDetailTransTbody.innerHTML = '';

      if (transactions.length === 0) {
        const tr = document.createElement('tr');
        tr.innerHTML = '<td colspan="7" style="text-align: center; color: #64748B; padding: 1rem;">لا توجد حركات نقل مسجلة لهذا العقد.</td>';
        this.poDetailTransTbody.appendChild(tr);
        return;
      }

      transactions.forEach(t => {
        const tr = document.createElement('tr');
        tr.innerHTML = `
          <td style="font-family: monospace; font-weight: 700;">${this.escapeHtml(t.ticketNumber || '-')}</td>
          <td dir="ltr" style="font-family: monospace; font-size: 0.8rem; color: #64748B;">${this.formatDate(t.transactionDate)}</td>
          <td style="font-weight: 700; color: #1E293B;">${this.escapeHtml(t.truckNumber || '-')}</td>
          <td>${this.escapeHtml(t.driverName || '-')}</td>
          <td style="font-weight: 700; color: #0F172A;">${this.formatWeight(t.netWeight)} طن</td>
          <td style="color: ${t.wasteQuantity > 0 ? '#DC2626' : '#64748B'}; font-weight: 700;">${this.formatWeight(t.wasteQuantity)} طن</td>
          <td><span class="finance-status-badge badge-neutral">${this.escapeHtml(t.status || 'مكتمل')}</span></td>
        `;
        this.poDetailTransTbody.appendChild(tr);
      });
    }

    renderPoStorageTable(transactions) {
      if (!this.poDetailStorageTbody) return;
      this.poDetailStorageTbody.innerHTML = '';

      if (transactions.length === 0) {
        const tr = document.createElement('tr');
        tr.innerHTML = '<td colspan="8" style="text-align: center; color: #64748B; padding: 1rem;">لا توجد حركات تخزين مسجلة لهذا العقد.</td>';
        this.poDetailStorageTbody.appendChild(tr);
        return;
      }

      transactions.forEach(s => {
        const tr = document.createElement('tr');
        tr.innerHTML = `
          <td style="font-family: monospace; font-weight: 700;">${this.escapeHtml(s.transactionCode || '-')}</td>
          <td dir="ltr" style="font-family: monospace; font-size: 0.8rem; color: #64748B;">${this.formatDate(s.transactionDate)}</td>
          <td><span class="finance-status-badge badge-neutral">${this.escapeHtml(s.action || s.serviceType || 'إيداع')}</span></td>
          <td style="font-weight: 700;">${this.escapeHtml(s.material || '-')}</td>
          <td style="font-weight: 700; color: #0F172A;">${this.formatWeight(s.internalWeight)} طن</td>
          <td>${s.externalWeight !== null && s.externalWeight !== undefined ? (this.formatWeight(s.externalWeight) + ' طن') : '<span style="color: #94A3B8;">-</span>'}</td>
          <td style="color: ${s.wasteQuantity > 0 ? '#DC2626' : '#64748B'}; font-weight: 700;">${this.formatWeight(s.wasteQuantity)} طن</td>
          <td style="color: #64748B;">${this.escapeHtml(s.warehouseName || '-')}</td>
        `;
        this.poDetailStorageTbody.appendChild(tr);
      });
    }

    async handleSavePaymentStatus() {
      if (!this.currentPoNumber) return;
      const newStatus = this.poDetailPaymentSelect?.value;
      const notes = this.poDetailPaymentNotes?.value?.trim() || '';

      if (!newStatus) return;

      if (this.poDetailBtnSavePayment) {
        this.poDetailBtnSavePayment.disabled = true;
        this.poDetailBtnSavePayment.textContent = 'جارٍ الحفظ...';
      }

      try {
        const res = await window.ChatApi.updateFinancePaymentStatus(this.currentPoNumber, newStatus, notes);
        if (res && res.success) {
          if (this.poDetailPaymentAlert) {
            this.poDetailPaymentAlert.className = 'finance-alert success';
            this.poDetailPaymentAlert.textContent = 'تم حفظ وتحديث حالة السداد بنجاح في قاعدة البيانات.';
            this.poDetailPaymentAlert.style.display = 'flex';
          }
          if (this.poDetailPaymentNotes) this.poDetailPaymentNotes.value = '';

          // Reload PO Details & History
          await this.loadPoDetails(this.currentPoNumber);
          this.loadPurchaseOrdersHistory();
          this.loadStatistics();
        } else {
          if (this.poDetailPaymentAlert) {
            this.poDetailPaymentAlert.className = 'finance-alert error';
            this.poDetailPaymentAlert.textContent = 'فشل في تحديث حالة السداد: ' + (res?.message || 'غير مصرح لك.');
            this.poDetailPaymentAlert.style.display = 'flex';
          }
        }
      } catch (err) {
        console.error('Error saving payment status:', err);
        if (this.poDetailPaymentAlert) {
          this.poDetailPaymentAlert.className = 'finance-alert error';
          this.poDetailPaymentAlert.textContent = 'حدث خطأ أثناء حفظ حالة السداد: صلاحيات غير كافية أو فشل في الاتصال.';
          this.poDetailPaymentAlert.style.display = 'flex';
        }
      } finally {
        if (this.poDetailBtnSavePayment) {
          this.poDetailBtnSavePayment.disabled = false;
          this.poDetailBtnSavePayment.innerHTML = `
            <svg width="15" height="15" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.5"><polyline points="20 6 9 17 4 12"/></svg>
            <span>حفظ وتحديث الحالة</span>
          `;
        }
      }
    }

    /* ------------------------------------------------------------------------
       4. Dedicated Performance Details View Logic (Requirements 10-13, 27)
       ------------------------------------------------------------------------ */
    async loadPerformanceDetails() {
      try {
        const poParam = (this.selectedPo && this.selectedPo !== 'all') ? this.selectedPo : '';
        const res = await window.ChatApi.getFinancePerformanceDetails(this.perfFromDate, this.perfToDate, poParam);
        if (res && res.success && res.data) {
          this.renderPerformanceDetails(res.data);
        }
      } catch (err) {
        console.error('Failed to load performance details:', err);
      }
    }

    renderPerformanceDetails(data) {
      if (!data) return;

      // Date range label
      if (this.perfRangeLabel) {
        if (data.filterFromDate === data.filterToDate) {
          this.perfRangeLabel.textContent = data.filterFromDate || 'اليوم';
        } else {
          this.perfRangeLabel.textContent = `من ${data.filterFromDate} إلى ${data.filterToDate}`;
        }
      }

      // Overall dial
      const perfPct = Number(data.overallPerformancePercentage || 0);
      if (this.perfDetailPercentage) this.perfDetailPercentage.textContent = perfPct.toFixed(2) + '%';
      if (this.perfDetailBadge && this.perfDetailBadgeText) {
        this.perfDetailBadge.className = 'finance-perf-badge';
        if (perfPct >= 95) {
          this.perfDetailBadge.classList.add('perf-positive');
          this.perfDetailBadgeText.textContent = 'أداء ممتاز';
        } else if (perfPct >= 75) {
          this.perfDetailBadge.classList.add('medium');
          this.perfDetailBadgeText.textContent = 'أداء متوسط / قيد التوريد';
        } else {
          this.perfDetailBadge.classList.add('low');
          this.perfDetailBadgeText.textContent = 'أداء منخفض / يتطلب متابعة';
        }
      }

      // Expected vs Actual grid
      if (this.perfDetailExpectedQty) this.perfDetailExpectedQty.textContent = this.formatWeight(data.cumulativeExpectedQuantity) + ' طن';
      if (this.perfDetailExpectedPrice) this.perfDetailExpectedPrice.textContent = this.formatCurrency(data.cumulativeExpectedPrice);
      if (this.perfDetailActualQty) this.perfDetailActualQty.textContent = this.formatWeight(data.cumulativeDeliveredQuantity) + ' طن';
      if (this.perfDetailActualPrice) this.perfDetailActualPrice.textContent = this.formatCurrency(data.cumulativeDeliveredPrice);
      if (this.perfDetailTotalWaste) this.perfDetailTotalWaste.textContent = this.formatWeight(data.cumulativeTotalWaste) + ' طن';
      if (this.perfDetailWastePrice) this.perfDetailWastePrice.textContent = this.formatCurrency(data.cumulativeWasteFinancialValue);
      if (this.perfDetailRemainingQty) this.perfDetailRemainingQty.textContent = this.formatWeight(data.cumulativeRemainingQuantity) + ' طن';
      if (this.perfDetailRemainingPrice) this.perfDetailRemainingPrice.textContent = this.formatCurrency(data.cumulativeRemainingPrice);

      // Requirements 11 & 12: Arithmetic Step-by-Step Transparent Formulas
      // 1. Quantitative Calculation Steps
      if (this.perfCalcQtySteps) {
        this.perfCalcQtySteps.innerHTML = `
          <div class="finance-calc-step">
            <span class="finance-calc-step-label">1. إجمالي الكمية التعاقدية المتوقعة (Expected Quantity):</span>
            <div class="calc-math-formula">${this.formatWeight(data.cumulativeExpectedQuantity)} طن</div>
          </div>
          <div class="finance-calc-step">
            <span class="finance-calc-step-label">2. إجمالي الهالك الفعلي (نقل + تخزين):</span>
            <div class="calc-math-formula">هالك النقل (${this.formatWeight(data.cumulativeTransportationWaste)} طن) + هالك التخزين (${this.formatWeight(data.cumulativeStorageWaste)} طن) = <strong>${this.formatWeight(data.cumulativeTotalWaste)} طن</strong></div>
          </div>
          <div class="finance-calc-step">
            <span class="finance-calc-step-label">3. حساب الكمية الفعلية المسلمة (Actual Delivered Quantity):</span>
            <div class="calc-math-formula">${data.quantitativeCalculationFormula || `${this.formatWeight(data.cumulativeExpectedQuantity)} - ${this.formatWeight(data.cumulativeTotalWaste)} = ${this.formatWeight(data.cumulativeDeliveredQuantity)} طن`}</div>
          </div>
          <div class="finance-calc-step">
            <span class="finance-calc-step-label">4. نسبة الإنجاز والأداء الكمي (Performance %):</span>
            <div class="calc-math-formula">${data.quantitativePerformancePercentageFormula || `(${this.formatWeight(data.cumulativeDeliveredQuantity)} / ${this.formatWeight(data.cumulativeExpectedQuantity)}) × 100 = ${perfPct.toFixed(2)}%`}</div>
          </div>
        `;
      }

      // 2. Financial Calculation Steps
      if (this.perfCalcFinSteps) {
        const finPct = Number(data.financialPerformancePercentage || 0);
        this.perfCalcFinSteps.innerHTML = `
          <div class="finance-calc-step">
            <span class="finance-calc-step-label">1. القيمة المالية المتوقعة لكافة العقود (Expected Financial Value):</span>
            <div class="calc-math-formula">${this.formatCurrency(data.cumulativeExpectedPrice)}</div>
          </div>
          <div class="finance-calc-step">
            <span class="finance-calc-step-label">2. القيمة المالية الفعلية المسلمة (Delivered Financial Value):</span>
            <div class="calc-math-formula">${this.formatCurrency(data.cumulativeDeliveredPrice)}</div>
          </div>
          <div class="finance-calc-step">
            <span class="finance-calc-step-label">3. القيمة المالية المفقودة في الهالك (Waste Financial Value):</span>
            <div class="calc-math-formula">${this.formatCurrency(data.cumulativeWasteFinancialValue)}</div>
          </div>
          <div class="finance-calc-step">
            <span class="finance-calc-step-label">4. نسبة الأداء المالي (Financial Performance %):</span>
            <div class="calc-math-formula">${data.financialPerformancePercentageFormula || `(${this.formatCurrency(data.cumulativeDeliveredPrice)} / ${this.formatCurrency(data.cumulativeExpectedPrice)}) × 100 = ${finPct.toFixed(2)}%`}</div>
          </div>
        `;
      }

      // 3. Waste Breakdown Steps
      if (this.perfCalcWasteSteps) {
        this.perfCalcWasteSteps.innerHTML = `
          <div class="finance-calc-step">
            <span class="finance-calc-step-label">1. هالك النقل التراكمي (Transportation Waste):</span>
            <div class="calc-math-formula">${this.formatWeight(data.cumulativeTransportationWaste)} طن (فارق ميزان الشاحنات المسجل فعلياً)</div>
          </div>
          <div class="finance-calc-step">
            <span class="finance-calc-step-label">2. هالك التخزين التراكمي (Storage Waste):</span>
            <div class="calc-math-formula">${this.formatWeight(data.cumulativeStorageWaste)} طن (فارق ميزان البسكول الخارجي عن الداخلي في المخازن)</div>
          </div>
          <div class="finance-calc-step">
            <span class="finance-calc-step-label">3. قاعدة احتساب إجمالي الهالك:</span>
            <div class="calc-math-formula">إجمالي الهالك = هالك النقل (${this.formatWeight(data.cumulativeTransportationWaste)} طن) + هالك التخزين (${this.formatWeight(data.cumulativeStorageWaste)} طن) = <strong>${this.formatWeight(data.cumulativeTotalWaste)} طن</strong></div>
          </div>
        `;
      }

      // 4. Range vs Cumulative Comparison (Requirement 13)
      if (this.perfCalcComparisonSteps) {
        this.perfCalcComparisonSteps.innerHTML = `
          <div class="finance-calc-step">
            <span class="finance-calc-step-label">حركات النطاق الزمني المحدد (${data.filterFromDate} إلى ${data.filterToDate}):</span>
            <div class="calc-math-formula">
              عدد النقلات: <strong>${Number(data.rangeTransportationCount || 0).toLocaleString('ar-EG')}</strong> | 
              الصافي المنقول: <strong>${this.formatWeight(data.rangeTransportationNetWeight)} طن</strong> | 
              الوارد المخزني المستقل: <strong>${this.formatWeight(data.rangeStorageImportedQuantity)} طن</strong> | 
              هالك الفترة: <strong>${this.formatWeight(data.rangeTotalWaste)} طن</strong>
            </div>
          </div>
          <div class="finance-calc-step">
            <span class="finance-calc-step-label">الموقف التراكمي لعقود أوامر الشراء (Cumulative PO Status):</span>
            <div class="calc-math-formula">
              إجمالي التعاقد: <strong>${this.formatWeight(data.cumulativeExpectedQuantity)} طن</strong> | 
              إجمالي المسلم: <strong>${this.formatWeight(data.cumulativeDeliveredQuantity)} طن</strong> | 
              إجمالي الهالك الكلي: <strong>${this.formatWeight(data.cumulativeTotalWaste)} طن</strong> | 
              المتبقي الكلي: <strong>${this.formatWeight(data.cumulativeRemainingQuantity)} طن</strong>
            </div>
          </div>
          <div class="finance-calc-step">
            <span class="finance-calc-step-label">التوضيح المحاسبي:</span>
            <div style="font-size: 0.8rem; color: #475569;">
              يتم احتساب نسبة الأداء الكلية بناءً على الموقف التراكمي للتعاقد لضمان عدم معاملة حركة اليوم كإجمالي للعقد كاملاً.
            </div>
          </div>
        `;
      }

      // PO Breakdown Table
      this.renderPerformancePoTable(data.poBreakdowns || []);
    }

    renderPerformancePoTable(pos) {
      if (!this.perfPosTbody) return;
      this.perfPosTbody.innerHTML = '';

      if (pos.length === 0) {
        const tr = document.createElement('tr');
        tr.innerHTML = '<td colspan="10" style="text-align: center; color: #64748B; padding: 1rem;">لا توجد أوامر شراء للمقارنة.</td>';
        this.perfPosTbody.appendChild(tr);
        return;
      }

      pos.forEach(p => {
        const tr = document.createElement('tr');
        tr.innerHTML = `
          <td><strong style="font-family: monospace; color: #0F172A;">${this.escapeHtml(p.poNumber)}</strong></td>
          <td style="font-weight: 700;">${this.escapeHtml(p.supplier || '-')}</td>
          <td style="font-weight: 700;">${this.formatWeight(p.expectedQuantity)} طن</td>
          <td style="color: #059669; font-weight: 700;">${this.formatWeight(p.deliveredQuantity)} طن</td>
          <td style="color: #DC2626; font-weight: 700;">${this.formatWeight(p.totalWasteQuantity)} طن</td>
          <td style="color: #B45309; font-weight: 700;">${this.formatWeight(p.remainingQuantity)} طن</td>
          <td><strong style="color: #1D4ED8;">${Number(p.performancePercentage || 0).toFixed(2)}%</strong></td>
          <td>${this.formatCurrency(p.expectedPrice)}</td>
          <td>${this.formatCurrency(p.deliveredPrice)}</td>
          <td style="text-align: center;">
            <button type="button" class="finance-btn-table-details" data-po="${this.escapeHtml(p.poNumber)}" title="عرض تفاصيل أمر الشراء">
              <svg width="13" height="13" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.2"><path d="M1 12s4-8 11-8 11 8 11 8-4 8-11 8-11-8-11-8z"/><circle cx="12" cy="12" r="3"/></svg>
              <span>التفاصيل</span>
            </button>
          </td>
        `;

        const btn = tr.querySelector('.finance-btn-table-details');
        btn?.addEventListener('click', () => {
          const po = btn.getAttribute('data-po');
          this.navigateTo(`/finance/purchase-orders/${encodeURIComponent(po)}`);
        });

        this.perfPosTbody.appendChild(tr);
      });

      // Real Forecasting Section
      if (data.forecasting) {
        const f = data.forecasting;
        const bannerTextEl = document.getElementById('perf-forecast-summary-text');
        if (bannerTextEl) {
          bannerTextEl.textContent = f.forecastingSummaryArabic || 'لا تتوفر توقعات حالية.';
        }
        const remQtyEl = document.getElementById('perf-forecast-rem-qty');
        if (remQtyEl) remQtyEl.textContent = this.formatWeight(f.expectedRemainingQuantity) + ' طن';

        const remValEl = document.getElementById('perf-forecast-rem-val');
        if (remValEl) remValEl.textContent = this.formatCurrency(f.expectedRemainingValue);

        const dailyPaceEl = document.getElementById('perf-forecast-daily-pace');
        if (dailyPaceEl) dailyPaceEl.textContent = this.formatWeight(f.averageDailyDeliveredQuantity) + ' طن/يوم';

        const daysEl = document.getElementById('perf-forecast-days');
        if (daysEl) daysEl.textContent = (f.estimatedDaysToCompletion || 0) + ' يوم';

        const dateEl = document.getElementById('perf-forecast-date');
        if (dateEl) dateEl.textContent = f.projectedCompletionDate ? this.formatDate(f.projectedCompletionDate) : 'غير محدد';

        const yieldEl = document.getElementById('perf-forecast-yield');
        if (yieldEl) yieldEl.textContent = this.formatCurrency(f.expectedFinalFinancialYield);
      }
    }

    /* ------------------------------------------------------------------------
       5. Detailed Management Table (Bottom Section)
       ------------------------------------------------------------------------ */
    async loadData() {
      this.setLoadingState(true);
      try {
        const params = {
          search: this.filters.search,
          po: this.filters.po,
          supplier: this.filters.supplier,
          paymentStatus: this.filters.paymentStatus,
          invoiceNumber: this.filters.invoiceNumber,
          dateFrom: this.fromDate,
          dateTo: this.toDate,
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

        let badgeClass = 'badge-unpaid';
        if (r.paymentStatus === 'مدفوع بالكامل') {
          badgeClass = 'badge-paid';
        } else if (r.paymentStatus === 'مدفوع جزئياً') {
          badgeClass = 'badge-partial';
        }

        const paidFormatted = this.formatCurrency(r.paidAmount);
        const paidHtml = r.paidAmount > 0 
          ? `<span class="finance-paid-link" data-po="${this.escapeHtml(r.poNumber)}" title="عرض تفاصيل الدفعات">${paidFormatted} <svg width="12" height="12" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.5"><path d="M18 13v6a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2V8a2 2 0 0 1 2-2h6"/><polyline points="15 3 21 3 21 9"/><line x1="10" y1="14" x2="21" y2="3"/></svg></span>`
          : `<span>0.00 ج.م</span>`;

        tr.innerHTML = `
          <td class="cell-po">
            <span class="finance-po-badge">${this.escapeHtml(r.poNumber || '-')}</span>
          </td>
          <td class="cell-supplier font-medium">
            ${this.escapeHtml(r.supplier || '-')}
          </td>
          <td class="cell-date font-numeric" dir="ltr">
            ${this.formatDate(r.orderDate)}
          </td>
          <td class="cell-qty font-numeric font-semibold">
            ${this.formatWeight(r.poQuantity)} طن
          </td>
          <td class="cell-val font-numeric font-semibold color-primary">
            ${this.formatCurrency(r.poValue)}
          </td>
          <td class="cell-price font-numeric">
            ${this.formatCurrency(r.tonPrice)}
          </td>
          <td class="cell-supplied-qty font-numeric font-semibold color-info">
            ${this.formatWeight(r.suppliedQuantity)} طن
          </td>
          <td class="cell-supplied-val font-numeric font-semibold">
            ${this.formatCurrency(r.suppliedValue)}
          </td>
          <td class="cell-remaining-qty font-numeric font-semibold ${r.remainingQuantity > 0 ? 'color-warning' : 'color-muted'}">
            ${this.formatWeight(r.remainingQuantity)} طن
          </td>
          <td class="cell-remaining-val font-numeric ${r.remainingValue > 0 ? 'color-warning' : 'color-muted'}">
            ${this.formatCurrency(r.remainingValue)}
          </td>
          <td class="cell-movements font-numeric">
            <span class="finance-count-badge">${Number(r.movementsCount || 0).toLocaleString('ar-EG')} نقلة</span>
          </td>
          <td class="cell-approved font-numeric font-semibold color-success">
            ${this.formatWeight(r.approvedQuantity)} طن
          </td>
          <td class="cell-unapproved font-numeric ${r.unapprovedQuantity > 0 ? 'color-danger font-semibold' : 'color-muted'}">
            ${this.formatWeight(r.unapprovedQuantity)} طن
          </td>
          <td class="cell-paid font-numeric font-semibold color-success">
            ${paidHtml}
          </td>
          <td class="cell-outstanding font-numeric font-semibold ${r.outstandingAmount > 0 ? 'color-danger' : 'color-success'}">
            ${this.formatCurrency(r.outstandingAmount)}
          </td>
          <td class="cell-status">
            <span class="finance-status-badge ${badgeClass}">${this.escapeHtml(r.paymentStatus)}</span>
          </td>
          <td class="cell-invoice font-numeric">
            ${r.invoiceNumber ? `<span class="finance-invoice-chip" dir="ltr">${this.escapeHtml(r.invoiceNumber)}</span>` : '<span class="color-muted">غير محدد</span>'}
          </td>
        `;

        tr.addEventListener('click', (e) => {
          if (e.target.closest('.finance-paid-link')) return;
          const poNum = tr.getAttribute('data-po');
          if (!poNum || poNum === '-') return;
          this.navigateTo(`/finance/purchase-orders/${encodeURIComponent(poNum)}`);
        });

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
      if (!this.paginationInfo) return;
      const start = this.totalRecords === 0 ? 0 : (this.currentPage - 1) * this.pageSize + 1;
      const end = Math.min(this.currentPage * this.pageSize, this.totalRecords);
      this.paginationInfo.textContent = `عرض ${start} إلى ${end} من إجمالي ${this.totalRecords} أمر شراء`;

      if (this.btnPrevPage) this.btnPrevPage.disabled = (this.currentPage <= 1);
      if (this.btnNextPage) this.btnNextPage.disabled = (this.currentPage >= this.totalPages);

      if (this.pageNumbersContainer) {
        this.pageNumbersContainer.innerHTML = '';
        for (let i = 1; i <= this.totalPages; i++) {
          if (i === 1 || i === this.totalPages || (i >= this.currentPage - 1 && i <= this.currentPage + 1)) {
            const btn = document.createElement('button');
            btn.type = 'button';
            btn.className = `finance-page-btn ${i === this.currentPage ? 'active' : ''}`;
            btn.textContent = i;
            btn.addEventListener('click', () => {
              this.currentPage = i;
              this.loadData();
            });
            this.pageNumbersContainer.appendChild(btn);
          }
        }
      }
    }

    /* ------------------------------------------------------------------------
       CEO Payment Modal
       ------------------------------------------------------------------------ */
    async showPaymentDetailsModal(poNumber) {
      if (!this.paymentModal) return;
      if (this.modalPoNumber) this.modalPoNumber.textContent = poNumber;
      if (this.modalPaymentsList) this.modalPaymentsList.innerHTML = '<div style="text-align: center; padding: 2rem;">جارٍ تحميل الدفعات...</div>';
      if (this.modalBackdrop) this.modalBackdrop.style.display = 'flex';
      this.paymentModal.style.display = 'flex';

      try {
        const res = await window.ChatApi.getFinancePayments(poNumber);
        if (res && res.success && Array.isArray(res.data)) {
          this.renderModalPayments(res.data);
        } else {
          if (this.modalPaymentsList) this.modalPaymentsList.innerHTML = '<div style="color: #64748B; padding: 2rem; text-align: center;">لا توجد دفعات مسجلة لهذا العقد.</div>';
        }
      } catch (err) {
        console.error('Error fetching payments:', err);
        if (this.modalPaymentsList) this.modalPaymentsList.innerHTML = '<div style="color: #DC2626; padding: 2rem; text-align: center;">فشل في تحميل سجل الدفعات.</div>';
      }
    }

    renderModalPayments(payments) {
      if (!this.modalPaymentsList) return;
      if (payments.length === 0) {
        this.modalPaymentsList.innerHTML = '<div style="color: #64748B; padding: 2rem; text-align: center;">لا توجد دفعات مالية مسجلة لهذا العقد.</div>';
        return;
      }

      let html = '<div class="finance-table-scroll"><table class="finance-table"><thead><tr><th>رقم الدفعة</th><th>المبلغ</th><th>التاريخ</th><th>طريقة السداد</th><th>الحالة</th><th>ملاحظات</th></tr></thead><tbody>';
      payments.forEach(p => {
        html += `
          <tr>
            <td style="font-family: monospace; font-weight: 700;">${this.escapeHtml(p.paymentNumber || p.id || '-')}</td>
            <td style="color: #059669; font-weight: 700;">${this.formatCurrency(p.amount)}</td>
            <td dir="ltr" style="font-family: monospace;">${this.formatDate(p.paymentDate || p.createdAt)}</td>
            <td>${this.escapeHtml(p.paymentMethod || 'تحويل بنكي')}</td>
            <td><span class="finance-status-badge badge-paid">${this.escapeHtml(p.status || 'معتمد')}</span></td>
            <td>${this.escapeHtml(p.notes || '-')}</td>
          </tr>
        `;
      });
      html += '</tbody></table></div>';
      this.modalPaymentsList.innerHTML = html;
    }

    closePaymentModal() {
      if (this.paymentModal) this.paymentModal.style.display = 'none';
      if (this.modalBackdrop) this.modalBackdrop.style.display = 'none';
    }

    resetFilters() {
      this.initDefaultDates();
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
      if (this.filterSupplierSelect) this.filterSupplierSelect.value = '';
      if (this.filterPaymentStatusSelect) this.filterPaymentStatusSelect.value = 'all';
      if (this.filterInvoiceInput) this.filterInvoiceInput.value = '';

      this.currentPage = 1;
      this.loadStatistics();
      this.loadPurchaseOrdersHistory();
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

    formatDateTime(dateStr) {
      if (!dateStr) return '-';
      try {
        const d = new Date(dateStr);
        if (isNaN(d.getTime())) return dateStr;
        const year = d.getFullYear();
        const month = String(d.getMonth() + 1).padStart(2, '0');
        const day = String(d.getDate()).padStart(2, '0');
        const hours = String(d.getHours()).padStart(2, '0');
        const minutes = String(d.getMinutes()).padStart(2, '0');
        return `${year}-${month}-${day} ${hours}:${minutes}`;
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

    bindNewHubAndSubviewsEvents() {
      // 7 Hub Cards click & keyboard navigation
      const cards = [
        { id: 'card-nav-finance-history', route: '/finance/history' },
        { id: 'card-nav-finance-suppliers', route: '/finance/suppliers' },
        { id: 'card-nav-finance-pos', route: '/finance/purchase-orders' },
        { id: 'card-nav-finance-performance', route: '/finance/performance' },
        { id: 'card-nav-finance-bank', route: '/finance/bank' },
        { id: 'card-nav-finance-expenses', route: '/finance/expenses' },
        { id: 'card-nav-finance-employees', route: '/finance/employees' }
      ];
      cards.forEach(c => {
        const el = document.getElementById(c.id);
        if (el) {
          el.addEventListener('click', (e) => {
            e.preventDefault();
            this.navigateTo(c.route);
          });
          el.addEventListener('keydown', (e) => {
            if (e.key === 'Enter' || e.key === ' ') {
              e.preventDefault();
              this.navigateTo(c.route);
            }
          });
        }
      });

      // Subview Back Buttons (Return to Finance Main Hub or Parents)
      const backButtons = [
        { id: 'finance-history-back-btn', route: '/finance' },
        { id: 'finance-suppliers-back-btn', route: '/finance' },
        { id: 'finance-supplier-detail-back-btn', route: '/finance/suppliers' },
        { id: 'finance-pos-back-btn', route: '/finance' },
        { id: 'finance-po-details-back-btn', route: '/finance/purchase-orders' },
        { id: 'finance-perf-back-btn', route: '/finance' },
        { id: 'finance-bank-back-btn', route: '/finance' },
        { id: 'finance-expenses-back-btn', route: '/finance' },
        { id: 'finance-employees-back-btn', route: '/finance' },
        { id: 'finance-emp-detail-back-btn', route: '/finance/employees' }
      ];
      backButtons.forEach(b => {
        const btn = document.getElementById(b.id);
        if (btn) {
          btn.addEventListener('click', (e) => {
            e.preventDefault();
            this.navigateTo(b.route);
          });
        }
      });

      // 1. History Filters & Pagination
      const histApplyBtn = document.getElementById('finance-hist-apply-btn');
      if (histApplyBtn) {
        histApplyBtn.addEventListener('click', () => {
          this.histPage = 1;
          this.loadHistoryData();
        });
      }
      const histResetBtn = document.getElementById('finance-hist-reset-btn');
      if (histResetBtn) {
        histResetBtn.addEventListener('click', () => {
          const df = document.getElementById('finance-hist-date-from');
          const dt = document.getElementById('finance-hist-date-to');
          const ty = document.getElementById('finance-hist-type-select');
          const po = document.getElementById('finance-hist-po-select');
          const sp = document.getElementById('finance-hist-supplier-select');
          const sr = document.getElementById('finance-hist-search-input');
          if (df) df.value = '';
          if (dt) dt.value = '';
          if (ty) ty.value = 'all';
          if (po) po.value = 'all';
          if (sp) sp.value = 'all';
          if (sr) sr.value = '';
          this.histPage = 1;
          this.loadHistoryData();
        });
      }
      const histRefreshBtn = document.getElementById('finance-hist-refresh-btn');
      if (histRefreshBtn) {
        histRefreshBtn.addEventListener('click', () => this.loadHistoryData());
      }
      const histSearch = document.getElementById('finance-hist-search-input');
      if (histSearch) {
        let t = null;
        histSearch.addEventListener('input', () => {
          clearTimeout(t);
          t = setTimeout(() => {
            this.histPage = 1;
            this.loadHistoryData();
          }, 350);
        });
      }
      const histPrev = document.getElementById('finance-hist-prev-page');
      if (histPrev) {
        histPrev.addEventListener('click', () => {
          if (this.histPage > 1) {
            this.histPage--;
            this.loadHistoryData();
          }
        });
      }
      const histNext = document.getElementById('finance-hist-next-page');
      if (histNext) {
        histNext.addEventListener('click', () => {
          if (this.histPage < (this.histTotalPages || 1)) {
            this.histPage++;
            this.loadHistoryData();
          }
        });
      }

      // 2. Suppliers Search & Refresh
      const supSearch = document.getElementById('finance-suppliers-search-input');
      if (supSearch) {
        let t = null;
        supSearch.addEventListener('input', () => {
          clearTimeout(t);
          t = setTimeout(() => this.filterSuppliersClientSide(), 250);
        });
      }
      const supRefresh = document.getElementById('finance-suppliers-refresh-btn');
      if (supRefresh) {
        supRefresh.addEventListener('click', () => this.loadSuppliersData());
      }

      // 3. Purchase Orders Filters & Refresh
      const posSearch = document.getElementById('finance-pos-search-input');
      if (posSearch) {
        let t = null;
        posSearch.addEventListener('input', () => {
          clearTimeout(t);
          t = setTimeout(() => this.filterPurchaseOrdersClientSide(), 250);
        });
      }
      const posStatus = document.getElementById('finance-pos-status-select');
      if (posStatus) {
        posStatus.addEventListener('change', () => this.filterPurchaseOrdersClientSide());
      }
      const posRefresh = document.getElementById('finance-pos-refresh-btn');
      if (posRefresh) {
        posRefresh.addEventListener('click', () => this.loadAllPurchaseOrdersData());
      }

      // 5. Bank Add Modal & Form
      const addBankBtn = document.getElementById('finance-btn-add-bank-trans');
      if (addBankBtn) {
        addBankBtn.addEventListener('click', () => this.openAddBankModal());
      }
      const bankCloseBtn = document.getElementById('bank-modal-close-btn');
      const bankCancelBtn = document.getElementById('bank-modal-cancel-btn');
      [bankCloseBtn, bankCancelBtn].forEach(b => {
        if (b) b.addEventListener('click', () => this.closeAddBankModal());
      });
      const bankForm = document.getElementById('finance-bank-form');
      if (bankForm) {
        bankForm.addEventListener('submit', (e) => this.submitBankTransaction(e));
      }
      const bankRefresh = document.getElementById('finance-bank-refresh-btn');
      if (bankRefresh) {
        bankRefresh.addEventListener('click', () => this.loadBankData());
      }

      // 6. Expenses Add Modal & Form
      const addExpBtn = document.getElementById('finance-btn-add-expense');
      if (addExpBtn) {
        addExpBtn.addEventListener('click', () => this.openAddExpenseModal());
      }
      const expCloseBtn = document.getElementById('exp-modal-close-btn');
      const expCancelBtn = document.getElementById('exp-modal-cancel-btn');
      [expCloseBtn, expCancelBtn].forEach(b => {
        if (b) b.addEventListener('click', () => this.closeAddExpenseModal());
      });
      const expForm = document.getElementById('finance-expense-form');
      if (expForm) {
        expForm.addEventListener('submit', (e) => this.submitExpense(e));
      }
      const expApplyBtn = document.getElementById('finance-exp-apply-btn');
      if (expApplyBtn) {
        expApplyBtn.addEventListener('click', () => this.loadExpensesData());
      }
      const expResetBtn = document.getElementById('finance-exp-reset-btn');
      if (expResetBtn) {
        expResetBtn.addEventListener('click', () => {
          const df = document.getElementById('finance-exp-date-from');
          const dt = document.getElementById('finance-exp-date-to');
          const cat = document.getElementById('finance-exp-category-filter');
          if (df) df.value = '';
          if (dt) dt.value = '';
          if (cat) cat.value = 'all';
          this.loadExpensesData();
        });
      }
      const expRefresh = document.getElementById('finance-expenses-refresh-btn');
      if (expRefresh) {
        expRefresh.addEventListener('click', () => this.loadExpensesData());
      }

      // 7. Employees Search & Refresh
      const empSearch = document.getElementById('finance-emp-search-input');
      if (empSearch) {
        let t = null;
        empSearch.addEventListener('input', () => {
          clearTimeout(t);
          t = setTimeout(() => this.filterEmployeesClientSide(), 250);
        });
      }
      const empRefresh = document.getElementById('finance-emp-refresh-btn');
      if (empRefresh) {
        empRefresh.addEventListener('click', () => this.loadEmployeesData());
      }
    }

    /* ------------------------------------------------------------------------
       1. HISTORY VIEW LOGIC (/finance/history)
       ------------------------------------------------------------------------ */
    async loadHistoryData() {
      const search = document.getElementById('finance-hist-search-input')?.value?.trim() || '';
      const type = document.getElementById('finance-hist-type-select')?.value || 'all';
      const po = document.getElementById('finance-hist-po-select')?.value || 'all';
      const supplier = document.getElementById('finance-hist-supplier-select')?.value || 'all';
      const fromDate = document.getElementById('finance-hist-date-from')?.value || '';
      const toDate = document.getElementById('finance-hist-date-to')?.value || '';

      const loadingEl = document.getElementById('finance-hist-loading');
      const emptyEl = document.getElementById('finance-hist-empty');
      const tbody = document.getElementById('finance-hist-tbody');
      const badge = document.getElementById('finance-hist-count-badge');

      if (loadingEl) loadingEl.style.display = 'flex';
      if (emptyEl) emptyEl.style.display = 'none';

      try {
        const res = await window.ChatApi.getFinanceHistory({
          search,
          type: type === 'all' ? '' : type,
          po: po === 'all' ? '' : po,
          supplier: supplier === 'all' ? '' : supplier,
          fromDate,
          toDate,
          page: this.histPage || 1,
          pageSize: this.histPageSize || 15
        });

        if (loadingEl) loadingEl.style.display = 'none';

        if (res && res.success && res.data) {
          const items = res.data.items || [];
          this.histTotalRecords = res.data.totalRecords || 0;
          this.histTotalPages = res.data.totalPages || 1;
          this.histPage = res.data.currentPage || 1;

          if (badge) badge.textContent = `${this.histTotalRecords.toLocaleString()} معاملة`;
          this.renderHistoryTable(items);

          if (res.data.availablePos && this.histPoPopulated !== true) {
            this.populateHistPoDropdown(res.data.availablePos);
          }
          if (res.data.availableSuppliers && this.histSuppliersPopulated !== true) {
            this.populateHistSupplierDropdown(res.data.availableSuppliers);
          }
          this.updateHistPagination();
        } else {
          if (emptyEl) emptyEl.style.display = 'flex';
          if (tbody) tbody.innerHTML = '';
        }
      } catch (err) {
        if (loadingEl) loadingEl.style.display = 'none';
        if (emptyEl) emptyEl.style.display = 'flex';
        console.error('Failed to load finance history:', err);
      }
    }

    populateHistPoDropdown(pos) {
      const select = document.getElementById('finance-hist-po-select');
      if (!select || !Array.isArray(pos)) return;
      this.histPoPopulated = true;
      const currentVal = select.value || 'all';
      select.innerHTML = '<option value="all">-- كافة أوامر الشراء --</option>';
      pos.forEach(p => {
        const opt = document.createElement('option');
        opt.value = p;
        opt.textContent = p;
        select.appendChild(opt);
      });
      select.value = currentVal;
    }

    populateHistSupplierDropdown(suppliers) {
      const select = document.getElementById('finance-hist-supplier-select');
      if (!select || !Array.isArray(suppliers)) return;
      this.histSuppliersPopulated = true;
      const currentVal = select.value || 'all';
      select.innerHTML = '<option value="all">-- كافة الموردين --</option>';
      suppliers.forEach(s => {
        const opt = document.createElement('option');
        opt.value = s;
        opt.textContent = s;
        select.appendChild(opt);
      });
      select.value = currentVal;
    }

    renderHistoryTable(items) {
      const tbody = document.getElementById('finance-hist-tbody');
      const emptyEl = document.getElementById('finance-hist-empty');
      if (!tbody) return;

      if (!items || items.length === 0) {
        tbody.innerHTML = '';
        if (emptyEl) emptyEl.style.display = 'flex';
        return;
      }
      if (emptyEl) emptyEl.style.display = 'none';

      tbody.innerHTML = items.map(t => {
        const isReceived = (t.direction === 'إيداع' || t.direction === 'مستلم' || t.direction === 'وارد');
        const dirBadge = isReceived
          ? `<span class="finance-status-badge badge-positive">${this.escapeHtml(t.direction)}</span>`
          : `<span class="finance-status-badge badge-waste">${this.escapeHtml(t.direction)}</span>`;

        return `
          <tr>
            <td><strong dir="ltr">${this.formatDate(t.transactionDate)}</strong></td>
            <td><span class="finance-status-badge badge-neutral">${this.escapeHtml(t.transactionTypeArabic || t.transactionType)}</span></td>
            <td><span dir="ltr" style="font-family: monospace;">${this.escapeHtml(t.transactionNumber || '-')}</span></td>
            <td>${t.poNumber ? `<span dir="ltr" style="font-weight: 700; color: #D97706;">${this.escapeHtml(t.poNumber)}</span>` : '<span style="color: #94A3B8;">-</span>'}</td>
            <td><strong>${this.escapeHtml(t.supplierOrEntity || '-')}</strong></td>
            <td><strong dir="ltr" style="font-family: monospace; color: ${isReceived ? '#059669' : '#DC2626'};">${this.formatCurrency(t.amount)}</strong></td>
            <td>${dirBadge}</td>
            <td><span class="finance-status-badge badge-neutral">${this.escapeHtml(t.status || 'معتمد')}</span></td>
            <td><span style="font-size: 0.85rem; color: #475569;">${this.escapeHtml(t.createdByUser || '-')}</span></td>
            <td><span style="font-size: 0.825rem; color: #64748B;">${this.escapeHtml(t.description || '-')}</span></td>
          </tr>
        `;
      }).join('');
    }

    updateHistPagination() {
      const info = document.getElementById('finance-hist-pagination-info');
      const prevBtn = document.getElementById('finance-hist-prev-page');
      const nextBtn = document.getElementById('finance-hist-next-page');
      const pagesContainer = document.getElementById('finance-hist-page-numbers');

      const p = this.histPage || 1;
      const tp = this.histTotalPages || 1;

      if (info) info.textContent = `صفحة ${p} من ${tp} (إجمالي ${this.histTotalRecords} معاملة)`;
      if (prevBtn) prevBtn.disabled = (p <= 1);
      if (nextBtn) nextBtn.disabled = (p >= tp);

      if (pagesContainer) {
        pagesContainer.innerHTML = '';
        for (let i = 1; i <= Math.min(tp, 7); i++) {
          const btn = document.createElement('button');
          btn.type = 'button';
          btn.className = `finance-page-btn ${i === p ? 'active' : ''}`;
          btn.textContent = i;
          btn.addEventListener('click', () => {
            this.histPage = i;
            this.loadHistoryData();
          });
          pagesContainer.appendChild(btn);
        }
      }
    }

    /* ------------------------------------------------------------------------
       2. SUPPLIERS VIEW LOGIC (/finance/suppliers)
       ------------------------------------------------------------------------ */
    async loadSuppliersData() {
      const loadingEl = document.getElementById('finance-suppliers-loading');
      const emptyEl = document.getElementById('finance-suppliers-empty');
      const tbody = document.getElementById('finance-suppliers-tbody');
      const countBadge = document.getElementById('finance-suppliers-count');

      if (loadingEl) loadingEl.style.display = 'flex';
      if (emptyEl) emptyEl.style.display = 'none';

      try {
        const res = await window.ChatApi.getFinanceSuppliers();
        if (loadingEl) loadingEl.style.display = 'none';

        if (res && res.success && Array.isArray(res.data)) {
          this.suppliersRawList = res.data;
          if (countBadge) countBadge.textContent = `${res.data.length} مورد`;
          this.renderSuppliersTable(res.data);
        } else {
          if (emptyEl) emptyEl.style.display = 'flex';
          if (tbody) tbody.innerHTML = '';
        }
      } catch (err) {
        if (loadingEl) loadingEl.style.display = 'none';
        if (emptyEl) emptyEl.style.display = 'flex';
        console.error('Failed to load suppliers:', err);
      }
    }

    filterSuppliersClientSide() {
      if (!this.suppliersRawList) return;
      const term = document.getElementById('finance-suppliers-search-input')?.value?.trim().toLowerCase() || '';
      if (!term) {
        this.renderSuppliersTable(this.suppliersRawList);
        return;
      }
      const filtered = this.suppliersRawList.filter(s =>
        (s.supplierName && s.supplierName.toLowerCase().includes(term)) ||
        (s.materials && s.materials.toLowerCase().includes(term))
      );
      this.renderSuppliersTable(filtered);
    }

    renderSuppliersTable(items) {
      const tbody = document.getElementById('finance-suppliers-tbody');
      const emptyEl = document.getElementById('finance-suppliers-empty');
      if (!tbody) return;

      if (!items || items.length === 0) {
        tbody.innerHTML = '';
        if (emptyEl) emptyEl.style.display = 'flex';
        return;
      }
      if (emptyEl) emptyEl.style.display = 'none';

      tbody.innerHTML = items.map(s => `
        <tr>
          <td><strong style="color: #0F172A; font-size: 0.95rem;">${this.escapeHtml(s.supplierName)}</strong></td>
          <td><span style="font-size: 0.85rem; color: #475569;">${this.escapeHtml(s.materials || '-')}</span></td>
          <td><span class="finance-status-badge badge-neutral">${s.purchaseOrdersCount} عقود</span></td>
          <td><strong dir="ltr">${this.formatWeight(s.totalContractedQuantity)} طن</strong></td>
          <td><strong dir="ltr" style="color: #059669;">${this.formatWeight(s.totalDeliveredQuantity)} طن</strong></td>
          <td><strong dir="ltr" style="color: #D97706;">${this.formatWeight(s.totalRemainingQuantity)} طن</strong></td>
          <td><strong dir="ltr" style="font-family: monospace;">${this.formatCurrency(s.totalFinancialValue)}</strong></td>
          <td><strong dir="ltr" style="color: #059669;">${this.formatCurrency(s.totalPaidAmount)}</strong></td>
          <td><strong dir="ltr" style="color: #DC2626;">${this.formatCurrency(s.totalOutstandingAmount)}</strong></td>
          <td>
            <button type="button" class="finance-btn-table-action" onclick="window.FinanceManagementApp.navigateTo('/finance/suppliers/' + encodeURIComponent('${this.escapeHtml(s.supplierName)}'))">
              <span>عرض التفاصيل</span>
              <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.5"><polyline points="9 18 15 12 9 6"/></svg>
            </button>
          </td>
        </tr>
      `).join('');
    }

    async loadSupplierDetail(id) {
      if (!id) return;
      const nameEl = document.getElementById('supplier-detail-name');
      if (nameEl) nameEl.textContent = id;

      try {
        const res = await window.ChatApi.getFinanceSupplierDetail(id);
        if (res && res.success && res.data) {
          const d = res.data;
          if (nameEl) nameEl.textContent = d.supplierName;
          const matSub = document.getElementById('supplier-detail-materials-sub');
          if (matSub) matSub.textContent = `المواد: ${d.materials || '-'}`;
          const poSub = document.getElementById('supplier-detail-po-count-sub');
          if (poSub) poSub.textContent = `عدد العقود: ${d.purchaseOrdersCount}`;

          const cq = document.getElementById('supplier-detail-contracted-qty');
          if (cq) cq.textContent = this.formatWeight(d.totalContractedQuantity) + ' طن';
          const dq = document.getElementById('supplier-detail-delivered-qty');
          if (dq) dq.textContent = this.formatWeight(d.totalDeliveredQuantity) + ' طن';
          const rq = document.getElementById('supplier-detail-remaining-qty');
          if (rq) rq.textContent = this.formatWeight(d.totalRemainingQuantity) + ' طن';

          const tv = document.getElementById('supplier-detail-total-val');
          if (tv) tv.textContent = this.formatCurrency(d.totalFinancialValue);
          const pa = document.getElementById('supplier-detail-paid-amt');
          if (pa) pa.textContent = this.formatCurrency(d.totalPaidAmount);
          const oa = document.getElementById('supplier-detail-outstanding-amt');
          if (oa) oa.textContent = this.formatCurrency(d.totalOutstandingAmount);

          const posTbody = document.getElementById('supplier-detail-pos-tbody');
          if (posTbody) {
            const pos = d.purchaseOrders || [];
            if (pos.length === 0) {
              posTbody.innerHTML = '<tr><td colspan="10" style="text-align:center; padding:1.5rem; color:#94A3B8;">لا توجد أوامر شراء مسجلة</td></tr>';
            } else {
              posTbody.innerHTML = pos.map(p => `
                <tr>
                  <td><strong dir="ltr" style="font-family: monospace; color: #D97706;">${this.escapeHtml(p.poNumber)}</strong></td>
                  <td><strong dir="ltr">${this.formatWeight(p.expectedQuantity)} طن</strong></td>
                  <td><strong dir="ltr">${this.formatCurrency(p.tonPrice)}</strong></td>
                  <td><strong dir="ltr">${this.formatCurrency(p.expectedPrice)}</strong></td>
                  <td><strong dir="ltr" style="color: #059669;">${this.formatWeight(p.deliveredQuantity)} طن</strong></td>
                  <td><strong dir="ltr">${this.formatWeight(p.remainingQuantity)} طن</strong></td>
                  <td><strong dir="ltr" style="color: #DC2626;">${this.formatWeight(p.totalWasteQuantity)} طن</strong></td>
                  <td><span class="finance-status-badge badge-neutral">${Number(p.performancePercentage || 0).toFixed(1)}%</span></td>
                  <td><span class="finance-status-badge ${p.paymentStatus === 'مدفوع بالكامل' ? 'badge-positive' : (p.paymentStatus === 'مدفوع جزئياً' ? 'badge-neutral' : 'badge-waste')}">${this.escapeHtml(p.paymentStatus || 'غير مدفوع')}</span></td>
                  <td>
                    <button type="button" class="finance-btn-table-action" onclick="window.FinanceManagementApp.navigateTo('/finance/purchase-orders/' + encodeURIComponent('${this.escapeHtml(p.poNumber)}'))">
                      <span>عرض</span>
                    </button>
                  </td>
                </tr>
              `).join('');
            }
          }

          const delTbody = document.getElementById('supplier-detail-deliveries-tbody');
          if (delTbody) {
            const dels = d.recentDeliveries || [];
            if (dels.length === 0) {
              delTbody.innerHTML = '<tr><td colspan="8" style="text-align:center; padding:1.5rem; color:#94A3B8;">لا توجد حركات نقل مسجلة لهذا المورد</td></tr>';
            } else {
              delTbody.innerHTML = dels.map(t => `
                <tr>
                  <td><span dir="ltr" style="font-family: monospace; font-weight: 700;">#${t.transactionNumber}</span></td>
                  <td><strong dir="ltr">${this.formatDate(t.transactionDate)}</strong></td>
                  <td><strong dir="ltr">${this.escapeHtml(t.carNumber || '-')}</strong></td>
                  <td>${this.escapeHtml(t.driverName || '-')}</td>
                  <td>${this.escapeHtml(t.materialType || '-')}</td>
                  <td><strong dir="ltr">${this.formatWeight(t.netWeight)} طن</strong></td>
                  <td><strong dir="ltr" style="color: #DC2626;">${this.formatWeight(t.wasteQuantity)} طن</strong></td>
                  <td><span class="finance-status-badge badge-neutral">${this.escapeHtml(t.receivingStatus || 'مستلم')}</span></td>
                </tr>
              `).join('');
            }
          }
        }
      } catch (err) {
        console.error('Failed to load supplier detail:', err);
      }
    }

    /* ------------------------------------------------------------------------
       3. PURCHASE ORDERS VIEW LOGIC (/finance/purchase-orders)
       ------------------------------------------------------------------------ */
    async loadAllPurchaseOrdersData() {
      const loadingEl = document.getElementById('finance-pos-all-loading');
      const emptyEl = document.getElementById('finance-pos-all-empty');
      const tbody = document.getElementById('finance-pos-all-tbody');
      const countBadge = document.getElementById('finance-pos-all-count');

      if (loadingEl) loadingEl.style.display = 'flex';
      if (emptyEl) emptyEl.style.display = 'none';

      try {
        const res = await window.ChatApi.getFinancePurchaseOrders();
        if (loadingEl) loadingEl.style.display = 'none';

        if (res && res.success && Array.isArray(res.data)) {
          this.posRawList = res.data;
          if (countBadge) countBadge.textContent = `${res.data.length} أمر شراء`;
          this.renderAllPurchaseOrdersTable(res.data);
        } else {
          if (emptyEl) emptyEl.style.display = 'flex';
          if (tbody) tbody.innerHTML = '';
        }
      } catch (err) {
        if (loadingEl) loadingEl.style.display = 'none';
        if (emptyEl) emptyEl.style.display = 'flex';
        console.error('Failed to load all purchase orders:', err);
      }
    }

    filterPurchaseOrdersClientSide() {
      if (!this.posRawList) return;
      const term = document.getElementById('finance-pos-search-input')?.value?.trim().toLowerCase() || '';
      const statusFilter = document.getElementById('finance-pos-status-select')?.value || 'all';

      const filtered = this.posRawList.filter(p => {
        const matchesTerm = !term ||
          (p.poNumber && p.poNumber.toLowerCase().includes(term)) ||
          (p.supplier && p.supplier.toLowerCase().includes(term));

        let matchesStatus = true;
        if (statusFilter === 'unpaid') matchesStatus = (p.paymentStatus === 'غير مدفوع' || !p.paymentStatus);
        else if (statusFilter === 'partial') matchesStatus = (p.paymentStatus === 'مدفوع جزئياً');
        else if (statusFilter === 'paid') matchesStatus = (p.paymentStatus === 'مدفوع بالكامل');

        return matchesTerm && matchesStatus;
      });

      this.renderAllPurchaseOrdersTable(filtered);
    }

    renderAllPurchaseOrdersTable(items) {
      const tbody = document.getElementById('finance-pos-all-tbody');
      const emptyEl = document.getElementById('finance-pos-all-empty');
      if (!tbody) return;

      if (!items || items.length === 0) {
        tbody.innerHTML = '';
        if (emptyEl) emptyEl.style.display = 'flex';
        return;
      }
      if (emptyEl) emptyEl.style.display = 'none';

      tbody.innerHTML = items.map(p => `
        <tr>
          <td><strong dir="ltr" style="font-family: monospace; color: #D97706; font-size: 0.95rem;">${this.escapeHtml(p.poNumber)}</strong></td>
          <td><strong>${this.escapeHtml(p.supplier || '-')}</strong></td>
          <td>${this.escapeHtml(p.material || 'بودرة')}</td>
          <td>${this.escapeHtml(p.destination || 'المصنع')}</td>
          <td><strong dir="ltr">${this.formatWeight(p.quantity)} طن</strong></td>
          <td><strong dir="ltr">${this.formatCurrency(p.tonPrice)}</strong></td>
          <td><strong dir="ltr">${this.formatCurrency(p.totalValue)}</strong></td>
          <td><strong dir="ltr" style="color: #059669;">${this.formatWeight(p.deliveredQuantity)} طن</strong></td>
          <td><strong dir="ltr" style="color: #D97706;">${this.formatWeight(p.remainingQuantity)} طن</strong></td>
          <td><strong dir="ltr" style="color: #DC2626;">${this.formatWeight(p.totalWasteQuantity)} طن</strong></td>
          <td><span class="finance-status-badge badge-neutral">${Number(p.performancePercentage || 0).toFixed(1)}%</span></td>
          <td><span class="finance-status-badge ${p.paymentStatus === 'مدفوع بالكامل' ? 'badge-positive' : (p.paymentStatus === 'مدفوع جزئياً' ? 'badge-neutral' : 'badge-waste')}">${this.escapeHtml(p.paymentStatus || 'غير مدفوع')}</span></td>
          <td><span dir="ltr">${this.formatDate(p.orderDate)}</span></td>
          <td>
            <button type="button" class="finance-btn-table-action" onclick="window.FinanceManagementApp.navigateTo('/finance/purchase-orders/' + encodeURIComponent('${this.escapeHtml(p.poNumber)}'))">
              <span>عرض التفاصيل</span>
              <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.5"><polyline points="9 18 15 12 9 6"/></svg>
            </button>
          </td>
        </tr>
      `).join('');
    }

    /* ------------------------------------------------------------------------
       5. BANK VIEW LOGIC (/finance/bank)
       ------------------------------------------------------------------------ */
    async loadBankData() {
      const loadingEl = document.getElementById('finance-bank-loading');
      const emptyEl = document.getElementById('finance-bank-empty');
      const tbody = document.getElementById('finance-bank-tbody');

      if (loadingEl) loadingEl.style.display = 'flex';
      if (emptyEl) emptyEl.style.display = 'none';

      try {
        const res = await window.ChatApi.getFinanceBankSummary();
        if (loadingEl) loadingEl.style.display = 'none';

        if (res && res.success && res.data) {
          const d = res.data;
          const balEl = document.getElementById('finance-bank-balance-val');
          if (balEl) balEl.textContent = this.formatCurrency(d.currentBalance);

          const depEl = document.getElementById('finance-bank-deposits-val');
          if (depEl) depEl.textContent = this.formatCurrency(d.totalReceived);

          const withEl = document.getElementById('finance-bank-withdrawals-val');
          if (withEl) withEl.textContent = this.formatCurrency(d.totalSent);

          const cntEl = document.getElementById('finance-bank-count-val');
          if (cntEl) cntEl.textContent = `${d.totalTransactionsCount} معاملة`;

          this.renderBankTable(d.transactions || []);
        } else {
          if (emptyEl) emptyEl.style.display = 'flex';
          if (tbody) tbody.innerHTML = '';
        }
      } catch (err) {
        if (loadingEl) loadingEl.style.display = 'none';
        if (emptyEl) emptyEl.style.display = 'flex';
        console.error('Failed to load bank summary:', err);
      }
    }

    renderBankTable(items) {
      const tbody = document.getElementById('finance-bank-tbody');
      const emptyEl = document.getElementById('finance-bank-empty');
      if (!tbody) return;

      if (!items || items.length === 0) {
        tbody.innerHTML = '';
        if (emptyEl) emptyEl.style.display = 'flex';
        return;
      }
      if (emptyEl) emptyEl.style.display = 'none';

      tbody.innerHTML = items.map(t => {
        const isDep = (t.direction === 'إيداع');
        return `
          <tr>
            <td><strong dir="ltr">${this.formatDate(t.transactionDate)}</strong></td>
            <td><span class="finance-status-badge badge-neutral">${this.escapeHtml(t.transactionType)}</span></td>
            <td><strong dir="ltr" style="font-family: monospace; color: ${isDep ? '#059669' : '#DC2626'};">${this.formatCurrency(t.amount)}</strong></td>
            <td><span class="finance-status-badge ${isDep ? 'badge-positive' : 'badge-waste'}">${isDep ? 'إيداع / Received' : 'صرف / Sent'}</span></td>
            <td><strong>${this.escapeHtml(t.bankName)}</strong></td>
            <td><span dir="ltr" style="font-family: monospace;">${this.escapeHtml(t.referenceNumber || '-')}</span></td>
            <td>${this.escapeHtml(t.description)}</td>
            <td><span style="font-size: 0.85rem; color: #475569;">${this.escapeHtml(t.createdBy || '-')}</span></td>
            <td><span style="font-size: 0.825rem; color: #64748B;">${this.escapeHtml(t.notes || '-')}</span></td>
          </tr>
        `;
      }).join('');
    }

    openAddBankModal() {
      const backdrop = document.getElementById('finance-add-bank-modal-backdrop');
      if (backdrop) backdrop.style.display = 'flex';
      const dateInp = document.getElementById('bank-trans-date');
      if (dateInp && !dateInp.value) {
        const now = new Date();
        const y = now.getFullYear();
        const m = String(now.getMonth() + 1).padStart(2, '0');
        const d = String(now.getDate()).padStart(2, '0');
        dateInp.value = `${y}-${m}-${d}`;
      }
    }

    closeAddBankModal() {
      const backdrop = document.getElementById('finance-add-bank-modal-backdrop');
      if (backdrop) backdrop.style.display = 'none';
      const form = document.getElementById('finance-bank-form');
      if (form) form.reset();
    }

    async submitBankTransaction(e) {
      e.preventDefault();
      const dateVal = document.getElementById('bank-trans-date')?.value;
      const bankVal = document.getElementById('bank-trans-bank-name')?.value?.trim();
      const typeVal = document.getElementById('bank-trans-type')?.value;
      const dirVal = document.getElementById('bank-trans-direction')?.value;
      const amtVal = parseFloat(document.getElementById('bank-trans-amount')?.value);
      const refVal = document.getElementById('bank-trans-ref')?.value?.trim();
      const descVal = document.getElementById('bank-trans-desc')?.value?.trim();
      const notesVal = document.getElementById('bank-trans-notes')?.value?.trim();

      if (!dateVal || !bankVal || !typeVal || !dirVal || isNaN(amtVal) || amtVal <= 0 || !descVal) {
        alert('يرجى ملء جميع الحقول الإلزامية والتأكد من إدخال مبلغ صحيح.');
        return;
      }

      const submitBtn = document.getElementById('bank-modal-submit-btn');
      if (submitBtn) {
        submitBtn.disabled = true;
        submitBtn.textContent = 'جاري الحفظ...';
      }

      try {
        const payload = {
          transactionDate: dateVal,
          bankName: bankVal,
          transactionType: typeVal,
          direction: dirVal,
          amount: amtVal,
          referenceNumber: refVal,
          description: descVal,
          notes: notesVal
        };

        const res = await window.ChatApi.createFinanceBankTransaction(payload);
        if (res && res.success) {
          this.closeAddBankModal();
          this.loadBankData();
        } else {
          alert(res?.message || 'تعذر حفظ المعاملة البنكية.');
        }
      } catch (err) {
        console.error('Failed to submit bank transaction:', err);
        alert('حدث خطأ أثناء الاتصال بالخادم.');
      } finally {
        if (submitBtn) {
          submitBtn.disabled = false;
          submitBtn.textContent = 'حفظ المعاملة البنكية';
        }
      }
    }

    /* ------------------------------------------------------------------------
       6. EXPENSES VIEW LOGIC (/finance/expenses)
       ------------------------------------------------------------------------ */
    async loadExpensesData() {
      const loadingEl = document.getElementById('finance-expenses-loading');
      const emptyEl = document.getElementById('finance-expenses-empty');
      const tbody = document.getElementById('finance-expenses-tbody');

      const catVal = document.getElementById('finance-exp-category-filter')?.value || 'all';
      const fromVal = document.getElementById('finance-exp-date-from')?.value || '';
      const toVal = document.getElementById('finance-exp-date-to')?.value || '';

      if (loadingEl) loadingEl.style.display = 'flex';
      if (emptyEl) emptyEl.style.display = 'none';

      try {
        const res = await window.ChatApi.getFinanceExpenses({
          category: catVal === 'all' ? '' : catVal,
          fromDate: fromVal,
          toDate: toVal
        });
        if (loadingEl) loadingEl.style.display = 'none';

        if (res && res.success && res.data) {
          const d = res.data;
          const totEl = document.getElementById('finance-exp-total-val');
          if (totEl) totEl.textContent = this.formatCurrency(d.totalExpenses);

          const todEl = document.getElementById('finance-exp-today-val');
          if (todEl) todEl.textContent = this.formatCurrency(d.todayExpenses);

          const rngEl = document.getElementById('finance-exp-range-val');
          if (rngEl) rngEl.textContent = this.formatCurrency(d.rangeExpenses);

          this.renderExpenseCategories(d.categoryBreakdown || []);
          this.renderExpensesTable(d.expenses || []);
        } else {
          if (emptyEl) emptyEl.style.display = 'flex';
          if (tbody) tbody.innerHTML = '';
        }
      } catch (err) {
        if (loadingEl) loadingEl.style.display = 'none';
        if (emptyEl) emptyEl.style.display = 'flex';
        console.error('Failed to load expenses:', err);
      }
    }

    renderExpenseCategories(categories) {
      const container = document.getElementById('finance-exp-categories-grid');
      if (!container) return;

      if (!categories || categories.length === 0) {
        container.innerHTML = '<div style="color: #94A3B8; padding: 1rem;">لا توجد مصاريف مصنفة حتى الآن.</div>';
        return;
      }

      container.innerHTML = categories.map(c => `
        <div class="finance-forecast-tile">
          <span class="finance-forecast-label">${this.escapeHtml(c.category)}</span>
          <span class="finance-forecast-val" style="color: #EA580C;">${this.formatCurrency(c.totalAmount)}</span>
          <span class="finance-forecast-sub">${c.count} معاملة (${Number(c.percentage || 0).toFixed(1)}%)</span>
        </div>
      `).join('');
    }

    renderExpensesTable(items) {
      const tbody = document.getElementById('finance-expenses-tbody');
      const emptyEl = document.getElementById('finance-expenses-empty');
      if (!tbody) return;

      if (!items || items.length === 0) {
        tbody.innerHTML = '';
        if (emptyEl) emptyEl.style.display = 'flex';
        return;
      }
      if (emptyEl) emptyEl.style.display = 'none';

      tbody.innerHTML = items.map(x => `
        <tr>
          <td><strong dir="ltr">${this.formatDate(x.expenseDate)}</strong></td>
          <td><span class="finance-status-badge badge-neutral">${this.escapeHtml(x.category)}</span></td>
          <td><strong dir="ltr" style="font-family: monospace; color: #EA580C;">${this.formatCurrency(x.amount)}</strong></td>
          <td><strong>${this.escapeHtml(x.title)}</strong></td>
          <td>${this.escapeHtml(x.beneficiary || '-')}</td>
          <td><span style="font-size: 0.85rem; color: #475569;">${this.escapeHtml(x.paymentMethod || 'نقدي')}</span></td>
          <td><span dir="ltr" style="font-family: monospace;">${this.escapeHtml(x.referenceNumber || '-')}</span></td>
          <td><span style="font-size: 0.85rem; color: #475569;">${this.escapeHtml(x.createdBy || '-')}</span></td>
          <td><span style="font-size: 0.825rem; color: #64748B;">${this.escapeHtml(x.notes || '-')}</span></td>
        </tr>
      `).join('');
    }

    openAddExpenseModal() {
      const backdrop = document.getElementById('finance-add-expense-modal-backdrop');
      if (backdrop) backdrop.style.display = 'flex';
      const dateInp = document.getElementById('exp-date');
      if (dateInp && !dateInp.value) {
        const now = new Date();
        const y = now.getFullYear();
        const m = String(now.getMonth() + 1).padStart(2, '0');
        const d = String(now.getDate()).padStart(2, '0');
        dateInp.value = `${y}-${m}-${d}`;
      }
    }

    closeAddExpenseModal() {
      const backdrop = document.getElementById('finance-add-expense-modal-backdrop');
      if (backdrop) backdrop.style.display = 'none';
      const form = document.getElementById('finance-expense-form');
      if (form) form.reset();
    }

    async submitExpense(e) {
      e.preventDefault();
      const dateVal = document.getElementById('exp-date')?.value;
      const catVal = document.getElementById('exp-category')?.value;
      const amtVal = parseFloat(document.getElementById('exp-amount')?.value);
      const titleVal = document.getElementById('exp-title')?.value?.trim();
      const benVal = document.getElementById('exp-beneficiary')?.value?.trim();
      const payVal = document.getElementById('exp-pay-method')?.value;
      const refVal = document.getElementById('exp-ref')?.value?.trim();
      const notesVal = document.getElementById('exp-notes')?.value?.trim();

      if (!dateVal || !catVal || isNaN(amtVal) || amtVal <= 0 || !titleVal) {
        alert('يرجى ملء جميع الحقول الإلزامية والتأكد من إدخال مبلغ صحيح.');
        return;
      }

      const submitBtn = document.getElementById('exp-modal-submit-btn');
      if (submitBtn) {
        submitBtn.disabled = true;
        submitBtn.textContent = 'جاري الحفظ...';
      }

      try {
        const payload = {
          expenseDate: dateVal,
          category: catVal,
          amount: amtVal,
          title: titleVal,
          beneficiary: benVal,
          paymentMethod: payVal,
          referenceNumber: refVal,
          notes: notesVal
        };

        const res = await window.ChatApi.createFinanceExpense(payload);
        if (res && res.success) {
          this.closeAddExpenseModal();
          this.loadExpensesData();
        } else {
          alert(res?.message || 'تعذر حفظ المصروف.');
        }
      } catch (err) {
        console.error('Failed to submit expense:', err);
        alert('حدث خطأ أثناء الاتصال بالخادم.');
      } finally {
        if (submitBtn) {
          submitBtn.disabled = false;
          submitBtn.textContent = 'حفظ المصروف';
        }
      }
    }

    /* ------------------------------------------------------------------------
       7. EMPLOYEES VIEW LOGIC (/finance/employees)
       ------------------------------------------------------------------------ */
    async loadEmployeesData() {
      const loadingEl = document.getElementById('finance-emp-loading');
      const emptyEl = document.getElementById('finance-emp-empty');
      const tbody = document.getElementById('finance-employees-tbody');
      const countBadge = document.getElementById('finance-emp-count');

      if (loadingEl) loadingEl.style.display = 'flex';
      if (emptyEl) emptyEl.style.display = 'none';

      try {
        const res = await window.ChatApi.getFinanceEmployees();
        if (loadingEl) loadingEl.style.display = 'none';

        if (res && res.success && Array.isArray(res.data)) {
          this.employeesRawList = res.data;
          if (countBadge) countBadge.textContent = `${res.data.length} موظف`;
          this.renderEmployeesTable(res.data);
        } else {
          if (emptyEl) emptyEl.style.display = 'flex';
          if (tbody) tbody.innerHTML = '';
        }
      } catch (err) {
        if (loadingEl) loadingEl.style.display = 'none';
        if (emptyEl) emptyEl.style.display = 'flex';
        console.error('Failed to load employees:', err);
      }
    }

    filterEmployeesClientSide() {
      if (!this.employeesRawList) return;
      const term = document.getElementById('finance-emp-search-input')?.value?.trim().toLowerCase() || '';
      if (!term) {
        this.renderEmployeesTable(this.employeesRawList);
        return;
      }
      const filtered = this.employeesRawList.filter(e =>
        (e.fullName && e.fullName.toLowerCase().includes(term)) ||
        (e.username && e.username.toLowerCase().includes(term)) ||
        (e.department && e.department.toLowerCase().includes(term)) ||
        (e.position && e.position.toLowerCase().includes(term))
      );
      this.renderEmployeesTable(filtered);
    }

    renderEmployeesTable(items) {
      const tbody = document.getElementById('finance-employees-tbody');
      const emptyEl = document.getElementById('finance-emp-empty');
      if (!tbody) return;

      if (!items || items.length === 0) {
        tbody.innerHTML = '';
        if (emptyEl) emptyEl.style.display = 'flex';
        return;
      }
      if (emptyEl) emptyEl.style.display = 'none';

      tbody.innerHTML = items.map(emp => {
        const salDisplay = emp.hasSalaryRecord
          ? `<strong dir="ltr" style="color:#059669;">${this.formatCurrency(emp.netSalary || emp.basicSalary)}</strong>`
          : '<span style="color:#94A3B8; font-size:0.8rem;">غير مسجل</span>';

        const attDisplay = emp.hasAttendanceRecord
          ? `<span class="finance-status-badge badge-positive">حضور ${Number(emp.attendanceRate || 0).toFixed(0)}% (${emp.daysPresent} يوم)</span>`
          : '<span style="color:#94A3B8; font-size:0.8rem;">جاهز للربط</span>';

        const lendDisplay = emp.hasLendingRecord
          ? `<strong dir="ltr" style="color:#DC2626;">${this.formatCurrency(emp.remainingLending)}</strong>`
          : '<span style="color:#94A3B8; font-size:0.8rem;">لا توجد سلف</span>';

        return `
          <tr>
            <td><strong style="color: #0F172A; font-size: 0.95rem;">${this.escapeHtml(emp.fullName)}</strong></td>
            <td><span dir="ltr" style="font-family: monospace;">${this.escapeHtml(emp.username)}</span></td>
            <td><span class="finance-status-badge badge-neutral">${this.escapeHtml(emp.department || 'عام')}</span></td>
            <td><span style="font-size: 0.85rem; color: #475569;">${this.escapeHtml(emp.position || emp.role)}</span></td>
            <td>${salDisplay}</td>
            <td>${attDisplay}</td>
            <td>${lendDisplay}</td>
            <td><span class="finance-status-badge badge-positive">${this.escapeHtml(emp.financialStatus || 'نشط')}</span></td>
            <td>
              <button type="button" class="finance-btn-table-action" onclick="window.FinanceManagementApp.navigateTo('/finance/employees/' + encodeURIComponent('${emp.id}'))">
                <span>عرض التفاصيل</span>
                <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.5"><polyline points="9 18 15 12 9 6"/></svg>
              </button>
            </td>
          </tr>
        `;
      }).join('');
    }

    async loadEmployeeDetail(id) {
      if (!id) return;
      try {
        const res = await window.ChatApi.getFinanceEmployeeDetail(id);
        if (res && res.success && res.data) {
          const emp = res.data;
          const nameEl = document.getElementById('emp-detail-name');
          const fnEl = document.getElementById('emp-detail-fullname');
          const unEl = document.getElementById('emp-detail-username');
          const dpEl = document.getElementById('emp-detail-dept');
          const roEl = document.getElementById('emp-detail-role');
          const stEl = document.getElementById('emp-detail-status');

          if (nameEl) nameEl.textContent = emp.fullName;
          if (fnEl) fnEl.textContent = emp.fullName;
          if (unEl) unEl.textContent = emp.username;
          if (dpEl) dpEl.textContent = `القسم: ${emp.department || '-'}`;
          if (roEl) roEl.textContent = `الوظيفة: ${emp.position || emp.role}`;
          if (stEl) stEl.textContent = `الحالة: ${emp.financialStatus || 'نشط'}`;

          const bsEl = document.getElementById('emp-detail-basic-salary');
          if (bsEl) bsEl.textContent = emp.hasSalaryRecord ? this.formatCurrency(emp.basicSalary) : 'غير محدد بالبيانات';

          const alEl = document.getElementById('emp-detail-allowances');
          if (alEl) alEl.textContent = emp.hasSalaryRecord ? this.formatCurrency(emp.allowances) : '0.00 ج.م';

          const deEl = document.getElementById('emp-detail-deductions');
          if (deEl) deEl.textContent = emp.hasSalaryRecord ? this.formatCurrency(emp.insuranceDeductions) : '0.00 ج.م';

          const nsEl = document.getElementById('emp-detail-net-salary');
          if (nsEl) nsEl.textContent = emp.hasSalaryRecord ? this.formatCurrency(emp.netSalary) : 'غير محدد بالبيانات';

          const pmSub = document.getElementById('emp-detail-pay-method-sub');
          if (pmSub) pmSub.textContent = `طريقة الدفع: ${emp.paymentMethod || 'غير محدد'}`;

          // Attendance
          const prEl = document.getElementById('emp-detail-att-present-badge');
          if (prEl) prEl.textContent = `حضور: ${emp.daysPresent} يوم`;
          const abEl = document.getElementById('emp-detail-att-absent-badge');
          if (abEl) abEl.textContent = `غياب: ${emp.daysAbsent} يوم`;
          const rtEl = document.getElementById('emp-detail-att-rate-badge');
          if (rtEl) rtEl.textContent = `النسبة: ${Number(emp.attendanceRate || 100).toFixed(0)}%`;

          const attTbody = document.getElementById('emp-detail-att-tbody');
          const attEmpty = document.getElementById('emp-detail-att-empty');
          const atts = emp.attendanceRecords || [];
          if (attTbody) {
            if (atts.length === 0) {
              attTbody.innerHTML = '';
              if (attEmpty) attEmpty.style.display = 'flex';
            } else {
              if (attEmpty) attEmpty.style.display = 'none';
              attTbody.innerHTML = atts.map(a => `
                <tr>
                  <td><strong dir="ltr">${this.formatDate(a.date)}</strong></td>
                  <td><span class="finance-status-badge ${a.status === 'حاضر' ? 'badge-positive' : 'badge-waste'}">${this.escapeHtml(a.status)}</span></td>
                  <td><span dir="ltr">${this.escapeHtml(a.checkInTime || '-')}</span></td>
                  <td><span dir="ltr">${this.escapeHtml(a.checkOutTime || '-')}</span></td>
                  <td>${this.escapeHtml(a.notes || '-')}</td>
                </tr>
              `).join('');
            }
          }

          // Lending
          const ltEl = document.getElementById('emp-detail-lend-total-badge');
          if (ltEl) ltEl.textContent = `إجمالي السلف: ${this.formatCurrency(emp.totalLending)}`;
          const lpEl = document.getElementById('emp-detail-lend-paid-badge');
          if (lpEl) lpEl.textContent = `المسدد: ${this.formatCurrency(emp.paidLending)}`;
          const lrEl = document.getElementById('emp-detail-lend-rem-badge');
          if (lrEl) lrEl.textContent = `المتبقي: ${this.formatCurrency(emp.remainingLending)}`;

          const lendTbody = document.getElementById('emp-detail-lend-tbody');
          const lendEmpty = document.getElementById('emp-detail-lend-empty');
          const lends = emp.lendings || [];
          if (lendTbody) {
            if (lends.length === 0) {
              lendTbody.innerHTML = '';
              if (lendEmpty) lendEmpty.style.display = 'flex';
            } else {
              if (lendEmpty) lendEmpty.style.display = 'none';
              lendTbody.innerHTML = lends.map(l => `
                <tr>
                  <td><strong dir="ltr">${this.formatDate(l.requestDate)}</strong></td>
                  <td><strong dir="ltr" style="font-family: monospace;">${this.formatCurrency(l.lendingAmount)}</strong></td>
                  <td><strong dir="ltr" style="color: #059669;">${this.formatCurrency(l.paidAmount)}</strong></td>
                  <td><strong dir="ltr" style="color: #DC2626;">${this.formatCurrency(l.remainingAmount)}</strong></td>
                  <td><span class="finance-status-badge badge-positive">${this.escapeHtml(l.approvalStatus)}</span></td>
                  <td>${this.escapeHtml(l.approvedBy || '-')}</td>
                  <td>${this.escapeHtml(l.notes || '-')}</td>
                </tr>
              `).join('');
            }
          }
        }
      } catch (err) {
        console.error('Failed to load employee detail:', err);
      }
    }
  }

  // Register instance globally
  window.FinanceManagementApp = new FinanceManagementApp();
})();
