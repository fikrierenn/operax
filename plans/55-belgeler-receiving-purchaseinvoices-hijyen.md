# Plan 55 — Belgeler hijyen: Receiving badge + PurchaseInvoices split + PO-forecast çift-sayım

**Tarih:** 2026-06-24
**Yazan:** Fikri / Claude
**Durum:** `Onaylandı`
**Modül:** M03 (Purchasing/Belgeler) + M11 (Finance, okuma katmanı)
**Paket:** STARTER

---

## 1. Problem

Belgeler track'ında üç ayrı hijyen borcu var:

1. **Receiving Index badge** standart dışı: durum rozeti `Index.cshtml:137` hardcoded ternary (`Posted?"Tamamlandı":...`) ile basılıyor; `Dict.StatusBadge()` kullanmıyor. Yeni statü (CLOSED_PARTIAL) eklenince UI sessizce eksik kalır. PurchaseInvoices zaten doğru pattern'i (`Dict.StatusBadge`) kullanıyor.
2. **PurchaseInvoices `Details.cshtml.cs` 368 satır** — `csharp-conventions.md` 300-satır soft limitini aşıyor (500 hard-limit altında ama bir sonraki dokunuşta split borcu).
3. **PO-forecast çift-sayım (C-E gap):** `sp_PoPost`→`sp_GeneratePaymentPlanFromPO` PO POSTED'da `SourceDocType='PURCHASE_ORDER'` **tahmini** (forecast/taahhüt) PaymentPlan açıyor. `Finance/PaymentPlan/Index` ekranı bu forecast'i gerçek payable'larla aynı listede/toplamda gösteriyor — sayaç, TotalPayable ve liste şişiyor. Aging TVF zaten `SourceDocType NOT IN ('PURCHASE_ORDER','SALES_ORDER')` ile dışlıyor; ekran 4 sorgusunda bu predikat **yok** (kanıt: `Index.cshtml.cs:45,51,56,62`).

## 2. Scope

### Kapsam dahili
- Receiving Index rozetini `Dict.StatusBadge()`'e çek (Details rozeti zaten doğruysa dokunma).
- `PurchaseInvoices/Details.cshtml.cs` (368) → service layer extraction ile <300 satır.
- `Finance/PaymentPlan/Index.cshtml.cs` 4 sorgusuna aging ile **aynı** `SourceDocType NOT IN ('PURCHASE_ORDER','SALES_ORDER')` yapısal dışlama.
- **Forecast hijyeni (tamamlayıcı):** PO terminal statüye (CLOSED/CLOSED_PARTIAL/CANCELLED) geçerken PURCHASE_ORDER tahmini planlarını CANCELLED'a çek (mevcut PO closure/cancel SP'lerine dar UPDATE).

### Kapsam dışı
- ❌ **Fatura-post'ta PO planı iptali** — danışman reddetti (a5fa08e2): kısmi faturalama (1 PO→N fatura) forecast'i bozar, append-only+idempotent-rebuild felsefesiyle çelişir.
- ❌ **Receiving→Invoice 1:1 kardinalite zorlaması** — danışman (a773e9dff) + kullanıcı kararı: GR=alıcı iç belgesi, VUK "1 irsaliye→1 fatura" SATICI tarafını bağlar. Purchase 1 GR→N kısmi fatura = standart ERP (SAP B1/Odoo/Logo), Plan 28 Faz C doğru, dokunulmaz. VUK kuralı satış tarafına ait → ayrı backlog (task_feb3570d: Shipping→SalesInvoice 1:1).
- ❌ Receiving/PurchaseInvoices tam UI/responsive standardizasyonu (util-renk→token, bespoke header) — ayrı tur.
- ❌ "Sipariş Taahhütleri" ayrı sekme — gereksinim doğrulanmadı; default payable görünümünden çıkarmak yeterli.
- ❌ `sp_PurchaseInvoicePost` finans mantığı (cari besleme/PriceVariance) — doğru, dokunulmaz.

