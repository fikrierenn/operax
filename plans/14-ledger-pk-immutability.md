# Plan 14 — Ledger Clustered Anahtar + Immutability + Dönem Kontrolü

**Tarih:** 2026-05-29 · **Güncelleme:** 2026-05-30 (K4 dönem kontrolü + K8 istisna/iz eklendi) · **Durum:** `Onaylandı (2026-05-29), kapsam genişledi (K4+K8 onay bekliyor)` · **Modül:** M02/M11 · **Kaynak:** AR-004 + AR-005 + KARAR K4/K8 (🟠 YÜKSEK)

> **KAPSAM GENİŞLEDİ (2026-05-30):** immutability + dönem kontrolü AYNI omurga (ikisi de defter bütünlüğü,
> aynı trigger ailesi, aynı migration). Bu yüzden K4 dönem kontrolü mekanizması bu plana eklendi (§2.d).

> ⚠️ **ÖN KOŞUL — UYGULAMA ÖNCESİ TEYİT (DOĞRULANMADI'ydı):** REFERENCE_STUDY.md'de StockMovement/
> AccountMovement PK'larının clustered + NEWID() olduğu **inline PK default davranışına** dayanıyordu, harf-harf
> `CLUSTERED` yazmıyordu. Migration yazmadan önce `sys.indexes` + `sys.index_columns` ile **gerçekten** clustered
> + GUID olduğu sorgulanıp teyit edilecek. Ayrıca `IX_StockMovement_*` index'lerinin gerçekten basılı olduğu
> doğrulanacak (K6 snapshot reddi → bakiye SUM'unun tek dayanağı bu index'ler).

## 1. Problem
İki ilişkili ledger sorunu:
- **AR-004 (PK fragmentasyon):** `StockMovement` + `AccountMovement` clustered PK = `UNIQUEIDENTIFIER DEFAULT NEWID()` (rastgele). Sürekli büyüyen tablolarda page split + index fragmentasyonu → INSERT yavaşlar, "Dapper+SARGable hız" iddiasını baltalar.
- **AR-005 (immutability):** `AccountMovement.IsDeleted` taşıyor → append-only ledger silinebilir. VISION "ERP truth immutable" + VUK 359 bütünlük ile çelişir. Düzeltme = ters kayıt (contra-entry), silme değil. `StockMovement` `IsCancelled` kullanıyor (daha iyi ama yine cancel≠reversal netleşmeli).

## 2. Scope
### Dahili
- **ADR:** ledger clustered anahtar stratejisi. Aday: `BIGINT IDENTITY` clustered PK + mevcut `Id GUID` nonclustered unique (dış referanslar GUID kalsın). Alternatif: `NEWSEQUENTIALID()`.
- **(b) immutability:** `AccountMovement`'tan `IsDeleted` kaldır → düzeltme `SourceDocType='REVERSAL'` ters kayıtla (zaten tasarımda var). (B2)
- **(c) StockMovement cancel→reversal:** cancel mekanizmasını reversal disiplinine bağla — ters StockMovement + `IsCancelled=1` set (şu an HİÇ set edilmiyor, REFERENCE_STUDY.md §1). `sp_*Reverse` SP'leri yaz.
- **(d) K4 DÖNEM KONTROLÜ — ZAMAN BAZLI (sadece MEKANİZMA — B12):**
  - Tetikleyiciler (hepsi tarih/dönem bazlı; bir tarihten öncesini tüm evrak girişine kapatır): muhasebe **ay kapanışı** (Logo/Mikro aktarımı sonrası) · **KDV beyan dönemi** kilidi · (ileride) **e-Defter berat → mutlak (LOCKED)**.
  - `AccountingPeriod` tablosu: **CompanyId (firma bazlı — 5 firma ayrı kapanır)** + dönem (yıl/ay) + statü `OPEN/CLOSED/LOCKED`. (⚠️ CompanyId TAŞIR — metadata sözlüğüyle karıştırma. **Bu ZAMAN bazlıdır; K5 sayım freeze SATIR bazlıdır — ayrı tablo, karıştırma.**)
  - `sp_GuardPeriodOpen(@companyId, @date)`: her onay/hareket SP'sinin **ilk satırı**; dönem OPEN değilse `THROW` (Türkçe: "Dönem kapalı; düzeltmeyi sonraki açık döneme girin."). Tek geçiş noktası.
  - **DB trigger (emniyet ağı):** StockMovement/AccountMovement/FinancialTransaction'a kapalı döneme denk INSERT/UPDATE/DELETE engelle (SP atlansa bile koruma kalsın).
  - **Statü makinesi:** OPEN→CLOSED geri alınabilir (yetkili, iz bırakır, geçici açma); CLOSED→LOCKED **tek yön, mutlak, dönüşsüz**.
  - **🪝 KANCA (K5 için):** guard, ileride **sayım freeze** için `sp_GuardStockFrozen(@companyId,@warehouseId,@binId,@itemId)` yan yana çağrılabilecek şekilde tasarlanır. **Bugün boş/no-op kanca** açılır; gerçek implementasyon M08/S7'de (`docs/MODULE_SPECS/M08_CycleCount_Freeze.md`).
- **(e) AR-004 clustered PK:** R4 — NEWSEQUENTIALID veya BIGINT identity clustered + GUID nonclustered (aynı migration).
- **(f) K8 İSTİSNA/OVERRIDE + İZ TABLOSU — `PeriodOverrideLog` (SİLİNMEZ):** Kilit tek başına yetmez; kontrollü istisna + zorunlu iz birlikte. Tablo alanları:
  - **Ne:** SourceDoc (tip/id/no), hedef tablo, hareketin **ait olduğu tarih** (`MovementDate`).
  - **Hangi kilit aşıldı:** `LockType` (PERIOD_CLOSED / PARTNER_RECONCILED / STOCK_FROZEN — üç kilit ailesi tek log'da iz olarak; **ama her kilit AYRI guard/tablo**).
  - **Kim:** kullanıcı (`OverriddenBy`).
  - **Ne zaman (İKİSİ AYRI — kritik):** işlem anı (`CreatedAt`, giriş tarihi) + hareketin ait olduğu tarih (`MovementDate`). Geç gelen belge denetimi için ayrı tutulur.
  - **Neden (ZORUNLU):** `ReasonCategory` (LATE_DOCUMENT / CORRECTION / SYSTEM_ERROR / OTHER) + `ReasonText` (serbest metin, min uzunluk — boş geçilemez). Kategori → raporu denetlenebilir yapar.
  - **Onaylayan (opsiyonel):** `ApprovedBy` — çift-onay için.
- **(g) `sp_GuardPeriodOpen` davranışı statüye bağlı:**
  - **OPEN** → serbest, iz yok.
  - **CLOSED** → **yetkili kullanıcı + zorunlu gerekçe** ile geçilebilir → `PeriodOverrideLog`'a **atomik** yazım (aynı transaction). Yetkisizse `THROW`. Gerekçe boşsa `THROW`.
  - **LOCKED** (e-Defter berat) → **İSTİSNA YOK.** Hiç kimse, hiçbir gerekçeyle giremez → koşulsuz `THROW`. Tek çözüm: sonraki açık döneme düzeltme kaydı. (Mutlak — override mekanizması/kancası bile açılmaz.)
- **(h) Yetki disiplini (kritik):** Override yetkisi **ÇOK DAR** (muhasebe sorumlusu / yönetici). **GÖREVLER AYRILIĞI:** override yapan kişi kendi override'ını **kendi ONAYLAYAMAZ** (yüksek riskte çift-onay → `OverriddenBy ≠ ApprovedBy`).
- **(i) Rapor view'ı veri modeline HAZIR (ekran YOK):** `PeriodOverrideLog` öyle tasarlanır ki "Dönem İstisna Raporu" sonradan **tek view/sorgu** ile çıksın (filtre: tarih aralığı, kullanıcı, kilit tipi, firma). Bugün ekran/UI yapılmaz.
### Dışı
- Mevcut veri migrasyonu büyük tabloda — dikkatli down/up script (faz ayrı).
- **K4 KAPSAM DIŞI (bugün YAPILMAYACAK):** dönem kapatma UI'si, otomatik kapatma, kapanış raporları, çapraz kontrol. Statü değişimini şimdilik **admin elle** yapar. Süreç/otomasyon muhasebe modülüyle (K1/K2 ertelenmiş) gelir. → **MEKANİZMA KUR, SÜREÇ KURMA.**
- **K5:** e-Defter/GİB üretimi yok; LOCKED dışarıdan sinyalle gelir (mali müşavir → admin).
- **K8 KAPSAM DIŞI (bugün YAPILMAYACAK):** override için UI/ekran/rapor ekranı yok (sadece tablo + guard + view-hazır model). **LOCKED'da override mekanizması/kancası KURULMAZ** (istisna yok demek = kanca bile yok).
- **Kilit aileleri TEK tabloya birleştirilmez:** (1) zaman → AccountingPeriod (bu plan) · (2) stok satırı → sayım freeze (M08/S7) · (3) partner+tarih → cari mutabakat freeze (M11/sonra, K9). Ortak nokta sadece guard çağrı zinciri; `PeriodOverrideLog` üçünün de iz kaydını `LockType` ile tutar ama kilit tabloları ayrıdır.

## 3. Alternatifler (clustered key)
- A: NEWID() korunsun — Reddedildi: fragmentasyon kanıtlı sorun.
- B: NEWSEQUENTIALID() — sıralı GUID, page split azalır; ama GUID 16 byte (geniş).
- C (öneri): BIGINT IDENTITY clustered + GUID nonclustered — en dar/sıralı clustered, GUID dış anahtar olarak kalır. ERP ledger standardı.

**5 lens:** 🔴 PK değişimi mevcut FK/sorguları etkiler → GUID'i nonclustered koruyarak kır. 🔵 Gerçek ihtiyaç: sıralı insert + immutable defter. 🟢 Aynı strateji tüm büyüyen tablolara (AuditLog vb.). ⚪ "GUID neden vardı?" → dağıtık üretim/merge; tek-DB'de gerek yok ama dış ref için tut. 🟡 ADR + yeni tablolarda uygula (mevcut migrate dikkatli).

## 4. Done
- [x] **ÖN KOŞUL:** `sys.indexes` ile StockMovement/AccountMovement/FinTx PK'sı **CLUSTERED+random GUID** teyit edildi + `IX_StockMovement_*` / `IX_AccountMovement_Partner_Date` / `UX_AccountMovement_Source` basılı doğrulandı (canlı VT, 2026-05-31)
- [x] ADR yazıldı (`docs/ADR/01-ledger-clustered-key.md`) — karar: BIGINT IDENTITY clustered + GUID nonclustered
- [ ] AccountMovement IsDeleted kaldırıldı + REVERSAL ile düzeltme akışı netleşti — **KALAN** (2 filtreli index rebuild gerek; referans yok, contained)
- [ ] StockMovement cancel → ters hareket + IsCancelled=1 (`sp_*Reverse`) — **KALAN**
- [x] **K4:** AccountingPeriod (firma bazlı) + sp_GuardPeriodOpen + OPEN/CLOSED/LOCKED statü makinesi — canlı DB'de 5 senaryo doğrulandı · **KALAN: period trigger (emniyet ağı, SESSION_CONTEXT ile override uyumu)**
- [x] **K5 kancası:** `sp_GuardStockFrozen` no-op kanca açıldı (imza sabit; gövde M08/S7)
- [x] **K8:** `PeriodOverrideLog` tablosu (silinmez, ADR-01 BIGINT clustered; kategori+gerekçe CHECK; CreatedAt≠MovementDate ayrı)
- [x] **K8:** sp_GuardPeriodOpen statü davranışı — OPEN serbest / CLOSED yetki+gerekçe→atomik log / LOCKED koşulsuz throw
- [x] **K8:** override dar rol (UserCompany.Role Admin/Finance) + self-approval engeli (CHECK OverriddenBy≠ApprovedBy)
- [x] **K8:** Dönem İstisna Raporu view'a hazır veri modeli (`IX_PeriodOverrideLog_Report`; ekran YOK)
- [x] document-immutability.md §1.b ledger reversal + dönem kilidi kuralıyla güncellendi
- [ ] **KALAN:** onay SP'lerine (Receiving/Shipping/Transfer/CycleCount/ProductionFinish) `sp_GuardPeriodOpen` ilk-satır enjeksiyonu (movement date kaynağı + trigger çakışması dikkat)

## 5. Adımlar
1. [ ] **ÖN KOŞUL:** clustered PK + index gerçeğini sys.indexes ile teyit
2. [ ] ADR (clustered key kararı)
3. [ ] AccountMovement IsDeleted kaldır + reversal akış doğrula
4. [ ] StockMovement cancel→reversal disiplini (sp_*Reverse + IsCancelled set)
5. [ ] **K4:** AccountingPeriod tablo + sp_GuardPeriodOpen + trigger + statü makinesi
6. [ ] **K8:** PeriodOverrideLog tablo + guard statü davranışı (CLOSED→log, LOCKED→throw) + self-approval engeli
7. [ ] Onay SP'lerine `sp_GuardPeriodOpen` ilk-satır çağrısı enjekte
8. [ ] (Faz 2) mevcut tablolarda clustered key migrate (büyük — ayrı)

## 6. Onay
- [x] ADR onayı: BIGINT IDENTITY clustered (2026-05-31) · [x] K4 kapsam onayı: immutability+dönem mekanizması (2026-05-31) · Uygulama: 2026-05-31 (mekanizma çekirdeği — entegrasyon kısmen kaldı)

> İlişkili: AR-004, AR-005, KARAR K4 (REFERENCE_STUDY.md §7), document-immutability.md, schema_M11_AccountMovement.sql, schema_all.sql (StockMovement), plan 16 (cari besleme — dönem guard'ı tüketir)
