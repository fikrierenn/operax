# Plan 34 — UDF / Dinamik Kullanıcı Tanımlı Alanlar (Özel Alan)

> Tier 3. Çekirdek koda/şemaya dokunmadan müşteriye özel alan ekleme katmanı (SAP append-structure / 1C extension muadili).

**Tarih:** 2026-06-18
**Yazan:** Fikri / Claude
**Durum:** `Onaylandı` (2026-06-18)
**Modül:** M00 (Platform Core — extensibility)
**Paket:** STARTER (tüm paketlerde temel)

---

## 1. Problem

Dikey sektörlerin (tekstil: Beden/Renk, kitap: Yazar/ISBN, gıda: Alerjen) veri takip alanları standart çekirdek şemada yok. Her yeni alan için kolon + C# sınıfı + ekran değiştirmek bakım maliyetini artırır ve single-tenant çoklu kurulumda sürüm yönetimini imkânsızlaştırır. Operax'ın `docs/design/DYNAMIC_CUSTOM_FIELDS.md` tasarımı bu sorunu JSON+metadata UDF ile çözüyor **ama kodda 0 satır implement edilmiş.** Müşteriye özel alan eklemenin tek yolu şu an çekirdeği çatallamak — bu da projenin temel "core %100 temiz" ilkesine aykırı.

## 2. Scope

### Kapsam dahili (Faz 0 — Description sağlığa kavuşturma) [ÖNCE]
> Bulgu (2026-06-18, koddan doğrulandı): `Item.Description` kolonu gerçek açıklama değil, gizli JSON çantası (`UdfDataDto`: ActualDescription/Volume/Weight/MinQty/MaxQty/TempRange). Çantayı `Items/Details` formu round-trip ediyor; **ek olarak `Items/Index.cshtml.cs:64-88` MinQty'yi okuyup `CriticalSkuCount` KPI'ını (emniyet altı SKU) hesaplıyor → MinQty CANLI.** Mevcut hesap kötü: tüm ürünleri C#'a çekip JSON parse döngüsü. `ItemBinConfig.MinQty` (raf bazlı replenishment, `schema_M07_Replenishment.sql:4-18`) farklı amaç — bu ürün-seviyesi emniyet stoğu.
- `Item.Description` → gerçek açıklama kolonu (mevcut `ActualDescription` içeriği backfill ile taşınır).
- `MinQty`/`MaxQty` → **gerçek kolon** `MinStockLevel`/`MaxStockLevel` DECIMAL(18,4). MinQty CANLI (CriticalSku KPI) → silinmez, terfi eder.
- `CriticalSkuCount` KPI → C# JSON parse döngüsü yerine tek SQL COUNT (`balance < MinStockLevel`) — SARGable, hızlı.
- `Volume`/`Weight`/`TempRange` → **seed UDF tanımı** olarak dinamik sisteme taşınır (hardcode kolon YOK; tüketen yok) — UDF'in ilk kanıt-vakası.
- `Items/Details.cshtml.cs` + `Items/Index.cshtml.cs` → JSON serialize/deserialize hack'i kaldırılır, düz kolon + UDF panel.
- Backfill: mevcut Description JSON'ları parse → açıklama + MinStockLevel/MaxStockLevel kolonları + Volume/Weight/TempRange UDF AdditionalFields'a dağıt (veri kaybı yok).

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

