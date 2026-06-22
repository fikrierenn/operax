# Plan 44 — Stock Consume Primitive (Oversell/Concurrency Go-Live Blocker)

**Tarih:** 2026-06-22
**Durum:** Onay bekliyor
**Tier:** 3 (ledger çekirdeği — yeni SP pattern + schema + 5 SP rewire + concurrency)
**Paket:** V1 (go-live blocker)
**Direktif:** EXECUTION-FIRST — Stock Movement Engine SAP-seviyesi güvenilirlik = birinci öncelik.

---

## 1. Problem (go-live engeli)

**Operax mevcut kodla ilk ücretli müşteride canlıya ALINAMAZ.** Tek başına yeten blocker: eşzamanlı iki çıkış (sevkiyat/sarf) aynı item/bin stoğunu birlikte tüketip **negatif stok** yazabilir. Stok doğruluğu WMS'in tek satış vaadi — bunsuz ürün satılamaz.

## 2. Kök neden

Çıkış yolları ortak, kilitli, atomik-yeterlilik-kontrollü bir `consume` primitive'i kullanmıyor. Her posting SP'si kendi `INSERT INTO StockMovement` ISSUE'sunu atıyor; ne mevcut bakiyeyi kilitliyor ne kontrol ediyor.

## 3. Kod kanıtı (canlıdan doğrulandı, commit 30b0a66)

| Risk | Kanıt | Kanıt notu |
|---|---|---|
| **CRITICAL** | `db_objects_starter.sql` · `sp_ShippingPost` :1750-1761 | `ShippingLine`'dan doğrudan `-QtyBase` ISSUE INSERT; **bakiye sorgusu/guard YOK**. Tek kilit `ShippingHeader (UPDLOCK)` :1738 → sadece aynı belge; item/bin envanteri kilitsiz. İki shipment paralel oversell yapar. |
| **HIGH** | `db_objects_materialissue.sql` · `sp_MaterialIssuePost` :45-89 | Depo toplamı kontrol, sonra tek "en yüksek bakiyeli" bin; bin negatife düşebilir. |
| **HIGH** | `schema_all.sql` · `StockMovement` :537-567 | `SourceDocId` normal index; **unique idempotency anahtarı + SourceLineId YOK** → tekrar/retry = çift hareket. |
| **HIGH** | `schema_M11_LedgerIntegrity.sql` · `tr_GuardPeriod_StockMovement` :177-186 | Dönem guard yalnız AFTER INSERT; UPDATE şema-seviyesi kapalı değil → yetkisiz UPDATE dönem izini bozabilir. |

Temel sağlam (audit Low): `vw_InventoryBalance` perpetual-ledger toplamından türüyor, IsCancelled dışlıyor; `sp_GuardPeriodOpen` OPEN/CLOSED/LOCKED + override log güçlü. **Yani defter doğru, eksik olan atomik availability/allocation katmanı.**

## 4. Çözüm — `sp_ConsumeInventory` primitive

Tek atomik nokta: tüm stok ÇIKIŞLARI buradan geçer.

```
sp_ConsumeInventory
  @CompanyId, @WarehouseId, @BinId (NULL=otomatik), @ItemId, @UomId,
  @QtyBase, @LotNo NULL, @SerialNo NULL,
  @SourceDocType, @SourceDocId, @SourceLineId, @SourceDocNo,
  @UnitCost, @UserId, @MovementDate NULL, @BranchId
```

İç akış (atomik):
1. `SELECT @bal = ISNULL(SUM(QtyBase),0) FROM StockMovement WITH (UPDLOCK, HOLDLOCK)
    WHERE CompanyId=@CompanyId AND ItemId=@ItemId AND WarehouseId=@WarehouseId
      AND (@BinId IS NULL OR BinId=@BinId) AND (@LotNo IS NULL OR LotNo=@LotNo)
      AND IsCancelled=0;`
   → `UPDLOCK,HOLDLOCK` + uygun index = key-range kilit; eşzamanlı consumer'lar serialize olur (check-then-insert atomik).
