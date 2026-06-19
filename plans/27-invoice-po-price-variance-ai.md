# Plan 27 — Fatura↔Sipariş Fiyat Farkı + AI Gerekçe Denetimi

**Tier 3** · Durum: TASLAK (onay bekliyor) · Tarih: 2026-06-01

## Problem
Alış faturası onaylanırken satır fiyatı satınalma siparişindeki (PO) fiyatla karşılaştırılmıyor. Satınalmada anlaşılan fiyat = gerçek; faturada farklı gelirse **tolerans olmadan** fark kaydı oluşmalı. Satınalmacı farkı override edebilmeli ama **zorunlu gerekçe** girmeli; gerekçenin anlamlı olup olmadığı **yerel AI (qwen2.5)** ile denetlenmeli.

## Kullanıcı Kararları
- **Tolerans YOK.** PO fiyatından her sapma → fark kaydı (eşik yok).
- **Onay davranışı:** Fatura onaylanır (bloke değil), PriceVariance DRAFT ayrı izlenir (3-way match yumuşak).
- **Override:** satınalmacı zorunlu gerekçe girer; AI gerekçeyi denetler (advisory).
- **AI sağlayıcı:** YALNIZCA yerel qwen2.5. Bulut/OpenAI YOK — veri makineden çıkmaz. Endpoint Ollama API formatı (`http://localhost:11434/v1/chat/completions`). "OpenAI-compatible" = sadece JSON istek şeması adı (Ollama bunu konuşur), OpenAI şirketiyle ilgisi yok. Pattern Mosaik'ten (`D:\Dev\reporthub\Mosaik\Services\AiSummaryProvider.cs`) porte. **Not: yerel qwen şu an çalışmıyor (11434 boş) → AI verdict UNCHECKED kalır, sunucu açılınca otomatik çalışır.**
- **AI down/timeout:** yumuşak geç — gerekçe kaydedilir, "AI denetlenemedi" işaretlenir, iş bloke olmaz.

## Scope (5 faz)

### Faz A — Şema
`PriceVariance` tablosuna kolon ekle (idempotent ALTER):
- `OverrideReason NVARCHAR(MAX) NULL` — satınalmacı gerekçesi
- `AiVerdict NVARCHAR(20) NULL` — `PLAUSIBLE` / `IMPLAUSIBLE` / `UNCHECKED`
- `AiComment NVARCHAR(500) NULL` — AI'ın kısa değerlendirmesi
- `AiCheckedAt DATETIME2 NULL`
- `SourceDocType` zaten var → `PURCHASE_INVOICE` değeri kullanılacak.

