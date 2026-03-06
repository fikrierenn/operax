# OPERAX Platform — Mimari (ARCH.md)

> Sürüm: v2.0 | Tarih: Mart 2026  
> Kural: API only if needed — mümkünse aynı uygulama içinde server-side çöz.

---

## 1. Genel Yapı

```
┌──────────────────────────────────────────┐
│   TARAYICI (Masaüstü + El Terminali)     │
│   Razor Pages · TailwindCSS · Vanilla JS │
└──────────────────┬───────────────────────┘
                   │ HTTP/S
┌──────────────────▼───────────────────────┐
│   ASP.NET Core 10 — Razor Pages          │
│   Feature-based · Transaction Script     │
│   Dapper → SQL Server 2022               │
└──────┬───────────────────────────────────┘
       │
┌──────▼──────┐     ┌─────────────────────┐
│ SQL Server  │     │   Print Server      │
│   2022      │     │   (Zebra ZPL)       │
└─────────────┘     └─────────────────────┘
```

**Tek uygulama, iki view seti:**
- `Features/*/Index.cshtml` → masaüstü
- `Features/*/Terminal.cshtml` → el terminali (mobil-first, büyük dokunmatik alan)

---

## 2. Teknoloji Kararları (ADR)

| Konu | Karar | Gerekçe |
|---|---|---|
| Backend | .NET 10 ASP.NET Core | Default, LTS |
| UI | Razor Pages (feature-based) | API yok, server-side, sade |
| Pattern | Transaction Script | Katman şovu yok, hızlı |
| Data access | **Dapper** | Raw SQL kontrolü, performans |
| Stil | TailwindCSS | Utility-first, hızlı |
| JS | Vanilla JS | Minimum bağımlılık |
| DB | SQL Server 2022 | Kurumsal, ACID |
| Print | Ayrı Print Server | Zebra ZPL, harici client |
| El terminali | Aynı uygulama, ayrı view | Browser var, ayrı app gereksiz |
| Background jobs | Hangfire (SQL storage) | .NET native, UI dahili |
| Realtime | SignalR | Bildirim, kuyruk izleme |
| Cache | Redis | Distributed, multi-instance |
| Auth | ASP.NET Core Identity + Cookie | Server-side session, terminal uyumlu |

**API ne zaman açılır (sadece):**
- Print Server iletişimi (internal HTTP)
- Gelecekte mobil/native uygulama çıkarsa
- Harici ERP entegrasyonu (M16 webhook)

---

## 3. Solution Yapısı

```
Operax/
├── src/
│   ├── Operax.Web/                    # Ana uygulama
│   │   ├── Program.cs
│   │   ├── appsettings.json
│   │   ├── Features/
│   │   │   ├── Auth/
│   │   │   │   ├── Login.cshtml
│   │   │   │   └── Login.cshtml.cs
│   │   │   ├── Receiving/
│   │   │   │   ├── Receiving.sql.cs   # Dapper sorguları
│   │   │   │   ├── Index.cshtml       # Masaüstü liste
│   │   │   │   ├── Index.cshtml.cs
│   │   │   │   ├── Create.cshtml
│   │   │   │   ├── Create.cshtml.cs
│   │   │   │   └── Terminal.cshtml    # El terminali görünümü
│   │   │   ├── Sales/
│   │   │   ├── Shipping/
│   │   │   ├── Picking/
│   │   │   │   ├── Task.cshtml        # Terminal: toplama akışı
│   │   │   │   └── Task.cshtml.cs
│   │   │   ├── Transfer/
│   │   │   ├── CycleCount/
│   │   │   ├── Inventory/
│   │   │   ├── MasterData/
│   │   │   ├── Manufacturing/
│   │   │   ├── Service/
│   │   │   ├── Project/
│   │   │   ├── Incentives/
│   │   │   ├── Dashboard/
│   │   │   └── Admin/
│   │   │       ├── Dictionary/
│   │   │       ├── Parameters/
│   │   │       ├── Modules/
│   │   │       └── Users/
│   │   └── Lib/
│   │       ├── Db.cs                  # Dapper connection factory
│   │       ├── Guard.cs               # Guard clause helper
│   │       ├── Auth.cs                # CurrentUser, CurrentCompany
│   │       ├── Errors.cs              # Tek hata formatı
│   │       └── HangfireJobs/          # Background job tanımları
│   │
│   └── Operax.PrintServer/            # Ayrı servis
│       ├── Program.cs                 # Minimal API (sadece internal)
│       ├── ZebraService.cs            # ZPL oluşturma + gönderme
│       └── Templates/                 # Etiket şablonları (.zpl)
│
├── tests/
│   ├── Operax.UnitTests/
│   └── Operax.IntegrationTests/
│
├── docs/
│   ├── PRD.md
│   ├── ARCHITECTURE.md                # Bu dosya
│   ├── ALGO.md
│   ├── PLAN.md
│   ├── TODO.md
│   └── OPERAX_Analiz_ve_Plan.md
│
├── AGENT.md
└── docker-compose.yml
```

