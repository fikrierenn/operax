# Operax — Müşteriye-Özel Ekran/Form + Özel Kod (Customization & Extensibility)

> Derin referans araştırması (2026-06-19, reference-researcher) sentezi: SAP · 1C · Odoo · Dynamics 365 BC · ERPNext müşteri-özelleştirme mekanizmaları → Operax (.NET 10 + Razor Pages + Dapper + SQL-First + single-tenant) uyarlaması. Stack kararı değişmez; referanslardan yalnız **extensibility deseni** alındı.

## Yönetici özeti
- **Operax no-code katmanı (UDF) referanslarla paritede.** `UdfService` + `UserFieldDefinition` + `_CustomFields.cshtml` = Frappe Custom Field / SAP append-structure / BC pageextension muadili (TEXT/NUMBER/SELECT/DATE/BOOLEAN + STATIC/DICTIONARY/TABLE + whitelist + sunucu validasyon).
- **En büyük boşluk: pro-code C# event/hook yok** (`IDocumentHook` Grep=0). Beş platformun ortak/merkezi mekanizması — Operax'ta SP tarafında var (SQL-First, SP revizyonu) ama C# onay-öncesi/sonrası hook yok.
- **İkinci boşluk: named-slot ekran enjeksiyonu yok** (Odoo `xpath position`, BC pageext muadili). UDF tek sabit noktaya basıyor; keyfi bölgeye (toolbar/satır-altı/sekme) enjeksiyon yok. ViewComponent projede sıfır.

## Platformların ortak dersi
**"Stabil bir hook noktası yayınla; müşteri kodu oraya abone olsun; publisher abonelerini bilmez."** — BC publisher/`[EventSubscriber]`, Frappe `doc_events` dict, SAP BAdI interface, Odoo `super()` zinciri, 1C extension overlay. Hepsi aynı ilke.

