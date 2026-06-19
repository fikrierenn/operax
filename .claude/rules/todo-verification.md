# TODO Doğrulama Disiplini

Kapsam: `docs/TODO.md`, `docs/BUGS.md`, `docs/journal/*`, `plans/`. Action almadan önce **canlı kod** ile karşılaştırılır.

## Mutlak Kurallar

1. **TODO listesi bilgi değildir, hipotez tahtasıdır.** "Açık" yazısı bugün açık olduğunu kanıtlamaz; yazıldığı tarihte açıktı. Bugün de açık olduğunu kanıtlamak senin işin.

2. **Action almadan önce file:line ile doğrula.** Madde `Receiving/Details.cshtml.cs:128 yetki kontrolü yok` diyorsa:
   - `Read` ile o satırı oku
   - `[Authorize]` / `if (CurrentUser ...)` / `Guard` gibi anahtar kelime var mı bak
   - Yoksa açık, varsa kapalı — kullanıcıya bildir, fix etme

3. **"hardening" / "fix" commit'leri kırmızı bayraktır.** `git log --grep='hardening\|fix(security)\|post-review'` çıkıyorsa eski TODO **muhtemelen stale**. Action öncesi sweep zorunlu.

4. **Fix'lemeden önce sweep, fix sırasında değil.** 10 madde fix'liyormuş gibi başlayıp 5'incide "bu zaten kapalı" demek maliyetli. Önce tüm 10'u paralel `Read`/`Grep` ile doğrula, sonra açık olanları sırayla fix et.

## Workflow

### Adım 1 — TODO maddesini oku
Madde için file:line referansı yoksa → reddet, kullanıcıdan net file:line iste.

### Adım 2 — Paralel doğrulama (tek mesajda)
HIGH/CRITICAL maddelerin tümü için **paralel** `Read` + `Grep` çağrısı:

```
Read(Receiving/Details.cshtml.cs:120-135)
Read(sp_ReceivingPost L 50-90)
Grep("UpdateItemCost", "docs/sql/")
...
```

### Adım 3 — Gerçek açık listesi çıkar

| # | İddia | Kod kanıtı | Durum |
|---|---|---|---|
| H1 | sp_ReceivingPost cost yansıtmıyor | `sp_UpdateItemCostMovingAvg` çağrısı YOK | ❌ AÇIK |
| H2 | Partner risk kontrolü yok | `BlockOnLimitExceed` kullanılıyor | ✅ KAPALI |

Kullanıcıya bu tabloyu göster. **Onay almadan fix etme.**

### Adım 4 — TODO.md güncelle
Kapanmış maddeleri `[ ]` → `[x] ✅ KAPALI <tarih> — <kod kanıtı>` yap. Commit hash ekle.

### Adım 5 — Sadece açık olanları fix et

## Tetikleyiciler

Bu kural otomatik uygulanır:
- Kullanıcı "TODO'daki HIGH'ları kapat" / "borçları temizle" diyor
- Kullanıcı dışarıdan analiz/review paylaşıyor
- `docs/journal/*` veya `TODO.md` 3+ "HIGH" / "CRITICAL" işareti var
- Son 7 günde `fix(security)` veya `hardening` commit'i geçtiyse

## Anti-pattern

1. **"TODO'da yazıyor, demek açık" varsayımı** — yazılı her şey kanıt değil
2. **Sweep yapmadan fix'e başlamak** — gereksiz git diff şişirir
3. **Stale TODO'yu güncellemeden kapatmak** — sonraki oturum aynı stale'i okur, döngü
4. **Kullanıcı analizini doğrudan uygulamak** — dış analiz de hipotez, önce doğrula

## İlişkili

- `.claude/rules/before-major-change.md` — silme/rename öncesi grep
- `.claude/rules/plan-first.md` — Tier 3 işlerde plan
- `.claude/rules/session-protocol.md` — oturum başı/sonu ritüel
