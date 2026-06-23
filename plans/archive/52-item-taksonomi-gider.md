# Plan 52 — Item Taksonomisi + Gider Ayrımı (STOCK/SERVICE, evrak-driven)

**Durum:** ✅ TAMAMLANDI (2026-06-23) — Faz 1-4 + reviewer bulguları (CRIT-1 SERVICE-only iptal kilidi + IMP-1 costing SERVICE guard) fix + doğrulandı.
**Tier:** 3 (Item master + SP'ler + çoklu picker + UI; mali-mevzuat etkili)
**Tarih:** 2026-06-23
**Tetik:** Çok-amaçlı ürün (streç film: satılır+üretim+sarf) + "gider nasıl belirlenir" sorusu. İKİ uzman (erp-isleyis-danismani + mali-mevzuat/TDHP) hemfikir.

---

## 1. Problem

Mevcut `Item.ItemType` = STOCK / CONSUMABLE / SERVICE / FIXED_ASSET (tek-değer, mutually-exclusive). Sorunlar (kod-kanıtlı):

- **CONSUMABLE yanlış kategori:** "sarf" bir KULLANIM, ürün DOĞASI değil. Streç film hem satılır hem sarf → mutually-exclusive tip onu temsil edemez. Canlıda 16 kalemin hepsi STOCK; CONSUMABLE hiç kullanılmıyor (dead value). CHECK constraint yok.
- **🔴 BUG — SERVICE hayalet stok:** Hiçbir SP `ItemType` okumuyor (`grep ItemType db_objects*.sql` = 0). SERVICE item sevk/mal-kabul edilirse fiziksel-olmayan üründen StockMovement yazılır (negatif/hayalet stok). Şu an latent (hepsi STOCK) ama go-live riski.
- **EXPENSE belirsizliği:** "Gider olacaksa nasıl?" — saf gider (kira/elektrik) item mi? Hayır.

## 2. İki-Eksen Modeli (uzman + TDHP uzlaşısı)

- **Eksen A — "stok mu hizmet mi" → ÜRÜN belirler** (doğa, sabit). Fiziksel mal=STOCK; hizmet=SERVICE.
- **Eksen B — "hangi hesaba/gidere" → EVRAK belirler.** Aynı STOCK malı: satış→621 · üretim girdisi→150 · idari sarf→770. Item sabit, evrak karar verir.
- **Saf gider (kira/elektrik) HİÇ item değil** → mevcut `ExpenseInvoice + ExpenseType + CostCenter` taksonomisi (zaten doğru). EXPENSE item-tipi YAPILMAZ (Mikro da gideri ayrı tutar; TDHP 770 item değil).
- **TDHP teyidi:** ambalaj malzemesi (streç film) = 150 İlk Madde → stoklanır, kullanılınca giderleşir → STOCK item, evrak/kullanım belirler.

## 3. Scope

**DAHİL:**
- **Faz 1 — ItemType sadeleştir (STOCK/SERVICE/FIXED_ASSET):**
  - `Dtos.ItemType`: CONSUMABLE **kaldır** (Stock/Service/FixedAsset kalır).
  - migration_52: mevcut CONSUMABLE item'ları STOCK'a map (defensif, 0 satır beklenir) + CHECK constraint `ItemType IN ('STOCK','SERVICE','FIXED_ASSET')` (kapalı sistem-kümesi — SP'ler branch'leyecek, sözlük-driven DEĞİL).
  - Items/Details UI: ItemType dropdown 3 değer.
- **Faz 2 — SERVICE stok-atlama guard (BUG fix):** `sp_ReceivingPost` + `sp_ShippingPost` (+ varsa Transfer): StockMovement yazan döngüde `WHERE i.ItemType <> 'SERVICE'` (hizmet satırı stok yazmaz). SERVICE satılır/faturalanır ama fiziksel hareket üretmez.
- **Faz 3 — Picker'ları evrak-mantığına göre:**
  - Stok evrakları (MaterialIssue/Shipping/Transfer/BOM-girdi): `ItemType <> 'SERVICE'` (fiziksel). MaterialIssue `IN ('STOCK','CONSUMABLE')` → `<> 'SERVICE'`.
  - Fatura/satış-satınalma satırı: hepsi (SERVICE dahil).
- **Faz 4 — Plan 06 çelişki düzelt (doc):** `plans/06`'daki "EXPENSE item-tipi" satırı (Mikro §13 ile çelişiyor) → "EXPENSE item değil, ExpenseType ayrı" olarak revize. Plan 50/M2 notuna işle.

**HARİÇ (gerekçeli):**
- **SERVICE ayrı tablo (Mikro-tarzı):** reddedildi — tek-master discriminator (Operax SQL-first tercihi); ayrı tablo+CRUD+lookup bakım yükü. SERVICE item, fiyat/satır altyapısını ücretsiz alır.
- **153/150/770 GL hesap eşlemesi:** gelecek **GL muhasebeleştirme modülü** posting-rule işi (item-tipi değil; grup+hareket→hesap). Bu plan dışı.
- **FIXED_ASSET amortisman akışı:** demirbaş/255 ayrı domain; ItemType değeri korunur ama amortisman ayrı plan.

## 4. Alternatifler (reddedilen)
- **Kapasite flag'leri** (IsSaleable/IsConsumable/IsManufactured): reddedildi — çok-amaçlı üründe hepsi açılır (engel olmaz), bakım yükü, evrak zaten ayrımı veriyor. (Kullanıcı + iki uzman: flag gereksiz.)
- **EXPENSE item-tipi:** reddedildi — saf gider item değil (UOM/barkod/raf anlamsız), ExpenseType zaten var.
- **CONSUMABLE koru:** reddedildi — kullanım≠doğa; dead value; streç film karşı-örnek.

## 5. Riskler
| Risk | Etki | Mitigasyon |
|---|---|---|
| CONSUMABLE drop mevcut veri kırar | düşük | canlıda 0 CONSUMABLE; migration map + CHECK; fresh-DB ritüeli |
| SERVICE guard mevcut sevkleri etkiler | düşük | hepsi STOCK → guard no-op; smoke (SERVICE item ekle→sevk→stok yazılmadı) |
| Picker filtre değişimi ürün gizler | orta | `<> 'SERVICE'` STOCK/FIXED_ASSET'i korur; smoke her picker |
| CHECK constraint admin'i kısıtlar | düşük | ItemType bilinçli kapalı-küme (SP branch'ler); dictionary-driven DEĞİL (reason'dan farklı) |

## 6. Done Criteria
- [x] Faz 1: Dtos CONSUMABLE kaldır + migration_52 (CONSUMABLE→STOCK map + CHECK drop/recreate) + migration_41 canonical + Items UI 3 değer
- [x] Faz 2: sp_ReceivingPost(RECEIPT+RETURN)/ShippingPost SERVICE stok-atlama; smoke (SERVICE receiving→0 StockMovement, STOCK→1)
- [x] Faz 3: picker'lar evrak-mantığı (MaterialIssue/Shipping `<> SERVICE`, SalesOrders tüm aktif)
- [x] Faz 4: Plan 06 EXPENSE-item çelişki düzeltildi (revize banner)
- [x] Build 0/0 · sql-sp-reviewer (CRIT-1 SERVICE-only iptal kilidi + IMP-1 costing guard → FIX) · code-reviewer (2 LOW → stale yorum fix) · **fresh-DB ritüeli** (0 fail + CHECK doğru) · smoke (SERVICE-only reverse→OK/CANCELLED)
- [x] Plan arşive + journal

## 7. Faz sırası
1. Faz 1 (ItemType+migration+UI) → fresh-DB ritüeli
2. Faz 2 (SP guard) → smoke SERVICE
3. Faz 3 (picker'lar)
4. Faz 4 (doc)
5. Kapanış kapısı

## 8. 5 Lens
- 🔴 **Contrarian:** Fatal flaw? CONSUMABLE drop'la "sarf malzeme" bilgisi kaybolur mu? Hayır — sarf bir KULLANIM (MaterialIssue evrağı), item-tipi değil; streç film STOCK + MaterialIssue ile sarf olur.
- 🔵 **First Principles:** Doğru soru "ürünün doğası mı kullanımı mı?" — doğa (STOK/HİZMET) item'da, kullanım evrakta. İki eksen karıştırılmamalı.
- 🟢 **Expansionist:** Daha büyük? Tam GL posting-rule (153/150/770 otomatik fiş) — ama ayrı büyük modül; bu plan taksonomiyi doğru kurar, GL onun üstüne gelir.
- ⚪ **Outsider:** Yabancı ne garip bulur? "CONSUMABLE hem STOCK gibi davranıyor hem ayrı tip" → tekilleştir.
- 🟡 **Executor:** Pazartesi? Dtos CONSUMABLE sil → migration CHECK → SP SERVICE guard → picker filtre → smoke.

## 9. İlişkili
- `src/Operax.Web/Lib/Dtos.cs:316` (ItemType) · `Features/MasterData/Items/Details.cshtml.cs` · `MaterialIssue/Details.cshtml.cs:38` · `SalesOrders/Details.cshtml.cs` · `Shipping/Details.cshtml.cs`
- `docs/sql/db_objects_docchain.sql` (sp_ReceivingPost/ShippingPost) · `schema_M18_ExpenseReporting.sql` (ExpenseType — korunur)
- `plans/06-expense-service-analytic-accounting.md` (EXPENSE-item çelişkisi — Faz 4 düzeltir)
- `docs/reference/MIKRO_V16_ANALYSIS.md` §13 (ItemKind discriminator + gider ayrı master)
- İki uzman raporu: erp-isleyis-danismani (taksonomi) + TDHP araştırma (153/150/770)
**Kaynaklar:** [153 vs 770 — muhasebenews](https://www.muhasebenews.com/satilan-ticari-mallar-153-hesap-yerine-direkt-621-hesabina-kaydedilebilir-mi/) · [150 İlk Madde/ambalaj — muhasebedersleri](https://www.muhasebedersleri.com/hesaplar/150-ilk-madde-malzeme.html)
