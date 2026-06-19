/* OPERAX — Pure JS · Uygulama (router + state + event delegation) */
(function () {
  const I = window.ICONS;
  const SCREENS = window.SCREENS;
  const SHELL = window.SHELL;
  const { html, raw, esc } = window.UI;

  // ---------- Global state ----------
  const state = {
    route: 'dashboard',
    tweaks: { emptyState: false, accent: 'indigo' },
    local: {}, // Per-screen local state (reset on route change)
    formPO: null, formPOErrors: {},
    formSO: null, formSOErrors: {},
    formUser: null, formUserErrors: {},
    tweaksPanelOpen: false,
  };

  // ---------- Accent theming ----------
  const ACCENTS = [
    { key: 'indigo', b1: '243 75% 59%', b2: '263 70% 55%' },
    { key: 'violet', b1: '263 70% 55%', b2: '292 75% 55%' },
    { key: 'teal',   b1: '173 80% 36%', b2: '195 80% 42%' },
    { key: 'amber',  b1: '32 95% 50%',  b2: '15 92% 52%' },
  ];
  const applyAccent = () => {
    const a = ACCENTS.find((x) => x.key === state.tweaks.accent) || ACCENTS[0];
    const [hue, sat] = a.b1.split(' ');
    const r = document.documentElement.style;
    r.setProperty('--brand-500', `hsl(${a.b1})`);
    r.setProperty('--brand-600', `hsl(${hue} ${sat} 52%)`);
    r.setProperty('--brand-400', `hsl(${hue} ${sat} 68%)`);
    r.setProperty('--brand-300', `hsl(${hue} ${sat} 78%)`);
    r.setProperty('--brand-tint-15', `hsl(${a.b1} / 0.15)`);
    r.setProperty('--brand-tint-08', `hsl(${a.b1} / 0.08)`);
    r.setProperty('--brand-glow', `hsl(${a.b1} / 0.22)`);
    r.setProperty('--brand-grad', `linear-gradient(135deg, hsl(${a.b1}), hsl(${a.b2}))`);
    r.setProperty('--brand-grad-soft', `linear-gradient(135deg, hsl(${a.b1} / 0.10), hsl(${a.b2} / 0.10))`);
    r.setProperty('--shadow-brand', `0 8px 24px hsl(${a.b1} / 0.28), 0 2px 6px hsl(${a.b1} / 0.18)`);
  };

  // ---------- Router ----------
  const nav = (route) => {
    if (state.route !== route) state.local = {};
    state.route = route;
    location.hash = '#/' + route;
    window.scrollTo({ top: 0 });
    render();
  };
  const parseHash = () => {
    const h = location.hash || '#/dashboard';
    return h.replace(/^#\/?/, '') || 'dashboard';
  };

  // ---------- Render ----------
  const renderScreen = () => {
    const r = state.route;
    const parts = r.split('/');
    if (r === 'dashboard') return SCREENS.Dashboard(state);
    if (r === 'purchasing') return SCREENS.PurchasingList(state);
    if (parts[0] === 'purchasing' && parts[1] === 'detail') return SCREENS.PurchasingDetail(state, parts[2]);
    if (r === 'purchasing/new') return SCREENS.PurchasingNew(state);
    if (r === 'sales') return SCREENS.SalesList(state);
    if (parts[0] === 'sales' && parts[1] === 'detail') return SCREENS.SalesDetail(state, parts[2]);
    if (r === 'sales/new') return SCREENS.SalesNew(state);
    if (r === 'wms') return SCREENS.WmsScreen(state);
    if (r === 'inventory') return SCREENS.InventoryList(state);
    if (r === 'stok/hareket') return SCREENS.StokHareket(state);
    if (parts[0] === 'stok' && parts[1] === 'kart') return SCREENS.StokKartiDetail(state, parts[2]);
    if (r === 'production') return SCREENS.ProductionList(state);
    if (parts[0] === 'production' && parts[1] === 'detail') return SCREENS.ProductionDetail(state, parts[2]);
    if (r === 'cari') return SCREENS.CariList(state);
    if (parts[0] === 'cari' && parts[1] === 'detail') return SCREENS.CariDetail(state, parts[2]);
    if (r === 'accounting') return SCREENS.AccountingList(state);
    if (parts[0] === 'accounting' && parts[1] === 'detail') return SCREENS.AccountingDetail(state, parts[2]);
    if (r === 'cashbank') return SCREENS.CashBank(state);
    if (r === 'reports') return SCREENS.ReportsScreen(state);
    if (parts[0] === 'reports' && parts[1] === 'view') return SCREENS.ReportView(state, parts[2]);
    if (r === 'users') return SCREENS.UsersScreen(state);
    if (r === 'users/new') return SCREENS.UserNew(state);
    if (r === 'settings') return SCREENS.SettingsScreen(state);
    return SCREENS.Dashboard(state);
  };

  const renderChrome = () => {
    document.getElementById('cmdk-root').innerHTML = SHELL.renderCmdk();
  };

  const render = () => {
    const screenHtml = renderScreen();
    const main = `
      <div class="app">
        ${SHELL.renderSidebar(state.route)}
        <div class="main">
          ${SHELL.renderTopbar()}
          ${screenHtml.__raw || screenHtml}
        </div>
      </div>
    `;
    document.getElementById('app-root').innerHTML = main;
    renderChrome();
    renderTweaksPanel();
  };

  // ---------- Tweaks panel ----------
  const renderTweaksPanel = () => {
    const host = document.getElementById('tweaks-root');
    if (!state.tweaksPanelOpen) { host.innerHTML = ''; return; }
    const accentHero = (ACCENTS.find((a) => a.key === state.tweaks.accent) || ACCENTS[0]);
    const navItems = [
      { r: 'dashboard', l: 'Anasayfa' },
      { r: 'purchasing', l: 'Satınalma Listesi' },
      { r: 'purchasing/detail/PO-2026-00041', l: 'PO Detay (Onaylı)' },
      { r: 'purchasing/detail/PO-2026-00037', l: 'PO Detay (İptal)' },
      { r: 'purchasing/new', l: 'Yeni PO' },
      { r: 'sales', l: 'Satış Siparişleri' },
      { r: 'sales/detail/SO-2026-00128', l: 'Satış Detayı' },
      { r: 'cari', l: 'Cari Kartlar' },
      { r: 'cari/detail/TKB', l: 'Cari (Tedarikçi)' },
      { r: 'cari/detail/BRA', l: 'Cari (Müşteri)' },
      { r: 'inventory', l: 'Envanter Listesi' },
      { r: 'stok/kart/PR-0103', l: 'Stok Kartı' },
      { r: 'stok/hareket', l: 'Stok Hareketleri' },
      { r: 'wms', l: 'Depo / WMS' },
      { r: 'production', l: 'Üretim İş Emirleri' },
      { r: 'production/detail/WO-2026-0248', l: 'İş Emri Detayı' },
      { r: 'accounting', l: 'Yevmiye Fişleri' },
      { r: 'accounting/detail/AF-2026-00940', l: 'Yevmiye Detayı' },
      { r: 'cashbank', l: 'Kasa / Banka' },
      { r: 'reports', l: 'Rapor Merkezi' },
      { r: 'reports/view/po-perf', l: 'Örnek Rapor' },
      { r: 'users', l: 'Kullanıcılar' },
      { r: 'users/new', l: 'Yeni Kullanıcı' },
      { r: 'settings', l: 'Ayarlar' },
    ];
    host.innerHTML = `
      <div class="tweaks-panel" style="position:fixed;right:18px;bottom:18px;width:300px;background:#fff;border:1px solid var(--border);border-radius:12px;box-shadow:var(--shadow-lg);z-index:1000;max-height:calc(100vh - 36px);display:flex;flex-direction:column">
        <div style="display:flex;align-items:center;gap:8px;padding:12px 14px;border-bottom:1px solid var(--border)">
          <div style="font-size:13px;font-weight:700">Tweaks</div>
          <button class="icon-btn" style="margin-left:auto;width:24px;height:24px" data-action="tweaks-close">${I.X(13, 2)}</button>
        </div>
        <div style="padding:14px;overflow:auto;display:flex;flex-direction:column;gap:14px">
          <div>
            <div class="form-label" style="margin-bottom:8px">İçerik Durumu</div>
            <label style="display:flex;align-items:center;gap:8px;cursor:pointer">
              <label class="switch"><input type="checkbox" ${state.tweaks.emptyState ? 'checked' : ''} data-action="tweak-empty" /><span class="switch-track"></span></label>
              <span style="font-size:12px">Empty state (boş)</span>
            </label>
          </div>
          <div>
            <div class="form-label" style="margin-bottom:8px">Marka Rengi</div>
            <div style="display:flex;gap:8px">
              ${ACCENTS.map((a) => `<button data-action="tweak-accent" data-accent="${esc(a.key)}" style="width:32px;height:32px;border-radius:8px;background:linear-gradient(135deg, hsl(${a.b1}), hsl(${a.b2}));border:2px solid ${state.tweaks.accent === a.key ? 'var(--text)' : 'transparent'};cursor:pointer;padding:0"></button>`).join('')}
            </div>
          </div>
          <div>
            <div class="form-label" style="margin-bottom:8px">Hızlı Gezinme</div>
            <div style="display:flex;flex-direction:column;gap:4px;max-height:280px;overflow:auto">
              ${navItems.map((n) => `<button class="btn btn-secondary btn-xs" style="justify-content:flex-start;width:100%;height:28px" data-action="nav" data-route="${esc(n.r)}">${esc(n.l)}</button>`).join('')}
            </div>
          </div>
        </div>
      </div>
    `;
  };

  // ---------- Validation helpers ----------
  const validatePO = (f) => {
    const e = {};
    if (!f.supplier) e.supplier = 'Tedarikçi seçimi zorunludur';
    if (!f.refCode || f.refCode.length < 3) e.refCode = 'Referans kodu en az 3 karakter olmalı';
    if (!f.dueDate) e.dueDate = 'Vade tarihi zorunludur';
    else if (new Date(f.dueDate) < new Date(f.docDate)) e.dueDate = 'Vade tarihi evrak tarihinden sonra olmalı';
    return e;
  };
  const validateSO = (f) => {
    const e = {};
    if (!f.customer) e.customer = 'Müşteri seçimi zorunludur';
    if (!f.dueDate) e.dueDate = 'Vade tarihi zorunludur';
    else if (new Date(f.dueDate) < new Date(f.docDate)) e.dueDate = 'Vade tarihi evrak tarihinden sonra olmalı';
    return e;
  };
  const validateUser = (f) => {
    const e = {};
    if (!f.firstName || f.firstName.length < 2) e.firstName = 'Ad en az 2 karakter olmalı';
    if (!f.lastName || f.lastName.length < 2) e.lastName = 'Soyad en az 2 karakter olmalı';
    if (!f.email) e.email = 'E-posta zorunludur';
    else if (!/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(f.email)) e.email = 'Geçerli bir e-posta girin';
    if (!f.role) e.role = 'Rol seçimi zorunludur';
    return e;
  };

  // ---------- Event delegation ----------
  document.addEventListener('click', (e) => {
    const tweaksOpen = document.querySelector('.tweaks-panel');
    // Stop on internal cmdk content
    if (e.target.closest('[data-stop]')) {
      const target = e.target.closest('[data-action]');
      if (!target) return;
    }
    const t = e.target.closest('[data-action]');
    if (!t) return;
    const action = t.dataset.action;

    if (action === 'nav') {
      const r = t.dataset.route;
      if (r) {
        if (tweaksOpen && t.closest('.tweaks-panel')) { /* keep panel open */ }
        SHELL.closeCmdk();
        nav(r);
      }
      return;
    }
    if (action === 'open-cmdk') { SHELL.openCmdk(); return; }
    if (action === 'cmdk-close') { SHELL.closeCmdk(); return; }
    if (action === 'cmdk-pick') { SHELL.cmdkPick(parseInt(t.dataset.idx, 10)); return; }

    // Per-screen tabs/filters
    const tabActions = {
      'po-tab': () => { state.local.tab = t.dataset.tab; render(); },
      'sales-tab': () => { state.local.tab = t.dataset.tab; render(); },
      'wms-tab': () => { state.local.tab = t.dataset.tab; render(); },
      'wms-zone': () => { state.local.zone = t.dataset.zone; render(); },
      'cari-tab': () => { state.local.typeFilter = t.dataset.tab; render(); },
      'cari-detail-tab': () => { state.local.tab = t.dataset.tab; render(); },
      'stok-tab': () => { state.local.tab = t.dataset.tab; render(); },
      'stokh-tab': () => { state.local.typeFilter = t.dataset.tab; render(); },
      'prod-tab': () => { state.local.tab = t.dataset.tab; render(); },
      'acct-tab': () => { state.local.tab = t.dataset.tab; render(); },
      'users-tab': () => { state.local.filter = t.dataset.tab; render(); },
      'inv-cat': () => { state.local.catFilter = t.dataset.cat; render(); },
      'cb-acc': () => { state.local.account = t.dataset.acc; render(); },
      'settings-sec': () => { state.local.section = t.dataset.sec; render(); },
    };
    if (tabActions[action]) { tabActions[action](); return; }

    // Form submits
    if (action === 'po-submit') {
      const errs = validatePO(state.formPO || {});
      if (Object.keys(errs).length) {
        state.formPOErrors = errs;
        SHELL.pushToast({ kind: 'danger', msg: 'Lütfen formdaki hataları düzeltin' });
        render(); return;
      }
      SHELL.pushToast({ kind: 'success', msg: t.dataset.asdraft ? 'Taslak kaydedildi' : 'Sipariş başarıyla onaylandı' });
      state.formPO = null; state.formPOErrors = {};
      setTimeout(() => nav('purchasing'), 600);
      return;
    }
    if (action === 'so-submit') {
      const errs = validateSO(state.formSO || {});
      if (Object.keys(errs).length) { state.formSOErrors = errs; SHELL.pushToast({ kind: 'danger', msg: 'Lütfen formdaki hataları düzeltin' }); render(); return; }
      SHELL.pushToast({ kind: 'success', msg: t.dataset.asdraft ? 'Taslak kaydedildi' : 'Satış siparişi onaylandı' });
      state.formSO = null; state.formSOErrors = {};
      setTimeout(() => nav('sales'), 600);
      return;
    }
    if (action === 'user-submit') {
      const errs = validateUser(state.formUser || {});
      if (Object.keys(errs).length) { state.formUserErrors = errs; SHELL.pushToast({ kind: 'danger', msg: 'Lütfen formdaki hataları düzeltin' }); render(); return; }
      const u = state.formUser;
      SHELL.pushToast({ kind: 'success', msg: `${u.firstName} ${u.lastName} davet edildi` });
      state.formUser = null; state.formUserErrors = {};
      setTimeout(() => nav('users'), 600);
      return;
    }

    // Tweaks panel
    if (action === 'tweaks-toggle') { state.tweaksPanelOpen = !state.tweaksPanelOpen; renderTweaksPanel(); return; }
    if (action === 'tweaks-close') { state.tweaksPanelOpen = false; renderTweaksPanel(); window.parent.postMessage({ type: '__edit_mode_dismissed' }, '*'); return; }
    if (action === 'tweak-empty') {
      state.tweaks.emptyState = t.checked != null ? t.checked : !state.tweaks.emptyState;
      persistTweaks(); render();
      return;
    }
    if (action === 'tweak-accent') { state.tweaks.accent = t.dataset.accent; applyAccent(); persistTweaks(); renderTweaksPanel(); return; }

    // Quick create button on topbar — opens command palette
    if (action === 'quick-create') { SHELL.openCmdk(); return; }
  });

  // Input/change events for forms and search boxes
  document.addEventListener('input', (e) => {
    const t = e.target;
    if (t.dataset && t.dataset.form) {
      const formKey = 'form' + t.dataset.form.toUpperCase();
      const fObj = state[formKey] = state[formKey] || {};
      fObj[t.dataset.field] = t.type === 'checkbox' ? t.checked : t.value;
      // For boolean toggles use change event below; here just update
      return;
    }
    if (t.dataset && t.dataset.action) {
      const a = t.dataset.action;
      const searchMap = {
        'po-search': () => { state.local.q = t.value; renderScreenOnly(); },
        'cari-search': () => { state.local.q = t.value; renderScreenOnly(); },
        'sales-search': () => { state.local.q = t.value; renderScreenOnly(); },
        'stokh-search': () => { state.local.q = t.value; renderScreenOnly(); },
        'inv-search': () => { state.local.q = t.value; renderScreenOnly(); },
      };
      if (searchMap[a]) { searchMap[a](); return; }
      if (a === 'open-cmdk') return; // input inside readonly — skip
    }
    // cmdk input
    if (t.id === 'cmdk-input') { SHELL.cmdkSetQuery(t.value); return; }
  });

  document.addEventListener('change', (e) => {
    const t = e.target;
    if (t.dataset && t.dataset.form && t.type === 'checkbox' && t.dataset.bool) {
      const formKey = 'form' + t.dataset.form.toUpperCase();
      const fObj = state[formKey] = state[formKey] || {};
      fObj[t.dataset.field] = t.checked;
    }
  });

  // Re-render only the screen content (preserves focus in search boxes)
  let screenContainer = null;
  const renderScreenOnly = () => {
    const main = document.querySelector('.main');
    if (!main) { render(); return; }
    // Replace everything after topbar
    const topbar = main.querySelector('.topbar');
    while (topbar.nextSibling) main.removeChild(topbar.nextSibling);
    const screenHtml = renderScreen();
    const tmp = document.createElement('div');
    tmp.innerHTML = screenHtml.__raw || screenHtml;
    while (tmp.firstChild) main.appendChild(tmp.firstChild);
  };

  // Keyboard shortcuts
  document.addEventListener('keydown', (e) => {
    if ((e.metaKey || e.ctrlKey) && e.key === 'k') {
      e.preventDefault();
      SHELL.openCmdk();
      return;
    }
    const tag = (document.activeElement?.tagName || '').toLowerCase();
    const inField = tag === 'input' || tag === 'textarea' || tag === 'select';
    if (e.altKey && (e.key === 'n' || e.key === 'N') && !inField) {
      e.preventDefault();
      const base = state.route.split('/')[0];
      if (base === 'users') nav('users/new');
      else if (base === 'sales') nav('sales/new');
      else nav('purchasing/new');
      return;
    }
    if (e.key === 'Escape') {
      if (state.tweaksPanelOpen) { state.tweaksPanelOpen = false; renderTweaksPanel(); return; }
      const r = state.route;
      if (r.includes('/new') || r.includes('/detail')) {
        const base = r.split('/')[0];
        if (r.startsWith('cari/detail')) nav('cari');
        else if (r.startsWith('stok/kart')) nav('stok/hareket');
        else if (r.startsWith('production/detail')) nav('production');
        else if (r.startsWith('accounting/detail')) nav('accounting');
        else if (r.startsWith('reports/view')) nav('reports');
        else nav(base);
      }
      return;
    }
    // cmdk navigation
    if (document.querySelector('.cmdk-scrim')) {
      if (e.key === 'ArrowDown') { e.preventDefault(); SHELL.cmdkNavigate(1); }
      else if (e.key === 'ArrowUp') { e.preventDefault(); SHELL.cmdkNavigate(-1); }
      else if (e.key === 'Enter') { e.preventDefault(); SHELL.cmdkPick(); }
    }
  });

  // ---------- Hash routing ----------
  window.addEventListener('hashchange', () => {
    const r = parseHash();
    if (r !== state.route) { state.route = r; state.local = {}; render(); }
  });

  // ---------- Tweaks persistence (postMessage to host) ----------
  const persistTweaks = () => {
    try { window.parent.postMessage({ type: '__edit_mode_set_keys', edits: { ...state.tweaks } }, '*'); } catch (e) {}
  };

  // Host-driven Tweaks toggle from toolbar
  window.addEventListener('message', (e) => {
    const d = e.data || {};
    if (d.type === '__activate_edit_mode') { state.tweaksPanelOpen = true; renderTweaksPanel(); }
    else if (d.type === '__deactivate_edit_mode') { state.tweaksPanelOpen = false; renderTweaksPanel(); }
  });
  setTimeout(() => { try { window.parent.postMessage({ type: '__edit_mode_available' }, '*'); } catch (e) {} }, 0);

  // ---------- Boot ----------
  window.OPERAX = { nav, render, renderChrome, state };
  state.route = parseHash();
  applyAccent();
  render();
})();
