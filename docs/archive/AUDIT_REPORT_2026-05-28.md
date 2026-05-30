# Operax ERP/WMS Denetim Raporu — 2026-05-28

> Üreten: `operax-erp-wms-auditor` skill
> Yöntem: 4 paralel modül taraması + excess bash taraması + domain checklist karşılaştırması
> Kapsam: Tüm `Features/` modülleri + `docs/sql/` SP/şema coverage

---

## 1. Modül Olgunluk Skoru

| Modül | Olmazsa-Olmaz | Olgun | İleri | Genel | Paket |
|---|---|---|---|---|---|
| M01 Master Data | 6/7 ⚠️ | 4/6 | 0/4 | 🟢 Olgun | STARTER |
| M02 Inventory+Cost | 6/6 ✅ | 3/6 | 1/5 | 🟢 Olgun | STARTER |
| M03 Procure-to-Pay | 4/4 ✅ | 3/5 | 1/5 | 🟢 Olgun | STARTER |
| M04 Order-to-Cash | 4/4 ✅ | 3/6 | 0/5 | 🟢 Olgun | STARTER |
| M05/06 Ship+Pick | 4/4 ✅ | 3/5 | 0/6 | 🟢 Olgun | WMS_PRO |
| M07/08 Transfer+Count | 2/2 ✅ | 4/4 ✅ | — | 🟢 Tam | WMS_PRO |
| M09 Traceability | 2/2 ✅ | 2/3 | 0/2 | 🟢 Olgun | WMS_PRO |
| M10 Manufacturing | 4/4 ✅ | 5/5 ✅ | 1/6 | 🟢 Tam | MANUFACTURING |
| **M11 Finans** | **5/7 ⚠️** | 4/6 | 0/5 | 🟡 **Eksik** | STARTER |
| M15 Dashboard | 1/1 ✅ | 2/4 | 0/3 | 🟢 Olgun | ENTERPRISE |

**Sonuç:** STARTER'da tek 🟡 modül **M11 Finans** (veri girişi formları). Diğer tüm modüller olgun/tam.

---

## 2. 🔴 CRITICAL Eksikler (Olmazsa-Olmaz)

| # | Modül | Eksik | Kanıt | Plan |
|---|---|---|---|---|
| G1 | M11 | Hesap/Çek/Senet/Kart/Kredi Create formu yok | `Features/Finance/*/` — Create.cshtml yok | Plan 02 |
| G2 | M11 | Ödeme/Tahsilat kaydetme ekranı yok | `Features/Finance/Payments/` yok (sp hazır) | Plan 02 F5 |

## 3. 🟡 Önemli Eksikler (Olgun)

| # | Modül | Eksik | Kanıt |
|---|---|---|---|
| G3 | M01 | Partners risk/vade/eFatura alanları UI'da yok | `Partners/Details.cshtml.cs:59` DTO eksik |
| G4 | M03/04 | Receiving/Shipping Cancel handler yok | `Receiving/Details.cshtml.cs` Cancel yok |
| G5 | M03/04 | PO/SO/Recv/Ship satır silme yok | Add var, Delete handler yok |
| G6 | M00 | Parameters/StatusTransitions Create/Edit yok | salt liste |
| G7 | M00 | Settings boş, Companies klasörü yok | — |
| G8 | M02 | FIFO maliyet yok (sadece Moving Avg) | `sp_UpdateItemCostMovingAvg` tek yöntem |

## 4. 🟢 Gelecek (İleri — STARTER dışı, gap sayılmaz)

M03 RFQ + çok-seviye onay · M04 kampanya/iskonto · M10 MRP/CRP/OEE · M11 banka mutabakatı/nakit projeksiyon · M15 pivot/drag-drop dashboard · Reports/Service/Project/Incentives modülleri.

---

## 5. ♻️ FAZLA / Ölü Kod (Excess)