### KARAR (2026-06-18, koddan doğrulanmış)
- **UDF depolama:** Yeni `Item.AdditionalFields` kolonu. Mevcut `Description`-JSON hack'i KORUNMAZ — Faz 0'da temizlenir (Seçenek B+ benimsendi: refactor + UDF tek işte). Gerekçe: Description çantasını hiçbir iş mantığı tüketmiyor → blast radius 2 dosya (Details + Index) → güvenli temizlik.
- **Volume/Weight/TempRange → seed UDF** (hardcode kolon değil). Bunlar "core'da kolonu olmayan, tüketeni olmayan ekstra ürün özelliği" — UDF'in tanımı.
- **MinQty/MaxQty → gerçek kolon** (`MinStockLevel`/`MaxStockLevel`). DÜZELTME (2026-06-18): MinQty silinmez — `Index.cshtml.cs:64-88` CriticalSku KPI'ını besliyor (CANLI). Kolona terfi + KPI'ı SQL'e çevir.

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
| Description-JSON hack çakışması | ÇÖZÜLDÜ | — | Bulgu: çantayı SP/View okumuyor (blast radius=2 dosya). Faz 0 temizler |
| Faz 0 backfill veri kaybı (Description JSON parse hatası) | yüksek | düşük | Backfill idempotent + parse-fail satırı log + Description ham bırakılır; migrate öncesi yedek |

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

### Faz 0 — Description sağlığa kavuşturma (ÖNCE, temizlik + zemin)
0a. [ ] `Items/Index.cshtml.cs` Description kullanımını doğrula (listede ham JSON gösteriyor mu? bug ise kapsama gir)
0b. [ ] `schema_M_UDF.sql` içinde: `Item.AdditionalFields` + `Item.MinStockLevel` + `Item.MaxStockLevel` DECIMAL(18,4) NULL idempotent ALTER; `Item.Description` gerçek açıklama olarak kalır
0c. [ ] Backfill script: mevcut `Item.Description` JSON parse → `ActualDescription`→Description, `MinQty`→MinStockLevel, `MaxQty`→MaxStockLevel, `Volume/Weight/TempRange`→`AdditionalFields` JSON; idempotent (sadece JSON `{` ile başlayan satırlar)
0d. [ ] Seed UDF tanımları: `UserFieldDefinition`'a Volume(NUMBER)/Weight(NUMBER)/TempRange(SELECT_STATIC: Normal,Soğuk,Donmuş) — şirket bazlı seed
0e. [ ] `Items/Details.cshtml.cs` — `UdfDataDto` + JSON serialize/deserialize hack'i KALDIR; `ItemDto` düz `Description` + `MinStockLevel`/`MaxStockLevel` kolon + UDF panel
0f. [ ] `Items/Index.cshtml.cs` — `CriticalSkuCount` C# JSON döngüsünü tek SQL COUNT'a çevir (`balance < MinStockLevel`); DetailsModel.UdfDataDto bağımlılığını kaldır
0g. [ ] `Items/Details.cshtml` + `Index.cshtml` — JSON helper markup kaldır, UDF panel + düz Description + MinStock/MaxStock input

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

## 7.5 Faz 0+1 — Dosya Dosya Uygulama Spesifikasyonu (impl-spec, koddan doğrulandı)

> Her dosya okundu (`before-major-change.md §4`). Aşağısı düşünmeden uygulanabilir spec.

### docs/sql/schema_M_UDF.sql  [YENİ]
**Amaç:** UDF metadata tablosu + Item ek kolonları.
**Değişiklik:**
- `CREATE TABLE UserFieldDefinition` — kolonlar: Id(PK NEWID), CompanyId(FK Company), EntityName NVARCHAR(100), FieldName NVARCHAR(100), LabelText NVARCHAR(200), FieldType NVARCHAR(20), DataSourceType NVARCHAR(20) NULL, DataSourceKey NVARCHAR(1000) NULL, DefaultValue NVARCHAR(500) NULL, OrderNo INT DEFAULT 0, IsRequired BIT DEFAULT 0, IsActive BIT DEFAULT 1, IsDeleted BIT DEFAULT 0, CreatedAt/CreatedBy/UpdatedAt/UpdatedBy (`sql-conventions.md` zorunlu kolonlar).
- `CREATE UNIQUE INDEX UX_UDF_Field ON UserFieldDefinition(CompanyId, EntityName, FieldName) WHERE IsDeleted=0`
- `CREATE INDEX IX_UDF_Entity ON UserFieldDefinition(CompanyId, EntityName) WHERE IsDeleted=0 AND IsActive=1`
- İdempotent ALTER (pattern: `schema_M01_M04_StarterFields.sql:8-12`): `Item.AdditionalFields NVARCHAR(MAX) NULL`, `Item.MinStockLevel DECIMAL(18,4) NULL`, `Item.MaxStockLevel DECIMAL(18,4) NULL` — her biri `IF NOT EXISTS (sys.columns ...) BEGIN ALTER ... END GO`.
**Pattern referansı:** `schema_M01.sql:3-18` (tablo), `schema_M01_M04_StarterFields.sql:8-12` (idempotent ALTER).

