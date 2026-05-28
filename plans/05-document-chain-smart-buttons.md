# Plan 05 — Belge Zinciri Akışı (Odoo-style Smart Buttons)

**Tarih:** 2026-05-29
**Yazan:** Claude
**Durum:** `Taslak`
**Modül:** M03 + M04 + M11 (cross-cutting)
**Paket:** STARTER

---

## 1. Problem

Odoo'da olduğu gibi kullanıcı bir belgeden sonraki belgeyi tek tıkla, ön-dolu olarak oluşturabilmeli: Sipariş → Mal Kabul → Fatura → Ödeme zinciri. Operax'ta şu an her belge ayrı ayrı, elle, baştan oluşturuluyor — kaynak belgeden veri kopyalanmıyor, belgeler arası gezinme (smart button sayaç) yok. Bu hem yavaş hem hataya açık (kullanıcı PO satırlarını mal kabulde tekrar giriyor).

## 2. Scope

### Kapsam dahili
- **Akış butonları (downstream create):**
  - PO (POSTED) → Mal Kabul Oluştur
  - Receiving (POSTED) → Alış Faturası Oluştur
  - SO (POSTED) → Sevkiyat Oluştur
  - ExpenseInvoice/SalesInvoice (POSTED) → Ödeme/Tahsilat
- **Smart button sayaçları:** her belgede bağlı alt belge sayısı + tıkla-git
- **Ön-dolu create:** kaynak belge başlık + satırları hedefe kopyalanır (kalan miktarla)
- **Yetki kontrolü:** buton sadece yetkili rol görür
- **Ortak partial:** `_DocFlowButtons.cshtml`

### Kapsam dışı
- Production zinciri (M10 — zaten kendi akışı var)
- Transfer/CycleCount (zincir parçası değil)
- Kısmi miktar seçim diyalogu (ilk sürüm: tüm kalan miktar)

### Etkilenen dosyalar
- `docs/sql/db_objects_starter.sql` — sp_CreateReceivingFromPO, sp_CreateExpenseInvoiceFromReceiving, sp_CreateShippingFromSO
- `docs/sql/schema_*` — ExpenseInvoice.ReceivingId kolonu (yoksa)
- `src/Operax.Web/Features/Shared/_DocFlowButtons.cshtml` — yeni partial
- `src/Operax.Web/Lib/UiVms.cs` — DocFlowVm
- PO/SO/Receiving/Shipping/Invoice Details — partial + handler (6 dosya)
- `src/Operax.Web/Lib/Auth.cs` — yetki helper (HasRole)

**Tahmini boyut:** ~12 dosya / ~1200 satır.

## 3. Alternatifler

### A: Her belgede tam manuel (mevcut)
**Reddetme:** Yavaş, hataya açık, Odoo'nun gerisinde.

### B: Otomatik zincir (PO POSTED → Receiving otomatik)
**Reddetme:** Kontrolsüz; kullanıcı her mal kabulü onaylamak ister, otomatik istenmez.

### C: ✅ Smart button + ön-dolu DRAFT (seçilen)
**Açıklama:** Kullanıcı butona basar, hedef belge DRAFT + ön-dolu açılır, gözden geçirip onaylar.
**Sebep:** Odoo pattern'i — pratiklik + kontrol dengesi. Yetki ile sınırlı.

**5 lens:**
- 🔴 Contrarian: Kısmi mal kabul? İlk sürüm tüm kalan miktar, sonra kısmi seçim.
- 🔵 First Principles: Kullanıcı "aynı veriyi 2 kez girmek istemiyorum" — kaynak kopyala çekirdek.
- 🟢 Expansionist: Tüm zincire genişlet (PO→Recv→Inv→Pay tam) — evet, kapsam bu.
- ⚪ Outsider: Smart button sayacı yoksa kullanıcı "bu PO'dan kaç mal kabul oldu" bilemez — sayaç şart.
- 🟡 Executor: Pazartesi: sp_CreateReceivingFromPO (en çok kullanılan zincir).

