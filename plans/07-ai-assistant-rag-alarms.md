# Plan 07 — Operax AI Asistanı (RAG / Text2SQL + Alarm + Aksiyon)

**Tarih:** 2026-05-29
**Yazan:** Fikri / Claude
**Durum:** `Taslak`
**Modül:** M17 (Yeni — AI Layer)
**Paket:** PROFESSIONAL / ENTERPRISE

---

## 1. Problem

Operax verisi zengin ama kullanıcı bilgiye ulaşmak için ekran ekran gezmek, rapor filtrelemek zorunda. Riskli durumlar (nakit açığı, vade gecikmesi, min-altı stok) ancak biri bakarsa fark ediliyor — proaktif uyarı yok. Doğal dille soru sorma, anomali yakalama ve yetkiyle otomatik aksiyon (taslak PO, hatırlatma) yok. Single-tenant + finansal veri gizliliği nedeniyle hazır bulut AI doğrudan bağlanamaz.

Hedef: veriyi sunucudan çıkarmadan (veya müşteri tercihine göre) çalışan, soyut sağlayıcılı bir AI katmanı.

## 2. Scope

### Kapsam dahili
- **Faz 1 — Akıllı Sorgu (Text2SQL + Doküman RAG):** Doğal dil → güvenli read-only SQL → tablo/grafik cevap. Yardım/iş-kuralı dokümanlarında benzerlik araması.
- **Faz 2 — Alarm / Anomali:** Hangfire taramaları, eşik + istatistiksel anomali, nakit açığı tahmini, panel bildirimi + e-posta.
- **Faz 3 — Aksiyon Agent:** Tespit → öneri → **insan onaylı** uygulama (taslak PO, hatırlatma taslağı, eskalasyon).
- **Provider-agnostic** soyutlama: `Microsoft.Extensions.AI` `IChatClient` + `IEmbeddingGenerator`. Host config'den seçilir (Ollama / Azure OpenAI / bulut API).

### Kapsam dışı
- LLM fine-tuning / model eğitimi (sadece prompt + RAG)
- Sesli asistan / STT-TTS
- Otomatik **yazma** işlemlerinin onaysız tetiklenmesi (her zaman human-in-loop)
- Native SQL Server VECTOR kullanımı (SQL Server 2022'de yok; 2025'e ertelendi)
- Çoklu dil — sadece Türkçe asistan

### Etkilenen dosyalar (tahmin)
- `docs/sql/schema_M17_AI.sql` — `AiConversation`, `AiMessage`, `AiQueryLog`, `AiEmbedding`, `AiAlert`, `AiAlertRule`, `AiActionSuggestion`
- `docs/sql/db_objects_ai.sql` — `tvf_*` read-only whitelist view'ları, `sp_AiLogQuery`, anomali tarama SP'leri
- `src/Operax.Web/Lib/Ai/` — `IAiProvider`, `AiProviderFactory`, `Text2SqlService`, `SqlGuard`, `EmbeddingService`, `RagRetriever`
- `src/Operax.Web/Features/Assistant/` — `Index.cshtml(.cs)` sohbet ekranı, `_AlertBell` partial
- `src/Operax.Web/Lib/Jobs/` — `AnomalyScanJob`, `CashForecastJob` (Hangfire)
- `Program.cs` — DI: AI provider + Hangfire job kayıt
- `appsettings.json` — `Ai:Provider`, `Ai:Endpoint`, `Ai:Model`, `Ai:EmbeddingModel`

**Tahmini boyut:** ~22 dosya / ~2200 satır (3 faza bölünür).

## 3. Alternatifler

### A: Doğrudan bulut LLM'e tüm veri gönder (RAG yerine full-context)
**Açıklama:** Sorgu geldiğinde ilgili tabloları toptan LLM'e yolla, yorumlasın.
**Reddetme sebebi:** Finansal veri dışarı sızar (single-tenant gizlilik ihlali), token maliyeti patlar, halüsinasyon yüksek. Sayılar LLM'den gelir → yanlış.

