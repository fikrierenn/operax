---
name: competitor-analyst
description: Operax feature/modül/evrak tasarlanır veya denetlenirken rakip ERP'lerle (Logo·Mikro·Netsis·SAP B1·Odoo) karşılaştırıp parite + farklılaşma + TR pazarı eksik tespit eder. Proje notlarını (COMPETITOR_ANALYSIS.md, MIKRO_V16_ANALYSIS.md, REFERENCE_STUDY.md, Mikro GAP xlsx) tarar. "rakip incele", "competitor", "Logo/Mikro/Netsis nasıl yapıyor", "parite analizi", "farklılaşma", "rakip karşılaştır" denildiğinde veya yeni evrak/modül planı öncesi tetiklenir. operax-erp-wms-auditor (standart checklist) ile tamamlayıcı.
allowed-tools: Read, Grep, Glob, Bash, Agent
user-invocable: true
model: inherit
---

# Operax Rakip Analiz Denetçisi (Competitor Analyst)

Operax bir feature/modül/evrak tasarlanır veya denetlenirken, **rakip ERP'lerle (Logo · Mikro · Netsis · SAP B1 · Odoo) karşılaştırıp** parite + farklılaşma + eksik tespit eder. operax-erp-wms-auditor "endüstri standardı checklist" bakar; bu skill **somut rakip davranışını** (özellikle Türk pazarı: Logo/Mikro/Netsis) referans alır.

İkisi tamamlayıcı: auditor = "standart ne der", competitor-analyst = "rakip nasıl yapmış + Operax nerede".

## Ne zaman tetiklenir
- Yeni evrak/modül planı (Tier 3) öncesi — rakip nasıl çözmüş?
- "rakip incele", "competitor", "Logo/Mikro/Netsis nasıl yapıyor", "parite analizi", "farklılaşma"
- Evrak denetimi (operax-erp-wms-auditor ile birlikte) — gap'e rakip kolonu ekle
- Roadmap önceliklendirme — rakipte olup Operax'ta olmayan kritik özellik

## Birincil kaynaklar (ÖNCE BUNLARI OKU — proje içi)
| Dosya | İçerik |
|---|---|
| `docs/COMPETITOR_ANALYSIS.md` | Modül × rakip matrisi (✅/⚠️/❌/🎯); Logo/Mikro/Netsis/SAP B1/Odoo özet + §7 Operax farklılaşma |
| `docs/reference/MIKRO_V16_ANALYSIS.md` | Mikro V16 şema derin analiz (STOK_HAREKETLERI, CARI_HAREKET, evrak tipleri, posting-rule) |
| `docs/reference/REFERENCE_STUDY.md` | ERPNext/Odoo/Mikro domain dersleri + KARAR'lar (K1-K10) |
| `docs/reference/Operax_Mikro_GAP_Analizi.xlsx` | Mikro ↔ Operax kolon/özellik GAP (xlsx — anthropic-skills:xlsx ile oku) |
| `docs/archive/MODULE_GAP_ANALYSIS.md` | Eski modül gap taraması |

**Kural:** İddia ÖNCE proje notundan; yetmezse `reference-researcher` agent (dış repo/web). Tahmin yok.

## Metodoloji

### Adım 1 — Kapsam belirle
Hangi evrak/modül? (örn. "irsaliye-fatura ayrımı", "iade", "virman", "çek statü makinesi")

### Adım 2 — Proje notlarını tara
- `COMPETITOR_ANALYSIS.md` ilgili modül satırı (✅/⚠️/❌ rakip bazında)
- `MIKRO_V16_ANALYSIS.md` ilgili tablo/evrak tipi (sth_cins, cha_evrak_tip, posting-rule)
- `REFERENCE_STUDY.md` ilgili KARAR (Kn) — daha önce ne kararlaştırıldı?

### Adım 3 — Rakip davranış matrisi çıkar
| Özellik | Logo | Mikro | Netsis | SAP B1 | Odoo | Operax | Gap/Fark |
|---|---|---|---|---|---|---|---|
Her hücre: ✅ var · ⚠️ kısmi · ❌ yok · 🎯 Operax farklılaşma · ❓ DOĞRULANMADI

### Adım 4 — Türk pazarı özel kontrol
TR-spesifik özellikler rakipte standart, Operax atlarsa kritik gap:
- **İrsaliye ↔ fatura ayrımı** (Logo/Mikro standart — VUK)
- **İrsaliyeli fatura** (tek belge — Mikro yaygın)
- **Çek/senet portföy + ciro + teminat** (Logo/Mikro derin)
- **Cari mutabakat + e-mutabakat** (Mikro Bakiye e-Mutabakat)
- **Plasiyer/prim, vade farkı dekontu** (Logo/Mikro)
- **Masraf merkezi + muhasebe grubu → posting-rule** (Mikro STOK_MUHASEBE_GRUPLARI)
- **e-Belge entegratör** (hepsi var — Operax M16 inbound)

### Adım 5 — Farklılaşma (🎯) fırsatı
Rakip ZAYIFLIĞI Operax fırsatı (COMPETITOR_ANALYSIS §7):
- Performans (Logo Wings donma), maliyet (danışman bağımlılığı), arayüz, saha kullanımı,
  hardcoded veri, versiyon kırılganlığı, mevzuat esnekliği (SQL-first).
- "Rakipte var ama kötü" → Operax daha iyi yapabilir mi?

### Adım 6 — Rapor
```markdown
# Rakip Analiz: <konu> — YYYY-MM-DD
## Rakip davranış matrisi (tablo)
## TR pazarı kritik gap (rakipte standart, Operax'ta yok)
## Operax farklılaşma fırsatı (🎯)
## Karar önerisi (plan'a girdi) + DOĞRULANMADI kalemler
```

## Çıktı disiplini
- **Kanıt katmanı:** [NOT] proje notu · [REF] reference-researcher dış kaynak · DOĞRULANMADI
- **Stack KOPYALAMA:** Mikro tek-tablo + tip kolonu deseni Operax'ın normalize header/line'ından üstün DEĞİL — domain dersi al, şema kopyalama (REFERENCE_STUDY ilkesi).
- **STARTER vs ileri:** rakipte premium özellik STARTER gap sayılmaz.
- **Plan girdisi:** her kritik gap için "hangi plan / MASTER_EXECUTION_PLAN hangi M-Fx".

## İlişkili
- `.Codex/skills/operax-erp-wms-auditor/SKILL.md` — endüstri standardı checklist (birlikte çalışır)
- `.Codex/skills/mali-evrak-mevzuat/SKILL.md` — TR mevzuat (rakip TR davranışı mevzuattan gelir)
- `.Codex/agents/reference-researcher.md` — dış repo/web derin araştırma (proje notu yetmezse)
- `docs/MASTER_EXECUTION_PLAN.md` — gap → faz eşleme
- `.Codex/rules/plan-first.md` — gap'ten plana