---

## 4. Sayfa Akışı (Transaction Script)

```csharp
// Features/Receiving/Create.cshtml.cs
public class CreateModel : PageModel
{
    readonly IDbConnection _db;
    readonly ICurrentCompany _company;

    [BindProperty] public CreateReceivingInput Input { get; set; }

    public async Task OnGetAsync()
    {
        // Dapper: tedarikçi dropdown
        Suppliers = await _db.QueryAsync<Supplier>(
            ReceivingSql.GetSuppliers, new { CompanyId = _company.Id });
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid) return Page();

        var id = await _db.ExecuteScalarAsync<Guid>(
            ReceivingSql.Insert, new { _company.Id, Input.SupplierId, ... });

        return RedirectToPage("Details", new { id });
    }

    public async Task<IActionResult> OnPostPostReceivingAsync(Guid id)
    {
        // DRAFT → POSTED + StockMovement yaz
        using var tx = _db.BeginTransaction();
        await _db.ExecuteAsync(ReceivingSql.Post, new { id }, tx);
        await _db.ExecuteAsync(StockMovementSql.InsertReceipt, new { id }, tx);
        tx.Commit();

        return RedirectToPage("Details", new { id });
    }
}
```

```csharp
// Features/Receiving/Receiving.sql.cs — tüm SQL bu dosyada
public static class ReceivingSql
{
    public const string GetSuppliers = @"
        SELECT Id, Name FROM Account
        WHERE CompanyId = @CompanyId AND AccountType IN ('SUPPLIER','BOTH')
        AND IsDeleted = 0 ORDER BY Name";

    public const string Insert = @"
        INSERT INTO Receiving (Id, CompanyId, SupplierId, Status, CreatedAt, CreatedBy)
        OUTPUT INSERTED.Id
        VALUES (NEWID(), @CompanyId, @SupplierId, 'DRAFT', GETUTCDATE(), @UserId)";

    public const string Post = @"
        UPDATE Receiving SET Status = 'POSTED', UpdatedAt = GETUTCDATE()
        WHERE Id = @Id AND Status = 'DRAFT'";
}
```

---

## 5. El Terminali Görünümü

Aynı PageModel, farklı view. Terminal URL'i: `/receiving/terminal`

```csharp
// Terminal.cshtml — büyük dokunmatik, minimum metin
public async Task<IActionResult> OnGetTerminalAsync()
{
    // Aynı veri, sadece view farklı
    return Page("Terminal");
}
```

```html
<!-- Terminal.cshtml — mobil-first, büyük butonlar -->
<div class="flex flex-col gap-4 p-4">
    <!-- Barkod input — USB scanner buraya yazar -->
    <input id="barcodeInput" autofocus autocomplete="off"
           class="w-full text-2xl p-4 border-2 rounded-xl"
           placeholder="Barkod okutun..." />

    <!-- Ürün bilgisi -->
    <div id="itemInfo" class="hidden bg-green-50 p-4 rounded-xl text-xl"></div>

    <!-- Miktar -->
    <input type="number" id="qty"
           class="w-full text-3xl text-center p-4 border-2 rounded-xl"
           placeholder="Adet" />

    <!-- Onayla -->
    <button class="w-full bg-green-600 text-white text-2xl py-6 rounded-xl font-bold">
        ✓ Onayla
    </button>
</div>

<script>
// USB scanner klavye gibi davranır, Enter ile tamamlar
document.getElementById('barcodeInput').addEventListener('keypress', async (e) => {
    if (e.key !== 'Enter') return;
    const barcode = e.target.value.trim();
    const res = await fetch(`/api/barcode/lookup?code=${barcode}`);
    const item = await res.json();
    document.getElementById('itemInfo').textContent = `${item.sku} — ${item.name}`;
    document.getElementById('itemInfo').classList.remove('hidden');
    document.getElementById('qty').focus();
});
</script>
```

---

## 6. Print Server (Zebra ZPL)

Ayrı deployment, internal Minimal API. Ana uygulama `HttpClient` ile çağırır.

```csharp
// Operax.PrintServer/Program.cs
app.MapPost("/print/item-label", async (PrintRequest req, ZebraService zebra) =>
{
    var zpl = zebra.BuildItemLabel(req);
    await zebra.SendToNetworkPrinter(req.PrinterIp, zpl);
    return Results.Ok();
});

app.MapPost("/print/lpn-label", ...);
app.MapPost("/print/carton-label", ...);
```

