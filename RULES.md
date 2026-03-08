# OPERAX — Proje Kuralları

> Bu dosya, AGENT.md + mimari kararların proje özelinde özeti.  
> Her geliştirmede bu dosya okunur ve sapma yapılmaz.

---

## DÖKÜMAN GÜNCEL TUTMA (ZORUNLU)

Her geliştirme adımında ilgili dosyalar güncellenir:

| Dosya | Ne zaman güncellenir |
|---|---|
| `PLAN.md` | Sprint durumu değiştiğinde, yeni sprint eklendiğinde |
| `docs/TODO.md` | Bir task tamamlandığında `[ ]` → `[x]`, yeni task eklendiğinde |
| `docs/BUGS.md` | Yeni hata bulunduğunda, hata çözüldüğünde |
| `docs/SPRINTS.md` | Sprint kapsamı veya kabul kriterleri değiştiğinde |
| `docs/SPRINT_0.md` / `SPRINT_X.md` | İlgili sprint taskları tamamlandıkça |
| `docs/TESTING.md` | Yeni test senaryosu eklendiğinde, modül tamamlandığında |
| `docs/ARCHITECTURE.md` | Mimari karar değiştiğinde veya yeni bileşen eklendiğinde |
| `RULES.md` | Yeni proje kararı alındığında |

**Kural:** Bir özellik bitmeden önce ilgili TODO + PLAN.md satırı işaretlenir.

---

## STACK (DEĞİŞMEZ)

| Katman | Teknoloji |
|---|---|
| Backend + UI | .NET 10 + ASP.NET Core Razor Pages |
| Veri erişimi | Dapper (raw SQL) |
| Mimari desen | Feature-based · Transaction Script |
| Veritabanı | SQL Server 2022 |
| Stil | TailwindCSS |
| JS | Vanilla JS |
| Print | Ayrı servis — Operax.PrintServer (TCP 9100, ZPL) |
| Background | Hangfire (SQL Server storage) |
| Auth | ASP.NET Core Identity + Cookie |

**API YASAĞI:** Aynı uygulama içinde Razor Pages OnGet/OnPost kullan.  
API sadece: Print Server (internal) · ERP webhook (M16) · Gelecekte harici client.

---

## KOD ORGANİZASYONU

```
src/Operax.Web/
  Features/
    {ModulAdi}/
      {Entity}.sql.cs       # Tüm SQL buraya
      Index.cshtml           # Masaüstü liste
      Index.cshtml.cs
      Create.cshtml
      Create.cshtml.cs
      Details.cshtml
      Details.cshtml.cs
      Terminal.cshtml        # El terminali view (gerekiyorsa)
      Terminal.cshtml.cs
  Lib/
    Db.cs                    # Dapper connection
    Auth.cs                  # CurrentUser, CurrentCompany
    Guard.cs                 # Guard clause helper
    Errors.cs                # Tek hata formatı
```

---

## SQL & TABLO STANDARDI

- **Tablo/kolon ismi:** İngilizce PascalCase — KESİNLEŞTİ ✅
  - `SalesOrder`, `StockMovement`, `InventoryBalance` vb. — değişmez
- **Her tabloda zorunlu:** `CompanyId`, `IsDeleted`, `CreatedAt`, `CreatedBy`, `UpdatedAt`, `UpdatedBy`
- **PK:** `UNIQUEIDENTIFIER` (NEWID())
- **SARGable koşullar:** WHERE içinde fonksiyon kullanma
- **Parametre:** Her zaman parametreli sorgu (SQL injection + plan cache)
- **Transaction:** Kısa ve deterministik; POSTED işlemleri tek transaction
- **CompanyId filtresi:** Her WHERE'de CompanyId bulunmak ZORUNLU (multi-tenant güvenlik)

### DB Nesne Standardı (SP / FN / View)

