# Oturum Protokolü Kuralları

Bu dosya, her Claude/Antigravity oturumunun başı, ortası ve sonu ritüellerini tanımlar. Proje takibinde hiçbir detayın unutulmaması için bu kurallara tavizsiz uyulur.

---

## 1. Oturum Başlangıcı (İlk yanıttan önce ZORUNLU)

Her oturum başlatıldığında veya yeni bir Agent yüklendiğinde şu adımlar **sessizce ve sırayla** gerçekleştirilir:

1. **Son Günlüklerin Okunması:** `docs/journal/` altındaki en son 2 oturum günlüğü (`YYYY-MM-DD.md`) `view_file` ile okunur.
   - Dün neyi tamamladık?
   - Hangi işler yarım kaldı ve bugün nereden başlanacak?
   - Dün yapılan hatalar ve çıkarılan "düzeltme notları" nelerdir?
2. **TODO ve Plan Kontrolü:** `PLAN.md` (Sprintler) ve `docs/TODO.md` dosyalarındaki aktif öncelikler incelenir.
3. **Commit Durumu Denetimi:** `git status` çalıştırılarak uncommitted dosya sayısı öğrenilir. Eğer 15 dosya limiti aşıldıysa, yeni bir işe başlanmadan önce `commit-discipline.md` kurallarına göre commit-split planlanır.

---

## 2. Oturum Ortası Disiplini

1. **3 Paralel Özellik Sınırı:** Aynı anda en fazla 3 paralel özellik dalı/görevi açık kalabilir. Geliştirme odağının kaybolmaması için biri tamamlanmadan yeni bir başlığa geçilmez.
2. **Kural Değişikliklerinin Kalıcı Hale Getirilmesi:** Geliştirme sırasında alınan yeni kararlar veya kurallar konuşma geçmişinde bırakılamaz. Anında `.claude/rules/` altındaki ilgili kural dosyasına veya `RULES.md`'ye işlenir.
3. **Büyük İşlerde Spec-Plan-Execute Zinciri:** 3'ten fazla dosyayı etkileyecek büyük değişiklikler veya yeni özelliklerde:
   - Önce işin kapsamı ve sınırları (Spec) netleştirilir.
   - `docs/TODO.md` veya `PLAN.md` üzerinde adımlar (Plan) yazılır.
   - Ardından koda el atılır (Execute).

---

## 3. Oturum Sonu (Handoff Protokolü)

Kullanıcı oturumu kapatırken ("kapatabiliriz", "iyi geceler", "/handoff", "devam edeceğiz" vb.) şu adımlar uygulanır:

1. **Günlük Kaydının Yazılması:** `docs/journal/YYYY-MM-DD.md` dosyası oluşturulur (yoksa) veya güncellenir.
   - **Ana Konu:** Bu oturumda odaklanılan ana başlık.
   - **Tamamlananlar:** Dosya ve satır referanslı biten görevler.
   - **Build / Derleme Durumu:** `dotnet build` başarılı/başarısız durumu.
   - **Commit ve Git Durumu:** Yapılan commit'ler ve uncommitted kalan dosya listesi.
   - **Yarım Kalan İşler:** Nerede kalındığı.
   - **Yarına Başlangıç Noktası:** Sonraki oturum için 1-3 somut başlangıç adımı.
2. **Süreç Belgesi Güncelleme:** `PLAN.md` ve `docs/TODO.md` dosyalarındaki ilgili görevlerin durumları (`[/]` veya `[x]`) ve tamamlanma tarihleri güncellenir.
3. **CLAUDE.md Korunumu:** `CLAUDE.md` dosyasına oturum detayları veya tarihli günlük notları yazılmaz. `CLAUDE.md` sadece statik kimlik ve kurallar fihristidir.
