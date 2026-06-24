# Plan 54 — M3 PurchaseOrders Revizyon (kod + UI + mali + CLOSED statü)

**Durum:** AKTİF · onay bekliyor
**Tier:** 3 (statü modeli + SP + StatusTransition + immutability + UI + mali, çok dosya)
**Tarih:** 2026-06-24
**Bağlam:** Ticari track Faz 1 (MasterData/Plan 50 kapandı). 5 danışman raporu: code-reviewer (14 bulgu), erp-isleyis-danismani (CLOSED boşluğu + immutability + PaymentPlan), competitor-analyst (parite), UX trio, mali (mali-evrak + mali-islem).

---

## 1. Problem

PurchaseOrders modülü çalışıyor ama: (a) kod kuralı ihlalleri (magic string, eksik try/catch, SQL filter interpolation), (b) UI standardı dışı (inline layout, mobil çöküş, eksik satır-sil/vade-autofill), (c) **mali risk** — PO PaymentPlan'ın ledger/aging'e sızma + iptal hayalet-borç ihtimali (DOĞRULANACAK), (d) **CLOSED statü dead** — POSTED PO sonsuza dek açık görünüyor; 100/100 teslim alınmış PO ile hiç alınmamış ayırt edilemiyor.

## 2. Scope

**Faz 1 — Kod fix (code-reviewer doğruladı):**
- `SourceDoc.PurchaseOrder = "PURCHASE_ORDER"` sabiti ekle → Details D-7 + PriceVariances P-2/P-3 magic string'leri değiştir.
- PriceVariances `'REJECTED'` → `DocStatus.Rejected` (P-1).
- PriceVariances `OnPostApprove`/`OnPostReject` try/catch + ILogger (P-4/P-5) + AntiForgery doğrula/ekle.
- `sp_ApprovePriceVariance` CompanyId param (P-7 — SP imzası doğrula, eksikse ekle).
- Index `{filter}` string-interpolation → güvenli pattern (I-1/2/3; filter sabit, gerçek injection yok ama kural).
- Details guard clause: AddLine qty>0/itemId guard + OnPost ModelState (D-9/D-10).

**Faz 2 — Mali doğrulama + fix (kır varsa):**
- C-A: aging/mutabakat TVF/SP'leri `SourceDocType='PO'` hariç tutuyor mu (sipariş borç doğurmaz).
- C-D: `sp_GeneratePaymentPlanFromPO` AccountMovement YAZMIYOR doğrula (yazıyorsa ledger ihlali — kaldır).
- C-E: PO→ExpenseInvoice çift-PaymentPlan riski (transfer vs yeniden-üret) — mevcut akış doğrula.
- C: `sp_PoCancel` açık PaymentPlan'ı CANCELLED yapıyor mu (hayalet borç) — yoksa cascade ekle.

**Faz 3 — CLOSED gerçek statü (Tier 3 çekirdek):**
- `sp_ReceivingPost` (veya fatura SP): son kabulde `SUM(QtyReceived) >= SUM(QtyOrdered)` → PO `Status = CLOSED`.
- `sp_PoCancel` / yeni "kalanı kapat" handler: kısmi teslim + kalan iptal → `CLOSED_PARTIAL`.
- `sp_ValidateStatusTransition` PURCHASE_ORDER seed: POSTED→CLOSED, POSTED→CLOSED_PARTIAL, POSTED→CANCELLED.
- `document-immutability.md §3` matrise CLOSED/CLOSED_PARTIAL satırı (salt-görüntüleme).
- UI: Index + Details durum rozeti CLOSED/CLOSED_PARTIAL (UiHelpers.StatusBadge + Türkçe "Tamamlandı"/"Kısmi Kapandı").