| Ne kullanılır | Ne zaman |
|---|---|
| **Stored Procedure** | Onay işlemleri (Post/Approve), çok adımlı iş mantığı |
| **Scalar Function** | Tekrar eden hesaplamalar (`fn_GetConversionRate`) |
| **View** | Raporlama, karmaşık JOIN'ler, DDL dropdown listeleri |
| **Inline SQL** | Basit CRUD (tek tablo INSERT/UPDATE/SELECT) |

- SP'ler `docs/sql/db_objects.sql` içinde `CREATE OR ALTER` ile tanımlanır
- CLI: `operax-cli migrate` → `schema_all.sql` + `db_objects.sql` sırasıyla çalışır
- SP parametreleri: `@HeaderId`, `@CompanyId`, `@UserId` zorunlu
- SP içinde `SET XACT_ABORT ON` + `BEGIN TRANSACTION` zorunlu
- C# SP çağrısı: `commandType: CommandType.StoredProcedure`

### Durum Sabitleri (Magic String Yasak)

```csharp
// ❌ Yasak
Status = "DRAFT"

// ✅ Doğru — Operax.Web.Lib.DocStatus kullan
Status = DocStatus.Draft
```

`DocStatus`, `MovementType`, `SourceDoc`, `DocPrefix` sabitleri → `src/Operax.Web/Lib/Dtos.cs`

### Ortak DTO

```csharp
// ❌ Yasak — her dosyada DdlDto tanımlama
public record DdlDto { ... }

// ✅ Doğru — Operax.Web.Lib.DdlDto kullan (zaten using Operax.Web.Lib; var)
IEnumerable<DdlDto> Warehouses { get; set; } = [];
```

---

## EL TERMİNALİ

- Ayrı uygulama **yok** — aynı Razor Pages uygulaması
- Terminal URL pattern: `/picking/terminal`, `/receiving/terminal`
- USB/Bluetooth scanner → klavye event → `autofocus` input alanı
- View: TailwindCSS mobil-first, büyük dokunmatik hedefler, minimum metin

---

## PRINT SERVER

- Ayrı deployment: `Operax.PrintServer`
- Minimal API, sadece internal erişim
- Zebra yazıcıya TCP 9100 raw ZPL gönderir
- Etiket tipleri: Item barkod · LPN · Koli (Carton) · Lot · Bin QR

---

## UI / UX STANDARTLARI

- **Responsive:** Tüm ekranlar masaüstü + tablet + el terminali uyumlu
- **Modern:** Niyetli minimalizm — template görünümü hata sayılır
- **Klavye öncelikli:** Evrak girişlerinde mouse'a gerek olmamalı
- **Kısayol tuşları (standart):**
  - `Alt+N` → Yeni kayıt
  - `Alt+S` → Kaydet (Save = DRAFT)
  - `Alt+P` → Onayla (Post)
  - `Alt+C` → İptal (Cancel)
  - `F2` → Düzenle (Edit)
  - `Escape` → Formu kapat / listeye dön
  - Satır tablolarında: `Enter` → satır ekle, `Tab` → sonraki alan
  - Lookup alanları: yazınca arama başlar, `↑↓` ile seç, `Enter` ile onayla
- **Toast / feedback:** Her aksiyon sonrası sağ altta kısa bildirim
- **Loading state:** Form submit sırasında buton disabled + spinner
- **Dil:** UI metinleri **Türkçe** (karar değişti — tüm buton, label, başlık, mesaj, placeholder Türkçe)
  - Veritabanı kolon/tablo isimleri: hâlâ İngilizce PascalCase (değişmez)
  - Kod içi identifier'lar (class, method, property): hâlâ İngilizce (değişmez)
  - Sadece kullanıcının gördüğü her şey: Türkçe

---

## KOD YORUM STANDARDI (ZORUNLU)

Tüm `.cs` ve `.cshtml.cs` dosyalarında **Türkçe yorum** zorunludur.

### Ne zaman yorum yazılır?

