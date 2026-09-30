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

      // Filter state
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
      this.bindEvents();
    }

    bindDOMElements() {
      // Containers
      this.root = document.getElementById('finance-management-page-root');
      this.tableBody = document.getElementById('finance-table-body');
      this.tableContainer = document.getElementById('finance-table-scroll');
      this.loadingEl = document.getElementById('finance-table-loading');
      this.emptyEl = document.getElementById('finance-table-empty');
      this.errorEl = document.getElementById('finance-table-error');

      // KPIs
      this.kpiTotalPoValue = document.getElementById('finance-kpi-total-po-value');
      this.kpiTotalSuppliedQty = document.getElementById('finance-kpi-total-supplied-qty');
      this.kpiTotalRemainingQty = document.getElementById('finance-kpi-total-remaining-qty');
      this.kpiTotalPaidAmount = document.getElementById('finance-kpi-total-paid-amount');
      this.kpiTotalOutstandingAmount = document.getElementById('finance-kpi-total-outstanding-amount');
      this.kpiTotalMovements = document.getElementById('finance-kpi-total-movements');

      // User Profile Header
      this.userNameEl = document.getElementById('finance-mgmt-user-name');
      this.userRoleEl = document.getElementById('finance-mgmt-user-role');
      this.userAvatarEl = document.getElementById('finance-mgmt-user-avatar');

      // Filter Inputs
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

      // PO filter with debounce
      let poTimeout = null;
      this.filterPoInput?.addEventListener('input', (e) => {
        clearTimeout(poTimeout);
        poTimeout = setTimeout(() => {
          this.filters.po = e.target.value.trim();
          this.currentPage = 1;
          this.loadData();
        }, 350);
      });

      // Invoice filter with debounce
      let invTimeout = null;
      this.filterInvoiceInput?.addEventListener('input', (e) => {
        clearTimeout(invTimeout);
        invTimeout = setTimeout(() => {
          this.filters.invoiceNumber = e.target.value.trim();
          this.currentPage = 1;
          this.loadData();
        }, 350);
      });

      // Supplier select
      this.filterSupplierSelect?.addEventListener('change', (e) => {
        this.filters.supplier = e.target.value;
        this.currentPage = 1;
        this.loadData();
      });

      // Payment Status select
      this.filterPaymentStatusSelect?.addEventListener('change', (e) => {
        this.filters.paymentStatus = e.target.value;
        this.currentPage = 1;
        this.loadData();
      });

      // Date range
      this.filterDateFrom?.addEventListener('change', (e) => {
        this.filters.dateFrom = e.target.value;
        this.currentPage = 1;
        this.loadData();
      });

      this.filterDateTo?.addEventListener('change', (e) => {
        this.filters.dateTo = e.target.value;
        this.currentPage = 1;
        this.loadData();
      });

      // Reset & Refresh
      this.resetFiltersBtn?.addEventListener('click', () => this.resetFilters());
      this.refreshBtn?.addEventListener('click', () => this.loadData());

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

          this.renderKpis(data.kpiSummary);
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

    renderKpis(kpi) {
      if (!kpi) return;
      if (this.kpiTotalPoValue) {
        this.kpiTotalPoValue.textContent = this.formatCurrency(kpi.totalPoValue);
      }
      if (this.kpiTotalSuppliedQty) {
        this.kpiTotalSuppliedQty.textContent = this.formatWeight(kpi.totalSuppliedQuantity);
      }
      if (this.kpiTotalRemainingQty) {
        this.kpiTotalRemainingQty.textContent = this.formatWeight(kpi.totalRemainingQuantity);
      }
      if (this.kpiTotalPaidAmount) {
        this.kpiTotalPaidAmount.textContent = this.formatCurrency(kpi.totalPaidAmount);
      }
      if (this.kpiTotalOutstandingAmount) {
        this.kpiTotalOutstandingAmount.textContent = this.formatCurrency(kpi.totalOutstandingAmount);
      }
      if (this.kpiTotalMovements) {
        this.kpiTotalMovements.textContent = Number(kpi.totalMovementsCount || 0).toLocaleString('ar-EG');
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

          <!-- 4. كمية أمر الشراء -->
          <td class="cell-qty font-numeric font-semibold">
            ${this.formatWeight(r.poQuantity)} طن
          </td>

          <!-- 5. قيمة أمر الشراء -->
          <td class="cell-val font-numeric font-semibold color-primary">
            ${this.formatCurrency(r.poValue)}
          </td>

          <!-- 6. سعر الطن -->
          <td class="cell-price font-numeric">
            ${this.formatCurrency(r.tonPrice)}
          </td>

          <!-- 7. الكمية التي تم توريدها -->
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

        // Attach click listener for payment details link
        const paidLink = tr.querySelector('.finance-paid-link');
        if (paidLink) {
          paidLink.addEventListener('click', (e) => {
            e.preventDefault();
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

      this.currentPage = 1;
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

  window.FinanceManagementApp = new FinanceManagementApp();
})();
