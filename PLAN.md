# OPERAX — Master Sprint Planı

> Sürüm: v1.0 | Oluşturma: Mart 2026
> Güncelleme: Her sprint tamamlandığında `[x]` işaretle, tarih yaz.
> Bağlı dosyalar: `docs/BUGS.md` · `docs/SPRINTS.md` · `docs/TODO.md` · `RULES.md`

---

## Çalışma Kuralları

```
1. Her sprint başında: ilgili dosyaları birlikte okuyoruz
2. Kod yazılmadan önce etkilenen dosyalar READ ile okunur
3. Her task biter bitmez TODO.md + bu dosya güncellenir
4. Her sprint sonu: dotnet build — 0 hata, 0 uyarı zorunlu
5. Scope creep yasak — sprint dışı talep bir sonraki sprint'e yazılır
6. Büyük değişiklik = önce sor, sonra yaz
7. Her ekranda çalışırken UI Türkçe kuralı aşağıdan kontrol edilir
```

---

## Cross-Cutting Kural — UI Dili: TAMAMEN TÜRKÇE
> Mart 2026 kararı — RULES.md'de de güncellendi.
> Her sprint'te üzerinde çalışılan her ekrana uygulanır.

**Türkçe OLACAKLAR** (kullanıcının gördüğü her şey):
```
[ ] Sayfa başlıkları (h1, h2, <title>)
[ ] Butonlar      → Kaydet · İptal · Onayla · Yeni · Düzenle · Sil · Ekle · Geri
[ ] Form label    → Ürün Kodu · Depo · Tarih · Durum · Miktar · Birim ...
[ ] Tablo başlıkları (th)
[ ] Placeholder   → "Ürün adı veya kodu giriniz..."
[ ] Toast/bildirim → "Kaydedildi." · "İşlem başarısız."
[ ] Hata mesajları → "Bu alan zorunludur." · "Yeterli stok yok."
[ ] Modal başlık ve açıklamaları
[ ] Boş durum    → "Kayıt bulunamadı." · "Henüz satır eklenmedi."
[ ] Menü ve breadcrumb
[ ] Tooltip ve yardım metinleri
```

**Türkçe OLMAYACAKLAR** (değişmez):
```
[−] DB tablo/kolon → İngilizce PascalCase (StockMovement, CompanyId ...)
[−] C# identifier → İngilizce (class, method, property, variable)
[−] URL'ler       → /receiving · /shipping · /picking
[−] Dictionary.Code → DRAFT · POSTED · RECEIPT · ISSUE · COUNT_ADJ
[−] CSS class, JS değişken/fonksiyon adları
```

---

## Sprint Durumu

| Sprint | Başlık | Durum | Tamamlanma |
|---|---|---|---|
| **S0** | Foundation Fix — Build Düzelt | `DONE` | Mart 2026 |
| **S1** | M00 Platform Core Stabilize | `IN PROGRESS` | — |
| **S2** | M01 Master Data Tamamla | `PLANNED` | — |
| **S3** | M02 Inventory Ledger | `PLANNED` | — |
| **S4** | M03 Receiving + M04 Purchase Orders | `PLANNED` | — |
| **S5** | M04 Sales Orders + M05 Shipping | `PLANNED` | — |
| **S6** | M06 Picking + M07 Transfer | `PLANNED` | — |
| **S7** | M08 Cycle Count + M09 Traceability | `PLANNED` | — |
| **S8** | M10 Manufacturing | `PLANNED` | — |
| **S9** | Print Server | `PLANNED` | — |
| **S10** | M15 Dashboard + Raporlar | `PLANNED` | — |

---

## SPRINT 0 — Foundation Fix
> Hedef: Build geçiyor · Uygulama ayağa kalkıyor · 0 hata · 0 uyarı
> Detay: `docs/BUGS.md` ve `docs/SPRINT_0.md`

### Hatalar (19 hata — Build FAIL)

