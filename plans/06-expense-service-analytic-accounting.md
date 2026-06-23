# Plan 06 — Gider/Hizmet Akışı + Maliyet Merkezi Muhasebesi

**Tarih:** 2026-05-29
**Yazan:** Claude
**Durum:** `Taslak`
**Modül:** M02 + M03 + M04 + Expenses + yeni M-CostCenter
**Paket:** STARTER (gider/hizmet) + ENTERPRISE (maliyet dağıtımı)

> İsimlendirme: Odoo "Analytic Accounting" kavramının Operax-özgü Türkçe karşılığı.
> Eşdeğer: SAP CO Cost Center + CO-PA, IAS Canias boyut yapısı.

---

## 1. Problem

İki bağlantılı eksik:

> ⛔ **REVİZE (Plan 52, 2026-06-23 — iki uzman + TDHP):** Aşağıdaki "EXPENSE item-tipi" yaklaşımı **REDDEDİLDİ.** Saf gider (kira/elektrik/yakıt) bir "item" DEĞİLDİR (UOM/barkod/raf/BOM anlamsız; Mikro da gideri ayrı tabloda tutar) → mevcut **ExpenseInvoice + ExpenseType + CostCenter** taksonomisinde yaşar, Item'a taşınmaz. **ItemType = STOCK / SERVICE / FIXED_ASSET** (CONSUMABLE ve EXPENSE yok). Streç film gibi çok-amaçlı mal tek STOCK item'dır; satış/üretim/sarf/gider EVRAKLA belirlenir (iki-eksen modeli). **SERVICE stok-atlama guard'ı Plan 52 Faz 2'de YAPILDI** (sp_ReceivingPost/ShippingPost). Bu planın geri kalanı (CostDimension/CostAllocation maliyet-merkezi kısmı — B bölümü) hâlâ geçerli.

**A. Hizmet/gider alım-satım akışı (ItemType yönlendirmeli):** `Item.ItemType` kolonu var ama kullanılmıyor (hepsi STOCK). İki mekanizma tamamlayıcı:
- **ItemType** → "stok hareketi olsun mu?" (STOCK=evet; SERVICE/FIXED_ASSET=hayır → fiziksel hareket yok). ~~EXPENSE~~ → item DEĞİL (ExpenseType taksonomisi).
- **CostAllocation** → "maliyet/gelir hangi merkeze?" (boyutsal dağıtım)

ItemType set'i (Plan 52 ile netleşti — EXPENSE çıkarıldı):
| ItemType | Stok | Akış |
|---|---|---|
| STOCK | ✅ | satılır/üretilir/sarf — evrak belirler |
| SERVICE | ❌ | hizmet alış→gider / hizmet satış→gelir (stok yazmaz) |
| FIXED_ASSET | ❌ | sabit kıymet (amortisman ileride) |
| ~~EXPENSE~~ | — | **item değil** → ExpenseInvoice + ExpenseType (mevcut yapı) |

**B. Maliyet Merkezi Muhasebesi (çok-boyutlu, "sonsuz merkez"):** Operax'ta sadece basit `CostCenter` var (tek boyut, hiyerarşisiz, dağıtımsız). Sınırsız boyut (Masraf Merkezi/Proje/Departman/Şube) + her hareketi yüzdeyle çok-merkeze dağıtma yok. Gider → gider merkezi, gelir → gelir/proje merkezi esnek izlenemiyor.

## 2. Operax Maliyet Merkezi Modeli

| Kavram | Tablo | Açıklama |
|---|---|---|
| **Maliyet Boyutu** | `CostDimension` (yeni) | Sınırsız boyut: Masraf Merkezi, Proje, Departman, Şube, Kampanya |
| **Maliyet Merkezi** | `CostCenter` (mevcut — genişletilir) | DimensionId FK + ParentId (hiyerarşi). Her boyut altında sınırsız merkez |
| **Maliyet Dağıtımı** | `CostAllocation` (yeni) | (SourceType, SourceLineId, CostCenterId, Percent) — bir satır N merkeze % |

**Migrate YOK:** `CostCenter` mevcut tablo korunur, sadece `DimensionId` + `ParentId` kolonu eklenir. Mevcut kayıtlar "Masraf Merkezi" boyutuna default atanır.

## 3. Scope

### Kapsam dahili
**A. Hizmet/Gider akışı:**
- PO/SO satır eklemede ItemType (STOCK/SERVICE/EXPENSE/FIXED_ASSET) seçilebilsin
- `sp_ReceivingPost`/`sp_ShippingPost`: SERVICE/EXPENSE/FIXED_ASSET satırları StockMovement YAZMAZ (atla)
- SERVICE/EXPENSE PO → ExpenseInvoice doğrudan (mal kabul stok yazmaz)
- ExpenseType'a Direction (GİDER/GELİR) + AccountCode

**B. Maliyet Merkezi:**
- `CostDimension` — sınırsız boyut
- `CostCenter` genişletme — DimensionId + ParentId (hiyerarşi)
- `CostAllocation` — (SourceType, SourceLineId, CostCenterId, Percent)
- Bağ: ExpenseInvoiceLine, SalesInvoiceLine, FinancialTransaction
- Rapor: merkez bazlı gider/gelir özeti + Budget karşılaştırma

### Kapsam dışı
- Resmi muhasebe hesap planı (7xx/6xx VUK) — M16 dış muhasebe
- Otomatik dağıtım kuralları — sonra

