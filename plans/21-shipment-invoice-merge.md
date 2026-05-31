# Plan 21 — İrsaliye ↔ Fatura Ayrımı + N:1 Birleştirme (M-F2.1 / E1)

**Tarih:** 2026-06-01 · **Durum:** `Taslak (onay bekliyor)` · **Modül:** M04 · **Kaynak:** MASTER_EXECUTION_PLAN M-F2.1 [E1/E1.B]

> **3 skill doğrulandı:** code-explorer (mevcut yapı), mali-evrak (VUK md.230/231 7-gün),
> reference-researcher (ERPNext billed_amt/per_billed + Odoo qty_invoiced/grouping keys). Stack sabit (Dapper/SP).

---

## 1. Problem

Sevkiyat (Shipping=irsaliye) → fatura dönüşümü **1:1** (`SalesInvoice.ShippingId` FK + çift-post guard).
Gerçek ihtiyaç: **N irsaliye → 1 fatura** birleştirme (aynı cari, aynı ayın sevkiyatları tek faturada — VUK 7-gün
içinde). Mevcut model bunu yapamaz. Ayrıca kısmi faturalama (1 sevkiyat satırı 2 faturaya) imkânsız.

**Mevcut boşluklar (code-explorer kanıt):**
- `ShippingLine` faturalanan miktar izi YOK → kısmi/N:1 imkânsız
- `SalesInvoiceLine.SourceShipmentLineId` YOK → satır izlenebilirlik yok
- `SalesInvoice.ShippingId` header FK → 1:1 dayatıyor
- `SalesInvoice.IssueDate` / sevk tarihi (MovementDate) ayrımı YOK → 7-gün guard yapılamaz
- e-Belge `DespatchDocumentReference` / `BillingReference` alanları YOK

## 1.b BELGE AYRIMI PRENSİBİ (kullanıcı kuralı 2026-06-01 — DEĞİŞMEZ)

| Belge | StockMovement | AccountMovement | Gerekçe |
|---|---|---|---|
| **İrsaliye** (Shipping/Receiving POSTED) | ✅ mal hareketi | ❌ YOK | Mali belge değil; cari borç/alacak doğmaz |
| **Fatura** (Sales/Expense Invoice) | ❌ (stok zaten irsaliyede) | ✅ Debit/Credit | Mali belge; cari hareket fatura ile doğar |

- **Mevcut kod bu kurala uyuyor:** `sp_ShippingPost`/`sp_ReceivingPost` yalnızca StockMovement;
  AccountMovement sadece fatura SP'lerinde (`sp_GenerateSalesInvoiceFromShipping`, `sp_ExpenseInvoicePost`).
- **İki mod (Parameter.InvoiceMode):**
  - **INSTANT** → irsaliye onayı otomatik fatura tetikler (1:1); StockMovement + (fatura→AM) aynı an.
  - **DEFERRED** (varsayılan hedef) → irsaliye yalnızca StockMovement; fatura SONRA ayrı kesilir → AM o an.
    **N:1 birleştirme yalnızca DEFERRED modda** çalışır (INSTANT zaten 1:1 otomatik).
- **Kritik:** N:1 birleştirme StockMovement'a DOKUNMAZ (stok zaten irsaliyede yazıldı); yalnızca
  fatura + AccountMovement üretir, InvoicedQty günceller. Stok çift-sayım riski YOK.

### Direkt fatura senaryoları (irsaliyesiz) — fatura StockMovement yapar MI?

Fatura satırının StockMovement yazıp yazmaması KAYNAĞINA bağlı (çift-sayım önleme):

| Fatura satırı | SourceShipmentLineId | Kalem tipi | StockMovement | AccountMovement |
|---|---|---|---|---|
| İrsaliyeden gelen | DOLU | mal | ❌ (irsaliyede yazıldı) | ✅ |
| Direkt mallı (irsaliyesiz) | NULL | mal | ✅ (irsaliye yerine geçer) | ✅ |
| Hizmet/masraf | NULL | hizmet | ❌ (stok yok) | ✅ |