2. `IF @bal < @QtyBase THROW 53001, N'Yetersiz stok: <item/bin>', 1;`
3. `INSERT INTO StockMovement (... -@QtyBase 'ISSUE' ... @SourceLineId ...)`.
   Idempotency: filtered unique index `(SourceDocType,SourceDocId,SourceLineId,MovementType) WHERE SourceLineId IS NOT NULL AND IsCancelled=0` → çift insert THROW.

Caller (sp_ShippingPost vb.) artık ham INSERT yerine satır başına `EXEC sp_ConsumeInventory`. Reversal/cancel mevcut IsCancelled-flag mekanizmasında kalır (Plan 14 simetrisi bozulmaz).

## 5. Alternatifler (reddedilen)

- **A: Her SP'ye ayrı ayrı `IF (SELECT SUM...) < qty THROW` guard ekle.** RED: 5 SP'de tekrar + tutarsız kilit sırası → deadlock; idempotency yine yok. Tek primitive DRY + tek lock-order standardı.
- **B: Snapshot stok tablosu (`ItemStock.OnHand`) + `UPDATE ... WHERE OnHand>=qty`.** RED: perpetual-ledger'dan snapshot'a kayış mevcut doğru mimariyi (bakiye=SUM) kırar, drift riski. Ledger üstünde UPDLOCK,HOLDLOCK yeterli.
- **C: Uygulama katmanında (C#) kilit.** RED: SQL-first ihlali + çok-instance'da işe yaramaz. Kilit DB'de olmalı.

## 6. Riskler

| Risk | Etki | Olasılık | Mitigation |
|---|---|---|---|
| Yanlış kilit sırası → deadlock | yüksek | orta | Tek primitive = tek lock-order; tüm caller aynı sırada çağırır; Faz 6 deadlock-graph testi |
| HOLDLOCK key-range için index yetersiz → tablo/sayfa kilidi (perf) | yüksek | orta | `StockMovement(CompanyId,ItemId,WarehouseId,BinId) WHERE IsCancelled=0` index doğrula/ekle; SARGable predikat |
| Idempotency index mevcut NULL SourceLineId satırlarıyla çakışır | orta | düşük | Filtered index `WHERE SourceLineId IS NOT NULL` — tarihsel satırlar hariç |
| 5 SP rewire regresyon | yüksek | orta | Faz faz, her SP sonrası reversal-simetri + 0-ihlal smoke + sql-sp-reviewer |
| Reversal/IsCancelled bakiye etkisi | yüksek | düşük | Primitive yalnız ISSUE yazar; cancel mevcut flag SP'sinde; net bakiye=0 doğrula |

## 7. Done Criteria

- [ ] `sp_ConsumeInventory` (UPDLOCK,HOLDLOCK + yeterlilik THROW + idempotency)
- [ ] `StockMovement.SourceLineId` kolonu + filtered unique idempotency index (idempotent migration)
- [ ] Destek index doğrulandı (key-range kilit SARGable)
- [ ] 5 çıkış SP'si primitive'e bağlı (shipping pilot → material-issue → picking → production-consume → transfer)
- [ ] StockMovement UPDATE/DELETE deny guard (yalnız reversal SP)
- [ ] build 0/0 + sql-sp-reviewer (her faz) + reversal net-0 smoke
- [ ] Concurrency harness: 20-50 worker aynı item/bin → 0 oversell, deadlock-graph temiz
- [ ] Negatif smoke: yetersiz stok çıkışı THROW; çift-post idempotency THROW

## 8. Rollback

Faz bazlı commit. SP'ler `CREATE OR ALTER` → önceki sürüme revert. Schema (SourceLineId + index) additive (drop güvenli, veri silmez). Ledger verisi değişmez (yalnız yeni guard).

## 9. Fazlar (efor)

- **Faz 1 ✅ BİTTİ 2026-06-22 — CRITICAL kapandı:** `sp_ConsumeInventory` (UPDLOCK+HOLDLOCK düz-eşitlik key-range, @BinId zorunlu, LotNo IF-branch) + SourceLineId + 2 index + `sp_ShippingPost` rewire. build 0/0 (Web+Cli), sql-sp-reviewer 2 CRIT (OR-predikat key-range + BinId NOT NULL çelişki) → düzeltildi. Smoke 4/4: oversell THROW · normal · idempotency THROW · bin-guard. **E2E: gerçek boş-bin sevkiyatı engellendi (Mevcut:0 İstenen:50, DRAFT kaldı) — eski kod bin'i -50'ye düşürürdü. Canlı engeli kalktı.**
  - **Borç (sonraki faz):** IMP-1 `tvf_InventoryBalance` UomId-grain'i consume taban-birim toplamıyla tutarsız (ayrı fix). IMP-3 race'te ikinci insert ham 2627 (53002'ye çevrilmiyor, PageModel generic gösterir; düşük).
  - **Demo-veri bulgusu:** bazı sevkiyat satırları boş bin'e işaret ediyor (eski kör-oversell maskelemiş) — yeni kod doğru reddediyor; demo seed düzeltilmeli (kod değil).
- **Faz 2 ✅ BİTTİ 2026-06-22:** material-issue + transfer-out → primitive. Primitive `@MovementType` ile genelleştirildi (ISSUE/TRANSFER). **picking (sp_PickConfirm) StockMovement YAZMIYOR** (mantıksal işaret, gerçek çıkış shipping'de) → kapsam dışı. **production-consume DEAD/WIP** → ayrı karar. sql-sp-reviewer 0 CRIT/HIGH (reversal simetrisi + transfer out/in idempotency + lock-order doğrulandı). Smoke: TRANSFER consume + idempotency. IMP-1 (bin seçimi UoM-grain) düzeltildi.
  - **Borç (Faz 3):** IMP-2 — material-issue depo-toplam pre-guard (warehouse SUM) ile consume tek-bin kontrolü çelişir → stok çok bin'e dağılmışsa false-reject (split-pick yok). Multi-bin split allocation (Faz 3) çözer. Şimdilik oversell YOK, sadece dağıtık-stokta onaylanamama.
- **Faz 3 (~2g):** multi-bin split allocation + deterministik FEFO/FIFO sırası.
- **Faz 4 (~1g):** StockMovement UPDATE/DELETE deny (ledger immutability tam kapanış).
- **Faz 5 (~1g):** concurrency harness (Operax.Cli komutu, 20-50 paralel) + lock-order standardı dokümante.

## 10. 5 Lens
- 🔴 **Contrarian:** Fatal flaw = HOLDLOCK index yoksa perf çöker → Faz 1'de index doğrulama ilk adım.
- 🔵 **First Principles:** Stok çıkışı = "kontrol et + düş" tek atomik işlem; ayrıştığı an oversell. Primitive bunu fiziksel olarak tek yapar.
- 🟢 **Expansionist:** Primitive + idempotency = gelecek Logo/Mikro/e-ticaret entegrasyon retry'larına da güvenli zemin (kabul edilen gerçek #3+#4 köprüsü).
- ⚪ **Outsider:** "WMS oversell yapıyor mu?" — ilk depo gününde yakalanır; en utandırıcı bug.
- 🟡 **Executor:** Pazartesi — index doğrula, primitive yaz, shipping bağla, 30 worker test.

## 11. İlişkili
- `.claude/rules/document-immutability.md` §1.b (ledger append-only, IsCancelled) · `.claude/rules/sql-conventions.md` (XACT_ABORT/THROW/SARGable) · `.claude/rules/phase-review-gate.md` (her faz sql-sp-reviewer+smoke).
- Kapsam DIŞI (direktif): Profitability Engine yeni tabloları, MRP/APS/MES, genel muhasebe.
