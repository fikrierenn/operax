# Danışman Skill'lere Danışma Kuralı (ZORUNLU)

Operax'ta belirli iş türlerine dokunmadan **ÖNCE** ilgili **danışman (advisor) skill**'e danışılır — kod/ekran/SP yazmadan, hangi mevzuat/pattern/iş-kuralı geçerli netleşsin diye. Bu skill'ler **SALT-REHBER**: kendileri kod yazmaz, doğrulanacak noktaları + kaynakları verir. `paths:` yok — compact sonrası da geçerli.

## Temel İlke

> **"Kod/ekran doğru görünüyor" yetmez.** İş, aşağıdaki bir alana giriyorsa, üretmeden önce eşleşen skill'e danışılır; danışılmadan yazılan finans/ekran/domain işi **eksik kabul edilir**.

## Danışman Skill Kataloğu (iş türü → skill, kod-ÖNCESİ danış)

| İş türü / tetik | Skill | Detay kuralı |
|---|---|---|
| Muhasebe kaydı, TDHP hesap kodu, borç/alacak yönü, çek/senet muhasebe, cari kapama, şüpheli alacak, yevmiye/mizan (GL · M5-M8/M11) | **`muhasebe-mevzuat`** | `coding-discipline.md §5` |
| İade faturası, e-Belge senaryo, fatura iptal/düzelt, VUK tarih kuralı, irsaliye↔fatura, tevkifat, KDV iade (M03/M04/M11/e-Belge) | **`mali-evrak-mevzuat`** | `coding-discipline.md §5` |
| Mutabakat (GL↔subledger/banka/cari), varyans analizi, yevmiye doğruluğu, dönem kapanışı (M11/M02) | **`mali-islem-akislari`** | `coding-discipline.md §5` |
| Ekran etkileşim/akış: form akışı, otomatik doldurma, klavye, boş durum, hata geri bildirimi, satır girişi | **`screen-ux-standard`** | `ui-standard.md §8` |
| Kanıtlı UI deseni: data grid, form, combobox/typeahead, inline validasyon, görsel hiyerarşi (NNGroup/Baymard/Fiori) | **`ux-design-patterns`** | `ui-standard.md §8` |
| Mobil-first responsive: header stack, kart-grid collapse, geniş-tablo scroll, KPI/form grid breakpoint | **`tailwind-responsive`** | `ui-standard.md §8` + §1/§2 |
| Yeni feature/modül/evrak tasarımı → rakip parite + farklılaşma (Logo/Mikro/Netsis/SAP B1/Odoo) | **`competitor-analyst`** | yeni modül planı öncesi |
| Modül gap/fazla-eksik denetim (endüstri-standart checklist) | **`operax-erp-wms-auditor`** | "gap analizi", "modül denetimi" |
| Yerel LLM çağrısı (LLamaSharp, GGUF, süreç-içi inference) | **`local-llm-integration`** | `Llama.` namespace yazarken |
| Kod yazarken/düzenlerken yaygın hata önleme (yazım sırasında) | **`code-quality-checklist`** | her kod dokunuşunda |
| Yüksek-belirsizlik + yüksek-maliyet karar (mimari/yön) | **`llm-council`** | "council this", "pressure-test" |

### Modelleme kararı (skill değil, AJAN)
Statü kümesi / finansal araç tipi / evrak zinciri / yaşam-döngüsü **modelleme** kararı → `erp-isleyis-danismani` ajanı (salt-okuma domain danışman). Kod-uyum review → `code-reviewer`, güvenlik → `security-reviewer`, SP iş-doğruluğu → `sql-sp-reviewer`.

## Eylem (DOĞRUDAN çalıştırılır — danışman DEĞİL)

Bunlar "danış" değil "yap" skill'leri; tetiklenince işi üretir: `demo-veri-uret` · `referans-tanim-seed` · `sql-migration-writer` · `yetenek-uret` · `impl-spec` · `plan-tracker` · `session-handoff` · `presentation-builder`/`bkm-sunum`.

## Kural

1. İş bir danışman alanına giriyorsa → **önce skill, sonra kod/ekran.**
2. Danışman skill yoksa ve tekrarlayan ihtiyaçsa → `yetenek-uret` ile üret (footprint-ladder).
3. Skill çıktısı rehberdir; mevzuatı/pattern'i **dayatmaz** — kararı sen verirsin ama gerekçeyle.
4. Yeni skill üretilince bu kataloğa satır ekle.

## İlişkili
- `.claude/rules/coding-discipline.md §5` — finans/muhasebe domain skill detay
- `.claude/rules/ui-standard.md §8` — ekran UX skill detay
- `.claude/rules/agent-usage.md` — ajan (reviewer/danışman) seçimi
- `.claude/rules/footprint-ladder.md` — yeni skill/agent en-dar-basamak