```csharp
// ZebraService.cs — ZPL oluşturma
public string BuildItemLabel(PrintRequest req) => $@"
^XA
^FO50,50^BY3^BCN,100,Y,N,N^FD{req.Barcode}^FS
^FO50,180^A0N,30,30^FD{req.Sku}^FS
^FO50,220^A0N,25,25^FD{req.Name}^FS
^XZ";

// Network printer'a raw TCP gönder (port 9100)
public async Task SendToNetworkPrinter(string ip, string zpl)
{
    using var client = new TcpClient(ip, 9100);
    using var stream = client.GetStream();
    var data = Encoding.UTF8.GetBytes(zpl);
    await stream.WriteAsync(data);
}
```

**Baskı tetikleyicileri:**

| Tetikleyici | Etiket Tipi | Otomatik mı? |
|---|---|---|
| Receiving POSTED | Item barkod | Parametre ile |
| LPN oluştur | LPN + içerik | Evet |
| Shipment POSTED | Koli (Carton) | Evet |
| Cycle Count başlat | Bin/lokasyon QR | Manuel |

---

## 7. Lib Katmanı

```csharp
// Lib/Db.cs — Dapper connection factory
public class Db(IConfiguration config)
{
    public IDbConnection Open() =>
        new SqlConnection(config.GetConnectionString("Default"));
}

// Lib/Auth.cs — HttpContext'ten çek
public class CurrentUser(IHttpContextAccessor ctx)
{
    public Guid Id => Guid.Parse(ctx.HttpContext!.User.FindFirstValue(ClaimTypes.NameIdentifier)!);
    public Guid CompanyId => Guid.Parse(ctx.HttpContext!.User.FindFirstValue("company")!);
    public string[] Roles => ctx.HttpContext!.User.FindAll(ClaimTypes.Role).Select(x => x.Value).ToArray();
}

// Lib/Guard.cs — guard clause helper
public static class Guard
{
    public static T NotNull<T>(T? value, string field) =>
        value ?? throw new ValidationException($"{field} boş olamaz.");

    public static void Against(bool condition, string message)
    {
        if (condition) throw new BusinessException(message);
    }
}

// Lib/Errors.cs — tek hata formatı
public record ErrorResponse(string Code, string Message, string? Field = null);
```

---

## 8. Veritabanı Standartları

- **Schema:** İngilizce PascalCase (master dökümndaki tanımlar sabit: `SalesOrder`, `StockMovement` vb.)
- **Soft delete:** `IsDeleted BIT + DeletedAt + DeletedBy` — tüm tablolarda
- **Timestamp:** `CreatedAt, CreatedBy, UpdatedAt, UpdatedBy` — tüm tablolarda
- **PK:** `UNIQUEIDENTIFIER` (NEWID())
- **CompanyId:** Her tabloda zorunlu — row-level isolation

**Kritik index'ler:**
```sql
-- StockMovement — en sık sorgulanan
CREATE INDEX IX_StockMovement_Company_Item_Date
    ON StockMovement(CompanyId, ItemId, MovementDate DESC)
    INCLUDE (QtyBase, MovementType, LocationId);

-- InventoryBalance — unique bakiye
CREATE UNIQUE INDEX IX_InventoryBalance_Key
    ON InventoryBalance(CompanyId, ItemId, WarehouseId, LocationId, LotId);

-- EventQueue — job processing
CREATE INDEX IX_EventQueue_Pending
    ON EventQueue(Status, CreatedAt)
    WHERE Status IN ('PENDING', 'FAILED');
```

---

## 9. Docker Compose (Yerel Geliştirme)

```yaml
services:
  web:
    build: ./src/Operax.Web
    ports: ["5000:8080"]
    environment:
      ConnectionStrings__Default: "Server=db;Database=Operax;..."
      Redis__Connection: "redis:6379"

  printserver:
    build: ./src/Operax.PrintServer
    ports: ["5001:8080"]
    # Zebra yazıcı IP'lerine network erişimi olmalı

  db:
    image: mcr.microsoft.com/mssql/server:2022-latest
    environment:
      SA_PASSWORD: "Dev!Password1"
      ACCEPT_EULA: "Y"
    volumes:
      - sqldata:/var/opt/mssql

  redis:
    image: redis:7-alpine

volumes:
  sqldata:
```

---

## 10. Özet: Kesinleşen Kararlar

| Konu | Karar |
|---|---|
| **Backend + UI** | .NET 10 Razor Pages — tek uygulama |
| **Veri erişimi** | Dapper + raw SQL (EF Core değil) |
| **Mimari** | Feature-based Transaction Script |
| **El terminali** | Aynı uygulama, `Terminal.cshtml` view'ı |
| **Barkod okuma** | USB scanner → klavye event → input alanı |
| **Zebra yazıcı** | Ayrı Print Server servisi, TCP port 9100 |
| **Background jobs** | Hangfire (SQL Server storage) |
| **Auth** | ASP.NET Core Identity + Cookie (terminal uyumlu) |
| **Stil** | TailwindCSS |
| **JS** | Vanilla JS |
| **DB izolasyon** | Tek şema + CompanyId her tabloda |
| **API** | Sadece Print Server (internal) + ileride ERP webhook |
