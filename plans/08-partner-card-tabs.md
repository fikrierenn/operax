# Plan 08 — Cari Kart Tablı Yapı (360° Cari Görünümü)

> Bu şablon Tier 3 işler içindir.

**Tarih:** 2026-05-29
**Yazan:** Fikri / Claude
**Durum:** `Uygulamada`
**Modül:** M01 (Master Data) + M03/M04/M11 entegrasyon
**Paket:** STARTER

---

## 1. Problem

Cari kart (`Partners/Details`) bugün tek düz kaydırmalı sayfa: düzenleme formu + bakiye KPI + vade analizi + son 30 hareket. Cari ile ilgili diğer her şey (siparişler, fiyatlar, faturalar, çek/senet, ilgili kişiler, adresler, banka hesapları, belgeler, görüşme notları, istatistik) **dağınık** veya **hiç yok**. Kullanıcı bir cariyi 360° görmek için 5-6 farklı ekran geziyor. Rakiplerde (Logo/Mikro/Netsis) cari kart tablı 360° görünümdür — bu en görünür eksiklerden.

## 2. Scope

### Kapsam dahili
- **Tab kabuğu:** `Partners/Details` server-side `?tab=` ile tablı yapıya geçer. Aktif tab dışındaki ağır veri yüklenmez (lazy).
- **Veri-hazır tablar (yeni tablo gerekmez):** Genel · Ekstre/Hareketler · Siparişler · Fiyatlar · Faturalar · Çek/Senet
- **Sorumlu temsilci:** Cari → satış temsilcisi + satınalma sorumlusu ataması. `Partner` tablosuna `SalesRepUserId` + `PurchaseRepUserId` (NVARCHAR(450) NULL, FK `AspNetUsers`). Genel tabında iki dropdown (aktif kullanıcılar). İleride "benim carilerim" filtresi + temsilci bazlı rapor temeli.
- **Yeni tablo + tab:** İlgili Kişiler (`PartnerContact`) · Adresler (`PartnerAddress`) · Banka Hesapları (`PartnerBankAccount`) · Görüşme Notları/CRM (`PartnerActivity`)
- **İstatistik tab:** Aylık ciro grafiği (Chart.js), en çok alınan/satılan ürün, risk & limit doluluk barı
- **Lokal AI altyapısı (erken kurulur):** ReportHub/Mosaik pattern'i port — `Lib/Ai/ILlmRunner.cs` + `LlamaSharpRunner.cs` (LLamaSharp, in-process GGUF, CPU, singleton + `SemaphoreSlim(1,1)`, `IsReady` graceful degrade — model yoksa "AI yapılandırılmamış"). Bulut yok, API key yok. Model `App_Data/models/llm/*.gguf`.
- **AI / Öngörü — tab başına interleave:** 3 özellik runner'ı tüketir: (1) Cari özeti/brief, (2) Ödeme öngörüsü + AI risk önerisi, (3) Sonraki en iyi aksiyon + iletişim taslağı. Her özellik **ilgili tab yapılırken** eklenir (örn. Ekstre/İstatistik hazır olunca brief+öngörü, CRM tabı hazır olunca aksiyon+taslak). Ayrı toplu "AI tabı" yerine ilgili tab içinde AI kartı + bir özet "AI / Öngörü" sekmesi. Plan 07 bu altyapıyı genel asistan için de paylaşır. (Doğal dil Q&A bu planda yok — Plan 07.)
- Her yeni tablo: idempotent şema + zorunlu kolonlar (CompanyId/IsDeleted/CreatedAt/By/UpdatedAt/By) + CRUD

### İleride (bu plan zemin hazırlar, ayrı iş)
- **Temsilci bazlı satır-seviyesi yetki:** Admin olmayan kullanıcı yalnızca kendi atandığı carileri görür (`WHERE SalesRepUserId = @CurrentUserId OR PurchaseRepUserId = @CurrentUserId`). Bu plandaki `SalesRepUserId`/`PurchaseRepUserId` kolonları bunun temelidir. Partners/Index + ilgili sorgulara opsiyonel filtre + rol kontrolü olarak ileride eklenir.

### Kapsam dışı
- **Belgeler/Ekler (`PartnerAttachment`)** — dosya yükleme/saklama altyapısı (filesystem vs blob, güvenlik, virüs tarama) ayrı karar gerektirir → **ayrı mini-plan**, bu plana dahil değil.
- Cari Ekstre yazdırılabilir raporu (`Partners/Statement` — TODO FEAT-EKSTRE) ayrı iş; bu plandaki "Ekstre" tabı ekran-içi görünüm, yazdırma o işte.
- Çoklu para birimi dönüşümü (açık sipariş/bakiye TRY varsayımı korunur).

