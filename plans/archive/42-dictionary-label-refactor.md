# Plan 42 — Enum Etiketleri Sözlük-Tabanlı (Tek Kaynak)

**Tarih:** 2026-06-22
**Durum:** ONAYLANDI 2026-06-22 (kullanıcı: tam refactor + yaşam-döngüsü tipleri + POSTED="İşlendi"/APPROVED="Onaylandı")
**Tier:** 3 (yeni servis + seed + ~8 helper emekli + çok view + caching)
**Kaynak:** Kullanıcı tespiti — cari ekstrede İngilizce kod + etiketlerin C#'ta hardcode'u (sözlük varken çift kaynak).

---

## 1. Problem

Enum display etiketleri iki yerde yaşıyor: (1) `DictionaryValue.NameTr` (VT, dropdown'ları besliyor), (2) `UiHelpers` hardcoded `switch` helper'ları (StatusBadge/StatusText/FinanceStatusBadge/LoanMethodLabel/AccountTypeLabel/ItemTypeLabel/SourceDocLabel + AuditActionLabel). Çift kaynak → tutarsızlık (örn. CASH→Kasa hem sözlükte hem C#'ta). Kullanıcı düzeltmesi sözlükte yayılmıyor (kod deploy gerek). Ayrıca bazı kodlar sözlükte hiç yok (`SOURCE_DOC_TYPE` tip yok, `MOV_TYPE` boş, `STATUS` 4/15 eksik).

## 2. Scope

**Dahil:** Merkezi sözlük-etiket okuma servisi (cached, global+şirket çözümü, koda fallback) · eksik sözlük tip/değerlerini seed (SOURCE_DOC_TYPE, MOV_TYPE, STATUS tamamlama, SERIAL/LOT/LPN/PRODUCTION/PICKTASK/BUDGET/RISK/BRANCH/CARD/UDF statü-tipleri Plan 41 kümeleriyle) · `UiHelpers` label helper'larını servise yönlendir (etiket VT'den) · badge metotlarında etiket=VT, **renk/stil kod'da kalır**.

**Hariç:** Badge renk mantığı (semantic CSS sınıfı kod-driven kalır — sözlükte renk tutmak over-engineering) · AuditActionLabel (audit aksiyon vokabüleri ayrı, kullanıcı-görünür değil) · Plan 41 C# `const` sabitleri (kod-içi karşılaştırma kodları — onlar kalır; bu plan yalnız DISPLAY etiketini VT'ye taşır).

## 3. KRİTİK tasarım problemi — ÇÖZÜLDÜ (erp-isleyis-danismani + VT doğrulama)

**KARAR: Yaşam-döngüsü-bazlı sözlük tipi (5 tip), modül-bazlı DEĞİL. Context kolonu GEREKMİYOR.**

Danışman + canlı VT bulgusu: "POSTED bağlam sorunu" büyük ölçüde **yanılsama** — Mal Kabul'ün "Tamamlandı"sı aslında farklı KOD (`COMPLETED`/`RECEIVED`), POSTED değil. Kodlar doğru eşlenince çakışma kalmıyor. Direction (ALACAK/BORÇ) UI'da hep AYRI badge → statü etiketi yön-bağımsız (grep ile doğrulandı: 0 yön-dallı statü switch'i) → **`DictionaryValue.Context` kolonu eklenmez.**

**Yaşam-döngüsü tipleri (modül değil, döngü):**
| Tip | Kod kümesi (Plan 41) | Gerekçe |
|---|---|---|
| `DOC_STATUS` (mevcut STATUS'u netleştir) | DocStatus 15 kod | PO/SO/Receiving/Invoice ortak belge döngüsü (DRAFT→POSTED→CANCELLED + PAID/PARTIAL/CLOSED...) |
| `PRODUCTION_STATUS` (yeni) | ProductionStatus | Zaten ayrı const (Dtos.cs:251) — IN_PROGRESS/COMPLETED |
| `PICKTASK_STATUS` (yeni) | PickTaskStatus | Görev atama döngüsü (ASSIGNED) |
| `CHEQUE_STATUS` (yeni) | ChequeStatus | Finansal araç döngüsü (PORTFOLIO/IN_BANK...) |
| `LOAN_STATUS` (yeni) | LoanStatus | ACTIVE/CLOSED/RESTRUCTURED |
| Context-bağımsız: `SOURCE_DOC_TYPE`/`MOV_TYPE`/`INSTRUMENT_TYPE`/`ACCOUNT_TYPE`/`ITEM_TYPE`/`LOAN_CALC_METHOD`/`CARD_TYPE`/`RISK_CATEGORY`/`BRANCH_TYPE`/`PAYMENT_PLAN_STATUS`/`BUDGET_*`/`PARTNER_TYPE` | Plan 41 kümeleri | Tek etiket, düz taşı |

**Neden saf B (SO_STATUS/RECEIVING_STATUS… per-belge tip) DEĞİL:** 8+ belge × 5 statü = tip enflasyonu + "Taslak" 8 kez tekrar. Yaşam-döngüsü-bazlı (5 tip) = sweet spot. Statünün belge-tipine bağlılığı (Odoo "field model'e ait" prensibi) **`Label(type, code)` çağrısında type argümanıyla** yaşar — çağıran hangi döngüyü gösterdiğini bilir.

**Okuma imzası:** `Label(string type, string code, string? context = null)` — context şimdilik kullanılmaz (gelecekte gerekirse gettext-msgctxt tarzı, geriye-uyumlu eklenebilir). %100 çağrı `Label(type, code)`.

**Standart kanıt:** Odoo (state field model'e ait), SAP B1 (DocStatus belge-tipine bağlı), gettext msgctxt (context fallback). Hiçbiri "tek global enum + ekran override switch" yapmıyor → mevcut C# switch anti-pattern.

### Bağımsız bulgu — DRIFT (acil)
VT `STATUS.POSTED = 'Tamamlandı'` ↔ C# `StatusText` POSTED = `'İşlendi'`. **Şimdiden çelişiyor.** Plan 42 sözlüğü tek-kaynak yapınca tek değere iner → **kullanıcı kararı: POSTED kanonik etiketi ne?** (aşağıda).

## 4. Mimari (SQL-first uyumlu)

- **`IDictionaryLabels` servisi** (`Lib/DictionaryLabels.cs`): açılışta/ilk erişimde `DictionaryValue` (TypeCode, Code, NameTr, NameEn, CompanyId) tek sorgu → `Dictionary<(string type,string code), string>` cache. Şirket-özel satır global'i ezer. `Label(typeCode, code)` → NameTr, yoksa `SourceDocLabel`-tarzı koda fallback (asla boş). Sözlük admin edit'inde cache invalidation (versiyon damgası / `IMemoryCache` TTL 5dk).
- **Helper'lar servise delege:** `UiHelpers.SourceDocLabel(code)` → `labels.Label("SOURCE_DOC_TYPE", code)`. (UiHelpers static; servis DI → ya helper'ları extension/inject et ya da view'da `@inject IDictionaryLabels`.) **Karar:** view'larda `@inject`, badge'lerde etiket parametre olarak geçilir.
- **Badge:** `StatusBadge(code, label)` imzası — renk koddan, label çağrandan (sözlükten). Geriye-uyum: label null ise StatusText fallback.

## 5. Alternatifler (reddedilen)
1. **Her render'da DictionaryValue JOIN (SP'de):** RED — her ekran SP'si join şişer, cache daha ucuz. (Statement SP'de join cazip ama tutarsız olur — bazı ekran SP yok.)
2. **Hepsini canonical, badge'leri de sözlükte renk kolonu:** RED — renk sunum, veri değil; CSS sınıfı kod'da kalmalı (ui-standard).
3. **Hiç dokunma (hardcode kalsın):** RED — kullanıcı tam refactor seçti.

## 6. Riskler
| Risk | Etki | Mitigasyon |
|---|---|---|
| Bağlama bağlı etiket kaybı (POSTED) | orta | Seçenek C: context-bağımlı olanlar override; gerisi canonical |
| Cache bayatlama (admin edit sonrası) | düşük | TTL 5dk veya edit'te invalidation |
| Eksik seed → boş etiket | orta | Koda fallback (kod göster, asla boş) + seed eksiksizlik testi |
| Şirket/global çözüm yanlış | orta | Şirket satır > global; ikisi de yoksa kod |
| StatusBadge 14 çağrı imza değişimi | orta | Aşamalı: eski imza korunur (overload), label opsiyonel |

## 7. Done Criteria
- [x] `IDictionaryLabels` servisi + DI + cache + fallback (DictionaryLabels.cs, IMemoryCache 5dk, şirket→global→kod)
- [x] Seed: 22 tip ~140 değer (SOURCE_DOC_TYPE/MOV_TYPE/STATUS 4→13/INSTRUMENT/CHEQUE/LOAN/PRODUCTION/PICKTASK/... idempotent global)
- [x] UiHelpers label helper'ları EMEKLİ (StatusBadge/StatusText/FinanceStatusBadge/LoanMethodLabel/AccountTypeLabel/ItemTypeLabel/SourceDocLabel silindi) — çift kaynak bitti
- [x] Badge: etiket VT (Dict.StatusBadge), renk kod (UiHelpers.BadgeClass)
- [x] build 0/0 (Web+Cli) + code-reviewer (0 HIGH/CRITICAL) + smoke (İşlem kolonu "Alış Faturası", badge textContent "İşlendi" title-case = sözlük)
- [x] DRIFT kapandı: STATUS.POSTED "Tamamlandı"→"İşlendi", APPROVED→"Onaylandı"
- [x] Inline finans badge switch'leri de sözlüğe (Cheques/PaymentPlan/Loans/Accounts/Aging/Snapshot/SalesInvoices)
- [ ] (kalan, düşük) Sözlük admin edit→ekran yansıma cache TTL testi yapılmadı (mekanizma sağlam) · EBelgeStatus (ayrı domain, seed yok) + LoanPayment hesaplanmış statü = kapsam dışı bırakıldı

## 8. Adımlar
1. Seed eksik tip/değerler (VT) — SQL idempotent.
2. `DictionaryLabels` servisi + DI + cache.
3. `SourceDocLabel` (yeni, 4 çağrı) servise → pilot doğrula.
4. Diğer helper'lar (Account/Item/Loan/FinanceStatus/Status) sırayla → çağrı yerleri.
5. Badge imza overload (label opsiyonel).
6. build + review + smoke + cache-invalidation testi.

## 9. 5 Lens
- 🔴 **Contrarian:** Fatal flaw = bağlama bağlı etiket (POSTED). C ile çözülür yoksa nüans kaybı.
- 🔵 **First Principles:** Etiket = veri (kullanıcı düzeltebilmeli) → VT. Renk = sunum → kod. Ayrım net.
- 🟢 **Expansionist:** Tek kaynak → çok-dil (NameEn zaten var) + müşteriye-özel etiket bedava.
- ⚪ **Outsider:** "Neden Kasa iki yerde yazılı?" — bu refactor onu bitirir.
- 🟡 **Executor:** Pazartesi — seed + servis + SourceDocLabel pilot, sonra dalga dalga helper.

## 10. İlişkili
- `.claude/rules/architecture.md` §8 (UDF/sözlük) · `.claude/rules/turkish-ui.md` (UI Türkçe) · `plans/archive/41-status-code-constants.md` (kod sabitleri — bu plan onların ETİKETİNİ VT'ye taşır, kodları değil).
