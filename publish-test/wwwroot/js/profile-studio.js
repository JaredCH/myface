(() => {
  const root = document.getElementById('profileStudioApp');
  if (!root) {
    return;
  }

  const username = root.dataset.username || '';
  const snapshotPayload = readJsonScript('profileStudioSnapshot');
  const initialTokens = readJsonScript('profileStudioThemeTokens') ?? {};

  const TemplateIds = Object.freeze({
    Minimal: 0,
    Expanded: 1,
    Pro: 2,
    Vendor: 3,
    Guru: 4,
    CustomHtml: 5
  });

  const TemplateLabels = {
    0: 'Minimal',
    1: 'Expanded',
    2: 'Pro',
    3: 'Vendor',
    4: 'Guru',
    5: 'Custom HTML'
  };

  const PanelTypeLabels = {
    0: 'About',
    1: 'Skills',
    2: 'Projects',
    3: 'Contact',
    4: 'Activity',
    5: 'Testimonials',
    6: 'Shop',
    7: 'Policies',
    8: 'Payments',
    9: 'References',
    10: 'Summary',
    11: 'Custom Block 1',
    12: 'Custom Block 2',
    13: 'Custom Block 3',
    14: 'Custom Block 4'
  };

  const TokenToCssVar = {
    bg: '--profile-bg',
    text: '--text-main',
    muted: '--text-muted',
    accent: '--accent',
    border: '--panel-border',
    panel: '--panel-surface',
    buttonbg: '--btn-bg',
    buttontext: '--btn-text',
    buttonborder: '--btn-border'
  };

  const API = {
    settings: '/profile/settings',
    selectTemplate: '/profile/template/select',
    applyTheme: '/profile/theme/apply',
    panelTypes: (template) => `/profile/template/${template}/panels`,
    createPanel: '/profile/panel/create',
    updatePanel: (id) => `/profile/panel/${id}`,
    deletePanel: (id) => `/profile/panel/${id}`,
    togglePanel: (id) => `/profile/panel/${id}/toggle`,
    reorderPanel: (id) => `/profile/panel/${id}/reorder`,
    customHtmlStatus: '/profile/custom-html/status',
    customHtmlUpload: '/profile/custom-html/upload',
    customHtmlDelete: '/profile/custom-html'
  };

  const state = {
    username,
    snapshot: normalizeSnapshot(snapshotPayload),
    panels: [],
    selectedPanelId: null,
    panelTypes: [],
    customHtmlStatus: null,
    cssTokens: { ...initialTokens }
  };

  const dom = {
    templateCards: Array.from(root.querySelectorAll('[data-template-option]')),
    templateHint: root.querySelector('[data-template-current]'),
    templateHintContainer: root.querySelector('[data-template-hint]'),
    panelList: document.getElementById('panelList'),
    panelFilter: document.getElementById('panelFilter'),
    panelTypeSelect: document.getElementById('panelTypeSelect'),
    panelCreateContent: document.getElementById('panelCreateContent'),
    panelCreateButton: root.querySelector('[data-action="create-panel"]'),
    panelEditor: document.getElementById('panelEditor'),
    panelContent: document.getElementById('panelContent'),
    panelFormat: document.getElementById('panelFormat'),
    panelTitle: root.querySelector('[data-panel-title]'),
    panelMeta: root.querySelector('[data-panel-meta]'),
    panelButtons: {
      toggle: root.querySelector('[data-action="toggle-panel"]'),
      reorderUp: root.querySelector('[data-action="reorder-up"]'),
      reorderDown: root.querySelector('[data-action="reorder-down"]'),
      save: root.querySelector('[data-action="save-panel"]'),
      delete: root.querySelector('[data-action="delete-panel"]')
    },
    refreshButton: root.querySelector('[data-action="refresh"]'),
    themeForm: document.getElementById('themeForm'),
    htmlForm: document.getElementById('htmlUploadForm'),
    htmlStatus: document.getElementById('customHtmlStatus'),
    htmlButtons: {
      refresh: root.querySelector('[data-action="html-refresh"]'),
      preview: root.querySelector('[data-action="html-preview"]'),
      delete: root.querySelector('[data-action="html-delete"]')
    },
    toast: document.getElementById('studioToast'),
    previewOpen: root.querySelector('[data-action="studio-preview"]'),
    previewOverlay: document.getElementById('studioPreview'),
    previewFrame: document.getElementById('studioPreviewFrame'),
    previewButtons: {
      close: document.querySelector('[data-preview-action="close"]'),
      refresh: document.querySelector('[data-preview-action="refresh"]')
    }
  };

  state.panels = sortPanels(state.snapshot.panels);
  applyCssTokens(state.cssTokens);
  hydrateTemplateCards();
  bindEvents();
  renderPanelList();
  updatePanelEditor(null);
  hydrateThemeForm();
  loadPanelTypes(state.snapshot.settings.templateType);
  refreshCustomHtmlStatus();
  updateTemplateHint();

  function readJsonScript(id) {
    const el = document.getElementById(id);
    if (!el) {
      return null;
    }

    try {
      const payload = el.textContent?.trim();
      return payload ? JSON.parse(payload) : null;
    } catch {
      return null;
    }
  }

  function normalizeSnapshot(raw) {
    if (!raw || typeof raw !== 'object') {
      return {
        settings: { templateType: TemplateIds.Minimal },
        panels: [],
        theme: { overrides: {}, preset: null }
      };
    }

    const settings = raw.settings ?? {};
    return {
      settings,
      panels: Array.isArray(raw.panels) ? raw.panels : [],
      theme: raw.theme ?? { overrides: {}, preset: null }
    };
  }

  function sortPanels(panels) {
    return [...(panels ?? [])].sort((a, b) => a.position - b.position);
  }

  function bindEvents() {
    dom.refreshButton?.addEventListener('click', () => refreshSnapshot());
    dom.panelFilter?.addEventListener('change', renderPanelList);
    dom.panelList?.addEventListener('click', handlePanelListClick);
    dom.panelList?.addEventListener('pointerdown', handlePanelPointerDown);
    dom.panelCreateButton?.addEventListener('click', handleCreatePanel);
    dom.panelButtons.toggle?.addEventListener('click', () => togglePanelVisibility());
    dom.panelButtons.reorderUp?.addEventListener('click', () => reorderPanel(-1));
    dom.panelButtons.reorderDown?.addEventListener('click', () => reorderPanel(1));
    dom.panelButtons.save?.addEventListener('click', handleSavePanel);
    dom.panelButtons.delete?.addEventListener('click', handleDeletePanel);
    dom.themeForm?.addEventListener('submit', handleThemeSubmit);
    dom.themeForm?.querySelector('[data-action="reset-theme-fields"]')?.addEventListener('click', resetThemeForm);
    dom.htmlForm?.addEventListener('submit', handleHtmlUpload);
    dom.htmlButtons.refresh?.addEventListener('click', (e) => {
      e.preventDefault();
      refreshCustomHtmlStatus();
    });
    dom.htmlButtons.preview?.addEventListener('click', (e) => {
      e.preventDefault();
      window.open('/profile/custom-html/preview', '_blank', 'noopener');
    });
    dom.htmlButtons.delete?.addEventListener('click', handleHtmlDelete);
    dom.previewOpen?.addEventListener('click', openPreviewModal);
    dom.previewButtons.close?.addEventListener('click', closePreviewModal);
    dom.previewButtons.refresh?.addEventListener('click', refreshPreviewFrame);
    dom.previewOverlay?.addEventListener('click', (event) => {
      if (event.target === dom.previewOverlay) {
        closePreviewModal();
      }
    });

    window.addEventListener('pointerup', clearPanelDragState);
    window.addEventListener('pointercancel', clearPanelDragState);
  }

  function hydrateTemplateCards() {
    dom.templateCards.forEach((card) => {
      const templateValue = Number(card.dataset.templateValue);
      card.addEventListener('click', () => handleTemplateSelection(templateValue));
    });
    highlightActiveTemplate();
  }

  function highlightActiveTemplate() {
    const activeTemplate = Number(state.snapshot.settings?.templateType ?? TemplateIds.Minimal);
    dom.templateCards.forEach((card) => {
      const templateValue = Number(card.dataset.templateValue);
      const isActive = templateValue === activeTemplate;
      card.classList.toggle('is-active', isActive);
      const requireHtml = templateValue === TemplateIds.CustomHtml;
      const htmlReady = state.customHtmlStatus ? state.customHtmlStatus.hasCustomHtml : true;
      const shouldDisable = requireHtml && !htmlReady;
      card.classList.toggle('is-disabled', shouldDisable);
      card.setAttribute('aria-disabled', shouldDisable ? 'true' : 'false');
    });
  }

  function updateTemplateHint() {
    const templateValue = Number(state.snapshot.settings?.templateType ?? TemplateIds.Minimal);
    const label = TemplateLabels[templateValue] ?? 'Unknown';
    dom.templateHint && (dom.templateHint.textContent = label);
  }

  async function handleTemplateSelection(templateValue) {
    const active = Number(state.snapshot.settings?.templateType ?? TemplateIds.Minimal);
    if (templateValue === active) {
      return;
    }

    if (templateValue === TemplateIds.CustomHtml && !state.customHtmlStatus?.hasCustomHtml) {
      showToast('Upload and sanitize custom HTML before enabling custom mode.', 'error');
      return;
    }

    try {
      const snapshot = await requestJson(API.selectTemplate, {
        method: 'POST',
        body: { template: templateValue }
      });
      applySnapshot(snapshot);
      showToast(`Template switched to ${TemplateLabels[templateValue] ?? 'new layout'}.`, 'success');
    } catch (err) {
      showToast(err.message || 'Unable to switch template.', 'error');
    }
  }

  function renderPanelList() {
    if (!dom.panelList) {
      return;
    }

    const isCustomHtml = Number(state.snapshot.settings?.templateType) === TemplateIds.CustomHtml;
    dom.panelList.innerHTML = '';

    if (isCustomHtml) {
      dom.panelList.innerHTML = '<li><div class="panel-empty">Panels are hidden while custom HTML is active.</div></li>';
      togglePanelEditing(true);
      return;
    }

    const filter = dom.panelFilter?.value ?? 'visible';
    const filtered = state.panels.filter((panel) => (
      filter === 'all' ? true : panel.isVisible
    ));

    if (filtered.length === 0) {
      dom.panelList.innerHTML = '<li><div class="panel-empty">No panels yet. Use "Add panel" to create one.</div></li>';
      togglePanelEditing(false, true);
      return;
    }

    const frag = document.createDocumentFragment();
    filtered.forEach((panel) => {
      const li = document.createElement('li');
      const button = document.createElement('button');
      button.type = 'button';
      button.dataset.panelId = String(panel.id);
      button.className = `panel-card panel-list-button${panel.isVisible ? '' : ' is-hidden'}${panel.id === state.selectedPanelId ? ' active' : ''}`;
      button.innerHTML = `
        <div class="panel-list-row">
          <span class="panel-drag-hint" aria-hidden="true"></span>
          <div class="panel-list-copy">
            <strong>${PanelTypeLabels[panel.panelType] ?? panel.panelType}</strong>
            <div class="panel-meta">
              <span class="panel-position">#${panel.position}</span>
              <span class="panel-status-pill ${panel.isVisible ? 'is-visible' : 'is-hidden'}">${panel.isVisible ? 'Visible' : 'Hidden'}</span>
            </div>
          </div>
        </div>`;
      li.appendChild(button);
      frag.appendChild(li);
    });

    dom.panelList.appendChild(frag);
    togglePanelEditing(false);
  }

  function handlePanelPointerDown(event) {
    const target = event.target;
    if (!(target instanceof Element)) {
      return;
    }

    const button = target.closest('button[data-panel-id]');
    if (!button) {
      return;
    }

    button.classList.add('is-grabbing');
    root.classList.add('is-panel-dragging');
  }

  function clearPanelDragState() {
    if (!root.classList.contains('is-panel-dragging')) {
      return;
    }

    root.classList.remove('is-panel-dragging');
    dom.panelList?.querySelectorAll('.is-grabbing').forEach((btn) => btn.classList.remove('is-grabbing'));
  }

  function handlePanelListClick(event) {
    const target = event.target;
    if (!(target instanceof Element)) {
      return;
    }

    const button = target.closest('button[data-panel-id]');
    if (!button) {
      return;
    }

    const id = Number(button.dataset.panelId);
    const panel = state.panels.find((p) => p.id === id);
    if (!panel) {
      return;
    }

    state.selectedPanelId = id;
    updatePanelEditor(panel);
    renderPanelList();
  }

  function updatePanelEditor(panel) {
    const isCustomHtml = Number(state.snapshot.settings?.templateType) === TemplateIds.CustomHtml;
    if (!panel || isCustomHtml) {
      dom.panelTitle && (dom.panelTitle.textContent = isCustomHtml ? 'Custom HTML active' : 'Select a panel to edit');
      dom.panelMeta && (dom.panelMeta.textContent = '');
      dom.panelContent.disabled = true;
      dom.panelFormat.disabled = true;
      dom.panelContent.value = '';
      dom.panelFormat.value = 'markdown';
      setPanelButtonsDisabled(true);
      if (isCustomHtml) {
        dom.panelContent.placeholder = 'Panels unavailable while custom HTML is active.';
      }
      return;
    }

    dom.panelTitle && (dom.panelTitle.textContent = PanelTypeLabels[panel.panelType] ?? `Panel ${panel.id}`);
    const formatLabel = (panel.contentFormat || '').toString().toUpperCase() || 'MARKDOWN';
    dom.panelMeta && (dom.panelMeta.textContent = `Position #${panel.position} · ${formatLabel} · ${panel.isVisible ? 'Visible' : 'Hidden'}`);
    dom.panelContent.disabled = false;
    dom.panelFormat.disabled = false;
    dom.panelContent.value = panel.content;
    dom.panelFormat.value = panel.contentFormat || 'markdown';
    dom.panelContent.placeholder = '';

    if (dom.panelButtons.toggle) {
      dom.panelButtons.toggle.textContent = panel.isVisible ? 'Hide panel' : 'Show panel';
      dom.panelButtons.toggle.disabled = false;
    }
    if (dom.panelButtons.reorderUp) {
      dom.panelButtons.reorderUp.disabled = panel.position <= 1;
    }
    if (dom.panelButtons.reorderDown) {
      dom.panelButtons.reorderDown.disabled = panel.position >= state.panels.length;
    }
    if (dom.panelButtons.save) {
      dom.panelButtons.save.disabled = false;
    }
    if (dom.panelButtons.delete) {
      dom.panelButtons.delete.disabled = false;
    }
  }

  function setPanelButtonsDisabled(value) {
    Object.values(dom.panelButtons).forEach((btn) => {
      if (btn) {
        btn.disabled = value;
      }
    });
  }

  function togglePanelEditing(forceDisabled, skipClear = false) {
    const disable = Boolean(forceDisabled);
    dom.panelCreateButton && (dom.panelCreateButton.disabled = disable);
    dom.panelTypeSelect && (dom.panelTypeSelect.disabled = disable);
    dom.panelCreateContent && (dom.panelCreateContent.disabled = disable);
    if (disable && !skipClear) {
      updatePanelEditor(null);
    }
  }

  async function handleCreatePanel() {
    const template = Number(state.snapshot.settings?.templateType);
    if (template === TemplateIds.CustomHtml) {
      showToast('Panels are disabled for custom HTML.', 'error');
      return;
    }

    const panelType = Number(dom.panelTypeSelect?.value);
    if (Number.isNaN(panelType)) {
      showToast('Choose a panel type first.', 'error');
      return;
    }

    const content = (dom.panelCreateContent?.value ?? '').trim() || 'New panel content here.';

    try {
      const panel = await requestJson(API.createPanel, {
        method: 'POST',
        body: {
          panelType,
          content,
          contentFormat: 'markdown'
        }
      });
      state.panels.push(panel);
      state.panels = sortPanels(state.panels);
      if (dom.panelCreateContent) {
        dom.panelCreateContent.value = '';
      }
      renderPanelList();
      showToast('Panel created.', 'success');
    } catch (err) {
      showToast(err.message || 'Unable to create panel.', 'error');
    }
  }

  async function handleSavePanel() {
    const panel = getSelectedPanel();
    if (!panel) {
      return;
    }

    const content = dom.panelContent.value.trim();
    if (!content) {
      showToast('Content cannot be empty.', 'error');
      return;
    }

    const contentFormat = dom.panelFormat.value;

    try {
      const updated = await requestJson(API.updatePanel(panel.id), {
        method: 'PUT',
        body: { content, contentFormat }
      });
      state.panels = state.panels.map((p) => (p.id === updated.id ? updated : p));
      updatePanelEditor(updated);
      renderPanelList();
      showToast('Panel saved.', 'success');
    } catch (err) {
      showToast(err.message || 'Unable to save panel.', 'error');
    }
  }

  async function handleDeletePanel() {
    const panel = getSelectedPanel();
    if (!panel) {
      return;
    }

    if (!confirm('Delete this panel? This action cannot be undone.')) {
      return;
    }

    try {
      await requestJson(API.deletePanel(panel.id), { method: 'DELETE' });
      state.panels = state.panels.filter((p) => p.id !== panel.id);
      state.selectedPanelId = null;
      renderPanelList();
      updatePanelEditor(null);
      showToast('Panel deleted.', 'success');
    } catch (err) {
      showToast(err.message || 'Unable to delete panel.', 'error');
    }
  }

  async function togglePanelVisibility() {
    const panel = getSelectedPanel();
    if (!panel) {
      return;
    }

    try {
      const updated = await requestJson(API.togglePanel(panel.id), {
        method: 'POST',
        body: { isVisible: !panel.isVisible }
      });
      state.panels = state.panels.map((p) => (p.id === updated.id ? updated : p));
      updatePanelEditor(updated);
      renderPanelList();
      showToast(updated.isVisible ? 'Panel is now visible.' : 'Panel hidden.', 'success');
    } catch (err) {
      showToast(err.message || 'Unable to toggle panel.', 'error');
    }
  }

  async function reorderPanel(delta) {
    const panel = getSelectedPanel();
    if (!panel) {
      return;
    }

    const nextPosition = Math.max(1, panel.position + delta);

    try {
      const panels = await requestJson(API.reorderPanel(panel.id), {
        method: 'POST',
        body: { position: nextPosition }
      });
      state.panels = sortPanels(panels);
      const updated = state.panels.find((p) => p.id === panel.id) ?? null;
      updatePanelEditor(updated);
      renderPanelList();
      showToast('Panel order updated.', 'success');
    } catch (err) {
      showToast(err.message || 'Unable to reorder panel.', 'error');
    }
  }

  function getSelectedPanel() {
    if (!state.selectedPanelId) {
      showToast('Select a panel first.', 'error');
      return null;
    }

    const panel = state.panels.find((p) => p.id === state.selectedPanelId);
    if (!panel) {
      showToast('Selected panel is missing.', 'error');
      return null;
    }

    return panel;
  }

  async function loadPanelTypes(templateValue) {
    if (templateValue === TemplateIds.CustomHtml) {
      state.panelTypes = [];
      populatePanelTypeSelect();
      return;
    }

    try {
      const payload = await requestJson(API.panelTypes(templateValue));
      state.panelTypes = Array.isArray(payload) ? payload : [];
      populatePanelTypeSelect();
    } catch {
      state.panelTypes = [];
      populatePanelTypeSelect();
    }
  }

  function populatePanelTypeSelect() {
    if (!dom.panelTypeSelect) {
      return;
    }

    const template = Number(state.snapshot.settings?.templateType);
    if (template === TemplateIds.CustomHtml) {
      dom.panelTypeSelect.innerHTML = '<option value="">Custom HTML mode disabled panels</option>';
      dom.panelTypeSelect.disabled = true;
      return;
    }

    const options = state.panelTypes.map((type) => {
      const option = document.createElement('option');
      option.value = String(type);
      option.textContent = PanelTypeLabels[type] ?? `Panel ${type}`;
      return option;
    });

    dom.panelTypeSelect.innerHTML = '';
    if (options.length === 0) {
      const empty = document.createElement('option');
      empty.value = '';
      empty.textContent = 'No panel slots available for this template.';
      dom.panelTypeSelect.appendChild(empty);
      dom.panelTypeSelect.disabled = true;
      return;
    }

    options.forEach((opt) => dom.panelTypeSelect.appendChild(opt));
    dom.panelTypeSelect.disabled = false;
  }

  function hydrateThemeForm() {
    if (!dom.themeForm) {
      return;
    }

    const presetInput = dom.themeForm.querySelector('[name="preset"]');
    if (presetInput) {
      presetInput.value = state.snapshot.theme?.preset ?? '';
    }
    const overrides = state.snapshot.theme?.overrides ?? {};
    Object.entries(overrides).forEach(([key, value]) => {
      const input = dom.themeForm.querySelector(`[name="${key}"]`);
      if (input) {
        input.value = value;
      }
    });
  }

  function resetThemeForm(event) {
    event.preventDefault();
    dom.themeForm?.reset();
    hydrateThemeForm();
  }

  async function handleThemeSubmit(event) {
    event.preventDefault();
    if (!dom.themeForm) {
      return;
    }

    const formData = new FormData(dom.themeForm);
    const preset = (formData.get('preset') ?? '').toString().trim() || null;
    const overrides = {};
    let hasOverrides = false;
    for (const [key, value] of formData.entries()) {
      if (key === 'preset') {
        continue;
      }

      const trimmed = value.toString().trim();
      if (trimmed) {
        overrides[key] = trimmed;
        hasOverrides = true;
      }
    }

    try {
      const snapshot = await requestJson(API.applyTheme, {
        method: 'POST',
        body: { preset, overrides: hasOverrides ? overrides : null }
      });
      applySnapshot(snapshot);
      showToast('Theme updated.', 'success');
    } catch (err) {
      showToast(err.message || 'Unable to update theme.', 'error');
    }
  }

  async function refreshSnapshot() {
    try {
      const snapshot = await requestJson(API.settings);
      applySnapshot(snapshot);
      showToast('Snapshot refreshed.', 'success');
    } catch (err) {
      showToast(err.message || 'Unable to refresh snapshot.', 'error');
    }
  }

  function applySnapshot(snapshot) {
    const previousTemplate = Number(state.snapshot?.settings?.templateType ?? TemplateIds.Minimal);
    state.snapshot = normalizeSnapshot(snapshot);
    state.panels = sortPanels(state.snapshot.panels);
    const stillExists = state.panels.some((p) => p.id === state.selectedPanelId);
    if (!stillExists) {
      state.selectedPanelId = null;
    }
    hydrateThemeForm();
    renderPanelList();
    updatePanelEditor(state.panels.find((p) => p.id === state.selectedPanelId) ?? null);
    updateTemplateHint();
    highlightActiveTemplate();
    applyCssOverrides(state.snapshot.theme?.overrides ?? {});
    const currentTemplate = Number(state.snapshot.settings?.templateType ?? TemplateIds.Minimal);
    if (currentTemplate !== previousTemplate) {
      loadPanelTypes(currentTemplate);
    }
  }

  function applyCssTokens(tokens) {
    if (!tokens) {
      return;
    }

    Object.entries(tokens).forEach(([key, value]) => {
      root.style.setProperty(key, value);
    });
  }

  function applyCssOverrides(overrides) {
    if (!overrides) {
      return;
    }

    Object.entries(overrides).forEach(([key, value]) => {
      const cssVar = TokenToCssVar[key.toLowerCase()];
      if (cssVar && value) {
        state.cssTokens[cssVar] = value;
        root.style.setProperty(cssVar, value);
      }
    });
  }

  function openPreviewModal() {
    if (!dom.previewOverlay || !dom.previewFrame) {
      showToast('Preview unavailable.', 'error');
      return;
    }

    const url = buildPreviewUrl();
    if (!url) {
      showToast('Missing username for preview.', 'error');
      return;
    }

    dom.previewFrame.src = url;
    dom.previewOverlay.hidden = false;
    requestAnimationFrame(() => dom.previewOverlay.classList.add('is-visible'));
  }

  function closePreviewModal(event) {
    if (event) {
      event.preventDefault();
    }

    if (!dom.previewOverlay || !dom.previewFrame) {
      return;
    }

    dom.previewOverlay.classList.remove('is-visible');
    setTimeout(() => {
      if (!dom.previewOverlay.classList.contains('is-visible')) {
        dom.previewOverlay.hidden = true;
        dom.previewFrame.src = 'about:blank';
      }
    }, 220);
  }

  function refreshPreviewFrame(event) {
    if (event) {
      event.preventDefault();
    }

    if (!dom.previewFrame) {
      return;
    }

    const url = buildPreviewUrl();
    if (url) {
      dom.previewFrame.src = url;
    }
  }

  function buildPreviewUrl() {
    if (!state.username) {
      return null;
    }

    const encoded = encodeURIComponent(state.username);
    const cacheBust = Date.now();
    return `/u/${encoded}?studio_preview=${cacheBust}`;
  }

  async function refreshCustomHtmlStatus() {
    try {
      const status = await requestJson(API.customHtmlStatus);
      state.customHtmlStatus = status;
      renderCustomHtmlStatus(status);
      highlightActiveTemplate();
    } catch (err) {
      showToast(err.message || 'Unable to load custom HTML status.', 'error');
    }
  }

  function renderCustomHtmlStatus(status) {
    if (!dom.htmlStatus) {
      return;
    }

    if (!status) {
      dom.htmlStatus.innerHTML = '<p>Unable to load custom HTML status.</p>';
      return;
    }

    const uploadedAt = status.uploadedAt ? new Date(status.uploadedAt).toLocaleString() : 'Never';
    const fileState = status.hasCustomHtml ? 'File uploaded' : 'No file';
    const templateState = status.templateActive ? 'Template active' : 'Template inactive';
    const validation = status.validationPassed ? 'Validation passed' : 'Validation failed';

    const errors = (status.validationErrors ?? []).map((err) => `<li>${escapeHtml(err)}</li>`).join('');
    const errorBlock = errors ? `<div><strong>Errors</strong><ul>${errors}</ul></div>` : '';

    dom.htmlStatus.innerHTML = `
      <p><strong>${fileState}</strong> · ${templateState} · ${validation}</p>
      <p>Version ${status.version} · Uploaded at ${uploadedAt}</p>
      ${status.relativePath ? `<p>Storage path: ${escapeHtml(status.relativePath)}</p>` : ''}
      ${errorBlock}`;
  }

  async function handleHtmlUpload(event) {
    event.preventDefault();
    if (!dom.htmlForm) {
      return;
    }

    const formData = new FormData(dom.htmlForm);
    if (!formData.get('htmlFile')) {
      showToast('Choose a .html file before uploading.', 'error');
      return;
    }

    try {
      const response = await fetch(API.customHtmlUpload, {
        method: 'POST',
        body: formData,
        credentials: 'same-origin'
      });
      let payload = null;
      try {
        payload = await response.json();
      } catch {
        showToast('Upload failed: unexpected response.', 'error');
        return;
      }
      if (!response.ok || !payload.success) {
        renderCustomHtmlStatus(payload.status);
        showToast(payload.errors?.[0] || 'Upload failed.', 'error');
        return;
      }

      state.customHtmlStatus = payload.status;
      renderCustomHtmlStatus(payload.status);
      highlightActiveTemplate();
      showToast('Custom HTML uploaded and sanitized.', 'success');
    } catch (err) {
      showToast(err.message || 'Unable to upload HTML.', 'error');
    }
  }

  async function handleHtmlDelete(event) {
    event.preventDefault();
    if (!confirm('Remove custom HTML? This cannot be undone.')) {
      return;
    }

    try {
      const status = await requestJson(API.customHtmlDelete, { method: 'DELETE' });
      state.customHtmlStatus = status;
      renderCustomHtmlStatus(status);
      highlightActiveTemplate();
      showToast('Custom HTML removed.', 'success');
    } catch (err) {
      showToast(err.message || 'Unable to remove HTML.', 'error');
    }
  }

  async function requestJson(url, options = {}) {
    const { method = 'GET', body } = options;
    const headers = { Accept: 'application/json', ...(options.headers ?? {}) };
    const fetchOptions = {
      method,
      credentials: 'same-origin',
      headers
    };

    if (body instanceof FormData) {
      fetchOptions.body = body;
    } else if (body !== undefined) {
      headers['Content-Type'] = 'application/json';
      fetchOptions.body = JSON.stringify(body);
    }

    const response = await fetch(url, fetchOptions);
    const text = await response.text();
    let data = null;
    if (text) {
      try {
        data = JSON.parse(text);
      } catch {
        throw new Error('Invalid server response.');
      }
    }

    if (!response.ok) {
      const message = data?.error || data?.errors?.[0] || 'Request failed.';
      throw new Error(message);
    }

    return data;
  }

  function showToast(message, variant = 'info') {
    if (!dom.toast || !message) {
      return;
    }

    dom.toast.textContent = message;
    dom.toast.classList.remove('success', 'error');
    if (variant !== 'info') {
      dom.toast.classList.add(variant);
    }

    dom.toast.classList.add('is-visible');
    setTimeout(() => dom.toast.classList.remove('is-visible'), 2800);
  }

  function escapeHtml(value) {
    const text = value == null ? '' : String(value);
    return text
      .replace(/&/g, '&amp;')
      .replace(/</g, '&lt;')
      .replace(/>/g, '&gt;');
  }
})();