### B: Sadece kural-tabanlı dashboard (AI yok)
**Açıklama:** Sabit eşikli uyarılar + hazır rapor filtreleri, LLM hiç yok.
**Reddetme sebebi:** Doğal dil sorgusu ve esnek anomali olmaz; kullanıcının asıl istediği "akıllı" deneyim karşılanmaz. Sadece Faz 2'nin küçük bir alt kümesi.

### C: SEÇİLEN — Provider-agnostic katmanlı AI (Text2SQL + RAG + Alarm + Agent)
**Açıklama:** `Microsoft.Extensions.AI` soyutlaması; LLM cevabı **SQL üretir veya özetler ama sayıyı SQL döndürür**. Embedding'ler yerel tabloda, benzerlik C#/SQL'de cosine. Host müşteri seçer (yerel default).
**Sebep:** Gizlilik korunur (yerel model mümkün), sayı doğruluğu SQL'den gelir, sağlayıcı değiştirilebilir, fazlara bölünür → risk yönetilir.

**5 lens kontrolü:**
- 🔴 **Contrarian:** Fatal flaw = Text2SQL yanlış/zararlı SQL üretir. Mitigation: read-only connection + view whitelist + üretilen SQL'i parse-validate (sadece SELECT, whitelist tablolar, CompanyId zorunlu enjekte).
- 🔵 **First Principles:** Asıl ihtiyaç "soru → doğru sayı + zamanında uyarı". LLM araç, kaynak-of-truth değil. Sayı her zaman DB'den.
- 🟢 **Expansionist:** Aynı altyapı ileride: otomatik rapor özeti, e-posta taslağı, müşteri segmentasyonu, talep tahmini.
- ⚪ **Outsider:** "Neden ERP içinde chatbot?" — çünkü ekran gezme yükünü kaldırır; ama gizlilik soran ilk şey olur → yerel model cevabı hazır.
- 🟡 **Executor:** Pazartesi: NuGet `Microsoft.Extensions.AI` + Ollama provider, tek read-only view, "kaç açık sipariş var" sorusu uçtan uca çalışsın (walking skeleton).

## 4. Riskler

| Risk | Etki | Olasılık | Mitigation |
|---|---|---|---|
| Text2SQL zararlı/yanlış SQL üretir | yüksek | orta | Read-only conn + SELECT-only parse + view whitelist + CompanyId zorunlu + EXEC öncesi `SET ROWCOUNT`/timeout |
| LLM halüsinasyon (uydurma sayı) | yüksek | orta | Sayı LLM'den değil SQL'den; LLM sadece SQL üretir + sonucu cümleler |
| Gizlilik — veri dışarı çıkar | yüksek | düşük (yerel) | Yerel Ollama default; bulut sadece açık onayla; PII maskeleme opsiyonu |
| Yerel model zayıf → kötü SQL | orta | orta | Few-shot şema prompt + şema sözlüğü + fallback "anlayamadım" + örnek soru kütüphanesi |
| Alarm yorgunluğu (çok uyarı) | orta | yüksek | Eşik ayarı + önem derecesi + günlük özet + sessizleştirme |
| Hangfire job yükü | düşük | düşük | Gece çalış + batch + ayrı kuyruk |
| Embedding tablosu şişer | düşük | orta | Sadece doküman + kural metni embed; iş verisi embed edilmez (o Text2SQL) |

## 5. Done Criteria

- [ ] Faz 1: "bu ay kaç açık satınalma siparişi var" gibi 10 örnek soru doğru SQL + doğru sayı döndürür
- [ ] Üretilen SQL SELECT-only + whitelist + CompanyId enjekte — guard testleri geçer
- [ ] Faz 2: nakit açığı + min-altı stok + vade gecikme alarmları panelde + e-posta
- [ ] Faz 3: stok min-altı → taslak PO önerisi oluşur, kullanıcı onayıyla gerçek PO'ya döner
- [ ] Provider config'den değişir (Ollama ↔ Azure OpenAI) kod değişmeden
- [ ] `operax-cli migrate` 0 hata
- [ ] `dotnet build` 0 hata 0 uyarı
- [ ] Gizlilik: yerel modda hiçbir dış ağ çağrısı yok (doğrulandı)
- [ ] `docs/MODULE_SPECS/M17_AI.md` yazıldı

