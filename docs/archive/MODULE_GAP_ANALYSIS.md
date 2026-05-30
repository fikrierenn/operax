# Operax — Modül Gap Analizi (Tam Tarama)

> Tarih: 2026-05-28
> Yöntem: 4 paralel Explore agent ile tüm `Features/` modülleri tarandı (sayfa varlığı, CRUD tamlığı, OnPost handler, SP coverage).
> Amaç: Modül modül tamamlama önceliği belirlemek.

---

## Yönetici Özeti

**Beklenenden iyi:** WMS (Picking/Transfer/CycleCount), Üretim (M10 tam — BOM NCalc + terminal + rota), Muhasebe (Expenses), Bütçe (Budget), Dashboard zaten **tam çalışıyor**.

**Asıl boşluk STARTER paketinin küçük parçalarında:**
1. 🔴 **M11 Finance** — veri girişi (Create) formları HİÇ YOK
2. 🟡 **Ortak evrak** — satır silme + Cancel handler'ları eksik (PO/SO/Receiving/Shipping)
3. 🟡 **M01 Partners** — risk/vade/eFatura alanları UI'da yok (şemada var)
4. 🟡 **M00 Admin** — Parameters/StatusTransitions/Settings/Companies CRUD eksik

**Hiç yok (placeholder):** Reports, Project, Service, Integration, Incentives (STARTER dışı, gelecek faz).

---

## Modül × Durum Matrisi

### STARTER Paketi (M00-M04 + M11)

| Modül | Ekran | Liste | Yeni | Detay | Düzenle | Sil | Onayla | İptal | Durum |
|---|---|---|---|---|---|---|---|---|---|
| **M00** | Users | ✅ | ✅ | — | ✅ | ❌ | — | — | ✅ Tam |
| M00 | Roles | ✅ | ✅ | — | ❌ | ✅ | — | — | ✅ Yeterli |
| M00 | Dictionary | ✅ | ✅(değer) | ✅ | ❌ | ❌ | — | — | ⚠️ Edit eksik |
| M00 | **Parameters** | ✅ | ❌ | — | ❌ | ❌ | — | — | 🔴 Salt liste |
| M00 | Modules | ✅ | — | — | ✅(toggle) | — | — | — | ✅ Yeterli |
| M00 | **StatusTransitions** | ✅ | ❌ | — | ❌ | ❌ | — | — | 🔴 Salt liste |
| M00 | AuditLog | ✅ | — | — | — | — | — | — | ✅ (read-only by design) |
| M00 | **Settings** | ⚠️ boş | — | — | ❌ | — | — | — | 🔴 İçerik yok |
| M00 | **Companies** | ❌ | ❌ | ❌ | ❌ | — | — | — | 🔴 Klasör yok |
| **M01** | Items | ✅ | ✅ | ✅ | ✅ | ❌ | — | — | ✅ Tam (UOM+barkod alt liste var) |
| M01 | **Partners** | ✅ | ✅ | ✅ | ⚠️ | ❌ | — | — | 🟡 risk/vade/eFatura alanları yok |
| M01 | Locations(Bins) | ✅ | ❌ | — | ❌ | ❌ | — | — | ⚠️ read-only |
| **M02** | Balance | ✅ | — | — | — | — | — | — | ✅ (read-only by design) |
| M02 | Movements | ✅ | — | — | — | — | — | — | ✅ (read-only by design) |
| **M03** | PO List | ✅ | — | — | — | — | — | — | ✅ |
| M03 | PO Details | — | ✅ | ✅ | ✅ | 🟡 satır sil yok | ✅ | ✅ | 🟡 satır silme yok |
| M03 | Receiving | ✅ | ✅ | ✅ | ✅ | 🟡 | ✅ | 🔴 Cancel yok | 🟡 |
| M03 | PriceVariances | ✅ | — | — | — | — | ✅ | ✅(reject) | ✅ Tam |
| **M04** | SO List/Details | ✅ | ✅ | ✅ | 🟡 | ✅ | ✅ | ✅ | 🟡 satır silme yok |
| M04 | Shipping | ✅ | ✅ | ✅ | 🟡 | ✅ | ✅ | 🔴 Cancel yok | 🟡 |
| M04 | SalesInvoices | ✅ | ⚠️(otomatik) | ✅ | — | — | — | — | ⚠️ manuel create yok (InvoiceMode=INSTANT) |
| **M11** | Accounts | ✅ | 🔴 | ✅ | — | — | — | — | 🔴 Create yok |
| M11 | Cheques/Notes | ✅ | 🔴 | ✅ | — | — | (statü) | — | 🔴 Create yok, senet SP yok |
| M11 | Loans | ✅ | 🔴 | ✅ | — | — | (öde) | — | 🔴 Create yok (sp_CreateLoan hazır!) |
| M11 | CreditCards | ✅ | 🔴 | ✅ | — | — | — | — | 🔴 Create + ekstre kapat/öde yok |
| M11 | PaymentPlan | ✅ | — | ❌ | — | — | — | — | ⚠️ Details yok |
| M11 | Aging | ✅ | — | ❌ | — | — | — | — | ⚠️ drill-down yok |
| M11 | Snapshot | ✅ | — | — | — | — | — | — | ✅ Tam |
| M11 | **Payments** | ❌ | ❌ | — | — | — | — | — | 🔴 Ödeme kaydet yok (sp hazır!) |

