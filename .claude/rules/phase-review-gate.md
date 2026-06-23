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

### 3.5 Fresh-DB Migrate Testi (migrate listesi / schema / db_objects değiştiyse) — ZORUNLU RİTÜEL

**Neden:** Dev DB'de objeler tarihsel olarak elle uygulanmış olabilir → `migrate` listesi eksik olsa bile dev ÇALIŞIR ama **temiz müşteri kurulumu PATLAR** (single-tenant fresh install). Mevcut DB'ye `migrate` koşmak bu farkı GİZLER. Tek güvenilir kanıt: sıfırdan boş DB.

**Recipe (atlanamaz):**
```bash
# 1. Sunucu adını al
operax-cli query "SELECT @@SERVERNAME"
# 2. Tek-kullanımlık boş DB
operax-cli query "IF DB_ID('Operax_FreshTest') IS NOT NULL BEGIN ALTER DATABASE Operax_FreshTest SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE Operax_FreshTest; END; CREATE DATABASE Operax_FreshTest"
# 3. OPERAX_CONN ile o DB'ye migrate (CLI env override)
OPERAX_CONN="Server=<srv>;Database=Operax_FreshTest;Integrated Security=True;TrustServerCertificate=True" operax-cli migrate   # → 0 fail ŞART
# 4. Beklenen objeleri DOĞRULA (OBJECT_ID NULL değil) + çakışan view/SP doğru sürüm mü (ayırt edici kolon)
# 5. DROP DATABASE Operax_FreshTest (SINGLE_USER ile)
```
- **0 fail + beklenen tüm obje mevcut + çift-tanım objeler canonical sürüm** olmadan faz kapanmaz.
- Patladıysa: "neresi patladı" = ilk fail eden script → eksik bağımlılık/sıra. Düzelt, tekrar.

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
| `db_objects_reversal.sql` migrate listesinde yoktu | Smoke (SP canlıya gitmemişti) | fresh-DB migrate testi (§3.5) |
| Reconciliation 6 dosyası migrate'te yoktu (Plan 48) — dev elle-uygulanmış, fresh install kırık | Plan 47 ölü-nesne taraması | fresh-DB migrate testi (§3.5) |
| PO/SO tvf APPROVED→POSTED | CLI query | code-reviewer (DocStatus sabiti) |

## Kısayol (paralel çalıştır)

Bağımsız kontroller tek mesajda paralel Agent çağrısıyla çalıştırılır:
```
code-reviewer (sonnet) + sql-sp-reviewer (opus) + security-reviewer (opus)  → paralel
build-validator (haiku)                                                        → önce
fresh-DB migrate testi (§3.5)                                                  → migrate/schema/db_objects değiştiyse
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
