# OPERAX — Test Stratejisi
> Güncelleme: Mart 2026
> Test projesi: `src/Operax.Tests/` — henüz oluşturulmadı (Sprint 0 sonrası)

---

## Test Felsefesi

```
1. Test olmayan özellik tamamlanmış sayılmaz
2. Her servis sınıfı için en az 3 unit test zorunlu
3. Her belge akışı (Receiving, Shipping vb.) için 1 integration test
4. Test isimleri Türkçe ve açıklayıcı olacak
5. Test yorumları Türkçe (RULES.md kod yorum standardı)
```

---

## Test Projesi Kurulumu

> Sprint 0 tamamlandıktan sonra yapılacak.

```bash
# Test projesi oluştur
dotnet new xunit -n Operax.Tests -o src/Operax.Tests

# Solution'a ekle
dotnet sln Operax.sln add src/Operax.Tests/Operax.Tests.csproj

# Gerekli paketler
dotnet add src/Operax.Tests package Moq
dotnet add src/Operax.Tests package FluentAssertions
dotnet add src/Operax.Tests package Microsoft.Data.SqlClient

# Web projesine referans
dotnet add src/Operax.Tests reference src/Operax.Web/Operax.Web.csproj
```

### Klasör Yapısı

```
src/Operax.Tests/
  Unit/
    Services/
      DynamicBomServiceTests.cs
      ProductionActivityServiceTests.cs
      AutoTraceabilityServiceTests.cs
    Lib/
      GuardTests.cs
    Helpers/
      UomConversionTests.cs
  Integration/
    Flows/
      ReceivingFlowTests.cs
      ShippingFlowTests.cs
      PickingFlowTests.cs
      TransferFlowTests.cs
      CycleCountFlowTests.cs
      ProductionFlowTests.cs
    Setup/
      TestDbFixture.cs    ← Test DB kurulum ve temizleme
      TestDataBuilder.cs  ← Test verisi oluşturucu (Builder pattern)
```

---

## Unit Test Kuralları

### Test Dosyası Şablonu

```csharp
namespace Operax.Tests.Unit.Services;

/// <summary>
/// DynamicBomService — dinamik BOM hesaplama servisinin testleri
/// </summary>
public class DynamicBomServiceTests
{
    // Test için sahte bağımlılıklar (mock)
    private readonly Mock<Db> _mockDb;
    private readonly DynamicBomService _service;

    public DynamicBomServiceTests()
    {
        // Her testten önce temiz mock oluştur
        _mockDb = new Mock<Db>();
        _service = new DynamicBomService(_mockDb.Object);
    }

    [Fact]
    public void FormulaHesapla_GecerliFormul_DogruSonucDoner()
    {
        // Hazırlık: test parametreleri
        var parametreler = new Dictionary<string, string> { ["BOY"] = "100", ["EN"] = "50" };
        var formul = "BOY * EN / 1000";

        // Çalıştır
        var sonuc = _service.EvaluateFormula(formul, parametreler);

        // Doğrula
        sonuc.Should().Be(5m);
    }

    [Fact]
    public async Task SifarisHatlariOlustur_GecerliUrun_HatlarEklenir()
    {
        // Test içeriği...
    }
}
```

### Test İsimlendirme

```
[MetodAdı]_[Senaryo]_[BeklenenSonuç]

Örnekler:
FormulaHesapla_GecerliFormul_DogruSonucDoner
FormulaHesapla_BosBilesken_SifirDoner
StokHareketiYaz_YetersizStok_HataFirlatir
LotNoUret_LotTakipliUrun_LotOnekiIleUretir
LotNoUret_LotTakipliDegilUrun_BosStringDoner
```

---

## Integration Test Kuralları

### Test DB Kurulumu

```csharp
/// <summary>
/// Integration testler için izole SQL Server test veritabanı yönetir.
/// Her test sınıfı bu fixture'ı kullanır.
/// </summary>
public class TestDbFixture : IDisposable
{
    // Test DB adı: her test çalışmasında benzersiz
    public string DbName { get; } = $"Operax_Test_{Guid.NewGuid():N}";
    public string ConnectionString { get; }

    public TestDbFixture()
    {
        // Test DB oluştur ve schema'yı uygula
        ConnectionString = $"Server=(localdb)\\mssqllocaldb;Database={DbName};...";
        SchemaUygula();
        SeedDataYukle();
    }

    private void SchemaUygula()
    {
        // docs/sql/ altındaki tüm schema dosyalarını sırayla çalıştır
    }

    private void SeedDataYukle()
    {
        // Temel seed data: şirket, sözlük değerleri, parametreler
    }

    public void Dispose()
    {
        // Test bitti, DB'yi temizle
        DropTestDb();
    }
}
```

