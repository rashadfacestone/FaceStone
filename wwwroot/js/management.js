/**
 * FaceStone Management Department
 * Material Receiving, Weight Auditing & Logistics Oversight Dashboard
 * Supports Arabic RTL, Server-Side Filtering, Sorting, Pagination, and KPI Calculations.
 */

(function () {
  'use strict';

  // Localization Dictionary for Dropdown Display Text & Table Values
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

    // Receiving / Delivery Statuses
    'Approved / Accepted': 'مقبول / معتمد',
    'Partially Accepted': 'قبول جزئي',
    'Pending': 'قيد الانتظار',
    'Under Quality Test': 'قيد اختبارات الجودة',
    'Rejected': 'مرفوض'
  };

  function getArabicText(str) {
    if (!str) return '-';
    return ARABIC_LABELS[str] || str;
  }

  class ManagementDashboardManager {
    constructor() {
      this.state = {
        page: 1,
        pageSize: 10,
        search: '',
        materialType: 'all',
        receivingStatus: 'all',
        destination: 'all',
        fromDate: '',
        toDate: '',
        sortBy: 'date',
        sortOrder: 'desc',
        isLoading: false,
        totalRecords: 0,
        totalPages: 1
      };

      this.searchDebounceTimer = null;
      this.optionsPopulated = false;
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
      this.pageRoot = document.getElementById('management-page-root');
      if (!this.pageRoot) return;

      // Header Elements
      this.btnBackToDepts = document.getElementById('management-back-to-departments-btn');
      this.userNameEl = document.getElementById('mgmt-user-name');
      this.userRoleEl = document.getElementById('mgmt-user-role');
      this.userAvatarEl = document.getElementById('mgmt-user-avatar');

      // KPI Summary Elements
      this.summaryMovements = document.getElementById('mgmt-summary-movements');
      this.summaryReceived = document.getElementById('mgmt-summary-received');
      this.summaryAccepted = document.getElementById('mgmt-summary-accepted');
      this.summaryPending = document.getElementById('mgmt-summary-pending');

      // Filter Elements
      this.searchInput = document.getElementById('mgmt-search-input');
      this.searchClearBtn = document.getElementById('mgmt-search-clear-btn');
      this.dateFromInput = document.getElementById('mgmt-date-from');
      this.dateToInput = document.getElementById('mgmt-date-to');
      this.filterMaterialSelect = document.getElementById('mgmt-filter-material');
      this.filterStatusSelect = document.getElementById('mgmt-filter-status');
      this.filterDestinationSelect = document.getElementById('mgmt-filter-destination');
      this.resetFiltersBtn = document.getElementById('mgmt-reset-filters-btn');
      this.refreshBtn = document.getElementById('mgmt-refresh-btn');

      // Table & Pagination Elements
      this.tableBody = document.getElementById('mgmt-table-body');
      this.badgeCount = document.getElementById('mgmt-badge-count');
      this.paginationInfo = document.getElementById('mgmt-pagination-info');
      this.pageSizeSelect = document.getElementById('mgmt-page-size-select');
      this.prevBtn = document.getElementById('mgmt-prev-btn');
      this.nextBtn = document.getElementById('mgmt-next-btn');
      this.pageIndicator = document.getElementById('mgmt-page-indicator');
      this.sortableHeaders = this.pageRoot.querySelectorAll('th.sortable');

      this.attachEventListeners();
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

      // Search input with debounce
      this.searchInput?.addEventListener('input', (e) => {
        const val = e.target.value;
        if (this.searchClearBtn) {
          this.searchClearBtn.style.display = val.length > 0 ? 'block' : 'none';
        }
        clearTimeout(this.searchDebounceTimer);
        this.searchDebounceTimer = setTimeout(() => {
          this.state.search = val.trim();
          this.state.page = 1;
          this.loadData();
        }, 350);
      });

      // Search clear button
      this.searchClearBtn?.addEventListener('click', () => {
        if (this.searchInput) {
          this.searchInput.value = '';
          this.searchClearBtn.style.display = 'none';
        }
        this.state.search = '';
        this.state.page = 1;
        this.loadData();
      });

      // Date Filters
      this.dateFromInput?.addEventListener('change', (e) => {
        this.state.fromDate = e.target.value;
        this.state.page = 1;
        this.loadData();
      });

      this.dateToInput?.addEventListener('change', (e) => {
        this.state.toDate = e.target.value;
        this.state.page = 1;
        this.loadData();
      });

      // Dropdown Filters
      this.filterMaterialSelect?.addEventListener('change', (e) => {
        this.state.materialType = e.target.value;
        this.state.page = 1;
        this.loadData();
      });

      this.filterStatusSelect?.addEventListener('change', (e) => {
        this.state.receivingStatus = e.target.value;
        this.state.page = 1;
        this.loadData();
      });

      this.filterDestinationSelect?.addEventListener('change', (e) => {
        this.state.destination = e.target.value;
        this.state.page = 1;
        this.loadData();
      });

      // Reset Filters
      this.resetFiltersBtn?.addEventListener('click', () => {
        this.resetFilters();
      });

      // Refresh
      this.refreshBtn?.addEventListener('click', () => {
        this.loadData();
      });

      // Sortable Column Headers
      this.sortableHeaders.forEach(th => {
        th.addEventListener('click', () => {
          const sortKey = th.dataset.sort;
          if (!sortKey) return;

          if (this.state.sortBy === sortKey) {
            this.state.sortOrder = this.state.sortOrder === 'asc' ? 'desc' : 'asc';
          } else {
            this.state.sortBy = sortKey;
            this.state.sortOrder = 'desc';
          }

          this.updateSortHeaderIcons();
          this.state.page = 1;
          this.loadData();
        });
      });

      // Page Size Change
      this.pageSizeSelect?.addEventListener('change', (e) => {
        this.state.pageSize = parseInt(e.target.value, 10) || 10;
        this.state.page = 1;
        this.loadData();
      });

      // Pagination Buttons
      this.prevBtn?.addEventListener('click', () => {
        if (this.state.page > 1) {
          this.state.page--;
          this.loadData();
        }
      });

      this.nextBtn?.addEventListener('click', () => {
        if (this.state.page < this.state.totalPages) {
          this.state.page++;
          this.loadData();
        }
      });
    }

    updateSortHeaderIcons() {
      this.sortableHeaders.forEach(th => {
        const sortKey = th.dataset.sort;
        const icon = th.querySelector('.sort-icon');
        if (!icon) return;

        if (this.state.sortBy === sortKey) {
          th.classList.add('active-sort');
          icon.textContent = this.state.sortOrder === 'asc' ? '▲' : '▼';
        } else {
          th.classList.remove('active-sort');
          icon.textContent = '↕';
        }
      });
    }

    resetFilters() {
      this.state.search = '';
      this.state.materialType = 'all';
      this.state.receivingStatus = 'all';
      this.state.destination = 'all';
      this.state.fromDate = '';
      this.state.toDate = '';
      this.state.page = 1;

      if (this.searchInput) this.searchInput.value = '';
      if (this.searchClearBtn) this.searchClearBtn.style.display = 'none';
      if (this.dateFromInput) this.dateFromInput.value = '';
      if (this.dateToInput) this.dateToInput.value = '';
      if (this.filterMaterialSelect) this.filterMaterialSelect.value = 'all';
      if (this.filterStatusSelect) this.filterStatusSelect.value = 'all';
      if (this.filterDestinationSelect) this.filterDestinationSelect.value = 'all';

      this.loadData();
    }

    /**
     * Load Data from Backend API
     */
    async loadData() {
      if (this.state.isLoading) return;
      this.state.isLoading = true;
      this.renderLoadingState();

      try {
        const params = {
          search: this.state.search,
          materialType: this.state.materialType,
          receivingStatus: this.state.receivingStatus,
          destination: this.state.destination,
          fromDate: this.state.fromDate,
          toDate: this.state.toDate,
          sortBy: this.state.sortBy,
          sortOrder: this.state.sortOrder,
          page: this.state.page,
          pageSize: this.state.pageSize
        };

        const res = await ChatApi.getManagementData(params);

        if (res.success && res.data) {
          const data = res.data;
          this.state.totalRecords = data.totalCount;
          this.state.totalPages = data.totalPages;
          this.state.page = data.page;

          // 1. Render Summary KPIs
          this.renderSummary(data.summary);

          // 2. Populate Filter Dropdown Options (once or update)
          if (data.filterOptions && !this.optionsPopulated) {
            this.populateFilterDropdowns(data.filterOptions);
            this.optionsPopulated = true;
          }

          // 3. Render Table Rows
          this.renderTable(data.items);

          // 4. Render Pagination Controls
          this.renderPagination(data);
        } else {
          this.renderErrorState(res.message || 'فشل في تحميل بيانات الإدارة.');
        }
      } catch (err) {
        console.error('Error loading management data:', err);
        this.renderErrorState('حدث خطأ في الاتصال بالخادم أثناء استرجاع البيانات.');
      } finally {
        this.state.isLoading = false;
      }
    }

    renderSummary(summary) {
      if (!summary) return;

      if (this.summaryMovements) {
        this.summaryMovements.textContent = summary.totalMovements.toLocaleString('ar-EG');
      }
      if (this.summaryReceived) {
        this.summaryReceived.textContent = summary.totalReceivedAmount.toLocaleString('en-US', {
          minimumFractionDigits: 3, maximumFractionDigits: 3
        });
      }
      if (this.summaryAccepted) {
        this.summaryAccepted.textContent = summary.totalAcceptedQuantity.toLocaleString('en-US', {
          minimumFractionDigits: 3, maximumFractionDigits: 3
        });
      }
      if (this.summaryPending) {
        this.summaryPending.textContent = summary.totalUnacceptedPendingQuantity.toLocaleString('en-US', {
          minimumFractionDigits: 3, maximumFractionDigits: 3
        });
      }
    }

    populateFilterDropdowns(options) {
      if (!options) return;

      const populate = (selectEl, items, allText) => {
        if (!selectEl || !Array.isArray(items)) return;
        const currentVal = selectEl.value;
        let html = `<option value="all">${allText}</option>`;
        items.forEach(item => {
          const arabic = getArabicText(item);
          html += `<option value="${this.escapeHtml(item)}" ${currentVal === item ? 'selected' : ''}>${this.escapeHtml(arabic)}</option>`;
        });
        selectEl.innerHTML = html;
      };

      populate(this.filterMaterialSelect, options.materialTypes, 'جميع أنواع الخامات');
      populate(this.filterStatusSelect, options.receivingStatuses, 'جميع حالات الاستلام');
      populate(this.filterDestinationSelect, options.destinations, 'جميع الوجهات');
    }

    renderTable(items) {
      if (!this.tableBody) return;

      if (!items || items.length === 0) {
        this.tableBody.innerHTML = `
          <tr>
            <td colspan="12" class="mgmt-empty-cell">
              <div class="mgmt-empty-box">
                <svg width="40" height="40" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.8"><circle cx="11" cy="11" r="8"/><line x1="21" y1="21" x2="16.65" y2="16.65"/><line x1="8" y1="11" x2="14" y2="11"/></svg>
                <h3>لا توجد سجلات مطابقة للبحث أو الفلتر</h3>
                <p>يرجى تعديل معايير البحث أو الضغط على "إعادة ضبط" لعرض كافة السجلات المحفوظة.</p>
              </div>
            </td>
          </tr>
        `;
        if (this.badgeCount) this.badgeCount.textContent = '0 سجل';
        return;
      }

      if (this.badgeCount) {
        this.badgeCount.textContent = `إجمالي ${this.state.totalRecords} سجل`;
      }

      this.tableBody.innerHTML = items.map(r => {
        // Date formatting in Arabic locale
        const d = new Date(r.date);
        const dateFormatted = !isNaN(d.getTime()) 
          ? d.toLocaleDateString('ar-EG', { year: 'numeric', month: 'short', day: 'numeric' })
          : '-';

        // Material & Destination in Arabic
        const displayMaterial = getArabicText(r.materialType);
        const displayDestination = getArabicText(r.destination);
        const displayStatus = getArabicText(r.deliveryStatus);

        // Status Badge Style Class
        let statusClass = 'status-pending';
        const st = (r.deliveryStatus || '').toLowerCase();
        if (st.includes('approved') || st.includes('accepted') && !st.includes('partially') && !st.includes('unaccepted')) {
          statusClass = 'status-approved';
        } else if (st.includes('reject')) {
          statusClass = 'status-rejected';
        } else if (st.includes('test') || st.includes('quality')) {
          statusClass = 'status-testing';
        } else if (st.includes('partial')) {
          statusClass = 'status-partial';
        }

        // Car & Movement details
        let carsHtml = '';
        if (r.carMovements && r.carMovements.length > 0) {
          carsHtml = r.carMovements.map(cm => `
            <div class="mgmt-car-item">
              <span class="mgmt-car-plate">${this.escapeHtml(cm.carNumber)}</span>
              <span class="mgmt-movement-link">حركة #${cm.transactionNumber}</span>
            </div>
          `).join('');
        } else if (r.carPlatesNumber) {
          carsHtml = `<span class="mgmt-car-plate">${this.escapeHtml(r.carPlatesNumber)}</span>`;
        } else {
          carsHtml = '-';
        }

        const notesText = (r.notes && r.notes.trim() !== '') ? this.escapeHtml(r.notes) : '-';

        return `
          <tr>
            <!-- 1. رقم الحركة -->
            <td><strong class="mgmt-tx-cell">#${this.escapeHtml(r.transactionNumber)}</strong></td>

            <!-- 2. التاريخ -->
            <td class="mgmt-date-cell">${dateFormatted}</td>

            <!-- 3. رقم أمر الشراء (PO) -->
            <td><span class="mgmt-po-badge">${this.escapeHtml(r.poNumber || '-')}</span></td>

            <!-- 4. نوع الخامة -->
            <td class="mgmt-material-cell" title="${this.escapeHtml(displayMaterial)}">${this.escapeHtml(displayMaterial)}</td>

            <!-- 5. عدد النقلات -->
            <td><span class="mgmt-trips-pill">${r.numberOfTransactions} نقلة</span></td>

            <!-- 6. إجمالي الكمية المستلمة -->
            <td><strong class="mgmt-total-amount">${r.totalReceivedAmount.toFixed(3)} طن</strong></td>

            <!-- 7. أرقام السيارات / أرقام الحركات -->
            <td class="mgmt-cars-col">${carsHtml}</td>

            <!-- 8. الكمية المقبولة -->
            <td><span class="mgmt-accepted-val">${r.acceptedQuantity.toFixed(3)} طن</span></td>

            <!-- 9. الكمية المعلقة / غير المعتمدة -->
            <td><span class="mgmt-pending-val">${r.unacceptedPendingQuantity.toFixed(3)} طن</span></td>

            <!-- 10. حالة الاستلام -->
            <td><span class="mgmt-status-badge ${statusClass}">${this.escapeHtml(displayStatus)}</span></td>

            <!-- 11. الوجهة -->
            <td class="mgmt-dest-cell" title="${this.escapeHtml(displayDestination)}">${this.escapeHtml(displayDestination)}</td>

            <!-- 12. ملاحظات -->
            <td class="mgmt-notes-cell" title="${notesText}">${notesText}</td>
          </tr>
        `;
      }).join('');
    }

    renderPagination(data) {
      if (!this.paginationInfo) return;

      const start = data.totalCount === 0 ? 0 : (data.page - 1) * data.pageSize + 1;
      const end = Math.min(data.page * data.pageSize, data.totalCount);

      this.paginationInfo.textContent = `عرض ${start.toLocaleString('ar-EG')} إلى ${end.toLocaleString('ar-EG')} من إجمالي ${data.totalCount.toLocaleString('ar-EG')} سجل`;

      if (this.pageIndicator) {
        this.pageIndicator.textContent = `صفحة ${data.page.toLocaleString('ar-EG')} من ${data.totalPages.toLocaleString('ar-EG')}`;
      }

      if (this.prevBtn) {
        this.prevBtn.disabled = data.page <= 1;
      }
      if (this.nextBtn) {
        this.nextBtn.disabled = data.page >= data.totalPages;
      }
    }

    renderLoadingState() {
      if (!this.tableBody) return;
      this.tableBody.innerHTML = `
        <tr id="mgmt-table-loading-row">
          <td colspan="12" class="mgmt-loading-cell">
            <div class="mgmt-spinner-box">
              <span class="trans-btn-spinner"></span>
              <span>جاري تحميل وتحديث سجلات الإدارة...</span>
            </div>
          </td>
        </tr>
      `;
    }

    renderErrorState(msg) {
      if (!this.tableBody) return;
      this.tableBody.innerHTML = `
        <tr>
          <td colspan="12" class="mgmt-error-cell">
            <div class="mgmt-error-box">
              <svg width="36" height="36" viewBox="0 0 24 24" fill="none" stroke="#DC2626" stroke-width="2"><circle cx="12" cy="12" r="10"/><line x1="12" y1="8" x2="12" y2="12"/><line x1="12" y1="16" x2="12.01" y2="16"/></svg>
              <h3>تعذر تحميل البيانات</h3>
              <p>${this.escapeHtml(msg)}</p>
              <button type="button" class="mgmt-btn-secondary" onclick="window.ManagementApp.loadData()">إعادة المحاولة</button>
            </div>
          </td>
        </tr>
      `;
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
     * Updates user information badge when viewing Management department
     */
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
