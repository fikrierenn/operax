# Plan 22 — Evrak Status Tutarlılığı + Reversal UI Bağlama (P0)

**Tarih:** 2026-06-01 · **Durum:** `Taslak (onay bekliyor)` · **Modül:** M03/M04/M07/M08/M10/M11 · **Kaynak:** operax-erp-wms-auditor 4-paralel denetim (2026-06-01)

> **Denetim bulgusu:** 7 reversal SP yazıldı (plan 14/18/19) ama HİÇBİRİ UI'a bağlı değil — POSTED evrak iptal edilemiyor.
> Ek: status sözlüğü tutarsız (PO POSTED vs SO APPROVED), tvf'ler APPROVED arıyor, transition seed'de POSTED→CANCELLED yok.

---

## 1. Problem (4 katmanlı, birbirine bağlı)

### P1 — Reversal SP'leri UI'sız (en yüksek muhasebe riski)
7 SP tanımlı, sıfır PageModel çağrısı:
`sp_ShippingReverse`, `sp_TransferReverse`, `sp_CycleCountReverse`, `sp_ProductionReverse`,
`sp_SalesInvoiceReverse`, `sp_ExpenseInvoiceReverse`, `sp_PaymentReverse`.
→ Yanlış sevkiyat/transfer/fatura/tahsilat POSTED sonrası düzeltilemiyor.

### P2 — Status sözlüğü tutarsız (zincir kırık)
- **PO:** kod+SP `POSTED` yazıyor (206 kayıt), `tvf_OpenPurchaseOrders`+`vw_OpenPurchaseOrders` `APPROVED` arıyor (2 kayıt) → **Receiving PO dropdown'u 206 siparişi göstermiyor.**
- **SO:** veri `APPROVED` (156), `tvf_OpenSalesOrders` `APPROVED` arıyor → tutarlı ama PO ile asimetrik.
- Karar gerek: tek sözlük (POSTED) mü, çift (PO/SO farklı) mı?

### P3 — StatusTransition seed (DÜZELTİLDİ — denetim varsayımı yanlıştı)
- ⚠️ **Denetim "POSTED→CANCELLED yok → reversal THROW" dedi ama YANLIŞ:** reversal SP'leri
  `sp_ValidateStatusTransition` **ÇAĞIRMIYOR** (0 kez); kendi inline guard'ı var
  (`IF @Status <> 'POSTED' THROW` + `IF @Status = 'CANCELLED' THROW`). → **Faz B gereksiz, DÜŞÜRÜLDÜ.**
- Kalan (düşük öncelik, P3): duplike seed temizlik + SHIPMENT/SHIPPING isim ikiliği — kozmetik, ertelendi.

### P4 — Cari defter eksik besleme (ayrı ama ilişkili)
- `sp_PayLoanInstallment` + `sp_CloseStatement` → AccountMovement yazmıyor.
- `sp_PayCreditCardStatement` → CompanyId guard zayıf (IDOR).

## 2. Scope

### Dahili
- **Faz A — Status sözlüğü birleştir:** `tvf_OpenPurchaseOrders`/`vw_OpenPurchaseOrders`
  `APPROVED`→`POSTED` (PO veri POSTED). SO için karar: APPROVED kalsın (156 kayıt migration riski) VEYA
  POSTED'e taşı. **Öneri:** PO+SO ikisi de POSTED kanonik; SO verisi APPROVED→POSTED migration + tvf güncelle.
- **Faz B — StatusTransition seed düzelt:** idempotent (NOT EXISTS) + POSTED→CANCELLED tüm DocumentType için +
  COMPLETED→CANCELLED (CycleCount/Production) + duplike temizle. SHIPMENT→SHIPPING isim hizalama (veya alias).
- **Faz C — Reversal UI (7 SP):** her Details.cshtml.cs'e `OnPostCancelAsync`/`OnPostReverseAsync` +
  _DocToolbar'a "İptal Et" butonu (POSTED durumda). SP THROW 50000-59999 catch → TempData.
  - Shipping, Transfer, CycleCount, Production, SalesInvoice, Expense, Payment(Finance)
- **Faz D — Cari besleme fix:** sp_PayLoanInstallment + sp_CloseStatement AM INSERT;
  sp_PayCreditCardStatement @CompanyId guard.

### Kapsam Dışı (ayrı plan)
- Eksik evrak tipleri: RETURN (M-F2.2/FIFO bağlı), WASTE/OPENING (M-F2.3), Virman (M-F4.1)
- Plan 21 N:1 UI (ayrı, devam eden)
- FX_DIFF, override yolu, AR index filter (backlog)

## 3. Alternatifler (status sözlüğü)
- **A: PO+SO ikisi POSTED (seçilen)** — tek sözlük, DocStatus.Posted; SO veri migration. Tutarlı, magic-string azalır.
- **B: PO POSTED, SO APPROVED ayrı kalsın** — Reddedildi: iki sipariş tipi farklı sözcük = kalıcı kafa karışıklığı.
- **C: Her şey APPROVED** — Reddedildi: 206 PO + tüm SP POSTED yazıyor, daha büyük migration.

