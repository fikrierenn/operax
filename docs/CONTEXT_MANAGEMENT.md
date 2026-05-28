# Operax — Bağlam Yönetimi Anayasası
*26 Mayıs 2026 — Yüksek Disiplinli Geliştirme Yaklaşımı*

## 1. Problem Tanımı ve LLM Bağlam Aşınması

Solo-geliştirici ve yapay zeka destekli (AI-assisted) uzun soluklu projelerde, zamanla LLM bağlam penceresinin (context window) şişmesi ve önceki kararların unutulması en büyük tıkanma noktasıdır. Tespit edilen bağlam aşınması belirtileri:
- **"Dün Konuşulan Karar Hatırlanmıyor":** Kararların ve tercihlerin dosyalara işlenmek yerine konuşma geçmişinde kalması (`/compact` sonrasında bilginin uçması).
- **"Biriken Commit Backlog'u":** Paralel ilerleyen birden fazla özellik yüzünden onlarca dosyanın commit'lenmeden birikmesi ve takip zorluğu.
- **"CLAUDE.md'nin Şişmesi ve Çöküşü":** Günlük oturum detaylarının ve geçici notların `CLAUDE.md`'ye yazılarak dosyanın okunamaz hale gelmesi.
- **"Durum ve Plan Çelişkileri":** Sprint planı (`PLAN.md`) ile TODO listelerinin (`TODO.md`) eşzamanlı güncellenmemesi yüzünden projenin mevcut olgunluğunun kaybolması.

---

## 2. Tasarım İlkeleri (Anayasal Kurallar)

Aşağıdaki 7 kural **tavizsiz** ve **koşulsuz** olarak her oturumda uygulanır:

### İlke 1: Üç Katman Ayrımı
Her bilgi **tam olarak tek bir dosyada** yaşar:
- **Kimlik (Identity):** `CLAUDE.md` (Kök Dizin) -> Proje tech stack'i, modülleri ve kurallar fihristi. (Max 200 satır).
- **Kurallar (Rules):** `.claude/rules/*.md` -> Konuya göre ayrılmış davranış, kodlama, UI ve git kuralları.
- **Süreç (Process):** `PLAN.md`, `docs/TODO.md` ve `docs/journal/YYYY-MM-DD.md`.

### İlke 2: 200 Satır Eşiği
- `CLAUDE.md` ve `.claude/rules/*.md` altındaki tüm kural dosyaları **her zaman 200 satır altında** kalmalıdır (hedef: 100-150 satır). Aşarsa konu alt başlıklara bölünür.

### İlke 3: Günlük Kayıtları (Session Journal) CLAUDE.md'de Yaşamaz
- Oturumda yapılan işler, yarım kalanlar ve commit özetleri asla `CLAUDE.md`'ye yazılmaz. 
- Bu geçici ve tarihli veriler için sadece `docs/journal/YYYY-MM-DD.md` dosyası kullanılır. `CLAUDE.md` her zaman güncel, temiz ve statik kalır.

### İlke 4: 15 Dosya Eşiği (Stop-the-World)
- Çalışma alanında uncommitted dosya sayısı **15** veya daha fazlaysa, yeni bir özelliğe başlanamaz veya yeni bir görev alınamaz. Önce planlı commit-split yapılır.

### İlke 5: 3 Paralel Özellik Sınırı
- Aynı anda en fazla 3 paralel özellik dalı (in-flight feature/branch) açık kalabilir. Biri tamamlanıp commit edilmeden yeni bir işe başlanamaz.

### İlke 6: Spec ──> Plan ──> Execute Zinciri
- 3'ten fazla dosyayı etkileyecek her türlü büyük mimari değişiklik, şema veya refactoring öncesinde:
  1. İşin kapsamı ve sınırları (Spec) netleştirilir.
  2. `docs/TODO.md` veya `PLAN.md` üzerinde adımlar (Plan) yazılır.
  3. Kodlama ve dosya düzenleme adımlarına (Execute) ancak plan onaylandıktan sonra geçilir.

### İlke 7: Karar Kalıcılığı
- "Şöyle yapalım", "şu kurala uyalım" şeklinde konuşma esnasında mutabık kalınan her geliştirici tercihi anında ilgili `.claude/rules/*.md` dosyasına işlenir. Konuşma hafızasına bırakılan her bilgi `/compact` veya yeni session sonrasında kaybolur.

---

## 3. Hedef Dosya Mimarisi

```
D:/Dev/Operax/
├── CLAUDE.md                            # ~100 satır. Proje kimliği + kurallar fihristi
├── PLAN.md                              # Master sprint planı ve durumları
├── RULES.md                             # Geliştirici kuralları ve şema standartları (legacy/statik)
├── .claude/
│   ├── rules/
│   │   ├── session-protocol.md          # Oturum başı/sonu ritüelleri (no paths)
│   │   ├── session-memory.md            # 3 katmanlı bilgi koruma disiplini (no paths)
│   │   ├── turkish-ui.md                # Türkçe UI standartları (no paths)
│   │   ├── commit-discipline.md         # Branching ve 15-dosya limiti (no paths)
│   │   ├── architecture.md              # Dapper, SP ve single-tenant mimari kuralları (no paths)
│   │   ├── coding-discipline.md         # Türkçe yorum standardı, 80-satır limiti, NCalc (no paths)
│   │   ├── sql-conventions.md           # T-SQL, SP ve migration kuralları (paths: **/*.sql)
│   │   └── before-major-change.md       # Büyük değişiklik öncesi checklist (no paths)
│   ├── settings.json                    # Hook kayıtları ve izin tanımları
│   └── settings.local.json              # Makineye özel yerel ayarlar (.gitignore'd)
├── docs/
│   ├── CONTEXT_MANAGEMENT.md            # BU DOSYA (Bağlam Yönetimi Anayasası)
│   ├── TODO.md                          # Ekran ve modül bazlı TODO listesi
│   ├── BUGS.md                          # Bug ve hata takip tablosu
│   ├── ARCHITECTURE.md                  # Sistem mimarisi ve genel akışlar
│   ├── SPRINT_0.md                      # Sprint 0 detayları ve geçmişi
│   ├── SPRINTS.md                       # Sprint planı detayları
│   ├── TESTING.md                       # Test senaryoları ve doğrulama checklist'i
│   └── journal/
│       └── YYYY-MM-DD.md                # Günlük oturum handoff kayıtları
```

---

## 4. Oturum Ritüelleri ve Handoff Disiplini

### Oturum Başlarken:
1. Son commit geçmişi ve `git status` durumu incelenir.
2. `docs/journal/` altındaki son iki günlük okunur ve nerede kalındığı doğrulanır.
3. `docs/TODO.md`'deki en üst öncelikli görevler incelenir.

### Oturum Biterken:
1. Geliştirilen özellikler test edilir ve `dotnet build` ile derleme başarısı doğrulanır.
2. `PLAN.md` ve `docs/TODO.md` dosyalarındaki ilgili görevlerin durumları güncellenir.
3. `docs/journal/YYYY-MM-DD.md` dosyasına bu oturumda yapılanların, yarım kalanların ve sonraki adımların yazıldığı **handoff günlüğü** eklenir.