### Faz B — SP: fatura onayında fark tespiti
`sp_PurchaseInvoicePost` içine (AccountMovement INSERT'inden önce): her `PurchaseInvoiceLine` için
PO satır fiyatını bul (`PurchaseInvoiceLine.SourceReceivingLineId → ReceivingLine.PurchaseOrderLineId → PurchaseOrderLine.Price`).
Fark varsa (`UnitPrice <> pol.Price`, tolerans yok) `PriceVariance(SourceDocType='PURCHASE_INVOICE', SourceDocId=@InvoiceId, SourceLineId=line, ExpectedPrice=pol.Price, ActualPrice=line.UnitPrice, Status='DRAFT')` INSERT. **Post yine başarılı.** PO bağı yoksa (manuel satır) fark üretilmez.

### Faz C — C# AI client (minimal, porte)
`Lib/Ai/OperaxAiClient.cs` — OpenAI-uyumlu `/chat/completions`, JSON response, soft-fail:
- Config `appsettings.json` `Ai` bölümü: `Enabled`, `BaseUrl` (default `http://localhost:11434/v1`), `Model` (`qwen2.5`), `ApiKey` (ollama'da boş), `TimeoutSeconds`.
- `IHttpClientFactory` ("ai" client), DI singleton.
- Metod: `Task<AiReasonVerdict> CheckJustificationAsync(string context, string reason, ct)` → JSON `{verdict, comment}`. Hata/timeout → `UNCHECKED`.
- `Ai:Enabled=false` veya bağlantı yok → her zaman `UNCHECKED` (çekirdek etkilenmez).

### Faz D — UI: PriceVariances onay akışı
`PurchaseOrders/PriceVariances` (mevcut) genişlet veya fatura kaynaklı için filtre:
- DRAFT fark satırında **gerekçe textarea (zorunlu)** + "Onayla (Override)".
- Onayda: AI `CheckJustificationAsync` çağrılır → `AiVerdict`/`AiComment` kaydedilir, gösterilir.
- AI `IMPLAUSIBLE` dese de satınalmacı yine onaylayabilir (advisory, kilit değil) — ama AI yorumu görünür kalır (denetim izi).
- `Reddet` → `REJECTED`.

### Faz E — Review + smoke
build-validator → code-reviewer → sql-sp-reviewer (SP) → security-reviewer (AI client: SSRF/secret) → E2E smoke (PO farklı fiyatlı fatura → variance DRAFT → gerekçe + AI verdict → APPROVED).

## Dosyalar (~8)
- `docs/sql/schema_M02_Costing.sql` (ALTER) veya yeni `schema_M02_PriceVarianceAi.sql` + CLI migrate listesi
- `docs/sql/db_objects_docchain.sql` (sp_PurchaseInvoicePost fark INSERT)
- `src/Operax.Web/Lib/Ai/OperaxAiClient.cs` (+ DTO'lar)
- `src/Operax.Web/Program.cs` (HttpClient + DI + Ai config)
- `src/Operax.Web/appsettings.json` (Ai bölümü, key yok)
- `src/Operax.Web/Features/PurchaseOrders/PriceVariances.cshtml(.cs)` (gerekçe + AI)

## Riskler
- **AI yavaş (yerel qwen):** onay UI'ı AI'ı beklerken donmasın → timeout + soft-fail; gerekirse async/arka plan.
- **Prompt injection (gerekçe metni):** AI'a kullanıcı metni gider; AI çıktısı sadece advisory + kaydedilir, otomatik aksiyon yok → düşük risk.
- **SSRF:** BaseUrl config'den (kullanıcı verisi değil) → güvenli.
- **PO bağı kopuk satır:** manuel satırda PO fiyatı yok → fark üretme (sessiz atla, log).

## 5 Lens
- 🔴 Contrarian: AI gereksiz mi? Gerekçe zorunlu + denetim izi yeterli olabilir; AI "advisory katman", iş onsuz da yürür (soft-fail tasarımı bunu garantiler).
- 🔵 First-principles: Asıl ihtiyaç "anlaşılan fiyat korunsun + sapma izlensin". AI ikincil; çekirdek = variance kaydı.
- 🟢 Expansionist: Aynı AI client ileride fatura OCR/özet/Text2SQL'e taban olur (Plan 07 M17 ile birleşir).
- ⚪ Outsider: "Niye tolerans yok?" → kullanıcı kararı: anlaşılan fiyat bağlayıcı, sapma her zaman görünür olmalı.
- 🟡 Executor: Pazartesi: Faz A şema + Faz B SP (AI olmadan da fark kaydı çalışır) → sonra AI katmanı.

## Rollback
- Faz A/B geri: ALTER kolonları drop, SP'den fark INSERT bloğu çıkar (variance üretmez).
- AI: `Ai:Enabled=false` → tüm akış AI'sız çalışır (gerekçe zorunlu kalır).

## Done Criteria
- PO 10₺ ↔ fatura 11₺ → onay sonrası PriceVariance DRAFT (Expected=10, Actual=11).
- Gerekçe girilmeden override edilemez.
- AI çalışıyorsa verdict+comment kaydedilir; AI down → UNCHECKED, iş akar.
- `Ai:Enabled=false` → çekirdek build+akış bozulmaz.