## 4. Riskler
| Risk | Önlem |
|---|---|
| SO APPROVED→POSTED migration 156 kayıt | Tek UPDATE + tvf eş-zaman; smoke |
| POSTED→CANCELLED transition reversal'ı açar ama child guard | Reversal SP zaten child-fatura/tahsilat REJECT eder (mevcut) |
| Reversal child zinciri (PO iptal ama Receiving var) | SP guard'ları mevcut; UI THROW gösterir |
| Cari AM besleme çift-post | UX_AccountMovement_Source unique mevcut |

## 5. Done Criteria
- [ ] tvf/vw OpenPurchaseOrders POSTED; Receiving PO dropdown 206 görüyor
- [ ] SO APPROVED→POSTED migration + tvf; SO dropdown tutarlı
- [ ] StatusTransition idempotent seed + POSTED→CANCELLED tüm tip + duplike temiz
- [ ] 7 reversal SP UI'a bağlı (OnPostCancel + buton + THROW catch)
- [ ] sp_PayLoanInstallment + sp_CloseStatement AM yazıyor; sp_PayCreditCardStatement CompanyId guard
- [ ] Smoke: her evrak POSTED→iptal→ters kayıt + child guard; build 0/0
- [ ] sql-sp-reviewer + security-reviewer

## 6. Rollback
tvf/vw CREATE OR ALTER önceki; SO migration ters UPDATE; transition seed DELETE+eski; UI handler sök; SP eski sürüm.

## 7. Adımlar
- [x] **Faz A:** tvf/vw OpenPO/SO POSTED IN(POSTED,APPROVED) — PO dropdown 2→207 `a161f68`
- [~] **Faz B:** DÜŞÜRÜLDÜ — reversal SP ValidateStatusTransition çağırmıyor, inline guard yeterli
- [x] **Faz C1:** Shipping/Transfer/CycleCount/Production reversal UI (4 WMS) — OnPostReverseAsync + "İptal Et" buton + THROW catch.
  **+ 2 kritik düzeltme:** (1) `db_objects_reversal.sql` migrate listesinde değildi → CLI migrate'e eklendi (SP'ler hiç deploy edilmiyordu).
  (2) **Ledger çift-sayım bug'ı:** 5 StockMovement reversal SP'si hem `IsCancelled=1` flag hem `-QtyBase` REVERSAL satırı yazıyordu; tvf_InventoryBalance `IsCancelled=0` filtrelediği için stok reversal sonrası 2× geri geliyordu (smoke ile +100 doğrulandı). Fix: REVERSAL INSERT kaldırıldı, flag-only model (guard UPDATE rowcount'a taşındı). Smoke: reversal sonrası aktif net=0, tek restorasyon ✓. document-immutability.md §1.b güncellendi.
  > ⚠️ **Scope notu:** C1 4 WMS'i tek grup aldı ama yalnızca Shipping STARTER (M04). Transfer(M07)/CycleCount(M08)/Production(M10) STARTER dışı — yapıldı, kalıyor ama **bundan sonra STARTER odak** (M00/01/02/03/04/11). Sıradaki iş STARTER'a öncelik vermeli.
- [ ] **Faz C2:** SalesInvoice/Expense/Payment reversal UI (3 finans/fatura) — hepsi STARTER (M04/M03/M11)
- [ ] **Faz D:** Cari besleme fix (loan/card AM + card CompanyId)
- [ ] **Faz E:** Smoke + sql-sp-reviewer + security-reviewer

## 8. 5 Lens
- 🔴 **Contrarian:** Fatal = SO migration 156 kayıt yanlışsa açık SO listesi bozulur → tek transaction + smoke.
- 🔵 **First Principles:** Soru "reversal SP var mı" değil "kullanıcı yanlış POSTED evrakı düzeltebiliyor mu" — hayır.
- 🟢 **Expansionist:** Status sözlüğü + transition omurgası tüm gelecek evrak tiplerinin (iade/fire/virman) temeli.
- ⚪ **Outsider:** "206 sipariş onaylı ama mal kabul ekranında görünmüyor" — kullanıcı için en şaşırtıcı bug.
- 🟡 **Executor:** Faz A (dropdown fix) en hızlı görünür değer; sonra reversal UI.

## 9. İlişkili
- operax-erp-wms-auditor denetim (2026-06-01, 4 paralel Explore)
- plan 14 (StockMovement reversal SP) · plan 18 (sp_PaymentReverse) · plan 19 (mutabakat)
- `docs/sql/db_objects.sql` (tvf/vw OpenPO/SO) · `schema_StatusTransitions.sql` · `docs/sql/db_objects_reversal.sql`
- `.claude/rules/document-immutability.md` (POSTED→CANCELLED + child guard)
- `Lib/Dtos.cs` DocStatus sabitleri