### WMS_PRO Paketi (STARTER dışı — ZATEN TAM)

| Modül | Durum |
|---|---|
| M06 Picking (Index+Details+Terminal, sp_PickLinePost) | ✅ Tam |
| M07 Transfer (Index+Details+Terminal+Putaway+Replenishment, sp_TransferPost) | ✅ Tam |
| M08 CycleCount (Index+Details+Terminal, sp_CycleCountPost) | ✅ Tam |
| M09 LPN/Lot/Serial | ⚠️ Read-only (AutoTraceabilityService ile sistem üretir — tasarım) |

### MANUFACTURING + diğer (STARTER dışı — ZATEN TAM)

| Modül | Durum |
|---|---|
| M10 Production (Order+Terminal+BOM NCalc+WorkCenters+Routes, sp_ProductionLoadBOM/CreatePickTask/Finish) | ✅ Tam |
| M15 Dashboard (tüm metrik DB'den, hardcoded yok) | ✅ Tam |
| Expenses (Muhasebe — CRUD + DRAFT→POSTED→PAID) | ✅ Tam |
| Budget (Bütçe + 30 gün nakit tahmini) | ✅ Tam |

### Placeholder (boş klasör — gelecek faz)

Reports, Project, Service, Integration, Incentives — 0 işlevsellik.

---

## Tamamlama Önceliği (Modül Modül)

Kullanıcı kararı: "Bir modülü tamamla, sonra diğerine geç."

### 🥇 Plan 02 — M11 Finance Create + Eksik SP'ler
**En büyük boşluk: kullanıcı finans verisi ekleyemiyor.**
- Hesap aç formu (kasa/banka)
- Çek/Senet girişi formu (alınan/verilen)
- Kredi aç formu (sp_CreateLoan zaten hazır — 7 tip)
- Kredi kartı tanımı formu
- Ödeme/Tahsilat kaydet ekranı (sp_RecordPaymentAndAutoClose hazır)
- Senet SP'leri: sp_DepositNote/CollectNote/ReturnNote
- Kart ekstre: sp_CloseStatement + sp_PayCreditCardStatement
- Bonus: sp_EndorseCheque (ciro), virman, tvf_CashProjection

### 🥈 Plan 03 — Evrak Bütünlüğü + Ortak Eksikler
- PO/SO/Receiving/Shipping: satır silme handler'ı
- Receiving/Shipping: Cancel handler (ters StockMovement)
- PO/SO Cancel: sp_PoCancel/sp_SoCancel SP'ye taşı (sp_ValidateStatusTransition)
- CRIT-1: SP THROW catch wrap (tüm post handler'lar)
- CRIT-2: _PageHeader.Sub XSS fix
- CRIT-3: "APPROVED" magic string
- CRIT-4: ILogger<T> DI eksik PageModel'ler

### 🥉 Plan 04 — M01 Partners + M00 Admin Tamamlama
- Partners Edit: risk/vade/eFatura sekmeleri (RiskScore, PaymentTermDays, CreditLimit, EFaturaMukellef, DefaultPaymentMethod)
- Parameters: Create/Edit/Delete handler
- StatusTransitions: Create/Edit handler
- Settings: parametre alanları + OnPost
- Companies: yeni Admin/Companies CRUD klasörü

### Plan 05+ (gelecek faz, STARTER sonrası)
- Reports modülü (ReportsScreen + ReportView)
- e-Belge inbound sync (M16)
- Service/RMA, Project, Incentives

---

## STARTER "Bitti" Tanımı

STARTER paketi şu 3 plan tamamlanınca canlıya hazır:
- [ ] Plan 02 — M11 Finance Create + SP
- [ ] Plan 03 — Evrak bütünlüğü + CRIT bulguları
- [ ] Plan 04 — Partners risk + Admin tamamlama

Sonra E2E test: PO → Receiving → ItemCost → SO → Shipping → Invoice → Tahsilat → Mali Durum.
