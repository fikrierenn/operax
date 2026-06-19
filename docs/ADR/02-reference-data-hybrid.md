# ADR-02 — Referans Veri: Hibrit (Kod-Çıpalı + Dict-Dinamik)

**Tarih:** 2026-06-19
**Durum:** Kabul edildi
**Bağlam kaynağı:** `docs/reference/REFERENCE_INVENTORY.md` (referans-tanim-seed envanteri) + extension/customization tartışması.

## Bağlam

Operax referans tanımları (statü, hareket/evrak tipi, birim, KDV, döviz, ödeme şekli, marka, sektörel öznitelik) iki uçta yönetilebilir:
- **Tamamen kod sabiti** (`Dtos.cs`): kullanıcı genişletemez, her değişiklik yeni sürüm.
- **Tamamen dinamik** (DictionaryValue): kullanıcı her şeyi ekler/siler/yeniden adlandırır.

Tamamen dinamik **tehlikeli**: SP'ler ve C# bazı değerlere göre dallanıyor (`sp_ValidateStatusTransition`, Post SP'leri, maliyet/ledger). Kullanıcı "POSTED"i silse/yeniden kodlasa → SP kırılır, stok/ledger bozulur. Tamamen sabit ise tek-pazar/sektör esnekliği ölür (UDF dersi, Plan 34).

## Karar

**Hibrit — referans tipini "iş-mantığı dallandırıyor mu" sorusuyla ayır:**

### 1. Kod-çıpalı (CODE immutable; `Dtos.cs` sabiti)
Mantık-dallandıran, durum-makinesi taşıyan tipler. SP/C# bunların **CODE**'una göre `IF/CASE` yapar.
- `DocStatus`, `MovementType`, `SourceDoc`, `CHEQUE_STATUS`, `PriceDirection`, `PartnerType`, `DocPrefix`.
- **Sınır vaka — şu an hardcoded, `Dtos.cs`'e TAŞINMALI:** `ACCOUNT_TYPE`, `TRANSACTION_TYPE` (SP/UI dallanıyor ama sabit değil → magic-string ihlali, `architecture §3`).
- CODE değişmez; **label/Türkçe ad dict'te tutulabilir** (kullanıcı görüneni değiştirir, kod identity'si sabit kalır).
- Akış (geçişler) **data-driven kalır:** `StatusTransition` tablosu — yeni geçiş = veri, kod değil. Sistem bu yönüyle zaten yarı-dinamik.
- Yeni custom değer eklemek = SP-dallanması gerektiriyorsa **kod ister** (kaçınılmaz, kabul edilir).

### 2. Dict-dinamik (DictionaryValue; kullanıcı tam yönetir)
Mantık-dallandırmayan, kullanıcı-genişletilebilir referans. Kod bunları **runtime'da dict'ten okur**, dallanmaz.
- `UOM`, `TAX_RATE`, `CURRENCY`, `PAYMENT_METHOD`, `PAYMENT_TERM`, `PARTNER_CATEGORY`, `WITHHOLDING`, `BRAND`.
- Tek doğruluk kaynağı = dict tabloları (seed'li, CompanyId-scoped, `IsSystem` flag).
- Sektörel öznitelikler (tekstil Beden/Renk, gıda Alerjen, kitap Yayınevi) = DictionaryType + **UDF** (Plan 34) — çekirdek şemaya kolon eklemeden.

## Sonuçlar

- **Seed:** çekirdek referans (kod-çıpalı tiplerin label'ı + dict-dinamik tipler) `seed_reference.sql` ile her şirkete idempotent seed edilir (P0 — REFERENCE_INVENTORY). Hardcoded FK-Id yok (FBUG-4 dersi).
- **Kod:** dict-dinamik değerlere kod CODE ile değil, kullanıcı-seçimiyle (FK/CODE) bağlanır; sabit değere göre dallanma yok.
- **Magic-string kapatma:** ACCOUNT_TYPE/TRANSACTION_TYPE/CURRENCY/PAYMENT_METHOD kodları `Dtos.cs`'e (kod-çıpalı) veya dict'e (dinamik) — dağınık hardcoded string biter.
- **Genişletme:** yeni dict değeri otomatik UI/sorguya gelir; yeni davranış (SP-dallanma) plan + kod ister. Extension katmanı (event/hook, UDF) bunun üstüne (bkz. müşteri-özel-ekran araştırması).
- **Tehlike önlendi:** ledger-kritik CODE'lar kullanıcı eliyle bozulamaz.

## İlişkili
- `docs/reference/REFERENCE_INVENTORY.md` — tip-tip envanter + hangi katman.
- `.claude/skills/referans-tanim-seed/SKILL.md` — bu kararı uygulayan seed üretici.
- `src/Operax.Web/Lib/Dtos.cs` — kod-çıpalı sabitler.
- `.claude/rules/architecture.md §3` (magic-string yasağı), `§8` (UDF) · `.claude/rules/footprint-ladder.md`.
- ADR-01 (ledger clustered key).
