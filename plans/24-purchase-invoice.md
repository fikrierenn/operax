# Plan 24 — Mal Alım Faturası (PurchaseInvoice)

**Tarih:** 2026-06-01 · **Durum:** `Taslak (onay bekliyor)` · **Modül:** M03 (Satınalma) + M11 (Cari) · **Paket:** STARTER (olmazsa-olmaz — COMPETITOR_ANALYSIS:310) · **Kaynak:** Plan 05 mimari bulgusu + mali-evrak-mevzuat + competitor-analyst + denetim (2026-06-01)

> **Kapsam bölündü (denetim 2026-06-01):** Sarf tüketim (Consumption) → **Plan 25**. Bu plan SADECE mal alım faturası + ItemType genişletme. PurchaseInvoice asıl STARTER blocker'ını (Plan 05 boş-fatura) çözer. 4 faz.

---

## 1. Problem

Operax'ta **mal alım faturası** belge tipi YOK. Mevcut:
- `ExpenseInvoice` — gider/hizmet (elektrik, kira). `ExpenseInvoiceLine` → `ExpenseTypeId`+`CostCenterId` NOT NULL; **mal satırı taşıyamaz** (ItemId yok).
- `SalesInvoice` — satış.

Plan 05 `sp_CreateExpenseInvoiceFromReceiving` mal kabulden ExpenseInvoice oluşturdu — **yanlış eşleme** (auditor C1: satır INSERT edilemiyor, boş fatura). Mal alındığında alım faturası kesilemiyor → KDV indirimi, cari borç (AccountMovement), stok maliyeti faturaya bağlanamıyor.

### Kalem türü (ItemType genişletme — yeni kolon YOK)
`Item.ItemType` ZATEN VAR (`schema_M01_M04_StarterFields.sql:22`, değerler STOCK/SERVICE/FIXED_ASSET, dead column — hiç okunmuyor). **Yeni `ItemKind` AÇMA** (mükerrer — session-memory ihlali). ItemType **genişletilir**:

| ItemType | Açıklama | Alış belge | Stok |
|---|---|---|---|
| **STOCK** (mevcut, =satılacak mal) | Ticari mal | PurchaseInvoice | Var |
| **CONSUMABLE** (yeni) | Sarf malzeme | PurchaseInvoice | Var (tüketim → Plan 25) |
| **SERVICE** (mevcut) | Hizmet/gider | ExpenseInvoice | Yok |
| **FIXED_ASSET** (mevcut) | Demirbaş | (kapsam dışı) | — |

PurchaseInvoice satırı STOCK + CONSUMABLE kabul eder (ikisi de stok girer + cari borç). SERVICE → ExpenseInvoice.

### Mevzuat (mali-evrak-mevzuat + competitor-analyst + denetim)
- `InvoiceDate` = **tedarikçinin** belge tarihi. `SupplierInvoiceNo` + `SupplierInvoiceDate` ayrı kolon zorunlu (Mikro `cha_belge_no`/`cha_belge_tarih`). BA/BS + KDV bu tarihe bağlı.
- **`SupplierInvoiceUuid NVARCHAR(40) NULL`** (Mikro `cha_uuid`) — şimdiden eklenir (M16 e-Fatura inbound bağlanacak; mükerrer kontrol UUID+No). Kullanımı sonra.
- **AccountMovement besleme YALNIZCA PurchaseInvoice POST'unda** — irsaliye/mal kabul cari beslemez. R0/K3 drift kapanır. SRBNB/GL/COGS YAZILMAZ.
- **7-gün:** alıcı tarafında **HARD THROW DEĞİL → soft uyarı** (geç tedarikçi faturası yine kaydedilmeli — KDV indirimi). Hesap `Receiving.DocDate` (irsaliye tarihi) üzerinden, CreatedAt değil.

---

## 2. Scope

