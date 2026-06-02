---
name: ux-design-patterns
description: Operax ekranlarını KANITLI UX pattern'leriyle tasarla/revize et (NNGroup, Baymard, SAP Fiori, Smashing). Data grid, form, combobox/typeahead, inline validasyon, klavye akışı, görsel hiyerarşi, boş durum. "kullanıcı dostu yap", "ekran tasarla/revize et", "ux pattern", "data grid", "form tasarımı", "combobox" denildiğinde danış. screen-ux-standard (etkileşim/akış) ile tamamlayıcı; bu skill = kanıtlı tasarım kuralları.
allowed-tools: Read, Grep, Glob, Edit, Bash
user-invocable: true
model: inherit
---

# Operax UX Tasarım Pattern'leri (Kanıtlı)

Kaynak: NNGroup, Baymard, Smashing, SAP Fiori, Pencil&Paper. `screen-ux-standard.md` akış/otomatik-doldurmayı tanımlar; bu skill **somut tasarım kuralları** (tablo/form/combobox/validasyon/hiyerarşi). Detay rapor: `docs/UX_RESEARCH_2026-06-03.md`.

## 1. Satır Girişi — Klavye (EN YÜKSEK ETKİ)
- **Tab/Enter akışı:** satır tablosunda `Tab`=sonraki alan; son alandan `Tab`/`Enter`=yeni satır + odak ilk alana (Ürün). `Esc`=iptal. Excel hissi. [NNGroup, Epicor]
- **Sticky başlık:** uzun satır tablosunda `thead { position:sticky; top:0 }`. [Stanford]
- **Akıllı varsayılan/miras:** yeni satır depo/KDV/para birimini önceki satırdan miras alır; PO'dan miktar/birim/fiyat otomatik dolar (override edilebilir). [NNGroup]
- **Inline edit ≤2 alan, modal/drawer ≥3 alan** veya yan etkili/riskli aksiyon. Referans tabloya bakılacaksa modal değil **drawer**. [Pencil&Paper]

## 2. Data Grid / Liste
- **Hizalama:** metin SOLA, sayı SAĞA + mono/tabular rakam. Başlık içerikle aynı hiza. [Pencil&Paper]
- **Sıralama:** her sütun başlığında chevron; varsayılan en yeni/öncelikli.
- **Hızlı filtre chip** anlık (TASLAK/ONAYLI, Bugün/Hafta); **gelişmiş filtre** (tarih aralığı) Apply ister. Aktif filtre badge + "Temizle".
- **Sayfalama** (sonsuz kaydırma YASAK ERP listede): varsayılan 25 satır, "Sayfa 3/12" konum. [Pencil&Paper]
- **Satır aksiyon:** ≤2 ikon satır sonunda; ≥3 → "…" menü. Hover'da satır vurgu + cursor pointer (tıklanabilir ima).
- **Yoğunluk:** depo/muhasebe maksimum veri ister → **Compact (40px) varsayılan**.
- **Boş durum 3 parça:** ikon + "neden boş" + CTA ("İlk X'i oluştur"). Salt "Kayıt yok" YASAK. [NNGroup]
- **Skeleton** yükleme (spinner değil); spinner sadece kısa submit. [NNGroup]

## 3. Form
- **Tek kolon akış** (modal/detay): tek kolon çok-kolondan ~15sn hızlı. İstisna: Şehir+İlçe, Başlangıç+Bitiş yan yana. [CXL]
- **Etiket ÜSTTE** (sol-etiket %50 yavaş). [NNGroup] — Operax `form-label` zaten üstte.
- **Placeholder ≠ açıklama** (yazınca kaybolur); açıklama `form-hint`. [NNGroup]
- **Zorunlu `*` + opsiyonele "(opsiyonel)"** — ikisi birden işaretli.
- **Alan genişliği içerikle eşleşsin** (TCKN dar, açıklama tam genişlik).
- **Gruplar 3-5 alan**, başlık+boşlukla ayır; 10+ ilgisiz alan → sekme/accordion. Gelişmiş alanlar "Gelişmiş Seçenekler" toggle (staged disclosure). [NNGroup]