- [ ] `Program.cs:16` — `AddDefaultIdentity` CS1061 — NuGet ref eksik
- [ ] `Shipping/Details.cshtml:4,10,11,17,23,76` — `IsNew` CS1061
- [ ] `Shipping/Details.cshtml.cs:140` — `IsNew` CS0103
- [ ] `CycleCount/Details.cshtml.cs:73` — `BinId, ItemId, QtySystem, QtyCounted` CS0103
- [ ] `CycleCount/Details.cshtml.cs:100` — `QtyDifference` null ref
- [ ] `MasterData/Items/Details.cshtml.cs:83,90` — `UomId` CS0103
- [ ] `Transfer/Details.cshtml.cs:79` — `FromBinId, ToBinId` CS0103
- [ ] `Transfer/Putaway.cshtml.cs:57,64` — `ItemId` CS0103
- [ ] `Production/Details.cshtml.cs:96` — `ProductionLineDto.ItemId` CS1061

### Güvenlik

- [ ] `Newtonsoft.Json 11.0.1` → `13.0.3` upgrade (GHSA-5crp-9r3c-p9vr)
- [ ] `Operax.Cli/Program.cs` hardcoded credentials → appsettings.json

### Uyarılar (27 uyarı)

- [ ] 15x CS8602 null dereference — Receiving, Shipping, Transfer, Production, Picking
- [ ] 5x CS8601 null assignment — Items, Shipping, SalesOrders, PurchaseOrders, Receiving
- [ ] 3x CS9113 unused parameter — Dictionary/Details, Users/Index, AutoTraceabilityService

**Kabul Kriteri:** `dotnet build src/Operax.Web/Operax.Web.csproj` → 0 hata, 0 uyarı

---

## SPRINT 1 — M00 Platform Core Stabilize
> Önkoşul: Sprint 0 tamamlandı
> Hedef: Login, admin ekranları, role-based auth sorunsuz çalışıyor

- [ ] Login akışı test — company claim düzgün set ediliyor mu?
- [ ] `CurrentCompany.Id` middleware'i doğrula — Guid.Empty gelmiyor mu?
- [ ] Rol bazlı sayfa yetkilendirmesi — `[Authorize(Roles="...")]` tutarlı mı?
- [ ] `/admin/audit-log` ekranı — schema var, UI yok
- [ ] StatusTransition engine — Posted işlemlerinde çalışıyor mu?
- [ ] Seed data kontrolü — `seed_core.sql` çalıştırıldı mı, eksik var mı?
- [ ] Hangfire dashboard — `/admin/jobs` erişilebiliyor mu?
- [ ] **UI Türkçe taraması** — Mevcut tüm admin ekranlarını tara, İngilizce UI metni bırakma
  - Login sayfası, menü, layout, admin alt ekranlar (Users, Roles, Dictionary, Parameters, Modules, StatusTransitions)

**Kabul Kriteri:** Admin kullanıcısı login → tüm admin ekranlarını görebiliyor → logout · Hiçbir ekranda İngilizce UI metni yok

---

## SPRINT 2 — M01 Master Data Tamamla
> Önkoşul: Sprint 1 tamamlandı
> Hedef: Ürün, partner, depo, bin verileri girilebilir ve doğrulanır

- [ ] Items/Details — UomId hatası giderildi, tam fonksiyonel test
- [ ] Items — UOM dönüşüm alt listesi (ItemUOM) çalışıyor mu?
- [ ] Items — Barkod alt listesi (ItemBarcode) çalışıyor mu?
- [ ] Items — `IsLotTracked`, `IsSerialTracked` toggle var mı?
- [ ] Partners — Müşteri/Tedarikçi formu tam mı?
- [ ] Warehouses — Depo formu + liste
- [ ] Bins — Lokasyon ekleme/düzenleme (schema_M01_Bins.sql)
- [ ] UOM — Birim listesi ve dönüşüm tanımları (schema_M01_UOM.sql)

**Kabul Kriteri:** Test ürünü oluştur → UOM + barkod ekle → partner ekle → depo + bin ekle

