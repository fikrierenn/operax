# Plan 13 — /api/switch-company Güvenlik Sıkılaştırma

**Tarih:** 2026-05-29 · **Güncelleme:** 2026-05-30 (§3 KARARSIZ kapandı → Model 3, K10) · **Durum:** `Onaylandı (2026-05-29) — §3 firma-yetki KARAR: Model 3` · **Modül:** M00 · **Kaynak:** AR-003 + K10 (🟠 YÜKSEK)

## 1. Problem
`Program.cs:97-116` `/api/switch-company`:
- `.DisableAntiforgery()` → CSRF açık (kötü niyetli sayfa kullanıcının firmasını değiştirebilir).
- Hedef firmaya kullanıcının **erişim yetkisi kontrol edilmiyor** — herhangi bir `companyId` doğrudan `company` claim'i olarak yazılıyor. Kullanıcı yetkisi olmayan firmaya geçebilir → **yetki atlama + veri sızıntısı** (AR-001 ile birleşince ağır).

## 2. Scope
### Dahili
- Antiforgery'yi geri ekle (sidebar formu zaten `@Html.AntiForgeryToken()` üretiyor — `DisableAntiforgery()` kaldır, token doğrula).
- Firma-erişim yetki kontrolü: kullanıcı yalnızca **kendisine atanmış firmalara** geçebilir. Kaynak: kullanıcının izinli firma listesi (claim seti / yeni `UserCompany` tablosu — AÇIK SORU bkz. §3).
### Dışı
- Firma-başına farklı rol **yönetim ekranı/UI** — yetenek omurgada var ama ekran/süreç sonra (bugün düz kullanım).

## 3. §3 — Firma-Bazlı Yetki — KARAR: MODEL 3 (K10, 2026-05-29) — KARARSIZ KAPANDI

**Karar:** İzinli firmalar `UserCompany(UserId, CompanyId, Role)` tablosunda; **Model 3** (kişi+firma+rol).
Önceki "Açık Tasarım Sorusu" (claim mi tablo mu) → **tablo (b)** seçildi.

- ~~Model 1~~ (tek firma global rol) / ~~Model 2~~ (çok firma tek global rol) — **reddedildi.**
  - Model 2 "A'da tam yetkili, B'de sadece bakış" yapamaz; ihtiyaç kaçınılmaz → yetki sistemi baştan yazılır.
  - Model 3 maliyeti Model 2'den **tek kolon** fazla (Role); sonradan eklemek pahalı → omurga şimdi Model 3.
- **Gerçek durum:** çoğu kullanıcı tek firma, dar rol (saha/depo). İstisnai gruplar (muhasebe, satınalma,
  pazarlama, IT, üst yönetim) çok firmaya bakar — her firmada belirli rolde (muhasebeci her yerde muhasebe rolü).
  Rol kişiye değil **kişi+firma** çiftine ait → Model 3.

### 3.1 Kurulum
- **`UserCompany(UserId, CompanyId, Role)` köprü tablosu:** eriştiği firmalar + her firmadaki rolü. Çoğu kullanıcı tek satır.
- **switch-company erişim kontrolü:** SADECE UserCompany'deki firmalara geçiş; yetkisiz firma → **403** (claim yazılmaz). IDOR kapanır.
- **switch-company rol-aware (⚠️ ZORUNLU):** geçişte company claim'i + **rol claim'i** aktif firmanın `UserCompany.Role`'una göre **yeniden set**. Aksi halde A'nın rolüyle B'de dolaşır = izolasyon açığının yetki versiyonu (sessiz). Bu kısım opsiyonel değil; eksikse plan yarım.
- **CurrentUser.Roles firma-bağlamlı:** global DEĞİL → aktif firma bağlamında çözülür.
- **UI:** tek-firma kullanıcıda switcher görünmez; sadece çok-firmalılar görür (+ liste UserCompany'ye göre filtreli, eski `SELECT FROM Company` değil).

### 3.2 Kullanım bugün düz
Omurga tam (firma-başına farklı rol yetenek), bugün herkes her firmada tek rol. Farklılaştırma hazır, kullanılmıyor; yönetim ekranı sonra.

### 3.3 Plan 12 ile ilişki (bağımlılık)
İzolasyon (plan 12) "claim neyse onu süz"; yetki (bu §3) "claim'i ancak hak ettiğin firmaya çevirebilirsin + o firmanın rolünü alırsın". İkisi birlikte switch-company açığını kapatır. **plan 12 güvenliği BU plana bağlı** — claim serbest değişirse izolasyon dekoratif.

## 4. Alternatifler
- A: Sadece antiforgery ekle, yetki kontrolü yok — Reddedildi: asıl açık (yetki atlama) kalır.
- B: Çoklu claim ile izin — Reddedildi: firma-bazlı rol vermez (Model 2 sınırı).
- C (seçilen): UserCompany tablosu (Model 3) — tam çözüm; tek kolon fazla, sonradan eklemek pahalı.

**5 lens:** 🔴 Tek-firmalı kullanıcıda switcher zaten görünmüyor (userCompanies.Count>1) ama endpoint hâlâ açık → doğrudan POST riski. 🔵 Gerçek ihtiyaç: "yalnız yetkili firmaya geç". 🟢 UserCompany → firma-bazlı RBAC açar. ⚪ "switcher tüm firmaları listeliyor" (Layout: `SELECT ... FROM Company`) → o da yetkiye göre filtrelenmeli. 🟡 antiforgery=5dk; yetki=karara bağlı.

## 5. Done
- [ ] DisableAntiforgery kaldırıldı, token doğrulanıyor
- [ ] `UserCompany(UserId, CompanyId, Role)` tablosu (şema + migration: mevcut claim'leri taşı)
- [ ] Yetkisiz companyId → 403/redirect (claim yazılmaz) — UserCompany kontrolü
- [ ] switch-company company + **rol** claim aktif firmaya göre yeniden set (rol-aware)
- [ ] CurrentUser.Roles firma-bağlamlı çözüm
- [ ] Sidebar firma listesi UserCompany'ye göre filtreli (eski SELECT FROM Company değil)
- [ ] Audit log: firma değiştirme (kim, hangi firma)

## 6. Onay
- [x] §3 kararı alındı: **Model 3 (K10, 2026-05-29)** · [ ] Gösterildi · [ ] Onay: <tarih>

> İlişkili: AR-003, AR-001, KARAR K10, Plan 12 (izolasyon — güvenliği BU plana bağlı), security-principles.md §8
