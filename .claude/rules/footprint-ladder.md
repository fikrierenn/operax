# Footprint Ladder — Yeni Yetenek En Dar Basamakta

_pusula'dan uyarlandı (Hermes "narrow waist"). `paths:` yok — compact sonrası da geçerli._

## Temel İlke

**Çekirdek dar bel; yetenek kenarda.** Her yeni kalıcı yapı (rule/skill/agent/SP/şema/Razor sayfası) bakım yükü + bağlam maliyeti getirir. Bir ihtiyaç çıktığında merdivenin EN ALT (en dar) basamağında çöz; üst basamağa ancak alt basamak yetmezse çık.

## Merdiven (alttan üste — alt = dar/ucuz)

| # | Basamak | Ne zaman | Maliyet |
|---|---|---|---|
| 1 | **Mevcut SP/rule/sorgu/servisi genişlet** | Var olana 1 fonksiyon/satır/kolon eklemek çözüyorsa | ~0 yeni yüzey |
| 2 | **Yeni skill** | Tekrarlanan iş akışı; tetik-bazlı yüklenir (her zaman bağlamda değil) | Düşük — sadece tetiklenince |
| 3 | **Yeni rule** | Kalıcı davranış kuralı (her oturum geçerli) | Orta — core ise her session bağlamda |
| 4 | **Yeni agent** | Özelleşmiş, salt-okuma/denetim alt-ajan | Orta — tanım + model seçimi |
| 5 | **Yeni SP/şema/migration** | Kalıcı DB nesnesi (tablo/SP/TVF) | Orta — migrate + backfill + sql-sp-reviewer |
| 6 | **Yeni Razor feature/sayfa (SON ÇARE)** | Kullanıcı-görünür yeni yüzey; başka basamak çözemiyor | Yüksek — UI+SQL+test+perf+nav + Tier 3 plan |

## Kurallar

1. **Aşağıdan yukarı sor:** "Bunu mevcut X'i genişleterek çözebilir miyim?" → hayırsa bir üst basamak.
2. **Atlama yapma:** 6. basamağa (yeni sayfa) gitmeden önce 1-5 elendi mi?
3. **Şüphede aşağıda kal.** Dar çözüm yetmezse büyütmek kolay; geniş çözümü küçültmek zor.
4. **Büyük basamak = Tier 3 plan** (`plan-first.md`): 5-6. basamak schema/UI içerir → plan zorunlu, faz kapanış kapısı (`phase-review-gate.md`).

## Anti-pattern

- ❌ "Yeni özellik = yeni sayfa" refleksi → önce mevcut sayfaya bölüm/sekme/sorgu eklenebilir mi?
- ❌ Tek-kullanımlık iş için yeni skill/agent → mevcut akışta inline çöz.
- ❌ "İleride lazım olur" diye geniş soyutlama (bkz. `coding-discipline.md` simplicity-first, Plan 34 UDF dersi: Volume/Weight için hardcode kolon yerine UDF).
- ❌ **Skill/agent yaratmadan önce mevcut listeyi kontrol ETMEMEK** → aynı isim/işlev dup. Yeni skill/agent ÖNCESİ available-skills listesine (proje `.claude/skills/` + global + plugin) ve `.claude/agents/`'a bak; aynısı varsa GENİŞLET, yaratma.
- ❌ Dış repodan/başka projeden "esin" diye Operax'ta zaten olanı tekrar kurmak → önce mevcut rule/skill ile kıyasla (örn. file-size-discipline zaten `csharp-conventions.md`'de var).

## İlişkili
- `.claude/rules/coding-discipline.md` — simplicity-first (aynı damar).
- `.claude/rules/plan-first.md` — Tier sistemi (büyük basamak = Tier 3 plan).
- `.claude/rules/agent-usage.md` — yeni agent basamağı (model/rol seçimi).
- `.claude/skills/yetenek-uret/SKILL.md` — basamağı seçip convention-uyumlu üreten skill.
