# Plan 09 — Cari Hesap Defteri (AccountMovement Subsidiary Ledger)

> Tier 3. Finans çekirdeği — tek doğru kaynak (single source of truth).

**Tarih:** 2026-05-29
**Yazan:** Fikri / Claude
**Durum:** `Uygulamada`
**Modül:** M11 (Finans) + M03/M04 (fatura) entegrasyon
**Paket:** STARTER

---

## 1. Problem

Cari bakiye/ekstre bugün **iki kopuk kaynaktan** üretiliyor: KPI (Toplam Borç/Alacak/Net) `tvf_PaymentPlanAging` → `PaymentPlan`'dan; ekstre hareketleri ise `SalesInvoice + ExpenseInvoice + FinancialTransaction` UNION'undan. Bu ikisi birbirini tutmuyor:

- SUP-001'de KPI **78.500 borç** gösteriyor ama ekstrede tek **35.000 ödeme** var, **DEVİR 0** — çünkü borcu doğuran fatura PaymentPlan'da var, ExpenseInvoice'ta yok (veya ters). Kaynaklar bağdaşmıyor.

Stok tarafında bu sorun yok çünkü her hareket tek `StockMovement` defterine yazılıyor; bakiye = `SUM`. Cari tarafında karşılığı yok. **Çözüm: cari için de tek hareket defteri** (`AccountMovement`), tıpkı StockMovement gibi.

## 2. Scope

### Kapsam dahili
- **`AccountMovement` tablosu** — her cari hareketinin immutable kaydı (Borç/Alacak, tarih, kaynak belge).
- **Posting entegrasyonu** — fatura onayı, ödeme/tahsilat, çek tahsili her POST'ta defter satırı yazar (StockMovement gibi, aynı transaction).
- **Backfill** — mevcut SalesInvoice/ExpenseInvoice/FinancialTransaction kayıtları tek seferlik deftere taşınır (idempotent).
- **Repoint** — cari bakiye (KPI), ekstre, DEVİR → `AccountMovement`'tan okunur. Plan 08 UNION ekstresi bunun yerine geçer.
- **Sabitler** — `AccountMovementType` (Dtos.cs): SALES_INVOICE, PURCHASE_INVOICE, PAYMENT, COLLECTION, CHEQUE_IN, CHEQUE_OUT, OPENING, VARIANCE, REVERSAL.

### Kapsam dışı
- **Yaşlandırma (aging)** PaymentPlan'da kalır (vade *planı* ≠ defter). Faz 5'te tutarlılık kontrolü; tam migrasyon ayrı iş.
- Çoklu para birimi yeniden değerleme (kur farkı) — AmountTRY tutulur, revaluation ayrı.
- Muhasebe fiş entegrasyonu (Plan 06 analitik muhasebe) — ayrı.

### İşaret konvansiyonu (kilit)
Tek cari hesap. `NetBakiye = SUM(Borc) - SUM(Alacak)`. **Pozitif = cari bize borçlu (alacağımız), negatif = biz cariye borçluyuz.**

| Olay | Borç | Alacak |
|---|---|---|
| Satış faturası (müşteri bize borçlanır) | ✓ | |
| Tahsilat (müşteriden para) | | ✓ |
| Alış faturası (biz tedarikçiye borçlanırız) | | ✓ |
| Ödeme (tedarikçiye para) | ✓ | |

(Mevcut UNION ekstre konvansiyonuyla aynı — devamlılık.)

### Etkilenen dosyalar
- `docs/sql/schema_M11_AccountMovement.sql` — YENİ tablo + index
- `docs/sql/db_objects_starter.sql` — posting SP'lerine insert; `tvf_PartnerBalance` / `tvf_AccountLedger`
- `docs/sql/migrate_backfill_accountmovement.sql` — tek seferlik backfill
- `src/Operax.Web/Lib/Dtos.cs` — `AccountMovementType` sabitleri
- `Features/MasterData/Partners/Details.cshtml.cs` — ekstre/KPI defterden
- Fatura/ödeme posting noktaları (SP veya PageModel)

**Tahmini:** ~10-14 dosya, çok fazlı.

## 3. Alternatifler

### A: Mevcut UNION sorgusunu düzelt (defter yok)
**Açıklama:** Ekstre UNION'una eksik kaynakları ekle, KPI'yı da aynı UNION'dan üret.
**Reddetme:** Her yeni belge tipi (çek, senet, kredi) UNION'a el ekleme gerektirir; N kaynak × her ekran = kırılgan, yavaş, tekrar tutmama riski sürekli geri gelir. Kök neden çözülmez.

### B: PaymentPlan'ı tek kaynak yap
**Açıklama:** Her şeyi PaymentPlan'a yaz.
**Reddetme:** PaymentPlan vade *planı* (gelecek taksitler); gerçekleşen hareket defteri değil. İkisi farklı kavram, karıştırmak vade analizini bozar.

### C: (seçilen) AccountMovement subsidiary ledger
**Açıklama:** StockMovement muadili tek immutable cari defter; her POST yazar; bakiye/ekstre/aging buradan türer.
**Sebep:** SQL-First + StockMovement pattern'i ile birebir tutarlı, tek doğru kaynak, denetlenebilir, yeni belge tipi sadece "deftere yaz" der. Ekstre ile bakiye matematiksel olarak daima tutar.

