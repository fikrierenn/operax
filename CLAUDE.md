# CLAUDE.md — Operax WMS Platform

Bu dosya her Claude/Antigravity oturumunun başında okunur. **Değişmez kurallar + kimlik.** Detaylı kurallar, geçmiş kararlar ve süreç notları ilgili alt dosyalarda yaşar:

- `.claude/rules/*.md` — davranış ve yazılım kuralları (yeni kod yazmadan önce mutlaka okunur)
- `docs/CONTEXT_MANAGEMENT.md` — bağlam yönetimi anayasası (ilkeler bütünü)
- `docs/journal/YYYY-MM-DD.md` — günlük oturum kayıtları (nerede kalındığı buraya işlenir)
- `PLAN.md` — master sprint planı ve durumları
- `docs/TODO.md` — aktif modül/ekran todo'ları (durum sembolleri: `[ ]` yapılacak · `[/]` devam ediyor · `[x]` tamamlandı)
- `docs/BUGS.md` — bug ve hata takip kayıtları
- `docs/ARCHITECTURE.md` — projenin detaylı mimari tasarım belgesi

---

## 0. OTURUM BAŞI RİTÜELİ — ZORUNLU
Her yeni oturum başlatıldığında sessizce ve elle şu 3 adım uygulanır:
1. `docs/journal/` altındaki **son 2 günlüğü** oku (yarım kalan işler ve düzeltme notları).
2. `PLAN.md` ve `docs/TODO.md` dosyasındaki aktif sprint/öncelik sırasını doğrula.
3. `git status` ile uncommitted dosya sayısını kontrol et (15 dosya eşiğini aştıysa önce commit-split yap).

---

## 1. PROJE KİMLİĞİ VE TECH STACK
**Operax** — yüksek performanslı, bağımsız dağıtılabilir (single-tenant per client) Depo Yönetim Sistemi (WMS) ve İşletme Yönetim Platformu.

- **Backend + UI:** .NET 10.0 (`net10.0`) + ASP.NET Core Razor Pages (Feature-Based klasör yapısı)
- **Veri Erişimi:** Dapper (raw SQL - raw performans önceliği). EF Core kesinlikle kullanılmaz.
- **Veritabanı:** SQL Server 2022
- **Arka Plan İşleri:** Hangfire (SQL Server storage)
- **Stil & Tasarım:** Vanilla CSS (maksimum esneklik, özel premium temalar)
- **Dinamik BOM Formül Değerlendirme:** Parametrik BOM için NCalc kütüphanesi (DataTable.Compute yasaktır)
- **Kimlik Yönetimi:** ASP.NET Core Identity + Dapper tabanlı özel Identity store (`DapperUserStore` ve `DapperRoleStore`)

---

## 2. TEMEL ÇALIŞMA PRENSİPLERİ
1. **Sistematik Çalış:** Konuşma geçmişine güvenme. Önemli kararlar ADR'ye, kurallar `.claude/rules/` altına, süreç `TODO.md` ve `journal`'a yazılır.
2. **Dil Kuralı:** Kod, veritabanı şeması ve C# identifier'lar (sınıf, değişken, metot) tamamen **İngilizce**. Kullanıcının gördüğü her şey (buton, placeholder, toast, label) ve C# yorum satırları tamamen **Türkçe** (UTF-8).
3. **Save-Point Commit:** Testler yeşile döndüğünde veya bağımsız bir aşama bittiğinde hemen save-point commit'i yap.
4. **200 Satır Eşiği:** Bu dosya ve `.claude/rules/*.md` dosyaları 200 satır altında kalmalıdır.

---

## 3. HIZLI REFERANS — SIK BAKILAN DOSYALAR

| Amaç / İşlev | Dosya Yolu |
|---|---|
| Dapper Bağlantı Yönetimi | [Db.cs](file:///d:/Dev/Operax/src/Operax.Web/Lib/Db.cs) |
| Identity Özel User Store | [DapperUserStore.cs](file:///d:/Dev/Operax/src/Operax.Web/Lib/DapperUserStore.cs) |
| Identity Özel Role Store | [DapperRoleStore.cs](file:///d:/Dev/Operax/src/Operax.Web/Lib/DapperRoleStore.cs) |
| Şirket / Tenant Yönetimi | [Auth.cs](file:///d:/Dev/Operax/src/Operax.Web/Lib/Auth.cs) |
| Ortak DTO'lar ve Sabitler | [Dtos.cs](file:///d:/Dev/Operax/src/Operax.Web/Lib/Dtos.cs) |
| Uygulama Pipeline Başlangıcı | [Program.cs](file:///d:/Dev/Operax/src/Operax.Web/Program.cs) |
| Hata Tipleri ve Formatı | [Errors.cs](file:///d:/Dev/Operax/src/Operax.Web/Lib/Errors.cs) |
| Guard Clause Yardımcısı | [Guard.cs](file:///d:/Dev/Operax/src/Operax.Web/Lib/Guard.cs) |
