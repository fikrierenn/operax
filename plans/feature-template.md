# Plan NN — <kısa başlık>

> Bu şablon Tier 3 işler içindir. Tier 1 (yok) ve Tier 2 (TODO satırı) için kullanma.

**Tarih:** YYYY-MM-DD
**Yazan:** Fikri / Claude
**Durum:** `Taslak` | `Onaylandı` | `Uygulamada` | `Tamamlandı` | `İptal`
**Modül:** M00 / M01 / M02 / M03 / M04 / M11 / vb.
**Paket:** STARTER / WMS_PRO / MANUFACTURING / ENTERPRISE

---

## 1. Problem

Hangi sorunu çözüyoruz? Net 2-4 cümle. "Şunu yapacağız" değil, "şu problem var, şu yüzden çözmemiz lazım".

## 2. Scope

### Kapsam dahili
- ...

### Kapsam dışı
- ... (açıkça söyle, scope creep'i engelle)

### Etkilenen dosyalar (tahmin)
- `docs/sql/schema_M*.sql` — şema değişikliği
- `docs/sql/db_objects.sql` — SP/View
- `src/Operax.Web/Features/<X>/Index.cshtml` — UI
- `src/Operax.Web/Lib/*.cs` — backend

**Tahmini boyut:** N dosya / M satır.

## 3. Alternatifler

### A: <alternatif başlık>
**Açıklama:** ...
**Reddetme sebebi:** ...

### B: <alternatif başlık>
**Açıklama:** ...
**Reddetme sebebi:** ...

### C: <seçilen — hangisi yapılacak>
**Açıklama:** ...
**Sebep:** ...

> En az 2 alternatif reddedilmeli — yoksa "Tier 3 mü gerçekten?" sorgulanmalı. Tek yol varsa muhtemelen Tier 2.

**5 lens kontrolü:**
- 🔴 **Contrarian:** ...
- 🔵 **First Principles:** ...
- 🟢 **Expansionist:** ...
- ⚪ **Outsider:** ...
- 🟡 **Executor:** ...

## 4. Riskler

| Risk | Etki | Olasılık | Mitigation |
|---|---|---|---|
| ... | düşük/orta/yüksek | düşük/orta/yüksek | nasıl ele alınacak |

## 5. Done Criteria

Bu plan tamamlandı sayılması için:

- [ ] <Ölçülebilir kriter 1>
- [ ] <Ölçülebilir kriter 2>
- [ ] `operax-cli migrate` 0 hata
- [ ] `dotnet build` 0 hata 0 uyarı
- [ ] Test/doğrulama: ...
- [ ] Dokümantasyon: `docs/MODULE_SPECS/M<NN>_*.md` güncellendi (varsa)
- [ ] Rollback yolu test edildi (varsa)

## 6. Rollback Planı

Eğer bu değişiklik production'da sorun yaratırsa nasıl geri alınır?

- Git revert: `git revert <commit>` — uygun mu?
- DB migration varsa down script: ...
- Config rollback: ...

## 7. Adımlar / İçerdiği TODO maddeleri

1. [ ] **<ID>** Adım 1 — schema yaz
2. [ ] **<ID>** Adım 2 — SP yaz
3. [ ] **<ID>** Adım 3 — UI (Index)
4. [ ] **<ID>** Adım 4 — UI (Details)
5. [ ] **<ID>** Adım 5 — seed
6. [ ] **<ID>** Adım 6 — migrate + test
7. [ ] **<ID>** Adım 7 — dokümantasyon

> `docs/TODO.md`'ye de bu maddeleri ayrıca ekle, plan dosyası ve TODO senkron olsun.

## 8. İlişkili

- ADR: `docs/ADR/<NN>-<konu>.md` (varsa)
- Önceki plan: `plans/<NN-1>-<konu>.md` (varsa)
- Spec dokümanı: `docs/MODULE_SPECS/M<NN>_*.md`
- Konuşma referans: `docs/journal/YYYY-MM-DD.md`

## 9. Onay

> Kullanıcı onay verene kadar implement edilmez.

- [ ] Plan kullanıcıya gösterildi
- [ ] Geri bildirim alındı (varsa düzeltildi)
- [ ] Onay alındı: <tarih, kullanıcı imzası>
