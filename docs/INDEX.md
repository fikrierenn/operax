# Operax — Doküman Haritası (INDEX)

Tek bakışta hangi bilgi nerede. (Toparlama: 2026-05-31)

## 🎯 Canonical — aktif yönetim
| Doküman | İçerik |
|---|---|
| [MASTER_EXECUTION_PLAN.md](MASTER_EXECUTION_PLAN.md) | **TEK canonical plan** — modül-sıralı yapılacaklar (F0-F7). "Sırada ne var" buradan okunur. |
| [MASTER_ROADMAP.md](MASTER_ROADMAP.md) | Modül kapsam referansı (Faz 1/2/3 backend+UI). |
| [TODO.md](TODO.md) | Ekran/modül bazlı aktif todo + kod-review borçları (CRIT/HIGH/IMP). |
| [BUGS.md](BUGS.md) | Bug/hata takibi (AR-001..). |
| [ARCHITECTURE.md](ARCHITECTURE.md) | Mimari tasarım belgesi. |
| [TESTING.md](TESTING.md) | Test stratejisi. |
| [COMPETITOR_ANALYSIS.md](COMPETITOR_ANALYSIS.md) | Rakip matrisi (Logo/Mikro/Netsis/SAP B1/Odoo). |
| [VISION.md](VISION.md) | Ürün vizyonu + defter stratejisi kararları. |
| [CONTEXT_MANAGEMENT.md](CONTEXT_MANAGEMENT.md) | Bağlam yönetimi anayasası. |
| [AGENTS.md](AGENTS.md) | Paralel agent çalışma stratejisi. |

## 🏗️ design/ — mimari tasarım spec'leri
- [DYNAMIC_CUSTOM_FIELDS.md](design/DYNAMIC_CUSTOM_FIELDS.md) — UDF mimarisi
- [DATABASE_DRIVEN_LOCALIZATION.md](design/DATABASE_DRIVEN_LOCALIZATION.md) — dinamik çoklu dil
- [MULTI_COMPANY_SWITCHER.md](design/MULTI_COMPANY_SWITCHER.md) — çoklu şirket geçiş mimarisi

## 📚 reference/ — dış referans araştırmaları (girdi, değişmez)
- [REFERENCE_STUDY.md](reference/REFERENCE_STUDY.md) — açık-kaynak ERP/WMS çalışması (B1-B18)
- [MIKRO_V16_ANALYSIS.md](reference/MIKRO_V16_ANALYSIS.md) — Mikro V16/V17 şema referansı (§12 E1-E13)
- `Operax_Mikro_GAP_Analizi.xlsx` · `OPERAX_Platform_Master_Document_v2_2_TR.docx`

## 📁 MODULE_SPECS/ — modül detay spec'leri
M02 Costing/FIFO · M03 Purchasing · M04 SalesInvoice · M08 CycleCount · M11 Finance · M16 Integration

## 🗄️ archive/ — superseded/tarihsel (silinmedi, geri alınabilir)
- `SPRINTS.md` · `SPRINT_0.md` — sprint modeli (modül-execution'a geçildi)
- `MODULE_GAP_ANALYSIS.md` · `GAP_DETAIL.md` · `AUDIT_REPORT_2026-05-28.md` — gap'ler MASTER_EXECUTION_PLAN F0-F7'ye taşındı
- `OPERAX_Analiz_ve_Plan.md` · `OPERAX Platform Master Döküman.md` — eski planlama (MASTER_EXECUTION_PLAN + MASTER_ROADMAP supersede etti)

## 🗂️ Diğer
- `journal/YYYY-MM-DD.md` — günlük oturum kayıtları
- `plans/NN-*.md` — Tier 3 iş planları · `sql/` — şema + db_objects

## Bakım kuralı
Yeni planlama dokümanı açma — MASTER_EXECUTION_PLAN tek canonical. Bilgi çoğullaşırsa buraya değil, ilgili canonical'a yaz. Superseded olan → `archive/` (git mv, silme yok).
