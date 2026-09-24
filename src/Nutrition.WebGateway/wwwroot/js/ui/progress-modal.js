/**
 * ProgressModalController
 * Manages the Face Transformation & Progress dashboard card,
 * and the Visual Progress Detail & Full Body multi-tab modal.
 */
export class ProgressModalController {
  /**
   * @param {Object} options
   * @param {import('../services/progress-service.js').ProgressPhotosService} options.progressService
   * @param {import('../ui/toast.js').ToastNotificationService} options.toastService
   * @param {import('../ui/confetti.js').ConfettiService} options.confettiService
   * @param {import('../services/auth-service.js').AuthService} [options.authService]
   * @param {import('../core/state.js').AppState} options.appState
   * @param {import('../core/event-bus.js').EventBus} options.eventBus
   */
  constructor({ progressService, toastService, confettiService, authService, appState, eventBus }) {
    this._progress = progressService;
    this._toast = toastService;
    this._confetti = confettiService;
    this._authService = authService;
    this._state = appState;
    this._bus = eventBus;

    this._bindEvents();

    // Listen for events
    this._bus.on('progress:open', (targetTab) => this.open(targetTab));
    this._bus.on('progress:saved', () => this.refresh());
  }

  get elements() {
    return {
      modal: document.getElementById('progress-modal'),
      btnClose: document.getElementById('btn-close-progress'),
      btnOpenHeader: document.getElementById('btn-open-progress-header'),
      btnOpenBody: document.getElementById('btn-open-body-modal'),
      btnQuickCheckin: document.getElementById('btn-quick-photo-checkin'),
      faceDeltaPill: document.getElementById('face-delta-pill'),
      modalDeltaPill: document.getElementById('modal-delta-pill'),
      imgBaselineFace: document.getElementById('img-baseline-face'),
      txtBaselineDate: document.getElementById('txt-baseline-date'),
      txtBaselineWeight: document.getElementById('txt-baseline-weight'),
      imgCurrentFace: document.getElementById('img-current-face'),
      txtCurrentDate: document.getElementById('txt-current-date'),
      txtCurrentWeight: document.getElementById('txt-current-weight'),
      txtDaysElapsed: document.getElementById('txt-days-elapsed'),
      txtNetLoss: document.getElementById('txt-net-loss'),
      detailBaselineFaceImg: document.getElementById('detail-baseline-face-img'),
      detailBaselineFaceDate: document.getElementById('detail-baseline-face-date'),
      detailBaselineFaceWeight: document.getElementById('detail-baseline-face-weight'),
      detailCurrentFaceImg: document.getElementById('detail-current-face-img'),
      detailCurrentFaceDate: document.getElementById('detail-current-face-date'),
      detailCurrentFaceWeight: document.getElementById('detail-current-face-weight'),
      detailBaselineBodyImg: document.getElementById('detail-baseline-body-img'),
      detailBaselineBodyDate: document.getElementById('detail-baseline-body-date'),
      detailBaselineBodyWeight: document.getElementById('detail-baseline-body-weight'),
      detailCurrentBodyImg: document.getElementById('detail-current-body-img'),
      detailCurrentBodyDate: document.getElementById('detail-current-body-date'),
      detailCurrentBodyWeight: document.getElementById('detail-current-body-weight'),
      timelineGalleryGrid: document.getElementById('timeline-gallery-grid'),
      formUpload: document.getElementById('form-progress-upload'),
      dropzone: document.getElementById('progress-dropzone'),
      fileInput: document.getElementById('progress-file-input'),
      dropzonePrompt: document.getElementById('dropzone-prompt'),
      dropzonePreview: document.getElementById('dropzone-preview'),
      imgPreview: document.getElementById('progress-img-preview'),
      inpWeight: document.getElementById('inp-checkin-weight'),
      inpDate: document.getElementById('inp-checkin-date'),
      inpType: document.getElementById('inp-photo-type'),
      chkBaseline: document.getElementById('chk-is-baseline'),
      inpNotes: document.getElementById('inp-checkin-notes'),
      btnCancelUpload: document.getElementById('btn-cancel-progress-upload')
    };
  }

