# OPERAX — Bug & Hata Takibi

> Güncelleme: Mart 2026
> Son build: **0 hata · 0 uyarı · BUILD SUCCESS** ✅ (commit: 694c2b4)
> Format: `[ ]` açık · `[x]` çözüldü · Çözüldüğünde tarih + commit notu ekle

---

## HATA SEVİYELERİ

- 🔴 **ERROR** — Build fail, düzeltilmeden devam edilemez
- 🟠 **SECURITY** — Güvenlik açığı, bir sonraki build'de çözülmeli
- 🟡 **WARNING** — Build geçer ama kod kötü, aynı sprint'te temizlenmeli
- 🔵 **DESIGN** — Mimari sorun, sprint kapsamında ele alınacak

---

## 🔴 BUILD HATALARI (CS ERROR — 19 adet)

### BUG-001 · AddDefaultIdentity CS1061
```
Dosya  : src/Operax.Web/Program.cs
Satır  : 16
Hata   : CS1061 — 'IServiceCollection' does not contain 'AddDefaultIdentity'
Sebep  : Microsoft.AspNetCore.Identity.UI paketi csproj'da var ama
         using Microsoft.AspNetCore.Identity eksik olabilir veya
         paket sürümü .NET 10 ile uyumsuz.
Çözüm  : using ekle veya AddIdentity<IdentityUser, IdentityRole>() kullan.
Sprint : S0
```
- [x] Çözüldü: Mart 2026 — commit 694c2b4 (Sprint 0+1)

---

### BUG-002 · Shipping IsNew CS1061 (cshtml)
```
Dosya  : src/Operax.Web/Features/Shipping/Details.cshtml
Satırlar: 4, 10, 11, 17, 23, 76
Hata   : CS1061 — 'DetailsModel' does not contain definition for 'IsNew'
Sebep  : Details.cshtml.cs satır 20'de IsNew tanımlı görünüyor ama
         derleme zamanında model bağlaması çözülemiyor.
Çözüm  : Details.cshtml.cs'de IsNew property'nin erişilebilirliğini kontrol et.
         public bool IsNew => Header.Id == Guid.Empty;  ← satır 20'de mevcut
         Sorun muhtemelen ShippingHeaderDto.Id default değeri Guid.Empty değil.
Sprint : S0
```
- [x] Çözüldü: Mart 2026 — commit 694c2b4 (Sprint 0+1)

---

### BUG-003 · Shipping IsNew CS0103 (cshtml.cs)
```
Dosya  : src/Operax.Web/Features/Shipping/Details.cshtml.cs
Satır  : 140
Hata   : CS0103 — 'IsNew' not found in current context
Sebep  : OnPostAsync metodu içinde IsNew doğrudan kullanılıyor,
         ancak bu bir instance property — 'this.IsNew' veya direkt
         Header.Id == Guid.Empty kontrolü daha güvenli.
Çözüm  : if (IsNew) → if (Header.Id == Guid.Empty) ile değiştir
Sprint : S0
```
- [x] Çözüldü: Mart 2026 — commit 694c2b4 (Sprint 0+1)

---

### BUG-004 · CycleCount BinId/ItemId/QtySystem/QtyCounted CS0103
```
Dosya  : src/Operax.Web/Features/CycleCount/Details.cshtml.cs
Satır  : 73 (kolonlar: 28, 35, 43, 54)
Hata   : CS0103 — 'BinId', 'ItemId', 'QtySystem', 'QtyCounted' not found
Sebep  : OnPostAddLineAsync metodunda anonim obje başlatılırken
         kısa property syntax (shorthand) kullanılmış ama lokal değişken
         adları DTO property adlarıyla eşleşmiyor.
         Lokal: binId, itemId, qtySystem, qtyCounted (lowercase)
         Anonim: { BinId, ItemId, QtySystem, QtyCounted } (uppercase shorthand)
Çözüm  : new { BinId = binId, ItemId = itemId, QtySystem = qtySystem, QtyCounted = qtyCounted }
         (explicit assignment kullan)
Sprint : S0
```
- [x] Çözüldü: Mart 2026 — commit 694c2b4 (Sprint 0+1)

---

### BUG-005 · MasterData Items UomId CS0103
```
Dosya  : src/Operax.Web/Features/MasterData/Items/Details.cshtml.cs
Satırlar: 83, 90
Hata   : CS0103 — 'UomId' not found in current context
Sebep  : ItemUOM veya ItemBarcode eklerken UomId değişkeni tanımlı değil
         ya da yanlış scope'ta.
Çözüm  : İlgili metodun parametresi olarak Guid uomId ekle veya
         DTO'dan oku.
Sprint : S0
```
- [x] Çözüldü: Mart 2026 — commit 694c2b4 (Sprint 0+1)

