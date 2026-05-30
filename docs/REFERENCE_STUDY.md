# OPERAX — Referans Açık-Kaynak ERP/WMS Çalışması

> Tarih: 2026-05-29 · Tür: Salt-okuma araştırma turu (kod/şema değiştirilmedi)
> Bağlam: Dış mimari review yaraları R1–R4'ü (BUGS.md AR-001..006) referans
> projelerle doğrulamak + çözüm deseni çıkarmak.
> **Kanıt katmanı her iddiada işaretli:** `[REPO]` = repo kodu okundu · `[DOC]` =
> resmi doküman · `[OPERAX]` = Operax kaynağı file:line · `DOĞRULANMADI` = teyit edilemedi.
>
> Stack kararı sabit: **Dapper + raw SQL + Transaction Script + single-tenant.**
> Referanslardan veri erişimi DEĞİL; domain / model / izolasyon dersi alınır.
> "EF Core'a geçelim" gibi bir sonuç bu belgede YOKTUR.

---

## 0. Yönetici Özeti — En Kritik 3 Bulgu

1. **Operax'ta "perpetual accounting" yok ve cari defter drift ediyor.** Stok onayı
   (`sp_ReceivingPost`/`sp_ShippingPost`) StockMovement + ItemCost'u atomik günceller
   ama **hiçbir onay SP'si `AccountMovement`'a INSERT yapmıyor** `[OPERAX]`. AccountMovement
   yalnızca tek seferlik `migrate_backfill_accountmovement.sql` ile dolduruluyor; sonrasında
   beslenmiyor → cari bakiye zamanla gerçeği yansıtmaz. ERPNext bunu `stock_value_difference`
   tek alanı üzerinden her harekette 2 dengeli GL kaydıyla çözüyor `[REPO]`. **Bu, R1/R2'den
   daha büyük ve review'da adı geçmeyen bir yapısal açık.**

2. **R3 (firmalar arası sızıntı) için "global filter" kurtarıcısı YOK — ama Operax aslında
   referanslardan daha güçlü konumda.** Smartstore ve nopCommerce'in ikisi de EF global
   query filter KULLANMIYOR; izolasyon her sorguda elle `ApplyStoreFilter`/`ApplyStoreMapping`
   çağrısıyla yapılıyor `[REPO]` — yani Operax'ın "elle WHERE CompanyId" probleminin daha
   düzenli hali, garanti değil. Operax'ın `CompanyId` her satırda zorunlu olması (opt-in
   store-mapping değil) **daha sıkı bir model.** Eksik tek şey: merkezi uygulanma garantisi.

3. **R1 (immutability) + R4 (GUID PK) zaten doğru teşhis; çözüm deseni ERPNext'te birebir var.**
   ERPNext: tekil defter satırı (SLE/GL Entry) **edit/cancel edilemez** (`on_cancel` throw),
   iptal = `is_cancelled=1` + ters kayıt `[REPO]`. Operax'ın AccountMovement tasarım yorumu
   ("silme yok → REVERSAL") doğru yönde ama **ters kaydı yazan SP yok** ve tablo hâlâ
   `IsDeleted` taşıyor `[OPERAX]`.

**R1–R4 çözüm deseni bulundu mu?** R1 ✅ (ERPNext reversal modeli) · R2 ✅ (ERPNext FIFO JSON
kuyruğu) · R3 ✅ (TVF-sargılı + analyzer; referanslardan daha sıkı zaten) · R4 ✅
(NEWSEQUENTIALID / BIGINT identity clustered — standart).

---

## 1. Operax Gerçek Şema (taban referans) `[OPERAX]`

Karşılaştırmaların dayandığı doğrulanmış mevcut durum:

### StockMovement (`schema_M02.sql:3-29`, canlı `schema_all.sql:525-555`)
- Kolonlar: Id(PK NEWID), CompanyId, WarehouseId, BinId, ItemId, MovementType
  (RECEIPT/ISSUE/TRANSFER/COUNT_ADJ), QtyBase(±), UomId, QtyOriginal, SourceDoc{Type,Id,No},
  LpnId, LotNo, SerialNo, ExpiryDate, CreatedAt/By, **IsCancelled** BIT, CancelledAt/By,
  UnitCost (ALTER ile `schema_M02_Costing.sql:33`).
- **Sadece delta** tutuyor; running balance / valuation snapshot kolonu YOK.
- Bakiye = `vw_InventoryBalance` → `SUM(QtyBase) WHERE IsCancelled=0` (fiziksel bakiye tablosu yok).
- PK = `UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID()` → default **clustered, rastgele GUID**.
- **`IsCancelled=1`'i yazan hiçbir SP/C# yok** (Grep: 0 match) — alan ölü-hazır. Stok iptal SP'si
  (sp_ReceivingCancel/sp_ReverseStock) **yok**; PO cancel sadece header status flip
  (`PurchaseOrders/Details.cshtml.cs:230`), Receiving'de cancel handler hiç yok.

### ItemCost (`schema_M02_Costing.sql:12-25`)
- Id, CompanyId, ItemId, WarehouseId(NULL=genel), **AvgCost** DEC(18,4), **OnHandQty** DEC(18,6),
  LastReceiptDate, UpdatedAt. Unique: (CompanyId,ItemId,WarehouseId).
- **Tek maliyet kolonu = Moving Average.** FIFO cost-layer tablosu YOK. Parametrede
  `MOVING_AVG/FIFO/STANDARD` metni var (`schema_M01_M04_StarterFields.sql:30`) ama yalnız
  MOVING_AVG'ı uygulayan SP mevcut.
- Moving-avg formülü (`db_objects_starter.sql:44-49`):
  `NewAvg = ((OldQty*OldAvg)+(Qty*ISNULL(UnitCost,OldAvg)))/NewQty`. ISSUE'da AvgCost değişmez,
  sadece OnHandQty düşer.