### docs/sql/db_objects_udf.sql  [YENİ]  (backfill + seed)
**Amaç:** Description-JSON → kolon/UDF taşıma + seed UDF tanımı. Migrate'in DB-nesne fazında çalışır (tolerant:false).
**Değişiklik:**
- Backfill UPDATE: `Item` satırlarında `Description LIKE '{%'` olanlar için `JSON_VALUE(Description,'$.MinQty')→MinStockLevel`, `$.MaxQty→MaxStockLevel`, `$.Volume/$.Weight/$.TempRange` → `AdditionalFields` JSON (`JSON_MODIFY` veya yeniden kur), `$.ActualDescription→Description` (en son, çünkü Description okunuyor). **Sıra kritik:** önce diğer alanları oku, EN SON Description'ı ActualDescription'a indir.
- İdempotent: yalnız `Description LIKE '{%'` (zaten taşınmış düz metin tekrar işlenmez).
- Seed UDF: her Company için `UserFieldDefinition`'a 3 satır — Volume(NUMBER), Weight(NUMBER), TempRange(SELECT/STATIC, DataSourceKey='Normal,Soğuk,Donmuş'). `WHERE NOT EXISTS` ile idempotent (pattern: `schema_M01_M04_StarterFields.sql:152-160`).
**Dikkat:** Backfill geri-alınamaz veri dönüşümü → migrate öncesi DB yedeği (plan §6). Parse-fail satır log + Description ham bırakılır.

### src/Operax.Cli/Program.cs  [DÜZENLE]
**Amaç:** Yeni şema + backfill migrate sırasına girsin.
**Değişiklik:**
- `:102` addon dizisine son eleman sonrası: `"schema_M_UDF.sql",` ekle.
- `:141` sonrası yeni blok: `var udf = Path.Combine(sqlDir, "db_objects_udf.sql"); if (File.Exists(udf)) await ExecuteScriptAsync(udf, tolerant: false);` — backfill DB-nesne fazında, schema ALTER'lardan SONRA.
**Pattern referansı:** `Program.cs:139-141` (putaway_pick bloğu).

### src/Operax.Web/Lib/UiVms.cs  [DÜZENLE]
**Amaç:** UDF render ViewModel'leri.
**Değişiklik:** Dosya sonuna iki record — `UdfFieldDef(Guid Id, string FieldName, string LabelText, string FieldType, string? DataSourceType, string? DataSourceKey, string? DefaultValue, int OrderNo, bool IsRequired)` + `CustomFieldsVm(string EntityName, IReadOnlyList<UdfFieldDef> Definitions, Dictionary<string,string> CurrentValues, bool ReadOnly=false)`.
**Pattern referansı:** `UiVms.cs:12-17` (record + XML doc stili).