  _bindEvents() {
    const el = this.elements;

    if (el.btnOpenHeader) {
      el.btnOpenHeader.addEventListener('click', () => this.open('pane-face-progress'));
    }
    if (el.btnOpenBody) {
      el.btnOpenBody.addEventListener('click', () => this.open('pane-body-progress'));
    }
    if (el.btnQuickCheckin) {
      el.btnQuickCheckin.addEventListener('click', () => this.open('pane-checkin-upload'));
    }
    if (el.btnClose) {
      el.btnClose.addEventListener('click', () => this.close());
    }

    if (el.modal) {
      el.modal.addEventListener('click', (e) => {
        if (e.target === el.modal) this.close();
      });
    }

    // Modal Tabs
    document.querySelectorAll('.progress-tab-btn').forEach(btn => {
      btn.addEventListener('click', () => {
        const tab = btn.dataset.tab;
        this.switchTab(tab);
      });
    });

    // Photo Type Pills
    document.querySelectorAll('.photo-type-selector .type-pill').forEach(pill => {
      pill.addEventListener('click', () => {
        document.querySelectorAll('.photo-type-selector .type-pill').forEach(p => p.classList.remove('active'));
        pill.classList.add('active');
        if (el.inpType) el.inpType.value = pill.dataset.type;
      });
    });

    // Photo Dropzone
    if (el.dropzone && el.fileInput) {
      el.dropzone.addEventListener('click', () => el.fileInput.click());
      el.fileInput.addEventListener('change', (e) => {
        if (e.target.files.length > 0) {
          const file = e.target.files[0];
          const reader = new FileReader();
          reader.onload = (re) => {
            if (el.imgPreview) el.imgPreview.src = re.target.result;
            if (el.dropzonePrompt) el.dropzonePrompt.style.display = 'none';
            if (el.dropzonePreview) el.dropzonePreview.style.display = 'block';
          };
          reader.readAsDataURL(file);
        }
      });
    }

    if (el.btnCancelUpload) {
      el.btnCancelUpload.addEventListener('click', () => {
        this.switchTab('pane-face-progress');
      });
    }

    if (el.formUpload) {
      el.formUpload.addEventListener('submit', (e) => this.handleSubmit(e));
    }
  }

  open(targetTab = 'pane-face-progress') {
    this.switchTab(targetTab);
    const el = this.elements;

    if (el.inpDate && !el.inpDate.value) {
      el.inpDate.value = new Date().toISOString().split('T')[0];
    }
    if (el.inpWeight && el.txtCurrentWeight && !el.inpWeight.value) {
      const match = el.txtCurrentWeight.textContent.match(/[\d\.]+/);
      if (match) el.inpWeight.value = match[0];
    }

    if (el.modal) el.modal.style.display = 'flex';
  }

  close() {
    const el = this.elements;
    if (el.modal) el.modal.style.display = 'none';
  }

  switchTab(targetTab) {
    document.querySelectorAll('.progress-tab-btn').forEach(b => {
      if (b.dataset.tab === targetTab) b.classList.add('active');
      else b.classList.remove('active');
    });

    document.querySelectorAll('.progress-tab-pane').forEach(p => {
      if (p.id === targetTab) p.style.display = 'block';
      else p.style.display = 'none';
    });
  }

