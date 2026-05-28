/* OPERAX — Pure JS · Ekranlar (3/3): Stok Hareket · Sales · Envanter · Üretim · Muhasebe · Raporlar · Users · Settings */
(function () {
  const I = window.ICONS;
  const D = window.OPX;
  const U = window.UI;
  const { html, raw, esc, statusBadge, shipBadge, movementBadge, woStatusBadge, priorityBadge,
    avatar, initials, pageHeader, kpiCard, sparkline, gauge, emptyState, tabs, btn } = U;

  // ============== STOK HAREKETLERİ ==============
  const StokHareket = (state) => {
    const empty = state.tweaks.emptyState;
    const local = state.local;
    local.typeFilter = local.typeFilter || 'all';
    local.q = local.q || '';
    let rows = empty ? [] : D.stockMovements;
    if (local.typeFilter !== 'all') rows = rows.filter((m) => m.type.k === local.typeFilter);
    if (local.q) {
      const qL = local.q.toLocaleLowerCase('tr-TR');
      rows = rows.filter((m) => m.id.toLowerCase().includes(qL) || m.sku.toLowerCase().includes(qL) || m.name.toLocaleLowerCase('tr-TR').includes(qL));
    }
    const counts = { all: D.stockMovements.length };
    ['in', 'out', 'tr', 'cnt', 'adj', 'ret'].forEach((k) => counts[k] = D.stockMovements.filter((m) => m.type.k === k).length);
    const ins = rows.filter((m) => m.qty > 0).reduce((a, m) => a + m.qty, 0);
    const outs = rows.filter((m) => m.qty < 0).reduce((a, m) => a + m.qty, 0);

    return html`
      <div class="page" data-screen-label="Stok Hareketleri" style="max-width:1640px">
        ${pageHeader({
          crumbs: [{ label: 'Anasayfa' }, { label: 'Stok', route: 'inventory' }, { label: 'Stok Hareketleri' }],
          title: 'Stok Hareketleri',
          sub: 'Tüm depolardaki giriş, çıkış, transfer ve sayım hareketlerinin merkezi akışı.',
          actions: `
            ${btn('Yenile', { kind: 'secondary', icon: 'Refresh' })}
            ${btn('Dışa Aktar', { kind: 'secondary', icon: 'Download' })}
            ${btn('Manuel Hareket', { kind: 'primary', icon: 'Plus', kbd: 'Alt+N' })}
          `,
        })}

        <div class="kpi-grid">
          ${kpiCard({ label: 'Bugünkü Hareket', value: empty ? 0 : counts.all, glow: 'brand', valueSize: 26, sub: 'Tüm türler · 7/24' })}
          ${kpiCard({ label: 'Toplam Giriş', value: empty ? 0 : D.fmtNum(ins), unit: 'birim', glow: 'success', valueSize: 26, sub: 'Mal kabul + transfer' })}
          ${kpiCard({ label: 'Toplam Çıkış', value: empty ? 0 : D.fmtNum(Math.abs(outs)), unit: 'birim', glow: 'danger', valueSize: 26, sub: 'Sevkiyat + iade' })}
          ${kpiCard({ label: 'Bekleyen Onay', value: empty ? 0 : D.stockMovements.filter((m) => m.status === 'pending').length, glow: 'warn', valueSize: 26, sub: 'Doğrulama gerekiyor' })}
        </div>

        <div class="card">
          ${tabs([
            { id: 'all', label: 'Tümü', count: counts.all },
            { id: 'in', label: 'Mal Kabul', icon: 'ArrowDownIn', count: counts.in },
            { id: 'out', label: 'Sevkiyat', icon: 'ArrowUpOut', count: counts.out },
            { id: 'tr', label: 'Transfer', icon: 'Swap', count: counts.tr },
            { id: 'cnt', label: 'Sayım', icon: 'ClipDoc', count: counts.cnt },
            { id: 'adj', label: 'Düzeltme', icon: 'Edit', count: counts.adj },
            { id: 'ret', label: 'İade', icon: 'Refresh', count: counts.ret },
          ], local.typeFilter, 'stokh-tab')}

          <div class="data-table-toolbar">
            <div style="position:relative;flex:0 0 280px">
              ${raw(I.Search(14, 2).replace('<svg ', '<svg style="position:absolute;left:11px;top:50%;transform:translateY(-50%);color:var(--text-4)" '))}
              <input class="form-ctrl" style="padding-left:34px;height:34px" placeholder="Fiş no, SKU, ürün adı ara…" value="${esc(local.q)}" data-action="stokh-search" />
            </div>
            <button class="chip">${I.Calendar(12, 2)}Son 30 gün${I.ChevronDown(11, 2)}</button>
            <button class="chip">Tüm depolar${I.ChevronDown(11, 2)}</button>
            <button class="chip">Tüm kullanıcılar${I.ChevronDown(11, 2)}</button>
            <button class="btn btn-ghost btn-sm" style="margin-left:auto">${I.Sliders(13, 2)}Sütunlar</button>
          </div>

          ${rows.length === 0 ? emptyState({ icon: 'Swap', title: 'Hareket bulunamadı', msg: 'Filtreleri değiştirin veya manuel hareket oluşturun.' }) : html`
            <table class="data-table">
              <thead><tr><th>Tarih · Saat</th><th>Fiş No</th><th>Tür</th><th>Ürün</th><th>Konum (Çıkış → Giriş)</th><th class="num">Miktar</th><th>Referans</th><th>Kullanıcı</th><th>Durum</th><th style="width:36px"></th></tr></thead>
              <tbody>
                ${raw(rows.map((m) => `
                  <tr style="cursor:pointer">
                    <td class="mono muted">${esc(D.fmtDateTime(m.time))}</td>
                    <td><span class="mono row-strong" style="color:var(--brand-500)">${esc(m.id)}</span></td>
                    <td>${movementBadge(m.type).__raw}</td>
                    <td style="cursor:pointer" data-action="nav" data-route="stok/kart/${esc(m.sku)}">
                      <div>
                        <div class="mono row-strong" style="color:var(--brand-500)">${esc(m.sku)}</div>
                        <div class="row-sub">${esc(m.name)}</div>
                      </div>
                    </td>
                    <td>
                      <div style="display:flex;align-items:center;gap:6px;font-size:12px">
                        ${m.fromLoc ? `<span class="mono" style="padding:2px 6px;background:var(--bg);border-radius:4px;border:1px solid var(--border)">${esc(m.fromLoc)}</span>` : ''}
                        ${m.fromLoc && m.toLoc ? I.ChevronRight(11, 2) : ''}
                        ${m.toLoc ? `<span class="mono" style="padding:2px 6px;background:var(--brand-tint-08);color:var(--brand-500);border-radius:4px">${esc(m.toLoc)}</span>` : ''}
                        ${!m.fromLoc && !m.toLoc ? '<span class="muted">—</span>' : ''}
                      </div>
                    </td>
                    <td class="num"><span class="mono row-strong" style="color:${m.qty > 0 ? 'var(--success-text)' : 'var(--danger-text)'};font-size:13px">${m.qty > 0 ? '+' : ''}${esc(D.fmtNum(m.qty))}</span> <span class="muted">${esc(m.uom)}</span></td>
                    <td>${m.ref ? `<span class="mono muted">${esc(m.ref)}</span>` : (m.supplier ? `<span class="mono muted">${esc(m.supplier)}</span>` : '<span class="muted">—</span>')}</td>
                    <td><div style="display:flex;align-items:center;gap:7px">${avatar(initials(m.user), { size: 22, fontSize: 9, color: m.userColor }).__raw}<span style="font-size:12px">${esc(m.user)}</span></div></td>
                    <td>${m.status === 'posted' ? '<span class="badge badge-success"><span class="badge-dot"></span>İşlendi</span>' : '<span class="badge badge-warn"><span class="badge-dot"></span>Onay Bekliyor</span>'}</td>
                    <td><button class="icon-btn" style="width:26px;height:26px">${I.More(14, 2)}</button></td>
                  </tr>
                `).join(''))}
              </tbody>
            </table>
          `}
        </div>
      </div>
    `;
  };

  // ============== SATIŞ LISTE ==============
  const SalesList = (state) => {
    const empty = state.tweaks.emptyState;
    const local = state.local;
    local.tab = local.tab || 'all';
    local.q = local.q || '';
    let rows = empty ? [] : D.salesOrders;
    if (local.tab !== 'all') rows = rows.filter((s) => s.status === local.tab);
    if (local.q) {
      const qL = local.q.toLocaleLowerCase('tr-TR');
      rows = rows.filter((s) => s.no.toLowerCase().includes(qL) || D.cariByCode(s.customer).name.toLocaleLowerCase('tr-TR').includes(qL));
    }
    const counts = {
      all: D.salesOrders.length,
      Draft: D.salesOrders.filter((s) => s.status === 'Draft').length,
      Posted: D.salesOrders.filter((s) => s.status === 'Posted').length,
      Cancelled: D.salesOrders.filter((s) => s.status === 'Cancelled').length,
    };
    const totalActive = rows.reduce((a, s) => a + (s.status !== 'Cancelled' ? s.total : 0), 0);

    return html`
      <div class="page" data-screen-label="Sales List">
        ${pageHeader({
          crumbs: [{ label: 'Anasayfa' }, { label: 'Satış' }, { label: 'Siparişler' }],
          title: 'Satış Siparişleri',
          sub: `Toplam ${empty ? 0 : D.salesOrders.length} evrak · Aktif tutar <span class="mono text-strong">${esc(D.fmtTL(totalActive))}</span>`,
          actions: `${btn('Dışa Aktar', { kind: 'secondary', icon: 'Download' })}${btn('Yeni Sipariş', { kind: 'primary', icon: 'Plus', action: 'nav', route: 'sales/new', kbd: 'Alt+N' })}`,
        })}

        <div class="card">
          ${tabs([
            { id: 'all', label: 'Tümü', count: counts.all },
            { id: 'Draft', label: 'Taslak', count: counts.Draft },
            { id: 'Posted', label: 'Onaylandı', count: counts.Posted },
            { id: 'Cancelled', label: 'İptal', count: counts.Cancelled },
          ], local.tab, 'sales-tab')}

          <div class="data-table-toolbar">
            <div style="position:relative;flex:0 0 280px">
              ${raw(I.Search(14, 2).replace('<svg ', '<svg style="position:absolute;left:11px;top:50%;transform:translateY(-50%);color:var(--text-4)" '))}
              <input class="form-ctrl" style="padding-left:34px;height:34px" placeholder="Evrak no, müşteri ara…" value="${esc(local.q)}" data-action="sales-search" />
            </div>
            <button class="chip">${I.Calendar(12, 2)}Son 30 gün${I.ChevronDown(11, 2)}</button>
            <button class="chip">Müşteri: Tümü${I.ChevronDown(11, 2)}</button>
            <button class="chip">Sevkiyat: Tümü${I.ChevronDown(11, 2)}</button>
          </div>

          ${rows.length === 0 ? emptyState({ icon: 'Tag', title: 'Sipariş bulunamadı', action: btn('Yeni Sipariş', { kind: 'primary', icon: 'Plus', action: 'nav', route: 'sales/new' }) }) : html`
            <table class="data-table">
              <thead><tr><th>Evrak No</th><th>Müşteri</th><th>Tarih</th><th>Vade</th><th class="num">Kalem</th><th class="num">Tutar</th><th>Durum</th><th>Sevkiyat</th><th style="width:36px"></th></tr></thead>
              <tbody>
                ${raw(rows.map((so) => {
                  const c = D.cariByCode(so.customer);
                  return `
                    <tr style="cursor:pointer" data-action="nav" data-route="sales/detail/${esc(so.no)}">
                      <td><span class="mono row-strong" style="color:var(--brand-500)">${esc(so.no)}</span></td>
                      <td><div style="display:flex;align-items:center;gap:10px">${avatar(so.customer, { size: 26, fontSize: 10 }).__raw}<div><div class="row-strong">${esc(c.name)}</div><div class="row-sub">${esc(c.city)} · VKN ${esc(c.tax)}</div></div></div></td>
                      <td class="mono muted">${esc(D.fmtDateShort(so.date))}</td>
                      <td class="mono">${esc(D.fmtDateShort(so.due))}</td>
                      <td class="num">${esc(so.items)}</td>
                      <td class="num"><span class="mono row-strong">${esc(D.fmtTL(so.total))}</span></td>
                      <td>${statusBadge(so.status).__raw}</td>
                      <td>${shipBadge(so.shipStatus).__raw}</td>
                      <td>${I.ChevronRight(14, 2)}</td>
                    </tr>
                  `;
                }).join(''))}
              </tbody>
            </table>
          `}
        </div>
      </div>
    `;
  };

  // ============== SATIŞ DETAY ==============
  const SalesDetail = (state, soNo) => {
    const so = D.salesOrders.find((x) => x.no === soNo) || D.salesOrders[0];
    const c = D.cariByCode(so.customer);
    const subtotal = D.SOLines.reduce((a, l) => a + l.qty * l.unitPrice, 0);
    const vat = subtotal * 0.20;
    const grand = subtotal + vat;
    const shippedRatio = D.SOLines.reduce((a, l) => a + (l.shipped / l.qty), 0) / D.SOLines.length;

    return html`
      <div class="page" data-screen-label="Sales Detail" style="max-width:1400px">
        ${pageHeader({
          crumbs: [
            { label: 'Anasayfa' },
            { label: 'Satış', route: 'sales' },
            { label: 'Siparişler', route: 'sales' },
            { label: so.no, mono: true },
          ],
          titleSlot: html`
            <div style="display:flex;align-items:center;gap:12px;margin-top:2px">
              <h1 class="mono" style="letter-spacing:-0.01em">${so.no}</h1>
              ${statusBadge(so.status)}
              ${shipBadge(so.shipStatus)}
            </div>
          `,
          sub: `${esc(c.name)} · Sipariş tarihi: <span class="mono">${esc(D.fmtDate(so.date))}</span> · Vade: <span class="mono">${esc(D.fmtDate(so.due))}</span>`,
          actions: `
            ${btn('Geri', { kind: 'ghost', icon: 'ChevronLeft', action: 'nav', route: 'sales' })}
            ${btn('PDF', { kind: 'secondary', icon: 'Download' })}
            ${so.status === 'Posted' && so.shipStatus !== 'shipped' ? btn('Sevkiyat Oluştur', { kind: 'primary', icon: 'Truck', kbd: 'Alt+S' }) : ''}
            ${so.status === 'Draft' ? btn('Onayla', { kind: 'success', icon: 'Check' }) : ''}
          `,
        })}

        <div class="kpi-grid">
          ${kpiCard({ label: 'Toplam Tutar', value: D.fmtTL(grand), glow: 'brand', valueSize: 22, sub: 'KDV dahil' })}
          ${kpiCard({ label: 'Sevk Oranı', value: `%${Math.round(shippedRatio * 100)}`, glow: 'success', valueSize: 22, sub: `${D.SOLines.filter((l) => l.shipped === l.qty).length} / ${D.SOLines.length} kalem tamamlandı` })}
          ${kpiCard({ label: 'Vadeye Kalan', value: 14, unit: 'gün', glow: 'warn', valueSize: 22, sub: `Termin: ${D.fmtDate(so.due)}` })}
          ${kpiCard({ label: 'Müşteri Bakiye', value: D.fmtTL(c.balance), glow: 'brand', valueSize: 22, sub: `Vade: ${c.paymentTerm} gün` })}
        </div>

        <div style="display:grid;grid-template-columns:1fr 320px;gap:14px">
          <div style="display:flex;flex-direction:column;gap:14px">
            <div class="card">
              <div class="card-hdr"><div class="card-title">Evrak Bilgileri</div></div>
              <div class="card-body" style="display:grid;grid-template-columns:repeat(4,1fr);gap:18px">
                <div><div class="form-label">Müşteri</div><div style="font-size:13px;font-weight:600;margin-top:4px;color:var(--brand-500);cursor:pointer" data-action="nav" data-route="cari/detail/${esc(so.customer)}">${esc(c.name)}</div><div style="font-size:11.5px;color:var(--text-3);margin-top:1px">VKN <span class="mono">${esc(c.tax)}</span></div></div>
                <div><div class="form-label">Şehir</div><div style="font-size:13px;font-weight:600;margin-top:4px">${esc(c.city)}</div></div>
                <div><div class="form-label">Ödeme Şartı</div><div style="font-size:13px;font-weight:600;margin-top:4px"><span class="mono">${esc(c.paymentTerm)}</span> gün vadeli</div></div>
                <div><div class="form-label">Sevkiyat Adresi</div><div style="font-size:13px;font-weight:600;margin-top:4px">Müşteri Deposu · ${esc(c.city)}</div></div>
                <div><div class="form-label">Satış Temsilcisi</div><div style="display:flex;align-items:center;gap:8px;margin-top:4px">${avatar('MY', { size: 22, fontSize: 9.5, gradient: 'linear-gradient(135deg, hsl(243 75% 59%), hsl(263 70% 55%))' })}<span style="font-size:13px;font-weight:600">Mehmet Yılmaz</span></div></div>
                <div><div class="form-label">Para Birimi</div><div class="mono" style="font-size:13px;font-weight:600;margin-top:4px">TRY · ₺</div></div>
                <div><div class="form-label">Referans</div><div class="mono" style="font-size:13px;font-weight:600;margin-top:4px">PO-MÜŞTERİ-2026/A18</div></div>
                <div><div class="form-label">Teslimat Tipi</div><div style="font-size:13px;font-weight:600;margin-top:4px">Kara · OPERAX Lojistik</div></div>
              </div>
            </div>

            <div class="card">
              <div class="card-hdr"><div><div class="card-title">Sipariş Kalemleri</div><div class="card-sub">${D.SOLines.length} kalem · sevkiyat oranı %${Math.round(shippedRatio * 100)}</div></div></div>
              <div class="card-body-flush">
                <table class="data-table">
                  <thead><tr><th style="width:40px" class="num">#</th><th>SKU</th><th>Ürün</th><th>Birim</th><th class="num">Miktar</th><th class="num">Sevk Edilen</th><th class="num">Birim Fiyat</th><th class="num">KDV</th><th class="num">Toplam</th></tr></thead>
                  <tbody>
                    ${raw(D.SOLines.map((l) => {
                      const t = l.qty * l.unitPrice;
                      const pct = (l.shipped / l.qty) * 100;
                      const pctColor = pct === 100 ? 'var(--success)' : pct > 0 ? 'var(--warn)' : 'var(--text-mute)';
                      const pctText = pct === 100 ? 'var(--success-text)' : pct > 0 ? 'var(--warn-text)' : 'var(--text-3)';
                      return `
                        <tr>
                          <td class="num muted mono">${esc(l.line)}</td>
                          <td style="cursor:pointer" data-action="nav" data-route="stok/kart/${esc(l.sku)}"><span class="mono row-strong" style="color:var(--brand-500)">${esc(l.sku)}</span></td>
                          <td>${esc(l.name)}</td>
                          <td><span class="badge badge-neutral" style="height:18px">${esc(l.uom)}</span></td>
                          <td class="num">${esc(D.fmtNum(l.qty))}</td>
                          <td class="num">
                            <div style="display:flex;align-items:center;gap:8px;justify-content:flex-end">
                              <div style="width:60px;height:5px;background:var(--bg-2);border-radius:3px;overflow:hidden"><div style="width:${pct}%;height:100%;background:${pctColor};border-radius:3px"></div></div>
                              <span class="mono" style="font-size:11.5px;font-weight:600;color:${pctText}">${esc(D.fmtNum(l.shipped))}</span>
                            </div>
                          </td>
                          <td class="num">${esc(D.fmtTLDec(l.unitPrice))}</td>
                          <td class="num muted">%${esc(l.vat)}</td>
                          <td class="num"><span class="row-strong">${esc(D.fmtTL(t))}</span></td>
                        </tr>
                      `;
                    }).join(''))}
                  </tbody>
                </table>
              </div>
              <div class="card-foot">
                <div style="font-size:11.5px;color:var(--text-3)">Tüm tutarlar TRY (₺) cinsindendir</div>
                <div style="display:flex;gap:24px;font-size:12.5px">
                  <div style="display:flex;flex-direction:column;align-items:flex-end"><span style="color:var(--text-3)">Ara Toplam</span><span class="mono" style="font-weight:600">${D.fmtTL(subtotal)}</span></div>
                  <div style="display:flex;flex-direction:column;align-items:flex-end"><span style="color:var(--text-3)">KDV (%20)</span><span class="mono" style="font-weight:600">${D.fmtTL(vat)}</span></div>
                  <div style="display:flex;flex-direction:column;align-items:flex-end"><span style="color:var(--text-3);font-size:10px;font-weight:700;text-transform:uppercase;letter-spacing:.08em">Genel Toplam</span><span class="mono" style="font-size:17px;font-weight:700;color:var(--brand-500)">${D.fmtTL(grand)}</span></div>
                </div>
              </div>
            </div>
          </div>

          <div style="display:flex;flex-direction:column;gap:14px">
            <div class="card">
              <div class="card-hdr"><div class="card-title">Sevkiyat Planı</div></div>
              <div class="card-body" style="padding:14px">
                ${raw([
                  { lpn: 'LPN-00510', date: '2026-05-29', items: 3, status: 'shipped' },
                  { lpn: 'LPN-00514', date: '2026-06-02', items: 1, status: 'pending' },
                  { lpn: 'LPN-00518', date: '2026-06-05', items: 1, status: 'pending' },
                ].map((s, i, arr) => `
                  <div style="display:flex;align-items:center;gap:10px;padding:8px 0;${i < arr.length-1 ? 'border-bottom:1px solid var(--border)' : ''}">
                    <div style="width:32px;height:32px;border-radius:8px;background:${s.status === 'shipped' ? 'var(--success-bg)' : 'var(--warn-bg)'};color:${s.status === 'shipped' ? 'var(--success-text)' : 'var(--warn-text)'};display:grid;place-items:center">${I.Truck(14, 2)}</div>
                    <div style="flex:1;min-width:0">
                      <div class="mono" style="font-size:12px;font-weight:600">${esc(s.lpn)}</div>
                      <div style="font-size:10.5px;color:var(--text-3)"><span class="mono">${esc(D.fmtDateShort(s.date))}</span> · ${esc(s.items)} kalem</div>
                    </div>
                    ${shipBadge(s.status).__raw}
                  </div>
                `).join(''))}
              </div>
            </div>
          </div>
        </div>
      </div>
    `;
  };

  // ============== SATIŞ YENİ ==============
  const SalesNew = (state) => {
    const f = state.formSO = state.formSO || { customer: '', docDate: '2026-05-27', dueDate: '2026-06-10', paymentTerm: '30', notes: '' };
    const errors = state.formSOErrors || {};
    const customers = D.cariList.filter((c) => c.type === 'customer');

    return html`
      <div class="page" data-screen-label="Sales New">
        ${pageHeader({
          crumbs: [{ label: 'Anasayfa' }, { label: 'Satış', route: 'sales' }, { label: 'Yeni Sipariş' }],
          title: 'Yeni Satış Siparişi',
          sub: 'Müşteri seçin, kalemleri ekleyin ve evrakı kaydedin.',
          actions: btn('Vazgeç', { kind: 'ghost', icon: 'X', action: 'nav', route: 'sales', kbd: 'ESC' }),
        })}

        <div style="max-width:720px;margin:0 auto">
          <div class="card">
            <div class="card-hdr">
              <div><div class="card-title">Evrak Başlığı</div><div class="card-sub">Satış siparişi temel bilgileri</div></div>
              <span class="badge badge-warn"><span class="badge-dot"></span>Taslak</span>
            </div>
            <div class="card-body" style="display:flex;flex-direction:column;gap:18px">
              <div class="form-group">
                <label class="form-label">Müşteri <span class="req">*</span></label>
                <select class="form-ctrl${errors.customer ? ' is-error' : ''}" data-form="so" data-field="customer">
                  <option value="">Müşteri seçin…</option>
                  ${customers.map((c) => `<option value="${esc(c.code)}" ${f.customer === c.code ? 'selected' : ''}>${esc(c.name)} · ${esc(c.city)}</option>`).join('')}
                </select>
                ${errors.customer ? `<div class="form-error">${I.AlertCircle(12, 2.4)}${esc(errors.customer)}</div>` : '<div class="form-hint">Adres, vade ve kredi limiti müşteriden otomatik alınır</div>'}
              </div>

              <div class="form-row">
                <div class="form-group">
                  <label class="form-label">Evrak Tarihi <span class="req">*</span></label>
                  <input type="date" class="form-ctrl mono" value="${esc(f.docDate)}" data-form="so" data-field="docDate" />
                </div>
                <div class="form-group">
                  <label class="form-label">Vade Tarihi <span class="req">*</span></label>
                  <input type="date" class="form-ctrl mono${errors.dueDate ? ' is-error' : ''}" value="${esc(f.dueDate)}" data-form="so" data-field="dueDate" />
                  ${errors.dueDate ? `<div class="form-error">${I.AlertCircle(12, 2.4)}${esc(errors.dueDate)}</div>` : ''}
                </div>
              </div>

              <div class="form-row">
                <div class="form-group">
                  <label class="form-label">Ödeme Şartı</label>
                  <select class="form-ctrl" data-form="so" data-field="paymentTerm">
                    <option value="0">Peşin</option><option value="15">15 gün vadeli</option><option value="30" selected>30 gün vadeli</option><option value="60">60 gün vadeli</option>
                  </select>
                </div>
                <div class="form-group">
                  <label class="form-label">Para Birimi</label>
                  <select class="form-ctrl"><option>₺ Türk Lirası (TRY)</option><option>$ ABD Doları (USD)</option><option>€ Euro (EUR)</option></select>
                </div>
              </div>

              <div class="form-group">
                <label class="form-label">Not / Açıklama</label>
                <textarea class="form-ctrl" data-form="so" data-field="notes" placeholder="Sipariş ile ilgili özel not, talimat veya teslimat detayı…">${esc(f.notes)}</textarea>
              </div>
            </div>
            <div class="card-foot">
              <button class="btn btn-ghost btn-sm" data-action="nav" data-route="sales">${I.X(13, 2)}İptal</button>
              <div style="display:flex;gap:8px">
                <button class="btn btn-secondary btn-sm" data-action="so-submit" data-asdraft="1">${I.FileText(13, 2)}Taslak Kaydet</button>
                <button class="btn btn-primary btn-sm" data-action="so-submit">${I.Check(13, 2)}Onayla & Kaydet<span class="btn-kbd">Alt+S</span></button>
              </div>
            </div>
          </div>
        </div>
      </div>
    `;
  };

  // ============== ENVANTER LİSTESİ ==============
  const InventoryList = (state) => {
    const empty = state.tweaks.emptyState;
    const local = state.local;
    local.q = local.q || '';
    local.catFilter = local.catFilter || 'all';

    let rows = empty ? [] : D.products.map((p) => D.stockCardByCode(p.sku));
    if (local.catFilter !== 'all') rows = rows.filter((r) => r.cat === local.catFilter);
    if (local.q) {
      const qL = local.q.toLocaleLowerCase('tr-TR');
      rows = rows.filter((r) => r.sku.toLowerCase().includes(qL) || r.name.toLocaleLowerCase('tr-TR').includes(qL));
    }
    const totalValue = rows.reduce((a, r) => a + r.onhand * r.avgCost, 0);
    const lowCount = rows.filter((r) => r.onhand < r.minStock).length;
    const categories = ['all', ...new Set(D.products.map((p) => p.cat))];

    return html`
      <div class="page" data-screen-label="Envanter" style="max-width:1640px">
        ${pageHeader({
          crumbs: [{ label: 'Anasayfa' }, { label: 'Stok' }, { label: 'Envanter' }],
          title: 'Stok / Envanter',
          sub: `Toplam ${empty ? 0 : D.products.length} SKU · Toplam stok değeri <span class="mono text-strong">${esc(D.fmtTL(totalValue))}</span>`,
          actions: `
            ${btn('İçeri Aktar', { kind: 'secondary', icon: 'Upload' })}
            ${btn('Dışa Aktar', { kind: 'secondary', icon: 'Download' })}
            ${btn('Yeni Ürün', { kind: 'primary', icon: 'Plus', kbd: 'Alt+N' })}
          `,
        })}

        <div class="kpi-grid">
          ${kpiCard({ label: 'Aktif SKU', value: empty ? 0 : D.products.length, glow: 'brand', valueSize: 24, sub: `${categories.length - 1} kategori` })}
          ${kpiCard({ label: 'Stok Değeri', value: empty ? '—' : D.fmtTL(totalValue), glow: 'success', valueSize: 22, sub: 'Hareketli ortalama maliyet' })}
          ${kpiCard({ label: 'Düşük Stok', value: empty ? 0 : lowCount, glow: 'danger', valueSize: 24, valueColor: 'var(--danger-text)', sub: 'Min seviye altında' })}
          ${kpiCard({ label: 'Ortalama Devir', value: empty ? '—' : 32, unit: 'gün', glow: 'warn', valueSize: 24, sub: 'Tüm ürünler' })}
        </div>

        <div class="card">
          <div class="data-table-toolbar" style="border-top:none">
            <div style="position:relative;flex:0 0 280px">
              ${raw(I.Search(14, 2).replace('<svg ', '<svg style="position:absolute;left:11px;top:50%;transform:translateY(-50%);color:var(--text-4)" '))}
              <input class="form-ctrl" style="padding-left:34px;height:34px" placeholder="SKU, ürün adı, barkod ara…" value="${esc(local.q)}" data-action="inv-search" />
            </div>
            <div style="display:flex;gap:4px">
              ${raw(categories.map((cat) => `
                <button class="btn ${local.catFilter === cat ? 'btn-primary' : 'btn-secondary'} btn-sm" data-action="inv-cat" data-cat="${esc(cat)}">${esc(cat === 'all' ? 'Tümü' : cat)}</button>
              `).join(''))}
            </div>
            <button class="btn btn-ghost btn-sm" style="margin-left:auto">${I.Sliders(13, 2)}Sütunlar</button>
          </div>

          ${rows.length === 0 ? emptyState({ icon: 'Box', title: 'Ürün yok', msg: 'İlk ürününüzü oluşturun.' }) : html`
            <table class="data-table">
              <thead><tr><th>SKU</th><th>Ürün</th><th>Kategori</th><th>ABC</th><th class="num">Mevcut</th><th class="num">Rezerve</th><th>Stok Durumu</th><th class="num">Birim Maliyet</th><th class="num">Toplam Değer</th></tr></thead>
              <tbody>
                ${raw(rows.map((r) => {
                  const isLow = r.onhand < r.minStock;
                  const pct = Math.round((r.onhand / r.maxStock) * 100);
                  return `
                    <tr style="cursor:pointer" data-action="nav" data-route="stok/kart/${esc(r.sku)}">
                      <td><span class="mono row-strong" style="color:var(--brand-500)">${esc(r.sku)}</span></td>
                      <td><div class="row-strong">${esc(r.name)}</div><div class="row-sub mono">${esc(r.barcode)}</div></td>
                      <td><span class="badge badge-neutral">${esc(r.cat)}</span></td>
                      <td><span class="badge badge-brand"><span class="badge-dot"></span>${esc(r.abc)}</span></td>
                      <td class="num"><span class="mono row-strong" style="color:${isLow ? 'var(--danger-text)' : 'var(--text)'}">${esc(D.fmtNum(r.onhand))}</span> <span class="muted">${esc(r.uom)}</span></td>
                      <td class="num"><span class="mono muted">${esc(D.fmtNum(r.reserved))}</span></td>
                      <td><div style="display:flex;align-items:center;gap:7px;width:140px">${gauge(pct, 5)}<span class="mono" style="font-size:11px;color:var(--text-3)">%${pct}</span></div></td>
                      <td class="num"><span class="mono">${esc(D.fmtTLDec(r.avgCost))}</span></td>
                      <td class="num"><span class="mono row-strong">${esc(D.fmtTL(r.onhand * r.avgCost))}</span></td>
                    </tr>
                  `;
                }).join(''))}
              </tbody>
            </table>
          `}
        </div>
      </div>
    `;
  };

  // ============== ÜRETİM LİSTE ==============
  const ProductionList = (state) => {
    const empty = state.tweaks.emptyState;
    const local = state.local;
    local.tab = local.tab || 'all';
    let rows = empty ? [] : D.workOrders;
    if (local.tab !== 'all') rows = rows.filter((w) => w.status === local.tab);
    const counts = { all: D.workOrders.length };
    ['Planned', 'Released', 'InProgress', 'Completed', 'Cancelled'].forEach((k) => counts[k] = D.workOrders.filter((w) => w.status === k).length);

    return html`
      <div class="page" data-screen-label="Production" style="max-width:1640px">
        ${pageHeader({
          crumbs: [{ label: 'Anasayfa' }, { label: 'Üretim' }, { label: 'İş Emirleri' }],
          title: 'İmalat Emirleri',
          sub: `${empty ? 0 : D.workOrders.length} aktif iş emri · ${counts.InProgress} hat üzerinde`,
          actions: `${btn('Dışa Aktar', { kind: 'secondary', icon: 'Download' })}${btn('Yeni İş Emri', { kind: 'primary', icon: 'Plus', kbd: 'Alt+N' })}`,
        })}

        <div class="kpi-grid">
          ${kpiCard({ label: 'Aktif İş Emri', value: empty ? 0 : counts.InProgress + counts.Released, glow: 'brand', valueSize: 24, sub: `${counts.InProgress} üretimde · ${counts.Released} bekliyor` })}
          ${kpiCard({ label: 'Bu Ay Tamamlanan', value: empty ? 0 : counts.Completed, unit: 'emir', glow: 'success', valueSize: 24, sub: '%96 OEE ortalama' })}
          ${kpiCard({ label: 'Hat Doluluk', value: empty ? '—' : '%84', glow: 'success', valueSize: 24, sub: '5 üretim hattı aktif' })}
          ${kpiCard({ label: 'Geciken', value: empty ? 0 : 2, unit: 'emir', glow: 'danger', valueSize: 24, valueColor: 'var(--danger-text)', sub: 'Termin aşımı' })}
        </div>

        <div class="card">
          ${tabs([
            { id: 'all', label: 'Tümü', count: counts.all },
            { id: 'Planned', label: 'Planlandı', count: counts.Planned },
            { id: 'Released', label: 'Açıldı', count: counts.Released },
            { id: 'InProgress', label: 'Üretimde', count: counts.InProgress },
            { id: 'Completed', label: 'Tamamlandı', count: counts.Completed },
            { id: 'Cancelled', label: 'İptal', count: counts.Cancelled },
          ], local.tab, 'prod-tab')}

          ${rows.length === 0 ? emptyState({ icon: 'Factory', title: 'İş emri yok' }) : html`
            <table class="data-table">
              <thead><tr><th>Emir No</th><th>Mamul</th><th>Hat</th><th>Öncelik</th><th class="num">Planlanan</th><th class="num">Üretilen</th><th>İlerleme</th><th>Operatör</th><th>Termin</th><th>Durum</th></tr></thead>
              <tbody>
                ${raw(rows.map((w) => {
                  const pct = Math.round((w.produced / w.planned) * 100);
                  return `
                    <tr style="cursor:pointer" data-action="nav" data-route="production/detail/${esc(w.no)}">
                      <td><span class="mono row-strong" style="color:var(--brand-500)">${esc(w.no)}</span></td>
                      <td><div class="row-strong">${esc(w.productName)}</div><div class="row-sub mono">${esc(w.product)}</div></td>
                      <td><span class="badge badge-neutral mono">${esc(w.line)}</span></td>
                      <td>${priorityBadge(w.priority).__raw}</td>
                      <td class="num mono">${esc(D.fmtNum(w.planned))}</td>
                      <td class="num"><span class="mono row-strong">${esc(D.fmtNum(w.produced))}</span></td>
                      <td><div style="display:flex;align-items:center;gap:7px;width:140px">${gauge(pct, 5)}<span class="mono" style="font-size:11px;color:var(--text-3)">%${pct}</span></div></td>
                      <td><div style="display:flex;align-items:center;gap:7px">${avatar(initials(w.operator), { size: 22, fontSize: 9, color: w.operatorColor }).__raw}<span style="font-size:12px">${esc(w.operator)}</span></div></td>
                      <td class="mono muted">${esc(D.fmtDateShort(w.due))}</td>
                      <td>${woStatusBadge(w.status).__raw}</td>
                    </tr>
                  `;
                }).join(''))}
              </tbody>
            </table>
          `}
        </div>
      </div>
    `;
  };

  // ============== ÜRETİM DETAY ==============
  const ProductionDetail = (state, woNo) => {
    const w = D.workOrderByNo(woNo);
    const bom = D.bomFor(woNo);
    const routing = D.routingFor();
    const pct = Math.round((w.produced / w.planned) * 100);

    return html`
      <div class="page" data-screen-label="Production Detail" style="max-width:1480px">
        ${pageHeader({
          crumbs: [
            { label: 'Anasayfa' },
            { label: 'Üretim', route: 'production' },
            { label: 'İş Emirleri', route: 'production' },
            { label: w.no, mono: true },
          ],
          titleSlot: html`
            <div style="display:flex;align-items:center;gap:12px;margin-top:4px">
              <h1 class="mono" style="letter-spacing:-0.01em">${w.no}</h1>
              ${woStatusBadge(w.status)}
              ${priorityBadge(w.priority)}
            </div>
          `,
          sub: `${esc(w.productName)} · <span class="mono">${esc(w.product)}</span> · Hat ${esc(w.line)} · Termin: <span class="mono">${esc(D.fmtDate(w.due))}</span>`,
          actions: `
            ${btn('Geri', { kind: 'ghost', icon: 'ChevronLeft', action: 'nav', route: 'production' })}
            ${btn('İş Emri PDF', { kind: 'secondary', icon: 'Download' })}
            ${w.status === 'Planned' ? btn('Aç', { kind: 'primary', icon: 'Check' }) : ''}
            ${w.status === 'Released' ? btn('Üretimi Başlat', { kind: 'success', icon: 'Lightning' }) : ''}
            ${w.status === 'InProgress' ? btn('İş Emrini Bitir', { kind: 'success', icon: 'CheckCircle' }) : ''}
          `,
        })}

        <div class="kpi-grid">
          ${kpiCard({ label: 'Üretim İlerlemesi', value: `%${pct}`, glow: 'brand', valueSize: 22, sub: `${D.fmtNum(w.produced)} / ${D.fmtNum(w.planned)} adet` })}
          ${kpiCard({ label: 'Sarf Edilen Malzeme', value: bom.length, unit: 'kalem', glow: 'success', valueSize: 22, sub: 'BOM\'a göre' })}
          ${kpiCard({ label: 'Tamamlanan Operasyon', value: `${routing.filter((r) => r.status === 'done').length} / ${routing.length}`, glow: 'brand', valueSize: 22, sub: 'Routing adımları' })}
          ${kpiCard({ label: 'Toplam Çalışma Süresi', value: routing.reduce((a, r) => a + r.time, 0), unit: 'sa', glow: 'warn', valueSize: 22, sub: 'Plan: 68 sa' })}
        </div>

        <div style="display:grid;grid-template-columns:1fr 1fr;gap:14px;margin-bottom:14px">
          <div class="card">
            <div class="card-hdr"><div><div class="card-title">Reçete (BOM)</div><div class="card-sub">${bom.length} hammadde · 1 adet mamul için</div></div></div>
            <div class="card-body-flush">
              <table class="data-table">
                <thead><tr><th>SKU</th><th>Hammadde</th><th class="num">Birim Kullanım</th><th class="num">Tüketilen</th><th>Fire</th></tr></thead>
                <tbody>
                  ${raw(bom.map((b) => `
                    <tr>
                      <td><span class="mono row-strong" style="color:var(--brand-500);cursor:pointer" data-action="nav" data-route="stok/kart/${esc(b.sku)}">${esc(b.sku)}</span></td>
                      <td>${esc(b.name)}</td>
                      <td class="num"><span class="mono">${esc(D.fmtNum(b.qty, b.uom === 'KG' ? 2 : 0))}</span> <span class="muted">${esc(b.uom)}</span></td>
                      <td class="num"><span class="mono row-strong">${esc(D.fmtNum(b.consumed, b.uom === 'KG' ? 1 : 0))}</span> <span class="muted">${esc(b.uom)}</span></td>
                      <td>${b.scrap > 0 ? `<span class="badge badge-warn">%${(b.scrap*100).toFixed(0)}</span>` : '<span class="muted">—</span>'}</td>
                    </tr>
                  `).join(''))}
                </tbody>
              </table>
            </div>
          </div>

          <div class="card">
            <div class="card-hdr"><div><div class="card-title">Operasyon Rotası</div><div class="card-sub">Routing adımları ve durumları</div></div></div>
            <div class="card-body" style="padding:14px 18px">
              ${raw(routing.map((r, i) => {
                const c = r.status === 'done' ? 'var(--success)' : r.status === 'active' ? 'var(--brand-500)' : 'var(--text-mute)';
                const bg = r.status === 'done' ? 'var(--success-bg)' : r.status === 'active' ? 'var(--brand-tint-08)' : 'var(--bg-2)';
                return `
                  <div style="display:flex;align-items:flex-start;gap:12px;padding:10px 0;${i < routing.length-1 ? 'border-bottom:1px solid var(--border)' : ''}">
                    <div style="width:28px;height:28px;border-radius:50%;background:${bg};color:${c};display:grid;place-items:center;font-weight:700;font-size:11px;flex-shrink:0">${r.status === 'done' ? I.Check(13, 3) : (r.status === 'active' ? I.Lightning(13, 2) : r.op)}</div>
                    <div style="flex:1;min-width:0">
                      <div style="display:flex;align-items:center;gap:8px"><span style="font-size:13px;font-weight:700">${esc(r.name)}</span><span class="mono" style="font-size:10.5px;color:var(--text-4)">OP ${esc(r.op)}</span></div>
                      <div style="font-size:11px;color:var(--text-3);margin-top:2px"><span class="mono">${esc(r.station)}</span> · ${esc(r.time)} sa · Operatör: ${esc(r.operator)}</div>
                    </div>
                    ${r.status === 'done' ? '<span class="badge badge-success"><span class="badge-dot"></span>Tamamlandı</span>' :
                      r.status === 'active' ? '<span class="badge badge-brand"><span class="badge-dot"></span>Devam Ediyor</span>' :
                      '<span class="badge badge-neutral"><span class="badge-dot"></span>Bekliyor</span>'}
                  </div>
                `;
              }).join(''))}
            </div>
          </div>
        </div>

        <div class="card">
          <div class="card-hdr">
            <div><div class="card-title">İş Emri Bilgileri</div></div>
          </div>
          <div class="card-body" style="display:grid;grid-template-columns:repeat(4,1fr);gap:18px">
            <div><div class="form-label">Hat</div><div style="font-size:13px;font-weight:600;margin-top:4px"><span class="mono">${esc(w.line)}</span> · Üretim Hattı</div></div>
            <div><div class="form-label">Operatör</div><div style="display:flex;align-items:center;gap:8px;margin-top:4px">${avatar(initials(w.operator), { size: 22, fontSize: 9.5, color: w.operatorColor })}<span style="font-size:13px;font-weight:600">${esc(w.operator)}</span></div></div>
            <div><div class="form-label">Planlanan</div><div class="mono" style="font-size:13px;font-weight:600;margin-top:4px">${esc(D.fmtNum(w.planned))} adet</div></div>
            <div><div class="form-label">Üretilen</div><div class="mono" style="font-size:13px;font-weight:600;margin-top:4px;color:var(--success-text)">${esc(D.fmtNum(w.produced))} adet</div></div>
            <div><div class="form-label">Başlangıç</div><div class="mono" style="font-size:13px;font-weight:600;margin-top:4px">${esc(D.fmtDate(w.start))}</div></div>
            <div><div class="form-label">Termin</div><div class="mono" style="font-size:13px;font-weight:600;margin-top:4px">${esc(D.fmtDate(w.due))}</div></div>
            <div><div class="form-label">Kategori</div><div style="font-size:13px;font-weight:600;margin-top:4px">${esc(w.cat)}</div></div>
            <div><div class="form-label">Öncelik</div><div style="margin-top:4px">${priorityBadge(w.priority)}</div></div>
          </div>
        </div>
      </div>
    `;
  };

  // ============== MUHASEBE — YEVMİYE LİSTE ==============
  const AccountingList = (state) => {
    const empty = state.tweaks.emptyState;
    const local = state.local;
    local.tab = local.tab || 'all';
    let rows = empty ? [] : D.journalEntries;
    if (local.tab !== 'all') rows = rows.filter((j) => j.status === local.tab);

    const counts = {
      all: D.journalEntries.length,
      posted: D.journalEntries.filter((j) => j.status === 'posted').length,
      draft: D.journalEntries.filter((j) => j.status === 'draft').length,
      cancelled: D.journalEntries.filter((j) => j.status === 'cancelled').length,
    };
    const totalPosted = D.journalEntries.filter((j) => j.status === 'posted').reduce((a, j) => a + j.total, 0);

    return html`
      <div class="page" data-screen-label="Muhasebe" style="max-width:1640px">
        ${pageHeader({
          crumbs: [{ label: 'Anasayfa' }, { label: 'Muhasebe' }, { label: 'Yevmiye Fişleri' }],
          title: 'Yevmiye Fişleri',
          sub: `Bu dönem ${empty ? 0 : counts.all} fiş · İşlenen tutar <span class="mono text-strong">${esc(D.fmtTL(totalPosted))}</span>`,
          actions: `${btn('Mizan', { kind: 'secondary', icon: 'BarChart' })}${btn('Hesap Planı', { kind: 'secondary', icon: 'Database' })}${btn('Yeni Fiş', { kind: 'primary', icon: 'Plus', kbd: 'Alt+N' })}`,
        })}

        <div class="kpi-grid">
          ${kpiCard({ label: 'Bu Ay Fiş', value: empty ? 0 : counts.all, glow: 'brand', valueSize: 24, sub: `${counts.posted} işlendi · ${counts.draft} taslak` })}
          ${kpiCard({ label: 'İşlenen Tutar', value: empty ? '—' : D.fmtTL(totalPosted), glow: 'success', valueSize: 22, sub: 'Onaylanmış kayıtlar' })}
          ${kpiCard({ label: 'Bekleyen Onay', value: empty ? 0 : counts.draft, glow: 'warn', valueSize: 24, sub: 'Taslak fişler' })}
          ${kpiCard({ label: 'Dönem Sonu Bakiye', value: empty ? '—' : '₺18.4M', glow: 'brand', valueSize: 22, sub: 'Mizan toplam' })}
        </div>

        <div class="card">
          ${tabs([
            { id: 'all', label: 'Tümü', count: counts.all },
            { id: 'posted', label: 'İşlendi', count: counts.posted },
            { id: 'draft', label: 'Taslak', count: counts.draft },
            { id: 'cancelled', label: 'İptal', count: counts.cancelled },
          ], local.tab, 'acct-tab')}

          ${rows.length === 0 ? emptyState({ icon: 'Coin', title: 'Fiş yok' }) : html`
            <table class="data-table">
              <thead><tr><th>Fiş No</th><th>Tür</th><th>Tarih</th><th>Açıklama</th><th class="num">Tutar</th><th>Kullanıcı</th><th>Durum</th><th style="width:36px"></th></tr></thead>
              <tbody>
                ${raw(rows.map((j) => `
                  <tr style="cursor:pointer" data-action="nav" data-route="accounting/detail/${esc(j.no)}">
                    <td><span class="mono row-strong" style="color:var(--brand-500)">${esc(j.no)}</span></td>
                    <td><span class="badge badge-info">${esc(j.type.label)}</span></td>
                    <td class="mono muted">${esc(D.fmtDateShort(j.date))}</td>
                    <td>${esc(j.desc)}</td>
                    <td class="num"><span class="mono row-strong">${esc(D.fmtTL(j.total))}</span></td>
                    <td><div style="display:flex;align-items:center;gap:7px">${avatar(initials(j.user), { size: 22, fontSize: 9, color: j.userColor }).__raw}<span style="font-size:12px">${esc(j.user)}</span></div></td>
                    <td>${statusBadge(j.status).__raw}</td>
                    <td>${I.ChevronRight(14, 2)}</td>
                  </tr>
                `).join(''))}
              </tbody>
            </table>
          `}
        </div>
      </div>
    `;
  };

  // ============== MUHASEBE — YEVMİYE DETAY ==============
  const AccountingDetail = (state, no) => {
    const j = D.journalEntries.find((x) => x.no === no) || D.journalEntries[0];
    const lines = D.journalLinesFor(j.no);
    const totalDebit = lines.reduce((a, l) => a + l.debit, 0);
    const totalCredit = lines.reduce((a, l) => a + l.credit, 0);

    return html`
      <div class="page" data-screen-label="Yevmiye Detay" style="max-width:1280px">
        ${pageHeader({
          crumbs: [
            { label: 'Anasayfa' },
            { label: 'Muhasebe', route: 'accounting' },
            { label: 'Fişler', route: 'accounting' },
            { label: j.no, mono: true },
          ],
          titleSlot: html`
            <div style="display:flex;align-items:center;gap:12px;margin-top:4px">
              <h1 class="mono" style="letter-spacing:-0.01em">${j.no}</h1>
              ${statusBadge(j.status)}
              <span class="badge badge-info">${j.type.label}</span>
            </div>
          `,
          sub: `${esc(j.desc)} · <span class="mono">${esc(D.fmtDate(j.date))}</span> · Kayıt: ${esc(j.user)}`,
          actions: `
            ${btn('Geri', { kind: 'ghost', icon: 'ChevronLeft', action: 'nav', route: 'accounting' })}
            ${btn('PDF', { kind: 'secondary', icon: 'Download' })}
            ${j.status === 'draft' ? btn('Onayla', { kind: 'success', icon: 'Check', kbd: 'Alt+S' }) : ''}
          `,
        })}

        <div class="card" style="margin-bottom:14px">
          <div class="card-hdr"><div><div class="card-title">Fiş Satırları</div><div class="card-sub">${lines.length} satır · Borç/Alacak dengeli</div></div></div>
          <div class="card-body-flush">
            <table class="data-table">
              <thead><tr><th style="width:40px" class="num">#</th><th>Hesap Kodu</th><th>Hesap Adı</th><th>Açıklama</th><th class="num">Borç</th><th class="num">Alacak</th></tr></thead>
              <tbody>
                ${raw(lines.map((l) => `
                  <tr>
                    <td class="num muted mono">${esc(l.line)}</td>
                    <td><span class="mono row-strong" style="color:var(--brand-500)">${esc(l.account)}</span></td>
                    <td><span class="row-strong">${esc(l.accountName)}</span></td>
                    <td class="muted">${esc(l.desc)}</td>
                    <td class="num">${l.debit > 0 ? `<span class="mono row-strong" style="color:var(--success-text)">${esc(D.fmtTLDec(l.debit))}</span>` : '<span class="muted">—</span>'}</td>
                    <td class="num">${l.credit > 0 ? `<span class="mono row-strong" style="color:var(--danger-text)">${esc(D.fmtTLDec(l.credit))}</span>` : '<span class="muted">—</span>'}</td>
                  </tr>
                `).join(''))}
              </tbody>
            </table>
          </div>
          <div class="card-foot">
            <div style="font-size:11.5px;color:var(--text-3)">Çift kayıt sistemi · Borç = Alacak ${totalDebit === totalCredit ? '✓' : '✗'}</div>
            <div style="display:flex;gap:24px;font-size:12.5px">
              <div style="display:flex;flex-direction:column;align-items:flex-end"><span style="color:var(--text-3);font-size:10px;font-weight:700;text-transform:uppercase">Toplam Borç</span><span class="mono" style="font-weight:700;color:var(--success-text)">${D.fmtTLDec(totalDebit)}</span></div>
              <div style="display:flex;flex-direction:column;align-items:flex-end"><span style="color:var(--text-3);font-size:10px;font-weight:700;text-transform:uppercase">Toplam Alacak</span><span class="mono" style="font-weight:700;color:var(--danger-text)">${D.fmtTLDec(totalCredit)}</span></div>
            </div>
          </div>
        </div>

        <div class="card">
          <div class="card-hdr"><div class="card-title">Audit / Denetim Kaydı</div></div>
          <div class="card-body" style="padding:14px 18px">
            ${raw([
              { who: initials(j.user), name: j.user, text: 'Fişi kaydetti', t: '14:22', d: '22 May', color: j.userColor },
              { who: 'MY', name: 'Mehmet Yılmaz', text: 'Hesap planını güncelledi', t: '11:08', d: '21 May', color: 'hsl(243 75% 59%)' },
            ].map((a, i, arr) => `
              <div style="display:flex;gap:10px;padding:8px 0;${i < arr.length-1 ? 'border-bottom:1px solid var(--border)' : ''}">
                ${avatar(a.who, { size: 26, fontSize: 9.5, color: a.color }).__raw}
                <div style="flex:1"><div style="font-size:12px;line-height:1.4"><span style="font-weight:600">${esc(a.name)}</span> <span style="color:var(--text-2)">${esc(a.text)}</span></div><div class="mono" style="font-size:10.5px;color:var(--text-4)">${esc(a.d)} · ${esc(a.t)}</div></div>
              </div>
            `).join(''))}
          </div>
        </div>
      </div>
    `;
  };

  // ============== KASA / BANKA ==============
  const CashBank = (state) => {
    const empty = state.tweaks.emptyState;
    const local = state.local;
    local.account = local.account || 'all';
    const accs = D.cashBankAccounts;
    let rows = empty ? [] : D.cashBankMovements;
    if (local.account !== 'all') rows = rows.filter((m) => m.account === local.account);
    const totalBal = accs.reduce((a, b) => a + (b.currency === 'TL' ? b.bal : 0), 0);

    return html`
      <div class="page" data-screen-label="Kasa Banka" style="max-width:1640px">
        ${pageHeader({
          crumbs: [{ label: 'Anasayfa' }, { label: 'Finans' }, { label: 'Kasa / Banka' }],
          title: 'Kasa & Banka Hesapları',
          sub: `TRY bakiye toplamı <span class="mono text-strong">${esc(D.fmtTL(totalBal))}</span> · ${accs.length} aktif hesap`,
          actions: `${btn('Mutabakat', { kind: 'secondary', icon: 'CheckCircle' })}${btn('Yeni Hareket', { kind: 'primary', icon: 'Plus', kbd: 'Alt+N' })}`,
        })}

        <div class="kpi-grid" style="grid-template-columns:repeat(4,1fr)">
          ${raw(accs.map((a, i) => {
            const sym = a.currency === 'TL' ? '₺' : a.currency === 'USD' ? '$' : '€';
            return `
              <div class="kpi" style="cursor:pointer" data-action="cb-acc" data-acc="${esc(a.id)}">
                <div class="kpi-glow ${a.type === 'cash' ? 'warn' : 'brand'}"></div>
                <div class="kpi-label" style="display:flex;align-items:center;gap:6px">${a.type === 'cash' ? I.Coin(13, 2) : I.CreditCard(13, 2)}${esc(a.name.split(' · ')[0])}</div>
                <div class="kpi-value" style="font-size:22px">${sym}${new Intl.NumberFormat('tr-TR').format(a.bal)}</div>
                <div class="kpi-trend" style="margin-top:6px">${esc(a.name.split(' · ').slice(1).join(' · '))}</div>
              </div>
            `;
          }).join(''))}
        </div>

        <div class="card">
          <div class="data-table-toolbar" style="border-top:none">
            <div style="display:flex;gap:4px">
              <button class="btn ${local.account === 'all' ? 'btn-primary' : 'btn-secondary'} btn-sm" data-action="cb-acc" data-acc="all">Tüm Hesaplar</button>
              ${raw(accs.map((a) => `<button class="btn ${local.account === a.id ? 'btn-primary' : 'btn-secondary'} btn-sm" data-action="cb-acc" data-acc="${esc(a.id)}">${esc(a.name.split(' · ')[0])}</button>`).join(''))}
            </div>
            <div class="spacer"></div>
            <button class="chip">${I.Calendar(12, 2)}Son 30 gün${I.ChevronDown(11, 2)}</button>
          </div>
          ${rows.length === 0 ? emptyState({ icon: 'Database', title: 'Hareket yok' }) : html`
            <table class="data-table">
              <thead><tr><th>Tarih</th><th>Fiş No</th><th>Hesap</th><th>Tür</th><th>Karşı Taraf</th><th>Açıklama</th><th class="num">Tutar</th><th>Para Birimi</th></tr></thead>
              <tbody>
                ${raw(rows.map((m) => `
                  <tr>
                    <td class="mono muted">${esc(D.fmtDateShort(m.date))}</td>
                    <td><span class="mono row-strong" style="color:var(--brand-500)">${esc(m.id)}</span></td>
                    <td><span class="row-strong">${esc(m.accountName.split(' · ')[0])}</span></td>
                    <td>${m.kind === 'in' ? '<span class="badge badge-success"><span class="badge-dot"></span>Tahsilat</span>' : '<span class="badge badge-danger"><span class="badge-dot"></span>Ödeme</span>'}</td>
                    <td>${esc(m.counterparty)}</td>
                    <td class="muted">${esc(m.desc)}</td>
                    <td class="num"><span class="mono row-strong" style="color:${m.kind === 'in' ? 'var(--success-text)' : 'var(--danger-text)'}">${m.kind === 'in' ? '+' : '−'}${esc(D.fmtTLDec(m.amount))}</span></td>
                    <td class="mono">${esc(m.currency)}</td>
                  </tr>
                `).join(''))}
              </tbody>
            </table>
          `}
        </div>
      </div>
    `;
  };

  // ============== RAPORLAR ==============
  const ReportsScreen = (state) => {
    const groups = [
      { label: 'Satınalma', icon: 'Cart', reports: [
        { id: 'po-perf', name: 'Aylık Satınalma Performansı', desc: 'Onaylı/Taslak/İptal tutar dağılımı' },
        { id: 'sup-perf', name: 'Tedarikçi Performansı', desc: 'Termin, kalite, fiyat değişimi' },
        { id: 'po-ageing', name: 'Açık Sipariş Yaşlandırma', desc: 'Vade aşımı analizi' },
      ]},
      { label: 'Satış', icon: 'Tag', reports: [
        { id: 'so-perf', name: 'Aylık Satış Performansı', desc: 'Müşteri ve ürün bazlı ciro' },
        { id: 'top-cust', name: 'En Çok Alan Müşteriler', desc: 'Top 20 müşteri raporu' },
        { id: 'so-fulfill', name: 'Sevkiyat Sürelerimiz', desc: 'Ortalama sevkiyat süresi' },
      ]},
      { label: 'Stok', icon: 'Box', reports: [
        { id: 'inv-val', name: 'Stok Değerlendirme', desc: 'Ortalama maliyet · ABC bazlı' },
        { id: 'inv-aged', name: 'Stok Yaşlandırma', desc: 'Yavaş hareket eden stoklar' },
        { id: 'cnt-var', name: 'Sayım Farkları', desc: 'Q1/Q2 sayım farkları' },
      ]},
      { label: 'Finans', icon: 'Coin', reports: [
        { id: 'trial-balance', name: 'Mizan', desc: 'Hesap bakiyeleri ve dönem hareketleri' },
        { id: 'cari-ageing', name: 'Cari Yaşlandırma', desc: 'Vadesi geçen alacak/borç' },
        { id: 'pl', name: 'Gelir Tablosu (P&L)', desc: 'Dönemsel kâr/zarar' },
        { id: 'cash-flow', name: 'Nakit Akış Tablosu', desc: 'Giriş/Çıkış dengesi' },
      ]},
    ];

    return html`
      <div class="page" data-screen-label="Raporlar" style="max-width:1480px">
        ${pageHeader({
          crumbs: [{ label: 'Anasayfa' }, { label: 'Finans' }, { label: 'Raporlar' }],
          title: 'Rapor Merkezi',
          sub: 'Hazır raporlar, dönemsel analizler ve yönetim panelleri',
          actions: `${btn('Planlanmış Raporlar', { kind: 'secondary', icon: 'Clock' })}${btn('Yeni Rapor', { kind: 'primary', icon: 'Plus' })}`,
        })}

        ${raw(groups.map((g) => `
          <div style="margin-bottom:18px">
            <div style="display:flex;align-items:center;gap:8px;margin-bottom:10px;color:var(--text-3)">
              ${I[g.icon](14, 2)}
              <span style="font-size:11px;font-weight:700;text-transform:uppercase;letter-spacing:0.08em">${esc(g.label)}</span>
            </div>
            <div style="display:grid;grid-template-columns:repeat(3,1fr);gap:12px">
              ${g.reports.map((r) => `
                <div class="card lift" style="cursor:pointer;padding:14px;display:flex;gap:12px;align-items:flex-start" data-action="nav" data-route="reports/view/${esc(r.id)}">
                  <div style="width:36px;height:36px;border-radius:9px;background:var(--brand-tint-08);color:var(--brand-500);display:grid;place-items:center;flex-shrink:0">${I.BarChart(16, 2)}</div>
                  <div style="flex:1;min-width:0">
                    <div style="font-size:13px;font-weight:700;color:var(--text)">${esc(r.name)}</div>
                    <div style="font-size:11.5px;color:var(--text-3);margin-top:3px">${esc(r.desc)}</div>
                    <div style="display:flex;gap:6px;margin-top:8px">
                      <span class="badge badge-neutral" style="height:18px;font-size:9.5px">PDF</span>
                      <span class="badge badge-neutral" style="height:18px;font-size:9.5px">Excel</span>
                    </div>
                  </div>
                  ${I.ChevronRight(14, 2)}
                </div>
              `).join('')}
            </div>
          </div>
        `).join(''))}
      </div>
    `;
  };

  // ============== RAPOR DETAY (örnek) ==============
  const ReportView = (state, reportId) => {
    const months = ['Ara', 'Oca', 'Şub', 'Mar', 'Nis', 'May'];
    const series = D.sparkSeries(6, 920, 90, 7);
    const totalSeries = series.reduce((a, b) => a + b, 0);

    return html`
      <div class="page" data-screen-label="Rapor" style="max-width:1480px">
        ${pageHeader({
          crumbs: [{ label: 'Anasayfa' }, { label: 'Raporlar', route: 'reports' }, { label: 'Aylık Satınalma Performansı' }],
          title: 'Aylık Satınalma Performansı',
          sub: 'Son 6 ay · Onaylanmış evraklar · Müşteri bazlı kırılım',
          actions: `${btn('Geri', { kind: 'ghost', icon: 'ChevronLeft', action: 'nav', route: 'reports' })}${btn('PDF İndir', { kind: 'secondary', icon: 'Download' })}${btn('Excel', { kind: 'secondary', icon: 'Download' })}`,
        })}

        <div class="kpi-grid" style="grid-template-columns:repeat(4,1fr)">
          ${kpiCard({ label: 'Toplam Tutar (6 ay)', value: `₺${(totalSeries/1000).toFixed(1)}M`, glow: 'brand', valueSize: 22, sub: 'Onaylı evraklar' })}
          ${kpiCard({ label: 'Ortalama / Ay', value: `₺${(totalSeries/6/1000).toFixed(1)}M`, glow: 'success', valueSize: 22, sub: '+%14 büyüme' })}
          ${kpiCard({ label: 'Toplam Evrak', value: 84, glow: 'brand', valueSize: 24, sub: '14 / ay ortalama' })}
          ${kpiCard({ label: 'Aktif Tedarikçi', value: 8, glow: 'warn', valueSize: 24, sub: 'En aktif: Türkbasınç' })}
        </div>

        <div class="card" style="margin-bottom:14px">
          <div class="card-hdr"><div><div class="card-title">Aylık Tutar Trendi</div><div class="card-sub">Sadece onaylanmış (Posted) evraklar</div></div></div>
          <div class="card-body" style="padding:24px 18px 18px">
            <div style="display:grid;grid-template-columns:repeat(6,1fr);gap:14px;align-items:flex-end;height:240px">
              ${raw(months.map((m, i) => {
                const max = Math.max(...series);
                const h = (series[i] / max) * 100;
                const isLast = i === months.length - 1;
                return `
                  <div style="display:flex;flex-direction:column;align-items:center;gap:8px;height:100%">
                    <div class="mono" style="font-size:11px;color:var(--text-3);font-weight:700">₺${(series[i]/1000).toFixed(2)}M</div>
                    <div style="flex:1;width:100%;max-width:80px;display:flex;flex-direction:column;justify-content:flex-end">
                      <div style="height:${h}%;background:${isLast ? 'var(--brand-grad)' : 'hsl(243 75% 59% / 0.8)'};border-radius:6px 6px 0 0;box-shadow:${isLast ? '0 6px 18px hsl(243 75% 59% / 0.35)' : 'none'}"></div>
                    </div>
                    <div style="font-size:12px;color:${isLast ? 'var(--text)' : 'var(--text-3)'};font-weight:${isLast ? 700 : 500}">${esc(m)}</div>
                  </div>
                `;
              }).join(''))}
            </div>
          </div>
        </div>

        <div class="card">
          <div class="card-hdr"><div><div class="card-title">Tedarikçi Bazlı Dağılım</div><div class="card-sub">İlk 6 tedarikçi · son 6 ay</div></div></div>
          <div class="card-body-flush">
            <table class="data-table">
              <thead><tr><th>Tedarikçi</th><th class="num">Evrak Sayısı</th><th class="num">Toplam Tutar</th><th>Pay</th><th class="num">Ort. Termin</th></tr></thead>
              <tbody>
                ${raw(D.suppliers.slice(0, 6).map((s, i) => {
                  const total = 380000 + (i * 211000);
                  const pct = Math.round((total / 2_400_000) * 100);
                  return `
                    <tr style="cursor:pointer" data-action="nav" data-route="cari/detail/${esc(s.id)}">
                      <td><div style="display:flex;align-items:center;gap:10px">${avatar(s.id, { size: 26, fontSize: 10 }).__raw}<div><div class="row-strong">${esc(s.name)}</div><div class="row-sub">${esc(s.city)}</div></div></div></td>
                      <td class="num mono">${esc(18 - i * 2)}</td>
                      <td class="num"><span class="mono row-strong">${esc(D.fmtTL(total))}</span></td>
                      <td><div style="display:flex;align-items:center;gap:8px;width:180px">${gauge(pct, 5)}<span class="mono" style="font-size:11px;color:var(--text-3)">%${pct}</span></div></td>
                      <td class="num mono">${esc(7 + i)} gün</td>
                    </tr>
                  `;
                }).join(''))}
              </tbody>
            </table>
          </div>
        </div>
      </div>
    `;
  };

  // ============== KULLANICILAR ==============
  const UsersScreen = (state) => {
    const empty = state.tweaks.emptyState;
    const local = state.local;
    local.filter = local.filter || 'all';
    const filtered = empty ? [] : (local.filter === 'all' ? D.users : D.users.filter((u) => u.status === local.filter));
    const counts = {
      all: D.users.length,
      active: D.users.filter((u) => u.status === 'active').length,
      pending: D.users.filter((u) => u.status === 'pending').length,
      inactive: D.users.filter((u) => u.status === 'inactive').length,
    };

    return html`
      <div class="page" data-screen-label="Users">
        ${pageHeader({
          crumbs: [{ label: 'Anasayfa' }, { label: 'Sistem' }, { label: 'Kullanıcılar' }],
          title: 'Kullanıcı Yönetimi',
          sub: 'Şirket içi kullanıcılar, roller ve erişim yetkileri',
          actions: `${btn('Davetiye Gönder', { kind: 'secondary', icon: 'Mail' })}${btn('Yeni Kullanıcı', { kind: 'primary', icon: 'Plus', action: 'nav', route: 'users/new', kbd: 'Alt+N' })}`,
        })}

        <div class="card">
          ${tabs([
            { id: 'all', label: 'Tümü', count: counts.all },
            { id: 'active', label: 'Aktif', count: counts.active },
            { id: 'pending', label: 'Bekleyen Davet', count: counts.pending },
            { id: 'inactive', label: 'Pasif', count: counts.inactive },
          ], local.filter, 'users-tab')}

          ${filtered.length === 0 ? emptyState({ icon: 'Users', title: 'Henüz kullanıcı yok', action: btn('Yeni Kullanıcı', { kind: 'primary', icon: 'Plus', action: 'nav', route: 'users/new' }) }) : html`
            <table class="data-table">
              <thead><tr><th>Kullanıcı</th><th>Rol</th><th>Departman</th><th>Durum</th><th>Son Giriş</th><th style="width:80px"></th></tr></thead>
              <tbody>
                ${raw(filtered.map((u) => {
                  const statusBg = u.status === 'active' ? 'success' : u.status === 'pending' ? 'warn' : 'danger';
                  const statusLabel = u.status === 'active' ? 'Aktif' : u.status === 'pending' ? 'Davet Bekliyor' : 'Pasif';
                  const roleBadge = u.role === 'Yönetici' ? 'brand' : u.role === 'Operatör' ? 'info' : 'neutral';
                  return `
                    <tr>
                      <td><div style="display:flex;align-items:center;gap:10px">${avatar(u.avatar, { size: 28, color: u.color }).__raw}<div><div class="row-strong">${esc(u.name)}</div><div class="row-sub mono">${esc(u.email)}</div></div></div></td>
                      <td><span class="badge badge-${roleBadge}"><span class="badge-dot"></span>${esc(u.role)}</span></td>
                      <td>${esc(u.dept)}</td>
                      <td><span class="badge badge-${statusBg}"><span class="badge-dot"></span>${esc(statusLabel)}</span></td>
                      <td class="muted mono">${u.last ? esc(D.fmtDateTime(u.last)) : '—'}</td>
                      <td><div style="display:flex;gap:4px"><button class="icon-btn" style="width:26px;height:26px">${I.Edit(13, 2)}</button><button class="icon-btn" style="width:26px;height:26px">${I.More(14, 2)}</button></div></td>
                    </tr>
                  `;
                }).join(''))}
              </tbody>
            </table>
          `}
        </div>
      </div>
    `;
  };

  // ============== YENİ KULLANICI ==============
  const UserNew = (state) => {
    const f = state.formUser = state.formUser || { firstName: '', lastName: '', email: '', role: '', dept: '', sendInvite: true, twoFA: true };
    const errors = state.formUserErrors || {};

    return html`
      <div class="page" data-screen-label="User New">
        ${pageHeader({
          crumbs: [{ label: 'Sistem' }, { label: 'Kullanıcılar', route: 'users' }, { label: 'Yeni' }],
          title: 'Yeni Kullanıcı',
          sub: 'Kullanıcı oluşturun ve davetiye gönderin. İlk girişte parola belirleyecektir.',
        })}

        <div style="max-width:560px;margin:0 auto">
          <div class="card">
            <div class="card-hdr"><div class="card-title">Hesap Bilgileri</div></div>
            <div class="card-body" style="display:flex;flex-direction:column;gap:18px">
              <div class="form-row">
                <div class="form-group">
                  <label class="form-label">Ad <span class="req">*</span></label>
                  <input type="text" class="form-ctrl${errors.firstName ? ' is-error' : ''}" placeholder="Mehmet" value="${esc(f.firstName)}" data-form="user" data-field="firstName" />
                  ${errors.firstName ? `<div class="form-error">${I.AlertCircle(12, 2.4)}${esc(errors.firstName)}</div>` : ''}
                </div>
                <div class="form-group">
                  <label class="form-label">Soyad <span class="req">*</span></label>
                  <input type="text" class="form-ctrl${errors.lastName ? ' is-error' : ''}" placeholder="Yılmaz" value="${esc(f.lastName)}" data-form="user" data-field="lastName" />
                  ${errors.lastName ? `<div class="form-error">${I.AlertCircle(12, 2.4)}${esc(errors.lastName)}</div>` : ''}
                </div>
              </div>

              <div class="form-group">
                <label class="form-label">E-posta <span class="req">*</span></label>
                <div class="form-prefix-wrap">
                  <span class="form-prefix">${I.Mail(14, 2)}</span>
                  <input type="email" class="form-ctrl with-prefix${errors.email ? ' is-error' : ''}" placeholder="ornek@operax.com.tr" value="${esc(f.email)}" data-form="user" data-field="email" />
                </div>
                ${errors.email ? `<div class="form-error">${I.AlertCircle(12, 2.4)}${esc(errors.email)}</div>` : '<div class="form-hint">Davetiye bu adrese gönderilecek</div>'}
              </div>

              <div class="form-row">
                <div class="form-group">
                  <label class="form-label">Rol <span class="req">*</span></label>
                  <select class="form-ctrl${errors.role ? ' is-error' : ''}" data-form="user" data-field="role">
                    <option value="">Rol seçin…</option>
                    <option value="admin" ${f.role==='admin'?'selected':''}>Yönetici (Tam yetki)</option>
                    <option value="manager" ${f.role==='manager'?'selected':''}>Departman Yöneticisi</option>
                    <option value="operator" ${f.role==='operator'?'selected':''}>Operatör</option>
                    <option value="viewer" ${f.role==='viewer'?'selected':''}>Görüntüleyici</option>
                  </select>
                  ${errors.role ? `<div class="form-error">${I.AlertCircle(12, 2.4)}${esc(errors.role)}</div>` : ''}
                </div>
                <div class="form-group">
                  <label class="form-label">Departman</label>
                  <select class="form-ctrl" data-form="user" data-field="dept">
                    <option value="">Seçiniz…</option><option>Satınalma</option><option>Depo</option><option>Muhasebe</option><option>Üretim</option><option>Yönetim</option>
                  </select>
                </div>
              </div>

              <div style="display:flex;flex-direction:column;gap:12px;padding:14px;background:var(--bg);border-radius:10px;border:1px solid var(--border)">
                <label style="display:flex;align-items:center;gap:10px;cursor:pointer">
                  <label class="switch"><input type="checkbox" ${f.sendInvite ? 'checked' : ''} data-form="user" data-field="sendInvite" data-bool="1" /><span class="switch-track"></span></label>
                  <div style="flex:1"><div style="font-size:12.5px;font-weight:600;color:var(--text)">Davet E-Postası Gönder</div><div style="font-size:11px;color:var(--text-3)">Kullanıcı kendi parolasını belirleyecek</div></div>
                </label>
                <div class="divider" style="margin:0"></div>
                <label style="display:flex;align-items:center;gap:10px;cursor:pointer">
                  <label class="switch"><input type="checkbox" ${f.twoFA ? 'checked' : ''} data-form="user" data-field="twoFA" data-bool="1" /><span class="switch-track"></span></label>
                  <div style="flex:1"><div style="font-size:12.5px;font-weight:600;color:var(--text)">İki Faktörlü Doğrulama Zorunlu</div><div style="font-size:11px;color:var(--text-3)">İlk girişte 2FA kurulumu istenecek</div></div>
                </label>
              </div>
            </div>
            <div class="card-foot">
              <button class="btn btn-ghost btn-sm" data-action="nav" data-route="users">${I.X(13, 2)}Vazgeç</button>
              <button class="btn btn-primary btn-sm" data-action="user-submit">${I.Check(13, 2)}Kullanıcıyı Oluştur<span class="btn-kbd">Alt+S</span></button>
            </div>
          </div>
        </div>
      </div>
    `;
  };

  // ============== AYARLAR ==============
  const SettingsScreen = (state) => {
    const local = state.local;
    local.section = local.section || 'general';
    const sections = [
      { id: 'general', label: 'Genel', icon: 'Cog' },
      { id: 'company', label: 'Şirket Bilgileri', icon: 'Building' },
      { id: 'docs', label: 'Evrak Numaralandırma', icon: 'FileText' },
      { id: 'security', label: 'Güvenlik', icon: 'Shield' },
      { id: 'localization', label: 'Bölge & Dil', icon: 'Globe' },
      { id: 'api', label: 'API & Entegrasyonlar', icon: 'Key' },
    ];

    const sideMenu = sections.map((s) => {
      const a = local.section === s.id;
      return `<div data-action="settings-sec" data-sec="${esc(s.id)}" style="display:flex;align-items:center;gap:10px;padding:8px 12px;font-size:12.5px;font-weight:${a?600:500};color:${a?'var(--brand-500)':'var(--text-2)'};background:${a?'var(--brand-tint-08)':'transparent'};border-radius:8px;cursor:pointer;margin-bottom:2px">${I[s.icon](15, 2)}${esc(s.label)}</div>`;
    }).join('');

    let body = '';
    if (local.section === 'general') {
      body = `
        <div class="card-hdr"><div><div class="card-title">Genel Ayarlar</div><div class="card-sub">Uygulama genelinde geçerli temel ayarlar</div></div></div>
        <div class="card-body" style="display:flex;flex-direction:column;gap:4px">
          ${[
            { t: 'Karanlık tema otomatik geç', d: 'Sistemin tema tercihine göre arayüz otomatik adapte olur', on: false },
            { t: 'E-posta bildirimleri', d: 'Önemli olaylar için e-posta uyarısı', on: true },
            { t: 'Bekleyen onaylar için günlük özet', d: 'Her sabah 09:00\'da özet e-postası gönder', on: true },
            { t: 'Yeni satınalma evrakları otomatik numaralandır', d: 'PO-YYYY-##### formatında sıralı numara verir', on: true },
            { t: 'Stok hareketleri için onay zorunlu', d: 'Tüm stok hareketleri çift onaylı işlenir', on: false },
          ].map((it, i, arr) => `
            <div style="display:flex;align-items:center;gap:16px;padding:12px 4px;${i < arr.length-1 ? 'border-bottom:1px solid var(--border)' : ''}">
              <div style="flex:1"><div style="font-size:13px;font-weight:600;color:var(--text)">${esc(it.t)}</div><div style="font-size:11.5px;color:var(--text-3);margin-top:1px">${esc(it.d)}</div></div>
              <label class="switch"><input type="checkbox" ${it.on ? 'checked' : ''} /><span class="switch-track"></span></label>
            </div>
          `).join('')}
        </div>
      `;
    } else if (local.section === 'company') {
      body = `
        <div class="card-hdr"><div><div class="card-title">Şirket Bilgileri</div><div class="card-sub">Resmi unvan, vergi kimliği ve adres</div></div><button class="btn btn-primary btn-sm">${I.Check(13, 2)}Kaydet</button></div>
        <div class="card-body" style="display:flex;flex-direction:column;gap:16px">
          <div class="form-row">
            <div class="form-group"><label class="form-label">Şirket Unvanı</label><input class="form-ctrl" value="Aydın Endüstri A.Ş." /></div>
            <div class="form-group"><label class="form-label">Kısa Ad</label><input class="form-ctrl" value="Aydın End." /></div>
          </div>
          <div class="form-row">
            <div class="form-group"><label class="form-label">Vergi Kimlik No</label><input class="form-ctrl mono" value="0123456789" /></div>
            <div class="form-group"><label class="form-label">Vergi Dairesi</label><input class="form-ctrl" value="Beşiktaş" /></div>
          </div>
          <div class="form-group"><label class="form-label">Merkez Adres</label><textarea class="form-ctrl">Levent Mah. Büyükdere Cd. No: 185, Şişli / İstanbul, 34394 Türkiye</textarea></div>
          <div class="form-row-3">
            <div class="form-group"><label class="form-label">Telefon</label><input class="form-ctrl mono" value="+90 (212) 555 00 00" /></div>
            <div class="form-group"><label class="form-label">E-posta</label><input class="form-ctrl mono" value="info@aydinendustri.com.tr" /></div>
            <div class="form-group"><label class="form-label">Web Sitesi</label><input class="form-ctrl mono" value="aydinendustri.com.tr" /></div>
          </div>
        </div>
      `;
    } else if (local.section === 'docs') {
      body = `
        <div class="card-hdr"><div><div class="card-title">Evrak Numaralandırma</div><div class="card-sub">Her evrak türü için format ve başlangıç sayacı</div></div></div>
        <div class="card-body-flush">
          <table class="data-table">
            <thead><tr><th>Evrak Türü</th><th>Önek</th><th>Format</th><th class="num">Sıradaki</th><th>Otomatik</th></tr></thead>
            <tbody>
              ${[
                { t: 'Satınalma Siparişi', p: 'PO', f: 'PO-{YYYY}-{#####}', next: '00042', auto: true },
                { t: 'Satış Siparişi', p: 'SO', f: 'SO-{YYYY}-{#####}', next: '00128', auto: true },
                { t: 'Sevkıyat Etiketi', p: 'LPN', f: 'LPN-{#####}', next: '00492', auto: true },
                { t: 'Sayım Fişi', p: 'CT', f: 'CT-{YY}-{####}', next: '0084', auto: true },
                { t: 'İade Fişi', p: 'RT', f: 'RT-{YY}-{####}', next: '0012', auto: false },
              ].map((d) => `
                <tr>
                  <td><span class="row-strong">${esc(d.t)}</span></td>
                  <td><span class="mono">${esc(d.p)}</span></td>
                  <td><span class="mono muted">${esc(d.f)}</span></td>
                  <td class="num"><span class="row-strong">${esc(d.next)}</span></td>
                  <td><label class="switch"><input type="checkbox" ${d.auto ? 'checked' : ''} /><span class="switch-track"></span></label></td>
                </tr>
              `).join('')}
            </tbody>
          </table>
        </div>
      `;
    } else if (local.section === 'security') {
      body = `
        <div class="card-hdr"><div class="card-title">Güvenlik Politikaları</div></div>
        <div class="card-body" style="display:flex;flex-direction:column;gap:4px">
          ${[
            { t: 'İki Faktörlü Doğrulama Zorunlu', d: 'Tüm yöneticiler için 2FA zorunlu', on: true },
            { t: 'Oturum Süresi', d: '8 saat hareketsizlik sonrası otomatik çıkış', on: true },
            { t: 'IP Beyaz Listesi', d: 'Yalnızca onaylı ağlardan erişim', on: false },
            { t: 'Parola Karmaşıklığı', d: 'En az 12 karakter, harf+sayı+sembol', on: true },
            { t: 'Denetim Kaydı (Audit Log)', d: 'Tüm kullanıcı işlemleri kaydedilir', on: true },
          ].map((it, i, arr) => `
            <div style="display:flex;align-items:center;gap:16px;padding:12px 4px;${i < arr.length-1 ? 'border-bottom:1px solid var(--border)' : ''}">
              <div style="flex:1"><div style="font-size:13px;font-weight:600;color:var(--text)">${esc(it.t)}</div><div style="font-size:11.5px;color:var(--text-3);margin-top:1px">${esc(it.d)}</div></div>
              <label class="switch"><input type="checkbox" ${it.on ? 'checked' : ''} /><span class="switch-track"></span></label>
            </div>
          `).join('')}
        </div>
      `;
    } else if (local.section === 'localization') {
      body = `
        <div class="card-hdr"><div class="card-title">Bölge & Dil Ayarları</div></div>
        <div class="card-body" style="display:flex;flex-direction:column;gap:16px">
          <div class="form-row">
            <div class="form-group"><label class="form-label">Arayüz Dili</label><select class="form-ctrl"><option>🇹🇷 Türkçe</option><option>🇬🇧 English</option></select></div>
            <div class="form-group"><label class="form-label">Para Birimi</label><select class="form-ctrl"><option>₺ Türk Lirası</option><option>$ ABD Doları</option><option>€ Euro</option></select></div>
          </div>
          <div class="form-row">
            <div class="form-group"><label class="form-label">Zaman Dilimi</label><select class="form-ctrl"><option>Europe/Istanbul (UTC+3)</option></select></div>
            <div class="form-group"><label class="form-label">Tarih Formatı</label><select class="form-ctrl mono"><option>DD.MM.YYYY</option><option>DD Mon YYYY</option><option>YYYY-MM-DD</option></select></div>
          </div>
        </div>
      `;
    } else if (local.section === 'api') {
      body = `
        <div class="card-hdr"><div><div class="card-title">API Anahtarları</div><div class="card-sub">Üçüncü taraf entegrasyonları için erişim anahtarları</div></div><button class="btn btn-primary btn-sm">${I.Plus(13, 2)}Yeni Anahtar</button></div>
        <div class="card-body-flush">
          <table class="data-table">
            <thead><tr><th>İsim</th><th>Anahtar</th><th>Yetki</th><th>Son Kullanım</th><th>Durum</th></tr></thead>
            <tbody>
              <tr><td><span class="row-strong">Üretim Hattı Webhook</span></td><td><span class="mono muted">opx_live_••••••••a3f2</span></td><td><span class="badge badge-brand">read+write</span></td><td class="muted mono">27 May · 13:42</td><td><span class="badge badge-success"><span class="badge-dot"></span>Aktif</span></td></tr>
              <tr><td><span class="row-strong">Tedarikçi EDI</span></td><td><span class="mono muted">opx_live_••••••••7b29</span></td><td><span class="badge badge-info">read</span></td><td class="muted mono">26 May · 22:18</td><td><span class="badge badge-success"><span class="badge-dot"></span>Aktif</span></td></tr>
              <tr><td><span class="row-strong">Eski BI Sistem</span></td><td><span class="mono muted">opx_live_••••••••e041</span></td><td><span class="badge badge-info">read</span></td><td class="muted mono">12 Mar · 08:14</td><td><span class="badge badge-neutral"><span class="badge-dot"></span>Devre dışı</span></td></tr>
            </tbody>
          </table>
        </div>
      `;
    }

    return html`
      <div class="page" data-screen-label="Settings">
        ${pageHeader({
          crumbs: [{ label: 'Anasayfa' }, { label: 'Ayarlar' }],
          title: 'Sistem Ayarları',
          sub: 'Şirket konfigürasyonu, güvenlik politikaları ve entegrasyonlar',
        })}

        <div style="display:grid;grid-template-columns:240px 1fr;gap:18px">
          <div class="card" style="padding:8px;height:fit-content">${raw(sideMenu)}</div>
          <div class="card">${raw(body)}</div>
        </div>
      </div>
    `;
  };

  window.SCREENS = window.SCREENS || {};
  Object.assign(window.SCREENS, {
    StokHareket, SalesList, SalesDetail, SalesNew,
    InventoryList, ProductionList, ProductionDetail,
    AccountingList, AccountingDetail, CashBank,
    ReportsScreen, ReportView, UsersScreen, UserNew, SettingsScreen,
  });
})();