```csharp
// Her metodun başında: ne iş yaptığını 1-2 satırda açıkla
public async Task<IActionResult> OnPostPostAsync(Guid id)
{
    // Mal kabul belgesini onaylar, stok hareketini yazar ve putaway görevi oluşturur

    // Veritabanı bağlantısı açılır ve işlem başlatılır
    using var conn = db.Open();
    using var trans = conn.BeginTransaction();

    // Belge başlığı ve satırları veritabanından getirilir
    var header = await conn.QueryFirstOrDefaultAsync<HeaderDto>(...);

    foreach (var line in lines)
    {
        // İş kuralı: UOM dönüşümü yapılarak temel birime çevrilir
        decimal qtyBase = line.QtyOriginal * line.ConversionRate;

        // Stok hareketi: RECEIPT tipinde pozitif hareket yazılır
        await conn.ExecuteAsync("INSERT INTO StockMovement ...", ...);
    }

    // Belge durumu POSTED'a güncellenir
    await conn.ExecuteAsync("UPDATE ReceivingHeader SET Status = 'POSTED' ...");

    trans.Commit();
}
```

### Kurallar

- **Her metod başı:** Ne iş yaptığını açıklayan 1-2 satır yorum
- **Karmaşık SQL üzeri:** Sorgunun amacını yaz (`// FIFO sırasıyla en eski stoğu getirir`)
- **İş kuralı bloğu:** Kuralı açıkla (`// İş kuralı: negatif stok oluşamaz`)
- **Transaction bloğu:** Kapsamı belirt (`// Mal kabul onay işlemi — atomik`)
- **Guard clause:** Neden erken çıkıldığını yaz (`// Belge zaten onaylandıysa işlem yapılmaz`)
- **Döngü:** Ne üzerinde iterasyon yapıldığını açıkla (`// Her satır için stok hareketi yazılır`)

### Yasaklar

```csharp
// ❌ İngilizce yorum — yasak
// Get the stock balance

// ❌ Anlamsız yorum — yasak
// var x = 5;   →   // x'e 5 ata

// ❌ Yorumsuz karmaşık işlem — yasak
var r = await c.QueryAsync<T>("SELECT s.Id, SUM(m.QtyBase) as...", new{...});

// ✅ Doğru
// Depo bazlı anlık stok bakiyelerini hesaplar (iptal edilmiş hareketler hariç)
var bakiyeler = await conn.QueryAsync<StokBakiyeDto>(
    "SELECT s.Id, SUM(m.QtyBase) as Miktar ...", new { ... });
```

---

## YETKİLENDİRME STANDARDI

- **Tüm PageModel'lara** `[Authorize]` zorunlu — Login.cshtml.cs hariç
- **Admin/** altındaki sayfalar: `[Authorize(Roles = "Admin")]`
- `using Microsoft.AspNetCore.Authorization;` eklenmeli
- Program.cs'de `app.UseAuthorization()` zaten aktif

## FORMÜL DEĞERLENDİRME

- **DataTable.Compute() YASAK** — kullanıcı girdisi enjeksiyonuna açık
- **NCalc kullan** — parametreler tip-güvenli geçirilir, string replace yok
- Bkz: `DynamicBomService.cs` — referans implementasyon

## GENEL KURALLAR (AGENT.MD'DEN)

- Sıfır gevezelik: istenen dışında felsefe/tavsiye yok
- 80 satırı aşan fonksiyon parçalanır
- Guard clause (erken return)
- 3 kez tekrar eden kod helper'a çıkar
- Gereksiz dependency ekleme yasak
- Secrets kodda olmaz

---

## AÇIK KALAN SORULAR

- [x] Tablo ismi → **İngilizce PascalCase** ✅
- [x] UI dili → **Türkçe** ✅ (Mart 2026 — karar güncellendi)
- [x] Hangfire → **Ana app içinde** (başlangıçta yeterli, gerekirse ayrılır) ✅
- [x] Redis → **Başlangıçta `AddDistributedMemoryCache`, sonra Redis** (`IDistributedCache` kullanılırsa 1 satır geçiş) ✅