### Akış Testi Şablonu

```csharp
/// <summary>
/// Mal kabul akışı — PO'dan stok girişine kadar uçtan uca test
/// </summary>
public class ReceivingFlowTests : IClassFixture<TestDbFixture>
{
    private readonly TestDbFixture _db;

    public ReceivingFlowTests(TestDbFixture db)
    {
        _db = db;
    }

    [Fact]
    public async Task MalKabul_TamAkis_StokArtar()
    {
        // Hazırlık: test verisi oluştur
        var sirketId = await _db.SirketOlustur();
        var urunId   = await _db.UrunOlustur(sirketId, lotTakipli: false);
        var depoId   = await _db.DepoOlustur(sirketId);

        // 1. Mal kabul belgesi oluştur
        var malKabulId = await MalKabulOlustur(sirketId, depoId);

        // 2. Satır ekle
        await MalKabulSatirEkle(malKabulId, urunId, miktar: 10);

        // 3. Onayla (POSTED)
        var sonuc = await MalKabulOnayla(malKabulId);

        // Doğrula: stok hareketi oluştu mu?
        var hareket = await StokHareketiGetir(malKabulId);
        hareket.Should().NotBeNull();
        hareket!.MovementType.Should().Be("RECEIPT");
        hareket.QtyBase.Should().Be(10);

        // Doğrula: stok bakiyesi arttı mı?
        var bakiye = await StokBakiyesiGetir(urunId, depoId);
        bakiye.Should().Be(10);
    }
}
```

---

## Modül Bazlı Test Planı

### Sprint 0 Sonrası — Temel Altyapı
- [ ] Test projesi oluştur
- [ ] TestDbFixture implementasyonu
- [ ] TestDataBuilder implementasyonu
- [ ] GuardTests — tüm Guard metodları test edilmeli

### Sprint 1 — M00 Core
- [ ] Şirket claim middleware testi
- [ ] Rol bazlı yetkilendirme testi

### Sprint 2 — M01 Master Data
- [ ] UOM dönüşüm hesaplama testleri
- [ ] Barkod unique kontrolü testleri

### Sprint 3 — M02 Inventory
- [ ] Stok bakiyesi hesaplama doğruluğu
- [ ] Negatif stok önleme testi

### Sprint 4 — M03/M04 Receiving + PO
- [ ] `ReceivingFlowTests` — tam akış testi
- [ ] Lot zorunluluğu business rule testi
- [ ] UOM dönüşümlü mal kabul testi

### Sprint 5 — M05 Shipping
- [ ] `ShippingFlowTests` — tam akış testi
- [ ] Kısmi sevkiyat testi
- [ ] Stok yetersizliği senaryosu

### Sprint 6 — M06/M07 Picking + Transfer
- [ ] FIFO allocation doğruluk testi
- [ ] FEFO allocation doğruluk testi
- [ ] Transfer sonrası net stok değişmemeli

### Sprint 7 — M08/M09 Sayım + Traceability
- [ ] Sayım fark hesaplama testi
- [ ] COUNT_ADJ hareketi testi
- [ ] Lot no üretim formatı testi
- [ ] Seri no unique kontrolü testi

### Sprint 8 — M10 Manufacturing
- [ ] `DynamicBomServiceTests` — formül hesaplama
- [ ] `ProductionActivityServiceTests` — maliyet hesaplama
- [ ] `ProductionFlowTests` — tam üretim akışı
- [ ] Rework senaryosu testi
- [ ] NCalc güvenli formül değerlendirmesi

---

## Test Çalıştırma

```bash
# Tüm testler
dotnet test src/Operax.Tests/Operax.Tests.csproj

# Sadece unit testler
dotnet test src/Operax.Tests/Operax.Tests.csproj --filter "Category=Unit"

# Sadece bir modülün testleri
dotnet test src/Operax.Tests/Operax.Tests.csproj --filter "FullyQualifiedName~ReceivingFlow"

# Verbose çıktı
dotnet test src/Operax.Tests/Operax.Tests.csproj -v normal

# Coverage raporu (coverlet gerekli)
dotnet test src/Operax.Tests/Operax.Tests.csproj --collect:"XPlat Code Coverage"
```

---

## Minimum Kapsam Hedefleri

| Katman | Hedef Kapsam |
|---|---|
| Service sınıfları | %80+ |
| Belge akışları (integration) | %70+ |
| Lib/ yardımcı sınıflar | %90+ |
| PageModel OnGet/OnPost | %50+ |
