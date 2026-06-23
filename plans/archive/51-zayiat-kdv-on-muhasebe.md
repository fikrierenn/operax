# Plan 51 — Zayiat KDV Ön-Muhasebe (sebep→KDV düzeltme)

**Durum:** ✅ TAMAMLANDI (2026-06-23)
**🔄 PIVOT (kullanıcı geri bildirimi):** Reason'lar HARDCODED sabit DEĞİL — **Dictionary-driven** (ui-standard §1.5 sıfır-hardcoded). `DictionaryType 'MATERIAL_ISSUE_REASON'` + 4 değer (Code=İngilizce, NameTr=Türkçe label, admin yönetir) + `DictionaryValue.RequiresKdvAdjustment` flag (mevzuat-bağlı, DictRefCols pattern). ScrapReason sabit sınıfı SİLİNDİ. Mücbir-sebep FIRE'dan ayrıştı (deprem/sel de mücbir). ReasonCode CHECK kaldırıldı (admin neden ekleyebilir → sözlük doğrular).
**Tier:** 3 (migration + SP + Dtos + PageModel + View; mali-mevzuat etkili)
**Tarih:** 2026-06-23
**Tetik:** mali-evrak-mevzuat denetimi — zayiat reason-code'ları KDV açısından farklı sonuç doğuruyor ama kod KDV-agnostik (M1 Faz 4 sadece stok düşürüyor). Kullanıcı: "sebep seçimine göre KDV kısmı ön-muhasebe olarak olması gerekeni yapsın."

---

## 1. Problem

VUK md.278 + **KDV md.30/c [DOC — GİB]:** zayi olan mala ait yüklenilen KDV **indirilemez** (indirilmişse düzeltilir = ilave edilecek KDV). **İstisna:** deprem/sel + Maliye-ilan mücbir-sebep yangın → korunur. **Normal üretim firesi** (Ticaret Odası oranı içinde) zayiat DEĞİL → KDV nötr.

Mevcut zayiat (Plan 47 Faz 4): `MaterialIssue.ReasonCode` (DAMAGE/FIRE/WASTE) → StockMovement SCRAP, **KDV yok**. Sorun: (1) WASTE "Hurda/Fire" KDV-nötr normal-fire ile KDV-düzeltme hurda'yı karıştırıyor; (2) hesaplanan ilave-KDV yok; (3) belgeleme (takdir komisyonu/rapor) alanı yok.

**Ön-muhasebe seviyesi:** Operax'ta tam GL (yevmiye/mizan) YOK. Bu plan **ön-muhasebe**: sebebe göre ilave-KDV'yi HESAPLA + belgede sakla + UI'da göster + belge-ref tut → muhasebeci/GL modülü buradan İlave-KDV'yi (360/391) işler. GL posting bu planın dışı.

## 2. Scope

**DAHİL:**
- **Reason taksonomisi (mevzuat-onaylı):** WASTE → ikiye böl: **SCRAP** (Hurda/İmha, KDV düzeltme) + **NORMAL_FIRE** (üretim firesi, KDV nötr). Final küme: `DAMAGE, FIRE, SCRAP, NORMAL_FIRE`.
- **migration_51:** MaterialIssueHeader'a:
  - `IsForceMajeure BIT NOT NULL DEFAULT 0` — mücbir sebep (yalnız FIRE; Maliye-ilan → KDV korunur)
  - `KdvAdjustmentAmount DECIMAL(18,2) NOT NULL DEFAULT 0` — POST'ta hesaplanan ilave-KDV (türev/snapshot)
  - `LegalDocRef NVARCHAR(100) NULL` — takdir komisyonu karar / itfaiye-sigorta rapor no
  - CHECK ReasonCode güncelle: `NULL | DAMAGE | FIRE | SCRAP | NORMAL_FIRE`
- **sp_MaterialIssuePost genişlet:** ReasonCode KDV-düzeltme sınıfındaysa (DAMAGE/FIRE/SCRAP) VE `IsForceMajeure=0` ise → her satır için `KDV = QtyBase × ItemCost.AvgCost × Item.TaxRate/100` topla → `KdvAdjustmentAmount` yaz. NORMAL_FIRE veya mücbir → 0. (Stok düşüşü değişmez; MovementType=SCRAP korunur.)
- **Dtos:** `ScrapReason` güncelle (Scrap, NormalFire ekle; Waste kaldır) + `ScrapReason.RequiresKdvAdjustment(code)` helper (KDV-sınıfı).
- **UI (Details):** Sebep dropdown 4 seçenek + (FIRE seçilince) "Mücbir sebep (Maliye-ilan)" checkbox + "Yasal belge no" input + POSTED sonrası "İlave Edilecek KDV: X ₺" göster.

**HARİÇ (gerekçeli):**
- **Tam GL fiş (360/391 yevmiye):** periyodik GL modülü (ayrı, muhasebe-mevzuat ön-koşullu).
- **Normal fire ORANI kontrolü** (Ticaret Odası oran tablosu): büyük veri; şimdilik NORMAL_FIRE = kullanıcı beyanı (oran aşımı tespiti ileride).
- **Item-lot bazlı gerçek yüklenilen KDV:** Operax alış-KDV'sini lot bazında tutmuyor → AvgCost×TaxRate yaklaşımı (mevzuat emsal-bedel ruhuna uygun, ön-muhasebe kabul).