### Kapsam dahili (STARTER)
- **Faz A — Şema:**
  - `Item.ItemType` değer kümesini genişlet: STOCK/CONSUMABLE/SERVICE/FIXED_ASSET (kolon zaten var; CHECK/dropdown güncelle, mevcut STOCK korunur). **Yeni kolon yok.**
  - `PurchaseInvoice`: `Id, CompanyId, PartnerId, BranchId NULL, DocNo (iç), SupplierInvoiceNo, SupplierInvoiceDate, SupplierInvoiceUuid NULL, InvoiceDate, DueDate, Subtotal, TaxAmount, GrandTotal, PaidAmount, Currency, ExchangeRate, Status` + audit. UNIQUE(CompanyId, PartnerId, SupplierInvoiceNo).
  - `PurchaseInvoiceLine`: `Id, InvoiceId, ItemId, UomId, Qty, UnitPrice, LineSubtotal, TaxRatePercent, TaxAmount, LineTotal, SourceReceivingLineId NULL, SourceLinkType (LINKED/UNLINKED)`. Satır-bazlı Receiving bağı (N:1). Header TaxAmount = satır toplamı (SP'de hesaplanır, ExpenseInvoiceLine computed pattern referans).
  - Idempotent migrate + CLI listesi.
- **Faz B — sp_PurchaseInvoicePost (EN KRİTİK):**
  - DRAFT → POSTED; **AccountMovement Alacak — cari borç YALNIZCA burada** (`AccountMovementType.PURCHASE_INVOICE`). SRBNB/GL/COGS YAZILMAZ.
  - Dönem guard (sp_GuardPeriodOpen, SupplierInvoiceDate). SupplierInvoiceNo + SupplierInvoiceDate boşsa THROW.
  - N:1 birleştirmede en eski `Receiving.DocDate` +7 gün → **soft uyarı (TempData), THROW değil**.
  - Tevkifatlı fatura bu sürümde girilemez (TaxAmount düz GrandTotal'e ekleniyor) — UI not + risk yazılı.
  - `sp_PurchaseInvoiceReverse` (AccountMovement ters-satır + e-belge guard).
- **Faz C — Plan 05 düzeltme:**
  - `sp_CreateExpenseInvoiceFromReceiving` → `sp_CreatePurchaseInvoiceFromReceiving` (satır kopyalı, PO fiyatından UnitPrice).
  - **before-major-change:** eski SP'yi çağıran tüm C# referansı grep'le (Receiving DocFlow + diğer). Yönlendir, sonra kaldır.
  - Receiving DocFlow butonu PurchaseInvoice'a yönlensin.
- **Faz D — CRUD UI + smoke + reviewer:**
  - `Features/PurchaseInvoices/` Index + Details (mal satırı + SupplierInvoiceNo/Date/Uuid). Ödeme zinciri (PurchaseInvoice → Payment ön-dolu).
  - Item Create/Edit: ItemType dropdown (4 değer).
  - Smoke: Receiving→PurchaseInvoice→satır dolu→Post→AccountMovement borç→ödeme. build 0/0. code-reviewer + sql-sp-reviewer + security-reviewer.

### Kapsam dışı (ayrı plan)
- **Sarf tüketim (Consumption) → Plan 25.**
- Faturalanmamış-mal-kabul raporu (SRBNB karşılığı, dönem-sonu) → backlog.
- Tek faturada STOCK+SERVICE karışık satır (SERVICE → ExpenseInvoice kalır).
- N:1 birleştirme UI ekranı (şema destekler, seçim UI ayrı).
- Tevkifat/KDV-2, döviz kur farkı muhasebesi, e-Fatura senaryo (TEMEL/TICARI) ayrımı.

---

## 3. Alternatifler
- **A: Ayrı PurchaseInvoice + ItemType genişletme (SEÇİLEN)** — mal/hizmet net ayrım, mevcut dead ItemType kolonu kullanılır (yeni kolon yok).
- **B: ExpenseInvoice'a ItemId ekle** — Reddedildi: gider modelini kirletir.
- **C: Yeni ItemKind kolonu** — Reddedildi: ItemType zaten var, mükerrer.
- **D: Consumption'ı bu plana dahil et** — Reddedildi (denetim): STARTER için ağır, ayrı Plan 25.

---

## 4. Riskler
| Risk | Önlem |
|---|---|
| AccountMovement çift besleme (Receiving + fatura) | İrsaliye stok-only; fatura cari-only (Plan 21) |
| ItemType mükerrer veri | Yeni kolon AÇMA — mevcut ItemType genişlet |
| Plan 05 eski SP commit'li | Faz C: grep referans + yönlendir + kaldır (before-major-change) |
| SupplierInvoiceNo mükerrer | UNIQUE(CompanyId, PartnerId, SupplierInvoiceNo) |
| 7-gün alıcıda kayıt engeli | Soft uyarı (TempData), THROW değil; DocDate üzerinden |
| Tevkifatlı faturada yanlış cari borç | Bu sürümde tevkifat girilemez (UI not + risk) |

## 5. Done Criteria
- [ ] Item.ItemType genişletildi (STOCK/CONSUMABLE/SERVICE/FIXED_ASSET); yeni kolon yok; mevcut STOCK korundu
- [ ] PurchaseInvoice/Line + SupplierInvoiceNo/Date/Uuid + ExchangeRate idempotent migrate
- [ ] sp_PurchaseInvoicePost: AccountMovement borç + SupplierInvoiceDate zorunlu + dönem guard + 7-gün soft uyarı (DocDate)
- [ ] sp_PurchaseInvoiceReverse: ters-satır + e-belge guard
- [ ] sp_CreatePurchaseInvoiceFromReceiving: satır kopyalı (boş fatura çözüldü); Receiving DocFlow yönlendi; eski SP referansları grep'lendi + kaldırıldı
- [ ] PurchaseInvoices CRUD UI + Item ItemType dropdown + ödeme zinciri
- [ ] Smoke: Receiving→PurchaseInvoice→satır dolu→Post→AccountMovement borç→ödeme
- [ ] build 0/0; code-reviewer + sql-sp-reviewer + security-reviewer

## 6. Rollback
PurchaseInvoice/Line DROP; sp_* DROP; ItemType değer kümesi geri; Receiving DocFlow eski SP'ye/gizle. ExpenseInvoice + StockMovement etkilenmez.

## 7. Adımlar
- [ ] **Faz A:** schema_M03_PurchaseInvoice.sql (ItemType genişlet + 2 tablo) + CLI migrate
- [ ] **Faz B:** sp_PurchaseInvoicePost + sp_PurchaseInvoiceReverse
- [ ] **Faz C:** sp_CreatePurchaseInvoiceFromReceiving + DocFlow yönlendir + eski SP grep+kaldır
- [ ] **Faz D:** PurchaseInvoices CRUD + Item ItemType + ödeme zinciri + smoke + 3 reviewer

## 8. 5 Lens
- 🔴 **Contrarian:** Fatal = AccountMovement çift/yanlış besleme. Önlem: fatura cari-only (tek sefer), irsaliye stok-only.
- 🔵 **First Principles:** "Stoklu mu, ne zaman gider, belge tarihi kimin" — mal stoklu (alışta cari borç, tedarikçi tarihi).
- 🟢 **Expansionist:** PurchaseInvoice ileride tevkifat + e-Fatura inbound + N:1 konsolidasyonun temeli.
- ⚪ **Outsider:** "Alış faturası nerede" → bu plan asıl boşluğu kapatır; gider faturası (ExpenseInvoice) ayrı kalır, UI'da net ayrım.
- 🟡 **Executor:** Pazartesi Faz A şema; en kritik Faz B AccountMovement besleme.

## 9. İlişkili
- `plans/05-document-chain-smart-buttons.md` — Faz C bu planın SP'sini düzeltir
- `plans/25-consumption.md` — sarf tüketim (bu plandan bölündü)
- `.claude/rules/document-immutability.md` — fatura POSTED reversal
- `docs/reference/REFERENCE_STUDY.md` R0/K1/K3, B19 üç-tarih
- `docs/reference/MIKRO_V16_ANALYSIS.md` §0.5, §12.9 (N:1), §14 (belge tarihi)
- Plan 21 — belge ayrım prensibi

## 10. Onay
- [ ] Plan gösterildi
- [ ] Onay alındı
