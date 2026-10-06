/**
 * FaceStone Management Department
 * Central Read-Only Monitoring & Oversight Dashboard
 * Integrates real-time live data from Transportation, Storage, Finance, Employees, and Suppliers.
 * FaceStone Design System: Yellow (#F59E0B), Dark (#111827), Gold accents, Arabic RTL.
 */

(function () {
  'use strict';

  class ManagementDashboardManager {
    constructor() {
      this.currentView = 'hub';
      this.currentSupplierId = null;
      this.searchDebounceTimer = null;
      this.isInitialized = false;

      // Filter states
      this.filters = {
        transportation: { fromDate: '', toDate: '', search: '', po: '', driver: '', material: '', destination: '' },
        storage: { fromDate: '', toDate: '', search: '', material: '', action: '', po: '' },
        finance: { fromDate: '', toDate: '', search: '', po: '', paymentStatus: '' },
        employees: { search: '', department: '', position: '' },
        suppliers: { search: '', po: '', material: '' }
      };

      this.init();
    }

    init() {
      if (this.isInitialized) return;
      this.bindElements();
      this.bindEvents();
      this.isInitialized = true;
    }

    bindElements() {
      this.pageRoot = document.getElementById('management-page-root');
      if (!this.pageRoot) return;

      // Header Elements
      this.userNameEl = document.getElementById('mgmt-user-name');
      this.userRoleEl = document.getElementById('mgmt-user-role');
      this.userAvatarEl = document.getElementById('mgmt-user-avatar');

      // Global Search
      this.globalSearchInput = document.getElementById('mgmt-global-search-input');
      this.globalSearchClearBtn = document.getElementById('mgmt-search-clear-btn');
      this.globalSearchSpinner = document.getElementById('mgmt-search-spinner');
      this.globalSearchDropdown = document.getElementById('mgmt-search-dropdown');
      this.globalSearchResultsContainer = document.getElementById('mgmt-search-results-container');

      // Hub View & Cards
      this.hubView = document.getElementById('mgmt-hub-view');
      this.cardTransportation = document.getElementById('mgmt-card-transportation');
      this.cardStorage = document.getElementById('mgmt-card-storage');
      this.cardFinance = document.getElementById('mgmt-card-finance');
      this.cardEmployees = document.getElementById('mgmt-card-employees');
      this.cardSuppliers = document.getElementById('mgmt-card-suppliers');

      // Subviews
      this.viewTransportation = document.getElementById('mgmt-view-transportation');
      this.viewStorage = document.getElementById('mgmt-view-storage');
      this.viewFinance = document.getElementById('mgmt-view-finance');
      this.viewEmployees = document.getElementById('mgmt-view-employees');
      this.viewSuppliers = document.getElementById('mgmt-view-suppliers');
      this.viewSupplierDetails = document.getElementById('mgmt-view-supplier-details');

      // Subview Back Buttons
      this.btnTransBack = document.getElementById('mgmt-trans-back-btn');
      this.btnStorageBack = document.getElementById('mgmt-storage-back-btn');
      this.btnFinBack = document.getElementById('mgmt-fin-back-btn');
      this.btnEmpBack = document.getElementById('mgmt-emp-back-btn');
      this.btnSupBack = document.getElementById('mgmt-sup-back-btn');
      this.btnSupDetailBack = document.getElementById('mgmt-supdetail-back-btn');

      // Transportation Filter Elements
      this.transDateFrom = document.getElementById('mgmt-trans-date-from');
      this.transDateTo = document.getElementById('mgmt-trans-date-to');
      this.transFilterPo = document.getElementById('mgmt-trans-filter-po');
      this.transFilterDriver = document.getElementById('mgmt-trans-filter-driver');
      this.transFilterMaterial = document.getElementById('mgmt-trans-filter-material');
      this.transFilterDestination = document.getElementById('mgmt-trans-filter-destination');
      this.transSearchInput = document.getElementById('mgmt-trans-search-input');
      this.transBtnApply = document.getElementById('mgmt-trans-btn-apply');
      this.transBtnReset = document.getElementById('mgmt-trans-btn-reset');

      // Storage Filter Elements
      this.storageDateFrom = document.getElementById('mgmt-storage-date-from');
      this.storageDateTo = document.getElementById('mgmt-storage-date-to');
      this.storageFilterMaterial = document.getElementById('mgmt-storage-filter-material');
      this.storageFilterAction = document.getElementById('mgmt-storage-filter-action');
      this.storageFilterPo = document.getElementById('mgmt-storage-filter-po');
      this.storageSearchInput = document.getElementById('mgmt-storage-search-input');
      this.storageBtnApply = document.getElementById('mgmt-storage-btn-apply');
      this.storageBtnReset = document.getElementById('mgmt-storage-btn-reset');

      // Finance Filter Elements
      this.finDateFrom = document.getElementById('mgmt-fin-date-from');
      this.finDateTo = document.getElementById('mgmt-fin-date-to');
      this.finFilterPo = document.getElementById('mgmt-fin-filter-po');
      this.finFilterStatus = document.getElementById('mgmt-fin-filter-status');
      this.finSearchInput = document.getElementById('mgmt-fin-search-input');
      this.finBtnApply = document.getElementById('mgmt-fin-btn-apply');
      this.finBtnReset = document.getElementById('mgmt-fin-btn-reset');
      this.finExpensesTbody = document.getElementById('mgmt-fin-expenses-tbody');
      this.finExpensesBadge = document.getElementById('mgmt-fin-expenses-badge');
      this.finBankTbody = document.getElementById('mgmt-fin-bank-tbody');
      this.finBankBadge = document.getElementById('mgmt-fin-bank-badge');

      // Employees Filter Elements
      this.empFilterDept = document.getElementById('mgmt-emp-filter-dept');
      this.empFilterPos = document.getElementById('mgmt-emp-filter-pos');
      this.empSearchInput = document.getElementById('mgmt-emp-search-input');
      this.empBtnApply = document.getElementById('mgmt-emp-btn-apply');
      this.empBtnReset = document.getElementById('mgmt-emp-btn-reset');

      // Suppliers Filter Elements
      this.supFilterPo = document.getElementById('mgmt-sup-filter-po');
      this.supFilterMaterial = document.getElementById('mgmt-sup-filter-material');
      this.supSearchInput = document.getElementById('mgmt-sup-search-input');
      this.supBtnApply = document.getElementById('mgmt-sup-btn-apply');
      this.supBtnReset = document.getElementById('mgmt-sup-btn-reset');

      // Employee Modal Elements
      this.empModalBackdrop = document.getElementById('mgmt-employee-modal-backdrop');
      this.empModalCloseBtn = document.getElementById('mgmt-emp-modal-close-btn');
      this.empModalBody = document.getElementById('mgmt-emp-modal-body');

      // Transportation Modal Elements
      this.transModalBackdrop = document.getElementById('mgmt-trans-modal-backdrop');
      this.transModalCloseBtn = document.getElementById('mgmt-trans-modal-close-btn');
      this.transModalBody = document.getElementById('mgmt-trans-modal-body');

      // Storage Modal Elements
      this.storageModalBackdrop = document.getElementById('mgmt-storage-modal-backdrop');
      this.storageModalCloseBtn = document.getElementById('mgmt-storage-modal-close-btn');
      this.storageModalBody = document.getElementById('mgmt-storage-modal-body');

      // PO Modal Elements
      this.poModalBackdrop = document.getElementById('mgmt-po-modal-backdrop');
      this.poModalCloseBtn = document.getElementById('mgmt-po-modal-close-btn');
      this.poModalBody = document.getElementById('mgmt-po-modal-body');
    }

    bindEvents() {
      // 5 Main Landing Cards Navigation
      if (this.cardTransportation) {
        this.cardTransportation.addEventListener('click', () => this.navigateTo('/management/transportation'));
      }
      if (this.cardStorage) {
        this.cardStorage.addEventListener('click', () => this.navigateTo('/management/storage'));
      }
      if (this.cardFinance) {
        this.cardFinance.addEventListener('click', () => this.navigateTo('/management/finance'));
      }
      if (this.cardEmployees) {
        this.cardEmployees.addEventListener('click', () => this.navigateTo('/management/employees'));
      }
      if (this.cardSuppliers) {
        this.cardSuppliers.addEventListener('click', () => this.navigateTo('/management/suppliers'));
      }

      // Subview Back Buttons
      if (this.btnTransBack) this.btnTransBack.addEventListener('click', () => this.navigateTo('/management'));
      if (this.btnStorageBack) this.btnStorageBack.addEventListener('click', () => this.navigateTo('/management'));
      if (this.btnFinBack) this.btnFinBack.addEventListener('click', () => this.navigateTo('/management'));
      if (this.btnEmpBack) this.btnEmpBack.addEventListener('click', () => this.navigateTo('/management'));
      if (this.btnSupBack) this.btnSupBack.addEventListener('click', () => this.navigateTo('/management'));
      if (this.btnSupDetailBack) this.btnSupDetailBack.addEventListener('click', () => this.navigateTo('/management/suppliers'));

      // Global Search Input
      if (this.globalSearchInput) {
        this.globalSearchInput.addEventListener('input', (e) => this.handleGlobalSearchInput(e.target.value));
        this.globalSearchInput.addEventListener('focus', () => {
          if (this.globalSearchInput.value.trim().length >= 2) {
            this.showSearchDropdown();
          }
        });
      }

      if (this.globalSearchClearBtn) {
        this.globalSearchClearBtn.addEventListener('click', () => {
          if (this.globalSearchInput) this.globalSearchInput.value = '';
          this.globalSearchClearBtn.style.display = 'none';
          this.hideSearchDropdown();
        });
      }

      // Close search dropdown on click outside
      document.addEventListener('click', (e) => {
        if (!e.target.closest('#mgmt-global-search-bar') && !e.target.closest('#mgmt-search-dropdown')) {
          this.hideSearchDropdown();
        }
      });

      // Transportation Filters
      if (this.transBtnApply) {
        this.transBtnApply.addEventListener('click', () => {
          this.filters.transportation.fromDate = this.transDateFrom ? this.transDateFrom.value : '';
          this.filters.transportation.toDate = this.transDateTo ? this.transDateTo.value : '';
          this.filters.transportation.po = this.transFilterPo ? this.transFilterPo.value.trim() : '';
          this.filters.transportation.driver = this.transFilterDriver ? this.transFilterDriver.value.trim() : '';
          this.filters.transportation.material = this.transFilterMaterial ? this.transFilterMaterial.value.trim() : '';
          this.filters.transportation.destination = this.transFilterDestination ? this.transFilterDestination.value.trim() : '';
          this.filters.transportation.search = this.transSearchInput ? this.transSearchInput.value.trim() : '';
          this.loadTransportationData();
        });
      }
      if (this.transBtnReset) {
        this.transBtnReset.addEventListener('click', () => {
          if (this.transDateFrom) this.transDateFrom.value = '';
          if (this.transDateTo) this.transDateTo.value = '';
          if (this.transFilterPo) this.transFilterPo.value = '';
          if (this.transFilterDriver) this.transFilterDriver.value = '';
          if (this.transFilterMaterial) this.transFilterMaterial.value = '';
          if (this.transFilterDestination) this.transFilterDestination.value = '';
          if (this.transSearchInput) this.transSearchInput.value = '';
          this.filters.transportation = { fromDate: '', toDate: '', search: '', po: '', driver: '', material: '', destination: '' };
          this.loadTransportationData();
        });
      }

      // Storage Filters
      if (this.storageBtnApply) {
        this.storageBtnApply.addEventListener('click', () => {
          this.filters.storage.fromDate = this.storageDateFrom ? this.storageDateFrom.value : '';
          this.filters.storage.toDate = this.storageDateTo ? this.storageDateTo.value : '';
          this.filters.storage.material = this.storageFilterMaterial ? this.storageFilterMaterial.value.trim() : '';
          this.filters.storage.action = this.storageFilterAction ? this.storageFilterAction.value : '';
          this.filters.storage.po = this.storageFilterPo ? this.storageFilterPo.value.trim() : '';
          this.filters.storage.search = this.storageSearchInput ? this.storageSearchInput.value.trim() : '';
          this.loadStorageData();
        });
      }
      if (this.storageBtnReset) {
        this.storageBtnReset.addEventListener('click', () => {
          if (this.storageDateFrom) this.storageDateFrom.value = '';
          if (this.storageDateTo) this.storageDateTo.value = '';
          if (this.storageFilterMaterial) this.storageFilterMaterial.value = '';
          if (this.storageFilterAction) this.storageFilterAction.value = '';
          if (this.storageFilterPo) this.storageFilterPo.value = '';
          if (this.storageSearchInput) this.storageSearchInput.value = '';
          this.filters.storage = { fromDate: '', toDate: '', search: '', material: '', action: '', po: '' };
          this.loadStorageData();
        });
      }

      // Finance Filters
      if (this.finBtnApply) {
        this.finBtnApply.addEventListener('click', () => {
          this.filters.finance.fromDate = this.finDateFrom ? this.finDateFrom.value : '';
          this.filters.finance.toDate = this.finDateTo ? this.finDateTo.value : '';
          this.filters.finance.po = this.finFilterPo ? this.finFilterPo.value.trim() : '';
          this.filters.finance.paymentStatus = this.finFilterStatus ? this.finFilterStatus.value : '';
          this.filters.finance.search = this.finSearchInput ? this.finSearchInput.value.trim() : '';
          this.loadFinanceData();
        });
      }
      if (this.finBtnReset) {
        this.finBtnReset.addEventListener('click', () => {
          if (this.finDateFrom) this.finDateFrom.value = '';
          if (this.finDateTo) this.finDateTo.value = '';
          if (this.finFilterPo) this.finFilterPo.value = '';
          if (this.finFilterStatus) this.finFilterStatus.value = '';
          if (this.finSearchInput) this.finSearchInput.value = '';
          this.filters.finance = { fromDate: '', toDate: '', search: '', po: '', paymentStatus: '' };
          this.loadFinanceData();
        });
      }

      // Employees Filters
      if (this.empBtnApply) {
        this.empBtnApply.addEventListener('click', () => {
          this.filters.employees.department = this.empFilterDept ? this.empFilterDept.value : '';
          this.filters.employees.position = this.empFilterPos ? this.empFilterPos.value.trim() : '';
          this.filters.employees.search = this.empSearchInput ? this.empSearchInput.value.trim() : '';
          this.loadEmployeesData();
        });
      }
      if (this.empSearchInput) {
        this.empSearchInput.addEventListener('input', (e) => {
          clearTimeout(this._empDebounce);
          this._empDebounce = setTimeout(() => {
            this.filters.employees.search = e.target.value.trim();
            this.loadEmployeesData();
          }, 300);
        });
      }
      if (this.empBtnReset) {
        this.empBtnReset.addEventListener('click', () => {
          if (this.empFilterDept) this.empFilterDept.value = '';
          if (this.empFilterPos) this.empFilterPos.value = '';
          if (this.empSearchInput) this.empSearchInput.value = '';
          this.filters.employees = { search: '', department: '', position: '' };
          this.loadEmployeesData();
        });
      }

      // Suppliers Filters
      if (this.supBtnApply) {
        this.supBtnApply.addEventListener('click', () => {
          this.filters.suppliers.po = this.supFilterPo ? this.supFilterPo.value.trim() : '';
          this.filters.suppliers.material = this.supFilterMaterial ? this.supFilterMaterial.value.trim() : '';
          this.filters.suppliers.search = this.supSearchInput ? this.supSearchInput.value.trim() : '';
          this.loadSuppliersData();
        });
      }
      if (this.supSearchInput) {
        this.supSearchInput.addEventListener('input', (e) => {
          clearTimeout(this._supDebounce);
          this._supDebounce = setTimeout(() => {
            this.filters.suppliers.search = e.target.value.trim();
            this.loadSuppliersData();
          }, 300);
        });
      }
      if (this.supBtnReset) {
        this.supBtnReset.addEventListener('click', () => {
          if (this.supFilterPo) this.supFilterPo.value = '';
          if (this.supFilterMaterial) this.supFilterMaterial.value = '';
          if (this.supSearchInput) this.supSearchInput.value = '';
          this.filters.suppliers = { search: '', po: '', material: '' };
          this.loadSuppliersData();
        });
      }

      // Modal Closers
      if (this.empModalCloseBtn) this.empModalCloseBtn.addEventListener('click', () => this.closeEmployeeModal());
      if (this.empModalBackdrop) {
        this.empModalBackdrop.addEventListener('click', (e) => {
          if (e.target === this.empModalBackdrop) this.closeEmployeeModal();
        });
      }

      if (this.transModalCloseBtn) this.transModalCloseBtn.addEventListener('click', () => this.closeTransportationModal());
      if (this.transModalBackdrop) {
        this.transModalBackdrop.addEventListener('click', (e) => {
          if (e.target === this.transModalBackdrop) this.closeTransportationModal();
        });
      }

      if (this.storageModalCloseBtn) this.storageModalCloseBtn.addEventListener('click', () => this.closeStorageModal());
      if (this.storageModalBackdrop) {
        this.storageModalBackdrop.addEventListener('click', (e) => {
          if (e.target === this.storageModalBackdrop) this.closeStorageModal();
        });
      }

      if (this.poModalCloseBtn) this.poModalCloseBtn.addEventListener('click', () => this.closePoModal());
      if (this.poModalBackdrop) {
        this.poModalBackdrop.addEventListener('click', (e) => {
          if (e.target === this.poModalBackdrop) this.closePoModal();
        });
      }

      // Popstate handling for SPA navigation within /management/*
      window.addEventListener('popstate', (e) => {
        const path = window.location.pathname;
        if (path.startsWith('/management')) {
          this.handleRoute(path);
        }
      });

      // Real-time Event Listener (SignalR / Inter-Department events)
      document.addEventListener('facestone:data-changed', (e) => {
        const dept = e.detail?.department;
        console.log('[Management] Real-time data change event received:', dept, e.detail?.action);
        this.handleRealtimeUpdate(dept);
      });

      // Inter-department domestic event listeners
      ['transportation:updated', 'storage:updated', 'finance:updated', 'facestone:po:updated'].forEach(evtName => {
        document.addEventListener(evtName, () => {
          this.handleRealtimeUpdate('all');
        });
      });

      // Background periodic polling (every 6 seconds while Management page is visible)
      this.startRealtimePolling();
    }

    /* ------------------------------------------------------------------------
       Real-Time & Visibility Management
       ------------------------------------------------------------------------ */

    isManagementPageVisible() {
      const root = document.getElementById('management-page-root');
      return Boolean(root && root.style.display !== 'none' && !root.hidden);
    }

    handleRealtimeUpdate(dept) {
      if (!this.isManagementPageVisible()) return;
      // Always refresh overview metrics so cards stay fresh
      this.loadOverview();
      // Silently refresh active subview without loading flicker
      if (this.currentView !== 'hub') {
        this.loadData(true);
      }
    }

    startRealtimePolling() {
      if (this.pollTimer) clearInterval(this.pollTimer);
      this.pollTimer = setInterval(() => {
        if (this.isManagementPageVisible()) {
          this.handleRealtimeUpdate('poll');
        }
      }, 6000);
    }

    /* ------------------------------------------------------------------------
       Navigation & Subview Toggling
       ------------------------------------------------------------------------ */

    navigateTo(route, filterQuery = null) {
      if (window.location.pathname !== route) {
        history.pushState({ view: 'management', subRoute: route }, '', route);
      }
      this.handleRoute(route, filterQuery);
    }

    handleRoute(route, filterQuery = null) {
      this.hideAllViews();
      this.hideSearchDropdown();

      if (!route || route === '/management' || route === '/departments/management') {
        this.showLandingHub();
      } else if (route === '/management/transportation') {
        this.showTransportationView(filterQuery);
      } else if (route === '/management/storage') {
        this.showStorageView(filterQuery);
      } else if (route === '/management/finance') {
        this.showFinanceView(filterQuery);
      } else if (route === '/management/employees') {
        this.showEmployeesView(filterQuery);
      } else if (route === '/management/suppliers') {
        this.showSuppliersView(filterQuery);
      } else if (route.startsWith('/management/suppliers/')) {
        const supplierId = decodeURIComponent(route.replace('/management/suppliers/', ''));
        this.showSupplierDetailsView(supplierId);
      } else {
        this.showLandingHub();
      }
    }

    hideAllViews() {
      if (this.hubView) {
        this.hubView.style.display = 'none';
        this.hubView.classList.remove('active');
      }
      if (this.viewTransportation) {
        this.viewTransportation.classList.remove('active');
        this.viewTransportation.style.display = 'none';
      }
      if (this.viewStorage) {
        this.viewStorage.classList.remove('active');
        this.viewStorage.style.display = 'none';
      }
      if (this.viewFinance) {
        this.viewFinance.classList.remove('active');
        this.viewFinance.style.display = 'none';
      }
      if (this.viewEmployees) {
        this.viewEmployees.classList.remove('active');
        this.viewEmployees.style.display = 'none';
      }
      if (this.viewSuppliers) {
        this.viewSuppliers.classList.remove('active');
        this.viewSuppliers.style.display = 'none';
      }
      if (this.viewSupplierDetails) {
        this.viewSupplierDetails.classList.remove('active');
        this.viewSupplierDetails.style.display = 'none';
      }
    }

    showLandingHub() {
      this.currentView = 'hub';
      this.hideAllViews();
      if (this.hubView) {
        this.hubView.classList.add('active');
        this.hubView.style.display = 'block';
      }
      this.loadOverview();
      window.scrollTo({ top: 0, behavior: 'smooth' });
    }

    showTransportationView(filterQuery = null) {
      this.currentView = 'transportation';
      this.hideAllViews();
      if (this.viewTransportation) {
        this.viewTransportation.classList.add('active');
        this.viewTransportation.style.display = 'block';
      }
      if (filterQuery !== null && this.transSearchInput) {
        this.transSearchInput.value = filterQuery;
        this.filters.transportation.search = filterQuery;
      }
      this.loadTransportationData();
      window.scrollTo({ top: 0, behavior: 'smooth' });
    }

    showStorageView(filterQuery = null) {
      this.currentView = 'storage';
      this.hideAllViews();
      if (this.viewStorage) {
        this.viewStorage.classList.add('active');
        this.viewStorage.style.display = 'block';
      }
      if (filterQuery !== null && this.storageSearchInput) {
        this.storageSearchInput.value = filterQuery;
        this.filters.storage.search = filterQuery;
      }
      this.loadStorageData();
      window.scrollTo({ top: 0, behavior: 'smooth' });
    }

    showFinanceView(filterQuery = null) {
      this.currentView = 'finance';
      this.hideAllViews();
      if (this.viewFinance) {
        this.viewFinance.classList.add('active');
        this.viewFinance.style.display = 'block';
      }
      if (filterQuery !== null && this.finSearchInput) {
        this.finSearchInput.value = filterQuery;
        this.filters.finance.search = filterQuery;
      }
      this.loadFinanceData();
      window.scrollTo({ top: 0, behavior: 'smooth' });
    }

    showEmployeesView(filterQuery = null) {
      this.currentView = 'employees';
      this.hideAllViews();
      if (this.viewEmployees) {
        this.viewEmployees.classList.add('active');
        this.viewEmployees.style.display = 'block';
      }
      if (filterQuery !== null && this.empSearchInput) {
        this.empSearchInput.value = filterQuery;
        this.filters.employees.search = filterQuery;
      }
      this.loadEmployeesData();
      window.scrollTo({ top: 0, behavior: 'smooth' });
    }

    showSuppliersView(filterQuery = null) {
      this.currentView = 'suppliers';
      this.hideAllViews();
      if (this.viewSuppliers) {
        this.viewSuppliers.classList.add('active');
        this.viewSuppliers.style.display = 'block';
      }
      if (filterQuery !== null && this.supSearchInput) {
        this.supSearchInput.value = filterQuery;
        this.filters.suppliers.search = filterQuery;
      }
      this.loadSuppliersData();
      window.scrollTo({ top: 0, behavior: 'smooth' });
    }

    showSupplierDetailsView(supplierId) {
      this.currentView = 'supplier-details';
      this.currentSupplierId = supplierId;
      this.hideAllViews();
      if (this.viewSupplierDetails) {
        this.viewSupplierDetails.classList.add('active');
        this.viewSupplierDetails.style.display = 'block';
      }
      this.loadSupplierDetail(supplierId);
      window.scrollTo({ top: 0, behavior: 'smooth' });
    }

    /* ------------------------------------------------------------------------
       Data Loaders (Read-only, Live Aggregations)
       ------------------------------------------------------------------------ */

    async loadData(silent = false) {
      if (this.currentView === 'hub') {
        await this.loadOverview();
      } else if (this.currentView === 'transportation') {
        await this.loadTransportationData(silent);
      } else if (this.currentView === 'storage') {
        await this.loadStorageData(silent);
      } else if (this.currentView === 'finance') {
        await this.loadFinanceData(silent);
      } else if (this.currentView === 'employees') {
        await this.loadEmployeesData(silent);
      } else if (this.currentView === 'suppliers') {
        await this.loadSuppliersData(silent);
      } else if (this.currentView === 'supplier-details' && this.currentSupplierId) {
        await this.loadSupplierDetail(this.currentSupplierId, silent);
      }
    }

    /**
     * 1. Overview for 5 Hub Cards
     */
    async loadOverview() {
      try {
        const res = await window.ChatApi.getManagementOverview();
        if (res && res.success && res.data) {
          const d = res.data;

          // Transportation Card Metrics
          this.setElText('hub-trans-trips', this.formatNumber(d.transportation?.tripsCount, 0));
          this.setElText('hub-trans-qty', `${this.formatNumber(d.transportation?.totalReceivedQuantity, 2)} طن`);
          this.setElText('hub-trans-waste', `${this.formatNumber(d.transportation?.totalWasteQuantity, 2)} طن`);

          // Storage Card Metrics
          this.setElText('hub-storage-deposit', `${this.formatNumber(d.storage?.totalDeposit, 2)} طن`);
          this.setElText('hub-storage-issue', `${this.formatNumber(d.storage?.totalIssue, 2)} طن`);
          this.setElText('hub-storage-balance', `${this.formatNumber(d.storage?.currentBalance, 2)} طن`);

          // Finance Card Metrics
          this.setElText('hub-fin-expenses', this.formatCurrency(d.finance?.totalExpenses));
          this.setElText('hub-fin-povalue', this.formatCurrency(d.finance?.totalPoValue));
          this.setElText('hub-fin-perf', `${(d.finance?.performancePercentage || 0).toFixed(1)}%`);

          // Employees Card Metrics
          this.setElText('hub-emp-count', this.formatNumber(d.employees?.totalEmployees, 0));
          this.setElText('hub-emp-salaries', this.formatCurrency(d.employees?.totalSalariesRecorded));
          this.setElText('hub-emp-lending', this.formatCurrency(d.employees?.totalRemainingLending));

          // Suppliers Card Metrics
          this.setElText('hub-sup-count', this.formatNumber(d.suppliers?.totalSuppliers, 0));
          this.setElText('hub-sup-pos', this.formatNumber(d.suppliers?.totalPos, 0));
          this.setElText('hub-sup-remqty', `${this.formatNumber(d.suppliers?.totalRemainingQuantity, 2)} طن`);
        }
      } catch (err) {
        console.error('Error loading management overview:', err);
      }
    }

    /**
     * 2. Transportation Subview Data
     */
    async loadTransportationData(silent = false) {
      const tbody = document.getElementById('mgmt-trans-tbody');
      const badgeCount = document.getElementById('mgmt-trans-count-badge') || document.getElementById('mgmt-trans-badge-count');
      if (tbody && !silent) tbody.innerHTML = this.renderLoadingRow(17);

      try {
        const res = await window.ChatApi.getManagementTransportation(this.filters.transportation);
        if (res && res.success && res.data) {
          const d = res.data;

          // 10 KPIs
          this.setElText('mgmt-trans-kpi-trips', this.formatNumber(d.totalTrips, 0));
          this.setElText('mgmt-trans-kpi-total-qty', this.formatNumber(d.totalQuantity, 3));
          this.setElText('mgmt-trans-kpi-net-qty', this.formatNumber(d.netQuantity, 3));
          this.setElText('mgmt-trans-kpi-waste', this.formatNumber(d.totalWaste, 3));
          this.setElText('mgmt-trans-kpi-cars', this.formatNumber(d.carsCount, 0));
          this.setElText('mgmt-trans-kpi-drivers', this.formatNumber(d.driversCount, 0));
          this.setElText('mgmt-trans-kpi-completed', this.formatNumber(d.completedShipmentsCount, 0));
          this.setElText('mgmt-trans-kpi-current', this.formatNumber(d.currentShipmentsCount, 0));
          this.setElText('mgmt-trans-kpi-duration', d.avgShippingDurationMinutes ? `${d.avgShippingDurationMinutes} دقيقة` : '-');
          this.setElText('mgmt-trans-kpi-last-movement', d.lastMovementNumber ? `#${d.lastMovementNumber}` : '-');
          this.setElText('mgmt-trans-kpi-last-movement-date', d.lastMovementDate ? `بتاريخ ${this.formatDate(d.lastMovementDate)}` : 'لا توجد حركات');

          // Transactions Table
          const items = d.items || [];
          if (badgeCount) badgeCount.textContent = `${items.length} حركة معروضة`;
          this.renderTransportationTable(items);
        } else {
          if (tbody) tbody.innerHTML = this.renderEmptyRow('لم يتم العثور على حركات نقل مطابقة للشروط', 17);
        }
      } catch (err) {
        console.error('Error loading transportation data:', err);
        if (tbody) tbody.innerHTML = this.renderErrorRow('تعذر تحميل بيانات النقل من الخادم', 17);
      }
    }

    renderTransportationTable(items) {
      const tbody = document.getElementById('mgmt-trans-tbody');
      if (!tbody) return;

      if (!items || items.length === 0) {
        tbody.innerHTML = this.renderEmptyRow('لا توجد حركات نقل مسجلة', 17);
        return;
      }

      tbody.innerHTML = items.map(t => {
        const internalWb = t.internalWeighbridgeCode ? `${this.escapeHtml(t.internalWeighbridgeCode)} (${this.formatNumber(t.internalNetWeight, 3)})` : '-';
        const externalWb = t.externalWeighbridgeCode ? `${this.escapeHtml(t.externalWeighbridgeCode)} (${this.formatNumber(t.externalNetWeight, 3)})` : '-';
        const times = (t.departureTime || t.arrivalTime) ? `${this.escapeHtml(t.departureTime || '-')} / ${this.escapeHtml(t.arrivalTime || '-')}` : '-';
        const duration = t.shippingDurationMinutes ? `${t.shippingDurationMinutes} د` : '-';
        const attachments = t.hasAttachments ? `<span class="mgmt-code-pill info font-mono">📎 ${t.attachmentsCount || 1}</span>` : '<span class="text-secondary">-</span>';

        return `
          <tr>
            <td><span class="mgmt-code-pill font-mono">#${this.escapeHtml(t.transactionNumber || t.id)}</span></td>
            <td class="font-mono">${this.formatDate(t.transactionDate)}</td>
            <td><span class="mgmt-code-pill text-gold font-mono">${this.escapeHtml(t.poNumber || '-')}</span></td>
            <td><strong>${this.escapeHtml(t.companyName || '-')}</strong></td>
            <td>${this.escapeHtml(t.materialType || '-')}</td>
            <td class="font-mono">${this.escapeHtml(t.carNumber || '-')}</td>
            <td>${this.escapeHtml(t.driverName || '-')}</td>
            <td class="font-mono text-secondary">${internalWb}</td>
            <td class="font-mono text-secondary">${externalWb}</td>
            <td class="font-mono text-green"><strong>${this.formatNumber(t.netWeight, 3)}</strong> طن</td>
            <td class="font-mono text-red">${this.formatNumber(t.wasteQuantity, 3)} طن</td>
            <td class="font-mono">${times}</td>
            <td class="font-mono">${duration}</td>
            <td>${this.renderStatusBadge(t.receivingStatus || t.transactionStatus)}</td>
            <td>${this.escapeHtml(t.destination || '-')}</td>
            <td>${attachments}</td>
            <td>
              <button type="button" class="mgmt-action-btn" onclick="window.ManagementApp.openTransportationModal(${t.id})">
                عرض التفاصيل
              </button>
            </td>
          </tr>
        `;
      }).join('');
    }

    async openTransportationModal(id) {
      if (!this.transModalBackdrop || !this.transModalBody) return;
      this.transModalBackdrop.style.display = 'flex';
      this.transModalBody.innerHTML = '<div class="mgmt-table-empty">جاري تحميل تفاصيل الشحنة...</div>';

      try {
        const res = await window.ChatApi.getManagementTransportationDetail(id);
        if (res && res.success && res.data) {
          const t = res.data;
          this.transModalBody.innerHTML = `
            <div class="mgmt-detail-grid">
              <div class="mgmt-detail-field">
                <span class="mgmt-detail-field-label">رقم الحركة:</span>
                <span class="mgmt-detail-field-value font-mono font-bold">#${this.escapeHtml(t.transactionNumber || t.id)}</span>
              </div>
              <div class="mgmt-detail-field">
                <span class="mgmt-detail-field-label">أمر الشراء (PO):</span>
                <span class="mgmt-detail-field-value font-mono text-gold">${this.escapeHtml(t.poNumber || '-')}</span>
              </div>
              <div class="mgmt-detail-field">
                <span class="mgmt-detail-field-label">تاريخ الحركة:</span>
                <span class="mgmt-detail-field-value font-mono">${this.formatDate(t.transactionDate)}</span>
              </div>
              <div class="mgmt-detail-field">
                <span class="mgmt-detail-field-label">الشركة / المورد:</span>
                <span class="mgmt-detail-field-value font-bold">${this.escapeHtml(t.companyName || '-')}</span>
              </div>
              <div class="mgmt-detail-field">
                <span class="mgmt-detail-field-label">نوع الخامة:</span>
                <span class="mgmt-detail-field-value">${this.escapeHtml(t.materialType || '-')}</span>
              </div>
              <div class="mgmt-detail-field">
                <span class="mgmt-detail-field-label">رقم السيارة / اللوحة:</span>
                <span class="mgmt-detail-field-value font-mono">${this.escapeHtml(t.carNumber || '-')}</span>
              </div>
              <div class="mgmt-detail-field">
                <span class="mgmt-detail-field-label">اسم السائق:</span>
                <span class="mgmt-detail-field-value">${this.escapeHtml(t.driverName || '-')}</span>
              </div>
              <div class="mgmt-detail-field">
                <span class="mgmt-detail-field-label">الميزان الداخلي:</span>
                <span class="mgmt-detail-field-value font-mono">${this.escapeHtml(t.internalWeighbridgeCode || '-')} (صافي: ${this.formatNumber(t.internalNetWeight, 3)} طن)</span>
              </div>
              <div class="mgmt-detail-field">
                <span class="mgmt-detail-field-label">الميزان الخارجي:</span>
                <span class="mgmt-detail-field-value font-mono">${this.escapeHtml(t.externalWeighbridgeCode || '-')} (صافي: ${this.formatNumber(t.externalNetWeight, 3)} طن)</span>
              </div>
              <div class="mgmt-detail-field">
                <span class="mgmt-detail-field-label">الكمية الصافية المستلمة:</span>
                <span class="mgmt-detail-field-value font-mono text-green font-bold">${this.formatNumber(t.netWeight, 3)} طن</span>
              </div>
              <div class="mgmt-detail-field">
                <span class="mgmt-detail-field-label">كمية الهالك (Waste):</span>
                <span class="mgmt-detail-field-value font-mono text-red">${this.formatNumber(t.wasteQuantity, 3)} طن</span>
              </div>
              <div class="mgmt-detail-field">
                <span class="mgmt-detail-field-label">وقت المغادرة:</span>
                <span class="mgmt-detail-field-value font-mono">${this.escapeHtml(t.departureTime || '-')}</span>
              </div>
              <div class="mgmt-detail-field">
                <span class="mgmt-detail-field-label">وقت الوصول:</span>
                <span class="mgmt-detail-field-value font-mono">${this.escapeHtml(t.arrivalTime || '-')}</span>
              </div>
              <div class="mgmt-detail-field">
                <span class="mgmt-detail-field-label">زمن الشحن (المدة):</span>
                <span class="mgmt-detail-field-value font-mono">${t.shippingDurationMinutes ? `${t.shippingDurationMinutes} دقيقة` : '-'}</span>
              </div>
              <div class="mgmt-detail-field">
                <span class="mgmt-detail-field-label">حالة الحركة:</span>
                <span class="mgmt-detail-field-value">${this.renderStatusBadge(t.receivingStatus || t.transactionStatus)}</span>
              </div>
              <div class="mgmt-detail-field">
                <span class="mgmt-detail-field-label">الجهة / الوجهة:</span>
                <span class="mgmt-detail-field-value">${this.escapeHtml(t.destination || '-')}</span>
              </div>
              <div class="mgmt-detail-field" style="grid-column: 1 / -1;">
                <span class="mgmt-detail-field-label">الملاحظات والمرفقات:</span>
                <span class="mgmt-detail-field-value">${this.escapeHtml(t.notes || 'لا توجد ملاحظات مسجلة')} ${t.hasAttachments ? `(عدد المرفقات: ${t.attachmentsCount || 1})` : ''}</span>
              </div>
            </div>
            <div style="margin-top: 1.5rem; text-align: left;">
              <button type="button" class="btn-mgmt-nav" onclick="window.ManagementApp.closeTransportationModal()">إغلاق</button>
            </div>
          `;
        } else {
          this.transModalBody.innerHTML = '<div class="mgmt-table-empty text-red">تعذر تحميل تفاصيل حركة النقل.</div>';
        }
      } catch (err) {
        console.error('Error loading transportation detail:', err);
        this.transModalBody.innerHTML = '<div class="mgmt-table-empty text-red">خطأ في جلب بيانات حركة النقل.</div>';
      }
    }

    closeTransportationModal() {
      if (this.transModalBackdrop) this.transModalBackdrop.style.display = 'none';
    }

    /**
     * 3. Storage Subview Data
     */
    async loadStorageData(silent = false) {
      const tbody = document.getElementById('mgmt-storage-tbody');
      const badgeCount = document.getElementById('mgmt-storage-count-badge') || document.getElementById('mgmt-storage-badge-count');
      if (tbody && !silent) tbody.innerHTML = this.renderLoadingRow(16);

      try {
        const res = await window.ChatApi.getManagementStorage(this.filters.storage);
        if (res && res.success && res.data) {
          const d = res.data;

          // 8 KPIs
          this.setElText('mgmt-storage-kpi-deposit', this.formatNumber(d.totalDeposit, 3));
          this.setElText('mgmt-storage-kpi-issue', this.formatNumber(d.totalIssue, 3));
          this.setElText('mgmt-storage-kpi-balance', this.formatNumber(d.currentBalance, 3));
          this.setElText('mgmt-storage-kpi-waste', this.formatNumber(d.totalWaste, 3));
          this.setElText('mgmt-storage-kpi-po-exports', this.formatNumber(d.poRelatedExportsQuantity, 3));
          this.setElText('mgmt-storage-kpi-po-exports-count', `${this.formatNumber(d.poRelatedExportsCount, 0)} حركات مربوطة بـ PO`);
          this.setElText('mgmt-storage-kpi-non-po-imports', this.formatNumber(d.nonPoImportsQuantity, 3));
          this.setElText('mgmt-storage-kpi-non-po-imports-count', `${this.formatNumber(d.nonPoImportsCount, 0)} حركات عامة`);
          this.setElText('mgmt-storage-kpi-tx-count', this.formatNumber(d.transactionsCount, 0));
          this.setElText('mgmt-storage-kpi-materials-count', this.formatNumber((d.materialBreakdown || []).length, 0));

          // Material Breakdown Cards
          this.renderMaterialBreakdown(d.materialBreakdown || []);

          // Transactions Table
          const items = d.items || [];
          if (badgeCount) badgeCount.textContent = `${items.length} حركة معروضة`;
          this.renderStorageTable(items);
        } else {
          if (tbody) tbody.innerHTML = this.renderEmptyRow('لم يتم العثور على حركات مخازن مطابقة للشروط', 16);
        }
      } catch (err) {
        console.error('Error loading storage data:', err);
        if (tbody) tbody.innerHTML = this.renderErrorRow('تعذر تحميل بيانات المخازن من الخادم', 16);
      }
    }

    renderMaterialBreakdown(breakdown) {
      const container = document.getElementById('mgmt-storage-materials-breakdown');
      if (!container) return;

      if (!breakdown || breakdown.length === 0) {
        container.innerHTML = '<span class="text-secondary">لا توجد بيانات تفصيلية للخامات</span>';
        return;
      }

      container.innerHTML = breakdown.map(m => {
        const bal = (m.incomingQuantity || 0) - (m.outgoingQuantity || 0);
        return `
          <div class="mgmt-material-pill">
            <div class="mgmt-material-pill-title">${this.escapeHtml(m.material)}</div>
            <div class="mgmt-material-pill-stats">
              <div>وارد: <span class="text-green font-mono">${this.formatNumber(m.incomingQuantity, 2)}</span> طن</div>
              <div>منصرف: <span class="text-red font-mono">${this.formatNumber(m.outgoingQuantity, 2)}</span> طن</div>
              <div>رصيد صافي: <strong class="text-gold font-mono">${this.formatNumber(bal, 2)}</strong> طن</div>
            </div>
          </div>
        `;
      }).join('');
    }

    renderStorageTable(items) {
      const tbody = document.getElementById('mgmt-storage-tbody');
      if (!tbody) return;

      if (!items || items.length === 0) {
        tbody.innerHTML = this.renderEmptyRow('لا توجد حركات مخزن مسجلة', 16);
        return;
      }

      tbody.innerHTML = items.map(s => {
        const internalWb = s.internalWeighbridgeCode ? `${this.escapeHtml(s.internalWeighbridgeCode)} (${this.formatNumber(s.internalNetWeight, 3)})` : '-';
        const externalWb = s.externalWeighbridgeCode ? `${this.escapeHtml(s.externalWeighbridgeCode)} (${this.formatNumber(s.externalNetWeight, 3)})` : '-';
        const attachments = s.hasAttachments ? '<span class="mgmt-code-pill info font-mono">📎 نعم</span>' : '<span class="text-secondary">-</span>';

        return `
          <tr>
            <td><span class="mgmt-code-pill font-mono">#${this.escapeHtml(s.movementNumber || s.id)}</span></td>
            <td><span class="mgmt-code-pill font-mono">${this.escapeHtml(s.referenceNumber || '-')}</span></td>
            <td class="font-mono">${this.formatDate(s.transactionDate)}</td>
            <td>${this.renderTransactionTypeBadge(s.action)}</td>
            <td>${this.escapeHtml(s.serviceType || '-')}</td>
            <td><strong>${this.escapeHtml(s.material || '-')}</strong></td>
            <td><span class="mgmt-code-pill text-gold font-mono">${this.escapeHtml(s.poNumber || '-')}</span></td>
            <td class="font-mono text-green">${s.incomingQuantity > 0 ? this.formatNumber(s.incomingQuantity, 3) : '-'}</td>
            <td class="font-mono text-red">${s.outgoingQuantity > 0 ? this.formatNumber(s.outgoingQuantity, 3) : '-'}</td>
            <td class="font-mono text-gold"><strong>${this.formatNumber(s.closingBalance, 3)}</strong> طن</td>
            <td class="font-mono text-secondary">${internalWb}</td>
            <td class="font-mono text-secondary">${externalWb}</td>
            <td>${this.escapeHtml(s.receivingEmployee || '-')}</td>
            <td>${this.escapeHtml(s.storageLocation || '-')}</td>
            <td>${attachments}</td>
            <td>
              <button type="button" class="mgmt-action-btn" onclick="window.ManagementApp.openStorageModal(${s.id})">
                عرض التفاصيل
              </button>
            </td>
          </tr>
        `;
      }).join('');
    }

    async openStorageModal(id) {
      if (!this.storageModalBackdrop || !this.storageModalBody) return;
      this.storageModalBackdrop.style.display = 'flex';
      this.storageModalBody.innerHTML = '<div class="mgmt-table-empty">جاري تحميل تفاصيل حركة المخزن...</div>';

      try {
        const res = await window.ChatApi.getManagementStorageDetail(id);
        if (res && res.success && res.data) {
          const s = res.data;
          this.storageModalBody.innerHTML = `
            <div class="mgmt-detail-grid">
              <div class="mgmt-detail-field">
                <span class="mgmt-detail-field-label">رقم الحركة بالمخزن:</span>
                <span class="mgmt-detail-field-value font-mono font-bold">#${this.escapeHtml(s.movementNumber || s.id)}</span>
              </div>
              <div class="mgmt-detail-field">
                <span class="mgmt-detail-field-label">رقم المرجع (Ref):</span>
                <span class="mgmt-detail-field-value font-mono">${this.escapeHtml(s.referenceNumber || '-')}</span>
              </div>
              <div class="mgmt-detail-field">
                <span class="mgmt-detail-field-label">التاريخ والوقت:</span>
                <span class="mgmt-detail-field-value font-mono">${this.formatDate(s.transactionDate)}</span>
              </div>
              <div class="mgmt-detail-field">
                <span class="mgmt-detail-field-label">نوع العملية:</span>
                <span class="mgmt-detail-field-value">${this.renderTransactionTypeBadge(s.action)}</span>
              </div>
              <div class="mgmt-detail-field">
                <span class="mgmt-detail-field-label">نوع الخدمة:</span>
                <span class="mgmt-detail-field-value">${this.escapeHtml(s.serviceType || '-')}</span>
              </div>
              <div class="mgmt-detail-field">
                <span class="mgmt-detail-field-label">نوع الخامة:</span>
                <span class="mgmt-detail-field-value font-bold">${this.escapeHtml(s.material || '-')}</span>
              </div>
              <div class="mgmt-detail-field">
                <span class="mgmt-detail-field-label">أمر الشراء المرتبط (PO):</span>
                <span class="mgmt-detail-field-value font-mono text-gold">${this.escapeHtml(s.poNumber || '-')}</span>
              </div>
              <div class="mgmt-detail-field">
                <span class="mgmt-detail-field-label">الكمية الواردة (إيداع):</span>
                <span class="mgmt-detail-field-value font-mono text-green font-bold">${s.incomingQuantity > 0 ? `${this.formatNumber(s.incomingQuantity, 3)} طن` : '-'}</span>
              </div>
              <div class="mgmt-detail-field">
                <span class="mgmt-detail-field-label">الكمية المنصرفة (صرف):</span>
                <span class="mgmt-detail-field-value font-mono text-red font-bold">${s.outgoingQuantity > 0 ? `${this.formatNumber(s.outgoingQuantity, 3)} طن` : '-'}</span>
              </div>
              <div class="mgmt-detail-field">
                <span class="mgmt-detail-field-label">الرصيد بعد الحركة:</span>
                <span class="mgmt-detail-field-value font-mono text-gold font-bold">${this.formatNumber(s.closingBalance, 3)} طن</span>
              </div>
              <div class="mgmt-detail-field">
                <span class="mgmt-detail-field-label">الميزان الداخلي:</span>
                <span class="mgmt-detail-field-value font-mono">${this.escapeHtml(s.internalWeighbridgeCode || '-')} (صافي: ${this.formatNumber(s.internalNetWeight, 3)} طن)</span>
              </div>
              <div class="mgmt-detail-field">
                <span class="mgmt-detail-field-label">الميزان الخارجي:</span>
                <span class="mgmt-detail-field-value font-mono">${this.escapeHtml(s.externalWeighbridgeCode || '-')} (صافي: ${this.formatNumber(s.externalNetWeight, 3)} طن)</span>
              </div>
              <div class="mgmt-detail-field">
                <span class="mgmt-detail-field-label">الموظف المسؤول:</span>
                <span class="mgmt-detail-field-value">${this.escapeHtml(s.receivingEmployee || '-')}</span>
              </div>
              <div class="mgmt-detail-field">
                <span class="mgmt-detail-field-label">الموقع / المستودع:</span>
                <span class="mgmt-detail-field-value">${this.escapeHtml(s.storageLocation || '-')}</span>
              </div>
              <div class="mgmt-detail-field" style="grid-column: 1 / -1;">
                <span class="mgmt-detail-field-label">الملاحظات والمرفقات:</span>
                <span class="mgmt-detail-field-value">${this.escapeHtml(s.notes || 'لا توجد ملاحظات')} ${s.hasAttachments ? '(يوجد ملفات مرفقة)' : ''}</span>
              </div>
            </div>
            <div style="margin-top: 1.5rem; text-align: left;">
              <button type="button" class="btn-mgmt-nav" onclick="window.ManagementApp.closeStorageModal()">إغلاق</button>
            </div>
          `;
        } else {
          this.storageModalBody.innerHTML = '<div class="mgmt-table-empty text-red">تعذر تحميل تفاصيل حركة المخزن.</div>';
        }
      } catch (err) {
        console.error('Error loading storage detail:', err);
        this.storageModalBody.innerHTML = '<div class="mgmt-table-empty text-red">خطأ في جلب بيانات حركة المخزن.</div>';
      }
    }

    closeStorageModal() {
      if (this.storageModalBackdrop) this.storageModalBackdrop.style.display = 'none';
    }

    /**
     * 4. Finance Subview Data
     */
    async loadFinanceData(silent = false) {
      const tbody = document.getElementById('mgmt-fin-po-tbody') || document.getElementById('mgmt-fin-tbody');
      const badgeCount = document.getElementById('mgmt-fin-po-count-badge') || document.getElementById('mgmt-fin-badge-count');
      if (tbody && !silent) tbody.innerHTML = this.renderLoadingRow(12);

      try {
        const res = await window.ChatApi.getManagementFinance(this.filters.finance);
        if (res && res.success && res.data) {
          const d = res.data;

          // 9 KPIs
          this.setElText('mgmt-fin-kpi-total-po-val', this.formatNumber(d.totalPoValue, 2));
          this.setElText('mgmt-fin-kpi-delivered-val', this.formatNumber(d.deliveredValue, 2));
          this.setElText('mgmt-fin-kpi-remaining-val', this.formatNumber(d.remainingValue, 2));
          this.setElText('mgmt-fin-kpi-waste-val', this.formatNumber(d.wasteValue, 2));
          this.setElText('mgmt-fin-kpi-delivered-qty', this.formatNumber(d.importedQuantity, 3));
          this.setElText('mgmt-fin-kpi-expenses', this.formatNumber(d.totalExpenses, 2));
          this.setElText('mgmt-fin-kpi-bank-balance', this.formatNumber(d.bankBalance, 2));
          
          const statusSummary = Object.entries(d.paymentStatusCounts || {})
            .map(([k, v]) => `${k}: ${v}`)
            .join(' | ') || 'مستقر';
          this.setElText('mgmt-fin-kpi-payment-status', statusSummary);
          this.setElText('mgmt-fin-kpi-perf', `${(d.performancePercentage || 0).toFixed(1)}%`);

          // POs Table
          const items = d.items || [];
          if (badgeCount) badgeCount.textContent = `${items.length} أمر شراء معروض`;
          this.renderFinanceTable(items);

          // Operational Expenses Table
          this.renderExpensesTable(d.recentExpenses || []);

          // Bank Transactions Table
          this.renderBankTransactionsTable(d.recentBankTransactions || []);
        } else {
          if (tbody) tbody.innerHTML = this.renderEmptyRow('لم يتم العثور على أوامر شراء مطابقة', 12);
        }
      } catch (err) {
        console.error('Error loading finance data:', err);
        if (tbody) tbody.innerHTML = this.renderErrorRow('تعذر تحميل بيانات المالية من الخادم', 12);
      }
    }

    renderFinanceTable(items) {
      const tbody = document.getElementById('mgmt-fin-po-tbody') || document.getElementById('mgmt-fin-tbody');
      if (!tbody) return;

      if (!items || items.length === 0) {
        tbody.innerHTML = this.renderEmptyRow('لا توجد أوامر شراء مسجلة', 12);
        return;
      }

      tbody.innerHTML = items.map(p => `
        <tr>
          <td><span class="mgmt-code-pill text-gold font-mono">${this.escapeHtml(p.poNumber)}</span></td>
          <td><strong>${this.escapeHtml(p.supplier)}</strong></td>
          <td class="font-mono">${this.formatDate(p.orderDate)}</td>
          <td class="font-mono">${this.formatNumber(p.quantity, 2)} طن</td>
          <td class="font-mono">${this.formatCurrency(p.tonPrice)}</td>
          <td class="font-mono text-gold"><strong>${this.formatCurrency(p.totalValue)}</strong></td>
          <td class="font-mono text-green">${this.formatNumber(p.deliveredQuantity, 2)} طن</td>
          <td class="font-mono">${this.formatNumber(p.remainingQuantity, 2)} طن</td>
          <td>${this.renderProgressBar(p.progressPercentage)}</td>
          <td>${this.renderPaymentBadge(p.paymentStatus)}</td>
          <td class="font-mono text-secondary">${this.escapeHtml(p.invoiceNumber || '-')}</td>
          <td>
            <button type="button" class="mgmt-action-btn" onclick="window.ManagementApp.openPoModal('${this.escapeHtml(p.poNumber)}')">
              عرض التفاصيل
            </button>
          </td>
        </tr>
      `).join('');
    }

    renderExpensesTable(expenses) {
      const tbody = this.finExpensesTbody || document.getElementById('mgmt-fin-expenses-tbody');
      const badge = this.finExpensesBadge || document.getElementById('mgmt-fin-expenses-badge');
      if (!tbody) return;

      if (badge) badge.textContent = `${expenses.length} مصروف مسجل`;

      if (!expenses || expenses.length === 0) {
        tbody.innerHTML = this.renderEmptyRow('لا توجد مصروفات تشغيلية مسجلة', 5);
        return;
      }

      tbody.innerHTML = expenses.map(e => `
        <tr>
          <td class="font-mono">${this.formatDate(e.expenseDate)}</td>
          <td><span class="mgmt-status-badge info">${this.escapeHtml(e.category || 'عام')}</span></td>
          <td>
            <div><strong>${this.escapeHtml(e.title || '-')}</strong></div>
            ${e.beneficiary ? `<div class="text-secondary" style="font-size: 0.8rem;">المستفيد: ${this.escapeHtml(e.beneficiary)}</div>` : ''}
          </td>
          <td class="font-mono text-red font-bold">${this.formatCurrency(e.amount)}</td>
          <td>${this.escapeHtml(e.createdBy || '-')}</td>
        </tr>
      `).join('');
    }

    renderBankTransactionsTable(transactions) {
      const tbody = this.finBankTbody || document.getElementById('mgmt-fin-bank-tbody');
      const badge = this.finBankBadge || document.getElementById('mgmt-fin-bank-badge');
      if (!tbody) return;

      if (badge) badge.textContent = `${transactions.length} معاملة بنكية مسجلة`;

      if (!transactions || transactions.length === 0) {
        tbody.innerHTML = this.renderEmptyRow('لا توجد معاملات بنكية مسجلة', 6);
        return;
      }

      tbody.innerHTML = transactions.map(b => {
        const isDeposit = (b.direction || '').toLowerCase() === 'in' || (b.direction || '').includes('إيداع') || (b.transactionType || '').includes('Deposit');
        return `
          <tr>
            <td class="font-mono">${this.formatDate(b.transactionDate)}</td>
            <td><strong>${this.escapeHtml(b.bankName || 'البنك')}</strong> - ${this.escapeHtml(b.transactionType || '-')}</td>
            <td>${isDeposit ? '<span class="mgmt-status-badge success">إيداع وارد</span>' : '<span class="mgmt-status-badge danger">سحب / تحويل</span>'}</td>
            <td class="font-mono ${isDeposit ? 'text-green' : 'text-red'} font-bold">${this.formatCurrency(b.amount)}</td>
            <td>${this.escapeHtml(b.description || '-')}</td>
            <td class="font-mono text-secondary">${this.escapeHtml(b.referenceNumber || '-')}</td>
          </tr>
        `;
      }).join('');
    }

    async openPoModal(poNumber) {
      if (!this.poModalBackdrop || !this.poModalBody) return;
      this.poModalBackdrop.style.display = 'flex';
      this.poModalBody.innerHTML = '<div class="mgmt-table-empty">جاري تحميل تفاصيل أمر الشراء...</div>';

      try {
        const res = await window.ChatApi.getManagementPurchaseOrderDetail(poNumber);
        if (res && res.success && res.data) {
          const p = res.data;
          this.poModalBody.innerHTML = `
            <div class="mgmt-detail-grid">
              <div class="mgmt-detail-field">
                <span class="mgmt-detail-field-label">رقم أمر الشراء:</span>
                <span class="mgmt-detail-field-value font-mono text-gold font-bold">${this.escapeHtml(p.poNumber)}</span>
              </div>
              <div class="mgmt-detail-field">
                <span class="mgmt-detail-field-label">المورد المتعاقد:</span>
                <span class="mgmt-detail-field-value font-bold">${this.escapeHtml(p.supplier)}</span>
              </div>
              <div class="mgmt-detail-field">
                <span class="mgmt-detail-field-label">تاريخ أمر الشراء:</span>
                <span class="mgmt-detail-field-value font-mono">${this.formatDate(p.orderDate)}</span>
              </div>
              <div class="mgmt-detail-field">
                <span class="mgmt-detail-field-label">الكمية المطلوبة:</span>
                <span class="mgmt-detail-field-value font-mono">${this.formatNumber(p.quantity, 2)} طن</span>
              </div>
              <div class="mgmt-detail-field">
                <span class="mgmt-detail-field-label">سعر الطن:</span>
                <span class="mgmt-detail-field-value font-mono">${this.formatCurrency(p.tonPrice)}</span>
              </div>
              <div class="mgmt-detail-field">
                <span class="mgmt-detail-field-label">القيمة الإجمالية:</span>
                <span class="mgmt-detail-field-value font-mono text-gold font-bold">${this.formatCurrency(p.totalValue)}</span>
              </div>
              <div class="mgmt-detail-field">
                <span class="mgmt-detail-field-label">الكمية الموردة:</span>
                <span class="mgmt-detail-field-value font-mono text-green font-bold">${this.formatNumber(p.deliveredQuantity, 2)} طن</span>
              </div>
              <div class="mgmt-detail-field">
                <span class="mgmt-detail-field-label">الكمية المتبقية:</span>
                <span class="mgmt-detail-field-value font-mono text-red">${this.formatNumber(p.remainingQuantity, 2)} طن</span>
              </div>
              <div class="mgmt-detail-field">
                <span class="mgmt-detail-field-label">كمية الهالك (Waste):</span>
                <span class="mgmt-detail-field-value font-mono text-red">${this.formatNumber(p.wasteQuantity, 2)} طن</span>
              </div>
              <div class="mgmt-detail-field">
                <span class="mgmt-detail-field-label">نسبة الإنجاز:</span>
                <span class="mgmt-detail-field-value">${this.renderProgressBar(p.progressPercentage)}</span>
              </div>
              <div class="mgmt-detail-field">
                <span class="mgmt-detail-field-label">حالة السداد:</span>
                <span class="mgmt-detail-field-value">${this.renderPaymentBadge(p.paymentStatus)}</span>
              </div>
              <div class="mgmt-detail-field">
                <span class="mgmt-detail-field-label">رقم الفاتورة:</span>
                <span class="mgmt-detail-field-value font-mono text-secondary">${this.escapeHtml(p.invoiceNumber || '-')}</span>
              </div>
              <div class="mgmt-detail-field">
                <span class="mgmt-detail-field-label">شحنات النقل المسجلة:</span>
                <span class="mgmt-detail-field-value font-mono">${this.formatNumber(p.transportationTripsCount || 0, 0)} نقلة (صافي: ${this.formatNumber(p.transportationDeliveredWeight || 0, 3)} طن)</span>
              </div>
              <div class="mgmt-detail-field">
                <span class="mgmt-detail-field-label">حركات المخزن المسجلة:</span>
                <span class="mgmt-detail-field-value font-mono">${this.formatNumber(p.storageTransactionsCount || 0, 0)} حركة (وارد: ${this.formatNumber(p.storageDeliveredQuantity || 0, 3)} طن)</span>
              </div>
            </div>
            <div style="margin-top: 1.5rem; text-align: left;">
              <button type="button" class="btn-mgmt-nav" onclick="window.ManagementApp.closePoModal()">إغلاق</button>
            </div>
          `;
        } else {
          this.poModalBody.innerHTML = '<div class="mgmt-table-empty text-red">تعذر تحميل تفاصيل أمر الشراء.</div>';
        }
      } catch (err) {
        console.error('Error loading PO detail:', err);
        this.poModalBody.innerHTML = '<div class="mgmt-table-empty text-red">خطأ في جلب بيانات أمر الشراء.</div>';
      }
    }

    closePoModal() {
      if (this.poModalBackdrop) this.poModalBackdrop.style.display = 'none';
    }

    /**
     * 5. Employees Subview Data
     */
    async loadEmployeesData(silent = false) {
      const tbody = document.getElementById('mgmt-emp-tbody');
      const badgeCount = document.getElementById('mgmt-emp-count-badge') || document.getElementById('mgmt-emp-badge-count');
      if (tbody && !silent) tbody.innerHTML = this.renderLoadingRow(9);

      try {
        const res = await window.ChatApi.getManagementEmployees(this.filters.employees);
        if (res && res.success && Array.isArray(res.data)) {
          const items = res.data;

          const totalEmployees = items.length;
          const salaried = items.filter(e => e.hasSalaryRecord).length;
          const totalLending = items.reduce((sum, e) => sum + (e.remainingLending || 0), 0);

          // 4 KPIs
          this.setElText('mgmt-emp-kpi-total', this.formatNumber(totalEmployees, 0));
          this.setElText('mgmt-emp-kpi-salaried', this.formatNumber(salaried, 0));
          this.setElText('mgmt-emp-kpi-lending', this.formatCurrency(totalLending));
          this.setElText('mgmt-emp-kpi-attendance', 'منتظم');

          // Employees Table
          if (badgeCount) badgeCount.textContent = `${items.length} موظف معروض`;
          this.renderEmployeesTable(items);
        } else {
          if (tbody) tbody.innerHTML = this.renderEmptyRow('لم يتم العثور على موظفين مطابقين', 9);
        }
      } catch (err) {
        console.error('Error loading employees data:', err);
        if (tbody) tbody.innerHTML = this.renderErrorRow('تعذر تحميل بيانات الموظفين من الخادم', 9);
      }
    }

    renderEmployeesTable(items) {
      const tbody = document.getElementById('mgmt-emp-tbody');
      if (!tbody) return;

      if (!items || items.length === 0) {
        tbody.innerHTML = this.renderEmptyRow('لا يوجد موظفون مسجلون', 9);
        return;
      }

      tbody.innerHTML = items.map(e => `
        <tr>
          <td>
            <div class="mgmt-user-pill" style="padding: 0; background: transparent;">
              <div class="mgmt-user-avatar" style="width: 32px; height: 32px; font-size: 0.8rem;">${this.getInitials(e.fullName || e.username)}</div>
              <div class="mgmt-user-meta">
                <span class="mgmt-user-name">${this.escapeHtml(e.fullName || e.username)}</span>
                <span class="mgmt-user-role font-mono">${this.escapeHtml(e.username || '')}</span>
              </div>
            </div>
          </td>
          <td>${this.escapeHtml(e.department || 'عام')}</td>
          <td>${this.escapeHtml(e.position || e.role || '-')}</td>
          <td><span class="mgmt-status-badge info" title="نظام التقييم غير مدرج بعد في قاعدة البيانات">${this.escapeHtml(e.rate || 'التقييم غير مدرج حالياً')}</span></td>
          <td>
            ${e.hasSalaryRecord 
              ? `<strong class="text-gold font-mono">${this.formatCurrency(e.basicSalary)}</strong>` 
              : `<span class="text-secondary">${this.escapeHtml(e.salaryStatusMessage || 'غير مسجل بالنظام المالي')}</span>`
            }
          </td>
          <td class="font-mono">
            ${e.remainingLending > 0 
              ? `<span class="text-red font-bold">${this.formatCurrency(e.remainingLending)}</span>` 
              : `<span class="text-green">0.00 ج.م</span>`
            }
          </td>
          <td>${e.daysPresent > 0 ? `${e.daysPresent} يوم حضور` : 'غير مسجل'}</td>
          <td>${this.renderFinancialStatusBadge(e.financialStatus)}</td>
          <td>
            <button type="button" class="mgmt-action-btn" onclick="window.ManagementApp.openEmployeeModal('${this.escapeHtml(e.id)}')">
              عرض التفاصيل
            </button>
          </td>
        </tr>
      `).join('');
    }

    async openEmployeeModal(empId) {
      if (!this.empModalBackdrop || !this.empModalBody) return;
      this.empModalBackdrop.style.display = 'flex';
      this.empModalBody.innerHTML = '<div class="mgmt-table-empty">جاري تحميل ملف الموظف...</div>';

      try {
        const res = await window.ChatApi.getManagementEmployeeDetail(empId);
        if (res && res.success && res.data) {
          const detail = res.data;
          const emp = detail.employee || detail;
          this.empModalBody.innerHTML = `
            <div class="mgmt-detail-grid">
              <div class="mgmt-detail-field">
                <span class="mgmt-detail-field-label">اسم الموظف:</span>
                <span class="mgmt-detail-field-value font-bold">${this.escapeHtml(emp.fullName || emp.username)}</span>
              </div>
              <div class="mgmt-detail-field">
                <span class="mgmt-detail-field-label">اسم المستخدم:</span>
                <span class="mgmt-detail-field-value font-mono">${this.escapeHtml(emp.username)}</span>
              </div>
              <div class="mgmt-detail-field">
                <span class="mgmt-detail-field-label">البريد الإلكتروني:</span>
                <span class="mgmt-detail-field-value font-mono">${this.escapeHtml(emp.email || '-')}</span>
              </div>
              <div class="mgmt-detail-field">
                <span class="mgmt-detail-field-label">القسم:</span>
                <span class="mgmt-detail-field-value">${this.escapeHtml(emp.department || 'عام')}</span>
              </div>
              <div class="mgmt-detail-field">
                <span class="mgmt-detail-field-label">المسمى الوظيفي:</span>
                <span class="mgmt-detail-field-value">${this.escapeHtml(emp.position || emp.role || '-')}</span>
              </div>
              <div class="mgmt-detail-field">
                <span class="mgmt-detail-field-label">تقييم الموظف (Rate):</span>
                <span class="mgmt-detail-field-value">
                  <span class="mgmt-status-badge info">${this.escapeHtml(emp.rate || 'التقييم غير مدرج حالياً في النظام')}</span>
                </span>
              </div>
              <div class="mgmt-detail-field">
                <span class="mgmt-detail-field-label">الراتب الأساسي:</span>
                <span class="mgmt-detail-field-value">
                  ${emp.hasSalaryRecord ? `<strong class="text-gold font-mono">${this.formatCurrency(emp.basicSalary)}</strong>` : `<span class="text-secondary">${this.escapeHtml(emp.salaryStatusMessage || 'غير مسجل بالنظام المالي')}</span>`}
                </span>
              </div>
              <div class="mgmt-detail-field">
                <span class="mgmt-detail-field-label">إجمالي السلف:</span>
                <span class="mgmt-detail-field-value font-mono text-red">${this.formatCurrency(emp.totalLending)}</span>
              </div>
              <div class="mgmt-detail-field">
                <span class="mgmt-detail-field-label">المسدد من السلف:</span>
                <span class="mgmt-detail-field-value font-mono text-green">${this.formatCurrency(emp.paidLending)}</span>
              </div>
              <div class="mgmt-detail-field">
                <span class="mgmt-detail-field-label">المتبقي من السلف:</span>
                <span class="mgmt-detail-field-value font-mono text-red font-bold">${this.formatCurrency(emp.remainingLending)}</span>
              </div>
              <div class="mgmt-detail-field">
                <span class="mgmt-detail-field-label">آخر حركة سلفة:</span>
                <span class="mgmt-detail-field-value font-mono">${emp.lastLendingDate ? this.formatDate(emp.lastLendingDate) : 'لا توجد حركات سلف'}</span>
              </div>
              <div class="mgmt-detail-field">
                <span class="mgmt-detail-field-label">حالة الحضور:</span>
                <span class="mgmt-detail-field-value">${emp.daysPresent > 0 ? `${emp.daysPresent} يوم حضور` : 'غير مسجل'}</span>
              </div>
            </div>
            <div style="margin-top: 1.5rem; text-align: left;">
              <button type="button" class="btn-mgmt-nav" onclick="window.ManagementApp.closeEmployeeModal()">إغلاق</button>
            </div>
          `;
        } else {
          this.empModalBody.innerHTML = '<div class="mgmt-table-empty text-red">تعذر تحميل بيانات الموظف.</div>';
        }
      } catch (err) {
        console.error('Error loading employee detail modal:', err);
        this.empModalBody.innerHTML = '<div class="mgmt-table-empty text-red">خطأ في جلب بيانات الموظف.</div>';
      }
    }

    closeEmployeeModal() {
      if (this.empModalBackdrop) this.empModalBackdrop.style.display = 'none';
    }

    /**
     * 6. Suppliers Subview Data
     */
    async loadSuppliersData(silent = false) {
      const tbody = document.getElementById('mgmt-sup-tbody');
      const badgeCount = document.getElementById('mgmt-sup-count-badge') || document.getElementById('mgmt-sup-badge-count');
      if (tbody && !silent) tbody.innerHTML = this.renderLoadingRow(12);

      try {
        const res = await window.ChatApi.getManagementSuppliers(this.filters.suppliers);
        if (res && res.success && Array.isArray(res.data)) {
          const items = res.data;

          const totalSuppliers = items.length;
          const totalPos = items.reduce((s, x) => s + (x.posCount || 0), 0);
          const origQty = items.reduce((s, x) => s + (x.originalQuantity || 0), 0);
          const delivQty = items.reduce((s, x) => s + (x.deliveredQuantity || 0), 0);
          const remQty = items.reduce((s, x) => s + (x.remainingQuantity || 0), 0);
          const overallProgress = origQty > 0 ? (delivQty / origQty) * 100 : 0;

          // 6 KPIs
          this.setElText('mgmt-sup-kpi-count', this.formatNumber(totalSuppliers, 0));
          this.setElText('mgmt-sup-kpi-pos', this.formatNumber(totalPos, 0));
          this.setElText('mgmt-sup-kpi-orig-qty', this.formatNumber(origQty, 3));
          this.setElText('mgmt-sup-kpi-delivered-qty', this.formatNumber(delivQty, 3));
          this.setElText('mgmt-sup-kpi-rem-qty', this.formatNumber(remQty, 3));
          this.setElText('mgmt-sup-kpi-progress', `${overallProgress.toFixed(1)}%`);

          // Table
          if (badgeCount) badgeCount.textContent = `${items.length} مورد معروض`;
          this.renderSuppliersTable(items);
        } else {
          if (tbody) tbody.innerHTML = this.renderEmptyRow('لم يتم العثور على موردين مطابقين', 12);
        }
      } catch (err) {
        console.error('Error loading suppliers data:', err);
        if (tbody) tbody.innerHTML = this.renderErrorRow('تعذر تحميل بيانات الموردين من الخادم', 12);
      }
    }

    renderSuppliersTable(items) {
      const tbody = document.getElementById('mgmt-sup-tbody');
      if (!tbody) return;

      if (!items || items.length === 0) {
        tbody.innerHTML = this.renderEmptyRow('لا يوجد موردون مسجلون', 12);
        return;
      }

      tbody.innerHTML = items.map(s => `
        <tr>
          <td><strong>${this.escapeHtml(s.supplierName)}</strong></td>
          <td class="font-mono text-center">${this.formatNumber(s.posCount, 0)}</td>
          <td class="font-mono">${this.formatNumber(s.originalQuantity, 2)} طن</td>
          <td class="font-mono text-green">${this.formatNumber(s.deliveredQuantity, 2)} طن</td>
          <td class="font-mono text-gold">${this.formatNumber(s.remainingQuantity, 2)} طن</td>
          <td class="font-mono text-red">${this.formatNumber(s.wastedQuantity, 2)} طن</td>
          <td class="font-mono">${this.formatCurrency(s.tonPrice)}</td>
          <td class="font-mono text-gold">${this.formatCurrency(s.expectedValue)}</td>
          <td class="font-mono text-green">${this.formatCurrency(s.deliveredValue)}</td>
          <td class="font-mono text-secondary">${this.formatCurrency(s.remainingValue)}</td>
          <td>${this.renderProgressBar(s.progressPercentage)}</td>
          <td>
            <button type="button" class="mgmt-action-btn" onclick="window.ManagementApp.navigateTo('/management/suppliers/' + encodeURIComponent('${this.escapeHtml(s.supplierName)}'))">
              عرض التفاصيل
            </button>
          </td>
        </tr>
      `).join('');
    }

    /**
     * 7. Supplier Details Subview Data
     */
    async loadSupplierDetail(supplierId, silent = false) {
      const posTbody = document.getElementById('sup-detail-pos-tbody') || document.getElementById('mgmt-supdetail-pos-tbody');
      const transTbody = document.getElementById('sup-detail-trans-tbody') || document.getElementById('mgmt-supdetail-trans-tbody');
      const storageTbody = document.getElementById('sup-detail-storage-tbody') || document.getElementById('mgmt-supdetail-storage-tbody');

      if (posTbody && !silent) posTbody.innerHTML = this.renderLoadingRow(10);
      if (transTbody && !silent) transTbody.innerHTML = this.renderLoadingRow(10);
      if (storageTbody && !silent) storageTbody.innerHTML = this.renderLoadingRow(8);

      try {
        const res = await window.ChatApi.getManagementSupplierDetail(supplierId);
        if (res && res.success && res.data) {
          const d = res.data;
          const s = d.summary || {};

          this.setElText('mgmt-supdetail-name', s.supplierName || supplierId);
          this.setElText('mgmt-supdetail-materials', `الخامات الموردة: ${this.escapeHtml(s.materials || '-')}`);

          // KPIs
          this.setElText('mgmt-supdetail-kpi-pos', this.formatNumber(s.posCount, 0));
          this.setElText('mgmt-supdetail-kpi-orig-qty', this.formatNumber(s.originalQuantity, 3));
          this.setElText('mgmt-supdetail-kpi-delivered-qty', this.formatNumber(s.deliveredQuantity, 3));
          this.setElText('mgmt-supdetail-kpi-rem-qty', this.formatNumber(s.remainingQuantity, 3));
          this.setElText('mgmt-supdetail-kpi-waste', this.formatNumber(s.wastedQuantity, 3));
          this.setElText('mgmt-supdetail-kpi-expected-val', this.formatNumber(s.expectedValue, 2));
          this.setElText('mgmt-supdetail-kpi-delivered-val', this.formatNumber(s.deliveredValue, 2));
          this.setElText('mgmt-supdetail-kpi-rem-val', this.formatNumber(s.remainingValue, 2));
          this.setElText('mgmt-supdetail-kpi-progress', `${(s.progressPercentage || 0).toFixed(1)}%`);

          // 1. POs Table
          const pos = d.purchaseOrders || [];
          const posBadge = document.getElementById('sup-detail-pos-badge');
          if (posBadge) posBadge.textContent = `${pos.length} PO`;
          if (posTbody) {
            if (pos.length === 0) {
              posTbody.innerHTML = this.renderEmptyRow('لا توجد أوامر شراء مربوطة بهذا المورد', 10);
            } else {
              posTbody.innerHTML = pos.map(p => `
                <tr>
                  <td><span class="mgmt-code-pill text-gold font-mono">${this.escapeHtml(p.poNumber)}</span></td>
                  <td class="font-mono">${this.formatDate(p.orderDate)}</td>
                  <td class="font-mono">${this.formatNumber(p.quantity, 2)} طن</td>
                  <td class="font-mono">${this.formatCurrency(p.tonPrice)}</td>
                  <td class="font-mono text-gold">${this.formatCurrency(p.totalValue)}</td>
                  <td class="font-mono text-green">${this.formatNumber(p.deliveredQuantity, 2)} طن</td>
                  <td class="font-mono">${this.formatNumber(p.remainingQuantity, 2)} طن</td>
                  <td>${this.renderProgressBar(p.progressPercentage)}</td>
                  <td>${this.renderPaymentBadge(p.paymentStatus)}</td>
                  <td class="font-mono text-secondary">${this.escapeHtml(p.invoiceNumber || '-')}</td>
                </tr>
              `).join('');
            }
          }

          // 2. Transportation Table
          const trans = d.transportationTransactions || [];
          const transBadge = document.getElementById('sup-detail-trans-badge');
          if (transBadge) transBadge.textContent = `${trans.length} حركة نقل`;
          if (transTbody) {
            if (trans.length === 0) {
              transTbody.innerHTML = this.renderEmptyRow('لا توجد شحنات نقل مسجلة لهذا المورد', 10);
            } else {
              transTbody.innerHTML = trans.map(t => `
                <tr>
                  <td><span class="mgmt-code-pill font-mono">#${this.escapeHtml(t.transactionNumber || t.id)}</span></td>
                  <td class="font-mono">${this.formatDate(t.transactionDate)}</td>
                  <td><span class="mgmt-code-pill text-gold font-mono">${this.escapeHtml(t.poNumber || '-')}</span></td>
                  <td>${this.escapeHtml(t.materialType || '-')}</td>
                  <td class="font-mono">${this.escapeHtml(t.carNumber || '-')}</td>
                  <td>${this.escapeHtml(t.driverName || '-')}</td>
                  <td class="font-mono text-green"><strong>${this.formatNumber(t.netWeight, 3)}</strong> طن</td>
                  <td class="font-mono text-red">${this.formatNumber(t.wasteQuantity, 3)} طن</td>
                  <td>${this.renderStatusBadge(t.receivingStatus)}</td>
                  <td>${this.escapeHtml(t.destination || '-')}</td>
                </tr>
              `).join('');
            }
          }

          // 3. Storage Table
          const storage = d.storageTransactions || [];
          const storageBadge = document.getElementById('sup-detail-storage-badge');
          if (storageBadge) storageBadge.textContent = `${storage.length} حركة مخزن`;
          if (storageTbody) {
            if (storage.length === 0) {
              storageTbody.innerHTML = this.renderEmptyRow('لا توجد حركات مخزن مسجلة لهذا المورد', 8);
            } else {
              storageTbody.innerHTML = storage.map(s => `
                <tr>
                  <td><span class="mgmt-code-pill font-mono">#${this.escapeHtml(s.movementNumber || s.id)}</span></td>
                  <td class="font-mono">${this.formatDate(s.transactionDate)}</td>
                  <td>${this.renderTransactionTypeBadge(s.action)}</td>
                  <td><strong>${this.escapeHtml(s.material || '-')}</strong></td>
                  <td class="font-mono text-green">${s.incomingQuantity > 0 ? this.formatNumber(s.incomingQuantity, 3) : '-'}</td>
                  <td class="font-mono text-red">${s.outgoingQuantity > 0 ? this.formatNumber(s.outgoingQuantity, 3) : '-'}</td>
                  <td class="font-mono text-gold"><strong>${this.formatNumber(s.closingBalance, 3)}</strong> طن</td>
                  <td>${this.escapeHtml(s.storageLocation || '-')}</td>
                </tr>
              `).join('');
            }
          }

        } else {
          this.setElText('mgmt-supdetail-name', 'تعذر تحميل بيانات المورد');
        }
      } catch (err) {
        console.error('Error loading supplier detail:', err);
      }
    }

    /* ------------------------------------------------------------------------
       Global Search Engine (Server-Side Grouped Results)
       ------------------------------------------------------------------------ */

    handleGlobalSearchInput(query) {
      const q = (query || '').trim();

      if (this.globalSearchClearBtn) {
        this.globalSearchClearBtn.style.display = q.length > 0 ? 'inline-block' : 'none';
      }

      if (q.length < 2) {
        this.hideSearchDropdown();
        return;
      }

      clearTimeout(this.searchDebounceTimer);
      this.searchDebounceTimer = setTimeout(async () => {
        this.executeGlobalSearch(q);
      }, 300);
    }

    async executeGlobalSearch(query) {
      if (!this.globalSearchResultsContainer) return;

      if (this.globalSearchSpinner) this.globalSearchSpinner.style.display = 'block';
      this.showSearchDropdown();
      this.globalSearchResultsContainer.innerHTML = '<div class="mgmt-search-item"><div class="mgmt-search-item-title text-secondary">جاري البحث في قاعدة البيانات...</div></div>';

      try {
        const res = await window.ChatApi.getManagementSearch(query);
        if (this.globalSearchSpinner) this.globalSearchSpinner.style.display = 'none';

        if (res && res.success && res.data) {
          this.renderGroupedSearchResults(res.data, query);
        } else {
          this.globalSearchResultsContainer.innerHTML = '<div class="mgmt-search-item"><div class="mgmt-search-item-title text-secondary">لا توجد نتائج مطابقة لـ "' + this.escapeHtml(query) + '"</div></div>';
        }
      } catch (err) {
        if (this.globalSearchSpinner) this.globalSearchSpinner.style.display = 'none';
        console.error('Global search error:', err);
        this.globalSearchResultsContainer.innerHTML = '<div class="mgmt-search-item"><div class="mgmt-search-item-title text-red">خطأ في تنفيذ عملية البحث</div></div>';
      }
    }

    renderGroupedSearchResults(data, query) {
      if (!this.globalSearchResultsContainer) return;

      const hasResults = (data.purchaseOrders?.length > 0) ||
                         (data.transportation?.length > 0) ||
                         (data.storage?.length > 0) ||
                         (data.finance?.length > 0) ||
                         (data.employees?.length > 0) ||
                         (data.suppliers?.length > 0);

      if (!hasResults) {
        this.globalSearchResultsContainer.innerHTML = `
          <div class="mgmt-search-item">
            <div class="mgmt-search-item-title text-secondary">لم يتم العثور على أي نتائج مطابقة لكلمة البحث: <strong>${this.escapeHtml(query)}</strong></div>
          </div>
        `;
        return;
      }

      let html = '';

      // 1. PURCHASE ORDERS
      if (data.purchaseOrders && data.purchaseOrders.length > 0) {
        html += `<div class="mgmt-search-group-header">📑 PURCHASE ORDERS (${data.purchaseOrders.length})</div>`;
        html += data.purchaseOrders.map(item => `
          <div class="mgmt-search-item" onclick="window.ManagementApp.openPoModal('${this.escapeHtml(item.title)}')">
            <div style="display: flex; justify-content: space-between; align-items: center; width: 100%;">
              <div class="mgmt-search-item-title text-gold font-bold">${this.escapeHtml(item.title)}</div>
              <span class="mgmt-code-pill" style="font-size: 0.75rem;">${this.escapeHtml(item.actionLabel || '[ عرض أمر الشراء ]')}</span>
            </div>
            <div class="mgmt-search-item-subtitle">${this.escapeHtml(item.subtitle)}</div>
            <div class="mgmt-search-item-meta">${this.escapeHtml(item.details)}</div>
          </div>
        `).join('');
      }

      // 2. TRANSPORTATION
      if (data.transportation && data.transportation.length > 0) {
        html += `<div class="mgmt-search-group-header">🚚 TRANSPORTATION (${data.transportation.length})</div>`;
        html += data.transportation.map(item => `
          <div class="mgmt-search-item" onclick="window.ManagementApp.openTransportationModal(${item.id})">
            <div style="display: flex; justify-content: space-between; align-items: center; width: 100%;">
              <div class="mgmt-search-item-title">${this.escapeHtml(item.title)}</div>
              <span class="mgmt-code-pill" style="font-size: 0.75rem;">${this.escapeHtml(item.actionLabel || '[ عرض النقل ]')}</span>
            </div>
            <div class="mgmt-search-item-subtitle">${this.escapeHtml(item.subtitle)}</div>
            <div class="mgmt-search-item-meta">${this.escapeHtml(item.details)}</div>
          </div>
        `).join('');
      }

      // 3. STORAGE
      if (data.storage && data.storage.length > 0) {
        html += `<div class="mgmt-search-group-header">📦 STORAGE (${data.storage.length})</div>`;
        html += data.storage.map(item => `
          <div class="mgmt-search-item" onclick="window.ManagementApp.openStorageModal(${item.id})">
            <div style="display: flex; justify-content: space-between; align-items: center; width: 100%;">
              <div class="mgmt-search-item-title">${this.escapeHtml(item.title)}</div>
              <span class="mgmt-code-pill" style="font-size: 0.75rem;">${this.escapeHtml(item.actionLabel || '[ عرض المخزن ]')}</span>
            </div>
            <div class="mgmt-search-item-subtitle">${this.escapeHtml(item.subtitle)}</div>
            <div class="mgmt-search-item-meta">${this.escapeHtml(item.details)}</div>
          </div>
        `).join('');
      }

      // 4. FINANCE
      if (data.finance && data.finance.length > 0) {
        html += `<div class="mgmt-search-group-header">💰 FINANCE (${data.finance.length})</div>`;
        html += data.finance.map(item => `
          <div class="mgmt-search-item" onclick="window.ManagementApp.navigateTo('/management/finance', '${this.escapeHtml(item.title)}')">
            <div style="display: flex; justify-content: space-between; align-items: center; width: 100%;">
              <div class="mgmt-search-item-title">${this.escapeHtml(item.title)}</div>
              <span class="mgmt-code-pill" style="font-size: 0.75rem;">${this.escapeHtml(item.actionLabel || '[ عرض المالية ]')}</span>
            </div>
            <div class="mgmt-search-item-subtitle">${this.escapeHtml(item.subtitle)}</div>
            <div class="mgmt-search-item-meta">${this.escapeHtml(item.details)}</div>
          </div>
        `).join('');
      }

      // 5. EMPLOYEES
      if (data.employees && data.employees.length > 0) {
        html += `<div class="mgmt-search-group-header">👥 EMPLOYEES (${data.employees.length})</div>`;
        html += data.employees.map(item => `
          <div class="mgmt-search-item" onclick="window.ManagementApp.openEmployeeModal('${this.escapeHtml(item.id)}')">
            <div style="display: flex; justify-content: space-between; align-items: center; width: 100%;">
              <div class="mgmt-search-item-title">${this.escapeHtml(item.title)}</div>
              <span class="mgmt-code-pill" style="font-size: 0.75rem;">${this.escapeHtml(item.actionLabel || '[ عرض الموظف ]')}</span>
            </div>
            <div class="mgmt-search-item-subtitle">${this.escapeHtml(item.subtitle)}</div>
            <div class="mgmt-search-item-meta">${this.escapeHtml(item.details)}</div>
          </div>
        `).join('');
      }

      // 6. SUPPLIERS
      if (data.suppliers && data.suppliers.length > 0) {
        html += `<div class="mgmt-search-group-header">🏭 SUPPLIERS (${data.suppliers.length})</div>`;
        html += data.suppliers.map(item => `
          <div class="mgmt-search-item" onclick="window.ManagementApp.navigateTo('/management/suppliers/' + encodeURIComponent('${this.escapeHtml(item.id || item.title)}'))">
            <div style="display: flex; justify-content: space-between; align-items: center; width: 100%;">
              <div class="mgmt-search-item-title">${this.escapeHtml(item.title)}</div>
              <span class="mgmt-code-pill" style="font-size: 0.75rem;">${this.escapeHtml(item.actionLabel || '[ عرض المورد ]')}</span>
            </div>
            <div class="mgmt-search-item-subtitle">${this.escapeHtml(item.subtitle)}</div>
            <div class="mgmt-search-item-meta">${this.escapeHtml(item.details)}</div>
          </div>
        `).join('');
      }

      this.globalSearchResultsContainer.innerHTML = html;
    }

    showSearchDropdown() {
      if (this.globalSearchDropdown) this.globalSearchDropdown.classList.add('show');
    }

    hideSearchDropdown() {
      if (this.globalSearchDropdown) this.globalSearchDropdown.classList.remove('show');
    }

    /* ------------------------------------------------------------------------
       UI Badges & Progress Bars
       ------------------------------------------------------------------------ */

    renderProgressBar(percentage) {
      const p = Math.max(0, Math.min(100, percentage || 0));
      return `
        <div class="mgmt-progress-wrap" title="${p.toFixed(1)}%">
          <div class="mgmt-progress-bar">
            <div class="mgmt-progress-fill" style="width: ${p}%"></div>
          </div>
          <span class="mgmt-progress-label font-mono">${p.toFixed(0)}%</span>
        </div>
      `;
    }

    renderStatusBadge(status) {
      const s = (status || '').toLowerCase();
      if (s.includes('approved') || s.includes('accepted') || s.includes('معتمد') || s.includes('مقبول') || s.includes('safe') || s.includes('intact')) {
        return '<span class="mgmt-status-badge success">معتمد ومطابق</span>';
      }
      if (s.includes('partially') || s.includes('جزئي')) {
        return '<span class="mgmt-status-badge warning">قبول جزئي</span>';
      }
      if (s.includes('pending') || s.includes('انتظار')) {
        return '<span class="mgmt-status-badge warning">قيد الفحص</span>';
      }
      if (s.includes('reject') || s.includes('مرفوض')) {
        return '<span class="mgmt-status-badge danger">مرفوض</span>';
      }
      return `<span class="mgmt-status-badge info">${this.escapeHtml(status || 'قيد الإجراء')}</span>`;
    }

    renderTransactionTypeBadge(type) {
      const t = (type || '').toLowerCase();
      if (t === 'deposit' || t === 'إيداع' || t === 'وارد' || t.includes('in') || t.includes('import')) {
        return '<span class="mgmt-status-badge success">إيداع وارد</span>';
      }
      if (t === 'issue' || t === 'صرف' || t === 'صادر' || t.includes('out') || t.includes('export')) {
        return '<span class="mgmt-status-badge danger">صرف صادر</span>';
      }
      return `<span class="mgmt-status-badge info">${this.escapeHtml(type || 'حركة')}</span>`;
    }

    renderPaymentBadge(status) {
      const s = (status || '').toLowerCase();
      if (s.includes('paid') || s.includes('مسدد') || s.includes('مدفوع') || s.includes('بالكامل')) {
        return '<span class="mgmt-status-badge success">مدفوع</span>';
      }
      if (s.includes('partial') || s.includes('جزئي') || s.includes('جزئياً')) {
        return '<span class="mgmt-status-badge warning">سداد جزئي</span>';
      }
      return '<span class="mgmt-status-badge danger">مستحق السداد</span>';
    }

    renderFinancialStatusBadge(status) {
      const s = (status || '').toLowerCase();
      if (s.includes('سلف') || s.includes('lending') || s.includes('مستحق')) {
        return '<span class="mgmt-status-badge warning">توجد سلفة</span>';
      }
      return '<span class="mgmt-status-badge success">منتظم</span>';
    }

    renderLoadingRow(cols) {
      return `
        <tr>
          <td colspan="${cols}" class="mgmt-table-empty">
            <div class="mgmt-search-spinner" style="display:inline-block; margin: 0.5rem auto;"></div>
            <div>جاري تحميل البيانات الحية من قاعدة البيانات...</div>
          </td>
        </tr>
      `;
    }

    renderEmptyRow(msg, cols) {
      return `
        <tr>
          <td colspan="${cols}" class="mgmt-table-empty">${this.escapeHtml(msg)}</td>
        </tr>
      `;
    }

    renderErrorRow(msg, cols) {
      return `
        <tr>
          <td colspan="${cols}" class="mgmt-table-empty text-red">${this.escapeHtml(msg)}</td>
        </tr>
      `;
    }

    /* ------------------------------------------------------------------------
       Formatters & Utilities
       ------------------------------------------------------------------------ */

    formatNumber(num, decimals = 2) {
      if (num === null || num === undefined || isNaN(num)) return '-';
      return Number(num).toLocaleString('ar-EG', {
        minimumFractionDigits: decimals,
        maximumFractionDigits: decimals
      });
    }

    formatCurrency(num) {
      if (num === null || num === undefined || isNaN(num)) return '- ج.م';
      return `${this.formatNumber(num, 2)} ج.م`;
    }

    formatDate(dateStr) {
      if (!dateStr) return '-';
      try {
        const d = new Date(dateStr);
        if (isNaN(d.getTime())) return dateStr;
        return d.toLocaleDateString('ar-EG', {
          year: 'numeric',
          month: 'short',
          day: 'numeric'
        });
      } catch {
        return dateStr;
      }
    }

    getInitials(name) {
      if (!name) return 'FS';
      return name
        .split(' ')
        .filter(Boolean)
        .map(w => w[0])
        .slice(0, 2)
        .join('')
        .toUpperCase();
    }

    setElText(id, text) {
      const el = document.getElementById(id);
      if (el) el.textContent = text !== null && text !== undefined ? text : '-';
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
        let roleArabic = 'الإدارة العامة';
        const r = (user.role || '').toUpperCase();
        if (r === 'CEO') roleArabic = 'الرئيس التنفيذي';
        else if (r === 'IT') roleArabic = 'الدعم الفني / IT';
        else if (r === 'FACTORY_MANAGER' || r === 'FACTORY MANAGER') roleArabic = 'مدير المصنع';
        this.userRoleEl.textContent = roleArabic;
      }
      if (this.userAvatarEl) {
        this.userAvatarEl.textContent = this.getInitials(user.fullName || user.username);
      }
    }
  }

  // Instantiate global management instance
  window.ManagementApp = new ManagementDashboardManager();

  // Hook into ChatApp view transitions
  document.addEventListener('chat:view:changed', (e) => {
    if (e.detail && e.detail.view === 'management') {
      const user = window.ChatApi?.getCurrentUser();
      window.ManagementApp.updateUserBadge(user);
      window.ManagementApp.loadData();
    }
  });

})();