---

## SPRINT 3 — M02 Inventory Ledger
> Önkoşul: Sprint 2 tamamlandı
> Hedef: Stok bakiyesi görülebilir, hareket geçmişi takip edilebilir

- [ ] `/inventory` — anlık stok bakiyesi ekranı
- [ ] `/inventory/movements` — StockMovement geçmişi (hareket defteri)
- [ ] Bin bazlı bakiye — `/inventory/bin-balance`
- [ ] Filtreler: Item, Warehouse, Bin, Lot, Serial, Tarih aralığı
- [ ] Negatif stok uyarısı — post işlemlerinde kontrol ve uyarı

**Kabul Kriteri:** Herhangi bir ürünün stok bakiyesini ve hareket geçmişini görebiliyoruz

---

## SPRINT 4 — M03 Receiving + M04 Purchase Orders
> Önkoşul: Sprint 3 tamamlandı
> Hedef: PO → Mal kabul → Stok girişi uçtan uca çalışır

- [ ] Receiving/Details tam test — build düzeldikten sonra akış testi
- [ ] Putaway akışı — ReceivingHeader → PutawayTask → Bin ataması
- [ ] AutoTraceabilityService — Lot/Serial otomatik üretim testi
- [ ] `/receiving/terminal` — el terminali barkod akışı
- [ ] Receiving → StockMovement (RECEIPT) doğru yazılıyor mu?
- [ ] PO → Receiving header otomatik link — PO onaylandığında Receiving oluşuyor mu?
- [ ] PO/Details CS8601 uyarıları temizle

**Kabul Kriteri:** PO oluştur → onayla → Receiving aç → satır ekle → post → stok artıyor

---

## SPRINT 5 — M04 Sales Orders + M05 Shipping
> Önkoşul: Sprint 4 tamamlandı
> Hedef: Sipariş → sevkiyat → stok çıkışı akışı tam çalışır

- [ ] SO/Details CS8601 + CS8602 uyarıları temizle
- [ ] SO → PickTask otomatik oluşturma
- [ ] SO statü akışı: DRAFT → APPROVED → PICKING → SHIPPED
- [ ] Shipping/Details IsNew hatası — Sprint 0'da düzeltildi, akış testi
- [ ] Shipping → StockMovement (ISSUE) doğru yazılıyor mu?
- [ ] `/shipping/terminal` — el terminali sevkiyat akışı
- [ ] SO → Shipping otomatik link (`TODO: Sales Order Notify Logic` implement et)
- [ ] Kısmi sevkiyat desteği

**Kabul Kriteri:** SO oluştur → onayla → sevkiyat yap → post → stok azalıyor

---

## SPRINT 6 — M06 Picking + M07 Transfer
> Önkoşul: Sprint 5 tamamlandı
> Hedef: Depo içi pick ve transfer terminalde çalışır

- [ ] PickTask FIFO/FEFO allocation — AllocationStrategy parametresine göre
- [ ] `/picking/terminal` — el terminali ana ekranı (barkod okuma akışı)
- [ ] Partial pick desteği
- [ ] Pick → Shipping otomatik güncelleme
- [ ] Transfer/Details FromBinId, ToBinId hatası — Sprint 0'da düzeltildi, akış testi
- [ ] Putaway.cshtml.cs ItemId hatası — Sprint 0'da düzeltildi, test
- [ ] Bin-to-bin transfer → StockMovement (TRANSFER) çifti doğrulanır
- [ ] `/transfer/terminal` — el terminali transfer akışı
- [ ] Replenishment ekranı (schema_M07_Replenishment.sql)

**Kabul Kriteri:** PickTask oluştur → terminalde onayla → bin-to-bin transfer → stok değişiyor

---

## SPRINT 7 — M08 Cycle Count + M09 Traceability
> Önkoşul: Sprint 6 tamamlandı
> Hedef: Sayım yapılabiliyor, lot/seri takibi izlenebiliyor

