/* OPERAX — Pure JS · Demo veri (Türkçe, gerçekçi)
   Tüm finansal değerler ₺ (TRY) cinsindendir */
(function () {

  const fmtTL = (n) => new Intl.NumberFormat('tr-TR', { style: 'currency', currency: 'TRY', maximumFractionDigits: 0 }).format(n);
  const fmtTLDec = (n) => new Intl.NumberFormat('tr-TR', { style: 'currency', currency: 'TRY', minimumFractionDigits: 2 }).format(n);
  const fmtNum = (n, d = 0) => new Intl.NumberFormat('tr-TR', { minimumFractionDigits: d, maximumFractionDigits: d }).format(n);
  const fmtDate = (s) => new Date(s).toLocaleDateString('tr-TR', { day: '2-digit', month: 'short', year: 'numeric' });
  const fmtDateShort = (s) => new Date(s).toLocaleDateString('tr-TR', { day: '2-digit', month: '2-digit', year: '2-digit' });
  const fmtDateTime = (s) => {
    const d = new Date(s);
    return d.toLocaleDateString('tr-TR', { day: '2-digit', month: 'short' }) + ' · ' + d.toLocaleTimeString('tr-TR', { hour: '2-digit', minute: '2-digit' });
  };
  const relTime = (s) => {
    const diff = (Date.now() - new Date(s).getTime()) / 1000;
    if (diff < 60) return 'az önce';
    if (diff < 3600) return `${Math.floor(diff / 60)} dk önce`;
    if (diff < 86400) return `${Math.floor(diff / 3600)} sa önce`;
    return `${Math.floor(diff / 86400)} gün önce`;
  };

  const suppliers = [
    { id: 'TKB', name: 'Türkbasınç Pnömatik A.Ş.', city: 'İstanbul', tax: '4720394102' },
    { id: 'KYM', name: 'Kayalar Metal San. Tic. Ltd. Şti.', city: 'Kocaeli', tax: '5610293841' },
    { id: 'ASE', name: 'Aselsan Elektronik Tedarik', city: 'Ankara', tax: '0890172634' },
    { id: 'DOK', name: 'Doku Tekstil İhracat', city: 'Bursa', tax: '3210984720' },
    { id: 'GMS', name: 'Gümüş Lojistik & Depo', city: 'İzmir', tax: '4101892736' },
    { id: 'AND', name: 'Anadolu Cıvata Endüstri', city: 'Konya', tax: '0673021984' },
    { id: 'EFE', name: 'Efes Ambalaj San.', city: 'Manisa', tax: '3290847102' },
    { id: 'BSR', name: 'Boğaziçi Reçine Kimya', city: 'İstanbul', tax: '1740293817' },
  ];

  const products = [
    { sku: 'PR-0103', name: 'Hidrolik Silindir 80mm', uom: 'AD', cat: 'Pnömatik' },
    { sku: 'PR-0207', name: 'Paslanmaz Civata M8x40', uom: 'AD', cat: 'Bağlantı' },
    { sku: 'PR-0341', name: 'Elektrik Motoru 3kW IE3', uom: 'AD', cat: 'Elektrik' },
    { sku: 'PR-0518', name: 'Polyester Kumaş 220gr', uom: 'MT', cat: 'Tekstil' },
    { sku: 'PR-0612', name: 'Termoplastik Reçine PA66', uom: 'KG', cat: 'Kimya' },
    { sku: 'PR-0724', name: 'Karton Ambalaj Kutusu 40x30', uom: 'AD', cat: 'Ambalaj' },
    { sku: 'PR-0805', name: 'PLC Modülü Siemens S7-1200', uom: 'AD', cat: 'Otomasyon' },
    { sku: 'PR-0901', name: 'Rulman SKF 6204-2RS', uom: 'AD', cat: 'Bağlantı' },
  ];

  const POs = [
    { no: 'PO-2026-00041', supplier: 'TKB', date: '2026-05-22', due: '2026-06-05', total: 184250.50, status: 'Posted', items: 6 },
    { no: 'PO-2026-00040', supplier: 'KYM', date: '2026-05-21', due: '2026-05-31', total: 92340.00, status: 'Posted', items: 12 },
    { no: 'PO-2026-00039', supplier: 'ASE', date: '2026-05-20', due: '2026-06-10', total: 612900.00, status: 'Draft',  items: 4 },
    { no: 'PO-2026-00038', supplier: 'DOK', date: '2026-05-19', due: '2026-05-29', total: 47820.75,  status: 'Posted', items: 8 },
    { no: 'PO-2026-00037', supplier: 'GMS', date: '2026-05-18', due: '2026-05-28', total: 21450.00,  status: 'Cancelled', items: 2 },
    { no: 'PO-2026-00036', supplier: 'AND', date: '2026-05-17', due: '2026-05-30', total: 138900.00, status: 'Posted', items: 14 },
    { no: 'PO-2026-00035', supplier: 'EFE', date: '2026-05-16', due: '2026-05-26', total: 8920.30,   status: 'Draft',  items: 3 },
    { no: 'PO-2026-00034', supplier: 'BSR', date: '2026-05-15', due: '2026-06-15', total: 274500.00, status: 'Posted', items: 7 },
    { no: 'PO-2026-00033', supplier: 'TKB', date: '2026-05-14', due: '2026-05-24', total: 56200.00,  status: 'Posted', items: 5 },
    { no: 'PO-2026-00032', supplier: 'KYM', date: '2026-05-13', due: '2026-05-23', total: 19800.00,  status: 'Cancelled', items: 4 },
  ];

  const supplierByCode = (code) => suppliers.find((s) => s.id === code) || { name: code, city: '—', tax: '—' };

  const POLines = [
    { line: 1, sku: 'PR-0103', name: 'Hidrolik Silindir 80mm',    uom: 'AD', qty: 48,   unitPrice: 1840.00, vat: 20 },
    { line: 2, sku: 'PR-0207', name: 'Paslanmaz Civata M8x40',    uom: 'AD', qty: 2400, unitPrice: 2.85,    vat: 20 },
    { line: 3, sku: 'PR-0901', name: 'Rulman SKF 6204-2RS',       uom: 'AD', qty: 120,  unitPrice: 412.30,  vat: 20 },
    { line: 4, sku: 'PR-0612', name: 'Termoplastik Reçine PA66',  uom: 'KG', qty: 800,  unitPrice: 92.40,   vat: 20 },
    { line: 5, sku: 'PR-0724', name: 'Karton Ambalaj Kutusu',     uom: 'AD', qty: 1500, unitPrice: 18.20,   vat: 20 },
    { line: 6, sku: 'PR-0518', name: 'Polyester Kumaş 220gr',     uom: 'MT', qty: 320,  unitPrice: 124.50,  vat: 20 },
  ];

  const activity = [
    { who: 'Mehmet Yılmaz', avatar: 'MY', action: 'PO-2026-00041 evrakını onayladı', target: 'PO-2026-00041', kind: 'success', time: '2026-05-27T14:22:00' },
    { who: 'Ayşe Demir', avatar: 'AD', action: 'LPN-00482 sevkıyatı oluşturdu', target: 'LPN-00482', kind: 'info', time: '2026-05-27T13:55:00' },
    { who: 'Sistem', avatar: 'SS', action: 'Düşük stok uyarısı: PR-0612 Reçine PA66', target: 'PR-0612', kind: 'warn', time: '2026-05-27T13:18:00' },
    { who: 'Burak Aksoy', avatar: 'BA', action: 'PO-2026-00037 evrakını iptal etti', target: 'PO-2026-00037', kind: 'danger', time: '2026-05-27T11:42:00' },
    { who: 'Selin Çelik', avatar: 'SÇ', action: 'A-12-04 konumuna 240 AD putaway tamamlandı', target: 'A-12-04', kind: 'success', time: '2026-05-27T10:30:00' },
    { who: 'Mehmet Yılmaz', avatar: 'MY', action: 'PO-2026-00039 taslağı kaydedildi', target: 'PO-2026-00039', kind: 'neutral', time: '2026-05-27T09:50:00' },
    { who: 'Ayşe Demir', avatar: 'AD', action: 'Sayım farkı raporu yayınlandı (Q2)', target: 'Rapor', kind: 'info', time: '2026-05-27T08:15:00' },
  ];

  const lowStock = [
    { sku: 'PR-0612', name: 'Reçine PA66', onhand: 142, min: 800, uom: 'KG' },
    { sku: 'PR-0207', name: 'Civata M8x40', onhand: 1240, min: 4000, uom: 'AD' },
    { sku: 'PR-0901', name: 'Rulman 6204-2RS', onhand: 38, min: 120, uom: 'AD' },
    { sku: 'PR-0518', name: 'Polyester 220gr', onhand: 215, min: 500, uom: 'MT' },
  ];

  const incoming = [
    { lpn: 'LPN-00488', supplier: 'TKB', sku: 'PR-0103', qty: 120,  eta: '2026-05-28', dock: 'D-02' },
    { lpn: 'LPN-00489', supplier: 'KYM', sku: 'PR-0207', qty: 5000, eta: '2026-05-28', dock: 'D-01' },
    { lpn: 'LPN-00490', supplier: 'ASE', sku: 'PR-0805', qty: 24,   eta: '2026-05-29', dock: 'D-03' },
    { lpn: 'LPN-00491', supplier: 'BSR', sku: 'PR-0612', qty: 800,  eta: '2026-05-30', dock: 'D-02' },
  ];

  const locations = (() => {
    const out = [];
    const zones = ['A', 'B', 'C'];
    const aisles = [11, 12, 13, 14, 15];
    zones.forEach((z) => {
      aisles.forEach((a) => {
        for (let r = 1; r <= 4; r++) {
          const fill = Math.round(Math.max(0, Math.min(100, 30 + 40 * Math.sin(z.charCodeAt(0) + a * r) + (r % 2 === 0 ? 25 : -10))));
          out.push({ code: `${z}-${a}-${String(r).padStart(2, '0')}`, zone: z, aisle: a, rack: r, fillPct: fill,
            sku: products[(a + r) % products.length].sku, uom: products[(a + r) % products.length].uom });
        }
      });
    });
    return out;
  })();

  const users = [
    { name: 'Mehmet Yılmaz', email: 'mehmet.yilmaz@operax.com.tr', role: 'Yönetici', dept: 'Satınalma', status: 'active', last: '2026-05-27T14:30:00', avatar: 'MY', color: 'hsl(243 75% 59%)' },
    { name: 'Ayşe Demir', email: 'ayse.demir@operax.com.tr', role: 'Operatör', dept: 'Depo', status: 'active', last: '2026-05-27T13:10:00', avatar: 'AD', color: 'hsl(160 84% 39%)' },
    { name: 'Burak Aksoy', email: 'burak.aksoy@operax.com.tr', role: 'Yönetici', dept: 'Muhasebe', status: 'active', last: '2026-05-27T11:55:00', avatar: 'BA', color: 'hsl(0 84% 60%)' },
    { name: 'Selin Çelik', email: 'selin.celik@operax.com.tr', role: 'Operatör', dept: 'Depo', status: 'active', last: '2026-05-27T10:45:00', avatar: 'SÇ', color: 'hsl(38 92% 50%)' },
    { name: 'Cem Aydın', email: 'cem.aydin@operax.com.tr', role: 'Görüntüleyici', dept: 'Yönetim', status: 'inactive', last: '2026-05-19T09:25:00', avatar: 'CA', color: 'hsl(263 70% 55%)' },
    { name: 'Deniz Şahin', email: 'deniz.sahin@operax.com.tr', role: 'Operatör', dept: 'Satınalma', status: 'pending', last: null, avatar: 'DŞ', color: 'hsl(195 80% 50%)' },
  ];

  const sparkSeries = (n, base, variance, seed = 1) => {
    const out = [];
    let x = base;
    for (let i = 0; i < n; i++) {
      x += (Math.sin(i * 1.7 + seed) + Math.cos(i * 0.7 + seed)) * variance;
      out.push(Math.max(0, Math.round(x)));
    }
    return out;
  };

  // ---------- Cari hesaplar ----------
  const customers = [
    { id: 'BRA', name: 'Brand Aydınlatma Tic. A.Ş.', city: 'İstanbul', tax: '8120374619' },
    { id: 'ZNT', name: 'Zenit Makina San. Ltd. Şti.', city: 'Bursa',    tax: '9920483715' },
    { id: 'ANK', name: 'Ankara Tesis Yönetim', city: 'Ankara',   tax: '7301829374' },
  ];

  const cariList = (() => {
    const out = [];
    suppliers.forEach((s, i) => {
      const bal = -1 * (45000 + (i * 38000) + ((s.id.charCodeAt(0) * 977) % 90000));
      out.push({
        id: s.id, code: s.id, name: s.name, type: 'supplier', city: s.city, tax: s.tax, balance: bal,
        paymentTerm: [15, 30, 45, 60][i % 4], creditLimit: 500000 + ((i * 100000) % 400000),
        overdueDays: i === 2 ? 12 : (i === 5 ? 6 : 0),
        contactPerson: ['Murat Erol', 'Hülya Şensoy', 'Ahmet Karaca', 'Fatma Çelik', 'Onur Akbaş', 'Zeynep Kara', 'Cenk Türk', 'Ela Yıldız'][i],
        contactEmail: `iletisim@${s.id.toLowerCase()}.com.tr`,
        contactPhone: `+90 (${[212, 262, 312, 224, 232, 332, 236, 212][i]}) ${300 + i * 17} ${10 + i * 3} ${20 + i}${4 + i}`,
        iban: `TR${String(33 + i * 7).padStart(2,'0')} ${String(6201 + i * 13).slice(0,4)} ${String(2917 + i * 17).slice(0,4)} ${String(8472 + i * 19).slice(0,4)} ${String(1029 + i * 23).slice(0,4)} 84`,
        bank: ['Garanti BBVA', 'İş Bankası', 'Yapı Kredi', 'Akbank', 'Ziraat Bankası', 'QNB Finansbank', 'DenizBank', 'TEB'][i],
        address: `${['Levent', 'Maslak', 'Kadıköy', 'Beşiktaş', 'Şişli', 'Ataşehir', 'Bakırköy', 'Üsküdar'][i]} Mah. ${['Büyükdere', 'Bağdat', 'Atatürk', 'İstiklal'][i % 4]} Cd. No: ${42 + i * 17}, ${s.city}`,
        since: `202${0 + (i % 5)}-${String(1 + (i % 12)).padStart(2,'0')}-15`,
        rating: [4.8, 4.6, 4.9, 4.3, 4.0, 4.7, 3.9, 4.5][i],
      });
    });
    customers.forEach((c, i) => {
      const bal = (38000 + i * 52000);
      out.push({
        id: c.id, code: c.id, name: c.name, type: 'customer', city: c.city, tax: c.tax, balance: bal,
        paymentTerm: [30, 45, 60][i % 3], creditLimit: 800000 + i * 200000,
        overdueDays: i === 1 ? 18 : 0,
        contactPerson: ['Eren Bulut', 'Pınar Aksoy', 'Selçuk Demir'][i],
        contactEmail: `satinalma@${c.id.toLowerCase()}.com.tr`,
        contactPhone: `+90 (${[212, 224, 312][i]}) ${410 + i * 23} ${20 + i * 7} ${30 + i}${8 + i}`,
        iban: `TR${String(58 + i * 11).padStart(2,'0')} ${String(6701 + i * 29).slice(0,4)} ${String(3017 + i * 31).slice(0,4)} ${String(9472 + i * 37).slice(0,4)} ${String(2029 + i * 41).slice(0,4)} 71`,
        bank: ['Garanti BBVA', 'İş Bankası', 'Yapı Kredi'][i],
        address: `${['Levent', 'Nilüfer', 'Çankaya'][i]} Mah. ${['İnönü', 'Cumhuriyet', 'Gazi'][i]} Cd. No: ${108 + i * 19}, ${c.city}`,
        since: `202${1 + i}-${String(3 + i * 2).padStart(2,'0')}-08`,
        rating: [4.7, 4.4, 4.9][i],
      });
    });
    return out;
  })();

  const cariByCode = (code) => cariList.find((c) => c.code === code) || cariList[0];

  const cariMovementsFor = (cari) => {
    const out = [];
    let bal = cari.balance + (cari.type === 'supplier' ? 250000 : -180000);
    const isSupplier = cari.type === 'supplier';
    const seed = cari.code.charCodeAt(0) + cari.code.charCodeAt(1);
    for (let i = 0; i < 14; i++) {
      const day = 27 - i * 2;
      const month = day < 1 ? 4 : 5;
      const realDay = day < 1 ? 30 + day : day;
      const date = `2026-${String(month).padStart(2,'0')}-${String(realDay).padStart(2,'0')}`;
      const t = (seed * (i + 3)) % 4;
      let kind, doc, debit = 0, credit = 0, desc;
      if (t === 0) {
        kind = isSupplier ? 'Alış Faturası' : 'Satış Faturası';
        doc = isSupplier ? `AF-2026-${String(401 + i * 11 % 200).padStart(4, '0')}` : `SF-2026-${String(80 + i * 7).padStart(4, '0')}`;
        const v = 4500 + ((seed * (i + 1)) % 78000);
        if (isSupplier) { credit = v; bal -= v; } else { debit = v; bal += v; }
        desc = isSupplier ? 'Tedarikçi faturası' : 'Müşteri faturası';
      } else if (t === 1) {
        kind = isSupplier ? 'Ödeme' : 'Tahsilat';
        doc = isSupplier ? `OD-2026-${String(120 + i * 5).padStart(4, '0')}` : `TH-2026-${String(94 + i * 6).padStart(4, '0')}`;
        const v = 18000 + ((seed * (i + 2)) % 92000);
        if (isSupplier) { debit = v; bal += v; } else { credit = v; bal -= v; }
        desc = (isSupplier ? 'Banka havalesi · ' : 'Banka tahsilatı · ') + cari.bank;
      } else if (t === 2) {
        kind = 'İade Faturası';
        doc = `IF-2026-${String(20 + i).padStart(4, '0')}`;
        const v = 1200 + ((seed * i) % 12000);
        if (isSupplier) { debit = v; bal += v; } else { credit = v; bal -= v; }
        desc = 'Hatalı parti iadesi';
      } else {
        kind = 'Dekont';
        doc = `DK-2026-${String(80 + i * 2).padStart(4, '0')}`;
        const v = 300 + ((seed * (i + 5)) % 4800);
        if (i % 2 === 0) { debit = v; bal += v; } else { credit = v; bal -= v; }
        desc = 'Vade farkı düzeltmesi';
      }
      out.push({ date, kind, doc, desc, debit, credit, balance: bal });
    }
    return out;
  };

  // ---------- Stok kart enriched ----------
  const stockCardByCode = (sku) => {
    const p = products.find((x) => x.sku === sku) || products[0];
    const seed = p.sku.charCodeAt(3) + p.sku.charCodeAt(4);
    return {
      sku: p.sku, name: p.name, uom: p.uom, cat: p.cat,
      barcode: '868' + String(seed * 9311).padStart(10, '0').slice(-10),
      onhand: 240 + ((seed * 73) % 4200),
      reserved: 30 + ((seed * 11) % 280),
      incoming: 120 + ((seed * 13) % 600),
      minStock: 200, maxStock: 5000,
      avgCost: 84.60 + (seed % 380), lastCost: 92.40 + (seed % 410),
      salePrice: 132.80 + (seed % 540), vat: 20,
      weight: (0.4 + (seed % 60) / 10).toFixed(2),
      volume: (0.012 + (seed % 30) / 1000).toFixed(3),
      primarySupplier: suppliers[(seed % suppliers.length)],
      isActive: true, abc: ['A', 'A', 'B', 'C', 'A', 'B'][seed % 6],
      shelfLife: [365, 730, 540, null, 90, null][seed % 6],
      createdAt: '2024-08-12', lastMovement: '2026-05-27', turnoverDays: 24 + (seed % 18),
    };
  };

  const stockByLocationFor = (sku) => {
    const seed = sku.charCodeAt(3) + sku.charCodeAt(5);
    const out = [];
    const zones = ['A', 'B', 'C'];
    for (let i = 0; i < 4; i++) {
      const z = zones[(seed + i) % 3];
      const a = 11 + ((seed + i * 7) % 5);
      const r = 1 + ((seed + i * 3) % 4);
      out.push({
        code: `${z}-${a}-${String(r).padStart(2, '0')}`,
        qty: 30 + ((seed * (i + 1) * 17) % 480),
        lot: `LOT-${String(seed + i * 11).padStart(5, '0')}`,
        expiry: i === 0 ? '2026-12-15' : i === 1 ? '2027-03-08' : null,
      });
    }
    return out;
  };

  const stockMovements = (() => {
    const out = [];
    const types = [
      { k: 'in',  label: 'Mal Kabul',    prefix: 'MK' },
      { k: 'out', label: 'Sevkiyat',     prefix: 'SV' },
      { k: 'tr',  label: 'Transfer',     prefix: 'TR' },
      { k: 'cnt', label: 'Sayım Farkı',  prefix: 'CT' },
      { k: 'adj', label: 'Düzeltme',     prefix: 'AD' },
      { k: 'ret', label: 'İade',         prefix: 'IA' },
    ];
    const userPool = ['Mehmet Yılmaz', 'Ayşe Demir', 'Selin Çelik', 'Burak Aksoy', 'Sistem'];
    const userColors = ['hsl(243 75% 59%)', 'hsl(160 84% 39%)', 'hsl(38 92% 50%)', 'hsl(0 84% 60%)', 'hsl(215 16% 47%)'];
    for (let i = 0; i < 28; i++) {
      const t = types[i % types.length];
      const p = products[(i * 3) % products.length];
      const sup = suppliers[(i * 5) % suppliers.length];
      const ui = (i * 7) % userPool.length;
      const day = 27 - Math.floor(i / 2);
      const month = day < 1 ? 4 : 5;
      const realDay = day < 1 ? 30 + day : day;
      const hour = String(8 + (i * 3) % 14).padStart(2, '0');
      const min = String((i * 13) % 60).padStart(2, '0');
      const baseQty = 20 + ((i * 41) % 800);
      out.push({
        id: `${t.prefix}-2026-${String(2480 - i * 7).padStart(5, '0')}`,
        type: t, sku: p.sku, name: p.name, uom: p.uom,
        qty: t.k === 'out' || t.k === 'ret' || (t.k === 'cnt' && i % 3 === 0) ? -baseQty : baseQty,
        fromLoc: t.k === 'tr' || t.k === 'out' ? `A-${11 + i % 5}-0${1 + i % 4}` : null,
        toLoc: t.k === 'tr' || t.k === 'in' ? `B-${12 + i % 4}-0${1 + i % 4}` : (t.k === 'cnt' || t.k === 'adj' ? `A-${11 + i % 5}-0${1 + i % 4}` : null),
        ref: t.k === 'in' ? `LPN-${String(488 + i).padStart(5, '0')}` : t.k === 'out' ? `SO-2026-${String(120 + i).padStart(5, '0')}` : null,
        supplier: t.k === 'in' ? sup.id : null,
        user: userPool[ui], userColor: userColors[ui],
        time: `2026-${String(month).padStart(2,'0')}-${String(realDay).padStart(2,'0')}T${hour}:${min}:00`,
        status: i % 11 === 0 ? 'pending' : 'posted',
      });
    }
    return out;
  })();

  const stockMovementsBySku = (sku) => stockMovements.filter((m) => m.sku === sku);

  // ---------- Sales orders ----------
  const salesOrders = (() => {
    const out = [];
    const cust = ['BRA', 'ZNT', 'ANK'];
    const statuses = ['Posted', 'Posted', 'Draft', 'Posted', 'Cancelled', 'Posted', 'Posted', 'Draft', 'Posted', 'Posted', 'Cancelled', 'Posted'];
    const ship = ['shipped', 'partial', 'pending', 'shipped', 'cancelled', 'partial', 'shipped', 'pending', 'shipped', 'shipped', 'cancelled', 'pending'];
    for (let i = 0; i < 12; i++) {
      const day = 27 - i * 2;
      const month = day < 1 ? 4 : 5;
      const realDay = day < 1 ? 30 + day : day;
      out.push({
        no: `SO-2026-${String(128 - i).padStart(5, '0')}`,
        customer: cust[i % cust.length],
        date: `2026-${String(month).padStart(2,'0')}-${String(realDay).padStart(2,'0')}`,
        due: `2026-06-${String(5 + (i * 3) % 22).padStart(2,'0')}`,
        total: 38000 + ((i * 17841) % 480000),
        status: statuses[i], shipStatus: ship[i],
        items: 3 + (i % 9),
      });
    }
    return out;
  })();

  const SOLines = [
    { line: 1, sku: 'PR-0341', name: 'Elektrik Motoru 3kW IE3',     uom: 'AD', qty: 12,  unitPrice: 4280.00, vat: 20, shipped: 12 },
    { line: 2, sku: 'PR-0805', name: 'PLC Modülü Siemens S7-1200',  uom: 'AD', qty: 6,   unitPrice: 9420.00, vat: 20, shipped: 6 },
    { line: 3, sku: 'PR-0901', name: 'Rulman SKF 6204-2RS',         uom: 'AD', qty: 200, unitPrice: 412.30,  vat: 20, shipped: 120 },
    { line: 4, sku: 'PR-0103', name: 'Hidrolik Silindir 80mm',      uom: 'AD', qty: 24,  unitPrice: 2180.00, vat: 20, shipped: 24 },
    { line: 5, sku: 'PR-0612', name: 'Termoplastik Reçine PA66',    uom: 'KG', qty: 400, unitPrice: 124.00,  vat: 20, shipped: 0 },
  ];

  // ---------- Üretim ----------
  const workOrders = (() => {
    const out = [];
    const products_prod = [
      { sku: 'WO-MTR-001', name: 'Motorlu Pompa Modülü 5HP', cat: 'Montaj' },
      { sku: 'WO-PNL-002', name: 'Kontrol Paneli 3-Fazlı', cat: 'Elektrik' },
      { sku: 'WO-VLV-003', name: 'Hidrolik Valf Bloğu 6/2', cat: 'Hidrolik' },
      { sku: 'WO-CHS-004', name: 'Şasi Komplesi 1200x800', cat: 'Mekanik' },
      { sku: 'WO-PCB-005', name: 'PCB Test Düzeneği', cat: 'Elektrik' },
    ];
    const states = ['Released', 'InProgress', 'InProgress', 'Completed', 'Released', 'Planned', 'InProgress', 'Completed', 'Cancelled', 'Released', 'Planned'];
    for (let i = 0; i < 11; i++) {
      const p = products_prod[i % products_prod.length];
      const planned = 100 + (i * 27) % 580;
      const produced = states[i] === 'Completed' ? planned : (states[i] === 'InProgress' ? Math.floor(planned * (0.3 + (i % 5) * 0.12)) : 0);
      out.push({
        no: `WO-2026-${String(248 - i * 3).padStart(4, '0')}`,
        product: p.sku, productName: p.name, cat: p.cat,
        planned, produced, status: states[i],
        line: ['L-01', 'L-02', 'L-03', 'L-01', 'L-04'][i % 5],
        priority: ['high', 'normal', 'urgent', 'normal', 'low'][i % 5],
        start: `2026-05-${String(20 - i % 10).padStart(2,'0')}`,
        due: `2026-06-${String(5 + i % 18).padStart(2,'0')}`,
        operator: ['Hakan Yıldırım', 'Selin Çelik', 'Burak Aksoy', 'Aslı Demirci', 'Onur Erdem'][i % 5],
        operatorColor: ['hsl(243 75% 59%)', 'hsl(38 92% 50%)', 'hsl(0 84% 60%)', 'hsl(160 84% 39%)', 'hsl(195 80% 50%)'][i % 5],
      });
    }
    return out;
  })();

  const workOrderByNo = (no) => workOrders.find((w) => w.no === no) || workOrders[0];

  const bomFor = (woNo) => {
    const w = workOrderByNo(woNo);
    return [
      { sku: 'PR-0341', name: 'Elektrik Motoru 3kW IE3',     uom: 'AD', qty: 1,    scrap: 0,    consumed: w.produced > 0 ? 1 : 0 },
      { sku: 'PR-0805', name: 'PLC Modülü Siemens S7-1200',  uom: 'AD', qty: 1,    scrap: 0,    consumed: w.produced > 0 ? 1 : 0 },
      { sku: 'PR-0103', name: 'Hidrolik Silindir 80mm',      uom: 'AD', qty: 2,    scrap: 0.02, consumed: w.produced * 2 },
      { sku: 'PR-0207', name: 'Paslanmaz Civata M8x40',      uom: 'AD', qty: 24,   scrap: 0.05, consumed: w.produced * 24 },
      { sku: 'PR-0901', name: 'Rulman SKF 6204-2RS',         uom: 'AD', qty: 4,    scrap: 0,    consumed: w.produced * 4 },
      { sku: 'PR-0612', name: 'Termoplastik Reçine PA66',    uom: 'KG', qty: 0.8,  scrap: 0.10, consumed: w.produced * 0.8 },
    ];
  };

  const routingFor = () => [
    { op: 10, name: 'Hammadde Kabul',  station: 'STA-IN-01',  time: 8,  status: 'done',    operator: 'Selin Çelik' },
    { op: 20, name: 'Mekanik Montaj',  station: 'STA-MNT-02', time: 24, status: 'done',    operator: 'Hakan Yıldırım' },
    { op: 30, name: 'Elektrik Montaj', station: 'STA-EL-01',  time: 18, status: 'active',  operator: 'Aslı Demirci' },
    { op: 40, name: 'Fonksiyon Testi', station: 'STA-QC-03',  time: 12, status: 'pending', operator: '—' },
    { op: 50, name: 'Ambalaj',         station: 'STA-PKG-01', time: 6,  status: 'pending', operator: '—' },
  ];

  // ---------- Muhasebe ----------
  const journalEntries = (() => {
    const out = [];
    const types = [
      { k: 'AF', label: 'Alış Faturası',  desc: 'Tedarikçi faturası kaydı' },
      { k: 'SF', label: 'Satış Faturası', desc: 'Müşteri faturası kaydı' },
      { k: 'BN', label: 'Banka Dekontu',  desc: 'Banka havalesi' },
      { k: 'KS', label: 'Kasa Tahsilatı', desc: 'Nakit tahsilat' },
      { k: 'IS', label: 'Maaş Tahakkuku', desc: 'Personel maaş tahakkuku' },
      { k: 'AM', label: 'Amortisman',     desc: 'Demirbaş amortisman' },
      { k: 'KP', label: 'Kapanış',         desc: 'Dönem sonu kapanış kaydı' },
      { k: 'TR', label: 'Transfer',        desc: 'Hesap virmanı' },
    ];
    const statuses = ['posted', 'posted', 'posted', 'draft', 'posted', 'posted', 'posted', 'cancelled', 'posted', 'draft', 'posted', 'posted', 'posted', 'posted'];
    for (let i = 0; i < 14; i++) {
      const t = types[i % types.length];
      const day = 27 - i * 2;
      const month = day < 1 ? 4 : 5;
      const realDay = day < 1 ? 30 + day : day;
      out.push({
        no: `${t.k}-2026-${String(940 - i * 7).padStart(5, '0')}`,
        type: t, date: `2026-${String(month).padStart(2,'0')}-${String(realDay).padStart(2,'0')}`,
        desc: t.desc, total: 4800 + ((i * 8741) % 380000), status: statuses[i],
        user: ['Burak Aksoy', 'Mehmet Yılmaz', 'Sistem', 'Ayşe Demir'][i % 4],
        userColor: ['hsl(0 84% 60%)', 'hsl(243 75% 59%)', 'hsl(215 16% 47%)', 'hsl(160 84% 39%)'][i % 4],
      });
    }
    return out;
  })();

  const journalLinesFor = () => [
    { line: 1, account: '120.01.001', accountName: 'Yurtiçi Alıcılar · Brand Aydınlatma', debit: 184250.50, credit: 0,         desc: 'Müşteri faturası — SF-2026-00128' },
    { line: 2, account: '391.01',     accountName: 'Hesaplanan KDV %20',                  debit: 0,         credit: 30708.42,  desc: 'KDV (%20)' },
    { line: 3, account: '600.01.001', accountName: 'Yurtiçi Satışlar · Mamul',            debit: 0,         credit: 153542.08, desc: 'Mamul satışı' },
  ];

  const cashBankAccounts = [
    { id: 'KASA-TL',     name: 'Merkez Kasa · TRY',                  type: 'cash', bal: 184240.50,  currency: 'TL' },
    { id: 'BANK-GAR-TL', name: 'Garanti BBVA · TR12...8472 · TRY',   type: 'bank', bal: 4280410.85, currency: 'TL' },
    { id: 'BANK-IS-USD', name: 'İş Bankası · TR45...2017 · USD',     type: 'bank', bal: 84120.00,   currency: 'USD' },
    { id: 'BANK-YK-EUR', name: 'Yapı Kredi · TR89...7710 · EUR',     type: 'bank', bal: 32180.00,   currency: 'EUR' },
  ];

  const cashBankMovements = (() => {
    const out = [];
    for (let i = 0; i < 16; i++) {
      const a = cashBankAccounts[i % cashBankAccounts.length];
      const isOut = i % 2 === 0;
      const day = 27 - Math.floor(i / 2);
      const month = day < 1 ? 4 : 5;
      const realDay = day < 1 ? 30 + day : day;
      out.push({
        id: `KB-2026-${String(1480 - i * 9).padStart(5, '0')}`,
        account: a.id, accountName: a.name, accountType: a.type, currency: a.currency,
        kind: isOut ? 'out' : 'in',
        kindLabel: isOut ? 'Ödeme' : 'Tahsilat',
        date: `2026-${String(month).padStart(2,'0')}-${String(realDay).padStart(2,'0')}`,
        counterparty: ['Türkbasınç Pnömatik', 'Brand Aydınlatma', 'Kayalar Metal', 'Garanti BBVA · Komisyon', 'Personel Bordrosu', 'Aselsan Elektronik', 'Zenit Makina'][i % 7],
        desc: isOut ? 'Tedarikçi ödemesi' : 'Müşteri tahsilatı',
        amount: 4800 + ((i * 7841) % 280000),
        user: 'Burak Aksoy',
      });
    }
    return out;
  })();

  window.OPX = {
    fmtTL, fmtTLDec, fmtNum, fmtDate, fmtDateShort, fmtDateTime, relTime,
    suppliers, supplierByCode, products,
    POs, POLines, activity, lowStock, incoming, locations, users, sparkSeries,
    customers, cariList, cariByCode, cariMovementsFor,
    stockCardByCode, stockByLocationFor, stockMovements, stockMovementsBySku,
    salesOrders, SOLines, workOrders, workOrderByNo, bomFor, routingFor,
    journalEntries, journalLinesFor, cashBankAccounts, cashBankMovements,
  };
})();