**Faz 4 — UI standardı + UX (referans: Expenses/Details):**
- Details: `class="doc-layout"` + `form-grid` + `table-scroll` (mobil çöküş fix).
- Satır-sil butonu (DRAFT), tedarikçi→vade autofill (JS), "İptal Et" confirm, kalıcı satır-ekle formu.
- inline layout style → Tailwind utility (Index/Details/PriceVariances).
- Details 407→<300 split (DTO/handler partial — Plan 50 pattern'i).

**HARİÇ (TODO'ya gap):** RFQ/teklif · satınalma talebi (PR) · onay workflow · blanket order · çoklu para · tedarikçi skoru · drop-ship · MRP→PO. (competitor gap — ayrı planlar.)

## 3. Alternatifler (reddedilen)
- **CLOSED türetilmiş rozet (statü yok):** reddedilen (kullanıcı gerçek statü istedi) — POSTED kalır, UI hesaplar. Daha dar ama statü ayrımı kalıcı değil.
- **Hepsini tek faz:** reddedilen — CLOSED schema/SP'yi koddan/UI'dan izole et, faz başına kapanış kapısı.
- **PriceVariance mantığına dokun:** reddedilen — M02 Costing işi (PO↔liste fiyatı, PO↔fatura değil); PO sadece tetikleyici.

## 4. Riskler
| Risk | Etki | Mitigasyon |
|---|---|---|
| CLOSED SP değişikliği mevcut POSTED PO'ları bozar | orta | Idempotent; sadece yeni kabul/iptal'de set; geçmiş POSTED'lar dokunulmaz |
| PaymentPlan ledger sızması zaten varsa (Faz 2'de bulunursa) | yüksek | fresh-DB + smoke ile aging'de PO yok doğrula; bulgu → fix |
| StatusTransition seed eksik → CLOSED geçişi THROW | orta | seed + smoke (POSTED→CLOSED geçişi test) |
| Details split davranış değiştirir | düşük | partial salt-taşıma (Plan 50 pattern), build+smoke |
| immutability matrix CLOSED satırı eksik → kilitli PO editlenir | orta | §3 matris + SP guard |

## 5. Done Criteria
- [x] ✅ Faz 1 (2026-06-24): SourceDoc.PurchaseOrder sabiti + magic string 0 (Details Cancel + PriceVariances) · PriceVariances 2 handler try/catch+ILogger (AntiForgery zaten auto-inject) · sp_ApprovePriceVariance @CompanyId (IDOR) · Details AddLine qty/itemId guard + OnPost ModelState guard + LoadFormDropdownsAsync helper. **filter {…} I-1/2/3 ATLANDI** (sabit param-fragman, gerçek injection yok). build 0/0 · fresh-DB 0 fail (SP 3 param) · code+sql-sp reviewer temiz (7 önceki kapandı) · smoke (3 sayfa 200, PriceVariances query, AddLine qty=0 red).
- [x] ✅ Faz 2 (2026-06-24): tvf_PaymentPlanAging + **tvf_FinancialPosition** (sql-sp-reviewer CRIT-1 yakaladı) PO/SO PaymentPlan hariç (açık sipariş ledger-dışı) · OnPostCancel PaymentPlan cascade + **transaction sarması** (IMP-1) · C-D sp_GeneratePaymentPlanFromPO AccountMovement yazmıyor (teyit) · tvf_OpenItemAging AM-bazlı PO-free. Smoke: aging 273600→195600, snapshot borç 289700 (PO 78000 çıktı), cascade dry-run 1 satır. **C-E (invoice post PO planını iptal etmiyor, çift estimate+actual) → TODO** (C-A/CRIT-1 ile aging+snapshot zaten temiz; kalan kozmetik liste + kısmi-fatura kararı PurchaseInvoices modülünde).
- [x] ✅ Faz 3 (2026-06-24): sp_ReceivingPost tam-kabulde PO→CLOSED (eski RECEIVED→CLOSED) · OnPostCloseRemaining POSTED→CLOSED_PARTIAL + PaymentPlan cascade (transaction) · StatusTransition seed (platform fix a0c0609: sistem-fallback + eksiksiz set) · immutability matris CLOSED/CLOSED_PARTIAL satırları · UI: Kalanı Kapat butonu + CLOSED flow + Dict badge (Kapandı/Kısmen Kapandı) + cancel/close confirm. build 0/0 · fresh-DB · sql-sp+code reviewer (Approved false-pos red, CLOSE_PARTIAL renk fix) · smoke: auto-CLOSE dry-run + CloseRemaining E2E browser (badge "Kısmen Kapandı").
- [ ] Faz 4: Details doc-layout/form-grid/table-scroll · satır-sil · vade autofill · confirm · <300 split. screenshot + smoke.
- [ ] HARİÇ gap'ler TODO'ya dokümante.
- [ ] Plan arşive + journal.

## 6. Faz sırası
1. **Faz 1 (kod)** önce — izole, düşük risk, pattern oturur.
2. **Faz 2 (mali doğrulama)** — Faz 3 öncesi ledger doğruluğu netleşmeli (CLOSED'ı mali doğru zemine kur).
3. **Faz 3 (CLOSED statü)** — schema/SP/seed/immutability/UI; en yüksek risk, fresh-DB.
4. **Faz 4 (UI/UX/split)** — en son, görsel + regresyon düşük.

## 7. Rollback
- Faz başına ayrı commit → git revert. CLOSED SP değişikliği: eski SP CREATE OR ALTER geri yükle. Statü sabitleri zaten tanımlı (şema değişmez, sadece SP set eder).

## 8. 5 Lens
- 🔴 **Contrarian:** Fatal flaw? CLOSED statü mevcut Receiving/fatura SP'lerine girer — çift-sayım/yanlış-kapanış riski. Mitigasyon: Faz 2 mali doğrulama önce + smoke net bakiye.
- 🔵 **First Principles:** "PO doğru modelleniyor + mali-nötr mü?" — CLOSED eksik + PaymentPlan sızma şüphesi. Önce bunlar.
- 🟢 **Expansionist:** RFQ/PR büyük fırsat ama ayrı plan — PO'yu şişirme.
- ⚪ **Outsider:** "Teslim alınmış sipariş hâlâ 'açık' görünüyor" garip — CLOSED bunu çözer.
- 🟡 **Executor:** Pazartesi: SourceDoc sabiti + PriceVariances try/catch → mali grep doğrulama → CLOSED SP.

## 9. İlişkili
- `.claude/rules/document-immutability.md` §2.1/§2.5/§3/§6 (PO zinciri, satır kilidi, matris, cancel)
- `.claude/rules/phase-review-gate.md` §3.5 (CLOSED schema → fresh-DB ritüeli)
- MEMORY: open-orders-not-in-ledger (PaymentPlan PO ledger-dışı)
- `docs/MODULE_SPECS/M03_Purchasing_Extended.md` (RFQ/PR gap kaynağı)
- Referans UI: `Features/Expenses/Details.cshtml` (doc-layout/form-grid/table-scroll/vade-autofill)
