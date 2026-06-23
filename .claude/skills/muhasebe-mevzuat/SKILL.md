---
name: muhasebe-mevzuat
description: >
  Operax finans/muhasebe özelliği (çek/senet, cari hesap, banka/kasa, gider, GL
  muhasebeleştirme, mizan, dönem) yazılırken TÜRK MUHASEBE MEVZUATI doğrulama rehberi —
  TDHP (Tek Düzen Hesap Planı) hesap işleyişi (borç/alacak yönü), çek/senet muhasebe
  kaydı (101 Alınan Çekler / 103 Verilen Çekler, alış-anı cari kapama, karşılıksız iade),
  cari hesap (120 Alıcılar / 320 Satıcılar), VUK değerleme, şüpheli alacak (128/138).
  SALT-REHBER + ONLINE-DOĞRULA: kesin hesap kodu/oran/özel durum gerektiğinde WebSearch ile
  yetkili kaynaktan (vergidosyasi, muhasebetr, GİB, mevzuat.gov.tr) teyit edip KAYNAK belirtir.
  "muhasebe kaydı", "hesap planı", "TDHP", "borç alacak hangi yön", "çek muhasebe", "karşılıksız
  çek", "cari kapama", "şüpheli alacak", "yevmiye fişi", "mizan", "muhasebeleştirme", "hesap kodu"
  denildiğinde veya M5/M6/M7/M8/M11 finans + GL özelliği yazarken çağrılır.
allowed-tools: Read, Grep, Glob, Bash, WebSearch, WebFetch
user-invocable: true
model: inherit
---

# Muhasebe Mevzuatı (Türk — TDHP/VUK) Rehberi

> **SALT-REHBER.** Mevzuatı koda gömmez — yazmadan önce hangi hesabın hangi yöne (borç/alacak)
> ve hangi ANDA hareket etmesi gerektiğini söyler. **Emin değilse ONLINE doğrular** (WebSearch
> yetkili kaynak) + kaynak belirtir. Operax SQL-First: kural SP'de yaşar, bu skill kuralın
> muhasebe-doğruluğunu garanti eder.

## Ne zaman tetiklenir
M5 Banka/Kasa · M6 Çek/Senet · M7 Kredi · M8 Gider · M11 Finans · GL muhasebeleştirme özelliği
yazarken/denetlerken. Bir hesabın yönü/anı belirsizse → önce buraya bak, gerekirse online doğrula.

## Tamamlayıcılık (footprint-ladder — dup değil)
| Skill | Bakış |
|---|---|
| **muhasebe-mevzuat** (bu) | Muhasebe-kaydı doğruluğu (TDHP hesap/yön/an, çek-senet kaydı) |
| `mali-evrak-mevzuat` | Evrak/e-belge mevzuatı (fatura/irsaliye VUK tarih, e-Fatura/iade) |
| `mali-islem-akislari` | Operasyonel akış (mutabakat/varyans/kapanış işleyişi) |
| `erp-isleyis-danismani` | Statü/finansal-araç modelleme kararı |

## ONLINE-DOĞRULA disiplini (ZORUNLU)
- Kesin hesap kodu, KDV/tevkifat oranı, tebliğ no, özel durum (örn. dövizli çek değerleme, reeskont)
  → **WebSearch** yetkili kaynak (vergidosyasi.com, muhasebetr.com, alomaliye.com, GİB, mevzuat.gov.tr).
- Bulguyu **kaynak linkiyle** raporla; ezbere hesap kodu yazma. Şüphe = ara, uydurma.
- Mevzuat değişebilir (oran/tebliğ) → tarih-duyarlı; "2026 itibarıyla" diye not düş.

---

## 1. TDHP Çekirdek Hesaplar (Operax kapsamı)

