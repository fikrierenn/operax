# Plan 06 — Gider/Hizmet Akışı + Analitik Muhasebe (Analytic Accounting)

**Tarih:** 2026-05-29
**Yazan:** Claude
**Durum:** `Taslak`
**Modül:** M02 + M03 + M04 + Expenses + yeni M-Analytic
**Paket:** STARTER (gider/hizmet) + ENTERPRISE (analitik dağıtım)

---

## 1. Problem

İki bağlantılı eksik:

**A. Hizmet/gider alım-satım akışı (ItemType yönlendirmeli):** `Item.ItemType` kolonu var ama kullanılmıyor (hepsi STOCK). İki mekanizma tamamlayıcı çalışır:
- **ItemType** → "stok hareketi olsun mu?" (STOCK=evet; SERVICE/EXPENSE/FIXED_ASSET=hayır → doğrudan gider/gelir)
- **CostAllocation** → "maliyet/gelir hangi merkeze?" (boyutsal dağıtım)

ItemType set'i genişletilir:
| ItemType | Stok | Akış |
|---|---|---|
| STOCK | ✅ | PO→Receiving→stok→maliyet |
| SERVICE | ❌ | hizmet alış→gider / hizmet satış→gelir |
| EXPENSE | ❌ | sadece gider (kira/elektrik/yakıt) |
| FIXED_ASSET | ❌ | sabit kıymet (amortisman ileride) |

Referans: SAP CO Cost Center + CO-PA, IAS Canias boyut yapısı — enterprise standart, Operax çok-boyut + yüzde dağıtımla eşdeğer.

**B. Analitik muhasebe (Odoo "sonsuz gider merkezi"):** Operax'ta sadece basit `CostCenter` var (tek boyut, dağıtımsız). Odoo'daki gibi sınırsız boyut (Masraf Merkezi/Proje/Departman/Şube) + her hareketi yüzdeyle çok-hesaba dağıtma yok. Gider → gider merkezi, gelir → gelir/proje merkezi esnek izlenemiyor.

## 2. Scope

### Kapsam dahili
**A. Hizmet/Gider akışı:**
- PO/SO satır eklemede ItemType=SERVICE/FIXED_ASSET seçilebilsin
- `sp_ReceivingPost`/`sp_ShippingPost`: SERVICE/FIXED_ASSET satırları StockMovement YAZMAZ (atla)
- SERVICE PO → ExpenseInvoice doğrudan (mal kabul stok yazmaz)
- ExpenseType'a Direction (GİDER/GELİR) + AccountCode

**B. Analitik Muhasebe:**
- `CostDimension` — sınırsız boyut (Masraf Merkezi, Proje, Departman, Şube)
- `AnalyticAccount` — plan altında sınırsız hesap, hiyerarşik (parent-child)
- `AnalyticDistribution` — (SourceType, SourceLineId, AnalyticAccountId, Percent)
- Bağ: ExpenseInvoiceLine, SalesInvoiceLine, FinancialTransaction
- CostCenter → "Masraf Merkezi" planı hesaplarına migrate (geri uyumlu)
- Rapor: analitik hesap bazlı gider/gelir özeti + Budget karşılaştırma

### Kapsam dışı
- Resmi muhasebe hesap planı (7xx/6xx VUK) — M16 dış muhasebe
- Otomatik dağıtım kuralları (auto-distribution rules) — sonra

### Etkilenen dosyalar
- `docs/sql/schema_M_Analytic.sql` — CostDimension/Account/Distribution (yeni)
- `docs/sql/schema_*` — ExpenseType.Direction+AccountCode, Item.ItemType kullanımı
- `docs/sql/db_objects.sql` — sp_ReceivingPost/ShippingPost SERVICE atlama
- `docs/sql/db_objects_starter.sql` — sp_SaveAnalyticDistribution
- `Features/Finance/Analytic/` — Plan + Account yönetimi (yeni)
- `Features/Expenses/`, `SalesInvoices/`, PO/SO Details — analitik dağıtım widget
- Rapor: `Features/Finance/AnalyticReport/`

**Tahmini boyut:** ~18 dosya / ~2000 satır.

## 3. Alternatifler

### A: Basit tek-boyut (CostCenter genişlet, dağıtımsız)
**Açıklama:** Her satır 1 masraf merkezine.
**Reddetme:** Odoo "sonsuz" esnekliği yok; proje+departman aynı anda izlenemiyor.

