# Plan 45 — Modül-Bazlı Tam Tamamlama Yol Haritası (Productization)

**Tarih:** 2026-06-22
**Durum:** Onay bekliyor
**Tier:** 3 (meta-plan — her modül kendi alt-planı/fazları)
**Direktif:** EXECUTION-FIRST — yeni modül değil, mevcut modülleri ürünleştir. Bir modülü KOMPLE bitir, sonra sıradaki.

---

## 1. İlke

Her modül **production-ready** ("komple bitti") tanımına ulaşana dek kapatılmaz. Kesişen yarım-fix yerine modül-tam-tamamlama. Bir modül bitince canlıya o modül parçası güvenle alınabilir.

## 2. Modül "Komple Bitti" Tanımı (DoD) — her modüle uygulanır

| # | Boyut | Kriter |
|---|---|---|
| **D1** | Veri doğruluğu | Ledger/stok yazımı atomik + concurrency-safe · reversal simetrik · idempotent · snapshot↔ledger drift yok |
| **D2** | Yaşam döngüsü | Tüm statü geçişleri `sp_ValidateStatusTransition` ile · POSTED immutability (child varsa kilit) · dönem guard |
| **D3** | Tamlık | Her iş olayının post + reverse SP'si var · eksik akış yok (örn. çek iade→cari leg) |
| **D4** | UI | List/Detail/Create ekranları tam · Türkçe (İngilizce kod yok) · validasyon · boş durum · hata geri bildirimi |
| **D5** | Güvenlik | CompanyId izolasyon · IDOR · injection · authz · mass-assignment |
| **D6** | Hata yönetimi | Silent failure yok · SP THROW→Türkçe mesaj · logging |
| **D7** | Test/smoke | Kritik yollar smoke-doğrulandı (E2E) |
| **D8** | Hijyen | Dead code yok · dosya boyutu · magic string yok |

**Modül kapanış kapısı:** her modül için `build-validator` → `code-reviewer` → `sql-sp-reviewer` (SP varsa) → `security-reviewer` (PageModel varsa) → E2E smoke. Sonra modül planı arşive.

## 3. Modül Sırası (foundation → leaf, go-live kritikliği)

| Sıra | Modül | Kapsam | Mevcut durum |
|---|---|---|---|
| **M0** | **Ayarlar/Tanımlar/Sabitler** (ÖNCE) | Sözlük, UDF, Parametre, NumberSeries, StatusTransition, Modüller, Settings, Roller, Kullanıcılar | Altyapı kurulu (admin ekranları + servisler + seed). **Gap'ler (audit 2026-06-22):** Sözlük değer DÜZENLE/SİL yok + tip yönetimi yok (Plan 42 "etiket değiştir" vaadi kırık) · NumberSeries seri-ekle/sil yok (edit-only) · Modüller aktivasyon toggle yok (salt-okuma) · Settings placeholder (handler yok) · StatusTransitions salt-okuma (kasıtlı). UDF/Parametre tam CRUD ✅ |
| **M1** | **Envanter/Stok + Costing** | StockMovement, consume, bin, lot, serial, ItemCost, sayım, transfer | Plan 44 (stok motoru ✅) + costing race ✅ — **en yakın bitmeye**; kalan: OnHandQty drift, PriceVariance, sayım/serial/lot UI, dead production servisleri |
| **M2** | **Master Veri** (Cari/Ürün/Depo/Şube) | Partner+AccountMovement, Item, Warehouse/Bin, Branch | Cari ledger ✅ wired; kalan: ReturnCheque cari leg, UI tamlık, UDF |
| **M3** | **Satınalma Zinciri** | PO → Receiving → PurchaseInvoice → Ödeme | stok/cari wired; kalan: variance, immutability, UI |
| **M4** | **Satış Zinciri** | SO → Shipping → SalesInvoice → Tahsilat | stok ✅ (Plan 44); kalan: invoice/tahsilat tamlık, UI |
| **M5** | **Banka/Kasa** | FinancialAccount, FinancialTransaction, virman, mutabakat | kısmi |
| **M6** | **Çek/Senet** | Cheque/PromissoryNote yaşam döngüsü + cari leg | kısmi (ReturnCheque gap) |
| **M7** | **Kredi** | Loan/LoanPayment, taksit, faiz | kısmi |
| **M8** | **Gider** | ExpenseInvoice, masraf merkezi, dağıtım | kısmi |

**Kapsam dışı (V1 sonrası):** MRP/APS/MES · genel muhasebe (GL/mizan) · Profitability Engine · Logo/Mikro connector (#4) · reverse logistics (#5) — bunlar ayrı roadmap (modül-tamamlama sonrası).

## 4. Her Modül İçin Akış

1. **Audit:** modülü DoD D1-D8'e karşı tara (kod kanıtlı gap listesi) → modül alt-planı (`plans/NN-module-<ad>.md`).
2. **Fix fazları:** gap'leri faz faz kapat (her faz kapanış kapısı).
3. **Modül smoke:** E2E (belge yarat→onayla→ledger doğru→iptal→net 0).
4. **Kapat:** DoD tüm ✅ → modül planı arşive + journal.

## 5. Riskler
- 🟡 Audit false-positive (Cari "beslenmiyor" gibi) → her gap koddan DOĞRULANIR (todo-verification), DOĞRULANMADI etiketi.
- 🟡 Modül sınırları kesişir (stok↔costing↔satış) → ortak altyapı (consume/AccountMovement) tek yerde, modül planı yalnız kendi yüzeyini kapatır.
- 🟢 Stok motoru zaten sertifikalı → M1 hızlı kapanır, DoD pattern'i kalibre eder.

## 6. Onay sorusu
Hangi modülden başlayalım? Öneri: **M1 (Envanter/Stok+Costing)** — en yakın bitmeye, DoD pattern'ini kurar, sonra zincirler. Yoksa iş-görünür bir zincir (M3/M4)?

## 7. İlişkili
- `plans/archive/44-stock-consume-primitive.md` (stok motoru — M1'in çekirdeği)
- `.claude/rules/phase-review-gate.md` · `.claude/rules/document-immutability.md` · `docs/MASTER_ROADMAP.md`
