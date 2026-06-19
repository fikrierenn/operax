---
name: demo-veri-uret
description: Operax kurulumuna gerçekçi, FK-tutarlı, ledger-doğru DEMO/TEST verisi basar (ürün/cari master + sipariş→malkabul→fatura zincirleri + stok hareketleri). Sektörel (tekstil/kitap/gıda) + hacim parametreli. "demo veri üret", "test verisi oluştur", "örnek veri", "fixture", "demo data", "boş kuruluma veri bas", "/demo-veri-uret" denildiğinde tetiklenir. Gerçek şirketi bozmaz (ayrı demo CompanyId), idempotent + temizlenebilir. demo-data-builder agent'ını orkestre eder; ledger Post SP'lerinden geçer.
allowed-tools: Read, Grep, Glob, Bash, Agent, AskUserQuestion
user-invocable: true
model: inherit
---

# demo-veri-uret — Demo/Test Verisi Üretimi (orkestratör)

> Boş/az-veri Operax kurulumuna gerçekçi demo veri basar. `demo-data-builder` agent'ı seed SQL üretir; bu skill netleştirir, uygular, doğrular, temizleme yolunu bırakır. Mevcut **statik** seed (`operax-cli seed` + `seed_*.sql`) yerine **dinamik/parametrik** üretim.

## 0. Footprint kontrolü
Kullanıcı sadece "biraz daha sabit veri" istiyorsa → yeni üretim değil, `docs/sql/seed_demo.sql`'i genişlet (daha dar). Bu skill: parametrik/sektörel/hacimli/zincirli üretim gerektiğinde.

## 1. Netleştir (AskUserQuestion ile)
- **Hedef şirket:** demo/test CompanyId (varsayılan test `00000000-...`). **Gerçek şirkete (d1e1b1a5) ASLA** — kullanıcı gerçek şirket derse uyar + ayrı demo şirket öner.
- **Sektör:** tekstil (Beden/Renk) · kitap (ISBN/yazar) · gıda (SKT/lot) · genel.
- **Hacim:** kaç ürün / cari / belge zinciri (örn. 20 ürün, 10 cari, 5 PO→Receiving, 5 SO→Shipping).
- **Zincir derinliği:** sadece master mı, yoksa onaylı belge + stok hareketi de mi (ledger).

## 2. Mevcut durum doğrula (CANLI)
- Hedef demo CompanyId var mı (`operax-cli query`); yoksa oluştur veya test şirketini kullan.
- Çakışma: o şirkette zaten `DEMO-%` kayıt var mı (idempotent temizlik gerekecek).
- DictionaryValue UOM/Category mevcut mu (FK için).

## 3. demo-data-builder agent'ını çağır
```
subagent_type: demo-data-builder · model: sonnet
prompt: sektör + hacim + demo CompanyId + zincir derinliği. Şemayı canlı oku, idempotent seed SQL üret
(temizlik DELETE → master INSERT → DRAFT belge → EXEC Post SP), ledger SP'den geçir, gerçek şirkete yazma.
```
Agent seed SQL + özet + uygula/temizle komutları döndürür.

## 4. Gözden geçir + uygula
- Dönen SQL'i OKU: hedef CompanyId demo mu? RAW StockMovement INSERT var mı (olmamalı — SP'den geçmeli)? `DEMO-` tag + temizlik DELETE var mı? FK sırası doğru mu?
- Şüpheli/gerçek-şirket riski varsa **UYGULAMA**, kullanıcıya göster.
- Temiz ise: `docs/sql/seed_demo_<sektor>.sql`'e yaz → `operax-cli script docs/sql/seed_demo_<sektor>.sql`.

## 5. Doğrula (smoke)
- Sayı: üretilen Item/Partner/belge adedi beklenenle uyuşuyor mu (`operax-cli query COUNT`).
- Ledger: onaylı belge varsa `tvf_InventoryBalance(@DemoCompany)` bakiye tutarlı mı, dönem guard THROW etmedi mi.
- Cari: AccountMovement bakiyesi (varsa) tutarlı mı.

## 6. Temizleme yolu bırak
- Üretilen script başındaki `DELETE ... WHERE Code/DocNo LIKE 'DEMO-%' AND CompanyId=@DemoCompany` re-run'da temizler.
- Kullanıcıya: "demo veriyi sil" → aynı DELETE'i çalıştır (veya script'i `--cleanup` modunda).

## Guardrails
- ❌ Gerçek şirket CompanyId'sine demo veri (veri kirliliği). Şüphede sor.
- ❌ StockMovement/AccountMovement RAW INSERT (ledger/maliyet/dönem bozulur — Post SP zorunlu).
- ❌ PageModel/cshtml'e hardcoded demo veri (`ui-standard §1.5`) — seed SQL meşru tek yer.
- ❌ Onaysız uygulama — SQL önce gözden geçirilir.
- ✅ Idempotent (DEMO- tag + temizlik DELETE), demo-CompanyId-kapsamlı, ledger SP'den, doğrulanmış.

## İlişkili
- `.claude/agents/demo-data-builder.md` — üretim işçisi (bu skill onu orkestre eder).
- `.claude/rules/architecture.md §4` (Post SP atomik) · `document-immutability.md` (ledger append-only) · `ui-standard.md §1.5` (sıfır hardcoded veri) · `sql-conventions.md` (zorunlu kolon).
- `docs/sql/seed_*.sql` — mevcut statik seed; `operax-cli script/seed`.
