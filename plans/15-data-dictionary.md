# Plan 15 — SQL Veri Sözlüğü (Data Dictionary)

**Tarih:** 2026-05-29
**Yazan:** Fikri / Claude
**Durum:** `Taslak`
**Modül:** M00 (Platform Core / Metadata)
**Paket:** STARTER

---

## 1. Problem

Şemada (50+ tablo, M00-M19) hangi tablonun ne işe yaradığını, hangi kolonun ne anlama geldiğini tutan **hiçbir yapı yok**. Üç somut acı:
1. **Kurumsal hafıza kaybı** — "QtyBase neydi, StockMovement neden hem CompanyId hem WarehouseId tutuyor" cevabı sadece kafalarda/dağınık schema dosyalarında.
2. **AI asistanı yakıtı (asıl sebep)** — Plan 07 (RAG + text-to-SQL). LLM'in doğru SQL üretmesi için kolonların İŞ ANLAMINI bilmesi şart. Ham "QtyBase" → halüsinasyon; "QtyBase = temel birime çevrilmiş miktar" → doğru sorgu. VISION Decision Layer altyapısı.
3. **Çoklu firma netliği** — hangi kolon firma izolasyonu için kritik (CompanyId), hangi tablo firma-bağımsız sistem verisi → güvenlik + denetim.

> **Karıştırma uyarısı:** Mevcut `Admin/Dictionary` (DictionaryType/DictionaryValue) = DRAFT/POSTED enum kod listeleri (Tr/En). Bu BAMBAŞKA. Ona dokunma. Biz tablo/kolon META verisi kuruyoruz.

## 2. Scope

### Kapsam dahili
- `docs/sql/schema_M00_Metadata.sql` — `MetaTable` + `MetaColumn` (idempotent)
- `sp_SyncMetadata` — sys.columns/tables/schemas okuyup EKSİK kolonları ekler; mevcut açıklamaları **asla ezmez** (INSERT…WHERE NOT EXISTS)
- `vMetaDrift` view — ORPHAN (meta'da var, şemada yok) + UNDOCUMENTED (şemada var, açıklama boş)
- `seed_metadata.sql` — ilk dalga gerçek açıklamalar (kritik tablolar)
- `.claude/rules` kuralı — yeni tablo/kolonda sp_SyncMetadata + DescriptionTr; vMetaDrift ORPHAN'da sprint kapanmaz

### Kapsam dışı
- `Admin/Dictionary` (enum kod sözlüğü) — dokunulmaz
- Admin UI (meta düzenleme ekranı) — ileride; bu fazda SQL + seed yeter
- AI/RAG entegrasyonu — Plan 07'nin işi (bu sözlüğü tüketir)

### Etkilenen dosyalar
- `docs/sql/schema_M00_Metadata.sql` (YENİ)
- `docs/sql/seed_metadata.sql` (YENİ)
- `src/Operax.Cli/Program.cs` (migrate + seed listesine kayıt)
- `.claude/rules/data-dictionary.md` (YENİ kural)

## 3. ADR — Bilinçli Konvansiyon İhlalleri

Operax kuralı "her tabloda CompanyId + GUID PK + IsDeleted". Burada **kasıten üçünü de ihlal** ediyoruz — bu sistem metadatası:
- **CompanyId YOK** → bir kolonun anlamı 5 firmada da aynıdır; firma-bağımsızdır.
- **GUID/NEWID PK YOK** → doğal anahtar (Schema+Table+Column); küçük statik tablo, idempotent upsert bedava, fragmentasyon yok.
- **IsDeleted YOK** → gerekmiyor; sp_SyncMetadata + vMetaDrift drift'i yönetir.

> Bu ADR sonradan "kural ihlali" diye geri alınmasın diye buraya yazıldı.

## 4. Alternatifler
- **A: Sadece kod yorumu/markdown doküman** — Reddedildi: sorgulanamaz, AI tüketemez, şemayla senkron kalmaz (çürür).
- **B: Extended properties (sys.sp_addextendedproperty)** — Reddedildi: Dapper'dan sorgulamak zahmetli, çok dilli (Tr/En) zayıf, drift görünürlüğü yok.
- **C (seçilen): Ayrı MetaTable/MetaColumn + sync SP + drift view** — Sorgulanabilir (AI/RAG join), Tr/En, idempotent sync, drift kontrolü.

**5 lens:**
- 🔴 Contrarian: Sözlük çürürse zararlı (yanlış bilgi). → vMetaDrift + sprint-kapanış kuralı bunu engeller.
- 🔵 First Principles: Gerçek ihtiyaç "kolonun iş anlamı"; sync + elle açıklama tam bunu verir.
- 🟢 Expansionist: Aynı meta ileride Admin UI + AI RAG + otomatik doküman üretir.
- ⚪ Outsider: "Neden iki sözlük var?" → isim benzerliği; Admin/Dictionary=enum, Meta*=şema. Net ayrım şart.
- 🟡 Executor: schema + sp + ilk dalga seed + drift sorgusu = bir oturum.

## 5. Done Criteria
- [ ] MetaTable/MetaColumn idempotent oluşur
- [ ] sp_SyncMetadata 2× çalışınca elle açıklamaları ezmez (yalnız yeni ekler)
- [ ] vMetaDrift: ORPHAN=0; UNDOCUMENTED=ilk dalga dışı (normal)
- [ ] İlk dalga elle: StockMovement, InventoryBalance, AccountMovement, Receiving(+line), SalesOrder(+line), PurchaseOrder(+line), Item, Company, NumberSeries
- [ ] Açıklama kalitesi: kolon adı tekrarı değil, anlam+bağlam+ilişki (FK hedefi / statü makinesi / birim)
- [ ] `operax-cli migrate` 0 hata; `.claude/rules/data-dictionary.md` yazıldı

## 6. Açıklama Kalite Disiplini (zorunlu)
- KÖTÜ: `CompanyId → "Şirket Id"` · İYİ: `CompanyId → "Kaydın ait olduğu tüzel kişilik; 5 firmalı holdingte izolasyon anahtarı, her sorguda WHERE predikatı zorunlu"`
- `*Id` → hangi tabloya FK · `Status` → hangi statü makinesi · miktar → hangi birim
- seed YAZMADAN ÖNCE ilgili `schema_M*.sql` OKU — tahminle yazma, kolon/tip gerçek şemadan.

## 7. Adımlar
1. [ ] schema_M00_Metadata.sql (MetaTable + MetaColumn)
2. [ ] sp_SyncMetadata (INSERT…WHERE NOT EXISTS — tablo + kolon)
3. [ ] vMetaDrift view (ORPHAN + UNDOCUMENTED)
4. [ ] Cli migrate + seed listesine kayıt
5. [ ] seed_metadata.sql — ilk dalga (şema okuyarak)
6. [ ] .claude/rules/data-dictionary.md
7. [ ] migrate + sp_SyncMetadata + seed çalıştır → vMetaDrift doğrula (ORPHAN=0)

## 8. Onay
- [ ] Plan gösterildi · [ ] Geri bildirim · [ ] Onay: <tarih>