### AccountMovement (`schema_M11_AccountMovement.sql:13-35`)
- Id(PK NEWID, named), CompanyId, PartnerId, MovementDate, **Borc/Alacak** DEC(18,2) (T-hesabı,
  tek `Amount` yok), Currency, SourceDoc{Type,Id,No}, Description, **IsDeleted** BIT, audit kolonları.
- Tasarım yorumu (`:5-6`): "Silme yok → cancel için REVERSAL ters kayıt", `SourceDocType='REVERSAL'`
  tanımlı — **ama ters kaydı yazan SP yok; tablo yine de IsDeleted taşıyor (çelişki).**
- PK = default clustered rastgele GUID (NEWID).
- **Hiçbir onay SP'si beslemiyor** → backfill sonrası drift.

### FinancialTransaction (`schema_M11_Finance.sql:46`) — nakit/banka defteri (ayrı, üçüncü defter).

> **Üç ayrı tek-amaçlı defter (StockMovement / AccountMovement / FinancialTransaction)
> SP düzeyinde birbirine bağlı değil. Çift-taraflı GL / yevmiye kavramı yok.**
> (`schema_M04_EBelge.sql:6`: "Defter (Yevmiye/Kebir) ... Operax dışı.")

---

## 2. ⭐ ERPNext Stok + Maliyet Modeli vs Operax M02/M11 (EN DETAYLI BÖLÜM)

Kaynak `[REPO]`: ERPNext `develop` — `stock/valuation.py`, `stock/stock_ledger.py`,
`controllers/stock_controller.py`, `stock_ledger_entry.{py,json}`, `gl_entry.{py,json}`,
`repost_item_valuation.py`. `[DOC]`: docs.frappe.io perpetual-inventory / SRBNB / valuation.

### 2.1 Defter satırı yapısı — yan yana

| Kavram | ERPNext Stock Ledger Entry (SLE) | Operax StockMovement | Fark |
|---|---|---|---|
| Hareket miktarı | `actual_qty` (±) | `QtyBase` (±) | ✅ eşdeğer |
| **Hareket sonrası bakiye** | `qty_after_transaction` | **YOK** | ❌ Operax SUM ile hesaplıyor |
| **Hareket sonrası değerleme** | `valuation_rate` | **YOK** (sadece ItemCost.AvgCost ayrı tabloda) | ❌ |
| **Hareket sonrası toplam değer** | `stock_value` | **YOK** | ❌ |
| **Değer değişimi (Δ)** | `stock_value_difference` | **YOK** | ❌ muhasebe köprüsü yok |
| **FIFO kuyruğu** | `stock_queue` (JSON `[[qty,rate],…]`) | **YOK** | ❌ FIFO yok |
| Birim maliyet | `incoming_rate`/`outgoing_rate` | `UnitCost` (tek) | kısmi |
| Kaynak belge | `voucher_type`+`voucher_no` | `SourceDocType`+`SourceDocId` | ✅ |
| Kaynak **satır** | `voucher_detail_no` | `SourceDocNo` (belge no, satır değil) | ⚠️ satır-bazı bağ zayıf |
| İptal | `is_cancelled` (silinmez) | `IsCancelled` (ama hiç set edilmiyor) | ⚠️ var ama ölü |
| Dönem kilidi | `stock_frozen_upto` (Stock Settings) | **YOK** | ❌ |

**Ana ders:** ERPNext SLE = **hareket + snapshot.** Her satır running balance/value/Δ taşır → `SUM` gerektirmez.
Operax StockMovement saf delta → bakiye sorgusu aggregate.
> ⚠️ **GÜNCELLEME (K6, 2026-05-30):** Bu bölümün eski "StockMovement'a QtyAfterTransaction/ValuationRate/StockValue
> snapshot kolonu ekle" önerisi **REDDEDİLDİ (K6).** Mikro da snapshot tutmuyor (MIKRO §1, karşı-kanıt) → snapshot
> reddi sektör-uyumlu. Bakiye `SUM(QtyBase) WHERE IsCancelled=0` + index ile (B7 iptal). FIFO maliyet ise
> snapshot DEĞİL, **kalıcı `StockCostConsumption` eşleme tablosuyla** çözülür (K7 revize — çıkış↔giriş katman izi,
> Mikro §1.5 deseni). ERPNext stock_queue JSON yerine normalize eşleme tablosu.

### 2.2 GL Entry — tek-satır defter modeli

| ERPNext GL Entry alanı | Açıklama | Operax AccountMovement | Fark |
|---|---|---|---|
| `account` | Hesap (tek satır=tek hesap) | YOK (cari bazlı, hesap planı yok) | ❌ kebir hesabı yok |
| `debit`/`credit` | Borç/Alacak (şirket TL) | `Borc`/`Alacak` | ✅ benzer |
| `party_type`+`party` | Cari (polimorfik) | `PartnerId` | ✅ |
| `voucher_type`+`voucher_no` | Kaynak belge | `SourceDocType`+`SourceDocId` | ✅ |
| `voucher_detail_no` | Kaynak satır (SLE ile aynı) | YOK | ❌ |
| `against_voucher` | Mahsuplaşma hedefi (ödeme→fatura) | YOK (PaymentPlan ayrı tutuyor) | ⚠️ |
| `cost_center` | Masraf merkezi (zorunlu boyut) | YOK | ❌ |
| `is_cancelled` | İptal bayrağı, silinmez | `IsDeleted` (yanlış semantik) | ❌ |

- ERPNext'te **muhasebe fişi = N adet GL Entry tek tabloda**, `voucher_no` ile gruplanır;
  ayrı başlık/satır tablosu yok. Çift-kayıt dengesi (Σdebit=Σcredit) **uygulama katmanında**
  zorlanır (tablo CHECK constraint değil) `[REPO]`.