### Etkilenen dosyalar (tahmin)
- `docs/sql/schema_M01_PartnerExtended.sql` — YENİ: 4 tablo (Contact/Address/BankAccount/Activity) + `ALTER Partner ADD SalesRepUserId, PurchaseRepUserId`
- `docs/sql/db_objects_starter.sql` — opsiyonel TVF/özet (istatistik aggregate)
- `src/Operax.Web/Features/MasterData/Partners/Details.cshtml(.cs)` — tab kabuğu + Genel
- `src/Operax.Web/Features/MasterData/Partners/Tabs/_*.cshtml` — her tab partial (10+ partial)
- `src/Operax.Web/Lib/Dtos.cs` veya yeni `PartnerTabDtos.cs` — tab DTO'ları
- `src/Operax.Web/wwwroot/css/parts/_misc.css` — tab CSS zaten var, gerekirse genişlet

**Tahmini boyut:** ~25-35 dosya / çok oturumlu. Faz faz shippable.

## 3. Alternatifler

### A: Client-side JS tab (tümünü baştan yükle)
**Açıklama:** Tek sayfada tüm tab verisi yüklenir, JS show/hide.
**Reddetme sebebi:** 8-10 veri seti her açılışta → yavaş, gereksiz DB yükü. Cari kart sık açılır.

### B: Her kaynak ayrı sayfa (`/Partners/{id}/Orders`)
**Açıklama:** Siparişler/Fiyatlar/Faturalar ayrı route'lar.
**Reddetme sebebi:** Birleşik cari kart bağlamı kaybolur, 10+ yeni route, breadcrumb karmaşası, "360° görünüm" hedefiyle çelişir.

### C: (seçilen) Tek Details + server-side `?tab=` lazy load
**Açıklama:** `Partners/Details/{id}?tab=orders`. `OnGetAsync` sadece aktif tab verisini + her zaman Genel başlığını yükler. Tab bar `.tabs/.tab` semantic class (zaten var), her tab içeriği ayrı `Tabs/_*.cshtml` partial.
**Sebep:** Performanslı (lazy), birleşik bağlam korunur, mevcut tab CSS + partial pattern'e oturur, route patlaması yok. Tab sayısı arttıkça partial eklemek ucuz.

**5 lens kontrolü:**
- 🔴 **Contrarian:** Fatal flaw — 10 tab × CRUD = devasa iş; yarıda kalırsa yarısı boş tab. Mitigasyon: faz faz, her faz kendi içinde shippable, boş tab gösterilmez (sadece hazır olanlar render).
- 🔵 **First Principles:** Gerçek ihtiyaç "cariyi tek yerden görmek". Edit formu + 3-4 okuma tabı bile değer üretir; tüm CRUD şart değil — okuma-önce, yazma-sonra.
- 🟢 **Expansionist:** Bu pattern (entity + tablar) ileride Item kartı, Proje kartı için de şablon olur → `_EntityTabs` reusable partial fırsatı.
- ⚪ **Outsider:** Yabancı "neden cari düzenleme formu hâlâ sayfanın yarısını kaplıyor" der → Genel tabı da kompakt olmalı, form tek tab.
- 🟡 **Executor:** Pazartesi: tab kabuğu + Genel + Siparişler + Fiyatlar (Faz 0-1). En çok istenen ikisi.

## 4. Riskler

| Risk | Etki | Olasılık | Mitigation |
|---|---|---|---|
| Scope patlaması (10 tab yarıda kalır) | orta | yüksek | Faz faz, her faz commit + shippable; hazır olmayan tab gizli |
| `Details.cshtml` 500 satır kırmızı çizgiyi aşar | orta | yüksek | Her tab ayrı partial; PageModel'i `PartnerTabService` ile böl |
| Yeni 4 tablo migrate riski | düşük | düşük | Idempotent CREATE, additive, geri alınabilir (DROP) |
| Edit formu tab içine girince mevcut POST kırılır | orta | orta | Genel tabı mevcut form'u aynen taşır, route/handler değişmez |
| Çoklu para / TRY varsayımı istatistikte yanıltır | düşük | orta | TRY varsayımı not düşülür, ileride kur dönüşümü ayrı iş |

## 5. Done Criteria

- [ ] `Partners/Details/{id}?tab=X` ile tab geçişi çalışır, aktif tab dışı veri yüklenmez
- [ ] Veri-hazır 6 tab render: Genel, Ekstre, Siparişler, Fiyatlar, Faturalar, Çek/Senet
- [ ] 4 yeni tablo + CRUD tab: Contact, Address, BankAccount, Activity
- [ ] İstatistik tab: aylık ciro grafiği + top ürün + risk/limit barı
- [ ] Her tab boş durumda `_EmptyState`
- [ ] `Details.cshtml` ve PageModel 500 satır altında (partial + service split)
- [ ] `operax-cli migrate` 0 hata
- [ ] `dotnet build` 0 hata 0 uyarı
- [ ] Inline style sadece layout (renk/font yok — `inline-style-guard`)
- [ ] Tüm UI Türkçe