### Etkilenen dosyalar
- `src/Operax.Web/Features/Receiving/Index.cshtml` — rozet (1 blok)
- `src/Operax.Web/Features/PurchaseInvoices/Details.cshtml.cs` — split (368→<300)
- `src/Operax.Web/Features/PurchaseInvoices/PurchaseInvoiceService.cs` — YENİ (extracted SP orkestrasyonu)
- `src/Operax.Web/Features/Finance/PaymentPlan/Index.cshtml.cs` — 4 sorguya filtre
- `docs/sql/db_objects_starter.sql` (veya PO closure SP'nin yaşadığı dosya) — terminal statüde estimate cancel
- `docs/sql/db_objects.sql` migrate listesi — değişen SP fresh-install'da var mı teyit

**Tahmini boyut:** 5 dosya / ~150 satır net değişim (split hariç).

## 3. Alternatifler (C-E gap için — asıl karar)

### A: Fatura-post'ta PO estimate planını iptal et
**Açıklama:** `sp_PurchaseInvoicePost`, faturanın bağlı olduğu PO'nun OPEN PURCHASE_ORDER planlarını CANCELLED yapar.
**Reddetme sebebi:** Kısmi faturalama (1 PO→N fatura) altında ilk faturada tüm forecast iptal olur → kalan açık tutarın nakit projeksiyonu kaybolur. Her okuyucu "iptal oldu mu" varsayımına bağımlı = kırılgan. Danışman (erp-isleyis-danismani) açıkça reddetti.

### B: Sadece kozmetik — hiçbir okuyucu/SP değişmesin, tek bir liste sorgusunu düzelt
**Açıklama:** Yalnız çift göstereni yamala.
**Reddetme sebebi:** Forecast planları sonsuza dek OPEN birikir; başka payable okuyucu eklenince çift-sayım geri gelir. Tutarsız.

### C: Yapısal dışlama (okuma katmanı) + terminal-statü hijyeni — SEÇİLEN
**Açıklama:** PURCHASE_ORDER planı = salt forecast/commitment; cari/payable defteri zaten `AccountMovement` (PO oraya hiç yazmıyor). Her payable okuyucu aging ile aynı `NOT IN ('PURCHASE_ORDER','SALES_ORDER')` predikatını kullanır (single-source + read-time disambiguation). PO terminal statüye geçince estimate CANCELLED (forecast hijyeni). Kısmi faturalama forecast'i = PO açık tutardan canlı türetilir (`OpenOrderAmount` deseni zaten var), PaymentPlan satır-mutasyonu yok.
**Sebep:** Mutasyon-suz = kırılamaz. SAP B1 ("PO accounting'e girmez" + "fatura kopyalanınca PO statü ile kapanır") ve Odoo ("PO journal üretmez") ile birebir. Append-only + idempotent-rebuild uyumlu.

**5 lens kontrolü:**
- 🔴 **Contrarian:** Fatal flaw = bir payable okuyucu predikatı atlarsa çift geri gelir → mitigation: tüm okuyucu envanteri grep'le çıkar, hepsine uygula.
- 🔵 **First Principles:** Yanlış soru = "PO planını ne zaman iptal edeyim". Doğru soru = "PO planı payable mı?" → değil, forecast. Çözüm okuma katmanında.
- 🟢 **Expansionist:** İleride forecast'i kullanıcıya göstermek istenirse ayrı sekme — ama default payable'a karıştırmadan; bu plan o kapıyı kapatmıyor.
- ⚪ **Outsider:** Yabancı "neden aynı tablo hem tahmin hem gerçek tutuyor" derdi → SourceDocType ayrımı bunu açıklıyor, predikat tutarlılığı şart.
- 🟡 **Executor:** Pazartesi = PaymentPlan/Index 4 sorguya predikat ekle, build, smoke (TotalPayable PO hariç).

## 4. Riskler

| Risk | Etki | Olasılık | Mitigation |
|---|---|---|---|
| Başka payable okuyucu predikatı atlar, çift geri gelir | orta | orta | grep ile PaymentPlan okuyan tüm sorguları tara, hepsine uygula |
| PO closure SP'ye eklenen UPDATE transaction'ı bozar | yüksek | düşük | mevcut BEGIN TRAN içine ekle, sql-sp-reviewer; fresh-DB migrate testi |
| Details split davranış kırar | orta | düşük | sadece SP-orkestrasyonunu taşı, mantık aynı; build+smoke |
| Değişen SP migrate listesinde yoksa fresh-install kırık | yüksek | orta | §3.5 fresh-DB migrate ritüeli zorunlu |

## 5. Done Criteria

- [ ] Receiving Index rozeti `Dict.StatusBadge()` kullanıyor; hardcoded ternary yok
- [ ] `PurchaseInvoices/Details.cshtml.cs` < 300 satır, davranış aynı
- [ ] `PaymentPlan/Index` 4 sorgusu PURCHASE_ORDER/SALES_ORDER dışlıyor; TotalPayable PO forecast'i içermiyor
- [ ] PO closure/cancel SP terminal statüde PURCHASE_ORDER estimate'i CANCELLED yapıyor
- [ ] `operax-cli migrate` 0 hata + fresh-DB migrate testi (§3.5) geçti
- [ ] `dotnet build` 0 hata 0 uyarı
- [ ] Smoke: PO POSTED→estimate plan; fatura POSTED→PaymentPlan/Index'te yalnız PURCHASE_INVOICE; PO CLOSED→estimate CANCELLED
- [ ] Faz kapanış kapısı: build-validator + code-reviewer + sql-sp-reviewer + (PaymentPlan PageModel değişti) security gözden geçir

## 6. Rollback Planı

- Git revert: her faz ayrı commit (plan: 55) → `git revert <hash>` temiz.
- SP: `CREATE OR ALTER` idempotent; önceki sürümü migrate ile geri yükle.
- Read-filter değişimi pür C#, DB'ye dokunmaz → revert yeterli.

## 7. Adımlar / Fazlar

1. [x] ✅ **F1** Receiving Index+Details rozet → `Dict.StatusBadge()` · build 0/0 · code-reviewer temiz
2. [x] ✅ **F2** PaymentPlan/Index 4 sorgu + Aging/Details + **sp_AutoClosePayments FIFO** forecast dışlama (gerçek auto-close drift bug'ı yakalandı) · build · smoke (367.700→289.700) · sql-sp-reviewer temiz · fresh-DB 0 fail
3. [ ] **F3** PO closure/cancel SP terminal-statü estimate cancel — ⚠️ sql-sp-reviewer "drift üretmez, forecast hiçbir parasal toplama girmiyor" dedi → **kozmetik hijyen**, backlog'a alınabilir
4. [ ] **F4** PurchaseInvoices Details split → PurchaseInvoiceService · build · code-reviewer
5. [ ] **F5** TODO.md senkron + journal

**Review borcu (kapsam-dışı, gelecek görsel pass):** Receiving Index/Details'te pre-existing Tailwind renk-utility (`text-indigo-600`/`bg-white`/`bg-slate-950/60`) → token. 5 MEDIUM, Plan 55 scope dışı (Belgeler tam görsel standardizasyonu ayrı tur).

> Faz sırası riskli-önce değil, bağımlılık-önce: F2 (asıl bug) erken; F4 (split, davranış-nötr) en sona.

## 8. İlişkili

- Danışman: erp-isleyis-danismani raporu (agentId a5fa08e2) — forecast-only model, (i) reddi
- Domain kuralı: `MEMORY.md` → açık sipariş ledger-dışı; `.claude/rules/document-immutability.md` §1.b (append-only)
- Önceki: plan 54 (PO CLOSED gerçek statü) — terminal statüler buradan
- Journal: `docs/journal/2026-06-24.md` (C-E gap notu)

## 9. Onay

- [ ] Plan kullanıcıya gösterildi
- [ ] Onay alındı: <tarih>
