# OPERAX — Master Sprint Planı

> Sürüm: v1.0 | Oluşturma: Mart 2026
> Güncelleme: Her sprint tamamlandığında `[x]` işaretle, tarih yaz.
> Bağlı dosyalar: `docs/BUGS.md` · `docs/SPRINTS.md` · `docs/TODO.md` · `RULES.md`

---

## 🎯 KALAN İŞ ÖNCELİK SIRASI (2026-06-21 — bu sırayla ilerlenir)

> Eksen: go-live değeri × maliyet × bağımlılık. Bittikçe `[x]` + tarih + commit.

### Tier 0 — Hemen (ucuz, gürültü temizler)
- [ ] **T0.1 Push** — bekleyen commit'ler → PR #1 güncel (Codex re-review)
- [ ] **T0.2 Stale plan arşivle** — 15 plan 3 hafta dokunulmamış; çoğu Plan 33/35 gibi done-ama-arşivlenmedi. Doğrula → `plans/archive/`. Gerçek durum netleşir.

### Tier 1 — In-flight bitir + yüksek-değer/düşük-maliyet
- [ ] **T1.1 Plan 37 test Faz 3e + 4** — UOM/fiyat (fn_GetConversionRate, sp_CheckPriceVariance) + auth/role/company-switch. Harness hazır.
- [ ] **T1.2 B3 Satınalma öneri-sipariş** — tvf_ReplenishmentSuggestions verisi hazır (NeededQty+tedarikçi+leadtime+MOQ); eksik: ekran + tedarikçiye gruplu draft-PO action. Ticari için yüksek değer, düşük footprint.
- [ ] **T1.3 ILogger DI** — ~12 PageModel catch'inde logger bağlı değil (TODO.md F0 CRIT-4). Mekanik, silent-failure kapatır.

### Tier 2 — Kalite / refactor
- [ ] **T2.1 Partners/Details split** — 550 satır (kırmızı çizgi), Service Layer'a böl.
- [ ] **T2.2 UI Portu semantic-class migration** — ~75 view (U-1/U-2), görsel tutarlılık.
- [ ] **T2.3 MEDIUM/LOW birikim** — SELECT* ~22, magic-string ~14, CancellationToken.

### Tier 3 — Greenfield modüller (her biri ayrı Tier-3 plan; sıra hedef müşteriye bağlı)
- [ ] **T3.1 Integration / M16** — kargo webhook + e-Belge köprü (boş modül, ticari için kritik altyapı).
- [ ] **T3.2 B1 MRP + B2 üretim planlama** — talep netleme + BOM patlatma + planlı sipariş / MPS-kapasite.
- [ ] **T3.3 B4 Forecast** — talep tahmini (MRP ön koşulu).
- [ ] **T3.4 Service · Project · Incentives** — boş modüller, niş; talep gelince.

**KARAR BEKLİYOR:** Tier 3 iç sırası (Integration mı MRP mi önce) = ağırlık ticari mi üretim mi? Netleşince güncellenir.

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
> **TAMAMLANDI ✅ — Mart 2026, commit 694c2b4**

- [x] Tüm 19 build hatası giderildi
- [x] Güvenlik açıkları kapatıldı (Newtonsoft.Json 13.0.3, hardcoded credentials)
- [x] 27 uyarı temizlendi (CS8602, CS8601, CS9113)

**Kabul Kriteri:** `dotnet build src/Operax.Web/Operax.Web.csproj` → 0 hata, 0 uyarı ✅

---

## SPRINT 1 — M00 Platform Core Stabilize
> Önkoşul: Sprint 0 tamamlandı
> Hedef: Login, admin ekranları, role-based auth sorunsuz çalışıyor
> **IN PROGRESS — Mart 2026**

- [ ] Login akışı test — company claim düzgün set ediliyor mu?
- [ ] `CurrentCompany.Id` middleware'i doğrula — Guid.Empty gelmiyor mu?
- [x] Rol bazlı kullanıcı yönetimi — Users/Create + Edit'e rol dropdown + company claim ataması eklendi
- [x] Roles/Create sayfası implement edildi (yeni rol oluşturma)
- [x] Roles/Index — Sil handler eklendi (Administrator rolü korumalı)
- [ ] `/admin/audit-log` ekranı — schema var, UI yok → **SIRADAKI**
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