### src/Operax.Web/Lib/UdfService.cs  [YENİ]
**Amaç:** Tanım oku + JSON çöz + whitelist/validasyon ile JSON üret.
**Değişiklik:** `public sealed class UdfService(Db db, ICurrentCompany company)` — metotlar:
- `Task<IReadOnlyList<UdfFieldDef>> LoadDefinitionsAsync(string entityName)` — `WHERE CompanyId=@CompanyId AND EntityName=@e AND IsActive=1 AND IsDeleted=0 ORDER BY OrderNo`.
- `Dictionary<string,string> ReadValues(string? json)` — null/boş→empty; `JsonException`→empty+`logger.LogWarning` (silent değil).
- `string BuildValidatedJson(IFormCollection form, IReadOnlyList<UdfFieldDef> defs, out List<string> errors)` — **Açık 2/3/5:** yalnız `defs` FieldName'leri işler; `form["UDF_"+FieldName]`; IsRequired boş→hata; NUMBER `decimal.TryParse(InvariantCulture)`; SELECT/STATIC değer `DataSourceKey.Split(',')` içinde mi; DATE/BOOLEAN/DICTIONARY/TABLE→`errors.Add` (Faz 1 reddet, Açık 4); çıktı invariant format JSON, yalnız geçen alanlar.
- `static IReadOnlyList<string> GetStaticOptions(string? key)` — `key.Split(',').Select(Trim).Where(len>0)`.
**Pattern referansı:** Dapper `using var conn=db.Open()` + named param (`Details.cshtml.cs:32-38`); exception (`csharp-conventions.md`).
**Dikkat:** Tüm Türkçe yorum (`coding-discipline.md`). DI: `Program.cs`'e `AddScoped<UdfService>()`.

### src/Operax.Web/Program.cs  [DÜZENLE]
**Değişiklik:** Diğer `AddScoped<XService>()` satırlarının yanına `builder.Services.AddScoped<UdfService>();` (SupplierItemService kaydının yanı — Grep ile bul).

### src/Operax.Web/Features/Shared/_CustomFields.cshtml  [YENİ]
**Amaç:** UDF alanlarını render et.
**Değişiklik:** `@model Operax.Web.Lib.CustomFieldsVm`; `Definitions` boşsa hiç HTML yok; her alan `form-group`+`form-label`(+`<span class="req">*</span>` IsRequired)+kontrol; input name `UDF_@field.FieldName`; value=CurrentValues[FieldName] ?? DefaultValue; TEXT→`<input class="form-ctrl">`, NUMBER→`<input type="number" step="any" class="form-ctrl">`, SELECT+STATIC→`<select class="form-ctrl">`+`UdfService.GetStaticOptions`; ReadOnly→`readonly/disabled`; DATE/BOOLEAN→`<!-- Faz 2 -->`.
**Pattern referansı:** `ui-standard.md §6` form; **inline style YASAK** (`inline-style-guard.md`).

### src/Operax.Web/Features/MasterData/Items/Details.cshtml.cs  [DÜZENLE]
**Amaç:** Description-JSON hack'i kaldır, gerçek kolon + UDF panel.
**Değişiklik:**
- Primary ctor'a `UdfService udfSvc` ekle (`:10`).
- `UdfDataDto` sınıfını (`:326-334`) **SİL**; `ItemDto`'dan (`:317-323`) ActualDescription/Volume/Weight/TempRange/MinQty/MaxQty JSON-helper alanlarını kaldır; yerine `string? Description`, `decimal? MinStockLevel`, `decimal? MaxStockLevel` (Volume/Weight/TempRange artık UDF).
- OnGet (`:56-92`): SELECT'e `i.Description, i.MinStockLevel, i.MaxStockLevel, i.AdditionalFields` ekle; `:66-92` JSON deserialize bloğunu **SİL**; ekle: `var udfDefs=await udfSvc.LoadDefinitionsAsync("Item"); UdfPanel=new CustomFieldsVm("Item",udfDefs,udfSvc.ReadValues(Item.AdditionalFields));`
- OnPost (`:146-156`): JSON serialize bloğunu **SİL**; ekle: `var defs=await udfSvc.LoadDefinitionsAsync("Item"); var udfJson=udfSvc.BuildValidatedJson(Request.Form,defs,out var errs); if(errs.Count>0){TempData["Error"]=string.Join("; ",errs);return Page();} Item.AdditionalFields=udfJson;`
- INSERT/UPDATE (`:161-189`): `Description, MinStockLevel, MaxStockLevel, AdditionalFields` kolonlarını ekle.
- `public CustomFieldsVm UdfPanel {get;set;}` property ekle.
**Dikkat:** `ItemDto` `[BindProperty]` (`:12`) — AdditionalFields'a kullanıcı bind etmesin, sadece servisten atanır (mass assignment, Açık).

