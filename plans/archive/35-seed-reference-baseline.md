# Plan 35 — Baseline Referans Tanımı Seed (Fresh-Install Operable)

> Tier 3 · Kaynak: `referans-tanim-seed` skill + `docs/reference/REFERENCE_INVENTORY.md` P0 · Branch: bu branch (plan33 sonrası)

## Problem

Fresh `migrate`+`seed` sonrası sistem **operable değil**. Canlı DB kanıtı (2026-06-19):

| Şirket | Dict durumu |
|---|---|
| `00000000` (sistem) | MOV_TYPE 0, STATUS 7, TAX_RATE 8 (dup!), UOM 0 |
| `d1e1…0001` (demo) | UOM 3, başka hiçbir tip yok |

- **TAX_RATE** yalnız sistem şirketinde (`setup_tax_dictionary.sql` hardcoded `00000000`) — gerçek şirkete kopyalanmıyor.
- **UOM** boş/tutarsız (commented seed bloğu, schema_M01_UOM.sql).
- **CURRENCY / PAYMENT_METHOD / PAYMENT_TERM / WITHHOLDING / ACCOUNT_TYPE / TRANSACTION_TYPE / PARTNER_CATEGORY** = hiç yok; bir kısmı C# magic-string (architecture §3 ihlali).
- **UN/ECE birim kodu** yok → e-Belge bloklayıcı (Adet=C62 zorunlu).

## Scope

Çekirdek (her kurulum) baseline referansların idempotent + per-company seed'i + taşıyıcı şema + magic-string sabitleştirme.

**Kapsam içi:**
1. `DictionaryValue`'ya nullable `UnEceCode NVARCHAR(10)` + `IsWholeNumber BIT` (yalnız UOM doldurur) — onaylandı.
2. `seed_reference.sql` — per-company idempotent: UOM(+UnEce/IsWholeNumber), TAX_RATE (her şirkete), CURRENCY, PAYMENT_METHOD, PAYMENT_TERM, WITHHOLDING (601-625), ACCOUNT_TYPE, TRANSACTION_TYPE, PARTNER_CATEGORY.
3. `Dtos.cs` — `PaymentMethod`, `AccountType`, `TransactionType` sabit sınıfları + dağınık magic-string çağrı yerleri (Finance) refactor — onaylandı.
4. Wire: schema addon → `migrate` array; `seed_reference.sql` → `seed` array (setup_tax'tan sonra, demo'dan önce).

**Kapsam dışı:** Demo/işlem verisi (demo-veri-uret). Sektörel dict (P2). Tekstil varyant / gıda lot-FEFO (yapısal, ayrı plan).

## Alternatifler (reddedilen)

- **Ayrı `Uom` tablosu** (kategori+factor): yapısal büyük değişiklik, P0 için aşırı. → nullable kolon yeterli.
- **UN/ECE'yi ertele**: e-Fatura sonra blocker; envanter zaten C62'yi doğruladı, şimdi koymak ucuz. → koy.
- **Magic-string'i ertele**: kullanıcı "şimdi taşı" dedi; dict değeri + sabit aynı anda tutarlı olur. → şimdi.

## Riskler

- 🔴 Magic-string refactor Finance'te regresyon — sabit değer DB string'iyle **birebir aynı** kalmalı (SQL eşleşmesi bozulmaz). Faz 3 build+code-review+smoke zorunlu.
- 🟡 `setup_tax_dictionary.sql` ile çakışma — seed_reference idempotent `WHERE NOT EXISTS`, TAX'ı ezmez; ama TAX_RATE dup (8 satır) temizlenmeli.
- 🟡 ALTER ADD COLUMN sonrası `GO` (batch — DocChain dersi).

## Done Criteria ✅ (2026-06-20 — tüm fazlar tamam)

- [x] ✅ Fresh `Operax_Test` migrate+seed → her şirkette: UOM≥12 (UnEce dolu, Adet=C62), TAX_RATE=4, CURRENCY=6, PAYMENT_METHOD=7, WITHHOLDING=25. (Demo Ltd + SYSTEM doğrulandı)
- [x] ✅ Build Web 0 hata (Cli 0/0). seed_reference fresh DB'de ok:12 0 fail.
- [x] ✅ `seed` re-run idempotent (re-run sonrası değer artmadı).
- [x] ✅ Dtos sabitleri (AccountType/TransactionType/PaymentMethod) kullanımda; Finance C# magic-string kaldı: yok. code-reviewer DB eşleşme %100.

**Faz özeti:** Faz 1 schema_M00_DictRefCols (UnEce+IsWholeNumber) · Faz 2 seed_reference.sql 9 tip per-company (sql-sp-reviewer CRIT-1 FK-repoint + XACT_ABORT fix) · Faz 3 Dtos 3 sabit + Finance refactor · Faz 4 fresh Operax_Test doğrulama.

**Kapsam dışı bulgu (ayrı task task_c6b83089):** fresh-install migrate'te 2 pre-existing bug — db_objects_pricelist_bulk STRING_SPLIT ordinal (compat<160) + seed_demo Warehouse FK sırası.

## Rollback

- Seed: idempotent veri; geri alma = `DELETE DictionaryValue WHERE TypeId IN (referans tipleri)` (gerekmez).
- Schema: nullable kolon, zararsız bırakılabilir.
- Dtos refactor: tek commit, `git revert`.

## Adımlar (fazlar — her faz kapanış kapısı)

- **Faz 1 — Şema:** `schema_M00_DictRefCols.sql` (ALTER DictionaryValue ADD UnEceCode, IsWholeNumber + GO). migrate array wire. → build-validator.
- **Faz 2 — Seed:** `seed_reference.sql` per-company idempotent (yukarı 9 tip). seed array wire. TAX dup temizliği. → sql-sp-reviewer (idempotency/per-company) + fresh smoke.
- **Faz 3 — Dtos sabit + refactor:** `Dtos.cs` 3 sabit sınıf + Finance çağrı yeri refactor. → build + code-reviewer + smoke.
- **Faz 4 — Doğrula:** fresh Operax_Test migrate+seed → operable assert (done criteria query).

## 5 Lens

- 🔴 **Contrarian:** Magic-string refactor blocker değil — fatal flaw: gereksiz regresyon riski açılış-kritik işle karışıyor. Mitigasyon: ayrı faz, kendi kapısı.
- 🔵 **First Principles:** Asıl soru "veri var mı" — sabitler ikincil; Faz 1-2 tek başına fresh-install'ı kurtarır.
- 🟢 **Expansionist:** Aynı taşıyıcı sektörel dict + UDF'i de besler — şimdi çekirdek doğru kurulursa P2 ucuzlar.
- ⚪ **Outsider:** "Neden TAX yalnız bir şirkette?" — yabancı gözle absürt; per-company seed temel beklenti.
- 🟡 **Executor:** Pazartesi: Faz 1 ALTER yaz+migrate, Faz 2 seed üret+test DB doğrula.
