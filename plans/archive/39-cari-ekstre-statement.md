# Plan 39 — Cari Hesap Ekstresi (Partner Statement)

**Durum:** ✅ TAMAMLANDI (kapsam daraltıldı) 2026-06-22 · Faz 1 + Faz 2a (OPEN tipi). **Faz 2b/3/4 (toplu/gönderim/WhatsApp) İPTAL** — kullanıcı kararı: ekstre GÖNDERİMİ olmayacak; tek-cari Excel indirme + ekranda görüntüleme + PDF (tarayıcı Yazdır→kaydet) yeterli.
**Tier:** 3 (yeni ekran + SP/TVF + Hangfire job + harici entegrasyon — çok fazlı)
**Kaynak:** Web rakip araştırması (reference-researcher, 2026-06-22) + COMPETITOR_ANALYSIS M11.E1 (⚠️→✅). Ba/Bs mutabakat mektubu KAPSAM DIŞI (GİB 2025'te kaldırdı).

---

## 1. Problem

Cari ekstre 5/5 rakipte (Logo·Mikro·Netsis·SAP B1·Odoo + QBO/Xero/Zoho/BC) **table-stakes**. Operax'ta veri katmanı hazır (`tvf_AccountLedger` + `fn_PartnerBalanceAsOf` + cari kartta `_Ekstre` tab) ama: standalone yazdırılabilir sayfa YOK, export YOK, aging band YOK, toplu/otomatik gönderim YOK. Müşteri tedarikçi/müşteriye basılı ekstre gönderemiyor (mutabakat öncesi standart akış).

## 2. Scope

**Dahil (fazlı):** standalone Statement sayfası + devir satırı + ekstre-içi aging band + PDF/Excel export + statement tipi (Tüm/Açık Kalem) + toplu (batch) + zamanlanmış otomatik + çok kanallı (e-posta/WhatsApp) + Öde/QR.

**Hariç:** çoklu döviz ekstre (M11.D1 — Operax'ta FX altyapısı yok, ayrı plan) · GL/kebir (K1) · Ba/Bs (kaldırıldı).

## 3. Rakip kanıtı (web — table-stakes vs differentiator)

- **Table-stakes:** standalone yazdırma + devir · evrak drill-down · PDF/e-posta · statement tipi (QBO 3 tip) · batch (QBO/BC/Odoo).
- **🎯 Differentiator (rakipte eksik):** ekstre-içi aging band (Xero'da YOK → 3rd-party) · zamanlanmış otomatik (QBO/Xero native YOK, en sık şikayet) · WhatsApp + Öde/QR (TR pazarı, 2025 trend).
- Operax avantajı: Hangfire zaten var (batch/scheduled bedava) · SQL-first (müşteriye özel format SP ile) · cari kart-içi tab (Logo donma şikayetine karşı akıcı).

## 4. SQL-First mimari (omurga)

Tek nesne: **`tvf_PartnerStatement(@CompanyId, @PartnerId, @From, @To, @StatementType)`** (veya SP `QueryMultiple`):
- **Devir:** `SUM(Debit-Credit) WHERE MovementDate < @From` → tek "Açılış/Devir" satırı (SARGable).
- **Running balance:** `SUM(Debit-Credit) OVER (ORDER BY MovementDate, Id ROWS UNBOUNDED PRECEDING) + @Opening` (C#'ta döngü YOK).
- **Aging band:** `CASE` ile 0-30/31-60/61-90/90+ kovaları (vade veya hareket tarihi bazlı).
- **Statement tipi:** `'ALL'` tüm hareket · `'OPEN'` açık kalem (kapanmamış evrak — PaymentPlan/eşleşme filtresi).
- C# yalın orkestratör (architecture.md §4).

## 5. Fazlar

### Faz 1 — Çekirdek + aging (table-stakes + en iyi differentiator)
- `tvf_PartnerStatement` (devir + running balance window + aging CASE; `@StatementType='ALL'`).
- `/MasterData/Partners/Statement/{id}?from&to` standalone print-optimize Razor sayfası (firma başlık + cari bilgi + dönem + devir + ledger + kapanış + 4-kutu aging band).
- Print CSS (`@media print` — `wwwroot/css/parts/_print.css`) + "Yazdır" butonu (vanilla `window.print()`).
- Excel/CSV export handler (Dapper sonucu stream — yeni NuGet yok).
- _Ekstre tab'a "Yazdırılabilir Ekstre" linki.
- **Kapanış:** sql-sp-reviewer + code-reviewer + security-reviewer (yeni PageModel) + smoke (devir+running+aging doğru).

### Faz 2 — Statement tipi + toplu (batch)
- ✅ **Faz 2a — `@StatementType='OPEN'` (açık kalem) BİTTİ 2026-06-22.** AccountMovement FIFO ile kapanmamış borçlar (aging FIFO'suyla birebir; PaymentPlan eşleşme tablosu GEREKMEDİ — "kapanmamış = FIFO ile karşılanmamış borç" yaklaşımı). Statement sayfası ALL/OPEN toggle. sql-sp-reviewer 6/6 temiz, smoke tutarlı. Commit (plan:39 Faz 2a).
- ⛔ **Faz 2b — Toplu (batch) BLOKLU:** İlk Hangfire job olur (kaynak job pattern YOK) + çıktının değeri Faz 3 e-postaya bağlı. **Karar gerek:** (1) batch çıktı formatı — CSV ZIP (PDF lib yok) vs QuestPDF NuGet ekle, (2) e-posta altyapısı (aşağı). E-posta gelmeden batch tek başına düşük değer.
- **Kapanış:** sql-sp-reviewer + smoke.

> **❌ Faz 2b/3/4 İPTAL (kullanıcı kararı 2026-06-22):** Ekstre gönderimi (e-posta/WhatsApp) + toplu ZIP **yapılmayacak**. Gerekçe: 1000 cari ZIP indirme dağıtımsız değersiz; gönderim de istenmiyor. Tek-cari ekstre yeterli: **Excel (CSV) indirme + ekranda görüntüleme + PDF (tarayıcı Yazdır→PDF kaydet, print CSS hazır)**. Server-PDF (QuestPDF) gerekmedi — print yeterli. E-posta/Hangfire-job/WhatsApp altyapısı bu plan kapsamından çıktı (ileride ayrı ihtiyaç olursa ayrı plan).

### Faz 3 — Zamanlanmış otomatik gönderim
- Hangfire recurring job + `Partner.StatementSchedule` (aylık/haftalık/kapalı) kolonu.
- E-posta gönderimi (mevcut M16/SMTP altyapısı — DOĞRULANACAK) + AuditLog gönderim izi.
- **Kapanış:** smoke (job tetikleme + iz).

### Faz 4 — Çok kanallı + Öde/QR
- E-posta + WhatsApp Business API (M16 Integration Bridge).
- Ekstre üstünde statik QR (IBAN) → ileride ödeme linki.
- **Kapanış:** entegrasyon smoke.

## 6. Alternatifler (reddedilen)
1. **C#'ta running balance döngüsü** — RED: SQL window function SQL-first + performanslı.
2. **QuestPDF/iTextSharp NuGet (Faz 1)** — RED ilk fazda: yazdır-dostu HTML + tarayıcı print yeterli, bağımlılık eklemeden parite. PDF kütüphanesi Faz 2+ değerlendirilir.
3. **Sadece tab'ı genişlet (standalone yok)** — RED: yazdırma/batch için ayrı route şart (rakip parite).
4. **Çoklu döviz Faz 1'e** — RED: FX altyapısı yok, ayrı plan (M11.D1).

## 7. Riskler
- 🟡 Aging tarih bazı (vade mi hareket mi) netleşmeli — PaymentPlan.DueDate varsa vade, yoksa MovementDate.
- 🟡 Açık-kalem (Faz 2) eşleşme tablosu (plan 18 B16) gerektirebilir — yoksa "kapanmamış = bakiyesi olan evrak" yaklaşımı.
- 🟡 Print CSS tüm tarayıcılarda tutarlı olmalı (smoke: Chrome print preview).
- 🟢 Faz 1 salt-okuma rapor — ledger'a dokunmaz, düşük risk.

## 8. Done Criteria
- [ ] Faz 1: Statement sayfası devir+running balance+aging doğru (smoke), yazdırma temiz, export çalışır. build 0 + reviewer'lar.
- [ ] M11.E1 ⚠️→✅ (COMPETITOR_ANALYSIS güncelle).
- [ ] Her faz ayrı commit (`plan:39`) + faz kapanış kapısı.

## 9. Rollback
Faz bazlı commit. Faz 1 salt-okuma (yeni SP+sayfa) → `dotnet sln`/dosya sil + SP drop. Ledger değişmez.

---

## 5 Lens
- 🔴 **Contrarian:** Fatal flaw — _Ekstre tab zaten var, standalone "gereksiz tekrar" sanılabilir. Değil: yazdırma/batch/otomatik tab içinde olmaz, ayrı route şart (rakip parite + müşteri ekstre gönderimi).
- 🔵 **First Principles:** Doğru soru "müşteri cari ekstresini nasıl PAYLAŞIR" — ekranda görmek değil, basılı/PDF/e-posta göndermek. Sunum+dağıtım eksiği, veri değil.
- 🟢 **Expansionist:** Aging band + scheduled + WhatsApp/QR rakip zayıflığı → Operax farklılaşması (Hangfire+SQL-first bedava avantaj).
- ⚪ **Outsider:** "ERP'de cari ekstre basılamıyor mu?" — ilk bakışta şaşırtıcı eksik; table-stakes.
- 🟡 **Executor:** Pazartesi — Faz 1 `tvf_PartnerStatement` + Statement sayfası (veri hazır, en hızlı görünür değer).