---

### BUG-006 · Transfer FromBinId/ToBinId CS0103
```
Dosya  : src/Operax.Web/Features/Transfer/Details.cshtml.cs
Satır  : 79 (kolonlar: 83, 94)
Hata   : CS0103 — 'FromBinId', 'ToBinId' not found
Sebep  : StockTransferLine DTO'sunda FromBinId ve ToBinId property'leri eksik
         ya da metod parametresi olarak alınmamış.
Çözüm  : TransferLineDto'ya Guid FromBinId + Guid ToBinId ekle
         veya metod parametresi olarak al.
Sprint : S0
```
- [x] Çözüldü: Mart 2026 — commit 694c2b4 (Sprint 0+1)

---

### BUG-007 · Transfer Putaway ItemId CS0103
```
Dosya  : src/Operax.Web/Features/Transfer/Putaway.cshtml.cs
Satırlar: 57, 64
Hata   : CS0103 — 'ItemId' not found
Sebep  : Putaway metodunda ItemId değişkeni scope dışında kalıyor.
Çözüm  : Metod parametresi olarak Guid itemId ekle.
Sprint : S0
```
- [x] Çözüldü: Mart 2026 — commit 694c2b4 (Sprint 0+1)

---

### BUG-008 · Production ProductionLineDto.ItemId CS1061
```
Dosya  : src/Operax.Web/Features/Production/Details.cshtml.cs
Satır  : 96
Hata   : CS1061 — 'DetailsModel.ProductionLineDto' does not contain 'ItemId'
Sebep  : ProductionLineDto record'unda ItemId property eksik.
         BOM satırının ürün bilgisine erişmek için gerekli.
Çözüm  : ProductionLineDto'ya Guid ItemId { get; set; } ekle
         ve ilgili SQL SELECT'e l.ItemId sütununu ekle.
Sprint : S0
```
- [x] Çözüldü: Mart 2026 — commit 694c2b4 (Sprint 0+1)

---

## 🟠 GÜVENLİK AÇIKLARI

### SEC-001 · Newtonsoft.Json Kritik CVE
```
Paket  : Newtonsoft.Json 11.0.1
Proje  : src/Operax.Web/Operax.Web.csproj
Hata   : NU1903 — GHSA-5crp-9r3c-p9vr (High severity)
Sebep  : 11.x sürümünde bilinen güvenlik açığı.
Çözüm  : <PackageReference Include="Newtonsoft.Json" Version="13.0.3" />
Sprint : S0
```
- [x] Çözüldü: Mart 2026 — commit 694c2b4 (Sprint 0+1)

---

### SEC-002 · Hardcoded SQL Credentials
```
Dosya  : src/Operax.Cli/Program.cs
Satır  : ~10 (ConnectionString const)
Hata   : Şifre kaynak kodunda açık metin
         "Server=BT-FIKRI\SQLEXPRESS;...Password=***REMOVED***;..."
Çözüm  : appsettings.json veya environment variable'a taşı.
Sprint : S0
```
- [x] Çözüldü: Mart 2026 — commit 694c2b4 (Sprint 0+1)

---

### SEC-003 · DataTable.Compute() Formula Injection
```
Dosya  : src/Operax.Web/Features/Production/DynamicBomService.cs
Satır  : ~77 (EvaluateFormula metodu)
Hata   : Kullanıcı girdisi doğrudan DataTable.Compute()'a veriliyor.
         Kötü niyetli formül ifadesi beklenmedik sonuçlar üretebilir.
Çözüm  : NCalc kütüphanesi kullan (safe, sandboxed expression evaluator)
         <PackageReference Include="NCalc" Version="2.1.0" />
Sprint : S8 (Manufacturing sprint'inde)
```
- [x] Çözüldü: Mart 2026 — commit 694c2b4 (Sprint 0+1)

---

## 🟡 NULL WARNINGS (CS8602 / CS8601 / CS8629 — 27 adet)

### NULL-001 · Receiving/Details.cshtml.cs
```
Satırlar: 34 (CS8601), 91 (CS8602), 130 (CS8602), 146 (CS8602)
Çözüm  : item?.Property ?? default  veya  Guard.NotNull(item, "item") kullan
Sprint : S0
```
- [x] Çözüldü: Mart 2026 — commit 694c2b4 (Sprint 0+1)

