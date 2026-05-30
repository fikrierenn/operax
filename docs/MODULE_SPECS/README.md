# Operax Modül Spec'leri

Her modülün detay tasarımı, eksik özellikler, şema değişiklikleri, SP listesi, UI ekranları ve test senaryolarını içerir.

Rakip analizi ve genel önceliklendirme için: [../COMPETITOR_ANALYSIS.md](../COMPETITOR_ANALYSIS.md)

## Dizin

### Yazılmış (Faz 2A öncelikleri)
| Modül | Spec | Kapsam |
|---|---|---|
| M02 — Maliyetlendirme | [M02_Costing_FIFO_Standard.md](M02_Costing_FIFO_Standard.md) | Moving Avg + FIFO + Standart maliyet motorları |
| M03 — Satınalma Genişletme | [M03_Purchasing_Extended.md](M03_Purchasing_Extended.md) | RFQ, fiyat farkı, çok seviyeli onay, vade, tedarikçi skoru |
| M04 — Satış Faturası + Fiyat | [M04_SalesInvoice_Pricing.md](M04_SalesInvoice_Pricing.md) | Çok katmanlı fiyat, kredi limit, EOD fatura, tevkifat, konsinye |
| M08 — CycleCount Freeze | [M08_CycleCount_Freeze.md](M08_CycleCount_Freeze.md) | Sayım freeze (stok satırı bazlı kilit) — YAZILI NOT, uygulama S7 (K5) |
| M11 — Finans SP'leri | [M11_Finance_Procedures.md](M11_Finance_Procedures.md) | Çek/senet/kredi/kart SP'leri, ödeme planı, nakit projeksiyon, yaşlandırma |
| M16 — Entegrasyon | [M16_Integration_EInvoice_Carrier.md](M16_Integration_EInvoice_Carrier.md) | e-Fatura, kargo, marketplace, muhasebe ihracı |

### Yazılacak (Faz 2B-2C)
- M01_MasterData_Variant.md — Beden/renk/varyant matrisi
- M05_Shipping_Loading.md — Sevkiyat planlama + araç + dock yönetimi
- M06_Picking_Wave_Zone.md — Wave + Zone + Cluster picking
- M09_Traceability_LPN_Carton.md — LPN/Carton/GS1-128 etiket
- M10_Manufacturing_MRP.md — Malzeme ihtiyaç planlama + Gantt
- M12_Service_RMA.md — Servis ticket + RMA + SLA + bakım
- M14_Commissions.md — Satış prim/komisyon kuralları
- M15_Reports_Dashboard.md — Pivot rapor + custom dashboard
- M17_Packaging.md — Paket/lisans yönetimi
- M20_B2B_Portal.md — Müşteri self-service portal
- M21_Mobile_Terminal.md — El terminali + mobil PWA

## Spec Format Standardı

Her spec dosyası sırayla şu başlıkları içerir:
1. **Kapsam ve Hedef**
2. **Eksik Özellikler ve Şema Eklemeleri** (her özellik için: sorun, çözüm, şema, SP, UI)
3. **Stored Procedure Tam Listesi**
4. **Yeni UI Ekranları**
5. **Test Senaryoları**
6. **Bağlı Modüller**