**5 lens:**
- 🔴 **Contrarian:** Backfill yanlışsa tüm bakiyeler bozulur → backfill idempotent + doğrulama sorgusu (defter toplamı == eski KPI) zorunlu, prod'a almadan reconcile.
- 🔵 **First Principles:** Bakiye = hareketlerin toplamı olmalı; ayrı "özet" kaynağı tutmak antipattern. Defter tek gerçek.
- 🟢 **Expansionist:** Aynı defter ileride muhasebe fişi (Plan 06), risk limiti gerçek-zamanlı kontrol, nakit akış projeksiyonu için temel olur.
- ⚪ **Outsider:** "Cari bakiyeyi 4 tablodan topluyorsunuz?" → standart ERP'de tek cari defter (Logo: CLFLINE, SAP: BSID/BSAD). Endüstri normu bu.
- 🟡 **Executor:** Pazartesi: tablo + backfill + bakiye/ekstre repoint. Posting entegrasyonu fatura akışı başına eklenir.

## 4. Riskler

| Risk | Etki | Olasılık | Mitigation |
|---|---|---|---|
| Backfill yanlış → bakiye bozulur | yüksek | orta | İdempotent (SourceDocType+SourceDocId unique); reconcile sorgusu eski KPI ile; rollback = TRUNCATE + yeniden |
| İşaret konvansiyonu hatası | yüksek | orta | Tablo (§2) net; test senaryosu her belge tipi için |
| Posting noktası atlanır → hareket deftere düşmez | orta | orta | Her POST SP'sinde insert; eksikse reconcile sorgusu yakalar |
| Çift kayıt (POST + backfill) | orta | düşük | Backfill yalnızca defterde yokken; POST sonrası backfill çalıştırılmaz |
| PaymentPlan ile aging tutarsızlığı | düşük | orta | Faz 5 reconcile; vade planı ayrı kavram olarak korunur |

## 5. Done Criteria

- [ ] `AccountMovement` tablosu + index (PartnerId, MovementDate)
- [ ] Backfill: mevcut fatura+ödeme deftere taşındı, **reconcile sorgusu: defter net bakiye == eski tvf_PaymentPlanAging net** (tüm cariler)
- [ ] Posting: fatura onayı + ödeme/tahsilat deftere yazıyor (yeni hareket KPI+ekstreye anında yansıyor)
- [ ] Cari kart KPI + ekstre + DEVİR `AccountMovement`'tan; SUP-001'de bakiye ekstreyle **tutuyor**
- [ ] Cancel → ters kayıt (REVERSAL), satır silinmiyor (evrak bütünlüğü)
- [ ] `operax-cli migrate` 0 hata · `dotnet build` 0 hata 0 uyarı
- [ ] Inline style/Türkçe/SQL kuralları korunur

## 6. Rollback

- Tablo additive → `DROP TABLE AccountMovement` + repoint commit revert → eski UNION ekstre geri gelir.
- Backfill yanlışsa `TRUNCATE AccountMovement` + düzeltip yeniden çalıştır (POST entegrasyonu öncesi güvenli).
- Posting insert'leri ayrı commit → tek tek revert.

## 7. Adımlar / Fazlar

### Faz 1 — Şema + sabitler
1. [ ] **AM-1** `schema_M11_AccountMovement.sql` — tablo + index + FK(Partner)
2. [ ] **AM-1** `Dtos.cs` `AccountMovementType` sabitleri
3. [ ] **AM-1** `tvf_PartnerBalance(@CompanyId)` + `tvf_AccountLedger(@CompanyId,@PartnerId,@From,@To)` (devir + hareketler)

### Faz 2 — Backfill + doğrulama
4. [ ] **AM-2** `migrate_backfill_accountmovement.sql` — SalesInvoice/ExpenseInvoice/FinancialTransaction → AccountMovement (idempotent)
5. [ ] **AM-2** Reconcile sorgusu: defter net == eski KPI net (cari bazında fark raporu)

### Faz 3 — Posting entegrasyonu
6. [ ] **AM-3** Fatura onayı (Sales/Expense) POST → AccountMovement insert (aynı transaction)
7. [ ] **AM-3** Ödeme/Tahsilat (FinancialTransaction create) → AccountMovement insert
8. [ ] **AM-3** Cancel → REVERSAL ters kayıt

### Faz 4 — Repoint (Plan 08 ekstre buraya bağlanır)
9. [ ] **AM-4** Cari kart KPI + ekstre + DEVİR → `tvf_AccountLedger`/`tvf_PartnerBalance`
10. [ ] **AM-4** Yaşlandırma raporu bakiye kaynağı gözden geçir (PaymentPlan vade'de kalır)

### Faz 5 — Tutarlılık + cleanup
11. [ ] **AM-5** PaymentPlan ↔ defter reconcile kontrolü (uyumsuzluk uyarısı)
12. [ ] **AM-5** journal + TODO + plan arşivle

> Plan 08 ekstre (UNION) işi Faz 4'te bu deftere repoint edilince tamamlanır.

## 8. İlişkili

- `.claude/rules/architecture.md` §4 (SQL-First, atomik POST) · §3 (StockMovement pattern)
- `.claude/rules/document-immutability.md` (cancel → ters kayıt)
- `[[open-orders-not-in-ledger]]` — sipariş deftere yazılmaz (taahhüt, hareket değil)
- Plan 08 — cari kart ekstre tabı (bu deftere bağlanacak)
- Plan 06 — analitik muhasebe (ileride aynı defteri tüketir)

## 9. Onay

- [x] Plan kullanıcıya gösterildi
- [x] Geri bildirim alındı
- [x] Onay alındı: 2026-05-29, Fikri