### B: ✅ Tam Analitik Muhasebe (Plan+Account+Distribution) (seçilen)
**Açıklama:** Sınırsız boyut + hiyerarşi + yüzde dağıtım.
**Sebep:** Kullanıcı açıkça Odoo modelini istedi ("sonsuz gider merkezi"); rekabet avantajı (Logo/Mikro'da bu esneklik zayıf).

### C: Sadece gider/hizmet akışı, analitik sonra
**Reddetme:** Gider satırı zaten analitik dağıtım istiyor — birlikte mantıklı.

**5 lens:**
- 🔴 Contrarian: Analitik dağıtım UI karmaşık olabilir — basit %100 tek-hesap default, çoklu opsiyonel.
- 🔵 First Principles: "Gider hangi merkeze?" sorusu = analitik hesap; dağıtım = gerçek hayatta paylaşılan gider (kira 3 departman).
- 🟢 Expansionist: Budget zaten var → analitik + budget = merkez bazlı bütçe takibi (güçlü kombinasyon).
- ⚪ Outsider: SERVICE item stok yazmamalı — şu an yazıyor olabilir, bu bug riski.
- 🟡 Executor: Pazartesi: CostDimension/Account şeması + CostCenter migrate.

## 4. Riskler

| Risk | Etki | Olasılık | Mitigation |
|---|---|---|---|
| CostCenter migrate veri kaybı | Yüksek | Düşük | CostCenter korunur, AnalyticAccount'a kopya + FK |
| Dağıtım %toplamı ≠ 100 | Orta | Yüksek | SP validation: SUM(Percent)=100 zorunlu |
| SERVICE atlama sp_ReceivingPost'u bozar | Yüksek | Orta | ItemType kontrolü additive, STOCK davranışı değişmez |
| 18 dosya çok büyük | Yüksek | Yüksek | A (gider/hizmet) ve B (analitik) ayrı fazlar, ayrı commit |

## 5. Done Criteria

- [ ] Faz A1: Item.ItemType PO/SO satırında seçilebilir; SERVICE satır stok yazmaz
- [ ] Faz A2: SERVICE PO → ExpenseInvoice; ExpenseType Direction+AccountCode
- [ ] Faz B1: CostDimension/Account CRUD + CostCenter migrate
- [ ] Faz B2: AnalyticDistribution — gider/gelir satırına % dağıtım widget
- [ ] Faz B3: Analitik rapor (hesap bazlı gider/gelir + budget karşılaştırma)
- [ ] Dağıtım %toplam=100 validation
- [ ] migrate 0 hata, smoke: kira gideri 3 departmana %40/%30/%30 dağıt → rapor doğru

## 6. Rollback
- CostCenter korunur (silinmez), AnalyticAccount paralel
- SERVICE atlama: ItemType kontrolü kaldırılınca eski davranış
- Yeni tablolar drop (veri yoksa)

## 7. Adımlar

### Faz A — Hizmet/Gider Akışı (STARTER)
1. [ ] PO/SO Details: ItemType göster + SERVICE seçimi
2. [ ] sp_ReceivingPost/ShippingPost: SERVICE/FIXED_ASSET → StockMovement atla
3. [ ] ExpenseType: Direction (GİDER/GELİR) + AccountCode kolonu
4. [ ] SERVICE PO onayında ExpenseInvoice öner/oluştur
5. [ ] Commit: feat(M03): hizmet/gider kalemli sipariş (plan: 06)

### Faz B1 — Analitik Şema + CRUD
1. [ ] schema_M_Analytic.sql: CostDimension, AnalyticAccount (hiyerarşik), AnalyticDistribution
2. [ ] CostCenter → AnalyticAccount migrate (Masraf Merkezi planı)
3. [ ] Features/Finance/Analytic: Plan + Account yönetim ekranı
4. [ ] Commit: feat: analitik muhasebe şema + CRUD (plan: 06)

### Faz B2 — Dağıtım Widget
1. [ ] sp_SaveAnalyticDistribution (%toplam=100 validation)
2. [ ] ExpenseInvoiceLine + SalesInvoiceLine + FinancialTransaction'a dağıtım widget
3. [ ] Commit: feat: analitik dağıtım (plan: 06)

### Faz B3 — Rapor
1. [ ] tvf_AnalyticSummary(@CompanyId, @PlanId, @From, @To)
2. [ ] Features/Finance/AnalyticReport — hesap bazlı gider/gelir + budget delta
3. [ ] Commit: feat: analitik rapor (plan: 06)

### Faz C — Test
1. [ ] E2E: kira gideri 3 departmana dağıt → rapor + budget karşılaştırma
2. [ ] Arşivle

## 8. İlişkili
- `docs/COMPETITOR_ANALYSIS.md` M03.S1 (hizmet kalemi)
- `docs/MODULE_SPECS/M03_Purchasing_Extended.md` §2.6 (hizmet kalemi)
- Mevcut: CostCenter, ExpenseType, Budget (genişletilecek)
- Bağımlılık: Plan 02 (Payment) bağımsız; Plan 05 (chain) gider zinciri ekleyebilir

## 9. Onay
- [ ] Plan gösterildi
- [ ] Kapsam kararı (A/B/C) alındı
- [ ] Onay alındı