### Etkilenen dosyalar
- `docs/sql/schema_M_CostDimension.sql` — CostDimension + CostCenter ALTER + CostAllocation
- `docs/sql/db_objects.sql` — sp_ReceivingPost/ShippingPost SERVICE/EXPENSE atlama
- `docs/sql/db_objects_starter.sql` — sp_SaveCostAllocation, tvf_CostSummary
- `Features/Finance/CostCenters/` — boyut + merkez yönetimi (yeni)
- `Features/Expenses/`, `SalesInvoices/`, PO/SO Details — dağıtım widget + ItemType
- `Features/Finance/CostReport/` — merkez bazlı rapor

**Tahmini boyut:** ~18 dosya / ~2000 satır.

## 4. Alternatifler

### A: Basit tek-boyut (CostCenter genişlet, dağıtımsız)
**Reddetme:** Çok-boyut esnekliği yok; proje+departman aynı anda izlenemiyor.

### B: ✅ Tam Maliyet Merkezi (Dimension+Center+Allocation) (seçilen)
**Sebep:** Kullanıcı çok-boyut + yüzde dağıtım istedi; rekabet avantajı (Logo/Mikro tek boyut, esneklik zayıf). SAP CO-PA / Odoo Analytic eşdeğeri.

### C: Sadece gider/hizmet, maliyet merkezi sonra
**Reddetme:** Gider satırı zaten merkez dağıtımı istiyor — birlikte mantıklı.

**5 lens:**
- 🔴 Contrarian: Dağıtım UI karmaşık — %100 tek-merkez default, çoklu opsiyonel.
- 🔵 First Principles: "Gider hangi merkeze?" = CostCenter; dağıtım = paylaşılan gider (kira 3 departman).
- 🟢 Expansionist: Budget zaten var → merkez bazlı bütçe takibi.
- ⚪ Outsider: SERVICE item stok yazmamalı — şu an yazıyor olabilir (bug riski).
- 🟡 Executor: Pazartesi: CostDimension şema + CostCenter ALTER (DimensionId+ParentId).

## 5. Riskler

| Risk | Etki | Olasılık | Mitigation |
|---|---|---|---|
| CostCenter ALTER mevcut veriyi bozar | Orta | Düşük | DimensionId nullable, mevcut → "Masraf Merkezi" default |
| Dağıtım %toplamı ≠ 100 | Orta | Yüksek | sp_SaveCostAllocation: SUM(Percent)=100 validation |
| SERVICE atlama sp_ReceivingPost bozar | Yüksek | Orta | ItemType kontrolü additive, STOCK davranışı değişmez |
| 18 dosya büyük | Yüksek | Yüksek | A ve B ayrı fazlar, ayrı commit |

## 6. Done Criteria

- [ ] Faz A1: ItemType PO/SO satırında seçilebilir; SERVICE/EXPENSE satır stok yazmaz
- [ ] Faz A2: SERVICE/EXPENSE PO → ExpenseInvoice; ExpenseType Direction+AccountCode
- [ ] Faz B1: CostDimension CRUD + CostCenter genişletme (DimensionId+ParentId)
- [ ] Faz B2: CostAllocation — gider/gelir satırına % dağıtım widget
- [ ] Faz B3: Maliyet raporu (merkez bazlı gider/gelir + budget karşılaştırma)
- [ ] sp_SaveCostAllocation %toplam=100 validation
- [ ] migrate 0 hata, smoke: kira gideri 3 departmana %40/%30/%30 → rapor doğru

## 7. Adımlar

### Faz A — Hizmet/Gider Akışı (STARTER)
1. [ ] PO/SO Details: ItemType göster + SERVICE/EXPENSE seçimi
2. [ ] sp_ReceivingPost/ShippingPost: SERVICE/EXPENSE/FIXED_ASSET → StockMovement atla
3. [ ] ExpenseType: Direction (GİDER/GELİR) + AccountCode kolonu
4. [ ] SERVICE/EXPENSE PO onayında ExpenseInvoice öner/oluştur
5. [ ] Commit: feat(M03): hizmet/gider kalemli sipariş (plan: 06)

### Faz B1 — Maliyet Merkezi Şema + CRUD
1. [ ] schema_M_CostDimension.sql: CostDimension + CostCenter ALTER (DimensionId+ParentId) + CostAllocation
2. [ ] Mevcut CostCenter kayıtları "Masraf Merkezi" boyutuna default
3. [ ] Features/Finance/CostCenters: boyut + merkez yönetim ekranı (hiyerarşik ağaç)
4. [ ] Commit: feat: maliyet merkezi şema + CRUD (plan: 06)

### Faz B2 — Dağıtım Widget
1. [ ] sp_SaveCostAllocation (%toplam=100 validation)
2. [ ] ExpenseInvoiceLine + SalesInvoiceLine + FinancialTransaction'a dağıtım widget
3. [ ] Commit: feat: maliyet dağıtımı (plan: 06)

### Faz B3 — Rapor
1. [ ] tvf_CostSummary(@CompanyId, @DimensionId, @From, @To)
2. [ ] Features/Finance/CostReport — merkez bazlı gider/gelir + budget delta
3. [ ] Commit: feat: maliyet merkezi raporu (plan: 06)

### Faz C — Test
1. [ ] E2E: kira gideri 3 departmana dağıt → rapor + budget karşılaştırma
2. [ ] Arşivle

## 8. İlişkili
- `docs/COMPETITOR_ANALYSIS.md` M03.S1 (hizmet kalemi)
- `docs/MODULE_SPECS/M03_Purchasing_Extended.md` §2.6
- Mevcut: CostCenter, ExpenseType, Budget (genişletilecek)
- Bağımlılık: Plan 02 (Payment) bağımsız; Plan 05 (chain) gider zinciri ekleyebilir

## 9. Onay
- [ ] Plan gösterildi
- [ ] Kapsam kararı (A/B/C) alındı
- [ ] Onay alındı
