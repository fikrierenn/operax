# Plan 34 — UDF / Dinamik Kullanıcı Tanımlı Alanlar (Özel Alan)

> Tier 3. Çekirdek koda/şemaya dokunmadan müşteriye özel alan ekleme katmanı (SAP append-structure / 1C extension muadili).

**Tarih:** 2026-06-18
**Yazan:** Fikri / Claude
**Durum:** `Taslak`
**Modül:** M00 (Platform Core — extensibility)
**Paket:** STARTER (tüm paketlerde temel)

---

## 1. Problem

Dikey sektörlerin (tekstil: Beden/Renk, kitap: Yazar/ISBN, gıda: Alerjen) veri takip alanları standart çekirdek şemada yok. Her yeni alan için kolon + C# sınıfı + ekran değiştirmek bakım maliyetini artırır ve single-tenant çoklu kurulumda sürüm yönetimini imkânsızlaştırır. Operax'ın `docs/design/DYNAMIC_CUSTOM_FIELDS.md` tasarımı bu sorunu JSON+metadata UDF ile çözüyor **ama kodda 0 satır implement edilmiş.** Müşteriye özel alan eklemenin tek yolu şu an çekirdeği çatallamak — bu da projenin temel "core %100 temiz" ilkesine aykırı.

## 2. Scope

### Kapsam dahili (Faz 1)
- `UserFieldDefinition` metadata tablosu + `Item.AdditionalFields` JSON kolonu (idempotent ALTER).
- `UdfService` — tanım okuma, JSON çöz, **whitelist + sunucu-taraf validasyon** ile JSON üretme.
- `_CustomFields.cshtml` partial — TEXT / NUMBER / SELECT(STATIC) render.
- Item Details GET/POST entegrasyonu.
- Admin "Özel Alanlar" CRUD ekranı (`[Authorize(Roles="Administrator")]`).
- Tasarımdaki 6 açığın baştan kapatılması (bkz. §4 + Kritik Detaylar).