### 5.1 Boş Placeholder Klasörler (5)
`Features/` altında 0 dosyalı klasörler:
- `Incentives/`, `Integration/`, `Project/`, `Service/` — gelecek modül placeholder (kabul edilebilir ama route üretmiyor)
- ⚠️ **`Sales/`** — SalesOrders ayrı klasörde; bu boş `Sales/` kafa karıştırıcı, **silinmeli**

### 5.2 UI Bekleyen SP'ler (ölü değil, bağlanmamış)
| SP | Durum |
|---|---|
| `sp_CreateLoan` | ✅ tam (7 tip) ama Loan Create UI yok → Plan 02 F1 bağlayacak |
| `sp_RecordPaymentAndAutoClose` | ✅ tam ama Payment UI yok → Plan 02 F5 bağlayacak |

**Not:** `sp_AutoClosePayments` (3 ref), `sp_UpdateItemCostMovingAvg` (5 ref), `sp_ValidateStatusTransition` (8 ref) → SP→SP zinciri sağlıklı, ölü DEĞİL.

### 5.3 DRY İhlali / Tekrar
- `ActionLabel` switch PO+SO Details'te birebir tekrar → `UiHelpers.AuditActionLabel` (IMP-6)
- SQL içi `'Sistem'` magic string PO+SO (IMP-8)

### 5.4 Hardcoded Veri (ui-standard §1.5 ihlali)
- PO Index/Details `DATEADD(DAY, 14, OrderDate)` — `Partner.PaymentTermDays` var, kullanılmalı (IMP-3)

### 5.5 Over-engineering
- ❌ Yok. e-Belge şeması eklendi ama outbound gönderim YAZILMADI (inbound sync kararı) — doğru karar, fazla kod yok.

---

## 6. Rakip Farklılaşma Skoru

Operax'ın Türk ERP 7 zayıflığını yapısal önleme durumu:

| # | Rakip Zayıflığı | Operax Önlemi | Durum |
|---|---|---|---|
| 1 | Yavaşlık/donma | Dapper raw SQL + SARGable + SELECT* yasak | ✅ Yapısal |
| 2 | Maliyet/danışman bağımlılığı | Single-tenant + operax-cli self-service | ✅ Yapısal |
| 3 | Kötü arayüz | Tek tasarım dili + tam Türkçe + partial | ✅ Yapısal |
| 4 | Saha kullanımı yok | El terminali (4 modül) + barkod | ✅ Yapısal |
| 5 | Hardcoded demo veri | §1.5 sıfır tolerans | ⚠️ 1 ihlal (IMP-3) |
| 6 | Versiyon kırılganlığı | Idempotent migration | ✅ Yapısal |
| 7 | Yerel mevzuat esnekliği | SQL-first iş mantığı | ✅ Yapısal |

**Skor: 6.5/7** — Operax tasarımı rakip zayıflıklarının neredeyse tamamını yapısal önlüyor. Tek aktif ihlal: hardcoded 14-gün vade (kolay fix).

---

## 7. Aksiyon Sırası (Önceliklendirilmiş)

1. **Plan 02** (devam) — M11 Finance Create formları (G1, G2 kapatır)
2. **Plan 03** — Evrak bütünlüğü: Cancel + satır silme (G4, G5) + CRIT-1..4
3. **Plan 04** — Partners risk (G3) + Admin (G6, G7)
4. **Hızlı temizlik** — boş `Sales/` klasörü sil, IMP-3 vade fix, IMP-6 DRY
5. **Faz 2B** (STARTER sonrası) — FIFO maliyet (G8), RFQ, e-Belge sync

---

## 8. STARTER "Bitti" İçin Kalan

- [ ] Plan 02 (M11 Create) — 6 faz
- [ ] Plan 03 (evrak bütünlüğü + CRIT)
- [ ] Plan 04 (Partners + Admin)
- [ ] E2E test: PO→Receiving→ItemCost→SO→Shipping→Invoice→Tahsilat→Mali Durum

**Tahmin:** STARTER tam ~3-4 oturum.
