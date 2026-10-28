/**
 * FaceStone Web - Attendance Department Application
 * Professional Arabic RTL Attendance Sheet with Mutual Exclusivity,
 * Mandatory Evidence Uploads, Control & Monitoring Integration, and Audit History.
 */
(function () {
  'use strict';

  class AttendanceApp {
    constructor() {
      this.initialized = false;
      this.selectedDate = this.getTodayDateString();
      this.employees = [];
      this.recordsState = new Map(); // userId => { status: 'Present' | 'Absent' | null, timestamp: string, notes: string }
      this.selectedFiles = []; // Array of File objects to be uploaded
      this.existingAttachments = []; // Array of AttendanceAttachmentDto objects already uploaded
      this.currentSubmissionId = null;
      this.currentStatus = 'DRAFT'; // 'DRAFT' | 'PENDING' | 'APPROVED' | 'REJECTED'
      this.rejectionReason = '';
      this.reviewedBy = '';
      this.reviewedAt = null;
      this.isSubmitting = false;
      this.isLoading = false;
      this.searchTerm = '';
    }

    getTodayDateString() {
      const today = new Date();
      const y = today.getFullYear();
      const m = String(today.getMonth() + 1).padStart(2, '0');
      const d = String(today.getDate()).padStart(2, '0');
      return `${y}-${m}-${d}`;
    }

    getNowDateTimeLocalString() {
      const now = new Date();
      const y = now.getFullYear();
      const m = String(now.getMonth() + 1).padStart(2, '0');
      const d = String(now.getDate()).padStart(2, '0');
      const h = String(now.getHours()).padStart(2, '0');
      const min = String(now.getMinutes()).padStart(2, '0');
      return `${y}-${m}-${d}T${h}:${min}`;
    }

    toLocalInputValue(dateStr) {
      if (!dateStr) return '';
      const d = new Date(dateStr);
      if (isNaN(d.getTime())) return '';
      const y = d.getFullYear();
      const m = String(d.getMonth() + 1).padStart(2, '0');
      const day = String(d.getDate()).padStart(2, '0');
      const h = String(d.getHours()).padStart(2, '0');
      const min = String(d.getMinutes()).padStart(2, '0');
      return `${y}-${m}-${day}T${h}:${min}`;
    }

    formatDisplayDateTime(dateStr) {
      if (!dateStr) return '-';
      const d = new Date(dateStr);
      if (isNaN(d.getTime())) return '-';
      const datePart = d.toLocaleDateString('ar-EG', { year: 'numeric', month: 'short', day: 'numeric' });
      const timePart = d.toLocaleTimeString('ar-EG', { hour: '2-digit', minute: '2-digit' });
      return `${datePart} • ${timePart}`;
    }

    init() {
      if (this.initialized) return;
      this.initialized = true;

      this.bindElements();
      this.bindEvents();
      this.setupDropzone();
      this.setupRealtime();

      if (this.datePicker) {
        this.datePicker.value = this.selectedDate;
      }
      if (this.dateDisplay) {
        this.dateDisplay.value = this.selectedDate;
      }

      this.loadAttendanceData(this.selectedDate);
    }

    bindElements() {
      this.root = document.getElementById('attendance-page-root');
      this.userAvatar = document.getElementById('attendance-user-avatar');
      this.userName = document.getElementById('attendance-user-name');
      this.userRole = document.getElementById('attendance-user-role');
      this.backBtn = document.getElementById('attendance-back-to-departments-btn');

      this.dateDisplay = document.getElementById('attendance-date-display');
      this.preparedByName = document.getElementById('attendance-prepared-by-name');
      this.preparedByRole = document.getElementById('attendance-prepared-by-role');
      this.statusSummaryText = document.getElementById('attendance-status-summary-text');

      this.statusBadge = document.getElementById('attendance-page-status-badge');
      this.statusBadgeText = document.getElementById('attendance-status-badge-text');

      this.rejectionBanner = document.getElementById('attendance-rejection-banner');
      this.rejectReviewer = document.getElementById('attendance-reject-reviewer');
      this.rejectDate = document.getElementById('attendance-reject-date');
      this.rejectReasonText = document.getElementById('attendance-reject-reason-text');
      this.btnReopenEdit = document.getElementById('btn-attendance-reopen-edit');

      this.datePicker = document.getElementById('attendance-date-picker');
      this.searchInput = document.getElementById('attendance-search-employee');
      this.btnRefresh = document.getElementById('attendance-btn-refresh');

      this.statTotal = document.getElementById('attendance-stat-total');
      this.statPresent = document.getElementById('attendance-stat-present');
      this.statAbsent = document.getElementById('attendance-stat-absent');
      this.statUnmarked = document.getElementById('attendance-stat-unmarked');

      this.tableBody = document.getElementById('attendance-table-body');
      this.tableLoading = document.getElementById('attendance-table-loading');
      this.tableEmpty = document.getElementById('attendance-table-empty');

      this.dropzone = document.getElementById('attendance-dropzone');
      this.fileInput = document.getElementById('attendance-file-input');
      this.filesList = document.getElementById('attendance-files-list');
      this.evidenceCountPill = document.getElementById('attendance-evidence-count-pill');

      this.btnReset = document.getElementById('btn-attendance-reset');
      this.btnSubmit = document.getElementById('btn-attendance-submit');
      this.feedbackBox = document.getElementById('attendance-submit-feedback');
    }

    bindEvents() {
      if (this.backBtn) {
        this.backBtn.addEventListener('click', () => {
          if (window.ChatApp) {
            window.ChatApp.backToDepartments();
          }
        });
      }

      // Requirement 3: Date is strictly locked to today. Manual date modification is blocked.
      if (this.datePicker) {
        this.datePicker.addEventListener('change', () => {
          this.selectedDate = this.getTodayDateString();
          this.datePicker.value = this.selectedDate;
          if (this.dateDisplay) this.dateDisplay.value = this.selectedDate;
        });
      }

      if (this.searchInput) {
        this.searchInput.addEventListener('input', (e) => {
          this.searchTerm = (e.target.value || '').trim().toLowerCase();
          this.renderTable();
        });
      }

      if (this.btnRefresh) {
        this.btnRefresh.addEventListener('click', () => {
          this.loadAttendanceData(this.selectedDate);
        });
      }

      if (this.btnReset) {
        this.btnReset.addEventListener('click', () => {
          this.selectedFiles = [];
          this.loadAttendanceData(this.selectedDate);
        });
      }

      if (this.btnSubmit) {
        this.btnSubmit.addEventListener('click', () => {
          this.handleSubmit();
        });
      }

      if (this.btnReopenEdit) {
        this.btnReopenEdit.addEventListener('click', () => {
          this.reopenForEdit();
        });
      }
    }

    setupDropzone() {
      if (!this.dropzone || !this.fileInput) return;

      this.dropzone.addEventListener('click', (e) => {
        // Prevent file dialog if clicking preview or remove button inside dropzone
        if (e.target.closest('button')) return;
        this.fileInput.click();
      });

      this.fileInput.addEventListener('change', (e) => {
        if (e.target.files && e.target.files.length > 0) {
          this.handleFilesAdded(Array.from(e.target.files));
          this.fileInput.value = '';
        }
      });

      ['dragenter', 'dragover'].forEach(eventName => {
        this.dropzone.addEventListener(eventName, (e) => {
          e.preventDefault();
          e.stopPropagation();
          this.dropzone.style.borderColor = '#0284C7';
          this.dropzone.style.background = '#F0F9FF';
        });
      });

      ['dragleave', 'drop'].forEach(eventName => {
        this.dropzone.addEventListener(eventName, (e) => {
          e.preventDefault();
          e.stopPropagation();
          this.dropzone.style.borderColor = '#94A3B8';
          this.dropzone.style.background = '#F8FAFC';
        });
      });

      this.dropzone.addEventListener('drop', (e) => {
        if (e.dataTransfer && e.dataTransfer.files && e.dataTransfer.files.length > 0) {
          this.handleFilesAdded(Array.from(e.dataTransfer.files));
        }
      });
    }

    handleFilesAdded(files) {
      const allowedExts = ['.pdf', '.jpg', '.jpeg', '.png'];
      const maxSizeBytes = 20 * 1024 * 1024; // 20 MB

      if (!files || files.length === 0) return;
      const file = files[0]; // Exactly ONE attachment at the end of the form

      const ext = '.' + file.name.split('.').pop().toLowerCase();
      if (!allowedExts.includes(ext)) {
        this.showFeedback(`الملف "${file.name}" غير مدعوم. الصيغ المدعومة هي: PDF, JPG, PNG.`, 'error');
        return;
      }

      if (file.size > maxSizeBytes) {
        this.showFeedback(`حجم الملف "${file.name}" يتجاوز الحد الأقصى (20 ميجابايت).`, 'error');
        return;
      }

      // Single attachment at the end replaces any previously chosen file
      this.selectedFiles = [file];
      this.renderSelectedFiles();
    }

    renderSelectedFiles() {
      if (!this.filesList) return;
      this.filesList.innerHTML = '';

      const totalCount = this.selectedFiles.length + this.existingAttachments.length;
      if (this.evidenceCountPill) {
        this.evidenceCountPill.textContent = totalCount > 0 ? `${totalCount} مستند مختار` : '0 مستند مختار';
        if (totalCount > 0) {
          this.evidenceCountPill.style.background = '#ECFDF5';
          this.evidenceCountPill.style.color = '#065F46';
          this.evidenceCountPill.style.border = '1px solid #A7F3D0';
        } else {
          this.evidenceCountPill.style.background = '#F1F5F9';
          this.evidenceCountPill.style.color = '#0F172A';
          this.evidenceCountPill.style.border = 'none';
        }
      }

      // 1. Existing Attachments
      this.existingAttachments.forEach((att, idx) => {
        const card = document.createElement('div');
        card.className = 'cm-attachment-card';
        card.style.background = '#FFFFFF';
        card.style.border = '1px solid #E2E8F0';
        card.style.borderRadius = '8px';
        card.style.padding = '10px 14px';
        card.style.display = 'flex';
        card.style.alignItems = 'center';
        card.style.justifyContent = 'space-between';
        card.style.gap = '8px';

        const isPdf = (att.fileType || '').toLowerCase().includes('pdf') || att.fileName.toLowerCase().endsWith('.pdf');
        const icon = isPdf ? '📄' : '🖼️';
        const sizeStr = this.formatFileSize(att.fileSize);

        card.innerHTML = `
          <div style="display:flex; align-items:center; gap:8px; overflow:hidden;">
            <span style="font-size:1.4rem;">${icon}</span>
            <div style="overflow:hidden;">
              <div style="font-weight:700; font-size:0.85rem; color:#0F172A; text-overflow:ellipsis; overflow:hidden; white-space:nowrap;" title="${this.escapeHtml(att.originalFileName || att.fileName)}">
                ${this.escapeHtml(att.originalFileName || att.fileName)}
              </div>
              <div style="font-size:0.75rem; color:#64748B;">
                <span>${sizeStr}</span> • <span style="color:#059669; font-weight:600;">سابق</span>
              </div>
            </div>
          </div>
          <div style="display:flex; align-items:center; gap:6px;">
            <button type="button" class="btn-att-preview" style="background:#F1F5F9; border:1px solid #CBD5E1; border-radius:6px; padding:4px 8px; font-size:0.75rem; cursor:pointer;" title="معاينة">
              👁️ معاينة
            </button>
          </div>
        `;

        const btnPrev = card.querySelector('.btn-att-preview');
        if (btnPrev) {
          btnPrev.addEventListener('click', (e) => {
            e.stopPropagation();
            this.previewAttachment(att);
          });
        }

        this.filesList.appendChild(card);
      });

      // 2. Newly Selected Files
      this.selectedFiles.forEach((file, idx) => {
        const card = document.createElement('div');
        card.className = 'cm-attachment-card';
        card.style.background = '#FFFFFF';
        card.style.border = '1px solid #0284C7';
        card.style.borderRadius = '8px';
        card.style.padding = '10px 14px';
        card.style.display = 'flex';
        card.style.alignItems = 'center';
        card.style.justifyContent = 'space-between';
        card.style.gap = '8px';

        const isPdf = file.name.toLowerCase().endsWith('.pdf');
        const icon = isPdf ? '📄' : '🖼️';
        const sizeStr = this.formatFileSize(file.size);

        card.innerHTML = `
          <div style="display:flex; align-items:center; gap:8px; overflow:hidden;">
            <span style="font-size:1.4rem;">${icon}</span>
            <div style="overflow:hidden;">
              <div style="font-weight:700; font-size:0.85rem; color:#0F172A; text-overflow:ellipsis; overflow:hidden; white-space:nowrap;" title="${this.escapeHtml(file.name)}">
                ${this.escapeHtml(file.name)}
              </div>
              <div style="font-size:0.75rem; color:#64748B;">
                <span>${sizeStr}</span> • <span style="color:#0284C7; font-weight:600;">جديد</span>
              </div>
            </div>
          </div>
          <div style="display:flex; align-items:center; gap:6px;">
            <button type="button" class="btn-file-remove" style="background:#FEE2E2; border:1px solid #FECACA; color:#DC2626; border-radius:6px; padding:4px 8px; font-size:0.75rem; cursor:pointer;" title="إزالة">
              ✕ إزالة
            </button>
          </div>
        `;

        const btnRemove = card.querySelector('.btn-file-remove');
        if (btnRemove) {
          btnRemove.addEventListener('click', (e) => {
            e.stopPropagation();
            this.selectedFiles.splice(idx, 1);
            this.renderSelectedFiles();
          });
        }

        this.filesList.appendChild(card);
      });
    }

    setupRealtime() {
      // Listen for SignalR real-time events forwarded by realtime.js
      window.addEventListener('facestone:realtime:attendance', (e) => {
        const detail = e.detail;
        if (detail && detail.formId && detail.formId === this.currentSubmissionId) {
          this.loadAttendanceData(this.selectedDate);
        }
      });

      window.addEventListener('facestone:realtime:control-monitoring', (e) => {
        const detail = e.detail;
        if (detail && (detail.department === 'attendance' || detail.formType === 'attendance')) {
          if (detail.formId === this.currentSubmissionId || detail.id === this.currentSubmissionId) {
            this.loadAttendanceData(this.selectedDate);
          }
        }
      });
    }

    updateUserBadge(user) {
      if (!user) return;
      const displayName = user.fullName || user.username || 'المستخدم';
      const roleText = window.ChatApp ? window.ChatApp.formatRoleArabic(user.role) : (user.role || 'موظف');

      if (this.userName) {
        this.userName.textContent = displayName;
      }
      if (this.userRole) {
        this.userRole.textContent = roleText;
      }
      if (this.preparedByName) {
        this.preparedByName.textContent = displayName;
      }
      if (this.preparedByRole) {
        this.preparedByRole.textContent = roleText;
      }
      if (this.userAvatar) {
        const name = user.fullName || user.username || 'FS';
        const parts = name.split(' ').filter(Boolean);
        let initials = 'FS';
        if (parts.length >= 2) {
          initials = (parts[0][0] + parts[1][0]).toUpperCase();
        } else if (parts.length === 1 && parts[0].length >= 2) {
          initials = parts[0].substring(0, 2).toUpperCase();
        }
        this.userAvatar.textContent = initials;
      }
    }

    async loadAttendanceData(dateStr) {
      this.isLoading = true;
      if (this.tableLoading) this.tableLoading.style.display = 'block';
      if (this.tableEmpty) this.tableEmpty.style.display = 'none';
      if (this.tableBody) this.tableBody.innerHTML = '';
      if (this.feedbackBox) this.feedbackBox.style.display = 'none';

      try {
        const res = await window.ChatApi.getAttendanceSheet(dateStr);
        if (this.tableLoading) this.tableLoading.style.display = 'none';

        if (res && res.success && res.data) {
          const d = res.data;
          this.employees = d.employees || [];
          this.currentSubmissionId = d.submissionId || null;
          this.currentStatus = (d.approvalStatus || 'DRAFT').toUpperCase();

          const sub = d.submission;
          if (sub) {
            this.rejectionReason = sub.rejectionReason || '';
            this.reviewedBy = sub.reviewedBy || '';
            this.reviewedAt = sub.reviewedAt || null;
            this.existingAttachments = sub.attachments || [];
          } else {
            this.rejectionReason = '';
            this.reviewedBy = '';
            this.reviewedAt = null;
            this.existingAttachments = [];
          }

          // Populate recordsState
          this.recordsState.clear();
          this.employees.forEach(emp => {
            const status = (emp.status && emp.status.trim()) ? emp.status : null;
            const subRec = sub && sub.records ? sub.records.find(r => r.userId === emp.userId || r.fullName === emp.fullName) : null;
            const appStatus = (subRec && subRec.approvalStatus) ? subRec.approvalStatus : (emp.approvalStatus || 'PENDING');
            const rejReason = (subRec && subRec.rejectionReason) ? subRec.rejectionReason : (emp.rejectionReason || '');
            const revBy = (subRec && subRec.reviewedBy) ? subRec.reviewedBy : (emp.reviewedBy || '');
            const revAt = (subRec && subRec.reviewedAt) ? subRec.reviewedAt : (emp.reviewedAt || null);
            const ts = (subRec && (subRec.attendanceDateTime || subRec.checkInTime)) 
              ? (subRec.attendanceDateTime || subRec.checkInTime)
              : (emp.attendanceDateTime || emp.checkInTime || emp.checkOutTime || (status && status.toLowerCase() === 'present' ? new Date().toISOString() : ''));

            this.recordsState.set(emp.userId, {
              recordId: subRec ? subRec.id : (emp.recordId || null),
              status: status,
              timestamp: ts,
              notes: (subRec ? subRec.notes : emp.notes) || '',
              approvalStatus: (appStatus || 'PENDING').toUpperCase(),
              rejectionReason: rejReason,
              reviewedBy: revBy,
              reviewedAt: revAt
            });
          });

          this.renderStatusBadge();
          this.renderRejectionBanner();
          this.renderSelectedFiles();
          this.renderTable();
          this.updateStatsCounters();
        } else {
          this.showFeedback(res?.message || 'تعذر تحميل بيانات كشف الحضور.', 'error');
        }
      } catch (err) {
        if (this.tableLoading) this.tableLoading.style.display = 'none';
        console.error('Error loading attendance sheet:', err);
        this.showFeedback('حدث خطأ أثناء تحميل سجلات كشف الحضور.', 'error');
      } finally {
        this.isLoading = false;
      }
    }

    async loadForEdit(submissionId) {
      try {
        const res = await window.ChatApi.getAttendanceSubmission(submissionId);
        if (res && res.success && res.data) {
          const sub = res.data;
          this.currentSubmissionId = sub.id;
          this.currentStatus = (sub.approvalStatus || 'REJECTED').toUpperCase();
          this.selectedDate = (sub.attendanceDate || '').split('T')[0] || this.selectedDate;
          if (this.datePicker) this.datePicker.value = this.selectedDate;

          this.rejectionReason = sub.rejectionReason || '';
          this.reviewedBy = sub.reviewedBy || '';
          this.reviewedAt = sub.reviewedAt || null;
          this.existingAttachments = sub.attachments || [];

          // Also get employees
          await this.loadAttendanceData(this.selectedDate);
          this.reopenForEdit();
        }
      } catch (err) {
        console.error('Error loading submission for edit:', err);
      }
    }

    reopenForEdit() {
      // Allows user to edit rejected or draft records
      this.isEditMode = true;
      if (this.btnSubmit) {
        this.btnSubmit.disabled = false;
        this.btnSubmit.style.opacity = '1';
        this.btnSubmit.innerHTML = `
          <svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.5"><polyline points="20 6 9 17 4 12"/></svg>
          <span>تعديل وإعادة تقديم / Edit & Resubmit</span>
        `;
      }
      this.renderTable();
    }

    renderStatusBadge() {
      if (!this.statusBadge || !this.statusBadgeText) return;

      const status = (this.currentStatus || 'DRAFT').toUpperCase();
      this.statusBadge.className = 'finance-badge-pill';

      if (status === 'APPROVED') {
        this.statusBadge.style.background = '#ECFDF5';
        this.statusBadge.style.color = '#065F46';
        this.statusBadge.style.border = '1px solid #A7F3D0';
        this.statusBadgeText.textContent = '✓ معتمد رسمياً (APPROVED)';
        if (this.statusSummaryText) {
          this.statusSummaryText.textContent = '✓ معتمد رسمياً (APPROVED)';
          this.statusSummaryText.style.color = '#065F46';
        }
      } else if (status === 'REJECTED') {
        this.statusBadge.style.background = '#FEF2F2';
        this.statusBadge.style.color = '#991B1B';
        this.statusBadge.style.border = '1px solid #FECACA';
        this.statusBadgeText.textContent = '✕ مرفوض للتعديل (REJECTED)';
        if (this.statusSummaryText) {
          this.statusSummaryText.textContent = '✕ مرفوض للتعديل (REJECTED)';
          this.statusSummaryText.style.color = '#991B1B';
        }
      } else if (status === 'PARTIALLY_REVIEWED') {
        this.statusBadge.style.background = '#FFFBEB';
        this.statusBadge.style.color = '#B45309';
        this.statusBadge.style.border = '1px solid #FCD34D';
        this.statusBadgeText.textContent = '⏳ مراجعة جزئية (PARTIALLY REVIEWED)';
        if (this.statusSummaryText) {
          this.statusSummaryText.textContent = '⏳ مراجعة جزئية (PARTIALLY REVIEWED)';
          this.statusSummaryText.style.color = '#B45309';
        }
      } else if (status === 'MIXED') {
        this.statusBadge.style.background = '#FEF2F2';
        this.statusBadge.style.color = '#B91C1C';
        this.statusBadge.style.border = '1px solid #FECACA';
        this.statusBadgeText.textContent = '⚠️ نتائج مراجعة متباينة (MIXED RESULTS)';
        if (this.statusSummaryText) {
          this.statusSummaryText.textContent = '⚠️ نتائج مراجعة متباينة (MIXED RESULTS)';
          this.statusSummaryText.style.color = '#B91C1C';
        }
      } else if (status === 'PENDING') {
        this.statusBadge.style.background = '#FFFBEB';
        this.statusBadge.style.color = '#92400E';
        this.statusBadge.style.border = '1px solid #FDE68A';
        this.statusBadgeText.textContent = '⏳ قيد المراجعة والتدقيق (PENDING REVIEW)';
        if (this.statusSummaryText) {
          this.statusSummaryText.textContent = '⏳ قيد المراجعة والتدقيق (PENDING REVIEW)';
          this.statusSummaryText.style.color = '#92400E';
        }
      } else {
        this.statusBadge.style.background = '#F1F5F9';
        this.statusBadge.style.color = '#0F172A';
        this.statusBadge.style.border = '1px solid #E2E8F0';
        this.statusBadgeText.textContent = 'مسودة جديدة (DRAFT)';
        if (this.statusSummaryText) {
          this.statusSummaryText.textContent = 'مسودة جديدة (DRAFT)';
          this.statusSummaryText.style.color = '#0F172A';
        }
      }

      // Check if any individual record has REJECTED status
      let hasRejectedRecords = false;
      for (const st of this.recordsState.values()) {
        if (st && st.approvalStatus === 'REJECTED') {
          hasRejectedRecords = true;
          break;
        }
      }

      // Toggle submit button state depending on status
      if (this.btnSubmit) {
        if (status === 'PENDING' && !this.isEditMode) {
          this.btnSubmit.disabled = true;
          this.btnSubmit.style.opacity = '0.6';
          this.btnSubmit.style.cursor = 'not-allowed';
          this.btnSubmit.title = 'الكشف قيد المراجعة والتدقيق حالياً في قسم المراقبة والتحكم.';
        } else if (status === 'APPROVED' && !this.isEditMode) {
          this.btnSubmit.disabled = true;
          this.btnSubmit.style.opacity = '0.6';
          this.btnSubmit.style.cursor = 'not-allowed';
          this.btnSubmit.title = 'تم اعتماد هذا الكشف رسمياً ولا يمكن التعديل عليه.';
        } else if (status === 'REJECTED' || status === 'MIXED' || (status === 'PARTIALLY_REVIEWED' && hasRejectedRecords) || this.isEditMode) {
          this.btnSubmit.disabled = false;
          this.btnSubmit.style.opacity = '1';
          this.btnSubmit.style.cursor = 'pointer';
          this.btnSubmit.innerHTML = `
            <svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.5"><polyline points="20 6 9 17 4 12"/></svg>
            <span>تعديل وإعادة تقديم / Edit & Resubmit</span>
          `;
        } else {
          this.btnSubmit.disabled = false;
          this.btnSubmit.style.opacity = '1';
          this.btnSubmit.style.cursor = 'pointer';
          this.btnSubmit.innerHTML = `
            <svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.5"><polyline points="20 6 9 17 4 12"/></svg>
            <span>Submit Attendance for Review / إرسال الحضور للمراجعة</span>
          `;
        }
      }
    }

    renderRejectionBanner() {
      if (!this.rejectionBanner) return;

      let hasRejectedRecords = false;
      let sampleRejectReason = this.rejectionReason || '';
      let sampleReviewer = this.reviewedBy || '';
      let sampleReviewedAt = this.reviewedAt;

      for (const st of this.recordsState.values()) {
        if (st && st.approvalStatus === 'REJECTED') {
          hasRejectedRecords = true;
          if (!sampleRejectReason && st.rejectionReason) sampleRejectReason = st.rejectionReason;
          if (!sampleReviewer && st.reviewedBy) sampleReviewer = st.reviewedBy;
          if (!sampleReviewedAt && st.reviewedAt) sampleReviewedAt = st.reviewedAt;
        }
      }

      if (this.currentStatus === 'REJECTED' || this.currentStatus === 'MIXED' || hasRejectedRecords) {
        this.rejectionBanner.style.display = 'block';
        if (this.rejectReviewer) {
          this.rejectReviewer.textContent = sampleReviewer || 'مراجع العمليات';
        }
        if (this.rejectDate) {
          this.rejectDate.textContent = sampleReviewedAt ? new Date(sampleReviewedAt).toLocaleString('ar-EG') : '-';
        }
        if (this.rejectReasonText) {
          this.rejectReasonText.textContent = sampleRejectReason || 'توجد سجلات مرفوضة يلزم تعديلها وإعادة تقديمها للمراجعة.';
        }
      } else {
        this.rejectionBanner.style.display = 'none';
      }
    }

    renderTable() {
      if (!this.tableBody) return;
      this.tableBody.innerHTML = '';

      const filtered = this.employees.filter(emp => {
        if (!this.searchTerm) return true;
        const name = (emp.fullName || '').toLowerCase();
        const role = (emp.role || '').toLowerCase();
        return name.includes(this.searchTerm) || role.includes(this.searchTerm);
      });

      if (filtered.length === 0) {
        if (this.tableEmpty) this.tableEmpty.style.display = 'block';
        return;
      } else {
        if (this.tableEmpty) this.tableEmpty.style.display = 'none';
      }

      // Check if table is locked (pending or approved, unless edit mode)
      const isLocked = (this.currentStatus === 'PENDING' || this.currentStatus === 'APPROVED') && !this.isEditMode;

      filtered.forEach((emp) => {
        const state = this.recordsState.get(emp.userId) || { 
          recordId: null, 
          status: null, 
          timestamp: '', 
          notes: '', 
          approvalStatus: 'PENDING',
          rejectionReason: '',
          reviewedBy: '',
          reviewedAt: null
        };
        const isPresent = state.status && state.status.toLowerCase() === 'present';
        const isAbsent = state.status && state.status.toLowerCase() === 'absent';
        const isRecordApproved = state.approvalStatus === 'APPROVED';

        // Approved records stay locked even during form resubmission/edit mode to prevent accidental tampering
        const isRowDisabled = isLocked || (this.isEditMode && isRecordApproved);

        const tr = document.createElement('tr');
        tr.className = 'finance-table-row';
        tr.style.transition = 'background 0.15s ease';

        // 1. Employee Name + Test Data Tag (Column 1)
        const nameHtml = `
          <div style="display:flex; align-items:center; gap:8px;">
            <div style="width:32px; height:32px; border-radius:50%; background:#0F172A; color:#FFFFFF; display:flex; align-items:center; justify-content:center; font-weight:800; font-size:0.75rem;">
              ${this.getInitials(emp.fullName || emp.username)}
            </div>
            <div>
              <div style="display:flex; align-items:center; gap:6px;">
                <span style="font-weight:700; color:#0F172A; font-size:0.9rem;">${this.escapeHtml(emp.fullName || emp.username)}</span>
                <span class="finance-badge-pill" style="font-size:0.65rem; background:#FEF3C7; color:#92400E; border:1px solid #FDE68A; padding:1px 6px; font-weight:700;">بيانات تجريبية</span>
              </div>
              <div style="font-size:0.75rem; color:#64748B;">${this.escapeHtml(emp.department || 'Operations')} • ID: ${emp.userId.substring(0, 8)}</div>
            </div>
          </div>
        `;

        // 2. System Role (Column 2)
        const roleArabic = window.ChatApp ? window.ChatApp.formatRoleArabic(emp.role) : (emp.role || 'موظف');
        const roleHtml = `
          <div>
            <span class="finance-badge-pill" style="font-size:0.75rem; font-weight:700; background:#F8FAFC; color:#334155; border:1px solid #E2E8F0;">
              ${this.escapeHtml(roleArabic)}
            </span>
            <div style="font-size:0.72rem; color:#94A3B8; margin-top:2px;">${this.escapeHtml(emp.role)}</div>
          </div>
        `;

        // 3. Present Checkbox (Mutually Exclusive, Column 3)
        const presentHtml = `
          <div style="display:flex; justify-content:center; align-items:center;">
            <input type="checkbox" class="att-chk-present" data-user-id="${emp.userId}"
              ${isPresent ? 'checked' : ''} ${isRowDisabled ? 'disabled' : ''}
              style="width:20px; height:20px; cursor:${isRowDisabled ? 'not-allowed' : 'pointer'}; accent-color:#059669;" />
          </div>
        `;

        // 4. Absent Checkbox (Mutually Exclusive, Column 4)
        const absentHtml = `
          <div style="display:flex; justify-content:center; align-items:center;">
            <input type="checkbox" class="att-chk-absent" data-user-id="${emp.userId}"
              ${isAbsent ? 'checked' : ''} ${isRowDisabled ? 'disabled' : ''}
              style="width:20px; height:20px; cursor:${isRowDisabled ? 'not-allowed' : 'pointer'}; accent-color:#DC2626;" />
          </div>
        `;

        // 5. Individual Attendance Date & Time (Column 5)
        const localInputValue = this.toLocalInputValue(state.timestamp);
        const displayDateTimeText = isPresent
          ? (state.timestamp ? this.formatDisplayDateTime(state.timestamp) : 'يرجى تحديد التاريخ والوقت')
          : 'غير مطلوب للغائب';

        const datetimeHtml = `
          <div class="att-dt-wrapper" style="display:flex; flex-direction:column; gap:4px;">
            <div style="display:flex; align-items:center; gap:6px;">
              <input type="datetime-local" class="att-input-datetime finance-filter-input" data-user-id="${emp.userId}"
                value="${localInputValue}"
                max="${this.getNowDateTimeLocalString()}"
                ${(!isPresent || isRowDisabled) ? 'disabled' : ''}
                style="padding:6px 8px; font-size:0.825rem; font-weight:700; width:100%; border-radius:6px; background:${!isPresent ? '#F8FAFC' : '#FFFFFF'}; border:1px solid #CBD5E1; color:#0F172A;" />
              <button type="button" class="btn-att-now" data-user-id="${emp.userId}"
                ${(!isPresent || isRowDisabled) ? 'disabled' : ''}
                title="تعيين الوقت والتاريخ الحالي"
                style="padding:5px 9px; font-size:0.75rem; font-weight:700; background:#E0F2FE; color:#0369A1; border:1px solid #BAE6FD; border-radius:6px; cursor:${(!isPresent || isRowDisabled) ? 'not-allowed' : 'pointer'}; white-space:nowrap;">
                الآن
              </button>
            </div>
            <div class="att-dt-display" data-user-id="${emp.userId}" style="font-size:0.72rem; color:${isPresent ? '#059669' : '#94A3B8'}; font-weight:600;">
              ${displayDateTimeText}
            </div>
            <div class="att-dt-error" data-user-id="${emp.userId}" style="font-size:0.72rem; color:#DC2626; font-weight:700; display:none;"></div>
          </div>
        `;

        // 6. Notes Input (Column 6)
        const notesHtml = `
          <div>
            <input type="text" class="att-input-notes finance-filter-input" data-user-id="${emp.userId}"
              value="${this.escapeHtml(state.notes || '')}" ${isRowDisabled ? 'disabled' : ''}
              placeholder="ملاحظات الحضور..."
              style="padding:6px 10px; font-size:0.825rem; width:100%; border-radius:6px;" />
          </div>
        `;

        // 7. Individual Approval Status (Column 7)
        let statusBadgeHtml = '';
        if (state.approvalStatus === 'APPROVED') {
          statusBadgeHtml = `
            <span class="cm-badge cm-badge-approved">✓ معتمد</span>
            ${state.reviewedBy ? `<div style="font-size:0.7rem; color:#047857; margin-top:2px;">بواسطة: ${this.escapeHtml(state.reviewedBy)}</div>` : ''}
          `;
        } else if (state.approvalStatus === 'REJECTED') {
          statusBadgeHtml = `
            <span class="cm-badge cm-badge-rejected" title="${this.escapeHtml(state.rejectionReason || '')}">✕ مرفوض</span>
            <div style="font-size:0.72rem; color:#DC2626; margin-top:2px; font-weight:700; line-height:1.2;">${this.escapeHtml(state.rejectionReason || 'يلزم التعديل')}</div>
            ${state.reviewedBy ? `<div style="font-size:0.68rem; color:#991B1B; margin-top:1px;">بواسطة: ${this.escapeHtml(state.reviewedBy)}</div>` : ''}
          `;
        } else if (state.approvalStatus === 'PENDING' && (this.currentStatus === 'PENDING' || this.currentStatus === 'PARTIALLY_REVIEWED' || this.currentStatus === 'MIXED')) {
          statusBadgeHtml = `<span class="cm-badge cm-badge-pending">⏳ قيد المراجعة</span>`;
        } else {
          statusBadgeHtml = state.status 
            ? `<span class="finance-badge-pill" style="font-size:0.75rem; background:#F1F5F9; color:#0F172A;">مسودة (${state.status === 'Present' ? 'حاضر' : 'غائب'})</span>` 
            : `<span style="color:#94A3B8; font-size:0.75rem;">لم يسجل</span>`;
        }

        tr.innerHTML = `
          <td>${nameHtml}</td>
          <td>${roleHtml}</td>
          <td style="text-align:center;">${presentHtml}</td>
          <td style="text-align:center;">${absentHtml}</td>
          <td>${datetimeHtml}</td>
          <td>${notesHtml}</td>
          <td style="text-align:center;">${statusBadgeHtml}</td>
        `;

        // Row background tint for quick visibility
        if (state.approvalStatus === 'REJECTED') {
          tr.style.background = '#FFF1F2';
        } else if (isPresent) {
          tr.style.background = '#F0FDF4';
        } else if (isAbsent) {
          tr.style.background = '#FEF2F2';
        }

        // Event Listeners for Row Controls
        const chkPresent = tr.querySelector('.att-chk-present');
        const chkAbsent = tr.querySelector('.att-chk-absent');
        const inputDt = tr.querySelector('.att-input-datetime');
        const btnNow = tr.querySelector('.btn-att-now');
        const inputNotes = tr.querySelector('.att-input-notes');
        const displayDt = tr.querySelector('.att-dt-display');
        const errorDt = tr.querySelector('.att-dt-error');

        if (inputNotes && !isRowDisabled) {
          inputNotes.addEventListener('input', (e) => {
            const current = this.recordsState.get(emp.userId) || { status: null, timestamp: '', notes: '' };
            current.notes = e.target.value;
            this.recordsState.set(emp.userId, current);
          });
        }

        // Datetime-local validation function
        const validateDateTimeInput = (val) => {
          if (!val) {
            return 'تاريخ ووقت الحضور إلزامي للموظف الحاضر';
          }
          const d = new Date(val);
          if (isNaN(d.getTime())) {
            return 'تاريخ ووقت غير صالح';
          }
          const nowMs = Date.now() + 2 * 60 * 1000; // Allow 2 minutes clock leeway
          if (d.getTime() > nowMs) {
            return 'لا يمكن تسجيل تاريخ أو وقت في المستقبل';
          }
          return null;
        };

        if (inputDt && !isRowDisabled) {
          const handleDtChange = () => {
            const current = this.recordsState.get(emp.userId) || { status: 'Present', timestamp: '', notes: '' };
            const err = validateDateTimeInput(inputDt.value);
            if (err) {
              if (errorDt) {
                errorDt.textContent = err;
                errorDt.style.display = 'block';
              }
              inputDt.style.borderColor = '#DC2626';
              inputDt.style.background = '#FEF2F2';
              current.timestamp = inputDt.value ? new Date(inputDt.value).toISOString() : '';
            } else {
              if (errorDt) {
                errorDt.textContent = '';
                errorDt.style.display = 'none';
              }
              inputDt.style.borderColor = '#CBD5E1';
              inputDt.style.background = '#FFFFFF';
              const d = new Date(inputDt.value);
              current.timestamp = d.toISOString();
              if (displayDt) {
                displayDt.textContent = this.formatDisplayDateTime(current.timestamp);
                displayDt.style.color = '#059669';
              }
            }
            this.recordsState.set(emp.userId, current);
          };

          inputDt.addEventListener('change', handleDtChange);
          inputDt.addEventListener('blur', handleDtChange);
        }

        if (btnNow && !isRowDisabled) {
          btnNow.addEventListener('click', (e) => {
            e.stopPropagation();
            const nowIso = new Date().toISOString();
            const nowLocal = this.getNowDateTimeLocalString();
            if (inputDt) {
              inputDt.value = nowLocal;
              inputDt.style.borderColor = '#CBD5E1';
              inputDt.style.background = '#FFFFFF';
            }
            if (errorDt) {
              errorDt.textContent = '';
              errorDt.style.display = 'none';
            }
            if (displayDt) {
              displayDt.textContent = this.formatDisplayDateTime(nowIso);
              displayDt.style.color = '#059669';
            }
            const current = this.recordsState.get(emp.userId) || { status: 'Present', timestamp: '', notes: '' };
            current.timestamp = nowIso;
            this.recordsState.set(emp.userId, current);
          });
        }

        if (chkAbsent && chkPresent && !isRowDisabled) {
          chkPresent.addEventListener('change', () => {
            const current = this.recordsState.get(emp.userId) || { status: null, timestamp: '', notes: '' };
            if (chkPresent.checked) {
              chkAbsent.checked = false;
              if (inputDt) {
                inputDt.disabled = false;
                inputDt.style.background = '#FFFFFF';
                if (!current.timestamp) {
                  current.timestamp = new Date().toISOString();
                  inputDt.value = this.getNowDateTimeLocalString();
                } else {
                  inputDt.value = this.toLocalInputValue(current.timestamp);
                }
              }
              if (btnNow) btnNow.disabled = false;
              if (displayDt) {
                displayDt.textContent = this.formatDisplayDateTime(current.timestamp);
                displayDt.style.color = '#059669';
              }
              if (errorDt) {
                errorDt.textContent = '';
                errorDt.style.display = 'none';
              }
              current.status = 'Present';
              this.recordsState.set(emp.userId, current);
              tr.style.background = '#F0FDF4';
            } else {
              if (inputDt) {
                inputDt.disabled = true;
                inputDt.value = '';
                inputDt.style.background = '#F8FAFC';
                inputDt.style.borderColor = '#CBD5E1';
              }
              if (btnNow) btnNow.disabled = true;
              if (displayDt) {
                displayDt.textContent = 'غير محدد';
                displayDt.style.color = '#94A3B8';
              }
              if (errorDt) {
                errorDt.textContent = '';
                errorDt.style.display = 'none';
              }
              current.status = null;
              current.timestamp = '';
              this.recordsState.set(emp.userId, current);
              tr.style.background = 'transparent';
            }
            this.updateStatsCounters();
            this.renderRowTimeAndStatus(tr, emp.userId);
          });

          chkAbsent.addEventListener('change', () => {
            const current = this.recordsState.get(emp.userId) || { status: null, timestamp: '', notes: '' };
            if (chkAbsent.checked) {
              chkPresent.checked = false;
              if (inputDt) {
                inputDt.disabled = true;
                inputDt.value = '';
                inputDt.style.background = '#F8FAFC';
                inputDt.style.borderColor = '#CBD5E1';
              }
              if (btnNow) btnNow.disabled = true;
              if (displayDt) {
                displayDt.textContent = 'غير مطلوب للغائب';
                displayDt.style.color = '#94A3B8';
              }
              if (errorDt) {
                errorDt.textContent = '';
                errorDt.style.display = 'none';
              }
              current.status = 'Absent';
              current.timestamp = '';
              this.recordsState.set(emp.userId, current);
              tr.style.background = '#FEF2F2';
            } else {
              if (displayDt) {
                displayDt.textContent = 'غير محدد';
                displayDt.style.color = '#94A3B8';
              }
              if (errorDt) {
                errorDt.textContent = '';
                errorDt.style.display = 'none';
              }
              current.status = null;
              current.timestamp = '';
              this.recordsState.set(emp.userId, current);
              tr.style.background = 'transparent';
            }
            this.updateStatsCounters();
            this.renderRowTimeAndStatus(tr, emp.userId);
          });
        }

        this.tableBody.appendChild(tr);
      });
    }

    showRowDtError(userId, message) {
      const errorDiv = document.querySelector(`.att-dt-error[data-user-id="${userId}"]`);
      const inputEl = document.querySelector(`.att-input-datetime[data-user-id="${userId}"]`);
      if (errorDiv) {
        errorDiv.textContent = message;
        errorDiv.style.display = 'block';
      }
      if (inputEl) {
        inputEl.style.borderColor = '#DC2626';
        inputEl.style.background = '#FEF2F2';
      }
    }

    renderRowTimeAndStatus(tr, userId) {
      const state = this.recordsState.get(userId);
      const cells = tr.querySelectorAll('td');
      if (cells.length >= 7) {
        // Status cell is column 7 (index 6)
        if (state && state.approvalStatus === 'APPROVED') {
          cells[6].innerHTML = `<span class="cm-badge cm-badge-approved">✓ معتمد</span>`;
        } else if (state && state.approvalStatus === 'REJECTED') {
          cells[6].innerHTML = `
            <span class="cm-badge cm-badge-rejected" title="${this.escapeHtml(state.rejectionReason || '')}">✕ مرفوض</span>
            <div style="font-size:0.72rem; color:#DC2626; margin-top:2px; font-weight:700;">${this.escapeHtml(state.rejectionReason || 'يلزم التعديل')}</div>
          `;
        } else if (state && state.approvalStatus === 'PENDING' && (this.currentStatus === 'PENDING' || this.currentStatus === 'PARTIALLY_REVIEWED' || this.currentStatus === 'MIXED')) {
          cells[6].innerHTML = `<span class="cm-badge cm-badge-pending">⏳ قيد المراجعة</span>`;
        } else if (state && state.status) {
          cells[6].innerHTML = `<span class="finance-badge-pill" style="font-size:0.75rem; background:#F1F5F9; color:#0F172A;">مسودة (${state.status === 'Present' ? 'حاضر' : 'غائب'})</span>`;
        } else {
          cells[6].innerHTML = `<span style="color:#94A3B8; font-size:0.75rem;">لم يسجل</span>`;
        }
      }
    }

    updateStatsCounters() {
      let present = 0;
      let absent = 0;
      let unmarked = 0;

      this.employees.forEach(emp => {
        const state = this.recordsState.get(emp.userId);
        if (state && state.status) {
          const st = state.status.toLowerCase();
          if (st === 'present') present++;
          else if (st === 'absent') absent++;
          else unmarked++;
        } else {
          unmarked++;
        }
      });

      if (this.statTotal) this.statTotal.textContent = this.employees.length;
      if (this.statPresent) this.statPresent.textContent = present;
      if (this.statAbsent) this.statAbsent.textContent = absent;
      if (this.statUnmarked) this.statUnmarked.textContent = unmarked;
    }

    /* ------------------------------------------------------------------------
       SUBMISSION & VALIDATION LOGIC
       ------------------------------------------------------------------------ */
    async handleSubmit() {
      if (this.isSubmitting) return;

      // 1. Strictly enforce today's date (Requirement 3)
      this.selectedDate = this.getTodayDateString();
      if (this.dateDisplay) this.dateDisplay.value = this.selectedDate;
      if (this.datePicker) this.datePicker.value = this.selectedDate;

      // 2. Validate employee records & individual date-time
      const recordsToSubmit = [];
      let hasValidationError = false;
      let firstErrorInput = null;

      for (const [userId, state] of this.recordsState.entries()) {
        if (state && state.status) {
          const emp = this.employees.find(e => e.userId === userId);
          const fullName = emp ? emp.fullName : 'الموظف';
          const isPresent = state.status.toLowerCase() === 'present';
          const isAbsent = state.status.toLowerCase() === 'absent';

          if (isPresent) {
            if (!state.timestamp || !state.timestamp.trim()) {
              hasValidationError = true;
              this.showRowDtError(userId, 'تاريخ ووقت الحضور مطلوب للموظف الحاضر');
              if (!firstErrorInput) firstErrorInput = document.querySelector(`.att-input-datetime[data-user-id="${userId}"]`);
              continue;
            }

            const dt = new Date(state.timestamp);
            if (isNaN(dt.getTime())) {
              hasValidationError = true;
              this.showRowDtError(userId, 'تاريخ ووقت غير صالح');
              if (!firstErrorInput) firstErrorInput = document.querySelector(`.att-input-datetime[data-user-id="${userId}"]`);
              continue;
            }

            const nowLeeway = Date.now() + 2 * 60 * 1000;
            if (dt.getTime() > nowLeeway) {
              hasValidationError = true;
              this.showRowDtError(userId, 'لا يمكن تسجيل وقت في المستقبل');
              if (!firstErrorInput) firstErrorInput = document.querySelector(`.att-input-datetime[data-user-id="${userId}"]`);
              continue;
            }
          }

          recordsToSubmit.push({
            recordId: state.recordId,
            userId: userId,
            fullName: emp ? emp.fullName : 'Unknown',
            role: emp ? emp.role : 'Employee',
            status: state.status,
            attendanceDateTime: isPresent ? state.timestamp : null,
            checkInTime: isPresent ? state.timestamp : null,
            checkOutTime: null,
            notes: state.notes || '',
            approvalStatus: state.approvalStatus || 'PENDING',
            rejectionReason: state.rejectionReason || null,
            isTestData: emp ? (emp.isTestData !== false) : true
          });
        }
      }

      if (hasValidationError) {
        this.showFeedback('⚠️ يرجى تصحيح أخطاء تاريخ ووقت الحضور المحددة باللون الأحمر. لا يُسمح بتواريخ فارغة أو مستقبلية.', 'error');
        if (firstErrorInput) {
          firstErrorInput.scrollIntoView({ behavior: 'smooth', block: 'center' });
          firstErrorInput.focus();
        }
        return;
      }

      if (recordsToSubmit.length === 0) {
        this.showFeedback('يجب تسجيل حالة حضور أو غياب لموظف واحد على الأقل قبل الإرسال للمراجعة.', 'error');
        return;
      }

      // 3. Mutual exclusivity verification
      for (const rec of recordsToSubmit) {
        const st = (rec.status || '').toLowerCase();
        if (st !== 'present' && st !== 'absent') {
          this.showFeedback(`حالة الحضور للموظف ${rec.fullName} غير صالحة.`, 'error');
          return;
        }
      }

      // 4. CRITICAL REQUIREMENT: MANDATORY ATTACHMENT UPLOAD
      const totalEvidenceCount = this.selectedFiles.length + this.existingAttachments.length;
      if (totalEvidenceCount === 0) {
        this.showFeedback('⚠️ مرفقات إثبات الحضور (Attendance Evidence) مطلوبة وإلزامية! لا يمكن إرسال كشف الحضور للمراجعة بدون رفع المستندات الداعمة (كشف بصمة، توقيعات، أو مستند رسمي).', 'error');
        if (this.dropzone) {
          this.dropzone.scrollIntoView({ behavior: 'smooth', block: 'center' });
          this.dropzone.style.borderColor = '#DC2626';
          this.dropzone.style.background = '#FEF2F2';
          setTimeout(() => {
            this.dropzone.style.borderColor = '#94A3B8';
            this.dropzone.style.background = '#F8FAFC';
          }, 3000);
        }
        return;
      }

      // 5. Send to backend
      this.isSubmitting = true;
      if (this.btnSubmit) {
        this.btnSubmit.disabled = true;
        this.btnSubmit.innerHTML = `
          <div class="finance-spinner" style="width:18px; height:18px; border-width:2px;"></div>
          <span>جاري إرسال الكشف والمرفقات للمراجعة...</span>
        `;
      }

      try {
        const formData = new FormData();
        formData.append('attendanceDate', this.selectedDate);
        formData.append('notes', 'تم تقديم كشف الحضور عبر البوابة الرسمية.');
        formData.append('recordsJson', JSON.stringify(recordsToSubmit));

        this.selectedFiles.forEach(file => {
          formData.append('attachments', file, file.name);
        });

        let res;
        // Check if resubmitting an existing submission (rejected, mixed, or partially reviewed)
        const isResubmit = this.currentSubmissionId && (this.currentStatus === 'REJECTED' || this.currentStatus === 'MIXED' || this.currentStatus === 'PARTIALLY_REVIEWED' || this.isEditMode);
        if (isResubmit) {
          res = await window.ChatApi.resubmitAttendance(this.currentSubmissionId, formData);
        } else {
          res = await window.ChatApi.submitAttendance(formData);
        }

        if (res && res.success) {
          this.showFeedback('✓ تم إرسال كشف الحضور والانصراف والمرفقات بنجاح إلى قسم المراقبة والتحكم للمراجعة والتدقيق.', 'success');
          this.selectedFiles = [];
          this.isEditMode = false;
          await this.loadAttendanceData(this.selectedDate);
        } else {
          this.showFeedback('تعذر إرسال كشف الحضور: ' + (res?.message || 'حدث خطأ في الخادم.'), 'error');
        }
      } catch (err) {
        console.error('Error submitting attendance:', err);
        this.showFeedback('حدث خطأ أثناء رفع المرفقات وإرسال الكشف.', 'error');
      } finally {
        this.isSubmitting = false;
        if (this.btnSubmit) {
          this.renderStatusBadge();
        }
      }
    }

    previewAttachment(att) {
      if (!att) return;
      const overlay = document.getElementById('cm-preview-overlay');
      const body = document.getElementById('cm-preview-body');
      const title = document.getElementById('cm-preview-title');
      const popout = document.getElementById('cm-preview-popout-link');
      const download = document.getElementById('cm-preview-download-link');
      const closeBtn = document.getElementById('cm-preview-close');

      if (!overlay || !body) return;

      const viewUrl = att.viewUrl || `/api/control-monitoring/attachments/attendance/${att.fileName}`;
      const downloadUrl = att.downloadUrl || `${viewUrl}?download=true`;

      // Append auth token if available
      const token = window.ChatApi ? window.ChatApi.getToken() : null;
      const authedUrl = token ? `${viewUrl}${viewUrl.includes('?') ? '&' : '?'}t=${encodeURIComponent(token)}` : viewUrl;

      if (title) title.textContent = att.originalFileName || att.fileName || 'معاينة المرفق';
      if (popout) popout.href = authedUrl;
      if (download) download.href = downloadUrl;

      const isPdf = (att.fileType || '').toLowerCase().includes('pdf') || att.fileName.toLowerCase().endsWith('.pdf');

      if (isPdf) {
        body.innerHTML = `
          <iframe src="${this.escapeHtml(authedUrl)}" style="width:100%; height:75vh; border:none; border-radius:8px;"></iframe>
        `;
      } else {
        body.innerHTML = `
          <div style="text-align:center; padding:1.5rem;">
            <img src="${this.escapeHtml(authedUrl)}" alt="${this.escapeHtml(att.fileName)}" style="max-width:100%; max-height:75vh; border-radius:8px; box-shadow:0 4px 16px rgba(0,0,0,0.2);" />
          </div>
        `;
      }

      overlay.style.display = 'flex';

      if (closeBtn) {
        closeBtn.onclick = () => {
          overlay.style.display = 'none';
          body.innerHTML = '';
        };
      }
    }

    showFeedback(message, type = 'info') {
      if (!this.feedbackBox) return;
      this.feedbackBox.style.display = 'block';
      this.feedbackBox.textContent = message;

      if (type === 'error') {
        this.feedbackBox.style.background = '#FEF2F2';
        this.feedbackBox.style.color = '#991B1B';
        this.feedbackBox.style.border = '1px solid #FECACA';
      } else if (type === 'success') {
        this.feedbackBox.style.background = '#ECFDF5';
        this.feedbackBox.style.color = '#065F46';
        this.feedbackBox.style.border = '1px solid #A7F3D0';
      } else {
        this.feedbackBox.style.background = '#F0F9FF';
        this.feedbackBox.style.color = '#0369A1';
        this.feedbackBox.style.border = '1px solid #BAE6FD';
      }

      this.feedbackBox.scrollIntoView({ behavior: 'smooth', block: 'nearest' });
    }

    formatFileSize(bytes) {
      if (!bytes || bytes <= 0) return '0 B';
      const k = 1024;
      const sizes = ['B', 'KB', 'MB', 'GB'];
      const i = Math.floor(Math.log(bytes) / Math.log(k));
      return parseFloat((bytes / Math.pow(k, i)).toFixed(1)) + ' ' + sizes[i];
    }

    getInitials(name) {
      if (!name) return 'FS';
      const parts = name.trim().split(' ').filter(Boolean);
      if (parts.length >= 2) {
        return (parts[0][0] + parts[1][0]).toUpperCase();
      }
      return name.substring(0, 2).toUpperCase();
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

  window.AttendanceApp = new AttendanceApp();
})();
