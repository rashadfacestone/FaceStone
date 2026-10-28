/* ==========================================================================
   FACESTONE PRODUCTION DEPARTMENT CONTROLLER (قسم الإنتاج)
   Redesigned Daily Production Tracking System
   - Strict server-enforced shift and employee mapping
   - Dynamic multi-product quantity inputs
   - Real-time arithmetic: Duration, Gross, Net, Product & Overall Hourly Rates, Waste %
   - Real database summaries and management answers without double-counting
   ========================================================================== */

(function () {
  'use strict';

  class ProductionApp {
    static init() {
      this.root = document.getElementById('production-page-root');
      if (!this.root) return;

      this.currentSection = 'form';
      this.shiftInfo = null;
      this.catalog = [];
      this.selectedProducts = new Map(); // productCode -> { id, code, name, category, quantity, unit }
      this.editSelectedProducts = new Map();

      this.bindUiEvents();
      this.loadShiftInfo();
      this.loadCatalog();
      this.loadStats();
      this.loadSubmissions();
      this.setDefaultDateAndTimes();
    }

    static updateUserBadge(user) {
      if (!user) return;
      const nameEl = document.getElementById('prd-user-name');
      const roleEl = document.getElementById('prd-user-role');
      const avatarEl = document.getElementById('prd-user-avatar');

      if (nameEl) nameEl.textContent = user.fullName || user.username || 'المستخدم';
      if (roleEl) {
        roleEl.textContent = (window.ChatApp && window.ChatApp.formatRoleArabic)
          ? window.ChatApp.formatRoleArabic(user.role)
          : user.role;
      }
      if (avatarEl) {
        const initials = (user.fullName || 'FS').split(' ').map(n => n[0]).join('').substring(0, 2).toUpperCase();
        avatarEl.textContent = initials || 'FS';
      }
    }

    static setDefaultDateAndTimes() {
      const now = new Date();
      const todayStr = this.formatDate(now);

      const dateInput = document.getElementById('prd-input-date');
      if (dateInput && !dateInput.value) {
        dateInput.value = todayStr;
      }

      // Default start and end times (e.g., 08:00 to 16:00 today)
      const startInput = document.getElementById('prd-input-start-time');
      const endInput = document.getElementById('prd-input-end-time');

      if (startInput && !startInput.value) {
        startInput.value = `${todayStr}T08:00`;
      }
      if (endInput && !endInput.value) {
        endInput.value = `${todayStr}T16:00`;
      }

      this.updateLiveCalculations();
    }

    static bindUiEvents() {
      // Tab switcher buttons
      const tabBtns = this.root.querySelectorAll('.prd-tab-btn');
      tabBtns.forEach(btn => {
        btn.addEventListener('click', (e) => {
          e.preventDefault();
          const target = btn.dataset.section;
          if (target) this.showSection(target);
        });
      });

      // Refresh button
      const refreshBtn = document.getElementById('prd-btn-refresh');
      refreshBtn?.addEventListener('click', () => {
        this.loadShiftInfo();
        this.loadStats();
        this.loadSubmissions();
      });

      // Add product button and select
      const addProdBtn = document.getElementById('prd-btn-add-product');
      addProdBtn?.addEventListener('click', () => this.handleAddProductClick());

      const catalogSelect = document.getElementById('prd-catalog-select');
      catalogSelect?.addEventListener('change', () => {
        if (catalogSelect.value) {
          this.addProduct(catalogSelect.value);
          catalogSelect.value = '';
        }
      });

      // Shift times change
      const startInput = document.getElementById('prd-input-start-time');
      const endInput = document.getElementById('prd-input-end-time');
      startInput?.addEventListener('input', () => this.updateLiveCalculations());
      endInput?.addEventListener('input', () => this.updateLiveCalculations());

      // Waste change
      const wasteInput = document.getElementById('prd-input-waste');
      wasteInput?.addEventListener('input', () => this.updateLiveCalculations());

      // Daily Form Submit
      const dailyForm = document.getElementById('prd-daily-form');
      dailyForm?.addEventListener('submit', (e) => {
        e.preventDefault();
        this.handleDailySubmission();
      });

      // Reset form
      const resetBtn = document.getElementById('prd-btn-reset-form');
      resetBtn?.addEventListener('click', () => this.resetDailyForm());

      // Summary Filter buttons
      const applyFiltersBtn = document.getElementById('prd-btn-apply-filters');
      applyFiltersBtn?.addEventListener('click', () => this.applySummaryFilters());

      const resetFiltersBtn = document.getElementById('prd-btn-reset-filters');
      resetFiltersBtn?.addEventListener('click', () => this.resetSummaryFilters());

      const exportSummaryBtn = document.getElementById('prd-btn-export-summary');
      exportSummaryBtn?.addEventListener('click', () => {
        this.loadStats();
        this.loadSubmissions();
      });

      // Edit Modal Events
      const editCatalogSelect = document.getElementById('prd-edit-catalog-select');
      const editAddProdBtn = document.getElementById('prd-edit-btn-add-product');
      editAddProdBtn?.addEventListener('click', () => {
        if (editCatalogSelect && editCatalogSelect.value) {
          this.addEditProduct(editCatalogSelect.value);
          editCatalogSelect.value = '';
        }
      });

      const editStartTime = document.getElementById('prd-edit-start-time');
      const editEndTime = document.getElementById('prd-edit-end-time');
      const editWaste = document.getElementById('prd-edit-waste');
      editStartTime?.addEventListener('input', () => this.updateEditLiveCalculations());
      editEndTime?.addEventListener('input', () => this.updateEditLiveCalculations());
      editWaste?.addEventListener('input', () => this.updateEditLiveCalculations());

      const editForm = document.getElementById('prd-edit-submission-form');
      editForm?.addEventListener('submit', (e) => {
        e.preventDefault();
        this.handleEditSubmissionSubmit();
      });
    }

    static showSection(section) {
      if (section === 'inventory' || section === 'orders' || section === 'batches') {
        section = 'form';
      }
      this.currentSection = section;

      const tabBtns = this.root.querySelectorAll('.prd-tab-btn');
      tabBtns.forEach(btn => {
        btn.classList.toggle('active', btn.dataset.section === section);
      });

      const secForm = document.getElementById('prd-sec-form');
      const secSummary = document.getElementById('prd-sec-summary');

      if (secForm) secForm.style.display = (section === 'form') ? 'block' : 'none';
      if (secSummary) {
        secSummary.style.display = (section === 'summary') ? 'block' : 'none';
        if (section === 'summary') {
          this.loadSubmissions();
        }
      }
    }

    static handleRoute(subRoute) {
      if (subRoute === '/production/summary' || subRoute === 'summary') {
        this.showSection('summary');
      } else {
        this.showSection('form');
      }
    }

    /* ----------------------------------------------------------------------
       Shift & Current Employee Info
       ---------------------------------------------------------------------- */
    static async loadShiftInfo() {
      try {
        const res = await window.ChatApi.getCurrentShiftInfo();
        if (res && res.success && res.data) {
          this.shiftInfo = res.data;
          this.renderShiftInfo(this.shiftInfo);
        } else {
          this.renderShiftError(res?.message || 'تعذر تحميل بيانات الوردية من الخادم');
        }
      } catch (err) {
        console.error('Error loading shift info:', err);
        this.renderShiftError('خطأ أثناء الاتصال بالخادم للتحقق من الوردية');
      }
    }

    static renderShiftInfo(info) {
      const shiftTextEl = document.getElementById('prd-current-shift-text');
      const formEmpNameEl = document.getElementById('prd-form-employee-name');
      const formShiftTagEl = document.getElementById('prd-form-shift-tag');
      const warningBanner = document.getElementById('prd-shift-warning-banner');
      const warningText = document.getElementById('prd-shift-warning-text');
      const submitBtn = document.getElementById('prd-btn-submit-daily');

      const shiftName = info.shift || info.shiftName || '';
      const empEmail = info.employeeEmail || info.email || '';

      if (info.canSubmit) {
        if (warningBanner) warningBanner.style.display = 'none';
        if (submitBtn) submitBtn.disabled = false;

        const shiftBadgeText = `${info.employeeName} — ${shiftName}`;
        if (shiftTextEl) shiftTextEl.textContent = shiftBadgeText;
        if (formEmpNameEl) formEmpNameEl.textContent = `${info.employeeName} (${empEmail})`;
        if (formShiftTagEl) {
          formShiftTagEl.textContent = shiftName;
          formShiftTagEl.style.background = shiftName.includes('الأولى') ? '#dbeafe' : '#fef3c7';
          formShiftTagEl.style.color = shiftName.includes('الأولى') ? '#1e40af' : '#92400e';
        }

        const now = new Date();
        const todayStr = this.formatDate(now);
        const startInput = document.getElementById('prd-input-start-time');
        const endInput = document.getElementById('prd-input-end-time');

        if (shiftName.includes('الثانية') || shiftName.includes('المسائية')) {
          if (startInput && (!startInput.value || startInput.value.endsWith('08:00'))) {
            startInput.value = `${todayStr}T16:00`;
          }
          if (endInput && (!endInput.value || endInput.value.endsWith('16:00'))) {
            const tomorrow = new Date(now.getTime() + 24 * 60 * 60 * 1000);
            const tomorrowStr = this.formatDate(tomorrow);
            endInput.value = `${tomorrowStr}T00:00`;
          }
        } else {
          if (startInput && (!startInput.value || startInput.value.endsWith('16:00'))) {
            startInput.value = `${todayStr}T08:00`;
          }
          if (endInput && (!endInput.value || endInput.value.endsWith('00:00'))) {
            endInput.value = `${todayStr}T16:00`;
          }
        }
        this.updateLiveCalculations();
      } else {
        if (warningBanner) warningBanner.style.display = 'block';
        if (warningText) warningText.textContent = info.errorMessage || info.message || 'حسابك غير مقترن بوردية إنتاج معتمدة';
        if (shiftTextEl) shiftTextEl.textContent = 'غير مصرح بتسجيل الإنتاج';
        if (formEmpNameEl) formEmpNameEl.textContent = info.employeeName || 'غير مسجل';
        if (formShiftTagEl) {
          formShiftTagEl.textContent = 'غير محدد';
          formShiftTagEl.style.background = '#fee2e2';
          formShiftTagEl.style.color = '#991b1b';
        }
        if (submitBtn) {
          submitBtn.disabled = true;
          submitBtn.title = info.errorMessage || 'غير مصرح';
        }
      }
    }

    static renderShiftError(msg) {
      const warningBanner = document.getElementById('prd-shift-warning-banner');
      const warningText = document.getElementById('prd-shift-warning-text');
      const submitBtn = document.getElementById('prd-btn-submit-daily');

      if (warningBanner) warningBanner.style.display = 'block';
      if (warningText) warningText.textContent = msg;
      if (submitBtn) submitBtn.disabled = true;
    }

    /* ----------------------------------------------------------------------
       Product Catalog
       ---------------------------------------------------------------------- */
    static async loadCatalog() {
      try {
        const res = await window.ChatApi.getProductionCatalog();
        if (res && res.success && Array.isArray(res.data)) {
          this.catalog = res.data;
          this.populateCatalogDropdowns(this.catalog);
        }
      } catch (err) {
        console.error('Error loading product catalog:', err);
      }
    }

    static populateCatalogDropdowns(products) {
      const mainSelect = document.getElementById('prd-catalog-select');
      const editSelect = document.getElementById('prd-edit-catalog-select');
      const filterSelect = document.getElementById('prd-filter-product');

      if (mainSelect) {
        mainSelect.innerHTML = '<option value="">-- اختر منتجاً لإضافته للقائمة --</option>';
        products.forEach(p => {
          const opt = document.createElement('option');
          opt.value = p.code || p.productCode;
          opt.textContent = `${p.code || p.productCode} — ${p.name || p.productNameAr} (${p.category || 'عام'})`;
          mainSelect.appendChild(opt);
        });
      }

      if (editSelect) {
        editSelect.innerHTML = '<option value="">-- إضافة منتج --</option>';
        products.forEach(p => {
          const opt = document.createElement('option');
          opt.value = p.code || p.productCode;
          opt.textContent = `${p.code || p.productCode} — ${p.name || p.productNameAr}`;
          editSelect.appendChild(opt);
        });
      }

      if (filterSelect) {
        filterSelect.innerHTML = '<option value="">جميع المنتجات</option>';
        products.forEach(p => {
          const opt = document.createElement('option');
          opt.value = p.name || p.code;
          opt.textContent = `${p.code || p.productCode} — ${p.name || p.productNameAr}`;
          filterSelect.appendChild(opt);
        });
      }
    }

    /* ----------------------------------------------------------------------
       Dynamic Products Selection (Daily Form)
       ---------------------------------------------------------------------- */
    static handleAddProductClick() {
      const select = document.getElementById('prd-catalog-select');
      if (select && select.value) {
        this.addProduct(select.value);
        select.value = '';
      }
    }

    static addProduct(productCode, initialQty = null) {
      if (!productCode) return;

      const prod = this.catalog.find(p => (p.code === productCode || p.productCode === productCode)) || {
        id: 0,
        code: productCode,
        name: productCode,
        category: 'عام',
        unit: 'طن'
      };

      const code = prod.code || prod.productCode || productCode;

      // Prevent duplicate product entry within the same submission
      if (this.selectedProducts.has(code)) {
        const existingInput = document.getElementById(`prd-qty-${code}`);
        if (existingInput) {
          existingInput.focus();
          existingInput.style.borderColor = '#f59e0b';
          setTimeout(() => { existingInput.style.borderColor = '#0284c7'; }, 1000);
        }
        return;
      }

      this.selectedProducts.set(code, {
        id: prod.id,
        code: code,
        name: prod.name || prod.productNameAr || code,
        category: prod.category || 'عام',
        unit: prod.unit || 'طن',
        quantity: initialQty
      });

      this.renderSelectedProducts();
      this.updateLiveCalculations();
    }

    static removeProduct(productCode) {
      this.selectedProducts.delete(productCode);
      this.renderSelectedProducts();
      this.updateLiveCalculations();
    }

    static renderSelectedProducts() {
      const container = document.getElementById('prd-selected-products-container');
      if (!container) return;

      if (this.selectedProducts.size === 0) {
        container.innerHTML = `
          <div id="prd-no-products-placeholder" style="text-align: center; padding: 2rem 1rem; background: #ffffff; border: 2px dashed #cbd5e1; border-radius: 8px; color: var(--prd-text-muted);">
            <div style="font-size: 1.8rem; margin-bottom: 0.35rem;">📦</div>
            <div style="font-weight: 700; font-size: 0.95rem; color: var(--prd-navy-dark);">لم يتم اختيار أي منتج بعد</div>
            <div style="font-size: 0.82rem; margin-top: 0.25rem;">يرجى اختيار المنتجات المنتجة من القائمة المنسدلة أعلاه لإدخال كمياتها</div>
          </div>
        `;
        return;
      }

      container.innerHTML = '';
      this.selectedProducts.forEach((item, code) => {
        const card = document.createElement('div');
        card.className = 'prd-selected-product-card';
        card.id = `prd-row-${code}`;

        const qtyVal = (item.quantity !== null && item.quantity !== undefined && item.quantity !== '')
          ? item.quantity
          : '';

        card.innerHTML = `
          <div style="display: flex; align-items: center; gap: 0.75rem; flex: 1;">
            <div style="width: 36px; height: 36px; border-radius: 8px; background: #e0f2fe; color: #0284c7; display: flex; align-items: center; justify-content: center; font-weight: 800; font-size: 0.85rem;">
              FS
            </div>
            <div>
              <div style="font-weight: 800; color: var(--prd-navy-dark); font-size: 0.92rem;">
                ${this.escape(item.name)}
              </div>
              <div style="display: flex; align-items: center; gap: 0.5rem; margin-top: 0.15rem;">
                <span style="font-size: 0.75rem; color: #64748b; font-family: monospace; font-weight: 700;">${this.escape(item.code)}</span>
                <span class="prd-badge" style="background: #f1f5f9; color: #475569; font-size: 0.7rem; padding: 0.1rem 0.4rem; border-radius: 4px;">${this.escape(item.category)}</span>
              </div>
            </div>
          </div>

          <div style="display: flex; align-items: center; gap: 1rem;">
            <!-- Live Product Rate Display -->
            <div style="text-align: right; min-width: 130px;">
              <span style="font-size: 0.72rem; color: var(--prd-text-muted); display: block;">معدل إنتاج المنتج:</span>
              <span id="prd-item-rate-${code}" style="font-weight: 800; font-size: 0.88rem; color: var(--prd-yellow-hover);">—</span>
            </div>

            <!-- Product Quantity Input -->
            <div style="display: flex; align-items: center; gap: 0.4rem;">
              <label for="prd-qty-${code}" style="font-size: 0.82rem; font-weight: 700; color: var(--prd-navy-dark); white-space: nowrap;">
                الكمية المنتجة (طن):
              </label>
              <input type="number" 
                     step="0.001" 
                     min="0.001" 
                     id="prd-qty-${code}" 
                     class="prd-product-qty-input" 
                     placeholder="0.000" 
                     value="${qtyVal}"
                     required>
            </div>

            <!-- Remove Product Button -->
            <button type="button" 
                    class="prd-btn-remove-prod" 
                    title="حذف المنتج من التسجيل"
                    onclick="ProductionApp.removeProduct('${code}')">
              &times; حذف
            </button>
          </div>
        `;

        container.appendChild(card);

        // Bind input event for quantity without re-rendering other inputs
        const input = card.querySelector(`#prd-qty-${code}`);
        input?.addEventListener('input', (e) => {
          const val = parseFloat(e.target.value);
          const currentItem = this.selectedProducts.get(code);
          if (currentItem) {
            currentItem.quantity = isNaN(val) ? null : val;
          }
          this.updateLiveCalculations();
        });
      });
    }

    /* ----------------------------------------------------------------------
       Live Automatic Calculations
       ---------------------------------------------------------------------- */
    static calculateDurationHours(startVal, endVal) {
      if (!startVal || !endVal) return null;

      const start = new Date(startVal);
      const end = new Date(endVal);

      if (isNaN(start.getTime()) || isNaN(end.getTime())) return null;

      const diffMs = end.getTime() - start.getTime();
      if (diffMs <= 0) return null;

      return diffMs / (1000 * 60 * 60);
    }

    static updateLiveCalculations() {
      const startVal = document.getElementById('prd-input-start-time')?.value;
      const endVal = document.getElementById('prd-input-end-time')?.value;
      const wasteVal = parseFloat(document.getElementById('prd-input-waste')?.value || 0);

      // 1. Duration Calculation
      const durationHours = this.calculateDurationHours(startVal, endVal);
      const durationBox = document.getElementById('prd-display-duration-box');
      const durationText = document.getElementById('prd-display-duration-text');
      const durationBadge = document.getElementById('prd-display-duration-badge');
      const durationHint = document.getElementById('prd-duration-hint');

      if (durationHours !== null && durationHours > 0) {
        const totalMinutes = Math.round(durationHours * 60);
        const hrs = Math.floor(totalMinutes / 60);
        const mins = totalMinutes % 60;

        if (durationText) durationText.textContent = `${durationHours.toFixed(2)} ساعة`;
        if (durationBadge) durationBadge.textContent = `${hrs} س ${mins} د`;
        if (durationHint) {
          durationHint.textContent = `فترة وردية سليمة (${durationHours.toFixed(2)} ساعة عمل فعلية)`;
          durationHint.style.color = '#10b981';
        }
        if (durationBox) durationBox.style.borderColor = '#10b981';
      } else {
        if (durationText) durationText.textContent = '—';
        if (durationBadge) durationBadge.textContent = 'غير صالح';
        if (durationHint) {
          if (startVal && endVal && new Date(endVal) <= new Date(startVal)) {
            durationHint.textContent = 'خطأ: نهاية الوردية يجب أن تكون بعد بداية الوردية';
            durationHint.style.color = '#ef4444';
          } else {
            durationHint.textContent = 'يرجى تحديد بداية ونهاية الوردية';
            durationHint.style.color = 'var(--prd-text-muted)';
          }
        }
        if (durationBox) durationBox.style.borderColor = '#cbd5e1';
      }

      // 2. Gross Production (Sum of Product Quantities)
      let totalGross = 0;
      this.selectedProducts.forEach((item, code) => {
        const qty = parseFloat(item.quantity);
        if (!isNaN(qty) && qty > 0) {
          totalGross += qty;
        }

        // Product Rate = Product Quantity / Duration Hours
        const itemRateEl = document.getElementById(`prd-item-rate-${code}`);
        if (itemRateEl) {
          if (durationHours && durationHours > 0 && !isNaN(qty) && qty > 0) {
            const prodRate = qty / durationHours;
            itemRateEl.textContent = `${prodRate.toFixed(3)} طن/س`;
          } else {
            itemRateEl.textContent = '—';
          }
        }
      });

      // 3. Net Production = Gross - Daily Waste
      const waste = isNaN(wasteVal) ? 0 : Math.max(0, wasteVal);
      const netProduction = Math.max(0, totalGross - waste);

      // 4. Overall Production Rate = Gross / Duration Hours
      let overallRate = null;
      if (durationHours && durationHours > 0 && totalGross > 0) {
        overallRate = totalGross / durationHours;
      }

      // 5. Waste Percentage = (Daily Waste / Total Gross) * 100
      let wastePct = null;
      if (totalGross > 0) {
        wastePct = (waste / totalGross) * 100;
      }

      // Update Live Arithmetic Displays
      const grossDisp = document.getElementById('prd-calc-gross-display');
      const netDisp = document.getElementById('prd-calc-net-display');
      const rateDisp = document.getElementById('prd-calc-rate-display');
      const wastePctDisp = document.getElementById('prd-calc-waste-pct-display');

      if (grossDisp) grossDisp.textContent = `${totalGross.toFixed(3)} طن`;
      if (netDisp) netDisp.textContent = `${netProduction.toFixed(3)} طن`;
      if (rateDisp) {
        rateDisp.textContent = overallRate !== null ? `${overallRate.toFixed(3)} طن/س` : '—';
      }
      if (wastePctDisp) {
        wastePctDisp.textContent = wastePct !== null ? `${wastePct.toFixed(3)}%` : '—';
      }
    }

    /* ----------------------------------------------------------------------
       Form Submission & Validation
       ---------------------------------------------------------------------- */
    static async handleDailySubmission() {
      const submitBtn = document.getElementById('prd-btn-submit-daily');
      const spinner = document.getElementById('prd-submit-spinner');

      const dateVal = document.getElementById('prd-input-date')?.value;
      const startVal = document.getElementById('prd-input-start-time')?.value;
      const endVal = document.getElementById('prd-input-end-time')?.value;
      const wasteVal = parseFloat(document.getElementById('prd-input-waste')?.value || 0);
      const notesVal = document.getElementById('prd-input-notes')?.value || '';

      if (!dateVal) {
        this.notify('يرجى تحديد تاريخ الإنتاج', 'error');
        return;
      }

      if (!startVal || !endVal) {
        this.notify('يرجى تحديد بداية ونهاية الوردية بدقة', 'error');
        return;
      }

      const durationHours = this.calculateDurationHours(startVal, endVal);
      if (!durationHours || durationHours <= 0) {
        this.notify('توقيت الوردية غير صالح: يجب أن يكون وقت النهاية بعد وقت البداية', 'error');
        return;
      }

      if (this.selectedProducts.size === 0) {
        this.notify('يرجى اختيار منتج واحد على الأقل وإدخال كميته المنتجة', 'error');
        return;
      }

      const productsPayload = [];
      let missingQty = false;

      this.selectedProducts.forEach((item, code) => {
        const qty = parseFloat(item.quantity);
        if (isNaN(qty) || qty <= 0) {
          missingQty = true;
        } else {
          productsPayload.push({
            productId: item.id || null,
            productName: item.name,
            quantity: qty,
            unit: item.unit || 'طن'
          });
        }
      });

      if (missingQty || productsPayload.length === 0) {
        this.notify('يرجى إدخال كمية منتجة موجبة (> 0) لكل منتج محدد', 'error');
        return;
      }

      if (isNaN(wasteVal) || wasteVal < 0) {
        this.notify('كمية الهالك يجب أن تكون رقماً موجباً أو صفراً', 'error');
        return;
      }

      const payload = {
        productionDate: dateVal,
        shiftStart: startVal,
        shiftEnd: endVal,
        wasteQuantity: wasteVal,
        notes: notesVal,
        products: productsPayload
      };

      if (submitBtn) submitBtn.disabled = true;
      if (spinner) spinner.style.display = 'inline-block';

      try {
        const res = await window.ChatApi.createDailyProductionSubmission(payload);
        if (res && res.success) {
          const refNum = res.data?.submissionNumber || res.data?.referenceNumber || '';
          this.notify(`تم حفظ تسجيل الإنتاج اليومي بنجاح برقم: ${refNum}`, 'success');
          this.resetDailyForm();
          this.loadStats();
          this.loadSubmissions();
          this.showSection('summary');
        } else {
          this.notify(res?.message || 'فشل حفظ تسجيل الإنتاج', 'error');
        }
      } catch (err) {
        console.error('Error submitting daily production:', err);
        this.notify('حدث خطأ غير متوقع أثناء حفظ تسجيل الإنتاج', 'error');
      } finally {
        if (submitBtn) submitBtn.disabled = false;
        if (spinner) spinner.style.display = 'none';
      }
    }

    static resetDailyForm() {
      this.selectedProducts.clear();
      this.renderSelectedProducts();

      const wasteInput = document.getElementById('prd-input-waste');
      if (wasteInput) wasteInput.value = '0.000';

      const notesInput = document.getElementById('prd-input-notes');
      if (notesInput) notesInput.value = '';

      this.setDefaultDateAndTimes();
      this.updateLiveCalculations();
    }

    /* ----------------------------------------------------------------------
       Today's Stats & Summary Dashboard
       ---------------------------------------------------------------------- */
    static async loadStats() {
      try {
        const res = await window.ChatApi.getDailyProductionSummaryStats();
        if (res && res.success && res.data) {
          const stats = res.data;
          const grossEl = document.getElementById('prd-stat-today-gross');
          const wasteEl = document.getElementById('prd-stat-today-waste');
          const netEl = document.getElementById('prd-stat-today-net');
          const rateEl = document.getElementById('prd-stat-today-rate');
          const wastePctEl = document.getElementById('prd-stat-today-waste-pct');
          const subCountEl = document.getElementById('prd-stat-today-submissions');

          const gross = stats.totalGrossToday ?? stats.totalGrossProduction ?? 0;
          const waste = stats.totalWasteToday ?? stats.totalWasteQuantity ?? 0;
          const net = stats.netProductionToday ?? stats.totalNetProduction ?? 0;
          const rate = stats.overallProductionRate ?? stats.overallHourlyRate ?? 0;
          const wastePct = stats.averageWastePercentage ?? stats.wastePercentage ?? 0;
          const count = stats.totalSubmissionsCount ?? stats.shiftSubmissionsCount ?? 0;

          if (grossEl) grossEl.textContent = `${Number(gross).toFixed(3)} طن`;
          if (wasteEl) wasteEl.textContent = `${Number(waste).toFixed(3)} طن`;
          if (netEl) netEl.textContent = `${Number(net).toFixed(3)} طن`;
          if (rateEl) rateEl.textContent = `${Number(rate).toFixed(3)} طن/ساعة`;
          if (wastePctEl) wastePctEl.textContent = `${Number(wastePct).toFixed(2)}%`;
          if (subCountEl) subCountEl.textContent = `${count}`;
        }
      } catch (err) {
        console.error('Error loading production stats:', err);
      }
    }

    static async loadSubmissions(filters = {}) {
      const tbody = document.getElementById('prd-daily-submissions-tbody');
      if (tbody) {
        tbody.innerHTML = '<tr><td colspan="9" style="text-align: center; padding: 2rem; color: var(--prd-text-muted);">جاري تحميل سجلات الإنتاج اليومية...</td></tr>';
      }

      try {
        const res = await window.ChatApi.getDailyProductionSubmissions(filters);
        if (res && res.success && Array.isArray(res.data)) {
          const submissions = res.data;
          this.renderSubmissionsTable(submissions);
          this.renderManagementSummary(submissions);

          const badgeCount = document.getElementById('prd-badge-records-count');
          if (badgeCount) badgeCount.textContent = `${submissions.length}`;

          this.populateEmployeeFilter(submissions);
        } else {
          if (tbody) {
            tbody.innerHTML = '<tr><td colspan="9" style="text-align: center; padding: 2rem; color: #ef4444;">تعذر تحميل السجلات</td></tr>';
          }
        }
      } catch (err) {
        console.error('Error loading submissions:', err);
        if (tbody) {
          tbody.innerHTML = '<tr><td colspan="9" style="text-align: center; padding: 2rem; color: #ef4444;">خطأ أثناء الاتصال بالخادم</td></tr>';
        }
      }
    }

    static populateEmployeeFilter(submissions) {
      const empSelect = document.getElementById('prd-filter-employee');
      if (!empSelect) return;

      const currentVal = empSelect.value;
      const emps = new Set();
      submissions.forEach(s => {
        if (s.employeeName) emps.add(s.employeeName);
      });

      empSelect.innerHTML = '<option value="">جميع مدراء الإنتاج</option>';
      emps.forEach(emp => {
        const opt = document.createElement('option');
        opt.value = emp;
        opt.textContent = emp;
        if (emp === currentVal) opt.selected = true;
        empSelect.appendChild(opt);
      });
    }

    static renderSubmissionsTable(submissions) {
      const tbody = document.getElementById('prd-daily-submissions-tbody');
      if (!tbody) return;

      if (submissions.length === 0) {
        tbody.innerHTML = `
          <tr>
            <td colspan="9" style="text-align: center; padding: 3rem 1.5rem; color: var(--prd-text-muted);">
              <div style="font-size: 2rem; margin-bottom: 0.5rem;">📋</div>
              <div style="font-weight: 700; font-size: 1rem; color: var(--prd-navy-dark);">لا توجد سجلات إنتاج مطابقة للبحث</div>
              <div style="font-size: 0.85rem; margin-top: 0.25rem;">استخدم نموذج "تسجيل الإنتاج اليومي" لتسجيل إنتاج الوردية</div>
            </td>
          </tr>
        `;
        return;
      }

      tbody.innerHTML = '';
      submissions.forEach(s => {
        const tr = document.createElement('tr');
        tr.style.borderBottom = '1px solid var(--prd-border)';

        const items = s.details || s.products || [];
        const productsHtml = items.map(p => `
          <div style="display: flex; align-items: center; justify-content: space-between; gap: 0.5rem; background: #f8fafc; padding: 0.2rem 0.5rem; border-radius: 4px; margin-bottom: 0.2rem; font-size: 0.78rem;">
            <span style="font-weight: 700; color: var(--prd-navy-dark);">${this.escape(p.productName || p.productNameAr)}:</span>
            <span style="font-weight: 800; color: #0284c7; direction: ltr;">${Number(p.quantity || p.producedQuantity).toFixed(3)} طن</span>
          </div>
        `).join('');

        const shiftVal = s.shift || s.shiftName || '';
        const shiftBadgeClass = shiftVal.includes('الأولى') ? 'background: #dbeafe; color: #1e40af;' : 'background: #fef3c7; color: #92400e;';

        const startTimeStr = this.formatDateTime(s.shiftStart);
        const endTimeStr = this.formatDateTime(s.shiftEnd);

        const refNum = s.submissionNumber || s.referenceNumber || '';
        const gross = s.totalGrossQuantity ?? s.totalGrossProduction ?? 0;
        const waste = s.wasteQuantity ?? 0;
        const net = s.netProductionQuantity ?? s.totalNetProduction ?? 0;
        const duration = s.durationHours ?? s.productionDurationHours ?? 0;
        const rate = s.productionRatePerHour ?? s.overallHourlyRate ?? 0;
        const wastePct = s.wastePercentage ?? 0;

        const prodDate = s.productionDate ? (typeof s.productionDate === 'string' ? s.productionDate.split('T')[0] : this.formatDate(new Date(s.productionDate))) : '';

        tr.innerHTML = `
          <td style="padding: 0.85rem 0.75rem; font-weight: 800; color: #0284c7; font-family: monospace;">
            ${this.escape(refNum)}
          </td>
          <td style="padding: 0.85rem 0.75rem; font-weight: 700; white-space: nowrap;">
            ${prodDate}
          </td>
          <td style="padding: 0.85rem 0.75rem;">
            <div style="font-weight: 700; color: var(--prd-navy-dark); font-size: 0.88rem;">${this.escape(s.employeeName)}</div>
            <span class="prd-badge" style="display: inline-block; margin-top: 0.2rem; padding: 0.15rem 0.45rem; border-radius: 4px; font-size: 0.72rem; font-weight: 700; ${shiftBadgeClass}">
              ${this.escape(shiftVal)}
            </span>
          </td>
          <td style="padding: 0.85rem 0.75rem; font-size: 0.78rem;">
            <div><span style="color: var(--prd-text-muted);">من:</span> ${startTimeStr}</div>
            <div><span style="color: var(--prd-text-muted);">إلى:</span> ${endTimeStr}</div>
            <div style="font-weight: 800; color: #0369a1; margin-top: 0.2rem;">⏱️ ${Number(duration).toFixed(2)} س</div>
          </td>
          <td style="padding: 0.85rem 0.75rem; min-width: 200px;">
            ${productsHtml}
          </td>
          <td style="padding: 0.85rem 0.75rem; font-weight: 800; color: #0284c7; font-size: 0.95rem; white-space: nowrap;">
            ${Number(gross).toFixed(3)} طن
          </td>
          <td style="padding: 0.85rem 0.75rem; font-size: 0.82rem; white-space: nowrap;">
            <div style="color: #ef4444; font-weight: 700;">هالك: ${Number(waste).toFixed(3)} طن</div>
            <div style="color: #10b981; font-weight: 800; margin-top: 0.15rem;">صافي: ${Number(net).toFixed(3)} طن</div>
          </td>
          <td style="padding: 0.85rem 0.75rem; font-size: 0.82rem; white-space: nowrap;">
            <div style="color: var(--prd-yellow-hover); font-weight: 800;">${Number(rate).toFixed(3)} طن/س</div>
            <div style="color: #8b5cf6; font-weight: 700; margin-top: 0.15rem;">هالك: ${Number(wastePct).toFixed(2)}%</div>
          </td>
          <td style="padding: 0.85rem 0.75rem; text-align: center; white-space: nowrap;">
            <div style="display: flex; gap: 0.4rem; justify-content: center;">
              <button type="button" class="prd-btn prd-btn-outline" style="padding: 0.3rem 0.6rem; font-size: 0.75rem;" onclick="ProductionApp.showSubmissionDetails(${s.id})">
                عرض
              </button>
              <button type="button" class="prd-btn prd-btn-navy" style="padding: 0.3rem 0.6rem; font-size: 0.75rem; background: #0284c7;" onclick="ProductionApp.openEditSubmissionModal(${s.id})">
                تعديل
              </button>
            </div>
          </td>
        `;

        tbody.appendChild(tr);
      });
    }

    /* ----------------------------------------------------------------------
       Management Q&A Summary Aggregation (No Double Counting)
       ---------------------------------------------------------------------- */
    static renderManagementSummary(submissions) {
      let totalGross = 0;
      let totalWaste = 0;
      let totalDurationHours = 0;
      const productMap = new Map();

      submissions.forEach(s => {
        totalGross += Number(s.totalGrossQuantity ?? s.totalGrossProduction ?? 0);
        totalWaste += Number(s.wasteQuantity ?? 0);
        totalDurationHours += Number(s.durationHours ?? s.productionDurationHours ?? 0);

        const items = s.details || s.products || [];
        items.forEach(p => {
          const name = p.productName || p.productNameAr;
          const qty = Number(p.quantity || p.producedQuantity || 0);
          if (!productMap.has(name)) {
            productMap.set(name, { name: name, total: 0 });
          }
          productMap.get(name).total += qty;
        });
      });

      const totalNet = Math.max(0, totalGross - totalWaste);
      const avgRate = totalDurationHours > 0 ? (totalGross / totalDurationHours) : 0;
      const wastePct = totalGross > 0 ? ((totalWaste / totalGross) * 100) : 0;

      const grossEl = document.getElementById('prd-summary-total-gross');
      const wasteEl = document.getElementById('prd-summary-total-waste');
      const netEl = document.getElementById('prd-summary-total-net');
      const rateEl = document.getElementById('prd-summary-avg-rate');
      const wastePctEl = document.getElementById('prd-summary-waste-pct');
      const breakdownEl = document.getElementById('prd-summary-products-breakdown');

      if (grossEl) grossEl.textContent = `${totalGross.toFixed(3)} طن`;
      if (wasteEl) wasteEl.textContent = `${totalWaste.toFixed(3)} طن`;
      if (netEl) netEl.textContent = `${totalNet.toFixed(3)} طن`;
      if (rateEl) rateEl.textContent = `${avgRate.toFixed(3)} طن/س`;
      if (wastePctEl) wastePctEl.textContent = `${wastePct.toFixed(2)}%`;

      if (breakdownEl) {
        if (productMap.size === 0) {
          breakdownEl.innerHTML = '<span style="font-size: 0.8rem; color: #cbd5e1;">لا توجد سجلات مطابقة للشروط المحددة</span>';
        } else {
          let chipsHtml = '';
          productMap.forEach(item => {
            chipsHtml += `
              <div style="background: rgba(255, 255, 255, 0.12); border: 1px solid rgba(255, 255, 255, 0.2); border-radius: 6px; padding: 0.35rem 0.75rem; display: flex; align-items: center; gap: 0.5rem;">
                <span style="font-size: 0.82rem; font-weight: 700; color: #ffffff;">${this.escape(item.name)}:</span>
                <span style="font-size: 0.85rem; font-weight: 800; color: #38bdf8; direction: ltr;">${item.total.toFixed(3)} طن</span>
              </div>
            `;
          });
          breakdownEl.innerHTML = chipsHtml;
        }
      }
    }

    /* ----------------------------------------------------------------------
       Summary Filter Actions
       ---------------------------------------------------------------------- */
    static applySummaryFilters() {
      const startDate = document.getElementById('prd-filter-start-date')?.value || '';
      const endDate = document.getElementById('prd-filter-end-date')?.value || '';
      const shift = document.getElementById('prd-filter-shift')?.value || '';
      const employee = document.getElementById('prd-filter-employee')?.value || '';
      const product = document.getElementById('prd-filter-product')?.value || '';

      const filters = {};
      if (startDate) filters.startDate = startDate;
      if (endDate) filters.endDate = endDate;
      if (shift) filters.shift = shift;
      if (employee) filters.employee = employee;
      if (product) filters.product = product;

      this.loadSubmissions(filters);
    }

    static resetSummaryFilters() {
      const startDateInput = document.getElementById('prd-filter-start-date');
      const endDateInput = document.getElementById('prd-filter-end-date');
      const shiftSelect = document.getElementById('prd-filter-shift');
      const empSelect = document.getElementById('prd-filter-employee');
      const prodSelect = document.getElementById('prd-filter-product');

      if (startDateInput) startDateInput.value = '';
      if (endDateInput) endDateInput.value = '';
      if (shiftSelect) shiftSelect.value = '';
      if (empSelect) empSelect.value = '';
      if (prodSelect) prodSelect.value = '';

      this.loadSubmissions();
    }

    /* ----------------------------------------------------------------------
       Details Modal
       ---------------------------------------------------------------------- */
    static async showSubmissionDetails(id) {
      try {
        const res = await window.ChatApi.getDailyProductionSubmission(id);
        if (!res || !res.success || !res.data) {
          this.notify('تعذر استرجاع تفاصيل السجل', 'error');
          return;
        }

        const s = res.data;
        const refNum = s.submissionNumber || s.referenceNumber || '';
        const shiftVal = s.shift || s.shiftName || '';
        const gross = s.totalGrossQuantity ?? s.totalGrossProduction ?? 0;
        const waste = s.wasteQuantity ?? 0;
        const net = s.netProductionQuantity ?? s.totalNetProduction ?? 0;
        const duration = s.durationHours ?? s.productionDurationHours ?? 0;
        const rate = s.productionRatePerHour ?? s.overallHourlyRate ?? 0;
        const wastePct = s.wastePercentage ?? 0;

        const titleEl = document.getElementById('prd-detail-modal-title');
        const contentEl = document.getElementById('prd-detail-modal-content');

        if (titleEl) titleEl.textContent = `تفاصيل تسجيل الإنتاج: ${refNum}`;

        const items = s.details || s.products || [];
        const itemsHtml = items.map(p => `
          <tr style="border-bottom: 1px solid var(--prd-border);">
            <td style="padding: 0.65rem 0.75rem; font-weight: 700;">${this.escape(p.productName || p.productNameAr)}</td>
            <td style="padding: 0.65rem 0.75rem; font-weight: 800; color: #0284c7; direction: ltr;">${Number(p.quantity || p.producedQuantity).toFixed(3)} طن</td>
            <td style="padding: 0.65rem 0.75rem; font-weight: 700; color: var(--prd-yellow-hover); direction: ltr;">${Number(p.productionRatePerHour || 0).toFixed(3)} طن/س</td>
          </tr>
        `).join('');

        const prodDate = s.productionDate ? (typeof s.productionDate === 'string' ? s.productionDate.split('T')[0] : this.formatDate(new Date(s.productionDate))) : '';

        if (contentEl) {
          contentEl.innerHTML = `
            <div style="display: grid; grid-template-columns: repeat(auto-fit, minmax(200px, 1fr)); gap: 1rem; background: #f8fafc; padding: 1rem; border-radius: 8px; margin-bottom: 1.25rem;">
              <div>
                <span style="font-size: 0.75rem; color: var(--prd-text-muted); display: block;">رقم التسجيل:</span>
                <span style="font-weight: 800; color: #0284c7; font-family: monospace; font-size: 1rem;">${this.escape(refNum)}</span>
              </div>
              <div>
                <span style="font-size: 0.75rem; color: var(--prd-text-muted); display: block;">تاريخ الإنتاج:</span>
                <span style="font-weight: 800; color: var(--prd-navy-dark);">${prodDate}</span>
              </div>
              <div>
                <span style="font-size: 0.75rem; color: var(--prd-text-muted); display: block;">الوردية المعينة:</span>
                <span style="font-weight: 800; color: var(--prd-navy-dark);">${this.escape(shiftVal)}</span>
              </div>
              <div>
                <span style="font-size: 0.75rem; color: var(--prd-text-muted); display: block;">مدير الإنتاج المسؤول:</span>
                <span style="font-weight: 800; color: var(--prd-navy-dark);">${this.escape(s.employeeName)} (${this.escape(s.employeeEmail)})</span>
              </div>
              <div>
                <span style="font-size: 0.75rem; color: var(--prd-text-muted); display: block;">بداية ونهاية الوردية:</span>
                <span style="font-weight: 700; font-size: 0.85rem;">${this.formatDateTime(s.shiftStart)} إلى ${this.formatDateTime(s.shiftEnd)}</span>
              </div>
              <div>
                <span style="font-size: 0.75rem; color: var(--prd-text-muted); display: block;">مدة الإنتاج الفعلية:</span>
                <span style="font-weight: 800; color: #0369a1;">⏱️ ${Number(duration).toFixed(2)} ساعة</span>
              </div>
            </div>

            <div style="margin-bottom: 1.25rem;">
              <h4 style="margin: 0 0 0.5rem; font-size: 0.95rem; font-weight: 800; color: var(--prd-navy-dark);">المنتجات المسجلة ومعدلات الإنتاج:</h4>
              <table style="width: 100%; border-collapse: collapse; text-align: right; font-size: 0.85rem;">
                <thead>
                  <tr style="background: #f1f5f9; border-bottom: 2px solid var(--prd-border); color: var(--prd-text-muted);">
                    <th style="padding: 0.65rem 0.75rem;">اسم المنتج</th>
                    <th style="padding: 0.65rem 0.75rem;">الكمية المنتجة (طن)</th>
                    <th style="padding: 0.65rem 0.75rem;">معدل إنتاج المنتج</th>
                  </tr>
                </thead>
                <tbody>
                  ${itemsHtml}
                </tbody>
              </table>
            </div>

            <div style="display: grid; grid-template-columns: repeat(auto-fit, minmax(130px, 1fr)); gap: 0.75rem; background: #e0f2fe; border: 1px solid #bae6fd; border-radius: 8px; padding: 1rem; margin-bottom: 1.25rem;">
              <div>
                <div style="font-size: 0.72rem; color: #0369a1;">إجمالي الإنتاج</div>
                <div style="font-size: 1.15rem; font-weight: 800; color: #0284c7;">${Number(gross).toFixed(3)} طن</div>
              </div>
              <div>
                <div style="font-size: 0.72rem; color: #0369a1;">كمية الهالك</div>
                <div style="font-size: 1.15rem; font-weight: 800; color: #ef4444;">${Number(waste).toFixed(3)} طن</div>
              </div>
              <div>
                <div style="font-size: 0.72rem; color: #0369a1;">صافي الإنتاج</div>
                <div style="font-size: 1.15rem; font-weight: 800; color: #10b981;">${Number(net).toFixed(3)} طن</div>
              </div>
              <div>
                <div style="font-size: 0.72rem; color: #0369a1;">معدل الإنتاج الكلي</div>
                <div style="font-size: 1.15rem; font-weight: 800; color: var(--prd-yellow-hover);">${Number(rate).toFixed(3)} طن/س</div>
              </div>
              <div>
                <div style="font-size: 0.72rem; color: #0369a1;">نسبة الهالك</div>
                <div style="font-size: 1.15rem; font-weight: 800; color: #8b5cf6;">${Number(wastePct).toFixed(2)}%</div>
              </div>
            </div>

            <div style="margin-bottom: 1rem;">
              <span style="font-size: 0.78rem; font-weight: 700; color: var(--prd-text-muted); display: block; margin-bottom: 0.25rem;">ملاحظات الإنتاج:</span>
              <div style="background: #ffffff; border: 1px solid var(--prd-border); border-radius: 6px; padding: 0.75rem; font-size: 0.88rem; color: var(--prd-text);">
                ${s.notes ? this.escape(s.notes) : '<span style="color: var(--prd-text-muted);">لا توجد ملاحظات مسجلة</span>'}
              </div>
            </div>

            <div style="font-size: 0.72rem; color: var(--prd-text-muted); border-top: 1px solid var(--prd-border); padding-top: 0.75rem; display: flex; justify-content: space-between; flex-wrap: wrap; gap: 0.5rem;">
              <span>تم الإدخال بواسطة: <b>${this.escape(s.createdBy || s.employeeEmail)}</b> بتاريخ: ${this.formatDateTime(s.createdAt)}</span>
              ${s.updatedAt ? `<span>آخر تعديل بواسطة: <b>${this.escape(s.updatedBy || '')}</b> بتاريخ: ${this.formatDateTime(s.updatedAt)}</span>` : ''}
            </div>
          `;
        }

        this.openModal('prd-modal-submission-details');
      } catch (err) {
        console.error('Error viewing submission details:', err);
        this.notify('حدث خطأ أثناء تحميل تفاصيل السجل', 'error');
      }
    }

    /* ----------------------------------------------------------------------
       Edit Modal
       ---------------------------------------------------------------------- */
    static async openEditSubmissionModal(id) {
      try {
        const res = await window.ChatApi.getDailyProductionSubmission(id);
        if (!res || !res.success || !res.data) {
          this.notify('تعذر تحميل بيانات السجل للتعديل', 'error');
          return;
        }

        const s = res.data;
        document.getElementById('prd-edit-id').value = s.id;
        document.getElementById('prd-edit-ref-display').textContent = s.submissionNumber || s.referenceNumber;
        document.getElementById('prd-edit-shift-display').textContent = s.shift || s.shiftName;
        document.getElementById('prd-edit-employee-display').textContent = s.employeeName;

        const prodDate = s.productionDate ? (typeof s.productionDate === 'string' ? s.productionDate.split('T')[0] : this.formatDate(new Date(s.productionDate))) : '';
        document.getElementById('prd-edit-date').value = prodDate;
        document.getElementById('prd-edit-start-time').value = s.shiftStart.substring(0, 16);
        document.getElementById('prd-edit-end-time').value = s.shiftEnd.substring(0, 16);
        document.getElementById('prd-edit-waste').value = Number(s.wasteQuantity || 0).toFixed(3);
        document.getElementById('prd-edit-notes').value = s.notes || '';

        this.editSelectedProducts.clear();
        const items = s.details || s.products || [];
        items.forEach(p => {
          const name = p.productName || p.productNameAr;
          this.editSelectedProducts.set(name, {
            id: p.productId || p.productCatalogId,
            name: name,
            quantity: Number(p.quantity || p.producedQuantity)
          });
        });

        this.renderEditProducts();
        this.updateEditLiveCalculations();
        this.openModal('prd-modal-edit-submission');
      } catch (err) {
        console.error('Error opening edit modal:', err);
        this.notify('حدث خطأ أثناء فتح نافذة التعديل', 'error');
      }
    }

    static addEditProduct(productCode) {
      if (!productCode) return;

      const prod = this.catalog.find(p => (p.code === productCode || p.productCode === productCode)) || {
        id: 0,
        name: productCode
      };

      const name = prod.name || prod.productNameAr || productCode;
      if (this.editSelectedProducts.has(name)) return;

      this.editSelectedProducts.set(name, {
        id: prod.id,
        name: name,
        quantity: null
      });

      this.renderEditProducts();
      this.updateEditLiveCalculations();
    }

    static removeEditProduct(productName) {
      this.editSelectedProducts.delete(productName);
      this.renderEditProducts();
      this.updateEditLiveCalculations();
    }

    static renderEditProducts() {
      const container = document.getElementById('prd-edit-products-container');
      if (!container) return;

      container.innerHTML = '';
      if (this.editSelectedProducts.size === 0) {
        container.innerHTML = '<div style="color: #ef4444; font-size: 0.85rem; padding: 0.5rem;">يجب اختيار منتج واحد على الأقل</div>';
        return;
      }

      this.editSelectedProducts.forEach((item, name) => {
        const row = document.createElement('div');
        row.style.cssText = 'display: flex; align-items: center; justify-content: space-between; background: #ffffff; padding: 0.5rem 0.75rem; border: 1px solid var(--prd-border); border-radius: 6px;';

        const qtyVal = (item.quantity !== null && item.quantity !== undefined && item.quantity !== '') ? item.quantity : '';

        row.innerHTML = `
          <div style="font-weight: 700; font-size: 0.85rem;">
            ${this.escape(item.name)}
          </div>
          <div style="display: flex; align-items: center; gap: 0.5rem;">
            <input type="number" 
                   step="0.001" 
                   min="0.001" 
                   id="prd-edit-qty-${this.escape(item.name)}" 
                   style="width: 110px; padding: 0.35rem 0.55rem; font-weight: 700; border: 1px solid #0284c7; border-radius: 4px; text-align: left; direction: ltr;" 
                   value="${qtyVal}" 
                   placeholder="0.000" 
                   required>
            <button type="button" class="prd-btn-remove-prod" style="padding: 0.25rem 0.5rem;" onclick="ProductionApp.removeEditProduct('${this.escape(item.name)}')">&times;</button>
          </div>
        `;

        container.appendChild(row);

        const input = row.querySelector(`input[type="number"]`);
        input?.addEventListener('input', (e) => {
          const val = parseFloat(e.target.value);
          const currentItem = this.editSelectedProducts.get(name);
          if (currentItem) {
            currentItem.quantity = isNaN(val) ? null : val;
          }
          this.updateEditLiveCalculations();
        });
      });
    }

    static updateEditLiveCalculations() {
      const startVal = document.getElementById('prd-edit-start-time')?.value;
      const endVal = document.getElementById('prd-edit-end-time')?.value;
      const wasteVal = parseFloat(document.getElementById('prd-edit-waste')?.value || 0);

      const durationHours = this.calculateDurationHours(startVal, endVal);

      let totalGross = 0;
      this.editSelectedProducts.forEach(item => {
        const qty = parseFloat(item.quantity);
        if (!isNaN(qty) && qty > 0) totalGross += qty;
      });

      const waste = isNaN(wasteVal) ? 0 : Math.max(0, wasteVal);
      const net = Math.max(0, totalGross - waste);
      const rate = (durationHours && durationHours > 0 && totalGross > 0) ? (totalGross / durationHours) : 0;
      const wastePct = totalGross > 0 ? ((waste / totalGross) * 100) : 0;

      document.getElementById('prd-edit-calc-gross').textContent = totalGross.toFixed(3);
      document.getElementById('prd-edit-calc-net').textContent = net.toFixed(3);
      document.getElementById('prd-edit-calc-rate').textContent = rate.toFixed(3);
      document.getElementById('prd-edit-calc-waste').textContent = `${wastePct.toFixed(2)}%`;
    }

    static async handleEditSubmissionSubmit() {
      const id = parseInt(document.getElementById('prd-edit-id')?.value, 10);
      const saveBtn = document.getElementById('prd-edit-btn-save');

      const dateVal = document.getElementById('prd-edit-date')?.value;
      const startVal = document.getElementById('prd-edit-start-time')?.value;
      const endVal = document.getElementById('prd-edit-end-time')?.value;
      const wasteVal = parseFloat(document.getElementById('prd-edit-waste')?.value || 0);
      const notesVal = document.getElementById('prd-edit-notes')?.value || '';

      if (!dateVal || !startVal || !endVal) {
        this.notify('يرجى ملء جميع الحقول المطلوبة', 'error');
        return;
      }

      const durationHours = this.calculateDurationHours(startVal, endVal);
      if (!durationHours || durationHours <= 0) {
        this.notify('توقيت الوردية غير صالح: وقت النهاية يجب أن يكون بعد وقت البداية', 'error');
        return;
      }

      const productsPayload = [];
      let missingQty = false;

      this.editSelectedProducts.forEach((item, name) => {
        const qty = parseFloat(item.quantity);
        if (isNaN(qty) || qty <= 0) {
          missingQty = true;
        } else {
          productsPayload.push({
            productId: item.id || null,
            productName: item.name,
            quantity: qty,
            unit: 'طن'
          });
        }
      });

      if (missingQty || productsPayload.length === 0) {
        this.notify('يرجى إدخال كمية منتجة موجبة لكل منتج محدد', 'error');
        return;
      }

      const payload = {
        productionDate: dateVal,
        shiftStart: startVal,
        shiftEnd: endVal,
        wasteQuantity: isNaN(wasteVal) ? 0 : wasteVal,
        notes: notesVal,
        products: productsPayload
      };

      if (saveBtn) saveBtn.disabled = true;

      try {
        const res = await window.ChatApi.updateDailyProductionSubmission(id, payload);
        if (res && res.success) {
          this.notify('تم تحديث تسجيل الإنتاج بنجاح', 'success');
          this.closeModal('prd-modal-edit-submission');
          this.loadStats();
          this.loadSubmissions();
        } else {
          this.notify(res?.message || 'فشل تحديث تسجيل الإنتاج', 'error');
        }
      } catch (err) {
        console.error('Error updating submission:', err);
        this.notify('حدث خطأ أثناء حفظ التعديلات', 'error');
      } finally {
        if (saveBtn) saveBtn.disabled = false;
      }
    }

    /* ----------------------------------------------------------------------
       Modal Helpers & Notifications
       ---------------------------------------------------------------------- */
    static openModal(modalId) {
      const el = document.getElementById(modalId);
      if (el) el.style.display = 'flex';
    }

    static closeModal(modalId) {
      const el = document.getElementById(modalId);
      if (el) el.style.display = 'none';
    }

    static notify(message, type = 'info') {
      if (window.ChatApp && typeof window.ChatApp.showToast === 'function') {
        window.ChatApp.showToast(message, type);
      } else {
        alert(message);
      }
    }

    static formatDate(d) {
      const year = d.getFullYear();
      const month = String(d.getMonth() + 1).padStart(2, '0');
      const day = String(d.getDate()).padStart(2, '0');
      return `${year}-${month}-${day}`;
    }

    static formatDateTime(dtStr) {
      if (!dtStr) return '—';
      try {
        const d = new Date(dtStr);
        if (isNaN(d.getTime())) return dtStr;
        return d.toLocaleString('ar-EG', {
          year: 'numeric',
          month: '2-digit',
          day: '2-digit',
          hour: '2-digit',
          minute: '2-digit',
          hour12: true
        });
      } catch {
        return dtStr;
      }
    }

    static escape(str) {
      if (str === null || str === undefined) return '';
      return String(str)
        .replace(/&/g, '&amp;')
        .replace(/</g, '&lt;')
        .replace(/>/g, '&gt;')
        .replace(/"/g, '&quot;')
        .replace(/'/g, '&#39;');
    }
  }

  // Expose to window for app navigation and cross-module calls
  window.ProductionApp = ProductionApp;

  // Auto-init on DOMContentLoaded if element exists
  if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', () => ProductionApp.init());
  } else {
    ProductionApp.init();
  }
})();
