/**
 * FaceStone Storage Department Management - Arabic RTL Application Module
 * Redesigned Storage Dashboard:
 *  - 3 Main Cards:
 *      1. وارد (Import) -> إذن إضافة مواد إلى المخزن (/storage/import)
 *      2. صادر (Export) -> إذن صرف بضاعة من المخزن (/storage/export)
 *      3. تحويل (Transfer) -> تحويل مواد من المخزن إلى الإنتاج (/storage/transfer)
 *  - Operations History Ledger & Production Work-in-Process (WIP) Tracking
 *  - Real-time stock validations, atomic sequences, and Control & Monitoring approval integration.
 */

(function () {
  'use strict';

  class StorageManager {
    constructor() {
      this.initialized = false;
      this.locations = [];
      this.clients = [];
      this.materials = [];
      this.materialStockMap = {}; // key: `${locationId}_${materialId}`, value: stockNumber
      this.activeView = 'hub'; // 'hub', 'import', 'export', 'transfer', 'item-card'
      this.isSubmitting = false;

      // Selection states for dynamic tables
      this.importSelectedMaterials = new Map(); // materialId -> { name, quantity }
      this.importHasNewMaterial = false;
      this.importNewMaterial = { name: '', quantity: '' };
      this.importMaterialType = null; // 'MECHANICAL' | 'CHEMICAL'

      this.exportSelectedMaterials = new Map(); // materialId -> { name, quantity, availableStock }
      this.transferSelectedMaterials = new Map(); // materialId -> { name, quantity, availableStock, trackingId }

      // Cached sequences
      this.nextSequences = { import: 1, export: 1, transfer: 1 };

      // Laboratory production reference traceability integration
      this.importEligibleReferences = [];
      this.selectedImportRef = null;
      this.transferEligibleReferences = [];
      this.selectedTransferRef = null;

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

      // Top-level views
      this.viewHub = document.getElementById('storage-hub-cards-view');
      this.viewImport = document.getElementById('storage-import-view');
      this.viewExport = document.getElementById('storage-export-view');
      this.viewTransfer = document.getElementById('storage-transfer-view');
      this.viewItemCard = document.getElementById('storage-item-card-view');

      // The 3 Main Dashboard Cards
      this.cardImport = document.getElementById('card-nav-storage-import');
      this.cardExport = document.getElementById('card-nav-storage-export');
      this.cardTransfer = document.getElementById('card-nav-storage-transfer');

      // Hub Navigation Events
      this.cardImport?.addEventListener('click', () => this.showImportView());
      this.cardExport?.addEventListener('click', () => this.showExportView());
      this.cardTransfer?.addEventListener('click', () => this.showTransferView());

      // Back Buttons
      document.getElementById('btn-import-back-to-hub')?.addEventListener('click', () => this.showDashboardHub());
      document.getElementById('btn-export-back-to-hub')?.addEventListener('click', () => this.showDashboardHub());
      document.getElementById('btn-transfer-back-to-hub')?.addEventListener('click', () => this.showDashboardHub());
      document.getElementById('btn-storage-back-to-hub')?.addEventListener('click', () => this.showDashboardHub());

      // Hub Tabs & Refresh
      this.btnTabHubOps = document.getElementById('btn-tab-hub-operations');
      this.btnTabHubWip = document.getElementById('btn-tab-hub-wip');
      this.hubOpsSection = document.getElementById('hub-operations-section');
      this.hubWipSection = document.getElementById('hub-wip-section');

      this.btnTabHubOps?.addEventListener('click', () => this.switchHubTab('ops'));
      this.btnTabHubWip?.addEventListener('click', () => this.switchHubTab('wip'));
      document.getElementById('btn-hub-refresh-ops')?.addEventListener('click', () => this.loadHubData());

      // Hub Filters
      document.getElementById('filter-hub-op-type')?.addEventListener('change', () => this.loadOperations());
      document.getElementById('filter-hub-op-status')?.addEventListener('change', () => this.loadOperations());

      // Global Toast
      this.toastEl = document.getElementById('storage-global-toast');
      this.toastMsg = document.getElementById('storage-toast-message');
      this.toastIcon = document.getElementById('storage-toast-icon');

      // Modals
      this.wipOutputModal = document.getElementById('storage-record-wip-modal');
      this.opDetailsModal = document.getElementById('storage-op-details-modal');

      document.getElementById('btn-close-wip-modal')?.addEventListener('click', () => this.closeWipModal());
      document.getElementById('btn-cancel-wip-modal')?.addEventListener('click', () => this.closeWipModal());
      document.getElementById('btn-close-op-details-modal')?.addEventListener('click', () => this.closeOpDetailsModal());
      document.getElementById('btn-cancel-op-details-modal')?.addEventListener('click', () => this.closeOpDetailsModal());

      // WIP Output Form submission
      document.getElementById('storage-wip-output-form')?.addEventListener('submit', (e) => {
        e.preventDefault();
        this.handleWipOutputSubmit();
      });

      // WIP Output quantity input listener for mass balance calculation
      document.getElementById('wip-modal-product-qty')?.addEventListener('input', () => {
        this.calculateWipMassBalance();
      });

      // WIP Output waste classification toggle
      document.querySelectorAll('input[name="wip-waste-class"]').forEach(radio => {
        radio.addEventListener('change', (e) => {
          const wrap = document.getElementById('wip-recoverable-wrap');
          if (wrap) {
            wrap.style.display = (e.target.value === 'ORDINARY') ? 'none' : 'block';
          }
        });
      });

      // Bind Import Form
      this.bindImportEvents();

      // Bind Export Form
      this.bindExportEvents();

      // Bind Transfer Form
      this.bindTransferEvents();

      // Register SignalR real-time updates listener
      this.bindSignalREvents();

      this.initialized = true;
    }

    updateUserBadge(user) {
      if (!user) return;
      if (this.userNameEl) this.userNameEl.textContent = user.fullName || user.username;
      if (this.userRoleEl) this.userRoleEl.textContent = user.role || 'مسؤول المستودع';
      if (this.userAvatarEl) {
        const initials = (user.fullName || user.username || 'FS')
          .split(' ')
          .map(w => w[0])
          .join('')
          .substring(0, 2)
          .toUpperCase();
        this.userAvatarEl.textContent = initials || 'FS';
      }

      // Pre-fill read-only responsible employee inputs in forms
      const empName = user.fullName || user.username || 'الموظف المسؤول';
      const importEmpEl = document.getElementById('import-responsible-emp');
      const exportEmpEl = document.getElementById('export-responsible-emp');
      const transferEmpEl = document.getElementById('transfer-responsible-emp');
      if (importEmpEl) importEmpEl.textContent = empName;
      if (exportEmpEl) exportEmpEl.textContent = empName;
      if (transferEmpEl) transferEmpEl.textContent = empName;
    }

    bindSignalREvents() {
      if (window.realTimeChat?.connection) {
        window.realTimeChat.connection.on('OnDepartmentDataChanged', (data) => {
          if (data && data.formType === 'Storage') {
            this.loadHubData();
          }
        });
        window.realTimeChat.connection.on('OnFormApprovalStatusChanged', (data) => {
          if (data && data.formType === 'Storage') {
            this.loadHubData();
          }
        });
      }
    }

    // =========================================================================
    // View Routing
    // =========================================================================

    showDashboardHub() {
      this.activeView = 'hub';
      if (this.viewHub) this.viewHub.style.display = 'block';
      if (this.viewImport) this.viewImport.style.display = 'none';
      if (this.viewExport) this.viewExport.style.display = 'none';
      if (this.viewTransfer) this.viewTransfer.style.display = 'none';
      if (this.viewItemCard) this.viewItemCard.style.display = 'none';

      if (window.location.pathname !== '/storage') {
        history.pushState({ view: 'storage', subRoute: '/storage' }, '', '/storage');
      }

      this.loadHubData();
    }

    async showImportView() {
      this.activeView = 'import';
      if (this.viewHub) this.viewHub.style.display = 'none';
      if (this.viewImport) this.viewImport.style.display = 'block';
      if (this.viewExport) this.viewExport.style.display = 'none';
      if (this.viewTransfer) this.viewTransfer.style.display = 'none';
      if (this.viewItemCard) this.viewItemCard.style.display = 'none';

      if (window.location.pathname !== '/storage/import') {
        history.pushState({ view: 'storage', subRoute: '/storage/import' }, '', '/storage/import');
      }

      await this.prepareImportForm();
    }

    async showExportView() {
      this.activeView = 'export';
      if (this.viewHub) this.viewHub.style.display = 'none';
      if (this.viewImport) this.viewImport.style.display = 'none';
      if (this.viewExport) this.viewExport.style.display = 'block';
      if (this.viewTransfer) this.viewTransfer.style.display = 'none';
      if (this.viewItemCard) this.viewItemCard.style.display = 'none';

      if (window.location.pathname !== '/storage/export') {
        history.pushState({ view: 'storage', subRoute: '/storage/export' }, '', '/storage/export');
      }

      await this.prepareExportForm();
    }

    async showTransferView() {
      this.activeView = 'transfer';
      if (this.viewHub) this.viewHub.style.display = 'none';
      if (this.viewImport) this.viewImport.style.display = 'none';
      if (this.viewExport) this.viewExport.style.display = 'none';
      if (this.viewTransfer) this.viewTransfer.style.display = 'block';
      if (this.viewItemCard) this.viewItemCard.style.display = 'none';

      if (window.location.pathname !== '/storage/transfer') {
        history.pushState({ view: 'storage', subRoute: '/storage/transfer' }, '', '/storage/transfer');
      }

      await this.prepareTransferForm();
    }

    handleRoute(subRoute) {
      if (!subRoute) {
        this.showDashboardHub();
        return;
      }
      if (subRoute === '/storage/import' || subRoute.includes('import')) {
        this.showImportView();
      } else if (subRoute === '/storage/export' || subRoute.includes('export')) {
        this.showExportView();
      } else if (subRoute === '/storage/transfer' || subRoute.includes('transfer')) {
        this.showTransferView();
      } else {
        this.showDashboardHub();
      }
    }

    async loadInitialData() {
      this.bindElements();
      await this.loadHubData();
    }

    // =========================================================================
    // Data Loading Helpers
    // =========================================================================

    async loadMetadata() {
      // Fetch locations
      try {
        const resLoc = await ChatApi.getStorageLocations();
        if (resLoc.success && Array.isArray(resLoc.data)) {
          this.locations = resLoc.data;
        }
      } catch (e) {
        console.error('Failed loading storage locations:', e);
      }

      // Fetch clients
      try {
        const resCli = await ChatApi.getStorageClients();
        if (resCli.success && Array.isArray(resCli.data)) {
          this.clients = resCli.data;
        }
      } catch (e) {
        console.error('Failed loading storage clients:', e);
      }

      // Fetch upcoming sequence numbers
      try {
        const resSeq = await ChatApi.getStorageNextNumbers();
        if (resSeq.success && resSeq.data) {
          this.nextSequences = {
            import: resSeq.data.nextImport ?? resSeq.data.nextImportNumber ?? 1,
            export: resSeq.data.nextExport ?? resSeq.data.nextExportNumber ?? 1,
            transfer: resSeq.data.nextTransfer ?? resSeq.data.nextTransferNumber ?? 1
          };
        }
      } catch (e) {
        console.error('Failed loading storage sequences:', e);
      }

      // Fetch materials
      try {
        const resMat = await ChatApi.getStorageMaterialsStock();
        if (resMat.success && Array.isArray(resMat.data)) {
          this.materials = resMat.data;
        }
      } catch (e) {
        console.error('Failed loading materials stock:', e);
      }
    }

    formatLocalDateTimeForInput(date = new Date()) {
      const year = date.getFullYear();
      const month = String(date.getMonth() + 1).padStart(2, '0');
      const day = String(date.getDate()).padStart(2, '0');
      const hours = String(date.getHours()).padStart(2, '0');
      const minutes = String(date.getMinutes()).padStart(2, '0');
      return `${year}-${month}-${day}T${hours}:${minutes}`;
    }

    // =========================================================================
    // VIEW 1: HUB DATA (Operations Ledger & WIP Batches)
    // =========================================================================

    switchHubTab(tabKey) {
      if (tabKey === 'ops') {
        if (this.btnTabHubOps) {
          this.btnTabHubOps.style.background = '#FFFFFF';
          this.btnTabHubOps.style.border = '1px solid #CBD5E1';
        }
        if (this.btnTabHubWip) {
          this.btnTabHubWip.style.background = 'transparent';
          this.btnTabHubWip.style.border = 'none';
        }
        if (this.hubOpsSection) this.hubOpsSection.style.display = 'block';
        if (this.hubWipSection) this.hubWipSection.style.display = 'none';
      } else {
        if (this.btnTabHubWip) {
          this.btnTabHubWip.style.background = '#FFFFFF';
          this.btnTabHubWip.style.border = '1px solid #CBD5E1';
        }
        if (this.btnTabHubOps) {
          this.btnTabHubOps.style.background = 'transparent';
          this.btnTabHubOps.style.border = 'none';
        }
        if (this.hubOpsSection) this.hubOpsSection.style.display = 'none';
        if (this.hubWipSection) this.hubWipSection.style.display = 'block';
      }
    }

    async loadHubData() {
      await Promise.all([
        this.loadOperations(),
        this.loadProductionBatches()
      ]);
    }

    async loadOperations() {
      const typeFilter = document.getElementById('filter-hub-op-type')?.value || 'ALL';
      const statusFilter = document.getElementById('filter-hub-op-status')?.value || 'ALL';
      const tbody = document.getElementById('hub-operations-tbody');

      if (tbody) {
        tbody.innerHTML = `
          <tr>
            <td colspan="9" style="text-align: center; padding: 2rem; color: #94A3B8;">
              <span class="storage-btn-spinner" style="display:inline-block; border-color:#059669; border-top-color:transparent; margin-left:8px;"></span>
              جاري تحميل أذونات المستودع...
            </td>
          </tr>
        `;
      }

      try {
        const res = await ChatApi.getStorageOperations({ type: typeFilter, status: statusFilter, page: 1, pageSize: 50 });
        if (!res.success || !Array.isArray(res.data)) {
          if (tbody) tbody.innerHTML = `<tr><td colspan="9" style="text-align:center; padding:2rem; color:#EF4444;">فشل جلب بيانات العمليات.</td></tr>`;
          return;
        }

        const items = res.data;

        // Calculate KPI summaries
        let importTons = 0, exportTons = 0, transferTons = 0, pendingCount = 0;
        items.forEach(op => {
          if (op.approvalStatus === 'PENDING') pendingCount++;
          if (op.approvalStatus === 'APPROVED') {
            if (op.operationType === 'IMPORT') importTons += (op.totalQuantity || 0);
            if (op.operationType === 'EXPORT') exportTons += (op.totalQuantity || 0);
            if (op.operationType === 'TRANSFER') transferTons += (op.totalQuantity || 0);
          }
        });

        const elImp = document.getElementById('kpi-hub-import-total');
        const elExp = document.getElementById('kpi-hub-export-total');
        const elTrf = document.getElementById('kpi-hub-transfer-total');
        const elPnd = document.getElementById('kpi-hub-pending-count');

        if (elImp) elImp.innerHTML = `<span>${importTons.toFixed(3)}</span> <span class="storage-kpi-unit">طن</span>`;
        if (elExp) elExp.innerHTML = `<span>${exportTons.toFixed(3)}</span> <span class="storage-kpi-unit">طن</span>`;
        if (elTrf) elTrf.innerHTML = `<span>${transferTons.toFixed(3)}</span> <span class="storage-kpi-unit">طن</span>`;
        if (elPnd) elPnd.innerHTML = `<span>${pendingCount}</span> <span class="storage-kpi-unit">طلب</span>`;

        if (items.length === 0) {
          if (tbody) {
            tbody.innerHTML = `
              <tr>
                <td colspan="9" style="text-align: center; padding: 2.5rem; color: #94A3B8;">
                  لا توجد حركات مسجلة مطابقة للفلاتر المختارة
                </td>
              </tr>
            `;
          }
          return;
        }

        if (tbody) {
          tbody.innerHTML = items.map(op => {
            const typeBadge = op.operationType === 'IMPORT'
              ? `<span class="badge-op-tag badge-op-import">وارد</span>`
              : op.operationType === 'EXPORT'
              ? `<span class="badge-op-tag badge-op-export">صادر</span>`
              : `<span class="badge-op-tag badge-op-transfer">تحويل</span>`;

            const statusBadge = op.approvalStatus === 'APPROVED'
              ? `<span class="badge-status-tag badge-status-approved">معتمد</span>`
              : op.approvalStatus === 'REJECTED'
              ? `<span class="badge-status-tag badge-status-rejected">مرفوض</span>`
              : `<span class="badge-status-tag badge-status-pending">قيد المراجعة</span>`;

            const secondParty = op.operationType === 'IMPORT'
              ? `المورد: ${this.escapeHtml(op.source || '-')}`
              : op.operationType === 'EXPORT'
              ? `${this.escapeHtml(op.clientName || 'عميل')} - ${this.escapeHtml(op.destinationName || '')}`
              : `خط الإنتاج (داخلي)`;

            const itemsSummary = (op.items && op.items.length > 0)
              ? op.items.map(i => `${this.escapeHtml(i.materialName)} (${Number(i.quantity).toFixed(3)} طن)`).join('<br/>')
              : `${Number(op.totalQuantity || 0).toFixed(3)} طن`;

            return `
              <tr>
                <td><strong style="color:#0F172A;">${this.escapeHtml(op.transactionFormattedNumber)}</strong></td>
                <td>${typeBadge}</td>
                <td style="font-size:0.8rem; color:#475569;">${this.formatDateTime(op.transactionDate)}</td>
                <td>${this.escapeHtml(op.storageLocationName || '-')}</td>
                <td style="font-size:0.85rem;">${secondParty}</td>
                <td style="font-size:0.8rem; line-height:1.4;">${itemsSummary}</td>
                <td>${this.escapeHtml(op.responsibleEmployee || '-')}</td>
                <td>${statusBadge}</td>
                <td>
                  <button type="button" class="storage-btn-secondary btn-view-op-detail" data-op-id="${op.id}" style="padding: 4px 10px; font-size: 0.775rem;">
                    تفاصيل
                  </button>
                </td>
              </tr>
            `;
          }).join('');

          // Bind view detail buttons
          tbody.querySelectorAll('.btn-view-op-detail').forEach(btn => {
            btn.addEventListener('click', (e) => {
              const id = parseInt(e.currentTarget.getAttribute('data-op-id'), 10);
              if (id) this.openOpDetailsModal(id);
            });
          });
        }
      } catch (e) {
        console.error('Error in loadOperations:', e);
      }
    }

    async loadProductionBatches() {
      const tbody = document.getElementById('hub-wip-tbody');
      if (!tbody) return;

      try {
        const res = await ChatApi.getStorageProductionBatches();
        if (!res.success || !Array.isArray(res.data)) {
          tbody.innerHTML = `<tr><td colspan="10" style="text-align:center; padding:2rem; color:#EF4444;">فشل جلب دفعات الإنتاج.</td></tr>`;
          return;
        }

        const batches = res.data;
        if (batches.length === 0) {
          tbody.innerHTML = `
            <tr>
              <td colspan="10" style="text-align: center; padding: 2.5rem; color: #94A3B8;">
                لا توجد دفعات إنتاج محولة حتى الآن. عند اعتماد أذونات التحويل للإنتاج، ستظهر هنا تلقائياً لمتابعة المخرجات وحساب الهالك.
              </td>
            </tr>
          `;
          return;
        }

        tbody.innerHTML = batches.map(b => {
          const isCompleted = b.status === 'مكتمل';
          const statusBadge = isCompleted
            ? `<span class="badge-status-tag badge-status-approved">مكتمل</span>`
            : `<span class="badge-status-tag badge-status-pending">قيد التشغيل</span>`;

          const outputAction = isCompleted
            ? `<span style="font-size:0.775rem; color:#059669; font-weight:700;">تم التسجيل</span>`
            : `<button type="button" class="storage-btn-primary btn-record-output" data-batch-id="${b.id}" style="background:#2563EB; padding:4px 10px; font-size:0.75rem;">تسجيل المخرجات</button>`;

          const wasteDisplay = isCompleted && b.wasteQuantity !== null
            ? `<strong>${Number(b.wasteQuantity).toFixed(3)} طن</strong> (${Number(b.wastePercentage).toFixed(2)}%)`
            : `<span style="color:#94A3B8;">بانتظار بيانات الإنتاج</span>`;

          return `
            <tr>
              <td><strong style="color:#2563EB;">${this.escapeHtml(b.trackingNumber)}</strong></td>
              <td style="font-size:0.8rem;">${this.formatDate(b.transferDate)}</td>
              <td>${this.escapeHtml(b.materialName)}</td>
              <td><strong style="color:#059669;">${Number(b.materialQuantityIssued).toFixed(3)}</strong> طن</td>
              <td>${this.escapeHtml(b.sourceStorageLocation || '-')}</td>
              <td>${statusBadge}</td>
              <td>${this.escapeHtml(b.finalProductName || '—')}</td>
              <td>${b.finalProductQuantity ? `${Number(b.finalProductQuantity).toFixed(3)} طن` : '—'}</td>
              <td>${wasteDisplay}</td>
              <td>${outputAction}</td>
            </tr>
          `;
        }).join('');

        // Bind output recording buttons
        tbody.querySelectorAll('.btn-record-output').forEach(btn => {
          btn.addEventListener('click', (e) => {
            const batchId = parseInt(e.currentTarget.getAttribute('data-batch-id'), 10);
            const batch = batches.find(b => b.id === batchId);
            if (batch) this.openWipModal(batch);
          });
        });

      } catch (e) {
        console.error('Error in loadProductionBatches:', e);
      }
    }

    // =========================================================================
    // VIEW 2: IMPORT FORM LOGIC
    // =========================================================================

    async prepareImportForm() {
      await this.loadMetadata();

      // Reset selection maps
      this.importSelectedMaterials.clear();
      this.importHasNewMaterial = false;
      this.importNewMaterial = { name: '', quantity: '' };
      this.importMaterialType = null;

      // Reset material type radios
      document.querySelectorAll('input[name="import-material-type"]').forEach(r => r.checked = false);
      const typeHint = document.getElementById('import-type-hint');
      if (typeHint) typeHint.style.display = 'none';

      // Set Next Sequence preview
      const seqEl = document.getElementById('import-seq-preview');
      if (seqEl) seqEl.textContent = `#${this.nextSequences.import}`;

      // Set default datetime
      const dtInput = document.getElementById('import-datetime');
      if (dtInput) dtInput.value = this.formatLocalDateTimeForInput();

      // Populate locations
      const locSelect = document.getElementById('import-location-select');
      if (locSelect) {
        locSelect.innerHTML = `<option value="" disabled selected>-- اختر مكان التخزين --</option>` +
          this.locations.map(loc => `<option value="${loc.id}">${this.escapeHtml(loc.name)} (${this.escapeHtml(loc.code)})</option>`).join('');
      }

      // Checkbox new material reset
      const chkNew = document.getElementById('import-chk-new-material');
      const wrapNew = document.getElementById('import-new-material-wrap');
      const inputNew = document.getElementById('import-new-material-name');
      if (chkNew) chkNew.checked = false;
      if (wrapNew) wrapNew.style.display = 'none';
      if (inputNew) inputNew.value = '';

      // Reset weighing fields
      ['import-ext-weight', 'import-loaded-weight', 'import-empty-weight', 'import-weighbridge-ref', 'import-weighing-emp', 'import-discrepancy-explanation'].forEach(id => {
        const el = document.getElementById(id);
        if (el) el.value = '';
      });
      const wtEl = document.getElementById('import-weighing-time');
      if (wtEl) wtEl.value = this.formatLocalDateTimeForInput();
      const res = document.getElementById('import-weighing-result');
      if (res) res.style.display = 'none';
      const warn = document.getElementById('import-discrepancy-warning');
      if (warn) warn.style.display = 'none';

      // Populate materials checklist (no type filter initially)
      this.renderImportChecklist();
      this.renderImportQuantitiesTable();
      this.clearAllErrors();

      // Reset & load Laboratory Production References
      this.selectedImportRef = null;
      const importRefInfo = document.getElementById('import-prod-ref-info');
      if (importRefInfo) importRefInfo.style.display = 'none';
      await this.loadImportProductionReferences();
    }

    renderImportChecklist() {
      const container = document.getElementById('import-materials-checklist');
      if (!container) return;

      if (this.materials.length === 0) {
        container.innerHTML = `<span style="grid-column: 1/-1; color: #94A3B8; font-size: 0.85rem;">لا توجد أصناف خامات مسجلة. يمكنك تحديد "إضافة صنف جديد" بالأسفل.</span>`;
        return;
      }

      container.innerHTML = this.materials.map(mat => {
        const isChecked = this.importSelectedMaterials.has(mat.id);
        return `
          <label class="storage-check-label ${isChecked ? 'is-checked' : ''}" data-mat-id="${mat.id}">
            <input type="checkbox" value="${mat.id}" ${isChecked ? 'checked' : ''} />
            <span>${this.escapeHtml(mat.name)}</span>
          </label>
        `;
      }).join('');

      // Add event listeners to checklist checkboxes
      container.querySelectorAll('input[type="checkbox"]').forEach(chk => {
        chk.addEventListener('change', (e) => {
          const matId = parseInt(e.target.value, 10);
          const mat = this.materials.find(m => m.id === matId);
          if (!mat) return;

          if (e.target.checked) {
            this.importSelectedMaterials.set(matId, { name: mat.name, quantity: '' });
            e.target.closest('.storage-check-label')?.classList.add('is-checked');
          } else {
            this.importSelectedMaterials.delete(matId);
            e.target.closest('.storage-check-label')?.classList.remove('is-checked');
          }
          this.renderImportQuantitiesTable();
        });
      });
    }

    renderImportQuantitiesTable() {
      const tbody = document.getElementById('import-quantities-tbody');
      const itemsBadge = document.getElementById('import-total-items-badge');
      const qtyBadge = document.getElementById('import-total-qty-badge');
      if (!tbody) return;

      const rows = [];
      let totalQty = 0;

      // Existing materials
      this.importSelectedMaterials.forEach((val, matId) => {
        const currentVal = val.quantity;
        const numVal = parseFloat(currentVal);
        if (!isNaN(numVal) && numVal > 0) totalQty += numVal;

        rows.push(`
          <tr data-mat-id="${matId}">
            <td><strong>${this.escapeHtml(val.name)}</strong></td>
            <td>
              <input type="number" class="storage-input import-qty-input" data-mat-id="${matId}"
                     step="0.001" min="0.001" placeholder="0.000" value="${this.escapeHtml(currentVal)}" required />
            </td>
            <td style="text-align: center;">
              <button type="button" class="storage-btn-secondary btn-remove-import-line" data-mat-id="${matId}" style="padding: 4px 8px; color: #DC2626;">
                ✕ حذف
              </button>
            </td>
          </tr>
        `);
      });

      // New material line
      if (this.importHasNewMaterial) {
        const newName = this.importNewMaterial.name.trim() || '(صنف جديد)';
        const currentVal = this.importNewMaterial.quantity;
        const numVal = parseFloat(currentVal);
        if (!isNaN(numVal) && numVal > 0) totalQty += numVal;

        rows.push(`
          <tr data-new-line="true" style="background: #F0FDF4;">
            <td>
              <strong id="import-new-mat-row-name" style="color: #059669;">✨ ${this.escapeHtml(newName)}</strong>
              <span style="font-size: 0.75rem; color: #64748B; display: block;">صنف جديد سيتم إنشاؤه</span>
            </td>
            <td>
              <input type="number" class="storage-input" id="import-new-mat-qty-input"
                     step="0.001" min="0.001" placeholder="0.000" value="${this.escapeHtml(currentVal)}" required />
            </td>
            <td style="text-align: center;">
              <span style="font-size: 0.8rem; color: #059669; font-weight: 700;">جديد</span>
            </td>
          </tr>
        `);
      }

      const totalItems = this.importSelectedMaterials.size + (this.importHasNewMaterial ? 1 : 0);
      if (itemsBadge) itemsBadge.textContent = totalItems;
      if (qtyBadge) qtyBadge.textContent = `${totalQty.toFixed(3)} طن`;

      if (rows.length === 0) {
        tbody.innerHTML = `
          <tr>
            <td colspan="3" style="text-align: center; padding: 2rem; color: #94A3B8;">
              اختر صنفاً أو أكثر من القائمة أعلاه لإدخال كمياتها
            </td>
          </tr>
        `;
        return;
      }

      tbody.innerHTML = rows.join('');

      // Input change listeners
      tbody.querySelectorAll('.import-qty-input').forEach(input => {
        input.addEventListener('input', (e) => {
          const matId = parseInt(e.target.getAttribute('data-mat-id'), 10);
          if (this.importSelectedMaterials.has(matId)) {
            this.importSelectedMaterials.get(matId).quantity = e.target.value;
            this.updateImportTotalSummary();
          }
        });
      });

      const newQtyInput = document.getElementById('import-new-mat-qty-input');
      newQtyInput?.addEventListener('input', (e) => {
        this.importNewMaterial.quantity = e.target.value;
        this.updateImportTotalSummary();
      });

      // Remove buttons
      tbody.querySelectorAll('.btn-remove-import-line').forEach(btn => {
        btn.addEventListener('click', (e) => {
          const matId = parseInt(e.currentTarget.getAttribute('data-mat-id'), 10);
          this.importSelectedMaterials.delete(matId);
          // Uncheck corresponding checkbox
          const chk = document.querySelector(`#import-materials-checklist input[value="${matId}"]`);
          if (chk) {
            chk.checked = false;
            chk.closest('.storage-check-label')?.classList.remove('is-checked');
          }
          this.renderImportQuantitiesTable();
        });
      });
    }

    updateImportTotalSummary() {
      let totalQty = 0;
      this.importSelectedMaterials.forEach(val => {
        const num = parseFloat(val.quantity);
        if (!isNaN(num) && num > 0) totalQty += num;
      });
      if (this.importHasNewMaterial) {
        const num = parseFloat(this.importNewMaterial.quantity);
        if (!isNaN(num) && num > 0) totalQty += num;
      }
      const qtyBadge = document.getElementById('import-total-qty-badge');
      if (qtyBadge) qtyBadge.textContent = `${totalQty.toFixed(3)} طن`;
    }

    bindImportEvents() {
      // Material type radio buttons
      document.querySelectorAll('input[name="import-material-type"]').forEach(radio => {
        radio.addEventListener('change', async (e) => {
          this.importMaterialType = e.target.value;
          this.importSelectedMaterials.clear();

          // Update type hint text
          const typeHint = document.getElementById('import-type-hint');
          if (typeHint) {
            const typeName = this.importMaterialType === 'MECHANICAL' ? 'الميكانيكية' : 'الكيميائية';
            typeHint.textContent = `عرض أصناف الخامات ${typeName} فقط — تم تصفية القائمة`;
            typeHint.style.display = 'block';
          }

          // Highlight selected type label
          document.getElementById('import-type-mechanical-label')?.classList.toggle('is-checked', this.importMaterialType === 'MECHANICAL');
          document.getElementById('import-type-chemical-label')?.classList.toggle('is-checked', this.importMaterialType === 'CHEMICAL');

          // Fetch filtered materials from backend
          try {
            const res = await ChatApi.getRawMaterials(this.importMaterialType);
            if (res.success && Array.isArray(res.data)) {
              this.materials = res.data;
            }
          } catch (e) {
            console.warn('Could not filter materials by type:', e);
          }

          this.renderImportChecklist();
          this.renderImportQuantitiesTable();
          this.clearFieldError('import-material-type');
        });
      });

      // Weighing inputs live calculation
      ['import-loaded-weight', 'import-empty-weight', 'import-ext-weight'].forEach(id => {
        document.getElementById(id)?.addEventListener('input', () => this.updateImportWeighingDisplay());
      });

      // Toggle new material checkbox
      const chkNew = document.getElementById('import-chk-new-material');
      const wrapNew = document.getElementById('import-new-material-wrap');
      const inputNew = document.getElementById('import-new-material-name');

      chkNew?.addEventListener('change', (e) => {
        this.importHasNewMaterial = e.target.checked;
        if (wrapNew) wrapNew.style.display = e.target.checked ? 'block' : 'none';
        if (e.target.checked && inputNew) {
          inputNew.focus();
        }
        this.renderImportQuantitiesTable();
      });

      inputNew?.addEventListener('input', (e) => {
        this.importNewMaterial.name = e.target.value;
        const rowName = document.getElementById('import-new-mat-row-name');
        if (rowName) {
          rowName.textContent = `✨ ${e.target.value.trim() || '(صنف جديد)'}`;
        }
      });

      // Reset
      document.getElementById('btn-import-reset')?.addEventListener('click', () => {
        this.prepareImportForm();
      });

      // Laboratory Production Reference Listeners
      document.getElementById('import-btn-refresh-refs')?.addEventListener('click', async () => {
        await this.loadImportProductionReferences();
      });
      document.getElementById('import-prod-ref-select')?.addEventListener('change', (e) => {
        this.onImportProductionReferenceChanged(e.target.value);
      });

      // Submit
      document.getElementById('storage-import-form')?.addEventListener('submit', (e) => {
        e.preventDefault();
        this.handleImportSubmit();
      });
    }

    updateImportWeighingDisplay() {
      const loaded = parseFloat(document.getElementById('import-loaded-weight')?.value) || 0;
      const empty = parseFloat(document.getElementById('import-empty-weight')?.value) || 0;
      const ext = parseFloat(document.getElementById('import-ext-weight')?.value) || 0;

      const net = loaded - empty;
      const resultBox = document.getElementById('import-weighing-result');
      const warnBox = document.getElementById('import-discrepancy-warning');

      if (loaded > 0 && empty > 0) {
        const diff = Math.abs(net - ext);
        const pct = ext > 0 ? (diff / ext) * 100 : 0;

        const netEl = document.getElementById('import-net-weight-display');
        const diffEl = document.getElementById('import-weight-diff-display');
        const pctEl = document.getElementById('import-weight-pct-display');

        if (netEl) netEl.textContent = `${net.toFixed(3)} طن`;
        if (diffEl) diffEl.textContent = `${diff.toFixed(3)} طن`;
        if (pctEl) pctEl.textContent = `${pct.toFixed(2)} %`;

        if (resultBox) resultBox.style.display = 'block';

        const flagged = pct > 0.5 || diff > 0.05;
        if (warnBox) warnBox.style.display = flagged ? 'block' : 'none';
      } else {
        if (resultBox) resultBox.style.display = 'none';
        if (warnBox) warnBox.style.display = 'none';
      }
    }

    async handleImportSubmit() {
      if (this.isSubmitting) return;
      this.clearAllErrors();

      const locationId = parseInt(document.getElementById('import-location-select')?.value, 10);
      const supplier = document.getElementById('import-supplier-select')?.value?.trim();
      const phone = document.getElementById('import-phone')?.value?.trim();
      const dtVal = document.getElementById('import-datetime')?.value;
      const notes = document.getElementById('import-notes')?.value?.trim();

      let hasError = false;

      // Validate material type
      if (!this.importMaterialType) {
        this.setFieldError('import-material-type', 'نوع المادة الخام مطلوب — اختر ميكانيكية أو كيميائية.');
        hasError = true;
      }

      if (!dtVal) {
        this.setFieldError('import-datetime', 'التاريخ والوقت مطلوب.');
        hasError = true;
      }
      if (!locationId || isNaN(locationId)) {
        this.setFieldError('import-location-select', 'مكان التخزين مطلوب.');
        hasError = true;
      }
      if (!supplier) {
        this.setFieldError('import-supplier-select', 'المورد / المصدر مطلوب.');
        hasError = true;
      }

      // Check materials
      const totalItems = this.importSelectedMaterials.size + (this.importHasNewMaterial ? 1 : 0);
      if (totalItems === 0) {
        this.showToast('error', 'يجب اختيار صنف واحد على الأقل في إذن الإضافة.');
        return;
      }

      const items = [];

      // Validate existing materials
      for (const [matId, val] of this.importSelectedMaterials) {
        const qty = parseFloat(val.quantity);
        if (isNaN(qty) || qty <= 0) {
          this.showToast('error', `الكمية المدخلة للصنف '${val.name}' يجب أن تكون أكبر من الصفر.`);
          return;
        }
        items.push({
          rawMaterialId: matId,
          quantity: qty
        });
      }

      // Validate new material
      if (this.importHasNewMaterial) {
        const newName = this.importNewMaterial.name.trim();
        if (!newName) {
          this.setFieldError('import-new-material-name', 'اسم الصنف الجديد مطلوب.');
          hasError = true;
        } else {
          // Check duplicate name
          const exists = this.materials.some(m => m.name.toLowerCase() === newName.toLowerCase());
          if (exists) {
            this.setFieldError('import-new-material-name', 'يوجد صنف مسجل بالفعل بهذا الاسم.');
            hasError = true;
          }
        }

        const newQty = parseFloat(this.importNewMaterial.quantity);
        if (isNaN(newQty) || newQty <= 0) {
          this.showToast('error', 'الكمية المدخلة للصنف الجديد يجب أن تكون أكبر من الصفر.');
          return;
        }

        items.push({
          newMaterialName: newName,
          quantity: newQty
        });
      }

      // Validate discrepancy explanation if warning is shown
      const warnBox = document.getElementById('import-discrepancy-warning');
      if (warnBox && warnBox.style.display !== 'none') {
        const expl = document.getElementById('import-discrepancy-explanation')?.value?.trim();
        if (!expl) {
          this.setFieldError('import-discrepancy-explanation', 'تفسير الفرق في الوزن مطلوب عند وجود فرق يتجاوز 0.5%.');
          hasError = true;
        }
      }

      if (hasError) return;

      // Validate against Laboratory accepted quantity if linked
      if (this.selectedImportRef && this.selectedImportRef.acceptedQuantity) {
        const totalItemsQty = items.reduce((sum, it) => sum + (it.quantity || 0), 0);
        if (totalItemsQty > this.selectedImportRef.acceptedQuantity) {
          this.showToast('error', `إجمالي كميات الأصناف (${totalItemsQty.toFixed(3)} طن) يتجاوز الكمية المقبولة في فحص المعمل (${Number(this.selectedImportRef.acceptedQuantity).toFixed(3)} طن).`);
          return;
        }
      }

      // Build weighing payload
      const loadedW = parseFloat(document.getElementById('import-loaded-weight')?.value) || null;
      const emptyW = parseFloat(document.getElementById('import-empty-weight')?.value) || null;
      const extW = parseFloat(document.getElementById('import-ext-weight')?.value) || null;
      const weighingTime = document.getElementById('import-weighing-time')?.value || null;
      const weighbridgeRef = document.getElementById('import-weighbridge-ref')?.value?.trim() || null;
      const weighingEmp = document.getElementById('import-weighing-emp')?.value?.trim() || null;
      const discrepancyExpl = document.getElementById('import-discrepancy-explanation')?.value?.trim() || null;

      const payload = {
        transactionDate: new Date(dtVal).toISOString(),
        storageLocationId: locationId,
        productionReferenceNumber: this.selectedImportRef ? this.selectedImportRef.productionReferenceNumber : null,
        source: supplier,
        sourcePhone: phone || null,
        notes: notes || null,
        materialType: this.importMaterialType,
        externalReportedWeight: extW,
        internalLoadedWeight: loadedW,
        internalEmptyWeight: emptyW,
        internalWeighingTime: weighingTime ? new Date(weighingTime).toISOString() : null,
        weighbridgeReference: weighbridgeRef,
        weighingResponsibleEmployee: weighingEmp,
        discrepancyExplanation: discrepancyExpl,
        items: items
      };

      const btnSubmit = document.getElementById('btn-import-submit');
      const spinner = document.getElementById('import-spinner');
      this.isSubmitting = true;
      if (btnSubmit) btnSubmit.disabled = true;
      if (spinner) spinner.style.display = 'inline-block';

      try {
        const res = await ChatApi.submitStorageImport(payload);
        if (res.success && res.data) {
          const txNum = res.data.transactionFormattedNumber || `#${res.data.transactionNumber}`;
          this.showToast('success', `تم حفظ إذن الإضافة برقم ${txNum} بنجاح، بانتظار اعتماد قسم التحكم والمراقبة.`);
          await this.showDashboardHub();
        } else {
          this.showToast('error', res.message || 'فشل حفظ إذن الإضافة.');
        }
      } catch (err) {
        this.showToast('error', err.message || 'حدث خطأ في الاتصال بالسيرفر.');
      } finally {
        this.isSubmitting = false;
        if (btnSubmit) btnSubmit.disabled = false;
        if (spinner) spinner.style.display = 'none';
      }
    }

    async loadImportProductionReferences() {
      const selectEl = document.getElementById('import-prod-ref-select');
      if (!selectEl) return;
      selectEl.innerHTML = `<option value="">-- جاري تحميل الأرقام المرجعية المعتمدة من المعمل... --</option>`;
      try {
        const res = await ChatApi.getLaboratoryReferencesForStorageImport();
        if (res && res.success && Array.isArray(res.data)) {
          this.importEligibleReferences = res.data;
          if (res.data.length === 0) {
            selectEl.innerHTML = `<option value="">-- لا توجد خامات معملية أو منتجات معتمدة جاهزة للاستلام حالياً --</option>`;
          } else {
            selectEl.innerHTML = `<option value="">-- اختياري: اختر الرقم المرجعي المعتمد من المعمل للاستلام --</option>` +
              res.data.map(r => {
                const stageText = r.stage === 'FINAL_PRODUCT_APPROVED' ? '📦 منتج نهائي' : '🔬 مادة خام';
                const secText = r.laboratorySection === 'PHYSICAL' ? 'فيزيائي' : 'كيميائي';
                return `<option value="${r.productionReferenceNumber}">#${r.productionReferenceNumber} - [${stageText}] ${this.escapeHtml(r.rawMaterialDescription)} (مقبول: ${Number(r.acceptedQuantity).toFixed(3)} طن - ${secText})</option>`;
              }).join('');
          }
        } else {
          selectEl.innerHTML = `<option value="">-- تعذر تحميل الأرقام المرجعية --</option>`;
        }
      } catch (e) {
        console.warn('Error loading lab references for storage import:', e);
        selectEl.innerHTML = `<option value="">-- تعذر تحميل الأرقام المرجعية --</option>`;
      }
    }

    onImportProductionReferenceChanged(refNo) {
      const infoEl = document.getElementById('import-prod-ref-info');
      if (!refNo) {
        this.selectedImportRef = null;
        if (infoEl) infoEl.style.display = 'none';
        return;
      }
      const ref = this.importEligibleReferences.find(r => r.productionReferenceNumber === parseInt(refNo, 10));
      if (!ref) {
        this.selectedImportRef = null;
        if (infoEl) infoEl.style.display = 'none';
        return;
      }
      this.selectedImportRef = ref;
      if (infoEl) {
        const isFinal = ref.stage === 'FINAL_PRODUCT_APPROVED';
        const secLabel = ref.laboratorySection === 'PHYSICAL' ? 'المعمل الفيزيائي' : 'المعمل الكيميائي';
        infoEl.innerHTML = `
          <div style="display:flex; justify-content:space-between; align-items:center; flex-wrap:wrap; gap:8px;">
            <strong>🔗 الرقم المرجعي للإنتاج: #${ref.productionReferenceNumber}</strong>
            <span style="background:#059669; color:#fff; border-radius:4px; padding:2px 8px; font-size:0.75rem; font-weight:700;">
              ${isFinal ? 'منتج نهائي مطابق للمواصفات' : 'مادة خام مقبولة بالمعمل'} (${secLabel})
            </span>
          </div>
          <div style="margin-top:6px; display:grid; grid-template-columns: repeat(auto-fit, minmax(180px, 1fr)); gap:6px; font-size:0.8rem;">
            <div>المادة / الصنف: <strong>${this.escapeHtml(ref.rawMaterialDescription)}</strong></div>
            <div>المورد / المصدر: <strong>${this.escapeHtml(ref.supplierName || 'داخلي')}</strong></div>
            <div>الكمية المقبولة: <strong style="color:#059669; font-size:0.95rem;">${Number(ref.acceptedQuantity).toFixed(3)} طن</strong></div>
            ${ref.purchaseOrderNumber ? `<div>أمر الشراء / التشغيلة: <strong>${this.escapeHtml(ref.purchaseOrderNumber)}</strong></div>` : ''}
          </div>
        `;
        infoEl.style.display = 'block';
      }

      // Auto-fill fields if empty or applicable
      const supplierInput = document.getElementById('import-supplier-select');
      if (supplierInput && ref.supplierName && (!supplierInput.value || supplierInput.value === '')) {
        supplierInput.value = ref.supplierName;
      }
      const extWeightInput = document.getElementById('import-ext-weight');
      if (extWeightInput && ref.acceptedQuantity && (!extWeightInput.value || extWeightInput.value === '')) {
        extWeightInput.value = ref.acceptedQuantity;
      }

      // Automatically suggest material type
      if (ref.stage !== 'FINAL_PRODUCT_APPROVED') {
        const targetType = ref.laboratorySection === 'CHEMICAL' ? 'CHEMICAL' : 'MECHANICAL';
        const radio = document.querySelector(`input[name="import-material-type"][value="${targetType}"]`);
        if (radio && !radio.checked) {
          radio.checked = true;
          radio.dispatchEvent(new Event('change'));
        }
      }
    }

    // =========================================================================
    // VIEW 3: EXPORT FORM LOGIC
    // =========================================================================

    async prepareExportForm() {
      await this.loadMetadata();
      this.exportSelectedMaterials.clear();

      // Sequence
      const seqEl = document.getElementById('export-seq-preview');
      if (seqEl) seqEl.textContent = `#${this.nextSequences.export}`;

      // Datetime
      const dtInput = document.getElementById('export-datetime');
      if (dtInput) dtInput.value = this.formatLocalDateTimeForInput();

      // Locations
      const locSelect = document.getElementById('export-location-select');
      if (locSelect) {
        locSelect.innerHTML = `<option value="" disabled selected>-- اختر مكان التخزين --</option>` +
          this.locations.map(loc => `<option value="${loc.id}">${this.escapeHtml(loc.name)} (${this.escapeHtml(loc.code)})</option>`).join('');
      }

      // Clients
      const cliSelect = document.getElementById('export-client-select');
      if (cliSelect) {
        cliSelect.innerHTML = `<option value="" disabled selected>-- اختر العميل المعتمد --</option>` +
          this.clients.map(c => `<option value="${c.id}">${this.escapeHtml(c.name)}</option>`).join('');
      }

      // Destinations reset
      const destSelect = document.getElementById('export-destination-select');
      if (destSelect) {
        destSelect.innerHTML = `<option value="" disabled selected>-- اختر العميل أولاً --</option>`;
      }

      // Phone
      const phoneInput = document.getElementById('export-phone');
      if (phoneInput) phoneInput.value = '';

      this.renderExportChecklist();
      this.renderExportQuantitiesTable();
      this.clearAllErrors();
    }

    bindExportEvents() {
      // Client change listener
      const cliSelect = document.getElementById('export-client-select');
      const destSelect = document.getElementById('export-destination-select');
      const phoneInput = document.getElementById('export-phone');
      const destHint = document.getElementById('export-dest-hint');

      cliSelect?.addEventListener('change', (e) => {
        const cliId = parseInt(e.target.value, 10);
        const client = this.clients.find(c => c.id === cliId);

        if (!client) {
          if (destSelect) destSelect.innerHTML = `<option value="" disabled selected>-- اختر العميل أولاً --</option>`;
          return;
        }

        // Prefill phone if client has phone
        if (phoneInput && client.phoneNumber) {
          phoneInput.value = client.phoneNumber;
        }

        // Cascaded destinations
        if (destSelect) {
          if (client.destinations && client.destinations.length > 0) {
            destSelect.innerHTML = `<option value="" disabled selected>-- اختر الوجهة المعتمدة --</option>` +
              client.destinations.map(d => `<option value="${d.id}">${this.escapeHtml(d.name)}</option>`).join('');
            if (destHint) destHint.textContent = `${client.destinations.length} وجهة معتمدة لهذا العميل`;
          } else {
            destSelect.innerHTML = `<option value="" disabled selected>-- لا توجد وجهات مسجلة لهذا العميل --</option>`;
            if (destHint) destHint.textContent = 'تنبيه: يجب إضافة وجهة للعميل من سجل العملاء';
          }
        }
      });

      // Storage Location change -> reload materials stock for selected location
      const locSelect = document.getElementById('export-location-select');
      locSelect?.addEventListener('change', async (e) => {
        const locId = parseInt(e.target.value, 10);
        if (locId) {
          await this.reloadMaterialStockForLocation(locId);
          this.renderExportChecklist();
          this.renderExportQuantitiesTable();
        }
      });

      // Export weighing inputs live calculation
      ['export-loaded-weight', 'export-empty-weight', 'export-ext-weight'].forEach(id => {
        document.getElementById(id)?.addEventListener('input', () => this.updateExportWeighingDisplay());
      });

      // Reset
      document.getElementById('btn-export-reset')?.addEventListener('click', () => {
        this.prepareExportForm();
      });

      // Submit
      document.getElementById('storage-export-form')?.addEventListener('submit', (e) => {
        e.preventDefault();
        this.handleExportSubmit();
      });
    }

    updateExportWeighingDisplay() {
      const loaded = parseFloat(document.getElementById('export-loaded-weight')?.value) || 0;
      const empty = parseFloat(document.getElementById('export-empty-weight')?.value) || 0;
      const ext = parseFloat(document.getElementById('export-ext-weight')?.value) || 0;

      const net = loaded - empty;
      const resultBox = document.getElementById('export-weighing-result');
      const warnBox = document.getElementById('export-discrepancy-warning');

      if (loaded > 0 && empty > 0) {
        const diff = Math.abs(net - ext);
        const pct = ext > 0 ? (diff / ext) * 100 : 0;

        const netEl = document.getElementById('export-net-weight-display');
        const diffEl = document.getElementById('export-weight-diff-display');
        const pctEl = document.getElementById('export-weight-pct-display');

        if (netEl) netEl.textContent = `${net.toFixed(3)} طن`;
        if (diffEl) diffEl.textContent = `${diff.toFixed(3)} طن`;
        if (pctEl) pctEl.textContent = `${pct.toFixed(2)} %`;

        if (resultBox) resultBox.style.display = 'block';

        const flagged = pct > 0.5 || diff > 0.05;
        if (warnBox) warnBox.style.display = flagged ? 'block' : 'none';
      } else {
        if (resultBox) resultBox.style.display = 'none';
        if (warnBox) warnBox.style.display = 'none';
      }
    }

    async reloadMaterialStockForLocation(locationId) {
      try {
        const res = await ChatApi.getStorageMaterialsStock(locationId);
        if (res.success && Array.isArray(res.data)) {
          this.materials = res.data;
          // Update any already selected materials with new available stock
          this.exportSelectedMaterials.forEach((val, matId) => {
            const currentStockInfo = this.materials.find(m => m.id === matId);
            val.availableStock = currentStockInfo ? (currentStockInfo.availableQuantity ?? currentStockInfo.availableStock ?? 0) : 0;
          });
          this.transferSelectedMaterials.forEach((val, matId) => {
            const currentStockInfo = this.materials.find(m => m.id === matId);
            val.availableStock = currentStockInfo ? (currentStockInfo.availableQuantity ?? currentStockInfo.availableStock ?? 0) : 0;
          });
        }
      } catch (e) {
        console.error('Failed reloading stock for location:', e);
      }
    }

    renderExportChecklist() {
      const container = document.getElementById('export-materials-checklist');
      if (!container) return;

      if (this.materials.length === 0) {
        container.innerHTML = `<span style="grid-column:1/-1; color:#94A3B8; font-size:0.85rem;">اختر مكان التخزين أولاً لعرض الأصناف المتاحة.</span>`;
        return;
      }

      container.innerHTML = this.materials.map(mat => {
        const isChecked = this.exportSelectedMaterials.has(mat.id);
        const stockQty = Number(mat.availableQuantity ?? mat.availableStock ?? 0);
        const stockText = `${stockQty.toFixed(3)} طن`;
        const hasStock = stockQty > 0;
        return `
          <label class="storage-check-label ${isChecked ? 'is-checked' : ''}" data-mat-id="${mat.id}" style="${!hasStock ? 'opacity:0.75;' : ''}">
            <input type="checkbox" value="${mat.id}" ${isChecked ? 'checked' : ''} />
            <div style="display:flex; flex-direction:column;">
              <span>${this.escapeHtml(mat.name)}</span>
              <span style="font-size:0.75rem; color:${hasStock ? '#059669' : '#DC2626'}; font-weight:700;">
                المتاح: ${stockText}
              </span>
            </div>
          </label>
        `;
      }).join('');

      container.querySelectorAll('input[type="checkbox"]').forEach(chk => {
        chk.addEventListener('change', (e) => {
          const matId = parseInt(e.target.value, 10);
          const mat = this.materials.find(m => m.id === matId);
          if (!mat) return;

          const stockQty = Number(mat.availableQuantity ?? mat.availableStock ?? 0);
          if (e.target.checked) {
            this.exportSelectedMaterials.set(matId, {
              name: mat.name,
              availableStock: stockQty,
              quantity: ''
            });
            e.target.closest('.storage-check-label')?.classList.add('is-checked');
          } else {
            this.exportSelectedMaterials.delete(matId);
            e.target.closest('.storage-check-label')?.classList.remove('is-checked');
          }
          this.renderExportQuantitiesTable();
        });
      });
    }

    renderExportQuantitiesTable() {
      const tbody = document.getElementById('export-quantities-tbody');
      const itemsBadge = document.getElementById('export-total-items-badge');
      const qtyBadge = document.getElementById('export-total-qty-badge');
      if (!tbody) return;

      const rows = [];
      let totalQty = 0;

      this.exportSelectedMaterials.forEach((val, matId) => {
        const currentQty = parseFloat(val.quantity);
        if (!isNaN(currentQty) && currentQty > 0) totalQty += currentQty;

        const isOverStock = !isNaN(currentQty) && currentQty > val.availableStock;
        const stockBadge = val.availableStock > 0
          ? `<span class="stock-pill-badge stock-pill-available">${Number(val.availableStock).toFixed(3)} طن متاح</span>`
          : `<span class="stock-pill-badge stock-pill-empty">نفد المخزون</span>`;

        rows.push(`
          <tr data-mat-id="${matId}">
            <td><strong>${this.escapeHtml(val.name)}</strong></td>
            <td>${stockBadge}</td>
            <td>
              <input type="number" class="storage-input export-qty-input ${isOverStock ? 'is-invalid' : ''}" data-mat-id="${matId}"
                     step="0.001" min="0.001" placeholder="0.000" value="${this.escapeHtml(val.quantity)}" required />
              ${isOverStock ? `<span style="color:#DC2626; font-size:0.75rem; display:block; margin-top:2px;">الكمية المطلوبة تتجاوز الرصيد المتاح!</span>` : ''}
            </td>
            <td style="text-align: center;">
              <button type="button" class="storage-btn-secondary btn-remove-export-line" data-mat-id="${matId}" style="padding: 4px 8px; color: #DC2626;">
                ✕ حذف
              </button>
            </td>
          </tr>
        `);
      });

      if (itemsBadge) itemsBadge.textContent = this.exportSelectedMaterials.size;
      if (qtyBadge) qtyBadge.textContent = `${totalQty.toFixed(3)} طن`;

      if (rows.length === 0) {
        tbody.innerHTML = `
          <tr>
            <td colspan="4" style="text-align: center; padding: 2rem; color: #94A3B8;">
              اختر صنفاً أو أكثر من القائمة أعلاه لإدخال كميات الصرف
            </td>
          </tr>
        `;
        return;
      }

      tbody.innerHTML = rows.join('');

      tbody.querySelectorAll('.export-qty-input').forEach(input => {
        input.addEventListener('input', (e) => {
          const matId = parseInt(e.target.getAttribute('data-mat-id'), 10);
          if (this.exportSelectedMaterials.has(matId)) {
            this.exportSelectedMaterials.get(matId).quantity = e.target.value;
            this.renderExportQuantitiesTable();
          }
        });
      });

      tbody.querySelectorAll('.btn-remove-export-line').forEach(btn => {
        btn.addEventListener('click', (e) => {
          const matId = parseInt(e.currentTarget.getAttribute('data-mat-id'), 10);
          this.exportSelectedMaterials.delete(matId);
          const chk = document.querySelector(`#export-materials-checklist input[value="${matId}"]`);
          if (chk) {
            chk.checked = false;
            chk.closest('.storage-check-label')?.classList.remove('is-checked');
          }
          this.renderExportQuantitiesTable();
        });
      });
    }

    async handleExportSubmit() {
      if (this.isSubmitting) return;
      this.clearAllErrors();

      const locationId = parseInt(document.getElementById('export-location-select')?.value, 10);
      const clientId = parseInt(document.getElementById('export-client-select')?.value, 10);
      const destinationId = parseInt(document.getElementById('export-destination-select')?.value, 10);
      const phone = document.getElementById('export-phone')?.value?.trim();
      const dtVal = document.getElementById('export-datetime')?.value;
      const notes = document.getElementById('export-notes')?.value?.trim();

      let hasError = false;

      if (!dtVal) {
        this.setFieldError('export-datetime', 'التاريخ والوقت مطلوب.');
        hasError = true;
      }
      if (!locationId || isNaN(locationId)) {
        this.setFieldError('export-location-select', 'مكان التخزين مطلوب.');
        hasError = true;
      }
      if (!clientId || isNaN(clientId)) {
        this.setFieldError('export-client-select', 'العميل مطلوب.');
        hasError = true;
      }
      if (!destinationId || isNaN(destinationId)) {
        this.setFieldError('export-destination-select', 'الوجهة المعتمدة مطلوبة.');
        hasError = true;
      }

      if (this.exportSelectedMaterials.size === 0) {
        this.showToast('error', 'يجب اختيار صنف واحد على الأقل لصرفه.');
        return;
      }

      const items = [];
      for (const [matId, val] of this.exportSelectedMaterials) {
        const qty = parseFloat(val.quantity);
        if (isNaN(qty) || qty <= 0) {
          this.showToast('error', `الكمية المدخلة للصنف '${val.name}' يجب أن تكون أكبر من الصفر.`);
          return;
        }
        if (qty > val.availableStock) {
          this.showToast('error', `الكمية المطلوبة للصنف '${val.name}' (${qty.toFixed(3)} طن) تتجاوز الرصيد المتاح بالمخزن (${Number(val.availableStock).toFixed(3)} طن).`);
          return;
        }
        items.push({
          rawMaterialId: matId,
          quantity: qty
        });
      }

      // Validate discrepancy explanation if warning is shown
      const warnBox = document.getElementById('export-discrepancy-warning');
      if (warnBox && warnBox.style.display !== 'none') {
        const expl = document.getElementById('export-discrepancy-explanation')?.value?.trim();
        if (!expl) {
          this.setFieldError('export-discrepancy-explanation', 'تفسير الفرق في الوزن مطلوب عند وجود فرق يتجاوز 0.5%.');
          hasError = true;
        }
      }

      if (hasError) return;

      // Build weighing payload
      const loadedW = parseFloat(document.getElementById('export-loaded-weight')?.value) || null;
      const emptyW = parseFloat(document.getElementById('export-empty-weight')?.value) || null;
      const extW = parseFloat(document.getElementById('export-ext-weight')?.value) || null;
      const weighingTime = document.getElementById('export-weighing-time')?.value || null;
      const weighbridgeRef = document.getElementById('export-weighbridge-ref')?.value?.trim() || null;
      const weighingEmp = document.getElementById('export-weighing-emp')?.value?.trim() || null;
      const discrepancyExpl = document.getElementById('export-discrepancy-explanation')?.value?.trim() || null;

      const payload = {
        transactionDate: new Date(dtVal).toISOString(),
        storageLocationId: locationId,
        clientId: clientId,
        destinationId: destinationId,
        clientPhone: phone || null,
        notes: notes || null,
        externalReportedWeight: extW,
        internalLoadedWeight: loadedW,
        internalEmptyWeight: emptyW,
        internalWeighingTime: weighingTime ? new Date(weighingTime).toISOString() : null,
        weighbridgeReference: weighbridgeRef,
        weighingResponsibleEmployee: weighingEmp,
        discrepancyExplanation: discrepancyExpl,
        items: items
      };

      const btnSubmit = document.getElementById('btn-export-submit');
      const spinner = document.getElementById('export-spinner');
      this.isSubmitting = true;
      if (btnSubmit) btnSubmit.disabled = true;
      if (spinner) spinner.style.display = 'inline-block';

      try {
        const res = await ChatApi.submitStorageExport(payload);
        if (res.success && res.data) {
          const txNum = res.data.transactionFormattedNumber || `#${res.data.transactionNumber}`;
          this.showToast('success', `تم حفظ إذن الصرف برقم ${txNum} بنجاح، بانتظار اعتماد قسم التحكم والمراقبة.`);
          await this.showDashboardHub();
        } else {
          this.showToast('error', res.message || 'فشل حفظ إذن الصرف.');
        }
      } catch (err) {
        this.showToast('error', err.message || 'حدث خطأ في الاتصال بالسيرفر.');
      } finally {
        this.isSubmitting = false;
        if (btnSubmit) btnSubmit.disabled = false;
        if (spinner) spinner.style.display = 'none';
      }
    }

    // =========================================================================
    // VIEW 4: TRANSFER FORM LOGIC
    // =========================================================================

    async prepareTransferForm() {
      await this.loadMetadata();
      this.transferSelectedMaterials.clear();

      // Sequence
      const seqEl = document.getElementById('transfer-seq-preview');
      if (seqEl) seqEl.textContent = `#${this.nextSequences.transfer}`;

      // Datetime
      const dtInput = document.getElementById('transfer-datetime');
      if (dtInput) dtInput.value = this.formatLocalDateTimeForInput();

      // Locations
      const locSelect = document.getElementById('transfer-location-select');
      if (locSelect) {
        locSelect.innerHTML = `<option value="" disabled selected>-- اختر مكان التخزين المصدر --</option>` +
          this.locations.map(loc => `<option value="${loc.id}">${this.escapeHtml(loc.name)} (${this.escapeHtml(loc.code)})</option>`).join('');
      }

      this.renderTransferChecklist();
      this.renderTransferQuantitiesTable();
      this.clearAllErrors();

      // Reset & load Laboratory Production References
      this.selectedTransferRef = null;
      const transferRefInfo = document.getElementById('transfer-prod-ref-info');
      if (transferRefInfo) transferRefInfo.style.display = 'none';
      await this.loadTransferProductionReferences();
    }

    bindTransferEvents() {
      const locSelect = document.getElementById('transfer-location-select');
      locSelect?.addEventListener('change', async (e) => {
        const locId = parseInt(e.target.value, 10);
        if (locId) {
          await this.reloadMaterialStockForLocation(locId);
          this.renderTransferChecklist();
          this.renderTransferQuantitiesTable();
        }
      });

      document.getElementById('btn-transfer-reset')?.addEventListener('click', () => {
        this.prepareTransferForm();
      });

      // Laboratory Production Reference Listeners
      document.getElementById('transfer-btn-refresh-refs')?.addEventListener('click', async () => {
        await this.loadTransferProductionReferences();
      });
      document.getElementById('transfer-prod-ref-select')?.addEventListener('change', (e) => {
        this.onTransferProductionReferenceChanged(e.target.value);
      });

      document.getElementById('storage-transfer-form')?.addEventListener('submit', (e) => {
        e.preventDefault();
        this.handleTransferSubmit();
      });
    }

    renderTransferChecklist() {
      const container = document.getElementById('transfer-materials-checklist');
      if (!container) return;

      if (this.materials.length === 0) {
        container.innerHTML = `<span style="grid-column:1/-1; color:#94A3B8; font-size:0.85rem;">اختر مكان التخزين المصدر أولاً لعرض المواد المتاحة.</span>`;
        return;
      }

      container.innerHTML = this.materials.map(mat => {
        const isChecked = this.transferSelectedMaterials.has(mat.id);
        const stockQty = Number(mat.availableQuantity ?? mat.availableStock ?? 0);
        const stockText = `${stockQty.toFixed(3)} طن`;
        const hasStock = stockQty > 0;
        return `
          <label class="storage-check-label ${isChecked ? 'is-checked' : ''}" data-mat-id="${mat.id}" style="${!hasStock ? 'opacity:0.75;' : ''}">
            <input type="checkbox" value="${mat.id}" ${isChecked ? 'checked' : ''} />
            <div style="display:flex; flex-direction:column;">
              <span>${this.escapeHtml(mat.name)}</span>
              <span style="font-size:0.75rem; color:${hasStock ? '#2563EB' : '#DC2626'}; font-weight:700;">
                المتاح: ${stockText}
              </span>
            </div>
          </label>
        `;
      }).join('');

      container.querySelectorAll('input[type="checkbox"]').forEach(chk => {
        chk.addEventListener('change', (e) => {
          const matId = parseInt(e.target.value, 10);
          const mat = this.materials.find(m => m.id === matId);
          if (!mat) return;

          const stockQty = Number(mat.availableQuantity ?? mat.availableStock ?? 0);
          if (e.target.checked) {
            const currentYear = new Date().getFullYear();
            const txNumStr = String(this.nextSequences.transfer).padStart(4, '0');
            const lineIdx = this.transferSelectedMaterials.size + 1;
            const trackingPreview = `TRF-${currentYear}-${txNumStr}-L${lineIdx}`;

            this.transferSelectedMaterials.set(matId, {
              name: mat.name,
              availableStock: stockQty,
              quantity: '',
              trackingId: trackingPreview
            });
            e.target.closest('.storage-check-label')?.classList.add('is-checked');
          } else {
            this.transferSelectedMaterials.delete(matId);
            e.target.closest('.storage-check-label')?.classList.remove('is-checked');
          }
          this.renderTransferQuantitiesTable();
        });
      });
    }

    renderTransferQuantitiesTable() {
      const tbody = document.getElementById('transfer-quantities-tbody');
      const itemsBadge = document.getElementById('transfer-total-items-badge');
      const qtyBadge = document.getElementById('transfer-total-qty-badge');
      if (!tbody) return;

      const rows = [];
      let totalQty = 0;

      this.transferSelectedMaterials.forEach((val, matId) => {
        const currentQty = parseFloat(val.quantity);
        if (!isNaN(currentQty) && currentQty > 0) totalQty += currentQty;

        const isOverStock = !isNaN(currentQty) && currentQty > val.availableStock;
        const stockBadge = val.availableStock > 0
          ? `<span class="stock-pill-badge stock-pill-available">${Number(val.availableStock).toFixed(3)} طن متاح</span>`
          : `<span class="stock-pill-badge stock-pill-empty">نفد المخزون</span>`;

        rows.push(`
          <tr data-mat-id="${matId}">
            <td><strong>${this.escapeHtml(val.name)}</strong></td>
            <td>${stockBadge}</td>
            <td>
              <input type="number" class="storage-input transfer-qty-input ${isOverStock ? 'is-invalid' : ''}" data-mat-id="${matId}"
                     step="0.001" min="0.001" placeholder="0.000" value="${this.escapeHtml(val.quantity)}" required />
              ${isOverStock ? `<span style="color:#DC2626; font-size:0.75rem; display:block; margin-top:2px;">تتجاوز الرصيد المتاح!</span>` : ''}
            </td>
            <td>
              <span class="storage-seq-badge" style="color:#2563EB; border-color:#BFDBFE; background:#EFF6FF;">
                ${this.escapeHtml(val.trackingId)}
              </span>
            </td>
          </tr>
        `);
      });

      if (itemsBadge) itemsBadge.textContent = this.transferSelectedMaterials.size;
      if (qtyBadge) qtyBadge.textContent = `${totalQty.toFixed(3)} طن`;

      if (rows.length === 0) {
        tbody.innerHTML = `
          <tr>
            <td colspan="4" style="text-align: center; padding: 2rem; color: #94A3B8;">
              اختر مادة أو أكثر من القائمة أعلاه لإدخال كميات التحويل
            </td>
          </tr>
        `;
        return;
      }

      tbody.innerHTML = rows.join('');

      tbody.querySelectorAll('.transfer-qty-input').forEach(input => {
        input.addEventListener('input', (e) => {
          const matId = parseInt(e.target.getAttribute('data-mat-id'), 10);
          if (this.transferSelectedMaterials.has(matId)) {
            this.transferSelectedMaterials.get(matId).quantity = e.target.value;
            this.renderTransferQuantitiesTable();
          }
        });
      });
    }

    async handleTransferSubmit() {
      if (this.isSubmitting) return;
      this.clearAllErrors();

      const locationId = parseInt(document.getElementById('transfer-location-select')?.value, 10);
      const dtVal = document.getElementById('transfer-datetime')?.value;
      const notes = document.getElementById('transfer-notes')?.value?.trim();

      let hasError = false;

      if (!dtVal) {
        this.setFieldError('transfer-datetime', 'التاريخ والوقت مطلوب.');
        hasError = true;
      }
      if (!locationId || isNaN(locationId)) {
        this.setFieldError('transfer-location-select', 'مكان التخزين المصدر مطلوب.');
        hasError = true;
      }

      if (this.transferSelectedMaterials.size === 0) {
        this.showToast('error', 'يجب اختيار مادة واحدة على الأقل لتحويلها.');
        return;
      }

      const items = [];
      for (const [matId, val] of this.transferSelectedMaterials) {
        const qty = parseFloat(val.quantity);
        if (isNaN(qty) || qty <= 0) {
          this.showToast('error', `الكمية المدخلة للمادة '${val.name}' يجب أن تكون أكبر من الصفر.`);
          return;
        }
        if (qty > val.availableStock) {
          this.showToast('error', `الكمية المراد تحويلها للمادة '${val.name}' (${qty.toFixed(3)} طن) تتجاوز الرصيد المتاح بالمخزن (${Number(val.availableStock).toFixed(3)} طن).`);
          return;
        }
        items.push({
          rawMaterialId: matId,
          quantity: qty
        });
      }

      if (hasError) return;

      const payload = {
        transactionDate: new Date(dtVal).toISOString(),
        storageLocationId: locationId,
        productionReferenceNumber: this.selectedTransferRef ? this.selectedTransferRef.productionReferenceNumber : null,
        notes: notes || null,
        items: items
      };

      const btnSubmit = document.getElementById('btn-transfer-submit');
      const spinner = document.getElementById('transfer-spinner');
      this.isSubmitting = true;
      if (btnSubmit) btnSubmit.disabled = true;
      if (spinner) spinner.style.display = 'inline-block';

      try {
        const res = await ChatApi.submitStorageTransfer(payload);
        if (res.success && res.data) {
          const txNum = res.data.transactionFormattedNumber || `#${res.data.transactionNumber}`;
          this.showToast('success', `تم حفظ إذن التحويل للإنتاج برقم ${txNum} بنجاح، بانتظار اعتماد قسم التحكم والمراقبة.`);
          await this.showDashboardHub();
        } else {
          this.showToast('error', res.message || 'فشل حفظ إذن التحويل.');
        }
      } catch (err) {
        this.showToast('error', err.message || 'حدث خطأ في الاتصال بالسيرفر.');
      } finally {
        this.isSubmitting = false;
        if (btnSubmit) btnSubmit.disabled = false;
        if (spinner) spinner.style.display = 'none';
      }
    }

    async loadTransferProductionReferences() {
      const selectEl = document.getElementById('transfer-prod-ref-select');
      if (!selectEl) return;
      selectEl.innerHTML = `<option value="">-- جاري تحميل أرقام دورات الإنتاج... --</option>`;
      try {
        const res = await ChatApi.getLaboratoryReferencesForProduction();
        if (res && res.success && Array.isArray(res.data)) {
          this.transferEligibleReferences = res.data;
          if (res.data.length === 0) {
            selectEl.innerHTML = `<option value="">-- لا توجد دورات إنتاج معملية نشطة حالياً --</option>`;
          } else {
            selectEl.innerHTML = `<option value="">-- اختياري: اختر الرقم المرجعي لمتابعة تشغيلة الإنتاج --</option>` +
              res.data.map(r => {
                const secText = r.laboratorySection === 'PHYSICAL' ? 'فيزيائي' : 'كيميائي';
                return `<option value="${r.productionReferenceNumber}">#${r.productionReferenceNumber} - ${this.escapeHtml(r.rawMaterialDescription)} (مقبول: ${Number(r.acceptedQuantity).toFixed(3)} طن - ${secText})</option>`;
              }).join('');
          }
        } else {
          selectEl.innerHTML = `<option value="">-- تعذر تحميل الأرقام المرجعية --</option>`;
        }
      } catch (e) {
        console.warn('Error loading lab references for storage transfer:', e);
        selectEl.innerHTML = `<option value="">-- تعذر تحميل الأرقام المرجعية --</option>`;
      }
    }

    onTransferProductionReferenceChanged(refNo) {
      const infoEl = document.getElementById('transfer-prod-ref-info');
      if (!refNo) {
        this.selectedTransferRef = null;
        if (infoEl) infoEl.style.display = 'none';
        return;
      }
      const ref = this.transferEligibleReferences.find(r => r.productionReferenceNumber === parseInt(refNo, 10));
      if (!ref) {
        this.selectedTransferRef = null;
        if (infoEl) infoEl.style.display = 'none';
        return;
      }
      this.selectedTransferRef = ref;
      if (infoEl) {
        const secLabel = ref.laboratorySection === 'PHYSICAL' ? 'المعمل الفيزيائي' : 'المعمل الكيميائي';
        infoEl.innerHTML = `
          <div style="display:flex; justify-content:space-between; align-items:center;">
            <strong>🔗 دورة تشغيلية مرتبطة بالرقم المرجعي: #${ref.productionReferenceNumber}</strong>
            <span style="background:#2563EB; color:#fff; border-radius:4px; padding:2px 8px; font-size:0.75rem;">${secLabel}</span>
          </div>
          <div style="margin-top:4px; font-size:0.8rem;">
            المادة الخام الأساسية: <strong>${this.escapeHtml(ref.rawMaterialDescription)}</strong> | الكمية المعتمدة بالمعمل: <strong>${Number(ref.acceptedQuantity).toFixed(3)} طن</strong>
          </div>
        `;
        infoEl.style.display = 'block';
      }
    }

    // =========================================================================
    // MODALS: Production Output & Operation Details
    // =========================================================================

    openWipModal(batch) {
      if (!this.wipOutputModal) return;
      this.currentWipBatch = batch;

      document.getElementById('wip-modal-batch-id').value = batch.id;
      document.getElementById('wip-modal-tracking-num').textContent = batch.trackingNumber;
      document.getElementById('wip-modal-material-name').textContent = batch.materialName;
      document.getElementById('wip-modal-raw-qty').textContent = `${Number(batch.materialQuantityIssued).toFixed(3)} طن`;

      const prodName = document.getElementById('wip-modal-product-name');
      const prodQty = document.getElementById('wip-modal-product-qty');
      const notes = document.getElementById('wip-modal-notes');

      if (prodName) prodName.value = '';
      if (prodQty) prodQty.value = '';
      if (notes) notes.value = '';

      // Reset waste classification
      const ordinaryRadio = document.getElementById('wip-waste-ordinary');
      if (ordinaryRadio) ordinaryRadio.checked = true;
      const recoverableWrap = document.getElementById('wip-recoverable-wrap');
      if (recoverableWrap) recoverableWrap.style.display = 'none';
      const recoverableName = document.getElementById('wip-recoverable-name');
      if (recoverableName) recoverableName.value = '';
      const recoverableQty = document.getElementById('wip-recoverable-qty');
      if (recoverableQty) recoverableQty.value = '';

      // Populate destination location dropdown
      const destSelect = document.getElementById('wip-recoverable-dest');
      if (destSelect && this.locations && this.locations.length > 0) {
        destSelect.innerHTML = `<option value="" disabled selected>-- اختر المستودع --</option>` +
          this.locations.map(loc => `<option value="${loc.id}">${this.escapeHtml(loc.name)} (${this.escapeHtml(loc.code)})</option>`).join('');
      }

      this.calculateWipMassBalance();
      this.wipOutputModal.style.display = 'flex';
    }


    closeWipModal() {
      if (this.wipOutputModal) this.wipOutputModal.style.display = 'none';
      this.currentWipBatch = null;
    }

    calculateWipMassBalance() {
      if (!this.currentWipBatch) return;
      const rawQty = parseFloat(this.currentWipBatch.materialQuantityIssued) || 0;
      const outputVal = parseFloat(document.getElementById('wip-modal-product-qty')?.value);

      const elWasteQty = document.getElementById('wip-modal-waste-qty');
      const elWastePct = document.getElementById('wip-modal-waste-percent');

      if (isNaN(outputVal) || outputVal < 0) {
        if (elWasteQty) elWasteQty.textContent = '0.000 طن';
        if (elWastePct) elWastePct.textContent = '0.00 %';
        return;
      }

      const wasteQty = Math.max(0, rawQty - outputVal);
      const wastePct = rawQty > 0 ? (wasteQty / rawQty) * 100 : 0;

      if (elWasteQty) elWasteQty.textContent = `${wasteQty.toFixed(3)} طن`;
      if (elWastePct) elWastePct.textContent = `${wastePct.toFixed(2)} %`;
    }

    async handleWipOutputSubmit() {
      if (this.isSubmitting) return;

      const batchId = this.currentWipBatch?.id || parseInt(document.getElementById('wip-modal-batch-id')?.value, 10);
      const prodName = document.getElementById('wip-modal-product-name')?.value?.trim();
      const prodQty = parseFloat(document.getElementById('wip-modal-product-qty')?.value);
      const notes = document.getElementById('wip-modal-notes')?.value?.trim();
      const wasteClass = document.querySelector('input[name="wip-waste-class"]:checked')?.value || 'ORDINARY';

      if (!prodName) {
        this.showToast('error', 'اسم المنتج النهائي مطلوب.');
        return;
      }
      if (isNaN(prodQty) || prodQty <= 0) {
        this.setFieldError('wip-modal-product-qty', 'كمية المنتج النهائي يجب أن تكون أكبر من الصفر.');
        return;
      }
      if (this.currentWipBatch && prodQty > this.currentWipBatch.materialQuantityIssued) {
        this.setFieldError('wip-modal-product-qty', `كمية المنتج النهائي لا يمكن أن تتجاوز كمية المادة الخام المستلمة (${Number(this.currentWipBatch.materialQuantityIssued).toFixed(3)} طن).`);
        return;
      }

      let recoverableQty = null;
      let recoverableName = null;
      let recoverableDestId = null;

      if (wasteClass !== 'ORDINARY') {
        recoverableName = document.getElementById('wip-recoverable-name')?.value?.trim();
        recoverableQty = parseFloat(document.getElementById('wip-recoverable-qty')?.value);
        recoverableDestId = parseInt(document.getElementById('wip-recoverable-dest')?.value, 10) || null;

        if (!recoverableName) {
          this.setFieldError('wip-recoverable-name', 'اسم المادة المستردة مطلوب.');
          return;
        }
        if (isNaN(recoverableQty) || recoverableQty <= 0) {
          this.setFieldError('wip-recoverable-qty', 'كمية المادة المستردة يجب أن تكون أكبر من الصفر.');
          return;
        }
        if (!recoverableDestId) {
          this.setFieldError('wip-recoverable-dest', 'موقع التخزين المستهدف مطلوب.');
          return;
        }
      }

      const payload = {
        finalProductName: prodName,
        finalProductQuantity: prodQty,
        notes: notes || null,
        wasteClassification: wasteClass,
        recoverableProductName: recoverableName,
        recoverableQuantity: recoverableQty,
        recoverableUnit: 'طن',
        destinationStorageLocationId: recoverableDestId
      };

      const btnSubmit = document.getElementById('btn-wip-modal-submit');
      const spinner = document.getElementById('wip-modal-spinner');
      this.isSubmitting = true;
      if (btnSubmit) btnSubmit.disabled = true;
      if (spinner) spinner.style.display = 'inline-block';

      try {
        const res = await ChatApi.recordStorageProductionOutput(batchId, payload);
        if (res.success) {
          const wasteQty = res.data?.wasteQuantity ?? 0;
          const wastePct = res.data?.wastePercentage ?? 0;
          this.showToast('success', `تم تسجيل مخرجات الإنتاج. الهالك: ${wasteQty.toFixed(3)} طن (${wastePct.toFixed(2)}%).`);
          this.closeWipModal();
          await this.loadProductionBatches();
        } else {
          this.showToast('error', res.message || 'فشل تسجيل مخرجات الإنتاج.');
        }
      } catch (err) {
        this.showToast('error', err.message || 'حدث خطأ في الاتصال بالسيرفر.');
      } finally {
        this.isSubmitting = false;
        if (btnSubmit) btnSubmit.disabled = false;
        if (spinner) spinner.style.display = 'none';
      }
    }


    async openOpDetailsModal(operationId) {
      if (!this.opDetailsModal) return;
      const modalBody = document.getElementById('op-details-modal-body');
      const modalTitle = document.getElementById('op-details-modal-title');
      const modalSub = document.getElementById('op-details-modal-sub');

      if (modalBody) {
        modalBody.innerHTML = `<div style="text-align:center; padding:2rem; color:#94A3B8;">جاري تحميل تفاصيل العملية...</div>`;
      }
      this.opDetailsModal.style.display = 'flex';

      try {
        const res = await ChatApi.getStorageOperationDetails(operationId);
        if (!res.success || !res.data) {
          if (modalBody) modalBody.innerHTML = `<div style="color:#DC2626; padding:1.5rem;">فشل جلب تفاصيل العملية.</div>`;
          return;
        }

        const op = res.data;
        if (modalTitle) modalTitle.textContent = `تفاصيل العملية ${op.transactionFormattedNumber}`;
        if (modalSub) modalSub.textContent = `نوع الحركة: ${op.operationType} | تاريخ: ${this.formatDateTime(op.transactionDate)}`;

        const itemsHtml = op.items && op.items.length > 0
          ? `
            <table class="storage-qty-table" style="margin-top:10px;">
              <thead>
                <tr>
                  <th>اسم الصنف</th>
                  <th>الكمية بالطن</th>
                  <th>رقم التتبع</th>
                </tr>
              </thead>
              <tbody>
                ${op.items.map(i => `
                  <tr>
                    <td><strong>${this.escapeHtml(i.materialName)}</strong></td>
                    <td style="font-weight:700; color:#059669;">${Number(i.quantity).toFixed(3)} طن</td>
                    <td><span class="storage-seq-badge">${this.escapeHtml(i.trackingNumber || '—')}</span></td>
                  </tr>
                `).join('')}
              </tbody>
            </table>
          `
          : '<p style="color:#94A3B8;">لا توجد بنود مسجلة.</p>';

        if (modalBody) {
          modalBody.innerHTML = `
            <div style="background:#F8FAFC; border:1px solid #E2E8F0; border-radius:10px; padding:1rem; margin-bottom:1rem;">
              <div style="display:grid; grid-template-columns:repeat(2,1fr); gap:10px; font-size:0.85rem;">
                <div><span style="color:#64748B;">مكان التخزين:</span> <strong>${this.escapeHtml(op.storageLocationName || '-')}</strong></div>
                <div><span style="color:#64748B;">الموظف المسؤول:</span> <strong>${this.escapeHtml(op.responsibleEmployee || '-')}</strong></div>
                <div><span style="color:#64748B;">حالة الاعتماد:</span> <strong>${this.escapeHtml(op.approvalStatus)}</strong></div>
                <div><span style="color:#64748B;">المراجع:</span> <strong>${this.escapeHtml(op.reviewedBy || 'بانتظار المراجعة')}</strong></div>
                ${op.source ? `<div><span style="color:#64748B;">المورد / المصدر:</span> <strong>${this.escapeHtml(op.source)}</strong> (${this.escapeHtml(op.sourcePhone || 'بدون هاتف')})</div>` : ''}
                ${op.clientName ? `<div><span style="color:#64748B;">العميل:</span> <strong>${this.escapeHtml(op.clientName)}</strong> - وجهة: ${this.escapeHtml(op.destinationName || '')}</div>` : ''}
              </div>
              ${op.notes ? `<div style="margin-top:10px; font-size:0.8rem; color:#475569; border-top:1px dashed #CBD5E1; padding-top:6px;"><strong>ملاحظات:</strong> ${this.escapeHtml(op.notes)}</div>` : ''}
              ${op.rejectionReason ? `<div style="margin-top:10px; font-size:0.8rem; color:#DC2626; background:#FEE2E2; border:1px solid #FCA5A5; padding:6px 10px; border-radius:6px;"><strong>سبب الرفض:</strong> ${this.escapeHtml(op.rejectionReason)}</div>` : ''}
            </div>

            <h4 style="margin:1rem 0 0.5rem 0; font-size:0.95rem; font-weight:800; color:#0F172A;">بنود الأصناف المسجلة:</h4>
            ${itemsHtml}
          `;
        }
      } catch (err) {
        if (modalBody) modalBody.innerHTML = `<div style="color:#DC2626; padding:1.5rem;">خطأ: ${this.escapeHtml(err.message)}</div>`;
      }
    }

    closeOpDetailsModal() {
      if (this.opDetailsModal) this.opDetailsModal.style.display = 'none';
    }

    // =========================================================================
    // Utilities & Toast
    // =========================================================================

    setFieldError(fieldId, errorMsg) {
      const errEl = document.getElementById(`err-${fieldId}`);
      if (errEl) {
        errEl.textContent = errorMsg;
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
      const errs = this.pageRoot?.querySelectorAll('.storage-field-error');
      errs?.forEach(e => {
        e.textContent = '';
        e.classList.remove('visible');
      });
      const inputs = this.pageRoot?.querySelectorAll('.is-invalid');
      inputs?.forEach(i => i.classList.remove('is-invalid'));
    }

    showToast(type, message) {
      if (!this.toastEl) return;
      this.toastEl.className = `storage-alert-toast ${type}`;
      if (this.toastMsg) this.toastMsg.textContent = message;
      if (this.toastIcon) {
        this.toastIcon.textContent = (type === 'success') ? '✓' : (type === 'error' ? '✕' : 'ℹ');
      }
      this.toastEl.style.display = 'flex';
      setTimeout(() => {
        if (this.toastEl) this.toastEl.style.display = 'none';
      }, 5000);
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

    formatDate(dateStr) {
      if (!dateStr) return '-';
      try {
        const d = new Date(dateStr);
        return d.toLocaleDateString('ar-EG', { year: 'numeric', month: 'short', day: 'numeric' });
      } catch {
        return dateStr;
      }
    }

    formatDateTime(dateStr) {
      if (!dateStr) return '-';
      try {
        const d = new Date(dateStr);
        return d.toLocaleString('ar-EG', {
          year: 'numeric',
          month: 'numeric',
          day: 'numeric',
          hour: '2-digit',
          minute: '2-digit'
        });
      } catch {
        return dateStr;
      }
    }
  }

  // Export Singleton
  window.StorageApp = new StorageManager();

})();