### NULL-002 · Shipping/Details.cshtml.cs
```
Satırlar: 32 (CS8601), 68 (CS8602), 172 (CS8602), 211 (CS8602)
Çözüm  : header null ise erken dön; stock null ise 0 kullan
Sprint : S0
```
- [x] Çözüldü: Mart 2026 — commit 694c2b4 (Sprint 0+1)

### NULL-003 · SalesOrders/Details.cshtml.cs
```
Satırlar: 30 (CS8601), 85 (CS8602)
Sprint : S0
```
- [x] Çözüldü: Mart 2026 — commit 694c2b4 (Sprint 0+1)

### NULL-004 · PurchaseOrders/Details.cshtml.cs
```
Satırlar: 30 (CS8601), 85 (CS8602)
Sprint : S0
```
- [x] Çözüldü: Mart 2026 — commit 694c2b4 (Sprint 0+1)

### NULL-005 · MasterData/Items/Details.cshtml.cs
```
Satır  : 27 (CS8601)
Sprint : S0
```
- [x] Çözüldü: Mart 2026 — commit 694c2b4 (Sprint 0+1)

### NULL-006 · Picking/Details.cshtml.cs
```
Satırlar: 73 (CS8602), 74 (CS8602)
Sprint : S0
```
- [x] Çözüldü: Mart 2026 — commit 694c2b4 (Sprint 0+1)

### NULL-007 · Transfer/Details.cshtml.cs
```
Satırlar: 79 (CS8602), 100 (CS8602)
Sprint : S0
```
- [x] Çözüldü: Mart 2026 — commit 694c2b4 (Sprint 0+1)

### NULL-008 · Production/Details.cshtml.cs
```
Satırlar: 49 (CS8602), 77 (CS8602), 121 (CS8602), 121 (CS8629)
Sprint : S0
```
- [x] Çözüldü: Mart 2026 — commit 694c2b4 (Sprint 0+1)

### NULL-009 · CycleCount/Details.cshtml.cs
```
Satır  : 100 (CS8602) — line.QtyDifference null olabilir
Sprint : S0
```
- [x] Çözüldü: Mart 2026 — commit 694c2b4 (Sprint 0+1)

---

## 🟡 UNUSED PARAMETER WARNINGS (CS9113)

### UNUSED-001 · Dictionary/Details.cshtml.cs
```
Dosya  : src/Operax.Web/Features/Admin/Dictionary/Details.cshtml.cs
Satır  : 8, kolon 50
Uyarı  : CS9113 — 'company' parametresi okunmuyor
Çözüm  : Kullanılacaksa kompanyıya göre filtrele; kullanılmayacaksa kaldır
Sprint : S0
```
- [x] Çözüldü: Mart 2026 — commit 694c2b4 (Sprint 0+1)

### UNUSED-002 · Users/Index.cshtml.cs
```
Dosya  : src/Operax.Web/Features/Admin/Users/Index.cshtml.cs
Satır  : 8, kolon 90
Uyarı  : CS9113 — 'roleManager' parametresi okunmuyor
Çözüm  : RoleManager kullanılıyorsa implement et, değilse kaldır
Sprint : S0
```
- [x] Çözüldü: Mart 2026 — commit 694c2b4 (Sprint 0+1)

### UNUSED-003 · AutoTraceabilityService.cs
```
Dosya  : src/Operax.Web/Features/Receiving/AutoTraceabilityService.cs
Satır  : 8, kolon 61
Uyarı  : CS9113 — 'company' parametresi okunmuyor
Çözüm  : Lot/Serial üretiminde CompanyId filtresi ekle (güvenlik için gerekli)
Sprint : S0
```
- [x] Çözüldü: Mart 2026 — commit 694c2b4 (Sprint 0+1)

---

## 🔵 MİMARİ / TASARIM SORUNLARI

### DESIGN-001 · Double ORM Anti-Pattern
```
Sorun  : EF Core (sadece Identity için) + Dapper (her şey) — iki ORM birden
Çözüm  : EF Core'u tamamen kaldır, Identity tablolarını da Dapper ile yönet
         VEYA tüm veri erişimini EF Core'a geçir (Dapper performansı için önerilmez)
Sprint : Gelecek faz
```

### DESIGN-002 · Magic Strings
```
Sorun  : "DRAFT", "POSTED", "CANCELLED", "RECEIPT", "ISSUE", "CONSUMPTION"
         "REWORK", "SCRAP", "FIFO", "FEFO" — tüm kod dosyalarına dağılmış
Çözüm  : Lib/ altında StatusCodes.cs + MovementTypes.cs static class
Sprint : S1 başında yapılacak
```

