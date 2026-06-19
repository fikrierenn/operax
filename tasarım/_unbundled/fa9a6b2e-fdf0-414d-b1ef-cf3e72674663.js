/* OPERAX — Pure JS · Uygulama kabuğu (sidebar + topbar + cmdk + toast) */
(function () {
  const I = window.ICONS;
  const { html, raw, esc } = window.UI;

  const NAV_GROUPS = [
    { label: 'Genel', items: [
      { id: 'dashboard', label: 'Anasayfa', icon: 'Home' },
    ]},
    { label: 'Operasyon', items: [
      { id: 'purchasing', label: 'Satınalma', icon: 'Cart', badge: '12' },
      { id: 'sales',      label: 'Satış',     icon: 'Tag' },
      { id: 'wms',        label: 'Depo / WMS', icon: 'Warehouse', badge: '4' },
      { id: 'inventory',  label: 'Stok / Envanter', icon: 'Box' },
      { id: 'stok/hareket', label: 'Stok Hareketleri', icon: 'Swap' },
      { id: 'production', label: 'Üretim', icon: 'Factory' },
    ]},
    { label: 'Finans', items: [
      { id: 'cari',       label: 'Cari Kartlar', icon: 'CreditCard' },
      { id: 'accounting', label: 'Muhasebe', icon: 'Coin' },
      { id: 'cashbank',   label: 'Kasa / Banka', icon: 'Database' },
      { id: 'reports',    label: 'Raporlar', icon: 'BarChart' },
    ]},
    { label: 'Sistem', items: [
      { id: 'users',    label: 'Kullanıcılar', icon: 'Users', badge: '6' },
      { id: 'settings', label: 'Ayarlar', icon: 'Cog' },
    ]},
  ];

  const renderSidebar = (route) => {
    const baseRoute = route.split('/')[0];
    const html = `
      <aside class="side">
        <div class="side-brand">
          <div class="side-brand-mark">OX</div>
          <div>
            <div class="side-brand-name">OPERAX</div>
            <div class="side-brand-sub">ERP · WMS Platform</div>
          </div>
        </div>

        <div class="side-company" title="Şirket seçimi">
          <div class="side-company-logo">AY</div>
          <div style="min-width:0;flex:1">
            <div class="side-company-name" style="white-space:nowrap;overflow:hidden;text-overflow:ellipsis">Aydın Endüstri A.Ş.</div>
            <div class="side-company-sub">TR · İstanbul</div>
          </div>
          ${I.ChevronDown(14, 2)}
        </div>

        <nav class="side-nav">
          ${NAV_GROUPS.map((g) => `
            <div>
              <div class="side-group-label">${esc(g.label)}</div>
              ${g.items.map((it) => {
                const isActive = it.id.includes('/') ? route === it.id : baseRoute === it.id;
                return `
                  <div class="side-item${isActive ? ' active' : ''}" data-action="nav" data-route="${esc(it.id)}">
                    <span class="side-item-icon">${I[it.icon](16, 2)}</span>
                    <span>${esc(it.label)}</span>
                    ${it.badge ? `<span class="side-item-badge">${esc(it.badge)}</span>` : ''}
                  </div>
                `;
              }).join('')}
            </div>
          `).join('')}
        </nav>

        <div class="side-foot">
          <div class="side-foot-avatar">MY</div>
          <div style="min-width:0;flex:1">
            <div class="side-foot-name" style="white-space:nowrap;overflow:hidden;text-overflow:ellipsis">Mehmet Yılmaz</div>
            <div class="side-foot-role">Yönetici · Satınalma</div>
          </div>
          <div class="side-foot-action" title="Çıkış">${I.LogOut(15, 2)}</div>
        </div>
      </aside>
    `;
    return html;
  };

  const renderTopbar = () => `
    <div class="topbar">
      <div class="topbar-search" data-action="open-cmdk">
        ${I.Search(14, 2).replace('<svg ', '<svg class="topbar-search-icon" ')}
        <input type="text" placeholder="Evrak, ürün, tedarikçi, kullanıcı ara…" readonly style="cursor:pointer" />
        <span class="topbar-search-kbd">⌘ K</span>
      </div>
      <div class="topbar-actions">
        <button class="icon-btn" title="Hızlı oluştur" data-action="quick-create">${I.Plus(16, 2)}</button>
        <button class="icon-btn" title="Bildirimler">${I.Bell(16, 2)}<span class="icon-btn-dot"></span></button>
        <button class="icon-btn" title="Yardım">${I.Info(16, 2)}</button>
        <div style="width:1px;height:22px;background:var(--border);margin:0 4px"></div>
        <button class="btn btn-secondary btn-sm">${I.Calendar(13, 2)}<span class="mono">27 May 2026</span></button>
      </div>
    </div>
  `;

  // ---------- Command palette ----------
  const CMDK_ITEMS = [
    { group: 'Hızlı git', icon: 'Home',       label: 'Anasayfa',                kbd: 'G H', route: 'dashboard' },
    { group: 'Hızlı git', icon: 'Cart',       label: 'Satınalma Siparişleri',   kbd: 'G P', route: 'purchasing' },
    { group: 'Hızlı git', icon: 'Tag',        label: 'Satış Siparişleri',       kbd: 'G S', route: 'sales' },
    { group: 'Hızlı git', icon: 'Warehouse',  label: 'Depo / WMS',              kbd: 'G W', route: 'wms' },
    { group: 'Hızlı git', icon: 'Factory',    label: 'Üretim İş Emirleri',      kbd: 'G F', route: 'production' },
    { group: 'Cari & Stok', icon: 'CreditCard', label: 'Cari Kart Listesi',           route: 'cari' },
    { group: 'Cari & Stok', icon: 'Building',   label: 'Cari Detay · Türkbasınç',     route: 'cari/detail/TKB' },
    { group: 'Cari & Stok', icon: 'Box',        label: 'Stok Kartı · Hidrolik Silindir', route: 'stok/kart/PR-0103' },
    { group: 'Cari & Stok', icon: 'Box',        label: 'Stok Kartı · Reçine PA66',    route: 'stok/kart/PR-0612' },
    { group: 'Cari & Stok', icon: 'Swap',       label: 'Stok Hareketleri',            route: 'stok/hareket' },
    { group: 'Finans', icon: 'Coin',     label: 'Yevmiye Fişleri',  route: 'accounting' },
    { group: 'Finans', icon: 'Database', label: 'Kasa / Banka',     route: 'cashbank' },
    { group: 'Finans', icon: 'BarChart', label: 'Rapor Merkezi',    route: 'reports' },
    { group: 'İşlemler', icon: 'Plus',   label: 'Yeni Satınalma Siparişi', kbd: 'Alt N', route: 'purchasing/new' },
    { group: 'İşlemler', icon: 'Plus',   label: 'Yeni Satış Siparişi',     route: 'sales/new' },
    { group: 'İşlemler', icon: 'Plus',   label: 'Yeni Kullanıcı',          route: 'users/new' },
    { group: 'Sistem', icon: 'Users', label: 'Kullanıcılar', route: 'users' },
    { group: 'Sistem', icon: 'Cog',   label: 'Ayarlar',      route: 'settings' },
  ];

  let cmdkState = { open: false, q: '', active: 0 };

  const filteredCmdk = () => {
    if (!cmdkState.q) return CMDK_ITEMS;
    const qL = cmdkState.q.toLocaleLowerCase('tr-TR');
    return CMDK_ITEMS.filter((i) => i.label.toLocaleLowerCase('tr-TR').includes(qL));
  };

  const renderCmdk = () => {
    if (!cmdkState.open) return '';
    const items = filteredCmdk();
    const grouped = items.reduce((acc, it) => { (acc[it.group] = acc[it.group] || []).push(it); return acc; }, {});
    let idx = -1;
    const sections = Object.entries(grouped).map(([g, list]) => {
      const rows = list.map((it) => {
        idx++;
        const isActive = idx === cmdkState.active;
        return `
          <div class="cmdk-item${isActive ? ' active' : ''}" data-action="cmdk-pick" data-idx="${idx}">
            <span class="cmdk-item-icon">${I[it.icon](15, 2)}</span>
            <span class="cmdk-item-label">${esc(it.label)}</span>
            ${it.kbd ? `<span class="cmdk-item-kbd">${esc(it.kbd)}</span>` : ''}
          </div>
        `;
      }).join('');
      return `<div><div class="cmdk-group-label">${esc(g)}</div>${rows}</div>`;
    }).join('');
    return `
      <div class="cmdk-scrim" data-action="cmdk-close">
        <div class="cmdk" data-stop="1">
          <div class="cmdk-input-wrap">
            ${I.Search(16, 2)}
            <input class="cmdk-input" id="cmdk-input" placeholder="Ne yapmak istiyorsunuz?" value="${esc(cmdkState.q)}" />
            <span class="mono" style="font-size:10px;color:var(--text-4);padding:2px 6px;border:1px solid var(--border);border-radius:4px">ESC</span>
          </div>
          <div class="cmdk-list">
            ${items.length === 0 ? `<div style="padding:28px;text-align:center;color:var(--text-3);font-size:13px">Sonuç bulunamadı</div>` : sections}
          </div>
          <div style="display:flex;align-items:center;gap:12px;padding:8px 14px;border-top:1px solid var(--border);background:var(--surface-2);font-size:11px;color:var(--text-3)">
            <span><span class="mono" style="background:#fff;border:1px solid var(--border);padding:1px 5px;border-radius:4px">↑↓</span> gezin</span>
            <span><span class="mono" style="background:#fff;border:1px solid var(--border);padding:1px 5px;border-radius:4px">↵</span> seç</span>
            <span style="margin-left:auto">OPERAX Quick Actions</span>
          </div>
        </div>
      </div>
    `;
  };

  const openCmdk = () => { cmdkState.open = true; cmdkState.q = ''; cmdkState.active = 0; window.OPERAX.renderChrome(); setTimeout(() => document.getElementById('cmdk-input')?.focus(), 30); };
  const closeCmdk = () => { cmdkState.open = false; window.OPERAX.renderChrome(); };
  const cmdkNavigate = (dir) => {
    const items = filteredCmdk();
    cmdkState.active = Math.max(0, Math.min(items.length - 1, cmdkState.active + dir));
    window.OPERAX.renderChrome();
  };
  const cmdkPick = (idx) => {
    const items = filteredCmdk();
    const it = items[idx != null ? idx : cmdkState.active];
    if (it && it.route) { closeCmdk(); window.OPERAX.nav(it.route); }
  };
  const cmdkSetQuery = (q) => { cmdkState.q = q; cmdkState.active = 0; window.OPERAX.renderChrome(); setTimeout(() => { const el = document.getElementById('cmdk-input'); if (el) { el.focus(); el.setSelectionRange(el.value.length, el.value.length); } }, 0); };

  // ---------- Toast ----------
  const toastState = { items: [] };
  const pushToast = (t) => {
    const id = Math.random().toString(36).slice(2);
    toastState.items.push({ id, ...t });
    renderToasts();
    setTimeout(() => { toastState.items = toastState.items.filter((x) => x.id !== id); renderToasts(); }, t.duration || 3200);
  };
  const renderToasts = () => {
    let host = document.getElementById('toast-host');
    if (!host) {
      host = document.createElement('div');
      host.id = 'toast-host';
      host.className = 'toast-host';
      document.body.appendChild(host);
    }
    host.innerHTML = toastState.items.map((t) => {
      const ico = t.kind === 'success' ? I.Check(11, 3) : t.kind === 'danger' ? I.X(11, 3) : I.Info(11, 3);
      return `<div class="toast toast-${t.kind || 'info'}"><div class="toast-icon">${ico}</div><div>${esc(t.msg)}</div></div>`;
    }).join('');
  };

  window.SHELL = {
    renderSidebar, renderTopbar, renderCmdk,
    openCmdk, closeCmdk, cmdkNavigate, cmdkPick, cmdkSetQuery,
    pushToast,
  };
})();
