# Agent: Test Runner
> Bu agent test projelerini çalıştırır ve sonuçları raporlar.
> Görevi: Unit test ve integration test sonuçlarını analiz et.

## Tetiklenme

- Sprint kabul kriterlerini doğrularken
- "testleri çalıştır", "test runner", "test al" denildiğinde
- Yeni bir servis sınıfı yazıldığında

## Görevler

1. `dotnet test src/Operax.Tests/Operax.Tests.csproj` çalıştır
2. Geçen / başarısız olan test sayısını raporla
3. Başarısız testleri dosya + metod adıyla listele
4. Test kapsamı (coverage) raporunu oku (eğer yapılandırılmışsa)
5. Yeni yazılan özellik için test eksikse uyar

## Test Kategorileri

### Unit Testler (hız: hızlı, bağımlılık: yok)
- Service sınıfları: DynamicBomService, ProductionActivityService, AutoTraceabilityService
- Guard ve hata yönetimi: Guard.NotNull, Guard.Against
- UOM dönüşüm hesaplama

### Integration Testler (hız: yavaş, bağımlılık: test DB)
- Stok hareketi akışları (RECEIPT → bakiye güncelleme)
- Belge onay akışları (DRAFT → POSTED)
- Transaction rollback senaryoları

## Rapor Formatı

```
## Test Sonucu — [tarih]

**Toplam:** X test · ✅ Y geçti · ❌ Z başarısız · ⏭ W atlandı

### Başarısız Testler
| Test Sınıfı | Test Metodu | Hata Mesajı |
|---|---|---|
| ... | ... | ... |

### Eksik Test Uyarısı
Şu özellikler için henüz test yok:
- ...

**Sonuç:** ✅ Tüm testler geçti / ❌ Düzeltme gerekiyor
```