### src/Operax.Web/Features/MasterData/Items/Details.cshtml  [DÜZENLE]
**Değişiklik:** `:173-207` ActualDescription/Volume/Weight/TempRange/MinQty/MaxQty input bloğunu kaldır; `Description`(düz açıklama)+`MinStockLevel`+`MaxStockLevel` için `form-ctrl` input bırak; sonrasına koşullu `<partial name="_CustomFields" model="Model.UdfPanel"/>` (Definitions.Count>0). **Tailwind utility salatasını `.form-ctrl`/`.form-group`'a çevir** (`ui-standard.md §2` ihlali düzeltilir).
**Dikkat:** İkincil kapsam ama dokunulan blok temizleniyor (drive-by değil — aynı bloktayız).

### src/Operax.Web/Features/MasterData/Items/Index.cshtml.cs  [DÜZENLE]
**Amaç:** CriticalSku C# JSON döngüsü → SQL.
**Değişiklik:** `:60-88` (balances dict + itemDescs + foreach JSON parse) bloğunu **SİL**; yerine tek sorgu: `SELECT COUNT(1) FROM Item i WHERE i.CompanyId=@CompanyId AND i.IsDeleted=0 AND i.MinStockLevel IS NOT NULL AND (SELECT ISNULL(SUM(QtyBalance),0) FROM tvf_InventoryBalance(@CompanyId) b WHERE b.ItemId=i.Id) < i.MinStockLevel`. `DetailsModel.UdfDataDto` bağımlılığı (`:75`) kalkar.
**Pattern referansı:** `tvf_InventoryBalance` kullanımı (`Details.cshtml.cs:95-97`).

### src/Operax.Web/Features/Admin/UdfFields/Index.cshtml(.cs)  [YENİ]
**Amaç:** UDF tanım CRUD.
**Değişiklik:** `[Authorize(Roles="Administrator")]`; OnGet list (EntityName filter), OnPostCreate (EntityName+FieldName+LabelText+FieldType+DataSourceKey+IsRequired+OrderNo; UX_UDF_Field çakışma kontrolü), OnPostSave (Label/sıra/zorunlu güncelle), OnPostDelete (soft `IsDeleted=1`). Her sorgu `WHERE CompanyId=@CompanyId`. View `ui-standard.md` form + tablo.
**Pattern referansı:** `Admin/Parameters/Index.cshtml.cs` (1:1 CRUD şablon — list/Create/Save/Delete + record DTO).

### src/Operax.Web/Features/Shared/_Layout.cshtml  [DÜZENLE]
**Değişiklik:** Admin side-item grubuna (`:271-279` Settings/NumberSeries yanı) `<a asp-page="/Admin/UdfFields/Index" class="side-item">…Özel Alanlar</a>` ekle (Administrator görünürlük guard mevcut blokta).

### Güvenlik özeti (hangi açık nerede kapanıyor)
- SQL injection: tüm sorgular parametreli (UdfService, Admin CRUD). TABLE source Faz 1'de yok.
- Anahtar enjeksiyonu (Açık 3): `BuildValidatedJson` yalnız tanımlı FieldName.
- Sunucu validasyon (Açık 2): IsRequired/FieldType C# tarafı.
- Kültür (Açık 5): InvariantCulture parse/format.
- Mass assignment: AdditionalFields servisten atanır, bind edilmez.
- CompanyId: her UDF sorgusu firma filtreli; LoadDefinitions kaynağı firma-filtreli → çapraz enjeksiyon imkânsız.

