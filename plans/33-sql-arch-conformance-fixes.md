# Plan 33 — SQL/SP + Mimari Bütünlük Düzeltmeleri

**Durum:** Onay bekliyor
**Tier:** 3 (schema + SP + PageModel + yeni helper — çok dosya, ledger riski)
**Kaynak:** İki paralel denetim workflow'u (SP iş-doğruluğu + C# mimari uyum), 2026-06-04.
Her bulgu paralel `sql-sp-reviewer`/`code-reviewer` (opus) ile bulundu, 2 bağımsız refuter (opus+sonnet) ile adversarial doğrulandı. Aşağıdakiler **onaylanmış** bulgular.

---

## 1. Problem

İki katmanlı denetim ortak kök neden ortaya çıkardı: **SQL-First + immutability disiplini çekirdekte sağlam, kenarda drift etmiş.**

- Bazı canlı WMS ekranları (Putaway, Picking/Terminal) ledger tablosu `StockMovement`'a **C#'tan doğrudan** yazıyor → dönem kilidi (`sp_GuardPeriodOpen`) + immutability + Türkçe THROW bypass.
- Stok yazan 8 SP `BEGIN TRAN` açıyor ama TRY/CATCH+ROLLBACK+THROW yok → kısmi-yazma riski.
- Stok onay SP'lerinde idempotency guard yok → çift-post stok 2× hareketlendirir.
- `sp_CorrectPurchaseInvoiceLine` AccountMovement'ı yerinde UPDATE ediyor (kendi yorumuna rağmen) → VUK/e-defter ihlali.
- PO/SO/Shipping edit handler'ları immutability guard'sız (`DocumentLock` helper hiç yazılmamış).
- THROW kod ailesi dağınık (60xxx PageModel filtresi yakalamaz → kullanıcı Türkçe mesaj görmüyor).

---

## 2. Scope

**Dahil:** SP transaction normalizasyonu, idempotency guard, ledger immutability fix, Currency/tip fix, THROW kod hizalama, dönem-tarih simetrisi, terminal ekranlarını SP'ye taşıma, `DocumentLock` helper + edit guard'ları, DEAD/WIP servis kararı.

**Hariç:** MEDIUM/LOW birikimi (`SELECT *` ~22, magic-string ~14, timezone ~3) — ayrı temizlik turu (TODO debt). R0 ledger drift (AccountMovement COGS) — bilinçli karar, ayrı plan.

---

## 3. Bulgu Envanteri (kanıtlı)

### SP Katmanı (db_objects*.sql)

| ID | Önem | SP / Dosya | Sorun | Fix |
|---|---|---|---|---|
| **C3** | CRIT | `sp_CorrectPurchaseInvoiceLine` (docchain:704-706) | AccountMovement yerinde UPDATE — immutability ihlali | UPDATE→ters satır (Debit=@OldGrand) + yeni doğru satır |
| **H1** | HIGH | `sp_CorrectPurchaseInvoiceLine` (704-706) | UPDATE'te CompanyId predikası yok | `AND CompanyId=@CompanyId` (C3 ile birlikte) |
| **C1** | CRIT | 8 SP: ShippingPost, ShippingCreatePickTask, TransferPost, CycleCountPost, ProductionLoadBOM, ProductionCreatePickTask, ProductionFinish, PickLinePost | TRY/CATCH+ROLLBACK+THROW yok | `sp_ReceivingPost` desenine getir (db_objects.sql:272-375) |
| **C2** | CRIT | 5 SP: ShippingPost, TransferPost, CycleCountPost, ProductionFinish, PickLinePost | Çift-post koruması yok → stok 2× | Status guard `IF @Status='POSTED' THROW 50010` + StockMovement(SourceDocType,SourceDocId,...) idempotent UNIQUE |
| **H2** | HIGH | `sp_PurchaseInvoiceReverse` (586) | Currency hardcoded 'TRY' → döviz fatura nötrlenmez | Faturadan `@Currency` oku, ters satıra yaz |
| **H3** | HIGH | `sp_DepositCheque`(60001/2), `sp_ReturnCheque`(60004), `sp_PayLoanInstallment`(60010) | THROW 60xxx → PageModel filtresi (50000-59999) yakalamaz | 60xxx→50xxx, modül-bazlı tutarlı aile |
| **H4** | HIGH | `sp_ApprovePriceVariance` (starter:266-300) | `@CompanyId` parametresi yok → IDOR | `@CompanyId` ekle + her iki sorguya predikat (PO JOIN) |
| **H5** | HIGH | `sp_MaterialIssueReverse` (materialissue:138) | `CancelledBy=@UserId` TRY_CAST'siz (GUID vs NVARCHAR) | `CancelledBy=TRY_CAST(@UserId AS UNIQUEIDENTIFIER)` |
| **H6s** | HIGH | `tr_GuardPeriod_StockMovement` (M11:175-185) | dönem kontrolü GETUTCDATE (onay-anı), AccountMovement MovementDate → asimetri | StockMovement'a MovementDate kolonu + INSERTED.MovementDate ile kontrol, iki trigger simetrik |

### C# Katmanı (Features/*)