- Operax AccountMovement cari-bazlı (Partner) tek defter; **kebir hesabı / hesap planı yok.**
  Bu bilinçli olabilir (resmi muhasebe M16 ile Logo/Mikro'ya devrediliyor — AR-008), ama
  perpetual stok değerlemesi için en azından stok/COGS karşı-hesap köprüsü gerekir.

### 2.3 Perpetual inventory: stok hareketi → muhasebe nasıl bağlanıyor

ERPNext akışı (aynı submit transaction'ı) `[REPO]` `stock_controller.py`:
1. `update_stock_ledger()` → SLE yazılır (valuation hesaplanır).
2. `make_gl_entries()` → `is_perpetual_inventory_enabled` ise her SLE'nin `stock_value_difference`'ı
   okunur, **iki dengeli GL Entry** üretilir:
   - Dr ambar varlık hesabı = `stock_value_difference`
   - Cr karşı hesap (COGS / SRBNB / Stock Adjustment) = `-stock_value_difference`

Standart hesap haritası `[DOC]`:
```
Mal Kabul : Dr Stock In Hand      / Cr Stock Received But Not Billed (SRBNB)
Alış Fat. : Dr SRBNB              / Cr Tedarikçi (AP)
Sevkiyat  : Dr COGS               / Cr Stock In Hand
Sayım farkı: Stock Adjustment    ↔ Stock In Hand
```

**Operax karşılığı:** YOK. `sp_ReceivingPost`/`sp_ShippingPost` AccountMovement'a hiç yazmıyor;
`sp_GenerateSalesInvoiceFromShipping` SalesInvoice+PaymentPlan yazıyor ama cari deftere değil
`[OPERAX]`. **SRBNB (Mal Kabul Edilen Faturalanmamış) köprü hesabı** kavramı da yok —
mal-kabul↔fatura zaman farkını muhasebede kapatan endüstri-standart eksik.

### 2.4 FIFO vs Moving Average

| | ERPNext `[REPO]` | Operax `[OPERAX]` |
|---|---|---|
| Yöntem saklama | `Item.valuation_method` (FIFO/Moving Average/LIFO); boşsa Stock Settings (varsayılan FIFO) | Parametre metni var, sadece MOVING_AVG uygulanır |
| FIFO veri yapısı | SLE.`stock_queue` JSON `[[qty,rate],…]` — **ayrı tablo değil** | YOK |
| FIFO mantığı | `valuation.py FIFOValuation`: `add_stock`→kuyruk sonu, `remove_stock`→kuyruk başı (index 0) | YOK |
| Moving avg | `get_moving_average_values`: `(eski_qty*rate+giriş*incoming)/toplam`; çıkışta rate korunur | aynı formül (`db_objects_starter.sql:44`) ✅ |
| Yöntem değişimi | Item'da **stok hareketi varsa değiştirilemez** (validate) | kontrol yok |
| Batch nüansı | Batch item'da `stock_queue` tutulmaz, batch-wise ortalama (PR #29804) | — |

**R2 dersi:** FIFO için **ayrı CostLayer tablosu ZORUNLU DEĞİL.** ERPNext FIFO kuyruğunu
satır-içi JSON'da taşıyor. Operax StockMovement'a `StockQueueJson NVARCHAR(MAX)` + SP içi
FIFO tüketim mantığı eklenerek aynı model SQL-first kurulabilir. **Tuzak:** batch/lot-takipli
item'da JSON kuyruk çalışmaz (ERPNext bundan vazgeçti) — Operax FIFO+lot tasarlarsa baştan
batch-wise ortalama planlamalı.

### 2.5 Immutable ledger + reversal + repost (R1 çözüm deseni)

`[REPO]`:
- `stock_ledger_entry.py on_cancel()` → `throw("Individual SLE cannot be cancelled...")`.
- `gl_entry.py on_cancel()` → aynı throw. **Tekil defter satırı edit/cancel YASAK.**
- v13+ Immutable Ledger: iptalde kayıt **silinmez**, `is_cancelled=1` + **ters kayıt** yazılır.
- Repost: geçmiş tarihli giriş sonrası sonraki tüm SLE'ler `update_entries_after`/
  `Repost Item Valuation` doctype ile **background job** olarak yeniden hesaplanır.
- Dönem kilidi: `stock_frozen_upto` + rol-bazlı geçmiş-tarih izni; saf immutable modda geriye
  dönük giriş kısıtlanır (repost maliyeti yüzünden).
- Nüans `DOĞRULANMADI` netlik: ters kaydın posting_date'i orijinal mi cancel tarihi mi —
  ERPNext issue'larında tartışmalı (#30547).

**Operax dersi:** document-immutability.md §6 ile uyumlu. Eklenecekler: (1) AccountMovement'tan
`IsDeleted` kaldır, `IsReversed`/ters kayıt ikilisine geç; (2) tekil defter satırı UPDATE/DELETE'i
DB trigger ile engelle; (3) `stock_frozen_upto` benzeri dönem kilidi (VUK/audit uyumu); (4)
geriye dönük girişe izin verilecekse Hangfire repost job'ı (senkron değil).

---

## 3. Multi-Store/Company İzolasyonu — Smartstore & nopCommerce (R3) `[REPO]`

### 3.1 Ortak desen (ikisi de aynı)
- **EF global query filter KULLANMIYORLAR.** İzolasyon iki parça:
  (a) `LimitedToStores` bool marker (false=tüm store'lar görür),
  (b) `StoreMapping(EntityId, EntityName, StoreId)` **polimorfik** köprü tablo.
- Opt-in görünürlük: mapping yoksa = herkese açık. Her sorguya filtre **elle** eklenir.

### 3.2 Mekanizma farkı

| | Smartstore | nopCommerce |
|---|---|---|
| Marker | `IStoreRestricted` | `IStoreMappingSupported` |
| Filtre konumu | **IQueryable extension** `query.ApplyStoreFilter(storeId)` | **Servis** `_storeMappingService.ApplyStoreMapping(query, storeId)` |
| Teknik | `subQuery.Contains(x.Id)` | correlated `Any(sm => …)` |
| Bypass | `QuerySettings.IgnoreMultiStore` (scoped) | `IgnoreStoreLimitations` (global ayar) |
| Tekil yetki | `AuthorizeAsync(entityName,id,storeId)` | `AuthorizeAsync(entity,storeId)` |

Her servis metodu kendi sorgusuna elle ekliyor (`ProductService.SearchProductsAsync`,
`CategoryService.GetAllCategoriesAsync` …). **Hiçbiri compile-time/global garanti vermiyor.**

### 3.3 Operax'ın avantajı + Dapper'a uyarlanabilir desenler

> Operax `CompanyId`'yi **her satırda zorunlu** tutuyor (opt-in store-mapping değil) →
> "her kayıt tam bir firmaya ait" = e-ticaret store-mapping'inden **daha sıkı izolasyon.**
> Eksik tek şey: her sorguda uygulanmasının **merkezi garantisi.**

| # | Desen | Operax felsefesine uyum | Artı | Eksi |
|---|---|---|---|---|
| **1** | **SQL TVF/View, `@CompanyId`-sargılı** (mevcut `tvf_InventoryBalance` deseni) | ⭐ EN UYGUN (SQL-first §4) | İzolasyon DB'de; param-cache hızlı; CLAUDE.md zaten bu yönde | Her tablo için TVF boilerplate; sadece OKUMA tarafı |
| **2** | **Merkezi Dapper wrapper** `QueryScopedAsync(sql, companyId)` — `{CompanyFilter}` placeholder yoksa runtime throw | Güçlü "unutma" garantisi | Tek geçiş noktası; param unutmak imkânsız; audit merkezi | JOIN'de hangi tablo belirsiz; string manipülasyon riski; SP'leri kapsamaz |
| **3** | **Statik analiz/test guard** — string SQL'de WHERE var ama CompanyId yoksa build fail (Roslyn/`cwm-roslyn-navigator`) | Tamamlayıcı emniyet ağı | CI'da yakalar; retroaktif tarar | False +/−; "var" ≠ "doğru tabloda"; sahte güven |
| **4** | Marker interface + generic repository base | ❌ Transaction Script'e ters | tip-güvenli | Repository pattern Operax'ta yok; ham SQL'i kısıtlar |

**Öneri:** **Desen 1 (birincil) + Desen 3 (emniyet ağı).** Desen 2 yazma/ad-hoc için ikincil.
Desen 4 önerilmez. → Bu, mevcut **plan 12 (CompanyId izolasyon)** kapsamına eklenmeli.

`DOĞRULANMADI`: nopCommerce'in store için `HasQueryFilter` kullanmadığı kod düzeyinde (dosya 404)
kesinleşmedi; servis kodu + doküman per-query mantığını gösteriyor.

---

## 4. Destek Repolar — (a çalınır / b görmezden gelinir / c gap)

### 4.1 RealAhmedOsama/Warehouse-Management-System (.NET 8) `[REPO]` README
- **(a)** Lokasyon `IsReceivable`/`IsPickable` bayrakları; Item `RequiresLot`/`RequiresSerial`
  (data-driven guard); **Available vs Allocated** stok ayrımı; immutable Movement + tek `MovementType` enum.
- **(b)** Clean Arch 4 katman + Repository/UoW + EF code-first + C# Value Object/rich entity —
  iş mantığı C#'ta; Operax'ta SP'de. Kopyalanmaz.
- **(c) GAP:** Available vs Allocated ayrımı (`tvf_InventoryBalance` tek bakiye veriyor →
  picking rezervasyon doğruluğu eksik); lokasyon eligibility flag'leri.

### 4.2 fjykTec/ModernWMS (.NET 7 + Vue) `[REPO]` README + `[DOC]` DeepWiki
- **(a)** KOBİ modül seti: ASN tabanlı Receiving · pick→**pack**→dispatch + **delivery confirmation** ayrımı.
- **(b)** Vue SPA + ayrı API + Nginx; çoklu-DB soyutlama — Operax Razor tek-uygulama, tek SQL Server. Alınmaz.
- **(c) GAP:** **ASN ara belge tipi** (tedarikçi-bildirimli beklenen sevkiyat; PO→Receiving arasına);
  pack/teslim-onayı adımlarının ayrışması. (CycleCount Operax'ta zaten var → bu kalemde Operax önde.)
  `DOĞRULANMADI`: ModernWMS lot/serial/stocktaking modül varlığı.

### 4.3 openwms/org.openwms (Java) `[REPO]` README + `[DOC]` DeepWiki
- **(a)** Domain decomposition CORE/COMMON/**WMS (ne)** / **TMS+MFC (nasıl)**. "Ne yapılacak"
  (operasyon gerçeği) ile "nasıl hareket" (routing/karar) ayrı katman — **Operax'ın "ERP truth /
  canlı operasyon / Decision" 3-katman vizyonunun birebir aynası.** Transport Unit/Order kavramı.
- **(b)** Mikroservis + servis-başına-DB + RabbitMQ + BPMN engine (Camunda) + donanım sürücüleri —
  Operax kasıtlı tek-uygulama/tek-DB/SQL-deterministik. Taban tabana zıt. Kesinlikle alınmaz.
- **(c) GAP:** Karar/yönlendirme katmanının operasyondan **kavramsal ayrımı.** Operax'ta
  allocation/öneri mantığı operasyon SP'lerine gömülü; ayrı "Decision" yüzeyi olmalı (ayrı SP/function
  bile olsa). Transport Unit (taşınan birimin içerikten bağımsız hareketi) kavramı.

### 4.4 awright18/Slice (.NET, Transaction Script + Dapper) `[REPO]` README + `[DOC]`
- **(a)** Felsefe birebir Operax: Wlaschin "Reinventing Transaction Script" + Bogard vertical slice +
  Dapper raw SQL, ORM reddi. **CQS sertleştirme:** command (Brighter) ≠ query (Darker), ayrı pipeline.
  Operax dersi: `OnPost` (komut→SP) ile `OnGet` (sorgu→TVF) disiplinli ayrışmalı. FluentValidation girişte.
- **(b)** Brighter/Darker command-bus + Refit/Polly + SPA client — Operax PageModel zaten request/handler;
  ek dispatcher gereksiz soyutlama. Alınmaz.
- **(c) GAP / SINAMA:** **Slice anatomisi tutarlılık denetimi.** Slice her feature'da AYNI iskeleti
  zorluyor. Operax'ta sınanacak: her `Features/<Modül>/` slice'ı ince-PageModel→SP mi, yoksa bazıları
  iş-mantığı-sızmış-şişman mı? → `operax-erp-wms-auditor` ile audit edilmeli.
  `DOĞRULANMADI`: Slice per-slice dosya anatomisi (tree render erişilemedi).

---

## 5. R1–R4 Çözüm Deseni Matrisi

| Yara | Operax durumu `[OPERAX]` | En iyi referans deseni | Operax'a uyarlama (Dapper/SQL-first saygılı) | İlgili plan |
|---|---|---|---|---|
| **R1** Ledger'da IsDeleted / immutability ihlali | AccountMovement `IsDeleted` var; StockMovement `IsCancelled` var ama hiç set edilmiyor; ters-kayıt SP'si yok | **ERPNext**: tekil satır cancel YASAK + `is_cancelled=1` + ters kayıt; repost background job | (1) AccountMovement'tan IsDeleted kaldır → `IsReversed`+ters kayıt; (2) DB trigger ile satır UPDATE/DELETE engelle; (3) `sp_*Reverse` SP'leri yaz; (4) `stock_frozen_upto` dönem kilidi | **plan 14** |
| **R2** FIFO yok, sadece Moving Avg | ItemCost tek AvgCost; FIFO tablo/kod yok; "İleri" sanılmış (yanlış) | **ERPNext**: SLE.`stock_queue` JSON `[[qty,rate]]`, ayrı tablo gerekmez | StockMovement'a `StockQueueJson` + SP içi FIFO tüketim; Item.ValuationMethod ilk hareketten sonra kilitli. TR enflasyonunda COGS/vergi maddi → **AR-006: "Olgun/gerekli"** | roadmap (AR-006) |
| **R3** Multi-company sızıntı (global filter yok) | Her sorguda elle WHERE CompanyId; unutulursa sızar | **Smartstore/nop**: merkezi `ApplyStoreFilter` (ama elle); **Operax zaten daha sıkı (CompanyId zorunlu)** | Desen 1 (TVF `@CompanyId`-sargılı) + Desen 3 (analyzer guard); SP'lerde `@CompanyId` zaten zorunlu | **plan 12** |
| **R4** GUID NEWID clustered PK fragmentasyonu | StockMovement + AccountMovement default clustered NEWID() | Standart SQL Server pratiği | ADR: BIGINT identity clustered PK + GUID nonclustered UNIQUE, VEYA NEWSEQUENTIALID() default | **plan 14** |
| **R0** (yeni) Perpetual accounting yok, cari drift | Hiçbir onay SP'si AccountMovement beslemiyor; backfill sonrası drift | **ERPNext**: `stock_value_difference` → 2 dengeli GL/harekete; SRBNB köprü hesabı | sp_ReceivingPost/ShippingPost AccountMovement'a atomik yazsın; SRBNB karşı-hesap kavramı | **YENİ — plan adayı** |

---

## 6. Önceliklendirilmiş "Çalınacaklar" Backlog'u

> Etki (E) / Maliyet (M): Y=Yüksek, O=Orta, D=Düşük. Sıra = (yüksek etki + düşük maliyet) önde.

| # | Çalınacak | Kaynak | E | M | Not |
|---|---|---|---|---|---|
| B1 | **R3: TVF `@CompanyId`-sargı + analyzer guard** | Smartstore/nop + Operax mevcut TVF | Y | D | Existential risk; mevcut desen genişletme. **plan 12** |
| B2 | **R1: AccountMovement IsDeleted→reversal + DB trigger** | ERPNext immutable ledger | Y | O | VUK/audit; **plan 14** |
| B3 | **R0: HAFİF cari besleme — onay SP'leri AccountMovement'a atomik borç/alacak** | ERPNext (sadece subledger kısmı) | Y | O | **K3.** GL/kebir/COGS/SRBNB **YOK.** Omurga çift-taraflıya hazır, modül açık değil. → **plan 16** |
| B12 | **Dönem kontrolü (period control) — ZAMAN bazlı + istisna/iz** | SAP OB52 / Logo dönem kapatma | Y | O | **K4 + K8.** AccountingPeriod (firma bazlı) + sp_GuardPeriodOpen + trigger + OPEN/CLOSED/LOCKED + GuardStockFrozen kancası. **K8: PeriodOverrideLog** (CLOSED→yetki+gerekçe+atomik log; LOCKED→istisna YOK; self-approval engeli; rapor view-hazır). → **plan 14** (aynı omurga) |
| B13 | **Sayım freeze — STOK SATIRI bazlı kilit** | — (Operax domain kararı) | O | O | **K5.** CompanyId+Warehouse+Bin+Item dondurma; bölge/oturum küme; hareket yasak (iptal→giriş→yeniden say); `sp_GuardStockFrozen`. K4'ten FARKLI (zaman değil satır). → **M08 / S7** (bugün spec notu) |
| B14 | **Cari mutabakat freeze — PARTNER+TARİH bazlı kilit** | — (Operax domain kararı) | O | O | **K9.** Mutabakat imzalanan partnerin X-öncesi cari hareketleri kilitli; geçmişe giriş override+log (K8) gerektirir; `sp_GuardPartnerReconciled` kancası. Üçüncü kilit ailesi (zaman/stok/partner). → **M11 / sonra** (bugün spec notu) |
| B15 | **Firma-bazlı yetki — Model 3 (kişi+firma+rol)** | nopCommerce/Smartstore çok-mağaza rol | Y | O | **K10.** `UserCompany(UserId,CompanyId,Role)`; switch-company **rol-aware + erişim kontrollü + antiforgery**. **plan 12 izolasyonunun güvenlik ön koşulu** (claim serbest değişirse izolasyon dekoratif). Omurga tam, kullanım düz. → **plan 13 §3** |

> **EK REFERANS (2026-05-30): Mikro V16/V17 şema incelemesi** → `docs/MIKRO_V16_ANALYSIS.md` (V17 mirror,
> 6 kritik tablo kolon kolon, reference-researcher ajanı). Operax yön kararlarını **dış kaynakla doğruladı:**
> **🔴 POLYMORPHIC LEDGER DOĞRULANDI** (en kritik): Mikro'da **her evrak tipi için ayrı tablo YOK** — tüm stok
> etkisi tek `STOK_HAREKETLERI` (sth_tip+sth_cins+sth_evraktip), tüm cari etki tek `CARI_HESAP_HAREKETLERI`
> (cha_evrak_tip+cha_tip+cha_kaynak). = Operax `StockMovement`+MovementType / `AccountMovement` kararı birebir.
> Yeni belge tipi → yeni ledger tablosu GEREKTİRMEZ, sadece yeni tip. **Ama belge başlığı:** Operax ayrı
> Header/Line tabloları (normalize, immutability+durum makinesi için) tutar — Mikro tek-tabloya gömer; Operax
> hibriti üstün (ledger birleşik + belge ayrışık). Detay: MIKRO_V16_ANALYSIS §0.5 ·
> **K6 DOĞRULANDI** (stok_hareketleri'nde running-balance/StockValue snapshot YOK; stoklar'da OnHand YOK) ·
> **K1/K3 DOĞRULANDI** (cari_hesap_hareketleri ≠ muhasebe_fisleri; `fis_ticari_uid` köprü = subledger→GL gevşek
> bağ, perpetual değil) · **K10 DOĞRULANDI** (`_firmano`+`_subeno` her satırda, global filter yok) ·
> **R4 KISMEN** (`_Guid` PK + artan int var; CLUSTERED fiziksel niteliği DOĞRULANMADI) · **K7 KISMEN** (maliyet
> harekette; ayrı maliyet-detay tablosu erişilemedi). Yeni yön gerektirmez. Operax GAP: çek konum izleme
> (`sck_nerede_cari_kodu`). NOT: ilk taslaktaki "320 tablo/tableData JSON/msf_borc" iddiaları halüsinasyondu, silindi.
| B4 | **R4: Ledger clustered PK düzelt (NEWSEQUENTIALID / BIGINT)** | SQL Server std | O | O | **plan 14** ile birlikte (aynı migration) |
| B5 | **R2: FIFO — kalıcı `StockCostConsumption` eşleme tablosu** | ERPNext stock_queue + Mikro STOK_HAREKET_MALIYET_DETAYLARI | O | O | **K7 (revize 2026-05-30).** Severity **Gerekli**. ✅ **KARAR:** kalıcı eşleme tablosu `StockCostConsumption(CikisMovementId, GirisMovementId, Miktar, Maliyet)` (Mikro §1.5 deseni); çıkış↔giriş katman izi denetlenebilir + iade'de geri-açılabilir. Snapshot reddi (K6) korunur. Roadmap |
| B16 | **Açık-kalem kapama/eşleme tablosu** (cari) | Mikro CARI_HAREKET_BORC_ALACAK_ESLEME (Tablo 74) | O | O | **OPERAX GAP.** "Hangi tahsilat hangi faturayı kapattı" izi yok; AccountMovement net bakiye tutuyor ama kalem eşleme yok. `AccountReconciliation(Borc/Alacak MovementId, Tutar, Bileşen)`. Doğru yaşlandırma + açık-fatura raporu + K9 mutabakat ile sinerji. → değerlendir |
| B17 | **Eksik evrak/hareket tipleri** (irsaliye↔fatura, iade, fire, virman, açılış…) | Mikro sth_cins/sth_evraktip/cha_cinsi tam enum (MIKRO §12) | Y | O | **OPERAX GAP.** Stok 5 MovementType ↔ Mikro 14 cins + 19 evraktip; cari 9 ↔ Mikro 42 cins. **En yüksek 4: E1 irsaliye↔fatura, E2 iade, E4 fire, E11 virman (Plan 11).** Çözüm (§0.5 uyumlu): yeni ledger tablosu AÇMA → SourceDocType kataloğu genişlet + ADJUST sebep kodu + belge zinciri. Çek statü: TEMİNAT+KISMİ ÖDEME ekle (sck_sonpoz 3,9). → MIKRO §12 karşılaştırma tablosu |
| B6 | **Available vs Allocated stok ayrımı** | RealAhmed WMS | O | O | Picking doğruluğu; `tvf_InventoryBalance` rezervasyon kolonu |
| ~~B7~~ | ~~SLE-snapshot kolonları~~ | — | — | — | **İPTAL — K6.** Snapshot reddi; `SUM(QtyBase) WHERE IsCancelled=0` kalıcı; repost altyapısı da kapsam dışı |
| — | **Periyodik GL muhasebeleştirme modülü** (gelecek-iş, plan AÇILMADI) | SAP/Logo/Odoo + **Mikro hesap planı/posting-rule (MIKRO §3.5)** | Y | Y | **K1/K2.** Ön koşul: **muhasebe-mevzuat skill'i**. ✅ **Posting-rule deseni netleşti (Mikro §3.5.3):** 3 yapı taşı — (1) HesapPlani(tip/çalışma şekli/hiyerarşi) (2) **PostingRule(MuhasebeGrup+HareketTipi→HesapKodu)** normalize eşleme (3) masraf merkezi boyutu. Muhasebeleştirme SP: subledger→grup+yön→hesap→fiş, `fis_ticari_uid` geri-bağ. e-Defter üretimi (K5) kapsam dışı |
| B8 | **Lokasyon IsReceivable/IsPickable** | RealAhmed WMS | D | D | Hedef hücre eligibility guard |
| B9 | **Slice tutarlılık audit'i** | Slice | D | D | `operax-erp-wms-auditor` ile çalıştır |
| B10 | **ASN ara belge tipi** | ModernWMS | D | O | 3PL/tedarikçi entegrasyon senaryosu |
| B11 | **Decision/routing katmanı ayrımı** | OpenWMS | D | Y | 3-katman vizyon somutlaşması; uzun vade |

---

## 7. KARARLAR (2026-05-29 oturum 2 — Fikri) + Kalan Açık Sorular

> Aşağıdaki K1–K7 kararları bu çalışmanın açık sorularını kapattı. Kalan tek açık soru §7.8'de.

- **K1 — Resmi muhasebe modeli:** Operax resmi defteri (yevmiye/kebir/çift-taraflı GL) **ileride**
  tutacak; **gerçek-zamanlı GL değil.** Model: subledger → GL **periyodik muhasebeleştirme** (posting
  period; SAP/Logo/Odoo deseni). Operasyon alt-defterleri (StockMovement, AccountMovement,
  FinancialTransaction) gerçek-zamanlı; muhasebe katmanı bunları aylık/seçimli yevmiye fişine çevirir.
  VISION "ERP truth / canlı / Decision" doktriniyle hizalı: muhasebe = truth katmanına periyodik yansıtma.
- **K2 — Muhasebe modülü ertelendi.** Yazılacağı gün **önce muhasebe-mevzuat skill'i** (VUK, e-Defter
  tebliğleri, hesap planı, berat, GİB formatları) yapılacak. Bugün GL katmanı / muhasebeleştirme SP'si /
  hesap planı **YAZILMAZ.** Sadece gelecek-iş kaydı.
- **K3 — Cari defter besleme (R0/B3) HAFİF:** AccountMovement gerçek cari mutabakat defteri olur ve
  onay SP'leri (sp_ReceivingPost, sp_ShippingPost, sp_GenerateSalesInvoiceFromShipping) atomik
  borç/alacak yazar. **YAPILMAZ:** kebir fişi, COGS, SRBNB köprü hesabı, çift-taraflı GL, masraf merkezi,
  hesap planı (= K1 ertelenmiş modül). Omurga çift-taraflıya HAZIR (Borc/Alacak var) ama modül AÇILMAZ.
- **K4 — Dönem kontrolü (period control) BUGÜN, ZAMAN BAZLI, sadece MEKANİZMA.** Muhasebe değil; operasyonel
  veri bütünlüğü (SAP OB52 / Logo dönem kapatma muadili). Tetikleyiciler tarih/dönem bazlı: muhasebe ay kapanışı,
  KDV beyan dönemi, (ileride) e-Defter berat→mutlak. `AccountingPeriod` (CompanyId **firma bazlı** + yıl/ay +
  OPEN/CLOSED/LOCKED) + `sp_GuardPeriodOpen(@companyId,@date)` (her hareket SP'sinin ilk satırı) + DB trigger
  (emniyet ağı) + statü makinesi (OPEN→CLOSED geri alınır/iz bırakır; CLOSED→LOCKED tek yön, dönüşsüz) +
  **`sp_GuardStockFrozen` kancası** (boş/no-op; K5 sayım freeze M08'de dolduracak).
  **YAPILMAZ (bugün):** dönem kapatma UI, otomatik kapatma, kapanış raporu, çapraz kontrol. Statü admin elle.
  → MEKANİZMA KUR, SÜREÇ KURMA. (→ plan 14 §2.d)
- **K5 — Sayım freeze (stok/satır bazlı kilit) KARAR VERİLDİ; uygulama M08/S7.** Dönem kilidinden FARKLI:
  tüm dönemi değil **belirli satırları** dondurur. Granülarite CompanyId+Warehouse+Bin+Item (gerekirse Lot) —
  **depo bazlı değil**. Bölge/oturum = dondurulan satır kümesi; dondurulmuş kaleme hareket yasak (çözüm:
  oturum iptal→giriş→yeniden say; biten oturumlar korunur, atomik değil). Guard: stok SP'leri
  `sp_GuardStockFrozen(@companyId,@warehouseId,@binId,@itemId)`'dan geçer. **Bugün sadece M08 spec'ine yazılı
  not** (`docs/MODULE_SPECS/M08_CycleCount_Freeze.md`), kod yok.
- **K8 — Dönem kilidi istisna + iz katmanı (plan 14'e EKLENDİ — kilidin ayrılmaz parçası).** Kilit tek başına
  yetmez; kontrollü istisna + zorunlu iz birlikte. Guard statüye bağlı: OPEN→serbest; **CLOSED→yetkili+zorunlu
  gerekçe ile geçilir, her geçiş `PeriodOverrideLog`'a atomik loglanır** (yetkisiz/gerekçesiz throw);
  **LOCKED→İSTİSNA YOK, koşulsuz throw** (tek çözüm sonraki açık döneme düzeltme). PeriodOverrideLog **silinmez**;
  alanlar: SourceDoc, hedef tablo, MovementDate (hareketin ait olduğu tarih) **≠** CreatedAt (işlem anı, ikisi
  ayrı), LockType, OverriddenBy, ReasonCategory (LATE_DOCUMENT/CORRECTION/SYSTEM_ERROR/OTHER)+ReasonText (zorunlu),
  ApprovedBy (opsiyonel). **Yetki dar** (muhasebe sorumlusu/yönetici) + **görevler ayrılığı: OverriddenBy ≠ ApprovedBy**.
  Rapor view'a hazır model (ekran sonra). → plan 14 §2.f-i.
- **K9 — Cari mutabakat freeze (partner+tarih bazlı kilit) KARAR NOTU; uygulama M11/sonra.** Üçüncü kilit ailesi:
  ne tüm firma (zaman), ne tüm stok (satır) — **belirli partnerin belirli tarihe kadarki cari hareketlerini**
  dondurur. Müşteriyle X-tarihli bakiye mutabakatı imzalandıysa, partnerin X-öncesi cari hareketleri kilitlenir;
  geçmişe giriş override+log (K8) gerektirir. Guard: `sp_GuardPartnerReconciled` (GuardStockFrozen kardeşi).
  **Bugün sadece M11 spec'ine yazılı not** (`docs/MODULE_SPECS/M11_Finance_Procedures.md`), kod yok.

  > **KİLİT AİLESİ (3 tür — tek tabloya birleştirilmez, hepsi aynı guard çağrı zincirinden geçer):**
  > (1) **Zaman bazlı** → `AccountingPeriod` (ay kapanış/KDV/berat) — plan 14, BUGÜN ·
  > (2) **Stok satırı bazlı** → sayım freeze — M08, S7 ·
  > (3) **Partner+tarih bazlı** → cari mutabakat freeze — M11, sonra.
  > İz kaydı (`PeriodOverrideLog`) üçünü `LockType` ile ayırarak tutar; kilit tabloları ayrıdır.
- **K5 — e-Defter/GİB:** Operax e-Defter **ÜRETMEZ** (XML/imza/GİB gönderim — yıllar sonrası ayrı iş).
  Sadece kapalı/beratlı dönemi **bilir ve saygı gösterir** (LOCKED dışarıdan sinyalle: mali müşavir "kapandı"
  der, admin LOCKED'a çeker).
- **K6 — Snapshot reddi (B7 ÇÖP):** StockMovement'a QtyAfterTransaction/ValuationRate/StockValue
  **EKLENMEZ.** `SUM(QtyBase) WHERE IsCancelled=0` modeli kalıcı. Snapshot reddi → repost altyapısı da
  kapsam dışı; FIFO snapshot'sız (SP içi anlık kuyruk).
- **K7 — FIFO (R2/AR-006):** severity **"İleri" → "Gerekli"** (TR enflasyon COGS/vergi etkisi). Snapshot'sız,
  SP içi anlık kuyruk; ayrı CostLayer tablosu gerekmez (ERPNext stock_queue deseni). B7'ye bağımlı DEĞİL.

### 7.8 Kalan Açık Sorular

1. **R0 (perpetual accounting) bir plan açılacak mı?** Bu, review'da yer almayan ama R1–R4'ten
   büyük yapısal açık. AR-008 (resmi muhasebe M16 ile Logo/Mikro'ya devredilecek mi?) kararına
   bağlı: Eğer cari/COGS muhasebesi Operax'ta tutulacaksa B3 zorunlu; Logo/Mikro'ya devredilecekse
   AccountMovement'ın rolü "sadece operasyonel cari takip" olarak küçülür ve drift kabul edilebilir.
   **→ Önce AR-008 netleşmeli.**

2. **R2 (FIFO) önceliği yükseltilsin mi?** Audit "İleri" demişti; bu çalışma TR enflasyon ortamında
   COGS/vergiyi maddi etkilediği için AR-006'yı "gerekli"ye çıkarıyor. Roadmap'te öne alınsın mı?

3. **B7 (StockMovement snapshot kolonları) onaylanır mı?** B3 ve B5'in ortak ön koşulu; tek migration'da
   yapılırsa verimli ama StockMovement şemasına dokunur (Tier 3, geri-dönüşü zor).

4. **Mevcut plan 12/14 bu çalışmanın desenlerini absorbe etsin mi, yoksa ayrı plan mı?**
   Öneri: plan 12'ye Desen 1+3, plan 14'e ERPNext reversal + clustered PK detayını ekle (yeni plan
   açma — duplikasyon). R0 için yeni plan (16?) gerekebilir.

---

## 8. Kaynak / Confidence Özeti

- **`[REPO]` (kod okundu):** ERPNext develop (valuation.py, stock_ledger.py, stock_controller.py,
  SLE/GL Entry json+py, repost_item_valuation.py); Smartstore main (IStoreRestricted, StoreMapping,
  StoreMappingService, IStoreRestrictedQueryExtensions); nopCommerce develop (StoreMapping,
  StoreMappingService, Product/Category/ManufacturerService); Operax tüm M02/M11 şema + db_objects.
- **`[DOC]`:** docs.frappe.io (perpetual inventory, SRBNB, valuation, immutable ledger); DeepWiki
  (ModernWMS, OpenWMS, Slice); GitHub README'ler.
- **`DOĞRULANMADI` (dürüstlük):** Operax inline PK'ların `CLUSTERED` harfiyle yazılı olmaması (default
  davranışa dayanıyor); ERPNext ters-kayıt posting_date'i (issue tartışmalı); `get_incoming_rate` tam
  imzası; nopCommerce store `HasQueryFilter` yokluğunun kod kanıtı; ModernWMS lot/serial modülü;
  Slice per-slice dosya anatomisi.

---

*Bu belge salt-okuma araştırma çıktısıdır. Hiçbir üretim kodu veya şema değiştirilmemiştir.
Uygulama (B1–B11) ayrı plan onayına tabidir.*