### Uygulama sırası (bağımlılık)
schema_M_UDF → Program.cs(CLI) migrate satırı → db_objects_udf backfill+seed → migrate çalıştır → UiVms → UdfService+DI → _CustomFields → Items/Details(.cs+.cshtml) → Items/Index.cs → Admin/UdfFields → _Layout → build → smoke → faz kapanış kapısı.

## 8. İlişkili

- Tasarım: `docs/design/DYNAMIC_CUSTOM_FIELDS.md` (v2.2)
- Kural: `.claude/rules/architecture.md` §8 (UDF), `.claude/rules/security-principles.md` §10-11 (whitelist, mass assignment)
- Faz kapısı: `.claude/rules/phase-review-gate.md`
- Pattern ref: `Features/MasterData/Items/Details.cshtml.cs`, `docs/sql/schema_M01.sql`, `Lib/UiVms.cs`

## 9.5 Uygulama Sonucu (2026-06-18)

**Faz 0 + Faz 1 tamamlandı.** Branch `feat/udf-custom-fields`, commit C1(SQL)/C2(backend)/C3(UI).

- Build: Web + CLI **0 hata** (1 pre-existing ForwardedHeaders uyarısı, bu plan dışı).
- Phase-review-gate: code-reviewer + sql-sp-reviewer + security-reviewer paralel + smoke.
  - sql-sp-reviewer **2 CRITICAL** (backfill veri kaybı: ActualDescription yoksa Description NULL) → `COALESCE(JSON_VALUE(...), Description)` + `NULLIF` ile düzeltildi.
  - code-reviewer 1 HIGH (OnGetAsync 85 satır) → `LoadSupplierTabAsync` helper'a bölündü.
  - security-reviewer: kritik bulgu YOK (6 açık doğrulandı kapalı).
- Smoke (crafted-row): 2 test item (ActualDescription'lı + 'sız) → backfill doğru kolon/UDF dağıtımı, **veri kaybı yok**, **idempotent** (2. geçiş bozulmadı). Test verisi temizlendi.
- **Browser smoke (preview, login→ekran):** Admin/Özel Alanlar 3 seed tanım render; Item Details UDF paneli (Volume/Weight/TempRange) render; kaydet→DB→reload round-trip (`{"Volume":"42","TempRange":"Soğuk"}` persist); Admin'den yeni "Renk" tanımı→Item formunda otomatik `UDF_Renk` render = **tam dinamik döngü doğrulandı.**
- **Browser smoke 2 PRE-EXISTING bug ortaya çıkardı** (item edit-save tamamen kırıkmış, UDF dışı): (1) Details formunda hidden `Item.Id` yok → INSERT dup-key; (2) Item'da `UpdatedAt/UpdatedBy` kolonu yok → UPDATE 'Invalid column'. İkisi de düzeltildi (commit `fix(plan-34): item edit`).

**DEBT (kapsam dışı, ayrı tur):**
- `Items/Details.cshtml` tüm dosya Tailwind utility salatası (`ui-standard.md §2` ihlali) — PRE-EXISTING (bu plandan önce branch'te vardı), bu plan sadece dokunduğu bloğu var olan stille bıraktı. Tüm-view semantic-class refactoru ayrı iş.
- `Admin/UdfFields/Index.cshtml` + `Details.cshtml.cs` inline-style (renk/font-size) — kardeş `Admin/Parameters/Index.cshtml` ile aynı kabul edilmiş pattern; proje-geneli inline-style temizliğiyle birlikte.
- `DetailsModel` 312 satır (300 eşiği) — sıradaki dokunuşta service layer'a bölünmeli.
- `db_objects_udf.sql` backfill TRY/CATCH sarmalı yok (XACT_ABORT var) — düşük öncelik.

## 9. Onay

> Kullanıcı onay verene kadar implement edilmez.

- [x] Plan kullanıcıya gösterildi
- [x] Geri bildirim alındı → Description-JSON bulgusu + Faz 0 temizlik eklendi (2026-06-18)
- [x] Onay alındı: 2026-06-18, Fikri ("onaylıyorum")
