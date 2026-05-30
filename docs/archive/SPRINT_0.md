# SPRINT 0 — Foundation Fix
> Hedef: Build geçiyor · Uygulama ayağa kalkıyor · 0 hata · 0 uyarı
> Önkoşul: Yok — ilk sprint
> Kabul Kriteri: `dotnet build src/Operax.Web/Operax.Web.csproj` → 0 hata, 0 uyarı

---

## Görev Listesi

### BLOK 1 — Kritik Build Hataları (19 hata)
> Bu blok tamamlanmadan uygulama ayağa kalkmaz.

---

#### TASK-S0-01 · Program.cs AddDefaultIdentity Hatası
```
Dosya  : src/Operax.Web/Program.cs
Satır  : 16
Hata   : CS1061 — 'IServiceCollection' does not contain 'AddDefaultIdentity'
```

**Yapılacak:**
1. `Program.cs` oku
2. `Operax.Web.csproj` oku — `Microsoft.AspNetCore.Identity.UI` paketi var mı?
3. Paket varsa → `using Microsoft.AspNetCore.Identity;` eksik mi kontrol et
4. Alternatif: `AddDefaultIdentity` yerine şu kullanılabilir:
   ```csharp
   builder.Services.AddIdentity<IdentityUser, IdentityRole>(options =>
       options.SignIn.RequireConfirmedAccount = false)
       .AddEntityFrameworkStores<OperaxDbContext>()
       .AddDefaultTokenProviders();
   ```
5. Build al, hata geçiyor mu kontrol et

- [ ] Tamamlandı

---

#### TASK-S0-02 · Shipping/Details IsNew Hatası (cshtml)
```
Dosya  : src/Operax.Web/Features/Shipping/Details.cshtml
Satırlar: 4, 10, 11, 17, 23, 76
Hata   : CS1061 — 'DetailsModel' does not contain 'IsNew'

Dosya  : src/Operax.Web/Features/Shipping/Details.cshtml.cs
Satır  : 140
Hata   : CS0103 — 'IsNew' not found in current context
```

**Yapılacak:**
1. `Features/Shipping/Details.cshtml.cs` oku
2. `IsNew` property tanımlı mı bak (`public bool IsNew => Header.Id == Guid.Empty;`)
3. Tanımlıysa ama hata varsa → `Header` default değeri `new()` olduğundan `Id = Guid.Empty` zaten
4. `Details.cshtml.cs` satır 140: `if (IsNew)` → `if (Header.Id == Guid.Empty)` yap (daha açık)
5. `Details.cshtml`'de `@Model.IsNew` çağrıları doğru mu bak

- [ ] Tamamlandı

---

#### TASK-S0-03 · CycleCount DTO Property Eksikleri
```
Dosya  : src/Operax.Web/Features/CycleCount/Details.cshtml.cs
Satır  : 73
Hata   : CS0103 — 'BinId', 'ItemId', 'QtySystem', 'QtyCounted' not found
```

**Yapılacak:**
1. `Features/CycleCount/Details.cshtml.cs` oku
2. `OnPostAddLineAsync(Guid id, Guid binId, ...)` metodunu bul
3. Anonim obje `new { BinId, ItemId, QtySystem, QtyCounted }` yerine
   `new { BinId = binId, ItemId = itemId, QtySystem = qtySystem, QtyCounted = qtyCounted }` kullan
4. Aynı metodda satır 100 — `line.QtyDifference` null ise:
   `CountLineDto`'da `QtyDifference` property hesaplanıyor mu kontrol et
   (genellikle `decimal QtyDifference => QtyCounted - QtySystem;` gibi computed olmalı)

- [ ] Tamamlandı

---

#### TASK-S0-04 · MasterData/Items UomId Eksikleri
```
Dosya  : src/Operax.Web/Features/MasterData/Items/Details.cshtml.cs
Satırlar: 83, 90
Hata   : CS0103 — 'UomId' not found
```

**Yapılacak:**
1. `Features/MasterData/Items/Details.cshtml.cs` oku
2. İlgili metod parametrelerine `Guid uomId` eklenmiş mi kontrol et
3. ItemUOM veya ItemBarcode ekleme metodlarında `uomId` parametresi eksikse ekle
4. Ayrıca satır 27 — CS8601 null assignment uyarısı (TASK-S0-09 kapsamında da)