## 4. Inline Validasyon — "Reward Early, Punish Late"
- Alan **önceden hatalıysa** → düzeltmeye başlayınca hatayı ANLIK sil. [Smashing/Baymard]
- Alan **önceden doğruysa** → yeni hatayı **blur**'a kadar bekle (yazarken kesme).
- Boş zorunlu alan hatası **yalnızca submit'te** (odak alınca/yazarken değil).
- Hata = border + renk + **ikon + metin** (renk tek başına yetmez); "neden + nasıl düzelt".
- API kontrolü (kod tekrarı) **300-500ms debounce**. Kritik alanda (IBAN/VKN) doğruysa **yeşil ✓**.

## 5. Combobox / Typeahead (uzun ürün/cari listesi)
- **Odakta hemen aç** + son/sık kullanılanları önceden göster (0 karakterde bile). [Smart Interface]
- **Maks ~10 öneri**; fazlası filtrele. Türkçe fuzzy (ı/i, ğ/g) + ortadan eşleşme. [Baymard]
- **Klavye:** ↑↓ gez, Enter seç, Esc kapat, Tab uygula. ARIA `role=combobox`/`aria-expanded`.
- **Liste sonunda "+ '<metin>' oluştur"** — ürün/cari anında aç (create-inline). [Mobbin]
- **1000+ kayıt:** debounce + sanallaştırma (yalnız görünür satır DOM).

## 6. Navigasyon
- **Breadcrumb 3+ seviyede zorunlu** (hiyerarşi, ziyaret geçmişi değil; son öğe tıklanamaz). [NNGroup]
- **Sidebar = hangi modül; breadcrumb = hangi sayfa.** Karıştırma.
- **Command palette (Ctrl+K)** güç kullanıcı: global arama+komut, hint görünür, son kullanılanlar. [GitHub/Medium]
- **Kısayolu tooltip'te göster** (gizli kısayol öğretmez). [NNGroup]
- **Fiori floorplan:** Liste Raporu (filtre barı+tablo) → tıkla → Object Page (başlık + sekmeler: Satırlar/Belgeler/Denetim İzi). [SAP Fiori]

## 7. Görsel Hiyerarşi (SAP Fiori)
- **Ekran başına TEK birincil görev**; ikincil aksiyon görsel altta/gizli.
- **Sayfada tek `btn-primary`** (dolu/marka); gerisi `btn-secondary`/text-link. Onayla > Taslak Kaydet > İptal hiyerarşisi.
- **Vurgu = gereksizi çıkar** (her şeyi vurgulamak = hiçbir şeyi vurgulamak).
- **Semantik renk tutarlı:** success=onaylı, warn=taslak, danger=iptal, info=bilgi; renk+ikon/metin birlikte (renk tek başına bilgi taşımaz).
- **F-pattern:** en sık taranan (belge no/tarih/cari) en solda; durum badge sağda görünür.
- **Sekonder bilgi bağlam kaybetmeden:** tooltip/expandable row/drawer.

## 8. Operax Uygulama Önceliği
1. 🔴 Satır girişi Tab/Enter klavye (§1) — çok yüksek etki
2. 🔴 Inline validasyon reward-early/punish-late (§4)
3. 🔴 Boş durum 3 parça `_EmptyState` (§2) + sticky başlık (§1)
4. 🟡 Combobox son-kullanılan+oluştur (§5)
5. 🟡 Yoğunluk Compact + skeleton (§2)
6. 🟢 Command palette Ctrl+K (§6), staged disclosure (§3)

Her ekran ayrı commit; veri-giriş ekranları (Sipariş/Mal Kabul/Fatura) önce. Faz sonu `phase-review-gate.md`.
