# M08 — Cycle Count + Sayım Freeze Spesifikasyonu

> Bu doküman M08 Cycle Count modülünün sayım-freeze (stok satırı bazlı kilit) kurallarını tanımlar.

**Tarih:** 2026-05-30 · **Durum:** YAZILI NOT (uygulama S7 — bugün KOD YOK) · **Modül:** M08
**Kaynak:** KARAR K5 (Fikri, 2026-05-29) — `docs/REFERENCE_STUDY.md` §7 (B13)

---

## 0. Önemli Ayrım — Dönem Kontrolü ≠ Sayım Freeze

İki ayrı mekanizma, **birbirine karıştırılmaz, tek tabloya sıkıştırılmaz**:

| | Dönem Kontrolü (K4) | Sayım Freeze (K5 — bu belge) |
|---|---|---|
| Granülarite | **ZAMAN** (yıl/ay dönemi) | **STOK SATIRI** (CompanyId+Warehouse+Bin+Item, gerekirse Lot) |
| Kapsam | Bir tarihten öncesini tüm evrak girişine kapatır | Sadece sayımdaki belirli satır kümesini dondurur |
| Tablo | `AccountingPeriod` (CompanyId taşır) | sayım oturumu + dondurulmuş satır kümesi |
| Plan | plan 14 (BUGÜN, mekanizma) | M08 / S7 (SONRA) |
| Guard | `sp_GuardPeriodOpen(@companyId,@date)` | `sp_GuardStockFrozen(@companyId,@warehouseId,@binId,@itemId)` |

Kanca: `sp_GuardStockFrozen` **kancası** plan 14'te açılır (boş/no-op); **gerçek implementasyon** bu modülde (S7).

---

## 1. Freeze Granülaritesi — Stok Satırı Bazlı (depo bazlı DEĞİL)

- Kilit anahtarı: **CompanyId + WarehouseId + BinId + ItemId** (gerekirse + LotNo).
- **Depo bazlı kilit YASAK.** B deposu sayılırken sistem çalışır; A deposundaki — ve B deposunda
  sayım **kapsamı dışındaki** — mallar hareket görmeye devam eder.
- Sadece sayım oturumunun dondurduğu satır kümesi kilitlenir.

## 2. Bölge / Oturum = Dondurulmuş Satır Kümesi

- "Bölge" veya "oturum" = sayım başında dondurulan **satır kümesi** (fiziksel rafla değil, KÜMEYLE tanımlı).
- Kesinleşme (POST) ve iptal bu **küme bazında** yapılır.
- Birden çok oturum eşzamanlı olabilir; her biri kendi satır kümesini dondurur (örtüşme engellenir).

## 3. Dondurulmuş Kaleme Hareket Yasağı + Çözüm Döngüsü

- Dondurulmuş bir kaleme stok hareketi (RECEIPT/ISSUE/TRANSFER/ADJUSTMENT) **YASAK**.
- Acil giriş gerekirse çözüm döngüsü:
  1. İlgili **sayım oturumu iptal** edilir,
  2. stok hareketi (giriş) yapılır,
  3. o oturum **yeniden sayılıp kesinleştirilir**.
- **Atomik değil, bölge bazlı:** biten/kesinleşmiş diğer oturumlar **KORUNUR** (iptalden etkilenmez).

## 4. Guard Entegrasyonu

Tüm stok hareket SP'leri (sp_ReceivingPost, sp_ShippingPost, sp_TransferPost, sp_CycleCountPost,
üretim sarf/giriş) **ilk satırlarda** şu guard'dan geçer:

```
EXEC sp_GuardStockFrozen @CompanyId, @WarehouseId, @BinId, @ItemId;  -- (+ @LotNo opsiyonel)
```

- Kalem aktif bir sayım oturumunda dondurulmuşsa SP `THROW` ile reddeder.
- **Engel mesajı (Türkçe):** "Bu kalem aktif sayımda (Sayım #...). Hareket için sayım oturumunu iptal edin."
- Kanca plan 14'te no-op olarak açılır; bu modül gerçek kontrolü (aktif oturum + küme üyeliği) doldurur.

## 5. BUGÜN YAPILMAYACAK (sınır)

- Freeze tablo şeması, oturum yönetimi, `sp_GuardStockFrozen` gerçek gövdesi, UI — hepsi **S7 sprint**.
- Bu belge sadece kararın **kaybolmaması** için yazılı nottur (K5).

## 6. İlişkili

- `docs/REFERENCE_STUDY.md` §7 (K4/K5 ayrımı) + §6 backlog B13
- `plans/14-ledger-pk-immutability.md` §2.d — `sp_GuardStockFrozen` kancası (plan 14 açar, bu modül doldurur)
- `.claude/rules/document-immutability.md` — evrak/defter bütünlüğü
- `docs/TODO.md` M08 Cycle Count ekranları (mevcut) + öncelik sırası (B13 ertelenmiş)
