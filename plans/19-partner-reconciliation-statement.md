# Plan 19 — Cari Mutabakat Turu / Mutabakat Mektubu (PartnerReconciliationLog)

**Tarih:** 2026-06-01 · **Durum:** `Taslak (onay bekliyor)` · **Modül:** M11 · **Kaynak:** TTK md.94 + plan 18 kapsam-dışı devamı

> **AYRIM — KARIŞTIRMA:** Bu plan **mutabakat turu** (partner'a bakiye gönder, onay/itiraz al).
> Plan 18 `AccountReconciliation` ise **açık-kalem kapama** (hangi tahsilat hangi faturayı kapattı).
> İkisi farklı domain: kapama = iç defter; mutabakat = dış partner onayı (TTK md.94).

---

## 1. Problem

Partner kartında "en son ne zaman mutabakat yapıldı, hangi bakiye onaylandı" bilgisi yok.
TTK md.94: hesap devresi sonunda (default takvim yılı sonu) bakiye cetveli gönderilir;
karşı taraf **1 ay içinde** noter/iadeli/KEP ile itiraz etmezse bakiye **kesinleşir** (sessiz onay).

Operax bunu izlemiyor → "bu cariyle en son mutabakat ne zaman, bakiye tuttu mu?" sorusu cevapsız.

## 2. Scope

### Dahili
- **`PartnerReconciliationLog` tablosu (append-only):**
  - `Id, CompanyId, PartnerId`
  - `StatementDate` — bakiye cetveli kesim tarihi (mutabakat dönemi sonu)
  - `BalanceSnapshot` — o tarihteki net bakiye (`SUM(Debit-Credit) <= StatementDate`)
  - `Status` — SENT / CONFIRMED / DISPUTED / EXPIRED_CONFIRMED (1 ay sessiz onay)
  - `SentAt, SentChannel` (KEP/NOTER/EMAIL/POST)
  - `ResponseAt, ResponseNote` — onay/itiraz
  - `DeadlineAt` = SentAt + 1 ay (TTK md.94 sessiz onay penceresi)
  - `CreatedAt, CreatedBy` (append-only — `IsDeleted` yok, TTK md.82)
- **`sp_CreateReconciliationStatement`:** bakiye snapshot al + SENT kaydı oluştur (sp_GuardPeriodOpen)
- **`sp_RespondReconciliation`:** CONFIRMED / DISPUTED işle
- **Partner kartı tab:** son mutabakat tarihi + bakiye + statü (salt görüntü); `_Ekstre` yanına
- **Hangfire job (opsiyonel):** DeadlineAt geçen SENT → EXPIRED_CONFIRMED (sessiz onay)
- **🔒 MUTABAKAT KİLİDİ (plan 14 üçüncü kilit ailesi — K9):** CONFIRMED/EXPIRED_CONFIRMED
  mutabakat sonrası, o partner için `StatementDate` öncesine yeni AM hareketi/iptal girilemez.
  - `sp_GuardPartnerReconciled(@CompanyId, @PartnerId, @MovementDate)` — plan 14'te `sp_GuardStockFrozen`
    no-op kancası gibi açılır; bu planda GERÇEK gövde yazılır.
  - Davranış: en son CONFIRMED mutabakatın StatementDate'inden ESKİ tarihe hareket → THROW
    (yetkili override → `PeriodOverrideLog` LockType='PARTNER_RECONCILED', iz zaten hazır).
  - Çağrı noktası: AM yazan onay SP'leri (sp_*InvoicePost, sp_RecordPayment, sp_*Reverse) —
    `sp_GuardPeriodOpen` yanında ZAMAN+PARTNER iki kilit yan yana.
  - **Kilit aileleri ayrı (document-immutability.md):** ZAMAN=AccountingPeriod · STOK=sayım freeze ·
    PARTNER+TARİH=bu. Ortak nokta sadece guard zinciri + PeriodOverrideLog.LockType izi.

### Kapsam Dışı
- Otomatik mutabakat mektubu PDF/KEP gönderimi (M16 entegrasyon ilerisi)
- Bileşik faiz / cari hesap sözleşmesi (TTK md.96 — özel kurum, çoğu KOBİ kullanmaz)
- Çapraz-kur mutabakatı

## 3. Alternatifler

**A: Partner'a 2 kolon (LastReconciliationDate + LastReconciledBalance)**
- Reddedildi: mutabakat geçmişi yok, TTK md.94 sessiz-onay/itiraz akışı modellenmez, denetimde yetersiz.

**B: PartnerReconciliationLog ayrı tablo (seçilen)**
- Her tur ayrı satır; geçmiş + statü + deadline; TTK md.94 tam karşılığı; append-only (md.82).

**C: AccountReconciliation'a gömme**
- Reddedildi: açık-kalem kapama ≠ mutabakat turu. Farklı granülarite, farklı yaşam döngüsü. Karıştırma.

## 4. Riskler

| Risk | Etki | Önlem |
|---|---|---|
| BalanceSnapshot drift | Mutabakat tarihindeki bakiye sonradan değişir (geç gelen belge) | Snapshot anı sabit yazılır; AM append-only → geçmiş SUM değişmez (kapalı dönem guard) |
| Sessiz onay yanlış tetik | Deadline geçti ama gönderim ulaşmadı | SentChannel + ResponseAt audit; EXPIRED_CONFIRMED ayrı statü (manuel CONFIRMED'den ayrık) |
| Açık-kalem ile karışma | İki recon tablosu | İsim + doc netliği; bu plan mutabakat, plan 18 kapama |

## 5. Done Criteria

- [ ] `PartnerReconciliationLog` tablosu (append-only, deadline + statü makinesi)
- [ ] `sp_CreateReconciliationStatement` — snapshot + SENT + dönem guard
- [ ] `sp_RespondReconciliation` — CONFIRMED/DISPUTED
- [ ] `sp_GuardPartnerReconciled` — mutabakat kilidi (StatementDate öncesi hareket → THROW)
- [ ] AM yazan SP'lere guard enjeksiyonu (sp_*InvoicePost, sp_RecordPayment, sp_*Reverse)
- [ ] Partner kartı mutabakat tab (son tarih + bakiye + statü)
- [ ] Smoke: snapshot bakiyesi `tvf_PartnerBalance` ile tutarlı + mutabakat kilidi testi
- [ ] mali-evrak-mevzuat + sql-sp-reviewer

## 6. Rollback
Tablo DROP, SP'ler önceki sürüm, Partner tab kaldır.

## 7. Adımlar
- [ ] **Faz 0:** Plan 18 (açık-kalem kapama) bitmiş olmalı — bakiye/açık kalem kaynağı hazır
- [ ] **Faz 1:** `PartnerReconciliationLog` şema
- [ ] **Faz 2:** sp_CreateReconciliationStatement + sp_RespondReconciliation
- [ ] **Faz 3:** `sp_GuardPartnerReconciled` gerçek gövde + AM yazan SP'lere enjeksiyon (plan 14 kanca tamamlanır)
- [ ] **Faz 4:** Partner kartı mutabakat tab (UI)
- [ ] **Faz 5:** Hangfire deadline → EXPIRED_CONFIRMED job (opsiyonel)
- [ ] **Faz 6:** Smoke + mutabakat kilidi testi + reviewer

## 8. 5 Lens
- 🔴 **Contrarian:** BalanceSnapshot geç gelen belgeyle çelişir mi? AM append-only + dönem kilidi koruyor.
- 🔵 **First Principles:** Soru "bakiye kaç" değil "karşı taraf bu bakiyeyi onayladı mı".
- 🟢 **Expansionist:** Temel = otomatik KEP mutabakat mektubu (M16) — bu onun veri katmanı.
- ⚪ **Outsider:** "Mutabakat var ama gönderim yok" — manuel statü ilk sürümde yeterli.
- 🟡 **Executor:** Plan 18 bitsin, sonra tablo + 2 SP + tab.

## 9. İlişkili
- `plans/14-ledger-pk-immutability.md` — guard kanca mimarisi; bu plan 3. kilit ailesini (PARTNER) tamamlar
- `docs/sql/schema_M11_LedgerIntegrity.sql` — PeriodOverrideLog.LockType='PARTNER_RECONCILED' (iz hazır), sp_GuardStockFrozen kanca deseni
- `plans/18-open-item-reconciliation.md` — açık-kalem kapama (ÖN KOŞUL, karıştırma)
- TTK md.94 (cari hesap bakiye onayı) · md.82 (10 yıl saklama)
- `.claude/skills/mali-evrak-mevzuat/SKILL.md` (TTK md.94 notu)
- `docs/sql/db_objects_starter.sql` — tvf_PartnerBalance (snapshot kaynağı)
- `src/Operax.Web/Features/MasterData/Partners/` — kart tab'ı