| Platform | Mekanizma | Operax'a alınan | Reddedilen |
|---|---|---|---|
| SAP | BAdI interface + enhancement framework | interface-hook ilkesi, filter-BAdI (koşullu hook) | implicit enhancement (keyfi satıra kod), user-exit, repo-merge upgrade |
| 1C | Configuration Extensions (ayrı overlay config) | müşteri kodu **ayrı derleme birimi** (`Operax.Ext.*`) | interpreted dil + runtime metadata merge (perf'e zıt) |
| Odoo | model override (`super()`) + view inheritance (`xpath position`) | iki-eksen ayrımı (mantık ↔ ekran), named-slot enjeksiyon | runtime XML-arch parsing + ORM |
| Dynamics BC | `[IntegrationEvent]` publisher/subscriber + pageextension | `IDocumentHook` birebir referansı (çok-abone, publisher bilmez) | runtime extension yükleme + dispatcher |
| ERPNext/Frappe | `doc_events` dict (DocType→lifecycle→handler) | **registry/dictionary** modeli (hard-coded if-else değil) | Python introspection, `override_doctype_class` (çoklu-app çakışır) |

## Operax katman modeli — no-code → pro-code merdiveni (footprint-ladder uyumlu)

| # | Basamak | Mekanizma | Referans muadili | Durum |
|---|---|---|---|---|
| 1 | No-code alan | UDF (`UdfService` + Admin/UdfFields) | Frappe Custom Field, SAP append-struct | ✅ VAR |
| 2 | No-code iş kuralı (SQL) | Müşteriye özel SP revizyonu (`sp_*Post`) | SAP BAdI (SQL eşdeğeri), 1C extension | ✅ VAR (`architecture §4`) |
| 3 | Düşük-kod ekran bölgesi | Named-slot + ViewComponent ile çekirdek forma bölge enjeksiyonu | Odoo `xpath`, BC pageext | ❌ ÖNERİ (B2) |
| 4 | Pro-code C# hook | `IDocumentHook` (Before/AfterPost) + DI registry | BC publisher/subscriber, Frappe `doc_events` | ❌ ÖNERİ (B1, en yüksek değer) |
| 5 | Tam custom Razor sayfa | Müşteriye özel feature klasörü | 1C extension yeni form | ✅ mümkün |

## B1 — `IDocumentHook` tasarımı (BC + Frappe sentezi; EN YÜKSEK DEĞER)

Single-tenant + derlenmiş → **runtime assembly load YOK** (referansların en pahalı kısmı atlanır). Müşteri kodu ayrı `Operax.Ext.<Musteri>` projesi, derleme-zamanı DI ile linklenir, çekirdek repo'ya dokunmaz (1C extension ilkesi).

```
Lib/Hooks/IDocumentHook.cs:
  Task BeforePostAsync(DocContext ctx, IDbConnection conn, IDbTransaction tx);
  Task AfterPostAsync(DocContext ctx, ...);
  DocContext = { DocType, HeaderId, CompanyId, UserId }
```
- Çoklu kayıt: `services.AddScoped<IDocumentHook, AcmeReceivingHook>()` (BC "çok abone"). Çekirdek hook'ları bilmez → `IEnumerable<IDocumentHook>` enjekte, `DocType` filtresiyle ilgilileri çağırır (Frappe `doc_events` filtresi).
- **KRİTİK atomiklik kararı (`architecture §4` + ledger immutability):** ledger/stok yan-etkisi **HER ZAMAN SP içinde** kalır. C# hook = **yalın yan-etki** (bildirim, dış API, audit, e-Belge tetik). Stok/cari hareketi C# hook'a TAŞINMAZ → SP transaction atomikliği bozulmaz. (Araştırma seçenek-ii; sql-sp-reviewer zorunlu.)
- Çağrı yeri: onay orkestrasyonu (PageModel'de SP çağrısının çevresi) — `Receiving/Details.cshtml.cs`, `Shipping/Details.cshtml.cs` vb.

## B2 — Named-slot ekran enjeksiyonu (Odoo xpath muadili)
Razor'da xpath yok; eşdeğeri: `<vc:doc-extension zone="Receiving.AfterLines" model="ctx" />` ViewComponent → DI'dan `IDocExtension`'ları `zone` filtresiyle çağırır. UDF paneli bu mekanizmanın özel hali olur (genelleştirme). ViewComponent yeni pattern (Tier 3).

## Reddedilenler (Operax'a uymaz)
- Runtime assembly load / plugin discovery (single-tenant → derleme-zamanı DI yeterli).
- 1C interpreted dil / runtime form merge (perf'e zıt — Plan 34 §3 zaten reddetti).
- SAP implicit enhancement + user-exit (çekirdek çatallama anti-pattern).
- Frappe `override_doctype_class` (çoklu-app çakışır).
- **Ledger/stok yan-etkisini C# hook'a taşımak** (§4 atomik SP + immutability kırılır).

## Backlog (etki/maliyet)
| # | İş | Basamak | Etki | Maliyet |
|---|---|---|---|---|
| B1 | `IDocumentHook` (Before/AfterPost, DI registry, yalın yan-etki) + `Operax.Ext.*` proje konvansiyonu | 4 | Yüksek | Orta (Tier 3 plan + phase-gate, sql-sp-reviewer) |
| B2 | Named-slot ViewComponent (`zone`) + UDF'i bu mekanizmaya devr | 3 | Orta | Orta (yeni pattern, Tier 3) |
| B3 | `IValidationRule<T>` (onay öncesi müşteri kuralı, B1 alt-kümesi) | 4 | Orta | Düşük (B1 üstüne) |
| B4 | No-code event→aksiyon kural tablosu | 2/5 | Düşük | Yüksek → **ERTELE** (SP+Hangfire yeterli) |

## DOĞRULANMADI
- SAP BAdI belirli kod imzaları (help.sap.com JS-render; ilke güvenli, imza snippet'ten).
- B1 atomiklik (seçenek i/ii) uygulama planı + phase-gate gerektirir.

## İlişkili
- `plans/34-udf-custom-fields.md` (no-code katmanı — basamak 1, yapıldı) · `docs/design/DYNAMIC_CUSTOM_FIELDS.md`.
- `.claude/rules/architecture.md §4` (atomik SP — B1 kısıtı) · `§8` (UDF) · `.claude/rules/footprint-ladder.md`.
- `docs/ADR/02-reference-data-hybrid.md` (referans veri kararı — komşu konu).
- `src/Operax.Web/Lib/UdfService.cs`, `Features/Shared/_CustomFields.cshtml`, `Features/Receiving|Shipping/Details.cshtml.cs` (B1 hook gireceği yer).