| ID | Önem | Dosya | Sorun | Fix |
|---|---|---|---|---|
| **AC1** | CRIT | `Transfer/Putaway.cshtml.cs:47-86` | C# transaction'ında StockTransfer POSTED + StockMovement INSERT, SP atlanmış | `sp_PutawayPost` (veya `sp_TransferPost('BIN_TO_BIN')`) SP'sine taşı |
| **AC2** | CRIT | `Picking/Terminal.cshtml.cs:60-112` | Atomik toplama + durum geçişi C#'ta orkestre | `sp_PickConfirm` SP'sine taşı (veya `sp_PickLinePost` barkod param ile genişlet) |
| **AC3** | CRIT | `Production/ProductionReceiptService.cs:115-143` | C#'ta StockMovement INSERT, şema-uyumsuz, DEAD/WIP (DI+caller yok — DOĞRULANDI) | Sil (caller yok) |
| **AH1** | HIGH | `PurchaseInvoices/Details.cshtml.cs:152-175` | tutar/KDV hesabı PageModel ham UPDATE | `sp_RecalcPurchaseInvoiceTotals` SP'sine taşı |
| **AH2** | HIGH | `Expenses/Details.cshtml.cs:115-138` | satır INSERT/DELETE + TotalAmount C# transaction | `sp_ExpenseInvoiceAddLine/DeleteLine` SP'leri |
| **AH3** | HIGH | `Transfer/Putaway.cshtml.cs:90` | `catch{rollback;throw}` log yok, ILogger yok | (AC1 ile çözülür — SP'ye taşıyınca catch normalize) |
| **AH4** | HIGH | `Transfer/Terminal.cshtml.cs:92` | boş-loglu generic catch | ILogger + SqlException ayrımı |
| **AH5** | HIGH | `Picking/Terminal.cshtml.cs:115` | SqlException/SP THROW ayrımı yok (log var) | (AC2 ile çözülür — SP'ye taşıyınca `when(Number 50000-59999)`) |
| **AH6** | HIGH | `PurchaseOrders/Details.cshtml.cs:174-178` | PO başlık edit durum/child guard'sız | `DocumentLock.PoHasReceiving` + Status guard |
| **AH7** | HIGH | `PurchaseOrders/Details.cshtml.cs:185-208` | `OnPostAddLineAsync` POSTED'a satır eklenebiliyor | handler başına guard |
| **AH8** | HIGH | `Production/ProductionActivityService.cs:54-67` | C#'ta StockMovement, DEAD/WIP, şema-uyumsuz | Sil (caller yok) |

---

## 4. Fazlar (önerilen sıra — düşük risk + yüksek getiri önce)

### Faz A — Ledger Immutability (VUK-bağlayıcı, lokalize)
- C3 + H1: `sp_CorrectPurchaseInvoiceLine` UPDATE→ters-kayıt + CompanyId.
- H2: `sp_PurchaseInvoiceReverse` Currency faturadan.
- H5: `sp_MaterialIssueReverse` TRY_CAST.
- **Kapanış:** sql-sp-reviewer + smoke (düzeltme faturası → cari ekstrede orijinal iz korunuyor + net=0).

### Faz B — Terminal/Putaway SP'ye Taşıma (mimari + idempotency + hata aynı anda)
- AC1: `sp_PutawayPost` yaz, Putaway.cshtml.cs C# INSERT'lerini kaldır → AH3 çözülür.
- AC2: `sp_PickConfirm` yaz, Picking/Terminal.cshtml.cs orkestrasyonunu kaldır → AH5 çözülür.
- AH4: Transfer/Terminal ILogger + SqlException ayrımı.
- **Kapanış:** sql-sp-reviewer + security-reviewer + smoke (putaway → StockMovement bakiye doğru, çift-tara reddedilir).

### Faz C — SP Transaction Normalizasyon + Idempotency
> DÜZELTME (2026-06-19, koddan doğrulandı — plan scope'u stale çıktı):
- **C2 ✅ (gerçek fix yapıldı):** Çift-post riski yalnız **sp_ProductionFinish + sp_PickLinePost**'taydı (UPDLOCK+guard yoktu). Eklendi: UPDLOCK + status/QtyPicked guard + TRY/CATCH. sql-sp-reviewer CRITICAL yok, smoke (COMPLETED emre finish→THROW 50010). commit.
  - ShippingPost (canlı=starter:1704)/TransferPost/CycleCountPost **zaten** UPDLOCK+sp_ValidateStatusTransition ile çift-post korumalıydı (POSTED→POSTED kuralı yok→THROW).
  - Plan'ın `StockMovement(SourceDocType,SourceDocId) UNIQUE` fikri **ÇALIŞMAZ** (belge çok-satırlı→o kolonlar tekil değil). Doğru mekanizma status guard.
- **C1 (8→5 SP, DÜŞÜK ÖNCELİK — ERTELENDİ):** sp_ShippingPost zaten hardened (starter). Kalan 5 (ShippingCreatePickTask/TransferPost/CycleCountPost/ProductionLoadBOM/ProductionCreatePickTask) TRY/CATCH'siz **ama `SET XACT_ABORT ON` zaten partial-write'ı auto-rollback ediyor** → consistency polish, kritik değil. ProductionFinish+PickLinePost C1 zaten yapıldı (C2 ile birlikte).
- **DEBT (ayrı):** PickLinePost ISSUE-stok yazması (sevkiyatta çift-sayım riski) + dönem-guard eksikliği.
- **Kapanış:** sql-sp-reviewer + smoke ✅ (yapılan 2 SP için).

### Faz D — DocumentLock Helper + Edit Guard ✅ (2026-06-19)
- `Lib/DocumentLock.cs` ✅ — 4 async helper (PO→Receiving, Receiving→PurchaseInvoice, SO→Shipping, Shipping→SalesInvoice). Rule §7 imzası uyarlandı (ExpenseInvoice değil PurchaseInvoice — §7 stale çıktı, koddan düzeltildi).
- Guard wire ✅: PO/SO/Shipping edit + PO/SO/Receiving add-line + Receiving edit.
- **Kapanış ✅:** code-reviewer (2 bulgu CRITICAL+HIGH düzeltildi) + smoke (faturalı receiving→guard true, build 0/0). commit.
- **DEBT:** CycleCount guard (count-immutability ayrı, status-bazlı child değil) eklenmedi — gerçek child yok; Shipping OnPostAddLine 93-satır (pre-existing) split.

### Faz E — THROW Kod Hizalama + DEAD Servis Temizliği
- H3: 60xxx→50xxx (DepositCheque/ReturnCheque/PayLoanInstallment).
- AC3 + AH8: `ProductionReceiptService.cs` + `ProductionActivityService.cs` sil (DynamicBomService de değerlendir).
- **Kapanış:** build-validator + grep (kalan 60xxx yok, dead servis referansı yok).

### Faz F — Dönem-Tarih Simetrisi (en büyük — şema değişikliği)
- H6s: StockMovement.MovementDate kolonu + trigger INSERTED.MovementDate.
- **Risk:** Mevcut StockMovement satırlarına MovementDate backfill (CreatedAt'tan).
- **Kapanış:** sql-sp-reviewer + smoke (kapalı döneme geriye-dönük stok → THROW).

---

## 5. Alternatifler (reddedilen)

1. **Hepsini tek commit'te düzelt** — RED: ledger SP'leri yüksek risk, faz-faz smoke gerekli; tek dev review yükü taşınamaz.
2. **Terminal ekranlarını C#'ta bırak + sadece guard ekle** — RED: SQL-First architecture.md §4 ihlali kalıcı olur; dönem kilidi/immutability bypass devam eder. SP'ye taşıma tek doğru çözüm.
3. **DEAD servisleri SP'ye taşı (silme yerine)** — RED: caller/DI yok, planlanmış feature yok. YAGNI — sil; WIP atölye terminali planlanırsa o zaman SP olarak yaz.

---

## 6. Riskler

- **Ledger SP değişikliği canlı veri bozabilir** → her faz ayrı smoke + reversal net=0 doğrulaması.
- **StockMovement.MovementDate backfill** → Faz F en sona, ayrı dikkatli migration.
- **Idempotent UNIQUE index mevcut çift kayıt varsa migration patlar** → önce duplicate tara (CLI query).
- **Terminal SP'ye taşıma davranış değiştirebilir** → mevcut C# mantığını birebir SP'ye port, smoke karşılaştır.

## 7. Done Criteria

- [ ] 3 CRITICAL SP + 3 CRITICAL C# bulgusu kapalı (kanıt: file:line + smoke).
- [ ] 6 HIGH SP + 8 HIGH C# bulgusu kapalı veya bilinçli ertelendi (TODO debt).
- [ ] `DocumentLock.cs` mevcut + en az PO edit/addline guard'lı.
- [ ] DEAD üretim servisleri silindi, build 0/0.
- [ ] Kalan 60xxx THROW yok.
- [ ] Her faz: build-validator + ilgili reviewer + smoke geçti.

## 8. Rollback

Her faz ayrı commit (plan:33 referans). Faz geri alınması = o commit revert. Ledger SP'leri `CREATE OR ALTER` — önceki sürüm git'te.

---

## 5 Lens Kontrolü

- 🔴 **Contrarian:** Fatal flaw — terminal SP'ye taşırken mevcut C# davranışı birebir korunmazsa sessiz regresyon. Mitigasyon: smoke karşılaştırma.
- 🔵 **First Principles:** Doğru soru "neden ledger C#'ta yazılmış" — hızlı terminal eklerken SP yazma maliyetinden kaçınılmış. Kök çözüm = SP zorunluluğunu CI guard'a bağla (gelecek).
- 🟢 **Expansionist:** Daha büyük fırsat — `scan-isolation` benzeri "ham StockMovement INSERT C#'ta" statik guard yazılabilir, drift'i kalıcı önler.
- ⚪ **Outsider:** Yabancı "aynı iş hem C#'ta hem SP'de iki kez yazılmış (Picking)" garip bulurdu — kod tekrarı + tutarsızlık.
- 🟡 **Executor:** Pazartesi sabahı Faz A (lokalize, VUK-kritik) ile başla.