## 3. Alternatifler (reddedilen)
- **KDV'yi GL fişine bırak:** reddedildi — GL yok; ön-muhasebe seviyesinde hesap+belge şart (kullanıcı istedi).
- **Tek "zayiat" reason + KDV her zaman düzelt:** reddedildi — normal fire'da KDV nötr (yanlış düzeltme = fazla vergi).
- **WASTE'i koru, flag ekle:** reddedildi — "Hurda/Fire" ad çakışması kök karışıklık; net ayrım daha doğru.

## 4. Riskler
| Risk | Etki | Mitigasyon |
|---|---|---|
| WASTE→SCRAP/NORMAL_FIRE migration mevcut veri | düşük | zayiat yeni (Plan 47, bu oturum); canlıda WASTE kaydı ~0, migration WASTE→SCRAP map + uyar |
| AvgCost×TaxRate gerçek yüklenilen-KDV değil | orta | ön-muhasebe yaklaşımı, emsal-bedel ruhuna uygun; lot-KDV ileride (HARİÇ) |
| KdvAdjustmentAmount snapshot drift | düşük | POST'ta tek yazım, reverse'de sıfırla (iptal → KDV düzeltme geri alınır) |
| CHECK constraint mevcut WASTE'i reddeder | orta | migration önce WASTE→SCRAP UPDATE, sonra CHECK; fresh-DB ritüeli |

## 5. Done Criteria
- [x] migration_51: 3 header kolon + DictionaryValue.RequiresKdvAdjustment flag + WASTE→SCRAP map + ReasonCode CHECK kaldır (sözlük-driven) — idempotent
- [x] seed_material_issue_reason: DictionaryType+4 değer per-company (Code EN, NameTr TR, KDV flag) — set-based idempotent
- [x] sp_MaterialIssuePost: KDV-sınıfı sözlük flag'inden okur; mücbir → KdvAdjustmentAmount hesap (LEFT JOIN ItemCost); reverse sıfırla
- [x] Dtos: ScrapReason SİLİNDİ + DictType.MaterialIssueReason; PageModel Reasons sözlükten + ValidReasonAsync
- [x] UI: sözlük dropdown + mücbir checkbox + belge-ref + POSTED KDV göster (Türkçe, hardcoded option yok)
- [x] Build 0/0 · code+sql-sp+security reviewer (sql-sp+security TEMİZ; code 3 HIGH = pre-existing Tailwind, drive-by dışı→TODO) · **fresh-DB ritüeli** (0 fail+3 kolon+flag+8 reason+eski CHECK yok) · smoke: DAMAGE 100×%20×2=40, NORMAL_FIRE=0, mücbir FIRE=0, reverse=0
- [x] Plan arşive + journal

## 6. Faz sırası
1. migration_51 (schema) → fresh-DB ritüeli
2. Dtos (ScrapReason + helper)
3. sp_MaterialIssuePost (KDV hesap)
4. UI (Details)
5. Kapanış kapısı (review + smoke)

## 7. Rollback
git revert (migration kolonları DROP idempotent değil → migration'a DROP guard veya manuel). Stok davranışı değişmedi (sadece KDV-snapshot eklendi) → düşük risk.

## 8. 5 Lens
- 🔴 **Contrarian:** Fatal flaw? AvgCost×TaxRate gerçek alış-KDV değil → ama lot-KDV yok; emsal-bedel ön-muhasebe kabul, tam GL'de düzeltilir.
- 🔵 **First Principles:** Doğru soru "bu zayiat KDV düzeltme gerektirir mi?" → sebep belirler (md.30/c); normal fire ≠ zayiat.
- 🟢 **Expansionist:** Daha büyük? GL fiş otomasyonu — ama ayrı modül; ön-muhasebe hesap yeterli ilk adım.
- ⚪ **Outsider:** Yabancı ne garip bulur? "WASTE hem normal fire hem hurda" → ayır.
- 🟡 **Executor:** Pazartesi? migration 3 kolon → SP CASE → UI dropdown+checkbox → smoke 4 senaryo.

## 9. İlişkili
- `.claude/skills/muhasebe-mevzuat` (TDHP) + `.claude/skills/mali-evrak-mevzuat` (KDV md.30/c kaynak)
- `docs/sql/db_objects_materialissue.sql` (sp_MaterialIssuePost) · `migration_49` (ReasonCode)
- `plans/archive/47-module-M1-inventory.md` (Faz 4 zayiat temeli)
- `.claude/rules/phase-review-gate.md §3.5` (fresh-DB ritüeli)

**Kaynaklar:** [GİB KDV md.30](https://gib.gov.tr/node/86847) · [KDV md.30/c zayi mal — denet.com.tr](https://www.denet.com.tr/vergi/dosyalar/kdv6/KDV6-30.pdf) · [Zayi/fire KDV — alomaliye](https://www.alomaliye.com/2022/02/24/zayi-olan-kaybolan-calinan-fireye-tabi-olan-degeri-dusen-mallarin-kdv-durumu/)