  async refresh() {
    const user = this._authService?.currentUser;
    const isCompareAllowed = user?.entitlements?.allowPhotoCompare ?? this._authService?.isAdmin() ?? true;
    const faceCard = document.getElementById('face-progress-card');
    const faceGrid = faceCard?.querySelector('.face-comparison-grid');
    let lockedOverlay = document.getElementById('face-progress-locked-overlay');

    if (!isCompareAllowed) {
      if (faceGrid) faceGrid.style.display = 'none';
      if (!lockedOverlay && faceCard) {
        lockedOverlay = document.createElement('div');
        lockedOverlay.id = 'face-progress-locked-overlay';
        lockedOverlay.style.cssText = 'padding: 2.5rem 1rem; text-align: center; background: rgba(18, 22, 28, 0.7); border-radius: 12px; border: 1px dashed rgba(255, 255, 255, 0.1); margin-top: 1rem;';
        lockedOverlay.innerHTML = `
          <div style="font-size: 2rem; margin-bottom: 0.5rem;">🔒</div>
          <div style="font-weight: 600; font-size: 1.05rem; margin-bottom: 0.25rem;">Visual Photo Comparison is a Premium Feature</div>
          <div style="font-size: 0.85rem; color: var(--text-muted); max-width: 420px; margin: 0 auto 1.25rem;">Track facial slimming, non-scale victories, and side-by-side milestone transformation with automated weight delta calculations.</div>
          <button type="button" class="btn btn-sm btn-primary" onclick="openQuotaModal()">⚡ Upgrade to Premium</button>
        `;
        faceCard.appendChild(lockedOverlay);
      } else if (lockedOverlay) {
        lockedOverlay.style.display = 'block';
      }
      return;
    }

    if (faceGrid) faceGrid.style.display = 'grid';
    if (lockedOverlay) lockedOverlay.style.display = 'none';

    try {
      const data = await this._progress.getComparison(this._state.userId);
      if (!data) return;

      const el = this.elements;
      const bf = data.baselineFace;
      const cf = data.currentFace;
      const bb = data.baselineFullBody;
      const cb = data.currentFullBody;

      const formatDate = (iso) => {
        if (!iso) return '';
        const d = new Date(iso);
        return d.toLocaleDateString(undefined, { month: 'short', day: 'numeric' });
      };

      // Dashboard Face Card
      if (bf && el.imgBaselineFace) {
        el.imgBaselineFace.src = bf.photoUri;
        if (el.txtBaselineDate) el.txtBaselineDate.textContent = `Baseline (${formatDate(bf.capturedAtUtc)})`;
        if (el.txtBaselineWeight) el.txtBaselineWeight.textContent = `${bf.weightKg.toFixed(1)} kg`;
      }

      if (cf && el.imgCurrentFace) {
        el.imgCurrentFace.src = cf.photoUri;
        if (el.txtCurrentDate) el.txtCurrentDate.textContent = `Latest (${formatDate(cf.capturedAtUtc)})`;
        if (el.txtCurrentWeight) {
          const deltaStr = data.weightDeltaKg < 0 ? `▼ ${Math.abs(data.weightDeltaKg)} kg` : `+${data.weightDeltaKg} kg`;
          el.txtCurrentWeight.textContent = `${cf.weightKg.toFixed(1)} kg (${deltaStr})`;
        }
      }

      if (el.txtDaysElapsed) el.txtDaysElapsed.textContent = `${data.daysElapsed} Days`;
      if (el.txtNetLoss) {
        el.txtNetLoss.textContent = data.weightDeltaKg < 0 ? `▼ ${Math.abs(data.weightDeltaKg)} kg` : `${data.weightDeltaKg} kg`;
      }
      if (el.faceDeltaPill) {
        const deltaText = data.weightDeltaKg < 0 ? `▼ ${Math.abs(data.weightDeltaKg)} kg in ${data.daysElapsed} Days` : `${data.weightDeltaKg} kg`;
        el.faceDeltaPill.textContent = deltaText;
      }
      if (el.modalDeltaPill) {
        const deltaText = data.weightDeltaKg < 0 ? `▼ ${Math.abs(data.weightDeltaKg)} kg in ${data.daysElapsed} Days` : `${data.weightDeltaKg} kg`;
        el.modalDeltaPill.textContent = deltaText;
      }

      // Modal Detail Face
      if (bf && el.detailBaselineFaceImg) {
        el.detailBaselineFaceImg.src = bf.photoUri;
        if (el.detailBaselineFaceDate) el.detailBaselineFaceDate.textContent = `Day 1 (${formatDate(bf.capturedAtUtc)})`;
        if (el.detailBaselineFaceWeight) el.detailBaselineFaceWeight.textContent = `${bf.weightKg.toFixed(1)} kg`;
      }
      if (cf && el.detailCurrentFaceImg) {
        el.detailCurrentFaceImg.src = cf.photoUri;
        if (el.detailCurrentFaceDate) el.detailCurrentFaceDate.textContent = `Day ${data.daysElapsed} (${formatDate(cf.capturedAtUtc)})`;
        if (el.detailCurrentFaceWeight) el.detailCurrentFaceWeight.textContent = `${cf.weightKg.toFixed(1)} kg (▼ ${Math.abs(data.weightDeltaKg)} kg)`;
      }

      // Modal Detail Full Body
      if (bb && el.detailBaselineBodyImg) {
        el.detailBaselineBodyImg.src = bb.photoUri;
        if (el.detailBaselineBodyDate) el.detailBaselineBodyDate.textContent = `Day 1 (${formatDate(bb.capturedAtUtc)})`;
        if (el.detailBaselineBodyWeight) el.detailBaselineBodyWeight.textContent = `${bb.weightKg.toFixed(1)} kg`;
      }
      if (cb && el.detailCurrentBodyImg) {
        el.detailCurrentBodyImg.src = cb.photoUri;
        if (el.detailCurrentBodyDate) el.detailCurrentBodyDate.textContent = `Day ${data.daysElapsed} (${formatDate(cb.capturedAtUtc)})`;
        if (el.detailCurrentBodyWeight) el.detailCurrentBodyWeight.textContent = `${cb.weightKg.toFixed(1)} kg (▼ ${Math.abs(data.weightDeltaKg)} kg)`;
      }

      // Timeline Gallery
      this.renderTimelineGallery(data.allPhotos || []);
    } catch (err) {
      console.error('[ProgressModalController] Failed to refresh:', err);
    }
  }