- [ ] Tamamlandı

---

#### TASK-S0-05 · Transfer/Details FromBinId / ToBinId Eksikleri
```
Dosya  : src/Operax.Web/Features/Transfer/Details.cshtml.cs
Satır  : 79
Hata   : CS0103 — 'FromBinId', 'ToBinId' not found
```

**Yapılacak:**
1. `Features/Transfer/Details.cshtml.cs` oku
2. `OnPostAddLineAsync` veya ilgili metodu bul
3. `StockTransferLineDto`'ya `Guid FromBinId` + `Guid ToBinId` ekle
4. SQL SELECT'e bu kolonları ekle

- [ ] Tamamlandı

---

#### TASK-S0-06 · Transfer/Putaway ItemId Eksikleri
```
Dosya  : src/Operax.Web/Features/Transfer/Putaway.cshtml.cs
Satırlar: 57, 64
Hata   : CS0103 — 'ItemId' not found
```

**Yapılacak:**
1. `Features/Transfer/Putaway.cshtml.cs` oku
2. İlgili metod imzasına `Guid itemId` parametresi ekle
3. Mevcut anonim obje `new { ItemId, ... }` → `new { ItemId = itemId, ... }` yap

- [ ] Tamamlandı

---

#### TASK-S0-07 · Production/Details ProductionLineDto.ItemId Eksik
```
Dosya  : src/Operax.Web/Features/Production/Details.cshtml.cs
Satır  : 96
Hata   : CS1061 — 'ProductionLineDto' does not contain 'ItemId'
```

**Yapılacak:**
1. `Features/Production/Details.cshtml.cs` oku
2. `ProductionLineDto` record'unu bul
3. `Guid ItemId { get; set; }` property ekle
4. İlgili SQL SELECT sorgusuna `l.ItemId` sütununu ekle

- [ ] Tamamlandı

---

### BLOK 2 — Güvenlik
> Build geçse de bu blok aynı sprint'te tamamlanır.

---

#### TASK-S0-08 · Newtonsoft.Json Güvenlik Güncellemesi
```
Paket  : Newtonsoft.Json 11.0.1
CVE    : GHSA-5crp-9r3c-p9vr (High severity)
```

**Yapılacak:**
1. `src/Operax.Web/Operax.Web.csproj` aç
2. `Newtonsoft.Json` satırını bul
3. `Version="11.0.1"` → `Version="13.0.3"` değiştir
4. `dotnet restore` çalıştır, paket indiriyor mu kontrol et

- [ ] Tamamlandı

---

#### TASK-S0-09 · Operax.Cli Hardcoded Credentials
```
Dosya  : src/Operax.Cli/Program.cs
Sorun  : Connection string içinde şifre açık metin olarak yazılı
```

**Yapılacak:**
1. `src/Operax.Cli/Program.cs` oku
2. Hardcoded connection string'i bul
3. `src/Operax.Cli/appsettings.json` oluştur:
   ```json
   {
     "ConnectionStrings": {
       "Default": "Server=.;Database=Operax;Trusted_Connection=True;TrustServerCertificate=True"
     }
   }
   ```
4. `Operax.Cli.csproj`'a `Microsoft.Extensions.Configuration.Json` paketi ekle (zaten var mı kontrol et)
5. `Program.cs`'de `IConfiguration` ile connection string oku
6. Git'e commit ederken `appsettings.json`'ı `.gitignore`'a ekle

- [ ] Tamamlandı

---

### BLOK 3 — Warning Temizliği (27 uyarı)
> Build geçer ama bu uyarılar aynı sprint'te sıfırlanır.

---

#### TASK-S0-10 · Null Warning Temizliği (CS8602 / CS8601 / CS8629)

**Etkilenen dosyalar ve satırlar:**

