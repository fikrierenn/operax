# Faz Kapanış Kontrol Kapısı (Phase Review Gate)

Her Tier 3 planın her fazı bitmeden commit atılmaz. Aşağıdaki zincir **sırayla** çalıştırılır.

## Zorunlu Kontrol Zinciri

### 1. Build (her faz)
```
Agent: build-validator | model: haiku
```
- 0 hata, 0 uyarı şart. Tek hata varsa commit yok.

### 2. Kod İnceleme (her faz)
```
Agent: code-reviewer | model: sonnet
```
- Türkçe yorum eksikliği, 80 satır aşımı, guard clause, magic string, CompanyId filtresi.
- HIGH/CRITICAL bulgu varsa → düzelt, tekrar build.

### 3. SQL/SP İnceleme (SP veya şema değiştiyse)
```
Agent: sql-sp-reviewer | model: opus
```
- Transaction atomikliği (SET XACT_ABORT + BEGIN/COMMIT/ROLLBACK).
- THROW kod aralığı (50000-59999).
- Ledger tutarlılığı (StockMovement↔AccountMovement).
- Immutability/reversal (flag-only vs ters-satır karışmamalı).
- CompanyId predikası, SARGable WHERE.
- CRITICAL bulgu varsa → düzelt, tekrar build.

### 4. Güvenlik İnceleme (yeni PageModel veya SP varsa)
```
Agent: security-reviewer | model: opus
```
- SQL injection, IDOR, mass assignment, secret leakage, open redirect.
- Confidence ≥ 80 kritik bulgu varsa → düzelt.

### 5. Manuel Smoke (stok/ledger hareketi olan her faz)
- Gerçek POSTED kayıt üzerinde uçtan uca test.
- SP sonrası: StockMovement bakiyesi doğrula (tvf_InventoryBalance).
- Reversal varsa: net bakiye = 0 doğrula.
- **Smoke atlanamaz** — build 0/0 smoke'un yerini tutmaz.

## Geçmiş Dersler (neden zorunlu)

| Olay | Nasıl yakalandı | Erken yakalansaydı |
|---|---|---|
| `sp_ShippingPost` SHIPMENT→SHIPPING | E2E smoke | sql-sp-reviewer |
| Reversal çift-sayım (+100 stok) | Smoke | sql-sp-reviewer (flag+ters-satır çakışması) |
| `db_objects_reversal.sql` migrate listesinde yoktu | Smoke (SP canlıya gitmemişti) | db-schema-checker |
| PO/SO tvf APPROVED→POSTED | CLI query | code-reviewer (DocStatus sabiti) |

## Kısayol (paralel çalıştır)

Bağımsız kontroller tek mesajda paralel Agent çağrısıyla çalıştırılır:
```
code-reviewer (sonnet) + sql-sp-reviewer (opus) + security-reviewer (opus)  → paralel
build-validator (haiku)                                                        → önce
smoke                                                                          → en son
```

## İstisnalar

- **Tier 1/2 iş:** build-validator yeterli (reviewer'lar opsiyonel).
- **Sadece dokümantasyon/plan değişikliği:** hiçbiri zorunlu değil.
- **Kullanıcı "hızlıca geç" derse:** atlanan kontrol commit mesajına `[review-skipped: <gerekçe>]` notu + TODO.md'ye borç satırı.

## İlişkili

- `.claude/rules/plan-first.md` — Tier sistemi
- `.claude/rules/test-discipline.md` — test koşumu
- `.claude/rules/sql-conventions.md` — SP standartları
- `.claude/rules/document-immutability.md` — reversal mekanizması