## 6. Rollback Planı

- AI katmanı tamamen opsiyonel modül: `Ai:Enabled=false` → asistan menüsü + job'lar kapanır, çekirdek ERP etkilenmez.
- Git revert: faz bazlı commit'ler ayrı → istenen faz revert edilir.
- DB: M17 tabloları bağımsız (FK çekirdeğe sadece okuma referansı). Down script: `DROP TABLE Ai*`.
- Hangfire job'ları `RecurringJob.RemoveIfExists` ile kaldırılır.

## 7. Adımlar / İçerdiği TODO maddeleri

### Faz 1 — Akıllı Sorgu (Text2SQL + RAG)
1. [ ] **AI-1** NuGet + `IAiProvider` soyutlama + `AiProviderFactory` (Ollama/Azure/OpenAI) + DI + appsettings
2. [ ] **AI-2** `schema_M17_AI.sql` — AiConversation/AiMessage/AiQueryLog/AiEmbedding
3. [ ] **AI-3** `SqlGuard` — SELECT-only parse, view whitelist, CompanyId enjeksiyon, timeout/rowcount
4. [ ] **AI-4** Read-only view whitelist + şema sözlüğü (LLM prompt için tablo/kolon açıklamaları)
5. [ ] **AI-5** `Text2SqlService` — şema prompt + few-shot + SQL üret + guard + EXEC + sonucu cümlele
6. [ ] **AI-6** `Features/Assistant/Index` — sohbet ekranı (soru → tablo/grafik + SQL şeffaflığı)
7. [ ] **AI-7** Doküman RAG: kural/yardım metni embed + cosine retriever (opsiyonel ikinci tur)

### Faz 2 — Alarm / Anomali
8. [ ] **AI-8** `schema` — AiAlert/AiAlertRule
9. [ ] **AI-9** `AnomalyScanJob` + `CashForecastJob` (Hangfire recurring)
10. [ ] **AI-10** Eşik kuralları + istatistiksel anomali (z-score / IQR) SP'leri
11. [ ] **AI-11** `_AlertBell` topbar partial + alarm listesi ekranı + e-posta

### Faz 3 — Aksiyon Agent
12. [ ] **AI-12** `schema` — AiActionSuggestion
13. [ ] **AI-13** Öneri üretici (min-altı stok → taslak PO payload) — yazma YOK, öneri kaydı
14. [ ] **AI-14** Onay ekranı: öneriyi gör → onayla → mevcut SP'yi çağır (yetki kontrollü)
15. [ ] **AI-15** Audit log + dokümantasyon `M17_AI.md`

> `docs/TODO.md`'ye AI-1..AI-15 maddeleri eklenecek (plan onayı sonrası).

## 8. İlişkili

- Spec: `docs/MODULE_SPECS/M17_AI.md` (yazılacak)
- Güvenlik: `.claude/rules/security-principles.md` §1 (SQL injection), §7 (ex.Message gizleme)
- SQL: `.claude/rules/sql-conventions.md` §2 (parametreli sorgu)
- Mimari: `.claude/rules/architecture.md` §4 (SQL-first — Text2SQL bunu pekiştirir)
- Önceki plan: `plans/02-m11-finance-create-forms.md` (nakit/vade verisi alarmların kaynağı)
- Konuşma: `docs/journal/2026-05-29.md`

## 9. Onay

- [ ] Plan kullanıcıya gösterildi
- [ ] Geri bildirim alındı
- [ ] Onay alındı: <tarih>