### Kapsam dışı (sonraki fazlar)
- DATE / BOOLEAN alan tipleri → Faz 2.
- `DICTIONARY` data source (DictionaryType/DictionaryValue tabloları yok) → ertelendi, gerçek ihtiyaçla.
- `TABLE` data source (UdfWhitelist) → Faz 2.
- Computed column + persisted index (sıralama/filtre) → Faz 2, müşteri talebiyle.
- Evrak zinciri inheritance (sipariş→sevkiyat→fatura UDF taşıma, SP'lere dokunma) → Faz 3.
- Item dışı entity'ler (Partner, SalesOrderLine, ReceivingLine…) → Faz 2-4.
- UDF değeri üzerinden liste filtreleme/raporlama → Faz 4.

### Etkilenen dosyalar (Faz 1 tahmin)
- `docs/sql/schema_M_UDF.sql` — YENİ: tablo + Item.AdditionalFields ALTER
- `src/Operax.Cli/Program.cs` — migrate listesine 1 satır (~`:102`)
- `src/Operax.Web/Lib/UiVms.cs` — `UdfFieldDef` + `CustomFieldsVm` record
- `src/Operax.Web/Lib/UdfService.cs` — YENİ servis
- `src/Operax.Web/Program.cs` — `AddScoped<UdfService>()`
- `src/Operax.Web/Features/Shared/_CustomFields.cshtml` — YENİ partial
- `src/Operax.Web/Features/MasterData/Items/Details.cshtml(.cs)` — entegrasyon
- `src/Operax.Web/Features/Admin/UdfFields/Index.cshtml(.cs)` — YENİ CRUD
- `src/Operax.Web/Features/Shared/_Layout.cshtml` — sidebar linki

**Tahmini boyut:** ~9 dosya / ~600 satır (Faz 1).

## 3. Alternatifler

### A: Klasik EAV (Entity-Attribute-Value satır tablosu)
**Açıklama:** Her özel alan değeri ayrı satır (`UdfValue(EntityId, FieldId, Value)`). Tip kayması yok, her alan kendi satırında.
**Reddetme sebebi:** Her kayıt okumada N JOIN → düşük performans; Operax'ın "SELECT sadece gerekli kolon, JOIN optimize" ve raw-performans önceliğine ters. Tasarım belgesi de bunu açıkça reddetmiş.

### B: Gerçek kolon ekleme (müşteri başına ALTER TABLE)
**Açıklama:** Single-tenant olduğu için her müşteri DB'sinde özel kolonlar fiziksel açılır.
**Reddetme sebebi:** Şema çatallanması = sürüm/migration cehennemi; çekirdek SP/View'lar kolon farkından kırılır; "core %100 temiz" ilkesi ölür. Tasarımın çözmek istediği sorunun ta kendisi.

### C: JSON hibrit + metadata + whitelist validasyon (SEÇİLEN)
**Açıklama:** Tek `AdditionalFields NVARCHAR(MAX)` kolonu + `UserFieldDefinition` metadata + SQL Server 2022 `JSON_VALUE`. Alan tanımı = veri, kod değil. 6 açık baştan kapalı.
**Sebep:** Şema değişmez, JOIN yok, mevcut kod tabanı zaten `Description` kolonunda JSON serialize ediyor (organik pattern). Tasarım belgesiyle uyumlu, en ucuz + en performanslı (derlenmiş C#, yorumlayıcı yok → 1C/canias'tan hızlı).

**5 lens kontrolü:**
- 🔴 **Contrarian:** Fatal flaw = `JSON_VALUE` lexical sort + indekssiz full-scan. → Faz 1'de NUMBER sıralama amaçlı kullanılmaz; index Faz 2'ye ertelendi, bilinçli.
- 🔵 **First Principles:** Gerçek soru "kolon mu eklemeli" değil — "core'a dokunmadan veri çeşitliliği nasıl taşınır". JSON+metadata bu soruya doğru cevap.
- 🟢 **Expansionist:** Aynı altyapı ileride evrak zinciri inheritance + sektör şablonları (preset UDF paketleri) için temel olur — büyük fırsat Faz 3+.
- ⚪ **Outsider:** Yabancı biri "neden HTML5 required'a güveniyorsun?" der → Açık 2 sunucu validasyonu baştan koyuldu.
- 🟡 **Executor:** Pazartesi sabahı: `schema_M_UDF.sql` yaz → migrate → `UdfService` → partial → Item Details wire → smoke.

## 4. Riskler

| Risk | Etki | Olasılık | Mitigation |
|---|---|---|---|
| Anahtar enjeksiyonu (tanımsız UDF_* JSON'a yazılır) | yüksek | orta | `BuildValidatedJson` yalnız `definitions`'daki FieldName işler (Açık 3) |
| HTML5 required bypass → zorunlu alan boş geçer | orta | yüksek | Sunucu-taraf IsRequired/FieldType validasyon (Açık 2) |
| NUMBER tr-TR virgül → parse/format bozulur | orta | yüksek | InvariantCulture parse + format (Açık 5) |
| JSON_VALUE indekssiz full-scan (büyük tablo) | orta | düşük (Faz1) | Faz 1 filtre/sıralama yok; computed-index Faz 2 |
| AdditionalFields mass-assignment | orta | düşük | `Request.Form`'dan değil servis üretir; `Item`'a dışarıdan bind yok |
| Mevcut Items/Details `Description` JSON pattern'i ile çakışma | düşük | orta | İlk dokunuşta `Details.cshtml.cs:67-87` oku, ayrıştır |

## 5. Done Criteria (Faz 1)

- [ ] `UserFieldDefinition` tablo + `Item.AdditionalFields` kolonu migrate ile gelir
- [ ] Admin "Özel Alanlar" ekranında TEXT/NUMBER/SELECT(STATIC) tanım eklenebilir
- [ ] Item Details'ta tanımlı alanlar render olur; kaydedince `AdditionalFields` JSON doğru yazılır
- [ ] Zorunlu boş alan → sunucu hata (HTML5 kapalıyken de durur)
- [ ] Tanımsız `UDF_*` form alanı JSON'a YAZILMAZ (enjeksiyon testi)
- [ ] `operax-cli migrate` 0 hata
- [ ] `dotnet build` 0 hata 0 uyarı
- [ ] Smoke: tanım ekle → Item kaydet → DB'de JSON doğrula (CLI query)
- [ ] Faz kapanış kapısı: build-validator → code-reviewer → security-reviewer (yeni PageModel + form binding) → smoke

## 6. Rollback Planı

- Git revert: kod dosyaları geri alınır (`git revert <commit>`).
- DB: `UserFieldDefinition` ve `Item.AdditionalFields` **bırakılır** (veri kaybı önlemi; boş JSON kolonu zararsız). Down script gerekmez — özellik UI'dan erişilmezse atıl kalır.
- Sidebar linki kaldırılınca kullanıcı erişimi kesilir → fiili rollback.

## 7. Adımlar / İçerdiği TODO maddeleri

### Faz 1 — Çekirdek altyapı (Item, TEXT/NUMBER/SELECT_STATIC, 6 açık kapalı)
1. [ ] **34-1** `docs/sql/schema_M_UDF.sql` — `UserFieldDefinition` tablo + UNIQUE(CompanyId,EntityName,FieldName) WHERE IsDeleted=0 + index; `Item.AdditionalFields` idempotent ALTER
2. [ ] **34-2** `src/Operax.Cli/Program.cs` migrate listesine `"schema_M_UDF.sql"` ekle (`schema_M01_SupplierItem.sql` sonrası)
3. [ ] **34-3** `Lib/UiVms.cs` — `UdfFieldDef` + `CustomFieldsVm` record
4. [ ] **34-4** `Lib/UdfService.cs` — `LoadDefinitionsAsync` / `ReadValues` / `BuildValidatedJson` / `GetStaticOptions` + `Program.cs` DI
5. [ ] **34-5** `Features/Shared/_CustomFields.cshtml` — TEXT/NUMBER/SELECT(STATIC) render; DATE/BOOLEAN/DICTIONARY/TABLE no-op + yorum
6. [ ] **34-6** `Features/MasterData/Items/Details.cshtml.cs` — UdfService inject, `ItemDto.AdditionalFields`, OnGet/OnPost entegrasyon (önce `:67-87` Description-JSON pattern'i oku)
7. [ ] **34-7** `Features/MasterData/Items/Details.cshtml` — koşullu UDF paneli
8. [ ] **34-8** `Features/Admin/UdfFields/Index.cshtml(.cs)` — tanım CRUD (soft-delete) + sidebar linki
9. [ ] **34-9** `operax-cli migrate` + smoke (tanım→kaydet→JSON doğrula) + faz kapanış kapısı

### Faz 2 — DATE/BOOLEAN + computed-index + Partner + TABLE source
10. [ ] DATE (invariant `yyyy-MM-dd`, `TODAY` default) + BOOLEAN switch render
11. [ ] İsteğe bağlı `PERSISTED computed column + CAST + index` (müşteri talebiyle)
12. [ ] `Partner.AdditionalFields` aktif + Partner Details wire
13. [ ] `UdfWhitelist` + `TABLE` data source (Açık 6 tamamlanır)

### Faz 3 — Evrak zinciri inheritance
14. [ ] `SalesOrderLine`/`ReceivingLine`/`ShippingLine`/`PurchaseOrderLine` `AdditionalFields` kolonları
15. [ ] `sp_ShippingPost` / `sp_ReceivingPost` UDF kopyalama (sql-sp-reviewer şart)

### Faz 4 — Yaygınlaştırma + raporlama
16. [ ] Diğer entity UDF desteği + readonly liste gösterimi
17. [ ] DICTIONARY data source (tablolar hazır olunca) + UDF üzerinden filtreleme

> `docs/TODO.md`'ye Faz 1 maddeleri ayrıca eklenecek (onay sonrası, plan-tracker).

## 8. İlişkili

- Tasarım: `docs/design/DYNAMIC_CUSTOM_FIELDS.md` (v2.2)
- Kural: `.claude/rules/architecture.md` §8 (UDF), `.claude/rules/security-principles.md` §10-11 (whitelist, mass assignment)
- Faz kapısı: `.claude/rules/phase-review-gate.md`
- Pattern ref: `Features/MasterData/Items/Details.cshtml.cs`, `docs/sql/schema_M01.sql`, `Lib/UiVms.cs`

## 9. Onay

> Kullanıcı onay verene kadar implement edilmez.

- [ ] Plan kullanıcıya gösterildi
- [ ] Geri bildirim alındı (varsa düzeltildi)
- [ ] Onay alındı: <tarih, kullanıcı imzası>