### DESIGN-003 · Uzun Metodlar (RULES.md: max 80 satır)
```
Sorun  : OnPostCreatePickTaskAsync() ~80 satır (Shipping/Details.cshtml.cs)
         OnPostPostAsync() ~45 satır (birçok dosyada)
Çözüm  : Private helper metodlara böl
Sprint : İlgili modül sprint'inde
```

### DESIGN-004 · Tekrar Eden Kod
```
Sorun  : Receiving, Shipping, Transfer, Production Details'de benzer
         add-line ve post logic tekrarlanıyor (~%15 kod tekrarı)
Çözüm  : Ortak helper service veya base class
Sprint : Gelecek faz
```

### DESIGN-005 · AutoTraceabilityService Print Stub
```
Dosya  : src/Operax.Web/Features/Receiving/AutoTraceabilityService.cs
Sorun  : EnqueueLabelPrintAsync() sadece Debug.WriteLine() yapıyor
         Print server entegrasyonu implement edilmemiş
Çözüm  : S9 (Print Server sprint'i) kapsamında implement et
Sprint : S9
```

### DESIGN-006 · Production Notify Logic Missing
```
Dosya  : src/Operax.Web/Features/Production/ProductionReceiptService.cs
Satır  : ~49
Sorun  : // TODO: Sales Order Notify Logic — eksik implementasyon
         Üretim tamamlandığında ilgili SO güncellenmeli
Çözüm  : S8 (Manufacturing sprint'i) kapsamında implement et
Sprint : S8
```

---

### DESIGN-007 · UI Dili Tutarsızlığı — İngilizce UI Metinleri
```
Karar  : Mart 2026 — UI tamamen Türkçe olacak (RULES.md güncellendi)
Sorun  : Mevcut ekranların büyük bölümünde buton, label, başlık,
         placeholder ve mesajlar İngilizce bırakılmış.
         Etkilenen alanlar (tespit edilenler):
           - Butonlar: "Save", "Cancel", "Post", "New", "Edit", "Delete", "Add Line"
           - Form label: "Warehouse", "Status", "Date", "Notes", "Carrier"
           - Tablo başlıkları: "Code", "Name", "Qty", "UOM", "Actions"
           - Placeholder: "Search...", "Select..."
           - Toast/mesajlar: "Saved.", "Error occurred."
           - Boş durum: "No records found."
           - Sayfa başlıkları: "Receiving", "Shipping", "Picking" vb.
Çözüm  : Her sprint'te üzerinde çalışılan ekran aynı anda Türkçeleştirilir.
         Türkçe karşılıklar (standart):
           Save       → Kaydet
           Cancel     → İptal
           Post       → Onayla / Belgele
           New        → Yeni
           Edit       → Düzenle
           Delete     → Sil
           Add Line   → Satır Ekle
           Warehouse  → Depo
           Status     → Durum
           Date       → Tarih
           Notes      → Notlar / Açıklama
           Carrier    → Taşıyıcı
           Code       → Kod
           Name       → Ad / İsim
           Quantity   → Miktar
           Actions    → İşlemler
           Search     → Ara / Arama yapın...
           Select     → Seçiniz
           No records → Kayıt bulunamadı.
           Saved.     → Kaydedildi.
           Error      → Hata oluştu.
           Back       → Geri
           Details    → Detay
           List       → Liste
Sprint : S1'den itibaren her sprint'te ilgili ekranlar → cross-cutting
```
- [ ] Çözüldü: Tamamlandığında "tüm ekranlar tarandı" notu ekle

---

## BUG ÇÖZÜM KAYDI

> Her bug çözüldüğünde aşağıya ekle.

| Bug ID | Çözüm Tarihi | Çözen | Notlar |
|---|---|---|---|
| BUG-001..009 + SEC-001..003 + NULL-001..009 + UNUSED-001..003 | Mart 2026 | Claude | Sprint 0 — commit 694c2b4 |
| S1-ROL-001 | Mart 2026 | Claude | Users/Create+Edit rol dropdown + company claim ataması eklendi |
| S1-ROL-002 | Mart 2026 | Claude | Roles/Create sayfası oluşturuldu (yeni dosyalar) |
| S1-ROL-003 | Mart 2026 | Claude | Roles/Index Sil handler eklendi; Administrator rolü korumalı |