  renderTimelineGallery(photos) {
    const el = this.elements;
    if (!el.timelineGalleryGrid) return;
    if (photos.length === 0) {
      el.timelineGalleryGrid.innerHTML = '<div style="color: var(--text-muted); font-size: 0.85rem; padding: 1rem;">No progress photos captured yet.</div>';
      return;
    }

    const typeLabels = ['Face Photo', 'Full Body (Front)', 'Full Body (Side)'];
    el.timelineGalleryGrid.innerHTML = photos.map(p => {
      const d = new Date(p.capturedAtUtc);
      const dateStr = d.toLocaleDateString(undefined, { month: 'short', day: 'numeric', year: 'numeric' });
      const tagClass = p.isBaseline ? 'baseline-badge' : 'current-badge';
      const tagLabel = p.isBaseline ? 'Baseline' : 'Check-In';

      return `
        <div class="timeline-card">
          <div class="timeline-thumb-frame">
            <img src="${p.photoUri}" alt="Progress photo" class="timeline-thumb" onerror="this.onerror=null; this.src='/assets/placeholder-progress.svg';">
            <div class="photo-meta-overlay" style="padding: 6px 8px;">
              <span class="weight-tag" style="font-size: 0.72rem;">${p.weightKg.toFixed(1)} kg</span>
              <span class="compare-badge ${tagClass}" style="font-size: 0.65rem; padding: 1px 5px;">${tagLabel}</span>
            </div>
          </div>
          <div class="timeline-info">
            <div class="timeline-date">${dateStr} · ${typeLabels[p.photoType] || 'Photo'}</div>
            ${p.notes ? `<div style="font-size: 0.72rem; color: var(--text-muted); line-height: 1.3;">${p.notes}</div>` : ''}
          </div>
        </div>
      `;
    }).join('');
  }

  async handleSubmit(e) {
    e.preventDefault();
    const el = this.elements;
    const fileInput = el.fileInput;
    if (!fileInput || !fileInput.files || fileInput.files.length === 0) {
      this._toast.show({ title: 'Photo Required', message: 'Please select or capture a photo before submitting.' });
      return;
    }

    const formData = new FormData();
    formData.append('image', fileInput.files[0]);
    formData.append('userId', this._state.userId);
    formData.append('weightKg', parseFloat(el.inpWeight?.value) || 80.0);
    formData.append('photoType', parseInt(el.inpType?.value) || 0);
    formData.append('isBaseline', el.chkBaseline ? el.chkBaseline.checked : false);
    formData.append('capturedDate', el.inpDate?.value || new Date().toISOString().split('T')[0]);
    formData.append('notes', el.inpNotes ? el.inpNotes.value.trim() : '');

    const btnSubmit = document.getElementById('btn-submit-progress-upload');
    if (btnSubmit) {
      btnSubmit.disabled = true;
      btnSubmit.textContent = 'Saving Check-In...';
    }

    try {
      const data = await this._progress.uploadPhoto(formData);

      if (btnSubmit) {
        btnSubmit.disabled = false;
        btnSubmit.textContent = 'Save Progress Check-In 🎉';
      }

      this._toast.show({ title: 'Progress Check-In Saved! 🎉', message: data.message });
      this._confetti.burst();

      // Reset form
      if (el.formUpload) el.formUpload.reset();
      if (el.dropzonePrompt) el.dropzonePrompt.style.display = 'block';
      if (el.dropzonePreview) el.dropzonePreview.style.display = 'none';

      await this.refresh();
      this.switchTab('pane-timeline-gallery');

      this._bus.emit('progress:saved', data);
    } catch (err) {
      if (btnSubmit) {
        btnSubmit.disabled = false;
        btnSubmit.textContent = 'Save Progress Check-In 🎉';
      }
      this._toast.show({ title: 'Upload Failed', message: err.message || 'Could not save progress photo.' });
    }
  }
}
