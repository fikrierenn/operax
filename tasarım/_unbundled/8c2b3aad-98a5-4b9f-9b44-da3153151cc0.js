/* OPERAX — Pure JS · Ekranlar (2/3): WMS · Cari · Stok · Envanter · Satış */
(function () {
  const I = window.ICONS;
  const D = window.OPX;
  const U = window.UI;
  const { html, raw, esc, statusBadge, shipBadge, movementBadge, avatar, initials,
    pageHeader, kpiCard, sparkline, gauge, emptyState, tabs, btn } = U;

  // ============== WMS ==============
  const WmsScreen = (state) => {
    const empty = state.tweaks.emptyState;
    const local = state.local;
    local.zone = local.zone || 'A';
    local.tab = local.tab || 'locations';
    const zoneLocs = empty ? [] : D.locations.filter((l) => l.zone === local.zone);

    return html`
      <div class="page" data-screen-label="WMS" style="max-width:1640px">
        ${pageHeader({
          crumbs: [{ label: 'Anasayfa' }, { label: 'Depo / WMS' }],
          title: 'Depo Operasyonları',
          sub: 'Merkez Depo · İstanbul · 12.480 / 16.000 göz aktif kullanımda',
          actions: `
            ${btn('Senkronize Et', { kind: 'secondary', icon: 'Refresh' })}
            ${btn('Harita Görünümü', { kind: 'secondary', icon: 'Map' })}
            ${btn('Yeni Sevkiyat', { kind: 'primary', icon: 'Plus', kbd: 'Alt+N' })}
          `,
        })}

        <div class="kpi-grid">
          ${kpiCard({ label: 'Aktif Konum', value: empty ? '—' : 60, glow: 'brand', valueSize: 24 })}
          ${kpiCard({ label: 'Doluluk Oranı', value: empty ? '—' : '%78', glow: 'success', valueSize: 24 })}
          ${kpiCard({ label: 'Bekleyen Putaway', value: empty ? '—' : 12, glow: 'warn', valueSize: 24 })}
          ${kpiCard({ label: 'Bugünkü Pick', value: empty ? '—' : 348, glow: 'brand', valueSize: 24 })}
        </div>

        <div class="card" style="margin-bottom:14px">
          ${tabs([
            { id: 'locations', label: 'Konumlar', icon: 'Warehouse', count: 60 },
            { id: 'incoming', label: 'Bekleyen Girişler', icon: 'Truck', count: empty ? 0 : D.incoming.length },
            { id: 'putaway', label: 'Putaway Görevleri', icon: 'Forklift', count: 12 },
            { id: 'pick', label: 'Pick Listeleri', icon: 'Pkg', count: 7 },
          ], local.tab, 'wms-tab')}

          ${local.tab === 'locations' ? html`
            <div class="data-table-toolbar">
              <div style="display:flex;gap:4px">
                ${raw(['A', 'B', 'C'].map((z) => `
                  <button class="btn ${local.zone === z ? 'btn-primary' : 'btn-secondary'} btn-sm" data-action="wms-zone" data-zone="${esc(z)}" style="min-width:80px">Zon ${esc(z)}</button>
                `).join(''))}
              </div>
              <div style="margin-left:12px;font-size:11.5px;color:var(--text-3)">
                <span class="dot" style="background:var(--success)"></span> &lt;60%
                <span class="dot" style="background:var(--warn)"></span> 60-85%
                <span class="dot" style="background:var(--danger)"></span> &gt;85%
              </div>
              <div class="spacer"></div>
              <div style="position:relative">
                ${raw(I.Search(13, 2).replace('<svg ', '<svg style="position:absolute;left:10px;top:50%;transform:translateY(-50%);color:var(--text-4)" '))}
                <input class="form-ctrl" style="height:30px;padding-left:30px;width:220px;font-size:12px" placeholder="A-12-04 ara…" />
              </div>
            </div>

            ${empty ? emptyState({ icon: 'Warehouse', title: 'Konum tanımlı değil', msg: 'Bu zon için henüz konum yapılandırılmamış.', action: btn('Konum Ekle', { kind: 'primary', icon: 'Plus' }) }) : html`
              <div style="padding:18px">
                <div style="display:grid;grid-template-columns:repeat(5,1fr);gap:10px">
                  ${raw(zoneLocs.map((loc) => {
                    const color = loc.fillPct > 85 ? 'var(--danger)' : loc.fillPct > 60 ? 'var(--warn)' : 'var(--success)';
                    const tintBg = loc.fillPct > 85 ? 'hsl(0 86% 97%)' : loc.fillPct > 60 ? 'hsl(48 96% 96%)' : 'hsl(152 81% 96%)';
                    const tintBd = loc.fillPct > 85 ? 'hsl(0 84% 85%)' : loc.fillPct > 60 ? 'hsl(38 92% 80%)' : 'hsl(160 60% 80%)';
                    return `
                      <div style="background:${tintBg};border:1px solid ${tintBd};border-radius:10px;padding:10px;cursor:pointer">
                        <div style="display:flex;justify-content:space-between;align-items:flex-start">
                          <span class="mono" style="font-size:13px;font-weight:700;color:var(--text)">${esc(loc.code)}</span>
                          <span class="mono" style="font-size:13px;font-weight:700;color:${color}">${esc(loc.fillPct)}%</span>
                        </div>
                        <div style="height:4px;background:#fff;border-radius:2px;margin:6px 0;overflow:hidden">
                          <div style="width:${loc.fillPct}%;height:100%;background:${color};border-radius:2px"></div>
                        </div>
                        <div style="display:flex;align-items:center;justify-content:space-between;font-size:10.5px;color:var(--text-3)">
                          <span class="mono">${esc(loc.sku)}</span><span>${esc(loc.uom)}</span>
                        </div>
                      </div>
                    `;
                  }).join(''))}
                </div>
              </div>
            `}
          ` : ''}

          ${local.tab === 'incoming' ? html`
            <div class="card-body-flush">
              ${empty ? emptyState({ icon: 'Truck', title: 'Planlı sevkiyat yok', msg: 'Bekleyen giriş sevkiyatı bulunmuyor.' }) : html`
                <table class="data-table">
                  <thead><tr><th>LPN</th><th>Tedarikçi</th><th>SKU</th><th>Ürün</th><th class="num">Miktar</th><th>ETA</th><th>Dock</th><th>Durum</th><th style="width:100px"></th></tr></thead>
                  <tbody>
                    ${raw(D.incoming.map((it, i) => {
                      const p = D.products.find((x) => x.sku === it.sku) || {};
                      const statusBg = i === 0 ? 'warn' : i === 1 ? 'info' : 'neutral';
                      const statusLabel = i === 0 ? 'Yolda' : i === 1 ? 'Planlandı' : 'Beklemede';
                      return `
                        <tr>
                          <td><span class="mono row-strong" style="color:var(--brand-500)">${esc(it.lpn)}</span></td>
                          <td>${esc(D.supplierByCode(it.supplier).name.split(' ').slice(0, 2).join(' '))}</td>
                          <td style="cursor:pointer" data-action="nav" data-route="stok/kart/${esc(it.sku)}"><span class="mono">${esc(it.sku)}</span></td>
                          <td>${esc(p.name || '—')}</td>
                          <td class="num"><span class="row-strong">${esc(D.fmtNum(it.qty))}</span> <span class="muted">${esc(p.uom || '')}</span></td>
                          <td class="mono">${esc(D.fmtDateShort(it.eta))}</td>
                          <td><span class="badge badge-brand"><span class="badge-dot"></span>${esc(it.dock)}</span></td>
                          <td><span class="badge badge-${statusBg}"><span class="badge-dot"></span>${esc(statusLabel)}</span></td>
                          <td><button class="btn btn-secondary btn-xs">Putaway</button></td>
                        </tr>
                      `;
                    }).join(''))}
                  </tbody>
                </table>
              `}
            </div>
          ` : ''}

          ${local.tab === 'putaway' ? emptyState({ icon: 'Forklift', title: 'Putaway sekmesi', msg: 'Devam eden 12 putaway görevi · Detaylar için Konumlar sekmesini kullanın.' }) : ''}
          ${local.tab === 'pick' ? emptyState({ icon: 'Pkg', title: 'Pick sekmesi', msg: 'Aktif 7 pick listesi.' }) : ''}
        </div>
      </div>
    `;
  };

  // ============== CARI LIST ==============
  const CariList = (state) => {
    const empty = state.tweaks.emptyState;
    const local = state.local;
    local.typeFilter = local.typeFilter || 'all';
    local.q = local.q || '';
    let rows = empty ? [] : D.cariList;
    if (local.typeFilter !== 'all') rows = rows.filter((c) => c.type === local.typeFilter);
    if (local.q) {
      const qL = local.q.toLocaleLowerCase('tr-TR');
      rows = rows.filter((c) => c.name.toLocaleLowerCase('tr-TR').includes(qL) || c.code.toLowerCase().includes(qL));
    }
    const counts = {
      all: D.cariList.length,
      supplier: D.cariList.filter((c) => c.type === 'supplier').length,
      customer: D.cariList.filter((c) => c.type === 'customer').length,
    };

    return html`
      <div class="page" data-screen-label="Cari Kartlar">
        ${pageHeader({
          crumbs: [{ label: 'Anasayfa' }, { label: 'Muhasebe' }, { label: 'Cari Kartlar' }],
          title: 'Cari Hesaplar',
          sub: 'Tedarikçi ve müşteri hesapları, bakiyeler ve kredi limitleri',
          actions: `${btn('İçeri Aktar', { kind: 'secondary', icon: 'Upload' })}${btn('Yeni Cari', { kind: 'primary', icon: 'Plus', kbd: 'Alt+N' })}`,
        })}

        <div class="card">
          ${tabs([
            { id: 'all', label: 'Tümü', count: counts.all },
            { id: 'supplier', label: 'Tedarikçiler', count: counts.supplier },
            { id: 'customer', label: 'Müşteriler', count: counts.customer },
          ], local.typeFilter, 'cari-tab')}

          <div class="data-table-toolbar">
            <div style="position:relative;flex:0 0 280px">
              ${raw(I.Search(14, 2).replace('<svg ', '<svg style="position:absolute;left:11px;top:50%;transform:translateY(-50%);color:var(--text-4)" '))}
              <input class="form-ctrl" style="padding-left:34px;height:34px" placeholder="Cari kod veya isim ara…" value="${esc(local.q)}" data-action="cari-search" />
            </div>
            <button class="chip">Şehir: Tümü${I.ChevronDown(11, 2)}</button>
            <button class="chip">Bakiye: Tümü${I.ChevronDown(11, 2)}</button>
          </div>

          ${rows.length === 0 ? emptyState({ icon: 'Users', title: 'Cari hesap yok', msg: 'İlk cari hesabınızı oluşturun.' }) : html`
            <table class="data-table">
              <thead><tr><th>Cari</th><th>Tür</th><th>VKN</th><th>Şehir</th><th class="num">Vade</th><th class="num">Bakiye</th><th>Risk</th><th style="width:36px"></th></tr></thead>
              <tbody>
                ${raw(rows.map((c) => {
                  const isDebt = c.balance < 0;
                  const usage = Math.round((Math.abs(c.balance) / c.creditLimit) * 100);
                  return `
                    <tr style="cursor:pointer" data-action="nav" data-route="cari/detail/${esc(c.code)}">
                      <td><div style="display:flex;align-items:center;gap:10px">${avatar(c.code, { size: 28, fontSize: 10.5 }).__raw}<div><div class="row-strong">${esc(c.name)}</div><div class="row-sub mono">${esc(c.code)}</div></div></div></td>
                      <td>${c.type === 'supplier' ? '<span class="badge badge-brand"><span class="badge-dot"></span>Tedarikçi</span>' : '<span class="badge badge-info"><span class="badge-dot"></span>Müşteri</span>'}</td>
                      <td class="mono">${esc(c.tax)}</td>
                      <td>${esc(c.city)}</td>
                      <td class="num mono">${esc(c.paymentTerm)} gün</td>
                      <td class="num"><span class="mono row-strong" style="color:${isDebt ? 'var(--danger-text)' : 'var(--success-text)'}">${isDebt ? '−' : '+'}${esc(D.fmtTL(Math.abs(c.balance)))}</span></td>
                      <td><div style="display:flex;align-items:center;gap:7px"><div style="width:60px">${gauge(usage, 5)}</div><span class="mono" style="font-size:11px;color:var(--text-3)">%${usage}</span></div></td>
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

  // ============== CARI DETAY ==============
  const CariDetail = (state, code) => {
    const empty = state.tweaks.emptyState;
    const cari = D.cariByCode(code);
    const local = state.local;
    local.tab = local.tab || 'overview';
    const isSupplier = cari.type === 'supplier';
    const balAbs = Math.abs(cari.balance);
    const isDebt = cari.balance < 0;
    const movements = empty ? [] : D.cariMovementsFor(cari);
    const relatedPOs = empty || cari.type !== 'supplier' ? [] : D.POs.filter((p) => p.supplier === cari.code);
    const spark = D.sparkSeries(24, isSupplier ? 280 : 320, 22, cari.code.charCodeAt(0));

    return html`
      <div class="page" data-screen-label="Cari Kart" style="max-width:1480px">
        ${pageHeader({
          crumbs: [
            { label: 'Anasayfa' },
            { label: 'Muhasebe', route: 'cari' },
            { label: 'Cari Kartlar', route: 'cari' },
            { label: cari.code, mono: true },
          ],
          titleSlot: html`
            <div style="display:flex;align-items:center;gap:14px;margin-top:4px">
              ${avatar(cari.code, { size: 48, fontSize: 16 })}
              <div>
                <div style="display:flex;align-items:center;gap:10px">
                  <h1 style="margin:0">${cari.name}</h1>
                  ${isSupplier ? html`<span class="badge badge-brand"><span class="badge-dot"></span>Tedarikçi</span>` : html`<span class="badge badge-info"><span class="badge-dot"></span>Müşteri</span>`}
                  <span class="badge badge-success"><span class="badge-dot"></span>Aktif</span>
                </div>
                <p style="margin-top:4px">Cari Kod <span class="mono text-strong">${cari.code}</span> · VKN <span class="mono">${cari.tax}</span> · ${cari.city}, Türkiye · Müşteri olalı: <span class="mono">${D.fmtDate(cari.since)}</span></p>
              </div>
            </div>
          `,
          actions: `
            ${btn('Geri', { kind: 'ghost', icon: 'ChevronLeft', action: 'nav', route: 'cari' })}
            ${btn('Hesap Ekstresi', { kind: 'secondary', icon: 'Mail' })}
            ${btn('PDF', { kind: 'secondary', icon: 'Download' })}
            ${btn('Yeni İşlem', { kind: 'primary', icon: 'Plus', kbd: 'Alt+N' })}
          `,
        })}

        <div class="kpi-grid">
          ${kpiCard({ label: isDebt ? 'Borç Bakiyemiz' : 'Alacak Bakiyemiz', value: empty ? '—' : D.fmtTL(balAbs), glow: isDebt ? 'danger' : 'success', valueSize: 22, valueColor: isDebt ? 'var(--danger-text)' : 'var(--success-text)', sub: isDebt ? `Vadesi geçen: ${cari.overdueDays} gün` : 'Tahsil edilecek' })}
          ${kpiCard({ label: isSupplier ? 'Açık Sipariş' : 'Bekleyen Sevkiyat', value: empty ? '—' : (isSupplier ? relatedPOs.filter((p) => p.status !== 'Cancelled').length : 4), glow: 'brand', valueSize: 22, sub: isSupplier ? `${D.fmtTL(relatedPOs.reduce((a, p) => a + (p.status !== 'Cancelled' ? p.total : 0), 0))} tutarında` : '3 adet hazırlanıyor' })}
          ${kpiCard({ label: '12 Aylık Hacim', value: empty ? '—' : `₺${(11.2 + (cari.code.charCodeAt(0) % 8)).toFixed(1)}M`, glow: 'brand', valueSize: 22, sub: `${(cari.code.charCodeAt(0) * 17) % 240 + 60} işlem` })}
          ${kpiCard({ label: 'Kredi Limiti', value: empty ? '—' : D.fmtTL(cari.creditLimit), glow: 'warn', valueSize: 22, sub: `Kullanım: %${Math.round((balAbs / cari.creditLimit) * 100)}` })}
        </div>

        <div class="card">
          ${tabs([
            { id: 'overview', label: 'Özet', icon: 'Info' },
            { id: 'movements', label: 'Hareketler', icon: 'Swap', count: empty ? 0 : movements.length },
            { id: 'orders', label: isSupplier ? 'Satınalma Geçmişi' : 'Satış Geçmişi', icon: 'Cart', count: empty ? 0 : (isSupplier ? relatedPOs.length : 8) },
            { id: 'contact', label: 'İletişim', icon: 'Users' },
            { id: 'docs', label: 'Belgeler', icon: 'FileText', count: 5 },
          ], local.tab, 'cari-detail-tab')}

          ${local.tab === 'overview' ? html`
            <div style="padding:18px;display:grid;grid-template-columns:1fr 340px;gap:18px">
              <div style="display:flex;flex-direction:column;gap:14px">
                <div style="display:grid;grid-template-columns:repeat(3,1fr);gap:14px">
                  <div style="padding:14px;background:var(--surface-2);border:1px solid var(--border);border-radius:10px">
                    <div style="display:flex;align-items:center;gap:6px;margin-bottom:8px;color:var(--text-3)">${I.Building(13, 2)}<span class="form-label" style="margin-bottom:0">Adres</span></div>
                    <div style="font-size:12.5px;color:var(--text);line-height:1.55">${cari.address}</div>
                  </div>
                  <div style="padding:14px;background:var(--surface-2);border:1px solid var(--border);border-radius:10px">
                    <div style="display:flex;align-items:center;gap:6px;margin-bottom:8px;color:var(--text-3)">${I.Phone(13, 2)}<span class="form-label" style="margin-bottom:0">Telefon</span></div>
                    <div class="mono" style="font-size:13px;color:var(--text);font-weight:600">${cari.contactPhone}</div>
                  </div>
                  <div style="padding:14px;background:var(--surface-2);border:1px solid var(--border);border-radius:10px">
                    <div style="display:flex;align-items:center;gap:6px;margin-bottom:8px;color:var(--text-3)">${I.Mail(13, 2)}<span class="form-label" style="margin-bottom:0">E-posta</span></div>
                    <div class="mono" style="font-size:12.5px;color:var(--brand-500);font-weight:600">${cari.contactEmail}</div>
                  </div>
                </div>

                <div class="card" style="box-shadow:none">
                  <div class="card-hdr">
                    <div><div class="card-title">Finansal Bilgiler</div><div class="card-sub">Banka, vergi ve ödeme şartları</div></div>
                    <button class="btn btn-ghost btn-xs">${I.Edit(12, 2)}Düzenle</button>
                  </div>
                  <div class="card-body" style="display:grid;grid-template-columns:repeat(2,1fr);gap:16px">
                    <div><div class="form-label">Banka</div><div style="font-size:13px;font-weight:600;margin-top:4px">${cari.bank}</div></div>
                    <div><div class="form-label">IBAN</div><div class="mono" style="font-size:12.5px;margin-top:4px">${cari.iban}</div></div>
                    <div><div class="form-label">VKN</div><div class="mono" style="font-size:13px;font-weight:600;margin-top:4px">${cari.tax}</div></div>
                    <div><div class="form-label">Vergi Dairesi</div><div style="font-size:13px;font-weight:600;margin-top:4px">Beşiktaş</div></div>
                    <div><div class="form-label">Ödeme Şartı</div><div style="font-size:13px;font-weight:600;margin-top:4px"><span class="mono">${cari.paymentTerm}</span> gün vadeli</div></div>
                    <div><div class="form-label">Para Birimi</div><div class="mono" style="font-size:13px;font-weight:600;margin-top:4px">TRY · ₺</div></div>
                  </div>
                </div>

                <div class="card" style="box-shadow:none">
                  <div class="card-hdr">
                    <div class="card-title">Son 5 Hareket</div>
                    <button class="btn btn-ghost btn-xs" data-action="cari-detail-tab" data-tab="movements">Tümünü Gör${raw(I.ChevronRight(12, 2))}</button>
                  </div>
                  <div class="card-body-flush">
                    ${empty ? emptyState({ title: 'Henüz hareket yok' }) : html`
                      <table class="data-table">
                        <thead><tr><th>Tarih</th><th>Evrak</th><th>Açıklama</th><th class="num">Borç</th><th class="num">Alacak</th><th class="num">Bakiye</th></tr></thead>
                        <tbody>
                          ${raw(movements.slice(0, 5).map((m) => `
                            <tr>
                              <td class="mono muted">${esc(D.fmtDateShort(m.date))}</td>
                              <td><span class="mono row-strong" style="color:var(--brand-500)">${esc(m.doc)}</span></td>
                              <td>${esc(m.kind)} · <span class="muted">${esc(m.desc)}</span></td>
                              <td class="num">${m.debit > 0 ? `<span style="color:var(--success-text);font-weight:600" class="mono">${esc(D.fmtTL(m.debit))}</span>` : '—'}</td>
                              <td class="num">${m.credit > 0 ? `<span style="color:var(--danger-text);font-weight:600" class="mono">${esc(D.fmtTL(m.credit))}</span>` : '—'}</td>
                              <td class="num mono row-strong" style="color:${m.balance < 0 ? 'var(--danger-text)' : 'var(--text)'}">${esc(D.fmtTL(m.balance))}</td>
                            </tr>
                          `).join(''))}
                        </tbody>
                      </table>
                    `}
                  </div>
                </div>
              </div>

              <div style="display:flex;flex-direction:column;gap:14px">
                <div class="card" style="box-shadow:none">
                  <div class="card-hdr"><div class="card-title">Bakiye Seyri · 6 ay</div></div>
                  <div class="card-body" style="padding:12px 14px 14px">
                    <div style="display:flex;align-items:baseline;justify-content:space-between;margin-bottom:6px">
                      <span class="mono" style="font-size:18px;font-weight:700;color:${isDebt ? 'var(--danger-text)' : 'var(--success-text)'}">${isDebt ? '−' : '+'}${D.fmtTL(balAbs)}</span>
                      <span class="kpi-delta down" style="font-size:10.5px">${raw(I.ArrowDown(10, 2.5))}%4.2</span>
                    </div>
                    ${raw(sparkline(spark, { color: isDebt ? 'var(--danger)' : 'var(--success)', w: 280, h: 56 }))}
                    <div class="mono" style="display:flex;justify-content:space-between;font-size:10px;color:var(--text-4);margin-top:6px">
                      <span>Ara</span><span>Şub</span><span>Nis</span><span>May</span>
                    </div>
                  </div>
                </div>

                <div class="card" style="box-shadow:none">
                  <div class="card-hdr"><div class="card-title">İlgili Kişi</div></div>
                  <div class="card-body" style="padding:14px">
                    <div style="display:flex;align-items:center;gap:12px;margin-bottom:12px">
                      ${avatar(initials(cari.contactPerson), { size: 36, fontSize: 12 })}
                      <div>
                        <div style="font-size:13px;font-weight:700">${cari.contactPerson}</div>
                        <div style="font-size:11px;color:var(--text-3)">${isSupplier ? 'Satış Sorumlusu' : 'Satınalma Müdürü'}</div>
                      </div>
                    </div>
                    <div style="display:flex;flex-direction:column;gap:7px;font-size:11.5px">
                      <div style="display:flex;align-items:center;gap:7px;color:var(--text-2)">${I.Phone(11.5, 2)}<span class="mono">${cari.contactPhone}</span></div>
                      <div style="display:flex;align-items:center;gap:7px;color:var(--text-2)">${I.Mail(11.5, 2)}<span class="mono">${cari.contactEmail}</span></div>
                    </div>
                  </div>
                </div>

                <div class="card" style="box-shadow:none">
                  <div class="card-hdr"><div class="card-title">${isSupplier ? 'Tedarikçi Skoru' : 'Müşteri Skoru'}</div></div>
                  <div class="card-body" style="padding:14px">
                    <div style="display:flex;align-items:baseline;gap:6px">
                      <span class="mono" style="font-size:32px;font-weight:800;color:var(--brand-500)">${cari.rating.toFixed(1)}</span>
                      <span style="font-size:12px;color:var(--text-3)">/ 5.0</span>
                    </div>
                    <div style="display:flex;gap:1px;margin-top:4px">
                      ${raw([1,2,3,4,5].map((i) => I.Star(13, 1.5).replace('stroke="currentColor"', `stroke="${i <= Math.round(cari.rating) ? 'var(--warn)' : 'var(--border-strong)'}"`).replace('fill="none"', `fill="${i <= Math.round(cari.rating) ? 'var(--warn)' : 'none'}"`)).join(''))}
                    </div>
                    <div style="margin-top:12px;display:flex;flex-direction:column;gap:6px">
                      ${raw([{l:'Termin', v:94},{l:'Kalite', v:88},{l:'İletişim', v:96}].map((m) => `
                        <div>
                          <div style="display:flex;justify-content:space-between;font-size:10.5px;margin-bottom:3px"><span style="color:var(--text-3)">${esc(m.l)}</span><span class="mono text-strong">%${esc(m.v)}</span></div>
                          ${gauge(m.v, 4)}
                        </div>
                      `).join(''))}
                    </div>
                  </div>
                </div>
              </div>
            </div>
          ` : ''}

          ${local.tab === 'movements' ? html`
            ${movements.length === 0 ? emptyState({ icon: 'Swap', title: 'Hareket yok' }) : html`
              <table class="data-table">
                <thead><tr><th>Tarih</th><th>Evrak No</th><th>Tür</th><th>Açıklama</th><th class="num">Borç</th><th class="num">Alacak</th><th class="num">Bakiye</th></tr></thead>
                <tbody>
                  ${raw(movements.map((m) => `
                    <tr>
                      <td class="mono muted">${esc(D.fmtDateShort(m.date))}</td>
                      <td><span class="mono row-strong" style="color:var(--brand-500)">${esc(m.doc)}</span></td>
                      <td><span class="badge badge-neutral">${esc(m.kind)}</span></td>
                      <td class="muted">${esc(m.desc)}</td>
                      <td class="num">${m.debit > 0 ? `<span class="mono row-strong" style="color:var(--success-text)">${esc(D.fmtTL(m.debit))}</span>` : '<span class="muted">—</span>'}</td>
                      <td class="num">${m.credit > 0 ? `<span class="mono row-strong" style="color:var(--danger-text)">${esc(D.fmtTL(m.credit))}</span>` : '<span class="muted">—</span>'}</td>
                      <td class="num mono row-strong" style="color:${m.balance < 0 ? 'var(--danger-text)' : 'var(--success-text)'}">${esc(D.fmtTL(m.balance))}</td>
                    </tr>
                  `).join(''))}
                </tbody>
              </table>
              <div class="card-foot">
                <div style="font-size:12px;color:var(--text-3)">Dönem · Borç <span class="mono text-strong">${D.fmtTL(movements.reduce((a, m) => a + m.debit, 0))}</span> · Alacak <span class="mono text-strong">${D.fmtTL(movements.reduce((a, m) => a + m.credit, 0))}</span></div>
                <div style="font-size:12.5px">Güncel Bakiye <span class="mono" style="font-weight:700;font-size:15px;color:${isDebt ? 'var(--danger-text)' : 'var(--success-text)'};margin-left:6px">${isDebt ? '−' : ''}${D.fmtTL(balAbs)}</span></div>
              </div>
            `}
          ` : ''}

          ${local.tab === 'orders' ? html`
            <div class="card-body-flush">
              ${empty || (isSupplier && relatedPOs.length === 0) ? emptyState({ icon: 'FileText', title: 'Sipariş yok' }) : html`
                <table class="data-table">
                  <thead><tr><th>Evrak No</th><th>Tarih</th><th>Vade</th><th class="num">Kalem</th><th class="num">Tutar</th><th>Durum</th><th style="width:36px"></th></tr></thead>
                  <tbody>
                    ${raw((isSupplier ? relatedPOs : D.POs.slice(0, 6)).map((po) => `
                      <tr style="cursor:pointer" data-action="nav" data-route="purchasing/detail/${esc(po.no)}">
                        <td><span class="mono row-strong" style="color:var(--brand-500)">${esc(po.no)}</span></td>
                        <td class="mono muted">${esc(D.fmtDateShort(po.date))}</td>
                        <td class="mono">${esc(D.fmtDateShort(po.due))}</td>
                        <td class="num">${esc(po.items)}</td>
                        <td class="num"><span class="mono row-strong">${esc(D.fmtTL(po.total))}</span></td>
                        <td>${statusBadge(po.status).__raw}</td>
                        <td>${I.ChevronRight(14, 2)}</td>
                      </tr>
                    `).join(''))}
                  </tbody>
                </table>
              `}
            </div>
          ` : ''}

          ${local.tab === 'contact' ? html`
            <div style="padding:18px;display:grid;grid-template-columns:repeat(3,1fr);gap:14px">
              ${raw([
                { name: cari.contactPerson, role: isSupplier ? 'Satış Sorumlusu' : 'Satınalma Müdürü', phone: cari.contactPhone, email: cari.contactEmail, primary: true },
                { name: 'Levent Öztürk', role: 'Muhasebe', phone: cari.contactPhone.replace(/\d$/, '5'), email: 'muhasebe@' + cari.code.toLowerCase() + '.com.tr' },
                { name: 'Defne Aksoy', role: 'Lojistik', phone: cari.contactPhone.replace(/\d\d$/, '12'), email: 'sevkiyat@' + cari.code.toLowerCase() + '.com.tr' },
              ].map((p) => `
                <div style="padding:16px;border:1px solid var(--border);border-radius:12px;background:${p.primary ? 'var(--brand-tint-08)' : 'var(--surface)'}">
                  <div style="display:flex;align-items:center;gap:12px;margin-bottom:12px">
                    ${avatar(initials(p.name), { size: 40, fontSize: 13 }).__raw}
                    <div style="flex:1;min-width:0">
                      <div style="font-size:13.5px;font-weight:700">${esc(p.name)}</div>
                      <div style="font-size:11.5px;color:var(--text-3)">${esc(p.role)}</div>
                    </div>
                    ${p.primary ? '<span class="badge badge-brand" style="height:18px"><span class="badge-dot"></span>Birincil</span>' : ''}
                  </div>
                  <div style="display:flex;flex-direction:column;gap:6px;font-size:11.5px">
                    <div style="display:flex;align-items:center;gap:7px;color:var(--text-2)">${I.Phone(11.5, 2)}<span class="mono">${esc(p.phone)}</span></div>
                    <div style="display:flex;align-items:center;gap:7px;color:var(--text-2)">${I.Mail(11.5, 2)}<span class="mono">${esc(p.email)}</span></div>
                  </div>
                </div>
              `).join(''))}
            </div>
          ` : ''}

          ${local.tab === 'docs' ? html`
            <div class="card-body-flush">
              <table class="data-table">
                <thead><tr><th>Belge</th><th>Tür</th><th>Boyut</th><th>Tarih</th><th style="width:80px"></th></tr></thead>
                <tbody>
                  ${raw([
                    { n: 'Bayi Sözleşmesi 2026.pdf', t: 'Sözleşme', sz: '1.2 MB', d: '2026-01-15' },
                    { n: 'Vergi Levhası.pdf', t: 'Resmi Belge', sz: '248 KB', d: '2026-01-10' },
                    { n: 'Faaliyet Belgesi.pdf', t: 'Resmi Belge', sz: '312 KB', d: '2025-12-22' },
                    { n: 'İmza Sirküleri.pdf', t: 'Resmi Belge', sz: '180 KB', d: '2025-11-08' },
                    { n: 'Kalite Sertifikası.pdf', t: 'Kalite', sz: '720 KB', d: '2025-09-04' },
                  ].map((f) => `
                    <tr>
                      <td><div style="display:flex;align-items:center;gap:8px">${I.FileText(14, 2)}<span class="row-strong">${esc(f.n)}</span></div></td>
                      <td><span class="badge badge-neutral">${esc(f.t)}</span></td>
                      <td class="mono muted">${esc(f.sz)}</td>
                      <td class="mono muted">${esc(D.fmtDateShort(f.d))}</td>
                      <td><div style="display:flex;gap:4px"><button class="icon-btn" style="width:26px;height:26px">${I.Download(13, 2)}</button></div></td>
                    </tr>
                  `).join(''))}
                </tbody>
              </table>
            </div>
          ` : ''}
        </div>
      </div>
    `;
  };

  // ============== STOK KARTI DETAY ==============
  const StokKartiDetail = (state, sku) => {
    const empty = state.tweaks.emptyState;
    const card = D.stockCardByCode(sku);
    const local = state.local;
    local.tab = local.tab || 'overview';
    const locs = empty ? [] : D.stockByLocationFor(card.sku);
    const movements = empty ? [] : D.stockMovementsBySku(card.sku);
    const movementSpark = D.sparkSeries(30, 80, 18, card.sku.charCodeAt(3));
    const stockPct = Math.round((card.onhand / card.maxStock) * 100);
    const stockLevel = card.onhand < card.minStock ? 'low' : 'ok';

    return html`
      <div class="page" data-screen-label="Stok Karti" style="max-width:1480px">
        ${pageHeader({
          crumbs: [
            { label: 'Anasayfa' },
            { label: 'Stok', route: 'inventory' },
            { label: 'Ürün Kartları', route: 'inventory' },
            { label: card.sku, mono: true },
          ],
          titleSlot: html`
            <div style="display:flex;align-items:center;gap:14px;margin-top:4px">
              <div style="width:48px;height:48px;border-radius:12px;background:linear-gradient(135deg, hsl(243 75% 96%), hsl(263 70% 94%));border:1px solid hsl(243 75% 88%);display:grid;place-items:center;color:var(--brand-500);flex-shrink:0">${raw(I.Box(22, 1.6))}</div>
              <div style="min-width:0">
                <div style="display:flex;align-items:center;gap:10px;flex-wrap:wrap">
                  <h1 class="mono" style="margin:0;letter-spacing:-0.01em">${card.sku}</h1>
                  <span style="font-size:22px;font-weight:700;color:var(--text-2);letter-spacing:-0.02em">${card.name}</span>
                  <span class="badge badge-success"><span class="badge-dot"></span>Aktif</span>
                  <span class="badge badge-brand"><span class="badge-dot"></span>ABC: ${card.abc}</span>
                  ${stockLevel === 'low' ? html`<span class="badge badge-danger"><span class="badge-dot"></span>Düşük Stok</span>` : ''}
                </div>
                <p style="margin-top:4px"><span class="mono">Barkod ${card.barcode}</span> · Kategori <span class="text-strong">${card.cat}</span> · Birim <span class="mono">${card.uom}</span> · Son hareket <span class="mono">${D.fmtDateShort(card.lastMovement)}</span></p>
              </div>
            </div>
          `,
          actions: `
            ${btn('Geri', { kind: 'ghost', icon: 'ChevronLeft', action: 'nav', route: 'inventory' })}
            ${btn('Sayım Başlat', { kind: 'secondary', icon: 'ClipDoc' })}
            ${btn('Düzenle', { kind: 'secondary', icon: 'Edit' })}
            ${btn('Stok Hareketi', { kind: 'primary', icon: 'Plus', action: 'nav', route: 'stok/hareket', kbd: 'Alt+N' })}
          `,
        })}

        <div class="kpi-grid" style="grid-template-columns:repeat(5,1fr)">
          ${kpiCard({ label: 'Mevcut Stok', value: empty ? '—' : D.fmtNum(card.onhand), unit: card.uom, glow: 'brand', valueSize: 22, sub: `Min ${card.minStock} · Max ${card.maxStock}` })}
          ${kpiCard({ label: 'Rezerve', value: empty ? '—' : D.fmtNum(card.reserved), unit: card.uom, glow: 'warn', valueSize: 22, sub: 'Açık siparişler' })}
          ${kpiCard({ label: 'Yolda', value: empty ? '—' : D.fmtNum(card.incoming), unit: card.uom, glow: 'success', valueSize: 22, sub: 'Tedarikçide' })}
          ${kpiCard({ label: 'Konum Sayısı', value: empty ? 0 : locs.length, glow: 'brand', valueSize: 22, sub: 'Aktif' })}
          ${kpiCard({ label: 'Devir Hızı', value: empty ? '—' : card.turnoverDays, unit: 'gün', glow: stockLevel === 'low' ? 'danger' : 'success', valueSize: 22, sub: 'Son 90 gün' })}
        </div>

        <div class="card">
          ${tabs([
            { id: 'overview', label: 'Özet', icon: 'Info' },
            { id: 'locations', label: 'Konumlar', icon: 'Warehouse', count: empty ? 0 : locs.length },
            { id: 'movements', label: 'Hareketler', icon: 'Swap', count: empty ? 0 : movements.length },
            { id: 'pricing', label: 'Fiyat & Tedarikçi', icon: 'Tag2' },
            { id: 'spec', label: 'Teknik Özellikler', icon: 'Layers' },
          ], local.tab, 'stok-tab')}

          ${local.tab === 'overview' ? html`
            <div style="padding:18px;display:grid;grid-template-columns:1fr 1fr;gap:18px">
              <div class="card" style="box-shadow:none">
                <div class="card-hdr"><div class="card-title">Stok Seviyesi</div><span class="mono" style="font-size:11px;color:var(--text-3)">%${stockPct} dolu</span></div>
                <div class="card-body" style="padding:16px">
                  <div style="display:flex;align-items:baseline;gap:6px;margin-bottom:4px">
                    <span class="mono" style="font-size:34px;font-weight:800;color:var(--brand-500);letter-spacing:-0.02em">${D.fmtNum(card.onhand)}</span>
                    <span style="font-size:14px;font-weight:600;color:var(--text-3)">${card.uom}</span>
                    <span style="margin-left:auto" class="kpi-delta up">${raw(I.ArrowUp(10, 2.5))}%2.4</span>
                  </div>
                  <div style="position:relative;height:12px;background:var(--bg-2);border-radius:6px;overflow:hidden;margin-top:12px">
                    <div style="position:absolute;inset:0;width:${(card.minStock / card.maxStock) * 100}%;background:hsl(0 84% 60% / 0.18)"></div>
                    <div style="position:absolute;top:0;bottom:0;left:${(card.minStock / card.maxStock) * 100}%;width:2px;background:var(--danger)"></div>
                    <div style="position:absolute;top:0;bottom:0;height:100%;width:${stockPct}%;background:var(--brand-grad);border-radius:6px;box-shadow:0 1px 6px hsl(243 75% 59% / 0.4)"></div>
                  </div>
                  <div class="mono" style="display:flex;justify-content:space-between;margin-top:4px;font-size:10.5px;color:var(--text-4)">
                    <span>0</span><span>Min ${card.minStock}</span><span>Max ${D.fmtNum(card.maxStock)}</span>
                  </div>
                  <div style="margin-top:20px">
                    <div style="display:flex;justify-content:space-between;margin-bottom:4px"><span style="font-size:11px;color:var(--text-3)">Son 30 günlük seyir</span><span class="mono" style="font-size:11px;color:var(--text-3)">${D.fmtNum(card.onhand - 80)} → ${D.fmtNum(card.onhand)}</span></div>
                    ${raw(sparkline(movementSpark, { w: 400, h: 50 }))}
                  </div>
                </div>
              </div>

              <div class="card" style="box-shadow:none">
                <div class="card-hdr"><div class="card-title">Konum Dağılımı</div></div>
                <div class="card-body" style="padding:16px;display:flex;flex-direction:column;gap:8px">
                  ${locs.length === 0 ? '<div style="font-size:12px;color:var(--text-3);padding:16px;text-align:center">Konuma atanmamış</div>' : raw(locs.map((l) => {
                    const total = locs.reduce((a, x) => a + x.qty, 0);
                    const pct = total > 0 ? (l.qty / total) * 100 : 0;
                    return `
                      <div style="display:flex;align-items:center;gap:10px">
                        <span class="mono row-strong" style="width:76px;font-size:12px">${esc(l.code)}</span>
                        <div style="flex:1;height:8px;background:var(--bg-2);border-radius:4px;overflow:hidden"><div style="width:${pct}%;height:100%;background:var(--brand-grad);border-radius:4px"></div></div>
                        <span class="mono" style="width:76px;text-align:right;font-size:12px;font-weight:600">${esc(D.fmtNum(l.qty))} <span style="color:var(--text-4);font-weight:400">${esc(card.uom)}</span></span>
                      </div>
                    `;
                  }).join(''))}
                  <div class="divider" style="margin:6px 0"></div>
                  <div style="display:flex;justify-content:space-between;font-size:12px">
                    <span style="color:var(--text-3)">Toplam Konum Sayısı</span>
                    <span class="mono text-strong">${locs.length} konum · ${D.fmtNum(locs.reduce((a, l) => a + l.qty, 0))} ${card.uom}</span>
                  </div>
                </div>
              </div>

              <div class="card" style="box-shadow:none;grid-column:1 / -1">
                <div class="card-hdr">
                  <div><div class="card-title">Son Hareketler</div><div class="card-sub">Bu SKU için kayıtlı son 6 hareket</div></div>
                  <button class="btn btn-ghost btn-xs" data-action="stok-tab" data-tab="movements">Tümünü Gör${raw(I.ChevronRight(12, 2))}</button>
                </div>
                <div class="card-body-flush">
                  ${movements.length === 0 ? emptyState({ title: 'Henüz hareket yok' }) : html`
                    <table class="data-table">
                      <thead><tr><th>Tarih</th><th>Fiş No</th><th>Tür</th><th>Lokasyon</th><th class="num">Miktar</th><th>Kullanıcı</th></tr></thead>
                      <tbody>
                        ${raw(movements.slice(0, 6).map((m) => `
                          <tr>
                            <td class="mono muted">${esc(D.fmtDateTime(m.time))}</td>
                            <td><span class="mono row-strong" style="color:var(--brand-500)">${esc(m.id)}</span></td>
                            <td>${movementBadge(m.type).__raw}</td>
                            <td class="mono">${esc(m.toLoc || m.fromLoc || '—')}</td>
                            <td class="num"><span class="mono row-strong" style="color:${m.qty > 0 ? 'var(--success-text)' : 'var(--danger-text)'}">${m.qty > 0 ? '+' : ''}${esc(D.fmtNum(m.qty))}</span> <span class="muted">${esc(m.uom)}</span></td>
                            <td><div style="display:flex;align-items:center;gap:7px">${avatar(initials(m.user), { size: 20, fontSize: 9, color: m.userColor }).__raw}<span style="font-size:12px">${esc(m.user)}</span></div></td>
                          </tr>
                        `).join(''))}
                      </tbody>
                    </table>
                  `}
                </div>
              </div>
            </div>
          ` : ''}

          ${local.tab === 'locations' ? html`
            <div class="card-body-flush">
              ${locs.length === 0 ? emptyState({ icon: 'Warehouse', title: 'Konum yok', msg: 'Bu ürün hiçbir konuma atanmamış.' }) : html`
                <table class="data-table">
                  <thead><tr><th>Konum</th><th>Lot No</th><th class="num">Miktar</th><th>SKT</th><th>Durum</th><th style="width:160px"></th></tr></thead>
                  <tbody>
                    ${raw(locs.map((l) => `
                      <tr>
                        <td><span class="mono row-strong" style="color:var(--brand-500)">${esc(l.code)}</span></td>
                        <td><span class="mono muted">${esc(l.lot)}</span></td>
                        <td class="num"><span class="row-strong mono">${esc(D.fmtNum(l.qty))}</span> <span class="muted">${esc(card.uom)}</span></td>
                        <td class="mono">${l.expiry ? esc(D.fmtDateShort(l.expiry)) : '<span class="muted">—</span>'}</td>
                        <td><span class="badge badge-success"><span class="badge-dot"></span>Hazır</span></td>
                        <td><div style="display:flex;gap:4px"><button class="btn btn-secondary btn-xs">Transfer</button><button class="btn btn-secondary btn-xs">Sayım</button></div></td>
                      </tr>
                    `).join(''))}
                  </tbody>
                </table>
              `}
            </div>
          ` : ''}

          ${local.tab === 'movements' ? html`
            ${movements.length === 0 ? emptyState({ icon: 'Swap', title: 'Hareket yok' }) : html`
              <table class="data-table">
                <thead><tr><th>Tarih</th><th>Fiş No</th><th>Tür</th><th>Çıkış / Giriş</th><th class="num">Miktar</th><th>Referans</th><th>Kullanıcı</th><th>Durum</th></tr></thead>
                <tbody>
                  ${raw(movements.map((m) => `
                    <tr>
                      <td class="mono muted">${esc(D.fmtDateTime(m.time))}</td>
                      <td><span class="mono row-strong" style="color:var(--brand-500)">${esc(m.id)}</span></td>
                      <td>${movementBadge(m.type).__raw}</td>
                      <td>
                        <div style="display:flex;align-items:center;gap:6px;font-size:12px">
                          ${m.fromLoc ? `<span class="mono">${esc(m.fromLoc)}</span>` : ''}
                          ${m.fromLoc && m.toLoc ? I.ChevronRight(11, 2) : ''}
                          ${m.toLoc ? `<span class="mono">${esc(m.toLoc)}</span>` : ''}
                          ${!m.fromLoc && !m.toLoc ? '<span class="muted">—</span>' : ''}
                        </div>
                      </td>
                      <td class="num"><span class="mono row-strong" style="color:${m.qty > 0 ? 'var(--success-text)' : 'var(--danger-text)'}">${m.qty > 0 ? '+' : ''}${esc(D.fmtNum(m.qty))}</span> <span class="muted">${esc(m.uom)}</span></td>
                      <td>${m.ref ? `<span class="mono muted">${esc(m.ref)}</span>` : '<span class="muted">—</span>'}</td>
                      <td><div style="display:flex;align-items:center;gap:7px">${avatar(initials(m.user), { size: 22, fontSize: 9, color: m.userColor }).__raw}<span style="font-size:12px">${esc(m.user)}</span></div></td>
                      <td>${m.status === 'posted' ? '<span class="badge badge-success"><span class="badge-dot"></span>İşlendi</span>' : '<span class="badge badge-warn"><span class="badge-dot"></span>Bekliyor</span>'}</td>
                    </tr>
                  `).join(''))}
                </tbody>
              </table>
            `}
          ` : ''}

          ${local.tab === 'pricing' ? html`
            <div style="padding:18px;display:grid;grid-template-columns:1fr 1fr;gap:14px">
              <div class="card" style="box-shadow:none">
                <div class="card-hdr"><div class="card-title">Fiyat Bilgileri</div></div>
                <div class="card-body" style="display:grid;grid-template-columns:1fr 1fr;gap:14px;padding:18px">
                  ${raw([
                    { l: 'Ortalama Maliyet', v: D.fmtTLDec(card.avgCost), s: 'Hareketli ortalama' },
                    { l: 'Son Alış Fiyatı', v: D.fmtTLDec(card.lastCost), s: D.fmtDate(card.lastMovement) },
                    { l: 'Liste Satış Fiyatı', v: D.fmtTLDec(card.salePrice), s: `Marj: %${Math.round(((card.salePrice - card.avgCost) / card.salePrice) * 100)}`, hi: true },
                    { l: 'KDV Oranı', v: `%${card.vat}`, s: 'Standart oran' },
                  ].map((it) => `
                    <div style="padding:14px;background:${it.hi ? 'var(--brand-tint-08)' : 'var(--surface-2)'};border:1px solid ${it.hi ? 'hsl(243 75% 59% / 0.2)' : 'var(--border)'};border-radius:10px">
                      <div class="form-label">${esc(it.l)}</div>
                      <div class="mono" style="font-size:19px;font-weight:700;margin-top:6px;color:${it.hi ? 'var(--brand-500)' : 'var(--text)'}">${esc(it.v)}</div>
                      <div style="font-size:11px;color:var(--text-3);margin-top:2px">${esc(it.s)}</div>
                    </div>
                  `).join(''))}
                </div>
              </div>
              <div class="card" style="box-shadow:none">
                <div class="card-hdr"><div class="card-title">Tedarikçiler</div><button class="btn btn-ghost btn-xs">${I.Plus(12, 2)}Ekle</button></div>
                <div class="card-body-flush">
                  <table class="data-table">
                    <thead><tr><th>Tedarikçi</th><th class="num">Birim Fiyat</th><th>Termin</th><th>Birincil</th></tr></thead>
                    <tbody>
                      ${raw([
                        { id: card.primarySupplier.id, name: card.primarySupplier.name, price: card.lastCost, lead: 7, primary: true },
                        { id: 'KYM', name: 'Kayalar Metal', price: card.lastCost * 1.08, lead: 10 },
                        { id: 'AND', name: 'Anadolu Cıvata Endüstri', price: card.lastCost * 0.96, lead: 14 },
                      ].map((s) => `
                        <tr style="cursor:pointer" data-action="nav" data-route="cari/detail/${esc(s.id)}">
                          <td><div style="display:flex;align-items:center;gap:8px">${avatar(s.id, { size: 24, fontSize: 9.5 }).__raw}<span class="row-strong">${esc(s.name)}</span></div></td>
                          <td class="num"><span class="mono row-strong">${esc(D.fmtTLDec(s.price))}</span></td>
                          <td class="mono"><span class="row-strong">${esc(s.lead)}</span> gün</td>
                          <td>${s.primary ? '<span class="badge badge-brand"><span class="badge-dot"></span>Birincil</span>' : ''}</td>
                        </tr>
                      `).join(''))}
                    </tbody>
                  </table>
                </div>
              </div>
            </div>
          ` : ''}

          ${local.tab === 'spec' ? html`
            <div style="padding:18px;display:grid;grid-template-columns:repeat(3,1fr);gap:14px">
              ${raw([
                { i: 'Hash',     l: 'SKU',             v: card.sku, mono: true },
                { i: 'Tag2',     l: 'Barkod (EAN-13)', v: card.barcode, mono: true },
                { i: 'Layers',   l: 'Kategori',        v: card.cat },
                { i: 'Box',      l: 'Birim',           v: card.uom, mono: true },
                { i: 'Scale',    l: 'Ağırlık (kg)',    v: card.weight, mono: true },
                { i: 'Volume3D', l: 'Hacim (m³)',      v: card.volume, mono: true },
                { i: 'AlertCircle', l: 'Min. Stok',    v: D.fmtNum(card.minStock) + ' ' + card.uom, mono: true },
                { i: 'CheckCircle', l: 'Max. Stok',    v: D.fmtNum(card.maxStock) + ' ' + card.uom, mono: true },
                { i: 'Clock',    l: 'Raf Ömrü',        v: card.shelfLife ? card.shelfLife + ' gün' : 'Sınırsız', mono: !!card.shelfLife },
                { i: 'Star',     l: 'ABC Sınıfı',      v: 'Sınıf ' + card.abc },
                { i: 'Calendar', l: 'Oluşturuldu',     v: D.fmtDate(card.createdAt), mono: true },
                { i: 'Refresh',  l: 'Devir Hızı',      v: card.turnoverDays + ' gün', mono: true },
              ].map((it) => `
                <div style="padding:14px;background:var(--surface-2);border:1px solid var(--border);border-radius:10px;display:flex;align-items:flex-start;gap:10px">
                  <div style="width:30px;height:30px;border-radius:7px;background:var(--brand-tint-08);color:var(--brand-500);display:grid;place-items:center;flex-shrink:0">${I[it.i](14, 2)}</div>
                  <div style="min-width:0">
                    <div class="form-label" style="margin-bottom:2px">${esc(it.l)}</div>
                    <div class="${it.mono ? 'mono' : ''}" style="font-size:13px;font-weight:600;color:var(--text)">${esc(it.v)}</div>
                  </div>
                </div>
              `).join(''))}
            </div>
          ` : ''}
        </div>
      </div>
    `;
  };

  window.SCREENS = window.SCREENS || {};
  Object.assign(window.SCREENS, { WmsScreen, CariList, CariDetail, StokKartiDetail });
})();