## 6. Rollback Planı

- Git revert faz commit'leri (her faz ayrı commit).
- Yeni tablolar additive → `DROP TABLE PartnerContact/Address/BankAccount/Activity` (veri yoksa risksiz).
- Tab kabuğu revert edilirse mevcut düz sayfa geri gelir (Genel tab içeriği = eski form).

## 7. Adımlar / Fazlar

### Faz 0 — Tab kabuğu + Genel + sorumlu temsilci
1. [ ] **TAB-0** `Details.cshtml` tab bar (`.tabs`), `?tab=` query param, `ActiveTab` enum
2. [ ] **TAB-0** `ALTER Partner ADD SalesRepUserId, PurchaseRepUserId` (NVARCHAR(450) NULL, FK AspNetUsers)
3. [ ] **TAB-0** Mevcut form → `Tabs/_Genel.cshtml` + iki temsilci dropdown'u (POST handler/route değişmez, bind alanları eklenir)
4. [ ] **TAB-0** Mevcut bakiye KPI + vade + son30 → `Tabs/_Ekstre.cshtml`
5. [ ] Commit: `feat(partner): tablı kart kabuğu + Genel/Ekstre + sorumlu temsilci (plan: 08)`

### Faz 1 — Veri-hazır okuma tabları
5. [ ] **TAB-1** Siparişler tabı — SO+PO by PartnerId, açık/kapalı/iptal alt-sekme, açık tutar
6. [ ] **TAB-1** Fiyatlar tabı — `PriceList`+`PriceListLine` WHERE PartnerId, yön + geçerlilik
7. [ ] **TAB-1** Faturalar tabı — Sales/ExpenseInvoice by PartnerId, ödeme durumu
8. [ ] **TAB-1** Çek/Senet tabı — Cheque+PromissoryNote portföyü by PartnerId
9. [ ] Commit: `feat(partner): sipariş/fiyat/fatura/çek-senet tabları (plan: 08)`

### Faz 2 — Yeni tablolar + CRUD tabları
10. [ ] **TAB-2** `schema_M01_PartnerExtended.sql`: PartnerContact, PartnerAddress, PartnerBankAccount, PartnerActivity
11. [ ] **TAB-2** İlgili Kişiler tabı + inline CRUD
12. [ ] **TAB-2** Adresler tabı (sevk/fatura, varsayılan) + CRUD
13. [ ] **TAB-2** Banka Hesapları tabı + CRUD
14. [ ] **TAB-2** Görüşme Notları (CRM) tabı + ekle/listele
15. [ ] Commit: `feat(partner): contact/address/bank/activity tabları + şema (plan: 08)`

### Faz 3 — İstatistik
16. [ ] **TAB-3** Aylık ciro grafiği (Chart.js, `GROUP BY DATEFROMPARTS`)
17. [ ] **TAB-3** En çok alınan/satılan ürün (top N)
18. [ ] **TAB-3** Risk & limit doluluk barı (mevcut Risk alanları + kredi limiti kullanımı)
19. [ ] Commit: `feat(partner): istatistik tabı (plan: 08)`

### Faz 4 — Cleanup
20. [ ] Dosya boyut kontrolü (500 satır), service split
21. [ ] `docs/TODO.md` + journal güncelle, plan arşivle

> `docs/TODO.md`'ye bu maddeler ayrıca eklenecek (plan onayından sonra).

## 8. İlişkili

- `.claude/rules/ui-standard.md` §4 (partial), §6 (form), tab CSS `_misc.css`
- `.claude/rules/inline-style-guard.md` — renk/font inline yasak
- `.claude/rules/document-immutability.md` — sipariş/fatura okuma tabları readonly
- `[[open-orders-not-in-ledger]]` — sipariş bakiyeye karışmaz (Siparişler tabı bilgi amaçlı)
- TODO: FEAT-EKSTRE (Cari Ekstre raporu — Ekstre tabının yazdırılabilir versiyonu)
- Kapsam dışı: `PartnerAttachment` (Belgeler/Ekler) → ayrı mini-plan

## 9. Onay

- [x] Plan kullanıcıya gösterildi
- [x] Geri bildirim alındı (sorumlu temsilci + ileride satır-yetki notu eklendi, belgeler kapsam dışı)
- [x] Onay alındı: 2026-05-29, Fikri
