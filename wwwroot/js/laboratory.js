/* ==========================================================================
   FACESTONE LABORATORY DEPARTMENT CONTROLLER (قسم المعمل)
   Smooth Slider Navigation • Physical / Chemical sections • ASTM C1240
   Per-user sample sequencing • Shared batch reference • State preservation
   ========================================================================== */

(function () {
  'use strict';

  const LAB_SECTION_AR = { PHYSICAL: 'المعمل الفيزيائي', CHEMICAL: 'المعمل الكيميائي' };

  const CHEMICAL_TESTS = [
    { index: 1, name: 'أكسيد السيليكون (SiO₂)', unit: '%', min: 85, minLabel: '85%', max: null, maxLabel: '-', method: 'ASTM C1240', defaultVal: '' },
    { index: 2, name: 'الكلوريدات (Cl⁻)', unit: '%', min: null, minLabel: '-', max: 0.10, maxLabel: '≤ 0.10%', method: 'ASTM C1240', defaultVal: '' },
    { index: 3, name: 'أكسيد الكالسيوم (CaO)', unit: '%', min: null, minLabel: '-', max: null, maxLabel: '-', method: 'ASTM C1240', defaultVal: '' },
    { index: 4, name: 'أكسيد الماغنسيوم (MgO)', unit: '%', min: null, minLabel: '-', max: null, maxLabel: '-', method: 'ASTM C1240', defaultVal: '' },
    { index: 5, name: 'أكسيد الحديد (Fe₂O₃)', unit: '%', min: null, minLabel: '-', max: null, maxLabel: '-', method: 'ASTM C1240', defaultVal: '' },
    { index: 6, name: 'أكسيد الالومنيوم (Al₂O₃)', unit: '%', min: null, minLabel: '-', max: null, maxLabel: '-', method: 'ASTM C1240', defaultVal: '' },
    { index: 7, name: 'الفقد بالحرق (L.O.I)', unit: '%', min: null, minLabel: '-', max: 6.0, maxLabel: '≤ 6%', method: 'ASTM C1240', defaultVal: '' },
    { index: 8, name: 'الرطوبة (Moisture Content)', unit: '%', min: null, minLabel: '-', max: 3.0, maxLabel: '≤ 3%', method: 'ASTM C1240', defaultVal: '' }
  ];

  const PHYSICAL_TESTS = [
    { index: 1, name: 'المساحة السطحية للسطح (النعومة)', unit: 'm²/gm', min: 15.0, minLabel: '15 m²/g', max: null, maxLabel: '-', method: 'ASTM C1240', defaultVal: '' },
    { index: 2, name: 'المحتجز على منخل مقاس 45 ميكرون', unit: '%', min: null, minLabel: '-', max: 10.0, maxLabel: '10%', method: 'ASTM C1240', defaultVal: '' },
    { index: 3, name: 'اللون', unit: '-', min: null, minLabel: '-', max: null, maxLabel: '-', method: 'ASTM C1240', defaultVal: 'رمادى', isText: true },
    { index: 4, name: 'زمن الشك الإبتدائي', unit: 'بالدقيقه', min: null, minLabel: '-', max: null, maxLabel: '-', method: 'ASTM C1240', defaultVal: '' },
    { index: 5, name: 'الضغط بعد (يومين)', unit: 'N/mm²', min: null, minLabel: '-', max: null, maxLabel: '-', method: 'ASTM C1240', defaultVal: '' },
    { index: 6, name: 'الضغط بعد 7 أيام', unit: 'N/mm²', min: null, minLabel: '-', max: null, maxLabel: '-', method: 'ASTM C1240', defaultVal: '' },
    { index: 7, name: 'معامل الفاعلية بعد 7 أيام', unit: '%', min: 105.0001, minLabel: '> 105 %', max: null, maxLabel: '-', method: 'ASTM C1240', defaultVal: '' },
    { index: 8, name: 'الضغط بعد 28 يوم', unit: 'N/mm²', min: null, minLabel: '-', max: null, maxLabel: '-', method: 'ASTM C1240', defaultVal: '' }
  ];

  const STATUS_AR = {
    PENDING: ['قيد المراجعة', 'lab-badge-pending'],
    APPROVED: ['معتمد', 'lab-badge-approved'],
    REJECTED: ['مرفوض', 'lab-badge-rejected']
  };

  const CONFORMITY_AR = {
    CONFORMS: ['مطابق للمواصفات', 'lab-badge-conforms'],
    NON_CONFORMING: ['غير مطابق للمواصفات', 'lab-badge-nonconforming'],
    PENDING_TESTING: ['يحتاج فحوصات إضافية', 'lab-badge-testing']
  };

  class LaboratoryApp {
    static init() {
      this.root = document.getElementById('laboratory-page-root');
      if (!this.root) return;

      this.activeSection = 'PHYSICAL';
      this.activeTab = 'raw';
      this.hasPhysical = false;
      this.hasChemical = false;
      this.nextSampleNumber = 1;
      this.batchReferences = [];
      this.cachedReports = [];
      this.isDirty = false;
      this.finalFormLoaded = false;
      this.inprocFormLoaded = false;
      this.currentVStep = 0;
      this.totalVSteps = 4;
      this.highestVisitedVStep = 0;

      this.bindElements();
      if (!this.eventsBound) {
        this.bindEvents();
        this.eventsBound = true;
      }
      this.updateUserBadge();
      this.resolveAccessAndRedirect();
    }

    static bindElements() {
      // Header & section switch
      this.sectionSwitch = document.getElementById('lab-section-switch');
      this.btnPhysical = document.getElementById('lab-btn-section-physical');
      this.btnChemical = document.getElementById('lab-btn-section-chemical');
      this.sectionSubtitle = document.getElementById('lab-section-subtitle');
      this.backBtn = document.getElementById('laboratory-back-to-departments-btn');

      // Independent Form Navigation Tabs
      this.formTabs = document.querySelectorAll('.lab-form-tab');
      this.panels = document.querySelectorAll('.lab-panel');
      this.tabRawTitle = document.getElementById('lab-tab-raw-title');

      // Panel Containers
      this.panelRaw = document.getElementById('lab-panel-raw');
      this.panelFinal = document.getElementById('lab-panel-final');
      this.panelInproc = document.getElementById('lab-panel-inproc');
      this.panelHistory = document.getElementById('lab-panel-history');
      this.panelTrace = document.getElementById('lab-panel-trace');

      // Inner Form Containers
      this.formContainer = document.getElementById('lab-form-container');
      this.finalContainer = document.getElementById('lab-final-container');
      this.inprocContainer = document.getElementById('lab-inproc-container');

      // History view elements
      this.historyTbody = document.getElementById('lab-history-tbody');
      this.histSectionFilter = document.getElementById('lab-hist-f-section');
      this.histComplyFilter = document.getElementById('lab-hist-f-comply');
      this.histSearchInput = document.getElementById('lab-hist-f-search');
      this.histRefreshBtn = document.getElementById('lab-hist-refresh-btn');

      // Trace elements
      this.traceInput = document.getElementById('lab-trace-ref-input');
      this.traceBtn = document.getElementById('lab-trace-btn');
      this.traceResult = document.getElementById('lab-trace-result');

      // Modal elements
      this.reportModal = document.getElementById('lab-report-modal');
      this.modalSheetContent = document.getElementById('lab-modal-sheet-content');
      this.modalPrintBtn = document.getElementById('lab-modal-print-btn');
      this.modalCloseBtn = document.getElementById('lab-modal-close-btn');
    }

    static bindEvents() {
      // Independent Tab navigation clicks
      this.formTabs.forEach((tab) => {
        tab.addEventListener('click', () => {
          const tabKey = tab.getAttribute('data-tab');
          if (tabKey) this.switchTab(tabKey);
        });
      });

      // Section switches
      this.btnPhysical?.addEventListener('click', () => this.switchSection('PHYSICAL'));
      this.btnChemical?.addEventListener('click', () => this.switchSection('CHEMICAL'));

      // History filters
      this.histSectionFilter?.addEventListener('change', () => this.loadHistoryList());
      this.histComplyFilter?.addEventListener('change', () => this.loadHistoryList());
      this.histSearchInput?.addEventListener('input', this.debounce(() => this.loadHistoryList(), 300));
      this.histRefreshBtn?.addEventListener('click', () => this.loadHistoryList());

      // Traceability
      this.traceBtn?.addEventListener('click', () => this.runTraceability());
      this.traceInput?.addEventListener('keydown', (e) => { if (e.key === 'Enter') this.runTraceability(); });

      // Print Modal
      this.modalCloseBtn?.addEventListener('click', () => this.closePrintModal());
      this.modalPrintBtn?.addEventListener('click', () => window.print());
      this.reportModal?.addEventListener('click', (e) => {
        if (e.target === this.reportModal) this.closePrintModal();
      });

      // Track unsaved changes
      this.root?.addEventListener('input', (e) => {
        if (['INPUT', 'TEXTAREA', 'SELECT'].includes(e.target.tagName)) {
          this.isDirty = true;
        }
      });

      // Protect against accidental loss of data when clicking the back button
      this.backBtn?.addEventListener('click', (e) => {
        if (!this.confirmLeaveIfDirty()) {
          e.preventDefault();
          e.stopImmediatePropagation();
        }
      }, true);
    }

    static confirmLeaveIfDirty() {
      if (!this.isDirty) return true;
      const confirmLeave = window.confirm('تنبيه: يوجد بيانات تم إدخالها في نماذج قسم المعمل لم تُحفظ بعد.\nهل أنت متأكد من رغبتك في مغادرة القسم وفقدان هذه البيانات؟');
      if (confirmLeave) {
        this.isDirty = false;
        return true;
      }
      return false;
    }

    // ======================================================================
    // INDEPENDENT FORM NAVIGATION (Open any form directly)
    // ======================================================================
    static switchTab(tabKey) {
      if (!tabKey) return;
      this.activeTab = tabKey;

      // Update tabs active state
      if (this.formTabs) {
        this.formTabs.forEach((tab) => {
          const isActive = tab.getAttribute('data-tab') === tabKey;
          tab.classList.toggle('active', isActive);
          tab.setAttribute('aria-selected', isActive ? 'true' : 'false');
        });
      }

      // Update panels active state (keeps all form inputs untouched in DOM)
      const tabMap = {
        raw: this.panelRaw,
        final: this.panelFinal,
        inproc: this.panelInproc,
        history: this.panelHistory,
        trace: this.panelTrace
      };

      Object.entries(tabMap).forEach(([key, panel]) => {
        if (!panel) return;
        const isActive = key === tabKey;
        panel.classList.toggle('active', isActive);
        panel.style.display = isActive ? 'block' : 'none';
      });

      // Lazy load views when their tab is opened
      if (tabKey === 'final' && !this.finalFormLoaded) {
        this.renderFinalCertView();
        this.finalFormLoaded = true;
      } else if (tabKey === 'inproc' && !this.inprocFormLoaded) {
        this.renderInProcessView();
        this.inprocFormLoaded = true;
      } else if (tabKey === 'history') {
        this.loadHistoryList();
      }
    }

    static openTab(tabKey) {
      this.switchTab(tabKey);
    }

    static goToSlide(index) {
      const keys = ['raw', 'final', 'inproc', 'history', 'trace'];
      if (typeof index === 'number' && keys[index]) {
        this.switchTab(keys[index]);
      } else if (typeof index === 'string') {
        this.switchTab(index);
      }
    }

    static updateUserBadge(userParam) {
      const user = userParam || (window.ChatApi ? window.ChatApi.getCurrentUser() : null);
      if (!user) return;
      const initials = (user.fullName || user.username || 'FS').split(' ').map(w => w[0]).slice(0, 2).join('').toUpperCase();
      const avatar = document.getElementById('lab-user-avatar');
      const name = document.getElementById('lab-user-name');
      const role = document.getElementById('lab-user-role');
      if (avatar) avatar.textContent = initials;
      if (name) name.textContent = user.fullName || user.username;
      if (role) role.textContent = this.roleArabic(user.role);
    }

    static roleArabic(role) {
      if (!role) return 'موظف معمل';
      const r = role.trim().toUpperCase();
      if (r === 'CEO') return 'الرئيس التنفيذي';
      if (r === 'IT') return 'مسؤول النظم';
      if (r === 'PHYSICAL_LAB_EMPLOYEE' || r === 'PHYSICAL LABORATORY EMPLOYEE') return 'موظف المعمل الفيزيائي';
      if (r === 'CHEMICAL_LAB_EMPLOYEE' || r === 'CHEMICAL LABORATORY EMPLOYEE') return 'موظف المعمل الكيميائي';
      return role;
    }

    static async resolveAccessAndRedirect(subRoute) {
      let hasPhysical = false, hasChemical = false, redirect = '';
      try {
        const res = await window.ChatApi.getDepartments();
        if (res && res.success && res.data) {
          hasPhysical = !!res.data.hasPhysicalLabAccess;
          hasChemical = !!res.data.hasChemicalLabAccess;
          redirect = res.data.laboratorySectionRedirect || '';
        }
      } catch (err) {
        console.error('[LaboratoryApp] departments lookup failed:', err);
      }

      if (!hasPhysical && !hasChemical) {
        this.renderAccessDenied();
        return;
      }

      this.hasPhysical = hasPhysical;
      this.hasChemical = hasChemical;

      // CEO / IT can access both sections
      if (hasPhysical && hasChemical) {
        if (this.sectionSwitch) this.sectionSwitch.style.display = 'flex';
      } else {
        if (this.sectionSwitch) this.sectionSwitch.style.display = 'none';
      }

      let target = redirect === 'CHEMICAL' ? 'CHEMICAL' : 'PHYSICAL';
      if (subRoute) {
        const s = subRoute.toLowerCase();
        if (s.includes('chemical') && hasChemical) target = 'CHEMICAL';
        else if (s.includes('physical') && hasPhysical) target = 'PHYSICAL';
      }

      this.setSection(target);
      let initialTab = 'raw';
      if (subRoute) {
        const s = subRoute.toLowerCase();
        if (s.includes('final')) initialTab = 'final';
        else if (s.includes('inproc')) initialTab = 'inproc';
        else if (s.includes('hist')) initialTab = 'history';
        else if (s.includes('trace')) initialTab = 'trace';
      }
      this.switchTab(initialTab);
    }

    static renderAccessDenied() {
      if (this.root) {
        this.root.innerHTML = `
          <div class="lab-access-denied" dir="rtl">
            <h3>غير مصرح بالوصول</h3>
            <p>لا تمتلك صلاحيات المعمل الفيزيائي أو المعمل الكيميائي. يرجى التواصل مع إدارة النظم (IT) لتخصيص الصلاحية المناسبة.</p>
            <button type="button" class="lab-btn lab-btn-primary" onclick="window.location.hash='#departments'">العودة للأقسام</button>
          </div>`;
      }
    }

    static setSection(section) {
      this.activeSection = section;
      if (this.btnPhysical) this.btnPhysical.classList.toggle('active', section === 'PHYSICAL');
      if (this.btnChemical) this.btnChemical.classList.toggle('active', section === 'CHEMICAL');

      const titleAr = section === 'CHEMICAL' ? 'تقرير الأختبارات الكيميائية' : 'تقرير الأختبارات الفيزيائية';
      if (this.sectionSubtitle) {
        this.sectionSubtitle.textContent = `${LAB_SECTION_AR[section]} — ${titleAr}`;
      }
      if (this.tabRawTitle) {
        this.tabRawTitle.textContent = titleAr;
      }

      // Re-render form with appropriate tests
      this.renderOfficialDocumentForm();
      if (this.finalFormLoaded) this.renderFinalCertView();
      if (this.inprocFormLoaded) this.renderInProcessView();
      if (this.activeTab === 'history') this.loadHistoryList();
    }

    static switchSection(section) {
      if (section === 'PHYSICAL' && !this.hasPhysical) return;
      if (section === 'CHEMICAL' && !this.hasChemical) return;
      if (!this.confirmLeaveIfDirty()) return;

      this.setSection(section);
      if (window.ChatApp && window.ChatApp.pushStateSilently) {
        window.ChatApp.pushStateSilently(section === 'CHEMICAL' ? '/laboratory/chemical' : '/laboratory/physical');
      }
    }

    // ======================================================================
    // SLIDE 0: OFFICIAL DOCUMENT FORM (تقرير الأختبارات الكيميائية / الفيزيائية)
    // ======================================================================
    static async renderOfficialDocumentForm() {
      if (!this.formContainer) return;
      this.formContainer.innerHTML = '<div class="lab-loading">جاري إعداد نموذج التقرير المعتمد وتحميل رقم العينة...</div>';

      const user = window.ChatApi ? window.ChatApi.getCurrentUser() : null;
      const userName = user ? (user.fullName || user.username || 'مدير المعمل') : 'مدير المعمل';
      const isChemical = this.activeSection === 'CHEMICAL';
      const tests = isChemical ? CHEMICAL_TESTS : PHYSICAL_TESTS;
      const reportTitle = isChemical ? 'تقرير الأختبارات الكيميائية' : 'تقرير الأختبارات الفيزيائية';

      // 1. Fetch sequential sample number for this specific user
      try {
        const nextRes = await window.ChatApi.getLaboratoryNextSampleNumber();
        if (nextRes && nextRes.success && nextRes.data) {
          this.nextSampleNumber = nextRes.data.sampleNumber || nextRes.data.nextSampleNumber || 1;
        }
      } catch (err) {
        console.warn('[LaboratoryApp] next sample number lookup failed:', err);
      }

      // 2. Fetch shared batch references from backend
      try {
        const batchRes = await window.ChatApi.getLaboratoryBatchReferences({ section: this.activeSection });
        if (batchRes && batchRes.success && Array.isArray(batchRes.data)) {
          this.batchReferences = batchRes.data;
        }
      } catch (err) {
        console.warn('[LaboratoryApp] batch references lookup failed:', err);
      }

      const today = new Date().toISOString().slice(0, 10);
      this.currentVStep = 0;
      this.highestVisitedVStep = 0;

      // Render the official document sheet with VERTICAL SLIDER (المنزلق الرأسي الداخلي للنموذج الأول)
      this.formContainer.innerHTML = `
        <div class="lab-document-wrap" dir="rtl">
          <!-- Top Action Bar -->
          <div class="lab-form-action-bar">
            <div class="lab-form-action-title">
              <span class="lab-seq-badge">
                <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.5"><polyline points="20 6 9 17 4 12"/></svg>
                رقم العينة المستحق: #${this.esc(String(this.nextSampleNumber))} (تسلسل تلقائي خاص بك)
              </span>
              <h3>${this.esc(reportTitle)} — مطابقة لمواصفة ASTM C1240</h3>
            </div>
            <div class="lab-form-action-btns">
              <button type="button" class="lab-btn lab-btn-primary" id="lab-btn-submit-form-top">
                <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><path d="M19 21H5a2 2 0 0 1-2-2V5a2 2 0 0 1 2-2h11l5 5v11a2 2 0 0 1-2 2z"/><polyline points="17 21 17 13 7 13 7 21"/><polyline points="7 3 7 8 15 8"/></svg>
                <span>حفظ واعتماد التقرير</span>
              </button>
              <button type="button" class="lab-btn lab-btn-ghost" id="lab-btn-preview-form">
                <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><polyline points="6 9 6 2 18 2 18 9"/><path d="M6 18H4a2 2 0 0 1-2-2v-5a2 2 0 0 1 2-2h16a2 2 0 0 1 2 2v5a2 2 0 0 1-2 2h-2"/><rect x="6" y="14" width="12" height="8"/></svg>
                <span>معاينة للطباعة</span>
              </button>
              <button type="button" class="lab-btn lab-btn-ghost" id="lab-btn-reset-form">إعادة تعيين</button>
            </div>
          </div>

          <div id="lab-form-alert"></div>

          <!-- Vertical Slider Layout Container -->
          <div class="lab-vslider-container">
            <!-- Sidebar: Vertical Stepper & Progress Indicator -->
            <aside class="lab-vslider-sidebar" aria-label="مؤشر مراحل النموذج">
              <div class="lab-vslider-sidebar-title">
                <svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><line x1="12" y1="5" x2="12" y2="19"/><polyline points="19 12 12 19 5 12"/></svg>
                <span>مراحل النموذج (4 أقسام)</span>
              </div>
              <div class="lab-vslider-steps" role="tablist" aria-label="خطوات النموذج الرأسي">
                <button type="button" class="lab-vstep-item active" id="lab-vstep-item-0" data-vstep="0" role="tab" aria-selected="true" aria-controls="lab-vsection-0">
                  <div class="lab-vstep-circle">1</div>
                  <div class="lab-vstep-content">
                    <span class="lab-vstep-title">بيانات العينة والتشغيلة</span>
                    <span class="lab-vstep-desc">المسمى ورقم الدفعة والتاريخ</span>
                  </div>
                </button>
                <button type="button" class="lab-vstep-item" id="lab-vstep-item-1" data-vstep="1" role="tab" aria-selected="false" aria-controls="lab-vsection-1">
                  <div class="lab-vstep-circle">2</div>
                  <div class="lab-vstep-content">
                    <span class="lab-vstep-title">فحوصات ASTM C1240</span>
                    <span class="lab-vstep-desc">نتائج القياسات والحدود</span>
                  </div>
                </button>
                <button type="button" class="lab-vstep-item" id="lab-vstep-item-2" data-vstep="2" role="tab" aria-selected="false" aria-controls="lab-vsection-2">
                  <div class="lab-vstep-circle">3</div>
                  <div class="lab-vstep-content">
                    <span class="lab-vstep-title">الملاحظات وقرار المطابقة</span>
                    <span class="lab-vstep-desc">مطابقة مواصفات الجودة</span>
                  </div>
                </button>
                <button type="button" class="lab-vstep-item" id="lab-vstep-item-3" data-vstep="3" role="tab" aria-selected="false" aria-controls="lab-vsection-3">
                  <div class="lab-vstep-circle">4</div>
                  <div class="lab-vstep-content">
                    <span class="lab-vstep-title">بيانات واعتماد المعمل</span>
                    <span class="lab-vstep-desc">التوقيع واعتماد التقرير</span>
                  </div>
                </button>
              </div>
            </aside>

            <!-- Main Area with Viewport & Controls -->
            <div class="lab-vslider-main">
              <!-- Up / Down Navigation Controls Toolbar -->
              <div class="lab-vslider-toolbar">
                <div class="lab-vslider-nav-btns">
                  <button type="button" class="lab-vslider-btn lab-vslider-btn-up" id="lab-vslider-btn-up" disabled title="القسم السابق / لأعلى (السهم لأعلى)" aria-label="السابق لأعلى">
                    <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.5"><polyline points="18 15 12 9 6 15"/></svg>
                    <span>السابق / لأعلى</span>
                  </button>
                  <button type="button" class="lab-vslider-btn lab-vslider-btn-down" id="lab-vslider-btn-down" title="القسم التالي / لأسفل (السهم لأسفل)" aria-label="التالي لأسفل">
                    <span>التالي / لأسفل</span>
                    <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.5"><polyline points="6 9 12 15 18 9"/></svg>
                  </button>
                </div>
                <div class="lab-vslider-counter-wrap">
                  <span class="lab-vslider-counter-badge" id="lab-vslider-counter">القسم 1 من 4 (Step 1 of 4)</span>
                  <div class="lab-vslider-progress-bar" aria-hidden="true">
                    <div class="lab-vslider-progress-fill" id="lab-vslider-progress-fill" style="width: 25%;"></div>
                  </div>
                </div>
              </div>

              <!-- Vertical Slider Viewport -->
              <div class="lab-vslider-viewport" id="lab-vslider-viewport" tabindex="0" role="region" aria-label="عارض أقسام النموذج الرأسي">
                <div class="lab-vslider-track" id="lab-vslider-track">

                  <!-- SECTION 0: بيانات العينة ورقم التشغيلة -->
                  <section class="lab-vsection" id="lab-vsection-0" data-vstep="0" role="tabpanel" aria-labelledby="lab-vstep-item-0">
                    <div class="lab-vsection-header">
                      <h4>
                        <svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><path d="M14 2H6a2 2 0 0 0-2 2v16a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V8z"/><polyline points="14 2 14 8 20 8"/><line x1="16" y1="13" x2="8" y2="13"/><line x1="16" y1="17" x2="8" y2="17"/></svg>
                        1. بيانات العينة الأساسية ورقم التشغيلة (.Batch no)
                      </h4>
                      <span class="lab-vsection-step-tag">القسم 1 من 4</span>
                    </div>

                    <div class="lab-vsection-body">
                      <!-- Header Banner -->
                      <div class="lab-sheet-header-banner" style="border-radius: 8px; margin-bottom: 16px;">
                        <div class="lab-sheet-header-brand">
                          <span class="lab-sheet-header-brand-name">FACESTONE</span>
                          <span class="lab-sheet-header-brand-sub">full concrete solutions</span>
                        </div>
                        <div class="lab-sheet-header-logo-wrap">
                          <img src="/images/facestone-logo-transparent.png" alt="FaceStone Logo" class="lab-sheet-header-shield">
                        </div>
                      </div>

                      <table class="lab-doc-table">
                        <tbody>
                          <tr>
                            <td style="width: 20%; font-weight: 800; background: #F8FAFC;">مسمى العينة <span class="req">*</span></td>
                            <td style="width: 30%;">
                              <input type="text" class="lab-sheet-input" id="lab-in-sample-name" value="سيليكا باودر (Micro Silica Powder)" required>
                            </td>
                            <td style="width: 20%; font-weight: 800; background: #F8FAFC;">رقم العينة</td>
                            <td style="width: 30%;">
                              <input type="text" class="lab-sheet-input" id="lab-in-sample-no" value="${this.esc(String(this.nextSampleNumber))}" readonly title="رقم تسلسلي تلقائي ومستقل للموظف يتم إنشاؤه وتثبيته في قاعدة البيانات">
                            </td>
                          </tr>
                          <tr>
                            <td style="font-weight: 800; background: #F8FAFC;">وصف العينة <span class="req">*</span></td>
                            <td>
                              <input type="text" class="lab-sheet-input" id="lab-in-sample-desc" value="بودرة رمادية اللون" required>
                            </td>
                            <td style="font-weight: 800; background: #F8FAFC;">رقم الدفعة (.Batch no) <span class="req">*</span></td>
                            <td>
                              <div style="display: flex; gap: 4px;">
                                <select class="lab-sheet-input" id="lab-in-batch-select" style="text-align: right;">
                                  <option value="">-- اختر رقم تشغيلة مشترك --</option>
                                  ${this.batchReferences.map(b => `
                                    <option value="${this.esc(b.batchNumber)}" data-ref="${b.productionReferenceNumber}" data-desc="${this.esc(b.materialDescription || '')}">
                                      #${b.productionReferenceNumber} - ${this.esc(b.batchNumber)} (${this.esc(b.materialDescription || 'خامة')})
                                    </option>
                                  `).join('')}
                                  <option value="__NEW__">+ إدخال رقم دفعة جديد...</option>
                                </select>
                              </div>
                              <input type="text" class="lab-sheet-input" id="lab-in-batch-custom" placeholder="أدخل رقم الدفعة يدوياً (مثال: BATCH-01)" style="margin-top: 4px; display: none;">
                            </td>
                          </tr>
                          <tr>
                            <td style="font-weight: 800; background: #F8FAFC;">تاريخ استلام العينة <span class="req">*</span></td>
                            <td>
                              <input type="date" class="lab-sheet-input" id="lab-in-received-date" value="${today}" required>
                            </td>
                            <td style="font-weight: 800; background: #F8FAFC;">تاريخ إصدار التقرير <span class="req">*</span></td>
                            <td>
                              <input type="date" class="lab-sheet-input" id="lab-in-issue-date" value="${today}" required>
                            </td>
                          </tr>
                        </tbody>
                      </table>
                    </div>

                    <div class="lab-vsection-footer-nav">
                      <div></div>
                      <button type="button" class="lab-btn lab-btn-primary lab-vslider-btn-down-step" id="lab-vsec0-next-btn">
                        <span>الانتقال إلى نتائج التحليل (لأسفل)</span>
                        <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><polyline points="6 9 12 15 18 9"/></svg>
                      </button>
                    </div>
                  </section>

                  <!-- SECTION 1: نتائج التحليل المخبري -->
                  <section class="lab-vsection" id="lab-vsection-1" data-vstep="1" role="tabpanel" aria-labelledby="lab-vstep-item-1">
                    <div class="lab-vsection-header">
                      <h4>
                        <svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><polyline points="22 12 18 12 15 21 9 3 6 12 2 12"/></svg>
                        2. نتائج التحليل المخبري — مطابقة لمواصفة ASTM C1240
                      </h4>
                      <span class="lab-vsection-step-tag">القسم 2 من 4</span>
                    </div>

                    <div class="lab-vsection-body">
                      <table class="lab-doc-table" id="lab-test-results-table">
                        <thead>
                          <tr>
                            <th class="col-idx">م</th>
                            <th class="col-name" style="text-align: center;">الاختبار</th>
                            <th class="col-val">نتيجة الاختبار</th>
                            <th class="col-unit">وحدة القياس</th>
                            <th class="col-min">Min</th>
                            <th class="col-max">Max</th>
                            <th class="col-method">طريقة الاختبار</th>
                          </tr>
                        </thead>
                        <tbody>
                          ${tests.map(t => `
                            <tr data-test-index="${t.index}">
                              <td class="col-idx">${t.index}</td>
                              <td class="col-name">${this.esc(t.name)}</td>
                              <td class="col-val">
                                <input type="${t.isText ? 'text' : 'number'}"
                                       step="any"
                                       class="lab-sheet-input lab-test-input"
                                       id="lab-test-val-${t.index}"
                                       data-index="${t.index}"
                                       data-min="${t.min != null ? t.min : ''}"
                                       data-max="${t.max != null ? t.max : ''}"
                                       data-name="${this.esc(t.name)}"
                                       data-unit="${this.esc(t.unit)}"
                                       value="${this.esc(t.defaultVal)}"
                                       placeholder="أدخل النتيجة">
                              </td>
                              <td class="col-unit">${this.esc(t.unit)}</td>
                              <td class="col-min">${this.esc(t.minLabel)}</td>
                              <td class="col-max">${this.esc(t.maxLabel)}</td>
                              <td class="col-method">
                                <input type="text" class="lab-sheet-input" id="lab-test-method-${t.index}" value="${this.esc(t.method)}">
                              </td>
                            </tr>
                          `).join('')}
                        </tbody>
                      </table>
                    </div>

                    <div class="lab-vsection-footer-nav">
                      <button type="button" class="lab-btn lab-btn-ghost lab-vslider-btn-up-step" id="lab-vsec1-prev-btn">
                        <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><polyline points="18 15 12 9 6 15"/></svg>
                        <span>العودة لبيانات العينة (لأعلى)</span>
                      </button>
                      <button type="button" class="lab-btn lab-btn-primary lab-vslider-btn-down-step" id="lab-vsec1-next-btn">
                        <span>الانتقال لقرار المطابقة (لأسفل)</span>
                        <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><polyline points="6 9 12 15 18 9"/></svg>
                      </button>
                    </div>
                  </section>

                  <!-- SECTION 2: الملاحظات وقرار المطابقة -->
                  <section class="lab-vsection" id="lab-vsection-2" data-vstep="2" role="tabpanel" aria-labelledby="lab-vstep-item-2">
                    <div class="lab-vsection-header">
                      <h4>
                        <svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><path d="M21 15a2 2 0 0 1-2 2H7l-4 4V5a2 2 0 0 1 2-2h14a2 2 0 0 1 2 2z"/></svg>
                        3. الملاحظات الفنية وقرار المطابقة النهائي
                      </h4>
                      <span class="lab-vsection-step-tag">القسم 3 من 4</span>
                    </div>

                    <div class="lab-vsection-body">
                      <!-- Notes -->
                      <div class="lab-sheet-notes-section" style="margin-bottom: 24px;">
                        <div class="lab-sheet-notes-title">الملاحظات الفنية:</div>
                        <textarea class="lab-sheet-notes-textarea" id="lab-in-notes" rows="3">مطابقة لمواصفة ASTM C1240</textarea>
                      </div>

                      <!-- Decision -->
                      <div class="lab-sheet-notes-title" style="margin-bottom: 10px; font-weight: 800;">قرار المطابقة لمواصفات الجودة:</div>
                      <div class="lab-decision-selection">
                        <label class="lab-decision-option comply-tag">
                          <input type="radio" name="lab-doc-comply" id="lab-radio-comply" value="COMPLY" checked>
                          <span>☑ Comply (مطابق لمواصفة ASTM C1240)</span>
                        </label>
                        <label class="lab-decision-option notcomply-tag">
                          <input type="radio" name="lab-doc-comply" id="lab-radio-notcomply" value="NOT_COMPLIED">
                          <span>☒ Not Complied (غير مطابق للمواصفات)</span>
                        </label>
                      </div>
                    </div>

                    <div class="lab-vsection-footer-nav">
                      <button type="button" class="lab-btn lab-btn-ghost lab-vslider-btn-up-step" id="lab-vsec2-prev-btn">
                        <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><polyline points="18 15 12 9 6 15"/></svg>
                        <span>العودة لنتائج التحليل (لأعلى)</span>
                      </button>
                      <button type="button" class="lab-btn lab-btn-primary lab-vslider-btn-down-step" id="lab-vsec2-next-btn">
                        <span>الانتقال للكميات والاعتماد (لأسفل)</span>
                        <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><polyline points="6 9 12 15 18 9"/></svg>
                      </button>
                    </div>
                  </section>

                  <!-- SECTION 3: بيانات واعتماد مدير المعمل -->
                  <section class="lab-vsection" id="lab-vsection-3" data-vstep="3" role="tabpanel" aria-labelledby="lab-vstep-item-3">
                    <div class="lab-vsection-header">
                      <h4>
                        <svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><path d="M21 16V8a2 2 0 0 0-1-1.73l-7-4a2 2 0 0 0-2 0l-7 4A2 2 0 0 0 3 8v8a2 2 0 0 0 1 1.73l7 4a2 2 0 0 0 2 0l7-4A2 2 0 0 0 21 16z"/></svg>
                        4. بيانات واعتماد مدير المعمل
                      </h4>
                      <span class="lab-vsection-step-tag">القسم 4 من 4 (الأخير)</span>
                    </div>

                    <div class="lab-vsection-body">
                      <!-- Supplier / Source info -->
                      <div class="lab-storage-integration-box" style="margin-bottom: 20px;">
                        <div class="lab-field lab-field-full">
                          <label class="lab-label">المورد / المصدر (اختياري)</label>
                          <input type="text" class="lab-input" id="lab-in-supplier" placeholder="اسم المورد أو جهة التوريد">
                        </div>
                      </div>

                      <!-- Lab Manager Table -->
                      <table class="lab-manager-table" style="margin-bottom: 20px;">
                        <thead>
                          <tr>
                            <th colspan="2">مدير المعمل</th>
                          </tr>
                        </thead>
                        <tbody>
                          <tr>
                            <td class="label-cell">الاسم</td>
                            <td>
                              <input type="text" class="lab-sheet-input" id="lab-in-manager-name" value="${this.esc(userName)}" style="text-align: right;">
                            </td>
                          </tr>
                          <tr>
                            <td class="label-cell">التوقيع</td>
                            <td>
                              <input type="text" class="lab-sheet-input" id="lab-in-manager-sig" value="${this.esc(userName)}" style="text-align: right; font-family: monospace;">
                            </td>
                          </tr>
                          <tr>
                            <td class="label-cell">التاريخ</td>
                            <td>
                              <input type="date" class="lab-sheet-input" id="lab-in-manager-date" value="${today}">
                            </td>
                          </tr>
                        </tbody>
                      </table>

                      <!-- Footer banner matching PDF -->
                      <div class="lab-sheet-footer-banner" style="border-radius: 8px;">
                        <div class="lab-sheet-footer-row">
                          <div class="lab-sheet-footer-left">
                            <span>Address: Cairo- new Nozha, Street aladeeb M.Alsebaey</span>
                            <span>Tel: 0226206493</span>
                            <span>Emails: m.daha@facestone-eg.com | hanaa@facestone-eg.com | ranamohamed50066@gmail.com</span>
                          </div>
                          <div class="lab-sheet-footer-right">
                            <span style="font-weight: 800; font-size: 0.85rem;">FACESTONE</span>
                            <span>www.facestone-eg.com</span>
                          </div>
                        </div>
                      </div>
                    </div>

                    <div class="lab-vsection-footer-nav">
                      <button type="button" class="lab-btn lab-btn-ghost lab-vslider-btn-up-step" id="lab-vsec3-prev-btn">
                        <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><polyline points="18 15 12 9 6 15"/></svg>
                        <span>العودة لقرار المطابقة (لأعلى)</span>
                      </button>
                      <button type="button" class="lab-btn lab-btn-primary" id="lab-btn-submit-form" style="padding: 10px 24px; font-size: 0.9rem;">
                        <svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><path d="M19 21H5a2 2 0 0 1-2-2V5a2 2 0 0 1 2-2h11l5 5v11a2 2 0 0 1-2 2z"/><polyline points="17 21 17 13 7 13 7 21"/><polyline points="7 3 7 8 15 8"/></svg>
                        <span>حفظ واعتماد التقرير النهائي</span>
                      </button>
                    </div>
                  </section>

                </div>
              </div>
            </div>
          </div>
        </div>
      `;

      this.bindFormEvents();
    }

    // ======================================================================
    // VERTICAL SLIDER LOGIC (Up / Down Navigation within First Form Only)
    // ======================================================================
    static goToVStep(index) {
      if (index < 0 || index >= this.totalVSteps) return;
      if (index === this.currentVStep) return;

      // If moving downward, validate current section before proceeding
      if (index > this.currentVStep) {
        if (!this.validateVStep(this.currentVStep)) {
          return;
        }
      }

      this.currentVStep = index;
      if (index > this.highestVisitedVStep) {
        this.highestVisitedVStep = index;
      }

      // Vertical translation of the track inside first form only
      const track = document.getElementById('lab-vslider-track');
      if (track) {
        track.style.transform = `translateY(-${index * 100}%)`;
      }

      // Update vertical progress stepper items
      const stepItems = document.querySelectorAll('.lab-vstep-item');
      stepItems.forEach((item) => {
        const idx = parseInt(item.getAttribute('data-vstep'), 10);
        item.classList.toggle('active', idx === index);
        item.classList.toggle('completed', idx < index);
        item.setAttribute('aria-selected', idx === index ? 'true' : 'false');
        const circle = item.querySelector('.lab-vstep-circle');
        if (circle) {
          if (idx < index) {
            circle.innerHTML = '✓';
          } else {
            circle.textContent = String(idx + 1);
          }
        }
      });

      // Update Toolbar Controls
      const btnUp = document.getElementById('lab-vslider-btn-up');
      const btnDown = document.getElementById('lab-vslider-btn-down');
      const counter = document.getElementById('lab-vslider-counter');
      const progressFill = document.getElementById('lab-vslider-progress-fill');

      if (btnUp) btnUp.disabled = (index === 0);
      if (btnDown) btnDown.disabled = (index === this.totalVSteps - 1);

      if (counter) {
        counter.textContent = `القسم ${index + 1} من ${this.totalVSteps} (Step ${index + 1} of ${this.totalVSteps})`;
      }

      if (progressFill) {
        progressFill.style.width = `${((index + 1) / this.totalVSteps) * 100}%`;
      }

      // Scroll active section back to top
      const activeSec = document.getElementById(`lab-vsection-${index}`);
      if (activeSec) {
        activeSec.scrollTop = 0;
      }
    }

    static nextVStep() {
      if (this.currentVStep < this.totalVSteps - 1) {
        this.goToVStep(this.currentVStep + 1);
      }
    }

    static prevVStep() {
      if (this.currentVStep > 0) {
        this.goToVStep(this.currentVStep - 1);
      }
    }

    static validateVStep(stepIndex) {
      const alertBox = document.getElementById('lab-form-alert');
      const val = (id) => (document.getElementById(id)?.value || '').trim();

      // Clear previous error highlights
      document.querySelectorAll('.lab-sheet-input, .lab-input').forEach(el => el.classList.remove('is-invalid'));
      if (alertBox) alertBox.innerHTML = '';

      if (stepIndex === 0) {
        const sampleName = val('lab-in-sample-name');
        const sampleDesc = val('lab-in-sample-desc');
        const batchSelect = document.getElementById('lab-in-batch-select');
        const batchCustom = val('lab-in-batch-custom');
        let batchVal = '';
        if (batchSelect && batchSelect.value === '__NEW__') batchVal = batchCustom;
        else if (batchSelect && batchSelect.value) batchVal = batchSelect.value;
        else batchVal = batchCustom;

        const receivedDate = val('lab-in-received-date');
        const issueDate = val('lab-in-issue-date');

        let err = null;
        let invalidEl = null;

        if (!sampleName) {
          err = 'مسمى العينة مطلوب.';
          invalidEl = document.getElementById('lab-in-sample-name');
        } else if (!sampleDesc) {
          err = 'وصف العينة مطلوب.';
          invalidEl = document.getElementById('lab-in-sample-desc');
        } else if (!batchVal) {
          err = 'يرجى اختيار أو إدخال رقم الدفعة (.Batch no).';
          invalidEl = (batchSelect && batchSelect.value === '__NEW__') ? document.getElementById('lab-in-batch-custom') : batchSelect;
        } else if (!receivedDate) {
          err = 'تاريخ استلام العينة مطلوب.';
          invalidEl = document.getElementById('lab-in-received-date');
        } else if (!issueDate) {
          err = 'تاريخ إصدار التقرير مطلوب.';
          invalidEl = document.getElementById('lab-in-issue-date');
        }

        if (err) {
          if (invalidEl) {
            invalidEl.classList.add('is-invalid');
            invalidEl.focus();
          }
          if (alertBox) {
            alertBox.innerHTML = `<div class="lab-alert lab-alert-error">${this.esc(err)}</div>`;
            alertBox.scrollIntoView({ behavior: 'smooth', block: 'nearest' });
          }
          return false;
        }
      } else if (stepIndex === 3) {
        // Weight-related validation removed: users can enter weight values freely without blocking step navigation
        return true;
      }

      return true;
    }

    static bindFormEvents() {
      // Vertical slider toolbar buttons
      document.getElementById('lab-vslider-btn-up')?.addEventListener('click', () => this.prevVStep());
      document.getElementById('lab-vslider-btn-down')?.addEventListener('click', () => this.nextVStep());

      // In-section next/prev buttons
      document.querySelectorAll('.lab-vslider-btn-down-step').forEach(btn => {
        btn.addEventListener('click', () => this.nextVStep());
      });
      document.querySelectorAll('.lab-vslider-btn-up-step').forEach(btn => {
        btn.addEventListener('click', () => this.prevVStep());
      });

      // Stepper sidebar click
      document.querySelectorAll('.lab-vstep-item').forEach(item => {
        item.addEventListener('click', () => {
          const idx = parseInt(item.getAttribute('data-vstep'), 10);
          if (!isNaN(idx)) this.goToVStep(idx);
        });
      });

      // Keyboard & Vertical Swipe on vertical viewport
      const vViewport = document.getElementById('lab-vslider-viewport');
      if (vViewport) {
        let vStartY = 0, vStartX = 0, vDragging = false;
        const onVStart = (e) => {
          if (['INPUT', 'TEXTAREA', 'SELECT', 'BUTTON'].includes(e.target.tagName)) return;
          const touch = (e.touches && e.touches.length > 0) ? e.touches[0] : ((e.changedTouches && e.changedTouches.length > 0) ? e.changedTouches[0] : e);
          vStartY = touch.clientY || 0;
          vStartX = touch.clientX || 0;
          vDragging = true;
        };

        const onVEnd = (e) => {
          if (!vDragging) return;
          vDragging = false;
          const touch = (e.changedTouches && e.changedTouches.length > 0) ? e.changedTouches[0] : ((e.touches && e.touches.length > 0) ? e.touches[0] : e);
          const endY = touch.clientY || 0;
          const endX = touch.clientX || 0;
          const deltaY = endY - vStartY;
          const deltaX = endX - vStartX;

          // Predominantly vertical swipe of at least 40px
          if (Math.abs(deltaY) > 40 && Math.abs(deltaY) > Math.abs(deltaX) * 1.2) {
            if (deltaY < 0) {
              this.nextVStep();
            } else {
              this.prevVStep();
            }
          }
        };

        vViewport.addEventListener('touchstart', onVStart, { passive: true });
        vViewport.addEventListener('touchend', onVEnd, { passive: true });
        vViewport.addEventListener('mousedown', onVStart);
        vViewport.addEventListener('mouseup', onVEnd);

        vViewport.addEventListener('keydown', (e) => {
          if (['INPUT', 'TEXTAREA', 'SELECT'].includes(e.target.tagName)) return;
          if (e.key === 'ArrowDown') {
            e.preventDefault();
            this.nextVStep();
          } else if (e.key === 'ArrowUp') {
            e.preventDefault();
            this.prevVStep();
          }
        });
      }

      // Batch select behavior
      const batchSelect = document.getElementById('lab-in-batch-select');
      const batchCustom = document.getElementById('lab-in-batch-custom');
      const sampleNameInput = document.getElementById('lab-in-sample-name');

      batchSelect?.addEventListener('change', () => {
        if (batchSelect.value === '__NEW__') {
          if (batchCustom) {
            batchCustom.style.display = 'block';
            batchCustom.focus();
          }
        } else {
          if (batchCustom) batchCustom.style.display = 'none';
          const opt = batchSelect.selectedOptions[0];
          if (opt && opt.getAttribute('data-desc')) {
            const desc = opt.getAttribute('data-desc');
            if (desc && desc !== 'خامة') sampleNameInput.value = desc;
          }
        }
      });

      // Live test evaluation for ASTM C1240 compliance
      const testInputs = document.querySelectorAll('.lab-test-input');
      testInputs.forEach(input => {
        input.addEventListener('input', () => this.evaluateCompliance());
      });

      // Submit, Preview, Reset buttons
      document.getElementById('lab-btn-submit-form')?.addEventListener('click', () => this.submitOfficialForm());
      document.getElementById('lab-btn-submit-form-top')?.addEventListener('click', () => this.submitOfficialForm());
      document.getElementById('lab-btn-preview-form')?.addEventListener('click', () => this.previewCurrentForm());
      document.getElementById('lab-btn-reset-form')?.addEventListener('click', () => this.renderOfficialDocumentForm());
    }

    static evaluateCompliance() {
      const testInputs = document.querySelectorAll('.lab-test-input');
      let anyViolation = false;

      testInputs.forEach(input => {
        const valStr = input.value.trim();
        if (!valStr || input.type === 'text') {
          input.classList.remove('is-violated', 'is-valid');
          return;
        }

        const val = parseFloat(valStr);
        if (isNaN(val)) return;

        const minStr = input.getAttribute('data-min');
        const maxStr = input.getAttribute('data-max');
        const min = minStr !== '' ? parseFloat(minStr) : null;
        const max = maxStr !== '' ? parseFloat(maxStr) : null;

        let violated = false;
        if (min !== null && val < min) violated = true;
        if (max !== null && val > max) violated = true;

        if (violated) {
          input.classList.add('is-violated');
          input.classList.remove('is-valid');
          anyViolation = true;
        } else if (min !== null || max !== null) {
          input.classList.add('is-valid');
          input.classList.remove('is-violated');
        }
      });

      const radioComply = document.getElementById('lab-radio-comply');
      const radioNotComply = document.getElementById('lab-radio-notcomply');

      if (anyViolation) {
        if (radioNotComply) radioNotComply.checked = true;
      } else {
        if (radioComply) radioComply.checked = true;
      }
    }

    static async submitOfficialForm() {
      const alertBox = document.getElementById('lab-form-alert');
      const submitBtn = document.getElementById('lab-btn-submit-form');
      const val = (id) => (document.getElementById(id)?.value || '').trim();

      const sampleName = val('lab-in-sample-name');
      const sampleDesc = val('lab-in-sample-desc');
      const batchSelect = document.getElementById('lab-in-batch-select');
      const batchCustom = val('lab-in-batch-custom');

      let batchNumber = '';
      let productionRef = null;

      if (batchSelect && batchSelect.value === '__NEW__') {
        batchNumber = batchCustom;
      } else if (batchSelect && batchSelect.value) {
        batchNumber = batchSelect.value;
        const opt = batchSelect.selectedOptions[0];
        if (opt) productionRef = opt.getAttribute('data-ref');
      } else if (batchCustom) {
        batchNumber = batchCustom;
      }

      const receivedDate = val('lab-in-received-date');
      const issueDate = val('lab-in-issue-date');
      const managerName = val('lab-in-manager-name');
      const managerSig = val('lab-in-manager-sig');
      const managerDate = val('lab-in-manager-date');
      const notes = val('lab-in-notes');
      const complyVal = document.querySelector('input[name="lab-doc-comply"]:checked')?.value || 'COMPLY';

      const supplier = val('lab-in-supplier');

      let error = null;
      if (!sampleName) error = 'مسمى العينة مطلوب.';
      else if (!sampleDesc) error = 'وصف العينة مطلوب.';
      else if (!batchNumber) error = 'يرجى اختيار أو إدخال رقم الدفعة (.Batch no).';
      else if (!receivedDate) error = 'تاريخ استلام العينة مطلوب.';
      else if (!issueDate) error = 'تاريخ إصدار التقرير مطلوب.';

      if (error) {
        document.querySelectorAll('.lab-sheet-input, .lab-input').forEach(el => el.classList.remove('is-invalid'));
        if (!sampleName) {
          this.goToVStep(0);
          document.getElementById('lab-in-sample-name')?.classList.add('is-invalid');
        } else if (!sampleDesc) {
          this.goToVStep(0);
          document.getElementById('lab-in-sample-desc')?.classList.add('is-invalid');
        } else if (!batchNumber) {
          this.goToVStep(0);
          document.getElementById('lab-in-batch-select')?.classList.add('is-invalid');
        } else if (!receivedDate) {
          this.goToVStep(0);
          document.getElementById('lab-in-received-date')?.classList.add('is-invalid');
        } else if (!issueDate) {
          this.goToVStep(0);
          document.getElementById('lab-in-issue-date')?.classList.add('is-invalid');
        }

        if (alertBox) {
          alertBox.innerHTML = `<div class="lab-alert lab-alert-error">${this.esc(error)}</div>`;
          alertBox.scrollIntoView({ behavior: 'smooth', block: 'center' });
        }
        return;
      }

      // Collect test results array
      const testInputs = document.querySelectorAll('.lab-test-input');
      const testResults = [];
      testInputs.forEach(inp => {
        const idx = inp.getAttribute('data-index');
        const name = inp.getAttribute('data-name');
        const unit = inp.getAttribute('data-unit');
        const method = val(`lab-test-method-${idx}`);
        testResults.push({
          index: parseInt(idx, 10),
          testName: name,
          result: inp.value.trim(),
          unit: unit,
          method: method
        });
      });

      const fd = new FormData();
      fd.append('laboratorySection', this.activeSection);
      fd.append('sampleName', sampleName);
      fd.append('sampleDescription', sampleDesc);
      fd.append('batchNumber', batchNumber);
      if (productionRef) {
        fd.append('productionReferenceNumber', String(productionRef));
      }
      fd.append('sampleReceivedDate', receivedDate ? new Date(receivedDate).toISOString() : new Date().toISOString());
      fd.append('reportIssueDate', issueDate ? new Date(issueDate).toISOString() : new Date().toISOString());
      fd.append('labManagerName', managerName);
      fd.append('labManagerSignature', managerSig);
      fd.append('labManagerDate', managerDate ? new Date(managerDate).toISOString() : new Date().toISOString());
      fd.append('complyStatus', complyVal);
      fd.append('analysisResultsJson', JSON.stringify(testResults));
      fd.append('notes', notes);
      fd.append('inspectionDecision', complyVal === 'COMPLY' ? 'ACCEPTED' : 'REJECTED');
      fd.append('supplierName', supplier);
      fd.append('rawMaterialDescription', sampleName);
      fd.append('analysisDateTime', new Date().toISOString());

      submitBtn.disabled = true;
      submitBtn.textContent = 'جاري الحفظ والتثبيت...';

      try {
        const res = await window.ChatApi.submitLaboratoryRawMaterialReport(fd);
        if (res && res.success) {
          const report = res.data;
          const assignedSampleNo = report ? report.sampleNumber : this.nextSampleNumber;
          const assignedRef = report ? report.productionReferenceNumber : '-';

          this.isDirty = false;

          alertBox.innerHTML = `
            <div class="lab-alert lab-alert-success" style="display:flex; justify-content:space-between; align-items:center; flex-wrap:wrap; gap:8px;">
              <div>
                <b>✓ تم حفظ واعتماد التقرير بنجاح!</b>
                <div>رقم العينة المثبت للموظف: <b>#${this.esc(String(assignedSampleNo))}</b> | رقم المرجع المشترك: <b>#${this.esc(String(assignedRef))}</b> | الحالة: <b>${complyVal === 'COMPLY' ? 'Comply (مطابق)' : 'Not Complied (غير مطابق)'}</b></div>
              </div>
              <div style="display: flex; gap: 6px;">
                <button type="button" class="lab-btn lab-btn-primary" id="lab-btn-print-just-saved" style="padding: 6px 14px; font-size: 0.78rem;">
                  معاينة وطباعة التقرير الرسمي
                </button>
                <button type="button" class="lab-btn lab-btn-ghost" id="lab-btn-goto-history" style="padding: 6px 14px; font-size: 0.78rem;">
                  الانتقال لسجل التقارير &larr;
                </button>
              </div>
            </div>
          `;

          document.getElementById('lab-btn-print-just-saved')?.addEventListener('click', () => {
            this.openPrintModal(report);
          });
          document.getElementById('lab-btn-goto-history')?.addEventListener('click', () => {
            this.switchTab('history');
          });

          // Refresh sample number for next entry
          const nextRes = await window.ChatApi.getLaboratoryNextSampleNumber();
          if (nextRes && nextRes.success && nextRes.data) {
            this.nextSampleNumber = nextRes.data.sampleNumber || nextRes.data.nextSampleNumber || (assignedSampleNo + 1);
            const sampleNoInp = document.getElementById('lab-in-sample-no');
            if (sampleNoInp) sampleNoInp.value = String(this.nextSampleNumber);
          }

          // Pre-load history
          this.loadHistoryList();
        } else {
          alertBox.innerHTML = `<div class="lab-alert lab-alert-error">${this.esc((res && res.message) || 'فشل حفظ تقرير الفحص.')}</div>`;
        }
      } catch (err) {
        alertBox.innerHTML = `<div class="lab-alert lab-alert-error">تعذر الاتصال بالخادم: ${this.esc(err.message || '')}</div>`;
      } finally {
        submitBtn.disabled = false;
        submitBtn.textContent = 'حفظ واعتماد التقرير';
        alertBox.scrollIntoView({ behavior: 'smooth', block: 'center' });
      }
    }

    static previewCurrentForm() {
      const val = (id) => (document.getElementById(id)?.value || '').trim();
      const isChemical = this.activeSection === 'CHEMICAL';
      const tests = isChemical ? CHEMICAL_TESTS : PHYSICAL_TESTS;

      const testInputs = document.querySelectorAll('.lab-test-input');
      const testResults = [];
      testInputs.forEach(inp => {
        const idx = inp.getAttribute('data-index');
        const name = inp.getAttribute('data-name');
        const unit = inp.getAttribute('data-unit');
        const method = val(`lab-test-method-${idx}`);
        testResults.push({
          index: parseInt(idx, 10),
          testName: name,
          result: inp.value.trim(),
          unit: unit,
          method: method
        });
      });

      const batchSelect = document.getElementById('lab-in-batch-select');
      const batchCustom = val('lab-in-batch-custom');
      let batchNumber = batchSelect?.value === '__NEW__' ? batchCustom : (batchSelect?.value || batchCustom || 'BATCH-001');

      const dummyReport = {
        sampleNumber: this.nextSampleNumber,
        batchNumber: batchNumber,
        sampleName: val('lab-in-sample-name') || 'سيليكا باودر (Micro Silica Powder)',
        sampleDescription: val('lab-in-sample-desc') || 'بودرة رمادية اللون',
        sampleReceivedDate: val('lab-in-received-date') || new Date().toISOString(),
        reportIssueDate: val('lab-in-issue-date') || new Date().toISOString(),
        laboratorySection: this.activeSection,
        complyStatus: document.querySelector('input[name="lab-doc-comply"]:checked')?.value || 'COMPLY',
        notes: val('lab-in-notes') || 'مطابقة لمواصفة ASTM C1240',
        labManagerName: val('lab-in-manager-name') || 'مدير المعمل',
        labManagerSignature: val('lab-in-manager-sig') || 'مدير المعمل',
        labManagerDate: val('lab-in-manager-date') || new Date().toISOString(),
        analysisResultsJson: JSON.stringify(testResults)
      };

      this.openPrintModal(dummyReport);
    }

    // ======================================================================
    // OFFICIAL PRINT / PREVIEW MODAL
    // ======================================================================
    static openPrintModal(report) {
      if (!this.reportModal || !this.modalSheetContent) return;

      const isChemical = (report.laboratorySection || this.activeSection) === 'CHEMICAL';
      const titleAr = isChemical ? 'تقرير الأختبارات الكيميائية' : 'تقرير الأختبارات الفيزيائية';
      const tests = isChemical ? CHEMICAL_TESTS : PHYSICAL_TESTS;

      let parsedResults = [];
      try {
        if (report.analysisResultsJson) {
          parsedResults = JSON.parse(report.analysisResultsJson);
        }
      } catch (e) {
        console.warn('Failed to parse analysis results JSON:', e);
      }

      const rows = tests.map(t => {
        const found = Array.isArray(parsedResults) ? parsedResults.find(p => p.index === t.index) : null;
        const resVal = found ? found.result : (t.defaultVal || '-');
        const methodVal = found ? found.method : t.method;
        return {
          ...t,
          result: resVal,
          method: methodVal
        };
      });

      const rDate = report.sampleReceivedDate ? report.sampleReceivedDate.slice(0, 10) : '';
      const iDate = report.reportIssueDate ? report.reportIssueDate.slice(0, 10) : '';
      const mDate = report.labManagerDate ? report.labManagerDate.slice(0, 10) : '';
      const isComply = (report.complyStatus || 'COMPLY') === 'COMPLY';

      this.modalSheetContent.innerHTML = `
        <div class="lab-report-sheet" style="box-shadow: none; border: 1px solid #334155;">
          <!-- Header Banner -->
          <div class="lab-sheet-header-banner">
            <div class="lab-sheet-header-brand">
              <span class="lab-sheet-header-brand-name">FACESTONE</span>
              <span class="lab-sheet-header-brand-sub">full concrete solutions</span>
            </div>
            <div class="lab-sheet-header-logo-wrap">
              <img src="/images/facestone-logo-transparent.png" alt="FaceStone Logo" class="lab-sheet-header-shield">
            </div>
          </div>

          <!-- Body -->
          <div class="lab-sheet-body">
            <h2 class="lab-sheet-title-heading">${this.esc(titleAr)}</h2>

            <!-- 1. بيانات العينة -->
            <h3 class="lab-sheet-sec-heading">بيانات العينة</h3>
            <table class="lab-doc-table">
              <tbody>
                <tr>
                  <td style="width: 18%; font-weight: 800; background: #F8FAFC;">مسمى العينة</td>
                  <td style="width: 32%;">${this.esc(report.sampleName || 'سيليكا باودر (Micro Silica Powder)')}</td>
                  <td style="width: 18%; font-weight: 800; background: #F8FAFC;">رقم العينة</td>
                  <td style="width: 32%; font-weight: 800; color: #0F2A5C;">${this.esc(String(report.sampleNumber || '-'))}</td>
                </tr>
                <tr>
                  <td style="font-weight: 800; background: #F8FAFC;">وصف العينة</td>
                  <td>${this.esc(report.sampleDescription || 'بودرة رمادية اللون')}</td>
                  <td style="font-weight: 800; background: #F8FAFC;">رقم الدفعة (.Batch no)</td>
                  <td style="font-weight: 700;">${this.esc(report.batchNumber || '-')}</td>
                </tr>
                <tr>
                  <td style="font-weight: 800; background: #F8FAFC;">تاريخ أستالم العينة</td>
                  <td>${this.esc(rDate)}</td>
                  <td style="font-weight: 800; background: #F8FAFC;">تاريخ إصدار التقرير</td>
                  <td>${this.esc(iDate)}</td>
                </tr>
              </tbody>
            </table>

            <!-- 2. نتائج التحليل -->
            <h3 class="lab-sheet-sec-heading">نتائج التحليل</h3>
            <table class="lab-doc-table">
              <thead>
                <tr>
                  <th class="col-idx">م</th>
                  <th class="col-name" style="text-align: center;">الاختبار</th>
                  <th class="col-val">نتيجة الاختبار</th>
                  <th class="col-unit">وحدة القياس</th>
                  <th class="col-min">Min</th>
                  <th class="col-max">Max</th>
                  <th class="col-method">طريقة الاختبار</th>
                </tr>
              </thead>
              <tbody>
                ${rows.map(t => `
                  <tr>
                    <td class="col-idx">${t.index}</td>
                    <td class="col-name">${this.esc(t.name)}</td>
                    <td class="col-val" style="font-weight: 800; font-size: 0.88rem;">${this.esc(String(t.result))}</td>
                    <td class="col-unit">${this.esc(t.unit)}</td>
                    <td class="col-min">${this.esc(t.minLabel)}</td>
                    <td class="col-max">${this.esc(t.maxLabel)}</td>
                    <td class="col-method">${this.esc(t.method || 'ASTM C1240')}</td>
                  </tr>
                `).join('')}
              </tbody>
            </table>

            <!-- 3. الملاحظات -->
            <div class="lab-sheet-notes-section">
              <div class="lab-sheet-notes-title">الملاحظات:</div>
              <div style="font-size: 0.95rem; font-weight: 800; padding: 4px 6px;">
                ${this.esc(report.notes || 'مطابقة لمواصفة ASTM C1240')}
              </div>
            </div>

            <!-- 4. مدير المعمل -->
            <table class="lab-manager-table">
              <thead>
                <tr>
                  <th colspan="2">مدير المعمل</th>
                </tr>
              </thead>
              <tbody>
                <tr>
                  <td class="label-cell">الاسم</td>
                  <td style="font-weight: 700;">${this.esc(report.labManagerName || 'مدير المعمل')}</td>
                </tr>
                <tr>
                  <td class="label-cell">التوقيع</td>
                  <td style="font-family: monospace; font-size: 0.95rem;">${this.esc(report.labManagerSignature || 'مدير المعمل')}</td>
                </tr>
                <tr>
                  <td class="label-cell">التاريخ</td>
                  <td>${this.esc(mDate)}</td>
                </tr>
              </tbody>
            </table>

            <!-- 5. المطابقة Checkboxes -->
            <div class="lab-decision-selection">
              <div class="lab-decision-option comply-tag">
                <span style="font-size: 1.15rem;">${isComply ? '☑' : '☐'}</span>
                <span>Comply</span>
              </div>
              <div class="lab-decision-option notcomply-tag">
                <span style="font-size: 1.15rem;">${!isComply ? '☑' : '☐'}</span>
                <span>Not Complied</span>
              </div>
            </div>
          </div>

          <!-- Footer Banner -->
          <div class="lab-sheet-footer-banner">
            <div class="lab-sheet-footer-row">
              <div class="lab-sheet-footer-left">
                <span>Address: Cairo- new Nozha, Street aladeeb M.Alsebaey</span>
                <span>Tel: 0226206493</span>
                <span>Emails: m.daha@facestone-eg.com | hanaa@facestone-eg.com | ranamohamed50066@gmail.com</span>
              </div>
              <div class="lab-sheet-footer-right">
                <span style="font-weight: 800; font-size: 0.85rem;">FACESTONE</span>
                <span>www.facestone-eg.com</span>
              </div>
            </div>
          </div>
        </div>
      `;

      this.reportModal.style.display = 'flex';
    }

    static closePrintModal() {
      if (this.reportModal) this.reportModal.style.display = 'none';
    }

    // ======================================================================
    // SLIDE 1: FINAL PRODUCT CONFORMITY CERTIFICATE
    // ======================================================================
    static async renderFinalCertView() {
      if (!this.finalContainer) return;
      const sectionAr = LAB_SECTION_AR[this.activeSection];
      this.finalContainer.innerHTML = `
        <div id="lab-final-alert"></div>
        <div class="lab-form-card" style="max-width: 900px; margin: 0 auto;">
          <h3 style="margin:0 0 6px;color:#0F2A5C;font-size:0.98rem;font-weight:800;">إصدار شهادة مطابقة منتج نهائي — ${this.esc(sectionAr)}</h3>
          <p class="lab-hint" style="margin:0 0 16px;">اختر الرقم المرجعي التتبعي للإنتاج لإصدار شهادة المطابقة للمنتج النهائي.</p>
          <form id="lab-final-form" novalidate>
            <div class="lab-form-grid">
              <div class="lab-field lab-field-full">
                <label class="lab-label">الرقم المرجعي التتبعي للإنتاج <span class="req">*</span></label>
                <select class="lab-select" id="lab-final-ref" required>
                  <option value="">— اختر رقماً مرجعياً للإنتاج —</option>
                </select>
              </div>
            </div>
            <div class="lab-form-grid" style="margin-top:12px;">
              <div class="lab-field">
                <label class="lab-label">اسم المنتج النهائي <span class="req">*</span></label>
                <input type="text" class="lab-input" id="lab-final-name" placeholder="مثال: أحجار ديكور فاخرة 40×40" required>
              </div>
              <div class="lab-field">
                <label class="lab-label">قرار المطابقة للمواصفات <span class="req">*</span></label>
                <select class="lab-select" id="lab-final-decision" required>
                  <option value="CONFORMS">مطابق للمواصفات القياسية</option>
                  <option value="NON_CONFORMING">غير مطابق للمواصفات</option>
                  <option value="PENDING_TESTING">يحتاج فحوصات إضافية</option>
                </select>
              </div>
              <div class="lab-field lab-field-full">
                <label class="lab-label">ملاحظات الفاحص والتوصيات</label>
                <textarea class="lab-textarea" id="lab-final-notes" placeholder="ملاحظات وتوصيات المعمل النهائي..."></textarea>
              </div>
            </div>
            <div class="lab-form-actions">
              <button type="submit" class="lab-btn lab-btn-primary" id="lab-final-submit">إصدار واعتماد شهادة المطابقة</button>
            </div>
          </form>
        </div>
      `;

      // Populate reference options
      try {
        const rawRes = await window.ChatApi.getLaboratoryRawMaterialReports({ section: this.activeSection });
        if (rawRes && rawRes.success && Array.isArray(rawRes.data)) {
          const sel = document.getElementById('lab-final-ref');
          if (sel) {
            sel.innerHTML = '<option value="">— اختر رقماً مرجعياً للإنتاج —</option>' +
              rawRes.data.map(r => `<option value="${r.productionReferenceNumber}">#${r.productionReferenceNumber} - ${this.esc(r.batchNumber || r.rawMaterialDescription)}</option>`).join('');
          }
        }
      } catch (e) {
        console.warn('Failed to load raw refs for final cert:', e);
      }

      document.getElementById('lab-final-form')?.addEventListener('submit', async (e) => {
        e.preventDefault();
        const alertBox = document.getElementById('lab-final-alert');
        const refNo = parseInt(document.getElementById('lab-final-ref')?.value, 10);
        const name = document.getElementById('lab-final-name')?.value?.trim();
        const dec = document.getElementById('lab-final-decision')?.value;
        const notes = document.getElementById('lab-final-notes')?.value?.trim();

        if (!refNo || !name) {
          alertBox.innerHTML = '<div class="lab-alert lab-alert-error">يرجى استكمال الحقول الإلزامية واختيار الرقم المرجعي.</div>';
          return;
        }

        const fd = new FormData();
        fd.append('productionReferenceNumber', String(refNo));
        fd.append('finalProductName', name);
        fd.append('conformityDecision', dec);
        fd.append('notes', notes);
        fd.append('inspectionDateTime', new Date().toISOString());

        try {
          const res = await window.ChatApi.submitLaboratoryFinalCertificate(fd);
          if (res && res.success) {
            this.isDirty = false;
            alertBox.innerHTML = `<div class="lab-alert lab-alert-success">تم إصدار شهادة المطابقة بنجاح — رقم الشهادة: <b>${this.esc(res.data ? res.data.certificateNumber : '')}</b></div>`;
            document.getElementById('lab-final-form').reset();
          } else {
            alertBox.innerHTML = `<div class="lab-alert lab-alert-error">${this.esc((res && res.message) || 'فشل إصدار الشهادة.')}</div>`;
          }
        } catch (err) {
          alertBox.innerHTML = `<div class="lab-alert lab-alert-error">خطأ: ${this.esc(err.message || '')}</div>`;
        }
      });
    }

    // ======================================================================
    // SLIDE 2: IN-PROCESS QC TESTING
    // ======================================================================
    static async renderInProcessView() {
      if (!this.inprocContainer) return;
      const sectionAr = LAB_SECTION_AR[this.activeSection];
      this.inprocContainer.innerHTML = `
        <div id="lab-inproc-alert"></div>
        <div class="lab-form-card" style="max-width: 900px; margin: 0 auto;">
          <h3 style="margin:0 0 6px;color:#0F2A5C;font-size:0.98rem;font-weight:800;">تسجيل فحص أثناء التشغيل (In-Process QC) — ${this.esc(sectionAr)}</h3>
          <form id="lab-inproc-form" novalidate>
            <div class="lab-form-grid">
              <div class="lab-field">
                <label class="lab-label">الرقم المرجعي التتبعي للإنتاج <span class="req">*</span></label>
                <select class="lab-select" id="lab-inproc-ref" required>
                  <option value="">— اختر رقماً مرجعياً —</option>
                </select>
              </div>
              <div class="lab-field">
                <label class="lab-label">اسم المنتج / الخامة قيد التشغيل <span class="req">*</span></label>
                <input type="text" class="lab-input" id="lab-inproc-name" placeholder="مثال: خليط الخرسانة للصب" required>
              </div>
              <div class="lab-field">
                <label class="lab-label">مرحلة الإنتاج <span class="req">*</span></label>
                <select class="lab-select" id="lab-inproc-stage" required>
                  <option value="الخلط والتجهيز">الخلط والتجهيز</option>
                  <option value="الصب والكبس">الصب والكبس</option>
                  <option value="المعالجة الأولية">المعالجة الأولية</option>
                  <option value="التشطيب والتلوين">التشطيب والتلوين</option>
                  <option value="الفحص النهائي قبل التعبئة">الفحص النهائي قبل التعبئة</option>
                </select>
              </div>
              <div class="lab-field">
                <label class="lab-label">قرار الفحص <span class="req">*</span></label>
                <select class="lab-select" id="lab-inproc-decision" required>
                  <option value="MEETS_REQUIREMENTS">مطابق للمتطلبات الحالية</option>
                  <option value="DOES_NOT_MEET">غير مطابق للمتطلبات</option>
                  <option value="REQUIRES_TESTING">يحتاج فحوصات إضافية</option>
                </select>
              </div>
              <div class="lab-field lab-field-full">
                <label class="lab-label">نتائج الفحص والتحليل <span class="req">*</span></label>
                <textarea class="lab-textarea" id="lab-inproc-results" placeholder="سجل نتائج قياسات ومؤشرات مرحلة التشغيل..." required></textarea>
              </div>
            </div>
            <div class="lab-form-actions">
              <button type="submit" class="lab-btn lab-btn-primary" id="lab-inproc-submit">حفظ فحص التشغيل</button>
            </div>
          </form>
        </div>
      `;

      try {
        const rawRes = await window.ChatApi.getLaboratoryRawMaterialReports({ section: this.activeSection });
        if (rawRes && rawRes.success && Array.isArray(rawRes.data)) {
          const sel = document.getElementById('lab-inproc-ref');
          if (sel) {
            sel.innerHTML = '<option value="">— اختر رقماً مرجعياً —</option>' +
              rawRes.data.map(r => `<option value="${r.productionReferenceNumber}">#${r.productionReferenceNumber} - ${this.esc(r.batchNumber || r.rawMaterialDescription)}</option>`).join('');
          }
        }
      } catch (e) {
        console.warn('Failed to load raw refs for inproc:', e);
      }

      document.getElementById('lab-inproc-form')?.addEventListener('submit', async (e) => {
        e.preventDefault();
        const alertBox = document.getElementById('lab-inproc-alert');
        const refNo = parseInt(document.getElementById('lab-inproc-ref')?.value, 10);
        const name = document.getElementById('lab-inproc-name')?.value?.trim();
        const stage = document.getElementById('lab-inproc-stage')?.value;
        const dec = document.getElementById('lab-inproc-decision')?.value;
        const results = document.getElementById('lab-inproc-results')?.value?.trim();

        if (!refNo || !name || !results) {
          alertBox.innerHTML = '<div class="lab-alert lab-alert-error">يرجى استكمال الحقول الإلزامية.</div>';
          return;
        }

        const fd = new FormData();
        fd.append('productionReferenceNumber', String(refNo));
        fd.append('productName', name);
        fd.append('productionStage', stage);
        fd.append('inspectionDecision', dec);
        fd.append('analysisResultsJson', results);
        fd.append('testDateTime', new Date().toISOString());

        try {
          const res = await window.ChatApi.submitLaboratoryInProcessReport(fd);
          if (res && res.success) {
            this.isDirty = false;
            alertBox.innerHTML = '<div class="lab-alert lab-alert-success">تم حفظ فحص التشغيل بنجاح!</div>';
            document.getElementById('lab-inproc-form').reset();
          } else {
            alertBox.innerHTML = `<div class="lab-alert lab-alert-error">${this.esc((res && res.message) || 'فشل حفظ الفحص.')}</div>`;
          }
        } catch (err) {
          alertBox.innerHTML = `<div class="lab-alert lab-alert-error">خطأ: ${this.esc(err.message || '')}</div>`;
        }
      });
    }

    // ======================================================================
    // SLIDE 3: REPORTS HISTORY
    // ======================================================================
    static async loadHistoryList() {
      if (!this.historyTbody) return;
      this.historyTbody.innerHTML = '<tr><td colspan="10" class="lab-loading">جاري تحميل التقارير والفحوصات...</td></tr>';

      try {
        const params = {};
        const sec = this.histSectionFilter?.value;
        const comp = this.histComplyFilter?.value;
        const q = this.histSearchInput?.value?.trim();

        if (sec && sec !== 'ALL') params.section = sec;
        else params.section = this.activeSection;

        if (comp && comp !== 'ALL') params.complyStatus = comp;
        if (q) params.search = q;

        const res = await window.ChatApi.getLaboratoryRawMaterialReports(params);
        if (!res || !res.success || !Array.isArray(res.data)) {
          this.historyTbody.innerHTML = `<tr><td colspan="10" class="lab-empty">${this.esc((res && res.message) || 'تعذر تحميل السجلات.')}</td></tr>`;
          return;
        }

        this.cachedReports = res.data;
        if (this.cachedReports.length === 0) {
          this.historyTbody.innerHTML = '<tr><td colspan="10" class="lab-empty">لا توجد تقارير فحص مسجلة حتى الآن.</td></tr>';
          return;
        }

        this.historyTbody.innerHTML = this.cachedReports.map(r => {
          const isComp = (r.complyStatus || 'COMPLY') === 'COMPLY';
          const complyBadge = isComp
            ? '<span class="lab-badge lab-badge-approved">Comply (مطابق)</span>'
            : '<span class="lab-badge lab-badge-rejected">Not Complied (غير مطابق)</span>';
          const secBadge = r.laboratorySection === 'CHEMICAL'
            ? '<span class="lab-badge lab-badge-chemical">كيميائي</span>'
            : '<span class="lab-badge lab-badge-physical">فيزيائي</span>';
          const st = STATUS_AR[r.approvalStatus] || [r.approvalStatus, 'lab-badge-pending'];

          return `
            <tr>
              <td><b style="color: #0F2A5C; font-size: 0.9rem;">#${this.esc(String(r.sampleNumber || '-'))}</b></td>
              <td><span class="lab-badge lab-badge-ref">${this.esc(r.batchNumber || (`#${r.productionReferenceNumber}`))}</span></td>
              <td>${secBadge}</td>
              <td>${this.esc(r.sampleName || r.rawMaterialDescription || '-')}</td>
              <td>${r.sampleReceivedDate ? r.sampleReceivedDate.slice(0, 10) : '-'}</td>
              <td>${r.reportIssueDate ? r.reportIssueDate.slice(0, 10) : '-'}</td>
              <td>${complyBadge}</td>
              <td>${this.esc(r.labManagerName || r.responsibleEmployee || '-')}</td>
              <td><span class="lab-badge ${st[1]}">${st[0]}</span></td>
              <td>
                <button type="button" class="lab-btn lab-btn-ghost lab-btn-view-doc" data-id="${r.id}" style="padding: 4px 10px; font-size: 0.72rem;">
                  عرض التقرير / طباعة
                </button>
              </td>
            </tr>
          `;
        }).join('');

        this.historyTbody.querySelectorAll('.lab-btn-view-doc').forEach(btn => {
          btn.addEventListener('click', () => {
            const id = parseInt(btn.getAttribute('data-id'), 10);
            const r = this.cachedReports.find(x => x.id === id);
            if (r) this.openPrintModal(r);
          });
        });
      } catch (err) {
        this.historyTbody.innerHTML = `<tr><td colspan="10" class="lab-empty">تعذر تحميل السجلات: ${this.esc(err.message || '')}</td></tr>`;
      }
    }

    // ======================================================================
    // SLIDE 4: TRACEABILITY SEARCH
    // ======================================================================
    static async runTraceability() {
      const refNo = parseInt(this.traceInput?.value, 10);
      if (!this.traceResult) return;
      if (!refNo || refNo <= 0) {
        this.traceResult.innerHTML = '<div class="lab-trace-empty">أدخل رقماً مرجعياً صحيحاً (مثال: 1).</div>';
        return;
      }
      this.traceResult.innerHTML = '<div class="lab-loading">جاري تتبع دورة الإنتاج بالمرجع المشترك...</div>';
      try {
        const res = await window.ChatApi.getLaboratoryTraceability(refNo);
        if (!res || !res.success || !res.data) {
          this.traceResult.innerHTML = `<div class="lab-alert lab-alert-error">${this.esc((res && res.message) || 'الرقم المرجعي غير موجود.')}</div>`;
          return;
        }
        const t = res.data;
        const raw = t.rawMaterialReport || {};
        const cert = t.finalCertificate;
        const inProc = Array.isArray(t.inProcessReports) ? t.inProcessReports : [];
        const batches = Array.isArray(t.productionBatches) ? t.productionBatches : [];
        const imports = Array.isArray(t.storageImports) ? t.storageImports : [];
        const dec = cert ? (CONFORMITY_AR[cert.conformityDecision] || [cert.conformityDecision]) : null;

        this.traceResult.innerHTML = `
          <div class="lab-trace-panel" style="max-width: 900px; margin: 0 auto;">
            <h4>دورة الإنتاج المتكاملة للرقم المرجعي المشترك #${this.esc(String(refNo))} — ${this.esc(LAB_SECTION_AR[t.laboratorySection] || t.laboratorySection || '')}</h4>
            <div class="lab-trace-grid">
              <div class="lab-trace-item">
                <b>تقرير الفحص المعتمد</b>
                عينة رقم: #${this.esc(String(raw.sampleNumber || '-'))} | دفعة: ${this.esc(raw.batchNumber || '-')}
                <br>المطابقة: <b>${raw.complyStatus === 'COMPLY' ? 'Comply (مطابق)' : 'Not Complied'}</b>
              </div>
              <div class="lab-trace-item">
                <b>استلام المستودع (Storage)</b>
                ${imports.length} حركة استلام مرتبطة بالمرجع
              </div>
              <div class="lab-trace-item">
                <b>تشغيلات الإنتاج (Production)</b>
                ${batches.length} أمر تشغيل — مصروف: ${this.fmtQty(batches.reduce((s, b) => s + (b.materialQuantityIssued || 0), 0))} طن
              </div>
              <div class="lab-trace-item">
                <b>فحوصات التشغيل (In-Process QC)</b>
                ${inProc.length} فحص جودة مسجل
              </div>
              <div class="lab-trace-item">
                <b>شهادة مطابقة المنتج النهائي</b>
                ${cert ? this.esc(cert.certificateNumber) + ' — ' + this.esc(dec[0]) : 'لم تصدر بعد'}
              </div>
            </div>
          </div>`;
      } catch (err) {
        this.traceResult.innerHTML = `<div class="lab-alert lab-alert-error">تعذر الاتصال بالخادم: ${this.esc(err.message || '')}</div>`;
      }
    }

    // ======================================================================
    // UTILITIES
    // ======================================================================
    static esc(str) {
      return String(str == null ? '' : str)
        .replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;')
        .replace(/"/g, '&quot;').replace(/'/g, '&#39;');
    }

    static fmtQty(v) {
      const n = Number(v);
      if (isNaN(n)) return '-';
      return n.toLocaleString('en-US', { minimumFractionDigits: 3, maximumFractionDigits: 3 });
    }

    static debounce(fn, ms) {
      return function (...args) {
        clearTimeout(fn.__labDebounce);
        fn.__labDebounce = setTimeout(() => fn.apply(null, args), ms);
      };
    }
  }

  document.addEventListener('DOMContentLoaded', () => {
    window.LaboratoryApp = LaboratoryApp;
  });
  if (!window.LaboratoryApp) window.LaboratoryApp = LaboratoryApp;
})();