| Dosya | Satırlar | Uyarı Türü |
|---|---|---|
| `Receiving/Details.cshtml.cs` | 34, 91, 130, 146 | CS8601, CS8602 |
| `Shipping/Details.cshtml.cs` | 32, 68, 172, 211 | CS8601, CS8602 |
| `SalesOrders/Details.cshtml.cs` | 30, 85 | CS8601, CS8602 |
| `PurchaseOrders/Details.cshtml.cs` | 30, 85 | CS8601, CS8602 |
| `MasterData/Items/Details.cshtml.cs` | 27 | CS8601 |
| `Picking/Details.cshtml.cs` | 73, 74 | CS8602 |
| `Transfer/Details.cshtml.cs` | 79, 100 | CS8602 |
| `Production/Details.cshtml.cs` | 49, 77, 121, 121 | CS8602, CS8629 |
| `CycleCount/Details.cshtml.cs` | 100 | CS8602 |

**Genel çözüm yaklaşımı:**
```csharp
// CS8601: Null assignment — nullable değer non-nullable'a atanıyor
// Kötü:
Header = await conn.QueryFirstOrDefaultAsync<HeaderDto>(...);   // null olabilir
// İyi:
Header = await conn.QueryFirstOrDefaultAsync<HeaderDto>(...) ?? new();

// CS8602: Null dereference — null olabilecek objenin property'sine erişiliyor
// Kötü:
var rate = item.BaseUomId;   // item null olabilir
// İyi:
var rate = item?.BaseUomId ?? Guid.Empty;
// veya önce null check:
if (item is null) return Page();

// CS8629: Nullable value type — Nullable<T>.Value doğrudan kullanılıyor
// Kötü:
decimal cost = order.PlannedCost.Value;   // null olabilir
// İyi:
decimal cost = order.PlannedCost ?? 0m;
```

**Her dosya için yaklaşım:**
- Dosyayı oku
- Satır numarasına git
- Uyarıyı yukarıdaki pattern ile düzelt
- Bir sonraki dosyaya geç

- [ ] Receiving/Details.cshtml.cs — tamamlandı
- [ ] Shipping/Details.cshtml.cs — tamamlandı
- [ ] SalesOrders/Details.cshtml.cs — tamamlandı
- [ ] PurchaseOrders/Details.cshtml.cs — tamamlandı
- [ ] MasterData/Items/Details.cshtml.cs — tamamlandı
- [ ] Picking/Details.cshtml.cs — tamamlandı
- [ ] Transfer/Details.cshtml.cs — tamamlandı
- [ ] Production/Details.cshtml.cs — tamamlandı
- [ ] CycleCount/Details.cshtml.cs — tamamlandı

---

#### TASK-S0-11 · Unused Parameter Temizliği (CS9113)

| Dosya | Parametre | Çözüm |
|---|---|---|
| `Admin/Dictionary/Details.cshtml.cs:8` | `company` | Filtre için kullan ya da kaldır |
| `Admin/Users/Index.cshtml.cs:8` | `roleManager` | Kullanılıyorsa implement et, yoksa kaldır |
| `Receiving/AutoTraceabilityService.cs:8` | `company` | Lot/Serial üretiminde `CompanyId` filtresi ekle |

**Yapılacak:**
1. Her dosyayı oku
2. Parametrenin gerçekten gerekmediğini doğrula
3. Gerekmiyorsa → constructor'dan kaldır
4. Gerekiyorsa → kullanan kod ekle

- [ ] Dictionary/Details — tamamlandı
- [ ] Users/Index — tamamlandı
- [ ] AutoTraceabilityService — tamamlandı

---

### BLOK 4 — Doğrulama

#### TASK-S0-12 · Final Build Kontrolü
```
Komut: dotnet build src/Operax.Web/Operax.Web.csproj
Beklenen: 0 hata, 0 uyarı
```

- [ ] Build temiz geçiyor
- [ ] `dotnet run` ile uygulama ayağa kalkıyor (port 5000/5001)
- [ ] Login sayfası açılıyor
- [ ] Hata sayfası yok (unhandled exception yok)

---

## Sprint 0 Özeti

| Blok | Görev Sayısı | Durum |
|---|---|---|
| Kritik Build Hataları | 7 | — |
| Güvenlik | 2 | — |
| Warning Temizliği | 2 | — |
| Doğrulama | 1 | — |
| **Toplam** | **12** | — |

**Sprint 0 tamamlandığında Sprint 1'e geç.**