- **VUK [DOC md.230]:** hizmet faturası irsaliye gerektirmez; mallı direkt fatura "irsaliye yerine geçer".
- **Kod kuralı:** fatura post SP'sinde satır bazında: `SourceShipmentLineId IS NULL AND Item.IsStockItem=1`
  → StockMovement ISSUE yaz. Aksi → yazma. Çift-sayım guard satır-bazlı.
- **Bu plan kapsamı:** irsaliyeden N:1 birleştirme ANA hedef. Direkt mallı fatura StockMovement
  entegrasyonu bu planda tasarlanır ama Item.IsStockItem + hizmet kalem tipi M-F6.1'e bağlı olabilir →
  Faz sıralaması: önce irsaliye→fatura (StockMovement YOK yolu), sonra direkt mallı (StockMovement VAR).

## 2. Scope

### Dahili
- **Şema (additive, geri uyumlu):**
  - `ShippingLine += InvoicedQty DECIMAL(18,6) NOT NULL DEFAULT 0` (miktar-tabanlı — boolean DEĞİL; ERPNext billed_amt + Odoo qty_invoiced deseni). `RemainingQty = QtyBase − InvoicedQty` türetilir.
  - `SalesInvoiceLine += SourceShipmentLineId UNIQUEIDENTIFIER NULL` + FK ShippingLine + index (ERPNext dn_detail)
  - `SalesInvoice += IssueDate DATETIME2 NULL` (düzenleme tarihi, e-Fatura imza; InvoiceDate korunur geriye uyum)
  - `SalesInvoice.ShippingId` → KORUNUR (PrimaryShippingId bilgi amaçlı, NULL N:1'de); silinmez (immutability)
- **SP:**
  - `sp_GenerateSalesInvoiceFromShippings` (çoğul) — N sevkiyat → 1 fatura:
    - Guard: hepsi POSTED + aynı PartnerId + aynı Currency (Odoo grouping key) → aksi THROW
    - **7-gün VUK guard:** `DATEDIFF(DAY, MIN(sevk tarihi), @IssueDate) > 7` → THROW (mali-evrak)
    - Her fatura satırı `SourceShipmentLineId` + atomik `ShippingLine.InvoicedQty += qty`
    - Çift-post guard `RemainingQty <= 0` (miktar-tabanlı; eski EXISTS yerine)
    - sp_GuardPeriodOpen + sp_GuardPartnerReconciled zinciri korunur
  - `sp_GenerateSalesInvoiceFromShipping` (tekil) → çoğul SP'ye delege eden ince sarmalayıcı (geri uyum)
- **TVF:** `tvf_UninvoicedShipments(@CompanyId)` — POSTED + RemainingQty>0 sevkiyat satırları; partner+currency GROUP BY (birleştirme adayı). Statü türetme view: TO_BILL/PARTIAL/INVOICED
- **UI:** SalesInvoices/Create — faturalanmamış sevkiyat seç (partner+currency filtreli) → çoklu seç → birleştir → fatura. Kısmi miktar override.

### Kapsam Dışı (sonraki faz)
- e-Belge DespatchDocumentReference UBL üretimi (M16 — zorunluluk DOĞRULANMADI)
- Alış tarafı (Receiving→ExpenseInvoice) N:1 — simetrik ama ayrı faz
- İrsaliyeli fatura (tek belge anında) — ayrı senaryo
- COGS/FIFO (M-F3.1)

## 3. Alternatifler
- **A: Boolean IsInvoiced** — Reddedildi: ERPNext+Odoo ikisi de miktar-tabanlı; boolean kısmi/N:1 kaldıramaz.
- **B: Header FK çoklu (ShippingId N)** — Reddedildi: 1 fatura N sevkiyat → header tek FK yetmez; satıra inmeli.
- **C: InvoicedQty + SourceShipmentLineId satır bazlı (seçilen)** — ERPNext dn_detail + Odoo qty_invoiced füzyonu.

## 4. Riskler
| Risk | Önlem |
|---|---|
| Mevcut 1:1 SP regresyonu | Tekil SP → çoğul'a sarmalayıcı (geri uyum), smoke |
| Kısmi faturalama aşımı | InvoicedQty + RemainingQty guard (>QtyBase THROW) |
| Farklı para/cari birleşme | SP guard partner+currency eşitlik |
| 7-gün VUK ihlali | MIN(sevk)+7 guard THROW |
| Eski ShippingId verisi | Migration: mevcut faturalı sevkiyatların InvoicedQty backfill |

## 5. Done Criteria
- [ ] ShippingLine.InvoicedQty + SalesInvoiceLine.SourceShipmentLineId + SalesInvoice.IssueDate şema
- [ ] sp_GenerateSalesInvoiceFromShippings (N:1 + partner/currency/7-gün guard)
- [ ] Tekil SP sarmalayıcı (geri uyum)
- [ ] tvf_UninvoicedShipments + statü view
- [ ] SalesInvoices/Create UI (sevkiyat seç + birleştir + kısmi)
- [ ] Backfill: mevcut faturalı sevkiyat InvoicedQty
- [ ] Smoke: N:1 birleştirme + kısmi + 7-gün guard + çift-post; sql-sp-reviewer

## 6. Rollback
Additive kolonlar DROP; çoğul SP DROP; tekil SP eski sürüm; TVF DROP; UI sök.

## 7. Adımlar
- [ ] **Faz 1:** Şema (InvoicedQty + SourceShipmentLineId + IssueDate) + backfill
- [ ] **Faz 2:** sp_GenerateSalesInvoiceFromShippings (N:1 + guard'lar) + tekil sarmalayıcı
- [ ] **Faz 3:** tvf_UninvoicedShipments + statü view
- [ ] **Faz 4:** SalesInvoices/Create UI (birleştirme ekranı)
- [ ] **Faz 5:** Smoke + sql-sp-reviewer + mali-evrak doğrulama

## 8. 5 Lens
- 🔴 **Contrarian:** Backfill yanlışsa mevcut faturalar açık görünür → InvoicedQty backfill kritik, önce.
- 🔵 **First Principles:** Soru "1 sevkiyat 1 fatura" değil "hangi sevkiyat satırı ne kadar faturalandı".
- 🟢 **Expansionist:** Aynı desen alış (Receiving→ExpenseInvoice) ve iade (M-F2.2) için temel.
- ⚪ **Outsider:** "Neden InvoicedQty + SourceShipmentLineId iki yer?" → biri durum, biri iz; ERPNext de böyle.
- 🟡 **Executor:** Şema + çoğul SP + sarmalayıcı; UI sonra.

## 9. İlişkili
- `docs/MASTER_EXECUTION_PLAN.md` M-F2.1 [E1/E1.B]
- `docs/reference/MIKRO_V16_ANALYSIS.md` §12.9 (irsaliye birleştirme)
- VUK md.230 (sevk irsaliyesi) · md.231/5 (7-gün) · md.18 (süre)
- `.claude/rules/document-immutability.md` (IsInvoiced bağı, immutability)
- `docs/sql/schema_M04_SalesInvoice.sql` · `db_objects_starter.sql:243` (mevcut 1:1 SP)
- plan 16/18/19 (AM besleme + kapama + mutabakat — fatura zinciri tüketicisi)

## 10. Mevzuat DOĞRULANMADI (production öncesi)
- N→1 toplu/ay-sonu faturada 7 günü genişleten istisna var mı (VUK birincil)
- DespatchDocumentReference UBL şema-zorunluluk koşulu (efatura.gov.tr kılavuz)
- Konsinye/numune faturasız irsaliye dayanağı
- e-İrsaliye ciro eşiği güncel TL (509 SN VUK GT)
