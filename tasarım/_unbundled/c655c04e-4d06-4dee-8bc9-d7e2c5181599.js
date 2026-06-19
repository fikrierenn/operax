/* OPERAX — Pure JS · Ekranlar (1/2): Dashboard, Satınalma, Satış, WMS, Cari, Envanter */
(function () {
  const I = window.ICONS;
  const D = window.OPX;
  const U = window.UI;
  const { html, raw, esc, statusBadge, shipBadge, movementBadge, woStatusBadge, priorityBadge,
    avatar, initials, breadcrumb, pageHeader, kpiCard, sparkline, gauge, emptyState, tabs, statusFlow, btn } = U;

  // ============== DASHBOARD ==============
  const Dashboard = (state) => {
    const empty = state.tweaks.emptyState;
    const totalPO = empty ? 0 : D.POs.reduce((a, p) => a + (p.status !== 'Cancelled' ? p.total : 0), 0);
    const postedCount = empty ? 0 : D.POs.filter((p) => p.status === 'Posted').length;
    const draftCount = empty ? 0 : D.POs.filter((p) => p.status === 'Draft').length;
    const fillAvg = Math.round(D.locations.reduce((a, l) => a + l.fillPct, 0) / D.locations.length);

    const spark1 = D.sparkSeries(24, 180, 14, 1);
    const spark2 = D.sparkSeries(24, 92, 8, 5);
    const spark3 = D.sparkSeries(24, fillAvg, 4, 9);

    // Aylık satınalma performansı (stacked bars)
    const months = ['Ara', 'Oca', 'Şub', 'Mar', 'Nis', 'May'];
    const posted = [820, 940, 1120, 1280, 1410, 1640];
    const draft  = [110, 130, 90, 180, 160, 240];
    const cancel = [40, 60, 35, 80, 45, 30];
    const totals = posted.map((p, i) => p + draft[i] + cancel[i]);
    const maxT = Math.max(...totals);

    const bars = months.map((m, i) => {
      const pH = (posted[i] / maxT) * 100;
      const dH = (draft[i] / maxT) * 100;
      const cH = (cancel[i] / maxT) * 100;
      const isLast = i === months.length - 1;
      return `
        <div style="display:flex;flex-direction:column;align-items:center;gap:8px;height:100%">
          <div class="mono" style="font-size:10.5px;color:var(--text-3);font-weight:600">₺${(totals[i]/1000).toFixed(1)}M</div>
          <div style="flex:1;width:100%;max-width:56px;display:flex;flex-direction:column;justify-content:flex-end;gap:2px">
            <div style="height:${cH}%;background:var(--danger);opacity:.85;border-radius:4px 4px 0 0;min-height:${cancel[i]>0?2:0}px"></div>
            <div style="height:${dH}%;background:var(--warn);opacity:.85;min-height:2px"></div>
            <div style="height:${pH}%;background:${isLast?'var(--brand-grad)':'hsl(243 75% 59% / 0.85)'};border-radius:0 0 4px 4px;min-height:4px;${isLast?'box-shadow:0 4px 12px hsl(243 75% 59% / 0.3)':''}"></div>
          </div>
          <div style="font-size:11px;color:${isLast?'var(--text)':'var(--text-3)'};font-weight:${isLast?700:500}">${m}</div>
        </div>
      `;
    }).join('');

    return html`
      <div class="page" data-screen-label="Dashboard" style="max-width:1640px">
        ${pageHeader({
          crumbs: [{ label: 'Anasayfa' }, { label: 'Yönetici Görünümü' }],
          title: 'Anasayfa',
          sub: `Aydın Endüstri A.Ş. · ${D.fmtDate('2026-05-27')} · 27 May 2026 Çarşamba`,
          actions: `
            ${btn('Rapor İndir', { kind: 'secondary', icon: 'Download' })}
            ${btn('Yeni Sipariş', { kind: 'primary', icon: 'Plus', action: 'nav', route: 'purchasing/new', kbd: 'Alt+N' })}
          `,
        })}

        <div class="kpi-grid">
          ${kpiCard({ label: 'Açık Satınalma Tutarı', value: empty ? '—' : D.fmtTL(totalPO), glow: 'brand', valueSize: 22, sub: `<span class="kpi-delta up">${I.ArrowUp(10, 2.5)}%18.4</span> <span class="kpi-trend">vs geçen ay</span>` })}
          ${kpiCard({ label: 'Bu Ay Onaylanan', value: empty ? 0 : postedCount, unit: 'evrak', glow: 'success', sub: `<span class="kpi-delta up">${I.ArrowUp(10, 2.5)}%9.2</span> <span class="kpi-trend">${draftCount} taslak bekliyor</span>` })}
          ${kpiCard({ label: 'Depo Doluluk', value: empty ? '—' : `%${fillAvg}`, glow: 'warn', sub: `<span class="kpi-delta flat">${I.Dot(10, 2)}sabit</span> <span class="kpi-trend">${D.locations.length} aktif konum</span>` })}
          ${kpiCard({ label: 'Düşük Stoklu SKU', value: empty ? 0 : D.lowStock.length, unit: 'ürün', glow: 'danger', valueColor: 'var(--danger-text)', sub: `<span class="kpi-delta down">${I.ArrowDown(10, 2.5)}%4.0</span> <span class="kpi-trend">acil müdahale önerilir</span>` })}
        </div>

        <div style="display:grid;grid-template-columns:1fr 360px;gap:14px;margin-bottom:14px">
          <div class="card">
            <div class="card-hdr">
              <div>
                <div class="card-title">Aylık Satınalma Performansı</div>
                <div class="card-sub">Onaylı + Taslak + İptal · Son 6 ay</div>
              </div>
              <div style="display:flex;gap:4px">
                <button class="btn btn-secondary btn-xs">Aylık</button>
                <button class="btn btn-ghost btn-xs">Çeyrek</button>
                <button class="btn btn-ghost btn-xs">Yıl</button>
              </div>
            </div>
            <div class="card-body" style="padding:18px">
              <div style="display:grid;grid-template-columns:repeat(6,1fr);gap:14px;align-items:flex-end;height:200px">
                ${raw(bars)}
              </div>
              <div style="display:flex;gap:16px;margin-top:14px;padding-top:12px;border-top:1px solid var(--border);font-size:11.5px">
                <div style="display:flex;align-items:center;gap:6px"><span class="dot" style="background:var(--brand-500)"></span><span style="color:var(--text-2)">Onaylanmış</span></div>
                <div style="display:flex;align-items:center;gap:6px"><span class="dot" style="background:var(--warn)"></span><span style="color:var(--text-2)">Taslak</span></div>
                <div style="display:flex;align-items:center;gap:6px"><span class="dot" style="background:var(--danger)"></span><span style="color:var(--text-2)">İptal</span></div>
              </div>
            </div>
          </div>

          <div class="card">
            <div class="card-hdr"><div class="card-title">Son Aktivite</div></div>
            <div class="card-body" style="padding:6px 14px 12px">
              ${raw(empty ? '<div class="empty-state" style="padding:40px 16px"><p>Henüz aktivite yok</p></div>' :
                D.activity.slice(0, 6).map((a, i, arr) => {
                  const color = a.kind === 'success' ? 'var(--success)' : a.kind === 'danger' ? 'var(--danger)' : a.kind === 'warn' ? 'var(--warn)' : a.kind === 'info' ? 'var(--brand-500)' : 'hsl(215 16% 47%)';
                  return `
                    <div style="display:flex;gap:10px;padding:8px 0;${i < arr.length-1 ? 'border-bottom:1px solid var(--border)' : ''}">
                      ${avatar(a.avatar, { size: 28, color }).__raw}
                      <div style="flex:1;min-width:0">
                        <div style="font-size:12px;line-height:1.4"><span style="font-weight:600">${esc(a.who)}</span> <span style="color:var(--text-2)">${esc(a.action)}</span></div>
                        <div class="mono" style="font-size:10.5px;color:var(--text-4);margin-top:1px">${esc(D.relTime(a.time))}</div>
                      </div>
                    </div>
                  `;
                }).join('')
              )}
            </div>
          </div>
        </div>

        <div style="display:grid;grid-template-columns:1fr 1fr;gap:14px;margin-bottom:14px">
          <div class="card">
            <div class="card-hdr">
              <div class="card-title">Yaklaşan Sevkiyatlar</div>
              <button class="btn btn-ghost btn-xs" data-action="nav" data-route="wms">Tümünü gör${I.ChevronRight(12, 2)}</button>
            </div>
            <div class="card-body-flush">
              ${raw(empty ? emptyState({ icon: 'Truck', title: 'Planlı sevkiyat yok', msg: 'Bekleyen mal giriş sevkiyatı bulunmuyor.' }).__raw :
                `<table class="data-table">
                  <thead><tr><th>LPN</th><th>Tedarikçi</th><th>SKU</th><th class="num">Miktar</th><th>ETA</th><th>Dock</th></tr></thead>
                  <tbody>
                    ${D.incoming.map((it) => {
                      const p = D.products.find((x) => x.sku === it.sku) || {};
                      return `
                        <tr>
                          <td><span class="mono row-strong" style="color:var(--brand-500)">${esc(it.lpn)}</span></td>
                          <td>${esc(D.supplierByCode(it.supplier).name.split(' ').slice(0, 2).join(' '))}</td>
                          <td><span class="mono">${esc(it.sku)}</span></td>
                          <td class="num"><span class="row-strong">${esc(D.fmtNum(it.qty))}</span> <span class="muted">${esc(p.uom || '')}</span></td>
                          <td class="mono">${esc(D.fmtDateShort(it.eta))}</td>
                          <td><span class="badge badge-brand"><span class="badge-dot"></span>${esc(it.dock)}</span></td>
                        </tr>
                      `;
                    }).join('')}
                  </tbody>
                </table>`
              )}
            </div>
          </div>

          <div class="card">
            <div class="card-hdr">
              <div class="card-title">Düşük Stoklu Ürünler</div>
              <button class="btn btn-ghost btn-xs">Sipariş öner${I.Sparkle(12, 2)}</button>
            </div>
            <div class="card-body-flush">
              ${raw(empty ? emptyState({ icon: 'Box', title: 'Tüm stoklar yeterli düzeyde' }).__raw :
                `<table class="data-table">
                  <thead><tr><th>SKU</th><th>Ürün</th><th class="num">Mevcut</th><th class="num">Min</th><th>Doluluk</th></tr></thead>
                  <tbody>
                    ${D.lowStock.map((it) => {
                      const pct = Math.round((it.onhand / it.min) * 100);
                      return `
                        <tr style="cursor:pointer" data-action="nav" data-route="stok/kart/${esc(it.sku)}">
                          <td><span class="mono row-strong" style="color:var(--brand-500)">${esc(it.sku)}</span></td>
                          <td>${esc(it.name)}</td>
                          <td class="num"><span class="row-strong">${esc(D.fmtNum(it.onhand))}</span> <span class="muted">${esc(it.uom)}</span></td>
                          <td class="num muted mono">${esc(D.fmtNum(it.min))}</td>
                          <td><div style="display:flex;align-items:center;gap:8px"><div style="width:60px">${gauge(pct, 5)}</div><span class="mono" style="font-size:11px;color:var(--danger-text);font-weight:600">%${pct}</span></div></td>
                        </tr>
                      `;
                    }).join('')}
                  </tbody>
                </table>`
              )}
            </div>
          </div>
        </div>

        <div class="card">
          <div class="card-hdr">
            <div class="card-title">Son Satınalma Siparişleri</div>
            <button class="btn btn-ghost btn-xs" data-action="nav" data-route="purchasing">Hepsini gör${I.ChevronRight(12, 2)}</button>
          </div>
          <div class="card-body-flush">
            ${raw(empty ? emptyState({ icon: 'FileText', title: 'Hiç sipariş yok' }).__raw :
              `<table class="data-table">
                <thead><tr><th>Evrak No</th><th>Tedarikçi</th><th>Tarih</th><th class="num">Tutar</th><th>Durum</th><th style="width:36px"></th></tr></thead>
                <tbody>
                  ${D.POs.slice(0, 5).map((po) => {
                    const s = D.supplierByCode(po.supplier);
                    return `
                      <tr style="cursor:pointer" data-action="nav" data-route="purchasing/detail/${esc(po.no)}">
                        <td><span class="mono row-strong" style="color:var(--brand-500)">${esc(po.no)}</span></td>
                        <td><div style="display:flex;align-items:center;gap:10px">${avatar(po.supplier, { size: 24, fontSize: 10 }).__raw}<div><div class="row-strong">${esc(s.name)}</div><div class="row-sub">${esc(s.city)}</div></div></div></td>
                        <td class="mono muted">${esc(D.fmtDateShort(po.date))}</td>
                        <td class="num"><span class="row-strong">${esc(D.fmtTL(po.total))}</span></td>
                        <td>${statusBadge(po.status).__raw}</td>
                        <td>${I.ChevronRight(14, 2)}</td>
                      </tr>
                    `;
                  }).join('')}
                </tbody>
              </table>`
            )}
          </div>
        </div>
      </div>
    `;
  };

  // ============== PURCHASING LIST ==============
  const PurchasingList = (state) => {
    const empty = state.tweaks.emptyState;
    const local = state.local;
    local.tab = local.tab || 'all';
    local.q = local.q || '';
    let rows = empty ? [] : D.POs;
    if (local.tab !== 'all') rows = rows.filter((p) => p.status === local.tab);
    if (local.q) {
      const qL = local.q.toLocaleLowerCase('tr-TR');
      rows = rows.filter((p) => p.no.toLowerCase().includes(qL) || D.supplierByCode(p.supplier).name.toLocaleLowerCase('tr-TR').includes(qL));
    }
    const counts = {
      all: D.POs.length,
      Draft: D.POs.filter((p) => p.status === 'Draft').length,
      Posted: D.POs.filter((p) => p.status === 'Posted').length,
      Cancelled: D.POs.filter((p) => p.status === 'Cancelled').length,
    };
    const totalActive = rows.reduce((a, p) => a + (p.status !== 'Cancelled' ? p.total : 0), 0);

    return html`
      <div class="page" data-screen-label="Purchasing List">
        ${pageHeader({
          crumbs: [{ label: 'Anasayfa' }, { label: 'Satınalma' }, { label: 'Siparişler' }],
          title: 'Satınalma Siparişleri',
          sub: `Toplam ${empty ? 0 : D.POs.length} evrak · Aktif tutar <span class="mono text-strong">${esc(D.fmtTL(totalActive))}</span>`,
          actions: `
            ${btn('İçeri Aktar', { kind: 'secondary', icon: 'Upload' })}
            ${btn('Dışa Aktar', { kind: 'secondary', icon: 'Download' })}
            ${btn('Yeni Sipariş', { kind: 'primary', icon: 'Plus', action: 'nav', route: 'purchasing/new', kbd: 'Alt+N' })}
          `,
        })}

        <div class="card">
          ${tabs([
            { id: 'all', label: 'Tümü', count: counts.all },
            { id: 'Draft', label: 'Taslak', count: counts.Draft },
            { id: 'Posted', label: 'Onaylandı', count: counts.Posted },
            { id: 'Cancelled', label: 'İptal', count: counts.Cancelled },
          ], local.tab, 'po-tab')}

          <div class="data-table-toolbar">
            <div style="position:relative;flex:0 0 280px">
              ${I.Search(14, 2).replace('<svg ', '<svg style="position:absolute;left:11px;top:50%;transform:translateY(-50%);color:var(--text-4)" ')}
              <input class="form-ctrl" style="padding-left:34px;height:34px" placeholder="Evrak no, tedarikçi ara…" value="${esc(local.q)}" data-action="po-search" />
            </div>
            <button class="chip">${I.Calendar(12, 2)}Tarih: Son 30 gün${I.X(11, 2.5)}</button>
            <button class="chip">Tedarikçi: Tümü${I.ChevronDown(11, 2)}</button>
            <button class="chip">Tutar: Tümü${I.ChevronDown(11, 2)}</button>
            <button class="btn btn-ghost btn-sm" style="margin-left:auto">${I.Sliders(13, 2)}Sütunlar</button>
          </div>

          ${rows.length === 0 ? emptyState({
            icon: 'FileText',
            title: 'Eşleşen evrak bulunamadı',
            msg: empty ? 'Henüz hiç satınalma evrakı oluşturulmamış. İlk siparişinizi şimdi oluşturun.' : 'Arama veya filtreleri değiştirin.',
            action: btn('Yeni Sipariş', { kind: 'primary', icon: 'Plus', action: 'nav', route: 'purchasing/new' }),
          }) : html`
            <table class="data-table">
              <thead>
                <tr>
                  <th>Evrak No</th><th>Tedarikçi</th><th>Tarih</th><th>Vade</th>
                  <th class="num">Kalem</th><th class="num">Tutar</th><th>Durum</th><th style="width:36px"></th>
                </tr>
              </thead>
              <tbody>
                ${rows.map((po) => {
                  const s = D.supplierByCode(po.supplier);
                  return raw(`
                    <tr style="cursor:pointer" data-action="nav" data-route="purchasing/detail/${esc(po.no)}">
                      <td><span class="mono row-strong" style="color:var(--brand-500)">${esc(po.no)}</span></td>
                      <td>
                        <div style="display:flex;align-items:center;gap:10px">
                          ${avatar(po.supplier, { size: 26, fontSize: 10 }).__raw}
                          <div>
                            <div class="row-strong">${esc(s.name)}</div>
                            <div class="row-sub">${esc(s.city)} · VKN ${esc(s.tax)}</div>
                          </div>
                        </div>
                      </td>
                      <td class="mono muted">${esc(D.fmtDateShort(po.date))}</td>
                      <td class="mono">${esc(D.fmtDateShort(po.due))}</td>
                      <td class="num">${esc(po.items)}</td>
                      <td class="num"><span class="row-strong">${esc(D.fmtTL(po.total))}</span></td>
                      <td>${statusBadge(po.status).__raw}</td>
                      <td>${I.ChevronRight(14, 2)}</td>
                    </tr>
                  `);
                })}
              </tbody>
            </table>
          `}

          ${rows.length > 0 ? html`
            <div class="card-foot">
              <div style="font-size:12px;color:var(--text-3)"><span class="mono text-strong">${rows.length}</span> / ${D.POs.length} evrak gösteriliyor</div>
              <div style="display:flex;gap:4px">
                <button class="btn btn-secondary btn-xs">${raw(I.ChevronLeft(12, 2))}Önceki</button>
                <button class="btn btn-secondary btn-xs">1</button>
                <button class="btn btn-ghost btn-xs">2</button>
                <button class="btn btn-ghost btn-xs">3</button>
                <button class="btn btn-secondary btn-xs">Sonraki${raw(I.ChevronRight(12, 2))}</button>
              </div>
            </div>
          ` : ''}
        </div>
      </div>
    `;
  };

  // ============== PURCHASING DETAIL ==============
  const PurchasingDetail = (state, poNo) => {
    const po = D.POs.find((p) => p.no === poNo) || D.POs[0];
    const s = D.supplierByCode(po.supplier);
    const subtotal = D.POLines.reduce((a, l) => a + l.qty * l.unitPrice, 0);
    const vat = subtotal * 0.20;
    const grand = subtotal + vat;
    const dates = {
      created: '22 May 2026 · 09:14',
      draft: '22 May 2026 · 09:15',
      posted: po.status === 'Posted' ? '22 May 2026 · 14:22' : null,
      cancelled: po.status === 'Cancelled' ? '23 May 2026 · 10:30' : null,
    };
    const actionsRight = `
      ${btn('Geri', { kind: 'ghost', icon: 'ChevronLeft', action: 'nav', route: 'purchasing' })}
      ${btn('PDF', { kind: 'secondary', icon: 'Download' })}
      ${po.status === 'Draft' ? btn('Düzenle', { kind: 'secondary', icon: 'Edit' }) + btn('Onayla', { kind: 'success', icon: 'Check', kbd: 'Alt+S' }) : ''}
      ${po.status === 'Posted' ? `<button class="btn btn-secondary btn-sm" style="color:var(--danger-text);border-color:var(--danger-border)">${I.X(13,2)}İptal Et</button>` : ''}
    `;

    return html`
      <div class="page" data-screen-label="Purchasing Detail" style="max-width:1400px">
        ${pageHeader({
          crumbs: [
            { label: 'Anasayfa' },
            { label: 'Satınalma', route: 'purchasing' },
            { label: 'Siparişler', route: 'purchasing' },
            { label: po.no, mono: true },
          ],
          titleSlot: html`
            <div style="display:flex;align-items:center;gap:12px;margin-top:2px">
              <h1 class="mono" style="letter-spacing:-0.01em">${po.no}</h1>
              ${statusBadge(po.status)}
            </div>
          `,
          sub: `${esc(s.name)} · Oluşturma: ${esc(dates.created)} · Vade: <span class="mono">${esc(D.fmtDate(po.due))}</span>`,
          actions: actionsRight,
        })}

        <div style="margin-bottom:14px">${statusFlow(po.status, dates)}</div>

        <div style="display:grid;grid-template-columns:1fr 320px;gap:14px">
          <div class="stack" style="display:flex;flex-direction:column;gap:14px">
            <div class="card">
              <div class="card-hdr"><div class="card-title">Evrak Bilgileri</div></div>
              <div class="card-body" style="display:grid;grid-template-columns:repeat(4,1fr);gap:18px">
                <div>
                  <div class="form-label">Tedarikçi</div>
                  <div style="font-size:13px;font-weight:600;margin-top:4px;color:var(--brand-500);cursor:pointer" data-action="nav" data-route="cari/detail/${esc(po.supplier)}">${esc(s.name)}</div>
                  <div style="font-size:11.5px;color:var(--text-3);margin-top:1px">VKN <span class="mono">${esc(s.tax)}</span></div>
                </div>
                <div>
                  <div class="form-label">Şehir</div>
                  <div style="font-size:13px;font-weight:600;margin-top:4px">${esc(s.city)}</div>
                  <div style="font-size:11.5px;color:var(--text-3);margin-top:1px">Türkiye</div>
                </div>
                <div>
                  <div class="form-label">Para Birimi</div>
                  <div class="mono" style="font-size:13px;font-weight:600;margin-top:4px">TRY · ₺</div>
                </div>
                <div>
                  <div class="form-label">Vade Tarihi</div>
                  <div class="mono" style="font-size:13px;font-weight:600;margin-top:4px">${esc(D.fmtDate(po.due))}</div>
                  <div style="font-size:11.5px;color:var(--text-3);margin-top:1px">14 gün</div>
                </div>
                <div>
                  <div class="form-label">Sorumlu</div>
                  <div style="display:flex;align-items:center;gap:8px;margin-top:4px">
                    ${avatar('MY', { size: 22, fontSize: 9.5, gradient: 'linear-gradient(135deg, hsl(243 75% 59%), hsl(263 70% 55%))' })}
                    <span style="font-size:13px;font-weight:600">Mehmet Yılmaz</span>
                  </div>
                </div>
                <div><div class="form-label">Ödeme Şartı</div><div style="font-size:13px;font-weight:600;margin-top:4px">30 gün vadeli</div></div>
                <div><div class="form-label">Teslimat Yeri</div><div style="font-size:13px;font-weight:600;margin-top:4px">Merkez Depo · İstanbul</div></div>
                <div><div class="form-label">Referans</div><div class="mono" style="font-size:13px;font-weight:600;margin-top:4px">REF-2026-A042</div></div>
              </div>
            </div>

            <div class="card">
              <div class="card-hdr">
                <div><div class="card-title">Sipariş Kalemleri</div><div class="card-sub">${D.POLines.length} kalem</div></div>
                ${po.status === 'Draft' ? `<button class="btn btn-secondary btn-sm">${I.Plus(13,2)}Kalem Ekle</button>` : ''}
              </div>
              <div class="card-body-flush">
                <table class="data-table">
                  <thead><tr><th style="width:40px" class="num">#</th><th>SKU</th><th>Ürün Adı</th><th>Birim</th><th class="num">Miktar</th><th class="num">Birim Fiyat</th><th class="num">KDV</th><th class="num">Toplam</th></tr></thead>
                  <tbody>
                    ${D.POLines.map((l) => raw(`
                      <tr>
                        <td class="num muted mono">${esc(l.line)}</td>
                        <td style="cursor:pointer" data-action="nav" data-route="stok/kart/${esc(l.sku)}"><span class="mono row-strong" style="color:var(--brand-500)">${esc(l.sku)}</span></td>
                        <td>${esc(l.name)}</td>
                        <td><span class="badge badge-neutral" style="height:18px">${esc(l.uom)}</span></td>
                        <td class="num">${esc(D.fmtNum(l.qty))}</td>
                        <td class="num">${esc(D.fmtTLDec(l.unitPrice))}</td>
                        <td class="num muted">%${esc(l.vat)}</td>
                        <td class="num"><span class="row-strong">${esc(D.fmtTL(l.qty * l.unitPrice))}</span></td>
                      </tr>
                    `))}
                  </tbody>
                </table>
              </div>
              <div class="card-foot">
                <div style="font-size:11.5px;color:var(--text-3)">Tüm tutarlar TRY (₺) cinsindendir</div>
                <div style="display:flex;gap:24px;font-size:12.5px">
                  <div style="display:flex;flex-direction:column;align-items:flex-end"><span style="color:var(--text-3)">Ara Toplam</span><span class="mono" style="font-weight:600">${esc(D.fmtTL(subtotal))}</span></div>
                  <div style="display:flex;flex-direction:column;align-items:flex-end"><span style="color:var(--text-3)">KDV (%20)</span><span class="mono" style="font-weight:600">${esc(D.fmtTL(vat))}</span></div>
                  <div style="display:flex;flex-direction:column;align-items:flex-end"><span style="color:var(--text-3);font-size:10px;font-weight:700;text-transform:uppercase;letter-spacing:0.08em">Genel Toplam</span><span class="mono" style="font-size:17px;font-weight:700;color:var(--brand-500)">${esc(D.fmtTL(grand))}</span></div>
                </div>
              </div>
            </div>
          </div>

          <div class="stack" style="display:flex;flex-direction:column;gap:14px">
            <div class="card">
              <div class="card-hdr"><div class="card-title">Aktivite</div></div>
              <div class="card-body" style="padding:14px 18px">
                ${raw([
                  { who: 'MY', name: 'Mehmet Yılmaz', text: 'Siparişi onayladı', t: '14:22', d: '22 May', color: 'var(--success)' },
                  { who: 'MY', name: 'Mehmet Yılmaz', text: '6 kalem ekledi', t: '11:08', d: '22 May', color: 'hsl(215 16% 47%)' },
                  { who: 'AD', name: 'Ayşe Demir', text: 'Tedarikçi onayladı', t: '10:45', d: '22 May', color: 'var(--brand-500)' },
                  { who: 'MY', name: 'Mehmet Yılmaz', text: 'Taslak oluşturdu', t: '09:14', d: '22 May', color: 'hsl(215 16% 47%)' },
                ].map((a, i, arr) => `
                  <div style="display:flex;gap:10px;position:relative;margin-bottom:14px">
                    <div style="position:relative;flex-shrink:0">
                      ${avatar(a.who, { size: 26, fontSize: 9.5, color: a.color }).__raw}
                      ${i < arr.length-1 ? `<div style="position:absolute;top:30px;left:12.5px;bottom:-14px;width:1.5px;background:var(--border)"></div>` : ''}
                    </div>
                    <div style="flex:1">
                      <div style="font-size:12px;line-height:1.4"><span style="font-weight:600">${esc(a.name)}</span> <span style="color:var(--text-2)">${esc(a.text)}</span></div>
                      <div class="mono" style="font-size:10.5px;color:var(--text-4);margin-top:1px">${esc(a.d)} · ${esc(a.t)}</div>
                    </div>
                  </div>
                `).join(''))}
              </div>
            </div>

            <div class="card">
              <div class="card-hdr"><div class="card-title">İlgili Belgeler</div></div>
              <div class="card-body" style="padding:6px 8px">
                ${raw([
                  { name: 'Tedarikçi Teklifi', t: 'PDF · 248 KB' },
                  { name: 'Onay E-Postası', t: 'EML · 12 KB' },
                  { name: 'Lojistik Anlaşması', t: 'PDF · 1.2 MB' },
                ].map((f) => `
                  <div style="display:flex;align-items:center;gap:10px;padding:8px 10px;border-radius:8px;cursor:pointer">
                    <div style="width:30px;height:30px;border-radius:7px;background:var(--bg);display:grid;place-items:center;color:var(--text-3)">${I.FileText(14, 2)}</div>
                    <div style="flex:1;min-width:0">
                      <div style="font-size:12px;font-weight:600;color:var(--text)">${esc(f.name)}</div>
                      <div class="mono" style="font-size:10.5px;color:var(--text-4)">${esc(f.t)}</div>
                    </div>
                    ${I.Download(13, 2)}
                  </div>
                `).join(''))}
              </div>
            </div>
          </div>
        </div>
      </div>
    `;
  };

  // ============== PURCHASING NEW ==============
  const PurchasingNew = (state) => {
    const f = state.formPO = state.formPO || { supplier: '', refCode: '', docDate: '2026-05-27', dueDate: '2026-06-10', paymentTerm: '30', deliveryAddr: 'Merkez Depo · İstanbul', notes: '' };
    const errors = state.formPOErrors || {};

    return html`
      <div class="page" data-screen-label="Purchasing New">
        ${pageHeader({
          crumbs: [
            { label: 'Anasayfa' },
            { label: 'Satınalma', route: 'purchasing' },
            { label: 'Yeni Sipariş' },
          ],
          title: 'Yeni Satınalma Siparişi',
          sub: 'Tedarikçi seçin, kalemleri ekleyin ve evrakı taslak ya da onaylı olarak kaydedin.',
          actions: btn('Vazgeç', { kind: 'ghost', icon: 'X', action: 'nav', route: 'purchasing', kbd: 'ESC' }),
        })}

        <div style="max-width:720px;margin:0 auto">
          <div class="card">
            <div class="card-hdr">
              <div><div class="card-title">Evrak Başlığı</div><div class="card-sub">Sipariş hakkında temel bilgileri girin</div></div>
              <span class="badge badge-warn"><span class="badge-dot"></span>Taslak</span>
            </div>
            <div class="card-body" style="display:flex;flex-direction:column;gap:18px">

              <div class="form-group">
                <label class="form-label">Tedarikçi <span class="req">*</span></label>
                <select class="form-ctrl${errors.supplier ? ' is-error' : ''}" data-form="po" data-field="supplier">
                  <option value="">Tedarikçi seçin…</option>
                  ${D.suppliers.map((s) => `<option value="${esc(s.id)}" ${f.supplier === s.id ? 'selected' : ''}>${esc(s.name)} · ${esc(s.city)}</option>`).join('')}
                </select>
                ${errors.supplier ? `<div class="form-error">${I.AlertCircle(12, 2.4)}${esc(errors.supplier)}</div>` : '<div class="form-hint">VKN ve adres bilgileri tedarikçiden otomatik alınır</div>'}
              </div>

              <div class="form-row">
                <div class="form-group">
                  <label class="form-label">Referans Kodu <span class="req">*</span></label>
                  <div class="form-prefix-wrap">
                    <span class="form-prefix mono" style="font-size:12px">REF-</span>
                    <input type="text" class="form-ctrl with-prefix mono${errors.refCode ? ' is-error' : ''}" placeholder="2026-A042" value="${esc(f.refCode)}" data-form="po" data-field="refCode" />
                  </div>
                  ${errors.refCode ? `<div class="form-error">${I.AlertCircle(12, 2.4)}${esc(errors.refCode)}</div>` : ''}
                </div>
                <div class="form-group">
                  <label class="form-label">Para Birimi</label>
                  <select class="form-ctrl"><option>₺ Türk Lirası (TRY)</option><option>$ ABD Doları (USD)</option><option>€ Euro (EUR)</option></select>
                </div>
              </div>

              <div class="form-row">
                <div class="form-group">
                  <label class="form-label">Evrak Tarihi <span class="req">*</span></label>
                  <input type="date" class="form-ctrl mono" value="${esc(f.docDate)}" data-form="po" data-field="docDate" />
                </div>
                <div class="form-group">
                  <label class="form-label">Vade Tarihi <span class="req">*</span></label>
                  <input type="date" class="form-ctrl mono${errors.dueDate ? ' is-error' : ''}" value="${esc(f.dueDate)}" data-form="po" data-field="dueDate" />
                  ${errors.dueDate ? `<div class="form-error">${I.AlertCircle(12, 2.4)}${esc(errors.dueDate)}</div>` : ''}
                </div>
              </div>

              <div class="form-row">
                <div class="form-group">
                  <label class="form-label">Ödeme Şartı</label>
                  <select class="form-ctrl" data-form="po" data-field="paymentTerm">
                    <option value="0" ${f.paymentTerm==='0'?'selected':''}>Peşin</option>
                    <option value="15" ${f.paymentTerm==='15'?'selected':''}>15 gün vadeli</option>
                    <option value="30" ${f.paymentTerm==='30'?'selected':''}>30 gün vadeli</option>
                    <option value="60" ${f.paymentTerm==='60'?'selected':''}>60 gün vadeli</option>
                  </select>
                </div>
                <div class="form-group">
                  <label class="form-label">Teslimat Adresi</label>
                  <select class="form-ctrl" data-form="po" data-field="deliveryAddr">
                    <option>Merkez Depo · İstanbul</option><option>Şube Depo · İzmir</option><option>Üretim Tesisi · Bursa</option>
                  </select>
                </div>
              </div>

              <div class="form-group">
                <label class="form-label">Not / Açıklama</label>
                <textarea class="form-ctrl" placeholder="Sipariş ile ilgili özel not, talimat veya teslimat detayı…" data-form="po" data-field="notes">${esc(f.notes)}</textarea>
                <div class="form-hint">İsteğe bağlı · Tedarikçiye iletilir</div>
              </div>

              <div style="padding:12px;background:var(--bg);border-radius:10px;border:1px solid var(--border);display:flex;align-items:center;gap:12px">
                <div style="width:32px;height:32px;border-radius:8px;background:var(--brand-tint-15);color:var(--brand-500);display:grid;place-items:center">${I.Info(15, 2)}</div>
                <div style="flex:1;font-size:12px;color:var(--text-2)">Başlık kaydedildikten sonra <span class="text-strong">Kalemler</span> sekmesinden ürün ekleyebilirsiniz.</div>
              </div>
            </div>
            <div class="card-foot">
              <button class="btn btn-ghost btn-sm" data-action="nav" data-route="purchasing">${I.X(13, 2)}İptal</button>
              <div style="display:flex;gap:8px">
                <button class="btn btn-secondary btn-sm" data-action="po-submit" data-asdraft="1">${I.FileText(13, 2)}Taslak Olarak Kaydet</button>
                <button class="btn btn-primary btn-sm" data-action="po-submit">${I.Check(13, 2)}Onayla & Kaydet<span class="btn-kbd">Alt+S</span></button>
              </div>
            </div>
          </div>
        </div>
      </div>
    `;
  };

  window.SCREENS = window.SCREENS || {};
  Object.assign(window.SCREENS, { Dashboard, PurchasingList, PurchasingDetail, PurchasingNew });
})();