## 4. Riskler

| Risk | Etki | Olasılık | Mitigation |
|---|---|---|---|
| Kısmi miktar karmaşası | Orta | Yüksek | İlk sürüm tüm kalan; QtyReceived < QtyOrdered farkı |
| Çift belge oluşturma | Yüksek | Orta | Smart sayaç + "zaten N belge var" uyarısı |
| Yetki bypass | Yüksek | Düşük | Hem UI (@if rol) hem handler ([Authorize]) |
| 6 belge tek planda dağılır | Orta | Yüksek | Zincir başına commit (Satınalma → Satış → Ödeme) |

## 5. Done Criteria

- [ ] PO Details'te "Mal Kabul Oluştur" (POSTED + yetki) → ön-dolu Receiving DRAFT açılır
- [ ] Receiving Details'te "Alış Faturası" → ön-dolu ExpenseInvoice
- [ ] SO Details'te "Sevkiyat Oluştur" → ön-dolu Shipping DRAFT
- [ ] Invoice'larda "Ödeme/Tahsilat" → Payment formu cari+tutar ön-dolu
- [ ] Her belgede smart sayaç (bağlı alt belge + tıkla-git)
- [ ] Buton yetkisiz role görünmez
- [ ] `operax-cli migrate` 0 hata
- [ ] Smoke: PO aç→onayla→[Mal Kabul Oluştur]→satırlar dolu→onayla→[Fatura]→ödeme
- [ ] Plan arşive

## 6. Rollback
- Git: zincir başına commit
- DB: yeni SP CREATE OR ALTER; ExpenseInvoice.ReceivingId nullable kolon (geri uyumlu)

## 7. Adımlar

### Faz 1 — Altyapı
1. [ ] `_DocFlowButtons.cshtml` partial + DocFlowVm
2. [ ] Auth.cs HasRole helper
3. [ ] ExpenseInvoice.ReceivingId kolonu (yoksa)
4. [ ] Commit: feat: belge akış altyapısı (plan: 05)

### Faz 2 — Satınalma Zinciri
1. [ ] sp_CreateReceivingFromPO (PO satır → ReceivingLine, kalan miktar)
2. [ ] sp_CreateExpenseInvoiceFromReceiving
3. [ ] PO Details: "Mal Kabul Oluştur" + smart sayaç
4. [ ] Receiving Details: "Alış Faturası" + smart sayaç
5. [ ] Commit: feat(M03): satınalma zinciri smart button (plan: 05)

### Faz 3 — Satış Zinciri
1. [ ] sp_CreateShippingFromSO
2. [ ] SO Details: "Sevkiyat Oluştur" + sayaç
3. [ ] Shipping Details: "Fatura" (manuel, otomatik yanında) + sayaç
4. [ ] Commit: feat(M04): satış zinciri smart button (plan: 05)

### Faz 4 — Ödeme Zinciri
1. [ ] ExpenseInvoice Details: "Ödeme Yap" → Payment ön-dolu (PAYABLE)
2. [ ] SalesInvoice Details: "Tahsilat Al" → Payment ön-dolu (RECEIVABLE)
3. [ ] Payment formu source param ile ön-dolu (Plan 02 F5'e bağlı)
4. [ ] Commit: feat(M11): ödeme zinciri smart button (plan: 05)

### Faz 5 — Test + cleanup
1. [ ] E2E zincir testi
2. [ ] docs/TODO.md + arşiv

## 8. İlişkili
- `docs/GAP_DETAIL.md` — Plan 02 (Payment formu önkoşul)
- `.claude/rules/document-immutability.md` — zincir + kilit kuralı
- Bağımlılık: Plan 02 Faz 5 (Payment Create) önce bitmeli (ödeme zinciri için)

## 9. Onay
- [ ] Plan gösterildi
- [ ] Onay alındı
