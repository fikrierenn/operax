# Cross-Proje Semantik Katman Köprüsü

_BKM/Pusula verisiyle etkileşim bu oturumda da olabilir (CFO analitik soruları). Kural: bilgi sahibine yazılır._

## Okuma
- BKM Kitap veri sorusu (ciro, depo, e-ticaret, POS) → **önce `D:\Dev\pusula\sema\` oku** (bridges/codes/entities/metrics/queries.yaml). Join/filtre/kod oradan alınır, ezbere yazılmaz.
- MCP `sqlserver`/`portalhub`/`zirve` global — bu oturumdan da sorgulanır.
- T-SQL kuralları: `D:\Dev\pusula\.claude\rules\sql-server-conventions.md` (DMY tarih, compat 110, IsValid...).

## Yazma (KRİTİK)
- Oturum sırasında **yeni BKM şema gerçeği** öğrenilirse (köprü/enum/grain/metrik/golden SQL) → **`D:\Dev\pusula\sema\*.yaml`'a yazılır** (sema-ogren formatı: atomic + confidence + evidence). Operax dosyalarına YAZILMAZ.
- Tersi de geçerli: Operax şema gerçeği Operax'ta kalır, pusula'ya taşınmaz.
- İlke: **keşif nerede yapılırsa yapılsın, canonical sahibinin reposuna işlenir.** Konuşma hafızasında bırakılmaz.

## İlişkili
- `D:\Dev\pusula\sema\README.md` — format + dosya yönlendirme tablosu.
- `D:\Dev\pusula\.claude\skills\sema-ogren\SKILL.md` — kayıt formatı.