| Kod | Hesap | Karakter | Operax karşılığı |
|---|---|---|---|
| 100 | Kasa | Aktif (borç) | FinancialTransaction (Kasa hesabı) |
| 101 | Alınan Çekler | Aktif (borç) | `Cheque` Direction=Received, Status=PORTFOLIO |
| 102 | Bankalar | Aktif (borç) | FinancialTransaction (Banka hesabı) |
| 103 | Verilen Çekler ve Ödeme Emirleri (-) | Pasif düzenleyici | `Cheque` Direction=Issued |
| 120 | Alıcılar | Aktif (borç) | `AccountMovement` (müşteri cari, Borç bakiye) |
| 121 | Alacak Senetleri | Aktif (borç) | `PromissoryNote` Direction=Received |
| 128/138 | Şüpheli Ticari/Diğer Alacaklar | Aktif | (yasal takip — gelecek) |
| 153 | Ticari Mallar | Aktif | StockMovement / ItemCost |
| 320 | Satıcılar | Pasif (alacak) | `AccountMovement` (tedarikçi cari, Alacak bakiye) |
| 321 | Borç Senetleri | Pasif | `PromissoryNote` Direction=Issued |
| 191/391 | İndirilecek/Hesaplanan KDV | Aktif/Pasif | fatura KDV |
| 600/610 | Yurtiçi Satışlar / İade | Gelir | SalesInvoice |
| 770 | Genel Yönetim Gideri | Gider | ExpenseInvoice + CostCenter |

**Yön kuralı:** Aktif hesap artışı=Borç, azalışı=Alacak. Pasif tam tersi. Operax `AccountMovement`
bakiye = `SUM(Borç − Alacak)`; cari müşteri borç-bakiye (120), tedarikçi alacak-bakiye (320).

---

## 2. Çek/Senet Muhasebe İşleyişi (DOĞRULANMIŞ — 2026-06-23, kaynaklı)

**Alınan çek (müşteri çeki) — 3 an:**

| An | Kayıt (TDHP) | Operax |
|---|---|---|
| **Çek ALINDI** (portföye) | Borç **101** Alınan Çekler / Alacak **120** Alıcılar → **cari KAPANIR** | Cheque PORTFOLIO + **AccountMovement Credit** (partner alacaklanır) |
| **TAHSİL** (banka ödedi) | Borç **102** Bankalar / Alacak **101** → **cari'ye DOKUNMA** | FinancialTransaction INCOME + Cheque COLLECTED; **AccountMovement YAZMA** |
| **KARŞILIKSIZ / iade** | Borç **120** Alıcılar / Alacak **101** = **ters kayıt, cari geri açılır (iade)** + banka masrafı alıcıya | Cheque RETURNED + **AccountMovement Debit** (iade) + masraf |

- Yasal takibe geçilirse: 101 → **128** Şüpheli Ticari Alacaklar (esas faaliyet) / **138** (diğer).
- **Verilen çek** simetrik: verildiği an Borç **320** Satıcılar / Alacak **103** → payable cari kapanır.
- **Kritik hata deseni:** cari kapamayı TAHSİL anına bağlamak YANLIŞ — alış anına bağlanır (çek vadesi
  boyunca cari yanlış borçlu görünür, aging/risk şişer). Operax kodu bu hatayı taşıyordu (Plan 50 düzeltir).

**Senet (alacak/borç senedi):** 121/321; çek ile aynı mantık (alış-anı cari kapama).

**Kaynaklar:** [vergidosyasi — 101 Alınan Çekler](https://vergidosyasi.com/2017/11/14/101-alinan-cekler-hesabi-niteligi-isleyisi-ve-ornek-muhasebe-kayitlari/) · [muhasebetr — Çekler ve Karşılıksız Çekler](https://www.muhasebetr.com/yazarlarimiz/cumhurcetin/021/) · [muhasebedersleri — 101 işleyişi](https://www.muhasebedersleri.com/hesaplar/101-alinan-cekler.html)

---

## 3. Operax Eşleme + Disiplin

- **AccountMovement = cari alt-defter** (120/320). Çek portföyü = `Cheque` tablosu (101/103). GL fişi
  (yevmiye) **henüz yok** — periyodik GL muhasebeleştirme ayrı modül (subledger→GL posting-rule).
- Ledger append-only (`document-immutability.md`): düzeltme = ters kayıt (REVERSAL), silme yok.
- Dönem kilidi: her muhasebe hareketi `sp_GuardPeriodOpen` (LOCKED dönem = e-Defter berat → THROW).
- **Kod yazmadan önce:** hangi hesap, hangi yön, hangi an → 1-2 cümle + belirsizse online doğrula + kaynak.

## İlişkili
- `.claude/skills/mali-evrak-mevzuat/SKILL.md` — evrak/e-belge VUK (tamamlayıcı)
- `.claude/skills/mali-islem-akislari/SKILL.md` — operasyonel mutabakat/varyans/kapanış
- `.claude/rules/document-immutability.md` — ledger append-only + dönem kilidi
- `docs/reference/MIKRO_V16_ANALYSIS.md` §3.5 — posting-rule deseni (GL muhasebeleştirme)