- [ ] CycleCount/Details akış testi (Sprint 0 fix sonrası)
- [ ] Sayım başlatma → CycleCountLine oluşturma (QtySystem otomatik doldurma)
- [ ] Sayım farkı → COUNT_ADJ StockMovement
- [ ] Tolerance kontrolü (CountTolerance parametresi)
- [ ] `/cyclecount/terminal` — terminal sayım akışı
- [ ] LPN yönetimi — `/lpn` ekranı
- [ ] Lot listesi + hareket geçmişi — `/lot`
- [ ] Serial listesi + konum — `/serial`
- [ ] Lot/Serial bazlı stok bakiyesi görünümü

**Kabul Kriteri:** Sayım oluştur → satır gir → post → stok düzeltmesi oluşuyor; Lot geçmişi izlenebiliyor

---

## SPRINT 8 — M10 Manufacturing
> Önkoşul: Sprint 7 tamamlandı
> En Karmaşık Sprint — Dikkatli planlanacak

- [ ] Production/Details ItemId hatası — Sprint 0'da düzeltildi, akış testi
- [ ] **KRİTİK:** `DynamicBomService.EvaluateFormula()` — DataTable.Compute() → NCalc
- [ ] İş emri akışı: DRAFT → RELEASED → IN_PROGRESS → COMPLETED
- [ ] BOM hesaplama ve malzeme rezervasyonu
- [ ] ProductionActivityService — iş istasyonu başlat/durdur
- [ ] Hammadde tüketimi (CONSUMPTION StockMovement)
- [ ] Mamul kabulü (PRODUCTION StockMovement)
- [ ] Kalite kontrol — PASS/FAIL/REWORK akışı
- [ ] Rework yönetimi
- [ ] `/production/terminal` — iş istasyonu terminali
- [ ] SO → Production notify (TODO implement)

**Kabul Kriteri:** İş emri oluştur → BOM ile malzeme planla → aktivite başlat/bitir → mamul kabul → stok artıyor

---

## SPRINT 9 — Print Server
> Önkoşul: Sprint 7 tamamlandı (Lot/Serial hazır olmalı)

- [ ] `Operax.PrintServer` — Minimal API proje yapısı kur
- [ ] ZebraService — TCP 9100 raw ZPL gönderici
- [ ] Etiket şablonları: Item barkod · LPN · Koli · Lot · Bin QR
- [ ] Print queue tablosu — veya Hangfire job
- [ ] `AutoTraceabilityService.EnqueueLabelPrintAsync()` → gerçek implementasyon
- [ ] Receiving sonrası otomatik etiket (parametre ile)
- [ ] LPN oluşturmada otomatik etiket

**Kabul Kriteri:** Zebra yazıcıya ağ üzerinden etiket basılabiliyor

---

## SPRINT 10 — M15 Dashboard + Raporlar
> Önkoşul: Sprint 8 tamamlandı (tüm hareketler hazır)

- [ ] Ana dashboard — KPI kartları (günlük: Receiving, Shipping, Production)
- [ ] Stok özeti widget — kritik/düşük stok uyarısı
- [ ] Bekleyen işler — açık pick task, bekleyen PO, onay bekleyen SO
- [ ] Haftalık giriş/çıkış trend grafiği
- [ ] Kullanıcı bazlı dashboard konfigürasyonu

**Kabul Kriteri:** Yönetici login → anlık KPI, bekleyen işler, stok durumu görebiliyor

---

## Gelecek Faz (Tarihlenmemiş)

| Modül | Kod | Schema | Öncelik |
|---|---|---|---|
| Expenses | M18 | Hazır | Orta |
| Budgeting | M19 | Hazır | Orta |
| Service/Maintenance | M12 | Yok | Düşük |
| Project Management | M13 | Yok | Düşük |
| Integration Bridge (ERP Webhook) | M16 | Yok | Düşük |
| Email Notifications | — | — | Orta |
| 2FA + Gelişmiş Audit | — | — | Düşük |
